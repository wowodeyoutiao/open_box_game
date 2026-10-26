package constant

import "nanoserver/protocol/protobuf/pb"

type DeskStatus int32

const (
	// 创建桌子
	DeskStatusCreate DeskStatus = iota
	// 游戏
	DeskStatusPlaying
	// 空闲
	DeskStatusIdle
	// 已销毁
	DeskStatusDestory
)

var stringify = [...]string{
	DeskStatusCreate:  "创建",
	DeskStatusPlaying: "游戏中",
	DeskStatusIdle:    "空闲",
	DeskStatusDestory: "已销毁",
}

func (s DeskStatus) String() string {
	return stringify[s]
}

const (
	// token相关
	JwtTokenOK            int    = 200100                      // token有效
	JwtTokenInvalid       int    = -400100                     // 无效的token
	JwtTokenExpired       int    = -400101                     // 过期的token
	JwtTokenFormatErrCode int    = -400102                     // 提交的 token 格式错误
	JwtTokenFormatErrMsg  string = "提交的 token 格式错误"            // 提交的 token 格式错误
	JwtTokenMustValid     string = "token为必填项,请在请求header部分提交!" // 提交的 token 格式错误
)

// 道具时效性类型
const (
	ItemTimelinessShiChang = 1 // 固定时长
	ItemTimelinessQuJian   = 2 // 时间范围
)

// 职业
type MowJob int32

const (
	Warrior          MowJob = iota + 1 // 战士
	Wizard                             // 法师
	Ritualist                          // 祭祀
	PartnerWarrior   = 11              // 伙伴战士
	PartnerWizard    = 12              // 伙伴法师
	PartnerRitualist = 13              // 伙伴祭祀
	Pet              = 21              // 宠物
)

// 对应职业名称
var JobName = map[MowJob]string{
	Warrior:          "战士",
	Wizard:           "法师",
	Ritualist:        "祭祀",
	PartnerWarrior:   "伙伴战士",
	PartnerWizard:    "伙伴法师",
	PartnerRitualist: "伙伴祭祀",
	Pet:              "宠物",
}

// 对应task名称
var TaskName = map[int32]string{
	1: "日常任务",
	2: "周常任务",
	3: "成就任务",
	4: "每日福利",
	5: "日常活跃度奖励",
	6: "周常活跃度奖励",
	7: "主线任务",
	8: "VIP任务",
}

// 地图类型枚举
const (
	// PVEPeacefulMap 代表PVE玩法中的和平模式地图
	PVEPeacefulMap int32 = iota + 1

	// PVEWarMap 代表PVE玩法中的战争模式地图
	PVEWarMap

	// PVPOneOnOneMap 代表PVP玩法中的1对1模式地图
	PVPOneOnOneMap

	// PVPMultiOnMultiMap 代表PVP玩法中的多对多模式地图
	PVPMultiOnMultiMap

	// SoloTowerMap 代表个人爬塔类玩法地图
	SoloTowerMap

	// 世界boss
	WorldBossMap

	// ExperienceFBMap 代表经验副本 boss
	ExperienceFBMap

	// GoldFBMap 代表金币副本 boss
	GoldFBMap

	// EnhanceStoneFBMap 代表强化石副本 波次
	EnhanceStoneFBMap

	// BookPageFBMap 代表书页副本 波次
	BookPageFBMap

	// StarUpStoneFBMap 代表升星石副本 采集
	StarUpStoneFBMap

	// SoulStoneFBMap 代表魂石副本 采集
	SoulStoneFBMap

	// GuidanceFBMap 代表新手引导
	GuidanceFBMap

	// 战役（主线）副本
	BattleMap
	// 首领副本
	BossMap
	// 试炼塔
	TrialTowerMap
	// 遗迹
	RelicMap
	// 工会
	GuildMap
	// 一战到底
	FightEndMap
)

// BuffID 枚举定义了游戏中的各种buff类型。
type BuffID int

