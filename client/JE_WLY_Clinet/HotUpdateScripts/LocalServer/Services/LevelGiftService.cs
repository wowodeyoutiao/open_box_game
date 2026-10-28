using System.Collections.Generic;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 升级礼包。对齐原服 levelGift：按等级查 levelgift，按职业/性别过滤。
    /// </summary>
    public class LevelGiftService : SingletonTemplate<LevelGiftService>
    {
        public void GrantForLevel(LocalRole role, int level)
        {
            if (role == null)
                return;
            CFGItem_LevelGift cfg = CFG_LevelGift.GetByLevel(level);
            if (cfg == null || cfg.Gift == null)
                return;

            for (int i = 0; i < cfg.Gift.Length; i++)
            {
                CFGItem_LevelGift.GiftItem gift = cfg.Gift[i];
                if (gift.id <= 0 || gift.num <= 0)
                    continue;
                if (gift.job != 0 && gift.job != role.Job)
                    continue;
                if (gift.gender != 0 && gift.gender != role.Sex)
                    continue;

                if (gift.id == 1)
                {
                    role.Coin += gift.num;
                    continue;
                }
                if (gift.id == 14)
                {
                    role.TreasuerBox += gift.num;
                    continue;
                }
                AddItem(role, gift.id, gift.num);
            }
        }

        private static void AddItem(LocalRole role, int cfgId, long num)
        {
            CFGItem_Item cfg;
            try { cfg = CFG_Item.GetById(cfgId); }
            catch { return; }
            if (cfg == null)
                return;

            int type = EquipRuleService.ToInt(cfg.Type);
            if (type == 0)
                return;

            if (role.BagData == null)
                role.BagData = new Bag { ItemList = new List<ItemInfo>() };
            if (role.BagData.ItemList == null)
                role.BagData.ItemList = new List<ItemInfo>();

            if (type != 5)
            {
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    ItemInfo old = role.BagData.ItemList[i];
                    if (old != null && old.CfgId == cfgId && !EquipRuleService.IsEquipment(old))
                    {
                        old.Num += num;
                        return;
                    }
                }
            }

            var item = new ItemInfo
            {
                CfgId = cfgId,
                Uid = LocalData.Instance.AllocateItemUid(),
                Num = num,
                CreateTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Type = (ItemType)type,
            };
            if (type == 5)
            {
                item.Equip = new EquipInfo
                {
                    Quality = ToQuality(EquipRuleService.ToInt(cfg.Quality)),
                    Attributes = new List<Stats>(),
                };
            }

            int count = role.BagData.ItemList.Count;
            var items = new ItemInfo[count + 1];
            for (int i = 0; i < count; i++)
                items[i] = role.BagData.ItemList[i];
            items[count] = item;
            role.BagData.ItemList = System.Linq.Enumerable.ToList(items);
        }

        private static ItemQuality ToQuality(int value)
        {
            if (value < (int)ItemQuality.WHITE) return ItemQuality.WHITE;
            if (value > (int)ItemQuality.Rainbow) return ItemQuality.Rainbow;
            return (ItemQuality)value;
        }
    }
}
