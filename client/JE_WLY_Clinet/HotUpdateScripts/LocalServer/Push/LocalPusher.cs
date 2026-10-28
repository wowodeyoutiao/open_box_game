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

            // 登录时先把离线挂机收益结算掉，再推送最新基础和背包。
            var idle = IdleRewardService.Instance.SettleCurrent();
            if (idle != null && idle.BagFull)
                JEngine.Core.Log.PrintWarning("[IdleRewardService] 登录结算暂停: " + idle.Error);

            // 登录推送前统一重算，确保 Base / Stats / 大厅 UI 使用同一套属性。
            // 当前没有战斗消耗，登录时补满血量，避免显示旧存档里的默认 100。
            LocalRole current = d.Current;
            if (current != null)
            {
                SkillService.Instance.AutoLearn(current, false);
                PushAllSkillsNow(current);
                TaskService.Instance.EnsureInitialized(current);
                TaskService.Instance.ResetIfNeeded(current);
                PushAllTasksNow(current);
                // 上阵宠物的挂机产出在登录时结算一次（原服上线时按上次记录时间补发）
                PetService.Instance.SettleOfflineAward(current);
            }
            RoleStatService.Instance.RecalculateCurrent(true);

            PushUserBase();
            PushUserStats();
            PushBoxInfo();
            PushUserEquips();
            PushBagInfoNow();
            PushAutoDisassemblyNow();
            PushPartnerSlot();
            PushUserPet();
            PushSkillSlot();

            if (idle != null && idle.LevelsGained > 0)
                PushLevelUpNow(idle.NewLevel);

            // 大厅遇怪不再在这里推：改为 UIHall.StartLocalHomeEncounter() 本地自驱。
            // 原因：走 JEvent → ProtoC.Sever_HomeAutoEncounterPush → Sync_HomeAutoEncounter
            // → UIHall 这条链路实测没生效（大厅刷不出怪），排查成本高且难验证。
            // 大厅挂机表现本来就是 UI 层职责，单机下由 UI 自己 tick 更直接可靠。

            JEngine.Core.Log.Print("[LocalPusher] 登录推送完成");

#if LOCAL_TEST
            HotUpdateScripts.LocalNet.Testing.LocalTestBootstrap.TryRun();