// 定义枚举常量，使用iota自动递增。
const (
	BUFF_UNSPECIFIED                BuffID = iota // 未指定的buff，可以作为默认值
	BLEED_5SEC                                    // 每5秒损失1%的血量，持续30秒
	ARMOR_BREAK                                   // 物防和魔防降低10%，持续10秒
	SLOW                                          // 移动速度降低50%，持续5秒
	BLEED_1SEC                                    // 每1秒损失1%的血量，持续10秒
	ARMOR_BREAK_ONLY                              // 物防降低10%，持续10秒
	POISON                                        // 攻击力降低30%，持续5秒
	ATTACK_PERCENT_BOOST                          // 攻击力得到百分比提升
	HEAL                                          // 持续回复一定比例的生命值
	DEFENSE_DOUBLE_BOOST                          // 物理防御和魔法防御得到百分比提升
	ARMOR_BOOST                                   // 物理防御得到提升
	MAGIC_ARMOR_BOOST                             // 魔法防御得到提升
	ATTACK_SPEED_MOVE_SPEED_BOOST                 // 攻击速度和移动速度得到提升
	ATTACK_DEFENSE_BOOST                          // 攻击力和双防都将会得到提升
	STAR_UPGRADE_CHANCE_BOOST                     // 升星的成功几率得到提升
	FREEZE                                        // 冰冻效果
	INJURY                                        // 伤血效果
	ATTACK_PERCENT_BOOST_2          BuffID = 20   // 此状态下，攻击力得到百分比提升（第二个同名buff）
	DEFENSE_DOUBLE_BOOST_2          BuffID = 21   // 物理防御和魔法防御得到百分比提升（第二个同名buff）
	HEAL_PERCENT                    BuffID = 22   // 一次性回复一定百分比的生命
	ATTACK_SPEED_MOVE_SPEED_BOOST_2 BuffID = 23   // 攻速移速都会提升（第二个同名buff）
	GOD_MODE                        BuffID = 24   // 无敌状态，免疫所有伤害和控制技能
)

// 定义道具种类的枚举
const (
	NotInBag                         int32 = iota // 代表不进入包裹的数值类道具
	InBagDisposable                               // 代表进入包裹且可被销毁的道具
	InBagDisposableAndUsable                      // 代表进入包裹且可被销毁和使用的道具
	InBagTimeLimited                              // 代表进入包裹、可被销毁、带有时效的道具
	InBagDisposableUsableTimeLimited              // 代表进入包裹、可被销毁和使用、带有时效的道具
	Equipment                                     // 代表装备类，该类道具可装备、可分解
	Gem                                           // 代表宝石类，该类道具可镶嵌、可合成
	PartnerFragment                               // 代表伙伴碎片，该道具可合成，成为1个整卡伙伴
	ItemPartner                                   // 代表伙伴整卡
	ItemPet                                       // 代表战宠
)

// 定义道具子类型常量
const (
	ItemTypeNone       = 0 // 代表数值类道具
	ItemTypeMaterial   = 1 // 代表材料类道具
	ItemTypeEquipment  = 2 // 代表装备类道具
	ItemTypeGem        = 3 // 代表宝石类道具
	ItemTypeConsumable = 4 // 代表消耗类道具
)

// 道具增加/消耗日志
const (
	ItemAddWeb                = "ItemAdd_WebServer"
	ItemAddGame               = "ItemAdd_GameServer"
	ItemAddPvp                = "ItemAdd_PVP"
	ItemAddTask               = "ItemAdd_Task" // 任务
	ItemAddDrop               = "ItemAdd_Drop" // 掉落
	ItemAddGM                 = "ItemAdd_GM"
	ItemAddDistroy            = "ItemAdd_Distroy"             // 销毁
	ItemAddDisintegrate       = "ItemAdd_Disintegrate"        // 分解
	ItemAddSoloTower          = "ItemAdd_SoloTower"           // 爬塔
	ItemAddCompose            = "ItemAdd_Compose"             // 合成材料
	ItemAddGemstone           = "ItemAdd_Gemstone"            // 宝石卸下
	ItemAddDailymap           = "ItemAdd_Dailymap"            // 每日必做
	ItemAddLevelGift          = "ItemAdd_LevelGift"           // 等级奖励
	ItemAddAFKMonster         = "ItemAdd_AFKMonster"          // 挂机奖励怪物掉落
	ItemAddAFKBox             = "ItemAdd_AFKItem"             // 挂机奖励道具
	ItemAddOpenBox            = "ItemAdd_OpenBox"             // 挂机奖励箱子
	ItemAddPartner            = "ItemAdd_Partner"             // 伙伴
	ItemAddPetAuth            = "ItemAdd_PetAuth"             // 宠物自动给宠物蛋
	ItemAddPetGold            = "ItemAdd_PetGold"             // 宠物技能提供金币
	ItemAddPetExp             = "ItemAdd_PetExp"              // 宠物技能提供经验
	ItemAddPetAFK             = "ItemAdd_PetAFk"              // 宠物离线
	ItemAddGuildWish          = "ItemAdd_GuildWish"           // 自动给祈愿点
	ItemAddGuildMap           = "ItemAdd_GuildMap"            // 公会副本
	ItemAddGuildWishExp       = "ItemAdd_GuildWishExp"        // 祈愿提供经验
	ItemAddGuildShopBuy       = "ItemAdd_GuildShopBuy"        // 行会商店购买
	ItemAddGuildDonate        = "ItemDel_GuildDonate"         // 行会捐献
	ItemAddDropHaiDao         = "ItemAdd_Drop_HaiDao"         // 守卫海岛掉落
	ItemAddDropStarUpStoneFB  = "ItemAdd_Drop_StarUpStoneFB"  // 升星石掉落
	ItemAddDropSoulStoneFBMap = "ItemAdd_Drop_SoulStoneFBMap" // 魂石掉落
	ItemAddPetFull            = "ItemAdd_PetFull"             // 宠物满了

	ItemDelWeb            = "ItemDel_WebServer"
	ItemDelGame           = "ItemDel_GameServer"
	ItemDelChangeRoleName = "ItemDel_ChangeRoleName"
	ItemDelRefine         = "ItemDel_Refine"       // 洗炼
	ItemDelChat           = "ItemDel_Chat"         // 聊天
	ItemDelDistroy        = "ItemDel_Distroy"      // 销毁
	ItemDelExpired        = "ItemDel_Expired"      // 过期
	ItemDelDisintegrate   = "ItemDel_Disintegrate" // 分解
	ItemDelSkillUpgrade   = "ItemDel_SkillUpgrade" // 升级技能
	ItemDelCompose        = "ItemDel_Compose"      // 合成材料
	ItemDelGemstone       = "ItemDel_Gemstone"     // 宝石穿戴
	ItemDelHomeOpenBox    = "ItemDel_HomeOpenBox"  // 宝石穿戴
	ItemDelPetLevel       = "ItemDel_PetSLevel"    // 宠物升级
	ItemDelPetStar        = "ItemDel_PetStar"      // 宠物升星
	ItemDelGuildDonate    = "ItemDel_GuildDonate"  // 行会捐献
	ItemDelGuildShopBuy   = "ItemDel_GuildShopBuy" // 行会商店购买
	ItemDelVipExp         = "ItemDel_VipExp"       // vip经验
	ItemDelGuildWishExp   = "ItemDel_GuildWishExp" // 祈愿提供经验
	ItemDelGuardIsland    = "ItemDel_GuardIsland"  // 挂机守卫海岛
)

