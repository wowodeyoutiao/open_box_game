using System;
using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 战宠领域服务（第 55 节）。
    ///
    /// 原服的宠物逻辑散在 player.go 里（pet.go 只有 30 行），本服务按功能收口：
    ///   · 品质系数 / 升级经验系数 / 包裹格数（读 config.xml 的 Petquality / Petlevel / petbag）
    ///   · 宠物槽位（21 格：0 = 上阵位，1..20 = 包裹格）
    ///   · 获得宠物（对齐 Player.addPet 的槽位分配与包裹上限）
    ///
    /// **宠物不参战**：desk.go 里没有任何宠物实体，宠物对战斗的唯一影响是属性加成，
    /// 而且只取 0 号槽位那一只（原服 refreshStats 里的 p.pet[0]）。属性结算在
    /// RoleStatService.AddPets，本服务只提供数据与算法参数。
    /// </summary>
    public class PetService : SingletonTemplate<PetService>
    {
        /// <summary>宠物属性配置的职业号（原服 constant.Pet = 21，role.json 里 job=21 共 150 级）。</summary>
        public const int PetJob = 21;

        /// <summary>包裹格数量上限（config.xml &lt;petbag totlenum="20"/&gt; 的兜底值）。</summary>
        private const int FallbackBagTotal = 20;

        /// <summary>测试钩子：>= 0 时强制进阶暴击倍率（-1 = 按配置随机）。用于 55-B 的升星测试。</summary>
        public int ForcedStageRate = -1;

        // ==================== 配置读取 ====================

        /// <summary>
        /// 宠物品质系数（config.xml Petquality，对齐原服 GetpetQualityRate）。
        /// 作用在「等级基础属性」上：值 × 系数。读不到返回 1。
        /// </summary>
        public static float GetQualityRate(int quality)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.PetQualityDic != null)
                {
                    float rate;
                    if (config.PetQualityDic.TryGetValue(quality, out rate))
                        return rate;
                }
            }
            catch { }
            return 1f;
        }

        /// <summary>
        /// 宠物升级所需经验的品质系数（config.xml Petlevel，对齐原服 GetpetLevelRate）。
        /// 读不到返回 1。
        /// </summary>
        public static float GetLevelRate(int quality)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.PetLevelDic != null)
                {
                    float rate;
                    if (config.PetLevelDic.TryGetValue(quality, out rate))
                        return rate;
                }
            }
            catch { }
            return 1f;
        }

        /// <summary>包裹总格数（config.xml petbag totlenum）。</summary>
        public static int GetBagTotal()
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.PetBagTotal > 0)
                    return config.PetBagTotal;
            }
            catch { }
            return FallbackBagTotal;
        }

        /// <summary>
        /// 当前等级能解锁的包裹格数（对齐原服 addPet 里的 canhave 计算）：
        /// 遍历 config.xml &lt;Petbag&gt; 的「格数 → 需要等级」，取等级已满足的最大格数。
        /// 配置里全是 Level=1（10..20 格），所以低等级也是满 20 格。
        /// </summary>
        public static int GetUnlockedBagSlots(LocalRole role)
        {
            int level = role == null ? 1 : role.Level;
            int canHave = 0;
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.PetBagDic != null && config.PetBagDic.Count > 0)
                {
                    foreach (KeyValuePair<int, int> kv in config.PetBagDic)
                    {
                        // 配置存的是「格数 → 需要等级」
                        if (level >= kv.Value && kv.Key > canHave)
                            canHave = kv.Key;
                    }
                    return canHave;
                }
            }
            catch { }
            return GetBagTotal();
        }

        // ==================== 槽位 ====================

        /// <summary>
        /// 找第一个空的包裹格（对齐原服 checkEmptyPetSlot）：
        /// **跳过 0 号上阵位**（上阵位空着也不能自动塞新宠物进去，必须玩家拖上去）。
        /// 返回 -1 表示包裹已满。
        /// </summary>
        public int FindEmptySlot(LocalRole role)
        {
            if (role == null)
                return -1;
            role.EnsurePetSlots();
            for (int slot = LocalRole.ActivePetSlot + 1; slot < LocalRole.PetSlotCount; slot++)
            {
                PetSlotInfo pet = role.PetSlots[slot];
                if (pet == null || pet.CfgId <= 0)
                    return slot;
            }
            return -1;
        }

        /// <summary>某个宠物槽位（0 = 上阵）上的宠物配置；空槽返回 null。</summary>
        public static CFGItem_Pet GetPetCfgBySlot(LocalRole role, int slotId)
        {
            if (role == null)
                return null;
            PetSlotInfo pet = role.GetPetSlot(slotId);
            if (pet == null || pet.CfgId <= 0)
                return null;
            try { return CFG_Pet.GetById(pet.CfgId); }
            catch { return null; }
        }

        // ==================== 获得宠物 ====================

        /// <summary>
        /// 获得一只宠物（对齐原服 Player.addPet 的核心）：
        ///   1) 找一个空包裹格（0 号上阵位不自动占用）；
        ///   2) 该格必须落在「当前等级解锁的包裹格数」之内，否则算包裹已满；
        ///   3) 写入槽位（品质取配置，经验从 0 开始）；
        ///   4) 派发 ANY_PET_UPGRADED_TO_LEVEL + 记录宠物图鉴。
        ///
        /// 参数 level / star 用于离线奖励、活动直接给高等级宠物等入口（原服 addPet 也接 star）。
        /// 返回写入的槽位对象，失败返回 null 并写 error。
        /// </summary>
        public PetSlotInfo GrantPet(LocalRole role, int cfgId, int level, int star, out string error)
        {
            // level → 经验，再走内部实现（原服 addPet 也是先 petExpForLevel(1, exp, quality)）
            long exp = 0;
            if (level > 1)
                exp = LevelToExp(level, 0, GetQualityOf(cfgId));
            return GrantPetInternal(role, cfgId, exp, star, 0, out error);
        }

        /// <summary>取宠物品质（配置里的 Quality），读不到返回 1。</summary>
        private static int GetQualityOf(int cfgId)
        {
            CFGItem_Pet cfg = GetCfg(cfgId);
            return cfg == null ? 1 : cfg.Quality;
        }

        /// <summary>
        /// 获得宠物的统一实现（原服 addPet 的完整语义）：
        ///   exp 是**总经验**，会按品质经验系数折算成等级 + 剩余经验；
        ///   targetSlot &gt; 0 时直接写到该槽（宠物合成用），否则找第一个空包裹格。
        /// </summary>
        public PetSlotInfo GrantPetInternal(
            LocalRole role, int cfgId, long exp, int star, int targetSlot, out string error)
        {
            error = "";
            if (role == null)
            {
                error = "角色不存在";
                return null;
            }
            if (cfgId <= 0)
            {
                error = "宠物配置不存在";
                return null;
            }

            CFGItem_Pet petCfg;
            try { petCfg = CFG_Pet.GetById(cfgId); }
            catch { petCfg = null; }
            if (petCfg == null)
            {
                error = "宠物配置不存在";
                return null;
            }

            int slot = targetSlot;
            if (slot <= 0)
            {
                slot = FindEmptySlot(role);
                if (slot < 0)
                {
                    error = "宠物背包已满";
                    return null;
                }
                // 原服：包内格序号 > 当前等级解锁的格数 → 视为包裹已满
                if (slot > GetUnlockedBagSlots(role))
                {
                    error = "宠物背包已满";
                    return null;
                }
            }

            // 经验 → 等级（原服 addPet 里 petExpForLevel(1, exp, quality)）
            int level = 1;
            int newExp = 0;
            if (exp > 0)
                ExpToLevel(1, exp, petCfg.Quality, out level, out newExp);
            if (star < 0)
                star = 0;

            var info = new PetSlotInfo
            {
                Uid = LocalData.Instance.AllocateItemUid(),
                CfgId = cfgId,
                Level = level,
                Star = star,
                Exp = newExp,
                ExpStar = 0,
                Quality = petCfg.Quality,
            };
            role.EnsurePetSlots();
            role.PetSlots[slot] = info;

            TaskService.Instance.Dispatch(role, TargetType.ANY_PET_UPGRADED_TO_LEVEL, 0, level);
            MarkEverHave(role, cfgId);

            Log.Print("[PetService] 获得宠物 cfg=" + cfgId + " slot=" + slot
                + " lv=" + level + " star=" + star + " exp=" + newExp);
            return info;
        }

        /// <summary>记录宠物图鉴（原服 petEverHave）。</summary>
        public static void MarkEverHave(LocalRole role, int cfgId)
        {
            if (role == null || cfgId <= 0)
                return;
            if (role.PetEverHave == null)
                role.PetEverHave = new List<int>();
            if (!role.PetEverHave.Contains(cfgId))
                role.PetEverHave.Add(cfgId);
        }

        // ==================== 经验换算（对齐原服 petExpForLevel / petExpForExp）====================

        /// <summary>
        /// 把经验换算成「等级 + 剩余经验」（原服 petExpForLevel）：
        /// 每级需要 round(RoleCfg(Pet, 当前等级).NeedEXP × 品质经验系数)，够了就升级。
        /// </summary>
        public static void ExpToLevel(int level, long exp, int quality, out int newLevel, out int newExp)
        {
            if (level < 1)
                level = 1;
            float rate = GetLevelRate(quality);
            newLevel = level;
            long rest = exp < 0 ? 0 : exp;
            // 原服 for i := 0; i < len(RoleCfg) —— 上限就是 150 级（宠物表行数）
            for (int guard = 0; guard < MaxPetLevel; guard++)
            {
                CFGItem_Role roleCfg = GetPetRoleCfg(newLevel);
                if (roleCfg == null)
                    break;
                long need = (long)Math.Round(roleCfg.NeedEXP * rate, MidpointRounding.AwayFromZero);
                if (need <= 0)
                    break;
                if (rest < need)
                    break;
                rest -= need;
                newLevel++;
            }
            newExp = (int)rest;
        }

        /// <summary>
        /// 把「等级 + 当前经验」折成总经验（原服 petExpForExp，宠物合成时用来累加经验）：
        /// 累加 1..level-1 每级的 needExp，再加上当前经验。
        /// </summary>
        public static long LevelToExp(int level, int exp, int quality)
        {
            if (level < 1)
                level = 1;
            float rate = GetLevelRate(quality);
            long total = exp < 0 ? 0 : exp;
            for (int i = 1; i < level; i++)
            {
                CFGItem_Role roleCfg = GetPetRoleCfg(i);
                if (roleCfg == null)
                    break;
                total += (long)Math.Round(roleCfg.NeedEXP * rate, MidpointRounding.AwayFromZero);
            }
            return total;
        }

        /// <summary>宠物等级上限（role.json 的 job=21 行数，配置为 150）。</summary>
        private const int MaxPetLevel = 150;

        /// <summary>取宠物职业某等级的基础属性配置；越界/缺失返回 null。</summary>
        private static CFGItem_Role GetPetRoleCfg(int level)
        {
            try { return CFG_Role.GetByJobAndLevel(PetJob, level); }
            catch { return null; }
        }

        /// <summary>给宠物加经验（原服 petAddExp），返回是否有升级。</summary>
        public bool AddExp(LocalRole role, int slotId, long exp, out string error)
        {
            error = "";
            PetSlotInfo pet = role == null ? null : role.GetPetSlot(slotId);
            if (pet == null || pet.CfgId <= 0)
            {
                error = "宠物不存在";
                return false;
            }
            CFGItem_Pet petCfg = GetCfg(pet.CfgId);
            if (petCfg == null)
            {
                error = "宠物配置不存在";
                return false;
            }

            int oldLevel = pet.Level;
            int newLevel;
            int newExp;
            ExpToLevel(pet.Level, exp, pet.Quality, out newLevel, out newExp);
            if (newLevel > MaxPetLevel)
                newLevel = MaxPetLevel;

            pet.Level = newLevel;
            pet.Exp = newExp;
            TaskService.Instance.Dispatch(role, TargetType.ANY_PET_UPGRADED_TO_LEVEL, 0, newLevel);
            return newLevel != oldLevel;
        }

        // ==================== 升级（原服 petLevel / C2S_PetLevel）====================

        /// <summary>
        /// 宠物升级：消耗「宠物经验道具」（item 83），一次升一级，经验清零。
        ///
        /// 原服 petLevel 的三处细节都照抄：
        ///   ① 需要经验 = round(RoleCfg(Pet, 当前等级).NeedEXP × 品质经验系数) − 当前经验，
        ///      所以攒了部分经验时只补差额；
        ///   ② 满级判定用的是 `pet.Star >= len(StarConsume)`（不是等级！）——
        ///      配置里 StarConsume 有 10 档，即**满星（10 星）后不能再升级**；
        ///   ③ 升级后 Exp 直接清零（不保留溢出）。
        /// </summary>
        public CommonResponse LevelUp(int slotId)
        {
            var response = new CommonResponse { Code = -1, Error = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "角色不存在";
                return response;
            }

            PetSlotInfo pet = role.GetPetSlot(slotId);
            if (pet == null || pet.CfgId <= 0)
            {
                response.Error = "宠物不存在";
                return response;
            }

            CFGItem_Pet petCfg = GetCfg(pet.CfgId);
            if (petCfg == null)
            {
                response.Error = "宠物配置不存在";
                return response;
            }

            // 原服：if pet.Star >= len(petCfg.StarConsume) → ErrMaxLevel（满星即满级）
            if (petCfg.StarConsume != null && pet.Star >= petCfg.StarConsume.Length)
            {
                response.Error = "已满级";
                return response;
            }

            CFGItem_Role roleCfg = GetPetRoleCfg(pet.Level);
            if (roleCfg == null)
            {
                response.Error = "宠物等级配置不存在";
                return response;
            }

            float rate = GetLevelRate(pet.Quality);
            long needExp = (long)Math.Round(roleCfg.NeedEXP * rate, MidpointRounding.AwayFromZero)
                - pet.Exp;
            if (needExp < 0)
                needExp = 0;

            if (ItemService.CountItem(role, PetExpItemId) < needExp)
            {
                response.Error = "宠物经验不足";
                return response;
            }
            if (needExp > 0 && !ItemService.ConsumeItem(role, PetExpItemId, needExp))
            {
                response.Error = "宠物经验不足";
                return response;
            }

            pet.Level++;
            pet.Exp = 0;
            TaskService.Instance.Dispatch(role, TargetType.ANY_PET_UPGRADED_TO_LEVEL, 0, pet.Level);

            response.Code = 0;
            Commit(role, true);
            return response;
        }

        // ==================== 升星 / 进阶（原服 petStar / C2S_PetStar）====================

        /// <summary>
        /// 宠物升星（游戏里叫「进阶」）：消耗进阶材料，按 PetAdvance.rate 随机倍率，
        /// 攒满 StarConsume[star].values 就升一星；等级不够则**卡在满经验**不升星。
        ///
        /// 原服 petStar 的照抄点（有几处是原服自身写法上的怪癖，为保证行为一致都保留）：
        ///   ① 每次调用只累加**一份**材料经验：EndExp = 当前ExpStar + 材料基础值 × 随机倍率，
        ///      即使 Num &gt; 1 也只加一份（但材料照样扣 Num 个）；
        ///   ② 材料基础值取 PetAdvance/basevalue/item 里该道具的 num（84→10 / 85→40 / 86→150 / 87→500）；
        ///   ③ 随机倍率 70% ×1 / 20% ×2 / 10% ×3（PetAdvance/rate/stage）；
        ///   ④ 等级不足时：经验没满就补到满（EndExp = 满值），已经满了就报「宠物等级不足」。
        /// </summary>
        public PetStarResponse StarUp(int slotId, int cfgId, int num)
        {
            var response = new PetStarResponse { Code = -1, Error = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "角色不存在";
                return response;
            }
            if (num <= 0)
                num = 1;

            PetSlotInfo pet = role.GetPetSlot(slotId);
            if (pet == null || pet.CfgId <= 0)
            {
                response.Error = "宠物不存在";
                return response;
            }

            CFGItem_Pet petCfg = GetCfg(pet.CfgId);
            if (petCfg == null)
            {
                response.Error = "宠物配置不存在";
                return response;
            }
            if (petCfg.StarConsume == null || petCfg.StarConsume.Length == 0)
            {
                response.Error = "宠物没有进阶配置";
                return response;
            }
            if (pet.Star >= petCfg.StarConsume.Length)
            {
                response.Error = "已满阶";
                return response;
            }
            if (ItemService.CountItem(role, cfgId) < num)
            {
                response.Error = "道具数量不足";
                return response;
            }

            // 当前星级对应的消耗档：StarConsume 的 id 是 1..N，宠物 Star 是 0..N-1
            CFGItem_Pet.ConsumeItem consume = default(CFGItem_Pet.ConsumeItem);
            bool found = false;
            for (int i = 0; i < petCfg.StarConsume.Length; i++)
            {
                CFGItem_Pet.ConsumeItem item = petCfg.StarConsume[i];
                if (item.id - 1 == pet.Star)
                {
                    consume = item;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                response.Error = "宠物进阶配置不存在";
                return response;
            }

            long materialValue = GetAdvanceMaterialValue(cfgId);
            if (materialValue <= 0)
            {
                response.Error = "进阶道具配置不存在";
                return response;
            }

            int per = RollStageRate();
            if (per < 0)
            {
                response.Error = "进阶倍率配置不存在";
                return response;
            }
            response.ResPer = per;

            long endExp = pet.ExpStar + materialValue * per;
            if (endExp >= consume.values)
            {
                endExp -= consume.values;
                int needLevel = GetStarLimitLevel(petCfg, pet.Star + 1);
                if (pet.Level < needLevel)
                {
                    // 等级不够：经验没攒满就先补满（相当于存经验），攒满了才报错
                    if (pet.ExpStar != consume.values)
                        endExp = consume.values;
                    else
                    {
                        response.Error = "宠物等级不足";
                        return response;
                    }
                }
                else
                {
                    pet.Star++;
                }
            }

            if (!ItemService.ConsumeItem(role, cfgId, num))
            {
                response.Error = "道具数量不足";
                return response;
            }
            pet.ExpStar = (int)endExp;

            response.Code = 0;
            Commit(role, true);
            return response;
        }

        /// <summary>进阶材料的基础经验值（PetAdvance/basevalue/item 的 num）。</summary>
        private static long GetAdvanceMaterialValue(int itemCfgId)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config == null || config.PetAdvanceUseds == null)
                    return 0;
                for (int i = 0; i < config.PetAdvanceUseds.Length; i++)
                {
                    // struct 数组元素先拷局部变量再读字段（ILRuntime）
                    CFG_XMLConfig.XML_Config.GoodItem good = config.PetAdvanceUseds[i];
                    if (good.id == itemCfgId)
                        return good.num;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>升到 nextStar 需要的宠物等级（StarLimit[id = nextStar].values）。</summary>
        private static int GetStarLimitLevel(CFGItem_Pet petCfg, int nextStar)
        {
            if (petCfg == null || petCfg.StarLimit == null)
                return 0;
            for (int i = 0; i < petCfg.StarLimit.Length; i++)
            {
                CFGItem_Pet.ConsumeItem limit = petCfg.StarLimit[i];
                if (limit.id == nextStar)
                    return limit.values;
            }
            return 0;
        }

        /// <summary>
        /// 进阶随机倍率（原服 petStageRand）：按 PetAdvance/rate/stage 的 pro 万分比累加分段，
        /// rand = RandIn(total) + 1 落在 [1, total]。测试钩子 ForcedStageRate &gt;= 0 时直接返回它。
        /// </summary>
        public int RollStageRate()
        {
            if (ForcedStageRate >= 0)
                return ForcedStageRate;

            CFG_XMLConfig.XML_Config config = null;
            try { config = CFG_XMLConfig.xmlConfig; }
            catch { }
            if (config == null || config.PetAdvanceStages == null
                || config.PetAdvanceStages.Length == 0)
                return -1;

            int total = 0;
            for (int i = 0; i < config.PetAdvanceStages.Length; i++)
                total += config.PetAdvanceStages[i].pro;
            if (total <= 0)
                return -1;

            int rand = _rng.Next(total) + 1;
            int begin = 0;
            for (int i = 0; i < config.PetAdvanceStages.Length; i++)
            {
                CFG_XMLConfig.XML_Config.PetStageRate stage = config.PetAdvanceStages[i];
                if (begin + stage.pro >= rand)
                    return stage.rate;
                begin += stage.pro;
            }
            return -1;
        }

        // ==================== 换位 / 喂经验 / 合成（原服 petMove / C2S_PetMove）====================

        /// <summary>
        /// 宠物换位，原服 petMove 的三条分支都在这里：
        ///
        ///   ① 源槽为空 → 报错（拖不动空槽）；
        ///   ② 源是**经验宝宝**（PetKind.key == 2）且目标槽有宠物 →
        ///      把「经验宝宝自带经验 value + 目标已有经验 + 经验宝宝等级折算的总经验」全喂给目标，
        ///      经验宝宝消失（这是唯一的喂经验途径）；
        ///   ③ 两个槽都有宠物且**品质相同** → 走合成：
        ///      用 compose 表反查两只宠物道具共同的合成档（取 Raw 最短的一档），
        ///      按 ratio 随机产出新宠物，等级由两只宠物总经验折算，星级取两者较大值；
        ///   ④ 其余情况（空槽、品质不同、合成配置缺失）→ 直接互换。
        /// </summary>
        public PetMoveResponse Move(int fromSlotId, int toSlotId)
        {
            var response = new PetMoveResponse { Code = -1, Error = "", SlotId = toSlotId };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "角色不存在";
                return response;
            }
            role.EnsurePetSlots();
            if (fromSlotId < 0 || fromSlotId >= LocalRole.PetSlotCount
                || toSlotId < 0 || toSlotId >= LocalRole.PetSlotCount)
            {
                response.Error = "宠物槽位不存在";
                return response;
            }

            PetSlotInfo from = role.PetSlots[fromSlotId];
            if (from == null || from.CfgId <= 0)
            {
                response.Error = "宠物不存在";
                return response;
            }
            PetSlotInfo to = role.PetSlots[toSlotId];
            if (to == null)
            {
                to = new PetSlotInfo();
                role.PetSlots[toSlotId] = to;
            }

            CFGItem_Pet fromCfg = fromSlotId == LocalRole.ActivePetSlot ? null : GetCfg(from.CfgId);
            CFGItem_Pet toCfg = toSlotId == LocalRole.ActivePetSlot ? null : GetCfg(to.CfgId);

            // ① 经验宝宝喂给目标槽
            if (fromSlotId != LocalRole.ActivePetSlot && fromCfg != null
                && fromCfg.IsExpPet && toSlotId != LocalRole.ActivePetSlot && to.CfgId > 0)
            {
                long fromExp = LevelToExp(from.Level, from.Exp, from.Quality);
                long allExp = (fromCfg.PetKind != null ? fromCfg.PetKind.value : 0)
                    + to.Exp + fromExp;
                string error;
                AddExp(role, toSlotId, allExp, out error);
                role.PetSlots[fromSlotId] = new PetSlotInfo();
                response.Code = 0;
                Commit(role, true);
                return response;
            }

            // ② 品质相同 → 宠物合成
            if (fromSlotId != LocalRole.ActivePetSlot && toSlotId != LocalRole.ActivePetSlot
                && fromCfg != null && toCfg != null
                && from.CfgId > 0 && to.CfgId > 0
                && from.Quality == to.Quality)
            {
                PetMoveResponse composeResp = TryComposePet(role, from, to, fromSlotId, toSlotId);
                if (composeResp != null)
                {
                    Commit(role, true);
                    return composeResp;
                }
                // 合成配置缺失 → 原服是把两个槽互换并返回 ErrComposeError
                SwapSlots(role, fromSlotId, toSlotId);
                response.Error = "宠物合成失败";
                Commit(role, true);
                return response;
            }

            // ③ 默认：互换
            SwapSlots(role, fromSlotId, toSlotId);
            response.Code = 0;
            response.PetcfgId = from.CfgId;
            response.NewPetCfgId = to.CfgId;
            Commit(role, true);
            return response;
        }

        private static void SwapSlots(LocalRole role, int fromSlotId, int toSlotId)
        {
            PetSlotInfo tmp = role.PetSlots[toSlotId];
            role.PetSlots[toSlotId] = role.PetSlots[fromSlotId];
            role.PetSlots[fromSlotId] = tmp;
        }

        /// <summary>
        /// 宠物合成（原服 petMove 里「品质相同」那段）。成功返回 Code=0 的响应；
        /// 合成配置对不上时返回 null（由调用方换位 + 报 ErrComposeError）。
        ///
        /// 原服细节：
        ///   · 用两只宠物的**道具 id**（ItemID，如 50001）去 compose 表反查共同的 ComposeID；
        ///   · 有多档可选时取 Raw 数组**最短**的那一档（子集优先，避免跳过档位）；
        ///   · 产出 id 再反查宠物配置（ItemID → 宠物），查不到就是 newPetId=0；
        ///   · 总经验 = 两只宠物各自折算的总经验之和，星级取较大值；
        ///   · 合成后写入**目标槽**（原服 addPet(..., toSlotId, ...)）。
        /// </summary>
        private PetMoveResponse TryComposePet(
            LocalRole role, PetSlotInfo from, PetSlotInfo to, int fromSlotId, int toSlotId)
        {
            CFGItem_Pet fromCfg = GetCfg(from.CfgId);
            CFGItem_Pet toCfg = GetCfg(to.CfgId);
            if (fromCfg == null || toCfg == null)
                return null;

            List<int> fromComposes = ComposeService.GetComposeIdsByRaw(fromCfg.ItemID);
            List<int> toComposes = ComposeService.GetComposeIdsByRaw(toCfg.ItemID);
            if (fromComposes.Count == 0 || toComposes.Count == 0)
                return null;

            var composeIds = new List<int>();
            for (int i = 0; i < fromComposes.Count; i++)
            {
                for (int j = 0; j < toComposes.Count; j++)
                {
                    if (fromComposes[i] == toComposes[j])
                    {
                        composeIds.Add(fromComposes[i]);
                        break;
                    }
                }
            }
            if (composeIds.Count == 0)
                return null;

            int composeId = composeIds[0];
            for (int i = 1; i < composeIds.Count; i++)
            {
                CFGItem_Compose best = GetComposeCfg(composeId);
                CFGItem_Compose cur = GetComposeCfg(composeIds[i]);
                if (cur != null && best != null
                    && cur.Raw != null && best.Raw != null
                    && cur.Raw.Length < best.Raw.Length)
                    composeId = composeIds[i];
            }

            CFGItem_Compose composeCfg = GetComposeCfg(composeId);
            if (composeCfg == null)
                return null;

            int newItemId = ComposeService.Instance.RollProduced(composeCfg);
            if (newItemId <= 0)
                return null;

            // 道具 id → 宠物配置 id
            int newPetCfgId = 0;
            CFGItem_Pet newPetCfg = CFG_Pet.GetByItemId(newItemId);
            if (newPetCfg != null)
                newPetCfgId = newPetCfg.ID;

            long allExp = LevelToExp(from.Level, from.Exp, from.Quality)
                + LevelToExp(to.Level, to.Exp, to.Quality);
            int star = from.Star > to.Star ? from.Star : to.Star;

            // 先清空两个槽，再按「目标槽」写回（原服 addPet(..., toSlotId, ...)）
            role.PetSlots[fromSlotId] = new PetSlotInfo();
            role.PetSlots[toSlotId] = new PetSlotInfo();

            var response = new PetMoveResponse { Code = 0, Error = "", SlotId = toSlotId };
            if (newPetCfgId <= 0)
            {
                // 产出道具不是宠物（配置缺失）：材料已经消耗，按失败返回，避免凭空造宠物
                response.Code = -1;
                response.Error = "宠物合成失败";
                return response;
            }

            string error;
            PetSlotInfo created = GrantPetInternal(role, newPetCfgId, allExp, star, toSlotId, out error);
            if (created == null)
            {
                response.Code = -1;
                response.Error = error;
                return response;
            }
            response.PetcfgId = newPetCfgId;
            response.NewPetCfgId = newPetCfgId;
            return response;
        }

        private static CFGItem_Compose GetComposeCfg(int composeId)
        {
            try { return CFG_Compose.GetById(composeId); }
            catch { return null; }
        }

        // ==================== 获取入口 ====================

        /// <summary>
        /// 背包里收到「战宠道具」（item.Type = 9，比如 50001 尊·卡巴尔烈焰犬）时，
        /// **直接变成宠物**，不落背包 —— 对齐原服 addSource 里的
        /// <c>else if itemCfg.MainType == constant.ItemPet { for petCfg { if ItemID == id { addPet } } }</c>。
        ///
        /// 返回是否被当作宠物道具处理掉了（true 时调用方不要再往背包塞）。
        /// </summary>
        public bool GrantPetByItem(LocalRole role, int itemCfgId, long num)
        {
            if (role == null || itemCfgId <= 0 || num <= 0)
                return false;

            CFGItem_Pet petCfg;
            try { petCfg = CFG_Pet.GetByItemId(itemCfgId); }
            catch { petCfg = null; }
            if (petCfg == null)
                return false;

            int count = num > 20 ? 20 : (int)num;   // 一次最多 20 只，避免异常入参打爆背包
            int granted = 0;
            string error = "";
            for (int i = 0; i < count; i++)
            {
                if (GrantPetInternal(role, petCfg.ID, 0, 0, 0, out error) == null)
                    break;
                granted++;
            }
            Log.Print("[PetService] 道具转宠物 item=" + itemCfgId + " cfg=" + petCfg.ID
                + " 需求=" + num + " 成功=" + granted
                + (error == "" ? "" : " 中断原因=" + error));
            return granted > 0;
        }

        // ==================== 上阵宠物离线收益 ====================

        /// <summary>
        /// 上阵宠物的挂机产出（原服 pet.OfflineAward + 上线/每日 tick 里那段结算）。
        ///
        /// 配置形态：pet.json 的 OfflineAward 按**星级**分组，每项
        /// <c>{id: 道具id, num: 每 gap 秒产出量, gap: 间隔秒, max: 累计上限}</c>
        ///   · id = 2（经验）→ 玩家经验（走 addSource，和原服 ItemTypeExp 一致）
        ///   · id = 1（金币）→ 金币
        /// 原服用 petExp / petGold 两个累计值，达到配置的 max 后不再产出（是**终身累计**上限，
        /// 不是每轮上限），这里沿用同一语义。
        ///
        /// 结算锚点用 role.PetAwardAnchorUnix：没宠物/没配置时也会把锚点推到当前时间，
        /// 避免「换上一只没配置的宠物又换回来」时把中间这段时间补发出来。
        /// </summary>
        public void SettleOfflineAward(LocalRole role)
        {
            if (role == null)
                return;

            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PetSlotInfo pet = role.GetActivePet();
            CFGItem_Pet petCfg = pet == null ? null : GetCfg(pet.CfgId);
            CFGItem_Pet.OfflineItem.Item[] awards =
                petCfg == null ? null : petCfg.GetOfflineAwardByStar(pet.Star);

            if (awards == null || awards.Length == 0)
            {
                role.PetAwardAnchorUnix = now;
                return;
            }

            if (role.PetAwardAnchorUnix <= 0 || role.PetAwardAnchorUnix > now)
            {
                role.PetAwardAnchorUnix = now;
                return;
            }

            long elapsed = now - role.PetAwardAnchorUnix;
            if (elapsed <= 0)
                return;

            bool granted = false;
            for (int i = 0; i < awards.Length; i++)
            {
                CFGItem_Pet.OfflineItem.Item award = awards[i];
                if (award == null || award.id <= 0 || award.num <= 0 || award.gap <= 0)
                    continue;

                long times = elapsed / award.gap;
                if (times <= 0)
                    continue;

                long gain = award.num * times;
                if (award.max > 0)
                {
                    // 累计上限：按「已经产出过多少」扣额度
                    long already = award.id == 1 ? role.PetAwardGoldTotal
                        : award.id == 2 ? role.PetAwardExpTotal : 0;
                    long left = award.max - already;
                    if (left <= 0)
                        continue;
                    if (gain > left)
                        gain = left;
                }

                if (gain <= 0)
                    continue;
                if (!ItemService.Instance.GrantReward(role, award.id, gain))
                    continue;

                if (award.id == 1)
                    role.PetAwardGoldTotal += gain;
                else if (award.id == 2)
                    role.PetAwardExpTotal += gain;
                granted = true;
                Log.Print("[PetService] 宠物挂机产出 item=" + award.id + " num=" + gain
                    + " (间隔" + award.gap + "s × " + times + "次)");
            }

            role.PetAwardAnchorUnix = now;
            if (granted)
            {
                SaveCoordinator.Instance.MarkDirty();
                LocalPusher.Instance.PushUserBaseNow();
                LocalPusher.Instance.PushBagInfoNow();
            }
        }

        /// <summary>上次挂机结算的秒数（每帧调也只每秒真正结算一次）。</summary>
        private static long _lastAwardTickSec;

        /// <summary>
        /// 在线时的宠物挂机结算入口（BattleTicker 每帧调）。
        /// 原服是在每秒的 tick 里算的；这里同样限制成每秒最多一次，
        /// 免得每帧都去查表 + 推 UI。真正发奖仍按 OfflineAward 的 gap（配置 60 秒）触发。
        /// </summary>
        public void TickOfflineAward()
        {
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now == _lastAwardTickSec)
                return;
            _lastAwardTickSec = now;
            SettleOfflineAward(LocalData.Instance.Current);
        }

        // ==================== 内部工具 ====================

        /// <summary>宠物经验道具 id（原服 constant.ItemTypePetExp = 83）。</summary>
        public const int PetExpItemId = 83;

        private static readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 取宠物配置。**cfgId &lt;= 0 必须先挡掉**：
        /// 空槽位的 CfgId 是 0，直接 CFG_Pet.GetById(0) 会走 CFGTableBase.DicDatas[0]，
        /// 在 ILRuntime 下不是抛 KeyNotFoundException 而是把解释器栈打爆（StackOverflowException），
        /// 排查成本极高（第 55-B 节点踩过）。
        /// </summary>
        private static CFGItem_Pet GetCfg(int cfgId)
        {
            if (cfgId <= 0)
                return null;
            try { return CFG_Pet.GetById(cfgId); }
            catch { return null; }
        }

        /// <summary>
        /// 属性/背包变化后的统一收尾：重算属性 → 存档 → 推基础数据/属性/宠物/背包。
        /// </summary>
        private static void Commit(LocalRole role, bool pushBag)
        {
            RoleStatService.Instance.Recalculate(role, false);
            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushStatsNow();
            LocalPusher.Instance.PushUserPetNow();
            if (pushBag)
                LocalPusher.Instance.PushBagInfoNow();
        }
    }
}
