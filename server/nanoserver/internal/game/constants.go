package game

const (
	kCurPlayer = "player"
)

const (
	fieldDesk    = "desk"
	fieldPlayer  = "player"
	fieldMonster = "monster"
)

const BaseRate float64 = 10000.0 // 定义万分比的基数为 10000.0

type PlayerStatus int32 //玩家状态

const (
	PlayerStatusIdle    PlayerStatus = iota //空闲
	PlayerStatusPlaying                     //游戏中
	PlayerStatusDisconnect
	PlayerStatusOffLine
	PlayerStatusRobot //离线机器人
)

type PvpRecordType int32

const (
	PvpRecordTypeJiShi PvpRecordType = iota + 1 //计时赛
	PvpRecordTypeHuiHe                          //回合制
)

const (
	ActiveSkill  = iota + 1 // 主动技能，值为 1
	PassiveSkill            // 被动技能，值为 2
)

// 功能列表
const (
	ModuleYongZheZhengTu            int32 = iota + 1 // 勇者征途主界面按钮
	ModuleLuanDouZhiWang                             // 乱斗之王主界面按钮
	ModuleWuJinShiYan                                // 无尽试炼主界面按钮
	ModuleZhuangBei                                  // 装备主界面按钮
	ModuleJiNeng                                     // 技能主界面按钮
	ModulePaiHangBang                                // 排行榜主界面按钮
	ModuleYongZheZhengTuHePing                       // 勇者征途/和平模式副本按钮
	ModuleYongZheZhengTuZhanZheng                    // 勇者征途/战争模式副本按钮
	ModuleLuanDouZhiWang1VS1                         // 乱斗之王/1VS1模式
	ModuleWuJinShiYanPuTong                          // 无尽试炼/普通模式
	ModuleZhuangBeiQiangHua                          // 装备系统/强化页签
	ModuleZhuangBeiShengXing                         // 装备系统/升星页签
	ModuleZhuangBeiXiLian                            // 装备系统/洗炼页签
	ModuleJiNengShengJi                              // 技能系统/升级页签
	ModuleJiNengJinJie                               // 技能系统/进阶页签
	ModuleJiNengSheZhi                               // 技能系统/设置页签
	ModuleWorldBoss                                  // 世界boss
	ModuleDailyMustDoExpFB                           // 每日必做/经验副本
	ModuleDailyMustDoCoinFB                          // 每日必做/金币副本
	ModuleDailyMustDoStrengthFB                      // 每日必做/强化石副本
	ModuleDailyMustDoBookFB                          // 每日必做/书页副本
	ModuleDailyMustDoStarFB                          // 每日必做/升星石副本
	ModuleDailyMustDoSoulFB                          // 每日必做/魂石副本
	ModuleEquipmentSystemSoulFB                      // 装备系统/魂石页签
	ModuleRewardFunction                             // 悬赏功能
	ModuleTreasureBoxFunction                        // 百宝箱功能
	ModuleDestinyFunction                            // 命格功能
	ModuleLuanDouZhiWangMultiBattle                  // 乱斗之王/多人混战模式
	ModuleTaskFunction                               // 任务功能
	ModuleVoucherLottery                             // 点券大转盘
	ModuleEventCenter                                // 活动中心
	ModulePetSystem                                  // 宠物系统
)

const MaxPartnerNum int32 = 4 // 伙伴数量
const TurnBasedMax int32 = 6  // 回合制上阵数量