// 道具ID ,拓展必须对应item.xlsx ID 字段
const (
	ItemTypeCoin                = 1  // 金币	游戏内的通用货币
	ItemTypeExp                 = 2  // 经验
	ItemTypeGuildContribution   = 3  // 工会贡献
	ItemTypeSoul                = 4  // 战魂	游戏内获得的数值货币，通过PVP模块可获取
	ItemTypeCoupon              = 5  // 点券	游戏内的充值货币，对应点券
	ItemTypeEnergy              = 6  // 体力	游戏内获得的数值货币，对应能量
	ItemTypeVipExp              = 7  // vip经验	游戏内获得的数值货币
	ItemTypeTrialVoucher        = 8  // 试炼凭证	游戏内获得的数值货币
	ItemTypePVPTimes            = 10 // PVP次数
	ItemTypeTreasureNum         = 14 // 开箱次数
	ItemTypeLoudspeaker         = 29 // 喇叭
	ItemTypeGVGTimes            = 71 // GVG次数
	ItemTypePetExp              = 83 // 宠物经验
	ItemTypePetNormal           = 80 // 普通宠物蛋
	ItemTypeBossChallengeTimes  = 90 // Boss挑战次数
	ItemTypeRelicChallengeTimes = 91 // 遗迹挑战次数
	ItemTypeGuildWish           = 93 // 工会祈愿点
	ItemTypeAccelerateScroll    = 99 // 加速卷轴

)

type EquipmentType int

// 定义装备类型的常量，使用iota自动递增。
const (
	Weapon        EquipmentType = iota + 1 // 1: 代表武器
	Clothes                                // 2: 代表衣服
	Helmet                                 // 3: 代表头盔
	Necklace                               // 4: 代表项链
	LeftRing                               // 5: 代表左戒指
	RightRing                              // 6: 代表右戒指
	LeftBracelet                           // 7: 代表左手镯
	RightBracelet                          // 8: 代表右手镯
	Belt                                   // 9: 代表腰带
	Boots                                  // 10: 代表靴子
	MaxEquipSlot
)

const (
	BuffTypeBuff   = 1 // 增益
	BuffTypeDebuff = 2 // 减益
)

const (
	BuffMarkPassive    int32 = 1    // 被动
	BuffTypeChainSkill int32 = 1000 // 关联技能数值
)

// BuffType 表示游戏中的增益或减益效果的枚举类型。
type BuffType int

