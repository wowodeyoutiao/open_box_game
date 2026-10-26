using System.Collections.Generic;
using JEngine.Core;
using JEngine.Helper;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 玩家领域服务：登录、角色基础数据、副本入口、日常地图。
    ///
    /// 【职责边界】
    /// - 只负责「算什么」，不负责「怎么发」
    /// - 返回响应/推送对象，由 Adapter 层（LocalServer / LocalHttpRouter）投递
    /// - 数据从 LocalData（模拟服务端 DB）读写
    /// </summary>
    public class PlayerService : SingletonTemplate<PlayerService>
    {
        // ==================== HTTP：登录 ====================

        /// <summary>
        /// 构造 HTTP 登录响应（不走 LitJson，避免 struct + 嵌套 List 解析失败）
        /// </summary>
        public HotUpdateScripts.ProtoM.HTTP_LoginResponse BuildLoginResponse()
        {
            // 防御：即使启动 UI 没有调用 Bootstrap，也必须先加载存档再返回角色列表。
            if (LocalRuntime.IsLocalMode)
                LocalBootstrap.Init();

            LocalData d = LocalData.Instance;
            d.EnsureInit();

            // 角色列表以 LocalData.Roles 为唯一来源（模拟服务端 character 表）
            var list = new List<HotUpdateScripts.ProtoM.HTTP_CharacterInfo>();
            for (int i = 0; i < d.Roles.Count; i++)
                list.Add(ToCharacterInfo(d.Roles[i]));

            Log.Print($"[PlayerService] 登录 角色数={list.Count} 当前={d.Nickname} job={d.Job}");

            return new HotUpdateScripts.ProtoM.HTTP_LoginResponse
            {
                code = 0,
                characterList = list,
                config = new HotUpdateScripts.ProtoM.HTTP_ClientConfig
                {
                    version = "1.0.0",
                    heartbeat = 30,
                    forceUpdate = false,
                },
                messages = new List<string>(),
                token = "local_token",
                realname = true,
            };
        }

        /// <summary>LocalRole → 客户端 HTTP_CharacterInfo</summary>
        private static HotUpdateScripts.ProtoM.HTTP_CharacterInfo ToCharacterInfo(LocalRole r)
        {
            return new HotUpdateScripts.ProtoM.HTTP_CharacterInfo
            {
                name = r.Name,
                uid = r.Uid,
                headUrl = r.HeadUrl,
                coin = r.Coin,
                diamanta = r.Diamanta,
                soul = r.Soul,
                coupon = r.Coupon,
                energy = r.Energy,
                job = r.Job,
                sex = r.Sex,
                exp = r.Exp,
                level = r.Level,
                vip = r.Vip,
                recharge = 0,
                ip = "127.0.0.1",
                port = 7000,
                playerIp = "",
                debug = 0,
            };
        }

        /// <summary>
        /// 建角响应。必须返回完整的 HTTP_CharacterInfo，
        /// 否则客户端 LoginCharacterInfo.Insert(0, 默认角色) → job=0 → JobDesDic[(JobType)0] 崩溃。
        /// </summary>
        public object BuildCreateCharResponse(string postStr)
        {
            LocalData d = LocalData.Instance;
            d.EnsureInit();

            string name = "";
            int job = 1;
            int sex = 1;

            try
            {
                var req = JEngine.LitJson.JsonMapper.ToObject<
                    HotUpdateScripts.ProtoM.HTTP_CreateCharacterRequest>(postStr);
                if (req.name != null)
                    name = req.name.Trim();
                if (req.job > 0) job = req.job;
                if (req.sex > 0) sex = req.sex;
            }
            catch (System.Exception e)
            {
                string parseError = "建角请求解析失败: " + e.Message;
                LocalDiagnostics.RecordError("PlayerService", parseError);
                return new HotUpdateScripts.ProtoM.HTTP_CreateCharacterResponse
                {
                    code = -1,
                    date = parseError,
                    characterList = null,
                };
            }

            Log.Print($"[PlayerService] 建角 name={name} job={job} sex={sex}");

            string error;
            LocalRole role = d.CreateRole(name, job, sex, out error);
            if (role == null)
            {
                LocalDiagnostics.RecordError("PlayerService", "建角失败: " + error);
                return new HotUpdateScripts.ProtoM.HTTP_CreateCharacterResponse
                {
                    code = -1,
                    date = error,
                    characterList = null,
                };
            }

            RoleStatService.Instance.Recalculate(role, true);

            if (!SaveCoordinator.Instance.SaveNow())
            {
                // 建角必须落盘成功才算完成；否则回滚内存角色，避免玩家进入游戏后下次启动丢失。
                d.DeleteRoleByUid(role.Uid);
                string saveError = "本地存档创建失败，请检查存储空间";
                LocalDiagnostics.RecordError("PlayerService", saveError);
                return new HotUpdateScripts.ProtoM.HTTP_CreateCharacterResponse
                {
                    code = -1,
                    date = saveError,
                    characterList = null,
                };
            }

            return new HotUpdateScripts.ProtoM.HTTP_CreateCharacterResponse
            {
                code = 0,
                date = "",
                characterList = ToCharacterInfo(role),
            };
        }

        /// <summary>
        /// 选角。请求体带 uid，按 uid 从 Roles 里选中并把数据展开到镜像字段。
        /// 原实现走 JSON 路由，拿不到 uid，导致 LocalData 无法知道玩家选了谁。
        /// </summary>
        public object BuildSelectCharResponse(string postStr)
        {
            LocalData d = LocalData.Instance;
            d.EnsureInit();

            long uid = 0;
            try
            {
                var req = JEngine.LitJson.JsonMapper.ToObject<
                    HotUpdateScripts.ProtoM.HTTP_SelectCharacterRequest>(postStr);
                uid = req.uid;
            }
            catch (System.Exception e)
            {
                Log.PrintWarning($"[PlayerService] 选角请求解析失败: {e.Message}");
            }

            bool ok = uid > 0 && d.SelectRoleByUid(uid);
            if (!ok)
            {
                string message = "选角失败，角色不存在 uid=" + uid;
                LocalDiagnostics.RecordError("PlayerService", message);
                return new HotUpdateScripts.ProtoM.HTTP_SelectCharacterResponse
                {
                    code = -1,
                    token = "",
                };
            }

            RoleStatService.Instance.RecalculateCurrent();
            SaveCoordinator.Instance.SaveNow();
            Log.Print($"[PlayerService] 选角 uid={d.Uid} name={d.Nickname} job={d.Job} sex={d.Sex}");

            return new HotUpdateScripts.ProtoM.HTTP_SelectCharacterResponse
            {
                code = 0,
                token = "local_token",
            };
        }

        /// <summary>删除角色：本地权威数据删除后立即落盘。</summary>
        public object BuildDeleteCharResponse(string postStr)
        {
            long uid = 0;
            try
            {
                var req = JEngine.LitJson.JsonMapper.ToObject<
                    HotUpdateScripts.ProtoM.HTTP_LoginrateRequest>(postStr);
                uid = req.uid;
            }
            catch (System.Exception e)
            {
                LocalDiagnostics.RecordError("PlayerService", "删角请求解析失败: " + e.Message);
            }

            if (uid <= 0 || !LocalData.Instance.DeleteRoleByUid(uid))
            {
                LocalDiagnostics.RecordError("PlayerService", "删角失败 uid=" + uid);
                return new HotUpdateScripts.ProtoM.HTTP_LoginrateResponse { code = -1 };
            }

            SaveCoordinator.Instance.SaveNow();
            return new HotUpdateScripts.ProtoM.HTTP_LoginrateResponse { code = 0 };
        }

        // ==================== TCP：登录 ====================

        /// <summary>
        /// TCP 登录响应。GameStatus=0 触发客户端切场景进大厅。
        /// </summary>
        public object BuildLoginTcpResponse()
        {
            LocalData d = LocalData.Instance;
            d.EnsureInit();

            if (d.Current == null)
            {
                string message = "TCP 登录时没有选中角色";
                LocalDiagnostics.RecordError("PlayerService", message);
                return new LoginToGameServerResponse
                {
                    Code = -1,
                    Error = message,
                    GameStatus = 1,
                };
            }

            LocalBootstrap.EnsureAutoSaveTimer();
            SaveCoordinator.Instance.MarkDirty();
            Log.Print($"[PlayerService] C2S_Login uid={d.Uid} name={d.Nickname}");

            return new LoginToGameServerResponse
            {
                Code = 0,
                Error = "",
                Name = d.Nickname,
                Uid = d.Uid,
                HeadUrl = d.HeadUrl,
                Timestamp = GameUtil.Instance.GetCurrentTimeSeconds(),
                GameStatus = 0,
            };
        }

        // ==================== TCP：副本 / 日常 ====================

        public object GetInstanceInfo()
        {
            // ⚠️ 六个子结构都必须给空对象。
            // 返回裸 new InstanceInfoResponse() 时这 6 个字段全是 null，
            // 客户端 Sever_InstanceInfoResponse 一访问就 NRE（已实际踩到）。
            // ⚠️ Battle/Boss/TrialTower/Relic/AFK/ToEnd 是 InstanceInfoResponse 的
            //    **嵌套类型**（8 空格缩进的 partial class），必须写全限定名。
            return new InstanceInfoResponse
            {
                battle_map = new InstanceInfoResponse.Battle(),
                boss_map = new InstanceInfoResponse.Boss(),
                trial_tower_map = new InstanceInfoResponse.TrialTower(),
                relic_map = new InstanceInfoResponse.Relic(),
                afk = new InstanceInfoResponse.AFK(),
                to_end = new InstanceInfoResponse.ToEnd(),
            };
        }

        public object GetDailyMapInfo()
        {
            return new DailyMapInfoResponse { Code = 0, Error = "" };
        }

        /// <summary>PVP 信息：多人内容，阶段3 会删，先给空响应保证界面不崩</summary>
        public object GetPvpInfo()
        {
            return new PVPInfoResponse { Code = 0, Error = "" };
        }
    }
}
