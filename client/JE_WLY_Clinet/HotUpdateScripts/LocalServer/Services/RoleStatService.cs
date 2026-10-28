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
    /// 尚未接入命格、公会、VIP、buff（伙伴、被动技能、魂石、战宠已接入）。
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
            // 上阵伙伴的属性（原服 refreshStats 会遍历 partnerSlots 累加）
            PartnerService.Instance.AddPartnerStats(values);
            // 战宠属性（原服 refreshStats 里 petStatus / newpetStatus 那两段）。
            // 位置很关键：原服是先加宠物、再算魂石的生命百分比尾数，
            // 所以宠物血要算进「当前累计生命」这个基数里 → 必须放在 AddGemstones 之前。
            AddPets(role, values);
            // 魂石（原服 refreshStats 里 allGems / gemStatus 那两段）。
            // 必须放在 ApplyPercentages 之前，且生命百分比要在「装备+伙伴」都加完之后算基数。
            AddGemstones(role, values);
            // 强化大师 / 升星大师（原服紧跟魂石大师之后，两段都是「全身最低 + 最高达标档」）。
            AddEquipmentMaster(role, values);
            // 被动技能属性。必须在 ApplyPercentages 之前，
            // 否则 [勇猛] 这类 +5% 攻击/防御的被动不会生效。
            AddPassiveSkills(role, values);
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

                // 槽位强化属性（原服 refreshStats 里那段 enhanc_uid）：
                //   uuid = job%10*1000 + Enhance
                //   取配置里 slot 匹配的 Attribute，值 × 装备「配置品质」对应的强化系数
                // 强化属性算在装备层（会和装备百分比一起结算）。
                int enhanceLv = role.GetEquipEnhance(kv.Key);
                if (enhanceLv > 0)
                {
                    CFGItem_EquipStrengthen enhanceCfg = null;
                    try { enhanceCfg = CFG_EquipStrengthen.GetByJobAndLv(role.Job, enhanceLv); }
                    catch { enhanceCfg = null; }
                    if (enhanceCfg != null && enhanceCfg.Attribute != null)
                    {
                        float enhanceCoef = GetEnhanceQualityCoef(ToInt(cfg.Quality));
                        for (int e = 0; e < enhanceCfg.Attribute.Length; e++)
                        {
                            CFGItem_EquipStrengthen.Attribute_Item attr = enhanceCfg.Attribute[e];
                            if (attr.slot != slot || attr.attribute <= 0)
                                continue;
                            Add(equipPrimary, (Attribute)attr.attribute,
                                (int)(attr.value * enhanceCoef));
                        }
                    }
                }

                // 槽位升星属性（原服 refreshStats 里那段 star_uid）：
                //   uuid = job%10*100 + Stars
                //   取配置里 slot 匹配的 Attribute **原值**加入（不乘品质系数）
                // 配置里这些是百分比属性（13/16/17 物攻/物防/魔防%、15 生命%），
                // 会跟着 ApplyPercentages 一起结算。
                int stars = role.GetEquipStars(kv.Key);
                if (stars > 0)
                {
                    CFGItem_EquipStar starCfg = null;
                    try { starCfg = CFG_EquipStar.GetByJobAndLv(role.Job, stars); }
                    catch { starCfg = null; }
                    if (starCfg.Attribute != null)
                    {
                        for (int st = 0; st < starCfg.Attribute.Length; st++)
                        {
                            CFGItem_EquipStar.Attribute_Item attr = starCfg.Attribute[st];
                            if (attr.slot != slot || attr.attribute <= 0)
                                continue;
                            Add(equipPrimary, (Attribute)attr.attribute, attr.value);
                        }
                    }
                }

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

        /// <summary>
        /// 战宠属性，严格照抄原服 refreshStats 的宠物段：
        ///
        ///   petStatus := {}                                    // 只装「原值 + 百分比」，不进总属性
        ///   if len(p.pet) > 0 && p.pet[0].PetCfgId != 0 {       // **只看 0 号上阵槽**
        ///       StarAttribute[Star]                              → petStatus（星级属性，是百分比）
        ///       RoleCfg(Pet, Level).BassProperty × 品质系数       → petStatus（等级基础属性）
        ///           品质系数 = GetpetQualityRate(Quality)，为 0 或 PetKind.Key==2（经验宝宝）时取 1
        ///       newpetStatus = 攻/魔攻/物防/魔防/生命 各自 × (1 + 自身百分比/10000)
        ///       **只把这 5 个属性**并进 p.stats（其余属性丢掉）
        ///   }
        ///
        /// 三个容易做错的点：
        ///   ① 只有 0 号槽位（上阵）的宠物提供属性，包裹里的宠物完全不算；
        ///   ② 星级属性是**百分比**（13/14/15/16/17），等级基础属性才是数值，
        ///      两者都进 petStatus 同一个桶，最后只对 5 个属性做百分比放大；
        ///   ③ 放大完只并入攻/魔攻/物防/魔防/生命，petStatus 里的百分比本身不进总属性
        ///      （否则会跟装备百分比叠成双倍）。
        /// </summary>
        private static void AddPets(LocalRole role, Dictionary<Attribute, int> values)
        {
            if (role == null)
                return;

            PetSlotInfo pet = role.GetActivePet();
            if (pet == null || pet.CfgId <= 0)
                return;

            CFGItem_Pet petCfg;
            try { petCfg = CFG_Pet.GetById(pet.CfgId); }
            catch { petCfg = null; }
            if (petCfg == null)
                return;

            var petStatus = new Dictionary<Attribute, int>();

            // ① 星级属性（百分比）
            CFGItem_Pet.AttributeItem.Item[] starAttrs = petCfg.GetStarAttributeByLv(pet.Star);
            if (starAttrs != null)
            {
                for (int i = 0; i < starAttrs.Length; i++)
                {
                    CFGItem_Pet.AttributeItem.Item item = starAttrs[i];
                    if (item == null || item.key <= 0)
                        continue;
                    if (item.key >= (int)Attribute.MAX_Attribute)
                        continue;
                    Add(petStatus, (Attribute)item.key, item.value);
                }
            }

            // ② 等级基础属性 × 品质系数（经验宝宝系数强制 1，对齐原服）
            CFGItem_Role roleCfg = null;
            try { roleCfg = CFG_Role.GetByJobAndLevel(PetService.PetJob, pet.Level); }
            catch { roleCfg = null; }
            if (roleCfg != null)
            {
                CFGItem_Role.PropertyItem[] props = roleCfg.Property;
                float rate = PetService.GetQualityRate(pet.Quality);
                if (rate <= 0f || petCfg.IsExpPet)
                    rate = 1f;
                if (props != null)
                {
                    for (int i = 0; i < props.Length; i++)
                    {
                        CFGItem_Role.PropertyItem prop = props[i];
                        if (prop.Id <= 0 || prop.Id >= (int)Attribute.MAX_Attribute)
                            continue;
                        // 原服是 math.Round（.5 远离 0 进位），C# 默认 Math.Round 是银行家舍入
                        // （318.5 → 318），必须显式指定，否则宠物品质系数带出来的 .5 会差 1 点。
                        Add(petStatus, (Attribute)prop.Id,
                            (int)Math.Round(prop.Num * rate, System.MidpointRounding.AwayFromZero));
                    }
                }
            }

            // ③ 5 个属性各自叠自身百分比后并入总属性
            AddPetScaled(values, petStatus, Attribute.PHYSICAL_ATTACK, Attribute.PHYSICAL_ATTACK_PERCENTAGE);
            AddPetScaled(values, petStatus, Attribute.MAGIC_ATTACK, Attribute.MAGIC_ATTACK_PERCENTAGE);
            AddPetScaled(values, petStatus, Attribute.PHYSICAL_DEFENSE, Attribute.PHYSICAL_DEFENSE_PERCENTAGE);
            AddPetScaled(values, petStatus, Attribute.MAGIC_DEFENSE, Attribute.MAGIC_DEFENSE_PERCENTAGE);
            AddPetScaled(values, petStatus, Attribute.MAX_HEALTH, Attribute.HEALTH_PERCENTAGE);
        }

        /// <summary>
        /// 宠物单属性结算：value × (1 + 百分比/10000)，结果加到总属性。
        /// 原服写法是 int32(float64(v)*(1+float64(p)/BaseRate))，即**截断**而不是四舍五入。
        /// </summary>
        private static void AddPetScaled(
            Dictionary<Attribute, int> values,
            Dictionary<Attribute, int> petStatus,
            Attribute baseAttr,
            Attribute percentAttr)
        {
            int baseValue = Get(petStatus, baseAttr);
            if (baseValue <= 0)
                return;
            int percent = Get(petStatus, percentAttr);
            int final = baseValue;
            if (percent != 0)
                final = (int)(baseValue * (1.0 + percent / (double)BaseRate));
            if (final > 0)
                Add(values, baseAttr, final);
        }

        /// <summary>魂石自然加成配置的 ID 前缀（原服 EquipMasterCategoryGemStoneNature * 1000）。</summary>
        public const int GemStoneNatureMasterBase = 4000;

        /// <summary>
        /// 魂石属性，对齐原服 refreshStats 的宝石段：
        ///
        ///   for slot, equip := range p.equips {
        ///       for GemstoneSlot, Gemstone := range equip.Gemstone {
        ///           genstoneCfg := GetItemCfg(Gemstone)
        ///           if genstoneCfg.NatureMark > 0 { gemstoneNature = append(...) }
        ///           allGems = append(allGems, genstoneCfg.Itemlevel)
        ///           for attr := range genstoneCfg.Stats {
        ///               if attr.Key == HEALTH_PERCENTAGE { gemStatus.setStats(...) }
        ///               else { p.stats.setStats(attr.Key, attr.Value) }
        ///           }
        ///       }
        ///       // 4 个槽位同一自然系 → 自然加成
        ///       if len(gemstoneNature) == 4 && 全部相等 { 加 EquipMaster(4000+nature) 属性 }
        ///   }
        ///   // 魂石增加生命百分比（在装备合并之后、大师之前）
        ///   p.stats.MAX_HEALTH += 当前MAX_HEALTH * gemStatus[HEALTH_PERCENTAGE] / 10000
        ///   // 魂石大师：按 stage.Num 从大到小，取第一个满足的档位
        ///
        /// 三个和装备属性不同的点：
        ///   ① 魂石的攻/防/血 **不乘品质系数**，直接进总属性（不进 equipPrimary）；
        ///   ② 生命百分比不进 HEALTH_PERCENTAGE 桶，而是最后按当前总生命一次性加成；
        ///   ③ 自然加成 / 魂石大师的属性都是直接加到总属性上。
        /// </summary>
        private static void AddGemstones(LocalRole role, Dictionary<Attribute, int> values)
        {
            if (role == null || role.EquipGems == null || role.EquipGems.Count == 0)
                return;

            var allGems = new List<int>();
            int gemHealthPercent = 0;

            foreach (KeyValuePair<EquipSlot, int[]> kv in role.EquipGems)
            {
                int slot = (int)kv.Key;
                if (slot <= (int)EquipSlot.INVALID_EquipSlot || slot >= (int)EquipSlot.MaxEquipSlot)
                    continue;
                int[] gems = kv.Value;
                if (gems == null)
                    continue;

                var natures = new List<int>();
                for (int i = 0; i < gems.Length; i++)
                {
                    int cfgId = gems[i];
                    if (cfgId <= 0)
                        continue;

                    CFGItem_Item gemCfg;
                    try { gemCfg = CFG_Item.GetById(cfgId); }
                    catch { gemCfg = null; }
                    if (gemCfg == null)
                        continue;

                    if (gemCfg.NatureMark > 0)
                        natures.Add(gemCfg.NatureMark);
                    allGems.Add(ToInt(gemCfg.itemlevel));

                    Add(values, Attribute.PHYSICAL_ATTACK, ToInt(gemCfg.Phyatk));
                    Add(values, Attribute.MAGIC_ATTACK, ToInt(gemCfg.Magatk));
                    Add(values, Attribute.PHYSICAL_DEFENSE, ToInt(gemCfg.Phydef));
                    Add(values, Attribute.MAGIC_DEFENSE, ToInt(gemCfg.Magdef));
                    Add(values, Attribute.MAX_HEALTH, ToInt(gemCfg.HP));
                    Add(values, Attribute.HIT, ToInt(gemCfg.Hit));
                    Add(values, Attribute.DODGE, ToInt(gemCfg.Miss));
                    Add(values, Attribute.CRITICAL_STRIKE, ToInt(gemCfg.Crit));
                    Add(values, Attribute.TOUGHNESS, ToInt(gemCfg.Decrit));
                    Add(values, Attribute.PVP_DAMAGE_PERCENTAGE, ToInt(gemCfg.PVPdamage));
                    Add(values, Attribute.PVE_DAMAGE_PERCENTAGE, ToInt(gemCfg.PVEdamage));
                    gemHealthPercent += ToInt(gemCfg.HPPercent);
                }

                // 魂石自然属性：同一件装备 4 个槽位全是同一种自然系才生效
                if (natures.Count == LocalRole.GemSlotCount
                    && natures[0] == natures[1]
                    && natures[1] == natures[2]
                    && natures[2] == natures[3])
                {
                    CFGItem_EquipMaster natureCfg;
                    try { natureCfg = CFG_EquipMaster.GetById(GemStoneNatureMasterBase + natures[0]); }
                    catch { natureCfg = null; }
                    if (natureCfg != null && natureCfg.Attribute != null)
                    {
                        for (int a = 0; a < natureCfg.Attribute.Length; a++)
                        {
                            // struct 数组元素必须先拷到局部变量再读字段（ILRuntime）
                            CFGItem_EquipMaster.AttributeItem attr = natureCfg.Attribute[a];
                            Add(values, (Attribute)attr.key, attr.value);
                        }
                    }
                }
            }

            AddGemstoneMaster(values, allGems);

            // 魂石增加生命百分比：按「当前累计生命」一次性加成（不是 HEALTH_PERCENTAGE 桶）
            if (gemHealthPercent > 0)
            {
                int maxHealth = Get(values, Attribute.MAX_HEALTH);
                if (maxHealth > 0)
                    Add(values, Attribute.MAX_HEALTH,
                        (int)Math.Round(maxHealth * (gemHealthPercent / (double)BaseRate)));
            }
        }

        /// <summary>
        /// 魂石大师：把全身所有魂石的等级降序排好，按 config.xml 的
        /// Diamond/master/stage（num 从大到小）逐档判定：
        ///   魂石总数 >= num 且 第 num 高的魂石等级 >= 该档 Limit → 激活并 break。
        ///
        /// 玩家和伙伴共用同一套判定（伙伴走 PartnerSlot.Equips[i].Gemstone），
        /// 所以这里放开成 public。
        /// </summary>
        public static void AddGemstoneMaster(Dictionary<Attribute, int> values, List<int> allGems)
        {
            if (allGems == null || allGems.Count == 0)
                return;

            // 降序插入排序（不用委托比较，避免 ILRuntime 下的委托排序问题）
            for (int i = 1; i < allGems.Count; i++)
            {
                int value = allGems[i];
                int j = i - 1;
                while (j >= 0 && allGems[j] < value)
                {
                    allGems[j + 1] = allGems[j];
                    j--;
                }
                allGems[j + 1] = value;
            }

            CFG_XMLConfig.XML_Config config;
            try { config = CFG_XMLConfig.xmlConfig; }
            catch { config = null; }
            if (config == null || config.Diamond_MasterDic == null || config.Diamond_MasterDic.Count == 0)
                return;

            int count = config.Diamond_MasterDic.Count;
            var stageNums = new int[count];
            var stageIds = new int[count];
            int n = 0;
            foreach (KeyValuePair<int, int> kv in config.Diamond_MasterDic)
            {
                stageIds[n] = kv.Key;
                stageNums[n] = kv.Value;
                n++;
            }

            // 原服是「配置顺序倒序遍历」，配置里 num 递增，等价于按 num 降序
            for (int i = 1; i < n; i++)
            {
                int num = stageNums[i];
                int id = stageIds[i];
                int j = i - 1;
                while (j >= 0 && stageNums[j] < num)
                {
                    stageNums[j + 1] = stageNums[j];
                    stageIds[j + 1] = stageIds[j];
                    j--;
                }
                stageNums[j + 1] = num;
                stageIds[j + 1] = id;
            }

            for (int i = 0; i < n; i++)
            {
                int num = stageNums[i];
                if (num <= 0 || allGems.Count < num)
                    continue;

                CFGItem_EquipMaster masterCfg;
                try { masterCfg = CFG_EquipMaster.GetById(stageIds[i]); }
                catch { masterCfg = null; }
                if (masterCfg == null)
                    continue;
                if (allGems[num - 1] < masterCfg.Limit)
                    continue;

                if (masterCfg.Attribute != null)
                {
                    for (int a = 0; a < masterCfg.Attribute.Length; a++)
                    {
                        CFGItem_EquipMaster.AttributeItem attr = masterCfg.Attribute[a];
                        Add(values, (Attribute)attr.key, attr.value);
                    }
                }
                break;
            }
        }

        /// <summary>
        /// 强化大师 / 升星大师，对齐原服 refreshStats 的这两段：
        ///
        ///   var maxEnhance, maxStars int32
        ///   maxEnhance = math.MaxInt32
        ///   maxStars   = math.MaxInt32
        ///   for slot, equip := range p.equips {                    // 固定 [MaxEquipSlot]Slot 数组
        ///       if slot &lt;= INVALID || slot &gt;= MaxEquipSlot { continue }
        ///       if equip.Enhance &lt; maxEnhance { maxEnhance = equip.Enhance }
        ///       if equip.Stars   &lt; maxStars   { maxStars   = equip.Stars }
        ///   }
        ///   for _, equipmaster := range global.EquipMasterCfg {
        ///       if equipmaster.Category == Enhance &amp;&amp; maxEnhance &gt;= equipmaster.Limit { enhanceCfg = equipmaster }
        ///       if equipmaster.Category == Stars   &amp;&amp; maxStars   &gt;= equipmaster.Limit { starCfg    = equipmaster }
        ///   }
        ///
        /// ⚠️ 关键词是「**全身最低**」：原服遍历的是固定 13 项数组，
        ///    没穿的槽位 Enhance/Stars 都是 0，所以**少穿一件、或某件没强化，大师就不生效**。
        ///    客户端 UI 也是同一套算法（RoleM.GetEquipReinforce / UIEquipMaster 伙伴分支都是
        ///    `for i = 1; i &lt; MaxEquipSlot; i++` 取 Math.Min），所以这里必须遍历 1..12
        ///    并用 GetEquipEnhance/GetEquipStars（空槽返回 0），不能只遍历「已穿戴的部位」。
        ///
        /// 之前本地只移植了魂石大师、漏了这两段（2026-10-28 补）。
        /// </summary>
        private static void AddEquipmentMaster(LocalRole role, Dictionary<Attribute, int> values)
        {
            if (role == null || values == null)
                return;

            int minEnhance = int.MaxValue;
            int minStars = int.MaxValue;
            for (int i = (int)EquipSlot.INVALID_EquipSlot + 1; i < (int)EquipSlot.MaxEquipSlot; i++)
            {
                int enhance = role.GetEquipEnhance((EquipSlot)i);
                int stars = role.GetEquipStars((EquipSlot)i);
                if (enhance < minEnhance)
                    minEnhance = enhance;
                if (stars < minStars)
                    minStars = stars;
            }
            if (minEnhance == int.MaxValue)
                minEnhance = 0;
            if (minStars == int.MaxValue)
                minStars = 0;

            AddEquipMasterByLimit(values, CFG_EquipMaster.CategoryClass.Reinforce, minEnhance);
            AddEquipMasterByLimit(values, CFG_EquipMaster.CategoryClass.UpStar, minStars);
        }

        /// <summary>
        /// 按 Category + Limit 取**最高达标档**并累加其属性。
        /// 原服是「遍历时不断覆盖 enhanceCfg」，配置按 UUID/Limit 升序，等价于取最后一个满足的档。
        ///
        /// 玩家侧（强化/升星大师）和伙伴侧（PartnerService.AddPartnerEquipStats）共用这一份。
        /// </summary>
        public static void AddEquipMasterByLimit(Dictionary<Attribute, int> values,
            CFG_EquipMaster.CategoryClass category, int achieved)
        {
            if (values == null || achieved <= 0)
                return;

            List<CFGItem_EquipMaster> masters;
            try { masters = CFG_EquipMaster.GetMastersByCatedory(category); }
            catch { masters = null; }
            if (masters == null || masters.Count == 0)
                return;

            CFGItem_EquipMaster best = null;
            for (int i = 0; i < masters.Count; i++)
            {
                CFGItem_EquipMaster cfg = masters[i];
                if (cfg == null || cfg.Limit > achieved)
                    continue;
                if (best == null || cfg.Limit > best.Limit)
                    best = cfg;
            }
            if (best == null || best.Attribute == null)
                return;

            for (int a = 0; a < best.Attribute.Length; a++)
            {
                // struct 数组元素先拷局部变量再读字段（ILRuntime）
                CFGItem_EquipMaster.AttributeItem attr = best.Attribute[a];
                Add(values, (Attribute)attr.key, attr.value);
            }
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

        /// <summary>原服常量 PassiveSkill。</summary>
        private const int PassiveSkill = 2;

        /// <summary>
        /// 被动技能属性，对齐原服 refreshStats 的「// 被动技能」段：
        ///
        ///   for _, v := range p.skills {
        ///       if skillCfg.ActiveorPassive == PassiveSkill {
        ///           for _, e := range skillCfg.Effect {
        ///               p.stats.setStats(pb.Attribute(e.Key), e.Value)
        ///           }
        ///       }
        ///   }
        ///
        /// 也就是「已学的每个被动技能的 SkillEffect 逐条累加」。
        /// 玩家三职业的被动只有 3 个，门槛分别 20 / 60 / 100 级，各 1 级：
        ///   战士 27000[灵活] DODGE+20 / 28000[坚韧] TOUGHNESS+30 / 29000[厚泽] HP+5%
        ///   法师 30000[精准] HIT+25   / 31000[爆发] CRITICAL+35 / 32000[勇猛] 攻+5%
        ///   祭祀 33000[精准] HIT+25   / 34000[爆发] CRITICAL+35 / 35000[勇猛] 防+5%
        /// </summary>
        private static void AddPassiveSkills(LocalRole role, Dictionary<Attribute, int> values)
        {
            if (role == null || values == null || role.Skills == null)
                return;

            for (int i = 0; i < role.Skills.Count; i++)
            {
                SkillInfo skill = role.Skills[i];
                if (skill == null || skill.CfgId <= 0)
                    continue;

                CFGItem_Skill cfg;
                try { cfg = CFG_Skill.GetById(skill.CfgId); }
                catch { cfg = null; }
                if (cfg == null)
                    continue;

                int activeOrPassive;
                try { activeOrPassive = (int)cfg.ActiveorPassive; }
                catch { continue; }
                if (activeOrPassive != PassiveSkill)
                    continue;

                Dictionary<string, int>[] effect = cfg.SkillEffect;
                if (effect == null)
                    continue;
                for (int e = 0; e < effect.Length; e++)
                {
                    Dictionary<string, int> item = effect[e];
                    if (item == null)
                        continue;
                    foreach (KeyValuePair<string, int> kv in item)
                    {
                        int attrId;
                        if (!int.TryParse(kv.Key, out attrId))
                            continue;
                        if (attrId <= (int)Attribute.INVALID_Attribute
                            || attrId >= (int)Attribute.MAX_Attribute)
                            continue;
                        Add(values, (Attribute)attrId, kv.Value);
                    }
                }
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

        /// <summary>
        /// 装备强化数值的品质系数，对应 config.xml 的 Equipenhance/quality：
        ///   1→1  2→1.25  3→1.55  4→1.95  5→2.5  6→3.2  7→4  8→5
        /// 注意这跟 GetQualityCoefficient（装备基础值的品质系数）是两套配置。
        /// </summary>
        private static float GetEnhanceQualityCoef(int quality)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.EquipEnhanceDic != null)
                {
                    float value;
                    if (config.EquipEnhanceDic.TryGetValue(quality, out value) && value > 0)
                        return value;
                }
            }
            catch
            {
                // 读不到就按 1 处理
            }
            return 1f;
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
