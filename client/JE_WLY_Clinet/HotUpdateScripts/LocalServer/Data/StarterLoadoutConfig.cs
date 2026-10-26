using System.Collections.Generic;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 三职业一级基础装备白名单。
    ///
    /// 数据来自客户端 item.json：
    /// - Type = 5（装备）
    /// - Occupylimit 对应职业
    /// - Levellimit <= 1
    /// - Equiptype 1..12
    ///
    /// 不使用“扫描表取第一个”的动态规则，避免配置增加新装备后初始化结果漂移。
    /// </summary>
    public static class StarterLoadoutConfig
    {
        private static readonly int[] WarriorItems = { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112 };
        private static readonly int[] MageItems = { 1913, 1914, 1915, 1916, 1917, 1918, 1919, 1920, 1921, 1922, 1923, 1924 };
        private static readonly int[] PriestItems = { 3725, 3726, 3727, 3728, 3729, 3730, 3731, 3732, 3733, 3734, 3735, 3736 };

        private static readonly Dictionary<int, int[]> ItemsByJob = new Dictionary<int, int[]>
        {
            { 1, WarriorItems },
            { 2, MageItems },
            { 3, PriestItems }
        };

        public static bool TryGet(int job, out int[] itemIds)
        {
            return ItemsByJob.TryGetValue(job, out itemIds);
        }

        public static string GetJobName(int job)
        {
            if (job == 1) return "战士";
            if (job == 2) return "法师";
            if (job == 3) return "祭祀";
            return "未知职业";
        }
    }
}
