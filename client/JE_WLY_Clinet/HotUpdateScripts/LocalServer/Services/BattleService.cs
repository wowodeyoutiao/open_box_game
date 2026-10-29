using System;
using System.Collections.Generic;
using JEngine.Core;
using JEngine.Event;
using JEngine.Singleton;
using pb;
using UnityEngine;
using Attribute = pb.Attribute;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// PVE-01 最小战斗闭环。
    ///
    /// 本地 Bench 对应原服 Desk：
    /// - C2S_Join 进入普通战役；
    /// - 5ms 固定 tick；
    /// - 玩家普攻 / 怪物基础攻击；
    /// - 怪物全灭、超时、玩家死亡；
    /// - GameResultBattlePush + Settlement + 任务 + 存档。
    ///
    /// 当前只开放 Copytype=14（普通战役）。Boss/塔/遗迹后续复用。
    /// </summary>
    public class BattleService : SingletonTemplate<BattleService>
    {
        private const long LoopTickMs = 5;
        private const int DefaultAttackIntervalMs = 1000;
        private const int DeadSettleDelayMs = 1500;
        /// <summary>每日副本的死亡等待期（原服 desk.go：60 秒内可复活，超时才判负）。</summary>
        private const int DailyDeadWaitMs = 60000;

        /// <summary>
        /// 待翻倍奖励（原服 Player.reward / Player.roomType）。
        /// 结算时记下来，玩家在结算界面点「双倍」才真正发放第二份；关掉界面就丢弃。
        /// 必须放在服务级而不是 runtime 上 —— Settle 之后 _runtime 已经清空。
        /// </summary>
        private Dictionary<int, long> _pendingDoubleReward;
        private int _pendingDoubleRoomType;
        private const int WinSettleDelayMs = 3000;
        /// <summary>客户端 monster.moveTo 是 0.8s 补间，推太密会互相打断，这里做节流。</summary>
        private const int MinMovePushIntervalMs = 700;
        /// <summary>地图十字内缩量，与客户端 SceneManager 的 OffX / OffY 一致。</summary>
        private const int MapOffX = 6;
        private const int MapOffY = 13;
        /// <summary>伙伴普攻技能 UUID，对齐原服 monster_mgr.NormalSkillID = 999900。</summary>
        private const int PartnerNormalSkillId = 999900;

        /// <summary>万分比基数，与原服 constants.BaseRate 一致。</summary>
        private const double BaseRate = 10000.0;

        // 技能动作类型（原服 constant.SkillType*，对应 skill 表 SkillID 列）
        private const int SkillTypeNormalAttackWarrior = 1;
        private const int SkillTypeHalfMoon = 2;          // 月弧斩 扇形
        private const int SkillTypeFourSides = 3;         // 四方斩 四方向
        private const int SkillTypeBreakArmy = 4;         // 破军斩 长方形
        private const int SkillTypeDuel = 8;
        private const int SkillTypeMagicMissile = 15;     // 魔法飞弹 落点方框
        private const int SkillTypeStellarBurst = 16;     // 星爆术 落点方框
        private const int SkillTypeAuroraPulse = 17;      // 极光脉冲 长方形
        private const int SkillTypeRepelFoe = 20;
        private const int SkillTypeSoulTalisman = 28;     // 灵魂符咒 落点方框
        private const int SkillTypeEarthquakeWave = 29;   // 震地波 长方形
        private const int SkillTypeNetherSpell = 30;
        private const int SkillTypeDivineBattleArmor = 31;
        private const int SkillTypeDivineStrength = 34;
        private const int SkillTypeArcaneMastery = 35;
        private const int SkillTypeMassHeal = 36;
        private const int SkillTypeBlessingOfCourage = 39;
        private const int SkillTypeStomp = 52;
        private const int SkillTypeExecute = 57;          // 斩杀
        private const int SkillTypeTaunt = 58;
        private const int SkillTypeWreck = 61;
        private const int SkillTypeApocalypse = 65;
        private const int SkillTypeBleeding = 66;
        private const int SkillTypeFreezeTime = 500;
        private const int SkillTypeTimeSpaceLock = 600;
        /// <summary>地图出生点缺失时的回退点（战役 1-1 可行走点）。</summary>
        private const int FallbackBornX = 24;
        private const int FallbackBornY = 37;

        public sealed class BattleUnit
        {
            public long Uid;
            public int CfgId;
            public bool IsPlayer;
            /// <summary>是否为伙伴单位（原服 ObjectType.PARTNER）。</summary>
            public bool IsPartner;
            /// <summary>伙伴归属的玩家 Uid（客户端靠 OwnerUid 区分"自己的伙伴"）。</summary>
            public long OwnerUid;
            /// <summary>伙伴已解锁的技能 UUID（partner.json 按星级解出来的）。</summary>
            public List<int> PartnerSkills;
            /// <summary>伙伴下次可放技能的时间（毫秒），技能之间留间隔。</summary>
            public long NextSkillMs;
            /// <summary>伙伴技能轮转游标。</summary>
            public int SkillCursor;
            public string Name = "";
            public int Level;
            public int MaxHp;
            public int Hp;
            public int Attack;
            public int Defense;
            public int Hit;
            public int Dodge;
            public int Critical;
            public int Toughness;
            /// <summary>暴击伤害加成（万分比），对应 pb.Attribute.CRITICAL_DAMAGE_PERCENTAGE。</summary>
            public int CriticalDamage;

            // 减伤，对应原服 MapObject.Attacked 里的一整套 DMT_* 属性
            public int DmtFinalReduceFixed;
            public int DmtReduceFixedMonster;
            public int DmtReducePercentageMonster;
            public int DmtReduceFixedPlayer;
            public int DmtReducePercentagePlayer;
            public int DmtFinalReducePercentage;
            public int AttackRange = 1;
            public int AttackIntervalMs = DefaultAttackIntervalMs;
            public int AttackSkillId;
            public int MoveSpeed = 2;
            public long NextAttackMs;
            public double X;
            public double Y;
            /// <summary>出生点，脱战回位用。</summary>
            public double BornX;
            public double BornY;
            /// <summary>追击半径上限（原服 monsterCfg.Chase），超出则脱战回位。</summary>
            public double ChaseRange;
            /// <summary>
            /// 是否已被玩家索敌。原服怪物是被动仇恨：
            /// MapObject.Attacked 里 `if o.target == nil { o.target = attacker }`，
            /// 没被打过就不会主动追玩家。
            /// </summary>
            public bool HasTarget;
            /// <summary>
            /// 当前仇恨目标（玩家或伙伴）。对齐原服 `MapObject.target`：
            /// 首次被打时锁定攻击者，之后只追这一个；目标死亡则失效回退。
            /// ⚠️ 之前只有 HasTarget 布尔、攻击目标硬编码 runtime.Player，
            ///    导致伙伴打了怪也不会被怪打（伙伴不承伤）。
            /// </summary>
            public BattleUnit Target;
            public Direction Direction = Direction.DOWN;
            public bool Dead;
            /// <summary>
            /// 重生时间戳（毫秒）。只有「刷怪型房间」（魂石副本 / 每日副本 / 守卫海岛）
            /// 会用到，对齐原服 monster_mgr.reliveTime：死亡后 time.AfterFunc 原地复活。
            /// 0 = 不重生。
            /// </summary>
            public long ReviveAtMs;
            /// <summary>怪物是否为 Boss，决定击杀任务走 BOSS 还是普通怪目标。</summary>
            public bool IsBoss;
            /// <summary>已经推给客户端的格子坐标，用于按格补推 MovePush。</summary>
            public double PushedX;
            public double PushedY;
            public long LastMovePushMs;
        }

        /// <summary>
        /// 延迟生效的伤害。原服 Player/Monster.attack 里用
        /// `time.AfterFunc(skillCfg.DelayTime)` 把伤害丢进 chDelayAttack，
        /// 由 desk 主循环再调 delayAttack 真正扣血。
        /// 单机用 tick 驱动的队列等价实现，保证可复现、可在 batchmode 下推进。
        /// </summary>
        public sealed class PendingHit
        {
            public long AtMs;
            public BattleUnit Attacker;
            public BattleUnit Target;
            public int SkillId;
        }

        public sealed class BattleRuntime
        {
            public int RoomId;
            public int BattleId;
            /// <summary>
            /// battle.json 的关卡配置。
            /// ⚠️ **可以是 null** —— 魂石副本（RoomType 12）在 battle.json 里没有对应关卡，
            /// 这类「刷怪型房间」直接用 MapCfg 驱动。所有读 Cfg 的地方都要判空。
            /// </summary>
            public CFGItem_Battle Cfg;
            /// <summary>地图驱动房间的地图配置（battle 型房间为 null）。</summary>
            public CFGItem_Map MapCfg;
            /// <summary>房间类型（CFG_Room.RoomType），结算分支用。</summary>
            public int RoomType;
            /// <summary>击杀是否结算掉落（刷怪型房间开，战役/首领/试炼塔关）。</summary>
            public bool DropOnKill;
            /// <summary>怪物死亡后是否原地重生（刷怪型房间开）。</summary>
            public bool RespawnOnDeath;
            /// <summary>刷怪用的怪物 Uid 发号器（重生时重新发号，对齐原服 snowflake.GenID）。</summary>
            public long NextMonsterUid = 500000L;

            // ── 每日副本（RoomType 7..12）专用 ────────────────────────────
            /// <summary>每日副本配置（config.xml dailymap 段）。</summary>
            public CFG_XMLConfig.XML_Config.XML_DailyMap DailyCfg;
            /// <summary>怪物攻/防/血成长倍率：配置 × [INT(等级 / accommodation) + 1]。</summary>
            public int MonsterStatScale = 1;
            /// <summary>强化石/书页副本当前波次（其余房间恒 0）。</summary>
            public int Round;
            /// <summary>经验/金币副本当前评分等级（D/C/B/A/S/SS/SSS）。</summary>
            public string ScoreKey = "";
            /// <summary>经验/金币副本当前评分万分比（取 ScoreDic 里满足的最大档）。</summary>
            public int ScoreValue;
            /// <summary>经验/金币副本评分基数（那只 boss 的最大血量）。</summary>
            public int ScoreBossMaxHp;

            // ── 守卫海岛（RoomType 1）专用 ────────────────────────────────
            /// <summary>是否挂机副本房间（退出时按停留时间扣每日挂机时长）。</summary>
            public bool AfkRoom;
            /// <summary>掉落倍率加成（万分比），来自 GuardIsland 倍率表。</summary>
            public int DropMultiplierPercent;

            /// <summary>本场时长上限（毫秒），进房时算好：battle 用 Timelimit / 每日副本用配置 time /
            /// 守卫海岛用剩余挂机时长。</summary>
            public long LimitMs = 90000;
            public int Seed;
            public System.Random Rng;
            public long BeginMs;
            public long LastTickMs;
            public bool Ended;
            public BattleUnit Player;
            public List<BattleUnit> Monsters = new List<BattleUnit>();
            /// <summary>上阵伙伴单位。原服是独立 MapObject，跟怪物并列参与 AI。</summary>
            public List<BattleUnit> Partners = new List<BattleUnit>();
            /// <summary>延迟生效的伤害队列，对应原服 desk.chDelayAttack。</summary>
            public List<PendingHit> PendingHits = new List<PendingHit>();
            /// <summary>伙伴进场推送是否已发过（要等客户端进战斗场景后补推，否则会被丢掉）。</summary>
            public bool PartnersPushed;
            /// <summary>测试用：本场战斗是否出现过「怪物仇恨转向伙伴」。</summary>
            public bool PartnerAggroed;
            public long AllDeadAtMs;
            public long PlayerDeadAtMs;
            /// <summary>本次战斗已用掉的复活次数（原服 Player.reviveTimes，按场次累计）。</summary>
            public int ReviveTimes;
            /// <summary>
            /// 可行走矩形。对齐 SceneManager.loadMapInfo 的 OffX=6 / OffY=13 内缩规则，
            /// 客户端 GetOffZ 在越界时返回 Block，服务端必须钳制否则表现会错位。
            /// </summary>
            public int MinX = 0;
            public int MaxX = 0;
            public int MinY = 0;
            public int MaxY = 0;
            public bool HasBounds;
        }

        private BattleRuntime _runtime;

        /// <summary>技能 UUID -> 冷却结束时间。原服按 skill.Cooldown 校验，单机做服务端兜底。</summary>
        private readonly Dictionary<int, long> _skillReadyAt = new Dictionary<int, long>();

        public bool InBattle
        {
            get { return _runtime != null && !_runtime.Ended; }
        }

        /// <summary>只读：当前玩家血量，测试与调试用。</summary>
        public int PlayerHp
        {
            get { return _runtime == null || _runtime.Player == null ? 0 : _runtime.Player.Hp; }
        }

        /// <summary>首领每日挑战次数默认值。原服读 GlobalCfg.Copyset，客户端没有这份配置，先用 5 次。</summary>
        public const int DefaultBossChallengeTime = 5;

        /// <summary>试炼塔首层 BattleId（Copytype=16，章节 1、层 1）。</summary>
        private const int TrialTowerFirstBattleId = 1601001;

        /// <summary>
        /// 下一层 BattleId，对齐原服 Player.getNextBattleId(TrialTowerMap)：
        /// 进度为 0 时是首层，否则是 进度 + 1。
        /// 返回 0 表示已经打完全部层数（配置里没有下一层）。
        /// </summary>
        public static int GetNextTrialTowerId(LocalRole role)
        {
            if (role == null)
                return 0;
            int next = role.TrialTowerId == 0
                ? TrialTowerFirstBattleId
                : role.TrialTowerId + 1;
            try
            {
                // 配置里没有这一层说明已经到顶了。
                return CFG_Battle.GetById(next) != null ? next : 0;
            }
            catch { return 0; }
        }

        /// <summary>
        /// 首领挑战次数按自然日惰性重置（和 HomeAFK 同一个算法）。
        /// 首次进入或跨天时补齐次数。
        /// </summary>
        public void EnsureBossChallenge(LocalRole role)
        {
            if (role == null)
                return;
            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (role.BossResetDay != today)
            {
                role.BossResetDay = today;
                role.BossChallengeTime = DefaultBossChallengeTime;
                SaveCoordinator.Instance.MarkDirty();
            }
        }

        /// <summary>守卫海岛（和平模式房间，RoomType 1）：玩家进场刷怪，按每日挂机时长消耗。</summary>
        private static bool IsAfkRoom(int roomType)
        {
            return roomType == (int)CFG_Room.RoomType.PVE_Peace;
        }

        /// <summary>遗迹首关 BattleId（Copytype=17、章 1 层 1）。</summary>
        private const int RelicFirstBattleId = 1701001;

        /// <summary>
        /// 各副本每日免费挑战次数（config.xml Copyset：15 首领=5、17 遗迹=10）。
        /// 原服 getCopySetCfg(roomType) 等价实现。
        /// </summary>
        public static int GetCopySetTimes(int roomType)
        {
            try
            {
                CFG_XMLConfig.XML_Config xml = CFG_XMLConfig.xmlConfig;
                if (xml != null && xml.CopySetDic != null)
                {
                    int times;
                    if (xml.CopySetDic.TryGetValue(roomType, out times) && times > 0)
                        return times;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>遗迹次数跨天重置（次数取 Copyset 17）。</summary>
        public void EnsureRelicChallenge(LocalRole role)
        {
            if (role == null)
                return;
            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (role.RelicResetDay != today)
            {
                role.RelicResetDay = today;
                role.RelicChallengeTime = GetCopySetTimes((int)CFG_Room.RoomType.Relic);
                SaveCoordinator.Instance.MarkDirty();
            }
        }

        /// <summary>
        /// 遗迹下一关 BattleId，对齐原服 getNextBattleId(RelicMap)：
        ///   进度 0 → 17 01 001；否则按当前关的 MapMark：
        ///   MapMark==1（本章末关）→ 章+1、层 1；否则 → 层+1。
        /// 返回 0 表示配置里没有这一关（已通关全部）。
        /// </summary>
        public static int GetNextRelicId(LocalRole role)
        {
            if (role == null)
                return 0;
            if (role.RelicId <= 0)
                return RelicFirstBattleId;

            CFGItem_Battle cfg;
            try { cfg = CFG_Battle.GetById(role.RelicId); }
            catch { cfg = null; }
            if (cfg == null)
                return 0;

            int copyType = role.RelicId / 100000;
            int chapter = role.RelicId % 100000 / 1000;
            int layers = role.RelicId % 1000;

            // 原服用 MapMark==1 判断章末关。这里改成「看下一层的 Layers 是不是 layers+1」：
            // 同章下一层存在 → 层+1；否则说明到章末了 → 章+1、层 1。
            // （battle.json 的 MapMark 是 1.0/null，LitJson 在 ILRuntime 下接不了，见 CFG_Battle 注释）
            int sameChapterNextId = copyType * 100000 + chapter * 1000 + (layers + 1);
            CFGItem_Battle nextCfg;
            try { nextCfg = CFG_Battle.GetById(sameChapterNextId); }
            catch { nextCfg = null; }
            bool hasNextLayer = nextCfg != null
                && nextCfg.Copytype == copyType
                && nextCfg.Chapter == chapter
                && nextCfg.Layers == layers + 1;
            if (hasNextLayer)
                return sameChapterNextId;
            return copyType * 100000 + (chapter + 1) * 1000 + 1;
        }

        /// <summary>每天可挂机总时长（config.xml BaseValue/expcopy.dailytime，配置是 3600 秒）。</summary>
        public static int GetAfkDailyLimit()
        {
            try
            {
                CFG_XMLConfig.XML_Config xml = CFG_XMLConfig.xmlConfig;
                if (xml != null && xml.BaseValue != null && xml.BaseValue.expcopy != null
                    && xml.BaseValue.expcopy.dailytime > 0)
                    return xml.BaseValue.expcopy.dailytime;
            }
            catch { }
            return 3600;
        }

        /// <summary>
        /// 挂机时长跨天重置。原服每天把 AfkMultiplierID 重置成 -1（前端弹倍率选择），
        /// 单机版不接广告/元宝/充值，直接给免费档 0。
        /// </summary>
        public void EnsureAfkDaily(LocalRole role)
        {
            if (role == null)
                return;
            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (role.AfkResetDay != today)
            {
                role.AfkResetDay = today;
                role.AfkDailySec = 0;
                role.AfkMultiplierID = 0;
                SaveCoordinator.Instance.MarkDirty();
            }
        }

        /// <summary>剩余挂机秒数（对齐原服 Player.afkLeftTime）。</summary>
        public int GetAfkLeftSec(LocalRole role)
        {
            if (role == null)
                return 0;
            EnsureAfkDaily(role);
            int left = GetAfkDailyLimit() - role.AfkDailySec;
            return left < 0 ? 0 : left;
        }

        /// <summary>
        /// 挂机掉落倍率（万分比），来自 config.xml 的 GuardIsland/record/rate multiplier。
        /// ⚠️ 客户端解析时已经做过 `multiplier/10000 + 1`，这里要还原成万分比。
        /// </summary>
        public static int GetAfkDropMultiplier(long multiplierId)
        {
            try
            {
                CFG_XMLConfig.XML_Config xml = CFG_XMLConfig.xmlConfig;
                if (xml == null || xml.GuardIslandDic == null)
                    return 0;
                foreach (KeyValuePair<int, CFG_XMLConfig.XML_Config.GuardIslandItem> kv
                    in xml.GuardIslandDic)
                {
                    CFG_XMLConfig.XML_Config.GuardIslandItem item = kv.Value;
                    if (item != null && item.id == (int)multiplierId)
                        return (int)Math.Round((item.multiplier - 1f) * 10000f);
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 是否每日副本房间（7 经验 / 8 金币 / 9 强化石 / 10 书页 / 11 升星石 / 12 魂石）。
        /// 这类房间在 battle.json 里**没有关卡配置**，由 room.MapId → map.json 驱动。
        /// </summary>
        private static bool IsDailyMapRoom(int roomType)
        {
            return roomType >= (int)CFG_Room.RoomType.EXP
                && roomType <= (int)CFG_Room.RoomType.HS;
        }

        /// <summary>取某房间的每日副本配置（config.xml 的 dailymap 段，客户端已解析好）。</summary>
        public static CFG_XMLConfig.XML_Config.XML_DailyMap GetDailyMapConfig(int roomType)
        {
            try
            {
                CFG_XMLConfig.XML_Config xml = CFG_XMLConfig.xmlConfig;
                if (xml == null || xml.DayRoomDic == null)
                    return null;
                CFG_XMLConfig.XML_Config.XML_DailyMap cfg;
                return xml.DayRoomDic.TryGetValue((CFG_Room.RoomType)roomType, out cfg) ? cfg : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 把本场停留时长记到玩家每日挂机时间里（对齐原服 Player.delAfkLeftTime）。
        /// 用 runtime.LastTickMs - BeginMs 而不是真实时钟：batchmode 下时间是被 Tick 推进的，
        /// 用真实时钟测试永远是 0 秒，扣时长的逻辑就没法验证了。
        /// </summary>
        private static void AccumulateAfkTime(BattleRuntime runtime)
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null || runtime == null)
                return;
            int staySec = (int)Math.Max(0, (runtime.LastTickMs - runtime.BeginMs) / 1000);
            if (staySec <= 0)
                return;
            int limit = GetAfkDailyLimit();
            role.AfkDailySec += staySec;
            if (role.AfkDailySec > limit)
                role.AfkDailySec = limit;
            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            Log.Print("[BattleService] afk stay=" + staySec + "s used="
                + role.AfkDailySec + "/" + limit);
        }

        /// <summary>
        /// 每日副本怪物成长倍率，对齐原服 desk.playerJoin：
        ///   v = (玩家等级 / accommodation) * BaseRate + 1（这是百分比形式）
        ///   实际效果 = 配置值 × [INT(玩家等级 / accommodation) + 1]
        /// accommodation = 0（升星石/魂石副本）时不成长，返回 1。
        /// </summary>
        private static int BuildMonsterStatScale(
            CFG_XMLConfig.XML_Config.XML_DailyMap dailyCfg, int level)
        {
            if (dailyCfg == null || dailyCfg.accommodationNum <= 0)
                return 1;
            int scale = level / dailyCfg.accommodationNum + 1;
            return scale < 1 ? 1 : scale;
        }

        /// <summary>
        /// 每日副本次数惰性重置（跨天把所有房间次数按配置补满）。
        /// 原服 addDailyMapTimes / getDailyMapTimes 是同一套按 roomType 记的计数。
        /// </summary>
        public void EnsureDailyMapTimes(LocalRole role)
        {
            if (role == null)
                return;
            if (role.DailyMapTimes == null)
                role.DailyMapTimes = new Dictionary<int, int>();

            int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            if (role.DailyMapResetDay != today)
            {
                role.DailyMapResetDay = today;
                role.DailyMapTimes.Clear();
                SaveCoordinator.Instance.MarkDirty();
            }
        }

        /// <summary>读某房间剩余次数（没记录时按配置补满）。</summary>
        public int GetDailyMapTimes(LocalRole role, int roomType)
        {
            if (role == null)
                return 0;
            EnsureDailyMapTimes(role);
            int times;
            if (role.DailyMapTimes.TryGetValue(roomType, out times))
                return times;
            CFG_XMLConfig.XML_Config.XML_DailyMap cfg = GetDailyMapConfig(roomType);
            times = cfg != null && cfg.challengeTimeNum > 0 ? cfg.challengeTimeNum : 1;
            role.DailyMapTimes[roomType] = times;
            return times;
        }

        /// <summary>扣一次（进入即扣，对齐原服 addDailyMapTimes）。</summary>
        private static void ConsumeDailyMapTime(LocalRole role, int roomType)
        {
            if (role == null || role.DailyMapTimes == null)
                return;
            int times;
            if (role.DailyMapTimes.TryGetValue(roomType, out times) && times > 0)
                role.DailyMapTimes[roomType] = times - 1;
        }

        /// <summary>
        /// 只读：当前最近的存活怪物（uid + 坐标）。测试与调试用。
        /// 加它是因为「怪物会追着玩家跑」，测试里拿着进入时的静态坐标去 Move+Attack 会一直落空。
        /// </summary>
        public bool TryGetNearestAliveMonster(out long uid, out double x, out double y)
        {
            uid = 0;
            x = 0;
            y = 0;
            if (_runtime == null || _runtime.Player == null)
                return false;

            double best = double.MaxValue;
            for (int i = 0; i < _runtime.Monsters.Count; i++)
            {
                BattleUnit m = _runtime.Monsters[i];
                if (m == null || m.Dead)
                    continue;
                double dx = m.X - _runtime.Player.X;
                double dy = m.Y - _runtime.Player.Y;
                double dist = dx * dx + dy * dy;
                if (dist < best)
                {
                    best = dist;
                    uid = m.Uid;
                    x = m.X;
                    y = m.Y;
                }
            }
            return uid != 0;
        }

        /// <summary>只读：战场上伙伴单位数量，测试与调试用。</summary>
        public int BattlePartnerCount
        {
            get { return _runtime == null ? 0 : _runtime.Partners.Count; }
        }

        /// <summary>只读：存活怪物总血量，测试用（判断伙伴是否真的打出伤害）。</summary>
        public int MonsterHpTotal
        {
            get
            {
                if (_runtime == null)
                    return 0;
                int total = 0;
                for (int i = 0; i < _runtime.Monsters.Count; i++)
                {
                    BattleUnit m = _runtime.Monsters[i];
                    if (m != null)
                        total += m.Hp;
                }
                return total;
            }
        }

        /// <summary>
        /// 只读：首个"有仇恨目标"的存活怪物当前锁定的目标 Uid，测试用。
        /// 用来验证伙伴打了怪之后，怪物仇恨是否转向伙伴（原服 `o.target = attacker`）。
        /// </summary>
        public long FirstAggroMonsterTargetUid
        {
            get
            {
                if (_runtime == null)
                    return 0;
                for (int i = 0; i < _runtime.Monsters.Count; i++)
                {
                    BattleUnit m = _runtime.Monsters[i];
                    if (m != null && !m.Dead && m.HasTarget && m.Target != null)
                        return m.Target.Uid;
                }
                return 0;
            }
        }

        /// <summary>
        /// 只读：本场战斗是否出现过「怪物仇恨转向伙伴」，测试用。
        /// 比瞬态采样可靠 —— 怪物追伙伴超出 Chase 会脱战并清空 target。
        /// </summary>
        public bool PartnerAggroedInBattle
        {
            get { return _runtime != null && _runtime.PartnerAggroed; }
        }

        /// <summary>只读：战斗内玩家单位当前血量，测试用（验证伙伴承伤时玩家没掉血）。</summary>
        public int PlayerHpInBattle
        {
            get { return _runtime == null || _runtime.Player == null ? 0 : _runtime.Player.Hp; }
        }

        /// <summary>
        /// 只读：伙伴单位总血量，测试用（判断伙伴是否会承伤）。
        /// 原服怪物首次被打时 `o.target = attacker`，伙伴打了怪就会被怪打。
        /// </summary>
        public int PartnerHpTotal
        {
            get
            {
                if (_runtime == null)
                    return 0;
                int total = 0;
                for (int i = 0; i < _runtime.Partners.Count; i++)
                {
                    BattleUnit p = _runtime.Partners[i];
                    if (p != null)
                        total += p.Hp;
                }
                return total;
            }
        }

        /// <summary>只读：存活怪物数量，测试与调试用。</summary>
        public int AliveMonsterCount
        {
            get
            {
                if (_runtime == null)
                    return 0;
                int count = 0;
                for (int i = 0; i < _runtime.Monsters.Count; i++)
                {
                    BattleUnit m = _runtime.Monsters[i];
                    if (m != null && !m.Dead)
                        count++;
                }
                return count;
            }
        }

        /// <summary>只读：已对玩家产生仇恨的存活怪物数量，测试与调试用。</summary>
        public int AggroMonsterCount
        {
            get
            {
                if (_runtime == null)
                    return 0;
                int count = 0;
                for (int i = 0; i < _runtime.Monsters.Count; i++)
                {
                    BattleUnit m = _runtime.Monsters[i];
                    if (m != null && !m.Dead && m.HasTarget)
                        count++;
                }
                return count;
            }
        }

        public JoinDeskResponse Enter(JoinDeskRequest req)
        {
            var response = new JoinDeskResponse
            {
                Code = -1,
                Error = "",
                TableInfo = new TableInfo
                {
                    MonsterList = new List<MonsterInfo>(),
                },
                Location = new Point(),
                Direction = Direction.DOWN,
            };

            if (req == null)
            {
                response.Error = "进入战斗参数为空";
                return response;
            }
            if (_runtime != null && !_runtime.Ended)
            {
                response.Error = "当前已经在战斗中";
                return response;
            }

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "没有角色";
                return response;
            }

            CFGItem_Room room;
            try { room = CFG_Room.GetById(req.RoomId); }
            catch { room = null; }
            if (room == null)
            {
                response.Error = "房间配置不存在";
                return response;
            }
            // module 15 战役(14) / 首领(15) / 试炼塔(16) / 每日副本(7..12)
            int roomType = (int)room.Roomtype;
            bool isBossRoom = roomType == (int)CFG_Room.RoomType.Boss;
            bool isTowerRoom = roomType == (int)CFG_Room.RoomType.SLTower;
            bool isDailyRoom = IsDailyMapRoom(roomType);
            bool isAfkRoom = IsAfkRoom(roomType);
            bool isRelicRoom = roomType == (int)CFG_Room.RoomType.Relic;
            if (roomType != (int)CFG_Room.RoomType.Fight && !isBossRoom && !isTowerRoom
                && !isDailyRoom && !isAfkRoom && !isRelicRoom)
            {
                response.Error = "当前只支持战役、首领、试炼塔、遗迹、每日副本与守卫海岛";
                return response;
            }

            CFGItem_Battle cfg = null;
            CFGItem_Map mapCfg = null;
            CFG_XMLConfig.XML_Config.XML_DailyMap dailyCfg = null;
            if (isDailyRoom || isAfkRoom)
            {
                // 每日副本 / 守卫海岛**都不在 battle.json 里**（全表没有 Copytype 1/7~12 的关卡），
                // 地图与怪物来自 room.json 的 MapId → map.json 的 MonsterIds（含 point 锚点）。
                try { mapCfg = CFG_Map.GetById(room.MapId); }
                catch { mapCfg = null; }
                if (mapCfg == null)
                {
                    response.Error = "副本地图配置不存在";
                    return response;
                }
                dailyCfg = GetDailyMapConfig(roomType);
                if (role.Level < room.MinEntryLimit
                    || (room.MaxEntryLimit > 0 && role.Level > room.MaxEntryLimit))
                {
                    response.Error = "角色等级不足";
                    return response;
                }
                if (isAfkRoom)
                {
                    // 守卫海岛不按次数算，按每日挂机总时长
                    if (GetAfkLeftSec(role) <= 0)
                    {
                        response.Error = "今日挂机时间已用完";
                        return response;
                    }
                }
                else if (GetDailyMapTimes(role, roomType) <= 0)
                {
                    response.Error = "挑战次数不足";
                    return response;
                }
            }
            else
            {
                try { cfg = CFG_Battle.GetById(req.BattleId); }
                catch { cfg = null; }
                if (cfg == null)
                {
                    response.Error = "战役配置不存在";
                    return response;
                }
            }

            if (cfg != null)
            {
                // 原服 room_manager 一律要求 cfg.CopyType 与房间类型一致。
                if (cfg.Copytype != roomType)
                {
                    response.Error = isBossRoom ? "该战斗不是首领"
                        : isTowerRoom ? "该战斗不是试炼塔"
                        : "该战斗不是普通战役";
                    return response;
                }
                if (role.Level < cfg.Levellimit)
                {
                    response.Error = "角色等级不足";
                    return response;
                }
                if (role.TotalCombat < cfg.Powerlimit)
                {
                    response.Error = "角色战力不足";
                    return response;
                }
            }
            if (isBossRoom)
            {
                EnsureBossChallenge(role);
                if (role.BossChallengeTime <= 0)
                {
                    response.Error = "挑战次数不足";
                    return response;
                }
            }
            // 原服 TrialTowerMap 用 == 而不是 >：只能打下一层，不能跳层重复刷。
            if (isTowerRoom)
            {
                int nextTowerId = GetNextTrialTowerId(role);
                if (nextTowerId == 0)
                {
                    response.Error = "已达最高层";
                    return response;
                }
                if (req.BattleId != nextTowerId)
                {
                    response.Error = "试炼塔层数不合法";
                    return response;
                }
            }
            // 原服 RelicMap 用 > 判断：可以重打已通关的关，但不能跳关
            if (isRelicRoom)
            {
                int nextRelicId = GetNextRelicId(role);
                if (nextRelicId == 0 || req.BattleId > nextRelicId)
                {
                    response.Error = "遗迹关卡不合法";
                    return response;
                }
                EnsureRelicChallenge(role);
                if (role.RelicChallengeTime <= 0)
                {
                    response.Error = "挑战次数不足";
                    return response;
                }
            }

            RoleStatSnapshot stats = RoleStatService.Instance.Recalculate(role, true);
            int attack = stats != null
                ? Math.Max(stats.Get(Attribute.PHYSICAL_ATTACK), stats.Get(Attribute.MAGIC_ATTACK))
                : role.Attack;
            int defense = stats != null
                ? Math.Max(stats.Get(Attribute.PHYSICAL_DEFENSE), stats.Get(Attribute.MAGIC_DEFENSE))
                : role.Defense;
            int maxHp = stats != null ? stats.MaxHealth : role.MaxHp;
            if (maxHp <= 0) maxHp = 1;

            int seed = unchecked((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                ^ role.Uid.GetHashCode() ^ req.BattleId);
            int bornX;
            int bornY;
            // 地图驱动房间（魂石副本）没有 battle 配置，改用 room.MapId 找出生点/边界
            int mapIdForResolve = mapCfg != null ? room.MapId : 0;
            ResolveBornPoint(cfg, mapIdForResolve, out bornX, out bornY);
            int minX, maxX, minY, maxY;
            bool hasBounds = ResolveBounds(cfg, mapIdForResolve,
                out minX, out maxX, out minY, out maxY);
            var runtime = new BattleRuntime
            {
                RoomId = req.RoomId,
                BattleId = req.BattleId,
                Cfg = cfg,
                MapCfg = mapCfg,
                RoomType = roomType,
                DailyCfg = dailyCfg,
                // 成长倍率：攻防血 = 配置 × [INT(等级 / accommodation) + 1]
                // 升星石/魂石副本没有 accommodation（配置里没这项），保持 1 倍
                MonsterStatScale = BuildMonsterStatScale(dailyCfg, role.Level),
                // 掉落 / 重生只有「刷怪型」房间开：
                //   升星石(11)/魂石(12) —— 怪会重生、击杀掉落材料
                //   强化石(9)/书页(10)  —— 波次制，杀光刷下一波，不重生
                //   经验(7)/金币(8)     —— 单只 boss，不重生
                DropOnKill = isAfkRoom
                    || roomType == (int)CFG_Room.RoomType.SXS
                    || roomType == (int)CFG_Room.RoomType.HS,
                RespawnOnDeath = isAfkRoom
                    || roomType == (int)CFG_Room.RoomType.SXS
                    || roomType == (int)CFG_Room.RoomType.HS,
                AfkRoom = isAfkRoom,
                // 守卫海岛：掉落按 GuardIsland 倍率加成（免费档 0 = ×1）
                DropMultiplierPercent = isAfkRoom
                    ? GetAfkDropMultiplier(role.AfkMultiplierID) : 0,
                // 时长：battle 用 Timelimit / 守卫海岛用剩余挂机时长 / 每日副本用配置 time
                LimitMs = cfg != null
                    ? (long)(cfg.Timelimit > 0 ? cfg.Timelimit : 90) * 1000
                    : isAfkRoom
                        ? (long)GetAfkLeftSec(role) * 1000
                        : (long)(dailyCfg != null && dailyCfg.challengeTimeTime > 0
                            ? dailyCfg.challengeTimeTime : 180) * 1000,
                Seed = seed,
                Rng = new System.Random(seed),
                BeginMs = NowMs(),
                LastTickMs = NowMs(),
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY,
                HasBounds = hasBounds,
                Player = new BattleUnit
                {
                    Uid = role.Uid,
                    CfgId = role.Job,
                    IsPlayer = true,
                    Name = role.Name,
                    Level = role.Level,
                    MaxHp = maxHp,
                    Hp = maxHp,
                    Attack = Math.Max(1, attack),
                    Defense = Math.Max(0, defense),
                    Hit = stats != null ? stats.Get(Attribute.HIT) : 100,
                    Dodge = stats != null ? stats.Get(Attribute.DODGE) : 0,
                    Critical = stats != null ? stats.Get(Attribute.CRITICAL_STRIKE) : 0,
                    Toughness = stats != null ? stats.Get(Attribute.TOUGHNESS) : 0,
                    CriticalDamage = stats != null
                        ? stats.Get(Attribute.CRITICAL_DAMAGE_PERCENTAGE) : 0,
                    DmtFinalReduceFixed = stats != null
                        ? stats.Get(Attribute.DMT_FINAL_REDUCE_FIXED) : 0,
                    DmtReduceFixedMonster = stats != null
                        ? stats.Get(Attribute.DMT_REDUCE_FIXED_MONSTER) : 0,
                    DmtReducePercentageMonster = stats != null
                        ? stats.Get(Attribute.DMT_REDUCE_PERCENTAGE_MONSTER) : 0,
                    DmtReduceFixedPlayer = stats != null
                        ? stats.Get(Attribute.DMT_REDUCE_FIXED_PLAYER) : 0,
                    DmtReducePercentagePlayer = stats != null
                        ? stats.Get(Attribute.DMT_REDUCE_PERCENTAGE_PLAYER) : 0,
                    DmtFinalReducePercentage = stats != null
                        ? stats.Get(Attribute.DMT_FINAL_REDUCE_PERCENTAGE) : 0,
                    AttackSkillId = GetPlayerNormalSkill(role.Job),
                    X = bornX,
                    Y = bornY,
                    Direction = Direction.DOWN,
                },
            };

            SpawnMonsters(runtime);
            SpawnPartners(runtime);
            _runtime = runtime;
            _skillReadyAt.Clear();
            BattleTicker.Ensure();

            // 每日副本：原服 desk.playerJoin 里 addDailyMapTimes(roomType) 是**进入即扣**
            if (isDailyRoom && !isAfkRoom)
            {
                ConsumeDailyMapTime(role, roomType);
                SaveCoordinator.Instance.MarkDirty();
                SaveCoordinator.Instance.SaveNow();
            }
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushPlayerEnterDeskNow(new PlayerInfo
            {
                Uid = role.Uid,
                Nickname = role.Name,
                HeadUrl = role.HeadUrl,
                Level = role.Level,
                Job = role.Job,
                Gender = role.Sex,
                MaxHp = runtime.Player.MaxHp,
                CurHp = runtime.Player.Hp,
                Location = new Point { X = runtime.Player.X, Y = runtime.Player.Y },
                Direction = runtime.Player.Direction,
                BuffList = new List<BuffInfo>(),
                PartnerList = new List<ObjectInfo>(),
            });
            // 原服 desk.playerJoin 里 PVE_INSTANCE_ENTRY_REQUIREMENT(103) 只在
            // PVEPeacefulMap / PVEWarMap 派发，BattleMap（战役 14）不派发，这里保持一致。

            response.Code = 0;
            response.TableInfo.RoomCfgId = req.RoomId;
            response.TableInfo.RoomUid = role.Uid;
            response.TableInfo.BattleId = req.BattleId;
            response.Location = new Point { X = runtime.Player.X, Y = runtime.Player.Y };
            response.Direction = runtime.Player.Direction;
            response.LeftTime = runtime.LimitMs / 1000;
            for (int i = 0; i < runtime.Monsters.Count; i++)
            {
                BattleUnit m = runtime.Monsters[i];
                response.TableInfo.MonsterList.Add(new MonsterInfo
                {
                    CfgId = m.CfgId,
                    Uid = m.Uid,
                    Name = m.Name,
                    MaxHp = m.MaxHp,
                    CurHp = m.Hp,
                    Location = new Point { X = m.X, Y = m.Y },
                    Direction = m.Direction,
                });
            }
            return response;
        }

        public MoveResponse Move(MoveRequest req)
        {
            var response = new MoveResponse
            {
                Code = -1,
                Error = "",
                Location = new Point(),
                Direction = Direction.DOWN,
            };
            if (_runtime == null || _runtime.Ended)
            {
                response.Error = "当前不在战斗中";
                return response;
            }
            if (_runtime.Player.Dead)
            {
                response.Error = "角色已死亡";
                return response;
            }
            if (req == null || req.Location == null)
            {
                response.Error = "移动坐标为空";
                return response;
            }

            Direction direction = DirectionForMovement(req.Location);
            double px = req.Location.X;
            double py = req.Location.Y;
            ClampToMap(_runtime, ref px, ref py);
            _runtime.Player.X = px;
            _runtime.Player.Y = py;
            _runtime.Player.Direction = direction;
            response.Code = 0;
            response.Location = new Point
            {
                X = _runtime.Player.X,
                Y = _runtime.Player.Y,
            };
            response.Direction = _runtime.Player.Direction;
            LocalPusher.Instance.PushBattleMoveNow(new MovePush
            {
                Uid = _runtime.Player.Uid,
                Location = response.Location,
                Direction = response.Direction,
            });
            return response;
        }

        public AttackResponse Attack(AttackRequest req)
        {
            var response = new AttackResponse
            {
                Code = -1,
                Error = "",
                SkillId = req == null ? 0 : req.SkillId,
                TargetUids = req == null ? new long[0] : req.TargetUids,
                Location = req == null ? new Point() : req.Location,
                Direction = req == null ? Direction.DOWN : req.Direction,
            };
            if (_runtime == null || _runtime.Ended)
            {
                response.Error = "当前不在战斗中";
                return response;
            }
            if (_runtime.Player.Dead)
            {
                response.Error = "角色已死亡";
                return response;
            }
            if (req == null || req.TargetUids == null || req.TargetUids.Length == 0)
            {
                response.Error = "没有攻击目标";
                return response;
            }

            BattleUnit target = FindMonster(req.TargetUids[0]);
            if (target == null || target.Dead)
            {
                response.Error = "目标不存在";
                return response;
            }

            CFGItem_Skill skillCfg = GetSkillCfg(req.SkillId);
            if (skillCfg == null)
            {
                response.Error = "技能配置不存在";
                return response;
            }

            long now = NowMs();

            // 原服 Player.attack：skillCfg.CoolDown > 0 && now < skill.Cooldown 直接拒绝。
            if (skillCfg.CoolDown > 0)
            {
                long readyAt;
                if (_skillReadyAt.TryGetValue(req.SkillId, out readyAt) && now < readyAt)
                {
                    response.Error = "技能冷却时间未到";
                    return response;
                }
            }

            // 原服 Needaim：0 表示无目标技能，服务端会主动清空前端传的目标。
            bool needAim = skillCfg.Needaim != 0;

            Direction castDir = _runtime.Player.Direction;

            double aimX = req.Location != null ? req.Location.X : target.X;
            double aimY = req.Location != null ? req.Location.Y : target.Y;

            if (needAim)
                castDir = DirectionFromPoint(_runtime.Player.X, _runtime.Player.Y, target.X, target.Y);
            _runtime.Player.Direction = castDir;

            int radius = (int)skillCfg.Radius;
            // 技能进阶：Distance 直接加到半径上（原服 Player.attack 里 Radius += skillUpperlevel.Distance）
            CFGItem_Skill.UpperlevelSettingItem castUpper =
                CFG_Skill.GetUpperlevelSetting(skillCfg.UUID,
                    GetSkillAdvancedLevel(_runtime.Player, req.SkillId));
            if (castUpper != null)
                radius += castUpper.Distance;
            if (radius < 0)
                radius = 0;

            // 原服 Player.attack：先放点选目标，再把技能范围内的目标补进来。
            var targets = new List<BattleUnit>();
            if (needAim)
                targets.Add(target);
            for (int i = 0; i < _runtime.Monsters.Count; i++)
            {
                BattleUnit m = _runtime.Monsters[i];
                if (m == null || m.Dead)
                    continue;
                if (ContainsUnit(targets, m.Uid))
                    continue;
                if (!InSkillArea((int)skillCfg.SkillID, _runtime.Player.X, _runtime.Player.Y, castDir,
                        radius, aimX, aimY, m.X, m.Y))
                    continue;
                targets.Add(m);
            }

            // 技能距离：原服 maputil.Distance 是切比雪夫距离，且用玩家位置判定。
            int maxDistance = (int)skillCfg.MaxDistance;
            var valid = new List<BattleUnit>();
            for (int i = 0; i < targets.Count; i++)
            {
                BattleUnit m = targets[i];
                if (maxDistance > 0
                    && GridDistance(_runtime.Player.X, _runtime.Player.Y, m.X, m.Y) > maxDistance)
                    continue;
                valid.Add(m);
            }
            if (valid.Count == 0)
            {
                response.Error = maxDistance > 0 ? "距离过远" : "没有攻击目标";
                return response;
            }

            long[] targetUids = new long[valid.Count];
            for (int i = 0; i < valid.Count; i++)
                targetUids[i] = valid[i].Uid;

            // 原服最后统一 Broadcast 一次 AttackPush，带上全部目标。
            LocalPusher.Instance.PushBattleAttackNow(new AttackPush
            {
                AttackerId = _runtime.Player.Uid,
                SkillId = req.SkillId,
                TargetUids = targetUids,
                Location = new Point { X = aimX, Y = aimY },
                Direction = castDir,
            });

            // 伤害延迟生效：原服 time.AfterFunc(skillCfg.DelayTime) → chDelayAttack。
            int delayMs = (int)skillCfg.Delay;
            if (delayMs < 0)
                delayMs = 0;
            for (int i = 0; i < valid.Count; i++)
            {
                _runtime.PendingHits.Add(new PendingHit
                {
                    AtMs = now + delayMs,
                    Attacker = _runtime.Player,
                    Target = valid[i],
                    SkillId = req.SkillId,
                });
            }

            // 原服 skill.Cooldown = now + CoolDown - 500，并推送 SkillCooldownPush。
            if (skillCfg.CoolDown > 0)
            {
                _skillReadyAt[req.SkillId] = now + (long)skillCfg.CoolDown - 500;
                LocalPusher.Instance.PushSkillCooldownNow(new SkillCooldownPush
                {
                    CfgId = req.SkillId,
                    Delay = (int)skillCfg.CoolDown,
                });
            }

            response.SkillId = req.SkillId;
            response.TargetUids = targetUids;
            response.Location = new Point { X = aimX, Y = aimY };
            response.Direction = castDir;
            response.Code = 0;
            return response;
        }

        private static bool ContainsUnit(List<BattleUnit> list, long uid)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Uid == uid)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 复活（对齐原服 RoomManager.Revive）。
        ///
        /// 允许范围：除试炼塔 / 世界 Boss 外的房间（原服 -3）。
        /// 次数与消耗来自 config.xml 的 &lt;Revive&gt;&lt;key time needMoneyID num /&gt;，
        /// key 是「第几次复活」，用完返回 -4。
        ///
        /// 每日副本的死亡判负被拉长到 60 秒（见 DailyDeadWaitMs），
        /// 所以玩家在这段时间内点复活是有效的。
        /// </summary>
        public object Revive(byte[] req)
        {
            BattleRuntime runtime = _runtime;
            var res = new ReviveResponse { Code = 0, Error = "" };

            if (runtime == null || runtime.Ended)
            {
                res.Code = -1;
                res.Error = "不在战斗中";
                return res;
            }

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                res.Code = -1;
                res.Error = "没有角色";
                return res;
            }

            if (!runtime.Player.Dead)
            {
                res.Code = -2;
                res.Error = "角色未死亡";
                return res;
            }

            int roomType = runtime.RoomType;
            if (roomType == (int)CFG_Room.RoomType.SLTower
                || roomType == (int)CFG_Room.RoomType.WorldBoss)
            {
                res.Code = -3;
                res.Error = "该玩法不支持复活";
                return res;
            }

            CFG_XMLConfig.XML_Config.XML_Item_Revive cost = GetReviveCost(runtime.ReviveTimes + 1);
            if (cost == null)
            {
                res.Code = -4;
                res.Error = "复活次数已用完";
                return res;
            }

            if (!ItemService.Instance.TryConsumeId(role, cost.needMoneyID, cost.num))
            {
                res.Code = -5;
                res.Error = "复活所需道具不足";
                return res;
            }

            runtime.Player.Dead = false;
            runtime.Player.Hp = runtime.Player.MaxHp;
            runtime.PlayerDeadAtMs = 0;
            runtime.ReviveTimes++;

            role.CurHp = role.MaxHp;
            SaveCoordinator.Instance.MarkDirty();
            LocalPusher.Instance.PushStatsNow();

            res.CurHp = runtime.Player.Hp;
            res.Location = new Point { X = runtime.Player.X, Y = runtime.Player.Y };
            return res;
        }

        /// <summary>
        /// 副本扫荡（对齐原服 RoomManager.CopySweep → dailyMapCopySweep）。
        ///
        /// 只支持每日副本六类（经验/金币/强化石/书页/升星石/魂石）。
        /// 首领/遗迹的 battleMapCopySweep 需要逐关通关记录，暂未实现。
        ///
        /// 校验：VIP 门槛（dailymap 的 viplimit）→ 次数。
        /// 奖励：
        ///   经验/金币  = baseValueNum × quickaccessScore / 10000 × level
        ///   强化石/书页 = baseValueNum × quickaccessScore × level（原服这里没除 BaseRate，照抄）
        ///   升星石/魂石 = quickaccess 的 stage 列表逐个发
        /// </summary>
        public object CopySweep(byte[] req)
        {
            CopySweepRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<CopySweepRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CopySweepResponse { Code = -1, Error = "扫荡请求解析失败: " + e.Message };
            }

            var res = new CopySweepResponse { Code = 0, Error = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                res.Code = -1;
                res.Error = "没有角色";
                return res;
            }
            if (request == null || request.RoomId <= 0)
            {
                res.Code = -1;
                res.Error = "参数错误";
                return res;
            }

            CFGItem_Room room;
            try { room = CFG_Room.GetById(request.RoomId); }
            catch { room = null; }
            if (room == null)
            {
                res.Code = -1;
                res.Error = "副本配置不存在";
                return res;
            }

            int roomType = (int)room.Roomtype;
            if (!IsDailyMapRoom(roomType))
            {
                res.Code = -1;
                res.Error = "该副本不支持扫荡";
                return res;
            }

            CFG_XMLConfig.XML_Config.XML_DailyMap cfg = GetDailyMapConfig(roomType);
            if (cfg == null)
            {
                res.Code = -1;
                res.Error = "副本配置不存在";
                return res;
            }

            // 原服：p.vip < GetDailyMapTime(roomtype).VipLimit
            if (role.Vip < cfg.viplimit)
            {
                res.Code = -2;
                res.Error = "VIP等级不足，无法扫荡";
                return res;
            }

            // 原服：dailyMapTimes >= Num + VIP 加成
            if (GetDailyMapTimes(role, roomType) <= 0)
            {
                res.Code = -3;
                res.Error = "挑战次数不足";
                return res;
            }
            ConsumeDailyMapTime(role, roomType);

            // 结算奖励
            var rewards = new Dictionary<int, long>();
            switch ((CFG_Room.RoomType)roomType)
            {
                case CFG_Room.RoomType.EXP:
                case CFG_Room.RoomType.Gold:
                {
                    // 经验/金币：baseValueNum × score / 10000 × level
                    long num = (long)cfg.baseValueNum * cfg.quickaccessScore / 10000 * role.Level;
                    if (num > 0 && cfg.baseValueID > 0)
                        rewards[cfg.baseValueID] = num;
                    break;
                }
                case CFG_Room.RoomType.QHS:
                case CFG_Room.RoomType.SY:
                {
                    // 强化石/书页：baseValueNum × score × level（原服未除 BaseRate）
                    long num = (long)cfg.baseValueNum * cfg.quickaccessScore * role.Level;
                    if (num > 0 && cfg.baseValueID > 0)
                        rewards[cfg.baseValueID] = num;
                    break;
                }
                default:
                {
                    // 升星石/魂石：quickaccess 的 stage 列表
                    if (cfg.quickAccessStages != null)
                    {
                        for (int i = 0; i < cfg.quickAccessStages.Length; i++)
                        {
                            CFG_XMLConfig.XML_Config.XML_DailyMap.QuickStageItem stage = cfg.quickAccessStages[i];
                            if (stage == null || stage.id <= 0 || stage.num <= 0)
                                continue;
                            long exist;
                            rewards[stage.id] = rewards.TryGetValue(stage.id, out exist) ? exist + stage.num : stage.num;
                        }
                    }
                    break;
                }
            }

            if (rewards.Count == 0)
            {
                res.Code = -1;
                res.Error = "扫荡奖励配置缺失";
                return res;
            }

            var items = new List<ItemInfo>();
            foreach (KeyValuePair<int, long> kv in rewards)
            {
                ItemService.Instance.GrantReward(role, kv.Key, kv.Value);
                items.Add(new ItemInfo { CfgId = kv.Key, Num = (int)kv.Value });
            }

            // 扫荡结果也支持结算界面点「双倍」
            _pendingDoubleReward = new Dictionary<int, long>(rewards);
            _pendingDoubleRoomType = roomType;

            SaveCoordinator.Instance.MarkDirty();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushStatsNow();

            LocalPusher.Instance.PushBattleResultNow(new GameResultBattlePush
            {
                RoomId = room.ID,
                OverType = GameOverType.GameOverTypeCopySweep,
                Items = items,
            });
            return res;
        }

        /// <summary>自动战斗开关（客户端托管，服务端只记录 + 校验 VIP 门槛）。</summary>
        private bool _autoPlayEnabled;

        /// <summary>
        /// 自动战斗（对齐原服 RoomManager.AutoPlay）。
        ///
        /// 原服这个接口**只校验 VIP 等级**（vip &lt; config.xml 的 autobattle.viplevel → -1），
        /// 真正的托管逻辑在客户端：MainRole.Update() 里 AutoWalkOrAttack() 自动锁敌
        /// → sendAutoPath() 自动寻路 → 到了自动发 C2S_Attack。
        /// 所以这里不重复实现托管（否则会和客户端各打一次，伤害翻倍），
        /// 只做门槛校验并记录开关状态。
        /// </summary>
        public object AutoPlay(byte[] req)
        {
            var res = new AutoPlayResponse { Code = 0, Error = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                res.Code = -1;
                res.Error = "没有角色";
                return res;
            }

            int needVip = 0;
            try
            {
                CFG_XMLConfig.XML_Config xml = CFG_XMLConfig.xmlConfig;
                if (xml != null && xml.BaseValue != null)
                    needVip = xml.BaseValue.autoPlayVipLv;
            }
            catch { }

            if (role.Vip < needVip)
            {
                res.Code = -1;
                res.Error = "VIP" + needVip + " 解锁自动战斗";
                return res;
            }

            _autoPlayEnabled = true;
            return res;
        }

        /// <summary>只读：自动战斗是否已解锁开启，测试用。</summary>
        public bool AutoPlayEnabled
        {
            get { return _autoPlayEnabled; }
        }

        /// <summary>自动购买加时的开关（原服 Player.autoBuyInstanceTime）。</summary>
        private bool _autoBuyInstanceTime;

        /// <summary>
        /// 副本加时（对齐原服 RoomManager.InstanceTimeAdd）。
        ///
        /// 原服只对守卫海岛(PVE_Peace)和战役(PVE_War)生效，扣体力换 expirationTime。
        /// 本地守卫海岛用的是「每日挂机总时长」模型，所以换算成「把今天已用的时长还回去」：
        ///   AfkDailySec -= buytime_peace
        /// 消耗与时长读 config.xml 的 &lt;energy consumeenergy_peace / buytime_peace /&gt;。
        /// EnergyNum=0 时只记录自动购买开关（原服同）。
        /// </summary>
        public object InstanceTimeAdd(byte[] req)
        {
            InstanceTimeAddRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<InstanceTimeAddRequest>(req);
            }
            catch (System.Exception e)
            {
                return new InstanceTimeAddResponse { Code = -1, Error = "请求解析失败: " + e.Message };
            }

            var res = new InstanceTimeAddResponse { Code = 0, Error = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                res.Code = -1;
                res.Error = "没有角色";
                return res;
            }

            CFG_XMLConfig.XML_Config xml = CFG_XMLConfig.xmlConfig;
            if (xml == null || xml.Energy == null)
            {
                res.Code = -1;
                res.Error = "体力配置缺失";
                return res;
            }

            int needEnergy = xml.Energy.consumeenergy_peace;
            int addSec = xml.Energy.buytime_peace;
            if (needEnergy <= 0 || addSec <= 0)
            {
                res.Code = -1;
                res.Error = "加时配置无效";
                return res;
            }

            bool buy = request != null && request.EnergyNum != 0;
            if (buy)
            {
                if (role.Energy < needEnergy)
                {
                    res.Code = -2;
                    res.Error = "体力不足";
                    return res;
                }
                role.Energy -= needEnergy;
                LocalData.Instance.Energy = role.Energy;

                EnsureAfkDaily(role);
                role.AfkDailySec = Math.Max(0, role.AfkDailySec - addSec);

                SaveCoordinator.Instance.MarkDirty();
                LocalPusher.Instance.PushUserBaseNow();
            }
            else
            {
                _autoBuyInstanceTime = request != null && request.AutoBuy;
            }

            res.InstanceTimeLeft = GetAfkLeftSec(role);
            res.AutoBuy = _autoBuyInstanceTime;
            return res;
        }

        /// <summary>
        /// 双倍奖励（对齐原服 Player.dailyMapDoubleReward）。
        ///
        /// 结算/扫荡时把奖励记在 _pendingDoubleReward 里，玩家在结算界面点「双倍」才花点券
        /// 把这一份再发一次。Double=false 表示放弃翻倍，直接丢弃待领奖励。
        /// 消耗读 dailymap 段的 doubleaccess id/num（每日副本六类都有配）。
        /// </summary>
        public object DoubleReward(byte[] req)
        {
            DoubleRewardRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<DoubleRewardRequest>(req);
            }
            catch (System.Exception e)
            {
                return new DoubleRewardResponse { Code = -1, Error = "请求解析失败: " + e.Message };
            }

            var res = new DoubleRewardResponse { Code = 0, Error = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                res.Code = -1;
                res.Error = "没有角色";
                return res;
            }

            bool wantDouble = request != null && request.Double;

            // 放弃翻倍：清空待领奖励（原服 p.reward = []）
            if (!wantDouble)
            {
                _pendingDoubleReward = null;
                _pendingDoubleRoomType = 0;
                return res;
            }

            if (_pendingDoubleReward == null || _pendingDoubleReward.Count == 0 || _pendingDoubleRoomType <= 0)
            {
                res.Code = -2;
                res.Error = "没有可翻倍的奖励";
                return res;
            }

            CFG_XMLConfig.XML_Config.XML_DailyMap cfg = GetDailyMapConfig(_pendingDoubleRoomType);
            if (cfg == null || cfg.doubleaccessID <= 0 || cfg.doubleaccessNum <= 0)
            {
                res.Code = -2;
                res.Error = "该副本不支持双倍奖励";
                return res;
            }

            if (!ItemService.Instance.TryConsumeId(role, cfg.doubleaccessID, cfg.doubleaccessNum))
            {
                res.Code = -1;
                res.Error = "点券不足，无法双倍";
                return res;
            }

            // 把同一份奖励再发一次
            var doubled = _pendingDoubleReward;
            _pendingDoubleReward = null;
            _pendingDoubleRoomType = 0;

            foreach (KeyValuePair<int, long> kv in doubled)
            {
                if (kv.Key <= 0 || kv.Value <= 0)
                    continue;
                ItemService.Instance.GrantReward(role, kv.Key, kv.Value);
            }

            SaveCoordinator.Instance.MarkDirty();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushStatsNow();
            return res;
        }

        /// <summary>
        /// 选择守卫海岛的挂机倍率（对齐原服 Player.GuardIslandMultiplier）。
        ///
        /// ⚠️ 客户端 TcpC.C2S_GuardIslandMultiplier() 固定发 ID=0（Proto/TcpC.cs:387），
        ///    界面里的 ToggleIdx 并没有带上来。所以这里对 ID&lt;=0 做回退：
        ///    取倍率最低的那档（type=1 免费档），保证玩家点「免费倍率」能生效。
        ///
        /// 原服按 type 分支：免费/看广告/道具/充值 都是直接写入 AfkMultiplierID
        /// （道具和充值那两段的扣费逻辑在原服就是注释掉的）。
        /// </summary>
        public object GuardIslandMultiplier(byte[] req)
        {
            GuardIslandMultiplierRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<GuardIslandMultiplierRequest>(req);
            }
            catch (System.Exception e)
            {
                return new GuardIslandMultiplierResponse { Code = -1, Error = "请求解析失败: " + e.Message };
            }

            var res = new GuardIslandMultiplierResponse { Code = 0, Error = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                res.Code = -1;
                res.Error = "没有角色";
                return res;
            }

            CFG_XMLConfig.XML_Config xml = CFG_XMLConfig.xmlConfig;
            if (xml == null || xml.GuardIslandDic == null || xml.GuardIslandDic.Count == 0)
            {
                res.Code = -1;
                res.Error = "守卫海岛倍率配置缺失";
                return res;
            }

            long wantId = request == null ? 0 : request.ID;
            CFG_XMLConfig.XML_Config.GuardIslandItem cfg = FindGuardIslandMultiplier(xml, wantId);
            if (cfg == null && wantId <= 0)
                cfg = FindFreeGuardIslandMultiplier(xml);
            if (cfg == null)
            {
                res.Code = -1;
                res.Error = "倍率配置不存在: " + wantId;
                return res;
            }

            // 原服用 -1 表示「未设置」，免费档的 id 本身就是 0。
            // 本地把「0 = 免费档」当默认值（EnsureAfkDaily 也重置成 0），
            // 所以只有「已经选了非免费档、还想换另一个」才拒绝。
            if (role.AfkMultiplierID != 0 && role.AfkMultiplierID != cfg.id)
            {
                res.Code = -1;
                res.Error = "今日已选择过挂机倍率";
                return res;
            }

            role.AfkMultiplierID = cfg.id;
            res.ID = cfg.id;
            SaveCoordinator.Instance.MarkDirty();
            LocalPusher.Instance.PushUserBaseNow();
            return res;
        }

        /// <summary>按配置里的 id 字段找倍率（原服 GuardIslandCfg[record.ID]）。</summary>
        private static CFG_XMLConfig.XML_Config.GuardIslandItem FindGuardIslandMultiplier(
            CFG_XMLConfig.XML_Config xml, long id)
        {
            if (id <= 0)
                return null;
            foreach (KeyValuePair<int, CFG_XMLConfig.XML_Config.GuardIslandItem> kv in xml.GuardIslandDic)
            {
                CFG_XMLConfig.XML_Config.GuardIslandItem item = kv.Value;
                if (item != null && item.id == (int)id)
                    return item;
            }
            return null;
        }

        /// <summary>回退：倍率最低的免费档（type=1），客户端 ID=0 时用。</summary>
        private static CFG_XMLConfig.XML_Config.GuardIslandItem FindFreeGuardIslandMultiplier(
            CFG_XMLConfig.XML_Config xml)
        {
            CFG_XMLConfig.XML_Config.GuardIslandItem best = null;
            foreach (KeyValuePair<int, CFG_XMLConfig.XML_Config.GuardIslandItem> kv in xml.GuardIslandDic)
            {
                CFG_XMLConfig.XML_Config.GuardIslandItem item = kv.Value;
                if (item == null || item.type != 1)
                    continue;
                if (best == null || item.multiplier < best.multiplier)
                    best = item;
            }
            // 没有 type=1 就退而取倍率最低的那条，避免玩家卡在选不了倍率
            if (best == null)
            {
                foreach (KeyValuePair<int, CFG_XMLConfig.XML_Config.GuardIslandItem> kv in xml.GuardIslandDic)
                {
                    CFG_XMLConfig.XML_Config.GuardIslandItem item = kv.Value;
                    if (item == null)
                        continue;
                    if (best == null || item.multiplier < best.multiplier)
                        best = item;
                }
            }
            return best;
        }

        /// <summary>取第 times 次复活的消耗配置；没有该档返回 null。</summary>
        private static CFG_XMLConfig.XML_Config.XML_Item_Revive GetReviveCost(int times)
        {
            try
            {
                CFG_XMLConfig.XML_Config xml = CFG_XMLConfig.xmlConfig;
                if (xml == null || xml.ReviveDic == null)
                    return null;
                CFG_XMLConfig.XML_Config.XML_Item_Revive cost;
                return xml.ReviveDic.TryGetValue(times, out cost) ? cost : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>只读：本次战斗已用复活次数，测试用。</summary>
        public int ReviveTimesInBattle
        {
            get { return _runtime == null ? 0 : _runtime.ReviveTimes; }
        }

        /// <summary>
        /// 仅测试用：把战斗内玩家打成死亡状态，用来验证复活流程。
        /// 生产逻辑不会走这里（真实死亡来自 ApplyHit）。
        /// </summary>
        public void TestKillPlayerForRevive()
        {
            if (_runtime == null || _runtime.Player == null)
                return;
            _runtime.Player.Hp = 0;
            _runtime.Player.Dead = true;
            _runtime.PlayerDeadAtMs = 0;
        }

        public ExitResponse Exit()
        {
            var response = new ExitResponse { Code = 0, Error = "" };
            _skillReadyAt.Clear();
            if (_runtime != null)
            {
                // 守卫海岛：主动退出也要扣每日挂机时长（原服 exitRoom 里 delAfkLeftTime）
                if (_runtime.AfkRoom)
                    AccumulateAfkTime(_runtime);
                _runtime.Ended = true;
                _runtime = null;
            }
            return response;
        }

        public CommonResponse ClaimChapterReward(BattleRewardRequest req)
        {
            var response = new CommonResponse
            {
                Code = -1,
                Error = "",
                RequestCmdId = (uint)CmdId.C2S_BattleReward,
            };
            if (req == null)
            {
                response.Error = "参数错误";
                return response;
            }

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "没有角色";
                return response;
            }

            // 战役：一次把 RewardStage..BattleId 之间所有 ChapterAward 全领掉
            if (req.MType == MapType.BATTLE_MAP)
                return ClaimBattleChapterReward(role, response);

            // 遗迹：按「本章各关星级之和」解锁 10/20/30 三档宝箱，只能往前领
            if (req.MType == MapType.RELIC_MAP)
                return ClaimRelicChapterReward(role, req.ChapterId, response);

            response.Error = "暂只支持战役/遗迹章节奖励";
            return response;
        }

        private CommonResponse ClaimBattleChapterReward(LocalRole role, CommonResponse response)
        {
            if (role.BattleRewardStage >= role.BattleId)
            {
                response.Error = "没有可领取的章节奖励";
                return response;
            }

            int from = role.BattleRewardStage;
            int to = role.BattleId;
            bool any = false;
            for (int id = from + 1; id <= to; id++)
            {
                CFGItem_Battle cfg;
                try { cfg = CFG_Battle.GetById(id); }
                catch { cfg = null; }
                if (cfg == null || cfg.ChapterAward == null)
                    continue;
                for (int i = 0; i < cfg.ChapterAward.Length; i++)
                {
                    CFGItem_Battle.AwardItem award = cfg.ChapterAward[i];
                    if (award.id > 0 && award.num > 0)
                    {
                        ItemService.Instance.GrantReward(role, award.id, award.num);
                        any = true;
                    }
                }
            }

            role.BattleRewardStage = to;
            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushStatsNow();
            response.Code = any ? 0 : -1;
            if (!any) response.Error = "章节奖励配置为空";
            return response;
        }

        /// <summary>
        /// 遗迹章节星级宝箱（对齐原服 getBattleReward 的 RELIC_MAP 分支）：
        ///   ① 本章星数 = Σ(该章各关 Evaluation 星数)，关卡号 → 章号 = BattleId % 100000 / 1000（1701001 → 1）；
        ///   ② 已领档位存在 RelicChapterReward[chapter]，只能往后领，不能重复领；
        ///   ③ 奖励表 = battle 表里 CopyType==17 && Chapter==chapter 且 StarAward 非空的那一条
        ///      （就是每章最后一关，key = 10/20/30）；
        ///   ④ 一次把 (已领, 本章星数] 区间内的所有档位一起发，落到「已领到的最大档」。
        /// 原服还有「>=30 星上跑马灯」，单机版没有公告系统，略过。
        /// </summary>
        private CommonResponse ClaimRelicChapterReward(LocalRole role, int chapter, CommonResponse response)
        {
            if (chapter <= 0)
            {
                response.Error = "章节参数错误";
                return response;
            }
            if (role.RelicEvaluation == null)
                role.RelicEvaluation = new List<KeyValue>();
            if (role.RelicChapterReward == null)
                role.RelicChapterReward = new List<KeyValue>();

            int chapterStar = 0;
            for (int i = 0; i < role.RelicEvaluation.Count; i++)
            {
                KeyValue eval = role.RelicEvaluation[i];
                if (eval == null || eval.K <= 0)
                    continue;
                if (eval.K % 100000 / 1000 != chapter)
                    continue;
                chapterStar += eval.V;
            }

            int gotStar = 0;
            int gotIndex = -1;
            for (int i = 0; i < role.RelicChapterReward.Count; i++)
            {
                KeyValue got = role.RelicChapterReward[i];
                if (got == null || got.K != chapter)
                    continue;
                gotStar = got.V;
                gotIndex = i;
                break;
            }

            if (gotStar >= chapterStar)
            {
                response.Error = "没有可领取的星级奖励";
                return response;
            }

            CFGItem_Battle.StarAwardItem[] starAwards = null;
            List<CFGItem_Battle> chapterBattles;
            if (CFG_Battle.RelicDic.TryGetValue(chapter, out chapterBattles) && chapterBattles != null)
            {
                for (int i = 0; i < chapterBattles.Count; i++)
                {
                    if (chapterBattles[i] != null && chapterBattles[i].StarAward != null
                        && chapterBattles[i].StarAward.Length > 0)
                    {
                        starAwards = chapterBattles[i].StarAward;
                        break;
                    }
                }
            }
            if (starAwards == null)
            {
                response.Error = "星级奖励配置为空";
                return response;
            }

            // 本章星数连最低档都没到 → 明确提示「星级不足」。
            // 否则会落到下面 any=false 的「星级奖励配置为空」，玩家会以为功能坏了。
            int minKey = int.MaxValue;
            for (int i = 0; i < starAwards.Length; i++)
            {
                if (starAwards[i].key > 0 && starAwards[i].key < minKey)
                    minKey = starAwards[i].key;
            }
            if (minKey != int.MaxValue && chapterStar < minKey)
            {
                response.Error = "星级不足";
                return response;
            }

            int maxGotStar = gotStar;
            bool any = false;
            for (int i = 0; i < starAwards.Length; i++)
            {
                CFGItem_Battle.StarAwardItem tier = starAwards[i];
                if (tier.key <= gotStar || tier.key > chapterStar)
                    continue;
                if (tier.values == null)
                    continue;
                for (int j = 0; j < tier.values.Length; j++)
                {
                    CFGItem_Battle.AwardItem award = tier.values[j];
                    if (award.id > 0 && award.num > 0)
                    {
                        ItemService.Instance.GrantReward(role, award.id, award.num);
                        any = true;
                    }
                }
                if (tier.key > maxGotStar)
                    maxGotStar = tier.key;
            }

            if (!any)
            {
                response.Error = "星级奖励配置为空";
                return response;
            }

            if (gotIndex >= 0)
                role.RelicChapterReward[gotIndex].V = maxGotStar;
            else
                role.RelicChapterReward.Add(new KeyValue { K = chapter, V = maxGotStar });

            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushStatsNow();
            // 客户端本章宝箱状态读 InstanceInfo.relic_map.ChapterReward，领完必须重推一次
            var infoResp = PlayerService.Instance.GetInstanceInfo() as InstanceInfoResponse;
            if (infoResp != null)
                LocalPusher.Instance.PushInstanceInfoNow(infoResp);
            response.Code = 0;
            response.Error = "";
            Log.Print("[BattleService] 遗迹星级宝箱 chapter=" + chapter + " star="
                + gotStar + "->" + maxGotStar + " (本章 " + chapterStar + " 星)");
            return response;
        }

        public void TickNow()
        {
            Tick(NowMs());
        }

        public void Tick(long nowMs)
        {
            BattleRuntime runtime = _runtime;
            if (runtime == null || runtime.Ended)
                return;

            long limitMs = runtime.LimitMs > 0 ? runtime.LimitMs : 90000;
            if (nowMs - runtime.BeginMs >= limitMs)
            {
                Settle(GameOverType.GameOverTypeTime);
                return;
            }

            if (runtime.Player.Dead)
            {
                if (runtime.PlayerDeadAtMs > 0 && nowMs >= runtime.PlayerDeadAtMs)
                {
                    Settle(GameOverType.GameOverTypeDeadFaild);
                    return;
                }
            }
            else
            {
                // 刷怪型房间：先把到点的怪原地复活（原服 reliveTime 到点 relive）
                if (runtime.RespawnOnDeath)
                    UpdateRevives(runtime, nowMs);

                long dtMs = Math.Max(0, Math.Min(nowMs - runtime.LastTickMs, 50));
                for (int i = 0; i < runtime.Monsters.Count; i++)
                {
                    BattleUnit monster = runtime.Monsters[i];
                    if (monster == null || monster.Dead)
                        continue;

                    // 原服是被动仇恨：没被打过就不主动追人，原地待机 / 走回出生点。
                    // 被打过之后只追**锁定目标**（可能是玩家，也可能是伙伴）。
                    BattleUnit aggro = GetMonsterAggroTarget(runtime, monster);
                    if (aggro == null)
                    {
                        if (Math.Abs(monster.X - monster.BornX) > 0.1
                            || Math.Abs(monster.Y - monster.BornY) > 0.1)
                        {
                            StepMonsterTowards(monster, monster.BornX, monster.BornY, 0.1,
                                dtMs, runtime, nowMs);
                        }
                        continue;
                    }

                    double dx = aggro.X - monster.X;
                    double dy = aggro.Y - monster.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    // 原服 processTarget：离出生点超过 Chase 就脱战回位。
                    double awayX = monster.X - monster.BornX;
                    double awayY = monster.Y - monster.BornY;
                    if (monster.ChaseRange > 0
                        && Math.Sqrt(awayX * awayX + awayY * awayY) > monster.ChaseRange)
                    {
                        monster.HasTarget = false;
                        monster.Target = null;
                        continue;
                    }

                    if (distance > monster.AttackRange)
                    {
                        // 原服 Monst.processAI：先索敌/追击，进入攻击距离才攻击。
                        // 停在 AttackRange × 0.8 处，避免贴进目标身体。
                        StepMonsterTowards(monster, aggro.X, aggro.Y,
                            monster.AttackRange * 0.8, dtMs, runtime, nowMs);
                        // 原服 monster_move 每移动一格 Broadcast(RouteMovePush)；
                        // 不推的话客户端怪物会停在出生点，表现为远处隔空攻击。
                        PushMonsterMoveIfNeeded(monster, nowMs, false);
                    }
                    else if (nowMs >= monster.NextAttackMs)
                    {
                        PushMonsterMoveIfNeeded(monster, nowMs, true);
                        CastMonsterAttack(runtime, monster, aggro, nowMs);
                        monster.NextAttackMs = nowMs + Math.Max(100, monster.AttackIntervalMs);
                    }
                }
            }

            // 伙伴进场推送要等客户端进场景后再发，这里补一次。
            if (!runtime.PartnersPushed)
            {
                runtime.PartnersPushed = true;
                PushPartnerObjects(runtime);
            }

            // 伙伴 AI：跟随主人 / 索敌 / 追击 / 攻击
            if (!runtime.Player.Dead)
            {
                long partnerDt = Math.Max(0, Math.Min(nowMs - runtime.LastTickMs, 50));
                UpdatePartners(runtime, partnerDt, nowMs);
            }

            // 处理延迟生效的伤害，对应原服 desk 主循环消费 chDelayAttack。
            ResolvePendingHits(runtime, nowMs);
            if (runtime.Player.Dead && runtime.PlayerDeadAtMs <= 0)
            {
                // 原服 desk.go 的死亡判定按房间类型分：
                //   战役/首领/试炼塔/遗迹 → 1.5 秒后判负（来不及复活）
                //   每日副本（经验/金币/强化石/书页）→ 60 秒等待期，期间可以花道具复活
                runtime.PlayerDeadAtMs = nowMs + (IsDailyMapRoom(runtime.RoomType) ? DailyDeadWaitMs : DeadSettleDelayMs);
            }

            // 「全清通关」只对 battle 型房间成立；刷怪型房间的怪会重生，清光也不是结束条件
            if (runtime.Cfg != null)
            {
                bool allDead = true;
                for (int i = 0; i < runtime.Monsters.Count; i++)
                {
                    if (runtime.Monsters[i] != null && !runtime.Monsters[i].Dead)
                    {
                        allDead = false;
                        break;
                    }
                }
                if (allDead)
                {
                    if (runtime.AllDeadAtMs <= 0)
                        runtime.AllDeadAtMs = nowMs + WinSettleDelayMs;
                    else if (nowMs >= runtime.AllDeadAtMs)
                        Settle(GameOverType.GameOverTypeMDead);
                }
            }
            else if (runtime.DailyCfg != null)
            {
                UpdateDailyRoom(runtime, nowMs);
            }
            runtime.LastTickMs = nowMs;
        }

        private void Settle(GameOverType type)
        {
            BattleRuntime runtime = _runtime;
            if (runtime == null || runtime.Ended)
                return;
            runtime.Ended = true;
            _runtime = null;

            LocalRole role = LocalData.Instance.Current;
            var result = new GameResultBattlePush
            {
                BattleId = runtime.BattleId,
                RoomId = runtime.RoomId,
                OverType = type,
                Items = new List<ItemInfo>(),
                CompletionTime = Math.Max(0, (NowMs() - runtime.BeginMs) / 1000),
            };

            // 刷怪型房间（Cfg 为空）没有 Settlement / 进度概念，结算只推 GameResultBattlePush
            if (type == GameOverType.GameOverTypeMDead && role != null && runtime.Cfg != null)
            {
                for (int i = 0; i < runtime.Cfg.Settlement.Length; i++)
                {
                    CFGItem_Battle.AwardItem award = runtime.Cfg.Settlement[i];
                    if (award.id <= 0 || award.num <= 0)
                        continue;
                    ItemService.Instance.GrantReward(role, award.id, award.num);
                    result.Items.Add(new ItemInfo { CfgId = award.id, Num = award.num });

                    if (_pendingDoubleReward == null)
                        _pendingDoubleReward = new Dictionary<int, long>();
                    long existNum;
                    _pendingDoubleReward[award.id] = _pendingDoubleReward.TryGetValue(award.id, out existNum)
                        ? existNum + award.num : award.num;
                }
                _pendingDoubleRoomType = runtime.RoomType;

                bool isBossBattle = runtime.Cfg.Copytype == (int)CFG_Room.RoomType.Boss;
                bool isTowerBattle = runtime.Cfg.Copytype == (int)CFG_Room.RoomType.SLTower;
                bool isRelicBattle = runtime.Cfg.Copytype == (int)CFG_Room.RoomType.Relic;
                if (isBossBattle)
                {
                    // 原服 BossMap 分支：进度取最大值，结算时扣挑战次数，
                    // 派发 BOSS_LEVELS_CLEARED + TOTAL_BOSS_CHALLENGE。
                    if (role.BossId < runtime.BattleId)
                        role.BossId = runtime.BattleId;
                    if (role.BossChallengeTime > 0)
                        role.BossChallengeTime--;
                    TaskService.Instance.Dispatch(role, TargetType.BOSS_LEVELS_CLEARED,
                        runtime.BattleId, 1);
                    TaskService.Instance.Dispatch(role, TargetType.TOTAL_BOSS_CHALLENGE, 0, 1);
                }
                else if (isTowerBattle)
                {
                    // 原服 TrialTowerMap 分支：进度直接赋值（进入前已校验必须是下一层），
                    // 不扣次数，只派发 TRIAL_TOWER_FLOORS_CLEARED。
                    role.TrialTowerId = runtime.BattleId;
                    TaskService.Instance.Dispatch(role, TargetType.TRIAL_TOWER_FLOORS_CLEARED,
                        runtime.BattleId, 1);
                }
                else if (isRelicBattle)
                {
                    // 原服 RelicMap 分支：派发任务 → 进度取 max → **成功通关都扣次数** → 算 1~3 星评价
                    TaskService.Instance.Dispatch(role, TargetType.RELIC_CHAPTERS_CLEARED,
                        runtime.BattleId, 1);
                    TaskService.Instance.Dispatch(role, TargetType.TOTAL_RELIC_CHALLENGE, 0, 1);
                    if (role.RelicId < runtime.BattleId)
                        role.RelicId = runtime.BattleId;
                    if (role.RelicChallengeTime > 0)
                        role.RelicChallengeTime--;
                    RecordRelicStar(role, runtime.BattleId, ComputeRelicStar(runtime));
                }
                else
                {
                    if (role.BattleId < runtime.BattleId)
                        role.BattleId = runtime.BattleId;
                    TaskService.Instance.Dispatch(role, TargetType.BATTLE_CHAPTERS_CLEARED,
                        runtime.BattleId, 1);
                }
                SaveCoordinator.Instance.MarkDirty();
                SaveCoordinator.Instance.SaveNow();
                LocalPusher.Instance.PushUserBaseNow();
                LocalPusher.Instance.PushBagInfoNow();
                LocalPusher.Instance.PushStatsNow();
                // 首领（剩余次数）/ 试炼塔（层数）界面要刷新
                if (isBossBattle || isTowerBattle || isRelicBattle)
                {
                    var infoResp = PlayerService.Instance.GetInstanceInfo()
                        as InstanceInfoResponse;
                    if (infoResp != null)
                        LocalPusher.Instance.PushInstanceInfoNow(infoResp);
                }
            }

            // 每日副本结算（时间到 / 波次打完都发奖），对齐原服 dailymapGameOver
            if (role != null && runtime.Cfg == null && runtime.DailyCfg != null)
                SettleDailyReward(runtime, role, result);

            // 守卫海岛：按实际停留时长扣每日挂机时间（对齐原服 delAfkLeftTime）
            if (runtime.AfkRoom)
                AccumulateAfkTime(runtime);

            LocalPusher.Instance.PushBattleResultNow(result);
            Log.Print("[BattleService] settle type=" + type + " battle=" + runtime.BattleId
                + " seed=" + runtime.Seed);
        }

        /// <summary>
        /// 解析玩家出生点。
        /// 原服 desk.playerJoin 取 mapCfg.BornPoints[0]，再用 mapInfo.getAvailableCell 找可用格。
        /// 单机侧没有服务端地图格子数据，这里直接取配置出生点，缺失时回退。
        ///
        /// mapIdOverride &gt; 0 时（battle 配置为空的刷怪型房间）优先用它，
        /// 否则用 cfg.MapID。
        /// </summary>
        private static void ResolveBornPoint(CFGItem_Battle cfg, int mapIdOverride,
            out int x, out int y)
        {
            x = FallbackBornX;
            y = FallbackBornY;
            try
            {
                int mapId = mapIdOverride > 0 ? mapIdOverride : (cfg != null ? (int)cfg.MapID : 0);
                if (mapId <= 0)
                    return;
                CFGItem_Map mapCfg = CFG_Map.GetById(mapId);
                if (mapCfg == null || mapCfg.Born == null || mapCfg.Born.Length == 0)
                    return;
                x = mapCfg.Born[0].x;
                y = mapCfg.Born[0].y;
            }
            catch (Exception e)
            {
                Log.Print("[BattleService] 读取地图出生点失败 mapId="
                    + (mapIdOverride > 0 ? mapIdOverride : (cfg != null ? (int)cfg.MapID : 0))
                    + "，使用回退点 " + x + "," + y + " " + e.Message);
            }
        }

        /// <summary>
        /// 解析可行走矩形，对齐客户端 SceneManager.loadMapInfo：
        /// MinX = 6, MaxX = Wide - 6, MinY = 13, MaxY = High - 13；
        /// MinY > MaxY 时退化为 Height/2 - 1 的单行。
        ///
        /// 客户端 GetOffZ 只有边界判定（真实障碍数据加载在客户端被注释掉了），
        /// 所以服务端只需要钳制边界，不需要寻路。
        /// </summary>
        private static bool ResolveBounds(CFGItem_Battle cfg, int mapIdOverride,
            out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = 0;
            maxX = 0;
            minY = 0;
            maxY = 0;
            try
            {
                int mapId = mapIdOverride > 0 ? mapIdOverride : (cfg != null ? (int)cfg.MapID : 0);
                if (mapId <= 0)
                    return false;
                CFGItem_Map mapCfg = CFG_Map.GetById(mapId);
                if (mapCfg == null || mapCfg.Wide <= 0 || mapCfg.High <= 0)
                    return false;

                minX = MapOffX;
                maxX = mapCfg.Wide - MapOffX;
                minY = MapOffY;
                maxY = mapCfg.High - MapOffY;
                if (minY > maxY)
                {
                    minY = mapCfg.High / 2 - 1;
                    maxY = minY;
                }
                return maxX >= minX && maxY >= minY;
            }
            catch (Exception e)
            {
                Log.Print("[BattleService] 读取地图边界失败 mapId="
                    + (mapIdOverride > 0 ? mapIdOverride : (cfg != null ? (int)cfg.MapID : 0))
                    + " " + e.Message);
                return false;
            }
        }

        /// <summary>把坐标钳制进可行走矩形，避免客户端 GetOffZ 返回 Block 后表现停住。</summary>
        private static void ClampToMap(BattleRuntime runtime, ref double x, ref double y)
        {
            if (runtime == null || !runtime.HasBounds)
                return;
            if (x < runtime.MinX) x = runtime.MinX;
            if (x > runtime.MaxX) x = runtime.MaxX;
            if (y < runtime.MinY) y = runtime.MinY;
            if (y > runtime.MaxY) y = runtime.MaxY;
        }

        /// <summary>
        /// 把上阵伙伴放进战场。原服伙伴是独立 MapObject（ObjectType.PARTNER），
        /// 客户端靠 GameObjectAddPush 生成模型。
        /// 出生位置取玩家身边两格，再按格子钳制。
        /// </summary>
        private void SpawnPartners(BattleRuntime runtime)
        {
            if (runtime == null)
                return;
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return;

            List<PartnerInfo> partners = PartnerService.Instance.GetBattlePartners(role);
            for (int i = 0; i < partners.Count; i++)
            {
                PartnerInfo info = partners[i];
                if (info == null)
                    continue;
                CFGItem_Partner cfg = GetPartnerCfg(info.CfgId);
                if (cfg == null)
                    continue;

                // 伙伴是独立战斗单位，属性用**伙伴自己的最终属性表**
                // （含百分比放大）。原服 getScenePartner 里 MaxHp 取 partner.GetMaxHP()。
                Dictionary<Attribute, int> stats = RoleStatService.BuildPartnerStatsFinal(info);
                int maxHp = GetStatValue(stats, Attribute.MAX_HEALTH);
                if (maxHp <= 0)
                    maxHp = 100;

                double px = runtime.Player.X + (i % 2 == 0 ? 1.2 : -1.2);
                double py = runtime.Player.Y - 1.2 * (i / 2 + 1);
                ClampToMap(runtime, ref px, ref py);

                var unit = new BattleUnit
                {
                    Uid = info.Uid,
                    CfgId = info.CfgId,
                    IsPartner = true,
                    OwnerUid = role.Uid,
                    Name = cfg.Name,
                    Level = info.Level,
                    MaxHp = maxHp,
                    Hp = maxHp,
                    Attack = Math.Max(1, GetStatValue(stats, Attribute.PHYSICAL_ATTACK)
                        + GetStatValue(stats, Attribute.MAGIC_ATTACK)),
                    Defense = Math.Max(0, GetStatValue(stats, Attribute.PHYSICAL_DEFENSE)
                        + GetStatValue(stats, Attribute.MAGIC_DEFENSE)),
                    Hit = GetStatValue(stats, Attribute.HIT),
                    Dodge = GetStatValue(stats, Attribute.DODGE),
                    Critical = GetStatValue(stats, Attribute.CRITICAL_STRIKE),
                    Toughness = GetStatValue(stats, Attribute.TOUGHNESS),
                    CriticalDamage = GetStatValue(stats, Attribute.CRITICAL_DAMAGE_PERCENTAGE),
                    AttackRange = Math.Max(1, cfg.AttackDistance),
                    AttackIntervalMs = 1000,
                    AttackSkillId = PartnerNormalSkillId,
                    PartnerSkills = CopyPartnerSkillIds(info),
                    MoveSpeed = 3,
                    NextAttackMs = NowMs() + 600,
                    X = px,
                    Y = py,
                    BornX = px,
                    BornY = py,
                    Direction = Direction.DOWN,
                    PushedX = Math.Floor(px),
                    PushedY = Math.Floor(py),
                };
                runtime.Partners.Add(unit);
            }
        }

        /// <summary>
        /// 补推伙伴进场。必须在客户端已经进战斗场景之后推，
        /// 否则 FightController 会因 GameState == No 直接丢掉。
        /// </summary>
        private void PushPartnerObjects(BattleRuntime runtime)
        {
            if (runtime == null)
                return;
            for (int i = 0; i < runtime.Partners.Count; i++)
            {
                BattleUnit unit = runtime.Partners[i];
                if (unit == null)
                    continue;
                LocalPusher.Instance.PushBattleObjectAddNow(new GameObjectAddPush
                {
                    Info = new ObjectInfo
                    {
                        Type = ObjectType.PARTNER,
                        Uid = unit.Uid,
                        CfgID = unit.CfgId,
                        Level = unit.Level,
                        Name = unit.Name,
                        Location = new Point { X = unit.X, Y = unit.Y },
                        Direction = unit.Direction,
                        MaxHp = unit.MaxHp,
                        CurHp = unit.Hp,
                        OwnerUid = unit.OwnerUid,
                        BuffList = new List<BuffInfo>(),
                        Attributes = new List<Stats>(),
                    },
                });
            }
        }

        /// <summary>
        /// 伙伴 AI，对齐原服 Partner.processAI 的最小集：
        /// 索敌（优先跟主人打同一个目标）→ 追击 → 攻击；无目标时跟随主人。
        /// </summary>
        /// <summary>
        /// 怪物当前仇恨目标。对齐原服 `MapObject.target`：
        ///   · 首次被玩家/伙伴打中时锁定攻击者（见 ApplyHit）；
        ///   · 目标死亡或被清空 → 回退到玩家（玩家也死则无目标、回位待机）。
        ///
        /// ⚠️ 原服 `CanAttackTarget` 对已死目标返回 false，怪物会对着尸体空转；
        ///    这里做了个更合理的小改动：目标死后直接回退玩家，避免卡死。
        /// </summary>
        private static BattleUnit GetMonsterAggroTarget(BattleRuntime runtime, BattleUnit monster)
        {
            if (runtime == null || monster == null)
                return null;
            if (!monster.HasTarget && monster.Target == null)
                return null;

            BattleUnit target = monster.Target;
            if (target != null && !target.Dead)
                return target;

            // 锁定的目标已死/丢失：回退到还活着的玩家
            if (!runtime.Player.Dead)
            {
                monster.Target = runtime.Player;
                return runtime.Player;
            }
            return null;
        }

        private void UpdatePartners(BattleRuntime runtime, long dtMs, long nowMs)
        {
            if (runtime == null || runtime.Partners.Count == 0)
                return;

            for (int i = 0; i < runtime.Partners.Count; i++)
            {
                BattleUnit partner = runtime.Partners[i];
                if (partner == null || partner.Dead)
                    continue;

                BattleUnit target = FindPartnerTarget(runtime, partner);
                if (target == null)
                {
                    // 跟随主人：离得太远就走过去
                    double near = Distance(partner, runtime.Player);
                    if (near > 2.0)
                    {
                        StepMonsterTowards(partner, runtime.Player.X, runtime.Player.Y,
                            1.5, dtMs, runtime, nowMs);
                        PushMonsterMoveIfNeeded(partner, nowMs, false);
                    }
                    continue;
                }

                double dist = Distance(partner, target);
                if (dist > partner.AttackRange)
                {
                    StepMonsterTowards(partner, target.X, target.Y,
                        partner.AttackRange * 0.8, dtMs, runtime, nowMs);
                    PushMonsterMoveIfNeeded(partner, nowMs, false);
                }
                else if (nowMs >= partner.NextAttackMs)
                {
                    PushMonsterMoveIfNeeded(partner, nowMs, true);
                    CastPartnerAttack(runtime, partner, target, nowMs);
                    partner.NextAttackMs = nowMs + Math.Max(200, partner.AttackIntervalMs);
                }
            }
        }

        /// <summary>
        /// 伙伴索敌。原服 processSearch 用**主人的位置**算距离，
        /// 范围是 partnerCfg.AttackDistance，取最近的怪。
        /// </summary>
        private BattleUnit FindPartnerTarget(BattleRuntime runtime, BattleUnit partner)
        {
            if (runtime == null || partner == null)
                return null;

            int range = Math.Max(1, partner.AttackRange);
            BattleUnit closest = null;
            int best = int.MaxValue;
            for (int i = 0; i < runtime.Monsters.Count; i++)
            {
                BattleUnit monster = runtime.Monsters[i];
                if (monster == null || monster.Dead)
                    continue;
                int d = GridDistance(runtime.Player.X, runtime.Player.Y, monster.X, monster.Y);
                if (d > range)
                    continue;
                if (d < best)
                {
                    best = d;
                    closest = monster;
                }
            }
            return closest;
        }

        /// <summary>
        /// 伙伴攻击。普攻用 999900（原服 NormalSkillID），有技能则轮着放，
        /// 和玩家一样走 AttackPush + 延迟伤害。
        /// </summary>
        private void CastPartnerAttack(BattleRuntime runtime, BattleUnit partner, BattleUnit target, long nowMs)
        {
            if (runtime == null || partner == null || target == null || target.Dead)
                return;

            int skillId = PickPartnerSkill(partner, nowMs);
            CFGItem_Skill skillCfg = GetSkillCfg(skillId);

            Direction facing = DirectionFromPoint(partner.X, partner.Y, target.X, target.Y);
            partner.Direction = facing;

            LocalPusher.Instance.PushBattleAttackNow(new AttackPush
            {
                AttackerId = partner.Uid,
                SkillId = skillId,
                TargetUids = new long[] { target.Uid },
                Location = new Point { X = target.X, Y = target.Y },
                Direction = facing,
            });

            int delayMs = skillCfg != null ? (int)skillCfg.Delay : 0;
            if (delayMs < 0)
                delayMs = 0;
            runtime.PendingHits.Add(new PendingHit
            {
                AtMs = nowMs + delayMs,
                Attacker = partner,
                Target = target,
                SkillId = skillId,
            });
        }

        /// <summary>伙伴技能轮转：平均 3 秒放一次，其余用普攻。</summary>
        private static int PickPartnerSkill(BattleUnit partner, long nowMs)
        {
            if (partner == null || partner.PartnerSkills == null || partner.PartnerSkills.Count == 0)
                return PartnerNormalSkillId;
            if (nowMs < partner.NextSkillMs)
                return PartnerNormalSkillId;

            int index = partner.SkillCursor % partner.PartnerSkills.Count;
            partner.SkillCursor++;
            partner.NextSkillMs = nowMs + 3000;
            return partner.PartnerSkills[index];
        }

        private static List<int> CopyPartnerSkillIds(PartnerInfo info)
        {
            var ids = new List<int>();
            if (info == null || info.Skills == null)
                return ids;
            for (int i = 0; i < info.Skills.Count; i++)
            {
                SkillInfo skill = info.Skills[i];
                if (skill != null && skill.CfgId > 0)
                    ids.Add(skill.CfgId);
            }
            return ids;
        }

        private static int GetStatValue(Dictionary<Attribute, int> stats, Attribute attr)
        {
            if (stats == null)
                return 0;
            int value;
            return stats.TryGetValue(attr, out value) ? value : 0;
        }

        private static CFGItem_Partner GetPartnerCfg(int cfgId)
        {
            try { return CFG_Partner.GetById(cfgId); }
            catch { return null; }
        }

        private void SpawnMonsters(BattleRuntime runtime)
        {
            if (runtime == null)
                return;

            // 刷怪型房间（魂石副本）：battle 配置为空，怪物来自 map.json 的 MonsterIds
            if (runtime.MapCfg != null)
            {
                SpawnMonstersFromMap(runtime);
                return;
            }

            if (runtime.Cfg == null || runtime.Cfg.Monster == null)
                return;

            for (int i = 0; i < runtime.Cfg.Monster.Length; i++)
            {
                CFGItem_Battle.AwardItem group = runtime.Cfg.Monster[i];
                // AwardItem 是 struct，不能判 null；num 为 0 时下面的 for 自然不执行
                for (int n = 0; n < group.num; n++)
                {
                    CFGItem_Monster cfg;
                    try { cfg = CFG_Monster.GetById(group.id); }
                    catch { cfg = null; }
                    if (cfg == null)
                        continue;

                    int maxHp = 0;
                    int attack = 0;
                    int defense = 0;
                    int hit = 0;
                    int dodge = 0;
                    int crit = 0;
                    int toughness = 0;
                    int moveSpeed = 2;
                    if (cfg.BassProperty != null)
                    {
                        for (int p = 0; p < cfg.BassProperty.Length; p++)
                        {
                            Dictionary<string, int> entries = cfg.BassProperty[p];
                            if (entries == null) continue;
                            foreach (KeyValuePair<string, int> kv in entries)
                            {
                                int attr;
                                if (!int.TryParse(kv.Key, out attr)) continue;
                                switch ((Attribute)attr)
                                {
                                    case Attribute.PHYSICAL_ATTACK:
                                    case Attribute.MAGIC_ATTACK:
                                        attack += kv.Value;
                                        break;
                                    case Attribute.PHYSICAL_DEFENSE:
                                    case Attribute.MAGIC_DEFENSE:
                                        defense += kv.Value;
                                        break;
                                    case Attribute.HIT:
                                        hit += kv.Value;
                                        break;
                                    case Attribute.DODGE:
                                        dodge += kv.Value;
                                        break;
                                    case Attribute.CRITICAL_STRIKE:
                                        crit += kv.Value;
                                        break;
                                    case Attribute.TOUGHNESS:
                                        toughness += kv.Value;
                                        break;
                                    case Attribute.MAX_HEALTH:
                                        maxHp += kv.Value;
                                        break;
                                    case Attribute.MOVE_SPEED:
                                        moveSpeed = Math.Max(1, kv.Value / 100);
                                        break;
                                }
                            }
                        }
                    }
                    if (maxHp <= 0) maxHp = 100;

                    double baseX = group.point.x > 0 ? group.point.x : runtime.Player.X + 3;
                    double baseY = group.point.y > 0 ? group.point.y : runtime.Player.Y;
                    double x = baseX;
                    double y = baseY;
                    if (group.num > 1)
                    {
                        // 同一组怪物原本共用一个点，会和玩家/彼此完全重叠，
                        // 表现为一堆怪叠在一格上，这里绕圈散开。
                        double angle = Math.PI * 2 * n / group.num;
                        x = baseX + Math.Cos(angle) * 1.2;
                        y = baseY + Math.Sin(angle) * 1.2;
                    }
                    ClampToMap(runtime, ref x, ref y);
                    var unit = new BattleUnit
                    {
                        Uid = runtime.NextMonsterUid++,
                        CfgId = cfg.ID,
                        Name = cfg.Name,
                        Level = cfg.Level,
                        MaxHp = maxHp,
                        Hp = maxHp,
                        Attack = Math.Max(1, attack),
                        Defense = Math.Max(0, defense),
                        Hit = hit,
                        Dodge = dodge,
                        Critical = crit,
                        Toughness = toughness,
                        AttackRange = Math.Max(1, cfg.AttackDistance),
                        AttackIntervalMs = cfg.Delay > 0 ? cfg.Delay : DefaultAttackIntervalMs,
                        AttackSkillId = cfg.Skills != null && cfg.Skills.Length > 0
                            ? cfg.Skills[0] : GetPlayerNormalSkill(1),
                        MoveSpeed = moveSpeed,
                        NextAttackMs = NowMs() + (cfg.Delay > 0 ? cfg.Delay : DefaultAttackIntervalMs),
                        X = x,
                        Y = y,
                        BornX = x,
                        BornY = y,
                        ChaseRange = cfg.Chase > 0 ? cfg.Chase : 0,
                        Direction = Direction.DOWN,
                        IsBoss = cfg.Isboss > 0,
                        PushedX = Math.Floor(x),
                        PushedY = Math.Floor(y),
                    };
                    runtime.Monsters.Add(unit);
                }
            }
            if (runtime.Monsters.Count == 0)
                runtime.AllDeadAtMs = NowMs() + WinSettleDelayMs;
        }

        /// <summary>
        /// 地图驱动刷怪（魂石副本 / 之后的每日副本复用）。
        ///
        /// 对齐原服 desk 按 `mapCfg.MonsterIds` 铺怪：每条 {id, num, point}，
        /// point 是刷怪锚点，num 只怪绕锚点散开（原服是用 point 去 mapInfo.getAvailableCell 找空地，
        /// 单机没有格子数据，改成绕圈散开 + 边界钳制）。
        /// MaxMonster 是上限，超过就停。
        ///
        /// ⚠️ 属性解析这段和上面 battle 分支是同构的，暂时保留两份：
        ///    动 battle 分支风险太高（battle/boss/tower 30+ 条断言都走它），
        ///    等每日副本接完再考虑合并。
        /// </summary>
        private void SpawnMonstersFromMap(BattleRuntime runtime)
        {
            CFGItem_Map mapCfg = runtime.MapCfg;
            if (mapCfg == null || mapCfg.MonsterIds == null)
                return;

            int maxMonster = (int)mapCfg.MaxMonster;
            for (int i = 0; i < mapCfg.MonsterIds.Count; i++)
            {
                CFGItem_Map.CFGItem_MonsterId entry = mapCfg.MonsterIds[i];
                if (entry == null)
                    continue;

                CFGItem_Monster cfg;
                try { cfg = CFG_Monster.GetById(entry.id); }
                catch { cfg = null; }
                if (cfg == null)
                    continue;

                // struct 先拷到局部变量再读字段（ILRuntime）
                CFGItem_Map.CFGItem_BornPoint anchor = entry.point;

                for (int n = 0; n < entry.num; n++)
                {
                    if (maxMonster > 0 && runtime.Monsters.Count >= maxMonster)
                        break;

                    double baseX = anchor.x > 0 ? anchor.x : runtime.Player.X + 3;
                    double baseY = anchor.y > 0 ? anchor.y : runtime.Player.Y;
                    double x = baseX;
                    double y = baseY;
                    if (entry.num > 1)
                    {
                        double angle = Math.PI * 2 * n / entry.num;
                        // 7 只以上扩一圈，避免一圈挤在一起
                        double radius = 1.2 + 0.8 * (n / 7);
                        x = baseX + Math.Cos(angle) * radius;
                        y = baseY + Math.Sin(angle) * radius;
                    }
                    ClampToMap(runtime, ref x, ref y);

                    int maxHp = 0;
                    int attack = 0;
                    int defense = 0;
                    int hit = 0;
                    int dodge = 0;
                    int crit = 0;
                    int toughness = 0;
                    int moveSpeed = 2;
                    if (cfg.BassProperty != null)
                    {
                        for (int p = 0; p < cfg.BassProperty.Length; p++)
                        {
                            Dictionary<string, int> entries = cfg.BassProperty[p];
                            if (entries == null) continue;
                            foreach (KeyValuePair<string, int> kv in entries)
                            {
                                int attr;
                                if (!int.TryParse(kv.Key, out attr)) continue;
                                switch ((Attribute)attr)
                                {
                                    case Attribute.PHYSICAL_ATTACK:
                                    case Attribute.MAGIC_ATTACK:
                                        attack += kv.Value;
                                        break;
                                    case Attribute.PHYSICAL_DEFENSE:
                                    case Attribute.MAGIC_DEFENSE:
                                        defense += kv.Value;
                                        break;
                                    case Attribute.HIT:
                                        hit += kv.Value;
                                        break;
                                    case Attribute.DODGE:
                                        dodge += kv.Value;
                                        break;
                                    case Attribute.CRITICAL_STRIKE:
                                        crit += kv.Value;
                                        break;
                                    case Attribute.TOUGHNESS:
                                        toughness += kv.Value;
                                        break;
                                    case Attribute.MAX_HEALTH:
                                        maxHp += kv.Value;
                                        break;
                                    case Attribute.MOVE_SPEED:
                                        moveSpeed = Math.Max(1, kv.Value / 100);
                                        break;
                                }
                            }
                        }
                    }
                    if (maxHp <= 0) maxHp = 100;

                    // 每日副本怪物成长：攻/防/血 × [INT(等级 / accommodation) + 1]
                    int scale = runtime.MonsterStatScale > 0 ? runtime.MonsterStatScale : 1;
                    if (scale > 1)
                    {
                        maxHp *= scale;
                        attack *= scale;
                        defense *= scale;
                    }

                    // 经验/金币副本的评分基数 = 那只 boss 的最大血量（原服取第一只算掉血百分比）
                    if (runtime.ScoreBossMaxHp <= 0)
                        runtime.ScoreBossMaxHp = maxHp;

                    var unit = new BattleUnit
                    {
                        Uid = runtime.NextMonsterUid++,
                        CfgId = cfg.ID,
                        Name = cfg.Name,
                        Level = cfg.Level,
                        MaxHp = maxHp,
                        Hp = maxHp,
                        Attack = Math.Max(1, attack),
                        Defense = Math.Max(0, defense),
                        Hit = hit,
                        Dodge = dodge,
                        Critical = crit,
                        Toughness = toughness,
                        AttackRange = Math.Max(1, cfg.AttackDistance),
                        AttackIntervalMs = cfg.Delay > 0 ? cfg.Delay : DefaultAttackIntervalMs,
                        AttackSkillId = cfg.Skills != null && cfg.Skills.Length > 0
                            ? cfg.Skills[0] : GetPlayerNormalSkill(1),
                        MoveSpeed = moveSpeed,
                        NextAttackMs = NowMs() + (cfg.Delay > 0 ? cfg.Delay : DefaultAttackIntervalMs),
                        X = x,
                        Y = y,
                        BornX = x,
                        BornY = y,
                        ChaseRange = cfg.Chase > 0 ? cfg.Chase : 0,
                        Direction = Direction.DOWN,
                        IsBoss = cfg.Isboss > 0,
                        PushedX = Math.Floor(x),
                        PushedY = Math.Floor(y),
                    };
                    runtime.Monsters.Add(unit);
                }
            }
        }

        private Direction DirectionForMovement(Point location)
        {
            if (_runtime == null)
                return Direction.DOWN;
            double dx = location.X - _runtime.Player.X;
            double dy = location.Y - _runtime.Player.Y;
            if (Math.Abs(dx) > Math.Abs(dy))
                return dx >= 0 ? Direction.RIGHT : Direction.LEFT;
            return dy >= 0 ? Direction.UP : Direction.DOWN;
        }

        private static Direction DirectionTowards(double dx, double dy)
        {
            return DirectionFromPoint(0, 0, dx, dy);
        }

        /// <summary>
        /// 怪物按格补推 MovePush。原服 monster_move 每移动一格广播一次，
        /// 客户端 MonsterManager.monsterMove 才会播 walk 补间。
        /// </summary>
        private void PushMonsterMoveIfNeeded(BattleUnit monster, long nowMs, bool force)
        {
            if (monster == null || monster.Dead)
                return;
            double gx = Math.Floor(monster.X);
            double gy = Math.Floor(monster.Y);
            if (gx == monster.PushedX && gy == monster.PushedY)
                return;
            if (!force && nowMs - monster.LastMovePushMs < MinMovePushIntervalMs)
                return;

            monster.PushedX = gx;
            monster.PushedY = gy;
            monster.LastMovePushMs = nowMs;
            LocalPusher.Instance.PushBattleMoveNow(new MovePush
            {
                Uid = monster.Uid,
                Location = new Point { X = gx, Y = gy },
                Direction = monster.Direction,
            });
        }

        private BattleUnit FindMonster(long uid)
        {
            if (_runtime == null)
                return null;
            for (int i = 0; i < _runtime.Monsters.Count; i++)
            {
                if (_runtime.Monsters[i] != null && _runtime.Monsters[i].Uid == uid)
                    return _runtime.Monsters[i];
            }
            return null;
        }

        /// <summary>
        /// 怪物朝目标走一步。原服 moveTo(path[1]) 是按格走，这里按 5ms tick 连续推进，
        /// 但仍然只在跨格时补推 MovePush 给客户端。
        /// </summary>
        private void StepMonsterTowards(BattleUnit monster, double tx, double ty,
            double stopDistance, long dtMs, BattleRuntime runtime, long nowMs)
        {
            double dx = tx - monster.X;
            double dy = ty - monster.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist <= stopDistance + 0.01)
                return;

            double step = Math.Max(0.01, monster.MoveSpeed * 0.001 * dtMs);
            double allowed = dist - stopDistance;
            if (step > allowed)
                step = allowed;
            if (step <= 0)
                return;

            monster.X += dx / dist * step;
            monster.Y += dy / dist * step;
            monster.Direction = DirectionTowards(dx, dy);
            ClampToMap(runtime, ref monster.X, ref monster.Y);
        }

        /// <summary>
        /// 怪物攻击：原服 MonsterInfo.attack 里同样是先广播 AttackPush，
        /// 再 `time.AfterFunc(skillCfg.DelayTime)` 把伤害丢进 chDelayAttack。
        /// </summary>
        private void CastMonsterAttack(BattleRuntime runtime, BattleUnit monster, BattleUnit target, long nowMs)
        {
            if (runtime == null || monster == null || target == null || target.Dead)
                return;

            int skillId = monster.AttackSkillId;
            CFGItem_Skill skillCfg = GetSkillCfg(skillId);

            Direction facing = DirectionFromPoint(monster.X, monster.Y,
                target.X, target.Y);
            monster.Direction = facing;

            LocalPusher.Instance.PushBattleAttackNow(new AttackPush
            {
                AttackerId = monster.Uid,
                SkillId = skillId,
                TargetUids = new long[] { target.Uid },
                Location = new Point { X = target.X, Y = target.Y },
                Direction = facing,
            });

            int delayMs = skillCfg != null ? (int)skillCfg.Delay : 0;
            if (delayMs < 0)
                delayMs = 0;
            runtime.PendingHits.Add(new PendingHit
            {
                AtMs = nowMs + delayMs,
                Attacker = monster,
                Target = target,
                SkillId = skillId,
            });
        }

        /// <summary>消费延迟伤害队列，对应原服 desk 主循环的 chDelayAttack。</summary>
        private void ResolvePendingHits(BattleRuntime runtime, long nowMs)
        {
            if (runtime.PendingHits.Count == 0)
                return;

            for (int i = runtime.PendingHits.Count - 1; i >= 0; i--)
            {
                PendingHit hit = runtime.PendingHits[i];
                if (hit == null)
                {
                    runtime.PendingHits.RemoveAt(i);
                    continue;
                }
                if (nowMs < hit.AtMs)
                    continue;

                runtime.PendingHits.RemoveAt(i);

                if (hit.Target == null || hit.Target.Dead)
                    continue;
                // 玩家已死时，在途伤害不再结算（原服 CanAttackTarget 也会拦掉）。
                if (runtime.Player.Dead && hit.Target == runtime.Player)
                    continue;
                if (hit.Attacker == null || hit.Attacker.Dead)
                    continue;

                ApplyHit(runtime, hit);
            }
        }

        /// <summary>
        /// 伤害落地，对齐原服 Player.delayAttack + MapObject.Attacked：
        /// 命中/闪避 → 斩杀 → 伤害 → 暴击 → 减伤 → 扣血 → 死亡。
        /// </summary>
        private void ApplyHit(BattleRuntime runtime, PendingHit hit)
        {
            BattleUnit attacker = hit.Attacker;
            BattleUnit target = hit.Target;
            CFGItem_Skill skillCfg = GetSkillCfg(hit.SkillId);

            // 原服 MapObject.Attacked：怪物被打后 target 才会设成攻击者。
            // 伙伴打怪也算（原服伙伴延迟攻击走的是同一套 Attacked）。
            // `o.target == nil` 才设 —— 首击锁定，之后不再改（除非目标死亡）。
            if (!target.IsPlayer && (attacker.IsPlayer || attacker.IsPartner))
            {
                target.HasTarget = true;
                if (target.Target == null || target.Target.Dead)
                    target.Target = attacker;
                // 测试用：记录「仇恨是否曾转向伙伴」。
                // 不能靠瞬态采样 —— 怪物追伙伴时可能超出 Chase 脱战并清空 target。
                if (attacker.IsPartner)
                    runtime.PartnerAggroed = true;
            }

            // 命中-闪避
            int hitRate = attacker.Hit;
            int dodge = target.Dodge;
            if (hitRate < dodge && runtime.Rng.Next(Math.Max(1, dodge)) > hitRate)
            {
                LocalPusher.Instance.PushBattleHpNow(new ObjectHpChangePush
                {
                    TargetId = target.Uid,
                    AttackerId = attacker.Uid,
                    ChangeHp = 0,
                    SkillId = hit.SkillId,
                    MaxHp = target.MaxHp,
                    Critical = false,
                    Miss = true,
                });
                Log.Print("[BattleService] miss attacker=" + attacker.Uid + " target=" + target.Uid);
                return;
            }

            int skillType = skillCfg != null ? (int)skillCfg.SkillID : 0;

            // 斩杀：skillId/100 == SkillTypeExecute 时，按 SpecialEffect.Slay 比例直接斩掉。
            if (skillType == SkillTypeExecute && skillCfg.SpecialEffect != null)
            {
                double slay = ReadSlayRate(skillCfg);
                if (slay > 0 && target.MaxHp > 0
                    && (double)target.Hp / target.MaxHp <= slay)
                {
                    int before = target.Hp;
                    target.Hp = 0;
                    target.Dead = true;
                    PushHpAndDie(runtime, attacker, target, hit.SkillId, before, false, 0);
                    return;
                }
            }

            bool critical = RollCritical(attacker, target);
            int advancedLevel = GetSkillAdvancedLevel(attacker, hit.SkillId);
            int power = ComputeFinalPower(attacker, target, skillCfg, advancedLevel, critical);

            // 治疗/无伤技能（原服 power = 0）只播表现，不扣血。
            if (power <= 0)
            {
                PushHpAndDie(runtime, attacker, target, hit.SkillId, 0, false, 0);
                return;
            }

            power = ApplyDamageReduction(target, attacker, power);
            power = Math.Max(1, power);

            int damage = Math.Min(power, target.Hp);
            target.Hp -= damage;
            if (target.Hp < 0)
                target.Hp = 0;
            bool dead = target.Hp <= 0;
            if (dead)
                target.Dead = true;

            PushHpAndDie(runtime, attacker, target, hit.SkillId, damage, critical, 0);
        }

        private void PushHpAndDie(BattleRuntime runtime, BattleUnit attacker, BattleUnit target,
            int skillId, int damage, bool critical, int unknown)
        {
            LocalPusher.Instance.PushBattleHpNow(new ObjectHpChangePush
            {
                TargetId = target.Uid,
                AttackerId = attacker.Uid,
                ChangeHp = -damage,
                SkillId = skillId,
                MaxHp = target.MaxHp,
                Critical = critical,
                Miss = false,
            });

            if (!target.Dead)
                return;

            LocalPusher.Instance.PushBattleDieNow(new ObjectDiePush
            {
                ObjectId = target.Uid,
                CanRevive = false,
            });

            // 原服 MonsterInfo.die：玩家击杀时派发 KILL_MONSTER_REQUIREMENT
            // 以及 TOTAL_KILLS_OF_NORMAL_MONSTERS / TOTAL_KILLS_OF_BOSS_MONSTERS。
            LocalRole role = LocalData.Instance.Current;
            if ((attacker.IsPlayer || attacker.IsPartner) && !target.IsPlayer && role != null)
            {
                TaskService.Instance.Dispatch(role, TargetType.KILL_MONSTER_REQUIREMENT, target.CfgId, 1);
                TaskService.Instance.Dispatch(role,
                    target.IsBoss ? TargetType.TOTAL_KILLS_OF_BOSS_MONSTERS
                                  : TargetType.TOTAL_KILLS_OF_NORMAL_MONSTERS,
                    target.CfgId, 1);
            }

            // 刷怪型房间：击杀结算掉落 + 安排原地重生（对齐原服 MonsterInfo.drop / relive）
            if (!target.IsPlayer && !target.IsPartner)
                OnMonsterKilled(runtime, target);
        }

        /// <summary>
        /// 怪物死亡后处理：掉落 + 重生（只有刷怪型房间会真的做事）。
        ///
        /// 对齐原服 MonsterInfo.drop：
        ///   · 逐组 roll `Dorp[].ratio`（万分比，可被玩家 ITEM_DROP_RATE_PERCENTAGE 加成）
        ///   · 命中后从 `id[]` 随机一个，数量取 [num.min, num.max-1]（原服 RandIn(max-min)）
        ///   · 掉落直接进背包，再推 ItemDropPush 播特效（不需要走过去拾取）
        ///   · 怪物经验 EXP 也一并给
        /// 重生对齐 monster_mgr：`time.AfterFunc(Relive 毫秒)` 后原地复活。
        /// </summary>
        private void OnMonsterKilled(BattleRuntime runtime, BattleUnit monster)
        {
            if (runtime == null || monster == null)
                return;

            CFGItem_Monster monCfg;
            try { monCfg = CFG_Monster.GetById(monster.CfgId); }
            catch { monCfg = null; }
            if (monCfg == null)
                return;

            if (runtime.DropOnKill)
                RollMonsterDrop(runtime, monster, monCfg);

            if (runtime.RespawnOnDeath)
            {
                int reliveMs = 0;
                try { reliveMs = (int)monCfg.Relive; }
                catch { reliveMs = 0; }
                if (reliveMs <= 0)
                    reliveMs = 1000;
                monster.ReviveAtMs = NowMs() + reliveMs;
            }
        }

        /// <summary>掉落结算（刷怪型房间）。</summary>
        private void RollMonsterDrop(BattleRuntime runtime, BattleUnit monster, CFGItem_Monster monCfg)
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return;

            var packet = new ItemDropPush
            {
                Items = new List<ItemInfo>(),
                Location = new Point { X = monster.X, Y = monster.Y },
                ObjectId = monster.Uid,
            };

            int dropRatePercent = 0;
            RoleStatSnapshot stats = RoleStatService.Instance.Recalculate(role, false);
            if (stats != null)
                dropRatePercent = stats.Get(Attribute.ITEM_DROP_RATE_PERCENTAGE);

            if (monCfg.Dorp != null)
            {
                for (int i = 0; i < monCfg.Dorp.Length; i++)
                {
                    CFGItem_Monster.DorpItem group = monCfg.Dorp[i];
                    if (group == null || group.id == null || group.id.Length == 0)
                        continue;

                    // 掉落率加成 = 玩家 ITEM_DROP_RATE_PERCENTAGE + 挂机倍率（守卫海岛）
                    double ratio = group.ratio
                        * (1.0 + (dropRatePercent + runtime.DropMultiplierPercent) / BaseRate);
                    int roll = runtime.Rng.Next((int)BaseRate);
                    if (roll >= (int)ratio)
                        continue;

                    int id = group.id[runtime.Rng.Next(group.id.Length)];
                    if (id <= 0)
                        continue;

                    int min = group.num != null ? group.num.min : 1;
                    int max = group.num != null ? group.num.max : min;
                    if (min <= 0) min = 1;
                    // 原服：num = Min + RandIn(Max-Min)，即取 [min, max-1]
                    int num = max > min ? min + runtime.Rng.Next(max - min) : min;
                    if (num <= 0)
                        continue;

                    // 掉落自动分解：对齐原服 addSource 里 `reason == ItemAddDrop && MainType == Equipment` 的分支。
                    // 命中就就地碎成材料 —— 不进背包、也不进掉落包（原服 packet.Type = 3，前端不弹获得提示）。
                    if (ItemService.Instance.TryAutoDecomposeDrop(role, id, num))
                        continue;

                    ItemService.Instance.GrantReward(role, id, num);
                    packet.Items.Add(new ItemInfo { CfgId = id, Num = num });
                }
            }

            // 怪物经验（原服 drop 末尾的 monCfg.Experience 段）
            if (monCfg.Experience > 0)
            {
                ItemService.Instance.GrantReward(role, 2, monCfg.Experience);
                packet.Items.Add(new ItemInfo { CfgId = 2, Num = monCfg.Experience });
            }

            if (packet.Items.Count > 0)
                JEvent.defaultEvent.Post(packet);
        }

        /// <summary>
        /// 遗迹评价星级（1~3），对齐原服 RelicMap 结算那段：
        ///   timestar 恒 1（原服时光限制那一路写死 1）
        ///   allalive：伙伴全活 +1
        ///   allhealth：剩余总血量 >= 50% +1
        /// 原服把玩家和伙伴的血量合起来算，这里同样是玩家 + 上场伙伴的合计。
        /// </summary>
        private static int ComputeRelicStar(BattleRuntime runtime)
        {
            if (runtime == null || runtime.Player == null)
                return 1;

            int star = 1;
            long curHp = runtime.Player.Hp;
            long maxHp = runtime.Player.MaxHp;
            bool allAlive = true;
            for (int i = 0; i < runtime.Partners.Count; i++)
            {
                BattleUnit partner = runtime.Partners[i];
                if (partner == null)
                    continue;
                if (partner.Dead)
                    allAlive = false;
                curHp += partner.Hp;
                maxHp += partner.MaxHp;
            }
            if (allAlive)
                star++;
            if (maxHp > 0 && curHp >= maxHp / 2)
                star++;
            return star;
        }

        /// <summary>记录遗迹通关星级（同一关只保留最高星）。</summary>
        private static void RecordRelicStar(LocalRole role, int battleId, int star)
        {
            if (role == null || battleId <= 0 || star <= 0)
                return;
            if (role.RelicEvaluation == null)
                role.RelicEvaluation = new List<KeyValue>();
            for (int i = 0; i < role.RelicEvaluation.Count; i++)
            {
                KeyValue kv = role.RelicEvaluation[i];
                if (kv != null && kv.K == battleId)
                {
                    if (star > kv.V)
                        kv.V = star;
                    return;
                }
            }
            role.RelicEvaluation.Add(new KeyValue { K = battleId, V = star });
        }

        /// <summary>
        /// 每日副本每帧逻辑，对齐原服 desk 主循环的三个分支：
        ///   经验(7)/金币(8)  ：评分 = 那只 boss 的掉血百分比落在 ScoreDic 的哪个档位；boss 死了就结算
        ///   强化石(9)/书页(10)：杀光一波 → round++ 再刷一波；round 到 totlecycle 就结算
        ///   升星石(11)/魂石(12)：没有额外逻辑（原地重生在 UpdateRevives 里做）
        /// </summary>
        private void UpdateDailyRoom(BattleRuntime runtime, long nowMs)
        {
            int roomType = runtime.RoomType;
            bool isExpOrGold = roomType == (int)CFG_Room.RoomType.EXP
                || roomType == (int)CFG_Room.RoomType.Gold;
            bool isWave = roomType == (int)CFG_Room.RoomType.QHS
                || roomType == (int)CFG_Room.RoomType.SY;

            if (isExpOrGold)
            {
                UpdateDailyScore(runtime);
                if (AllMonstersDead(runtime))
                {
                    if (runtime.AllDeadAtMs <= 0)
                        runtime.AllDeadAtMs = nowMs + WinSettleDelayMs;
                    else if (nowMs >= runtime.AllDeadAtMs)
                        Settle(GameOverType.GameOverTypeMDead);
                }
                return;
            }

            if (!isWave || !AllMonstersDead(runtime))
                return;

            int totalCycle = runtime.DailyCfg != null ? runtime.DailyCfg.totlecycleNum : 0;
            if (totalCycle > 0 && runtime.Round >= totalCycle)
            {
                if (runtime.AllDeadAtMs <= 0)
                    runtime.AllDeadAtMs = nowMs + WinSettleDelayMs;
                else if (nowMs >= runtime.AllDeadAtMs)
                    Settle(GameOverType.GameOverTypeMDead);
                return;
            }

            // 下一波：清掉尸体列表后按 MonsterIds 重新铺怪（原服是 chRelive 立刻补怪）
            runtime.Round++;
            runtime.AllDeadAtMs = 0;
            runtime.Monsters.Clear();
            SpawnMonstersFromMap(runtime);
            PushDailyMapScore(runtime, 0, runtime.Monsters.Count);
        }

        private static bool AllMonstersDead(BattleRuntime runtime)
        {
            if (runtime == null || runtime.Monsters.Count == 0)
                return true;
            for (int i = 0; i < runtime.Monsters.Count; i++)
            {
                if (runtime.Monsters[i] != null && !runtime.Monsters[i].Dead)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 经验/金币副本评分：原服用「boss 掉血百分比」查 ScoreDic（D/C/B/A/S/SS/SSS），
        /// 取满足的最大档；档位变化时推 DailyMapScorePush 让客户端换评级。
        /// </summary>
        private static void UpdateDailyScore(BattleRuntime runtime)
        {
            CFG_XMLConfig.XML_Config.XML_DailyMap dailyCfg = runtime.DailyCfg;
            if (dailyCfg == null || dailyCfg.ScoreDic == null || dailyCfg.ScoreDic.Length == 0)
                return;
            if (runtime.ScoreBossMaxHp <= 0 || runtime.Monsters.Count == 0)
                return;

            BattleUnit boss = runtime.Monsters[0];
            if (boss == null)
                return;

            double damage = runtime.ScoreBossMaxHp - boss.Hp;
            if (damage < 0) damage = 0;
            double percent = damage / runtime.ScoreBossMaxHp * BaseRate;

            string oldKey = runtime.ScoreKey;
            for (int i = 0; i < dailyCfg.ScoreDic.Length; i++)
            {
                CFG_XMLConfig.XML_Config.XML_DailyMap.ScoreItem item = dailyCfg.ScoreDic[i];
                if (item == null)
                    continue;
                if (percent >= item.value)
                {
                    runtime.ScoreKey = item.key;
                    runtime.ScoreValue = item.value;
                }
            }
            if (oldKey != runtime.ScoreKey)
                PushDailyMapScore(runtime, (long)damage, runtime.Monsters.Count);
        }

        private static void PushDailyMapScore(BattleRuntime runtime, long totalDamage, int monsterNum)
        {
            if (runtime == null)
                return;
            JEvent.defaultEvent.Post(new DailyMapScorePush
            {
                MType = (MapType)runtime.RoomType,
                TotalDamage = totalDamage,
                Scoring = runtime.ScoreKey == null ? "" : runtime.ScoreKey,
                MonsterNum = monsterNum,
                Round = runtime.Round,
            });
        }

        /// <summary>
        /// 每日副本结算奖励，对齐原服 dailymapGameOver：
        ///   经验/金币：baseValue.Num × (1 + 评分万分比/10000) × 玩家等级
        ///   强化石/书页：baseValue.Num × 波次 × 玩家等级
        ///   升星石/魂石：没有固定结算奖励（靠击杀掉落）
        /// </summary>
        private static void SettleDailyReward(
            BattleRuntime runtime, LocalRole role, GameResultBattlePush result)
        {
            CFG_XMLConfig.XML_Config.XML_DailyMap dailyCfg = runtime.DailyCfg;
            if (dailyCfg == null || role == null)
                return;

            int roomType = runtime.RoomType;
            int itemId = dailyCfg.baseValueID;
            int baseNum = dailyCfg.baseValueNum;
            if (itemId <= 0 || baseNum <= 0)
                return;

            double num;
            if (roomType == (int)CFG_Room.RoomType.EXP
                || roomType == (int)CFG_Room.RoomType.Gold)
            {
                num = baseNum * (1.0 + runtime.ScoreValue / BaseRate) * role.Level;
            }
            else if (roomType == (int)CFG_Room.RoomType.QHS
                || roomType == (int)CFG_Room.RoomType.SY)
            {
                int round = runtime.Round < 1 ? 1 : runtime.Round;
                num = baseNum * round * role.Level;
            }
            else
            {
                return;
            }

            long finalNum = (long)Math.Max(1.0, Math.Round(num));
            ItemService.Instance.GrantReward(role, itemId, finalNum);

            var rewardItems = new List<ItemInfo>();
            rewardItems.Add(new ItemInfo { CfgId = itemId, Num = finalNum });
            result.Items.Add(new ItemInfo { CfgId = itemId, Num = finalNum });

            // 原服推的是 GameResultDailyMapPush（每日副本专用结果面板）
            JEvent.defaultEvent.Post(new GameResultDailyMapPush
            {
                RoomId = runtime.RoomId,
                Items = rewardItems,
            });

            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushStatsNow();
            Log.Print("[BattleService] daily settle room=" + runtime.RoomId
                + " type=" + roomType + " round=" + runtime.Round
                + " score=" + runtime.ScoreKey + " reward=" + itemId + "x" + finalNum);
        }

        /// <summary>
        /// 到点复活。对齐原服 Relive：重新发一个怪物 Uid（原服 snowflake.GenID 新号），
        /// 回到出生点、满血、清掉仇恨，并重新广播 MonsterInPush。
        /// </summary>
        private void UpdateRevives(BattleRuntime runtime, long nowMs)
        {
            for (int i = 0; i < runtime.Monsters.Count; i++)
            {
                BattleUnit monster = runtime.Monsters[i];
                if (monster == null || !monster.Dead || monster.ReviveAtMs <= 0)
                    continue;
                if (nowMs < monster.ReviveAtMs)
                    continue;

                monster.Dead = false;
                monster.ReviveAtMs = 0;
                monster.Hp = monster.MaxHp;
                monster.X = monster.BornX;
                monster.Y = monster.BornY;
                monster.PushedX = Math.Floor(monster.X);
                monster.PushedY = Math.Floor(monster.Y);
                monster.HasTarget = false;
                monster.Target = null;
                monster.NextAttackMs = nowMs + monster.AttackIntervalMs;
                monster.Uid = runtime.NextMonsterUid++;

                var packet = new MonsterInPush { MonsterList = new List<MonsterInfo>() };
                packet.MonsterList.Add(new MonsterInfo
                {
                    CfgId = monster.CfgId,
                    Uid = monster.Uid,
                    Name = monster.Name,
                    MaxHp = monster.MaxHp,
                    CurHp = monster.Hp,
                    Location = new Point { X = monster.X, Y = monster.Y },
                    Direction = monster.Direction,
                });
                JEvent.defaultEvent.Post(packet);
            }
        }

        /// <summary>
        /// 减伤，对齐原服 MapObject.Attacked：
        ///   damage -= DMT_FINAL_REDUCE_FIXED
        ///   攻击方是怪：-= DMT_REDUCE_FIXED_MONSTER，-= damage * DMT_REDUCE_PERCENTAGE_MONSTER
        ///   否则：      -= DMT_REDUCE_FIXED_PLAYER， -= damage * DMT_REDUCE_PERCENTAGE_PLAYER
        ///   damage -= damage * DMT_FINAL_REDUCE_PERCENTAGE
        /// </summary>
        private static int ApplyDamageReduction(BattleUnit target, BattleUnit attacker, int power)
        {
            double damage = power;
            damage -= target.DmtFinalReduceFixed;
            if (!attacker.IsPlayer)
            {
                damage -= target.DmtReduceFixedMonster;
                damage -= damage * target.DmtReducePercentageMonster / BaseRate;
            }
            else
            {
                damage -= target.DmtReduceFixedPlayer;
                damage -= damage * target.DmtReducePercentagePlayer / BaseRate;
            }
            damage -= damage * target.DmtFinalReducePercentage / BaseRate;
            if (damage < 1)
                damage = 1;
            return (int)damage;
        }

        /// <summary>斩杀比例，原服 `SpecialEffect.Slay / BaseRate`。</summary>
        private static double ReadSlayRate(CFGItem_Skill skillCfg)
        {
            if (skillCfg == null || skillCfg.SpecialEffect == null)
                return 0;
            try
            {
                return (double)skillCfg.SpecialEffect.Slay / BaseRate;
            }
            catch
            {
                return 0;
            }
        }

        private static int GetPlayerNormalSkill(int job)
        {
            if (job == 2) return 1400;
            if (job == 3) return 2700;
            return 100;
        }

        private static CFGItem_Skill GetSkillCfg(int skillId)
        {
            if (skillId <= 0)
                return null;
            try { return CFG_Skill.GetById(skillId); }
            catch { return null; }
        }

        /// <summary>
        /// 判断目标是否落在技能作用范围内。
        ///
        /// 原服是按格子枚举（getSquareCells / getSectorCells / getLineCells）
        /// 把范围内的怪全部加进 targets；单机侧没有服务端格子表，
        /// 这里用等价的几何判定近似，形状规则和原服一致：
        ///
        ///   HALF_MOON     扇形     方向偏差 &lt;= radius 且距离 &lt;= radius
        ///   FOUR_SIDES    四方向   四向直线（十字）
        ///   LINE          长方形   正前方，偏一格
        ///   SQUARE_SELF   方框     以自己为中心的方形
        ///   SQUARE_AIM    方框     以落点为中心的方形
        /// </summary>
        private static bool InSkillArea(int skillType, double sx, double sy, Direction castDir,
            int radius, double aimX, double aimY, double tx, double ty)
        {
            int dist = GridDistance(sx, sy, tx, ty);
            if (dist < 1)
                return false;

            int dirDiff = DirectionDiff(castDir, DirectionFromPoint(sx, sy, tx, ty));

            switch (skillType)
            {
                case SkillTypeHalfMoon:
                    return dist <= radius && dirDiff <= radius;

                case SkillTypeFourSides:
                    return dist <= radius && (dirDiff % 2) == 0;

                case SkillTypeBreakArmy:
                case SkillTypeAuroraPulse:
                case SkillTypeEarthquakeWave:
                    return dist <= radius && dirDiff <= 1;

                case SkillTypeMagicMissile:
                case SkillTypeStellarBurst:
                case SkillTypeSoulTalisman:
                    return GridDistance(aimX, aimY, tx, ty) <= radius;

                case SkillTypeFreezeTime:
                case SkillTypeTimeSpaceLock:
                case SkillTypeDuel:
                case SkillTypeRepelFoe:
                case SkillTypeMassHeal:
                case SkillTypeDivineStrength:
                case SkillTypeArcaneMastery:
                case SkillTypeBlessingOfCourage:
                case SkillTypeNetherSpell:
                case SkillTypeDivineBattleArmor:
                case SkillTypeTaunt:
                case SkillTypeStomp:
                case SkillTypeBleeding:
                case SkillTypeWreck:
                case SkillTypeApocalypse:
                    return dist <= radius;

                default:
                    // 普攻 / 怪物普攻等：只有目标本身
                    return dist <= Math.Max(1, radius);
            }
        }

        private static int DirectionDiff(Direction a, Direction b)
        {
            if (a == Direction.NONE || b == Direction.NONE)
                return 8;
            int diff = Math.Abs((int)a - (int)b) % 8;
            return diff > 4 ? 8 - diff : diff;
        }

        private static double Distance(BattleUnit a, BattleUnit b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 原服 maputil.Distance 是**切比雪夫距离** max(|dx|,|dy|)，
        /// 不是欧氏距离。技能 MaxDistance / 索敌都用这个。
        /// </summary>
        private static int GridDistance(double ax, double ay, double bx, double by)
        {
            return (int)Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
        }

        /// <summary>
        /// 对齐原服 maputil.DirectionFromPointF：不比较绝对值大小，
        /// 只要 X/Y 有差异就返回对角或轴向方向。
        /// </summary>
        private static Direction DirectionFromPoint(double sx, double sy, double tx, double ty)
        {
            if (sx < tx)
            {
                if (sy < ty) return Direction.UP_RIGHT;
                if (sy > ty) return Direction.DOWN_RIGHT;
                return Direction.RIGHT;
            }
            if (sx > tx)
            {
                if (sy < ty) return Direction.UP_LEFT;
                if (sy > ty) return Direction.DOWN_LEFT;
                return Direction.LEFT;
            }
            if (sy < ty) return Direction.UP;
            if (sy > ty) return Direction.DOWN;
            return Direction.NONE;
        }

        /// <summary>对齐原服 maputil.ShiftDirection：(dir + i + 8) % 8。</summary>
        private static Direction ShiftDirection(Direction dir, int i)
        {
            return (Direction)(((int)dir + i + 8) % 8);
        }

        /// <summary>
        /// 对齐原服 maputil.Move。注意 Y 轴向上：UP 是 Y 增加。
        /// </summary>
        private static void MovePoint(double x, double y, Direction dir, int distance,
            out double outX, out double outY)
        {
            outX = x;
            outY = y;
            switch (dir)
            {
                case Direction.UP:
                    outY += distance;
                    break;
                case Direction.UP_RIGHT:
                    outX += distance;
                    outY += distance;
                    break;
                case Direction.RIGHT:
                    outX += distance;
                    break;
                case Direction.DOWN_RIGHT:
                    outX += distance;
                    outY -= distance;
                    break;
                case Direction.DOWN:
                    outY -= distance;
                    break;
                case Direction.DOWN_LEFT:
                    outX -= distance;
                    outY -= distance;
                    break;
                case Direction.LEFT:
                    outX -= distance;
                    break;
                case Direction.UP_LEFT:
                    outX -= distance;
                    outY += distance;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 计算伤害，对齐原服 Player.delayAttack：
        ///
        ///   basePower = 攻击属性
        ///   power     = basePower + Σ skillCfg.Effect 加成
        ///   power    -= 目标防御
        ///   power    <= 0 时取 1
        ///
        /// 注意原服 Powerfactor **不参与伤害计算**（只用于战力），
        /// 技能伤害完全来自 SkillEffect 表，例如月弧斩 1 级是 [{13:500},{1:10}]，
        /// 即「5% 攻击 + 10 点固定攻击」。
        /// </summary>
        private int ComputeSkillPower(BattleUnit attacker, BattleUnit target, CFGItem_Skill skillCfg)
        {
            double basePower = Math.Max(1, attacker.Attack);
            double power = basePower;

            if (skillCfg != null && skillCfg.SkillEffect != null)
            {
                for (int i = 0; i < skillCfg.SkillEffect.Length; i++)
                {
                    Dictionary<string, int> entries = skillCfg.SkillEffect[i];
                    if (entries == null)
                        continue;
                    foreach (KeyValuePair<string, int> kv in entries)
                    {
                        int attr;
                        if (!int.TryParse(kv.Key, out attr))
                            continue;
                        switch ((Attribute)attr)
                        {
                            case Attribute.PHYSICAL_ATTACK:
                            case Attribute.MAGIC_ATTACK:
                            case Attribute.DMT_FINAL_INCREASE_FIXED:
                                power += kv.Value;
                                break;
                            case Attribute.PHYSICAL_ATTACK_PERCENTAGE:
                            case Attribute.MAGIC_ATTACK_PERCENTAGE:
                            case Attribute.DMT_FINAL_INCREASE_PERCENTAGE:
                            case Attribute.PVE_DAMAGE_PERCENTAGE:
                                power += basePower * kv.Value / BaseRate;
                                break;
                        }
                    }
                }
            }

            power -= Math.Max(0, target.Defense);

            // 原服 Player.delayAttack 末尾：
            //   if len(skillCfg.Effect) == 0 && skillCfg.SType != SkillTypeNormal { power = 0 }
            // 也就是「没有伤害加成且不是普攻」的技能（比如群体治愈术）不造成伤害。
            bool hasEffect = skillCfg != null && skillCfg.SkillEffect != null
                && skillCfg.SkillEffect.Length > 0;
            int sType = skillCfg != null ? skillCfg.Skilltype : 1;
            if (!hasEffect && sType != 1)
                return 0;

            if (power <= 0)
                power = 1;
            return (int)power;
        }

        /// <summary>
        /// 最终伤害（不含随机因素），对齐原服 Player.delayAttack：
        ///
        ///   power = 攻击 + Σ SkillEffect
        ///   damage_percentage_increase = 技能进阶 DamagePer + 暴击 CRITICAL_DAMAGE_PERCENTAGE
        ///   power -= 目标防御
        ///   power += power * damage_percentage_increase / BaseRate
        ///
        /// 注意进阶和暴击是**同一层百分比**，并且都在「减去防御」之后叠加。
        /// 公开出来是为了让测试能在不依赖随机暴击的情况下对拍数值。
        /// </summary>
        public int ComputeFinalPower(BattleUnit attacker, BattleUnit target,
            CFGItem_Skill skillCfg, int advancedLevel, bool critical)
        {
            int power = ComputeSkillPower(attacker, target, skillCfg);

            int damagePercent = 0;
            if (skillCfg != null)
            {
                CFGItem_Skill.UpperlevelSettingItem upper =
                    CFG_Skill.GetUpperlevelSetting(skillCfg.UUID, advancedLevel);
                if (upper != null)
                    damagePercent += upper.DamagePer;
            }
            if (critical)
                damagePercent += attacker.CriticalDamage;

            if (damagePercent != 0)
            {
                // 用 long 中间值，避免 power * damagePercent 溢出 int
                long boosted = (long)power + (long)power * damagePercent / 10000L;
                power = (int)boosted;
            }
            return power;
        }

        /// <summary>
        /// 取技能进阶等级，用来查 UpperlevelSetting。
        /// 玩家读 role.Skills、伙伴读 PartnerInfo.Skills，怪物永远 0（原服怪物没有进阶）。
        /// SkillInfo.CfgId 存的就是技能配置 UUID，所以按 UUID 匹配。
        /// </summary>
        public static int GetSkillAdvancedLevel(BattleUnit attacker, int skillUuid)
        {
            if (attacker == null || skillUuid <= 0)
                return 0;
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return 0;

            if (attacker.IsPlayer)
                return FindSkillAdvanced(role.Skills, skillUuid);

            if (attacker.IsPartner)
            {
                PartnerInfo partner = PartnerService.Instance.FindPartnerByUid(role, attacker.Uid);
                if (partner == null)
                    return 0;
                return FindSkillAdvanced(partner.Skills, skillUuid);
            }
            return 0;
        }

        private static int FindSkillAdvanced(List<SkillInfo> skills, int skillUuid)
        {
            if (skills == null)
                return 0;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillInfo skill = skills[i];
                if (skill != null && skill.CfgId == skillUuid)
                    return skill.Advanced;
            }
            return 0;
        }

        private bool RollCritical(BattleUnit attacker, BattleUnit target)
        {
            int rate = Math.Max(0, attacker.Critical - target.Toughness);
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.BaseValue != null && config.BaseValue.crit != null)
                    rate = (int)(rate * config.BaseValue.crit.k) + config.BaseValue.crit.prevalue;
            }
            catch { }
            if (rate <= 0)
                return false;
            if (rate >= 10000)
                return true;
            return _runtime.Rng.Next(10000) < rate;
        }

        private static long NowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>战斗固定 tick 驱动器，挂在 Unity 主线程。</summary>
    public class BattleTicker : MonoBehaviour
    {
        private static BattleTicker _instance;

        public static void Ensure()
        {
            if (_instance != null)
                return;
            var go = new GameObject("LocalBattleTicker");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<BattleTicker>();
        }

        private void Update()
        {
            if (BattleService.Instance.InBattle)
                BattleService.Instance.TickNow();
            // 上阵宠物的挂机产出（原服每秒 tick）——不在战斗里也要算
            PetService.Instance.TickOfflineAward();
        }
    }
}
