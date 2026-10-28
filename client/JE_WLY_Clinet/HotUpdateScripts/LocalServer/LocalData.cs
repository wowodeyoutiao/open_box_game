using System.Collections.Generic;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 单个角色的完整数据。对应服务端 character 表的一行。
    /// 这是权威数据：所有修改都落在 LocalRole 上，再由推送镜像到客户端 Manager。
    /// </summary>
    public class LocalRole
    {
        public long Uid;
        public string Name = "";
        public string HeadUrl = "";
        public int Job = 1;      // 1 战士 / 2 法师 / 3 祭祀
        public int Sex = 1;      // 1 男 / 2 女
        public int Level = 1;
        public int Vip;
        public long Exp;

        // 货币
        public long Coin;
        public long Diamanta;
        public long Coupon;
        public long Soul;
        /// <summary>试炼积分（item id=8 SLPZ），试炼商城货币。</summary>
        public long TrialVoucher;
        public long Energy = 100;

        // ===== 签到进度（对齐原服 db.Sign 的 UserRound/Round/Day/CreatedAt）=====
        // 原服 sign.json：Round=第几轮，Days=第几天，VIPLimit=达到该 VIP 奖励翻倍。
        // 每天只能签一次；当天已签判负。跨天时 Day++，本轮满则 Round++（整轮满则 UserRound++ 重开）。
        /// <summary>第几个大轮回（整轮打完后 +1，回到 Round=1）</summary>
        public int SignUserRound = 1;
        /// <summary>当前第几轮（对应 sign.json 的 Round）</summary>
        public int SignRound = 1;
        /// <summary>当前轮内第几天（对应 sign.json 的 Days，0 = 还没开始）</summary>
        public int SignDay = 0;
        /// <summary>上次签到的时间戳（秒），用来判断「今天签过没」</summary>
        public long SignLastUnix = 0;

        // 战斗属性运行时缓存；正式结果由 RoleStatService 统一计算。
        public int Attack = 10;
        public int Defense = 5;
        public int MaxHp = 100;
        public int CurHp = 100;
        public int TotalCombat = 200;

        // 背包 / 伙伴
        public Bag BagData = new Bag();
        public List<PartnerInfo> Partners = new List<PartnerInfo>();
        /// <summary>
        /// 伙伴上阵槽位。原服 Player.partnerSlots 是 [MaxPartnerNum=4] 固定数组。
        /// 单机不按 VIP 卡槽位，直接给满 4 个（VIP 配置里 Partnerslot 全为 0）。
        /// </summary>
        public List<PartnerSlotInfo> PartnerSlots = new List<PartnerSlotInfo>();

        /// <summary>
        /// 伙伴槽位上的装备位数量。槽位号与玩家一致（1..12，0 空置），
        /// 对齐原服 `PartnerSlot.Equips [pb.EquipSlot_MaxEquipSlot]Slot`。
        /// </summary>
        public const int PartnerEquipSlotCount = 13;

        /// <summary>把每个伙伴槽的 Equips 补成 13 个空槽（0 号空置）。旧档/新建槽都走这里。</summary>
        public void EnsurePartnerEquipSlots()
        {
            if (PartnerSlots == null)
                PartnerSlots = new List<PartnerSlotInfo>();
            for (int i = 0; i < PartnerSlots.Count; i++)
            {
                PartnerSlotInfo slot = PartnerSlots[i];
                if (slot == null)
                    continue;
                if (slot.Equips == null)
                    slot.Equips = new List<SlotInfo>();
                while (slot.Equips.Count < PartnerEquipSlotCount)
                {
                    slot.Equips.Add(new SlotInfo
                    {
                        Item = null,
                        Enhance = 0,
                        Stars = 0,
                        Gemstone = new int[0],
                    });
                }
            }
        }

        /// <summary>按槽位号（1..4）取伙伴槽，找不到返回 null。</summary>
        public PartnerSlotInfo FindPartnerSlot(int slotId)
        {
            if (PartnerSlots == null)
                return null;
            for (int i = 0; i < PartnerSlots.Count; i++)
            {
                if (PartnerSlots[i] != null && PartnerSlots[i].SlotId == slotId)
                    return PartnerSlots[i];
            }
            return null;
        }

        /// <summary>按伙伴 Uid 找它所在的槽（属性计算用），找不到返回 null。</summary>
        public PartnerSlotInfo FindPartnerSlotByUid(long partnerUid)
        {
            if (partnerUid <= 0 || PartnerSlots == null)
                return null;
            for (int i = 0; i < PartnerSlots.Count; i++)
            {
                PartnerSlotInfo slot = PartnerSlots[i];
                if (slot != null && slot.PartnerUid == partnerUid)
                    return slot;
            }
            return null;
        }

        // 战宠。原服 Player.pet 是 [PetsSlot_MaxPetsSlot=21] 定长数组：
        //   0 号 = 上阵位（只有它提供属性加成）
        //   1..20 = 包裹格（config.xml <petbag totlenum="20"/>）
        // 槽位永远保持 21 条（空位是 CfgId=0 的空对象），客户端 PetM.GetOnePetByIdx 按索引取。
        public const int PetSlotCount = (int)PetsSlot.MaxPetsSlot;

        /// <summary>上阵宠物槽位（原服 p.pet[0]）。</summary>
        public const int ActivePetSlot = 0;

        /// <summary>宠物槽位列表，长度恒为 <see cref="PetSlotCount"/>。</summary>
        public List<PetSlotInfo> PetSlots = new List<PetSlotInfo>();

        /// <summary>已获得过的宠物（原服 petEverHave），图鉴/红点用。</summary>
        public List<int> PetEverHave = new List<int>();

        // 上阵宠物离线收益（原服 pet.OfflineAward + petGold / petExp 累计上限）
        /// <summary>上次结算离线收益的时间（Unix 秒）。</summary>
        public long PetAwardAnchorUnix;
        /// <summary>宠物累计产出的经验（对齐原服 petExp，达到配置 max 后不再产出）。</summary>
        public long PetAwardExpTotal;
        /// <summary>宠物累计产出的金币（对齐原服 petGold，达到配置 max 后不再产出）。</summary>
        public long PetAwardGoldTotal;

        // 商城购买记录（限购统计用，原服写 db.Store 表）
        // 每 3 个 long 一条记录：[商品ID, 数量, 购买时间Unix秒]。
        // 之所以不定义 StoreBuyRecord 类：ILRuntime 里「热更内自定义类型作 List 泛型参数
        // + 索引器读字段」会抛 NotImplementedException，用基础类型平铺最稳。
        public List<long> StoreBuyLog = new List<long>();

        // 首领副本（module 17，Lv20）
        /// <summary>首领通关进度（已通关的最大 BattleId）。</summary>
        public int BossId;
        /// <summary>今日剩余挑战次数。</summary>
        public int BossChallengeTime;
        /// <summary>挑战次数重置日（yyyyMMdd）。</summary>
        public int BossResetDay;

        // 遗迹（RoomType 17，战役型副本，200 关）
        /// <summary>遗迹进度（已通关的最高 BattleId）。</summary>
        public int RelicId;
        /// <summary>今日剩余遗迹挑战次数。</summary>
        public int RelicChallengeTime;
        /// <summary>遗迹次数重置日（yyyyMMdd）。</summary>
        public int RelicResetDay;
        /// <summary>遗迹通关评价（BattleId → 星数 1~3，对应 pb.KeyValue 的 K/V）。</summary>
        public List<KeyValue> RelicEvaluation = new List<KeyValue>();
        /// <summary>
        /// 遗迹章节星级宝箱已领到第几档（Chapter → 已领取的星数档位 10/20/30，对应 pb.KeyValue 的 K/V）。
        /// 对齐原服 RelicMap.ChapterReward：每章按「本章各关星级之和」解锁 10/20/30 三档，只能往后领。
        /// </summary>
        public List<KeyValue> RelicChapterReward = new List<KeyValue>();

        // 挂机副本（守卫海岛，RoomType 1）
        /// <summary>今日已用挂机秒数（对齐原服 afkDailyTime）。</summary>
        public int AfkDailySec;
        /// <summary>挂机时长重置日（yyyyMMdd）。</summary>
        public int AfkResetDay;
        /// <summary>
        /// 挂机倍率 ID（对齐原服 AfkMultiplierID，每天重置）。
        /// 单机版不接广告/元宝/充值，只用免费档 0（multiplier=0，即 ×1）。
        /// </summary>
        public long AfkMultiplierID;

        // 每日副本（RoomType 7..12：经验/金币/强化石/书页/升星石/魂石）
        /// <summary>各每日副本今日剩余次数（key = RoomType 整型值）。</summary>
        public Dictionary<int, int> DailyMapTimes = new Dictionary<int, int>();
        /// <summary>每日副本挑战次数重置日（yyyyMMdd）。</summary>
        public int DailyMapResetDay;

        // 试炼塔副本（module 19，Lv30）
        /// <summary>试炼塔进度（已通关的最高层 BattleId）。</summary>
        public int TrialTowerId;

        /// <summary>身上已穿戴的装备（按部位）</summary>
        public Dictionary<EquipSlot, ItemInfo> Equips = new Dictionary<EquipSlot, ItemInfo>();

        /// <summary>
        /// 各部位装备强化等级（对齐原服 Slot.Enhance）。
        /// **强化是跟着槽位走的，不是跟着装备实例** —— 换装备不重置强化等级。
        /// 所以单独存一份，卸下装备时也保留。
        /// </summary>
        public Dictionary<EquipSlot, int> EquipEnhance = new Dictionary<EquipSlot, int>();

        /// <summary>取某个部位的强化等级（没记录返回 0）。</summary>
        public int GetEquipEnhance(EquipSlot slot)
        {
            if (EquipEnhance == null)
                return 0;
            int level;
            return EquipEnhance.TryGetValue(slot, out level) ? level : 0;
        }

        /// <summary>
        /// 各部位装备星级（对齐原服 Slot.Stars）。
        /// 同强化一样，**星级跟着槽位走**，换装备不重置。
        /// </summary>
        public Dictionary<EquipSlot, int> EquipStars = new Dictionary<EquipSlot, int>();

        /// <summary>取某个部位的星级（没记录返回 0）。</summary>
        public int GetEquipStars(EquipSlot slot)
        {
            if (EquipStars == null)
                return 0;
            int stars;
            return EquipStars.TryGetValue(slot, out stars) ? stars : 0;
        }

        /// <summary>每件装备的魂石槽数量（原服 pb.GemstoneSlot_MaxGemstoneSlot）。</summary>
        public const int GemSlotCount = 4;

        /// <summary>
        /// 各部位装备上镶嵌的魂石（对齐原服 Slot.Gemstone[MaxGemstoneSlot]）。
        ///
        /// - 下标 0..3 对应客户端 4 个魂石槽；
        /// - 存的是魂石的**道具配置 ID**（0 = 空槽）；
        /// - 和强化/星级一样**跟着槽位走**，换装备保留。
        /// </summary>
        public Dictionary<EquipSlot, int[]> EquipGems = new Dictionary<EquipSlot, int[]>();

        /// <summary>取某部位的魂石槽数组（没有就补一个长度 4 的空数组）。</summary>
        public int[] GetEquipGems(EquipSlot slot)
        {
            if (EquipGems == null)
                EquipGems = new Dictionary<EquipSlot, int[]>();
            int[] gems;
            if (!EquipGems.TryGetValue(slot, out gems) || gems == null || gems.Length != GemSlotCount)
            {
                gems = new int[GemSlotCount];
                EquipGems[slot] = gems;
            }
            return gems;
        }

        /// <summary>某部位是否镶嵌了魂石（属性结算用，不补建数组）。</summary>
        public int[] PeekEquipGems(EquipSlot slot)
        {
            if (EquipGems == null)
                return null;
            int[] gems;
            return EquipGems.TryGetValue(slot, out gems) ? gems : null;
        }

        /// <summary>
        /// 保证宠物槽位列表长度恒为 <see cref="PetSlotCount"/>（21）。
        /// 存档迁移、老档没这个字段、UI 按索引访问都会踩到「长度不足」，
        /// 所以读宠物数据前统一先补位（空位是 CfgId=0 的空对象）。
        /// </summary>
        public void EnsurePetSlots()
        {
            if (PetSlots == null)
                PetSlots = new List<PetSlotInfo>();
            while (PetSlots.Count < PetSlotCount)
                PetSlots.Add(new PetSlotInfo());
            while (PetSlots.Count > PetSlotCount)
                PetSlots.RemoveAt(PetSlots.Count - 1);
            for (int i = 0; i < PetSlots.Count; i++)
            {
                if (PetSlots[i] == null)
                    PetSlots[i] = new PetSlotInfo();
            }
        }

        /// <summary>取某个宠物槽位（越界返回 null，不抛异常）。</summary>
        public PetSlotInfo GetPetSlot(int slotId)
        {
            EnsurePetSlots();
            if (slotId < 0 || slotId >= PetSlots.Count)
                return null;
            return PetSlots[slotId];
        }

        /// <summary>上阵宠物（0 号槽）。没宠物时返回 CfgId=0 的空对象，不是 null。</summary>
        public PetSlotInfo GetActivePet()
        {
            return GetPetSlot(ActivePetSlot);
        }

        /// <summary>某槽位是否有宠物。</summary>
        public bool HasPet(int slotId)
        {
            PetSlotInfo pet = GetPetSlot(slotId);
            return pet != null && pet.CfgId > 0;
        }

        // 挂机进度
        public long HomeAFKUsedSec;
        public long HomeAFKAnchorUnix;
        public int HomeAFKResetDay;
        public long LastSeenUnix;

        // 大厅宝箱
        public int BoxLevel = 1;
        public long TreasuerBox;
        public long BoxQuick;
        public long BoxUpgradeEndUnix;
        public List<ItemInfo> BoxOpenItems = new List<ItemInfo>();

        // 系统邮件（单机替代服务端邮件库）
        public List<LocalMail> Mails = new List<LocalMail>();

        // 角色技能与 13 个技能槽
        public List<SkillInfo> Skills = new List<SkillInfo>();
        public int[] SkillSlots = new int[13];

        // 自动分解设置
        public AutoDisassemblyInfo AutoDisassembly = new AutoDisassemblyInfo();

        // 任务
        public List<TaskInfo> Tasks = new List<TaskInfo>();

        // 普通战役进度
        public int BattleId;
        public int BattleRewardStage;

        public LocalRole Clone()
        {
            var clone = new LocalRole
            {
                Uid = Uid,
                Name = Name,
                HeadUrl = HeadUrl,
                Job = Job,
                Sex = Sex,
                Level = Level,
                Vip = Vip,
                Exp = Exp,
                Coin = Coin,
                Diamanta = Diamanta,
                Coupon = Coupon,
                Soul = Soul,
                Energy = Energy,
                Attack = Attack,
                Defense = Defense,
                MaxHp = MaxHp,
                CurHp = CurHp,
                TotalCombat = TotalCombat,
                HomeAFKUsedSec = HomeAFKUsedSec,
                HomeAFKAnchorUnix = HomeAFKAnchorUnix,
                HomeAFKResetDay = HomeAFKResetDay,
                LastSeenUnix = LastSeenUnix,
                BoxLevel = BoxLevel,
                TreasuerBox = TreasuerBox,
                BoxQuick = BoxQuick,
                BoxUpgradeEndUnix = BoxUpgradeEndUnix,
                BoxOpenItems = new List<ItemInfo>(),
                BagData = new Bag { ItemList = new List<ItemInfo>() },
                Partners = new List<PartnerInfo>(),
                PartnerSlots = new List<PartnerSlotInfo>(),
                PetSlots = new List<PetSlotInfo>(),
                PetEverHave = new List<int>(),
                Equips = new Dictionary<EquipSlot, ItemInfo>(),
                Mails = new List<LocalMail>(),
                Skills = new List<SkillInfo>(),
                SkillSlots = new int[13],
                BattleId = BattleId,
                BattleRewardStage = BattleRewardStage,
                AutoDisassembly = new AutoDisassemblyInfo()
            };

            if (BoxOpenItems != null)
            {
                for (int i = 0; i < BoxOpenItems.Count; i++)
                {
                    ItemInfo item = CloneItem(BoxOpenItems[i]);
                    if (item != null)
                        clone.BoxOpenItems.Add(item);
                }
            }

            if (BagData != null && BagData.ItemList != null)
            {
                for (int i = 0; i < BagData.ItemList.Count; i++)
                {
                    ItemInfo item = CloneItem(BagData.ItemList[i]);
                    if (item != null)
                        clone.BagData.ItemList.Add(item);
                }
            }

            if (Partners != null)
            {
                for (int i = 0; i < Partners.Count; i++)
                {
                    PartnerInfo partner = ClonePartner(Partners[i]);
                    if (partner != null)
                        clone.Partners.Add(partner);
                }
            }

            if (PartnerSlots != null)
            {
                for (int i = 0; i < PartnerSlots.Count; i++)
                {
                    PartnerSlotInfo slot = PartnerSlots[i];
                    if (slot == null)
                        continue;
                    clone.PartnerSlots.Add(new PartnerSlotInfo
                    {
                        SlotId = slot.SlotId,
                        PartnerUid = slot.PartnerUid,
                        Equips = new List<SlotInfo>(),
                    });
                }
            }

            if (PetSlots != null)
            {
                for (int i = 0; i < PetSlots.Count; i++)
                {
                    PetSlotInfo pet = PetSlots[i];
                    clone.PetSlots.Add(pet == null ? new PetSlotInfo() : new PetSlotInfo
                    {
                        Uid = pet.Uid,
                        CfgId = pet.CfgId,
                        Level = pet.Level,
                        Star = pet.Star,
                        Exp = pet.Exp,
                        ExpStar = pet.ExpStar,
                        Quality = pet.Quality,
                        Enhance = pet.Enhance,
                        Skill = pet.Skill,
                    });
                }
            }

            if (PetEverHave != null)
                clone.PetEverHave.AddRange(PetEverHave);

            if (Equips != null)
            {
                foreach (KeyValuePair<EquipSlot, ItemInfo> kv in Equips)
                {
                    ItemInfo item = CloneItem(kv.Value);
                    if (item != null)
                        clone.Equips[kv.Key] = item;
                }
            }

            if (EquipGems != null)
            {
                foreach (KeyValuePair<EquipSlot, int[]> kv in EquipGems)
                {
                    if (kv.Value == null)
                        continue;
                    clone.EquipGems[kv.Key] = (int[])kv.Value.Clone();
                }
            }

            if (Skills != null)
            {
                for (int i = 0; i < Skills.Count; i++)
                {
                    SkillInfo skill = Skills[i];
                    if (skill == null) continue;
                    clone.Skills.Add(new SkillInfo
                    {
                        CfgId = skill.CfgId,
                        Cooldown = skill.Cooldown,
                        Advanced = skill.Advanced,
                    });
                }
            }
            if (SkillSlots != null)
                clone.SkillSlots = (int[])SkillSlots.Clone();
            if (AutoDisassembly != null)
            {
                clone.AutoDisassembly = new AutoDisassemblyInfo
                {
                    Open = AutoDisassembly.Open,
                    LevelSwitch = AutoDisassembly.LevelSwitch,
                    Level = AutoDisassembly.Level,
                    QualitySwitch = AutoDisassembly.QualitySwitch,
                    Quality = AutoDisassembly.Quality,
                    HighCombat = AutoDisassembly.HighCombat,
                };
            }

            return clone;
        }

        private static ItemInfo CloneItem(ItemInfo source)
        {
            if (source == null)
                return null;

            var item = new ItemInfo
            {
                CfgId = source.CfgId,
                Uid = source.Uid,
                Num = source.Num,
                CreateTime = source.CreateTime,
                Type = source.Type,
                Equip = null
            };

            if (source.Equip != null)
            {
                item.Equip = new EquipInfo
                {
                    Quality = source.Equip.Quality,
                    Attributes = new List<Stats>()
                };
                if (source.Equip.Attributes != null)
                {
                    for (int i = 0; i < source.Equip.Attributes.Count; i++)
                    {
                        Stats stat = source.Equip.Attributes[i];
                        if (stat == null)
                            continue;
                        item.Equip.Attributes.Add(new Stats { attr = stat.attr, Num = stat.Num });
                    }
                }
            }

            return item;
        }

        private static PartnerInfo ClonePartner(PartnerInfo source)
        {
            if (source == null)
                return null;

            var partner = new PartnerInfo
            {
                Uid = source.Uid,
                CfgId = source.CfgId,
                Level = source.Level,
                Star = source.Star,
                Skills = new List<SkillInfo>()
            };

            if (source.Skills != null)
            {
                for (int i = 0; i < source.Skills.Count; i++)
                {
                    SkillInfo skill = source.Skills[i];
                    partner.Skills.Add(new SkillInfo
                    {
                        CfgId = skill.CfgId,
                        Cooldown = skill.Cooldown,
                        Advanced = skill.Advanced
                    });
                }
            }

            return partner;
        }
    }

    /// <summary>
    /// 单机模式的本地数据 —— 模拟「服务端数据库」。
    ///
    /// 唯一真相源是 Roles。下面的扁平字段是当前角色的只读镜像，
    /// 只在 SelectRole() 时展开；存档只保存 Roles 及账号级 UID 游标。
    /// </summary>
    /// <summary>单机系统邮件。附件直接保存 ItemInfo，领取时再进入背包。</summary>
    public class LocalMail
    {
        public string MailId = "";
        public string Title = "";
        public string Desc = "";
        public int IsRead;
        public int IsExtract;
        public int IsAttachments;
        public long CreatedAt;
        public long ExpiryAt;
        public List<ItemInfo> Attachments = new List<ItemInfo>();
    }

    public class LocalData : SingletonTemplate<LocalData>
    {
        public const long DefaultNextRoleUid = 10001L;
        public const long DefaultNextItemUid = 9000001L;

        // ==================== 权威数据 ====================

        /// <summary>账号下的所有角色</summary>
        public List<LocalRole> Roles = new List<LocalRole>();

        /// <summary>当前选中角色下标，-1 表示未选中</summary>
        public int CurrentIdx = -1;

        /// <summary>账号级 UID 游标，保证存档后不会重复。</summary>
        public long NextRoleUid = DefaultNextRoleUid;
        public long NextItemUid = DefaultNextItemUid;

        /// <summary>当前角色（可能为 null）</summary>
        public LocalRole Current
        {
            get
            {
                if (Roles == null || CurrentIdx < 0 || CurrentIdx >= Roles.Count)
                    return null;
                return Roles[CurrentIdx];
            }
        }

        // ==================== 镜像字段（只读，由 SelectRole 展开）====================

        public long Uid;
        public string Nickname = "";
        public string HeadUrl = "";
        public int Level = 1;
        public int Job = 1;
        public int Sex = 1;
        public long Exp;
        public int Vip;

        public long Gold;
        public long Diamond;
        public long Coupon;
        public long Soul;
        public long TrialVoucher;
        public long Energy = 100;

        public int Attack = 10;
        public int Defense = 5;
        public int MaxHp = 100;
        public int CurHp = 100;
        public int TotalCombat = 200;

        public int BoxLevel = 1;
        public long TreasuerBox;
        public long BoxQuick;
        public long BoxUpgradeEndUnix;

        public List<PartnerInfo> Partners = new List<PartnerInfo>();
        public Bag BagData = new Bag();

        private bool _inited;

        /// <summary>
        /// 保证容器可用。不会再偷偷创建一个默认角色。
        /// 存档加载由 LocalBootstrap + SaveCoordinator 在登录前完成。
        /// </summary>
        public void EnsureInit()
        {
            if (_inited)
                return;
            InitializeEmpty();
        }

        /// <summary>初始化空账号。已有存档时由 SaveCoordinator 覆盖。</summary>
        public void InitializeEmpty()
        {
            _inited = true;
            Roles = new List<LocalRole>();
            CurrentIdx = -1;
            NextRoleUid = DefaultNextRoleUid;
            NextItemUid = DefaultNextItemUid;
            ClearMirror();
        }

        /// <summary>兼容旧调用名，语义已改为“空账号”，不再创建本地勇者。</summary>
        public void Reset()
        {
            InitializeEmpty();
        }

        /// <summary>
        /// 应用存档数据。
        /// selectedUid 找不到时选择第一个角色；没有角色则保持未选中。
        /// </summary>
        public void ApplyLoadedRoles(List<LocalRole> roles, long selectedUid, long nextRoleUid, long nextItemUid)
        {
            _inited = true;
            Roles = roles ?? new List<LocalRole>();
            NextRoleUid = nextRoleUid > 0 ? nextRoleUid : DefaultNextRoleUid;
            NextItemUid = nextItemUid > 0 ? nextItemUid : DefaultNextItemUid;

            EnsureUidCursors();

            int index = -1;
            if (selectedUid > 0)
            {
                for (int i = 0; i < Roles.Count; i++)
                {
                    if (Roles[i].Uid == selectedUid)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0 && Roles.Count > 0)
                index = 0;

            if (index >= 0)
                SelectRole(index);
            else
            {
                CurrentIdx = -1;
                ClearMirror();
            }
        }

        public long AllocateRoleUid()
        {
            EnsureUidCursors();
            return NextRoleUid++;
        }

        public long AllocateItemUid()
        {
            EnsureUidCursors();
            return NextItemUid++;
        }

        /// <summary>
        /// 唯一的新角色创建入口。
        /// 初始装备、货币、默认字段全部由 NewRoleFactory 统一生成。
        /// </summary>
        public LocalRole CreateRole(string name, int job, int sex, out string error)
        {
            EnsureInit();
            error = "";

            int maxRoles = 2;
            try
            {
                if (CFG_XMLConfig.xmlConfig != null && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.global != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.global.rolenum > 0)
                    maxRoles = CFG_XMLConfig.xmlConfig.BaseValue.global.rolenum;
            }
            catch
            {
                // 配置未加载时保留默认上限，具体原因由建角失败日志反馈。
            }

            if (Roles.Count >= maxRoles)
            {
                error = "最多只能创建 " + maxRoles + " 个角色";
                return null;
            }

            long roleUid = AllocateRoleUid();
            long itemUid = NextItemUid;
            LocalRole role = NewRoleFactory.Create(roleUid, name, job, sex, ref itemUid, out error);
            if (role == null)
                return null;

            NextItemUid = itemUid;
            AddRole(role);
            return role;
        }

        /// <summary>新增角色（建角时调用）</summary>
        public void AddRole(LocalRole role)
        {
            EnsureInit();
            if (role == null)
                return;
            Roles.Insert(0, role);
            EnsureUidCursors();
            SelectRole(0);
        }

        /// <summary>删除角色。返回 false 表示 uid 不存在。</summary>
        public bool DeleteRoleByUid(long uid)
        {
            EnsureInit();
            for (int i = 0; i < Roles.Count; i++)
            {
                if (Roles[i].Uid != uid)
                    continue;

                Roles.RemoveAt(i);
                if (Roles.Count == 0)
                {
                    CurrentIdx = -1;
                    ClearMirror();
                }
                else
                {
                    int nextIdx = i >= Roles.Count ? Roles.Count - 1 : i;
                    SelectRole(nextIdx);
                }
                return true;
            }
            return false;
        }

        /// <summary>按下标选中，并把 Roles[idx] 展开到镜像字段</summary>
        public void SelectRole(int idx)
        {
            if (Roles == null || idx < 0 || idx >= Roles.Count)
                return;

            CurrentIdx = idx;
            LocalRole r = Roles[idx];

            Uid = r.Uid;
            Nickname = r.Name;
            HeadUrl = r.HeadUrl;
            Job = r.Job;
            Sex = r.Sex;
            Level = r.Level;
            Vip = r.Vip;
            Exp = r.Exp;

            Gold = r.Coin;
            Diamond = r.Diamanta;
            Coupon = r.Coupon;
            Soul = r.Soul;
            TrialVoucher = r.TrialVoucher;
            Energy = r.Energy;

            Attack = r.Attack;
            Defense = r.Defense;
            MaxHp = r.MaxHp;
            CurHp = r.CurHp;
            TotalCombat = r.TotalCombat;

            BoxLevel = r.BoxLevel;
            TreasuerBox = r.TreasuerBox;
            BoxQuick = r.BoxQuick;
            BoxUpgradeEndUnix = r.BoxUpgradeEndUnix;

            Partners = r.Partners;
            BagData = r.BagData;
        }

        /// <summary>按 uid 选中</summary>
        public bool SelectRoleByUid(long uid)
        {
            EnsureInit();
            for (int i = 0; i < Roles.Count; i++)
            {
                if (Roles[i].Uid == uid)
                {
                    SelectRole(i);
                    return true;
                }
            }
            return false;
        }

        public LocalRole FindRole(long uid)
        {
            if (Roles == null)
                return null;
            for (int i = 0; i < Roles.Count; i++)
            {
                if (Roles[i].Uid == uid)
                    return Roles[i];
            }
            return null;
        }

        /// <summary>
        /// 用服务端返回的角色列表重建 Roles。
        /// 本地单机阶段主要保留接口兼容；已有角色会保留完整进度。
        /// </summary>
        public void SyncRoles(List<LocalRole> serverRoles)
        {
            EnsureInit();
            if (serverRoles == null || serverRoles.Count == 0)
                return;

            var merged = new List<LocalRole>();
            for (int i = 0; i < serverRoles.Count; i++)
            {
                LocalRole incoming = serverRoles[i];
                LocalRole exist = FindRole(incoming.Uid);
                if (exist != null)
                {
                    exist.Name = incoming.Name;
                    exist.HeadUrl = incoming.HeadUrl;
                    exist.Job = incoming.Job;
                    exist.Sex = incoming.Sex;
                    merged.Add(exist);
                }
                else
                {
                    merged.Add(incoming);
                }
            }

            Roles = merged;
            if (CurrentIdx < 0 || CurrentIdx >= Roles.Count)
                SelectRole(0);
            else
                SelectRole(CurrentIdx);
        }

        /// <summary>把镜像字段的改动写回当前角色。</summary>
        public void FlushToCurrent()
        {
            LocalRole r = Current;
            if (r == null)
                return;

            r.Name = Nickname;
            r.Level = Level;
            r.Vip = Vip;
            r.Exp = Exp;
            r.Coin = Gold;
            r.Diamanta = Diamond;
            r.Coupon = Coupon;
            r.Soul = Soul;
            r.TrialVoucher = TrialVoucher;
            r.Energy = Energy;
            r.Attack = Attack;
            r.Defense = Defense;
            r.MaxHp = MaxHp;
            r.CurHp = CurHp;
            r.TotalCombat = TotalCombat;

            r.BoxLevel = BoxLevel;
            r.TreasuerBox = TreasuerBox;
            r.BoxQuick = BoxQuick;
            r.BoxUpgradeEndUnix = BoxUpgradeEndUnix;
        }

        private void ClearMirror()
        {
            Uid = 0;
            Nickname = "";
            HeadUrl = "";
            Level = 1;
            Job = 1;
            Sex = 1;
            Exp = 0;
            Vip = 0;
            Gold = 0;
            Diamond = 0;
            Coupon = 0;
            Soul = 0;
            Energy = 100;
            Attack = 10;
            Defense = 5;
            MaxHp = 100;
            CurHp = 100;
            TotalCombat = 200;
            BoxLevel = 1;
            TreasuerBox = 0;
            BoxQuick = 0;
            BoxUpgradeEndUnix = 0;
            Partners = new List<PartnerInfo>();
            BagData = new Bag { ItemList = new List<ItemInfo>() };
        }

        private void EnsureUidCursors()
        {
            long maxRoleUid = DefaultNextRoleUid - 1;
            long maxItemUid = DefaultNextItemUid - 1;

            if (Roles != null)
            {
                for (int i = 0; i < Roles.Count; i++)
                {
                    LocalRole role = Roles[i];
                    if (role == null)
                        continue;
                    if (role.Uid > maxRoleUid)
                        maxRoleUid = role.Uid;

                    if (role.BagData != null && role.BagData.ItemList != null)
                    {
                        for (int j = 0; j < role.BagData.ItemList.Count; j++)
                        {
                            ItemInfo item = role.BagData.ItemList[j];
                            if (item != null && item.Uid > maxItemUid)
                                maxItemUid = item.Uid;
                        }
                    }

                    if (role.Equips != null)
                    {
                        foreach (KeyValuePair<EquipSlot, ItemInfo> kv in role.Equips)
                        {
                            if (kv.Value != null && kv.Value.Uid > maxItemUid)
                                maxItemUid = kv.Value.Uid;
                        }
                    }
                }
            }

            if (NextRoleUid <= maxRoleUid)
                NextRoleUid = maxRoleUid + 1;
            if (NextItemUid <= maxItemUid)
                NextItemUid = maxItemUid + 1;
        }
    }
}
