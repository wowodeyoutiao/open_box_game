using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 装备强化 / 升星。
    ///
    /// 对齐原服 Player.enhance：
    ///   1) 取该部位当前强化等级（**强化跟槽位走，不跟装备实例走**）
    ///   2) 上限 = 角色等级 × config.xml 的 equipstrenghthen.rate
    ///   3) 用「当前强化等级」查 equipstrengthen 配置，拿本次消耗
    ///   4) 校验并扣材料，Enhance++
    ///   5) 派发 ANY_EQUIP_STRENGTHENED_TO_LEVEL，重算属性
    ///
    /// 强化属性在 RoleStatService.AddEquipment 里结算。
    /// 升星本轮未做（star 有成功率/降级，需要单独对齐）。
    /// </summary>
    public class EquipService : SingletonTemplate<EquipService>
    {
        private const int DefaultEnhanceRate = 1;

        /// <summary>装备强化。</summary>
        public EnhanceResponse Enhance(EnhanceRequest req)
        {
            var response = new EnhanceResponse
            {
                Code = -1,
                Slot = req != null ? req.Slot : EquipSlot.INVALID_EquipSlot,
                PartnerSlotId = req != null ? req.PartnerSlotId : 0,
            };

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "没有角色";
                return response;
            }

            // 原服 Manager.Enhance 先校验槽位合法性
            int slotValue = (int)response.Slot;
            if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                || slotValue >= (int)EquipSlot.MaxEquipSlot)
            {
                response.Error = "装备槽位不合法";
                return response;
            }
            if (response.PartnerSlotId != 0)
            {
                response.Error = "单机版暂不支持伙伴装备强化";
                return response;
            }

            // 槽位要有装备才强化（原服 Slot 结构常驻，这里加保护，避免空槽浪费材料）
            ItemInfo equipped;
            if (role.Equips == null || !role.Equips.TryGetValue(response.Slot, out equipped)
                || equipped == null)
            {
                response.Error = "该部位没有装备";
                return response;
            }

            int current = role.GetEquipEnhance(response.Slot);
            // 上限：角色等级 × rate
            int maxLevel = role.Level * GetEnhanceRate();
            if (current >= maxLevel)
            {
                response.Error = "已达最高强化等级";
                response.NewLevel = current;
                return response;
            }

            CFGItem_EquipStrengthen cfg = null;
            try { cfg = CFG_EquipStrengthen.GetByJobAndLv(role.Job, current); }
            catch { cfg = null; }
            if (cfg == null)
            {
                response.Error = "强化配置不存在";
                response.NewLevel = current;
                return response;
            }

            // 校验材料
            if (cfg.Consume != null)
            {
                for (int i = 0; i < cfg.Consume.Length; i++)
                {
                    CFGItem_EquipStrengthen.Consume_Item cost = cfg.Consume[i];
                    if (cost.id <= 0 || cost.num <= 0)
                        continue;
                    if (ItemService.CountItem(role, cost.id) < cost.num)
                    {
                        response.Error = "强化材料不足";
                        response.NewLevel = current;
                        return response;
                    }
                }
            }

            // 扣材料
            if (cfg.Consume != null)
            {
                for (int i = 0; i < cfg.Consume.Length; i++)
                {
                    CFGItem_EquipStrengthen.Consume_Item cost = cfg.Consume[i];
                    if (cost.id <= 0 || cost.num <= 0)
                        continue;
                    ItemService.ConsumeItem(role, cost.id, cost.num);
                }
            }

            if (role.EquipEnhance == null)
                role.EquipEnhance = new Dictionary<EquipSlot, int>();
            role.EquipEnhance[response.Slot] = current + 1;

            RoleStatService.Instance.Recalculate(role, false);
            TaskService.Instance.Dispatch(role, TargetType.ANY_EQUIP_STRENGTHENED_TO_LEVEL,
                current + 1, 1);

            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushStatsNow();
            LocalPusher.Instance.PushEquipEnhanceNow(response.Slot, current + 1);

            response.Code = 0;
            response.NewLevel = current + 1;
            Log.Print("[EquipService] enhance slot=" + slotValue + " " + current + " -> "
                + response.NewLevel);
            return response;
        }

        /// <summary>
        /// 测试钩子：>= 0 时用固定权重代替随机数，用来稳定验证升星成功/失败两条分支。
        /// 正常运行保持 -1。
        /// </summary>
        public int ForcedStarWeight = -1;

        private readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 装备升星，对齐原服 Player.stars：
        ///
        ///   1) 上限：Stars >= 配置最大星级 → ErrMaxLevel
        ///   2) uuid = job*100 + Stars（用**当前**星级查配置）
        ///   3) 校验并扣 MaterialID
        ///   4) weight = RandIn(10000)
        ///      成功条件：weight &lt; SuccessRate × (1 + EQUIP_STAR_PERCENTAGE / 10000)
        ///   5) 成功 Stars++，失败 **Stars = FailLevel**（不是减 1，是掉到配置指定星级）
        ///   6) 无论成败都 refreshStats（失败掉星也会掉属性）
        ///
        /// 注意：材料在判定前就扣掉了，失败不退。
        /// </summary>
        public StarsResponse Stars(StarsRequest req)
        {
            var response = new StarsResponse
            {
                Code = -1,
                Slot = req != null ? req.Slot : EquipSlot.INVALID_EquipSlot,
                PartnerSlotId = req != null ? req.PartnerSlotId : 0,
            };

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "没有角色";
                return response;
            }

            int slotValue = (int)response.Slot;
            if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                || slotValue >= (int)EquipSlot.MaxEquipSlot)
            {
                response.Error = "装备槽位不合法";
                return response;
            }
            if (response.PartnerSlotId != 0)
            {
                response.Error = "单机版暂不支持伙伴装备升星";
                return response;
            }

            ItemInfo equipped;
            if (role.Equips == null || !role.Equips.TryGetValue(response.Slot, out equipped)
                || equipped == null)
            {
                response.Error = "该部位没有装备";
                return response;
            }

            int stars = role.GetEquipStars(response.Slot);
            int maxStar = GetMaxStar(role.Job);
            if (stars >= maxStar)
            {
                response.Error = "已达最高星级";
                response.NewLevel = stars;
                return response;
            }

            CFGItem_EquipStar cfg = null;
            try { cfg = CFG_EquipStar.GetByJobAndLv(role.Job, stars); }
            catch { cfg = null; }
            if (cfg == null)
            {
                response.Error = "升星配置不存在";
                response.NewLevel = stars;
                return response;
            }

            // 校验材料（失败不退材料，所以这里先校验再扣）
            if (cfg.MaterialID != null)
            {
                for (int i = 0; i < cfg.MaterialID.Length; i++)
                {
                    CFGItem_EquipStar.Material_Item cost = cfg.MaterialID[i];
                    if (cost.id <= 0 || cost.num <= 0)
                        continue;
                    if (ItemService.CountItem(role, cost.id) < cost.num)
                    {
                        response.Error = "升星材料不足";
                        response.NewLevel = stars;
                        return response;
                    }
                }
            }

            if (cfg.MaterialID != null)
            {
                for (int i = 0; i < cfg.MaterialID.Length; i++)
                {
                    CFGItem_EquipStar.Material_Item cost = cfg.MaterialID[i];
                    if (cost.id <= 0 || cost.num <= 0)
                        continue;
                    ItemService.ConsumeItem(role, cost.id, cost.num);
                }
            }

            // 成功率判定
            int weight = ForcedStarWeight >= 0 ? ForcedStarWeight : _rng.Next(10000);
            int starPercent = GetEquipStarPercent();
            // 原服：weight < SuccessRate * (1 + EQUIP_STAR_PERCENTAGE / BaseRate)
            double threshold = (double)cfg.SuccessRate * (1.0 + starPercent / 10000.0);
            bool success = (double)weight < threshold;

            int newStars;
            if (success)
            {
                newStars = stars + 1;
                TaskService.Instance.Dispatch(role, TargetType.ANY_EQUIP_STAR_UP_TIMES, 0, 1);
            }
            else
            {
                // 原服失败是「掉到 FailLevel」，不是「掉 1 星」
                newStars = cfg.FailLevel;
                if (newStars < 0)
                    newStars = 0;
            }

            if (role.EquipStars == null)
                role.EquipStars = new Dictionary<EquipSlot, int>();
            role.EquipStars[response.Slot] = newStars;

            // 无论成败都重算（失败掉星会掉属性）
            RoleStatService.Instance.Recalculate(role, false);

            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushStatsNow();
            LocalPusher.Instance.PushEquipStarNow(response.Slot, newStars);

            response.Code = 0;
            response.NewLevel = newStars;
            Log.Print("[EquipService] stars slot=" + slotValue + " " + stars + " -> " + newStars
                + " weight=" + weight + "/" + threshold + " success=" + success);
            return response;
        }

        /// <summary>配置里的最高星级（对齐原服 GlobalCfg.Equipstar.Maxlevel，取配置条数 - 1）。</summary>
        private static int GetMaxStar(int job)
        {
            try { return CFG_EquipStar.GetMaxStar(job); }
            catch { return 0; }
        }

        /// <summary>升星成功率加成属性（EQUIP_STAR_PERCENTAGE，万分比）。</summary>
        private static int GetEquipStarPercent()
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return 0;
            RoleStatSnapshot stats = RoleStatService.Instance.Recalculate(role, false);
            return stats == null ? 0 : stats.Get(Attribute.EQUIP_STAR_PERCENTAGE);
        }

        /// <summary>强化上限倍率（config.xml 的 equipstrenghthen.rate）。</summary>
        private static int GetEnhanceRate()
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.BaseValue != null
                    && config.BaseValue.equipStrenghthen > 0)
                    return config.BaseValue.equipStrenghthen;
            }
            catch { }
            return DefaultEnhanceRate;
        }
    }
}
