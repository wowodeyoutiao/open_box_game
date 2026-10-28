using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 角色经验/升级规则。
    ///
    /// 对齐原服 player.go：
    /// - 经验增加后，上限为当前等级 NeedEXP * expstore.rate；
    /// - 单机没有伙伴上阵分支，按“主角直接升级”执行；
    /// - while 连续升级，每级扣当前等级 NeedEXP；
    /// - 达到 rolelevel.maxlevel 停止。
    /// </summary>
    public class LevelUpService : SingletonTemplate<LevelUpService>
    {
        private const int DefaultExpStoreRate = 25;
        private const int DefaultMaxLevel = 150;

        public sealed class AddExpResult
        {
            public int LevelsGained;
            public int NewLevel;
            public long OldExp;
            public long NewExp;
        }

        /// <summary>增加经验并按原服规则自动升级。</summary>
        public AddExpResult AddExp(LocalRole role, long amount)
        {
            var result = new AddExpResult();
            if (role == null)
                return result;

            result.OldExp = role.Exp;
            if (amount <= 0)
            {
                result.NewExp = role.Exp;
                result.NewLevel = role.Level;
                return result;
            }

            role.Exp += amount;
            long needExp = GetNeedExp(role.Job, role.Level);
            int rate = GetExpStoreRate();
            if (needExp > 0 && rate > 0)
            {
                long cap = needExp * rate;
                if (role.Exp > cap)
                    role.Exp = cap;
            }
            if (role.Exp < 0)
                role.Exp = 0;

            // 原服 ItemTypeExp 分支：只有"战斗单位数 == 1"（没伙伴上阵）才自动升级，
            // 有伙伴时经验只进池子，等玩家在经验池界面手动分配。
            if (GetCombatUnitNum(role) == 1)
                result.LevelsGained = ProcessLevelUp(role);
            result.NewExp = role.Exp;
            result.NewLevel = role.Level;
            return result;
        }

        /// <summary>
        /// 战斗单位数，对齐原服 Player.getCombatPartnerNum：
        /// **从主角本人算起**（num = 1），每个上阵伙伴 +1。
        /// 所以：没伙伴 = 1、1 个伙伴 = 2、2 个伙伴 = 3。
        /// </summary>
        public int GetCombatUnitNum(LocalRole role)
        {
            if (role == null)
                return 0;
            return 1 + PartnerService.Instance.GetBattlePartners(role).Count;
        }

        /// <summary>
        /// 只处理已有经验能升的等级，不额外加经验。
        /// 对齐原服 Player.levelUp 的两个分支（判据是 getCombatPartnerNum == 1）：
        ///   战斗单位数 == 1（只有主角） → while 连升，经验全给主角
        ///   否则（有伙伴上阵）          → 只升 1 级
        /// </summary>
        public int ProcessLevelUp(LocalRole role)
        {
            if (role == null)
                return 0;

            int maxLevel = GetMaxLevel();
            if (role.Level >= maxLevel)
                return 0;

            int gained = 0;
            int combatUnitNum = GetCombatUnitNum(role);
            if (combatUnitNum == 1)
            {
                while (role.Level < maxLevel)
                {
                    long needExp = GetNeedExp(role.Job, role.Level);
                    if (needExp <= 0 || role.Exp < needExp)
                        break;

                    role.Exp -= needExp;
                    if (role.Exp < 0)
                        role.Exp = 0;
                    role.Level++;
                    gained++;
                    LevelGiftService.Instance.GrantForLevel(role, role.Level);
                }
            }
            else
            {
                long needExp = GetNeedExp(role.Job, role.Level);
                if (needExp > 0 && role.Exp >= needExp)
                {
                    role.Exp -= needExp;
                    if (role.Exp < 0)
                        role.Exp = 0;
                    role.Level++;
                    gained = 1;
                    LevelGiftService.Instance.GrantForLevel(role, role.Level);
                }
            }

            if (gained > 0)
            {
                SkillService.Instance.AutoLearn(role);
                TaskService.Instance.Dispatch(role, TargetType.ROLE_LEVEL_REQUIREMENT, 0, role.Level);
                // 原服还会派发 UNLOCK_PARTNER_SLOT：按 Openslot 配置算出已解锁槽位数
                TaskService.Instance.Dispatch(role, TargetType.UNLOCK_PARTNER_SLOT, 0,
                    PartnerService.Instance.CountUnlockedSlots(role));
            }
            return gained;
        }

        /// <summary>
        /// 伙伴升级（原服 Partner.levelUp）：
        /// - 伙伴等级不能超过主角等级（Manager.UpgradeLevel 里先拦）；
        /// - 消耗的是**主角经验**，按伙伴职业+当前等级取 NeedEXP；
        /// - 一次只升 1 级。
        /// </summary>
        public bool LevelUpPartner(LocalRole role, PartnerInfo partner)
        {
            if (role == null || partner == null)
                return false;
            if (partner.Level >= GetMaxLevel())
                return false;
            if (partner.Level >= role.Level)
                return false;

            CFGItem_Partner cfg = null;
            try { cfg = CFG_Partner.GetById(partner.CfgId); }
            catch { cfg = null; }
            if (cfg == null)
                return false;

            long needExp = GetNeedExp(cfg.job, partner.Level);
            if (needExp <= 0 || role.Exp < needExp)
                return false;

            role.Exp -= needExp;
            if (role.Exp < 0)
                role.Exp = 0;
            partner.Level++;
            return true;
        }

        public long GetNeedExp(int job, int level)
        {
            try
            {
                CFGItem_Role cfg = CFG_Role.GetByJobAndLevel(job, level);
                return cfg == null ? 0 : cfg.NeedEXP;
            }
            catch
            {
                return 0;
            }
        }

        public int GetMaxLevel()
        {
            int cfgMax = 0;
            try
            {
                if (CFG_XMLConfig.xmlConfig != null
                    && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.rolelevel != null)
                    cfgMax = CFG_XMLConfig.xmlConfig.BaseValue.rolelevel.maxlevel;
            }
            catch { }
            if (cfgMax > 0)
                return cfgMax;
            return DefaultMaxLevel;
        }

        private int GetExpStoreRate()
        {
            try
            {
                if (CFG_XMLConfig.xmlConfig != null
                    && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.expstore != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.expstore.rate > 0)
                    return CFG_XMLConfig.xmlConfig.BaseValue.expstore.rate;
            }
            catch { }
            return DefaultExpStoreRate;
        }
    }
}
