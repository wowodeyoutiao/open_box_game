using System.Collections.Generic;
using JEngine.Core;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 装备穿戴规则。所有校验都只读，不修改背包或角色数据。
    /// </summary>
    public static class EquipRuleService
    {
        public static bool TryValidateEquip(
            LocalRole role,
            ItemInfo item,
            EquipSlot slot,
            out string error)
        {
            error = "";
            if (role == null)
            {
                error = "没有角色";
                return false;
            }
            if (item == null)
            {
                error = "装备不存在";
                return false;
            }

            int slotValue = (int)slot;
            if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                || slotValue >= (int)EquipSlot.MaxEquipSlot)
            {
                error = "装备部位无效";
                return false;
            }

            CFGItem_Item cfg;
            try
            {
                cfg = CFG_Item.GetById(item.CfgId);
            }
            catch (System.Exception e)
            {
                error = "装备配置不存在";
                Log.PrintWarning("[EquipRule] 装备配置读取失败 cfg=" + item.CfgId + " " + e.Message);
                return false;
            }
            if (cfg == null || ToInt(cfg.Type) != (int)ItemType.EQUIPMENT)
            {
                error = "该物品不是装备";
                return false;
            }

            if (ToInt(cfg.Equiptype) != slotValue)
            {
                error = "装备部位不匹配";
                return false;
            }

            int occupyLimit = ToInt(cfg.Occupylimit);
            if (occupyLimit != 0 && occupyLimit != role.Job)
            {
                error = "职业不符，无法穿戴";
                return false;
            }

            int genderLimit = ToInt(cfg.Genderlimit);
            if (genderLimit != 0 && genderLimit != role.Sex)
            {
                error = "性别不符，无法穿戴";
                return false;
            }

            if (ToInt(cfg.Levellimit) > role.Level)
            {
                error = "等级不足，无法穿戴";
                return false;
            }

            if (item.Equip == null)
            {
                error = "装备数据异常";
                return false;
            }

            return true;
        }

        public static bool TryValidateUnequip(
            LocalRole role,
            EquipSlot slot,
            out ItemInfo item,
            out string error)
        {
            item = null;
            error = "";
            if (role == null)
            {
                error = "没有角色";
                return false;
            }

            int slotValue = (int)slot;
            if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                || slotValue >= (int)EquipSlot.MaxEquipSlot)
            {
                error = "装备部位无效";
                return false;
            }

            if (role.Equips == null
                || !role.Equips.TryGetValue(slot, out item)
                || item == null)
            {
                error = "该部位没有装备";
                return false;
            }

            if (!CanAddToBag(role, item, out error))
                return false;

            return true;
        }

        /// <summary>校验把一件道具放入背包后是否超容量。</summary>
        public static bool CanAddToBag(LocalRole role, ItemInfo item, out string error)
        {
            error = "";
            if (role == null || item == null)
            {
                error = "数据异常";
                return false;
            }

            var bag = role.BagData;
            int currentTotal = bag != null && bag.ItemList != null ? bag.ItemList.Count : 0;
            int currentEquip = 0;
            int currentItem = 0;
            if (bag != null && bag.ItemList != null)
            {
                for (int i = 0; i < bag.ItemList.Count; i++)
                {
                    ItemInfo entry = bag.ItemList[i];
                    if (entry == null)
                        continue;
                    if (IsEquipment(entry))
                        currentEquip++;
                    else
                        currentItem++;
                }
            }

            int totalLimit = 500;
            int equipLimit = 200;
            int itemLimit = 300;
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.BaseValue != null && config.BaseValue.bagNum != null)
                {
                    totalLimit = config.BaseValue.bagNum.totlenum;
                    equipLimit = config.BaseValue.bagNum.equipnum;
                    itemLimit = config.BaseValue.bagNum.itemnum;
                }
            }
            catch
            {
                // 配置异常时使用 xml 默认值。
            }

            if (totalLimit > 0 && currentTotal + 1 > totalLimit)
            {
                error = "背包已满";
                return false;
            }

            if (IsEquipment(item))
            {
                if (equipLimit > 0 && currentEquip + 1 > equipLimit)
                {
                    error = "装备背包已满";
                    return false;
                }
            }
            else if (itemLimit > 0 && currentItem + 1 > itemLimit)
            {
                error = "道具背包已满";
                return false;
            }

            return true;
        }

        public static bool IsEquipment(ItemInfo item)
        {
            if (item == null)
                return false;
            if (item.Type == ItemType.EQUIPMENT)
                return true;
            try
            {
                CFGItem_Item cfg = CFG_Item.GetById(item.CfgId);
                return cfg != null && ToInt(cfg.Type) == (int)ItemType.EQUIPMENT;
            }
            catch
            {
                return false;
            }
        }

        public static int ToInt(decimal? value)
        {
            return value.HasValue ? (int)value.Value : 0;
        }
    }
}