// 定义BuffType的枚举值
const (
	// 持续性百分比回血
	BuffPercentageHeal BuffType = iota + 1
	// 持续性固定值回血
	BuffFixedHeal
	// 百分比加双防
	BuffPercentageDefenseBoost
	// 百分比加攻击
	BuffPercentageAttackBoost
	// 持续性百分比扣血
	BuffPercentageDamage
	// 持续性固定值扣血
	BuffFixedDamage
	// 百分比减防御
	BuffPercentageDefenseDecrease
	// 百分比增加攻速
	BuffPercentageAttackSpeedIncrease
	// 百分比增加移速
	BuffPercentageMovementSpeedIncrease
	// 百分比降低攻速
	BuffPercentageAttackSpeedDecrease
	// 百分比降低移速
	BuffPercentageMovementSpeedDecrease
	// 百分比减攻击
	BuffPercentageAttackDecrease
	// 百分比增加攻击力和双防
	BuffPercentageAttackBothDefenseIncrease
	// 百分比增加攻速移速
	BuffPercentageAttackMoveSpeedIncrease
	// 一次性百分比回血
	BuffFixedHealOnece
	// 无敌模式
	BuffGodMode

	// 百分比增加装备星级
	BuffPercentEquipStarIncrease = 50
	// 冰冻
	BuffFreeze = 51
	// 以角色的攻击力作为伤血计算基准值，走正常的战斗公式计算伤血效果
	BuffFixedDamage2 = 52
	// 嘲讽
	BuffTaunt = 53
	// 百分比最终减伤
	BuffDmtFinalReducePercentage = 54
	// 受控时间降低
	BuffTauntTimeReduce = 55
	// 反伤
	BuffReflectDamage = 56
	// 降低双防降低回血速度
	BuffPercentageDefenseHealthDecrease = 57
	// 代表替身宝宝技能
	BuffDecoy = 58
	// 代表护盾效果
	BuffShield = 59
	// 代表普攻带有的击退效果
	BuffKnockback = 60
	// 代表法师魔法酸雨技能
	BuffAcidRain = 61
	// 代表法师吸血盾
	BuffLifeLeechShield = 62
	// 挫折意志
	BuffFrustratedWill = 63
	// 代表技能群体净化术的效果
	BuffMassPurification = 64
	// 代表技能对怪伤害的百分比提升效果
	BuffIncreaseFixedMonster = 65
	// 代表宠物天赋中增加武器百分比攻击力的效果
	BuffPetWeaponAttackPercentageIncrease = 66
	// 百分比降低目标的暴击值
	BuffPercentageCritChanceDecrease = 67
	// 代表不受到任何负面效果的影响
	BuffNoNegativeEffect = 68
	// 百分比降低命中值
	BuffPercentageAccuracyDecrease = 69
	// 百分比提升暴击伤害
	BuffPercentageCritDamageIncrease = 70
	// 百分比提升命中值
	BuffPercentageAccuracyIncrease = 71
	// 百分比提升生命上限
	BuffPercentageMaxHealthIncrease = 72
	// 百分比提升PK伤害
	BuffPercentagePKDamageIncrease = 73
	// 74：代表攻击者百分比降低攻击、防御和暴击率
	BuffPercentageAttackDefenseCritDecrease = 74
	// 通用减益效果
	BuffCommonDebuff = 75
	// 通用增益效果
	BuffCommonBuff = 76
	// 77：代表受到伤害不会超出一定比例
	BuffDamageLimit = 77
	// 78：代表一场战斗中有一次复活的机会
	BuffReviveOnce = 78
	// 79：代表免疫效果
	BuffImmune = 79
)

type BuffStatus int32

const (
	BuffStatusStart BuffType = iota + 1 // 开始
	BuffStatusStop                      // 结束
)

const (
	SkillEffectTypeAll     int32 = iota + 1 // 全部
	SkillEffectTypePlayer                   // 对人生效
	SkillEffectTypeMonster                  // 对怪生效
)

// 技能产生buff影响范围类型
const (
	// 自身
	SkillEffectnumSlef int32 = iota
	// 友方全体
	SkillEffectnumAlliance
	// 己方血量最低
	SkillEffectnumAllianceLowHp
	// 敌方一列(2个)
	SkillEffectnumEnemyColumn
	// 敌方一排(3个)
	SkillEffectnumEnemyRow
	// 敌方范围
	SkillEffectnumEnemyRange
	// 敌方单体
	SkillEffectnumEnemySingle
	// 敌方全体
	SkillEffectnumEnemyAll
)

