using System;
using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 大厅挂机收益服务。
    ///
    /// 规则对齐服务端 player.go：
    /// - onlinebattle.basetime 为挂机总时长；
    /// - 每 triggertime 秒触发一次 maintrigger 事件；
    /// - type=1 普通奖励，type=2 怪物事件；
    /// - 怪物事件按 monster.Dorp 掉落，并给 Experience；
    /// - 在线和离线共用同一套 Anchor/UsedSec 结算；
    /// - 背包放不下时本次不结算、不消耗时间，等待下次重试。
    /// </summary>
    public class IdleRewardService : SingletonTemplate<IdleRewardService>
    {
        private const long BaseRate = 10000L;
        private const int DefaultBaseTime = 7200;
        private const int DefaultTriggerTime = 10;

        public sealed class IdleSettlementResult
        {
            public bool Applied;
            public bool BagFull;
            public int Events;
            public long Coin;
            public long Exp;
            public long Treasure;
            public int LevelsGained;
            public int NewLevel;
            public bool Mailed;
            public string Error = "";
            public List<ItemInfo> Items = new List<ItemInfo>();
        }

        private sealed class RewardGrant
        {
            public int CfgId;
            public long Num;
        }

        private sealed class RewardBatch
        {
            public long Coin;
            public long Exp;
            public long Treasure;
            public readonly List<RewardGrant> Grants = new List<RewardGrant>();
        }

        public IdleSettlementResult SettleCurrent()
        {
            LocalData data = LocalData.Instance;
            LocalRole role = data == null ? null : data.Current;
            if (role == null)
                return new IdleSettlementResult { Error = "没有角色" };
            return Settle(role, NowUnix(), 0);
        }

        public IdleSettlementResult SettleCurrent(int seed)
        {
            LocalData data = LocalData.Instance;
            LocalRole role = data == null ? null : data.Current;
            if (role == null)
                return new IdleSettlementResult { Error = "没有角色" };
            return Settle(role, NowUnix(), seed);
        }

        public IdleSettlementResult Settle(LocalRole role, long now, int seed)
        {
            var result = new IdleSettlementResult();
            if (role == null)
            {
                result.Error = "没有角色";
                return result;
            }

            LocalPusher.EnsureHomeAFKReset(role);
            int baseTime = GetBaseTime();
            int triggerTime = GetTriggerTime();
            if (role.HomeAFKAnchorUnix <= 0)
            {
                role.HomeAFKAnchorUnix = now;
                SaveCoordinator.Instance.MarkDirty();
                return result;
            }

            long remaining = baseTime - role.HomeAFKUsedSec;
            if (remaining <= 0)
            {
                role.HomeAFKAnchorUnix = now;
                SaveCoordinator.Instance.MarkDirty();
                return result;
            }

            long elapsed = now - role.HomeAFKAnchorUnix;
            if (elapsed < triggerTime)
                return result;

            long available = System.Math.Min(elapsed, remaining);
            int events = (int)(available / triggerTime);
            if (events <= 0)
                return result;

            var batch = new RewardBatch();
            var rng = seed == 0
                ? new System.Random(unchecked((int)NowUnix()) ^ role.Uid.GetHashCode())
                : new System.Random(seed);

            for (int i = 0; i < events; i++)
                RollEvent(role, batch, rng);

            bool mailed = false;
            if (!CanAccept(role, batch, out string bagError))
            {
                // 原服 checkAddSourceBatch 失败后转系统邮件；货币/经验仍正常入账。
                List<MailService.MailAttachment> attachments = BuildMailAttachments(batch);
                if (attachments.Count == 0
                    || !MailService.Instance.SendSystemMail(role, "背包已满，挂机奖励转邮件", attachments))
                {
                    result.BagFull = true;
                    result.Error = bagError;
                    return result;
                }
                mailed = true;
                result.BagFull = true;
                result.Error = "背包已满，奖励已转邮件";
            }

            role.Coin += batch.Coin;
            LevelUpService.AddExpResult expResult = LevelUpService.Instance.AddExp(role, batch.Exp);
            role.TreasuerBox += batch.Treasure;
            if (!mailed)
                ApplyItems(role, batch, result.Items);
            if (expResult.LevelsGained > 0)
                RoleStatService.Instance.Recalculate(role, true);

            long consumed = (long)events * triggerTime;
            role.HomeAFKUsedSec += consumed;
            role.HomeAFKAnchorUnix += consumed;
            if (role.HomeAFKUsedSec >= baseTime)
            {
                role.HomeAFKUsedSec = baseTime;
                role.HomeAFKAnchorUnix = now;
            }

            LocalData data = LocalData.Instance;
            if (data != null && data.Current == role)
            {
                data.Gold = role.Coin;
                data.Exp = role.Exp;
                data.Level = role.Level;
                data.TreasuerBox = role.TreasuerBox;
            }

            SaveCoordinator.Instance.MarkDirty();
            result.Applied = true;
            result.Events = events;
            result.Coin = batch.Coin;
            result.Exp = batch.Exp;
            result.Treasure = batch.Treasure;
            result.LevelsGained = expResult.LevelsGained;
            result.NewLevel = role.Level;
            result.Mailed = mailed;
            Log.Print("[IdleReward] events=" + events + " coin=" + batch.Coin
                + " exp=" + batch.Exp + " treasure=" + batch.Treasure
                + " items=" + result.Items.Count + " afkUsed=" + role.HomeAFKUsedSec
                + " level=" + role.Level + " levelsGained=" + expResult.LevelsGained
                + " mailed=" + mailed);
            return result;
        }

        private static int GetBaseTime()
        {
            try
            {
                if (CFG_XMLConfig.xmlConfig != null
                    && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle.baseTime > 0)
                    return CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle.baseTime;
            }
            catch { }
            return DefaultBaseTime;
        }

        private static int GetTriggerTime()
        {
            try
            {
                if (CFG_XMLConfig.xmlConfig != null
                    && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle.triggerTime > 0)
                    return CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle.triggerTime;
            }
            catch { }
            return DefaultTriggerTime;
        }

        private static void RollEvent(LocalRole role, RewardBatch batch, System.Random rng)
        {
            CFGItem_MainTrigger trigger = CFG_MainTrigger.GetByLevel(role.Level);
            if (trigger == null || trigger.MonsterIds == null || trigger.MonsterIds.Length == 0)
                return;

            int totalRate = 0;
            for (int i = 0; i < trigger.MonsterIds.Length; i++)
                totalRate += trigger.MonsterIds[i].rate > 0 ? trigger.MonsterIds[i].rate : 0;
            if (totalRate <= 0)
                return;

            int roll = rng.Next(totalRate);
            int cursor = 0;
            CFGItem_MainTrigger.MonsterWeight picked = default(CFGItem_MainTrigger.MonsterWeight);
            for (int i = 0; i < trigger.MonsterIds.Length; i++)
            {
                int rate = trigger.MonsterIds[i].rate > 0 ? trigger.MonsterIds[i].rate : 0;
                if (rate <= 0)
                    continue;
                cursor += rate;
                if (roll < cursor)
                {
                    picked = trigger.MonsterIds[i];
                    break;
                }
            }

            if (picked.type == 1)
            {
                AddGrant(batch, picked.id, picked.num > 0 ? picked.num : 1);
            }
            else if (picked.type == 2)
            {
                int count = picked.num > 0 ? picked.num : 1;
                for (int i = 0; i < count; i++)
                    AddMonsterReward(batch, picked.id, rng);
            }
        }

        private static void AddMonsterReward(RewardBatch batch, int monsterId, System.Random rng)
        {
            CFGItem_Monster monster;
            try
            {
                monster = CFG_Monster.GetById(monsterId);
            }
            catch
            {
                return;
            }
            if (monster == null)
                return;

            if (monster.Experience > 0)
                batch.Exp += monster.Experience;

            if (monster.Dorp == null)
                return;
            for (int i = 0; i < monster.Dorp.Length; i++)
            {
                CFGItem_Monster.DorpItem drop = monster.Dorp[i];
                if (drop == null || drop.id == null || drop.id.Length == 0)
                    continue;
                if (drop.ratio <= 0)
                    continue;

                if (rng.Next(10000) >= drop.ratio)
                    continue;

                int index = drop.id.Length <= 1 ? 0 : rng.Next(drop.id.Length);
                int itemId = drop.id[index];
                int min = drop.num == null ? 1 : drop.num.min;
                int max = drop.num == null ? min : drop.num.max;
                if (max < min)
                    max = min;
                int amount = max <= min ? min : min + rng.Next(max - min);
                AddGrant(batch, itemId, amount);
            }
        }

        private static void AddGrant(RewardBatch batch, int cfgId, long num)
        {
            if (cfgId <= 0 || num <= 0)
                return;
            if (cfgId == 1)
            {
                batch.Coin += num;
                return;
            }
            if (cfgId == 14)
            {
                batch.Treasure += num;
                return;
            }
            if (cfgId == 2)
            {
                batch.Exp += num;
                return;
            }

            for (int i = 0; i < batch.Grants.Count; i++)
            {
                if (batch.Grants[i].CfgId == cfgId)
                {
                    batch.Grants[i].Num += num;
                    return;
                }
            }
            batch.Grants.Add(new RewardGrant { CfgId = cfgId, Num = num });
        }

        private static List<MailService.MailAttachment> BuildMailAttachments(RewardBatch batch)
        {
            var attachments = new List<MailService.MailAttachment>();
            for (int i = 0; i < batch.Grants.Count; i++)
            {
                RewardGrant grant = batch.Grants[i];
                if (grant.CfgId == 1 || grant.CfgId == 2 || grant.CfgId == 14)
                    continue;
                CFGItem_Item cfg;
                try { cfg = CFG_Item.GetById(grant.CfgId); }
                catch { continue; }
                if (cfg == null || EquipRuleService.ToInt(cfg.Type) == 0)
                    continue;
                attachments.Add(new MailService.MailAttachment
                {
                    CfgId = grant.CfgId,
                    Num = grant.Num,
                });
            }
            return attachments;
        }

        private static bool CanAccept(LocalRole role, RewardBatch batch, out string error)
        {
            error = "";
            int equipLimit = 200;
            int itemLimit = 300;
            int totalLimit = 500;
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
            catch { }

            int currentTotal = role.BagData == null || role.BagData.ItemList == null
                ? 0 : role.BagData.ItemList.Count;
            int currentEquip = 0;
            int currentItem = 0;
            if (role.BagData != null && role.BagData.ItemList != null)
            {
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    ItemInfo item = role.BagData.ItemList[i];
                    if (item == null) continue;
                    if (EquipRuleService.IsEquipment(item)) currentEquip++;
                    else currentItem++;
                }
            }

            int addEquip = 0;
            int addItem = 0;
            for (int i = 0; i < batch.Grants.Count; i++)
            {
                CFGItem_Item cfg;
                try { cfg = CFG_Item.GetById(batch.Grants[i].CfgId); }
                catch { continue; }
                if (cfg == null || EquipRuleService.ToInt(cfg.Type) == 0)
                    continue;
                if (EquipRuleService.ToInt(cfg.Type) == 5)
                    addEquip++;
                else if (!HasStackRoom(role, batch.Grants[i].CfgId))
                    addItem++;
            }

            if (totalLimit > 0 && currentTotal + addEquip + addItem > totalLimit)
            {
                error = "背包已满";
                return false;
            }
            if (equipLimit > 0 && currentEquip + addEquip > equipLimit)
            {
                error = "装备背包已满";
                return false;
            }
            if (itemLimit > 0 && currentItem + addItem > itemLimit)
            {
                error = "道具背包已满";
                return false;
            }
            return true;
        }

        private static bool HasStackRoom(LocalRole role, int cfgId)
        {
            if (role.BagData == null || role.BagData.ItemList == null)
                return false;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item != null && item.CfgId == cfgId && !EquipRuleService.IsEquipment(item))
                    return true;
            }
            return false;
        }

        private static void ApplyItems(LocalRole role, RewardBatch batch, List<ItemInfo> touched)
        {
            if (batch.Grants.Count == 0)
                return;
            if (role.BagData == null)
                role.BagData = new Bag { ItemList = new List<ItemInfo>() };
            if (role.BagData.ItemList == null)
                role.BagData.ItemList = new List<ItemInfo>();

            var newSlots = new ItemInfo[batch.Grants.Count];
            int newCount = 0;
            for (int i = 0; i < batch.Grants.Count; i++)
            {
                RewardGrant grant = batch.Grants[i];
                CFGItem_Item cfg;
                try { cfg = CFG_Item.GetById(grant.CfgId); }
                catch { continue; }
                if (cfg == null)
                    continue;

                int type = EquipRuleService.ToInt(cfg.Type);
                if (type == 0)
                    continue;

                if (type == 5)
                {
                    ItemInfo equip = CreateItem(cfg, 1);
                    newSlots[newCount++] = equip;
                    touched.Add(equip);
                    continue;
                }

                ItemInfo existing = FindStack(role, grant.CfgId);
                if (existing != null)
                {
                    existing.Num += grant.Num;
                    touched.Add(existing);
                }
                else
                {
                    ItemInfo created = CreateItem(cfg, grant.Num);
                    newSlots[newCount++] = created;
                    touched.Add(created);
                }
            }

            if (newCount == 0)
                return;

            int oldCount = role.BagData.ItemList.Count;
            var combined = new ItemInfo[oldCount + newCount];
            for (int i = 0; i < oldCount; i++)
                combined[i] = role.BagData.ItemList[i];
            for (int i = 0; i < newCount; i++)
                combined[oldCount + i] = newSlots[i];
            role.BagData.ItemList = System.Linq.Enumerable.ToList(combined);
        }

        private static ItemInfo FindStack(LocalRole role, int cfgId)
        {
            if (role.BagData == null || role.BagData.ItemList == null)
                return null;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item != null && item.CfgId == cfgId && !EquipRuleService.IsEquipment(item))
                    return item;
            }
            return null;
        }

        private static ItemInfo CreateItem(CFGItem_Item cfg, long num)
        {
            int type = EquipRuleService.ToInt(cfg.Type);
            var item = new ItemInfo
            {
                CfgId = cfg.ID,
                Uid = LocalData.Instance.AllocateItemUid(),
                Num = num,
                CreateTime = NowUnix(),
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
            return item;
        }

        private static ItemQuality ToQuality(int value)
        {
            if (value < (int)ItemQuality.WHITE) return ItemQuality.WHITE;
            if (value > (int)ItemQuality.Rainbow) return ItemQuality.Rainbow;
            return (ItemQuality)value;
        }

        private static long NowUnix()
        {
            return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
