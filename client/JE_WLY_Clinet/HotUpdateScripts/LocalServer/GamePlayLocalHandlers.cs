using System.Collections.Generic;
using JEngine.MyNet;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 尚未实现的单人动作协议。
    ///
    /// 这些协议会真实修改等级、背包、伙伴、宠物或战斗进度，不能再用 Code=0
    /// 伪装成功。当前统一返回明确失败，并从诊断台账中单独列出，后续接一个
    /// Service 就从这里移除一个。
    /// </summary>
    public static class GamePlayLocalHandlers
    {
        public static void Register(Dictionary<short, LocalServer.Handler> dict)
        {
            // 角色成长
            LocalServer.Reg(dict, CmdId.C2S_UpgradeLevel, OnUpgradeLevel);
            LocalServer.Reg(dict, CmdId.C2S_SkillUpgrade, OnSkillUpgrade);
            LocalServer.Reg(dict, CmdId.C2S_SkillAdvanced, OnSkillAdvanced);
            LocalServer.Reg(dict, CmdId.C2S_Enhance, OnEnhance);
            LocalServer.Reg(dict, CmdId.C2S_Stars, OnStars);
            LocalServer.Reg(dict, CmdId.C2S_GemstoneChange, OnGemstoneChange);
            LocalServer.Reg(dict, CmdId.C2S_Compose, OnCompose);
            LocalServer.Reg(dict, CmdId.C2S_SkillChange, OnSkillChange);

            // 魂石一键镶嵌（对齐原服 genstoneChangeAll：按等级挑最高候选逐槽轮换）
            LocalServer.Reg(dict, CmdId.C2S_GemstoneChangeAll, OnGemstoneChangeAll);

            // 伙伴
            LocalServer.Reg(dict, CmdId.C2S_PartnerCompose, OnPartnerCompose);
            LocalServer.Reg(dict, CmdId.C2S_PartnerDisassemble, OnPartnerDisassemble);
            LocalServer.Reg(dict, CmdId.C2S_PartnerReforge, OnPartnerReforge);

            // 宠物
            LocalServer.Reg(dict, CmdId.C2S_PetLevel, OnPetLevel);
            LocalServer.Reg(dict, CmdId.C2S_PetStar, OnPetStar);
            LocalServer.Reg(dict, CmdId.C2S_PetMove, OnPetMove);

            // C2S_BattleReward 已由 BasicLocalHandlers 接入 BattleService
        }

        /// <summary>
        /// 升级：对齐原服 Manager.UpgradeLevel。
        ///   PLAYER  → Player.levelUp（经验够就升，表现走 LevelUpPush）
        ///   PARTNER → 校验伙伴等级 &lt; 主角等级，然后 Partner.levelUp（消耗主角经验）
        /// 经验不足时原服静默返回 Code=0（客户端自己用 GameUtil.ComCoinToCheck 预判）。
        /// </summary>
        private static object OnUpgradeLevel(byte[] req)
        {
            var response = new CommonResponse
            {
                Code = 0,
                RequestCmdId = (uint)CmdId.C2S_UpgradeLevel,
            };

            UpgradeLevelRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<UpgradeLevelRequest>(req);
            }
            catch (System.Exception e)
            {
                response.Code = -1;
                response.Error = "升级请求解析失败: " + e.Message;
                return response;
            }

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Code = -1;
                response.Error = "没有角色";
                return response;
            }

            if (request != null && request.Type == ObjectType.PARTNER)
                return UpgradePartner(response, role, request.Uid);

            int gained = LevelUpService.Instance.ProcessLevelUp(role);
            if (gained > 0)
            {
                RoleStatService.Instance.Recalculate(role, true);
                LocalData.Instance.Level = role.Level;
                LocalData.Instance.Exp = role.Exp;
                SaveCoordinator.Instance.SaveNow();
                LocalPusher.Instance.PushUserBaseNow();
                LocalPusher.Instance.PushStatsNow();
                LocalPusher.Instance.PushLevelUpNow(role.Level);
            }
            return response;
        }

        /// <summary>伙伴升级分支（原服 Manager.UpgradeLevel 的 else 分支）。</summary>
        private static object UpgradePartner(CommonResponse response, LocalRole role, long partnerUid)
        {
            PartnerInfo partner = PartnerService.Instance.FindPartnerByUid(role, partnerUid);
            if (partner == null)
            {
                response.Code = -2;
                response.Error = "伙伴不存在";
                return response;
            }
            // 原服：partner.level >= p.level 直接报"已达最大等级"
            if (partner.Level >= role.Level)
            {
                response.Code = -3;
                response.Error = "伙伴等级不能超过主角等级";
                return response;
            }

            int oldLevel = partner.Level;
            if (!LevelUpService.Instance.LevelUpPartner(role, partner))
            {
                // 经验不足 / 已达上限：原服同样返回 Code=0，不报错
                response.Code = 0;
                return response;
            }

            // 伙伴等级变了，上阵属性要重算
            RoleStatService.Instance.Recalculate(role, true);
            LocalData.Instance.Level = role.Level;
            LocalData.Instance.Exp = role.Exp;
            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushStatsNow();
            LocalPusher.Instance.PushPartnerSlotNow(
                PartnerService.Instance.GetSlotInfo() as PartnerSlotInfoResponse);
            LocalPusher.Instance.PushLevelUpNow(partner.Level, partner.Uid);

            // 原服 Partner.levelUp：dispatch(UPGRADE_ANY_PARTNER_LEVEL, level, 1)
            TaskService.Instance.Dispatch(role, TargetType.UPGRADE_ANY_PARTNER_LEVEL,
                partner.Level, 1);
            JEngine.Core.Log.Print("[LevelUp] partner " + partner.CfgId + " " + oldLevel
                + " -> " + partner.Level);
            return response;
        }

        /// <summary>装备强化，对齐原服 Manager.Enhance。</summary>
        private static object OnEnhance(byte[] req)
        {
            EnhanceRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<EnhanceRequest>(req);
            }
            catch (System.Exception e)
            {
                return new EnhanceResponse
                {
                    Code = -1,
                    Error = "强化请求解析失败: " + e.Message,
                };
            }
            return EquipService.Instance.Enhance(request);
        }

        /// <summary>装备升星，对齐原服 Manager.Stars。</summary>
        private static object OnStars(byte[] req)
        {
            StarsRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<StarsRequest>(req);
            }
            catch (System.Exception e)
            {
                return new StarsResponse
                {
                    Code = -1,
                    Error = "升星请求解析失败: " + e.Message,
                };
            }
            return EquipService.Instance.Stars(request);
        }

        /// <summary>伙伴合成：对齐原服 Manager.PartnerCompose（碎片 → 整卡）。</summary>
        private static object OnPartnerCompose(byte[] req)
        {
            PartnerComposeRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<PartnerComposeRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CommonResponse
                {
                    Code = -1,
                    Error = "伙伴合成请求解析失败: " + e.Message,
                    RequestCmdId = (uint)CmdId.C2S_PartnerCompose,
                };
            }
            return PartnerService.Instance.Compose(request);
        }

        /// <summary>伙伴分解：对齐原服 Manager.PartnerDisassemble（重置回 1 级 0 星并退料）。</summary>
        private static object OnPartnerDisassemble(byte[] req)
        {
            PartnerDisassembleRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<PartnerDisassembleRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CommonResponse
                {
                    Code = -1,
                    Error = "伙伴分解请求解析失败: " + e.Message,
                    RequestCmdId = (uint)CmdId.C2S_PartnerDisassemble,
                };
            }
            return PartnerService.Instance.Disassemble(request);
        }

        /// <summary>伙伴洗练：对齐原服 Manager.PartnerReforge（免费退 70%，付费退全额）。</summary>
        private static object OnPartnerReforge(byte[] req)
        {
            PartnerReforgeRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<PartnerReforgeRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CommonResponse
                {
                    Code = -1,
                    Error = "伙伴洗练请求解析失败: " + e.Message,
                    RequestCmdId = (uint)CmdId.C2S_PartnerReforge,
                };
            }
            return PartnerService.Instance.Reforge(request);
        }

        /// <summary>魂石镶嵌 / 拆卸，对齐原服 Manager.GemstoneChange。</summary>
        private static object OnGemstoneChange(byte[] req)
        {
            GemstoneChangeRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<GemstoneChangeRequest>(req);
            }
            catch (System.Exception e)
            {
                return new GemstoneChangeResponse
                {
                    Code = -1,
                    Error = "魂石请求解析失败: " + e.Message,
                };
            }
            return GemService.Instance.Change(request);
        }

        /// <summary>宠物升级（消耗宠物经验道具），对齐原服 Manager.PetLevel。</summary>
        private static object OnPetLevel(byte[] req)
        {
            PetLevelRequest request = null;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<PetLevelRequest>(req);
            }
            catch (System.Exception e)
            {
                JEngine.Core.Log.PrintWarning("[LocalHandlers] 宠物升级请求解析失败: " + e.Message
                    + "，按 0 号槽位处理");
            }
            return PetService.Instance.LevelUp(request == null ? 0 : request.SlotId);
        }

        /// <summary>宠物升星（进阶），对齐原服 Manager.PetStar。</summary>
        private static object OnPetStar(byte[] req)
        {
            PetStarRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<PetStarRequest>(req);
            }
            catch (System.Exception e)
            {
                return new PetStarResponse
                {
                    Code = -1,
                    Error = "宠物升星请求解析失败: " + e.Message,
                };
            }
            return PetService.Instance.StarUp(request.SlotId, request.CfgId, request.Num);
        }

        /// <summary>宠物换位 / 喂经验 / 合成，对齐原服 Manager.PetMove。</summary>
        private static object OnPetMove(byte[] req)
        {
            PetMoveRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<PetMoveRequest>(req);
            }
            catch (System.Exception e)
            {
                return new PetMoveResponse
                {
                    Code = -1,
                    Error = "宠物换位请求解析失败: " + e.Message,
                };
            }
            return PetService.Instance.Move(request.SlotId, request.TargetSlotId);
        }

        /// <summary>魂石一键镶嵌，对齐原服 Manager.GemstoneChangeAll。</summary>
        private static object OnGemstoneChangeAll(byte[] req)
        {
            GemstoneChangeAllRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<GemstoneChangeAllRequest>(req);
            }
            catch (System.Exception e)
            {
                return new GemstoneChangeAllResponse
                {
                    Code = -1,
                    Error = "一键镶嵌请求解析失败: " + e.Message,
                    GemStoneSlot = new int[0],
                };
            }
            return GemService.Instance.ChangeAll(request);
        }

        /// <summary>道具合成（材料 / 魂石 / 装备），对齐原服 Manager.Compose。</summary>
        private static object OnCompose(byte[] req)
        {
            ComposeRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<ComposeRequest>(req);
            }
            catch (System.Exception e)
            {
                return new ComposeResponse
                {
                    Code = -1,
                    Error = "合成请求解析失败: " + e.Message,
                };
            }
            return ComposeService.Instance.Compose(request);
        }

        private static object OnSkillUpgrade(byte[] req)
        {
            SkillUpgradeRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<SkillUpgradeRequest>(req);
            }
            catch (System.Exception e)
            {
                return new SkillUpgradeResponse
                {
                    Code = -1,
                    Error = "技能升级请求解析失败: " + e.Message,
                };
            }
            return SkillService.Instance.Upgrade(request.SkillId, request.PartnerUid);
        }

        private static object OnSkillAdvanced(byte[] req)
        {
            SkillAdvancedRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<SkillAdvancedRequest>(req);
            }
            catch (System.Exception e)
            {
                return new SkillAdvancedResponse
                {
                    Code = -1,
                    Error = "技能进阶请求解析失败: " + e.Message,
                };
            }
            return SkillService.Instance.Advance(request.SkillId, request.PartnerUid);
        }

        private static object OnSkillChange(byte[] req)
        {
            SkillChangeRequest request;
            try
            {
                request = TCPClientMgr.ProtoDeSerialize<SkillChangeRequest>(req);
            }
            catch (System.Exception e)
            {
                return new SkillChangeResponse
                {
                    Code = -1,
                    Error = "技能换槽请求解析失败: " + e.Message,
                    OpType = 0,
                    Slot = SkillsSlot.INVALID_SkillsSlot,
                };
            }
            return SkillService.Instance.Change(request.OpType, request.Slot, request.SkillId);
        }

        /// <summary>
        /// 动作协议尚未实现：回失败并保留 RequestCmdId。
        /// 不修改 LocalData，不发送成功推送。
        /// </summary>
        private static void RegUnsupported(Dictionary<short, LocalServer.Handler> dict, CmdId cmd)
        {
            short id = (short)cmd;
            LocalDiagnostics.DeclareUnsupportedAction(id);
            LocalServer.Reg(dict, cmd, (req) =>
            {
                LocalDiagnostics.RecordUnsupportedAction(id, "动作协议尚未迁移到本地 Service");
                return new CommonResponse
                {
                    Code = -1,
                    Error = "单机版尚未实现该功能",
                    RequestCmdId = (uint)cmd,
                };
            });
        }
    }
}