// 技能类型
const (
	//===战士技能===
	SkillTypeNormalAttackWarrior int32 = iota + 1 // 战士普通攻击
	SkillTypeHalfMoon                             // 月弧斩
	SkillTypeFourSides                            // 四方斩
	SkillTypeBreakArmy                            // 破军斩
	SkillTypeIronBlood                            // 铁血狂潮
	SkillTypeWarGod                               // 战神荣耀
	SkillTypeSwiftStrike                          // 迅影追击 朝着当前的方向突进6米，并对路径上的敌对目标造成（110%攻击力+50点攻击力）的伤害，移动过程中为无敌状态。
	SkillTypeDuel                                 // 一决高下 对周围3米范围内的敌对目标发出嘲讽，使得目标有3%的几率处于嘲讽状态。（不能转换攻击目标和移动，普攻和技能均可使用）。
	SkillTypeDiscouragedWill                      // 挫折意志 释放后降低10%控制类技能的控制时间，并使得所受控制类技能的伤害降低9%，状态持续3秒。
	SkillTypeCounterStorm                         // 反击风暴 10%的几率将自身所受伤害的2%返还给攻击者，状态持续3秒
	SkillTypeFrenzyTouch                          // 狂暴之触
	SkillTypePlagueBlade                          // 瘟疫之刃
	SkillTypeStunSmash                            // 眩晕猛击

	//===法师技能===
	SkillTypeNormalAttackWizard // 法师普通攻击
	SkillTypeMagicMissile       // 魔法飞弹
	SkillTypeStellarBurst       // 星爆术
	SkillTypeAuroraPulse        // 极光脉冲
	SkillTypeTimeControl        // 时间操控
	SkillTypeMagicAmplification // 魔法增幅
	SkillTypeRepelFoe           // 拒敌千里 将身边2*2范围内的目标向外推出4米，并对目标造成（110%攻击力+40点攻击力）的伤害。
	SkillTypeDecoy              // 替身宝宝 在当前位置释放一个自身幻影（幻影无法移动攻击只能原地站立自身血量为本身血量的10%），本身则向当前朝向瞬移6米。
	SkillTypeInstantaneousMove  // 瞬息移动
	SkillTypeBloodthirstyCurse  // 嗜血之咒
	SkillTypeMagicBarrier       // 魔法结界
	SkillTypeInvincible         // 无懈可击
	SkillTypeManaExpel          // 魔力驱逐

	//===祭祀技能===
	SkillTypeNormalAttackRitualist // 祭祀普通攻击
	SkillTypeSoulTalisman          // 灵魂符咒
	SkillTypeEarthquakeWave        // 震地波
	SkillTypeNetherSpell           // 幽冥术
	SkillTypeDivineBattleArmor     // 神圣战甲术
	SkillTypeVenomGrimoire         // 毒蛊术
	SkillTypeToxicSand             // 毒砂术
	SkillTypeDivineStrength        // 神力精通
	SkillTypeArcaneMastery         // 魔力精通
	SkillTypeMassHeal              // 群体治愈术
	SkillTypeMassPurification      // 群体净化术
	SkillTypeLastGasp              // 回光返照
	SkillTypeBlessingOfCourage     // 勇气祝福

	// == boss 设计的技能 ==
	// 护盾
	SkillTypeShield = 51
	// 践踏
	SkillTypeStomp = 52
	// 吸取
	SkillTypeAbsorb = 53
	// 金身
	SkillTypeGoldenBody = 54
	// 重击
	SkillTypeHeavyBlow = 55
	// 分身
	SkillTypeDecoyBoss = 56
	// 斩杀
	SkillTypeExecute = 57
	// 嘲讽
	SkillTypeTaunt = 58
	// 怒气
	SkillTypeRage = 59
	// 卸甲
	SkillTypeDisarm = 60
	// 毁伤
	SkillTypeWreck = 61
	// 恐惧
	SkillTypeFear = 62
	// 中毒
	SkillTypePoison = 63
	// 致盲
	SkillTypeBlind = 64
	// 灭世
	SkillTypeApocalypse = 65
	// 出血
	SkillTypeBleeding = 66
	// 破兵
	SkillTypeBreakWeapon = 67
	// 回血
	SkillTypeHeal = 68
	// 群防
	SkillTypeGroupDefense = 69
	// 重生
	SkillTypeReborn = 70
	// 群体输出
	SkillTypeMassAttack = 71
	// 群体净化
	SkillTypeMassPurificationBoss = 72
	// 群体爆发
	SkillTypeMassBurst = 73
	// 回生术
	SkillTypeRebirth = 74
	// 专注
	SkillTypeConcentration = 75
	// 免疫
	SkillTypeImmune = 76
	// 强身
	SkillTypeBodyguard = 77
	// 重甲
	SkillTypeHeavyArmor = 78
	// 重伤
	SkillTypeHeavyWound = 79
	// 爆发
	SkillTypeBurstBoss = 80
	// 封锁
	SkillTypeSeal = 81
	// 战意
	SkillTypeBattleWill = 82
	// 护甲
	SkillTypeArmor = 83
	// 反击大事
	SkillTypeCounterAttack = 84
	// 自愈
	SkillTypeSelfHeal = 85

	// ===特殊技能===
	SkillTypeFreezeTime         = 500 // 千里冰封
	SkillTypeSwordHeavenBreaker = 501 // 剑开天门
	SkillTypeTimeSpaceLock      = 600 // 时空凝结
	SkillTypeMagicAcidRain      = 601 // 魔法酸雨

	//===被动技能===
	SkillTypeAgile           = 27000 // 【灵活】
	SkillTypeResilient       = 28000 // 【坚韧】
	SkillTypeAbundant        = 29000 // 【厚泽】
	SkillTypePrecise         = 30000 // 【精准】
	SkillTypeBurst           = 31000 // 【爆发】
	SkillTypeValiant         = 32000 // 【勇猛】
	SkillTypeIronWallI       = 36000 // 【铁壁Ⅰ】
	SkillTypeSpiritBarrierI  = 37000 // 【灵障Ⅰ】
	SkillTypeIronWallII      = 38000 // 【铁壁Ⅱ】
	SkillTypeSpiritBarrierII = 39000 // 【灵障Ⅱ】
	SkillTypeSlaughterI      = 40000 // 【屠戮Ⅰ】
	SkillTypeEnemyBreakerI   = 41000 // 【破敌Ⅰ】
	SkillTypeSlaughterII     = 42000 // 【屠戮Ⅱ】
	SkillTypeEnemyBreakerII  = 43000 // 【破敌Ⅱ】
	SkillTypeLifeSourceI     = 44000 // 【命源Ⅰ】
	SkillTypeHealingSpiritI  = 45000 // 【愈灵Ⅰ】
	SkillTypeLifeSourceII    = 46000 // 【命源Ⅱ】
	SkillTypeHealingSpiritII = 47000 // 【愈灵Ⅱ】

	//===怪物技能===
	SkillTypeNormalAttackMonster = 9999  // 怪物普攻
	WorldBossSkill1              = 10000 // 世界boss技能

	PartnerWarriorNormalSkillID   = 20000
	PartnerWizardNormalSkillID    = 20001
	PartnerRitualistNormalSkillID = 20002
)

