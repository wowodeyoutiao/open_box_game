using System;
using System.Collections.Generic;
using JEngine.Core;
using pb;
using PBAttribute = pb.Attribute;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 存档根对象。
    ///
    /// 刻意不复用 pb.Bag / pb.ItemInfo：协议结构不是存档契约。
    /// 未来协议字段变化时，只调整 Mapper，不改变已有存档格式。
    /// </summary>
    [Serializable]
    public sealed class SaveDocument
    {
        public int SchemaVersion = SaveDocumentMapper.CurrentSchemaVersion;
        public long SavedAtUnix;
        public long CurrentRoleUid;
        public long NextRoleUid = LocalData.DefaultNextRoleUid;
        public long NextItemUid = LocalData.DefaultNextItemUid;
        public List<RoleSaveV1> Roles = new List<RoleSaveV1>();
    }

    [Serializable]
    public sealed class RoleSaveV1
    {
        public long Uid;
        public string Name = "";
        public string HeadUrl = "";
        public int Job = 1;
        public int Sex = 1;
        public int Level = 1;
        public int Vip;
        public long Exp;

        public long Coin;
        public long Diamanta;
        public long Coupon;
        public long Soul;
        public long Energy = 100;

        public int Attack = 10;
        public int Defense = 5;
        public int MaxHp = 100;
        public int CurHp = 100;
        public int TotalCombat = 200;

        public long HomeAFKUsedSec;
        public long HomeAFKAnchorUnix;
        public long LastSeenUnix;

        public int BoxLevel = 1;
        public long TreasuerBox;
        public long BoxQuick;
        public long BoxUpgradeEndUnix;
        public List<ItemSaveV1> BoxOpenRes = new List<ItemSaveV1>();

        public List<ItemSaveV1> Bag = new List<ItemSaveV1>();
        public List<EquipSaveV1> Equips = new List<EquipSaveV1>();
        public List<PartnerSaveV1> Partners = new List<PartnerSaveV1>();
    }

    [Serializable]
    public sealed class EquipSaveV1
    {
        public int Slot;
        public ItemSaveV1 Item = new ItemSaveV1();
    }

    [Serializable]
    public sealed class ItemSaveV1
    {
        public int CfgId;
        public long Uid;
        public long Num;
        public long CreateTime;
        public int Type;
        public int Quality;
        public List<StatSaveV1> Attributes = new List<StatSaveV1>();
    }

    [Serializable]
    public sealed class StatSaveV1
    {
        public int Attr;
        public int Num;
    }

    [Serializable]
    public sealed class PartnerSaveV1
    {
        public long Uid;
        public int CfgId;
        public int Level;
        public int Star;
        public List<SkillSaveV1> Skills = new List<SkillSaveV1>();
    }

    [Serializable]
    public sealed class SkillSaveV1
    {
        public int CfgId;
        public long Cooldown;
        public int Advanced;
    }

    public static class SaveDocumentMapper
    {
        public const int CurrentSchemaVersion = 2;

        /// <summary>
        /// V1 把装备配置基础属性复制进了 Equip.Attributes。
        /// V2 改为：基础属性只来自配置，实例 Attributes 仅存随机/极品属性。
        /// 当前 V1 仅由本地建角产生，没有真实的随机属性，因此迁移时可以安全清空。
        /// </summary>
        public static SaveDocument MigrateV1ToV2(SaveDocument document)
        {
            if (document == null || document.SchemaVersion != 1)
                return document;

            if (document.Roles != null)
            {
                for (int i = 0; i < document.Roles.Count; i++)
                {
                    RoleSaveV1 role = document.Roles[i];
                    if (role == null)
                        continue;

                    ClearItemAttributes(role.Bag);
                    if (role.Equips != null)
                    {
                        for (int j = 0; j < role.Equips.Count; j++)
                        {
                            if (role.Equips[j] != null)
                                ClearItemAttributes(role.Equips[j].Item);
                        }
                    }
                }
            }

            document.SchemaVersion = 2;
            Log.Print("[SaveDocument] 存档 V1 → V2：清理实例中的基础属性副本");
            return document;
        }

        private static void ClearItemAttributes(ItemSaveV1 item)
        {
            if (item != null && item.Attributes != null)
            {
                item.Attributes.Clear();
            }
        }

        private static void ClearItemAttributes(List<ItemSaveV1> items)
        {
            if (items == null)
                return;
            for (int i = 0; i < items.Count; i++)
                ClearItemAttributes(items[i]);
        }

        public static SaveDocument FromLocalData(LocalData data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            var document = new SaveDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                SavedAtUnix = GetUnixSeconds(),
                CurrentRoleUid = data.Current != null ? data.Current.Uid : 0,
                NextRoleUid = data.NextRoleUid,
                NextItemUid = data.NextItemUid,
                Roles = new List<RoleSaveV1>()
            };

            if (data.Roles != null)
            {
                for (int i = 0; i < data.Roles.Count; i++)
                {
                    LocalRole role = data.Roles[i];
                    if (role != null)
                        document.Roles.Add(ToRoleSave(role));
                }
            }

            return document;
        }

        public static List<LocalRole> ToLocalRoles(SaveDocument document)
        {
            var roles = new List<LocalRole>();
            if (document == null || document.Roles == null)
                return roles;

            for (int i = 0; i < document.Roles.Count; i++)
            {
                RoleSaveV1 role = document.Roles[i];
                if (role != null)
                    roles.Add(ToLocalRole(role));
            }
            return roles;
        }

        private static RoleSaveV1 ToRoleSave(LocalRole role)
        {
            var save = new RoleSaveV1
            {
                Uid = role.Uid,
                Name = role.Name,
                HeadUrl = role.HeadUrl,
                Job = role.Job,
                Sex = role.Sex,
                Level = role.Level,
                Vip = role.Vip,
                Exp = role.Exp,
                Coin = role.Coin,
                Diamanta = role.Diamanta,
                Coupon = role.Coupon,
                Soul = role.Soul,
                Energy = role.Energy,
                Attack = role.Attack,
                Defense = role.Defense,
                MaxHp = role.MaxHp,
                CurHp = role.CurHp,
                TotalCombat = role.TotalCombat,
                HomeAFKUsedSec = role.HomeAFKUsedSec,
                HomeAFKAnchorUnix = role.HomeAFKAnchorUnix,
                LastSeenUnix = role.LastSeenUnix,
                BoxLevel = role.BoxLevel,
                TreasuerBox = role.TreasuerBox,
                BoxQuick = role.BoxQuick,
                BoxUpgradeEndUnix = role.BoxUpgradeEndUnix,
                BoxOpenRes = new List<ItemSaveV1>(),
                Bag = new List<ItemSaveV1>(),
                Equips = new List<EquipSaveV1>(),
                Partners = new List<PartnerSaveV1>()
            };

            if (role.BoxOpenItems != null)
            {
                for (int i = 0; i < role.BoxOpenItems.Count; i++)
                {
                    if (role.BoxOpenItems[i] == null)
                        continue;
                    save.BoxOpenRes.Add(ToItemSave(role.BoxOpenItems[i]));
                }
            }

            if (role.BagData != null && role.BagData.ItemList != null)
            {
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    if (role.BagData.ItemList[i] == null)
                        continue;
                    save.Bag.Add(ToItemSave(role.BagData.ItemList[i]));
                }
            }

            if (role.Equips != null)
            {
                foreach (KeyValuePair<EquipSlot, ItemInfo> kv in role.Equips)
                {
                    if (kv.Value == null)
                        continue;
                    save.Equips.Add(new EquipSaveV1
                    {
                        Slot = (int)kv.Key,
                        Item = ToItemSave(kv.Value)
                    });
                }
            }

            if (role.Partners != null)
            {
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    if (role.Partners[i] == null)
                        continue;
                    save.Partners.Add(ToPartnerSave(role.Partners[i]));
                }
            }

            return save;
        }

        private static LocalRole ToLocalRole(RoleSaveV1 save)
        {
            var role = new LocalRole
            {
                Uid = save.Uid,
                Name = save.Name,
                HeadUrl = save.HeadUrl,
                Job = save.Job,
                Sex = save.Sex,
                Level = save.Level,
                Vip = save.Vip,
                Exp = save.Exp,
                Coin = save.Coin,
                Diamanta = save.Diamanta,
                Coupon = save.Coupon,
                Soul = save.Soul,
                Energy = save.Energy,
                Attack = save.Attack,
                Defense = save.Defense,
                MaxHp = save.MaxHp,
                CurHp = save.CurHp,
                TotalCombat = save.TotalCombat,
                HomeAFKUsedSec = save.HomeAFKUsedSec,
                HomeAFKAnchorUnix = save.HomeAFKAnchorUnix,
                LastSeenUnix = save.LastSeenUnix,
                BoxLevel = save.BoxLevel > 0 ? save.BoxLevel : 1,
                TreasuerBox = save.TreasuerBox,
                BoxQuick = save.BoxQuick,
                BoxUpgradeEndUnix = save.BoxUpgradeEndUnix,
                BoxOpenItems = new List<ItemInfo>(),
                BagData = new Bag { ItemList = new List<ItemInfo>() },
                Equips = new Dictionary<EquipSlot, ItemInfo>(),
                Partners = new List<PartnerInfo>()
            };

            if (save.BoxOpenRes != null)
            {
                for (int i = 0; i < save.BoxOpenRes.Count; i++)
                {
                    ItemInfo item = ToItem(save.BoxOpenRes[i]);
                    if (item != null)
                        role.BoxOpenItems.Add(item);
                }
            }

            if (save.Bag != null)
            {
                for (int i = 0; i < save.Bag.Count; i++)
                {
                    ItemInfo item = ToItem(save.Bag[i]);
                    if (item != null)
                        role.BagData.ItemList.Add(item);
                }
            }

            if (save.Equips != null)
            {
                for (int i = 0; i < save.Equips.Count; i++)
                {
                    EquipSaveV1 equip = save.Equips[i];
                    if (equip == null || equip.Item == null)
                        continue;
                    EquipSlot slot = (EquipSlot)equip.Slot;
                    int slotValue = (int)slot;
                    if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                        || slotValue >= (int)EquipSlot.MaxEquipSlot)
                        continue;
                    ItemInfo item = ToItem(equip.Item);
                    if (item != null)
                        role.Equips[slot] = item;
                }
            }

            if (save.Partners != null)
            {
                for (int i = 0; i < save.Partners.Count; i++)
                {
                    PartnerInfo partner = ToPartner(save.Partners[i]);
                    if (partner != null)
                        role.Partners.Add(partner);
                }
            }

            return role;
        }

        private static ItemSaveV1 ToItemSave(ItemInfo item)
        {
            var save = new ItemSaveV1();
            if (item == null)
                return save;

            save.CfgId = item.CfgId;
            save.Uid = item.Uid;
            save.Num = item.Num;
            save.CreateTime = item.CreateTime;
            save.Type = (int)item.Type;
            save.Quality = item.Equip != null ? (int)item.Equip.Quality : 0;
            save.Attributes = new List<StatSaveV1>();

            if (item.Equip != null && item.Equip.Attributes != null)
            {
                for (int i = 0; i < item.Equip.Attributes.Count; i++)
                {
                    Stats stat = item.Equip.Attributes[i];
                    if (stat == null)
                        continue;
                    save.Attributes.Add(new StatSaveV1
                    {
                        Attr = (int)stat.attr,
                        Num = stat.Num
                    });
                }
            }

            return save;
        }

        private static ItemInfo ToItem(ItemSaveV1 save)
        {
            if (save == null || save.CfgId <= 0 || save.Uid <= 0)
                return null;

            var item = new ItemInfo
            {
                CfgId = save.CfgId,
                Uid = save.Uid,
                Num = save.Num,
                CreateTime = save.CreateTime,
                Type = (ItemType)save.Type,
                Equip = null
            };

            if (save.Quality > 0 || (save.Attributes != null && save.Attributes.Count > 0))
            {
                item.Equip = new EquipInfo
                {
                    Quality = (ItemQuality)save.Quality,
                    Attributes = new List<Stats>()
                };

                if (save.Attributes != null)
                {
                    for (int i = 0; i < save.Attributes.Count; i++)
                    {
                        StatSaveV1 stat = save.Attributes[i];
                        if (stat == null)
                            continue;
                        item.Equip.Attributes.Add(new Stats
                        {
                            attr = (PBAttribute)stat.Attr,
                            Num = stat.Num
                        });
                    }
                }
            }

            return item;
        }

        private static PartnerSaveV1 ToPartnerSave(PartnerInfo partner)
        {
            var save = new PartnerSaveV1();
            if (partner == null)
                return save;

            save.Uid = partner.Uid;
            save.CfgId = partner.CfgId;
            save.Level = partner.Level;
            save.Star = partner.Star;
            save.Skills = new List<SkillSaveV1>();

            if (partner.Skills != null)
            {
                for (int i = 0; i < partner.Skills.Count; i++)
                {
                    SkillInfo skill = partner.Skills[i];
                    if (skill == null)
                        continue;
                    save.Skills.Add(new SkillSaveV1
                    {
                        CfgId = skill.CfgId,
                        Cooldown = skill.Cooldown,
                        Advanced = skill.Advanced
                    });
                }
            }

            return save;
        }

        private static PartnerInfo ToPartner(PartnerSaveV1 save)
        {
            if (save == null || save.CfgId <= 0 || save.Uid <= 0)
                return null;

            var partner = new PartnerInfo
            {
                Uid = save.Uid,
                CfgId = save.CfgId,
                Level = save.Level,
                Star = save.Star,
                Skills = new List<SkillInfo>()
            };

            if (save.Skills != null)
            {
                for (int i = 0; i < save.Skills.Count; i++)
                {
                    SkillSaveV1 skill = save.Skills[i];
                    if (skill == null)
                        continue;
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

        public static long GetUnixSeconds()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
