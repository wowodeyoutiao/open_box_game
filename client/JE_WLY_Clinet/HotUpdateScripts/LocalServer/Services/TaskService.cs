using System;
using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 单人任务服务：初始化、目标推进、领奖。
    ///
    /// 对齐原服 task_mgr 的核心规则：
    /// - 按职业/等级/VIP 过滤首轮任务；
    /// - 角色等级/战力类目标取最大值；
    /// - 次数类目标累加；
    /// - COMPLETED 后才能领奖，领奖后 REWARDED；
    /// - 领奖产出走本地奖励入账。
    /// </summary>
    public class TaskService : SingletonTemplate<TaskService>
    {
        public int EnsureInitialized(LocalRole role)
        {
            if (role == null)
                return 0;
            if (role.Tasks == null)
                role.Tasks = new List<TaskInfo>();
            if (role.Tasks.Count > 0)
                return 0;

            CFG_Task table = CFG_Task.Get();
            if (table == null || table.DicDatas == null)
                return 0;

            int added = 0;
            foreach (KeyValuePair<long, CFGItem_Task> kv in table.DicDatas)
            {
                CFGItem_Task cfg = kv.Value;
                if (cfg == null)
                    continue;
                try
                {
                    if (cfg.TaskMark <= 0)
                        continue;
                }
                catch
                {
                    // TaskMark 在部分行是 null；ILRuntime 下 decimal 比较会抛异常。
                    continue;
                }
                if (cfg.Level != null && cfg.Level.Length >= 2
                    && (role.Level < cfg.Level[0] || role.Level > cfg.Level[1]))
                    continue;
                // VIP 任务依赖 VipLevel，单机当前先不派发，避免可空配置问题。
                if ((TaskClass)cfg.Class == TaskClass.VIP)
                    continue;
                // 七日任务依赖活动创建日，单机先不派发。
                if ((TaskClass)cfg.Class == TaskClass.SEVENDAY
                    || (TaskClass)cfg.Class == TaskClass.SEVENDAY_ACTIVITY)
                    continue;

                var task = new TaskInfo
                {
                    CfgId = (int)cfg.UUID,
                    Class = (TaskClass)cfg.Class,
                    Status = TaskStatus.IN_PROGRESS,
                    RefreshTime = 0,
                    Target = new TargetInfo
                    {
                        Type = (TargetType)cfg.Type,
                        TargetId = (int)cfg.Typeson,
                        TargetNum = (int)cfg.limit,
                        Amount = 0,
                        Completed = false,
                    },
                };
                role.Tasks.Add(task);
                added++;
            }

            for (int i = 0; i < role.Tasks.Count; i++)
            {
                TaskInfo task = role.Tasks[i];
                ApplyResetTime(task);
                ApplyInitialValue(role, task);
                CheckProgress(task);
            }
            if (added > 0)
                SaveCoordinator.Instance.MarkDirty();
            return added;
        }

        /// <summary>每日/每周任务到点重置。返回实际重置数量。</summary>
        public int ResetIfNeeded(LocalRole role)
        {
            if (role == null || role.Tasks == null)
                return 0;

            long now = NowUnix();
            int changed = 0;
            for (int i = 0; i < role.Tasks.Count; i++)
            {
                TaskInfo task = role.Tasks[i];
                if (task == null || !IsResetClass(task.Class))
                    continue;

                if (task.RefreshTime <= 0)
                {
                    task.RefreshTime = NextResetUnix(task.Class);
                    changed++;
                    continue;
                }
                if (now < task.RefreshTime)
                    continue;

                task.Target.Amount = 0;
                task.Target.Completed = false;
                task.Status = TaskStatus.IN_PROGRESS;
                task.RefreshTime = NextResetUnix(task.Class);
                changed++;
                if (LocalData.Instance.Current == role)
                    LocalPusher.Instance.PushTaskUpdateNow(task);
            }
            if (changed > 0)
                SaveCoordinator.Instance.MarkDirty();
            return changed;
        }

        private static bool IsResetClass(TaskClass taskClass)
        {
            return taskClass == TaskClass.DAILY
                || taskClass == TaskClass.DAILY_ACTIVITY
                || taskClass == TaskClass.WEEKLY
                || taskClass == TaskClass.WEEKLY_ACTIVITY
                || taskClass == TaskClass.ROUTINE;
        }

        private static void ApplyResetTime(TaskInfo task)
        {
            if (task == null || !IsResetClass(task.Class))
                return;
            if (task.RefreshTime <= 0)
                task.RefreshTime = NextResetUnix(task.Class);
        }

        private static long NextResetUnix(TaskClass taskClass)
        {
            DateTime now = DateTime.Now;
            DateTime next = taskClass == TaskClass.WEEKLY
                || taskClass == TaskClass.WEEKLY_ACTIVITY
                ? now.Date.AddDays(7)
                : now.Date.AddDays(1);
            return new DateTimeOffset(next).ToUnixTimeSeconds();
        }

        private static long NowUnix()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public void Dispatch(LocalRole role, TargetType type, int targetId, int value)
        {
            if (role == null || value <= 0)
                return;
            EnsureInitialized(role);
            for (int i = 0; i < role.Tasks.Count; i++)
            {
                TaskInfo task = role.Tasks[i];
                if (task == null || task.Target == null)
                    continue;
                if (task.Status != TaskStatus.IN_PROGRESS
                    && task.Class != TaskClass.DAILY_ACTIVITY
                    && task.Class != TaskClass.WEEKLY_ACTIVITY)
                    continue;
                if (task.Target.Type != type)
                    continue;

                switch (type)
                {
                    case TargetType.ROLE_POWER_REQUIREMENT:
                    case TargetType.ROLE_LEVEL_REQUIREMENT:
                    case TargetType.EQUIPMENT_STRENGTHENING_LEVEL_REQUIREMENT:
                    case TargetType.EQUIPMENT_UPGRADING_STAR_LEVEL_REQUIREMENT:
                    case TargetType.TOTAL_ATTEMPTS_IN_ENDLESS_TRIALS:
                    case TargetType.VIP_LEVEL:
                    case TargetType.ANY_PET_UPGRADED_TO_LEVEL:
                    case TargetType.UNLOCK_PARTNER_SLOT:
                        if (task.Target.Amount < value)
                            task.Target.Amount = value;
                        break;
                    case TargetType.SKILL_UPGRADING_REQUIREMENT:
                        if (task.Target.TargetId == targetId)
                            task.Target.Amount = task.Target.TargetNum;
                        break;
                    default:
                        task.Target.Amount += value;
                        break;
                }

                CheckProgress(task);
                if (LocalData.Instance.Current == role)
                    LocalPusher.Instance.PushTaskUpdateNow(task);
            }
        }

        public TaskComplateResponse Complete(int taskId)
        {
            var response = new TaskComplateResponse { Code = -1, Error = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null || role.Tasks == null)
            {
                response.Error = "没有角色";
                return response;
            }

            TaskInfo task = null;
            for (int i = 0; i < role.Tasks.Count; i++)
            {
                if (role.Tasks[i] != null && role.Tasks[i].CfgId == taskId)
                {
                    task = role.Tasks[i];
                    break;
                }
            }
            if (task == null)
            {
                response.Error = "任务不存在";
                return response;
            }
            if (task.Status != TaskStatus.COMPLETED)
            {
                response.Error = "任务尚未完成";
                return response;
            }

            CFGItem_Task cfg = GetCfg(taskId);
            if (cfg == null)
            {
                response.Error = "任务配置不存在";
                return response;
            }

            task.Status = TaskStatus.REWARDED;
            ApplyRewards(role, cfg.Reward);
            try
            {
                if (cfg.NextAchievement > 0)
                    AddTask(role, (long)cfg.NextAchievement);
            }
            catch { }
            SaveCoordinator.Instance.MarkDirty();
            if (LocalData.Instance.Current == role)
            {
                RoleStatService.Instance.Recalculate(role, true);
                LocalPusher.Instance.PushUserBaseNow();
                LocalPusher.Instance.PushBagInfoNow();
                LocalPusher.Instance.PushStatsNow();
                LocalPusher.Instance.PushTaskUpdateNow(task);
            }
            response.Code = 0;
            return response;
        }

        public void AddTask(LocalRole role, long cfgId)
        {
            if (role == null)
                return;
            for (int i = 0; i < role.Tasks.Count; i++)
                if (role.Tasks[i] != null && role.Tasks[i].CfgId == cfgId)
                    return;
            CFGItem_Task cfg = GetCfg((int)cfgId);
            if (cfg == null)
                return;
            var task = new TaskInfo
            {
                CfgId = (int)cfg.UUID,
                Class = (TaskClass)cfg.Class,
                Status = TaskStatus.IN_PROGRESS,
                Target = new TargetInfo
                {
                    Type = (TargetType)cfg.Type,
                    TargetId = (int)cfg.Typeson,
                    TargetNum = (int)cfg.limit,
                    Amount = 0,
                    Completed = false,
                },
            };
            ApplyResetTime(task);
            ApplyInitialValue(role, task);
            CheckProgress(task);
            role.Tasks.Add(task);
            if (LocalData.Instance.Current == role)
                LocalPusher.Instance.PushTaskAcceptNow(task);
        }

        private static void ApplyInitialValue(LocalRole role, TaskInfo task)
        {
            if (role == null || task == null || task.Target == null)
                return;
            switch (task.Target.Type)
            {
                case TargetType.ROLE_POWER_REQUIREMENT:
                    task.Target.Amount = role.TotalCombat;
                    break;
                case TargetType.ROLE_LEVEL_REQUIREMENT:
                    task.Target.Amount = role.Level;
                    break;
                case TargetType.VIP_LEVEL:
                    task.Target.Amount = role.Vip;
                    break;
                case TargetType.SKILL_UPGRADING_REQUIREMENT:
                    if (HasSkillAtLeast(role, task.Target.TargetId))
                        task.Target.Amount = task.Target.TargetNum;
                    break;
            }
        }

        private static void CheckProgress(TaskInfo task)
        {
            if (task == null || task.Target == null)
                return;
            if (task.Status == TaskStatus.COMPLETED || task.Status == TaskStatus.REWARDED)
                return;
            if (task.Target.TargetNum > 0 && task.Target.Amount >= task.Target.TargetNum)
            {
                task.Target.Amount = task.Target.TargetNum;
                task.Target.Completed = true;
                task.Status = TaskStatus.COMPLETED;
            }
        }

        private static bool HasSkillAtLeast(LocalRole role, int skillCfgId)
        {
            if (role == null || role.Skills == null)
                return false;
            int baseId = skillCfgId / 100;
            int needLevel = skillCfgId % 100;
            for (int i = 0; i < role.Skills.Count; i++)
            {
                SkillInfo skill = role.Skills[i];
                if (skill == null) continue;
                if (skill.CfgId / 100 == baseId && skill.CfgId % 100 >= needLevel)
                    return true;
            }
            return false;
        }

        private static CFGItem_Task GetCfg(int cfgId)
        {
            try { return CFG_Task.GetById(cfgId); }
            catch { return null; }
        }

        private static void ApplyRewards(LocalRole role, Dictionary<string, int>[] rewards)
        {
            if (role == null || rewards == null)
                return;
            for (int i = 0; i < rewards.Length; i++)
            {
                Dictionary<string, int> entry = rewards[i];
                if (entry == null) continue;
                foreach (KeyValuePair<string, int> kv in entry)
                {
                    int id;
                    if (!int.TryParse(kv.Key, out id) || id <= 0 || kv.Value <= 0)
                        continue;
                    AddReward(role, id, kv.Value);
                }
            }
        }

        private static void AddReward(LocalRole role, int id, long num)
        {
            if (id == 1)
            {
                role.Coin += num;
                LocalData.Instance.Gold = role.Coin;
            }
            else if (id == 2)
            {
                LevelUpService.Instance.AddExp(role, num);
                LocalData.Instance.Exp = role.Exp;
                LocalData.Instance.Level = role.Level;
            }
            else if (id == 14)
            {
                role.TreasuerBox += num;
                LocalData.Instance.TreasuerBox = role.TreasuerBox;
            }
            else
            {
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                if (role.BagData.ItemList == null)
                    role.BagData.ItemList = new List<ItemInfo>();

                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    ItemInfo old = role.BagData.ItemList[i];
                    if (old != null && old.CfgId == id && !EquipRuleService.IsEquipment(old))
                    {
                        old.Num += num;
                        return;
                    }
                }

                CFGItem_Item cfg;
                try { cfg = CFG_Item.GetById(id); }
                catch { return; }
                if (cfg == null) return;
                int type = EquipRuleService.ToInt(cfg.Type);
                role.BagData.ItemList.Add(new ItemInfo
                {
                    CfgId = id,
                    Uid = LocalData.Instance.AllocateItemUid(),
                    Num = num,
                    CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Type = (ItemType)type,
                });
            }
        }
    }
}