const (
	SkillType               int32 = iota //
	SkillTypeNormal                      // 普攻
	SkillTypeCareer                      // 技能
	SkillTypeCareerSpecial               // 特殊技能技能
	SkillTypeCareerSpecial4              // 特殊技能技能4
	SkillTypeCareerSpecial5              // 特殊技能技能5
)

const (
	SkillSlotType int32 = iota //
)

// 排行榜
const (
	RankTypeLevel           = 1  // 等级榜
	RankTypeFight           = 2  // 战力榜
	RankTypeSoul            = 3  // PVP胜场数量
	RankTypeTrial           = 4  // 试炼榜
	RankTypePvp             = 6  // 每日9点的计算(当天战魂)
	RankTypeSeason          = 7  // 赛季排行榜
	RankTypeWorldBoss       = 20 // 世界boss
	RankTypeWorldBossKf     = 21 // 世界boss的跨服榜
	RankTypeGightEnd        = 22 // 一战到底总榜
	RankTypeGightEndlayers  = 23 // 一战到底每一层榜单
	RankTypeTurnBased       = 24 // 回合制排行榜
	RankTypeSoulWeekly      = 25 // pvp胜场周榜
	RankTypeTurnBasedWeekly = 26 // 回合制周榜
	RankTypeFightWeekly     = 27 // 战力周榜
)

// 排行榜对应的中文名称
var RankTypeName = map[int]string{
	RankTypeLevel:           "等级日榜",
	RankTypeFight:           "战力日榜",
	RankTypeSoul:            "即时赛日榜",
	RankTypeTrial:           "试炼日榜",
	RankTypePvp:             "战魂日榜",
	RankTypeSeason:          "赛季排行榜",
	RankTypeWorldBoss:       "世界boss日榜",
	RankTypeWorldBossKf:     "世界boss的跨服榜",
	RankTypeGightEndlayers:  "一战到底每一层榜单",
	RankTypeGightEnd:        "一战到底总榜",
	RankTypeTurnBased:       "回合制日榜",
	RankTypeSoulWeekly:      "pvp胜场周榜",
	RankTypeTurnBasedWeekly: "回合制周榜",
	RankTypeFightWeekly:     "战力周榜",
}

// 需要分服务器的排行榜
var RankTypeServer = map[int64]bool{
	RankTypeFight: true, // 战力
	RankTypeSoul:  true, // 即时赛榜
}

// 需要查询服务器名字的排行榜
var RankTypeNameServer = map[int64]bool{
	RankTypeWorldBoss: true, // 战力
}

// 需要比较数据的排行榜
var RankTypeServerCompare = map[int64]bool{
	RankTypeLevel:          true, //
	RankTypeFight:          true, //
	RankTypeTrial:          true, //
	RankTypeWorldBoss:      true,
	RankTypeGightEndlayers: true,
}

// 日榜对应的周榜Id
var RankTypeWeekly = map[int64]int64{
	RankTypeSoul:      RankTypeSoulWeekly,
	RankTypeTurnBased: RankTypeTurnBasedWeekly,
	RankTypeFight:     RankTypeFightWeekly,
}

const (
	NeedMoney = 100 // 定义商城人民币支付的Id
	NeedAd    = 101 // 定义商城广告支付的Id
)