#endif
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

        /// <summary>挂机奖励或背包变化后整包刷新客户端背包镜像。</summary>
        public void PushBagInfoNow()
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null || role.BagData == null || role.BagData.ItemList == null)
                return;
            HotUpdateScripts.ProtoM.BagM.Instance.SetBagInfo(role.BagData.ItemList);
        }

        /// <summary>同步自动分解设置，避免 BagM.AutoDelSetInfo 为 null。</summary>
        public void PushAutoDisassemblyNow()
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return;
            if (role.AutoDisassembly == null)
                role.AutoDisassembly = new AutoDisassemblyInfo();
            // 出厂开关兜底：读档时机可能早于 config.xml 就绪，这里再补一次（只填全 0 的空壳）
            ItemService.ApplyAutoDisassemblyDefaults(role);
            HotUpdateScripts.ProtoM.BagM.Instance.AutoDelSetInfo = role.AutoDisassembly;
            JEvent.defaultEvent.Post(new GameEvent.AutoDelSet());
        }

        /// <summary>角色升级推送，对齐原服 LevelUpPush。</summary>
        public void PushLevelUpNow(int newLevel)
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return;
            PushLevelUpNow(newLevel, role.Uid);
        }

        /// <summary>
        /// 升级表现推送。Uid 是升级的对象：主角是 role.Uid，伙伴是 partner.Uid。
        /// 原服 Player.levelUp / Partner.levelUp 都发 RouteLevelUpPush。
        /// </summary>
        public void PushLevelUpNow(int newLevel, long uid)
        {
            JEvent.defaultEvent.Post(new LevelUpPush
            {
                NewLevel = newLevel,
                Uid = uid,
            });
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
                TrialVoucher = d.TrialVoucher,
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
            LocalRole current = data != null ? data.Current : null;
            EnsureHomeAFKReset(current);

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

            long used = current != null ? current.HomeAFKUsedSec : 0L;
            long remaining = baseSeconds - used;
            return remaining > 0 ? remaining : 0L;
        }

        /// <summary>
        /// 大厅挂机时间按自然日重置。服务端由 TimerFor0Oclock 清 vitalityTime，
        /// 单机没有跨天定时器，在登录/结算入口做同样的惰性重置。
        /// </summary>
        public static void EnsureHomeAFKReset(LocalRole role)
        {
            if (role == null)
                return;

            int today = int.Parse(System.DateTime.Now.ToString("yyyyMMdd"));
            if (role.HomeAFKResetDay == today)
                return;

            role.HomeAFKResetDay = today;
            role.HomeAFKUsedSec = 0;
            role.HomeAFKAnchorUnix = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveCoordinator.Instance.MarkDirty();
            JEngine.Core.Log.Print("[LocalPusher] HomeAFK daily reset day=" + today);
        }

        /// <summary>角色属性：决定血条、战力、攻防显示</summary>
        private void PushUserStats()
        {
            RoleStatSnapshot snapshot = RoleStatService.Instance.RecalculateCurrent();
            if (snapshot == null)
                return;

            LocalRole role = LocalData.Instance.Current;
            var push = new SyncUserStatsPush
            {
                // 过渡态：玩家属性仍含伙伴属性（伙伴还没真正参战），所以两个战力都取合并口径。
                // 补完伙伴战斗 AI 后改成 Combat=snapshot.Combat / TotalCombat=role.TotalCombat。
                Combat = snapshot.Combat,
                TotalCombat = snapshot.Combat,
            };
            push.Attributes.AddRange(snapshot.ToStatsList());

            // 伙伴属性单独一段下发（原服 packet.Partners）。
            // 客户端 PartnerM.GetOnePartnerStats 依赖它显示伙伴战力与四维，
            // 之前一直没填 → 伙伴面板永远是空的。
            FillPartnerStats(push, role);

            JEvent.defaultEvent.Post(push);
        }

        /// <summary>
        /// 填充 SyncUserStatsPush.Partners：每个**上阵**伙伴一条
        /// （Combat + 最终属性 + PartnerUid），对齐原服 partner.refreshStats 结尾的
        /// `packet.Partners = append(packet.Partners, ps)`。
        /// </summary>
        private static void FillPartnerStats(SyncUserStatsPush push, LocalRole role)
        {
            if (push == null || role == null)
                return;

            List<PartnerInfo> partners = PartnerService.Instance.GetBattlePartners(role);
            if (partners == null)
                return;

            int maxAttr = (int)Attribute.MAX_Attribute;
            for (int i = 0; i < partners.Count; i++)
            {
                PartnerInfo partner = partners[i];
                if (partner == null)
                    continue;

                Dictionary<Attribute, int> stats = RoleStatService.BuildPartnerStatsFinal(partner);
                var ps = new SyncUserStatsPush.PartnerStats
                {
                    PartnerUid = partner.Uid,
                    Combat = RoleStatService.GetPartnerCombat(partner),
                };

                // 按 Attribute 升序、过滤 <=0，与玩家属性列表保持同样的输出形态
                for (int a = 1; a < maxAttr; a++)
                {
                    Attribute attribute = (Attribute)a;
                    int value;
                    if (!stats.TryGetValue(attribute, out value) || value <= 0)
                        continue;
                    ps.Attributes.Add(new Stats { attr = attribute, Num = value });
                }
                push.Partners.Add(ps);
            }
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

                // 魂石：长度必须是 4 —— 客户端镶嵌面板按 0..3 四个槽位索引访问
                // （UIBuild_Equip.OnInSetUI 里 Diamond_OpenlvDic[i+1] 循环 4 次）。
                var gems = new int[LocalRole.GemSlotCount];
                int[] raw = i >= 1 && i < max ? role.PeekEquipGems((EquipSlot)i) : null;
                if (raw != null)
                {
                    for (int g = 0; g < gems.Length && g < raw.Length; g++)
                        gems[g] = raw[g];
                }

                list.Add(new SlotInfo
                {
                    Item = item ?? new ItemInfo(),
                    Gemstone = gems,
                });
            }

            JEvent.defaultEvent.Post(new SyncUserEquipsInfoPush { Equips = list });
            JEngine.Core.Log.Print($"[LocalPusher] 同步装备 {list.Count} 个槽位，其中穿戴 {role.Equips.Count} 件");
        }

        /// <summary>
        /// 战宠数据（对应客户端 Sever_SyncUserPetInfoPush）。
        /// 必须推满 21 个槽位：PetM.GetOnePetByIdx(0) 是大厅 SetPetSpine 的上阵宠物，
        /// UIPet 会按索引 0..20 遍历包裹，长度不足直接 ArgumentOutOfRange。
        /// </summary>
        private void PushUserPet()
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return;

            role.EnsurePetSlots();
            var list = new List<PetSlotInfo>(LocalRole.PetSlotCount);
            for (int i = 0; i < LocalRole.PetSlotCount; i++)
            {
                PetSlotInfo pet = role.PetSlots[i];
                list.Add(pet == null ? new PetSlotInfo() : new PetSlotInfo
                {
                    Uid = pet.Uid,
                    CfgId = pet.CfgId,
                    Level = pet.Level,
                    Star = pet.Star,
                    Exp = pet.Exp,
                    ExpStar = pet.ExpStar,
                    Quality = pet.Quality,
                });
            }

            JEvent.defaultEvent.Post(new SyncUserPetInfoPush
            {
                Pet = list,
                PetEverHave = role.PetEverHave == null
                    ? new int[0] : role.PetEverHave.ToArray(),
            });
            JEngine.Core.Log.Print($"[LocalPusher] 同步战宠 {LocalRole.PetSlotCount} 个槽位");
        }

        /// <summary>宠物状态变化（升级/升星/换位/获得）后立即刷新。</summary>
        public void PushUserPetNow()
        {
            PushUserPet();
        }

        /// <summary>伙伴槽位：底部「伙伴1-4」的占位</summary>
        /// <summary>
        /// 伙伴槽位推送（上阵/下阵/伙伴穿脱装备后都要调）。
        /// 必须带 Equips，否则客户端伙伴装备位永远是空的。
        /// </summary>
        public void PushPartnerSlot()
        {
            var push = new PartnerSlotUpdatePush { List = new List<PartnerSlotInfo>() };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                JEvent.defaultEvent.Post(push);
                return;
            }

            role.EnsurePartnerEquipSlots();
            for (int i = 0; i < role.PartnerSlots.Count; i++)
            {
                PartnerSlotInfo slot = role.PartnerSlots[i];
                if (slot == null)
                    continue;
                var info = new PartnerSlotInfo
                {
                    SlotId = slot.SlotId,
                    PartnerUid = slot.PartnerUid,
                    Equips = new List<SlotInfo>(),
                };
                if (slot.Equips != null)
                {
                    for (int s = 0; s < slot.Equips.Count; s++)
                    {
                        SlotInfo equip = slot.Equips[s];
                        info.Equips.Add(equip == null
                            ? new SlotInfo { Gemstone = new int[0] }
                            : new SlotInfo
                            {
                                Item = equip.Item,
                                Enhance = equip.Enhance,
                                Stars = equip.Stars,
                                Gemstone = equip.Gemstone == null ? new int[0] : (int[])equip.Gemstone.Clone(),
                            });
                    }
                }
                push.List.Add(info);
            }
            JEvent.defaultEvent.Post(push);
        }

        /// <summary>
        /// 技能槽位。长度必须是 13（pb.SkillsSlot_MaxSkillsSlot），
        /// 客户端 OnSkillShow 会循环 13 次按索引取；长度为 0 会导致越界。
        /// </summary>
        private void PushSkillSlot()
        {
            LocalRole role = LocalData.Instance.Current;
            var slots = new int[13];
            if (role != null && role.SkillSlots != null)
            {
                for (int i = 0; i < 13 && i < role.SkillSlots.Length; i++)
                    slots[i] = role.SkillSlots[i];
            }
            JEvent.defaultEvent.Post(new SyncUserSkillSlotPush { SkillsSlot = slots });
        }

        /// <summary>技能槽变化后立即刷新。</summary>
        public void PushSkillSlotNow()
        {
            PushSkillSlot();
        }

        public void PushPlayerEnterDeskNow(PlayerInfo player)
        {
            if (player == null)
                return;
            JEvent.defaultEvent.Post(new PlayerEnterDeskPush
            {
                Data = new System.Collections.Generic.List<PlayerInfo> { player },
            });
        }

        public void PushBattleMoveNow(MovePush packet)
        {
            if (packet != null)
                JEvent.defaultEvent.Post(packet);
        }

        public void PushBattleAttackNow(AttackPush packet)
        {
            if (packet != null)
                JEvent.defaultEvent.Post(packet);
        }

        public void PushBattleHpNow(ObjectHpChangePush packet)
        {
            if (packet != null)
                JEvent.defaultEvent.Post(packet);
        }

        public void PushBattleDieNow(ObjectDiePush packet)
        {
            if (packet != null)
                JEvent.defaultEvent.Post(packet);
        }

        /// <summary>
        /// 装备强化成功推送。客户端 Sever_EnhanceResponse 会
        /// RoleM.SetEquipeReinforeceLv(slot, newLevel) 并刷界面。
        /// </summary>
        public void PushEquipEnhanceNow(EquipSlot slot, int newLevel)
        {
            JEvent.defaultEvent.Post(new EnhanceResponse
            {
                Code = 0,
                Error = "",
                NewLevel = newLevel,
                Slot = slot,
                PartnerSlotId = 0,
            });
        }

        /// <summary>
        /// 装备升星推送。客户端 Sever_StarsResponse 会
        /// RoleM.SetEquipeReinforeceLv(slot, newLevel) 并刷界面。
        /// </summary>
        public void PushEquipStarNow(EquipSlot slot, int newLevel)
        {
            JEvent.defaultEvent.Post(new StarsResponse
            {
                Code = 0,
                Error = "",
                NewLevel = newLevel,
                Slot = slot,
                PartnerSlotId = 0,
            });
        }

        /// <summary>副本信息刷新（首领剩余次数等），客户端 Sever_C2S_InstanceInfo 会更新界面。</summary>
        public void PushInstanceInfoNow(InstanceInfoResponse data)
        {
            if (data != null)
                JEvent.defaultEvent.Post(data);
        }

        /// <summary>
        /// 战斗场景对象进场上报。客户端 Sever_GameObjectAddPush →
        /// FightController.CreatGameObject，伙伴就是靠这个生成模型的。
        /// </summary>
        public void PushBattleObjectAddNow(GameObjectAddPush packet)
        {
            if (packet != null)
                JEvent.defaultEvent.Post(packet);
        }

        /// <summary>伙伴槽位更新推送，客户端 Sever_PartnerSlotUpdatePush 会刷伙伴界面。</summary>
        public void PushPartnerSlotNow(PartnerSlotInfoResponse data)
        {
            if (data == null)
                return;
            var packet = new PartnerSlotUpdatePush();
            if (data.List != null)
                packet.List.AddRange(data.List);
            JEvent.defaultEvent.Post(packet);
        }

        /// <summary>技能冷却推送，客户端 Sever_SkillCooldownPush 会写 SkillM 并刷 UI。</summary>
        public void PushSkillCooldownNow(SkillCooldownPush packet)
        {
            if (packet != null)
                JEvent.defaultEvent.Post(packet);
        }

        public void PushBattleResultNow(GameResultBattlePush packet)
        {
            if (packet != null)
                JEvent.defaultEvent.Post(packet);
        }

        /// <summary>登录时把任务列表推给 TaskM。</summary>
        public void PushAllTasksNow(LocalRole role)
        {
            if (role == null || role.Tasks == null)
                return;
            for (int i = 0; i < role.Tasks.Count; i++)
                PushTaskAcceptNow(role.Tasks[i]);
        }

        public void PushTaskAcceptNow(TaskInfo task)
        {
            if (task == null)
                return;
            JEvent.defaultEvent.Post(new TaskAcceptPush { task = task });
        }

        public void PushTaskUpdateNow(TaskInfo task)
        {
            if (task == null)
                return;
            JEvent.defaultEvent.Post(new TaskUpdatePush { task = task });
        }

        /// <summary>登录时把已存档技能全部补推给 SkillM。</summary>
        public void PushAllSkillsNow(LocalRole role)
        {
            if (role == null || role.Skills == null)
                return;
            for (int i = 0; i < role.Skills.Count; i++)
                PushNewSkillNow(role.Skills[i]);
        }

        /// <summary>学习新技能后通知技能面板，并为技能槽刷新做准备。</summary>
        public void PushNewSkillNow(SkillInfo skill)
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null || skill == null)
                return;
            var slots = new int[13];
            if (role.SkillSlots != null)
            {
                for (int i = 0; i < 13 && i < role.SkillSlots.Length; i++)
                    slots[i] = role.SkillSlots[i];
            }
            JEvent.defaultEvent.Post(new NewSkillPush
            {
                skill = skill,
                SkillsSlot = slots,
            });
        }

    }
}
