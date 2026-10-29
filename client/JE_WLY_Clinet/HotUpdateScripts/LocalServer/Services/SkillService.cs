using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 角色技能自动学习。
    ///
    /// 对齐原服 player.go skillAutoLearn：
    /// - 只学习当前职业、角色等级达到 RoleLevellimit、SkillLevel <= 1 的技能；
    /// - 同一 SkillID 只保留最高/第一条，已学习跳过；
    /// - 主动技能优先放入空槽；特殊技能放 0 号槽，其他从 1 号槽开始；
    /// - 学习成功后推送 NewSkillPush。
    /// </summary>
    public class SkillService : SingletonTemplate<SkillService>
    {
        public int AutoLearn(LocalRole role)
        {
            return AutoLearn(role, true);
        }

        public int AutoLearn(LocalRole role, bool notify)
        {
            if (role == null)
                return 0;
            if (role.Skills == null)
                role.Skills = new List<SkillInfo>();
            if (role.SkillSlots == null || role.SkillSlots.Length != 13)
            {
                int[] slots = new int[13];
                if (role.SkillSlots != null)
                {
                    for (int i = 0; i < 13 && i < role.SkillSlots.Length; i++)
                        slots[i] = role.SkillSlots[i];
                }
                role.SkillSlots = slots;
            }

            int learned = 0;
            CFG_Skill table = CFG_Skill.Get();
            if (table == null || table.DicDatas == null)
                return 0;

            foreach (KeyValuePair<long, CFGItem_Skill> kv in table.DicDatas)
            {
                CFGItem_Skill cfg = kv.Value;
                if (cfg == null || cfg.SkillProfession != role.Job)
                    continue;
                if (cfg.SkillLevel > 1)
                    continue;
                if (HasSkillId(role, cfg.SkillID))
                    continue;

                decimal levelLimit;
                try
                {
                    levelLimit = cfg.RoleLevellimit;
                    if (levelLimit > role.Level)
                        continue;
                }
                catch
                {
                    // 原配置里 RoleLevellimit 存在 null，ILRuntime 下 decimal 比较会抛异常。
                    continue;
                }

                var skill = new SkillInfo
                {
                    CfgId = cfg.UUID,
                    Cooldown = 0,
                    Advanced = 0,
                };
                role.Skills.Add(skill);
                learned++;

                int activeOrPassive = 0;
                try { activeOrPassive = (int)cfg.ActiveorPassive; }
                catch { }
                // 原服 PassiveSkill = 2；只有主动技能会自动装槽。
                if (cfg.SkillLevel > 0 && activeOrPassive != 2)
                    EquipSkill(role, cfg);

                if (notify && LocalData.Instance.Current == role)
                    LocalPusher.Instance.PushNewSkillNow(skill);
                TaskService.Instance.Dispatch(role, TargetType.SKILL_UPGRADING_REQUIREMENT,
                    cfg.UUID, cfg.SkillLevel);
            }

            if (learned > 0)
                SaveCoordinator.Instance.MarkDirty();
            if (notify && LocalData.Instance.Current == role)
                LocalPusher.Instance.PushSkillSlotNow();
            return learned;
        }

        private static bool HasSkillId(LocalRole role, int skillId)
        {
            if (role.Skills == null)
                return false;
            for (int i = 0; i < role.Skills.Count; i++)
            {
                SkillInfo skill = role.Skills[i];
                if (skill == null) continue;
                try
                {
                    CFGItem_Skill cfg = CFG_Skill.GetById(skill.CfgId);
                    if (cfg != null && cfg.SkillID == skillId)
                        return true;
                }
                catch { }
            }
            return false;
        }

        public SkillUpgradeResponse Upgrade(int skillId, long partnerUid)
        {
            var response = new SkillUpgradeResponse
            {
                Code = -1,
                PartnerUid = partnerUid,
                OldSkillId = skillId,
                NewSkillId = skillId,
            };

            LocalRole role = LocalData.Instance.Current;
            SkillInfo owned = FindSkill(role, skillId, partnerUid);
            if (owned == null)
            {
                response.Error = "技能未学习";
                return response;
            }

            CFGItem_Skill cfg = GetCfg(skillId);
            if (cfg == null)
            {
                response.Error = "技能配置不存在";
                return response;
            }

            int maxLevel = CountSkillLevels(cfg.SkillProfession, cfg.SkillID);
            if (cfg.SkillLevel >= maxLevel)
            {
                response.Error = "技能已满级";
                return response;
            }

            CFGItem_Skill next = FindNextLevel(cfg.SkillProfession, cfg.SkillID, cfg.SkillLevel + 1);
            if (next == null)
            {
                response.Error = "下一级技能配置不存在";
                return response;
            }

            if (!CanConsume(role, cfg.PromoteConsume))
            {
                response.Error = "升级材料不足";
                return response;
            }
            Consume(role, cfg.PromoteConsume);

            int oldCfgId = owned.CfgId;
            owned.CfgId = next.UUID;
            // 伙伴技能没有「技能槽」概念（原服存在 partner.skills 列表里），只换 CfgId
            if (partnerUid == 0 && role.SkillSlots != null)
            {
                for (int i = 0; i < role.SkillSlots.Length; i++)
                {
                    if (role.SkillSlots[i] == oldCfgId)
                    {
                        role.SkillSlots[i] = next.UUID;
                        break;
                    }
                }
            }

            AfterSkillChanged(role);
            TaskService.Instance.Dispatch(role, TargetType.SKILL_UPGRADING_TIMES, 0, 1);
            TaskService.Instance.Dispatch(role, TargetType.SKILL_UPGRADING_REQUIREMENT,
                next.UUID, next.SkillLevel);
            response.Code = 0;
            response.NewSkillId = next.UUID;
            return response;
        }

        public SkillAdvancedResponse Advance(int skillId, long partnerUid)
        {
            var response = new SkillAdvancedResponse
            {
                Code = -1,
                PartnerUid = partnerUid,
                OldSkillId = skillId,
            };

            LocalRole role = LocalData.Instance.Current;
            SkillInfo owned = FindSkill(role, skillId, partnerUid);
            if (owned == null)
            {
                response.Error = "技能未学习";
                return response;
            }

            CFGItem_Skill cfg = GetCfg(skillId);
            if (cfg == null)
            {
                response.Error = "技能配置不存在";
                return response;
            }
            if (cfg.CanupperLevel <= 0 || owned.Advanced >= (int)cfg.CanupperLevel)
            {
                response.Error = "技能进阶已满级";
                response.NewLevel = owned.Advanced;
                return response;
            }

            CFGItem_Skill needCfg = FindAdvancedCfg(cfg, owned.Advanced + 1);
            if (needCfg == null)
            {
                response.Error = "进阶配置不存在";
                response.NewLevel = owned.Advanced;
                return response;
            }
            if (!CanConsume(role, needCfg.UpperlevelConsume))
            {
                response.Error = "进阶材料不足";
                response.NewLevel = owned.Advanced;
                return response;
            }
            Consume(role, needCfg.UpperlevelConsume);

            owned.Advanced++;
            AfterSkillChanged(role);
            response.Code = 0;
            response.NewLevel = owned.Advanced;
            return response;
        }

        public SkillChangeResponse Change(int opType, SkillsSlot slot, int skillId)
        {
            var response = new SkillChangeResponse
            {
                Code = -1,
                OpType = opType,
                Slot = slot,
                SkillId = skillId,
            };
            LocalRole role = LocalData.Instance.Current;
            SkillInfo owned = FindRoleSkill(role, skillId);
            if (owned == null)
            {
                response.Error = "技能未学习";
                return response;
            }

            int slotIndex = (int)slot;
            if (slotIndex < 0 || slotIndex >= 13)
            {
                response.Error = "技能槽位非法";
                return response;
            }
            CFGItem_Skill cfg = GetCfg(skillId);
            if (cfg == null)
            {
                response.Error = "技能配置不存在";
                return response;
            }
            if (slotIndex == 0 && cfg.Skilltype != 3)
            {
                response.Error = "只有特殊技能可以装备到 0 槽";
                return response;
            }
            if (role.SkillSlots == null || role.SkillSlots.Length != 13)
                role.SkillSlots = new int[13];

            if (opType == 1)
                role.SkillSlots[slotIndex] = skillId;
            else if (opType == 2)
                role.SkillSlots[slotIndex] = 0;
            else
            {
                response.Error = "技能槽操作类型非法";
                return response;
            }

            AfterSkillChanged(role);
            response.Code = 0;
            return response;
        }

        private static SkillInfo FindRoleSkill(LocalRole role, int skillId)
        {
            if (role == null || role.Skills == null)
                return null;
            for (int i = 0; i < role.Skills.Count; i++)
            {
                if (role.Skills[i] != null && role.Skills[i].CfgId == skillId)
                    return role.Skills[i];
            }
            return null;
        }

        private static PartnerInfo FindPartner(LocalRole role, long uid)
        {
            if (role == null || role.Partners == null)
                return null;
            for (int i = 0; i < role.Partners.Count; i++)
            {
                PartnerInfo partner = role.Partners[i];
                if (partner != null && partner.Uid == uid)
                    return partner;
            }
            return null;
        }

        /// <summary>
        /// 按 partnerUid 找技能：0 = 玩家自己的技能，否则 = 该伙伴的技能表。
        /// 对齐原服 skillsUpgrade/skillsAdvanced 的 paruid 分支
        /// （player.go:5589-5607，伙伴存在 partner.skills 里，匹配口径一致）。
        /// </summary>
        private static SkillInfo FindSkill(LocalRole role, int skillId, long partnerUid)
        {
            if (partnerUid == 0)
                return FindRoleSkill(role, skillId);

            PartnerInfo partner = FindPartner(role, partnerUid);
            if (partner == null || partner.Skills == null)
                return null;
            for (int i = 0; i < partner.Skills.Count; i++)
            {
                SkillInfo skill = partner.Skills[i];
                if (skill != null && skill.CfgId == skillId)
                    return skill;
            }
            return null;
        }

        private static CFGItem_Skill GetCfg(int skillId)
        {
            try { return CFG_Skill.GetById(skillId); }
            catch { return null; }
        }

        private static int CountSkillLevels(int job, int skillId)
        {
            int count = 0;
            CFG_Skill table = CFG_Skill.Get();
            if (table == null || table.DicDatas == null)
                return 0;
            foreach (KeyValuePair<long, CFGItem_Skill> kv in table.DicDatas)
            {
                CFGItem_Skill cfg = kv.Value;
                if (cfg != null && cfg.SkillProfession == job && cfg.SkillID == skillId)
                    count++;
            }
            return count;
        }

        private static CFGItem_Skill FindNextLevel(int job, int skillId, int level)
        {
            CFG_Skill table = CFG_Skill.Get();
            if (table == null || table.DicDatas == null)
                return null;
            foreach (KeyValuePair<long, CFGItem_Skill> kv in table.DicDatas)
            {
                CFGItem_Skill cfg = kv.Value;
                if (cfg != null && cfg.SkillProfession == job
                    && cfg.SkillID == skillId && cfg.SkillLevel == level)
                    return cfg;
            }
            return null;
        }

        private static CFGItem_Skill FindAdvancedCfg(CFGItem_Skill current, int targetLevel)
        {
            CFG_Skill table = CFG_Skill.Get();
            if (table == null || table.DicDatas == null)
                return null;

            // 对齐原服 skillsAdvanced：只认 SkillProfession + SkillID + CanupperLevel == 目标进阶
            // 且 UpperlevelConsume 非空的那一条，找不到就返回 null（不要退回到别的档位，
            // 否则会按错误的材料扣除）。
            foreach (KeyValuePair<long, CFGItem_Skill> kv in table.DicDatas)
            {
                CFGItem_Skill cfg = kv.Value;
                if (cfg == null || cfg.SkillProfession != current.SkillProfession
                    || cfg.SkillID != current.SkillID)
                    continue;
                int canUpper;
                try { canUpper = (int)cfg.CanupperLevel; }
                catch { continue; }
                if (canUpper == targetLevel
                    && cfg.UpperlevelConsume != null && cfg.UpperlevelConsume.Length > 0)
                    return cfg;
            }
            return null;
        }

        private static bool CanConsume(LocalRole role, CFGItem_Skill.Consume_Item[] costs)
        {
            if (costs == null)
                return true;
            for (int i = 0; i < costs.Length; i++)
            {
                int id = costs[i].id;
                long num = costs[i].num;
                if (id <= 0 || num <= 0) continue;
                // ItemService.CountItem 统一处理货币（1/2/4/5/8/14/99）与背包道具
                if (ItemService.CountItem(role, id) < num)
                    return false;
            }
            return true;
        }

        private static void Consume(LocalRole role, CFGItem_Skill.Consume_Item[] costs)
        {
            if (costs == null)
                return;
            for (int i = 0; i < costs.Length; i++)
            {
                int id = costs[i].id;
                long num = costs[i].num;
                if (id <= 0 || num <= 0) continue;
                ItemService.ConsumeItem(role, id, num);
            }
            LocalData.Instance.Gold = role.Coin;
        }

        private static long CountBagItem(LocalRole role, int cfgId)
        {
            if (role == null || role.BagData == null || role.BagData.ItemList == null)
                return 0;
            long count = 0;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item != null && item.CfgId == cfgId)
                    count += item.Num;
            }
            return count;
        }

        private static void RemoveBagItem(LocalRole role, int cfgId, long num)
        {
            if (role == null || role.BagData == null || role.BagData.ItemList == null)
                return;
            List<ItemInfo> keep = new List<ItemInfo>();
            long remain = num;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item == null) continue;
                if (remain > 0 && item.CfgId == cfgId)
                {
                    long take = item.Num <= remain ? item.Num : remain;
                    item.Num -= take;
                    remain -= take;
                }
                if (item.Num > 0)
                    keep.Add(item);
            }
            role.BagData.ItemList = keep;
        }

        private static void AfterSkillChanged(LocalRole role)
        {
            if (role == null)
                return;
            SaveCoordinator.Instance.MarkDirty();
            if (LocalData.Instance.Current == role)
            {
                RoleStatService.Instance.Recalculate(role, true);
                LocalPusher.Instance.PushUserBaseNow();
                LocalPusher.Instance.PushBagInfoNow();
                LocalPusher.Instance.PushStatsNow();
                LocalPusher.Instance.PushSkillSlotNow();
            }
        }

        private static void EquipSkill(LocalRole role, CFGItem_Skill cfg)
        {
            if (role.SkillSlots == null)
                return;
            if (cfg.Skilltype == (int)CFG_Skill.SkillType.Super)
            {
                role.SkillSlots[0] = cfg.UUID;
                return;
            }
            for (int i = 1; i < role.SkillSlots.Length; i++)
            {
                if (role.SkillSlots[i] == 0)
                {
                    role.SkillSlots[i] = cfg.UUID;
                    return;
                }
            }
        }
    }
}