// 定义商城类型的枚举
const (
	ShopTypeUnknown               int32 = iota
	ShopTypeRecharge                    // 充值商城
	ShopTypeCoupon                      // 点券商城
	ShopTypeGold                        // 金币商城
	ShopTypeSoul                        // 战魂商城
	ShopTypeTrial                       // 试炼商城
	ShopTypeSignin30Days                // 30日累计签到的人民币购买
	ShopTypeActivityRecommendGift       // 活动中心的推荐礼包
	ShopTypeFirstCharge                 // 首冲三日活动
	ShopTypeSubscription                // 周、月、季卡
	ShopTypeBoost                       // 购买体力
	ShopTypeVIPGift                     // VIP礼包
	ShopTypeOfferReward                 // 悬赏
	ShopTypePVP                         // pvp购买次数
	ShopTypeRelic                       // 玩法的挑战次数购买
	ShopTypeTrialReward                 // 试炼基金
	ShopTypeGuild                       // 工会商店
	shopTypePartner                     // 伙伴商店
	shopTypePet                         // 宠物商店
	ShopTypeAFK                         // 挂机奖励
	ShopTypeDailyGift                   // 每日礼包
	ShopTypeSoulStoneGift               // 魂石礼包（宝石礼包）
	ShopTypeGrowthFund                  // 成长基金
	ShopTypeLimitedTimeGift             // 限时礼包
	ShopTypeElementGift                 // 24: 元素礼包
	ShopTypeSevenDaysFun                // 25: 七日活动专用
	ShopTypeGuardIsland                 // 26: 守卫海岛专用倍率

	ShopTypeAll
)

// 定义商城类型的枚举
const (
	ShopGoodsIdSpecial = 12001
)

// 定义活动类型的枚举
const (
	ActivityTypeUnknown          int32 = iota // 0: 未知
	ActivityTypeContinuity                    // 1: 连续登录
	ActivityTypeCumulative                    // 2: 累计登录
	ActivityTypeGrade                         // 3: 等级奖励
	ActivityTypeRecommend                     // 4: 推荐礼包
	ActivityTypeCharge                        // 5: 首充多日礼包
	ActivityTypeTrunTable                     // 6: 点券大转盘
	ActivityTypeBounty                        // 7: 限时悬赏活动
	ActivityTypeSingleCharge                  // 8: 单笔充值返利
	ActivityTypeCumulativeCharge              // 9: 累计充值返利
	ActivityTypeTrial                         // 10: 试炼塔玩法中的试炼基金
	ActivityTypePartnerGift                   // 11: 活动中心的伙伴礼包
	ActivityTypePetGift                       // 12: 活动中心的宠物礼包
	ActivityTypeDailyGift                     // 13: 每日礼包
	ActivityTypeSoulStoneGift                 // 14: 魂石礼包（宝石礼包）
	ActivityTypeGrowthFund                    // 15: 成长基金
	ActivityTypeLimitedTimeGift               // 16: 限时礼包
	ActivityTypeElementGift                   // 17: 元素礼包
	ActivityTypeSevenDaysFun                  // 18: 开服七天乐
	ActivityTypeSevenDaysFunGift              // 19: 开服七天乐礼包

)

var ShopMap map[int32]pb.TargetType

func init() {
	ShopMap = make(map[int32]pb.TargetType)
	ShopMap[ShopTypeCoupon] = pb.TargetType_TOTAL_SHOP_PURCHASES_WITH_COUPONS
	ShopMap[ShopTypeGold] = pb.TargetType_TOTAL_SHOP_PURCHASES_WITH_GOLD
	ShopMap[ShopTypeSoul] = pb.TargetType_TOTAL_SHOP_PURCHASES_WITH_SOULS
	ShopMap[ShopTypeTrial] = pb.TargetType_TOTAL_SHOP_PURCHASES_WITH_TRIAL_TOKENS
	ShopMap[ShopTypeBoost] = pb.TargetType_TOTAL_BOOST_PURCHASES
}

var ItemQuality = [...]string{
	"",
	"#ffffff",
	"#6fb469",
	"#6d99d0",
	"#a36ef6",
	"#e67be2",
	"#d29435",
	"#d55756",
}

// 定义
const (
	EquipMasterCategoryUnknown        int32 = iota
	EquipMasterCategoryEnhance              // 强化
	EquipMasterCategoryStars                // 升星
	EquipMasterCategoryGemStone             // 魂石大师
	EquipMasterCategoryGemStoneNature       // 自然加成
)

const (
	GameOverType          int32 = iota
	GameOverTypeTime            // 时间到
	GameOverTypePDead           // 死亡复活超时
	GameOverTypeMDead           // 怪死光
	GameOverTypeDeadFaild       // 死亡失败
)

// LampType 表示玩家事件的类型。
type LampType int

