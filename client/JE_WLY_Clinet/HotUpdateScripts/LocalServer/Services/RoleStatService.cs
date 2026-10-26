using System.Collections.Generic;
using Exception = System.Exception;
using Math = System.Math;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>一次属性重算的不可变结果。</summary>
    public sealed class RoleStatSnapshot
    {
        private readonly Dictionary<Attribute, int> _values;

        public int Combat { get; private set; }

        public RoleStatSnapshot(Dictionary<Attribute, int> values, int combat)
        {
            _values = values ?? new Dictionary<Attribute, int>();
            Combat = combat;
        }

        public int Get(Attribute attribute)
        {
            int value;
            return _values.TryGetValue(attribute, out value) ? value : 0;
        }

        public int PhysicalAttack { get { return Get(Attribute.PHYSICAL_ATTACK); } }
        public int MagicAttack { get { return Get(Attribute.MAGIC_ATTACK); } }
        public int PhysicalDefense { get { return Get(Attribute.PHYSICAL_DEFENSE); } }
        public int MagicDefense { get { return Get(Attribute.MAGIC_DEFENSE); } }
        public int MaxHealth { get { return Get(Attribute.MAX_HEALTH); } }
        public int CurrentHealth { get { return Get(Attribute.HEALTH); } }
        public int AttackSpeed { get { return Get(Attribute.ATTACK_SPEED); } }
        public int MoveSpeed { get { return Get(Attribute.MOVE_SPEED); } }

        public List<Stats> ToStatsList()
        {
            var list = new List<Stats>();
            int max = (int)Attribute.MAX_Attribute;
            for (int i = 1; i < max; i++)
            {
                Attribute attribute = (Attribute)i;
                int value;
                if (!_values.TryGetValue(attribute, out value) || value <= 0)
                    continue;
                list.Add(new Stats { attr = attribute, Num = value });
            }
            return list;
        }
    }

    /// <summary>
    /// 本地角色属性计算。
    ///
    /// 当前实现是服务端 `player.go / refreshStats + calcCombatReal` 的最小闭环：
    /// - CFG_Role 基础属性；
    /// - 装备配置基础属性 × Equipquality 品质系数；
    /// - 装备实例 Attributes（随机/极品属性）；
    /// - 百分比属性；
    /// - 攻速/移速初始配置；
    /// - BaseValue.Power 战力公式。
    ///
    /// 尚未接入命格、魂石、宠物、公会、VIP、被动技能和 buff。
    /// </summary>
    public class RoleStatService : SingletonTemplate<RoleStatService>
    {
        private const int BaseRate = 10000;

        public RoleStatSnapshot RecalculateCurrent(bool fillHealth = false)
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return null;

            RoleStatSnapshot snapshot = Recalculate(role, fillHealth);

            LocalData data = LocalData.Instance;
            data.Attack = role.Attack;
            data.Defense = role.Defense;
            data.MaxHp = role.MaxHp;
            data.CurHp = role.CurHp;
            data.TotalCombat = role.TotalCombat;
            return snapshot;
        }

        public RoleStatSnapshot Recalculate(LocalRole role, bool fillHealth = false)
        {
            if (role == null)
                return null;

            var values = new Dictionary<Attribute, int>();
            AddRoleBase(role, values);
            AddInitialSpeed(role.Job, values);
            AddEquipment(role, values);
            AddCriticalDamage(values);
            ApplyPercentages(values);

            int maxHealth = Get(values, Attribute.MAX_HEALTH);
            if (maxHealth < 1)
                maxHealth = 1;
            Set(values, Attribute.MAX_HEALTH, maxHealth);

            int currentHealth = fillHealth
                ? maxHealth
                : (role.CurHp <= 0 || role.CurHp > maxHealth ? maxHealth : role.CurHp);
            Set(values, Attribute.HEALTH, currentHealth);

            int combat = CalculateCombat(role.Job, values);

            Attribute attackAttr = GetAttackAttribute(role.Job);
            Attribute defenseAttr = attackAttr == Attribute.PHYSICAL_ATTACK
                ? Attribute.PHYSICAL_DEFENSE
                : Attribute.MAGIC_DEFENSE;

            role.Attack = Get(values, attackAttr);
            role.Defense = Get(values, defenseAttr);
            role.MaxHp = maxHealth;
            role.CurHp = currentHealth;
            role.TotalCombat = combat;

            return new RoleStatSnapshot(values, combat);
        }

        private static void AddRoleBase(LocalRole role, Dictionary<Attribute, int> values)
        {
            try
            {
                CFGItem_Role cfg = CFG_Role.GetByJobAndLevel(role.Job, role.Level);
                if (cfg == null || cfg.Property == null)
                    return;

                for (int i = 0; i < cfg.Property.Length; i++)
                {
                    CFGItem_Role.PropertyItem item = cfg.Property[i];
                    Add(values, (Attribute)item.Id, item.Num);
                }
            }
            catch (Exception e)
            {
                Log.PrintError("[RoleStatService] 读取角色基础属性失败 job=" + role.Job
                    + " level=" + role.Level + " " + e.Message);
            }
        }

        private static void AddInitialSpeed(int job, Dictionary<Attribute, int> values)
        {
            int attackSpeed = 500;
            int moveSpeed = 180;

            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.InitialDic != null)
                {
                    CFG_XMLConfig.XML_Config.XML_Item_Initial initial;
                    if (config.InitialDic.TryGetValue(job, out initial) && initial != null)
                    {
                        if (initial.attackSpeed > 0)
                            attackSpeed = initial.attackSpeed;
                        if (initial.moveSpeed > 0)
                            moveSpeed = initial.moveSpeed;
                    }
                }
            }
            catch
            {
                // 配置异常时使用 xml 中的默认值。
            }

            Add(values, Attribute.ATTACK_SPEED, attackSpeed);
            Add(values, Attribute.MOVE_SPEED, moveSpeed);
        }

        private static void AddEquipment(LocalRole role, Dictionary<Attribute, int> values)
        {
            if (role.Equips == null)
                return;

            var equipPrimary = new Dictionary<Attribute, int>();
            foreach (KeyValuePair<EquipSlot, ItemInfo> kv in role.Equips)
            {
                int slot = (int)kv.Key;
                if (slot <= (int)EquipSlot.INVALID_EquipSlot || slot >= (int)EquipSlot.MaxEquipSlot)
                    continue;

                ItemInfo item = kv.Value;
                if (item == null)
                    continue;

                CFGItem_Item cfg;
                try
                {
                    cfg = CFG_Item.GetById(item.CfgId);
                }
                catch (Exception e)
                {
                    Log.PrintWarning("[RoleStatService] 装备配置缺失 cfg=" + item.CfgId + " " + e.Message);
                    continue;
                }
                if (cfg == null)
                    continue;

                float quality = GetQualityCoefficient(item.Equip != null
                    ? (int)item.Equip.Quality
                    : (int)ItemQuality.WHITE);

                // 服务端规则：攻/防/生命基础值乘装备品质系数。
                Add(equipPrimary, Attribute.PHYSICAL_ATTACK, Scale(cfg.Phyatk, quality));
                Add(equipPrimary, Attribute.MAGIC_ATTACK, Scale(cfg.Magatk, quality));
                Add(equipPrimary, Attribute.PHYSICAL_DEFENSE, Scale(cfg.Phydef, quality));
                Add(equipPrimary, Attribute.MAGIC_DEFENSE, Scale(cfg.Magdef, quality));
                Add(equipPrimary, Attribute.MAX_HEALTH, Scale(cfg.HP, quality));

                // 其余配置属性按服务端逻辑直接进入总属性。
                Add(values, Attribute.HIT, ToInt(cfg.Hit));
                Add(values, Attribute.DODGE, ToInt(cfg.Miss));
                Add(values, Attribute.CRITICAL_STRIKE, ToInt(cfg.Crit));
                Add(values, Attribute.TOUGHNESS, ToInt(cfg.Decrit));
                Add(values, Attribute.PVP_DAMAGE_PERCENTAGE, ToInt(cfg.PVPdamage));
                Add(values, Attribute.PVE_DAMAGE_PERCENTAGE, ToInt(cfg.PVEdamage));
                Add(values, Attribute.HEALTH_PERCENTAGE, ToInt(cfg.HPPercent));

                // 实例极品/随机属性直接加成。
                if (item.Equip != null && item.Equip.Attributes != null)
                {
                    for (int i = 0; i < item.Equip.Attributes.Count; i++)
                    {
                        Stats stat = item.Equip.Attributes[i];
                        if (stat == null)
                            continue;
                        Add(values, stat.attr, stat.Num);
                    }
                }
            }

            foreach (KeyValuePair<Attribute, int> kv in equipPrimary)
                Add(values, kv.Key, kv.Value);
        }

        private static void AddCriticalDamage(Dictionary<Attribute, int> values)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.BaseValue != null && config.BaseValue.crit != null)
                    Add(values, Attribute.CRITICAL_DAMAGE_PERCENTAGE,
                        (int)Math.Round(config.BaseValue.crit.critdamage));
            }
            catch
            {
                // 缺配置时不增加暴击伤害。
            }
        }

        private static void ApplyPercentages(Dictionary<Attribute, int> values)
        {
            ApplyPercent(values, Attribute.PHYSICAL_ATTACK, Attribute.PHYSICAL_ATTACK_PERCENTAGE);
            ApplyPercent(values, Attribute.MAGIC_ATTACK, Attribute.MAGIC_ATTACK_PERCENTAGE);
            ApplyPercent(values, Attribute.PHYSICAL_DEFENSE, Attribute.PHYSICAL_DEFENSE_PERCENTAGE);
            ApplyPercent(values, Attribute.MAGIC_DEFENSE, Attribute.MAGIC_DEFENSE_PERCENTAGE);
            ApplyPercent(values, Attribute.MAX_HEALTH, Attribute.HEALTH_PERCENTAGE);
            ApplyPercent(values, Attribute.HIT, Attribute.HIT_PERCENTAGE);
            ApplyPercent(values, Attribute.DODGE, Attribute.DODGE_PERCENTAGE);
            ApplyPercent(values, Attribute.CRITICAL_STRIKE, Attribute.CRITICAL_STRIKE_PERCENTAGE);
            ApplyPercent(values, Attribute.TOUGHNESS, Attribute.TOUGHNESS_PERCENTAGE);

            ApplySpeedPercent(values, Attribute.ATTACK_SPEED, Attribute.ATTACK_SPEED_PERCENTAGE);
            ApplySpeedPercent(values, Attribute.MOVE_SPEED, Attribute.MOVE_SPEED_PERCENTAGE);
        }

        private static void ApplyPercent(
            Dictionary<Attribute, int> values,
            Attribute baseAttr,
            Attribute percentAttr)
        {
            int baseValue = Get(values, baseAttr);
            int percent = Get(values, percentAttr);
            if (baseValue <= 0 || percent == 0)
                return;
            Add(values, baseAttr, (int)Math.Round(baseValue * (percent / (double)BaseRate)));
        }

        private static void ApplySpeedPercent(
            Dictionary<Attribute, int> values,
            Attribute baseAttr,
            Attribute percentAttr)
        {
            int baseValue = Get(values, baseAttr);
            int percent = Get(values, percentAttr);
            if (baseValue <= 0 || percent == 0)
                return;
            int value = (int)Math.Floor(baseValue / (1.0 + percent / (double)BaseRate));
            Set(values, baseAttr, value > 0 ? value : 0);
        }

        private static int CalculateCombat(int job, Dictionary<Attribute, int> values)
        {
            int maxHp = Get(values, Attribute.MAX_HEALTH);
            int pDef = Get(values, Attribute.PHYSICAL_DEFENSE);
            int mDef = Get(values, Attribute.MAGIC_DEFENSE);
            int attack = Get(values, GetAttackAttribute(job));

            int defValue = 100;
            double lifeValue = 1;
            double attackValue = 1;
            double hitValue = 10;
            double dodgeValue = 10;
            double critValue = 30;
            double toughnessValue = 10;
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.BaseValue != null && config.BaseValue.Power != null)
                {
                    CFG_XMLConfig.XML_Config.XML_Item_Power power = config.BaseValue.Power;
                    defValue = power.defvalue;
                    lifeValue = power.lifevalue;
                    attackValue = power.attackvalue;
                    hitValue = power.hitvalue;
                    dodgeValue = power.dodgevalue;
                    critValue = power.critvalue;
                    toughnessValue = power.toughness;
                }
            }
            catch
            {
                // 使用配置默认值。
            }

            double effectiveHp = maxHp + defValue * (pDef + mDef) / 2.0;
            double damagePercent = Get(values, Attribute.PVP_DAMAGE_PERCENTAGE)
                + Get(values, Attribute.PVE_DAMAGE_PERCENTAGE)
                + Get(values, Attribute.CRITICAL_DAMAGE_PERCENTAGE)
                + Get(values, Attribute.DMT_FINAL_INCREASE_PERCENTAGE);
            double effectivePower = attack * (1.0 + damagePercent / BaseRate);

            double combat = Math.Sqrt(effectiveHp * lifeValue)
                * Math.Sqrt(Math.Max(0, effectivePower) * attackValue)
                + Get(values, Attribute.HIT) * hitValue
                + Get(values, Attribute.DODGE) * dodgeValue
                + Get(values, Attribute.CRITICAL_STRIKE) * critValue
                + Get(values, Attribute.TOUGHNESS) * toughnessValue;

            return (int)Math.Round(combat);
        }

        private static Attribute GetAttackAttribute(int job)
        {
            // 与服务端一致：战士物理，法师/祭祀魔法。
            return job == 1 ? Attribute.PHYSICAL_ATTACK : Attribute.MAGIC_ATTACK;
        }

        private static float GetQualityCoefficient(int quality)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.EquipQualityDic != null)
                {
                    CFG_XMLConfig.XML_Config.Equipquality_Item item;
                    if (config.EquipQualityDic.TryGetValue(quality, out item)
                        && item != null && item._value > 0)
                        return item._value;
                }
            }
            catch
            {
                // 默认白装系数。
            }
            return 1f;
        }

        private static int Scale(decimal? value, float coefficient)
        {
            int raw = ToInt(value);
            if (raw == 0)
                return 0;
            return (int)Math.Round(raw * coefficient);
        }

        private static int ToInt(decimal? value)
        {
            return value.HasValue ? (int)value.Value : 0;
        }

        private static int Get(Dictionary<Attribute, int> values, Attribute attribute)
        {
            int value;
            return values.TryGetValue(attribute, out value) ? value : 0;
        }

        private static void Add(Dictionary<Attribute, int> values, Attribute attribute, int value)
        {
            if (value == 0)
                return;
            if (attribute <= Attribute.INVALID_Attribute || attribute >= Attribute.MAX_Attribute)
                return;
            int current = Get(values, attribute);
            values[attribute] = current + value;
        }

        private static void Set(Dictionary<Attribute, int> values, Attribute attribute, int value)
        {
            if (attribute <= Attribute.INVALID_Attribute || attribute >= Attribute.MAX_Attribute)
                return;
            values[attribute] = value;
        }
    }
}
