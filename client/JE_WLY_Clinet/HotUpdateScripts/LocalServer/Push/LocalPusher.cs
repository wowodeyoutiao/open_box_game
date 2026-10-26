using System.Collections.Generic;
using JEngine.Event;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 推送调度器：模拟服务端的「主动下发」。
    ///
    /// 服务端在登录时会主动 push 一整套数据，客户端各 Manager 靠这些推送才有状态。
    /// 机制：构造推送对象 → JEvent.defaultEvent.Post(obj) → ProtoC 的 Sever_*Push 自动接收。
    /// 不需要经过 ProtoMap。
    ///
    /// 顺序对齐服务端 player.go:1774-1790 的登录推送序列。
    /// </summary>
    public class LocalPusher : SingletonTemplate<LocalPusher>
    {
        private TimeItem _encounterTimer;

        /// <summary>
        /// 登录后的一次性推送。必须在 C2S_Login 响应发出之后调用。
        /// </summary>
        public void OnLogin()
        {
            LocalData d = LocalData.Instance;
            d.EnsureInit();

            // 登录推送前统一重算，确保 Base / Stats / 大厅 UI 使用同一套属性。
            // 当前没有战斗消耗，登录时补满血量，避免显示旧存档里的默认 100。
            RoleStatService.Instance.RecalculateCurrent(true);

            PushUserBase();
            PushUserStats();
            PushBoxInfo();
            PushUserEquips();
            PushPartnerSlot();
            PushSkillSlot();

            // 大厅遇怪不再在这里推：改为 UIHall.StartLocalHomeEncounter() 本地自驱。
            // 原因：走 JEvent → ProtoC.Sever_HomeAutoEncounterPush → Sync_HomeAutoEncounter
            // → UIHall 这条链路实测没生效（大厅刷不出怪），排查成本高且难验证。
            // 大厅挂机表现本来就是 UI 层职责，单机下由 UI 自己 tick 更直接可靠。

            JEngine.Core.Log.Print("[LocalPusher] 登录推送完成");
        }

        /// <summary>装备/等级等影响属性后，立即推送最新属性刷新 UI。</summary>
        public void PushStatsNow()
        {
            PushUserStats();
        }

        /// <summary>一键装备后立即刷新 RoleM 的装备槽数据。</summary>
        public void PushEquipsNow()
        {
            PushUserEquips();
        }

        /// <summary>宝箱数量、开箱结果或升级状态变化后立即刷新大厅显示。</summary>
        public void PushBoxNow()
        {
            PushBoxInfo();
            PushUserBase();
        }

        /// <summary>仅基础数据变化时刷新，例如击杀累计开箱次数。</summary>
        public void PushUserBaseNow()
        {
            PushUserBase();
        }

        /// <summary>
        /// 旧的遇怪推送入口（已停用，保留说明）。
        /// 现在由 UIHall.StartLocalHomeEncounter() 本地驱动。
        /// </summary>
        private void StartHomeEncounterLoop()
        {
            if (_encounterTimer != null)
                return;

            // TimeItem(总时长, 间隔, 回调, isLoop:true, isDelay:true)
            _encounterTimer = new TimeItem(0f, HomeEncounterInterval, () =>
            {
                JEvent.defaultEvent.Post(new HomeAutoEncounterPush
                {
                    Type = 2,   // 1=掉落物 / 2=遇怪
                    Id = 0,
                });
            }, true, true);

            GameHelper.Instance.AddInvoke(_encounterTimer);
        }

        /// <summary>
        /// 遇怪间隔（秒）。
        /// 一次出手序列是 skill×1~2 + attack + walk，约 4~6 秒，
        /// 间隔太短会导致上一轮动画没播完就刷新怪。
        /// </summary>
        private const float HomeEncounterInterval = 6f;

        /// <summary>
        /// 定时推送（遇怪 / Buff 到期 / CD 刷新）。由 Unity Update 驱动。
        /// </summary>
        public void Update(float deltaTime)
        {
            // TODO 遇怪：HomeAutoEncounterPush
        }

        // ==================== 具体推送 ====================

        /// <summary>角色基础数据：货币 / 体力 / 等级 / 经验</summary>
        private void PushUserBase()
        {
            LocalData d = LocalData.Instance;

            var push = new SyncUserBasePush
            {
                Uid = d.Uid,
                Coin = d.Gold,
                Coupon = d.Coupon,
                Soul = d.Soul,
                Energy = d.Energy,
                Vip = d.Vip,
                Exp = d.Exp,
                Level = d.Level,
                EnergyRecoveryTime = 0,
                Combat = d.TotalCombat,
                VipExp = 0,
                FateLevel = 0,
                FateExp = 0,
                TrialVoucher = 0,
                TreasuerBox = d.TreasuerBox,
                // HomeAFK 映射到 RoleM.BoxTimeAllSecound，决定大厅挂机状态：
                //   <= 0 → AnimType.Idle（主角站着不动）
                //   >  0 → AnimType.Move（走路 + 背景滚动）
                HomeAFK = GetHomeAFKRemaining(d),
                PetExp = 0,
                BoxQuick = d.BoxQuick,
                // 公会相关：多人内容，阶段3 删；先给 0
                GuildId = 0,
                GuildContribution = 0,
                GuildWish = 0,
                WishRecoveryTime = 0,
            };
            JEvent.defaultEvent.Post(push);
        }

        /// <summary>
        /// 当前在线挂机剩余时间。第一批只做“配置初始值 + 已用时间”的读取，
        /// 完整的每秒消耗、事件奖励和离线结算在挂机服务阶段接入。
        /// </summary>
        private static long GetHomeAFKRemaining(LocalData data)
        {
            long baseSeconds = 7200L;
            try
            {
                if (CFG_XMLConfig.xmlConfig != null
                    && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle.baseTime > 0)
                    baseSeconds = CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle.baseTime;
            }
            catch
            {
                // 配置未加载时保留服务端当前默认值 7200。
            }

            long used = data != null && data.Current != null
                ? data.Current.HomeAFKUsedSec : 0L;
            long remaining = baseSeconds - used;
            return remaining > 0 ? remaining : 0L;
        }

        /// <summary>角色属性：决定血条、战力、攻防显示</summary>
        private void PushUserStats()
        {
            RoleStatSnapshot snapshot = RoleStatService.Instance.RecalculateCurrent();
            if (snapshot == null)
                return;

            var push = new SyncUserStatsPush
            {
                Combat = snapshot.Combat,
                TotalCombat = snapshot.Combat,
            };
            push.Attributes.AddRange(snapshot.ToStatsList());

            JEvent.defaultEvent.Post(push);
        }

        /// <summary>
        /// 宝箱信息。
        /// ⚠️ Nowlevel 必须 >= 1，否则 UIHall.GetCurrentLevelBoxConfig 会去
        /// CFG_BoxLevel 查 id=0 而抛 KeyNotFoundException（该配置最小 id 是 1）。
        /// </summary>
        private void PushBoxInfo()
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return;

            BoxService.Instance.Refresh(role);

            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var box = new Userbox
            {
                Nowlevel = role.BoxLevel > 0 ? role.BoxLevel : 1,
                NowOpenLevel = 1,
                NextBoxRecoveryTime = role.BoxUpgradeEndUnix > now ? role.BoxUpgradeEndUnix : 0,
                // 推送副本，避免客户端领取时和权威列表操作同一个 List。
                NowOpenRes = BoxService.CopyItems(role.BoxOpenItems),
            };

            JEvent.defaultEvent.Post(new SyncUserBoxInfoPush { Box = box });
        }

        /// <summary>
        /// 身上装备。
        ///
        /// ⚠️ 客户端 RoleM.SetRoleEquipDic 的实现是按**索引**直接取：
        ///     for (i = 1; i &lt; MaxEquipSlot(=13); i++) RoleEquipDic.Add((EquipSlot)i, equipInfo[i]);
        /// 所以列表长度必须 ≥ 13 且索引与部位值对齐（[1]=武器 … [12]=最后一个部位），
        /// 否则会 ArgumentOutOfRangeException。SlotInfo.Item 也不能给 null。
        /// </summary>
        private void PushUserEquips()
        {
            LocalData d = LocalData.Instance;
            LocalRole role = d.Current;
            if (role == null)
                return;

            int max = (int)EquipSlot.MaxEquipSlot;   // 13
            var list = new List<SlotInfo>();
            for (int i = 0; i <= max; i++)
            {
                ItemInfo item = null;
                if (i >= 1 && i < max)
                {
                    ItemInfo tmp;
                    if (role.Equips.TryGetValue((EquipSlot)i, out tmp))
                        item = tmp;
                }

                list.Add(new SlotInfo
                {
                    Item = item ?? new ItemInfo(),
                    Gemstone = new int[3],   // 客户端宝石镶嵌会按索引访问
                });
            }

            JEvent.defaultEvent.Post(new SyncUserEquipsInfoPush { Equips = list });
            JEngine.Core.Log.Print($"[LocalPusher] 同步装备 {list.Count} 个槽位，其中穿戴 {role.Equips.Count} 件");
        }

        /// <summary>伙伴槽位：底部「伙伴1-4」的占位</summary>
        private void PushPartnerSlot()
        {
            var push = new PartnerSlotUpdatePush();
            // 暂不填实际伙伴，保持空列表即可（UI 显示空槽）
            JEvent.defaultEvent.Post(push);
        }

        /// <summary>
        /// 技能槽位。长度必须是 13（pb.SkillsSlot_MaxSkillsSlot），
        /// 客户端 OnSkillShow 会循环 13 次按索引取；长度为 0 会导致越界。
        /// </summary>
        private void PushSkillSlot()
        {
            var slots = new int[13];
            JEvent.defaultEvent.Post(new SyncUserSkillSlotPush { SkillsSlot = slots });
        }

    }
}