const (
	// 1. 排行榜榜首变化
	LampTopRankChanged LampType = iota + 1 // 玩家暂列XX排行榜榜首，可喜可贺！

	// 2. 乱斗之王玩法中连续获得10场胜利
	LampUnstoppableInBrawl // 玩家在乱斗之王玩法中已经暴走，无人可挡！

	// 3. 试炼塔玩法中通过10的整数倍层数
	LampTrialTowerLevelUp // 玩家通过了试炼塔第XX层，太厉害了！

	// 4. 战役玩法中通过每一章的最后一关
	LampCampaignChapterCompleted // 玩家通过了战役第X章，一往无前！

	// 5. 首领玩法中通过5的整数倍关数
	LampBossChapterCompleted // 玩家通过了首领第X关，收获了大量奖励！

	// 6. 遗迹玩法中领取每一章的30星宝箱奖励
	LampRelicChapterReward // 玩家领取了遗迹星级宝箱获取珍稀奖励，令人羡慕！

	// 7. 购买试炼塔的试炼基金
	LampTrialFundPurchased // 玩家购买了试炼基金，一次性领取了海量大奖！

	// 8. 伙伴召唤中抽取到橙色或红色品质的整卡英雄
	LampHeroSummon // 玩家运气爆棚，召唤出了（伙伴名称）！

	// 9. 宠物蛋抽取得到橙色品质以上的尊品宠物
	LampPetSummon // 玩家手气奇佳，召唤出了（宠物名称）！

	// 10. 宠物蛋合成得到橙色品质以上的尊品宠物
	LampPetComposeSummon // 玩家手气奇佳，合成出了（宠物名称）！

	// 11. 玩家成功进行了首充并获得了首充奖励
	LampFirstRecharge // 玩家获得了三日首充大礼，赢在起点！

	// 12. 玩家成功购买了特权卡
	LampPrivilegeCardPurchased // 玩家购买了X卡，获得了超值豪礼！

	// 13. 世界boss开启挑战
	LampWorldBossChallenge // 世界boss挑战开启，海量奖励等你来拿！

	// 14. 装备槽位升星到指定星级
	LampEquipmentStarUpgrade // 玩家成功打造X星槽位，傲视群雄！

	// 15. 合成出了8级以上且带有自然属性的宝石
	LampGemSynthesis // 玩家幸运的合成出了X级X魂石（X），可喜可贺！

	// 16. 行会会长上线
	LampGuildMasterLogin // 会长XXX已上线！

	// 17. 榜单排名第一的玩家上线
	LampTopRankOnline // XX榜第一名的玩家（角色名称）上线了，大家快来围观！
)

// 登录方式
const (
	PlatformGuest  = "guest"
	PlatformUnity  = "unity"
	PlatformWechat = "wechat"
	PlatformMobile = "mobile"
)

// 首页怪物
const (
	MaintriggerType int32 = iota //
	MaintriggerNormal
	MaintriggerMonster
)

// 抽奖类型
const (
	LotteryType int32 = iota //
	LotteryTypeBronze
	LotteryTypeSilver
	LotteryTypeGold
	LotteryTypePlatinum
	LotteryMax
)

// 中奖类型
const (
	LotteryAwardType int32 = iota //
	LotteryAwardFragment
	LotteryAwardWhole
	LotteryAwardMax
)

// 中奖模块
const (
	LotteryModuleType int32 = iota //
	LotteryModulePartner
	LotteryModulePet
	LotteryModuleMax
)

// 邮件内容
const (
	EmailContentSystem  = "系统邮件"
	EmailContentBagFull = "包裹已满"
	EmailContentGuidMap = "行会首领击杀奖励"
)

// Docx 枚举定义了游戏中不同类型的文档。用于活动
type Docx int

const (
	DocxActivity Docx = iota + 1 // 活动
)

type PayPlatformType string

const (
	PayPlatformTypeUnknown       PayPlatformType = "unknown"
	PayPlatformTypeWeChatAndroid PayPlatformType = "wc_android"
	PayPlatformTypeWeChatIOS     PayPlatformType = "wc_ios"
)

func (p PayPlatformType) String() string {
	return string(p)
}

// 修改邮件的计数器，操作为设置，增加，减少
type MailCounterOPType int

const (
	MailCounterOPTypeSet MailCounterOPType = iota + 1 // 设置
	MailCounterOPTypeAdd                              // 增加
	MailCounterOPTypeSub                              // 减少
)

const (
	EnvDev  = "dev"
	EnvTest = "test"
	EnvProd = "prod"
	EnvStag = "stag"
)

// GuardIsLandType 守卫海岛的类型*(挂机副本)
type GuardIsLandType int8

const (
	GuardIsLandTypeFree     GuardIsLandType = iota + 1 // 免费挂机
	GuardIsLandTypeAd                                  // 广告挂机
	GuardIsLandTypeItem                                // 道具挂机
	GuardIsLandTypeRecharge                            // 充值挂机
)
