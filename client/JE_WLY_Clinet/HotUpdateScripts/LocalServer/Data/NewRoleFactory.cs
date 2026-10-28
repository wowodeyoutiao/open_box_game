using System;
using System.Collections.Generic;
using JEngine.Core;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 唯一的新角色创建工厂。
    ///
    /// 默认角色、正式建角、后续调试建档都必须走这里，禁止再在 LocalData 或
    /// PlayerService 中各写一套初始字段。
    /// </summary>
    public static class NewRoleFactory
    {
        public static LocalRole Create(
            long roleUid,
            string name,
            int job,
            int sex,
            ref long nextItemUid,
            out string error)
        {
            error = "";

            if (roleUid <= 0)
            {
                error = "角色 UID 无效";
                return null;
            }
            if (string.IsNullOrEmpty(name))
            {
                error = "角色名为空";
                return null;
            }
            if (sex != 1 && sex != 2)
            {
                error = "性别参数无效";
                return null;
            }

            int[] starterItems;
            if (!StarterLoadoutConfig.TryGet(job, out starterItems))
            {
                error = "未知职业: " + job;
                return null;
            }

            int initialEnergy = 100;
            try
            {
                if (CFG_XMLConfig.xmlConfig != null
                    && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.energy != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.energy.fullValue > 0)
                    initialEnergy = CFG_XMLConfig.xmlConfig.BaseValue.energy.fullValue;
            }
            catch
            {
                // 配置未就绪时使用保守默认值。
            }

            var role = new LocalRole
            {
                Uid = roleUid,
                Name = name,
                Job = job,
                Sex = sex,
                Level = 1,
                Exp = 0,
                Energy = initialEnergy,
                BagData = new Bag { ItemList = new List<ItemInfo>() },
                Partners = new List<PartnerInfo>(),
                Equips = new Dictionary<EquipSlot, ItemInfo>()
            };

            for (int i = 0; i < starterItems.Length; i++)
            {
                int cfgId = starterItems[i];
                string itemError;
                ItemInfo item = CreateStarterItem(cfgId, nextItemUid, out itemError);
                if (item == null)
                {
                    error = "初始化装备失败: " + itemError;
                    return null;
                }

                nextItemUid++;

                EquipSlot slot = (EquipSlot)ToInt(CFG_Item.GetById(cfgId).Equiptype);
                int slotValue = (int)slot;
                if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                    || slotValue >= (int)EquipSlot.MaxEquipSlot)
                {
                    error = "装备部位无效: cfg=" + cfgId + " slot=" + (int)slot;
                    return null;
                }
                if (role.Equips.ContainsKey(slot))
                {
                    error = "新手装备部位重复: cfg=" + cfgId + " slot=" + (int)slot;
                    return null;
                }

                role.Equips[slot] = item;
            }

            // 新手伙伴：按玩家职业送对应的低品质伙伴（11/12/13 = 1/2/3 + 10）。
            // 正式入口应该是召唤（module 13），召唤系统未做前先用这里保证伙伴链路可玩。
            PartnerService.Instance.EnsureSlots(role);
            int starterPartner = GetStarterPartnerCfgId(job);
            if (starterPartner > 0)
                PartnerService.Instance.AddPartner(role, starterPartner);

            // 自动分解出厂开关（config.xml /Config/autodecompose/key）
            ItemService.ApplyAutoDisassemblyDefaults(role);

            Log.Print("[NewRoleFactory] 创建角色 uid=" + roleUid
                + " name=" + name
                + " job=" + StarterLoadoutConfig.GetJobName(job)
                + " equips=" + role.Equips.Count
                + " partners=" + role.Partners.Count);
            return role;
        }

        /// <summary>
        /// 新手伙伴配置 ID。伙伴 job 是玩家 job + 10，选品质 6 那组：
        /// 10(11 战士) / 13(12 法师) / 16(13 祭祀)。
        ///
        /// 品质 2/3/4 的伙伴在 partner.json 里 Skills 为 null（纯属性，不放技能），
        /// 品质 6 起才有技能，所以新手直接给品质 6，保证伙伴技能链路可见。
        /// </summary>
        private static int GetStarterPartnerCfgId(int job)
        {
            if (job == 2) return 13;
            if (job == 3) return 16;
            return 10;
        }

        private static ItemInfo CreateStarterItem(int cfgId, long uid, out string error)
        {
            error = "";
            CFGItem_Item cfg;
            try
            {
                cfg = CFG_Item.GetById(cfgId);
            }
            catch (Exception e)
            {
                error = "配置不存在 cfg=" + cfgId + " " + e.Message;
                return null;
            }

            if (cfg == null || ToInt(cfg.Type) != (int)ItemType.EQUIPMENT)
            {
                error = "配置不是装备 cfg=" + cfgId;
                return null;
            }

            // 装备基础属性属于配置，实例 Attributes 只存随机/极品属性。
            // 若把基础属性复制进实例，会被 RoleStatService 再按配置计算一次，造成翻倍。
            var equip = new EquipInfo
            {
                Quality = ToQuality(cfg.Quality),
                Attributes = new List<Stats>()
            };

            return new ItemInfo
            {
                CfgId = cfgId,
                Uid = uid,
                Num = 1,
                CreateTime = 0,
                Type = ItemType.EQUIPMENT,
                Equip = equip
            };
        }

        private static int ToInt(decimal? value)
        {
            return value.HasValue ? (int)value.Value : 0;
        }

        private static ItemQuality ToQuality(decimal? quality)
        {
            int value = ToInt(quality);
            if (value <= 0)
                value = 1;
            if (value < (int)ItemQuality.WHITE)
                return ItemQuality.WHITE;
            if (value > (int)ItemQuality.Rainbow)
                return ItemQuality.Rainbow;
            return (ItemQuality)value;
        }
    }
}
