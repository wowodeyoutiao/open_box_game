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
        public long Energy = 100;

        // 战斗属性运行时缓存；正式结果由 RoleStatService 统一计算。
        public int Attack = 10;
        public int Defense = 5;
        public int MaxHp = 100;
        public int CurHp = 100;
        public int TotalCombat = 200;

        // 背包 / 伙伴
        public Bag BagData = new Bag();
        public List<PartnerInfo> Partners = new List<PartnerInfo>();

        /// <summary>身上已穿戴的装备（按部位）</summary>
        public Dictionary<EquipSlot, ItemInfo> Equips = new Dictionary<EquipSlot, ItemInfo>();

        // 挂机进度
        public long HomeAFKUsedSec;
        public long HomeAFKAnchorUnix;
        public long LastSeenUnix;

        // 大厅宝箱
        public int BoxLevel = 1;
        public long TreasuerBox;
        public long BoxQuick;
        public long BoxUpgradeEndUnix;
        public List<ItemInfo> BoxOpenItems = new List<ItemInfo>();

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
                LastSeenUnix = LastSeenUnix,
                BoxLevel = BoxLevel,
                TreasuerBox = TreasuerBox,
                BoxQuick = BoxQuick,
                BoxUpgradeEndUnix = BoxUpgradeEndUnix,
                BoxOpenItems = new List<ItemInfo>(),
                BagData = new Bag { ItemList = new List<ItemInfo>() },
                Partners = new List<PartnerInfo>(),
                Equips = new Dictionary<EquipSlot, ItemInfo>()
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

            if (Equips != null)
            {
                foreach (KeyValuePair<EquipSlot, ItemInfo> kv in Equips)
                {
                    ItemInfo item = CloneItem(kv.Value);
                    if (item != null)
                        clone.Equips[kv.Key] = item;
                }
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
