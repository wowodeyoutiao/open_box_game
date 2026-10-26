package game

import (
	"encoding/json"
	"errors"
	"fmt"
	"math"
	"math/rand"
	"regexp"
	"sort"
	"strconv"
	"strings"
	"sync"
	"time"

	"nanoserver/db"
	"nanoserver/db/model"
	"nanoserver/global"
	shushumain "nanoserver/internal/shushu"
	"nanoserver/internal/web/api/provider"
	"nanoserver/internal/web/provide"
	"nanoserver/protocol"

	"nanoserver/pkg/async"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/errutil"
	"nanoserver/pkg/maputil"
	"nanoserver/pkg/sensitive"
	"nanoserver/pkg/snowflake"
	"nanoserver/protocol/protobuf/pb"

	"github.com/lonng/nano/serialize/protobuf"
	"github.com/lonng/nano/session"
	log "github.com/sirupsen/logrus"
	"github.com/spf13/viper"
)

type (
	PlayerSkill struct {
		CfgId         int32     `json:"cfgid"`
		AdvancedLevel int32     `json:"advanced_level"`
		Cooldown      time.Time `json:"-"`
		Distance      int32     `json:"-"`
	}

	PVPInfo struct {
		*PVPDuel
		*PVPMelee
		dailySoul   int // 每天pvp累计获得的战魂
		sessionSoul int // 每赛季pvp累计获得的战魂

		pvpWinStreakTimes int // 所有模式连胜次数

		pvpRecorder []*model.PvpRecord // PVP记录
	}

	PVPDuel struct {
		pvpScore           int32 // pvp分数
		dailyMatchTimes    int32
		dailyBuyNum        int32
		losingStreakCount  int32 // 1v1连胜
		winningStreakCount int32 // 1v1连败
	}

	PVPMelee struct {
		gvgScore        int32 // gvg分数
		dailyMatchTimes int32
		dailyBuyNum     int32
	}

	TurnBased struct {
		turnBasedSlot [TurnBasedMax]int64
	}

	Slot struct {
		ItemId   int64                                            `json:"id"`       // 装备的唯一标识符
		Stars    int32                                            `json:"stars"`    // 装备的星级
		Enhance  int32                                            `json:"enhance"`  // 装备的强化等级
		Gemstone [pb.GemstoneSlot_MaxGemstoneSlot]pb.GemstoneSlot `json:"gemstone"` // 装备的宝石
		Quality  int32                                            `json:"quality"`  // 装备的品质
	}

	// 最后一次洗练的属性
	LastEquipRefine struct {
		itemId  int64
		attr    pb.Attribute
		random  bool
		newStat pb.Stats
	}

	// 无尽试炼
	SoloTowerInfo struct {
		Layers     int32
		RewardTime time.Time
	}

	// 世界boss
	WorldBossInfo struct {
		dailyWorldBossTimes  int32 // 当天已经挑战次数
		totalWorldBossDamage int64 // 每天总伤害
	}
	// 宝石的阈值
	StoneThreshold struct {
		Level int32 // 宝石等级
		Num   int32 // 数目
	}

	DailyMap struct {
		RoomType      int32 `json:"room_type"`
		ChallengeTime int32 `json:"challenge_time"`
	}

	DailyMapReward struct {
		roomType int32
		reward   []global.IdNum
	}

	AutoDisassembly struct {
		ADOpen          bool  `json:"open"`
		ADLevelSwitch   bool  `json:"level_switch"`
		ADLevel         int32 `json:"level"`
		ADQualitySwitch bool  `json:"quality_switch"`
		ADQuality       int32 `json:"quality"`
		ADHighCombat    bool  `json:"high_combat"`
	}

	PartnerSlot struct {
		PartnerUid int64
		SlotId     int32                           // 伙伴槽位ID
		Equips     [pb.EquipSlot_MaxEquipSlot]Slot // 穿戴装备列表
	}
	BoxInfo struct {
		NowLevel            int32
		NextBoxRecoveryTime int64
		BoxRecoveryTime     time.Time // 最后一次宝箱刷新时间
		BeginAt             int64
		IsAccelerate        int32
		AccelerateNum       int32
		CostGold            int32
		TimeCost            int32
		NowOpenRes          []pb.ItemInfo
		// NowOpenLevel        int32
		// Job                 int32
	}
	PetSlotInfo struct {
		PetUid   int64 `json:"petUid"`   // 宠物ID
		PetKind  int32 `json:"petKind"`  // 宠物配置ID
		PetCfgId int32 `json:"petcfgid"` // 宠物配置ID
		Level    int32 `json:"level"`    // 宠物的等级
		Star     int32 `json:"star"`     // 宠物的星级
		Enhance  int32 `json:"enhance"`  // 宠物的强化等级
		Quality  int32 `json:"quality"`  // 装备的品质
		Exp      int32 `json:"exp"`      // 宠物的经验
		ExpStar  int32 `json:"expStar"`  // 宠物的经验
		ItemId   int32 `json:"itemId"`   // 宠物item的id
	}

	InstanceData struct {
		// 战役副本
		BattleMap struct {
			BattleId    int32
			RewardStage int32
		}
		// 世界boss
		BossMap struct {
			BattleId      int32
			ChallengeTime int32
		}
		// 试炼塔
		TrialTowerMap struct {
			BattleId int32
		}
		// 遗迹
		RelicMap struct {
			BattleId      int32
			ChallengeTime int32
			Evaluation    []global.KeyValue `json:"evaluation"`
			ChapterReward []global.KeyValue `json:"chapter_reward"` // key 章节id value 星数
		}
		// 一战到底
		FightEndMap struct {
			BattleId   int32
			Evaluation []global.FightEndInfo `json:"evaluation"` // key 对手UID value 通关时间
			RefreshAt  time.Time
		}
		// 全部类型副本挑战记录
		ChallengeRecord struct {
			Records map[int32]int32 `json:"records"` // key 副本类型 value 挑战次数
		}
	}
)

type Player struct {
	lock sync.Mutex

	session *session.Session
	logger  *log.Entry // 日志

	*MapObject
	*PVPInfo
	*SoloTowerInfo
	*WorldBossInfo
	*InstanceData
	*TurnBased
	// 玩家数据
	head         string           // 头像地址
	ip           string           // ip地址
	sex          int              // 性别
	tokens       map[int32]int64  // 代币
	tokenDeletes map[int32]int64  // 代币消耗
	firstUse     map[string]int64 // 代币消耗
	exp          int64            // 经验池
	energy       int64            // 体力	进入副本消耗
	vip          int32            // vip等级
	vipExp       int64            // vip经验
	recharge     int64            // 充值总数

	curState    PlayerStatus
	accountName string
	lastLoginAt time.Time
	combat      int32                           // 战斗力
	fateLevel   int32                           // 命格等级
	fateExp     int64                           // 命格经验
	equips      [pb.EquipSlot_MaxEquipSlot]Slot // 穿戴装备列表

	bag        *pb.Bag                            // 包裹
	skillsSlot [pb.SkillsSlot_MaxSkillsSlot]int32 // 穿戴技能列表
	tasks      *TaskMgr                           // 任务

	autoBuyInstanceTime bool      // 副本时间自动续费
	expirationTime      time.Time // 房间内剩余时间
	roomId              int32     // 最后房间ID

	energyRecoveryTime     time.Time // 最后一次体力恢复时间
	nextEnergyRecoveryTime int       // 下一次恢复体力时间（秒)
	disconnectTime         time.Time // 断线时间

	// petNormal           int32     //普通宠物次数
	petRecoveryTime     time.Time // 最后一次宠物蛋恢复时间
	nextPetRecoveryTime int       // 下一次恢复宠物蛋时间（秒)

	petGold                 int32     // 普通宠物送的金币
	petRecoveryTimeGold     time.Time // 最后一次宠物送的金币恢复时间
	nextPetRecoveryTimeGold int       // 下一次恢复宠物送的金币时间（秒）

	petExp                 int32     // 普通宠物送的经验
	petRecoveryTimeExp     time.Time // 最后一次宠物送的经验恢复时间
	nextPetRecoveryTimeExp int       // 下一次恢复宠物送的经验时间（秒)

	dayOnlineTime    int64 // 当日在线时长
	totalOnlineTime  int64 // 累计在线时长
	vitalityTime     int64 // 活力满满时间
	wellenOnlineTime int64 // 本次在线时长

	nowDay    int   // 在线第几天
	CreateDay int32 // 出生第几天
	// vitalityRecoveryTime int   //下一次给奖励时间（秒)

	privilege       []protocol.PrivilegeDetailTCP // 特权卡
	lastEquipRefine LastEquipRefine
	bCreate         bool // 新角色第一次登录gameserver
	bDirty          bool

	dailyMaps []*DailyMap
	*DailyMapReward
	*AutoDisassembly // 自动分解

	partnerSlots [MaxPartnerNum]*PartnerSlot          // 伙伴槽位
	partners     map[int64]*Partner                   // 伙伴列表
	box          BoxInfo                              // 宝箱信息
	pet          [pb.PetsSlot_MaxPetsSlot]PetSlotInfo // 宠物列表
	petEverHave  []int32                              // 曾经已拥有的宠物列表
	// partnerEverHave []int32                              //曾经已拥有的伙伴列表
	totalCombat int32 // 总战斗力
	guildId     int64 // 工会ID
	offlineTime int64 // 离线时间
	// gold         int64                                // 金币
	// diamond      int64                                // 点券
	guildContribution    int64     // 工会贡献
	guildWish            int64     // 工会祈愿
	wishRecoveryTime     time.Time // 最后一次工会祈愿恢复时间
	nextWishRecoveryTime int       // 下一次恢复工会祈愿时间（秒）)
	afkExpirationAt      time.Time // 自动挂机特权卡过期时间
	afkDailyTime         int64     // 每日自动挂机时
	AfkMultiplierID      int64     // 自动挂机倍率的id

	activityMgr *ActivityMgr
	Guidance    []int32 // 引导任务
	mailCounter int     // 用户当前邮件数量
}

func newPlayer(s *session.Session, uid int64, name, head, ip string) *Player {
	p := &Player{
		session: s,
		// uid:     uid,
		head: head,
		ip:   ip,

		logger: log.WithField(fieldPlayer, uid),

		curState:      PlayerStatusIdle,
		bag:           &pb.Bag{ItemList: []*pb.ItemInfo{}},
		equips:        [pb.EquipSlot_MaxEquipSlot]Slot{},
		PVPInfo:       &PVPInfo{PVPDuel: &PVPDuel{}, PVPMelee: &PVPMelee{}},
		SoloTowerInfo: &SoloTowerInfo{},

		MapObject: NewMapObject(ObjectTypePlayer, uid),

		expirationTime:      time.Now(),
		autoBuyInstanceTime: false,
		privilege:           []protocol.PrivilegeDetailTCP{},
		WorldBossInfo:       &WorldBossInfo{},
		dailyMaps:           []*DailyMap{},
		DailyMapReward:      &DailyMapReward{},
		AutoDisassembly:     &AutoDisassembly{},
		partnerSlots:        [MaxPartnerNum]*PartnerSlot{},
		partners:            map[int64]*Partner{},
		tokens:              map[int32]int64{},
		tokenDeletes:        map[int32]int64{},
		InstanceData:        &InstanceData{},
		activityMgr:         NewActivityManager(),
		TurnBased:           &TurnBased{},
		pet:                 [pb.PetsSlot_MaxPetsSlot]PetSlotInfo{},
	}

	for i := 0; i < int(MaxPartnerNum); i++ {
		sid_ := i + 1
		p.partnerSlots[i] = &PartnerSlot{SlotId: int32(sid_)}
		for j := 0; j < int(pb.EquipSlot_MaxEquipSlot); j++ {
			p.partnerSlots[i].Equips[j] = Slot{}
		}
	}

	p.InstanceData.FightEndMap.Evaluation = []global.FightEndInfo{}
	beginId := int32(1900001)
	for i := 0; i < 12; i++ {
		p.InstanceData.FightEndMap.Evaluation = append(p.InstanceData.FightEndMap.Evaluation, global.FightEndInfo{Uid: 0, BattleId: beginId, ChallengeTime: 0})
		beginId++
	}

	p.tasks = NewTask(p)
	p.name = name
	p.uid = uid
	p.InstanceData.RelicMap.Evaluation = []global.KeyValue{}
	p.InstanceData.ChallengeRecord.Records = make(map[int32]int32)
	p.pvpRecorder = []*model.PvpRecord{}
	p.Guidance = []int32{}
	p.bRobot = false
	return p
}

// IMapObject interface

func (p *Player) PushMsg(route string, v interface{}) error {
	return p.pushMsg(route, v)
}

func (p *Player) worldBossDamage(damage int64) {
	p.totalWorldBossDamage += damage
	if p.desk != nil {
		p.desk.DWorldBoss.curRoundDamage += damage
	}

	// p.logger.Debugf(" 本次伤害 %d 世界BOSS伤害累计 %d", damage, p.totalWorldBossDamage)
}

// IMapObject interface end

// RefreshStatsOption 定义刷新属性的选项函数
type RefreshStatsOption func(*refreshStatsConfig)

// refreshStatsConfig 刷新属性的配置
type refreshStatsConfig struct {
	shouldPushMessage bool // 是否推送消息
}

// WithRefreshStatsPushMessage 设置是否推送消息
func WithRefreshStatsPushMessage(push bool) RefreshStatsOption {
	return func(cfg *refreshStatsConfig) {
		cfg.shouldPushMessage = push
	}
}

// newRefreshStatsConfig 创建默认的刷新属性配置
func newRefreshStatsConfig() *refreshStatsConfig {
	return &refreshStatsConfig{
		shouldPushMessage: true, // 默认推送消息
	}
}

// 计算角色属性
func (p *Player) refreshStats(opts ...RefreshStatsOption) {
	defer func() {
		if err := recover(); err != nil {
			p.logger.Errorf("Error=%v", err)
			println(stack())
		}
	}()
	if !p.slock.TryLock() {
		return
	}
	defer p.slock.Unlock()

	cfg := newRefreshStatsConfig()
	for _, opt := range opts {
		opt(cfg)
	}

	now := time.Now()
	p.stats.clear()
	// 基础属性
	if !p.isRobot() {
		roleCfg, err := global.GetRoleCfg(int32(p.job), p.level)
		if err != nil {
			p.logger.Errorf("获取角色等级配置失败 level %d", p.level)
			return
		}

		for _, rc := range roleCfg.BassProperty {
			p.stats.setStats(pb.Attribute(rc.Key), rc.Value)
		}

		p.stats.setStats(pb.Attribute_ATTACK_SPEED, global.GetRoleInitial(int32(p.job)).AttackSpeed)
		p.stats.setStats(pb.Attribute_MOVE_SPEED, global.GetRoleInitial(int32(p.job)).MoveSpeed)
	} else {
		if r := robotMgr.getRankRobotInfo(p.serverID, p.uid); r != nil {
			if r.robotType == RobotTypePlayer {
				roleCfg, err := global.GetRoleCfg(int32(r.job), r.level)
				if err != nil {
					logger.Errorf("获取角色等级配置失败 level %d", p.level)
					return
				}

				for _, rc := range roleCfg.BassProperty {
					r.stats.setStats(pb.Attribute(rc.Key), rc.Value)
				}

				r.stats.setStats(pb.Attribute_ATTACK_SPEED, global.GetRoleInitial(int32(p.job)).AttackSpeed)
				r.stats.setStats(pb.Attribute_MOVE_SPEED, global.GetRoleInitial(int32(p.job)).MoveSpeed)
			} else {
				if r.cfg != nil {
					for _, attr := range r.cfg.Feature {
						r.stats.setStats(pb.Attribute(attr.Key), attr.Value)
					}

					r.stats.changeStats(pb.Attribute_ATTACK_SPEED, global.GetRoleInitial(int32(p.job)).AttackSpeed)
					r.stats.changeStats(pb.Attribute_MOVE_SPEED, global.GetRoleInitial(int32(p.job)).MoveSpeed)

					// 刷新伙伴属性
					for _, v := range p.partnerSlots {
						if v == nil {
							continue
						}
						partner := p.getPartnerInfo(v.PartnerUid)
						if partner == nil {
							continue
						}
						partner.refreshStats(nil, p)
					}
				} else {
					// 模拟对战机器人不在机器人表中
					roleCfg, err := global.GetRoleCfg(int32(r.job), r.level)
					if err != nil {
						r.logger.Errorf("获取角色等级配置失败 level %d", r.level)
						return
					}

					for _, rc := range roleCfg.BassProperty {
						r.stats.setStats(pb.Attribute(rc.Key), rc.Value)
					}
					r.stats.setStats(pb.Attribute_ATTACK_SPEED, global.GetRoleInitial(int32(p.job)).AttackSpeed)
					r.stats.setStats(pb.Attribute_MOVE_SPEED, global.GetRoleInitial(int32(p.job)).MoveSpeed)
				}
			}
		}
	}

	// 命格
	for _, gv := range global.FateCfg {
		if gv.FateLevel == p.fateLevel {
			for _, att := range gv.UpValue {
				if att.Job == int32(p.job) {
					p.stats.setStats(pb.Attribute(att.Key), att.Value)
				}
			}
			break
		}
	}

	// 魂石增加生命值基数=角色基础属性+命格
	gemHealthAdd := p.stat(pb.Attribute_MAX_HEALTH)

	// 装备 强化大师
	equipStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	gemStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	petStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	newpetStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	guildStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	guildSkillStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	var maxEnhance, maxStars int32
	maxEnhance = math.MaxInt32
	maxStars = math.MaxInt32
	var starCfg, enhanceCfg *global.EquipMasterConfig

	// 宠物
	// 宠物因为有些属性加给装备，所以提到前面来了
	if len(p.pet) > 0 {
		if p.pet[0].PetCfgId != 0 {
			petCfg := global.GetPetCfg(p.pet[0].PetCfgId)
			if petCfg != nil {

				for _, e2 := range petCfg.StarAttribute {
					if e2.Id == p.pet[0].Star {
						for _, e3 := range e2.Values {
							petStatus.setStats(pb.Attribute(e3.Key), e3.Value)
						}
						break
					}
				}

				roleCfg, _ := global.GetRoleCfg(constant.Pet, p.pet[0].Level)
				if roleCfg != nil {
					rate := global.GetpetQualityRate(p.pet[0].Quality)
					if rate == 0 || petCfg.PetKind.Key == 2 {
						rate = 1
					}
					for _, e := range roleCfg.BassProperty {
						petStatus.setStats(pb.Attribute(e.Key), int32(math.Round(float64(e.Value)*rate)))
					}
				}
			}
		}
	}

	// 计算属性
	newpetStatus.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(petStatus.getStats(pb.Attribute_PHYSICAL_ATTACK))*(1+(float64(petStatus.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate))))
	newpetStatus.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(petStatus.getStats(pb.Attribute_MAGIC_ATTACK))*(1+(float64(petStatus.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate))))
	newpetStatus.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(petStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE))*(1+float64(petStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	newpetStatus.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(petStatus.getStats(pb.Attribute_MAGIC_DEFENSE))*(1+(float64(petStatus.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate))))
	newpetStatus.setStats(pb.Attribute_MAX_HEALTH, int32(float64(petStatus.getStats(pb.Attribute_MAX_HEALTH))*(1+(float64(petStatus.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate))))
	for pk, pv := range newpetStatus.Attrs {
		if pk == pb.Attribute_PHYSICAL_ATTACK || pk == pb.Attribute_MAGIC_ATTACK ||
			pk == pb.Attribute_PHYSICAL_DEFENSE || pk == pb.Attribute_MAGIC_DEFENSE ||
			pk == pb.Attribute_MAX_HEALTH {
			p.stats.setStats(pk, pv)
		}
	}

	allGems := []int32{}
	for slot, equip := range p.equips {
		if slot <= int(pb.EquipSlot_INVALID_EquipSlot) || slot >= int(pb.EquipSlot_MaxEquipSlot) {
			continue
		}
		gemstoneNature := make([]int32, 0)
		for GemstoneSlot, Gemstone := range equip.Gemstone {
			if GemstoneSlot < int(pb.GemstoneSlot_INVALID_GemstoneSlot) || GemstoneSlot > int(pb.GemstoneSlot_MaxGemstoneSlot) {
				continue
			}
			genstoneCfg, err := global.GetItemCfg(int32(Gemstone))
			if err != nil {
				continue
			}

			if genstoneCfg.NatureMark > 0 {
				gemstoneNature = append(gemstoneNature, genstoneCfg.NatureMark)
			}
			// 对应宝石等级的数目加1
			allGems = append(allGems, genstoneCfg.Itemlevel)

			for _, attr := range genstoneCfg.Stats {
				if attr.Key == pb.Attribute_HEALTH_PERCENTAGE {
					gemStatus.setStats(attr.Key, attr.Value)
				} else {
					p.stats.setStats(attr.Key, attr.Value)
				}
			}
		}

		// 魂石自然属性
		if len(gemstoneNature) == int(pb.GemstoneSlot_MaxGemstoneSlot) && gemstoneNature[0] == gemstoneNature[1] && gemstoneNature[1] == gemstoneNature[2] && gemstoneNature[2] == gemstoneNature[3] {
			gemNatureCfg, ok := global.GetEquipMaster(constant.EquipMasterCategoryGemStoneNature*1000 + gemstoneNature[0])
			if ok != nil {
				continue
			}
			for _, v := range gemNatureCfg.Attribute {
				p.stats.setStats(pb.Attribute(v.Key), v.Value)
			}
		}

		if equip.Enhance < maxEnhance {
			maxEnhance = equip.Enhance
		}
		if equip.Stars < maxStars {
			maxStars = equip.Stars
		}
		if equip.ItemId == 0 {
			continue
		}
		equipInfo := p.getItemByUid(equip.ItemId)
		if equipInfo == nil {
			continue
		}
		equipCfg, err := global.GetItemCfg(equipInfo.CfgId)
		if err != nil {
			continue
		}

		// 基础值攻\防: （装备基础值+槽位强化基础值)*升星百分比 , 基础属性: 装备基础值*品质百分比
		for _, attr := range equipCfg.Stats {
			if attr.Key == pb.Attribute_PHYSICAL_ATTACK || attr.Key == pb.Attribute_MAGIC_ATTACK ||
				attr.Key == pb.Attribute_PHYSICAL_DEFENSE || attr.Key == pb.Attribute_MAGIC_DEFENSE ||
				attr.Key == pb.Attribute_MAX_HEALTH {
				if equipInfo.Equip != nil {
					// 如果是武器，会额外算上武器基础值
					if pb.EquipSlot(equipCfg.Equiptype) == pb.EquipSlot_Weapon {
						per := float64(1)
						// 获取某个buff的属性
						if buff := p.getBuffByType(constant.BuffPetWeaponAttackPercentageIncrease); buff != nil {
							per = float64(buff.BuffStats.getStats(pb.Attribute_WEAPON_ATTACK_PERCENTAGE)) / BaseRate
						}
						equipStatus.setStats(attr.Key, int32(math.Round(float64(attr.Value)*global.GetEquipqualityCoefficient(equipInfo.Equip.Quality))*per))
					} else {
						equipStatus.setStats(attr.Key, int32(math.Round(float64(attr.Value)*global.GetEquipqualityCoefficient(equipInfo.Equip.Quality))))
					}
				}
			} else {
				p.stats.setStats(attr.Key, attr.Value)
			}
		}
		// 极品属性直接计算
		if equipInfo.Equip != nil {
			for _, attr := range equipInfo.Equip.Attributes {
				p.stats.setStats(attr.Attr, attr.Num)
			}
		}

		star_uid := int32(p.job%10*100) + equip.Stars
		if equipStarCfg, err := global.GetStarsCfg(star_uid); err == nil {
			for _, star_attr := range equipStarCfg.Attribute {
				if star_attr.Slot == int32(slot) {
					equipStatus.setStats(pb.Attribute(star_attr.Attribute), star_attr.Value)
				}
			}
		}

		enhanc_uid := int32(p.job%10*1000) + equip.Enhance
		if equipEnhancCfg, err := global.GetEnhanceCfg(enhanc_uid); err == nil {
			for _, star_attr := range equipEnhancCfg.Attribute {
				if star_attr.Slot == int32(slot) {
					if equipCfg.Quality > 0 && equipCfg.Quality <= 7 {
						equipStatus.setStats(pb.Attribute(star_attr.Attribute), int32(float32(star_attr.Value)*global.GlobalCfg.Equipenhances[equipCfg.Quality-1].Value))
					}
				}
			}
		}
	}

	equipStatus.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_ATTACK))*(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(equipStatus.getStats(pb.Attribute_MAGIC_ATTACK))*(float64(equipStatus.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE))*(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(equipStatus.getStats(pb.Attribute_MAGIC_DEFENSE))*(float64(equipStatus.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAX_HEALTH, int32(float64(equipStatus.getStats(pb.Attribute_MAX_HEALTH))*(float64(equipStatus.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate)))

	for ek, ev := range equipStatus.Attrs {
		if ek == pb.Attribute_PHYSICAL_ATTACK || ek == pb.Attribute_MAGIC_ATTACK ||
			ek == pb.Attribute_PHYSICAL_DEFENSE || ek == pb.Attribute_MAGIC_DEFENSE ||
			ek == pb.Attribute_MAX_HEALTH {
			p.stats.setStats(ek, ev)
		}
	}

	// 魂石增加生命百分比
	p.stats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(gemHealthAdd)*(float64(gemStatus.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate)))

	for _, equipmaster := range global.EquipMasterCfg {
		if equipmaster.Category == constant.EquipMasterCategoryEnhance && maxEnhance >= equipmaster.Limit {
			enhanceCfg = equipmaster
		}
		if equipmaster.Category == constant.EquipMasterCategoryStars && maxStars >= equipmaster.Limit {
			starCfg = equipmaster
		}
	}

	// 魂石大师
	sort.Slice(allGems, func(i, j int) bool {
		return allGems[i] > allGems[j]
	})
	for i := len(global.GlobalCfg.Diamond.Master.Stage) - 1; i >= 0; i-- {
		if global.GlobalCfg.Diamond.Master.Stage[i].Num > 0 && len(allGems) >= int(global.GlobalCfg.Diamond.Master.Stage[i].Num) {
			equipMasterCfg, err := global.GetEquipMaster(global.GlobalCfg.Diamond.Master.Stage[i].ID)
			if err != nil {
				continue
			}
			if allGems[global.GlobalCfg.Diamond.Master.Stage[i].Num-1] >= equipMasterCfg.Limit {
				for _, v := range equipMasterCfg.Attribute {
					p.stats.setStats(pb.Attribute(v.Key), v.Value)
				}
				break
			}
		}
	}

	// 强化大师
	if enhanceCfg != nil {
		for _, v := range enhanceCfg.Attribute {
			p.stats.setStats(pb.Attribute(v.Key), v.Value)
		}
	}

	// 升星大师
	if starCfg != nil {
		for _, v := range starCfg.Attribute {
			p.stats.setStats(pb.Attribute(v.Key), v.Value)
		}
	}

	// 特权卡
	privilegeStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	for _, v := range p.privilege {
		cfg, ok := global.PrivilegeCfg[int32(v.Pid)]
		if !ok {
			continue
		}
		for _, att := range cfg.Pcontent {
			av := privilegeStatus.getStats(pb.Attribute(att.Key))
			if att.Value > av {
				privilegeStatus.changeStats(pb.Attribute(att.Key), att.Value)
			}
		}
	}
	for k, v := range privilegeStatus.Attrs {
		p.stats.setStats(k, v)
	}

	// VIP
	vipStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	for _, gv := range global.VIPCfg {
		if gv.VIPLevel <= p.vip {
			for _, att := range gv.Pcontent {
				av := vipStatus.getStats(pb.Attribute(att.Key))
				if att.Value > av {
					vipStatus.changeStats(pb.Attribute(att.Key), att.Value)
				}
			}
		}
	}
	for k, v := range vipStatus.Attrs {
		p.stats.setStats(k, v)
	}

	p.stats.setStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE, int32(global.GlobalCfg.BaseValue.Crit.CriticalDamage))

	// 工会基础属性
	if p.guildId > 0 {
		// 工会技能
		// guildManager := NewGuildManager()
		guild, err := guildManager.validateGuild(p.guildId)
		if err == nil {
			guildCfg := global.GetGuildCfg(int32(guild.Guild.Level))
			if guildCfg != nil {
				for _, e := range guildCfg.AttributeAdd {
					guildStatus.setStats(pb.Attribute(e.Key), e.Value)
				}
			}
			// 查询工会目前技能情况
			for _, e := range guild.Skills {
				skillCfg := global.GetGuildSkillCfg(e.UUid)
				if skillCfg != nil {
					for _, e2 := range skillCfg.ValueAdd {
						guildStatus.setStats(pb.Attribute(e2.Key), e2.Value)
					}
				}
			}
		}

		guildSkillStatus.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(guildStatus.getStats(pb.Attribute_PHYSICAL_ATTACK))*(1+(float64(guildStatus.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate))))
		guildSkillStatus.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(guildStatus.getStats(pb.Attribute_MAGIC_ATTACK))*(1+(float64(guildStatus.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate))))
		guildSkillStatus.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(guildStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE))*(1+float64(guildStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
		guildSkillStatus.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(guildStatus.getStats(pb.Attribute_MAGIC_DEFENSE))*(1+(float64(guildStatus.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate))))
		guildSkillStatus.setStats(pb.Attribute_MAX_HEALTH, int32(float64(guildStatus.getStats(pb.Attribute_MAX_HEALTH))*(1+(float64(guildStatus.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate))))
		// 计算属性
		for k, v := range guildSkillStatus.Attrs {
			p.stats.setStats(k, v)
		}
	}
	// 被动技能
	for _, v := range p.skills {
		if skillCfg, err := global.GetSkillCfg(v.CfgId); err == nil {
			if skillCfg.ActiveorPassive == PassiveSkill {
				for _, e := range skillCfg.Effect {
					p.stats.setStats(pb.Attribute(e.Key), e.Value)
				}
			}
		}
	}
	// buff
	petBuffs := p.getPetBuffs()
	if p.block.TryLock() {
		for _, buff := range p.buffs {
			buffCfg, err := global.GetBuffCfg(buff.Id)
			if err == nil {
				// buff白名单，有的buff不计算战斗力，
				// 判断是不是在白名单里面
				// 获取宠物buff
				if len(petBuffs) > 0 {
					for _, petBuff := range petBuffs {
						// 获取buffid对应的大类
						buffCfgDetail, err := global.GetBuffCfg(petBuff.Id)
						if err == nil {
							if petBuff.Id == buffCfg.ID && (constant.BuffType(buffCfgDetail.Pretype) == constant.BuffPercentageAttackBoost ||
								constant.BuffType(buffCfgDetail.Pretype) == constant.BuffPercentageDefenseBoost ||
								constant.BuffType(buffCfgDetail.Pretype) == constant.BuffPercentageMaxHealthIncrease ||
								constant.BuffType(buffCfgDetail.Pretype) == constant.BuffReflectDamage) {
								for i, e := range petBuff.Stats.Attrs {
									p.stats.setStats(i, e)
								}
							}
						}

					}
				}
			}

		}
		p.block.Unlock()
	}
	// 参与计算战斗力的属性写在上面!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

	combatStats := Stats{Attrs: p.stats.copyStats()}
	// buff
	if p.block.TryLock() {
		for _, buff := range p.buffs {
			buffCfg, err := global.GetBuffCfg(buff.Id)
			if err != nil {
				continue
			}

			if buffCfg.EffectTime != 0 {
				continue
			}

			if now.After(buff.OverTime) {
				continue
			}

			// 魔法酸雨的增伤只针对技能
			if buffCfg.Pretype == constant.SkillTypeMagicAcidRain {
				continue
			}

			have := false
			if len(petBuffs) > 0 {
				for _, petBuff := range petBuffs {
					if petBuff.Id == buffCfg.ID {
						have = true
						break
					}
				}
			}
			if have {
				continue
			}
			for i, e := range buff.BuffStats.Attrs {
				p.stats.setStats(i, e)
			}
		}
		p.block.Unlock()
	}
	// <0  减益 ; >0 增益
	p.stats.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(p.GetDC())*(float64(p.stats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	p.stats.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(p.GetMC())*(float64(p.stats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	p.stats.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(p.GetAR())*(float64(p.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	p.stats.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(p.GetMR())*(float64(p.stats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))

	p.stats.setStats(pb.Attribute_DODGE, int32(float64(p.stats.getStats(pb.Attribute_DODGE))*(float64(p.stats.getStats(pb.Attribute_DODGE_PERCENTAGE))/BaseRate)))
	p.stats.setStats(pb.Attribute_HIT, int32(float64(p.stats.getStats(pb.Attribute_HIT))*(float64(p.stats.getStats(pb.Attribute_HIT_PERCENTAGE))/BaseRate)))
	p.stats.setStats(pb.Attribute_CRITICAL_STRIKE, int32(float64(p.stats.getStats(pb.Attribute_CRITICAL_STRIKE))*(float64(p.stats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE))/BaseRate)))
	p.stats.setStats(pb.Attribute_TOUGHNESS, int32(float64(p.stats.getStats(pb.Attribute_TOUGHNESS))*(float64(p.stats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE))/BaseRate)))
	p.stats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(p.GetMaxHP())*float64(p.stats.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate))

	// 攻速移速增加  基础/(1+加成)；攻速移速降低  基础*(1+加成)
	p.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(float64(p.stats.getStats(pb.Attribute_ATTACK_SPEED))/(1+float64(p.stats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE))/BaseRate)))
	p.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(float64(p.stats.getStats(pb.Attribute_MOVE_SPEED))/(1+(float64(p.stats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE))/BaseRate))))

	// 基础属性 >= 0
	p.stats.changeStats(pb.Attribute_PHYSICAL_ATTACK, int32(math.Max(0, float64(p.GetDC()))))
	p.stats.changeStats(pb.Attribute_MAGIC_ATTACK, int32(math.Max(0, float64(p.GetMC()))))
	p.stats.changeStats(pb.Attribute_PHYSICAL_DEFENSE, int32(math.Max(0, float64(p.GetAR()))))
	p.stats.changeStats(pb.Attribute_MAGIC_DEFENSE, int32(math.Max(0, float64(p.GetMR()))))

	p.stats.changeStats(pb.Attribute_DODGE, int32(math.Max(0, float64(p.stats.getStats(pb.Attribute_DODGE)))))
	p.stats.changeStats(pb.Attribute_HIT, int32(math.Max(0, float64(p.stats.getStats(pb.Attribute_HIT)))))
	p.stats.changeStats(pb.Attribute_CRITICAL_STRIKE, int32(math.Max(0, float64(p.stats.getStats(pb.Attribute_CRITICAL_STRIKE)))))
	p.stats.changeStats(pb.Attribute_TOUGHNESS, int32(math.Max(0, float64(p.stats.getStats(pb.Attribute_TOUGHNESS)))))
	p.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(math.Max(0, float64(p.stats.getStats(pb.Attribute_ATTACK_SPEED)))))
	p.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(math.Max(0, float64(p.stats.getStats(pb.Attribute_MOVE_SPEED)))))
	p.stats.changeStats(pb.Attribute_MAX_HEALTH, int32(math.Max(0, float64(p.GetMaxHP()))))
	if p.curState == PlayerStatusPlaying {
		if p.currentHP > p.GetMaxHP() {
			p.currentHP = p.GetMaxHP()
		}
		p.stats.changeStats(pb.Attribute_HEALTH, p.currentHP)
		combatStats.changeStats(pb.Attribute_HEALTH, p.currentHP)
	} else {
		p.stats.changeStats(pb.Attribute_HEALTH, p.GetMaxHP())
		p.currentHP = p.GetMaxHP()
		combatStats.changeStats(pb.Attribute_HEALTH, p.GetMaxHP())
	}

	combat := p.calcCombatReal(&combatStats)

	// p.logger.Debugf(" [%s] 真实战力 %d , 虚假战力  %d", p.name, combat, p.calcCombat())

	tmpStats2 := p.stats.copyStats()
	packet := &pb.SyncUserStatsPush{Attributes: []*pb.Stats{}, Partners: []*pb.SyncUserStatsPush_PartnerStats{}}
	for k, v := range tmpStats2 {
		if v <= 0 {
			continue
		}
		packet.Attributes = append(packet.Attributes, &pb.Stats{Attr: k, Num: v})
	}
	packet.Combat = combat
	// 刷新伙伴属性
	for _, v := range p.partnerSlots {
		if v == nil {
			continue
		}
		partner := p.getPartnerInfo(v.PartnerUid)
		if partner == nil {
			continue
		}
		partner.refreshStats(packet, p)
	}

	packet.TotalCombat += packet.Combat
	for _, v := range packet.Partners {
		packet.TotalCombat += v.Combat
	}
	oldTotalCombat := p.totalCombat

	p.totalCombat = packet.TotalCombat
	if p.totalCombat > oldTotalCombat {
		p.tasks.dispatch(pb.TargetType_ROLE_POWER_REQUIREMENT, 0, p.totalCombat)
		if !p.bRobot {
			p.rankChangeTCP(constant.RankTypeFight, int64(p.totalCombat))
		}
	}
	if cfg.shouldPushMessage {
		p.pushMsg(protocol.RouteSyncUserStatsPush, packet)
	}
	// p.logger.Debugf("属性计算 当前生命:%d  最大生命:%d  物攻: %d 魔攻: %d", p.currentHP, p.GetMaxHP(), p.GetDC(), p.GetMC())
}

// 异步从数据库同步
func (p *Player) syncFromDB() bool {
	ch := make(chan bool)
	async.Run(func() {
		u, err := db.QueryUser(p.Uid())
		if err != nil {
			p.logger.Errorf("玩家基础数据同步错误,userId: %d Error=%v", p.Uid(), err)
			ch <- false
			return
		}
		// ru, err := db.QueryRegisterByUid(p.Uid())
		// if err != nil {
		// 	p.logger.Errorf("玩家基础数据同步错误Register,userId: %d Error=%v", p.Uid(), err)
		// 	ch <- false
		// 	return
		// }
		p.lock.Lock()
		defer p.lock.Unlock()
		p.totalOnlineTime = u.TotalOnlineTime
		p.dayOnlineTime = u.DayOnlineTime
		p.accountName = u.AccountName
		p.name = u.Name
		p.energy = u.Energy
		p.exp = u.Exp
		p.level = u.Level
		p.vip = u.Vip
		p.recharge = u.Recharge
		p.job = constant.MowJob(u.Job)
		p.accountName = u.AccountName
		p.energyRecoveryTime = time.Unix(u.EnergyRecoveryTime, 0)
		p.lastLoginAt = time.Unix(u.LastLoginAt, 0)
		if u.LastLoginAt == 0 {
			p.bCreate = true
		}

		p.dailySoul = u.DailySoul
		p.sessionSoul = u.SessionSoul
		p.pvpScore = int32(u.PvpScore)
		p.gvgScore = int32(u.GvgScore)
		p.PVPDuel.dailyBuyNum = int32(u.BuyMatchNumDuel)
		p.PVPDuel.dailyMatchTimes = int32(u.MatchTimesDuel)
		p.PVPMelee.dailyBuyNum = int32(u.BuyMatchNumMelee)
		p.PVPMelee.dailyMatchTimes = int32(u.MatchTimesMelee)

		p.SoloTowerInfo.Layers = int32(u.SoloTowerLayers)
		p.SoloTowerInfo.RewardTime = time.Unix(u.SoloTowerRewardTime, 0)
		p.sex = u.Sex
		p.fateExp = u.FateExp
		p.fateLevel = u.FateLevel
		p.vitalityTime = u.VitalityTime
		p.dailyWorldBossTimes = int32(u.WorldBossTimes)
		p.totalWorldBossDamage = u.WorldBossDamage
		p.serverID = u.ServerId
		// 新的同步总战斗力
		p.totalCombat = u.TotalCombat
		p.afkExpirationAt = time.Unix(u.AfkExpirationAt, 0)
		p.afkDailyTime = u.AfkDailyTime
		p.AfkMultiplierID = u.AfkMultiplierID
		p.offlineTime = u.OfflineTime
		// p.channelId = u.ChannelId
		// 体力恢复
		p.nextEnergyRecoveryTime = 0
		if p.energy < int64(global.GlobalCfg.BaseValue.Energy.Fullvalue) {
			// 离线恢复总体力
			recoverNum := (time.Now().Unix() - u.EnergyRecoveryTime) / int64(global.GlobalCfg.BaseValue.Energy.Timeinterval*60)
			// 不足恢复一次体力的时间
			leftTime := (time.Now().Unix() - u.EnergyRecoveryTime) % int64(global.GlobalCfg.BaseValue.Energy.Timeinterval*60)
			p.energy += recoverNum
			if p.energy > int64(global.GlobalCfg.BaseValue.Energy.Fullvalue) {
				p.energy = int64(global.GlobalCfg.BaseValue.Energy.Fullvalue)
				p.energyRecoveryTime = time.Now()
			} else {
				p.nextEnergyRecoveryTime = global.GlobalCfg.BaseValue.Energy.Timeinterval*60 - int(leftTime)
				p.energyRecoveryTime = time.Now().Add(-time.Duration(global.GlobalCfg.BaseValue.Energy.Timeinterval) * time.Minute)
			}
		}

		if err = protobuf.NewSerializer().Unmarshal(u.Bag, p.bag); err != nil {
			p.logger.Errorf("包裹数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}

		if err = json.Unmarshal([]byte(u.Skill), &p.skills); u.Skill != "" && err != nil {
			p.logger.Errorf("技能数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}

		if err = json.Unmarshal([]byte(u.Equips), &p.equips); u.Equips != "" && err != nil {
			p.logger.Errorf("装备数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}
		if err = json.Unmarshal([]byte(u.SkillSlot), &p.skillsSlot); u.SkillSlot != "" && err != nil {
			p.logger.Errorf("技能框数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}
		dbtask := &pb.DBTask{}
		if err = protobuf.NewSerializer().Unmarshal(u.Task, dbtask); err == nil {
			p.tasks.fromPB(dbtask)
		} else {
			p.logger.Errorf("包裹数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}

		if err = json.Unmarshal([]byte(u.DailyMap), &p.dailyMaps); u.DailyMap != "" && err != nil {
			p.logger.Errorf("每日必做数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}

		if err = json.Unmarshal([]byte(u.TurnBasedSlot), &p.turnBasedSlot); u.TurnBasedSlot != "" && err != nil {
			p.logger.Errorf("回合制数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}

		if err = json.Unmarshal([]byte(u.Guidance), &p.Guidance); u.Guidance != "" && err != nil {
			p.logger.Errorf("引导数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}

		// buff
		p.fromDbBuff(u.Buff)

		// 加载特权卡
		if privilege, err := provide.PrivilegeListTCP(p.uid); err == nil {
			for _, v := range privilege {
				p.addPrivilege(constant.ShopTypeSubscription, v.Pid, v.EndTime)
			}
		}

		if err = json.Unmarshal([]byte(u.Disassembly), &p.AutoDisassembly); u.Disassembly != "" && err != nil {
			p.logger.Errorf("自动分解数据反序列化失败 err: %s", err.Error())
			ch <- false
			return
		}

		// 代币
		if err = json.Unmarshal([]byte(u.Tokens), &p.tokens); u.Tokens != "" && err != nil {
			p.logger.Errorf("代币数据反序列化 err: %s", err.Error())
			ch <- false
			return
		}
		// 代币消耗
		if err = json.Unmarshal([]byte(u.TokenDeletes), &p.tokenDeletes); u.TokenDeletes != "" && err != nil {
			p.logger.Errorf("代币消耗数据反序列化 err: %s", err.Error())
			ch <- false
			return
		}
		if err = json.Unmarshal([]byte(u.FirstUse), &p.firstUse); u.FirstUse != "" && err != nil {
			p.logger.Errorf("代币消耗数据反序列化 err: %s", err.Error())
			ch <- false
			return
		}
		if err = p.syncPartnerFromDB(); err != nil {
			ch <- false
			return
		}
		if err = p.syncBoxFromDB(); err != nil {
			ch <- false
			return
		}
		if err = p.syncPetFromDB(); err != nil {
			ch <- false
			return
		}

		// 工会信息
		if err = p.syncGuildFromDB(); err != nil {
			p.logger.Errorf("工会信息同步失败 err: %s", err.Error())
			ch <- false
			return
		}
		if err = p.syncInstanceDataFromDB(); err != nil {
			ch <- false
			return
		}

		if err = p.syncPvpRecorderFromDB(); err != nil {
			ch <- false
			return
		}

		ch <- true
	})
	return <-ch
}

// 定义包级缓存和时间戳
// var (
// 	userBoxCache      = sync.Map{} // key: uid(int64), value: *model.UserBox
// 	userBoxCacheTimes = sync.Map{} // key: uid(int64), value: int64(Unix秒)
// )

func (p *Player) syncBoxFromDB() error {
	// var ubox *model.UserBox
	// var err error
	uid := p.Uid()
	now := time.Now().Unix()
	// cacheDuration := int64(10) // 缓存10秒

	// // 检查缓存时间
	// if val, ok := userBoxCache.Load(uid); ok {
	// 	if ts, ok2 := userBoxCacheTimes.Load(uid); ok2 {
	// 		if now-int64(ts.(int64)) < cacheDuration {
	// 			ubox = val.(*model.UserBox)
	// 		}
	// 	}
	// }
	// if ubox == nil {
	// 	ubox, err = db.QueryUserBox(uid)
	// 	if err == nil && ubox != nil {
	// 		userBoxCache.Store(uid, ubox)
	// 		userBoxCacheTimes.Store(uid, now)
	// 	}
	// }
	ubox, err := db.QueryUserBox(uid)
	if err != nil {
		p.logger.Errorf("玩家宝箱同步错误,userId: %d Error=%v", uid, err)
		return err
	}
	timeCost := ubox.TimeCost
	// 当前时间减最后在线时间
	if p.offlineTime > 0 {
		if ubox.NextBoxRecoveryTime != -1 {
			delta := now - p.offlineTime
			if delta > ubox.NextBoxRecoveryTime {
				ubox.NextBoxRecoveryTime = 1
				timeCost += int32(ubox.NextBoxRecoveryTime)
			} else {
				ubox.NextBoxRecoveryTime = ubox.NextBoxRecoveryTime - delta
				timeCost += int32(ubox.NextBoxRecoveryTime)
			}
		}
	}

	p.box = BoxInfo{
		NowLevel:            ubox.NowLevel,
		NextBoxRecoveryTime: ubox.NextBoxRecoveryTime,
		NowOpenRes:          []pb.ItemInfo{},
		BoxRecoveryTime:     time.Unix(ubox.BeginAt, 0),
		BeginAt:             ubox.BeginAt,
		IsAccelerate:        ubox.IsAccelerate,
		AccelerateNum:       ubox.AccelerateNum,
		CostGold:            ubox.CostGold,
		TimeCost:            timeCost,
		// NowOpenLevel:        ubox.NowOpenLevel,
		// Job:                 ubox.Job,
	}
	if ubox.NowOpenRes != "" && ubox.NowOpenRes != "0" {
		if err = json.Unmarshal([]byte(ubox.NowOpenRes), &p.box.NowOpenRes); err != nil {
			p.logger.Errorf("首页装备反序列化失败 err: %s", err.Error())
			return err
		}
	}
	return nil
}

func (p *Player) syncPartnerFromDB() error {
	list, err := db.QueryPartner(p.Uid())
	if err != nil {
		p.logger.Errorf("加载伙伴错误,userId: %d Error=%v", p.Uid(), err)

		return err
	}

	for _, v := range list {
		partner := &Partner{
			MapObject: NewMapObject(ObjectTypePartner, v.PartnerUid),
		}

		partner.cfgId = v.CfgId
		partner.star = v.Star
		partner.level = v.Level

		if err = json.Unmarshal([]byte(v.Skill), &partner.skills); v.Skill != "" && err != nil {
			p.logger.Errorf("技能数据反序列化 err: %s", err.Error())

			return err
		}
		partner.owner = p
		partner.cfg = global.GetPartnerCfg(partner.cfgId)
		partner.name = partner.cfg.Name
		partner.job = constant.MowJob(partner.cfg.Job)
		partner.attrRatio = BaseRate

		p.partners[v.PartnerUid] = partner
	}

	slots, err := db.QueryPartnerSlot(p.Uid())
	if err != nil {
		p.logger.Errorf("伙伴同步错误,userId: %d Error=%v", p.Uid(), err)
		return err
	}

	for i, v := range slots {
		pslot := &PartnerSlot{SlotId: v.SlotId, PartnerUid: v.PartnerUid}
		// p.partnerSlots[v.SlotId] = &PartnerSlot{sid: v.SlotId, puid: v.PartnerUid}
		if err = json.Unmarshal([]byte(v.Equips), &pslot.Equips); v.Equips != "" && err != nil {
			p.logger.Errorf("装备数据反序列化失败 err: %s", err.Error())
			return err
		}
		p.partnerSlots[i] = pslot
	}

	return nil
}

// // 定义包级缓存和时间戳
// var (
// 	userPetCache      = sync.Map{} // key: uid(int64), value: *model.UserPet
// 	userPetCacheTimes = sync.Map{} // key: uid(int64), value: int64(Unix秒)
// )

func (p *Player) syncPetFromDB() error {
	// var upet *model.UserPet
	// var err error
	uid := p.Uid()
	// now := time.Now().Unix()
	// cacheDuration := int64(10) // 缓存10秒

	// // 检查缓存时间
	// if val, ok := userPetCache.Load(uid); ok {
	// 	if ts, ok2 := userPetCacheTimes.Load(uid); ok2 {
	// 		if now-int64(ts.(int64)) < cacheDuration {
	// 			upet = val.(*model.UserPet)
	// 		}
	// 	}
	// }
	// if upet == nil {
	// 	upet, err = db.QueryUserPet(uid)
	// 	if err == nil && upet != nil {
	// 		userPetCache.Store(uid, upet)
	// 		userPetCacheTimes.Store(uid, now)
	// 	}
	// }
	upet, err := db.QueryUserPet(uid)
	if err != nil {
		p.logger.Errorf("玩家宠物同步错误,userId: %d Error=%v", uid, err)
		return err
	}
	if upet == nil {
		return nil
	}
	if upet.PetInfo != "" && upet.PetInfo != "0" {
		if err = json.Unmarshal([]byte(upet.PetInfo), &p.pet); err != nil {
			p.logger.Errorf("宠物列表反序列化失败 err: %s", err.Error())
			return err
		}
	}
	p.petEverHave = []int32{}
	if upet.PetEverHave != "" && upet.PetEverHave != "0" {
		if err = json.Unmarshal([]byte(upet.PetEverHave), &p.petEverHave); err != nil {
			p.logger.Errorf("宠物曾经拥有反序列化失败 err: %s", err.Error())
			return err
		}
	}
	// ...existing code...
	// 宠物恢复
	p.nextPetRecoveryTime = 0
	petNormalCount := p.getItemCountInBag(constant.ItemTypePetNormal)
	if petNormalCount >= int64(global.GlobalCfg.BaseValue.Petfree.Maxnum) {
		p.petRecoveryTime = time.Now()
	} else {
		if upet.PetRecoveryTime == 0 {
			// 当前时间的前一秒
			p.petRecoveryTime = time.Now().Add(-time.Second)
			p.nextPetRecoveryTime = int(global.GlobalCfg.BaseValue.Petfree.Recovertime * 60)
		}
		if upet.PetRecoveryTime > 0 {
			// 当前时间减去最后更新时间，就是需要恢复的次数
			recoverNum := (time.Now().Unix() - upet.PetRecoveryTime) / int64(global.GlobalCfg.BaseValue.Petfree.Recovertime*60)
			leftTime := (time.Now().Unix() - upet.PetRecoveryTime) % int64(global.GlobalCfg.BaseValue.Petfree.Recovertime*60)
			// 次数大于最大的次数，取最大次数
			if recoverNum+petNormalCount > int64(global.GlobalCfg.BaseValue.Petfree.Maxnum) {
				recoverNum = int64(global.GlobalCfg.BaseValue.Petfree.Maxnum) - petNormalCount
			}
			if recoverNum < 0 {
				recoverNum = 0
			}
			// 下次恢复时间
			p.nextPetRecoveryTime = int(global.GlobalCfg.BaseValue.Petfree.Recovertime*60) - int(leftTime)
			// 次数大于0才进行恢复
			if recoverNum > 0 {
				go p.addSource(constant.ItemTypePetNormal, int64(recoverNum), constant.ItemAddPetAFK)
				// 最后一次宠物蛋恢复时间
				p.petRecoveryTime = time.Now().Add(-time.Duration(leftTime) * time.Second)
			} else {
				p.petRecoveryTime = time.Unix(upet.PetRecoveryTime, 0)
			}
		}
	}
	// ...existing code...
	if p.pet[0].PetCfgId != 0 {
		petInfo := global.GetPetCfg(p.pet[0].PetCfgId)
		if petInfo != nil && len(petInfo.OfflineAward) > 0 {
			petGoldNum := int32(0)
			petGoldGap := int32(0)
			petGoldMax := int32(0)
			petExpNum := int32(0)
			petExpGap := int32(0)
			petExpMax := int32(0)
			for _, v := range petInfo.OfflineAward {
				if v.Key == p.pet[0].Star {
					for _, v2 := range v.Values {
						if v2.ID == constant.ItemTypeCoin {
							petGoldNum = v2.Num
							petGoldGap = int32(v2.Gap)
							petGoldMax = int32(v2.Max)
						}
						if v2.ID == constant.ItemTypeExp {
							petExpNum = v2.Num
							petExpGap = int32(v2.Gap)
							petExpMax = int32(v2.Max)
						}
					}
				}
			}
			if petGoldNum > 0 {
				if p.petGold < petGoldMax {
					if upet.PetRecoveryTimeGold > 0 {
						p.nextPetRecoveryTimeGold = 0
						recoverNumGold := (time.Now().Unix() - upet.PetRecoveryTimeGold) / int64(petGoldGap) * int64(petGoldNum)
						leftTime := (time.Now().Unix() - upet.PetRecoveryTimeGold) % int64(petGoldGap)
						if p.petGold+int32(recoverNumGold) > petGoldMax {
							p.petGold = petGoldMax
							p.petRecoveryTimeGold = time.Now()
							// upet.PetRecoveryTimeGold = time.Now().Unix()
							if petGoldMax-p.petGold > 0 {
								go p.addSource(constant.ItemTypeCoin, int64(petGoldMax-p.petGold), constant.ItemAddPetGold)
							}
						} else {
							p.petGold += int32(recoverNumGold)
							p.nextPetRecoveryTimeGold = int(petGoldGap) - int(leftTime)
							p.petRecoveryTimeGold = time.Now().Add(-time.Duration(petGoldGap))
							// upet.PetRecoveryTimeGold = time.Now().Unix() - int64(petGoldGap)
							if recoverNumGold > 0 {
								go p.addSource(constant.ItemTypeCoin, int64(recoverNumGold), constant.ItemAddPetGold)
							}
						}
					}
				}
			}
			if petExpNum > 0 {
				if p.petExp < petExpMax {
					if upet.PetRecoveryTimeExp > 0 {
						recoverNumExp := (time.Now().Unix() - upet.PetRecoveryTimeExp) / int64(petExpGap) * int64(petExpNum)
						leftTime := (time.Now().Unix() - upet.PetRecoveryTimeExp) % int64(petExpGap)
						if p.petExp+int32(recoverNumExp) > petExpMax {
							p.petExp = petExpMax
							p.petRecoveryTimeExp = time.Now()
							// upet.PetRecoveryTimeExp = time.Now().Unix()
							if petExpMax-p.petExp > 0 {
								go p.addSource(constant.ItemTypeExp, int64(petExpMax-p.petExp), constant.ItemAddPetExp)
							}
						} else {
							p.petExp += int32(recoverNumExp)
							p.nextPetRecoveryTimeExp = int(petExpGap) - int(leftTime)
							p.petRecoveryTimeExp = time.Now().Add(-time.Duration(petExpGap))
							// upet.PetRecoveryTimeExp = time.Now().Unix() - int64(petExpGap)
							if recoverNumExp > 0 {
								go p.addSource(constant.ItemTypeExp, int64(recoverNumExp), constant.ItemAddPetExp)
							}
						}
					}
				}
			}

		}
	}
	return nil
}

// 同步工会信息
func (p *Player) syncGuildFromDB() error {
	uid := p.Uid()

	guildMember, err := db.QueryGuildMember(uid, 1)
	if err != nil {
		p.logger.Errorf("工会信息同步失败 err: %s", err.Error())
		return err
	}
	if guildMember == nil {
		p.guildId = 0
	} else {
		p.guildId = guildMember.GuildID
		p.guildContribution = int64(guildMember.GuildContribution)
		p.guildWish = int64(guildMember.GuildWish)
	}
	if p.guildId > 0 {
		p.nextWishRecoveryTime = 0
		selectTakeSource := p.getItemCountInBag(constant.ItemTypeGuildWish)
		if selectTakeSource > int64(global.GlobalCfg.Guild.WishValue.MaxValue) {
			p.wishRecoveryTime = time.Now()
		} else {
			if guildMember.RecoveryTimeExp == 0 {
				p.wishRecoveryTime = time.Now().Add(-time.Second)
				p.nextWishRecoveryTime = int(global.GlobalCfg.Guild.WishValue.RefreshTime * 60)
			}
			// Guild wish recovery
			if guildMember.RecoveryTimeExp > 0 {
				// 离线恢复总祈愿点
				recoverNum := (time.Now().Unix() - guildMember.RecoveryTimeExp) / int64(global.GlobalCfg.Guild.WishValue.RefreshTime*60)
				// 不足恢复一次祈愿点的时间
				leftTime := (time.Now().Unix() - guildMember.RecoveryTimeExp) % int64(global.GlobalCfg.Guild.WishValue.RefreshTime*60)
				// 祈愿点恢复
				if recoverNum+selectTakeSource > int64(global.GlobalCfg.Guild.WishValue.MaxValue) {
					recoverNum = int64(global.GlobalCfg.Guild.WishValue.MaxValue) - selectTakeSource
				}
				p.nextWishRecoveryTime = int(global.GlobalCfg.Guild.WishValue.RefreshTime*60) - int(leftTime)
				if recoverNum > 0 {
					go p.addSource(constant.ItemTypeGuildWish, int64(recoverNum), constant.ItemAddGuildWish)
					p.wishRecoveryTime = time.Now().Add(-time.Duration(leftTime) * time.Second)
				} else {
					p.wishRecoveryTime = time.Unix(guildMember.RecoveryTimeExp, 0)
				}

			}
		}
	}
	return nil
}

func (p *Player) syncInstanceDataFromDB() error {
	u, err := db.QueryInstanceData(p.Uid())
	if err == nil {
		p.BattleMap.BattleId = u.BattleCurStage
		p.BattleMap.RewardStage = u.BattleRewardStage
		p.BossMap.BattleId = u.BossCurStage
		p.BossMap.ChallengeTime = u.BossChallengeTime
		p.TrialTowerMap.BattleId = u.TrialTowerCurStage

		p.RelicMap.BattleId = u.RelicCurStage
		p.RelicMap.ChallengeTime = u.RelicChallengeTime

		p.FightEndMap.BattleId = u.FightEndCurStage
		p.FightEndMap.RefreshAt = time.Unix(u.FightEndRefreshTime, 0)

		if err = json.Unmarshal([]byte(u.RelicEvaluation), &p.RelicMap.Evaluation); u.RelicEvaluation != "" && err != nil {
			p.logger.Errorf("RelicMap副本评价数据反序列化失败 err: %s", err.Error())

			return errutil.ErrDBOperation
		}
		if err = json.Unmarshal([]byte(u.RelicChapterReward), &p.RelicMap.ChapterReward); u.RelicChapterReward != "" && err != nil {
			p.logger.Errorf("RelicMap副本奖励数据反序列化失败 err: %s", err.Error())

			return errutil.ErrDBOperation
		}

		if err = json.Unmarshal([]byte(u.FightEndEvaluation), &p.FightEndMap.Evaluation); u.FightEndEvaluation != "" && err != nil {
			p.logger.Errorf("BattleToTheEndMap副本数据反序列化失败 err: %s", err.Error())

			return errutil.ErrDBOperation
		}

		if err = json.Unmarshal([]byte(u.ChallengeRecord), &p.ChallengeRecord.Records); u.ChallengeRecord != "" && err != nil {
			p.logger.Errorf("ChallengeRecord副本数据反序列化失败 err: %s", err.Error())

			return errutil.ErrDBOperation
		}
	}

	return nil
}

// 同步pvprecoder
func (p *Player) syncPvpRecorderFromDB() error {
	u, err := db.QueryPvpRecords(p.Uid())
	if err == nil {
		p.pvpRecorder = u
	}
	return nil
}

// 同步角色基础信息
func (p *Player) syncUserBase() {
	vitality := int64(0)
	// 查询vip对应的活力分数
	if p.vip > 0 {
		if vipCfg := global.GetVipCfg(p.vip); vipCfg != nil {
			vitality += int64(vipCfg.Energyadd) * 60
		}
	}
	// 查询特权卡对应的活力分数
	if len(p.privilege) > 0 {
		for _, v := range p.privilege {
			// 查询特权卡对应的活力分数
			if privilegeCfg := global.GetPrivilegeCfg(v.Pid); privilegeCfg != nil {
				vitality += int64(privilegeCfg.Energystate) * 60
			}
		}
	}

	packet := &pb.SyncUserBasePush{
		Uid:                p.Uid(),
		Coin:               p.tokens[constant.ItemTypeCoin],
		Diamanta:           p.tokens[constant.ItemTypeGuildContribution],
		Coupon:             p.tokens[constant.ItemTypeCoupon],
		Soul:               p.tokens[constant.ItemTypeSoul],
		TrialVoucher:       p.tokens[constant.ItemTypeTrialVoucher],
		TreasuerBox:        p.tokens[constant.ItemTypeTreasureNum],
		PetExp:             p.tokens[constant.ItemTypePetExp],
		BoxQuick:           p.tokens[constant.ItemTypeAccelerateScroll],
		GuildContribution:  p.tokens[constant.ItemTypeGuildContribution],
		GuildWish:          p.tokens[constant.ItemTypeGuildWish],
		VipExp:             p.tokens[constant.ItemTypeVipExp],
		Energy:             p.energy,
		Exp:                p.exp,
		Level:              p.level,
		Vip:                p.vip,
		EnergyRecoveryTime: int32(p.nextEnergyRecoveryTime),
		Combat:             p.combat,
		FateLevel:          p.fateLevel,
		FateExp:            p.fateExp,
		HomeAFK:            int64(global.GlobalCfg.BaseValue.Onlinebattle.Basetime) + vitality - p.vitalityTime,
		GuildId:            p.guildId,
		WishRecoveryTime:   int32(time.Now().Unix() + int64(p.nextWishRecoveryTime)),
	}
	p.pushMsg(protocol.RouteSyncUserBasePush, packet)
}

func (p *Player) SyncUserAdvance() {
	// 其他信息
	packet := &pb.SyncUserAdvanceInfoPush{
		Skills: []*pb.SkillInfo{}, AutoDisassembly: &pb.AutoDisassemblyInfo{},
		TurnBasedUids: []int64{}, Guidance: []int32{}, Box: &pb.Userbox{},
	}
	for _, v := range p.skills {
		packet.Skills = append(packet.Skills, &pb.SkillInfo{CfgId: v.CfgId, Cooldown: 0, Advanced: v.AdvancedLevel})
	}
	for _, v := range p.equips {
		Gemstone := make([]int32, 0)
		for _, v2 := range v.Gemstone {
			Gemstone = append(Gemstone, int32(v2))
		}
		slot := &pb.SlotInfo{Stars: v.Stars, Enhance: v.Enhance, Item: p.getItemByUid(v.ItemId), Gemstone: Gemstone}
		packet.Equips = append(packet.Equips, slot)
	}
	// 技能
	for _, v := range p.skillsSlot {
		packet.SkillsSlot = append(packet.SkillsSlot, v)
	}
	packet.Tasks = p.tasks.toPB().GetTasks()

	packet.Guidance = append(packet.Guidance, p.Guidance...)

	packet.AutoDisassembly.Open = p.ADOpen
	packet.AutoDisassembly.Level = p.ADLevel
	packet.AutoDisassembly.Quality = pb.ItemQuality(p.ADQuality)
	packet.AutoDisassembly.HighCombat = p.ADHighCombat
	packet.AutoDisassembly.QualitySwitch = p.ADQualitySwitch
	packet.AutoDisassembly.LevelSwitch = p.ADLevelSwitch
	// 回合制阵容
	for _, v := range p.turnBasedSlot {
		packet.TurnBasedUids = append(packet.TurnBasedUids, v)
	}

	p.pushMsg(protocol.RouteSyncUserAdvanceInfoPush, packet)
}

func (p *Player) syncUserSkillSlot() {
	packet := &pb.SyncUserSkillSlotPush{}
	for _, v := range p.skillsSlot {
		packet.SkillsSlot = append(packet.SkillsSlot, v)
	}
	p.pushMsg(protocol.SyncUserSkillSlotPush, packet)
}

func (p *Player) syncParterListPush() {
	packet := &pb.SyncPartnerListPush{}
	for _, v := range p.partners {
		partner := &pb.PartnerInfo{
			Uid:   v.uid,
			CfgId: v.cfgId,
			Level: v.level,
			Star:  v.star,
		}
		partner.Skills = []*pb.SkillInfo{}
		for _, v := range v.skills {
			partner.Skills = append(partner.Skills, &pb.SkillInfo{CfgId: v.CfgId, Cooldown: 0, Advanced: v.AdvancedLevel})
		}

		packet.List = append(packet.List, partner)
	}
	p.pushMsg(protocol.RouteSyncPartnerListPush, packet)
}

// 同步宝箱信息
func (p *Player) SyncUserBox() {
	packet := &pb.SyncUserBoxInfoPush{Box: &pb.Userbox{}}
	packet.Box.Nowlevel = p.box.NowLevel
	// packet.Box.NowOpenLevel = p.box.NowOpenLevel
	// packet.Box.Job = p.box.Job
	// 箱子，这里本来是给他还剩多少，但是因为前端的问题，需要给他时间戳
	if p.box.NextBoxRecoveryTime <= 0 {
		packet.Box.NextBoxRecoveryTime = 0
	} else {
		packet.Box.NextBoxRecoveryTime = time.Now().Unix() + p.box.NextBoxRecoveryTime
	}
	packet.Box.NowOpenRes = make([]*pb.ItemInfo, 0)
	for i := range p.box.NowOpenRes {
		v := &p.box.NowOpenRes[i]
		packet.Box.NowOpenRes = append(packet.Box.NowOpenRes, v)
	}
	p.pushMsg(protocol.RouteSyncUserBoxInfoPush, packet)
}

// 同步宠物信息
func (p *Player) SyncUserPet(needDel int32) {
	packet := &pb.SyncUserPetInfoPush{Pet: []*pb.PetSlotInfo{}, PetRecoveryTime: 0, PetEverHave: []int32{}}
	packet.Pet = []*pb.PetSlotInfo{}
	// 赋值
	for i := range p.pet {
		v := p.pet[i]
		packet.Pet = append(packet.Pet, &pb.PetSlotInfo{
			Uid:     v.PetUid,
			CfgId:   v.PetCfgId,
			Star:    v.Star,
			Level:   v.Level,
			Exp:     v.Exp,
			ExpStar: v.ExpStar,
		})
	}
	checkNum := int64(0)
	// 当前时间+下一次恢复时间
	checkNum = int64(global.GlobalCfg.BaseValue.Petfree.Maxnum) + int64(needDel)
	if p.checkTakeSource(constant.ItemTypePetNormal, checkNum) {
		packet.PetRecoveryTime = 0
	} else {
		// 这里是给前端的时间，就是当前时间加上需要等待的时间
		packet.PetRecoveryTime = int32(time.Now().Unix() + int64(p.nextPetRecoveryTime))
	}
	logger.Infof("SyncUserPetInfoPush PetRecoveryTime:%d, nextPetRecoveryTime:%d", packet.PetRecoveryTime, p.nextPetRecoveryTime)
	packet.PetEverHave = p.petEverHave
	p.pushMsg(protocol.RouteSyncUserPetInfoPush, packet)
}

func (p *Player) SyncUserEquipsInfo() {
	packet := &pb.SyncUserEquipsInfoPush{Equips: []*pb.SlotInfo{}}
	for _, v := range p.equips {
		var Gemstone []int32
		for _, v2 := range v.Gemstone {
			Gemstone = append(Gemstone, int32(v2))
		}
		slot := &pb.SlotInfo{Stars: v.Stars, Enhance: v.Enhance, Item: p.getItemByUid(v.ItemId), Gemstone: Gemstone}
		packet.Equips = append(packet.Equips, slot)
	}
	p.pushMsg(protocol.RouteSyncUserEquipsInfoPush, packet)
}

// 伙伴
func (p *Player) SyncUserPartnerSlot() {
	packet := &pb.PartnerSlotUpdatePush{List: []*pb.PartnerSlotInfo{}}
	for _, v := range p.partnerSlots {
		packet.List = append(packet.List, &pb.PartnerSlotInfo{
			SlotId:     v.SlotId,
			PartnerUid: v.PartnerUid,
			Equips:     p.FillEquipInfo(v.Equips),
		})
	}
	p.pushMsg(protocol.RoutePartnerSlotUpdatePush, packet)
}

func (p *Player) FillEquipInfo(equips [pb.EquipSlot_MaxEquipSlot]Slot) []*pb.SlotInfo {
	eslot := make([]*pb.SlotInfo, 0)
	for _, v := range equips {
		Gemstone := make([]int32, 0)
		for _, v2 := range v.Gemstone {
			Gemstone = append(Gemstone, int32(v2))
		}
		slot := &pb.SlotInfo{Stars: v.Stars, Enhance: v.Enhance, Item: p.getItemByUid(v.ItemId), Gemstone: Gemstone}
		eslot = append(eslot, slot)
	}
	return eslot
}

func (p *Player) online() {
	if p.bCreate {
		p.tasks.init()
		p.AutoDisassembly.ADOpen = false
		p.AutoDisassembly.ADLevelSwitch = global.GlobalCfg.AutoDecompose.LevelSwitch == 1
		p.AutoDisassembly.ADLevel = int32(global.GlobalCfg.AutoDecompose.Level)
		p.AutoDisassembly.ADQualitySwitch = global.GlobalCfg.AutoDecompose.QualitySwitch == 1
		p.AutoDisassembly.ADQuality = int32(global.GlobalCfg.AutoDecompose.Quality)
		p.AutoDisassembly.ADHighCombat = global.GlobalCfg.AutoDecompose.KeepBetterSwitch == 1

		time.AfterFunc(time.Second, func() {
			p.levelGift()
		})
		for _, v := range p.partnerSlots {
			slot := model.PartnerSlot{
				PartnerUid: 0,
				Uid:        p.uid,
				SlotId:     v.SlotId,
			}
			data, err := json.Marshal(v.Equips)
			if err == nil {
				slot.Equips = string(data)
			}
			if err := db.InsertPartnerSlot(&slot); err != nil {
				return
			}
		}

		instance_data := model.InstanceData{
			Uid:               p.uid,
			BattleCurStage:    0,
			BattleRewardStage: 0,
		}
		if err := db.AddInstanceData(&instance_data); err != nil {
			p.logger.Errorf("玩家副本数据同步错误,userId: %d Error=%v", p.uid, err)
		}
		p.PVPDuel.pvpScore = 1000
		p.PVPMelee.gvgScore = 1000
	}
	p.curState = PlayerStatusIdle
	p.syncUserBase()
	p.skillAutoLearn()
	p.SyncUserAdvance()
	p.SyncUserPartnerSlot()

	p.tasks.check()
	p.syncUserSkillSlot()
	p.syncParterListPush()

	p.refreshStats()
	p.currentHP = p.stat(pb.Attribute_MAX_HEALTH)
	p.defaultBuff()
	p.syncBuff()

	db.Online(p.uid)

	p.wellenOnlineTime = 0

	p.dirty(false)
	p.SyncUserPet(0)
	p.SyncUserBox()
	p.bCreate = false
	p.bRobot = false
	p.lastLoginAt = time.Now()
	guildManager.GuildLoginPush(p.uid)
	defaultManager.RankLogin(p.serverID, p.uid)
	// robotMgr.updateOfflinePlayer(p.uid)
	// 登录上报数据
	p.upLoginShushuDate()
	// if err := p.activityMgr.Load(p.uid); err != nil {
	// 	p.logger.Errorf("加载活动信息失败 err: %s", err.Error())
	// }

	p.logger.Infof("玩家上线,uid: %d , name: %s, serverId: %d ", p.uid, p.name, p.serverID)
}

func (p *Player) syncUserDay() {
	p.nowDay = 1
	p.CreateDay = 1
	check, err := db.RoleLoginNum(p.uid, 0, time.Now().Unix())
	if err != nil {
		return
	}
	p.nowDay = len(check)
	RoleFirstLoginNum, err := db.RoleFirstLoginNum(p.uid)
	if err != nil {
		return
	}
	p.CreateDay = RoleFirstLoginNum
}

func (p *Player) upLoginShushuDate() {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	shushuDate["name"] = "role_login"
	shushumain.ReportData(shushuDate)
	shushuUserSetDate := make(map[string]interface{}, 0)
	shushuUserSetDate["current_day"] = p.nowDay
	shushuUserSetDate["tmp_role_id"] = p.uid
	shushuUserSetDate["tmp_account"] = p.accountName
	shushumain.ReportUserSetData(shushuUserSetDate)
}

func (p *Player) dirty(v bool) {
	p.bDirty = v
}

// 异步保存数据库
func (p *Player) asyncSaveDb() {
	if p.curState == PlayerStatusPlaying {
		return
	}
	p.lock.Lock()
	defer p.lock.Unlock()

	async.Run(func() {
		p.syncSaveDb()
	})

	p.dirty(false)
}

// 保存数据库
func (p *Player) syncSaveDb() {
	p.lock.Lock()
	defer p.lock.Unlock()

	u := &model.User{}

	u.Uid = p.Uid()

	u.Energy = p.energy
	u.Recharge = p.recharge
	u.Vip = p.vip
	// u.VipExp = p.vipExp
	u.Level = p.level
	u.Exp = p.exp
	u.Name = p.name
	u.Job = int(p.job)
	u.Sex = p.sex
	u.AccountName = p.accountName
	u.EnergyRecoveryTime = p.energyRecoveryTime.Unix()
	u.DailySoul = int(p.dailySoul)
	u.SessionSoul = int(p.sessionSoul)
	u.OfflineTime = p.offlineTime
	u.PvpScore = int(p.pvpScore)
	u.GvgScore = int(p.gvgScore)

	u.MatchTimesDuel = int(p.PVPDuel.dailyMatchTimes)
	u.BuyMatchNumDuel = int(p.PVPDuel.dailyBuyNum)

	u.MatchTimesMelee = int(p.PVPMelee.dailyMatchTimes)
	u.BuyMatchNumMelee = int(p.PVPMelee.dailyBuyNum)

	u.SoloTowerLayers = int(p.SoloTowerInfo.Layers)
	u.SoloTowerRewardTime = p.SoloTowerInfo.RewardTime.Unix()

	u.FateExp = p.fateExp
	u.FateLevel = p.fateLevel

	u.WorldBossTimes = int(p.dailyWorldBossTimes)
	u.WorldBossDamage = p.totalWorldBossDamage
	u.DayOnlineTime = int64(p.dayOnlineTime)
	u.TotalOnlineTime = int64(p.totalOnlineTime)
	u.VitalityTime = p.vitalityTime
	u.TotalCombat = p.totalCombat
	u.AfkMultiplierID = p.AfkMultiplierID
	u.AfkExpirationAt = p.afkExpirationAt.Unix()
	u.AfkDailyTime = p.afkDailyTime
	data, err := protobuf.NewSerializer().Marshal(p.bag)
	if err == nil {
		u.Bag = data
		if len(u.Bag) == 0 {
			u.Bag = nil
		}
	}
	uBox := &model.UserBox{}
	uBox.Uid = p.Uid()
	uBox.NowLevel = p.box.NowLevel
	uBox.NextBoxRecoveryTime = p.box.NextBoxRecoveryTime
	uBox.BeginAt = p.box.BoxRecoveryTime.Unix()
	uBox.IsAccelerate = p.box.IsAccelerate
	uBox.AccelerateNum = p.box.AccelerateNum
	uBox.CostGold = p.box.CostGold
	uBox.TimeCost = p.box.TimeCost
	// uBox.NowOpenLevel = p.box.NowOpenLevel
	// uBox.Job = p.box.Job
	data, err = json.Marshal(p.box.NowOpenRes)
	if err == nil {
		uBox.NowOpenRes = string(data)
	}
	uPet := &model.UserPet{}
	uPet.Uid = p.Uid()
	if !p.petRecoveryTime.IsZero() {
		uPet.PetRecoveryTime = p.petRecoveryTime.Unix()
	}
	if !p.petRecoveryTimeGold.IsZero() {
		uPet.PetRecoveryTimeGold = p.petRecoveryTimeGold.Unix()
	}
	if !p.petRecoveryTimeExp.IsZero() {
		uPet.PetRecoveryTimeExp = p.petRecoveryTimeExp.Unix()
	}
	uPet.PetExp = p.petExp
	uPet.PetGold = p.petGold
	data, err = json.Marshal(p.pet)
	if err == nil {
		uPet.PetInfo = string(data)
	}
	data, err = json.Marshal(p.petEverHave)
	if err == nil {
		uPet.PetEverHave = string(data)
	}
	data, err = json.Marshal(p.skills)
	if err == nil {
		u.Skill = string(data)
	}

	data, err = json.Marshal(p.equips)
	if err == nil {
		u.Equips = string(data)
	}
	data, err = json.Marshal(p.skillsSlot)
	if err == nil {
		u.SkillSlot = string(data)
	}

	data, err = protobuf.NewSerializer().Marshal(p.tasks.toPB())
	if err == nil {
		u.Task = data
		if len(u.Task) == 0 {
			u.Task = nil
		}
	}

	u.Buff = p.toDbBuff()

	data, err = json.Marshal(p.dailyMaps)
	if err == nil {
		u.DailyMap = string(data)
	}

	data, err = json.Marshal(p.AutoDisassembly)
	if err == nil {
		u.Disassembly = string(data)
	}
	// 代币
	data, err = json.Marshal(p.tokens)
	if err == nil {
		u.Tokens = string(data)
	}
	// 代币消耗
	data, err = json.Marshal(p.tokenDeletes)
	if err == nil {
		u.TokenDeletes = string(data)
	}
	data, err = json.Marshal(p.firstUse)
	if err == nil {
		u.FirstUse = string(data)
	}
	for i := 0; i < int(MaxPartnerNum); i++ {
		p.syncSavePartnerSlotDb(p.partnerSlots[i].PartnerUid, p.partnerSlots[i].SlotId)
	}
	for _, v := range p.partners {
		p.syncSavePartnerDb(v.Uid(), model.Update)
	}

	data, err = json.Marshal(p.turnBasedSlot)
	if err == nil {
		u.TurnBasedSlot = string(data)
	}

	data, err = json.Marshal(p.Guidance)
	if err == nil {
		u.Guidance = string(data)
	}

	if err := db.UpdateUser(u); err != nil {
		p.logger.Errorf("保存角色信息错误, Error=%v", err)
		return
	}
	if err := db.UpdateUserBox(uBox); err != nil {
		p.logger.Errorf("保存用户开箱信息错误, Error=%v", err)
		return
	}
	if err := db.UpdateUserPet(uPet); err != nil {
		p.logger.Errorf("保存用户宠物信息错误, Error=%v", err)
		return
	}
	if p.guildId > 0 {
		uGuild := &model.GuildMember{}
		uGuild.GuildID = p.guildId
		uGuild.UserID = p.uid
		uGuild.RecoveryTimeExp = p.wishRecoveryTime.Unix()
		uGuild.GuildWish = int32(p.guildWish)
		uGuild.GuildContribution = int32(p.guildContribution)
		if err := db.UpdateUserGuild(uGuild); err != nil {
			p.logger.Errorf("保存用户工会信息错误, Error=%v", err)
			return
		}
	}
	if err := p.syncSaveInstanceDataDb(); err != nil {
		p.logger.Errorf("保存玩家实例信息错误, Error=%v", err)
		return
	}

	// 保存活动数据
	// if err := p.activityMgr.Save(); err != nil {
	// 	p.logger.Errorf("保存玩家活动信息错误, Error=%v", err)
	// 	return
	// }

	p.logger.Debugf("保存数据库, name=%s  level=%d", p.name, p.level)
}

// 伙伴相关
func (p *Player) syncSavePartnerSlotDb(puid int64, sid int32) error {
	slot := model.PartnerSlot{
		Uid:        p.uid,
		PartnerUid: puid,
		SlotId:     sid,
	}
	data, err := json.Marshal(p.getPartnerSlotInfo(sid).Equips)
	if err == nil {
		slot.Equips = string(data)
	}

	if err := db.UpdatePartnerSlot(&slot); err != nil {
		return err
	}
	return nil
}

func (p *Player) syncSavePartnerDb(puid int64, optype model.DBOpt) error {
	if p.isRobot() {
		return nil
	}

	info, ok := p.partners[puid]
	if !ok {
		return nil
	}
	switch optype {
	case model.Add:
		partners := model.Partner{
			Uid:        p.uid,
			PartnerUid: puid,
			CfgId:      info.cfgId,
			Level:      info.level,
			Star:       info.star,
		}

		data, err := json.Marshal(info.skills)
		if err == nil {
			partners.Skill = string(data)
		}

		if err := db.InsertPartner(&partners); err != nil {
			return err
		}
	case model.Del:
		partners := model.Partner{
			Uid:        p.uid,
			PartnerUid: puid,
		}
		if err := db.DeletePartner(&partners); err != nil {
			return err
		}
	case model.Update:
		partners := model.Partner{
			Uid:        p.uid,
			PartnerUid: puid,
			Level:      info.level,
			Star:       info.star,
		}
		data, err := json.Marshal(info.skills)
		if err == nil {
			partners.Skill = string(data)
		}
		if err := db.UpdatePartner(&partners); err != nil {
			return err
		}

	}
	return nil
}

func (p *Player) syncSaveInstanceDataDb() error {
	instanceData := model.InstanceData{
		Uid:               p.uid,
		BattleCurStage:    p.BattleMap.BattleId,
		BattleRewardStage: p.BattleMap.RewardStage,

		BossCurStage:      p.BossMap.BattleId,
		BossChallengeTime: p.BossMap.ChallengeTime,

		TrialTowerCurStage: p.TrialTowerMap.BattleId,

		RelicCurStage:      p.RelicMap.BattleId,
		RelicChallengeTime: p.RelicMap.ChallengeTime,

		FightEndCurStage:    p.FightEndMap.BattleId,
		FightEndRefreshTime: p.FightEndMap.RefreshAt.Unix(),
	}
	data, err := json.Marshal(p.RelicMap.Evaluation)
	if err == nil {
		instanceData.RelicEvaluation = string(data)
	}
	data, err = json.Marshal(p.RelicMap.ChapterReward)
	if err == nil {
		instanceData.RelicChapterReward = string(data)
	}

	data, err = json.Marshal(p.FightEndMap.Evaluation)
	if err == nil {
		instanceData.FightEndEvaluation = string(data)
	}

	data, err = json.Marshal(p.ChallengeRecord.Records)
	if err == nil {
		instanceData.ChallengeRecord = string(data)
	}

	if err := db.UpdateInstanceData(&instanceData); err != nil {
		return err
	}
	return nil
}

// 获取道具数量 By CfgID
func (p *Player) getItemCountInBag(id int32) int64 {
	var count int64
	itemCfg, ok := global.ItemCfg[id]
	if !ok {
		return 0
	}

	if itemCfg.MainType == constant.NotInBag {
		count, ok = p.tokens[id]
		if !ok {
			count = 0
		}
	} else {
		for _, item := range p.bag.ItemList {
			if id == item.CfgId {
				count += item.Num
			}
		}
	}

	return count
}

// 包裹剩余格子
func (p *Player) bagLeftCount(itemType pb.ItemType) int {
	if itemType == pb.ItemType_NOT_IN_BAG {
		if len(p.bag.ItemList)-p.getEquipNum() >= global.GlobalCfg.BaseValue.BagNum.TotleNum {
			return 0
		}
		return global.GlobalCfg.BaseValue.BagNum.TotleNum - len(p.bag.ItemList) - p.getEquipNum()
	}

	num := 0
	if itemType == pb.ItemType_EQUIPMENT {
		for _, item := range p.bag.GetItemList() {
			if item.Type == pb.ItemType_EQUIPMENT {
				num++
			}
		}

		if num-p.getEquipNum() >= global.GlobalCfg.BaseValue.BagNum.EquipNum {
			return 0
		}
		return global.GlobalCfg.BaseValue.BagNum.EquipNum - num + p.getEquipNum()
	} else {
		for _, item := range p.bag.GetItemList() {
			if item.Type != pb.ItemType_EQUIPMENT {
				num++
			}
		}

		if num >= global.GlobalCfg.BaseValue.BagNum.ItemNum {
			return 0
		}
		return global.GlobalCfg.BaseValue.BagNum.ItemNum - num
	}
}

func (p *Player) bagEquipLeftCount() int {
	num := 0
	for _, item := range p.bag.GetItemList() {
		if item.Type == pb.ItemType_EQUIPMENT {
			num++
		}
	}

	if num-p.getEquipNum() >= global.GlobalCfg.BaseValue.BagNum.EquipNum {
		return 0
	}
	return global.GlobalCfg.BaseValue.BagNum.EquipNum - num + p.getEquipNum()
}

func (p *Player) bagItemLeftCount() int {
	num := 0
	for _, item := range p.bag.GetItemList() {
		if item.Type != pb.ItemType_EQUIPMENT {
			num++
		}
	}

	if num >= global.GlobalCfg.BaseValue.BagNum.ItemNum {
		return 0
	}
	return global.GlobalCfg.BaseValue.BagNum.ItemNum - num
}

// 批量增加道具检查包裹空间
func (p *Player) checkAddSourceBatch(items []global.IdNum) error {
	var itemcount, equipcount, petCount int
	for _, s := range items {
		if item, err := global.GetItemCfg(s.ID); err == nil {
			if item.MainType == constant.ItemPet {
				petCount += int(s.Num)
			}

			if item.MainType != constant.NotInBag {
				if item.MainType == constant.Equipment {
					equipcount += int(s.Num)
				} else {
					num := s.Num/int32(item.Maxnum) + 1
					itemcount += int(num)
				}
			}
		} else {
			return err
		}
	}
	// p.logger.Debugf("checkAddSourceBatch 添加道具数量: %d , 装备包裹剩余格子: %d 道具包裹剩余格子: %d", len(items), p.bagEquipLeftCount(), p.bagItemLeftCount())

	targetPetNum := p.getEmptyPetSlotNum()
	if targetPetNum < petCount {
		return errutil.ErrPetFull
	}

	if equipcount > p.bagEquipLeftCount() || itemcount > p.bagItemLeftCount() {
		packet := &pb.SystemNotificationPush{Type: pb.Notification_BAG_FULL}
		p.pushMsg(protocol.RouteSystemNotificationPush, packet)
		return errutil.ErrBagFull
	}

	return nil
}

// 检查包裹空间
func (p *Player) checkAddSource(id int32, num int64) bool {
	itemCfg, ok := global.ItemCfg[id]
	if !ok {
		return false
	}

	if itemCfg.MainType == constant.NotInBag {
		return true
	}

	if itemCfg.Maxnum > 0 {
		needBag := num / itemCfg.Maxnum
		if num%itemCfg.Maxnum != 0 {
			needBag++
		}

		if p.bagLeftCount(pb.ItemType(itemCfg.MainType)) < int(needBag) {
			packet := &pb.SystemNotificationPush{Type: pb.Notification_BAG_FULL}
			p.pushMsg(protocol.RouteSystemNotificationPush, packet)
			return false
		}
	}

	return true
}

// 道具数量检查 By CfgID
func (p *Player) checkTakeSource(id int32, num int64) bool {
	itemCfg, ok := global.ItemCfg[id]
	if !ok {
		return false
	}
	num = int64(math.Abs(float64(num)))

	if itemCfg.MainType == constant.NotInBag {
		count, ok := p.tokens[id]
		if !ok {
			count = 0
		}
		if count < num {
			return false
		}

	} else {
		if p.getItemCountInBag(id) < num {
			return false
		}
	}

	return true
}

// 检查道具数量 By CfgID
func (p *Player) checkTakeSourceByUid(uid int64, num int64) bool {
	p.lock.Lock()
	defer p.lock.Unlock()

	for _, item := range p.bag.GetItemList() {
		if item.Uid == uid {
			if item.Num >= num {
				return true
			}
		}
	}
	return false
}

func (p *Player) addToken(id int32, num int64, reason string) {
	if global.IsToken(id) {
		if _, ok := p.tokens[id]; !ok {
			p.tokens[id] = num
		} else {
			p.tokens[id] += num
		}
		if _, ok := p.tokenDeletes[id]; !ok {
			if num < 0 {
				p.tokenDeletes[id] = int64(math.Abs(float64(num)))
			}
		} else {
			if num < 0 {
				p.tokenDeletes[id] += int64(math.Abs(float64(num)))
			}
		}
	}
	switch id {
	case constant.ItemTypeCoin:
		if num < 0 {
			p.tasks.dispatch(pb.TargetType_TOTAL_GOLD_COINS_CONSUMED_REQUIREMENT, 0, int32(math.Abs(float64(num))))
		}

	case constant.ItemTypeSoul:
		if reason == constant.ItemAddPvp {
			p.PVPInfo.dailySoul += int(num)
			p.PVPInfo.sessionSoul += int(num)

			if reason == constant.ItemAddPvp {
				// 更新排行榜
				p.rankChangeTCP(constant.RankTypePvp, int64(p.PVPInfo.dailySoul))
				p.rankChangeTCP(constant.RankTypeSeason, int64(p.PVPInfo.sessionSoul))
			}
			p.tasks.dispatch(pb.TargetType_TOTAL_ACQUIRED_SOULS, 0, int32(num))
		}

	case constant.ItemTypeEnergy:
		p.energy += num
		if p.energy < 0 {
			p.energy = 0
		} else if p.energy > int64(global.GlobalCfg.BaseValue.Energy.Maxvalue) {
			p.energy = int64(global.GlobalCfg.BaseValue.Energy.Maxvalue)
		}

	case constant.ItemTypeExp:
		p.exp += num
		cfg, err := global.GetRoleCfg(int32(p.job), p.level)
		if err == nil {
			if p.exp > cfg.NeedExp*global.GlobalCfg.BaseValue.Expstore.Rate {
				p.exp = cfg.NeedExp * global.GlobalCfg.BaseValue.Expstore.Rate
			}
		}
		if p.exp < 0 {
			p.exp = 0
		}
		if p.getCombatPartnerNum() == 1 { // 只有主角上阵直接升级
			p.levelUp()
		}

	case constant.ItemTypeVipExp:
		p.vipExp += int64(num)
		p.vipLevelUp()

	case constant.ItemTypeTrialVoucher:
		if num > 0 {
			p.tasks.dispatch(pb.TargetType_TOTAL_ACQUIRED_TRIAL_TOKENS, 0, int32(num))
		}

	case constant.ItemTypePVPTimes:
		p.PVPInfo.PVPDuel.dailyBuyNum += int32(num)

	case constant.ItemTypeGVGTimes:
		p.PVPInfo.PVPMelee.dailyBuyNum += int32(num)

	case constant.ItemTypeBossChallengeTimes:
		p.InstanceData.BossMap.ChallengeTime += int32(num)

	case constant.ItemTypeRelicChallengeTimes:
		p.InstanceData.RelicMap.ChallengeTime += int32(num)
	case constant.ItemTypeGuildContribution:
		p.guildContribution += num
		if p.guildContribution < 0 {
			p.guildContribution = 0
		}
	case constant.ItemTypeGuildWish:
		p.guildWish += num
		if p.guildWish < 0 {
			p.guildWish = 0
		}
	}
}

// 增加资源 By CfgID
func (p *Player) addSource(id int32, num int64, reason string) {
	p.lock.Lock()
	defer p.lock.Unlock()

	if !p.checkAddSource(id, num) {
		p.logger.Warnf("添加道具失败,包裹已满 userId %d  itemId: %d  itemNum: %d", p.Uid(), id, num)
		return
	}

	itemCfg, ok := global.ItemCfg[id]
	if !ok {
		p.logger.Warnf("添加道具失败,未找到配置 userId %d  itemId: %d  itemNum: %d", p.Uid(), id, num)
		return
	}

	packet := pb.AddSourcePush{Type: 1, Item: []*pb.ItemInfo{}}

	if itemCfg.MainType == constant.NotInBag {
		p.addToken(id, num, reason)

		packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Num: num})
		p.syncUserBase()
	} else if itemCfg.MainType == constant.ItemPartner {
		for _, par_cfg := range global.PartnerCfg {
			if par_cfg.ItemID == id {
				p.addPartner(par_cfg.Cid)
				break
			}
		}
	} else if itemCfg.MainType == constant.ItemPet {
		for _, pet_cfg := range global.PetCfg {
			if pet_cfg.ItemID == id {
				p.addPet(pet_cfg.ID, 0, 0, 0, 1, reason)
				break
			}
		}
	} else {
		var leftNum int64
		leftNum = num
		for _, item := range p.bag.GetItemList() {
			if item.CfgId == id {
				if item.Num < itemCfg.Maxnum {
					if num <= itemCfg.Maxnum-item.Num {
						item.Num += num
						leftNum = 0
						packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Uid: item.Uid, Num: num})
						break
					} else {
						addNum := itemCfg.Maxnum - item.Num
						leftNum = leftNum - addNum
						item.Num = itemCfg.Maxnum
						packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Uid: item.Uid, Num: addNum})
					}
				}
			}
		}
		for leftNum > 0 {
			// 道具获取时间，如果Timeliness=1，取获取时间当天0点
			getTime := time.Now().Unix()
			if itemCfg.Timeliness.Type == constant.ItemTimelinessShiChang {
				currentTime := time.Now()
				year, month, day := currentTime.Date()
				newTime := time.Date(year, month, day, 0, 0, 0, 0, time.UTC)
				getTime = newTime.Unix()
			}
			if leftNum > itemCfg.Maxnum {
				leftNum -= itemCfg.Maxnum
				itemUid := snowflake.GenID()
				newItem := &pb.ItemInfo{CfgId: id, Uid: itemUid, Num: itemCfg.Maxnum, CreateTime: getTime, Type: pb.ItemType(itemCfg.MainType)}

				// 随机品质&随机属性
				p.randomEquipQuality(newItem)
				p.upgradeRandomAttr(newItem, itemCfg)
				packet.Item = append(packet.Item, newItem)
				p.bag.ItemList = append(p.bag.ItemList, newItem)
			} else {
				itemUid := snowflake.GenID()
				newItem := &pb.ItemInfo{CfgId: id, Uid: itemUid, Num: leftNum, CreateTime: getTime, Type: pb.ItemType(itemCfg.MainType)}

				// 随机品质&随机属性
				p.randomEquipQuality(newItem)
				p.upgradeRandomAttr(newItem, itemCfg)
				packet.Item = append(packet.Item, newItem)
				p.bag.ItemList = append(p.bag.ItemList, newItem)
				leftNum = 0
			}
		}
	}
	if reason != constant.ItemAddAFKBox && reason != constant.ItemAddAFKMonster && reason != constant.ItemAddDisintegrate && reason != constant.ItemAddDrop {
		sourceInfo := make([]global.IdNum, 0)
		sourceInfo = append(sourceInfo, global.IdNum{ID: id, Num: int32(num)})
		p.upAddSourceShushuDate(sourceInfo, reason)
	}
	p.pushMsg(protocol.RouteAddSourcePush, &packet)

	p.dirty(true)

	// p.logger.Infof("addSource 增加道具  ItemCfgID:%d , Num:%d ,Reason:%s", id, num, reason)
}

// 减少资源 By CfgId
func (p *Player) takeSource(id int32, num int64, args ...string) {
	p.lock.Lock()
	defer p.lock.Unlock()

	packet := pb.TakeSourcePush{Item: []*pb.ItemInfo{}}
	sourceInfo := make([]global.IdNum, 0)
	itemCfg, ok := global.ItemCfg[id]
	if !ok {
		return
	}
	if itemCfg.MainType == constant.NotInBag {
		p.addToken(id, -num, "")
		sourceInfo = append(sourceInfo, global.IdNum{ID: id, Num: int32(num)})
		packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Num: num})
		p.syncUserBase()
	} else {
		var leftNum int64
		leftNum = num
		var rmUids []int64
		for _, item := range p.bag.GetItemList() {
			if item.CfgId == id && leftNum > 0 {
				sourceInfo = append(sourceInfo, global.IdNum{ID: item.CfgId, Num: int32(leftNum)})
				if item.Num >= leftNum {
					item.Num -= leftNum
					packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Uid: item.Uid, Num: leftNum})
					if item.Num == 0 {
						rmUids = append(rmUids, item.Uid)
					}
					break
				} else {
					packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Uid: item.Uid, Num: item.Num})
					rmUids = append(rmUids, item.Uid)
					leftNum = leftNum - item.Num
					if leftNum <= 0 {
						break
					}
				}
			}
		}
		if len(rmUids) > 0 {
			newBag := pb.Bag{}
			for _, item := range p.bag.ItemList {
				bFind := false
				for _, v := range rmUids {
					if v == item.Uid {
						bFind = true
						break
					}
				}
				if !bFind {
					newBag.ItemList = append(newBag.ItemList, item)
				}
				bFind = false
			}
			p.bag = &newBag
		}

		p.tasks.dispatch(pb.TargetType_TOTAL_ITEM_CONSUMPTION, itemCfg.ID, int32(num))
	}
	method := ""
	if len(args) > 0 {
		method = args[0]
	}
	if method != constant.ItemDelHomeOpenBox && method != constant.ItemDelGemstone && method != constant.ItemDelDisintegrate {
		p.upTakeSourceShushuDate(sourceInfo, method)
	}

	p.pushMsg(protocol.RouteTakeSourcePush, &packet)
	p.dirty(true)
	// p.logger.Infof("删除道具 ItemCfgID:%d , Num:%d ,Reason:%v", id, num, args)
}

// 减少资源 By Uid
func (p *Player) takeSourceByUid(uid int64, num int64, args ...string) {
	p.lock.Lock()
	defer p.lock.Unlock()

	packet := pb.TakeSourcePush{Item: []*pb.ItemInfo{}}
	sourceInfo := make([]global.IdNum, 0)
	defer p.pushMsg(protocol.RouteTakeSourcePush, &packet)

	for i, item := range p.bag.GetItemList() {
		if item.Uid == uid {
			item.Num -= num
			packet.Item = append(packet.Item, &pb.ItemInfo{Uid: uid, Num: num, CfgId: item.CfgId})
			sourceInfo = append(sourceInfo, global.IdNum{ID: item.CfgId, Num: int32(num)})
			if item.Num == 0 {
				p.bag.ItemList = append(p.bag.ItemList[:i], p.bag.ItemList[i+1:]...)
			}

			p.dirty(true)
			p.logger.Infof("takeSourceByUid 删除道具 ItemCfgID:%d , Num:%d ,Reason:%v", item.CfgId, num, args)
			break
		}
	}
	method := ""
	if len(args) > 0 {
		method = args[0]
	}
	if method != constant.ItemDelHomeOpenBox && method != constant.ItemDelGemstone && method != constant.ItemDelDisintegrate {
		p.upTakeSourceShushuDate(sourceInfo, method)
	}
}

// 批量删除资源
func (p *Player) takeSourceByUidBatch(items []global.UUIdNum, args string) {
	p.lock.Lock()
	defer p.lock.Unlock()

	if len(items) == 0 {
		return
	}
	packet := pb.TakeSourcePush{Item: []*pb.ItemInfo{}}
	defer p.pushMsg(protocol.RouteTakeSourcePush, &packet)
	sourceInfo := make([]global.IdNum, 0)
	for _, delitem := range items {
		for i, item := range p.bag.GetItemList() {
			if item.Uid == delitem.UID {
				sourceInfo = append(sourceInfo, global.IdNum{ID: item.CfgId, Num: int32(delitem.Num)})
				item.Num -= int64(delitem.Num)
				packet.Item = append(packet.Item, &pb.ItemInfo{Uid: delitem.UID, Num: int64(delitem.Num), CfgId: item.CfgId})

				if item.Num <= 0 {
					p.bag.ItemList = append(p.bag.ItemList[:i], p.bag.ItemList[i+1:]...)
				}
				break
			}
		}
	}
	if args != constant.ItemDelHomeOpenBox && args != constant.ItemDelGemstone && args != constant.ItemDelDisintegrate {
		p.upTakeSourceShushuDate(sourceInfo, args)
	}
	p.dirty(true)
	// p.logger.Infof("takeSourceByUidBatch 批量删除道具 Items:%+v ,Reason:%v", items, args)
}

func (p *Player) upTakeSourceShushuDate(sourceInfo []global.IdNum, entrance string) {
	if p.bCreate {
		return
	}
	// 创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	// upBaseInfo的数据合并进去
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	// 在合并点其他的数据
	shushuDate["name"] = "other_item_consume"
	cost_item_info := make([]map[string]interface{}, 0)
	for _, v := range sourceInfo {
		if v.Num > 0 {
			// 查询itemname
			if ItemCfg, err := global.GetItemCfg(v.ID); err == nil {
				cost_item_info = append(cost_item_info, map[string]interface{}{
					"item_id":   ItemCfg.ID,
					"item_name": ItemCfg.Name,
					"item_num":  v.Num,
				})
			}
		}
	}
	shushuDate["cost_item_info"] = cost_item_info
	shushuDate["entrance"] = entrance
	shushumain.ReportData(shushuDate)
}

// 批量增加资源 By CfgID
func (p *Player) addSourceBatch(items []global.IdNum, reason string) []*pb.ItemInfo {
	p.lock.Lock()
	defer p.lock.Unlock()

	if err := p.checkAddSourceBatch(items); err != nil {
		p.logger.Warnf("批量添加道具失败,包裹已满 userId %d  itemIs: %v  ", p.Uid(), items)
		return nil
	}

	if len(items) == 0 {
		return nil
	}
	packet := pb.AddSourcePush{Type: 1, Item: []*pb.ItemInfo{}}
	sourceInfo := make([]global.IdNum, 0)
	for _, additem := range items {
		id := additem.ID
		num := int64(additem.Num)
		itemCfg, ok := global.ItemCfg[id]
		if !ok {
			p.logger.Warnf("添加道具失败,未找到配置 userId %d  itemId: %d  itemNum: %d", p.Uid(), id, num)
			return nil
		}

		if itemCfg.MainType == constant.NotInBag {
			p.addToken(id, num, reason)
			sourceInfo = append(sourceInfo, additem)

			packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Num: num})
			p.syncUserBase()
		} else if itemCfg.MainType == constant.ItemPartner {
			for _, par_cfg := range global.PartnerCfg {
				if par_cfg.ItemID == id {
					p.addPartner(par_cfg.Cid)
					break
				}
			}
		} else if itemCfg.MainType == constant.ItemPet {
			for _, pet_cfg := range global.PetCfg {
				if pet_cfg.ItemID == id {
					p.addPet(pet_cfg.ID, 0, 0, 0, 1, reason)
					break
				}
			}
		} else {
			sourceInfo = append(sourceInfo, additem)
			var leftNum int64
			leftNum = num
			for _, item := range p.bag.GetItemList() {
				if item.CfgId == id {
					if item.Num < itemCfg.Maxnum {
						if leftNum <= itemCfg.Maxnum-item.Num {
							item.Num += leftNum
							leftNum = 0
							packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Uid: item.Uid, Num: num})
							break
						} else {
							addNum := itemCfg.Maxnum - item.Num
							leftNum = leftNum - addNum
							item.Num = itemCfg.Maxnum
							packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: id, Uid: item.Uid, Num: addNum})
						}
					}
				}
			}
			for leftNum > 0 {
				// 道具获取时间，如果Timeliness=1，取获取时间当天0点
				getTime := time.Now().Unix()
				if itemCfg.Timeliness.Type == constant.ItemTimelinessShiChang {
					currentTime := time.Now()
					year, month, day := currentTime.Date()
					newTime := time.Date(year, month, day, 0, 0, 0, 0, time.UTC)
					getTime = newTime.Unix()
				}
				if leftNum > itemCfg.Maxnum {
					leftNum -= itemCfg.Maxnum
					itemUid := snowflake.GenID()
					newItem := &pb.ItemInfo{CfgId: id, Uid: itemUid, Num: itemCfg.Maxnum, CreateTime: getTime, Type: pb.ItemType(itemCfg.MainType)}

					// 随机品质&随机属性
					p.randomEquipQuality(newItem)
					p.upgradeRandomAttr(newItem, itemCfg)
					packet.Item = append(packet.Item, newItem)
					p.bag.ItemList = append(p.bag.ItemList, newItem)
				} else {
					itemUid := snowflake.GenID()
					newItem := &pb.ItemInfo{CfgId: id, Uid: itemUid, Num: leftNum, CreateTime: getTime, Type: pb.ItemType(itemCfg.MainType)}

					// 随机品质&随机属性
					p.randomEquipQuality(newItem)
					p.upgradeRandomAttr(newItem, itemCfg)

					packet.Item = append(packet.Item, newItem)

					// 自动分解
					if reason == constant.ItemAddDrop && itemCfg.MainType == constant.Equipment && p.ADOpen {
						bDecompose := false
						if p.ADLevelSwitch && itemCfg.Levellimit <= int(p.ADLevel) {
							bDecompose = true
						}
						if p.ADQualitySwitch && itemCfg.Quality <= p.ADQuality {
							bDecompose = true
						}
						if p.ADHighCombat && p.calcCombatEquip(newItem) > p.calcCombatEquip(p.getItemByUid(p.equips[itemCfg.Equiptype].ItemId)) {
							bDecompose = false
						}
						if bDecompose {
							// p.logger.Debugf("自动分解 穿戴装备战力 %d  新装备战力 %d ", p.calcCombatEquip(p.getItemByUid(p.equips[itemCfg.Equiptype].ItemId)), p.calcCombatEquip(newItem))
							p.ItemDisassembly(newItem)
							packet.Type = 3
						}
					} else {
						p.bag.ItemList = append(p.bag.ItemList, newItem)
					}

					leftNum = 0
				}
			}
		}
	}
	if reason != constant.ItemAddOpenBox {
		if reason != constant.ItemAddAFKBox && reason != constant.ItemAddAFKMonster && reason != constant.ItemAddDisintegrate && reason != constant.ItemAddDrop {
			p.upAddSourceShushuDate(sourceInfo, reason)
		}
		p.pushMsg(protocol.RouteAddSourcePush, &packet)
	}
	p.dirty(true)

	return packet.Item
	// p.logger.Infof("addSourceBatch 批量增加道具  Items:%+v ,Reason:%s", items, reason)
}

func (p *Player) upAddSourceShushuDate(sourceInfo []global.IdNum, entrance string) {
	if p.bCreate {
		return
	}
	// 创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	// upBaseInfo的数据合并进去
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	// 在合并点其他的数据
	shushuDate["name"] = "other_item_get"
	get_item_info := make([]map[string]interface{}, 0)
	for _, v := range sourceInfo {
		if v.Num > 0 {
			// 查询itemname
			if ItemCfg, err := global.GetItemCfg(v.ID); err == nil {
				get_item_info = append(get_item_info, map[string]interface{}{
					"item_id":   ItemCfg.ID,
					"item_name": ItemCfg.Name,
					"item_num":  v.Num,
				})
			}
		}
	}
	shushuDate["get_item_info"] = get_item_info
	shushuDate["entrance"] = entrance
	shushumain.ReportData(shushuDate)
}

// 获取包裹道具信息 By Uid
func (p *Player) getItemByUid(uid int64) *pb.ItemInfo {
	if uid == 0 {
		return nil
	}

	for _, item := range p.bag.GetItemList() {
		if item.Uid == uid {
			return item
		}
	}
	return nil
}

// 获取包裹道具信息 By configId
func (p *Player) getItemByCfgId(cfgId int32) *pb.ItemInfo {
	if cfgId == 0 {
		return nil
	}

	for _, item := range p.bag.GetItemList() {
		if item.CfgId == cfgId {
			return item
		}
	}
	return nil
}

func (p *Player) newDay() {
	// 新的一周刷新试练塔层数
	// 判断是否新的一周（以周一为一周的开始）
	now := time.Now()
	year, week := now.ISOWeek()
	lastLoginYear, lastLoginWeek := p.lastLoginAt.ISOWeek()
	if year != lastLoginYear || week != lastLoginWeek {
		// 新的一周，重置试炼塔层数
		p.TrialTowerMap.BattleId = 0
	}
	// 查询他是第几天
	p.syncUserDay()
	if !p.bCreate {
		p.tasks.reset()
	}

	p.PVPInfo.PVPDuel.dailyBuyNum = 0
	p.PVPInfo.PVPDuel.dailyMatchTimes = 0
	p.PVPInfo.dailySoul = 0
	if !matchManager.sessionOpen {
		p.PVPInfo.sessionSoul = 0
	}
	p.PVPInfo.PVPMelee.dailyBuyNum = 0
	p.PVPInfo.PVPMelee.dailyMatchTimes = 0

	p.dailyWorldBossTimes = 0
	p.totalWorldBossDamage = 0
	p.dayOnlineTime = 0
	p.wellenOnlineTime = 0
	p.vitalityTime = 0
	for i := range p.dailyMaps {
		p.dailyMaps[i].ChallengeTime = 0
	}
	p.petGold = 0
	p.petExp = 0
	p.InstanceData.BossMap.ChallengeTime = global.GetCopySetCfg(constant.BossMap)
	p.InstanceData.RelicMap.ChallengeTime = global.GetCopySetCfg(constant.RelicMap)
	p.afkDailyTime = 0
	p.AfkMultiplierID = -1 // 每天重置为-1,代表前端需要弹窗
	// 12点还在线，就算是零点登陆了
	// 插入登陆记录
	device := protocol.Device{
		IP:     p.ip,
		Remote: p.ip,
	}
	db.InsertRoleLoginLog(p.uid, device)
	//
}

func (p *Player) update() {
	p.lock.Lock()
	defer p.lock.Unlock()
	nowTime := time.Now().Unix()
	// 不在线，不回复
	if p.curState > PlayerStatusDisconnect {
		return
	}
	// 体力恢复检测
	if p.nextEnergyRecoveryTime > 0 {
		p.nextEnergyRecoveryTime--
	}
	// 体力恢复速度加成,换算成秒
	timeinterval := int(float64(global.GlobalCfg.BaseValue.Energy.Timeinterval*60) * (1 - float64(p.stat(pb.Attribute_ENERGY_RECOVERY_SPEED_PERCENTAGE))/BaseRate))
	if p.energy < int64(global.GlobalCfg.BaseValue.Energy.Fullvalue) {
		if time.Now().After(p.energyRecoveryTime.Add(time.Duration(timeinterval) * time.Second)) {
			p.energy++
			if p.energy > int64(global.GlobalCfg.BaseValue.Energy.Maxvalue) {
				p.energy = int64(global.GlobalCfg.BaseValue.Energy.Maxvalue)
				p.nextEnergyRecoveryTime = 0
			} else {
				p.nextEnergyRecoveryTime = timeinterval
			}
			p.energyRecoveryTime = time.Now()
			p.syncUserBase()
		}
	}

	if p.guildId > 0 {
		// 祈愿点恢复检测
		if p.nextWishRecoveryTime > 0 {
			p.nextWishRecoveryTime--
		}
		// 祈愿点恢复速度加成,换算成秒
		wishTimeInterval := int(global.GlobalCfg.Guild.WishValue.RefreshTime * 60)
		if p.checkTakeSource(constant.ItemTypeGuildWish, int64(global.GlobalCfg.Guild.WishValue.MaxValue)) {
			p.nextWishRecoveryTime = wishTimeInterval
			p.wishRecoveryTime = time.Now()
		} else {
			if p.wishRecoveryTime.IsZero() {
				p.nextWishRecoveryTime = wishTimeInterval
				p.wishRecoveryTime = time.Now()
				p.syncUserBase()
			} else {
				if time.Now().After(p.wishRecoveryTime.Add(time.Duration(wishTimeInterval) * time.Second)) {
					go p.addSource(constant.ItemTypeGuildWish, 1, constant.ItemAddGuildWish)
					p.nextWishRecoveryTime = wishTimeInterval
					p.wishRecoveryTime = time.Now()
					p.syncUserBase()
				}
			}
		}

	}
	// 宠物恢复检测
	if p.nextPetRecoveryTime > 0 {
		p.nextPetRecoveryTime--
	}

	// 宠物恢复速度加成,换算成秒//体力恢复速度加成,换算成秒
	pettimeinterval := int(global.GlobalCfg.BaseValue.Petfree.Recovertime * 60)
	// 查询背包里面的宠物蛋数目,如果大于设置的数目就不回复了
	if p.checkTakeSource(constant.ItemTypePetNormal, int64(global.GlobalCfg.BaseValue.Petfree.Maxnum)) {
		p.nextPetRecoveryTime = pettimeinterval
		p.petRecoveryTime = time.Now()
	} else {
		if time.Now().After(p.petRecoveryTime.Add(time.Duration(pettimeinterval) * time.Second)) {
			go p.addSource(constant.ItemTypePetNormal, 1, constant.ItemAddPetAuth)
			p.nextPetRecoveryTime = pettimeinterval
			p.petRecoveryTime = time.Now()
			p.SyncUserPet(-1)
		}
	}

	if len(p.pet) > 0 {
		if p.pet[0].PetCfgId != 0 {
			petInfo := global.GetPetCfg(p.pet[0].PetCfgId)
			if petInfo != nil && len(petInfo.OfflineAward) > 0 {
				petGoldNum := int32(0)
				petGoldGap := int32(0)
				petGoldMax := int32(0)
				petExpNum := int32(0)
				petExpGap := int32(0)
				petExpMax := int32(0)
				for _, v := range petInfo.OfflineAward {
					if v.Key == p.pet[0].Star {
						for _, v2 := range v.Values {
							if v2.ID == constant.ItemTypeCoin {
								petGoldNum = v2.Num
								petGoldGap = int32(v2.Gap)
								petGoldMax = int32(v2.Max)
							}
							if v2.ID == constant.ItemTypeExp {
								petExpNum = v2.Num
								petExpGap = int32(v2.Gap)
								petExpMax = int32(v2.Max)
							}
						}
					}
				}
				// log.Debug("", p.petGold, "-----------", p.petRecoveryTimeGold, p.petExp, "-----------", p.petRecoveryTimeExp)
				// log.Debug("", p.pet[0].Star, p.petExp, "-----------", petGoldNum, "---", petGoldGap, "---", petGoldMax, "---", petExpNum, "---", petExpGap, "---", petExpMax)
				if petGoldNum > 0 {
					// 宠物金币恢复检测
					if p.nextPetRecoveryTimeGold > 0 {
						p.nextPetRecoveryTimeGold--
					}
					// 宠物金币速度加成,换算成秒
					timeintervalgold := int(float64(petGoldGap))
					if p.petGold < petGoldMax {
						if time.Now().After(p.petRecoveryTimeGold.Add(time.Duration(timeintervalgold) * time.Second)) {
							if p.petGold+petGoldNum > petGoldMax {
								p.petGold = petGoldMax
								p.nextPetRecoveryTimeGold = 0
								if petGoldMax-p.petGold > 0 {
									go p.addSource(constant.ItemTypeCoin, int64(petGoldMax-p.petGold), constant.ItemAddPetGold)
								}
							} else {
								p.petGold += petGoldNum
								p.nextPetRecoveryTimeGold = timeintervalgold
								if petGoldNum > 0 {
									go p.addSource(constant.ItemTypeCoin, int64(petGoldNum), constant.ItemAddPetGold)
								}
							}
							p.petRecoveryTimeGold = time.Now()
							p.syncUserBase()
						}
					}
				}

				if petExpNum > 0 {
					// 宠物经验恢复检测
					if p.nextPetRecoveryTimeExp > 0 {
						p.nextPetRecoveryTimeExp--
					}
					// 宠物经验速度加成,换算成秒
					timeintervalexp := int(float64(petExpGap))
					if p.petExp < petExpMax {
						if time.Now().After(p.petRecoveryTimeExp.Add(time.Duration(timeintervalexp) * time.Second)) {
							if p.petExp+petExpNum > petExpMax {
								p.petExp = petExpMax
								p.nextPetRecoveryTimeExp = 0
								if petExpMax-p.petExp > 0 {
									go p.addSource(constant.ItemTypeExp, int64(petExpMax-p.petExp), constant.ItemAddPetExp)
								}
							} else {
								p.petExp += petExpNum
								p.nextPetRecoveryTimeExp = timeintervalexp
								if petExpNum > 0 {
									go p.addSource(constant.ItemTypeExp, int64(petExpNum), constant.ItemAddPetExp)
								}
							}
							p.petRecoveryTimeExp = time.Now()
							p.syncUserBase()
						}
					}
				}

			}

		}
	}

	if p.box.NextBoxRecoveryTime > 0 {
		p.box.NextBoxRecoveryTime--
		p.box.TimeCost++
	} else {
		if p.box.NextBoxRecoveryTime == 0 {
			// 宝箱升级
			p.box.NowLevel++
			p.box.NextBoxRecoveryTime--
			// 发宝箱通知消息给前端
			// p.upBoxShushuDate(is_accelerate,)
			p.tasks.dispatch(pb.TargetType_CHEST_LEVEL_UPGRADED, 0, p.box.NowLevel)
			p.SyncUserBox()
			p.upBoxShushuDate(p.box)

		}
	}
	p.offlineTime = nowTime

	// 在线时长累加
	p.dayOnlineTime++
	p.totalOnlineTime++
	p.wellenOnlineTime++
	// 超过阈值就没有奖励了
	// 查询vip对应的活力分数
	vitality := int64(0)
	if p.vip > 0 {
		if vipCfg := global.GetVipCfg(p.vip); vipCfg != nil {
			vitality += int64(vipCfg.Energyadd) * 60
		}
	}
	// 查询特权卡对应的活力分数
	if len(p.privilege) > 0 {
		for _, v := range p.privilege {
			// 查询特权卡对应的活力分数
			if privilegeCfg := global.GetPrivilegeCfg(v.Pid); privilegeCfg != nil {
				vitality += int64(privilegeCfg.Energystate) * 60
			}
		}
	}
	// go p.openbox(3)
	// todo vip

	if p.vitalityTime < int64(global.GlobalCfg.BaseValue.Onlinebattle.Basetime)+vitality {
		p.vitalityTime++
		// 每隔一段时间发奖励
		if p.vitalityTime%int64(global.GlobalCfg.BaseValue.Onlinebattle.Triggertime) == 0 {
			// 随机
			randomType, id, randomReward := p.randomReward(p.level)
			packet := &pb.HomeAutoEncounterPush{
				Type:     randomType,
				Id:       id,
				ItemInfo: make([]*pb.ItemInfo, 0),
			}

			if len(randomReward) > 0 {
				var reason string
				if randomType == constant.MaintriggerMonster {
					reason = constant.ItemAddAFKMonster
				}
				if randomType == constant.MaintriggerNormal {
					reason = constant.ItemAddAFKBox
				}
				for _, v := range randomReward {
					packet.ItemInfo = append(packet.ItemInfo, &pb.ItemInfo{CfgId: v.ID, Num: int64(v.Num)})
				}

				// 延迟添加
				tmpReward := make([]global.IdNum, 0)
				for _, v := range randomReward {
					getItemCfg, ok := global.ItemCfg[v.ID]
					if !ok {
						continue
					}
					// 如果是使用道具
					if getItemCfg.UserNow > 0 {
						res, err := p.itemAutoUse(v.ID, v.Num)
						if err != nil {
							tmpReward = append(tmpReward, global.IdNum{ID: v.ID, Num: int32(v.Num)})
							// 使用失败
							continue
						}
						for _, v2 := range res {
							tmpReward = append(tmpReward, global.IdNum{ID: v2.ID, Num: int32(v2.Num)})
						}
					} else {
						tmpReward = append(tmpReward, global.IdNum{ID: v.ID, Num: int32(v.Num)})
					}
				}

				var delayTime time.Duration
				delayTime = time.Millisecond * 500
				if reason == constant.ItemAddAFKMonster {
					delayTime = time.Duration(math.Floor(float64(5)/float64(p.getCombatPartnerNum()))*1.2) * time.Second
				}
				// 告知前端的奖励
				err := p.checkAddSourceBatch(tmpReward)
				if err == nil {
					p.pushMsg(protocol.RouteHomeAutoEncounterPush, packet)
				}

				time.AfterFunc(time.Duration(delayTime), func() {
					// 检查背包有没有满
					if err == nil {
						p.addSourceBatch(tmpReward, reason)
					} else {
						attachment := []protocol.Attachment{}
						for _, v := range tmpReward {
							attachment = append(attachment, protocol.Attachment{UserPropsId: int64(v.ID), Num: int64(v.Num)})
						}
						p.SendMail(constant.ItemAddDailymap, attachment)
					}
				})
			}
		}
	}
	// 时间任务检测
	p.tasks.dispatch(pb.TargetType_TIME_RANGE, 0, 0)
	// 特权卡
	newPrivilege := []protocol.PrivilegeDetailTCP{}
	for _, v := range p.privilege {
		if time.Now().After(v.EndTime) {
			continue
		}
		newPrivilege = append(newPrivilege, v)
	}
	if len(p.privilege) != len(newPrivilege) {
		p.privilege = newPrivilege
	}

	// 游戏内玩家,buff由房间goroutine处理
	if p.desk == nil {
		p.processBuff()
		p.processHp()
	}
}

func (p *Player) itemAutoUse(CfgId int32, useNum int32) ([]global.IdNum, error) {
	reward := []global.IdNum{}
	if itemUseCfg, ok := global.ItemUseCfg[CfgId]; ok {
		// 检查使用道具的上限
		for _, v := range itemUseCfg.GiveGoods {
			if v.ID == constant.ItemTypeEnergy {
				if p.energy >= int64(global.GlobalCfg.BaseValue.Energy.Maxvalue) {
					return nil, errutil.ErrEnergyFull
				} else {
					needEnergy := int64(global.GlobalCfg.BaseValue.Energy.Maxvalue) - p.energy
					needNum := int32(math.Ceil(float64(needEnergy) / float64(v.Num)))
					if useNum > needNum {
						useNum = needNum
					}
					break
				}
			}
		}

		jobreward := []global.IdNumJobWeight{}
		jobrewardweight := []global.IdNumJobWeight{}
		if len(itemUseCfg.GiveGoods) > 0 {
			for _, gg := range itemUseCfg.GiveGoods {
				if p.job == constant.MowJob(gg.Job) || gg.Job == 0 {
					jobreward = append(jobreward, gg)
				}
			}
		}
		var weight int32
		for _, jr := range jobreward {
			weight += jr.Weight
			jobrewardweight = append(jobrewardweight, global.IdNumJobWeight{ID: jr.ID, Num: jr.Num, Job: jr.Job, Weight: int32(weight)})
		}
		for i := 0; i < int(useNum); i++ {
			tmpReward := []global.IdNumJobWeight{}
			if weight == 0 {
				tmpReward = jobrewardweight
			} else {
				w := global.RandIn(int(weight))
				for _, jrw := range jobrewardweight {
					if w < int(jrw.Weight) {
						tmpReward = append(tmpReward, global.IdNumJobWeight{ID: jrw.ID, Num: jrw.Num, Job: jrw.Job, Weight: weight})
						break
					}
				}
			}

			for _, tr := range tmpReward {
				bFind := false
				for k, r := range reward {
					if r.ID == tr.ID {
						if rewardCfg, err := global.GetItemCfg(r.ID); err == nil {
							if rewardCfg.Maxnum > 1 {
								reward[k].Num += tr.Num
								bFind = true
							}
						}
						break
					}
				}
				if !bFind {
					reward = append(reward, global.IdNum{ID: tr.ID, Num: tr.Num})
				}
			}

		}
	}
	return reward, nil
}

// 给到随机的奖励
func (p *Player) randomReward(level int32) (returnType int32, id int32, rewards []global.IdNum) {
	createCfg := global.MaintriggerCfg
	if len(createCfg) == 0 {
		return constant.MaintriggerType, 0, []global.IdNum{}
	}
	for _, v := range createCfg {
		for _, v1 := range v.Level {
			if level >= v1.MinLevel && level <= v1.MaxLevel {
				var rate int32
				for _, v2 := range v.MonsterIds {
					rate += v2.Rate
				}
				weight := global.RandIn(int(rate))
				DemoRandom := global.MonsterIds{}
				beginPriority := 0
				for _, v3 := range v.MonsterIds {
					if beginPriority+int(v3.Rate) >= weight {
						DemoRandom = v3
						break
					}
					beginPriority += int(v3.Rate)
				}
				// 不是怪物就直接给他
				if DemoRandom.Type == constant.MaintriggerNormal {
					return constant.MaintriggerNormal, DemoRandom.Id, []global.IdNum{{ID: DemoRandom.Id, Num: DemoRandom.Num}}
				}
				if DemoRandom.Type == constant.MaintriggerMonster {
					// 是怪物就给他怪物的奖励
					// log.Debug("怪物", DemoRandom.Id, DemoRandom.Num)
					dropInfo := monsterMgr.MonsterDorp(DemoRandom.Id, DemoRandom.Num)
					// log.Debug("怪物", dropInfo)
					return constant.MaintriggerMonster, DemoRandom.Id, dropInfo
				}
				return constant.MaintriggerType, 0, []global.IdNum{}
			} else {
				continue
			}
		}
	}
	return constant.MaintriggerType, 0, []global.IdNum{}
}

func (p *Player) upBoxShushuDate(boxinfo BoxInfo) {
	// 创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	// upBaseInfo的数据合并进去
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	// 在合并点其他的数据
	shushuDate["name"] = "box_level_up"
	shushuDate["box_level_before"] = boxinfo.NowLevel - 1
	shushuDate["box_level_after"] = boxinfo.NowLevel
	if boxinfo.IsAccelerate > 0 {
		shushuDate["is_accelerate"] = true
	} else {
		shushuDate["is_accelerate"] = false
	}
	shushuDate["accelerate_num"] = boxinfo.AccelerateNum
	shushuDate["cost_gold"] = boxinfo.CostGold
	shushuDate["time_cost"] = boxinfo.TimeCost
	shushumain.ReportData(shushuDate)

	shushuUserSetDate := make(map[string]interface{}, 0)
	shushuUserSetDate["current_box_level"] = boxinfo.NowLevel
	shushuUserSetDate["tmp_role_id"] = p.uid
	shushuUserSetDate["tmp_account"] = p.accountName
	shushumain.ReportUserSetData(shushuUserSetDate)
}

func (p *Player) levelUp() bool {
	if p.level >= global.GlobalCfg.BaseValue.Rolelevel.Maxlevel {
		return false
	}

	cfg, err := global.GetRoleCfg(int32(p.job), p.level)
	if err != nil {
		p.logger.Errorf("升级失败 userId: %d,  err: %s", p.uid, err.Error())
		return false
	}

	var bUpgrade bool
	if p.getCombatPartnerNum() == 1 {
		for p.exp >= cfg.NeedExp {
			p.exp -= cfg.NeedExp
			if p.exp <= 0 {
				p.exp = 0
			}

			cfg, err = global.GetRoleCfg(int32(p.job), p.level+1)
			if err != nil {
				return false
			}
			p.level++
			p.levelGift()
			bUpgrade = true
		}
	} else {
		if p.exp < cfg.NeedExp {
			return false
		}
		p.exp -= cfg.NeedExp
		if p.exp <= 0 {
			p.exp = 0
		}
		p.level++
		p.levelGift()
		bUpgrade = true
	}
	oldtotalCombat := p.totalCombat
	if bUpgrade {

		// p.logger.Debugf("升级 userId: %d,  level: %d  vip: %d  exp: %d", p.uid, p.level, p.vip, p.exp)
		p.refreshStats()
		newtotalCombat := p.totalCombat
		p.upMyLevelShushuDate(p.level, newtotalCombat-oldtotalCombat)
		p.currentHP = p.GetMaxHP()
		p.stats.setStats(pb.Attribute_HEALTH, p.currentHP)
		p.skillAutoLearn()

		p.pushMsg(protocol.RouteLevelUpPush, &pb.LevelUpPush{NewLevel: p.level, Uid: p.uid})
		if p.desk != nil {
			p.desk.Broadcast(protocol.RouteLevelUpPush, &pb.LevelUpPush{NewLevel: p.level, Uid: p.uid})
		}
		// 更新排行与任务
		p.rankChangeTCP(constant.RankTypeLevel, int64(p.level))
		p.tasks.dispatch(pb.TargetType_ROLE_LEVEL_REQUIREMENT, 0, p.level)

		num := int32(0)
		for _, v := range global.GlobalCfg.Openslot.IDLevel {
			if p.level >= v.Needlevel {
				num++
			}
		}
		p.tasks.dispatch(pb.TargetType_UNLOCK_PARTNER_SLOT, 0, num)

		p.tasks.check()
	}
	return true
}

func (p *Player) upMyLevelShushuDate(level int32, combat int32) {
	// 创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	shushuUserSetDate := make(map[string]interface{}, 0)
	// upBaseInfo的数据合并进去
	// baseInfo := p.ToMapForShushu()
	// for k, v := range baseInfo {
	// 	shushuDate[k] = v
	// 	shushuUserSetDate[k] = v
	// }
	// 在合并点其他的数据
	shushuDate["name"] = "role_level_upgrade"
	shushuDate["level_before"] = level - 1
	shushuDate["level_after"] = level
	shushuDate["role_job"] = constant.JobName[p.job]
	shushuDate["role_power"] = int32(p.combat)
	shushuDate["role_power_up"] = combat
	shushumain.ReportData(shushuDate)

	shushuUserSetDate["current_level"] = level
	shushuUserSetDate["tmp_role_id"] = p.uid
	shushuUserSetDate["tmp_account"] = p.accountName
	shushumain.ReportUserSetData(shushuUserSetDate)
}

// vip升级
func (p *Player) vipLevelUp() {
	cfg := global.GetVipCfg(p.vip + 1)
	if cfg == nil {
		p.logger.Errorf("vip升级失败 userId: %d", p.uid)
		return
	}
	if p.vip >= int32(len(global.VIPCfg)) {
		return
	}
	var bUpgrade bool
	delExp := int64(0)
	vip := p.getItemCountInBag(constant.ItemTypeVipExp)
	for vip >= int64(cfg.NeedExp) {
		delExp += int64(cfg.NeedExp)
		vip -= int64(cfg.NeedExp)
		bUpgrade = true
		p.vip++
		// Check if VIP level has reached the maximum
		if p.vip >= int32(len(global.VIPCfg)) {
			break
		}
		// Update the required experience for the next VIP level
		cfg = global.GetVipCfg(p.vip + 1)
		if cfg == nil {
			break
		}
	}
	if bUpgrade {
		p.refreshStats()
		// 上报
		p.upVipShushuDate(p.vip)
		p.tasks.dispatch(pb.TargetType_VIP_LEVEL, 0, p.vip)
		p.tasks.check()
		go p.takeSource(constant.ItemTypeVipExp, delExp, constant.ItemDelVipExp)
	}
	// p.syncUserBase()
}

func (p *Player) upVipShushuDate(vip int32) {
	// 创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	shushuUserSetDate := make(map[string]interface{}, 0)
	// upBaseInfo的数据合并进去
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
		shushuUserSetDate[k] = v
	}
	// 在合并点其他的数据
	shushuDate["name"] = "vip_upgrad"
	shushuDate["vip_before"] = vip - 1
	shushuDate["vip_after"] = vip
	shushumain.ReportData(shushuDate)
	shushuUserSetDate["current_vip"] = vip
	shushuUserSetDate["tmp_role_id"] = p.uid
	shushuUserSetDate["tmp_account"] = p.accountName
	shushumain.ReportUserSetData(shushuUserSetDate)
}

func (p *Player) setDesk(d *Desk) {
	if d == nil {
		p.logger.Error("桌号为空")
		return
	}

	p.desk = d
	p.roomId = d.cfgID

	p.logger = log.WithFields(log.Fields{fieldDesk: p.desk.deskID, fieldPlayer: p.Uid()})
}

func (p *Player) bindSession(s *session.Session) {
	p.session = s
	p.session.Set(kCurPlayer, p)
}

func (p *Player) removeSession() {
	if p.session != nil {
		p.session.Remove(kCurPlayer)
		p.session = nil
	}
}

func (p *Player) setPlayerStatus(s PlayerStatus) {
	p.curState = s
}

func (p *Player) isRobot() bool {
	return p.bRobot
}

func (p *Player) pushMsg(route string, v interface{}) error {
	if p.isRobot() {
		return nil
	}

	if p.curState == PlayerStatusOffLine || p.curState == PlayerStatusDisconnect {
		return nil
	}

	if p.session == nil {
		p.logger.Errorf("pushMsg Error ErrPlayerNotFound ,route:%s msg:%v", route, v)
		return errutil.ErrPlayerNotFound
	}

	if err := p.session.Push(route, v); err != nil {
		p.logger.Errorf("pushMsg Error:%s ,route:%s msg:%v", err.Error(), route, v)
		return err
	}
	return nil
}

func (p *Player) reset() {
	p.lock.Lock()
	defer p.lock.Unlock()

	p.desk = nil
	p.target = nil
	p.expirationTime = time.Now()
	p.dead = false
	p.roomId = 0
	p.setPlayerStatus(PlayerStatusIdle)
	p.stats.setStats(pb.Attribute_HEALTH, p.stats.getStats(pb.Attribute_MAX_HEALTH))

	p.currentHP = p.GetMaxHP()
	p.displayHP = p.currentHP
	p.reviveTimes = 0
	p.autoBuyInstanceTime = false

	p.resetBuff()

	for _, v := range p.partnerSlots {
		if v == nil {
			continue
		}
		if partner := p.getPartnerInfoBySlotId(v.SlotId); partner != nil {
			partner.reset()
		}
	}

	p.logger = log.WithField(fieldPlayer, p.uid)
}

// 学习技能
func (p *Player) skillAutoLearn() {
	for k, v := range global.SkillCfg {
		if v.SkillProfession != int32(p.job) {
			continue
		}

		if s := p.getSkill(k); s != nil {
			continue
		}

		bLearn := false
		for _, skill := range p.skills {
			if skill.CfgId/100 == v.SkillID {
				bLearn = true
				break
			}
		}
		if bLearn {
			continue
		}

		if v.RoleLevelLimit <= p.level && v.Level <= 1 {
			p.skills = append(p.skills, &PlayerSkill{CfgId: v.UID, Cooldown: time.Now(), AdvancedLevel: 0})
			if v.Level > 0 && v.ActiveorPassive != PassiveSkill {
				for i, v2 := range p.skillsSlot { // 主动技能学习后自动装备到技能槽位
					if v2 == 0 {
						// 如果是特殊技能，需要放到第一个槽位
						if v.SType == constant.SkillTypeCareerSpecial {
							p.skillsSlot[constant.SkillSlotType] = v.UID
						} else {
							if i == int(constant.SkillSlotType) {
								continue
							}
							p.skillsSlot[i] = v.UID
						}
						break
					}
				}
				// 技能
				skillslot := make([]int32, 0)
				for _, v1 := range p.skillsSlot {
					skillslot = append(skillslot, v1)
				}
				p.pushMsg(protocol.RouteNewSkill, &pb.NewSkillPush{Skill: &pb.SkillInfo{CfgId: v.UID, Cooldown: 0, Advanced: 0}, SkillsSlot: skillslot})
				p.tasks.dispatch(pb.TargetType_SKILL_UPGRADING_REQUIREMENT, v.UID, v.Level)

				p.refreshStats()
			}
		}
	}
}

func (p *Player) addSkill(skillId int32) {
	if s := p.getSkill(skillId); s != nil {
		return
	}

	cfg, err := global.GetSkillCfg(skillId)
	if err != nil {
		return
	}

	if cfg.SkillProfession != int32(p.job) {
		return
	}

	p.skills = append(p.skills, &PlayerSkill{CfgId: cfg.UID, Cooldown: time.Now(), Distance: cfg.MaxDistance})
}

func (p *Player) getSkill(cfgID int32) *PlayerSkill {
	for _, s := range p.skills {
		if s.CfgId == cfgID {
			return s
		}
	}

	return nil
}

func (p *Player) getSkillex(cfgID int32) (*PlayerSkill, int64) {
	for _, s := range p.skills {
		if s.CfgId == cfgID {
			return s, p.uid
		}
	}
	for _, v := range p.partnerSlots {
		if partner := p.getPartnerInfoBySlotId(v.SlotId); partner != nil {
			if skill := partner.getSkill(cfgID); skill != nil {
				return skill, partner.uid
			}
		}
	}
	return nil, 0
}

func (p *Player) die(object IMapObject) {
	p.dead = true
	p.deadTime = time.Now()
	p.target = nil

	p.reviveTime = time.Now().Add(1500 * time.Millisecond)
	if p.desk != nil && p.desk.roomCfg.Roomtype == constant.GuildMap {
		p.reviveTime = time.Now().Add(20 * 1000 * time.Millisecond)
	}

	packet := pb.ObjectDiePush{ObjectId: p.uid, ReviveTimes: int32(p.reviveTimes), CanRevive: true, Countdown: 60, ReviveTip: false}

	if p.reviveTimes >= len(global.GlobalCfg.Revive) {
		packet.CanRevive = false
		packet.Countdown = 6
	}

	p.resetBuff()
	p.SetObState(ObjectActionDead)

	// 没有复活技能，全都得死
	allDie := true
	if skill, obj := p.getSlotSkillByType(constant.SkillTypeReborn); skill != nil && obj != nil {
		if !obj.dead && time.Now().After(skill.Cooldown) {
			allDie = false
		}
	}
	if buff := p.getBuffByType(constant.BuffReviveOnce); buff != nil {
		allDie = false
	}

	if allDie {
		for _, v := range p.partnerSlots {
			if v == nil {
				continue
			}
			if partner := p.getPartnerInfoBySlotId(v.SlotId); partner != nil {
				partner.die(object)
			}
		}
	}

	packet.ReviveTip = allDie
	if p.desk != nil {
		p.desk.Broadcast(protocol.RouteObjectDiePush, &packet)
		// 混战模式记录击杀人数
		// if p.desk.matchUid > 0 && p.desk.MatchInfo != nil {
		// 	if p.desk.MatchInfo.mtype == pb.MatchType_FREE_FOR_ALL {
		// 		for i, mp := range p.desk.MatchInfo.mplayers {
		// 			if mp.uid == object.Uid() {
		// 				p.desk.MatchInfo.mplayers[i].killNum++
		// 				p.desk.MatchInfo.dirty = true
		// 			}
		// 		}
		// 	}
		// }
	} else {
		p.logger.Error("玩家死亡 , 桌子已经被清理")
	}
	// p.logger.Debugf("玩家 [%s] 死亡！！！", p.name)
}

func (p *Player) revive() bool {
	bornLocation := &p.desk.mapCfg.BornPoints[global.RandIn(len(p.desk.mapCfg.BornPoints))]
	cell := p.desk.mapInfo.getAvailableCell(bornLocation, 2)
	if cell == nil {
		logger.Warningf("复活失败, 未找到出生点 uid=%d  name=%s mapId=%d", p.uid, p.name, p.desk.mapId)
		return false
	}
	p.SetLocation(global.ToPointF(&cell.location))
	p.SetCell(cell)
	p.dead = false
	p.reviveTimes++
	p.currentHP = p.stat(pb.Attribute_MAX_HEALTH)
	p.displayHP = p.currentHP
	p.stats.setStats(pb.Attribute_HEALTH, p.currentHP)
	p.resetBuff()
	p.desk.Broadcast(protocol.RouteRevivePush, &pb.RevivePush{
		Uid:       p.uid,
		Location:  p.location,
		Direction: p.direction,
		CurHp:     p.currentHP,
	})

	for _, v := range p.partnerSlots {
		if v.PartnerUid == 0 {
			continue
		}
		if partner := p.getPartnerInfoBySlotId(v.SlotId); partner != nil {
			partner.revive()
		}
	}

	for _, v := range p.skillsSlot {
		if v == 0 {
			continue
		}
		if skill, _ := p.getSkillex(v); skill != nil {
			if scfg, _ := global.GetSkillCfg(skill.CfgId); scfg != nil && scfg.CoolDown > 10000 {
				p.pushMsg(protocol.RouteSkillCooldownPush, &pb.SkillCooldownPush{CfgId: skill.CfgId, Delay: 0})
			}
		}
	}

	p.AddBuff(int32(constant.GOD_MODE), p, int32(global.GlobalCfg.GVG.Relive.Protecttime*1000), nil, 0, 0)
	// p.logger.Debugf("***********[%s]玩家复活！！！%t   当前血量 %d -- %d --- %d --[%v]", p.name, p.isRobot(), p.currentHP, p.stat(pb.Attribute_HEALTH), p.GetMaxHP(), p.location)

	return true
}

func (p *Player) attack(targets []IMapObject, skillId int32, location *pb.Point, direction pb.Direction) {
	p.lock.Lock()
	defer p.lock.Unlock()

	skill, cast_uid := p.getSkillex(skillId)
	if skill == nil {
		p.logger.Debugf("攻击失败 使用了未学习的技能 userId: %d skillID: %d ", p.uid, skillId)
		return
	}

	castDir := p.direction
	castObjType := ObjectTypePlayer
	if cast_uid != p.uid {
		castObjType = ObjectTypePartner
		if partner := p.getPartnerInfo(cast_uid); partner != nil {
			castDir = partner.direction
		}
	}
	caster := p.MapObject
	if cast_uid != p.uid && p.getPartnerInfo(cast_uid) != nil {
		caster = p.getPartnerInfo(cast_uid).MapObject
	}

	skillCfg, err := global.GetSkillCfg(int32(skillId))
	if err != nil {
		p.logger.Debugf("攻击失败 技能配置读取失败 userId: %d skillID: %d ", p.uid, skillId)
		return
	}

	// 技能CD
	if skillCfg.CoolDown > 0 && time.Now().Before(skill.Cooldown) {
		p.logger.Debugf("攻击失败 技能冷却时间未到 userId: %d  skillID: %d ", p.uid, skillId)
		return
	}

	roomCfg := global.GetRoomCfg(p.desk.cfgID)
	if roomCfg == nil {
		p.logger.Debugf("攻击失败 房间配置不存在 userId: %d  desk.cfgID: %d ", p.uid, p.desk.cfgID)
		return
	}

	// 不需要目标的技能清空前端发送的目标
	if skillCfg.Needaim == 0 {
		targets = nil
	} else {
		if len(targets) == 0 {
			p.logger.Debugf("[%s]攻击失败 此技能需要目标 userId: %d  skillID: %d ", p.name, p.uid, skillId)
			return
		} else {
			// 被嘲讽的状态下不能攻击嘲讽者以外的目标
			if debuff := p.getBuffByType(constant.BuffTaunt); debuff != nil {
				var bFind bool
				for _, v := range targets {
					if v.Uid() == debuff.Owner.Uid() {
						bFind = true
					}
				}
				if !bFind {
					p.logger.Debugf(" 被嘲讽的状态下不能攻击嘲讽者以外的目标 ， 嘲讽者ID %d", debuff.Owner.Uid())
					return
				}
			}
		}
	}

	var targetsUids []int64
	attackCells := []*Cell{}
	var attackLocation *global.PointI
	if targets == nil {
		targets = []IMapObject{}
	}
	if location != nil {
		attackLocation = &global.PointI{X: int(location.X), Y: int(location.Y)}
	}
	if direction >= pb.Direction_UP && direction <= pb.Direction_UP_LEFT {
		p.direction = direction
	}

	Radius := skillCfg.Radius
	// 技能进阶效果
	if skillUpperlevel, err := global.GetUpperlevelSettingCfg(skillCfg.UID, skill.AdvancedLevel); err == nil {
		Radius += skillUpperlevel.Distance
	}
	// 技能影响到的全部格子
	switch skillCfg.SkillID {

	case constant.SkillTypeHalfMoon: // 扇形
		attackCells = p.desk.mapInfo.getSectorCells(&caster.cell.location, p.direction, Radius)

	case constant.SkillTypeFourSides: // 四方向
		for i := 0; i < 4; i++ {
			direction := maputil.ShiftDirection(caster.direction, i*2)
			attackCells = append(attackCells, p.desk.mapInfo.getLineCells(&p.cell.location, direction, Radius)...)
		}

	case constant.SkillTypeMagicMissile, constant.SkillTypeStellarBurst, constant.SkillTypeSoulTalisman: // 正方形
		if attackLocation != nil {
			attackCells = p.desk.mapInfo.getSquareCells(attackLocation, Radius)
		}

	case constant.SkillTypeFreezeTime, constant.SkillTypeTimeSpaceLock, constant.SkillTypeDuel, constant.SkillTypeRepelFoe,
		constant.SkillTypeMassHeal, constant.SkillTypeDivineStrength, constant.SkillTypeArcaneMastery, constant.SkillTypeBlessingOfCourage,
		constant.SkillTypeNetherSpell, constant.SkillTypeDivineBattleArmor, constant.SkillTypeTaunt, constant.SkillTypeStomp,
		constant.SkillTypeBleeding, constant.SkillTypeWreck, constant.SkillTypeApocalypse: // 正方形
		attackCells = p.desk.mapInfo.getSquareCells(&caster.cell.location, Radius)

	case constant.SkillTypeBreakArmy, constant.SkillTypeAuroraPulse, constant.SkillTypeEarthquakeWave: // 长方形
		attackCells = p.desk.mapInfo.getLineCells(&caster.cell.location, p.direction, Radius)

		// 独立处理有个特殊效果的技能
	case constant.SkillTypeSwiftStrike, constant.SkillTypeDecoy, constant.SkillTypeDecoyBoss, constant.SkillTypeInstantaneousMove, constant.SkillTypeMassPurification:
		pdesk := p.desk
		time.AfterFunc(time.Duration(skillCfg.DelayTime)*time.Millisecond, func() {
			if pdesk != nil && pdesk.status() == constant.DeskStatusPlaying {
				pdesk.delayAttackLock.Lock()
				defer pdesk.delayAttackLock.Unlock()

				if len(pdesk.chDelayAttack) >= chLen {
					logger.Warningln("P1 chDelayAttack 通道已满")
					return
				}

				pdesk.chDelayAttack <- &DelayAction{
					ObjType:   castObjType,
					ObjUid:    cast_uid,
					SkillId:   skillId,
					Target:    nil,
					Direction: castDir,
				}
			}
		})

	case constant.SkillTypeSwordHeavenBreaker:
		for i := 1; i <= int(skillCfg.SpecialEffect.MulAttact); i++ {
			pdesk := p.desk
			sblocation := location
			time.AfterFunc(time.Duration(skillCfg.DelayTime*int32(i))*time.Millisecond, func() {
				if pdesk != nil && pdesk.status() == constant.DeskStatusPlaying {
					pdesk.delayAttackLock.Lock()
					defer pdesk.delayAttackLock.Unlock()
					if len(pdesk.chDelayAttack) >= chLen {
						logger.Warningln("P2 chDelayAttack 通道已满")
						return
					}
					pdesk.chDelayAttack <- &DelayAction{
						ObjType:   castObjType,
						ObjUid:    cast_uid,
						SkillId:   skillId,
						Target:    nil,
						Direction: direction,
						Location:  global.PointI{X: int(sblocation.X), Y: int(sblocation.Y)},
					}
				}
			})
		}

	}

	for _, cell := range attackCells {
		if cell == nil || cell.block {
			continue
		}
		for _, obj := range cell.objects {
			bFind := false
			for _, have := range targets {
				if have.Uid() == obj.Uid() {
					bFind = true
					break
				}
			}
			if !bFind {
				if p.CanAttackTarget(obj) {

					if skillCfg.Object == constant.SkillEffectTypeMonster && obj.ObjectType() != ObjectTypeMonster {
						continue
					}
					if skillCfg.Object == constant.SkillEffectTypePlayer && obj.ObjectType() != ObjectTypePlayer {
						continue
					}
					targets = append(targets, obj)
				}
			}
		}
	}

	for _, t := range targets {
		if t.Uid() == p.uid {
			continue
		}

		if t.ObjectType() < ObjectTypePlayer && t.ObjectType() > ObjectTypeDecoy {
			continue
		}

		if t.ObjectType() == ObjectTypePlayer {
			if roomCfg.Roomtype <= constant.PVEPeacefulMap || roomCfg.Roomtype >= constant.SoloTowerMap {
				p.logger.Debugf("目标无法攻击,房间类型 %+v userId: %d , targetId:%d ", roomCfg.Roomtype, p.uid, t.Uid())
				continue
			}
		}
		// 和平模式不能攻击除怪以外目标
		if p.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
			if t.ObjectType() != ObjectTypeMonster {
				continue
			}
		}
		// 安全区内不能攻击玩家
		if p.desk.roomCfg.Roomtype == constant.PVEWarMap {
			if t.ObjectType() != ObjectTypeMonster && (p.desk.mapInfo.safeArea(p.location) || p.desk.mapInfo.safeArea(t.Location())) {
				p.logger.Debugf("安全去不能攻击玩家  位置:%v , 目标坐标:%v  skillID: %d ", p.location, t.Location(), skillId)
				continue
			}
		}

		// 技能距离
		if skillCfg.MaxDistance > 0 && maputil.Distance(p.location, t.Location()) > int(skillCfg.MaxDistance) {
			p.logger.Debugf("超出攻击距离 userId: %d, 位置:%v , 目标坐标:%v  skillID: %d ", p.uid, p.location, t.Location(), skillId)
			continue
		}

		caster.target = t

		caster.direction = maputil.DirectionFromPointF(p.location, t.Location())
		castDir = caster.direction

		targetsUids = append(targetsUids, t.Uid())

		// 伤害延迟生效
		pdesk := p.desk
		pt := t
		time.AfterFunc(time.Duration(skillCfg.DelayTime)*time.Millisecond, func() {
			if pdesk != nil && pdesk.status() == constant.DeskStatusPlaying {
				pdesk.delayAttackLock.Lock()
				defer pdesk.delayAttackLock.Unlock()
				if len(pdesk.chDelayAttack) >= chLen {
					logger.Warningln("P3 chDelayAttack 通道已满")
					return
				}
				pdesk.chDelayAttack <- &DelayAction{
					ObjType:   castObjType,
					ObjUid:    cast_uid,
					SkillId:   skillId,
					Target:    pt,
					Direction: castDir,
					Location:  global.PointI{X: p.cell.location.X, Y: p.cell.location.Y},
				}
			}
		})
	}

	// 技能buff触发
	caster.SkillBuffTrigger(skill)

	// 技能CD
	if skillCfg.CoolDown > 0 {
		skill.Cooldown = time.Now().Add(time.Duration(float64(skillCfg.CoolDown))*time.Millisecond - 500)
		p.pushMsg(protocol.RouteSkillCooldownPush, &pb.SkillCooldownPush{CfgId: skillId, Delay: skillCfg.CoolDown})
	}

	if cast_uid == p.uid {
		p.setAttackTime(global.GetActionsetCfg(skillCfg.SkillID))
	}

	packet := &pb.AttackPush{
		AttackerId: cast_uid,
		TargetUids: targetsUids,
		SkillId:    skillId,
		Location:   location,
		Direction:  castDir,
	}
	p.desk.Broadcast(protocol.RouteAttackPush, packet)

	// p.logger.Debugf("====[%s]====释放技能   玩家位置=%v skillID %d skillName: %s    朝向=%d", caster.Name(), p.location, skillId, skillCfg.Name, p.direction)

	p.SetObState(ObjectActionAttack)
}

func (p *Player) delayAction(target IMapObject, skillId int32, direction pb.Direction, Location global.PointI) {
	skillType := skillId / 100
	switch skillType {

	case constant.SkillTypeDecoy, constant.SkillTypeDecoyBoss:
		p.decoyEnd(skillId, direction)
	case constant.SkillTypeInstantaneousMove:
		p.instantaneousMoveEnd(skillId)
	case constant.SkillTypeSwordHeavenBreaker:
		if p.swordHeavenBreakerEnd(skillId) {
			skillCfg, err := global.GetSkillCfg(int32(skillId))
			if err != nil {
				break
			}

			attackCells := p.desk.mapInfo.getSquareCells(&Location, skillCfg.Radius)
			p.attackLocation(attackCells, skillCfg)
		}
	default:
		if target == nil {
			return
		}
		p.delayAttack(target, skillId)
	}
}

func (p *Player) attackLocation(cells []*Cell, skillCfg *global.SkillConfig) {
	var targets []IMapObject
	for _, cell := range cells {
		if cell == nil || cell.block {
			continue
		}
		for _, obj := range cell.objects {
			bFind := false
			for _, have := range targets {
				if have.Uid() == obj.Uid() {
					bFind = true
					break
				}
			}
			if !bFind {
				if p.CanAttackTarget(obj) {

					if skillCfg.Object == constant.SkillEffectTypeMonster && obj.ObjectType() != ObjectTypeMonster {
						continue
					}
					if skillCfg.Object == constant.SkillEffectTypePlayer && obj.ObjectType() != ObjectTypePlayer {
						continue
					}
					targets = append(targets, obj)
				}
			}
		}
	}

	for _, t := range targets {
		if t.Uid() == p.uid {
			continue
		}

		if t.ObjectType() < ObjectTypePlayer && t.ObjectType() > ObjectTypeDecoy {
			continue
		}

		if t.ObjectType() == ObjectTypePlayer {
			if p.desk.roomCfg.Roomtype <= constant.PVEPeacefulMap || p.desk.roomCfg.Roomtype >= constant.SoloTowerMap {
				p.logger.Debugf("目标无法攻击,房间类型 %+v userId: %d , targetId:%d ", p.desk.roomCfg.Roomtype, p.uid, t.Uid())
				continue
			}
		}

		// 和平模式不能攻击除怪以外目标
		if p.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
			if t.ObjectType() != ObjectTypeMonster {
				continue
			}
		}

		// 安全区内不能攻击玩家
		if p.desk.roomCfg.Roomtype == constant.PVEWarMap {
			if t.ObjectType() != ObjectTypeMonster && (p.desk.mapInfo.safeArea(p.location) || p.desk.mapInfo.safeArea(t.Location())) {
				continue
			}
		}

		// 技能距离
		if skillCfg.MaxDistance > 0 && maputil.Distance(p.location, t.Location()) > int(skillCfg.MaxDistance) {
			continue
		}

		p.delayAttack(t, skillCfg.UID)
	}
}

func (p *Player) delayAttack(target IMapObject, skillId int32) {
	if !p.CanAttackTarget(target) {
		return
	}

	skill := p.getSkill(skillId)
	if skill == nil {
		return
	}
	skillCfg, err := global.GetSkillCfg(int32(skillId))
	if err != nil {
		return
	}

	// 命中-闪避
	hit := p.stats.getStats(pb.Attribute_HIT)
	if tstats := target.GetStats(); tstats != nil {
		dodge := target.GetStats().getStats(pb.Attribute_DODGE)
		if hit < dodge && global.RandIn(int(dodge)) > int(hit) {
			target.Dodged()
			return
		}
	}

	// 技能效果
	var power, basePower, skillPower float64

	basePower = float64(p.stats.getStats(p.AttackAttributes()))

	power += basePower
	for _, v := range skillCfg.Effect {
		switch pb.Attribute(v.Key) {
		case pb.Attribute_PHYSICAL_ATTACK, pb.Attribute_MAGIC_ATTACK:
			skillPower += float64(v.Value)
		case pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE, pb.Attribute_MAGIC_ATTACK_PERCENTAGE, pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE:
			skillPower += float64(basePower) * (float64(v.Value) / BaseRate)
		case pb.Attribute_PVP_DAMAGE_PERCENTAGE:
			if target.ObjectType() == ObjectTypePlayer {
				skillPower += float64(basePower) * (float64(v.Value) / BaseRate)
			}
		case pb.Attribute_PVE_DAMAGE_PERCENTAGE:
			if target.ObjectType() == ObjectTypeMonster {
				skillPower += float64(basePower) * (float64(v.Value) / BaseRate)
			}
		case pb.Attribute_DMT_FINAL_INCREASE_FIXED:
			skillPower += float64(v.Value)
		}
	}

	power += skillPower

	var damage_percentage_increase, flat_damage_increase int32 // 增伤百分比，增伤固定值

	// 技能进阶效果
	if skillUpperlevel, err := global.GetUpperlevelSettingCfg(skillCfg.UID, skill.AdvancedLevel); err == nil {
		damage_percentage_increase += skillUpperlevel.DamagePer
	}

	// 暴击
	if len(skillCfg.Effect) > 0 || skillCfg.SType == constant.SkillTypeNormal {
		critical := p.stats.getStats(pb.Attribute_CRITICAL_STRIKE)
		tenacity := target.GetStats().getStats(pb.Attribute_TOUGHNESS)

		criticalRate := (math.Max(0, float64(critical-tenacity))*global.GlobalCfg.BaseValue.Crit.K + float64(global.GlobalCfg.BaseValue.Crit.PreValue))
		if global.RandIn(100) < int(criticalRate) {
			damage_percentage_increase += p.stat(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)
			target.Critical()
		}
	}

	// 攻击-防御
	if tstats := target.GetStats(); tstats != nil {
		power -= float64(tstats.getStats(maputil.DefenseAttributes(p.AttackAttributes())))
	}

	power += power * float64(damage_percentage_increase) / BaseRate
	power += float64(flat_damage_increase)

	// 最终增伤
	var final_damage_percentage_increase, final_flat_damage_increase int32
	if target.ObjectType() == ObjectTypeMonster {
		final_flat_damage_increase += p.stat(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)
		final_damage_percentage_increase += p.stat(pb.Attribute_PVE_DAMAGE_PERCENTAGE)
	} else {
		final_flat_damage_increase += p.stat(pb.Attribute_DMT_INCREASE_FIXED_PLAYER)
		final_damage_percentage_increase += p.stat(pb.Attribute_PVP_DAMAGE_PERCENTAGE)
	}
	final_damage_percentage_increase += p.stat(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE)
	final_flat_damage_increase += p.stat(pb.Attribute_DMT_FINAL_INCREASE_FIXED)

	power += power * float64(final_damage_percentage_increase) / BaseRate
	power += float64(final_flat_damage_increase)

	if power <= 0 {
		power = float64(rand.Intn(2)) + 1
	}
	if len(skillCfg.Effect) == 0 && skillCfg.SType != constant.SkillTypeNormal {
		power = 0
	}

	// 吸血&技能伤害转移
	suckblood := power * float64(p.stat(pb.Attribute_SUCKING_BLOOD_PERCENTAGE)) / float64(BaseRate)
	if skillCfg.SpecialEffect.DamTransfer > 0 {
		suckblood += power * float64(skillCfg.SpecialEffect.DamTransfer) / float64(BaseRate)
	}
	if suckblood > 0 {
		p.changeHp(int32(suckblood), p)
	}

	// p.logger.Debugf("P 攻击 targetID: %d  目标位置: %v skillID: %d  Power:%f   当前目标 %d", target.Uid(), target.Location(), skillId, power, p.Target().Uid())
	target.Attacked(p, skillId, skill.AdvancedLevel, int32(power), true)
}

// ====================================================================
func (p *Player) move(location *pb.Point) {
	p.lock.Lock()
	defer p.lock.Unlock()

	if p.desk == nil || !p.desk.mapCfg.Cells[*global.ToPointI(location)] {
		p.logger.Debugf("P 移动失败,不能到达   坐标: %v", location)
		return
	}

	if !p.CanMove() {
		p.logger.Debugf("P 移动失败,移动频率过快!/ 已死亡:[%t] /Debuff:[%t]", p.dead, p.HaveBuff(constant.BuffFreeze))
		return
	}

	if maputil.Distance(p.location, location) > 2 {
		p.logger.Debugf("P 移动失败,距离过远 起点 %v  终点 %v", p.location, location)
		return
	}

	if buff := p.getBuffByType(constant.BuffTaunt); buff != nil {
		ownerDir := maputil.DirectionFromPointF(p.location, buff.Owner.Location())
		goDir := maputil.DirectionFromPointF(p.location, location)
		if ownerDir != goDir {
			p.logger.Debugf("P 移动失败,被嘲讽状态只能朝嘲讽者移动 ownerDir %v  goDir %v", ownerDir, goDir)
			// return
		}
	}

	if p.desk.mapInfo.locationChange(p, location) {
		packet := &pb.MovePush{Uid: p.uid, Location: p.location, Direction: p.direction}
		p.desk.Broadcast(protocol.RouteMovePush, packet)
		for _, v := range p.cell.objects {
			if v.ObjectType() == ObjectTypeBuff {
				if p.desk.delObject(v.Uid()) {
					p.AddBuff(v.CfgID(), p, 0, nil, 0, 0)
					break
				}
			}
		}
	} else {
		p.pushMsg(protocol.RouteMovePush, &pb.MovePush{Uid: p.Uid(), Location: p.location, Direction: p.direction})
	}
	p.moveTime = time.Now().Add(time.Duration(p.stat(pb.Attribute_MOVE_SPEED)-100) * time.Millisecond)
	p.SetObState(ObjectActionMove)
}

func (p *Player) toDbBuff() []byte {
	dbbuff := &pb.DBBuffList{Buffs: []*pb.DBBuffInfo{}}

	for _, buf := range p.buffs {
		if buf.Mark == constant.BuffMarkPassive {
			continue
		}

		switch constant.BuffType(buf.Pretype) {
		case constant.BuffPercentEquipStarIncrease:
			one := &pb.DBBuffInfo{
				Id:            buf.Id,
				BuffType:      buf.BuffType,
				StartTime:     buf.StartTime.Unix(),
				OverTime:      buf.OverTime.Unix(),
				TickTime:      buf.TickTime.Unix(),
				CDTime:        buf.CDTime.Unix(),
				TickFrequency: buf.TickFrequency,
				Pretype:       buf.Pretype,
				OwnerId:       buf.Owner.Uid(),
				Status:        buf.Status,
				BuffStats:     []*pb.Stats{},
			}

			for k, v := range buf.BuffStats.Attrs {
				one.BuffStats = append(one.BuffStats, &pb.Stats{Attr: k, Num: v})
			}

			dbbuff.Buffs = append(dbbuff.Buffs, one)
		}
	}
	if data, err := protobuf.NewSerializer().Marshal(dbbuff); err == nil {
		return data
	}
	return nil
}

func (p *Player) fromDbBuff(buff []byte) {
	data := &pb.DBBuffList{}
	if err := protobuf.NewSerializer().Unmarshal(buff, data); err == nil {
		for _, dbbuff := range data.Buffs {
			if dbbuff.OverTime < time.Now().Unix() {
				continue
			}

			buff := &BuffInfo{
				Id:            dbbuff.Id,
				BuffType:      dbbuff.BuffType,
				Mark:          dbbuff.Mark,
				StartTime:     time.Unix(dbbuff.StartTime, 0),
				OverTime:      time.Unix(dbbuff.OverTime, 0),
				TickTime:      time.Unix(dbbuff.TickTime, 0),
				CDTime:        time.Unix(dbbuff.CDTime, 0),
				TickFrequency: dbbuff.TickFrequency,
				Pretype:       dbbuff.Pretype,
				BuffStats:     Stats{Attrs: make(map[pb.Attribute]int32)},
			}

			if owner, ok := defaultManager.player(dbbuff.OwnerId); ok {
				buff.Owner = owner
			} else {
				buff.Owner = p
			}
			for _, v := range dbbuff.BuffStats {
				buff.BuffStats.setStats(v.Attr, v.Num)
			}
			p.buffs[buff.Id] = buff
		}
	}
}

func (p *Player) HoneB(buff []byte) {
	data := &pb.DBBuffList{}
	if err := protobuf.NewSerializer().Unmarshal(buff, data); err == nil {
		for _, dbbuff := range data.Buffs {
			if dbbuff.OverTime < time.Now().Unix() {
				continue
			}

			buff := &BuffInfo{
				Id:            dbbuff.Id,
				BuffType:      dbbuff.BuffType,
				Mark:          dbbuff.Mark,
				StartTime:     time.Unix(dbbuff.StartTime, 0),
				OverTime:      time.Unix(dbbuff.OverTime, 0),
				TickTime:      time.Unix(dbbuff.TickTime, 0),
				CDTime:        time.Unix(dbbuff.CDTime, 0),
				TickFrequency: dbbuff.TickFrequency,
				Pretype:       dbbuff.Pretype,
				BuffStats:     Stats{Attrs: make(map[pb.Attribute]int32)},
			}

			if owner, ok := defaultManager.player(dbbuff.OwnerId); ok {
				buff.Owner = owner
			} else {
				buff.Owner = p
			}
			for _, v := range dbbuff.BuffStats {
				buff.BuffStats.setStats(v.Attr, v.Num)
			}
			p.buffs[buff.Id] = buff
		}
	}
}

func (p *Player) defaultBuff() {
	// 宠物加buff
	// 宠物buff数组，每个元素包含ID和对应的属性

	// 初始化buff
	for _, v := range p.buffs {
		p.delBuff(v.Id)
	}

	// 获取宠物天赋Buff列表
	petBuffs := p.getPetBuffs()
	for k, v := range global.BuffCfg {
		if v.Mark == constant.BuffMarkPassive {
			p.AddBuffNoRefresh(k, p, 24*60*60*1000, nil, 0, 0)
		}
		// 如果在这个数组里面，就加buff
		if len(petBuffs) > 0 {
			// 如果这个buff在这个数组里面，就加buff
			for _, vv := range petBuffs {
				if vv.Id == k {
					p.AddBuffNoRefresh(k, p, 24*60*60*1000, vv.Stats, vv.Rate, 0)
				}
			}
		}

	}
	p.refreshStats()
}

type PetBuff struct {
	Id    int32
	Rate  int32
	Stats *Stats
}

// 获取宠物天赋Buff列表
func (p *Player) getPetBuffs() []*PetBuff {
	petBuffs := []*PetBuff{}
	if len(p.pet) > 0 {
		if p.pet[0].PetCfgId != 0 {
			if petcfg := global.GetPetCfg(p.pet[0].PetCfgId); petcfg != nil {
				for _, v := range petcfg.Talent {
					bufStats := &Stats{Attrs: make(map[pb.Attribute]int32)}
					if len(v.Value) > 0 {
						for _, vv := range v.Value {
							bufStats.setStats(pb.Attribute(vv.ID), vv.Num)
						}
					}
					petBuffs = append(petBuffs, &PetBuff{Id: v.ID, Stats: bufStats, Rate: v.Rate})
				}
			}
		}
	}
	return petBuffs
}

func (p *Player) syncBuff() {
	packet := &pb.BuffListPush{BuffList: p.getPBBuffList()}
	p.pushMsg(protocol.RouteBuffListPush, packet)
}

// 穿脱装备opType 1穿 ，2脱  partnerSlotId 伙伴槽位 0 为主角
func (p *Player) changeEquip(opType int32, slot pb.EquipSlot, itemUid int64, partnerSlotId int32) error {
	p.lock.Lock()
	defer p.lock.Unlock()

	if slot <= pb.EquipSlot_INVALID_EquipSlot || slot >= pb.EquipSlot_MaxEquipSlot {
		return errutil.ErrEquipSlot
	}

	if partnerSlotId < 0 || partnerSlotId > 5 {
		return errutil.ErrEquipSlot
	}

	var equipSlot *Slot
	var job_ int
	var level_ int
	if partnerSlotId == 0 {
		equipSlot = &p.equips[slot]
		job_ = int(p.job)
		level_ = int(p.level)
	} else {
		equipSlot = &p.getPartnerSlotInfo(partnerSlotId).Equips[slot]

		if par := p.getPartnerInfoBySlotId(partnerSlotId); par != nil {
			job_ = int(par.cfg.Job) % 10
			level_ = int(par.level)
		} else {
			return errutil.ErrPartnerInvalid
		}
	}

	if opType == 1 {
		// item是最新的装备的信息
		if item := p.getItemByUid(itemUid); item != nil {
			itemCfg, err := global.GetItemCfg(item.CfgId)
			if err != nil {
				return err
			}
			if itemCfg.Equiptype != int(slot) {
				return errutil.ErrEquiptype
			}
			if itemCfg.Levellimit > level_ {
				return errutil.ErrLevel
			}
			if itemCfg.Occupylimit != 0 && itemCfg.Occupylimit != job_ {
				return errutil.ErrJob
			}
			// if itemCfg.Genderlimit != 0 && itemCfg.Genderlimit != p.sex {
			// 	return errutil.ErrGender
			// }
			// 检查是否已经在身上了
			if p.isOnEquip(item.Uid) {
				return errors.New(errutil.EquipOnUse)
			}
			// 同步包裹
			// 如果当前装备槽位有装备
			if equipSlot.ItemId != 0 {
				unequip := p.getItemByUid(equipSlot.ItemId)
				if unequip != nil {
					packet := &pb.AddSourcePush{Type: 2, Item: []*pb.ItemInfo{}}
					packet.Item = append(packet.Item, unequip)
					p.pushMsg(protocol.RouteAddSourcePush, packet)
					// 在首页开宝箱穿装备时，需要把之前的装备替换进之前的宝箱
					// 遍历当前打开的宝箱
					for i := range p.box.NowOpenRes {
						// 在候选区找到这个装备
						if p.box.NowOpenRes[i].Uid == itemUid {
							// 替换这个值
							p.box.NowOpenRes[i].CfgId = unequip.CfgId
							p.box.NowOpenRes[i].Uid = equipSlot.ItemId
							p.box.NowOpenRes[i].Num = 1
							p.box.NowOpenRes[i].Equip = &pb.EquipInfo{Quality: pb.ItemQuality(equipSlot.Quality)}
							break

						}
					}
				}

			} else {
				for i := range p.box.NowOpenRes {
					if p.box.NowOpenRes[i].Uid == itemUid {
						// 删除这个值
						p.box.NowOpenRes = append(p.box.NowOpenRes[:i], p.box.NowOpenRes[i+1:]...)
						break
					}
				}
			}
			packet2 := &pb.TakeSourcePush{Item: []*pb.ItemInfo{}}
			packet2.Item = append(packet2.Item, item)
			p.pushMsg(protocol.RouteTakeSourcePush, packet2)

			equipSlot.ItemId = item.Uid
			equipSlot.Quality = int32(item.Equip.Quality)
		}
	} else if opType == 2 {

		if p.bagEquipLeftCount() == 0 {
			return errutil.ErrBagFull
		}
		item := p.getItemByUid(equipSlot.ItemId)
		if item != nil {
			packet := &pb.AddSourcePush{Type: 2, Item: []*pb.ItemInfo{}}
			packet.Item = append(packet.Item, item)
			p.pushMsg(protocol.RouteAddSourcePush, packet)
			equipSlot.ItemId = 0
		}
	}

	return nil
}

// 已装备道具数量
func (p *Player) getEquipNum() int {
	num := 0
	for i, v := range p.equips {
		if pb.EquipSlot(i) <= pb.EquipSlot_INVALID_EquipSlot || pb.EquipSlot(i) >= pb.EquipSlot_MaxEquipSlot {
			continue
		}
		if v.ItemId != 0 {
			num++
		}
	}
	for _, v := range p.partnerSlots {
		for _, vv := range v.Equips {
			if vv.ItemId != 0 {
				num++
			}
		}
	}
	return num
}

func (p *Player) hasEquipment(itemUid int64) bool {
	for _, v := range p.equips {
		if v.ItemId == itemUid {
			return true
		}
	}
	for _, v := range p.partnerSlots {
		for _, vv := range v.Equips {
			if vv.ItemId == itemUid {
				return true
			}
		}
	}
	return false
}

func (p *Player) hasHomeOpenBox(itemUid int64) bool {
	for _, v := range p.box.NowOpenRes {
		if v.Uid == itemUid {
			return true
		}
	}
	return false
}

func (p *Player) stats4Combat(combatStats *Stats) *Stats {
	// <0  减益 ; >0 增益
	combatStats.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(combatStats.getStats(pb.Attribute_PHYSICAL_ATTACK))*(float64(combatStats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(combatStats.getStats(pb.Attribute_MAGIC_ATTACK))*(float64(combatStats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE))*(float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE))*(float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))

	combatStats.setStats(pb.Attribute_DODGE, int32(float64(combatStats.getStats(pb.Attribute_DODGE))*(float64(combatStats.getStats(pb.Attribute_DODGE_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_HIT, int32(float64(combatStats.getStats(pb.Attribute_HIT))*(float64(combatStats.getStats(pb.Attribute_HIT_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_CRITICAL_STRIKE, int32(float64(combatStats.getStats(pb.Attribute_CRITICAL_STRIKE))*(float64(combatStats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_TOUGHNESS, int32(float64(combatStats.getStats(pb.Attribute_TOUGHNESS))*(float64(combatStats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(combatStats.getStats(pb.Attribute_MAX_HEALTH))*float64(combatStats.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate))

	// 攻速移速增加  基础/(1+加成)；攻速移速降低  基础*(1+加成)
	combatStats.changeStats(pb.Attribute_ATTACK_SPEED, int32(float64(combatStats.getStats(pb.Attribute_ATTACK_SPEED))/(1+float64(combatStats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE))/BaseRate)))
	combatStats.changeStats(pb.Attribute_MOVE_SPEED, int32(float64(combatStats.getStats(pb.Attribute_MOVE_SPEED))/(1+(float64(combatStats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE))/BaseRate))))

	// 基础属性 >= 0
	combatStats.changeStats(pb.Attribute_PHYSICAL_ATTACK, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_PHYSICAL_ATTACK)))))
	combatStats.changeStats(pb.Attribute_MAGIC_ATTACK, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_MAGIC_ATTACK)))))
	combatStats.changeStats(pb.Attribute_PHYSICAL_DEFENSE, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE)))))
	combatStats.changeStats(pb.Attribute_MAGIC_DEFENSE, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE)))))

	combatStats.changeStats(pb.Attribute_DODGE, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_DODGE)))))
	combatStats.changeStats(pb.Attribute_HIT, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_HIT)))))
	combatStats.changeStats(pb.Attribute_CRITICAL_STRIKE, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_CRITICAL_STRIKE)))))
	combatStats.changeStats(pb.Attribute_TOUGHNESS, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_TOUGHNESS)))))
	combatStats.changeStats(pb.Attribute_ATTACK_SPEED, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_ATTACK_SPEED)))))
	combatStats.changeStats(pb.Attribute_MOVE_SPEED, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_MOVE_SPEED)))))
	combatStats.changeStats(pb.Attribute_MAX_HEALTH, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_MAX_HEALTH)))))

	return combatStats
}

// 战力（不计算buff）
func (p *Player) calcCombatReal(stats *Stats) int32 {
	combatStats := p.stats4Combat(stats)
	if combatStats == nil {
		return 0
	}
	// oldcombat := p.combat
	var skillCombat float64
	for _, v := range p.skills {
		if cfg, err := global.GetSkillCfg(v.CfgId); err == nil {
			skillCombat += float64(cfg.Powerfactor) * (1 + float64(v.AdvancedLevel))
		}
	}

	// 减伤阈值75%
	dmt_reduce := combatStats.getStats(pb.Attribute_DMT_REDUCE_PERCENTAGE_PLAYER) + combatStats.getStats(pb.Attribute_DMT_REDUCE_PERCENTAGE_MONSTER) + combatStats.getStats(pb.Attribute_DMT_FINAL_REDUCE_PERCENTAGE)
	if dmt_reduce > 7500 {
		dmt_reduce = 7500
	}

	// 有效生命
	effective_hp := (float64(combatStats.getStats(pb.Attribute_MAX_HEALTH))+global.GlobalCfg.BaseValue.Power.DefValue*(float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE))+float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE)))/2.0)/(1-float64(dmt_reduce)/BaseRate) +
		(float64(combatStats.getStats(pb.Attribute_DMT_REDUCE_FIXED_PLAYER))+float64(combatStats.getStats(pb.Attribute_DMT_REDUCE_FIXED_MONSTER)))/2.0 +
		float64(combatStats.getStats(pb.Attribute_DMT_FINAL_REDUCE_FIXED))
	// p.logger.Debugf("  第1部分 %f   第2部分 %f    第3部分 %f   ===有效生命 %f ", (float64(combatStats.getStats(pb.Attribute_MAX_HEALTH))+global.GlobalCfg.BaseValue.Power.DefValue*(float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE))+float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE)))/2.0)/(1-float64(dmt_reduce)/BaseRate),
	// 	(float64(combatStats.getStats(pb.Attribute_DMT_REDUCE_FIXED_PLAYER))+float64(combatStats.getStats(pb.Attribute_DMT_REDUCE_FIXED_MONSTER)))/2.0,
	// 	float64(combatStats.getStats(pb.Attribute_DMT_FINAL_REDUCE_FIXED)), effective_hp)
	// 有效输出
	effective_power := float64(combatStats.getStats(p.AttackAttributes()))*
		(1+float64(combatStats.getStats(pb.Attribute_PVP_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_PVE_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE))/BaseRate)*(1+skillCombat/100) +
		(float64(combatStats.getStats(pb.Attribute_DMT_INCREASE_FIXED_PLAYER))+float64(combatStats.getStats(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)))/2.0 +
		float64(combatStats.getStats(pb.Attribute_DMT_FINAL_INCREASE_FIXED))

	// p.logger.Debugf(" 面板攻击 %f  (1+增伤百分比+爆伤) %f  技能加成 %f   固定值 %f   ===有效输出 %f ", float64(combatStats.getStats(p.AttackAttributes())),
	// 	(1 + float64(combatStats.getStats(pb.Attribute_PVP_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_PVE_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE))/BaseRate),
	// 	(1 + skillCombat/100), (float64(combatStats.getStats(pb.Attribute_DMT_INCREASE_FIXED_PLAYER))+float64(combatStats.getStats(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)))/2.0, effective_power)

	combat := math.Sqrt(effective_hp*global.GlobalCfg.BaseValue.Power.LifeValue)*math.Sqrt(effective_power*global.GlobalCfg.BaseValue.Power.AttackValue) +
		float64(combatStats.getStats(pb.Attribute_HIT))*global.GlobalCfg.BaseValue.Power.Hitvalue +
		float64(combatStats.getStats(pb.Attribute_DODGE))*global.GlobalCfg.BaseValue.Power.Dodgevalue +
		float64(combatStats.getStats(pb.Attribute_CRITICAL_STRIKE))*global.GlobalCfg.BaseValue.Power.Critvalue +
		float64(combatStats.getStats(pb.Attribute_TOUGHNESS))*global.GlobalCfg.BaseValue.Power.Toughness

	// p.logger.Debugf(" 最终战力 %f", combat)
	p.combat = int32(math.Round(combat))

	return p.combat
}

// 战力
func (p *Player) calcCombat() int32 {
	var skillCombat float64
	for _, v := range p.skills {
		if cfg, err := global.GetSkillCfg(v.CfgId); err == nil {
			skillCombat += float64(cfg.Powerfactor) * (1 + float64(cfg.CanupperLevel))
		}
	}

	// 减伤阈值75%
	dmt_reduce := p.stat(pb.Attribute_DMT_REDUCE_PERCENTAGE_PLAYER) + p.stat(pb.Attribute_DMT_REDUCE_PERCENTAGE_MONSTER) + p.stat(pb.Attribute_DMT_FINAL_REDUCE_PERCENTAGE)
	if dmt_reduce > 7500 {
		dmt_reduce = 7500
	}

	// 有效生命
	effective_hp := (float64(p.GetMaxHP())+global.GlobalCfg.BaseValue.Power.DefValue*(float64(p.GetAR())+float64(p.GetMR()))/2.0)/(1-float64(dmt_reduce)/BaseRate) +
		(float64(p.stat(pb.Attribute_DMT_REDUCE_FIXED_PLAYER))+float64(p.stat(pb.Attribute_DMT_REDUCE_FIXED_MONSTER)))/2.0 +
		float64(p.stat(pb.Attribute_DMT_FINAL_REDUCE_FIXED))

	// 有效输出
	effective_power := float64(p.stat(p.AttackAttributes()))*
		(1+float64(p.stat(pb.Attribute_PVP_DAMAGE_PERCENTAGE)+p.stat(pb.Attribute_PVE_DAMAGE_PERCENTAGE)+p.stat(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)+p.stat(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE))/BaseRate)*(1+skillCombat/100) +
		(float64(p.stat(pb.Attribute_DMT_INCREASE_FIXED_PLAYER))+float64(p.stat(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)))/2.0 +
		float64(p.stat(pb.Attribute_DMT_FINAL_INCREASE_FIXED))

	combat := math.Sqrt(effective_hp*global.GlobalCfg.BaseValue.Power.LifeValue)*math.Sqrt(effective_power*global.GlobalCfg.BaseValue.Power.AttackValue) +
		float64(p.stat(pb.Attribute_HIT))*global.GlobalCfg.BaseValue.Power.Hitvalue +
		float64(p.stat(pb.Attribute_DODGE))*global.GlobalCfg.BaseValue.Power.Dodgevalue +
		float64(p.stat(pb.Attribute_CRITICAL_STRIKE))*global.GlobalCfg.BaseValue.Power.Critvalue +
		float64(p.stat(pb.Attribute_TOUGHNESS))*global.GlobalCfg.BaseValue.Power.Toughness

	return int32(math.Round(combat))
}

// 单个道具战力
func (p *Player) calcCombatEquip(equipInfo *pb.ItemInfo) int32 {
	if equipInfo == nil {
		return 0
	}
	equipCfg, err := global.GetItemCfg(equipInfo.CfgId)
	if err != nil {
		return 0
	}
	totalStatus := &Stats{Attrs: make(map[pb.Attribute]int32)}
	equipStatus := &Stats{Attrs: make(map[pb.Attribute]int32)}
	// 基础值攻\防: （装备基础值+槽位强化基础值)*升星百分比
	for _, attr := range equipCfg.Stats {
		if attr.Key == pb.Attribute_PHYSICAL_ATTACK || attr.Key == pb.Attribute_MAGIC_ATTACK ||
			attr.Key == pb.Attribute_PHYSICAL_DEFENSE || attr.Key == pb.Attribute_MAGIC_DEFENSE ||
			attr.Key == pb.Attribute_MAX_HEALTH {
			equipStatus.setStats(attr.Key, int32(math.Round(float64(attr.Value)*global.GetEquipqualityCoefficient(equipInfo.Equip.Quality))))
		} else {
			totalStatus.setStats(attr.Key, attr.Value)
		}
	}
	// 极品属性直接计算
	if equipInfo.Equip != nil {
		for _, attr := range equipInfo.Equip.Attributes {
			totalStatus.setStats(attr.Attr, attr.Num)
		}
	}

	equipStatus.setStats(pb.Attribute_PHYSICAL_ATTACK, 100+int32(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_ATTACK))*(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAGIC_ATTACK, 100+int32(float64(equipStatus.getStats(pb.Attribute_MAGIC_ATTACK))*(float64(equipStatus.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_PHYSICAL_DEFENSE, 100+int32(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE))*(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAGIC_DEFENSE, 100+int32(float64(equipStatus.getStats(pb.Attribute_MAGIC_DEFENSE))*(float64(equipStatus.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAX_HEALTH, 100+int32(float64(equipStatus.getStats(pb.Attribute_MAX_HEALTH))*(float64(equipStatus.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate)))

	for ek, ev := range equipStatus.Attrs {
		if ek == pb.Attribute_PHYSICAL_ATTACK || ek == pb.Attribute_MAGIC_ATTACK ||
			ek == pb.Attribute_PHYSICAL_DEFENSE || ek == pb.Attribute_MAGIC_DEFENSE ||
			ek == pb.Attribute_MAX_HEALTH {
			totalStatus.setStats(ek, ev)
		}
	}

	return p.calcCombatReal(totalStatus)
}

func (p *Player) addPrivilege(shopType int32, privilegeId int32, expirationTime time.Time) error {
	if shopType == constant.ShopTypeSubscription {
		for _, v := range p.privilege {
			if v.Pid == privilegeId {
				return errutil.ErrPrivilegeExist
			}
		}
		p.privilege = append(p.privilege, protocol.PrivilegeDetailTCP{Pid: privilegeId, EndTime: expirationTime})
	} else if shopType == constant.ShopTypeAFK {
		// 当前时间加30天后的0点
		day := global.GlobalCfg.BaseValue.Expcopy.Lastday
		p.afkExpirationAt = time.Date(time.Now().Year(), time.Now().Month(), time.Now().Day(), 0, 0, 0, 0, time.Local).AddDate(0, 0, int(day))
		p.afkDailyTime = 0
		p.syncSaveDb()
	}

	p.refreshStats()
	p.syncUserBase()
	p.logger.Debugf("添加特权卡成功 privilegeId=%d expirationTime=%v", privilegeId, expirationTime)

	return nil
}

// 更新排行榜

func (p *Player) rankChangeTCP(k int, v int64) {
	if p.isRobot() {
		if p.robotType == RobotTypeRobot {
			return
		}
		if p.robotType == RobotTypePlayer && k != constant.RankTypeSoul {
			return
		}
	}

	// 不是第一名的时候发消息
	cansend := 0
	serverId := int32(0)
	servername := ""
	rankMember := p.uid
	point := v
	RankServerId := int32(0)
	if k == constant.RankTypeGightEndlayers {
		for k1, v1 := range p.FightEndMap.Evaluation {
			if v1.BattleId == int32(v) {
				point = int64(1000000 - p.FightEndMap.Evaluation[k1].ChallengeTime)
			}
		}
		rankMember = int64(v % 100000)
	}
	if RankTypeServer, ok := constant.RankTypeServer[int64(k)]; ok && RankTypeServer {
		if k != constant.RankTypeGightEndlayers {
			rank := provide.IsFirst(rankMember, k, p.serverID)
			if len(rank) == 0 {
				cansend = 1
			}
		}
		serverId = p.serverID

	}
	if RankTypeNameServer, ok := constant.RankTypeNameServer[int64(k)]; ok && RankTypeNameServer {
		ServerInfo, err := provide.GetAllServerInfo()
		if err != nil {
			return
		}
		for _, v := range ServerInfo {
			if v.ServerId == p.serverID {
				servername = v.ServerName
				// 这里与一站到底冲突了
				RankServerId = p.serverID * 100
				break
			}
		}
	}

	provide.RankChangeTCP(&protocol.RankUserInfoInput{
		Uid:          rankMember,
		Type:         int32(k),
		Avatar:       p.head,
		Name:         p.name,
		Value:        point,
		Sex:          int32(p.sex),
		Job:          int32(p.job),
		Mysql:        true,
		Compare:      constant.RankTypeServerCompare[int64(k)],
		ServerId:     serverId,
		RankServerId: RankServerId,
		ServerName:   servername,
	})
	if RankTypeServer, ok := constant.RankTypeServer[int64(k)]; ok && RankTypeServer {
		if cansend == 1 {
			rank := provide.IsFirst(rankMember, k, p.serverID)
			if len(rank) > 0 {
				strSend := []string{p.name, constant.RankTypeName[k]}
				// 发送消息给所有在线用户
				defaultManager.Lamp(int32(constant.LampTopRankChanged), p.serverID, strSend)
			}
		}
	}
}

// 生成随机属性
func (p *Player) upgradeRandomAttr(item *pb.ItemInfo, itemCfg *global.ItemConfig) error {
	if item.Type != pb.ItemType_EQUIPMENT || item.Equip == nil {
		return errutil.ErrConfigNotFound
	}

	createCfg := global.GetRandomCreatCfg(int32(item.Equip.Quality), int32(itemCfg.Levellimit), int32(itemCfg.Equiptype))
	if createCfg == nil {
		return errutil.ErrConfigNotFound
	}

	var attrNum, distribUid, attrPart, weight int
	weight = global.RandIn(10000)
	for i := 0; i < len(createCfg.CatalogueNum); i++ {
		if weight < int(createCfg.CatalogueNum[i].Value) {
			attrNum = int(createCfg.CatalogueNum[i].Key)
			break
		}
	}

	if attrNum == 0 {
		return nil
	} else if attrNum > len(createCfg.KindNum) {
		attrNum = len(createCfg.KindNum)
	}

	attrList := []int{}
	tmpKindWeight := []global.KeyValue{}
	for _, v := range createCfg.KindNum {
		tmpKindWeight = append(tmpKindWeight, global.KeyValue{Key: v.Key, Value: v.Value})
	}

	fcontains := func(slice []int, val int) bool {
		for _, v := range slice {
			if v == val {
				return true
			}
		}
		return false
	}

	for i := 0; i < attrNum; i++ {
		if len(tmpKindWeight) == 0 {
			break
		}

		sort.Slice(tmpKindWeight, func(i, j int) bool {
			return tmpKindWeight[i].Value > tmpKindWeight[j].Value
		})
		weight = global.RandIn(int(tmpKindWeight[0].Value))
		sort.Slice(tmpKindWeight, func(i, j int) bool {
			return tmpKindWeight[i].Value < tmpKindWeight[j].Value
		})

		for wi, wv := range tmpKindWeight {
			if weight < int(wv.Value) && !fcontains(attrList, int(wv.Key)) {
				distribUid = int(wv.Key)
				attrList = append(attrList, distribUid)
				// 去重
				copy(tmpKindWeight[wi:], tmpKindWeight[wi+1:])
				tmpKindWeight = tmpKindWeight[:len(tmpKindWeight)-1]
				break
			}
		}

		// 随机分段
		weight = global.RandIn(10000)
		for _, v := range createCfg.DropSet {
			if weight < int(v.Value) {
				attrPart = int(v.Key)
				break
			}
		}

		if distribUid == 0 || attrPart == 0 {
			continue
		}
		distribCfg := global.GetRandomDistribCfg(int32(distribUid), int32(attrPart))
		if distribCfg == nil {
			continue
		}

		disCfg, ok := distribCfg.Interval[int32(item.Equip.Quality)]
		if !ok {
			continue
		}
		var statValue int32
		if disCfg.Max <= disCfg.Min {
			statValue = disCfg.Max
		} else {
			statValue = int32(global.RandIn(int(disCfg.Max)-int(disCfg.Min))+1) + disCfg.Min
		}
		if item.Equip == nil {
			item.Equip = &pb.EquipInfo{Attributes: []*pb.Stats{}}
		}
		item.Equip.Attributes = append(item.Equip.Attributes, &pb.Stats{Attr: pb.Attribute(distribCfg.Kind), Num: statValue})
		distribUid = 0
		attrPart = 0

		p.logger.Debugf("生成随机属性i=%d itemid=%d UUID=%d  key=%d  value=%d   attrlist=%v", i, item.CfgId, distribCfg.UUID, distribCfg.Kind, statValue, attrList)
	}

	return nil
}

func (p *Player) recharge4vip(cny int64) {
	old := p.vip
	p.recharge += cny
	for _, v := range global.VIPCfg {
		if p.recharge >= int64(v.NeedExp) && p.vip < v.VIPLevel {
			p.vip = v.VIPLevel
		}
	}
	p.syncUserBase()
	if old != p.vip {
		p.refreshStats()
	}
}

func (p *Player) changeRoleName(name string) (string, error) {
	if db.IsUserExistsByName(name) {
		return p.name, errutil.ErrAccountExists
	}

	if len(name) > 21 || len(name) < 6 {
		return p.name, errutil.ErrRoleNameLen
	}
	if strings.ToLower(viper.GetString("core.env")) == constant.EnvProd {
		if err := provider.WeChatV2.CheckMessageSecurity(p.accountName, name); err != nil {
			return p.name, err
		}
	}

	if sensitive.ContainsSensitiveWord(name) || !sensitive.IsValidName(name) {
		return p.name, errutil.ErrSensitiveWord
	}

	Rename := global.GlobalCfg.BaseValue.Rename
	// 校验背包情况
	res := p.checkTakeSource(int32(Rename.Needitem), int64(Rename.Neednum))
	if !res {
		return p.name, errutil.ErrItemNotEnough
	}
	// 减少道具
	p.takeSource(int32(Rename.Needitem), int64(Rename.Neednum), constant.ItemDelChangeRoleName)
	p.name = name
	u := &model.User{}
	u.Id = p.uid
	u.Name = name
	if err2 := db.UpdateUser(u); err2 != nil {
		return p.name, err2
	}
	return name, nil
}

// 强化
func (p *Player) enhance(id pb.EquipSlot, partnerslotid int32) (bool, int32, error) {
	// 查询当前槽位的信息
	var equipsInfo *Slot
	if partnerslotid == 0 {
		equipsInfo = &p.equips[pb.EquipSlot(id)]

		// 最高等级不让操作
		if equipsInfo.Enhance >= (p.level * global.GlobalCfg.BaseValue.Equipstrenghthen.Rate) {
			return false, -1, errutil.ErrMaxLevel
		}
	} else {
		equipsInfo = &p.getPartnerSlotInfo(partnerslotid).Equips[pb.EquipSlot(id)]

		if partner := p.getPartnerInfoBySlotId(partnerslotid); partner != nil {
			if equipsInfo.Enhance >= (partner.level * global.GlobalCfg.BaseValue.Equipstrenghthen.Rate) {
				return false, -1, errutil.ErrMaxLevel
			}
		} else {
			return false, -1, errutil.ErrPartnerSlotNil
		}
	}

	// 槽位的强化，拼接uuid
	uuid := int32(p.job%10*1000) + equipsInfo.Enhance
	// 目前升星所需要的信息
	enhanceNeed, err := global.GetEnhanceCfg(uuid)
	if err != nil {
		return false, -1, err
	}
	// 校验背包情况
	// 目前强化所需要的材料
	for _, v := range enhanceNeed.Consume {
		if !p.checkTakeSource(v.ID, int64(v.Num)) {
			return false, equipsInfo.Enhance, errutil.ErrItemNotEnough
		}
	}
	Cnum := int32(0)
	Pnum := int32(0)
	// 减少道具
	for _, v := range enhanceNeed.Consume {
		if v.ID == int32(constant.ItemTypeCoin) {
			Cnum = v.Num
		} else {
			Pnum = v.Num
		}
		p.takeSource(int32(v.ID), int64(v.Num), constant.ItemDelGame)
	}
	// 升级
	oldLevel := equipsInfo.Enhance
	equipsInfo.Enhance++
	newLevel := equipsInfo.Enhance
	// 任务上报
	p.tasks.dispatch(pb.TargetType_ANY_EQUIP_STRENGTHENED_TO_LEVEL, newLevel, 1)
	oldcombat := p.totalCombat
	p.refreshStats()
	newcombat := p.totalCombat
	p.upEnhanceShushuDate(partnerslotid, int32(id), Cnum, Pnum, newcombat-oldcombat, oldLevel, newLevel)
	return true, equipsInfo.Enhance, nil
}

func (p *Player) upEnhanceShushuDate(partnerslotid int32, equip_part int32, Cnum int32, Pnum int32, combat int32, oldLevel int32, newLevel int32) {
	// 创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	// upBaseInfo的数据合并进去
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	shushuDate["name"] = "equip_position_strengthen"
	// 新增字段
	shushuDate["partner_num"] = partnerslotid // 可根据实际情况填写伙伴数量
	shushuDate["equip_part"] = equip_part     // 可根据实际情况填写装备部位
	shushuDate["cost_item_num"] = Pnum        // 可根据实际消耗道具数量填写
	shushuDate["cost_gold"] = Cnum            // 可根据实际消耗金币数量填写
	// shushuDate["equip_power"] = 0              // 可根据实际装备战力填写
	shushuDate["equip_power_up"] = combat      // 可根据实际装备战力提升填写
	shushuDate["strengthen_after"] = newLevel  // 可根据实际强化等级填写
	shushuDate["strengthen_before"] = oldLevel // 可根据实际强化等级填写
	shushumain.ReportData(shushuDate)
}

// 升星
func (p *Player) stars(id pb.EquipSlot, partnerslotid int32) (bool, int32, error) {
	// 查询当前槽位的信息
	var equipsInfo *Slot
	if partnerslotid == 0 {
		equipsInfo = &p.equips[pb.EquipSlot(id)]
	} else {
		equipsInfo = &p.getPartnerSlotInfo(partnerslotid).Equips[pb.EquipSlot(id)]
	}
	if equipsInfo == nil {
		return false, -1, errutil.ErrEquipSlot
	}
	// 最高等级不让操作
	if equipsInfo.Stars >= global.GlobalCfg.BaseValue.Equipstar.Maxlevel {
		return false, -1, errutil.ErrMaxLevel
	}
	// 槽位的强化，拼接uuid
	uuid := int32(p.job*100) + equipsInfo.Stars
	// 目前升星所需要的信息
	StarsNeed, err := global.GetStarsCfg(uuid)
	if err != nil {
		return false, -1, err
	}
	// 校验背包情况
	// 目前升星所需要的材料
	for _, v := range StarsNeed.MaterialID {
		if !p.checkTakeSource(v.ID, int64(v.Num)) {
			return false, -1, errutil.ErrItemNotEnough
		}
	}
	Cnum := int32(0)
	Pnum := int32(0)
	oldstars := equipsInfo.Stars
	success := false
	// 减少道具
	for _, v := range StarsNeed.MaterialID {
		if v.ID == int32(constant.ItemTypeCoin) {
			Cnum = v.Num
		} else {
			Pnum = v.Num
		}
		p.takeSource(int32(v.ID), int64(v.Num), constant.ItemDelGame)
	}
	// 随机分段
	weight := global.RandIn(int(BaseRate))
	// 查询装备信息
	var equipCfg *global.ItemConfig
	if equipsInfo.ItemId > 0 {
		equipCfg2 := p.getItemByUid(equipsInfo.ItemId)
		if equipCfg2 == nil {
			return false, equipsInfo.Stars, err
		}
		equipCfg, err = global.GetItemCfg(int32(equipCfg2.CfgId))
		if err != nil {
			return false, equipsInfo.Stars, err
		}
	}

	haveUp := false
	if p.stat(pb.Attribute_EQUIP_STAR_PERCENTAGE) > 0 {
		haveUp = true
	}
	if float64(weight) < float64(StarsNeed.SuccessRate)*(1+float64(p.stat(pb.Attribute_EQUIP_STAR_PERCENTAGE))/BaseRate) {
		// 升级
		equipsInfo.Stars++
		if equipsInfo.Stars >= 10 && equipsInfo.Stars%5 == 0 {
			context := []string{p.name, strconv.Itoa(int(equipsInfo.Stars))}
			defaultManager.Lamp(int32(constant.LampEquipmentStarUpgrade), p.serverID, context)
		}
		success = true
		newstars := equipsInfo.Stars
		oldcombat := p.totalCombat
		p.refreshStats()
		newcombat := p.totalCombat
		p.tasks.dispatch(pb.TargetType_ANY_EQUIP_STAR_UP_TIMES, 0, 1)
		p.upStarsShushuDate(equipCfg, equipsInfo.Enhance, partnerslotid, int32(id), Cnum, Pnum, newcombat-oldcombat, oldstars, newstars, success, weight, haveUp, p.stat(pb.Attribute_EQUIP_STAR_PERCENTAGE))
		return true, equipsInfo.Stars, nil
	} else {
		equipsInfo.Stars = StarsNeed.FailLevel
	}
	newstars := equipsInfo.Stars
	oldcombat := p.totalCombat
	p.refreshStats()
	newcombat := p.totalCombat

	p.upStarsShushuDate(equipCfg, equipsInfo.Enhance, partnerslotid, int32(id), Cnum, Pnum, newcombat-oldcombat, oldstars, newstars, success, weight, haveUp, p.stat(pb.Attribute_EQUIP_STAR_PERCENTAGE))

	return false, equipsInfo.Stars, nil
}

func (p *Player) upStarsShushuDate(equipsDetail *global.ItemConfig, level int32, partnerslotid int32, equip_part int32, Cnum int32, Pnum int32, combat int32, oldLevel int32, newLevel int32, success bool, weight int, haveUp bool, per int32) {
	// 创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	// upBaseInfo的数据合并进去
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	shushuDate["name"] = "equip_star_up"
	// 新增字段
	if equipsDetail == nil {
		shushuDate["equip_id"] = 0      // 可根据实际装备ID填写
		shushuDate["equip_quality"] = 0 // 可根据实际装备品质填写
		shushuDate["equip_num"] = 0     // 可根据实际装备数量填写
		shushuDate["equip_name"] = ""   // 可根据实际装备名称填写
		shushuDate["equip_job"] = 0     // 可根据实际装备职业填写
	} else {
		shushuDate["equip_id"] = equipsDetail.ID           // 可根据实际装备ID填写
		shushuDate["equip_num"] = 1                        // 可根据实际装备数量填写
		shushuDate["equip_quality"] = equipsDetail.Quality // 可根据实际装备品质填写
		shushuDate["equip_name"] = equipsDetail.Name       // 可根据实际装备名称填写
		shushuDate["equip_job"] = equipsDetail.Occupylimit // 可根据实际装备职业填写
	}

	shushuDate["equip_part"] = equip_part     // 可根据实际装备部位填写
	shushuDate["equip_level"] = level         // 可根据实际装备等级填写
	shushuDate["partner_num"] = partnerslotid // 可根据实际情况填写伙伴数量
	shushuDate["cost_item_num"] = Pnum        // 可根据实际消耗道具数量填写
	shushuDate["cost_gold"] = Cnum            // 可根据实际消耗金币数量填写
	shushuDate["star_after"] = newLevel       // 可根据实际升星后星级填写
	shushuDate["star_before"] = oldLevel      // 可根据实际升星前星级填写
	shushuDate["is_success"] = success        // 可根据实际升星是否成功填写
	shushuDate["success_rate"] = weight       // 可根据实际升星成功率填写
	shushuDate["use_symbol"] = haveUp         // 可根据实际使用符文数量填写
	shushuDate["symbol_name"] = per           // 可根据实际符文名称填写
	// shushuDate["equip_power"] = 0               // 可根据实际装备战力填写
	shushuDate["equip_power_up"] = combat // 可根据实际装备战力提升填写
	shushumain.ReportData(shushuDate)
}

// 洗炼
func (p *Player) equipRefine(itemUid int64, attr pb.Attribute, random bool) error {
	item := p.getItemByUid(itemUid)
	if item == nil || item.Equip == nil {
		return errutil.ErrItemNotFound
	}

	bFind := false
	for _, v := range item.Equip.Attributes {
		if v.Attr == attr {
			bFind = true
		}
	}
	if !bFind {
		return errutil.ErrItemNotFound
	}

	itemCfg, err := global.GetItemCfg(item.CfgId)
	if err != nil {
		return err
	}
	var costId int32
	var costNum int64
	if random {
		for _, v := range global.GlobalCfg.Rifinerandom.Cost {
			if v.ID == itemCfg.Quality {
				if !p.checkTakeSource(v.ItemID, int64(v.ItemNum)) {
					return errutil.ErrItemNotEnough
				}
				costId = v.ItemID
				costNum = v.ItemNum
				break
			}
		}
	} else {
		for _, v := range global.GlobalCfg.Rifinedirect.Cost {
			if v.ID == itemCfg.Quality {
				if !p.checkTakeSource(v.ItemID, int64(v.ItemNum)) {
					return errutil.ErrItemNotEnough
				}
				costId = v.ItemID
				costNum = v.ItemNum
				break
			}
		}
	}
	p.takeSource(costId, costNum, constant.ItemDelRefine)

	if err = p.equipRefineRandom(item, attr, random); err != nil {
		return err
	}
	p.lastEquipRefine.itemId = itemUid
	p.lastEquipRefine.random = random
	p.lastEquipRefine.attr = attr

	return nil
}

func (p *Player) equipRefineRandom(item *pb.ItemInfo, attr pb.Attribute, random bool) error {
	itemCfg, err := global.GetItemCfg(item.CfgId)
	if err != nil {
		return err
	}

	createCfg := global.GetRandomCreatCfg(int32(item.Equip.Quality), int32(itemCfg.Levellimit), int32(itemCfg.Equiptype))
	if createCfg == nil {
		return errutil.ErrConfigNotFound
	}

	var weight, distribUid, attrPart int
	var distribCfg *global.RandomDistrib
	if random {
		tmpKindWeight := []global.KeyValue{}
		for _, v := range createCfg.KindNum {
			// 从随机列表中去除装备已有的高级属性，并且不是要洗炼的属性
			var bFind bool
			for _, v2 := range item.Equip.Attributes {
				if v2.Attr == pb.Attribute(v.Key/1000) && attr != pb.Attribute(v.Key) {
					bFind = true
					break
				}
			}
			if !bFind {
				tmpKindWeight = append(tmpKindWeight, global.KeyValue{Key: v.Key, Value: v.Value})
			}
		}

		sort.Slice(tmpKindWeight, func(i, j int) bool {
			return tmpKindWeight[i].Value > tmpKindWeight[j].Value
		})

		weight = global.RandIn(int(tmpKindWeight[0].Value))
		sort.Slice(tmpKindWeight, func(i, j int) bool {
			return tmpKindWeight[i].Value < tmpKindWeight[j].Value
		})

		for _, wv := range tmpKindWeight {
			if weight < int(wv.Value) {
				distribUid = int(wv.Key)
				break
			}
		}

		// 随机分段
		weight = global.RandIn(10000)
		for _, v := range createCfg.RefiningSet {
			if weight < int(v.Value) {
				attrPart = int(v.Key)
				break
			}
		}

		if distribUid == 0 || attrPart == 0 {
			return errutil.ErrConfigNotFound
		}
		distribCfg = global.GetRandomDistribCfg(int32(distribUid), int32(attrPart))
		if distribCfg == nil {
			return errutil.ErrConfigNotFound
		}
	} else {

		weight = global.RandIn(10000)
		for _, v := range createCfg.RefiningSet {
			if weight < int(v.Value) {
				attrPart = int(v.Key)
				break
			}
		}
		distribCfg = global.GetRandomDistribCfgByKind(int32(attr), int32(attrPart))
		if distribCfg == nil {
			return errutil.ErrConfigNotFound
		}
	}

	disCfg, ok := distribCfg.Interval[itemCfg.Quality]
	if !ok {
		return errutil.ErrConfigNotFound
	}
	var statValue int32
	if disCfg.Max <= disCfg.Min {
		statValue = disCfg.Max
	} else {
		statValue = int32(global.RandIn(int(disCfg.Max)-int(disCfg.Min))+1) + disCfg.Min
	}

	p.lastEquipRefine.newStat = pb.Stats{Attr: pb.Attribute(distribCfg.Kind), Num: statValue}
	return nil
}

func (p *Player) equipRefineReplace(replace bool) (*pb.ItemInfo, error) {
	item := p.getItemByUid(p.lastEquipRefine.itemId)
	if item == nil {
		return nil, errutil.ErrItemNotFound
	}
	if p.lastEquipRefine.itemId == 0 || p.lastEquipRefine.newStat.Attr == 0 ||
		p.lastEquipRefine.newStat.Num == 0 || p.lastEquipRefine.attr == 0 {
		return nil, errutil.ErrItemNotFound
	}

	logger.Debugf("最后于一次洗炼属性 类型: %d 得到的新属性 类型:%d 值: %d", p.lastEquipRefine.attr, p.lastEquipRefine.newStat.Attr, p.lastEquipRefine.newStat.Num)
	if replace {
		newStats := []*pb.Stats{}
		for _, v := range item.Equip.Attributes {
			if v.Attr == p.lastEquipRefine.attr {
				logger.Debugf("去除原有属性与最后洗炼属性相同类型: %d ", v.Attr)
				continue
			}
			newStats = append(newStats, v)
		}
		newStats = append(newStats, &p.lastEquipRefine.newStat)

		item.Equip.Attributes = item.Equip.Attributes[:0]
		for _, attr := range newStats {
			item.Equip.Attributes = append(item.Equip.Attributes, &pb.Stats{Attr: attr.Attr, Num: attr.Num})
			logger.Debugf("添加新属性 类型:%d  值:%d", attr.Attr, attr.Num)
		}
		p.refreshStats()
	}

	p.resetRefineInfo()
	return item, nil
}

func (p *Player) resetRefineInfo() {
	p.lastEquipRefine.attr = 0
	p.lastEquipRefine.itemId = 0
	p.lastEquipRefine.newStat = pb.Stats{}
	p.lastEquipRefine.random = false
}

// 穿脱技能opType 1穿 ，2脱
func (p *Player) skillChange(opType int32, slot pb.SkillsSlot, Uuid int32) error {
	bFind := false
	res := p.getSkill(Uuid)
	if res == nil {
		for _, v := range p.partnerSlots {
			partner := p.getPartnerInfoBySlotId(v.SlotId)
			if partner == nil {
				continue
			}
			for _, v2 := range partner.skills {
				if v2.CfgId == Uuid {
					bFind = true
					break
				}
			}
		}
	} else {
		bFind = true
	}
	if !bFind {
		return errutil.ErrChooseRole
	}

	if slot < pb.SkillsSlot_INVALID_SkillsSlot || slot > pb.SkillsSlot_MaxSkillsSlot {
		return errutil.ErrSkillsSlot
	}
	SkillInfo, ok := global.GetSkillCfg(Uuid)
	if ok != nil {
		return errutil.ErrConfigNotFound
	}
	if SkillInfo.SType != 3 && slot == 0 {
		return errutil.ErrSkillsSlotSpecial
	}
	if opType == 1 {
		p.skillsSlot[slot] = Uuid
	} else if opType == 2 {
		p.skillsSlot[slot] = 0
	}
	return nil
}

func (p *Player) getSlotSkillByType(sType int32) (*PlayerSkill, *MapObject) {
	for _, v := range p.skillsSlot {
		if v/100 == sType {
			if skill := p.getSkill(v); skill != nil {
				return skill, p.MapObject
			}

			for _, v2 := range p.partnerSlots {
				partner := p.getPartnerInfoBySlotId(v2.SlotId)
				if partner == nil {
					continue
				}
				if skill := partner.getSkill(v); skill != nil {
					return skill, partner.MapObject
				}
			}
		}
	}
	return nil, nil
}

// 升级技能
func (p *Player) skillsUpgrade(id int32, paruid int64) (int32, error) {
	// 当前技能等级
	// 查询当前槽位的信息
	var skill_proxy *PlayerSkill
	if paruid == 0 {
		for i, v := range p.skills {
			if v.CfgId == id {
				skill_proxy = p.skills[i]
				break
			}
		}
	} else {
		partner := p.getPartnerInfo(paruid)
		if partner == nil {
			return -1, errutil.ErrChooseRole
		}
		for i, v2 := range partner.skills {
			if v2.CfgId == id {
				skill_proxy = partner.skills[i]
				break
			}
		}
	}

	if skill_proxy == nil {
		return -1, errutil.ErrChooseRole
	}

	maxLevel := int32(0)
	skillCfg := global.SkillCfg
	// 目前升级所需要的信息
	SkillInfo, ok := global.GetSkillCfg(id)
	if ok != nil {
		return id, errutil.ErrConfigNotFound
	}
	for _, v := range skillCfg {
		if SkillInfo.SkillProfession == v.SkillProfession && SkillInfo.SkillID == v.SkillID {
			maxLevel++
		}
	}
	// 最高等级不让操作
	if SkillInfo.Level >= maxLevel {
		return id, errutil.ErrMaxLevel
	}
	// 校验背包情况
	// 目前强化所需要的材料
	for _, v := range SkillInfo.PromoteConsume {
		if !p.checkTakeSource(v.ID, int64(v.Num)) {
			return id, errutil.ErrItemNotEnough
		}
	}
	// 减少道具
	cnum := int32(0)
	pnum := int32(0)
	for _, v := range SkillInfo.PromoteConsume {
		if v.ID == int32(constant.ItemTypeCoin) {
			cnum += int32(v.Num)
		} else {
			pnum += int32(v.Num)
		}
		p.takeSource(v.ID, int64(v.Num), constant.ItemDelSkillUpgrade)
	}
	// 升级
	skill_proxy.CfgId++

	// 升级之后uuid会变，所以要把槽位里面的信息也同步更新
	for i, v := range p.skillsSlot {
		if v == id {
			p.skillsSlot[i] = skill_proxy.CfgId
			break
		}
	}
	level := SkillInfo.Level
	// 更新战斗力
	oldCombat := p.totalCombat
	p.refreshStats()
	newCombat := p.totalCombat
	p.tasks.dispatch(pb.TargetType_SKILL_UPGRADING_TIMES, 0, 1)

	p.upSkillShushuDate(SkillInfo, cnum, pnum, level, newCombat-oldCombat)
	return skill_proxy.CfgId, nil
}

func (p *Player) upSkillShushuDate(SkillInfo *global.SkillConfig, cnum int32, pnum int32, level int32, combat int32) {
	// 创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	// upBaseInfo的数据合并进去
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	// 在合并点其他的数据
	shushuDate["name"] = "skill_level_up"
	// 新增字段
	shushuDate["skill_id"] = SkillInfo.UID
	shushuDate["skill_name"] = SkillInfo.Name
	shushuDate["cost_item_num"] = pnum //
	shushuDate["cost_gold"] = cnum     // 没有消耗金币
	// shushuDate["skill_rank"] = genstoneCfg.Quality
	shushuDate["level_before"] = level - 1 // 没有等级变化
	shushuDate["level_after"] = level      // 没有等级变化
	shushuDate["power_up"] = combat
	shushumain.ReportData(shushuDate)
}

// 进阶技能
func (p *Player) skillsAdvanced(id int32, paruid int64) (int32, error) {
	// 技能id
	skillID := id / 100

	var skill_proxy *PlayerSkill
	if paruid == 0 {
		for i, v := range p.skills {
			if v.CfgId == id {
				skill_proxy = p.skills[i]
				break
			}
		}
	} else {
		partner := p.getPartnerInfo(paruid)
		if partner == nil {
			return -1, errutil.ErrChooseRole
		}
		for i, v2 := range partner.skills {
			if v2.CfgId == id {
				skill_proxy = partner.skills[i]
				break
			}
		}
	}

	// 目前的进阶等级
	advancedLevel := skill_proxy.AdvancedLevel

	// 目前进阶最大的等级
	SkillInfo, ok := global.GetSkillCfg(id)
	if ok != nil {
		return advancedLevel, errutil.ErrConfigNotFound
	}
	// 最高等级不让操作
	if advancedLevel >= SkillInfo.CanupperLevel {
		return advancedLevel, errutil.ErrMaxLevel
	}
	// 查询下一进阶需要的材料
	skillCfg := global.SkillCfg
	needConfig := global.SkillConfig{}
	for _, v := range skillCfg {
		if SkillInfo.SkillProfession == v.SkillProfession && advancedLevel+1 == v.CanupperLevel && skillID == v.SkillID && len(v.UpperlevelConsume) > 0 {
			needConfig = global.SkillConfig{
				UpperlevelConsume: v.UpperlevelConsume,
			}
			break
		}
	}

	// 校验背包情况
	// 目前进阶所需要的材料
	for _, v := range needConfig.UpperlevelConsume {
		if !p.checkTakeSource(v.ID, int64(v.Num)) {
			return advancedLevel, errutil.ErrItemNotEnough
		}
	}
	// 减少道具
	cnum := int32(0)
	pnum := int32(0)
	for _, v := range needConfig.UpperlevelConsume {
		if v.ID == int32(constant.ItemTypeCoin) {
			cnum += int32(v.Num)
		} else {
			pnum += int32(v.Num)
		}
		p.takeSource(v.ID, int64(v.Num), constant.ItemDelSkillUpgrade)
	}
	// 升级
	skill_proxy.AdvancedLevel++
	// 更新战斗力
	oldCombat := p.totalCombat
	p.refreshStats()
	newCombat := p.totalCombat
	p.refreshStats()
	p.upSkillGradeShushuDate(SkillInfo, cnum, pnum, skill_proxy.AdvancedLevel, newCombat-oldCombat)
	return skill_proxy.AdvancedLevel, nil
}

func (p *Player) upSkillGradeShushuDate(SkillInfo *global.SkillConfig, cnum int32, pnum int32, level int32, combat int32) {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	shushuDate["name"] = "skill_upgrade"
	shushuDate["skill_id"] = SkillInfo.UID
	shushuDate["skill_name"] = SkillInfo.Name
	shushuDate["cost_item_num"] = pnum
	shushuDate["cost_gold"] = cnum
	shushuDate["rank_before"] = level - 1
	shushuDate["rank_after"] = level
	shushuDate["power_up"] = combat
	shushumain.ReportData(shushuDate)
}

// 爬塔收益
func (p *Player) soloTowerOfflineReward() error {
	cfg := global.GetEndlesstrialsCfg(p.SoloTowerInfo.Layers)
	if cfg == nil {
		return errutil.ErrConfigNotFound
	}
	offsetMinite := (time.Now().Unix() - p.SoloTowerInfo.RewardTime.Unix())
	reward := []global.IdNum{}

	for _, v := range cfg.OfflineReward {
		multiple := offsetMinite / int64(v.Gap)
		if multiple == 0 {
			continue
		}
		num := int64(v.Num) * multiple
		if num > int64(v.Max) {
			num = int64(v.Max)
		}

		reward = append(reward, global.IdNum{ID: v.ID, Num: int32(num)})

		if !p.checkAddSource(v.ID, num) {
			return errutil.ErrBagFull
		}
	}
	sourceInfo := []global.IdNum{}
	for _, v := range reward {
		sourceInfo = append(sourceInfo, global.IdNum{ID: v.ID, Num: v.Num})
		// p.addSource(v.ID, int64(v.Num), constant.ItemAddSoloTower)
	}
	p.addSourceBatch(sourceInfo, constant.ItemAddSoloTower)
	p.SoloTowerInfo.RewardTime = time.Now()

	return nil
}

// 是否是升星石
func (p *Player) IsStarsLevel(CfgId int32, Num int32) (int32, error) {
	Starlevel := global.GlobalCfg.Starstone.Starlevel
	checkId := int32(-1)
	slot := int32(0)
	for _, v := range Starlevel {
		if v.ID == CfgId {
			checkId = v.Level
			slot = v.Slot
			break
		}
	}
	if checkId != -1 {
		for i := 1; i <= int(Num); i++ {
			if slot != 0 {
				if p.equips[slot].Stars < checkId {
					p.equips[slot].Stars = checkId
				} else {
					return int32(i - 1), errutil.StarsNoUseError
				}
			} else {
				selectId := make([]int, 0)
				for i, v := range p.equips {
					// 需要随机给他一个槽位升满
					if v.Stars < checkId && i != 0 {
						selectId = append(selectId, i)
					}
				}
				if len(selectId) == 0 {
					return int32(i - 1), errutil.StarsNoUseError
				}
				weight := global.RandIn(len(selectId))
				p.equips[selectId[weight]].Stars = checkId
			}
		}
		p.SyncUserEquipsInfo()
		p.refreshStats()

	}
	return Num, nil
}

// 是否是命格石头
func (p *Player) IsFate(CfgId int32, Num int32) ([]int, int32, error) {
	FateInfo := global.GlobalCfg.Fate.Basevalue.Item
	checkId := int32(-1)
	slot := int64(0)
	for _, v := range FateInfo {
		if v.ID == CfgId {
			checkId = v.ID
			slot = int64(v.Num)
			break
		}
	}
	perAll := make([]int, 0)
	if checkId != -1 {
		for i := 1; i <= int(Num); i++ {
			fatecfg, err := global.GetFateCfg(p.fateLevel)
			if err != nil {
				return perAll, int32(i), errutil.ErrConfigNotFound
			}
			per := fateStageRand()
			perAll = append(perAll, per)
			EndExp := p.fateExp + slot*int64(per)
			FateCfg := global.FateCfg
			// 查询这一重需要多少经验
			AllExp := fatecfg.FateExp
			for j := 1; j <= len(FateCfg)+1; j++ {
				fatecfg, err := global.GetFateCfg(p.fateLevel - int32(j))
				if err != nil || len(fatecfg.AdvancedNeed) > 0 {
					break
				}
				AllExp = AllExp + fatecfg.FateExp
			}
			if p.fateExp >= int64(AllExp) {
				if len(fatecfg.AdvancedNeed) > 0 {
					p.refreshStats()
					p.syncUserBase()
					return perAll, int32(i), errutil.ErrMaxLevel
				} else {
					p.fateLevel++
					p.fateExp = EndExp

				}
			} else {
				p.fateExp = EndExp
			}
		}
		p.refreshStats()
		p.syncUserBase()
	}
	return perAll, Num, nil
}

// 是否是命格石头
func (p *Player) IsPartner(CfgId int32, Num int32) ([]int, int32, error) {
	FateInfo := global.GlobalCfg.Fate.Basevalue.Item
	checkId := int32(-1)
	slot := int64(0)
	for _, v := range FateInfo {
		if v.ID == CfgId {
			checkId = v.ID
			slot = int64(v.Num)
			break
		}
	}
	perAll := make([]int, 0)
	if checkId != -1 {
		for i := 1; i <= int(Num); i++ {
			fatecfg, err := global.GetFateCfg(p.fateLevel)
			if err != nil {
				return perAll, int32(i), errutil.ErrConfigNotFound
			}
			per := fateStageRand()
			perAll = append(perAll, per)
			EndExp := p.fateExp + slot*int64(per)
			FateCfg := global.FateCfg
			// 查询这一重需要多少经验
			AllExp := fatecfg.FateExp
			for j := 1; j <= len(FateCfg)+1; j++ {
				fatecfg, err := global.GetFateCfg(p.fateLevel - int32(j))
				if err != nil || len(fatecfg.AdvancedNeed) > 0 {
					break
				}
				AllExp = AllExp + fatecfg.FateExp
			}
			if p.fateExp >= int64(AllExp) {
				if len(fatecfg.AdvancedNeed) > 0 {
					p.refreshStats()
					p.syncUserBase()
					return perAll, int32(i), errutil.ErrMaxLevel
				} else {
					p.fateLevel++
					p.fateExp = EndExp

				}
			} else {
				p.fateExp = EndExp
			}
		}
		p.refreshStats()
		p.syncUserBase()
	}
	return perAll, Num, nil
}

// 命格暴击
func fateStageRand() int {
	fateStage := global.GlobalCfg.Fate.Rate.Stage
	allPriority := 0
	beginPriority := 0
	// 活动内容
	for _, fateStageInfo := range fateStage {
		allPriority = allPriority + int(fateStageInfo.Pro)
	}
	rand := global.RandIn(allPriority) + 1
	for _, fateStageInfo := range fateStage {
		if beginPriority+int(fateStageInfo.Pro) >= rand {
			return fateStageInfo.Rate
		}
		beginPriority = beginPriority + int(fateStageInfo.Pro)
	}
	return -1
}

func (p *Player) SetPlayerInfo(fateLevel int32, fateExp int64) {
	// 升级
	p.fateLevel = fateLevel
	p.fateExp = fateExp
	// 更新战斗力
	p.refreshStats()
}

func (p *Player) SendMail(source string, attachment []protocol.Attachment, args ...string) {
	mail := protocol.MailAddRequest{
		Uid:          -1,
		RecipientUid: p.uid,
		Title:        constant.EmailContentSystem,

		ChannelCode: source,
		Attachment:  attachment,
	}

	if len(args) > 0 {
		mail.Desc = args[0]
	} else {
		mail.Desc = constant.EmailContentBagFull
	}

	if p.mailCounter >= int(global.GlobalCfg.BaseValue.Mailmax.Num) {
		packet := &pb.SystemNotificationPush{Type: pb.Notification_MAIL_FULL}
		p.pushMsg(protocol.RouteSystemNotificationPush, packet)
		return
	}

	p.SetMailCounter(constant.MailCounterOPTypeAdd, 1)

	if err := provide.Mailadd(&mail); err != nil {
		packet := &pb.SystemNotificationPush{Type: pb.Notification_MAIL_FULL}
		p.pushMsg(protocol.RouteSystemNotificationPush, packet)
	}
}

// 合成系统
func (p *Player) compose(composeInfo []*pb.ComposeInfo) ([]*pb.ComposeInfo, error) {
	whileAll := make(map[int32]int32)
	for _, v := range global.ComposeCfg {
		for _, v2 := range v.Raw {
			whileAll[v2] = v.ComposeID
		}
	}
	if len(composeInfo) == 0 {
		return nil, errutil.ErrItemNotFound
	}
	if composeInfo[0] == nil {
		return nil, errutil.ErrItemNotFound
	}
	firstRes := p.getItemByUid(composeInfo[0].Uid)
	if firstRes == nil {
		return nil, errutil.ErrItemNotFound
	}
	// 可堆叠数量大于1的都用商品id
	// 判断是不是能穿戴的装备
	ItemCfg, err := global.GetItemCfg(firstRes.CfgId)
	if err != nil {
		return nil, err
	}
	if ItemCfg.Maxnum > 1 {
		beginComposeId := whileAll[firstRes.CfgId]
		res, ok := global.GetComposeCfg(beginComposeId)
		if ok != nil {
			return nil, ok
		}

		change := true
		sourceDel := make([]global.IdNum, 0)
		sourceLen := 0
		for _, v := range composeInfo {
			itemInfo := p.getItemByUid(v.Uid)
			if itemInfo == nil {
				return nil, errutil.ErrItemNotFound
			}
			if !p.checkTakeSourceByUid(v.Uid, int64(v.Num)) {
				return nil, errutil.ErrItemNotEnough
			}
			// 查询是不是都是可以合成的 且都是一个种类
			if whileAll[itemInfo.CfgId] == 0 || beginComposeId != whileAll[itemInfo.CfgId] {
				change = false
			}
			sourceLen = sourceLen + int(v.Num)
			sourceDel = append(sourceDel, global.IdNum{
				ID:  int32(itemInfo.CfgId),
				Num: int32(v.Num),
			})
		}
		if sourceLen%int(res.NeedNum) != 0 {
			return nil, errutil.ErrItemNotEnough
		}
		if !change {
			return nil, errutil.ErrItemNotEnough
		}

		needGoldPer := int(res.NeedGold.Num) * sourceLen / int(res.NeedNum)
		// 校验背包情况
		// 目前合成所需要的必要材料
		if !p.checkTakeSource(res.NeedGold.ID, int64(needGoldPer)) {
			return nil, errutil.ErrItemNotEnough
		}
		// 删除合成需要的材料金币
		p.takeSource(res.NeedGold.ID, int64(needGoldPer), constant.ItemDelGemstone)
		// 删除合成需要的物品
		for _, v := range sourceDel {
			if !p.checkTakeSource(v.ID, int64(v.Num)) {
				return nil, errutil.ErrItemNotEnough
			}
		}
		for _, v := range sourceDel {
			p.takeSource(v.ID, int64(v.Num), constant.ItemDelCompose)
		}
		sourceAddReturn := make([]*pb.ComposeInfo, 0)
		// 随机分段
		whileSend := make(map[int32]int32)
		for i := 1; i <= sourceLen/int(res.NeedNum); i++ {
			weight := global.RandIn(int(BaseRate))
			if float64(weight) < float64(res.SuccessRate) {
				randId := composeRand(res)
				whileSend[randId]++
			}
		}
		var sourceAdd []global.IdNum
		if len(whileSend) > 0 {
			for id, num := range whileSend {
				sourceAdd = append(sourceAdd, global.IdNum{
					ID:  id,
					Num: num,
				})
				sourceAddReturn = append(sourceAddReturn, &pb.ComposeInfo{
					Uid: int64(id),
					Num: num,
				})
			}
			p.addSourceBatch(sourceAdd, constant.ItemAddCompose)

			for _, info := range sourceAdd {
				itemInfo, err := global.GetItemCfg(info.ID)
				if err != nil {
					return nil, err
				}
				if itemInfo.Quality > 4 && itemInfo.NatureMark > 0 {
					strSend := []string{p.name, strconv.Itoa(int(itemInfo.ID))}
					defaultManager.Lamp(int32(constant.LampGemSynthesis), p.serverID, strSend)
				}

			}
			return sourceAddReturn, nil
		} else {
			return nil, errutil.ComposeError
		}

	} else {
		beginComposeId := whileAll[firstRes.CfgId]
		res, ok := global.GetComposeCfg(beginComposeId)
		if ok != nil {
			return nil, ok
		}

		change := true
		sourceDel := make([]global.UUIdNum, 0)
		sourceLen := 0
		for _, v := range composeInfo {
			itemInfo := p.getItemByUid(v.Uid)
			if itemInfo == nil {
				return nil, errutil.ErrItemNotFound
			}
			if !p.checkTakeSourceByUid(v.Uid, int64(v.Num)) {
				return nil, errutil.ErrItemNotEnough
			}
			// 查询是不是都是可以合成的 且都是一个种类
			if whileAll[itemInfo.CfgId] == 0 || beginComposeId != whileAll[itemInfo.CfgId] {
				change = false
			}
			sourceLen = sourceLen + int(v.Num)
			sourceDel = append(sourceDel, global.UUIdNum{
				UID: v.Uid,
				Num: int64(v.Num),
			})
		}
		if sourceLen%int(res.NeedNum) != 0 {
			return nil, errutil.ErrItemNotEnough
		}
		if !change {
			return nil, errutil.ErrItemNotEnough
		}

		// 校验背包情况
		// 目前合成所需要的必要材料
		if !p.checkTakeSource(res.NeedGold.ID, int64(res.NeedGold.Num)) {
			return nil, errutil.ErrItemNotEnough
		}
		// 删除合成需要的材料金币
		p.takeSource(res.NeedGold.ID, int64(res.NeedGold.Num), constant.ItemDelGemstone)
		// 删除合成需要的物品
		p.takeSourceByUidBatch(sourceDel, constant.ItemDelCompose)
		sourceAddReturn := make([]*pb.ComposeInfo, 0)
		// 随机分段
		weight := global.RandIn(int(BaseRate))
		if float64(weight) < float64(res.SuccessRate) {
			whileSend := make(map[int32]int32)
			for i := 1; i <= sourceLen/int(res.NeedNum); i++ {
				randId := composeRand(res)
				whileSend[randId]++

			}
			var sourceAdd []global.IdNum
			for id, num := range whileSend {
				sourceAdd = append(sourceAdd, global.IdNum{
					ID:  id,
					Num: num,
				})
				sourceAddReturn = append(sourceAddReturn, &pb.ComposeInfo{
					Uid: int64(id),
					Num: num,
				})
			}
			p.addSourceBatch(sourceAdd, constant.ItemAddCompose)
			return sourceAddReturn, nil
		} else {
			return nil, errutil.ComposeError
		}
	}
}

// 合成概率分布
func composeRand(ComposeCfg *global.ComposeConfig) int32 {
	fateStage := ComposeCfg.Produced
	allPriority := 0
	beginPriority := 0
	// 活动内容
	for _, fateStageInfo := range fateStage {
		allPriority = allPriority + int(fateStageInfo.Ratio)
	}
	rand := global.RandIn(allPriority) + 1
	for _, fateStageInfo := range fateStage {
		if beginPriority+int(fateStageInfo.Ratio) >= rand {
			ids := fateStageInfo.ID
			idRand := global.RandIn(len(ids))
			return ids[idRand]
		}
		beginPriority = beginPriority + int(fateStageInfo.Ratio)
	}
	return -1
}

// 穿脱宝石opType 1穿 ，2脱
func (p *Player) genstoneChange(opType int32, slot pb.EquipSlot, stoneSlot pb.GemstoneSlot, itemUid, partnerslotid int32) error {
	if stoneSlot < pb.GemstoneSlot_INVALID_GemstoneSlot || stoneSlot > pb.GemstoneSlot_MaxGemstoneSlot {
		return errutil.ErrGenStoneSlot
	}

	var equipsInfo *Slot
	if partnerslotid == 0 {
		equipsInfo = &p.equips[pb.EquipSlot(slot)]
	} else {
		equipsInfo = &p.getPartnerSlotInfo(partnerslotid).Equips[pb.EquipSlot(slot)]
	}
	if equipsInfo == nil {
		return errutil.ErrEquipSlot
	}

	if opType == 1 {
		canSlot := 0
		for _, StageInfo := range global.GlobalCfg.Diamond.Openlevel.Stage {
			intLevel, _ := strconv.Atoi(StageInfo.Level)
			if int(p.level) >= intLevel {
				delInfo, _ := strconv.Atoi(StageInfo.Num)
				if delInfo > canSlot {
					canSlot = delInfo
				}
			}
		}
		if stoneSlot >= pb.GemstoneSlot(canSlot) {
			return errutil.ErrGenStoneSlot
		}
		// 校验背包情况
		// 目前合成所需要的必要材料
		if !p.checkTakeSource(itemUid, int64(1)) {
			return errutil.ErrItemNotEnough
		}
		// 判断是不是能穿戴的装备
		genstoneCfg, err := global.GetItemCfg(itemUid)
		if err != nil {
			return errutil.ErrItemNotEnough
		}
		can := false
		for _, stoneSlotInfo := range genstoneCfg.DiamondPosition {
			if stoneSlotInfo == int(pb.EquipSlot(slot)) {
				can = true
				continue
			}
		}
		if !can {
			return errutil.ErrGenStoneSlot
		}
		if equipsInfo.Gemstone[stoneSlot] > 0 {
			p.addSource(int32(equipsInfo.Gemstone[stoneSlot]), 1, constant.ItemAddGemstone)
		}
		p.takeSource(int32(itemUid), 1, constant.ItemDelGemstone)
		equipsInfo.Gemstone[stoneSlot] = pb.GemstoneSlot(itemUid)
		oldcombat := p.totalCombat
		p.refreshStats()
		newcombat := p.totalCombat
		// 查询装备信息
		var equipCfg *global.ItemConfig
		if equipsInfo.ItemId != 0 {
			equipCfg2 := p.getItemByUid(equipsInfo.ItemId)
			if equipCfg2 == nil {
				return errutil.ErrItemNotEnough
			}
			equipCfg, err = global.GetItemCfg(int32(equipCfg2.CfgId))
			if err != nil {
				return errutil.ErrItemNotEnough
			}
		}
		p.upStoneShushuDate(equipCfg, genstoneCfg, equipsInfo.Enhance, int32(slot), newcombat-oldcombat)

		return nil

	} else if opType == 2 {
		if equipsInfo.Gemstone[stoneSlot] == 0 {
			return nil
		}
		if !p.checkAddSource(int32(equipsInfo.Gemstone[stoneSlot]), 1) {
			return errutil.ErrBagFull
		}
		p.addSource(int32(equipsInfo.Gemstone[stoneSlot]), 1, constant.ItemAddGemstone)
		equipsInfo.Gemstone[stoneSlot] = 0
		p.refreshStats()
		return nil
	}
	return nil
}

func (p *Player) upStoneShushuDate(equipsDetail *global.ItemConfig, genstoneCfg *global.ItemConfig, level int32, equip_part int32, combat int32) {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	shushuDate["name"] = "equip_inlay"
	if equipsDetail == nil {
		shushuDate["equip_id"] = 0
		shushuDate["equip_num"] = 0
		shushuDate["equip_quality"] = 0
		shushuDate["equip_name"] = 0
		shushuDate["equip_job"] = 0
	} else {
		shushuDate["equip_id"] = equipsDetail.ID
		shushuDate["equip_num"] = 1
		shushuDate["equip_quality"] = equipsDetail.Quality
		shushuDate["equip_name"] = equipsDetail.Name
		shushuDate["equip_job"] = equipsDetail.Occupylimit
	}

	shushuDate["equip_part"] = equip_part
	shushuDate["equip_level"] = level

	// soul_stone_info 字段为嵌套map
	re := regexp.MustCompile(`(\d+)级(.)`)
	matches := re.FindStringSubmatch(genstoneCfg.Name)
	shushuDate["soul_stone_info"] = map[string]interface{}{
		"soul_stone_id":    genstoneCfg.ID,        // 宝石ID
		"soul_stone_name":  genstoneCfg.Name,      // 没有名称可填空字符串
		"soul_stone_level": genstoneCfg.Itemlevel, // 没有等级可填1
		"soul_stone_type":  matches[2],            // 没有类型可填0
	}

	// 正则：匹配“9级红魂石(雷)”这种，数字后面是“级”，再跟一个汉字
	// 示例正则: `(\d+)级(.)`
	// 说明: (\d+) 匹配数字，级为固定字，(.) 匹配一个汉字或任意字符
	// 用法示例:

	shushuDate["equip_power_up"] = combat
	shushumain.ReportData(shushuDate)
}

// 一键穿脱宝石opType 1穿 ，2脱
func (p *Player) genstoneChangeAll(slot pb.EquipSlot, partnerslotid int32) ([]int32, error) {
	var equipsInfo *Slot
	delLevel := int32(0)
	if partnerslotid == 0 {
		equipsInfo = &p.equips[pb.EquipSlot(slot)]
		delLevel = p.level

	} else {

		equipsInfo = &p.getPartnerSlotInfo(partnerslotid).Equips[pb.EquipSlot(slot)]
		delLevel = p.getPartnerInfoBySlotId(partnerslotid).level
	}
	if equipsInfo == nil {
		return nil, errutil.ErrEquipSlot
	}

	canDell := make([]global.IdKeyNum, 0)
	// for k1, _ := range genstoneCfgAll {
	for _, itemInfo := range p.bag.ItemList {
		genstoneCfgAll, err := global.GetItemCfg(itemInfo.CfgId)
		if err != nil {
			return nil, errutil.ErrItemNotEnough
		}
		if len(genstoneCfgAll.DiamondPosition) > 0 {
			whileSolt := false
			for _, diamond := range genstoneCfgAll.DiamondPosition {
				if diamond == int(slot) {
					whileSolt = true
				}
			}
			if !whileSolt {
				continue
			}
			// 1是背包里面的
			for i := 0; i < int(itemInfo.Num); i++ {
				canDell = append(canDell, global.IdKeyNum{
					ID:  itemInfo.CfgId,
					Num: genstoneCfgAll.Itemlevel,
					Key: 1,
				})
			}
		}
	}
	canSlot := 0
	for _, StageInfo := range global.GlobalCfg.Diamond.Openlevel.Stage {
		intLevel, _ := strconv.Atoi(StageInfo.Level)
		if int(delLevel) >= intLevel {
			delInfo, _ := strconv.Atoi(StageInfo.Num)
			if delInfo > canSlot {
				canSlot = delInfo
			}
		}
	}
	// old := make(map[int]IdKeyNum{}, 0)
	old := make([]global.IdKeyNum, 0)
	for _, stoneSlotInfo := range equipsInfo.Gemstone {
		// genstoneCfgInfo, _ := global.GetItemCfg(int32(stoneSlotInfo))
		if stoneSlotInfo > 0 {
			genstoneCfgAll, err := global.GetItemCfg(int32(stoneSlotInfo))
			if err != nil {
				return nil, errutil.ErrItemNotEnough
			}
			// 2是身上的
			old = append(old, global.IdKeyNum{
				ID:  int32(stoneSlotInfo),
				Num: genstoneCfgAll.Itemlevel,
			})
		} else {
			old = append(old, global.IdKeyNum{
				ID:  0,
				Num: 0,
			})
		}
		// old[i] = int32(stoneSlotInfo)
	}
	// newBag := topFourByField(MaxBagStone, canSlot, 1)
	// returnDate := make([]int32, 0, len(newBag))

	// for i, oldInfo := range old {
	// 	for _, newInfo := range newBag {
	// 		// 1是背包，2是身上
	// 		if newInfo.Key == 2 {
	// 			if int32(oldInfo) == newInfo.ID {
	// 				delete(old, i)
	// 				break
	// 			}
	// 		}
	// 	}
	// }
	// 按槽位顺序处理宝石镶嵌
	// for _, newInfo := range newBag {
	// 	// 1是背包，2是身上
	// 	// 跳过相同宝石
	for i := 1; i <= canSlot; i++ {
		newBag := topFourByField(canDell, 1, 1)
		if len(newBag) == 0 {
			continue
		}
		newInfo := newBag[0]
		for j, olditem := range old {
			if newInfo.Num > olditem.Num {
				if int32(olditem.Num) >= newInfo.ID {
					continue
				}
				if j >= canSlot {
					continue
				}
				// 处理旧宝石回包
				if olditem.ID != 0 {
					// 检查背包空间
					if !p.checkAddSource(int32(olditem.ID), 1) {
						return nil, errutil.ErrBagFull
					}
					// 先扣除新宝石
					p.takeSource(newInfo.ID, 1, constant.ItemDelGemstone)
					// 再添加旧宝石
					p.addSource(int32(olditem.ID), 1, constant.ItemAddGemstone)
					for k := 0; k < len(canDell); k++ {
						if canDell[k].ID == newInfo.ID {
							canDell = append(canDell[:k], canDell[k+1:]...)
							break
						}
					}
					canDell = append(canDell, global.IdKeyNum{
						ID:  int32(olditem.ID),
						Num: olditem.Num,
					})

				} else {
					// 直接扣除新宝石
					p.takeSource(newInfo.ID, 1, constant.ItemDelGemstone)
					// canDell = deleteItem(canDell, newInfo.ID)
					for k := 0; k < len(canDell); k++ {
						if canDell[k].ID == newInfo.ID {
							canDell = append(canDell[:k], canDell[k+1:]...)
							break
						}
					}
				}

				// 更新装备槽
				equipsInfo.Gemstone[j] = pb.GemstoneSlot(newInfo.ID)
				// // 删除old这个
				// delete(old, j)
				old[j] = global.IdKeyNum{
					ID:  int32(newInfo.ID),
					Num: newInfo.Num,
				}
				oldcombat := p.totalCombat
				p.refreshStats(WithRefreshStatsPushMessage(false))
				newcombat := p.totalCombat
				genstoneCfg, err := global.GetItemCfg(newInfo.ID)
				if err != nil {
					return nil, errutil.ErrItemNotEnough
				}
				var equipCfg *global.ItemConfig
				if equipsInfo.ItemId != 0 {
					equipCfg2 := p.getItemByUid(equipsInfo.ItemId)
					if equipCfg2 == nil {
						return nil, errutil.ErrItemNotEnough
					}
					equipCfg, err = global.GetItemCfg(int32(equipCfg2.CfgId))
					if err != nil {
						return nil, errutil.ErrItemNotEnough
					}
				}
				p.upStoneShushuDate(equipCfg, genstoneCfg, equipsInfo.Enhance, int32(slot), newcombat-oldcombat)
				break
			}
			// 	}
		}
	}
	// for j, olditem := range old {
	// if newInfo.Num > olditem.Num {

	// 	if int32(olditem) == newInfo.ID {
	// 		continue
	// 	}
	// 	if j >= canSlot {
	// 		continue
	// 	}
	// 	// 处理旧宝石回包
	// 	if olditem != 0 {
	// 		// 检查背包空间
	// 		if !p.checkAddSource(int32(olditem), 1) {
	// 			return nil, errutil.ErrBagFull
	// 		}
	// 		// 先扣除新宝石
	// 		p.takeSource(newInfo.ID, 1, constant.ItemDelGemstone)
	// 		// 再添加旧宝石
	// 		p.addSource(int32(olditem), 1, constant.ItemAddGemstone)

	// 	} else {
	// 		// 直接扣除新宝石
	// 		p.takeSource(newInfo.ID, 1, constant.ItemDelGemstone)
	// 	}

	// 	// 更新装备槽
	// 	equipsInfo.Gemstone[j] = pb.GemstoneSlot(newInfo.ID)
	// 	// // 删除old这个
	// 	delete(old, j)
	// 	oldcombat := p.totalCombat
	// 	p.refreshStats(WithRefreshStatsPushMessage(false))
	// 	newcombat := p.totalCombat
	// 	genstoneCfg, err := global.GetItemCfg(newInfo.ID)
	// 	if err != nil {
	// 		return nil, errutil.ErrItemNotEnough
	// 	}
	// 	var equipCfg *global.ItemConfig
	// 	if equipsInfo.ItemId != 0 {
	// 		equipCfg2 := p.getItemByUid(equipsInfo.ItemId)
	// 		if equipCfg2 == nil {
	// 			return nil, errutil.ErrItemNotEnough
	// 		}
	// 		equipCfg, err = global.GetItemCfg(int32(equipCfg2.CfgId))
	// 		if err != nil {
	// 			return nil, errutil.ErrItemNotEnough
	// 		}
	// 	}
	// 	p.upStoneShushuDate(equipCfg, genstoneCfg, equipsInfo.Enhance, int32(slot), newcombat-oldcombat)

	// 	break
	// }
	// 	}
	// }
	// 把equipsInfo.Gemstone[j] 按照num的从大到小排序
	returnDate := make([]int32, 0)
	for _, v := range equipsInfo.Gemstone {
		returnDate = append(returnDate, int32(v))
	}

	p.refreshStats()

	return returnDate, nil
}

// 根据Key字段对Item切片进行排序，并返回最大的4个元素
func topFourByField(items []global.IdKeyNum, lenNum int, compare int) []global.IdKeyNum {
	sort.Slice(items, func(i, j int) bool {
		if items[i].Num == items[j].Num {
			// 如果 Num 相同，则比较 Key 字段
			return items[i].Key > items[j].Key // 降序排序 Key
		}
		if compare > 0 {
			return items[i].Num > items[j].Num
		} else {
			return items[i].Num < items[j].Num
		}
	})

	// 然后，如果元素数量超过4，取前4个元素
	if len(items) > lenNum {
		items = items[:lenNum]
	}

	// 返回排序后的最大的4个元素
	return items
}

// 每日必做副本首次挑战
func (p *Player) dailyMapFirstChallenge(roomType int32) bool {
	for _, v := range p.dailyMaps {
		if v.RoomType == roomType {
			return true
		}
	}
	return false
}

// 每日必做副本挑战次数
func (p *Player) dailyMapTimes(roomType int32) int32 {
	for _, v := range p.dailyMaps {
		if v.RoomType == roomType {
			return v.ChallengeTime
		}
	}
	p.dailyMaps = append(p.dailyMaps, &DailyMap{RoomType: roomType})

	return 0
}

func (p *Player) addDailyMapTimes(roomType int32) {
	for i, v := range p.dailyMaps {
		if v.RoomType == roomType {
			p.dailyMaps[i].ChallengeTime++
		}
	}
}

func (p *Player) dailyMapDoubleReward(b bool) (int32, string) {
	var code int32
	var err string
	if !b {
		p.roomType = 0
		p.reward = []global.IdNum{}
	} else {
		if p.roomType > 0 {
			if cfg := global.GetDailyMapDoubleaccess(p.roomType); cfg != nil {
				if p.checkTakeSource(cfg.ID, cfg.Num) {
					p.takeSource(cfg.ID, cfg.Num)
					if err := p.checkAddSourceBatch(p.reward); err == nil {
						p.addSourceBatch(p.reward, constant.ItemAddDailymap)
					} else {
						attachment := []protocol.Attachment{}
						for _, v := range p.reward {
							attachment = append(attachment, protocol.Attachment{UserPropsId: int64(v.ID), Num: int64(v.Num)})
						}
						p.SendMail(constant.ItemAddDailymap, attachment)
					}
					p.roomType = 0
					p.reward = []global.IdNum{}
				} else {
					code = -1
					err = errutil.ErrCoinNotEnough.Error()
				}
			} else {
				code = -2
				err = errutil.ErrGetAward.Error()
			}
		}
	}
	return code, err
}

func (p *Player) dailyMapCopySweep(roomID int32) (int32, string) {
	var code int32
	var err string

	roomCfg := global.GetRoomCfg(roomID)
	if roomCfg == nil {
		code = -1
		err = errutil.ErrConfigNotFound.Error()
		return code, err
	}

	// VIP等级
	if p.vip < int32(global.GetDailyMapTime(int32(roomCfg.Roomtype)).VipLimit) {
		code = -2
		err = errutil.VIPNotEnough
		return code, err
	}

	// 挑战次数
	var vTimes int32
	if vipCfg := global.GetVipCfg(p.vip); vipCfg != nil {
		for _, v := range vipCfg.DailyTime {
			if v.Key == roomCfg.Roomtype {
				vTimes += v.Value
			}
		}
	}
	if p.dailyMapTimes(roomCfg.Roomtype) >= int32(global.GetDailyMapTime(roomCfg.Roomtype).Num)+vTimes {
		code = -3
		err = errutil.LimitHasBeenReached
		return code, err
	}

	// 挑战次数+1
	p.addDailyMapTimes(roomCfg.Roomtype)

	rewards := []global.IdNum{}
	switch roomCfg.Roomtype {
	case constant.ExperienceFBMap:
		baseValue := global.GlobalCfg.DailyMaps.ExpMap.BaseValue
		num := float64(baseValue.Num) * float64(global.GlobalCfg.DailyMaps.ExpMap.QuickAccess.Score) / BaseRate * float64(p.level)
		rewards = append(rewards, global.IdNum{ID: baseValue.ID, Num: int32(num)})

	case constant.GoldFBMap:
		baseValue := global.GlobalCfg.DailyMaps.GoldMap.BaseValue
		num := float64(baseValue.Num) * float64(global.GlobalCfg.DailyMaps.GoldMap.QuickAccess.Score) / BaseRate * float64(p.level)
		rewards = append(rewards, global.IdNum{ID: baseValue.ID, Num: int32(num)})

	case constant.EnhanceStoneFBMap:
		baseValue := global.GlobalCfg.DailyMaps.StrengthenMap.BaseValue
		num := float64(baseValue.Num) * float64(global.GlobalCfg.DailyMaps.StrengthenMap.QuickAccess.Score) * float64(p.level)
		rewards = append(rewards, global.IdNum{ID: baseValue.ID, Num: int32(num)})

	case constant.BookPageFBMap:
		baseValue := global.GlobalCfg.DailyMaps.SkillPageMap.BaseValue
		num := float64(baseValue.Num) * float64(global.GlobalCfg.DailyMaps.SkillPageMap.QuickAccess.Score) * float64(p.level)
		rewards = append(rewards, global.IdNum{ID: baseValue.ID, Num: int32(num)})

	case constant.StarUpStoneFBMap:
		for _, v := range global.GlobalCfg.DailyMaps.StarStoneMap.QuickAccess.Stages {
			rewards = append(rewards, monsterMgr.MonsterDorp(v.ID, int32(v.Num))...)
		}

	case constant.SoulStoneFBMap:
		for _, v := range global.GlobalCfg.DailyMaps.SoulStoneMap.QuickAccess.Stages {
			rewards = append(rewards, monsterMgr.MonsterDorp(v.ID, int32(v.Num))...)
		}
	}

	if err := p.checkAddSourceBatch(rewards); err == nil {
		p.addSourceBatch(rewards, constant.ItemAddDailymap)
	} else {
		attachment := []protocol.Attachment{}
		for _, v := range rewards {
			attachment = append(attachment, protocol.Attachment{UserPropsId: int64(v.ID), Num: int64(v.Num)})
		}
		p.SendMail(constant.ItemAddDailymap, attachment)
	}

	packet := &pb.GameResultBattlePush{Items: []*pb.ItemInfo{}, RoomId: roomCfg.ID, OverType: pb.GameOverType_GameOverTypeCopySweep}
	for _, v := range rewards {
		packet.Items = append(packet.Items, &pb.ItemInfo{CfgId: v.ID, Num: int64(v.Num)})
	}
	p.pushMsg(protocol.GameResultBattleMapPush, packet)

	p.tasks.dispatch(pb.TargetType_TOTAL_MATERIAL_INSTANCE_CHALLENGE, 0, 1)

	p.reward = rewards
	p.roomType = roomCfg.Roomtype
	return code, err
}

func (p *Player) mining(uid int64) {
	if p.desk == nil {
		return
	}
	mon, err := p.desk.monsterWithUid(uid)
	if err != nil {
		p.logger.Debugf(" %s 挖矿失败 err %s", p.name, err.Error())
		return
	}

	monCfg := global.GetMonsterCfg(mon.cfgId)
	if monCfg == nil {
		return
	}

	mon.die(p)
	p.desk.DDailyMap.mingCount++

	// p.attackTime = time.Now().Add(time.Duration(monCfg.Delay-1) * time.Millisecond)

	p.setAttackTime((monCfg.Delay - 1) * 1000)
}

// 等级奖励
func (p *Player) levelGift() {
	if cfg, ok := global.LevelGiftCfg[p.level]; ok {
		source := protocol.AddSource{Uid: p.uid, ShopType: constant.ShopTypeUnknown, Source: []*protocol.SourceInfo{}}
		for _, v := range cfg.Gift {
			if (v.Gender != 0 && p.sex != int(v.Gender)) || (v.Job != 0 && p.job != constant.MowJob(v.Job)) {
				continue
			}
			source.Source = append(source.Source, &protocol.SourceInfo{Id: v.ID, Num: int64(v.Num)})
		}

		if len(source.Source) > 0 {
			defaultManager.chAddSource <- source
		}
	}
}

func (p *Player) ItemDisassembly(itemInfo *pb.ItemInfo) {
	var getitem []global.KeyValue

	if itemInfo == nil {
		return
	}
	itemCfg, err := global.GetItemCfg(itemInfo.CfgId)
	if err != nil {
		return
	}

	for _, des := range itemCfg.Decompose {
		bFind := false
		for i, gi := range getitem {
			if gi.Key == des.Key {
				getitem[i].Value += des.Value
				bFind = true
				break
			}
		}
		if !bFind {
			getitem = append(getitem, des)
		}
	}

	addSource := []global.IdNum{}
	for _, v := range getitem {
		addSource = append(addSource, global.IdNum{ID: v.Key, Num: v.Value})
	}
	go p.addSourceBatch(addSource, constant.ItemAddDisintegrate)
}

// 设置宝箱
func (p *Player) homeBoxSetting(nowOpenLevel int32, Job int32) error {
	// 更新数据库
	// p.box.NowOpenLevel = nowOpenLevel
	// p.box.Job = Job
	p.SyncUserBox()
	return nil
}

func (p *Player) openbox(level int32, num int32, job int32) ([]*pb.ItemInfo, error) {
	// level := p.box.NowOpenLevel
	// 如果没设置，就等于角色等级
	if level == 0 {
		level = p.level
	}
	// job := p.box.Job
	// needLevel := int32(1)
	// for _, v := range global.GlobalCfg.Openslot.IDLevel {
	// 	if v.ID == 1 {
	// 		needLevel = v.Needlevel
	// 		break
	// 	}
	// }
	// if p.level < needLevel {
	// 	job = int32(p.job)
	// }
	// 查询num是不是合法的
	BoxInfo := global.GetBoxLevelCfg(p.box.NowLevel)
	if BoxInfo.Maxtime < num {
		return nil, errutil.ErrEnergyFull
	}
	randomEquip := p.randomEquip(level, num, job)
	if len(randomEquip) == 0 {
		return nil, errutil.ErrOpenBoxEmpty
	}
	if err := p.checkAddSourceBatch(randomEquip); err == nil {
		itemInfo := p.addSourceBatch(randomEquip, constant.ItemAddOpenBox)
		p.box.NowOpenRes = make([]pb.ItemInfo, len(itemInfo))
		for i, item := range itemInfo {
			p.box.NowOpenRes[i].CfgId = item.CfgId
			p.box.NowOpenRes[i].Uid = item.Uid
			p.box.NowOpenRes[i].Num = item.Num
			p.box.NowOpenRes[i].Equip = item.Equip
		}
		// 触发任务累计
		p.tasks.dispatch(pb.TargetType_TOTAL_BOX_OPENED, 0, int32(num))

		return itemInfo, nil
	}
	return nil, errutil.ErrBagFull
}

// 升级宝箱
func (p *Player) upgradeBox() error {
	// 判断是否可以升级
	if int(p.box.NowLevel) >= len(global.BoxLevelCfg) {
		return errutil.ErrConfigNotFound
	}
	// 判断是否在升级中
	if p.box.NextBoxRecoveryTime > 0 {
		return errutil.ErrBoxRecovery
	}
	// 判断等级够不够
	if p.level < global.BoxLevelCfg[p.box.NowLevel].Rolelevel {
		return errutil.ErrLevel
	}
	// 判断有没有材料
	BoxInfo := global.GetBoxLevelCfg(p.box.NowLevel)
	if BoxInfo == nil {
		return errutil.ErrConfigNotFound
	}
	p.box.CostGold = 0
	for _, v := range BoxInfo.Consume {
		// 扣材料
		if v.ID == constant.ItemTypeCoin {
			p.box.CostGold = v.Num
		}
		if !p.checkTakeSource(v.ID, int64(v.Num)) {
			return errutil.ErrItemNotEnough
		}
	}
	for _, v := range BoxInfo.Consume {
		p.takeSource(v.ID, int64(v.Num))
	}
	p.box.BeginAt = time.Now().Unix()
	p.box.IsAccelerate = 0
	p.box.AccelerateNum = 0
	p.box.TimeCost = 0
	p.box.NextBoxRecoveryTime = int64(BoxInfo.Needtime)
	p.SyncUserBox()
	return nil
}

// 加快升级宝箱
func (p *Player) UpgradeBoxQuick(num int32) error {
	// 判断是否在升级中
	if p.box.NextBoxRecoveryTime == 0 {
		return errutil.ErrBoxRecovery
	}
	// 判断有没有材料
	BoxInfo := global.GetBoxLevelCfg(p.box.NowLevel)
	if BoxInfo == nil {
		return errutil.ErrConfigNotFound
	}
	// 扣材料
	canNum := p.box.NextBoxRecoveryTime / int64(global.GlobalCfg.BaseValue.Boxlevelup.Accelerate)
	if num > int32(canNum) {
		num = int32(canNum)
		if p.box.NextBoxRecoveryTime%int64(global.GlobalCfg.BaseValue.Boxlevelup.Accelerate) != 0 {
			num++
		}
	}

	// 扣材料
	if !p.checkTakeSource(global.GlobalCfg.BaseValue.Boxlevelup.Itemid, int64(num)) {
		return errutil.ErrItemNotEnough
	}
	p.takeSource(global.GlobalCfg.BaseValue.Boxlevelup.Itemid, int64(num))

	p.box.NextBoxRecoveryTime -= int64(global.GlobalCfg.BaseValue.Boxlevelup.Accelerate * num)
	if p.box.NextBoxRecoveryTime < 0 {
		p.box.NextBoxRecoveryTime = 0
	} else {
		p.SyncUserBox()
	}
	p.box.IsAccelerate = 1
	p.box.AccelerateNum += num
	return nil
}

// 给到随机的奖励
func (p *Player) randomEquip(level int32, num int32, job int32) (rewards []global.IdNum) {
	createCfg := global.OpenBoxCfg
	if len(createCfg) == 0 {
		return []global.IdNum{}
	}
	for _, v := range createCfg {
		// todo校验是否能开这么多箱子
		for _, v1 := range v.Level {
			if level >= v1.MinLevel && level <= v1.MaxLevel {
				var dropInfo []global.IdNum
				for i := 0; i < int(num); i++ {
					var rate int32
					for _, v2 := range v.Equiprandom {
						if job == 0 {
							rate += v2.Rate
						} else {
							if v2.Job == job {
								rate += v2.Rate
							}
						}
					}
					weight := global.RandIn(int(rate))
					DemoRandom := global.IdRateJob{}
					beginPriority := 0
					for _, v3 := range v.Equiprandom {
						if job == 0 {
							if beginPriority+int(v3.Rate) >= weight {
								DemoRandom = v3
								break
							}
							beginPriority += int(v3.Rate)
						} else {
							if v3.Job == job {
								if beginPriority+int(v3.Rate) >= weight {
									DemoRandom = v3
									break
								}
								beginPriority += int(v3.Rate)
							}
						}
					}
					dropInfo = append(dropInfo, global.IdNum{ID: DemoRandom.Id, Num: 1})
				}
				return dropInfo
			} else {
				continue
			}
		}
	}
	return []global.IdNum{}
}

// 随机装备品质
func (p *Player) randomEquipQuality(item *pb.ItemInfo) {
	if item.Type != pb.ItemType_EQUIPMENT {
		return
	}
	BoxLevelCfg := global.GetBoxLevelCfg(p.box.NowLevel)
	if BoxLevelCfg == nil {
		return
	}
	var rate int32
	for _, v2 := range BoxLevelCfg.Qualityrandom {
		rate += v2.Rate
	}
	weight := global.RandIn(int(rate))
	DemoRandom := global.IdRate{}
	beginPriority := 0
	for _, v3 := range BoxLevelCfg.Qualityrandom {
		if beginPriority+int(v3.Rate) >= weight {
			DemoRandom = v3
			break
		}
		beginPriority += int(v3.Rate)
	}
	if item.Equip == nil {
		item.Equip = &pb.EquipInfo{Quality: pb.ItemQuality_INVALID_Quality}
	}
	item.Equip.Quality = pb.ItemQuality(DemoRandom.Id)
}

func (p *Player) boxResIntoBag(Uid int64) error {
	for i := range p.box.NowOpenRes {
		if p.box.NowOpenRes[i].Uid == Uid {
			// 删除这个值
			p.box.NowOpenRes = append(p.box.NowOpenRes[:i], p.box.NowOpenRes[i+1:]...)
			break
		}
	}
	return nil
}

func (p *Player) getCombatPartnerNum() int32 {
	if p.partners == nil {
		return 0
	}
	num := 1
	for _, v := range p.partnerSlots {
		if v.PartnerUid > 0 {
			num++
		}
	}
	return int32(num)
}

// 伙伴信息
func (p *Player) getPartnerInfo(uid int64) *Partner {
	partner, ok := p.partners[uid]
	if !ok {
		return nil
	}
	return partner
}

func (p *Player) getPartnerInfoByCfgId(cfgId int32) *Partner {
	for _, v := range p.partners {
		if v.cfg.Cid == cfgId {
			return v
		}
	}
	return nil
}

func (p *Player) getPartnerInfoBySlotId(slot int32) *Partner {
	if slot < 1 || slot > 4 {
		return nil
	}
	if p.partnerSlots[slot-1].PartnerUid == 0 {
		return nil
	}
	partner, ok := p.partners[p.partnerSlots[slot-1].PartnerUid]
	if !ok {
		return nil
	}
	return partner
}

// 伙伴槽位信息
func (p *Player) getPartnerSlotInfo(slot int32) *PartnerSlot {
	for i := 0; i < len(p.partnerSlots); i++ {
		if p.partnerSlots[i].SlotId == slot {
			return p.partnerSlots[i]
		}
	}
	return nil
}

// 增加伙伴
func (p *Player) addPartner(cfgId int32) error {
	if cfgId == 0 {
		return nil
	}

	// 重复获得伙伴转换成碎片
	for _, v := range p.partners {
		chip := []global.IdNum{}
		if v.cfg.Cid == cfgId {
			for _, c := range v.cfg.ChipNum {
				chip = append(chip, global.IdNum{ID: c.Key, Num: c.Value})
			}
			go p.addSourceBatch(chip, constant.ItemAddPartner)
			return nil
		}
	}

	par := CreatePartner(cfgId)
	par.owner = p
	p.partners[par.Uid()] = par

	// 解锁技能
	par.learnSkill()

	p.syncSavePartnerDb(par.uid, model.Add)

	par.SyncUserPartner()

	return nil
}

// 伙伴上阵
func (p *Player) partnerCombat(slotId int32, partnerUid int64) error {
	partnerSlot := p.getPartnerSlotInfo(slotId)
	if partnerSlot == nil {
		return errutil.ErrPartnerInvalid
	}

	partner := p.getPartnerInfo(partnerUid)
	if partner == nil {
		return errutil.ErrPartnerInvalid
	}

	if partnerSlot.PartnerUid > 0 {
		return errutil.ErrPartnerInvalid
	}

	for _, v := range p.partnerSlots {
		if v.PartnerUid == partnerUid {
			return errutil.ErrPartnerInvalid
		}

		info := p.getPartnerInfo(v.PartnerUid)
		if info != nil {
			if info.cfg.Cid == partner.cfg.Cid {
				return errutil.ErrPartnerInvalid
			}
		}
	}

	partnerSlot.PartnerUid = partnerUid

	p.refreshStats()
	p.SyncUserPartnerSlot()
	return nil
}

// 伙伴休息
func (p *Player) partnerRest(slotId int32) error {
	slotInfo := p.getPartnerSlotInfo(slotId)
	if slotInfo == nil {
		return errutil.ErrPartnerInvalid
	}
	partner := p.getPartnerInfoBySlotId(slotId)
	if partner == nil {
		return errutil.ErrPartnerInvalid
	}

	equipnum := 0
	for _, v := range slotInfo.Equips {
		if v.ItemId > 0 {
			equipnum++
		}
	}

	if equipnum > p.bagEquipLeftCount() {
		return errutil.ErrBagFull
	}

	partnerSlot := p.getPartnerSlotInfo(slotId)
	if partnerSlot == nil {
		return errutil.ErrPartnerInvalid
	}

	// p.partnerSlots[slotId].PartnerUid = 0
	for k, v := range partnerSlot.Equips {
		if v.ItemId <= 0 {
			continue
		}
		p.changeEquip(2, pb.EquipSlot(k), v.ItemId, partnerSlot.SlotId)
	}

	// 自动下阵伙伴技能
	bskillslotchange := false
	for _, v := range partner.skills {
		for slot := 0; slot < len(p.skillsSlot); slot++ {
			if p.skillsSlot[slot] == v.CfgId {
				p.skillsSlot[slot] = 0
				bskillslotchange = true
			}
		}
	}
	if bskillslotchange {
		p.syncUserSkillSlot()
	}

	partnerSlot.PartnerUid = 0

	p.refreshStats()
	p.SyncUserPartnerSlot()
	return nil
}

// 伙伴合成
func (p *Player) partnerCompose(cfgId int32) error {
	for _, v := range p.partners {
		if v.cfg.Cid == cfgId {
			return errutil.ErrPartnerInvalid
		}
	}

	if cfg := global.GetPartnerCfg(cfgId); cfg != nil {
		for _, v := range cfg.ChipNum {
			if !p.checkTakeSource(v.Key, int64(v.Value)) {
				return errutil.ErrItemNotEnough
			}
		}

		for _, v := range cfg.ChipNum {
			p.takeSource(v.Key, int64(v.Value))
		}

		p.addPartner(cfgId)
	}
	return nil
}

// partnerReforge
func (p *Player) partnerReforge(partnerUid int64, isFree bool) error {
	par := p.getPartnerInfo(partnerUid)
	if par == nil {
		return errutil.ErrPartnerInvalid
	}
	if par.level <= 1 {
		return errutil.ErrLevel
	}

	var gotItems []global.IdNum
	var gotExp int64

	if !isFree {
		for _, v := range global.GlobalCfg.Partner.Forge.Charge {
			if v.Quality == par.cfg.Quality {
				if p.getItemCountInBag(int32(v.ID)) < int64(v.Num) {
					return errutil.ErrItemNotEnough
				}
				p.takeSource(v.ID, v.Num)
			}
		}
	}

	for i := par.star; i > 0; i-- {
		for _, v := range par.cfg.StarConsume {
			if v.Id == i {
				for _, cost := range v.Values {
					bFind := false
					for j := 0; j < len(gotItems); j++ {
						if gotItems[j].ID == cost.Key {
							gotItems[j].Num += cost.Value
							bFind = true
						}
					}
					if !bFind {
						gotItems = append(gotItems, global.IdNum{ID: cost.Key, Num: cost.Value})
					}
				}
			}
		}
	}

	for i := par.level - 1; i > 0; i-- {
		cfg, err := global.GetRoleCfg(int32(par.cfg.Job), i)
		if err != nil {
			continue
		}
		gotExp += cfg.NeedExp
	}

	if isFree {
		for i := 0; i < len(gotItems); i++ {
			gotItems[i].Num = int32(math.Ceil(float64(gotItems[i].Num) * float64(global.GlobalCfg.Partner.Forge.Free.Rate/100.0)))
		}
		gotExp = int64(math.Ceil(float64(gotExp) * float64(global.GlobalCfg.Partner.Forge.Free.Rate/100.0)))
	}

	gotItems = append(gotItems, global.Exp2ExpItems(gotExp)...)

	p.addSourceBatch(gotItems, constant.ItemAddDisintegrate)

	// 等级1  星级0 装备全部卸下
	par.star = 0
	par.level = 1
	for j := 0; j < len(p.skillsSlot); j++ {
		for _, v := range par.skills {
			if v.CfgId == p.skillsSlot[j] {
				p.skillsSlot[j] = 0
			}
		}
	}

	for _, slot := range p.partnerSlots {
		if slot.PartnerUid == partnerUid {
			for k, v := range slot.Equips {
				if v.ItemId <= 0 {
					continue
				}
				p.changeEquip(2, pb.EquipSlot(k), v.ItemId, slot.SlotId)
			}
			break
		}
	}
	// 解锁技能
	par.skills = []*PlayerSkill{}
	par.learnSkill()

	par.SyncUserPartner()
	par.refreshStats(nil, p)
	p.SyncUserPartnerSlot()
	p.syncUserSkillSlot()

	return nil
}

// partnerDisassemble
func (p *Player) partnerDisassemble(partnerUid int64) error {
	par := p.getPartnerInfo(partnerUid)
	if par == nil {
		return errutil.ErrPartnerInvalid
	}

	if par.cfg.Quality < int32(global.GlobalCfg.Partner.Decompose.Quality) {
		return errutil.ErrPartnerInvalid
	}

	if par.star < int32(global.GlobalCfg.Partner.Decompose.Starlevel) {
		return errutil.ErrPartnerInvalid
	}

	var gotItems []global.IdNum
	var gotExp int64
	var tmp []global.IdNum
	for i := par.star; i > 0; i-- {
		for _, v := range par.cfg.StarConsume {
			if v.Id == i {
				for _, cost := range v.Values {
					bFind := false
					for j := 0; j < len(tmp); j++ {
						if tmp[j].ID == cost.Key {
							tmp[j].Num += cost.Value
							bFind = true
							break
						}
					}
					if !bFind {
						tmp = append(tmp, global.IdNum{ID: cost.Key, Num: cost.Value})
					}
				}
			}
		}
	}

	for i := 0; i < len(tmp); i++ {
		if cfg, err := global.GetItemCfg(tmp[i].ID); err == nil {
			if cfg.MainType == constant.PartnerFragment {
				num := int32(math.Ceil(float64(tmp[i].Num) * float64(global.GlobalCfg.Partner.Decompose.Rate/100.0)))
				if num > 0 {
					bFind := false
					for j := 0; j < len(gotItems); j++ {
						if gotItems[j].ID == int32(global.GlobalCfg.Partner.Decompose.ID) {
							gotItems[j].Num += num
							bFind = true
							break
						}
					}
					if !bFind {
						gotItems = append(gotItems, global.IdNum{ID: int32(global.GlobalCfg.Partner.Decompose.ID), Num: num})
					}
				}

			} else {
				gotItems = append(gotItems, tmp[i])
			}
		}
	}

	for i := par.level - 1; i > 0; i-- {
		cfg, err := global.GetRoleCfg(int32(par.cfg.Job), i)
		if err != nil {
			continue
		}
		gotExp += cfg.NeedExp
	}

	gotItems = append(gotItems, global.Exp2ExpItems(gotExp)...)

	p.addSourceBatch(gotItems, constant.ItemAddDisintegrate)

	// 等级1  星级0 装备技能全部卸下
	par.star = 0
	par.level = 1
	for j := 0; j < len(p.skillsSlot); j++ {
		for _, v := range par.skills {
			if v.CfgId == p.skillsSlot[j] {
				p.skillsSlot[j] = 0
			}
		}
	}

	for _, slot := range p.partnerSlots {
		if slot.PartnerUid == partnerUid {
			for k, v := range slot.Equips {
				if v.ItemId <= 0 {
					continue
				}
				p.changeEquip(2, pb.EquipSlot(k), v.ItemId, slot.SlotId)
			}
			break
		}
	}
	// 解锁技能
	par.skills = []*PlayerSkill{}
	par.learnSkill()
	par.SyncUserPartner()
	par.refreshStats(nil, p)
	p.SyncUserPartnerSlot()
	p.syncUserSkillSlot()

	return nil
}

// 是否已装备
func (p *Player) isOnEquip(itemUid int64) bool {
	for _, v := range p.equips {
		if v.ItemId == itemUid {
			return true
		}
	}

	for _, v := range p.partnerSlots {
		for _, vv := range v.Equips {
			if vv.ItemId == itemUid {
				return true
			}
		}
	}
	return false
}

// 一键戴最好装备
func (p *Player) equipBest(objType pb.ObjectType, partnerSlotID int32) {
	if partnerSlotID < 0 || partnerSlotID > 5 {
		return
	}
	levevl_ := p.level
	job_ := p.job
	for slot := 1; slot < int(pb.EquipSlot_MaxEquipSlot); slot++ {

		var equipSlot *Slot
		if objType == pb.ObjectType_PLAYER {
			equipSlot = &p.equips[slot]
		} else {
			if p.getPartnerSlotInfo(partnerSlotID).PartnerUid == 0 {
				return
			}
			partner := p.getPartnerInfoBySlotId(partnerSlotID)
			if partner == nil {
				return
			}
			levevl_ = partner.level
			job_ = constant.MowJob(partner.cfg.Job) % 10
			equipSlot = &p.getPartnerSlotInfo(partnerSlotID).Equips[slot]
		}

		var bestEquip *pb.ItemInfo
		if p.equips[slot].ItemId > 0 {
			bestEquip = p.getItemByUid(equipSlot.ItemId)
		}

		for _, item := range p.bag.ItemList {
			if item.CfgId == 0 {
				continue
			}
			if item.Type != pb.ItemType_EQUIPMENT {
				continue
			}

			itemCfg, err := global.GetItemCfg(item.CfgId)
			if err != nil {
				continue
			}

			if itemCfg.Equiptype != slot {
				continue
			}

			if itemCfg.Levellimit > int(levevl_) {
				continue
			}

			if itemCfg.Occupylimit != 0 && itemCfg.Occupylimit != int(job_) {
				continue
			}

			if p.isOnEquip(item.Uid) {
				continue
			}

			if bestEquip == nil {
				bestEquip = p.getItemByUid(item.Uid)
				continue
			}

			if p.calcCombatEquip(bestEquip) < p.calcCombatEquip(item) {
				bestEquip = item
			}
		}

		if bestEquip != nil && bestEquip.Uid != equipSlot.ItemId {
			p.changeEquip(1, pb.EquipSlot(slot), bestEquip.Uid, int32(partnerSlotID))
		}
	}

	if objType == pb.ObjectType_PLAYER {
		p.SyncUserEquipsInfo()
	} else {
		p.SyncUserPartnerSlot()
	}

	p.refreshStats()
}

func (p *Player) getScenePartner() []*pb.ObjectInfo {
	if p.desk == nil {
		return nil
	}

	partnerList := []*pb.ObjectInfo{}
	for _, v := range p.partnerSlots {
		if v == nil {
			continue
		}

		partner := p.getPartnerInfo(v.PartnerUid)
		if partner == nil {
			continue
		}
		if partner.desk == nil {
			continue
		}
		partnerList = append(partnerList, &pb.ObjectInfo{
			Type:      pb.ObjectType(partner.objectType),
			Uid:       partner.uid,
			CfgID:     partner.cfgId,
			Level:     partner.Level(),
			Name:      partner.Name(),
			Location:  partner.location,
			Direction: partner.direction,
			MaxHp:     partner.GetMaxHP(),
			CurHp:     partner.currentHP,
			OwnerUid:  p.uid,
		})

	}
	return partnerList
}

// 增加宠物
func (p *Player) addPet(cfgId int32, exp int32, star int32, toSlotId int32, NeedDel int32, Channel string) (newSort int32, err error) {
	if cfgId == 0 {
		return 0, nil
	}
	endPetSlot := toSlotId
	// par := CreatePet(cfgId)
	// par.owner = p
	petSlot := p.checkEmptyPetSlot()
	if petSlot == -1 {
		return 0, errutil.ErrPetFull
	}
	// 判断这个等级能开多少槽位
	BagInfo := global.GlobalCfg.Petbag
	canhave := int32(0)
	for _, v := range BagInfo {
		// 判断当前等级能开多少槽位
		if p.level >= int32(v.Level) {
			canhave = int32(v.Value)
		} else {
			continue
		}
	}
	// 假设他现在能开10个，给到key是10，也是可以放进去的，因为0是上阵的
	if petSlot > canhave {
		return 0, errutil.ErrPetFull
	}
	// cfgid对应的宠物信息赋值给petslot
	petCfg := global.GetPetCfg(cfgId)
	if petCfg == nil {
		return 0, errutil.ErrConfigNotFound
	}
	petExpForLevel := int32(1)
	newExp := int32(0)
	if exp > 0 {
		petExpForLevel, newExp, err = p.petExpForLevel(1, int32(exp), petCfg.Quality)
		if err != nil {
			return 0, err
		}
	}
	if toSlotId == 0 {
		endPetSlot = petSlot
	}
	p.pet[endPetSlot] = PetSlotInfo{
		PetCfgId: cfgId,
		Quality:  petCfg.Quality,
		Level:    petExpForLevel,
		Exp:      newExp,
		Star:     star,
		ItemId:   petCfg.ItemID,
		ExpStar:  0,
	}
	p.tasks.dispatch(pb.TargetType_ANY_PET_UPGRADED_TO_LEVEL, 0, petExpForLevel)
	// 判断p.petEverHave有没有，没有的话，就添加新的
	if p.petEverHave == nil {
		p.petEverHave = []int32{cfgId}
	} else {
		nochange := 0
		for _, v := range p.petEverHave {
			if v == cfgId {
				nochange++
				continue
			}
		}
		if nochange == 0 {
			p.petEverHave = append(p.petEverHave, cfgId)
		}
	}
	p.syncSavePetDb()
	p.SyncUserPet(NeedDel)
	if Channel != "合成" {
		p.upPetAddShushuDate(petCfg, Channel)
	}
	return petSlot, nil
}

func (p *Player) upPetAddShushuDate(petCfg *global.PetConfig, Channel string) {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	zun := false
	if petCfg.Mark == 1 {
		zun = true
	}
	shushuDate["name"] = "pet_catch"
	shushuDate["item_id"] = petCfg.ID
	shushuDate["item_name"] = petCfg.Name
	shushuDate["item_quality"] = petCfg.Quality
	shushuDate["is_zun"] = zun
	shushuDate["method"] = Channel
	shushumain.ReportData(shushuDate)
}

// 校验是否有空的宠物槽位 有的话返回具体的槽位
func (p *Player) checkEmptyPetSlot() int32 {
	// 判断是否有空的槽位
	for k, v := range p.pet {
		if v.PetCfgId == 0 {
			if k == 0 {
				continue
			}
			return int32(k)
		}
	}
	return -1
}

// 获取空余槽位数量
func (p *Player) getEmptyPetSlotNum() int {
	num := 0
	for k, v := range p.pet {
		if k == 0 {
			continue
		}

		if v.PetCfgId == 0 {
			num++
		}
	}
	return num
}

// 宠物信息
func (p *Player) getPetInfo(SlotId int32) *PetSlotInfo {
	for i, pet := range p.pet {
		if i == int(SlotId) {
			return &pet
		}
	}
	return nil
}

// 宠物升星
func (p *Player) petStar(SlotId int32, CfgId int32, Num int32) (resNum int32, err error) {
	resper := int32(0)
	EndExp := int64(p.pet[SlotId].ExpStar)
	needLevel := int32(0)
	pet := p.getPetInfo(SlotId)
	if pet == nil {
		return resper, errutil.ErrPetInvalid
	}

	petCfg := global.GetPetCfg(pet.PetCfgId)
	if petCfg == nil {
		return resper, errutil.ErrConfigNotFound
	}

	if pet.Star >= int32(len(petCfg.StarConsume)) {
		return resper, errors.New("已满阶")
	}
	// 删除材料
	if !p.checkTakeSource(CfgId, int64(Num)) {
		return resper, errutil.ErrItemNotEnough
	}

	oldExpStar := p.pet[SlotId].ExpStar
	newExpStar := oldExpStar
	oldStar := p.pet[SlotId].Star
	// 查询宠物当前的星级所需要的材料
	for _, v := range petCfg.StarConsume {
		// 查询这个道具对应多少经验
		// perAll := make([]int, 0)
		for i := 0; i <= int(Num); i++ {
			if v.Id-1 == pet.Star {
				PetAdvanceInfo := global.GlobalCfg.PetAdvance.Basevalue.Item
				slot := int64(0)
				for _, v2 := range PetAdvanceInfo {
					if v2.ID == CfgId {
						slot = int64(v2.Num)
						break
					}
				}
				per := petStageRand()
				resper = int32(per)
				if per == -1 {
					return 0, errutil.ErrConfigNotFound
				}
				// perAll = append(perAll, per)
				EndExp = int64(p.pet[SlotId].ExpStar) + slot*int64(per)
				// 查询这个道具对应多少经验
				if EndExp >= int64(v.Values) {
					EndExp -= int64(v.Values)
					for _, v3 := range petCfg.StarLimit {
						if v3.Id == pet.Star+1 {
							needLevel = v3.Values
							break
						}
					}
					// 如果当前宠物等级不够，不能升星
					if pet.Level < needLevel {
						// 如果已经满经验了，就不扣除材料
						if p.pet[SlotId].ExpStar != v.Values {
							EndExp = int64(v.Values)
						} else {
							return 0, errutil.ErrPetLevelNotEnough // 宠物等级不足
						}
						break
					} else {
						p.pet[SlotId].Star++
						break
					}
				} else {
					break
				}
				// pet.ExpStar+
			}
		}
	}
	newStar := p.pet[SlotId].Star
	p.takeSource(CfgId, int64(Num), constant.ItemDelPetStar)
	p.pet[SlotId].ExpStar = int32(EndExp)
	newExpStar = p.pet[SlotId].ExpStar
	p.syncSavePetDb()
	p.SyncUserPet(0)
	oldcombat := p.totalCombat
	p.refreshStats()
	newcombat := p.totalCombat
	// 升星数据上报
	p.upPetStarShushuDate(petCfg, CfgId, Num, oldExpStar, newExpStar, oldStar, newStar, newcombat-oldcombat)
	return resper, nil
}

func (p *Player) upPetStarShushuDate(petCfg *global.PetConfig, CfgId int32, Num int32, oldExpStar int32, newExpStar int32, oldStar int32, newStar int32, combatDiff int32) {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	zun := false
	if petCfg.Mark == 1 {
		zun = true
	}

	shushuDate["name"] = "pet_overshoot"
	shushuDate["item_id"] = petCfg.ID
	shushuDate["item_name"] = petCfg.Name
	shushuDate["item_quality"] = petCfg.Quality
	shushuDate["is_zun"] = zun
	// 查询config
	shushuDate["cost_item_name"] = CfgId
	shushuDate["cost_item_num"] = Num
	shushuDate["rank_before"] = oldStar
	shushuDate["rank_after"] = newStar
	shushuDate["progress_before"] = oldExpStar // 可根据实际情况填写进度
	shushuDate["progress_after"] = newExpStar  // 可根据实际情况填写进度
	shushuDate["power_up"] = combatDiff
	shushumain.ReportData(shushuDate)
}

// 宠物升星暴击
func petStageRand() int {
	petStage := global.GlobalCfg.PetAdvance.Rate.Stage
	allPriority := 0
	beginPriority := 0
	// 活动内容
	for _, petStageInfo := range petStage {
		allPriority = allPriority + int(petStageInfo.Pro)
	}
	rand := global.RandIn(allPriority) + 1
	for _, petStageInfo := range petStage {
		if beginPriority+int(petStageInfo.Pro) >= rand {
			return petStageInfo.Rate
		}
		beginPriority = beginPriority + int(petStageInfo.Pro)
	}
	return -1
}

func (p *Player) petLevel(SlotId int32) error {
	pet := p.getPetInfo(SlotId)
	if pet == nil {
		return errutil.ErrPetInvalid
	}

	petCfg := global.GetPetCfg(pet.PetCfgId)
	if petCfg == nil {
		return errutil.ErrConfigNotFound
	}
	roleCfg, _ := global.GetRoleCfg(constant.Pet, pet.Level)
	if roleCfg == nil {
		return errutil.ErrConfigNotFound
	}
	if pet.Star >= int32(len(petCfg.StarConsume)) {
		return errutil.ErrMaxLevel
	}
	// 查询宠物当前的等级所需要的经验
	per := global.GetpetLevelRate(petCfg.Quality)
	NeedExp := math.Round(float64(roleCfg.NeedExp)*per) - float64(pet.Exp)
	if !p.checkTakeSource(constant.ItemTypePetExp, int64(NeedExp)) {
		return errutil.ErrItemNotEnough
	}
	p.takeSource(constant.ItemTypePetExp, int64(NeedExp), constant.ItemDelPetLevel)
	p.pet[SlotId].Level++
	p.pet[SlotId].Exp = 0
	p.syncSavePetDb()
	p.SyncUserPet(0)
	p.refreshStats()
	return nil
}

// 升星查询这个经验对应多少等级
func (p *Player) petExpForStar(level int32, exp int32) (newLevel int32, newExp int32, err error) {
	// for i := 0; i < len(global.RoleCfg); i++ {
	// 	petCfg := global.GetPetCfg(pet.PetCfgId)
	// 	if err != nil {
	// 		return 0, 0, err
	// 	}
	// 	if int64(exp) >= RoleCfg.NeedExp {
	// 		exp -= int32(RoleCfg.NeedExp)
	// 		level++
	// 	} else {
	// 		return level, exp, nil
	// 	}
	// }
	return level, exp, nil
}

// 升级查询这个经验对应多少等级
func (p *Player) petExpForLevel(level int32, exp int32, quality int32) (newLevel int32, newExp int32, err error) {
	for i := 0; i < len(global.RoleCfg); i++ {
		RoleCfg, err := global.GetRoleCfg(constant.Pet, level)
		if err != nil {
			return 0, 0, err
		}
		// 每个伙伴品质对应不同的系数
		per := global.GetpetLevelRate(quality)
		if float64(exp) >= math.Round(float64(RoleCfg.NeedExp)*per) {
			exp -= int32(math.Round(float64(RoleCfg.NeedExp) * per))
			level++
		} else {
			return level, exp, nil
		}
	}
	return level, exp, nil
}

// 查询这个经验对应多少经验
func (p *Player) petExpForExp(level int32, exp int32, quality int32) (allExp int32, err error) {
	allExp = int32(0)
	for i := 1; i < len(global.RoleCfg); i++ {
		RoleCfg, err := global.GetRoleCfg(constant.Pet, int32(i))
		if err != nil {
			return 0, err
		}
		// 这级需要的经验，如果没超过这级，不能加到经验里面
		// 每个伙伴品质对应不同的系数
		per := global.GetpetLevelRate(quality)
		if int(level) > i {
			exp += int32(math.Round(float64(RoleCfg.NeedExp) * per))
		} else {
			return allExp + exp, nil
		}
	}
	return allExp + exp, nil
}

func (p *Player) petAddExp(SlotId int32, exp int32) (bool, error) {
	up := false
	pet := p.getPetInfo(SlotId)
	if pet == nil {
		return up, errutil.ErrPetInvalid
	}
	petCfg := global.GetPetCfg(p.pet[SlotId].PetCfgId)
	if petCfg == nil {
		return up, errutil.ErrConfigNotFound
	}

	willexp := int64(exp)
	oldLevel := p.pet[SlotId].Level
	petExpForLevel, newExp, err := p.petExpForLevel(p.pet[SlotId].Level, int32(willexp), petCfg.Quality)
	if err != nil {
		return up, err
	}
	p.tasks.dispatch(pb.TargetType_ANY_PET_UPGRADED_TO_LEVEL, 0, petExpForLevel)
	p.pet[SlotId].Level = petExpForLevel
	p.pet[SlotId].Exp = newExp
	// pet.Star++
	if oldLevel != p.pet[SlotId].Level {
		up = true
	}
	p.syncSavePetDb()
	p.SyncUserPet(0)
	return up, nil
}

// 宠物信息
func (p *Player) petMove(fromSlotId, toSlotId int32) (newSlot int32, newPet int32, newPetCfgId int32, err error) {
	if fromSlotId < 0 || fromSlotId >= int32(len(p.pet)) || toSlotId < 0 || toSlotId >= int32(len(p.pet)) {
		return 0, 0, 0, errutil.ErrPetInvalid
	}
	// 会有好几种情况，第一种是交换两个空的宠物，第二种是交换两个有宠物的宠物，第三种是交换一个空的宠物和一个有宠物的宠物
	if p.pet[fromSlotId].PetCfgId == 0 {
		return 0, 0, 0, errutil.ErrPetInvalid
	}
	newSlot = toSlotId
	var petCfg *global.PetConfig

	// 如果拖动的宠物是经验宝宝，那么直接给目的槽位宠物加经验
	if fromSlotId != 0 {
		petCfg = global.GetPetCfg(p.pet[fromSlotId].PetCfgId)
	}
	var toPetCfg *global.PetConfig
	if toSlotId != 0 {
		toPetCfg = global.GetPetCfg(p.pet[toSlotId].PetCfgId)
	}

	toLevel := p.pet[toSlotId].Level
	fromLevel := p.pet[fromSlotId].Level
	fromStar := p.pet[fromSlotId].Star
	toStar := p.pet[toSlotId].Star
	// 两个位置都有人，如果他们品级都一样，直接合成
	if p.pet[fromSlotId].PetCfgId != 0 && p.pet[toSlotId].PetCfgId != 0 {
		if fromSlotId != 0 && toSlotId != 0 {
			if petCfg.PetKind.Key == 2 {
				if p.pet[toSlotId].PetCfgId != 0 {
					// 是经验宝宝，有两个经验，一个是经验宝宝累积的经验，还有就是经验宝宝自身配表的经验
					fromExp, err := p.petExpForExp(p.pet[fromSlotId].Level, p.pet[fromSlotId].Exp, p.pet[fromSlotId].Quality)
					if err != nil {
						return newSlot, 0, 0, err
					}
					allExp := petCfg.PetKind.Value + p.pet[toSlotId].Exp + fromExp
					up, err := p.petAddExp(toSlotId, allExp)
					if err != nil {
						return newSlot, 0, 0, err
					}
					if up {
						p.upPetLevelShushuDate(petCfg, toPetCfg, p.pet[toSlotId].Star, toLevel, fromLevel)
					}
					p.pet[fromSlotId] = PetSlotInfo{}
					p.syncSavePetDb()
					p.SyncUserPet(0)
					return newSlot, 0, 0, nil
				} else {
					p.pet[toSlotId], p.pet[fromSlotId] = p.pet[fromSlotId], p.pet[toSlotId]
					p.syncSavePetDb()
					p.SyncUserPet(0)
					return newSlot, 0, 0, nil
				}
			}
			if p.pet[fromSlotId].Quality == p.pet[toSlotId].Quality {
				// 合成
				whileAll := make(map[int32][]int32)
				for _, v := range global.ComposeCfg {
					for _, v2 := range v.Raw {
						whileAll[v2] = append(whileAll[v2], v.ComposeID)
					}
				}
				newId := int32(0)
				newPetId := int32(0)
				if len(whileAll[p.pet[fromSlotId].ItemId]) == 0 || len(whileAll[p.pet[toSlotId].ItemId]) == 0 {
					p.pet[toSlotId], p.pet[fromSlotId] = p.pet[fromSlotId], p.pet[toSlotId]
					p.syncSavePetDb()
					p.SyncUserPet(0)
					return newSlot, 0, 0, errutil.ErrComposeError
				}

				// 两个数组有交集，说明就是可以合成的
				if len(whileAll[p.pet[fromSlotId].ItemId]) > 0 && len(whileAll[p.pet[toSlotId].ItemId]) > 0 {
					composeIds := make([]int32, 0)
					for _, v := range whileAll[p.pet[fromSlotId].ItemId] {
						for _, v2 := range whileAll[p.pet[toSlotId].ItemId] {
							if v == v2 {
								composeIds = append(composeIds, v)
								break
							}
						}
					}
					if len(composeIds) == 0 {
						p.pet[toSlotId], p.pet[fromSlotId] = p.pet[fromSlotId], p.pet[toSlotId]
						p.syncSavePetDb()
						p.SyncUserPet(0)
						return newSlot, 0, 0, errutil.ErrComposeError
					}
					// 查找子集，优先读子集
					// 查找最短的Raw数组，把这个数组的第一个元素作为合成id
					composeId := composeIds[0]
					for _, v := range composeIds {
						if len(global.ComposeCfg[v].Raw) < len(global.ComposeCfg[composeId].Raw) {
							composeId = v
						}
					}
					logger.Debugf("合成id %d", composeId)
					// 合成的id
					if global.ComposeCfg[composeId] == nil {
						p.pet[toSlotId], p.pet[fromSlotId] = p.pet[fromSlotId], p.pet[toSlotId]
						p.syncSavePetDb()
						p.SyncUserPet(0)
						return newSlot, 0, 0, errutil.ErrComposeError
					}
					newId = composeRand(global.ComposeCfg[composeId])
					// 转换成宠物id
					for _, v := range global.PetCfg {
						if v.ItemID == newId {
							newPetId = v.ID
							break
						}
					}

					// 两个宠物的经验相加
					fromExp, err := p.petExpForExp(p.pet[fromSlotId].Level, p.pet[fromSlotId].Exp, p.pet[fromSlotId].Quality)
					if err != nil {
						return newSlot, 0, 0, err
					}
					toExp, err := p.petExpForExp(p.pet[toSlotId].Level, p.pet[toSlotId].Exp, p.pet[toSlotId].Quality)
					if err != nil {
						return newSlot, 0, 0, err
					}
					allExp := fromExp + toExp

					// 星级取最大星级
					star := int32(0)
					if p.pet[fromSlotId].Star > p.pet[toSlotId].Star {
						star = p.pet[fromSlotId].Star
					} else {
						star = p.pet[toSlotId].Star
					}
					fromPetLevel := p.pet[fromSlotId].Level
					toPetLevel := p.pet[toSlotId].Level
					// 去掉原来的宠物
					p.pet[fromSlotId] = PetSlotInfo{}
					p.pet[toSlotId] = PetSlotInfo{}
					newSlot, _ = p.addPet(newPetId, allExp, star, toSlotId, 0, "合成")
					p.syncSavePetDb()
					// 查询这个宠物的品质
					petInfo := global.GetPetCfg(newPetId)
					if petInfo.Quality >= 6 && petInfo.PetKind.Key == 1 {
						strSend := []string{p.name, strconv.Itoa(int(petInfo.ItemID))}
						defaultManager.Lamp(int32(constant.LampPetComposeSummon), p.serverID, strSend)
					}
					p.upPetComposeShushuDate(petCfg, toPetCfg, petInfo, fromPetLevel, toPetLevel, p.pet[newSlot].Level)
					// p.SyncUserPet(0)
					if len(global.ComposeCfg[composeId].Return) > 0 {
						for _, cv := range global.ComposeCfg[composeId].Return {
							if cv.Key == newId {
								for _, cv2 := range cv.Values {
									// 给玩家添加材料
									logger.Debugf("给玩家添加材料 %d %d", cv2.ID, cv2.Num)
									p.addSource(cv2.ID, int64(cv2.Num), constant.ItemAddCompose)
								}
							}
						}
					}
					return newSlot, newPetId, newPetId, nil
				} else {
				}
			}
		}
	}
	p.pet[toSlotId], p.pet[fromSlotId] = p.pet[fromSlotId], p.pet[toSlotId]
	p.syncSavePetDb()
	p.SyncUserPet(0)
	if fromSlotId == 0 || toSlotId == 0 {
		oldcombat := p.totalCombat
		p.defaultBuff()
		newcombat := p.totalCombat
		if toSlotId == 0 {
			p.upPetOnShushuDate(petCfg, toPetCfg, fromLevel, toLevel, fromStar, toStar, newcombat-oldcombat)
		}
	}
	return -1, 0, 0, nil
}

func (p *Player) upPetLevelShushuDate(petCfg *global.PetConfig, toPetCfg *global.PetConfig, star int32, tolevel int32, fromlevel int32) {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	zun := false
	if petCfg.Mark == 1 {
		zun = true
	}
	shushuDate["is_zun"] = zun
	shushuDate["name"] = "pet_gobble"
	shushuDate["item_id"] = toPetCfg.ID
	shushuDate["item_name"] = toPetCfg.Name
	shushuDate["item_quality"] = toPetCfg.Quality
	shushuDate["item_rank"] = star

	shushuDate["eaten_id"] = petCfg.ID
	shushuDate["eaten_name"] = petCfg.Name
	shushuDate["eaten_quality"] = petCfg.Quality

	shushuDate["eaten_level"] = fromlevel
	shushuDate["level_after"] = tolevel - 1
	shushuDate["level_before"] = tolevel
	shushumain.ReportData(shushuDate)
}

func (p *Player) upPetComposeShushuDate(fromPet *global.PetConfig, toPet *global.PetConfig, newPet *global.PetConfig, fromLevel int32, toLevel int32, level int32) {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	zun := false
	if newPet.Mark == 1 {
		zun = true
	}

	shushuDate["name"] = "pet_synthesis"
	item_zun := false
	if fromPet.Mark == 1 && toPet.Mark == 1 {
		item_zun = true
	}
	shushuDate["cost_pet_info"] = make([]map[string]interface{}, 0, 2)
	shushuDate["cost_pet_info"] = append(shushuDate["cost_pet_info"].([]map[string]interface{}), map[string]interface{}{
		"item_id":      fromPet.ID,
		"item_name":    fromPet.Name,
		"item_quality": fromPet.Quality,
		"item_level":   fromLevel,
	})
	shushuDate["cost_pet_info"] = append(shushuDate["cost_pet_info"].([]map[string]interface{}), map[string]interface{}{
		"item_id":      toPet.ID,
		"item_name":    toPet.Name,
		"item_quality": toPet.Quality,
		"item_level":   toLevel,
		"item_zun":     item_zun,
	})
	shushuDate["is_zun"] = zun
	shushuDate["synthesis_item_id"] = newPet.ItemID
	shushuDate["synthesis_item_name"] = newPet.Name
	shushuDate["synthesis_item_quality"] = newPet.Quality
	shushuDate["synthesis_item_level"] = level
	shushumain.ReportData(shushuDate)
}

func (p *Player) upPetOnShushuDate(fromPet *global.PetConfig, toPet *global.PetConfig, fromLevel int32, toLevel int32, fromStar int32, toStar int32, combat int32) {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	tozun := false
	fromzun := false
	if toPet != nil && toPet.Mark == 1 {
		tozun = true
	}
	if fromPet != nil && fromPet.Mark == 1 {
		fromzun = true
	}
	shushuDate["name"] = "pet_equip"
	if toPet == nil {
		shushuDate["before_item_id"] = 0
		shushuDate["before_item_name"] = ""
		shushuDate["item_level_before"] = 0
		shushuDate["item_quality_before"] = 0
		shushuDate["before_is_zun"] = false
	} else {
		shushuDate["before_item_id"] = toPet.ID
		shushuDate["before_item_name"] = toPet.Name
		shushuDate["item_level_before"] = toLevel
		shushuDate["item_quality_before"] = toStar
		shushuDate["before_is_zun"] = tozun
	}

	shushuDate["is_zun"] = fromzun
	shushuDate["item_id"] = fromPet.ID
	shushuDate["item_name"] = fromPet.Name
	shushuDate["item_level_after"] = fromLevel
	shushuDate["item_rank"] = toStar
	shushuDate["item_quality"] = fromPet.Quality
	shushuDate["power_up"] = combat
	shushumain.ReportData(shushuDate)
}

func (p *Player) syncSavePetDb() error {
	data, err := json.Marshal(p.pet)
	if err != nil {
		return err
	}
	data2, err := json.Marshal(p.petEverHave)
	if err != nil {
		return err
	}
	pet := model.UserPet{
		Uid:         p.uid,
		PetInfo:     string(data),
		PetEverHave: string(data2),
	}

	if err := db.UpdateUserPet(&pet); err != nil {
		return err
	}
	return nil
}

// 当前应该挑战的关卡ID
func (p *Player) getNextBattleId(CopyType int32) int32 {
	var battleId int32

	var copyType_ int32
	var chapter_ int32
	var layers_ int32

	copyType_ = CopyType
	switch CopyType {
	case constant.BattleMap:
		if p.BattleMap.BattleId == 0 {
			battleId = copyType_*100000 + 1*1000 + 1
		} else {
			copyType_ = p.BattleMap.BattleId / 100000
			chapter_ = p.BattleMap.BattleId % 100000 / 1000
			layers_ = p.BattleMap.BattleId % 1000

			cfg := global.GetBattleMapCfg(p.BattleMap.BattleId)
			if cfg == nil {
				return 0
			}
			if cfg.MapMark == 1 {
				chapter_++
				layers_ = 1

			} else {
				layers_++
			}
			battleId = copyType_*100000 + chapter_*1000 + layers_
		}
	case constant.BossMap:
		if p.BossMap.BattleId == 0 {
			battleId = copyType_*100000 + 1*1000 + 1
		} else {
			battleId = p.BossMap.BattleId + 1
		}
	case constant.TrialTowerMap:
		if p.TrialTowerMap.BattleId == 0 {
			battleId = copyType_*100000 + 1*1000 + 1
		} else {
			battleId = p.TrialTowerMap.BattleId + 1
		}
	case constant.RelicMap:
		if p.RelicMap.BattleId == 0 {
			battleId = copyType_*100000 + 1*1000 + 1
		} else {
			copyType_ = p.RelicMap.BattleId / 100000
			chapter_ = p.RelicMap.BattleId % 100000 / 1000
			layers_ = p.RelicMap.BattleId % 1000

			cfg := global.GetBattleMapCfg(p.RelicMap.BattleId)
			if cfg == nil {
				return 0
			}
			if cfg.MapMark == 1 {
				chapter_++
				layers_ = 1

			} else {
				layers_++
			}

			battleId = copyType_*100000 + chapter_*1000 + layers_
		}
	case constant.FightEndMap:
		if p.FightEndMap.BattleId == 0 {
			battleId = copyType_*100000 + 1
		} else {
			battleId = p.FightEndMap.BattleId + 1
		}

	default:
		return 0
	}
	return battleId
}

// 填充副本信息
func (p *Player) fillInstanceInfo(res *pb.InstanceInfoResponse) {
	res.BattleMap = &pb.InstanceInfoResponse_Battle{
		BattleId:    p.InstanceData.BattleMap.BattleId,
		RewardStage: p.InstanceData.BattleMap.RewardStage,
	}
	res.BossMap = &pb.InstanceInfoResponse_Boss{
		BattleId:      p.InstanceData.BossMap.BattleId,
		ChallengeTime: p.InstanceData.BossMap.ChallengeTime,
	}
	res.TrialTowerMap = &pb.InstanceInfoResponse_TrialTower{
		BattleId: p.InstanceData.TrialTowerMap.BattleId,
	}
	res.RelicMap = &pb.InstanceInfoResponse_Relic{
		BattleId:      p.InstanceData.RelicMap.BattleId,
		ChallengeTime: p.InstanceData.RelicMap.ChallengeTime,
	}

	for _, v := range p.InstanceData.RelicMap.Evaluation {
		res.RelicMap.Evaluation = append(res.RelicMap.Evaluation, &pb.KeyValue{
			K: v.Key,
			V: v.Value,
		})
	}
	for _, v := range p.InstanceData.RelicMap.ChapterReward {
		res.RelicMap.ChapterReward = append(res.RelicMap.ChapterReward, &pb.KeyValue{
			K: v.Key,
			V: v.Value,
		})
	}

	res.Afk = &pb.InstanceInfoResponse_AFK{DailyTime: 0}
	res.Afk.DailyTime = p.afkLeftTime()
	if p.afkExpirationAt.Before(time.Now()) {
		res.Afk.ExpirationAt = 0
	} else {
		res.Afk.ExpirationAt = p.afkExpirationAt.Unix()
	}
	res.Afk.AfkMultiplierID = p.AfkMultiplierID

	res.ToEnd = &pb.InstanceInfoResponse_ToEnd{}

	if p.FightEndMap.RefreshAt.Day() != time.Now().Day() {
		res.ToEnd.BattleId = 0
		p.FightEndMap.BattleId = 0
		p.FightEndMap.RefreshAt = time.Now()

		for i := 0; i < len(p.FightEndMap.Evaluation); i++ {
			p.FightEndMap.Evaluation[i].Uid = 0
			p.FightEndMap.Evaluation[i].ChallengeTime = 0
		}

	}
	res.ToEnd.BattleId = p.FightEndMap.BattleId
	for i := 0; i < len(p.FightEndMap.Evaluation); i++ {
		res.ToEnd.Evaluation = append(res.ToEnd.Evaluation, &pb.KeyValue{
			K: p.FightEndMap.Evaluation[i].ChallengeTime,
			V: p.FightEndMap.Evaluation[i].ChallengeTime,
		})
	}
}

// 获取章节奖励
func (p *Player) getBattleReward(map_type pb.MapType, chapter_id int32) error {
	if map_type == pb.MapType_BATTLE_MAP {
		if p.BattleMap.BattleId <= p.BattleMap.RewardStage {
			return errutil.ErrRequestFailed
		}

		if p.bagItemLeftCount() <= 1 {
			return errutil.ErrBagFull
		}

		rewards := []global.IdNum{}
		for i := p.BattleMap.RewardStage; i <= p.BattleMap.BattleId; i++ {
			if cfg := global.GetBattleMapCfg(i); cfg != nil {
				for _, v := range cfg.ChapterAward {
					rewards = append(rewards, global.IdNum{
						ID:  v.ID,
						Num: v.Num,
					})
				}
			}
		}

		p.addSourceBatch(rewards, constant.ItemAddDrop)

		p.BattleMap.RewardStage = p.BattleMap.BattleId

	} else if map_type == pb.MapType_RELIC_MAP {
		rewards := []global.IdNum{}
		chapter_star := int32(0) // 本章节以获得的星
		for _, v := range p.RelicMap.Evaluation {
			if v.Key%100000/1000 != chapter_id {
				continue
			}
			chapter_star += v.Value
		}

		get_star := int32(0) // 已经领取的星
		// get_k := 0

		for _, v := range p.RelicMap.ChapterReward {
			if v.Key == chapter_id {
				if v.Value >= chapter_star {
					return errutil.ErrRequestFailed
				} else {
					get_star = v.Value
					// get_k = k
					break
				}
			}
		}

		if get_star >= chapter_star {
			return errutil.ErrRequestFailed
		}

		var cfg []global.KeyValueEx2
		for _, v := range global.BattleCfg {
			if v.CopyType == int32(map_type) && v.Chapter == chapter_id && len(v.StarAward) > 0 {
				cfg = v.StarAward
				break
			}
		}
		if len(cfg) == 0 {
			return errutil.ErrRequestFailed
		}

		max_get_star := int32(0) // 最新领取的星数
		for _, v := range cfg {
			if v.Key > get_star && v.Key <= chapter_star {
				for _, v2 := range v.Values {
					rewards = append(rewards, global.IdNum{
						ID:  v2.ID,
						Num: v2.Num,
					})
				}
				max_get_star = v.Key
			}
		}

		bFind := false
		for i := 0; i < len(p.RelicMap.ChapterReward); i++ {
			if p.RelicMap.ChapterReward[i].Key == chapter_id {
				p.RelicMap.ChapterReward[i].Value = max_get_star
				bFind = true
				break
			}
		}
		if !bFind {
			p.RelicMap.ChapterReward = append(p.RelicMap.ChapterReward, global.KeyValue{
				Key:   chapter_id,
				Value: max_get_star,
			})
		}

		p.addSourceBatch(rewards, constant.ItemAddDrop)
		p.asyncSaveDb()

		// 跑马灯
		if max_get_star >= 30 {
			context := []string{p.name}
			defaultManager.Lamp(int32(constant.LampRelicChapterReward), p.serverID, context)
		}
	}

	return nil
}

func (p *Player) battleMapCopySweep(roomID, battleID, sweepTimes int32) (int32, string) {
	var code int32
	var err string

	roomCfg := global.GetRoomCfg(roomID)
	if roomCfg == nil {
		code = -1
		err = errutil.ErrConfigNotFound.Error()
		return code, err
	}

	battleCfg := global.GetBattleMapCfg(battleID)
	if battleCfg == nil {
		code = -2
		err = errutil.ErrConfigNotFound.Error()
		return code, err
	}

	switch roomCfg.Roomtype {
	case constant.BossMap:
		if p.BossMap.BattleId < battleID {
			return -3, errutil.ErrRequestFailed.Error()
		}
		if p.BossMap.ChallengeTime <= 0 {
			return -4, errutil.ErrRequestFailed.Error()
		}

		// sweepTimes = p.BossMap.ChallengeTime
		p.BossMap.ChallengeTime -= sweepTimes
		p.tasks.dispatch(pb.TargetType_TOTAL_BOSS_CHALLENGE, 0, sweepTimes)
	case constant.RelicMap:
		if sweepTimes > p.RelicMap.ChallengeTime {
			return -4, errutil.ErrRequestFailed.Error()
		}
		for _, v := range p.RelicMap.Evaluation {
			if v.Key == battleID {
				if v.Value < 3 {
					return -5, errutil.ErrRequestFailed.Error()
				}
				break
			}
		}
		p.RelicMap.ChallengeTime -= sweepTimes

		p.tasks.dispatch(pb.TargetType_TOTAL_RELIC_CHALLENGE, 0, sweepTimes)
	}

	rewards := []global.IdNum{}

	for i := 0; i < int(sweepTimes); i++ {
		rewards = append(rewards, battleCfg.Settlement...)
	}

	if err := p.checkAddSourceBatch(rewards); err == nil {
		p.addSourceBatch(rewards, constant.ItemAddDailymap)
	} else {
		attachment := []protocol.Attachment{}
		for _, v := range rewards {
			attachment = append(attachment, protocol.Attachment{UserPropsId: int64(v.ID), Num: int64(v.Num)})
		}
		p.SendMail(constant.ItemAddDailymap, attachment)
	}

	packet := &pb.GameResultBattlePush{BattleId: battleID, OverType: pb.GameOverType_GameOverTypeCopySweep, Items: []*pb.ItemInfo{}}

	for _, v := range rewards {
		packet.Items = append(packet.Items, &pb.ItemInfo{CfgId: v.ID, Num: int64(v.Num)})
	}
	p.pushMsg(protocol.GameResultBattleMapPush, packet)

	p.reward = rewards
	p.roomType = roomCfg.Roomtype
	return code, err
}

// 挂机剩余时间
func (p *Player) afkLeftTime() int32 {
	leftTime := global.GlobalCfg.BaseValue.Expcopy.Dailytime - int32(p.afkDailyTime)
	if leftTime < 0 {
		leftTime = 0
	}
	return leftTime
}

func (p *Player) delAfkLeftTime(del_sec int64) {
	p.afkDailyTime += del_sec
}

func (p *Player) ToMapForShushu() map[string]interface{} {
	TmpPetID := int64(0)
	TmpPetLevel := int32(0)
	TmpPetQuality := int32(0)
	TmpMonthCard := false
	TmpLifetimeCard := false
	TmpPartnerLevel := int32(0)
	TmpPetName := ""
	if len(p.pet) > 0 {
		TmpPetID = int64(p.pet[0].PetCfgId)
		TmpPetLevel = int32(p.pet[0].Level)
		TmpPetQuality = int32(p.pet[0].Quality)
	}
	zun := false
	// 查询宠物信息
	if petCfg := global.GetPetCfg(int32(TmpPetID)); petCfg != nil {
		TmpPetName = petCfg.Name
		if petCfg.Mark == 1 {
			zun = true
		}
	}

	for _, v := range p.privilege {
		cfg, ok := global.PrivilegeCfg[int32(v.Pid)]
		if !ok {
			continue
		}
		if cfg.Duration < 31 {
			TmpMonthCard = true
		} else {
			TmpLifetimeCard = true
		}
	}
	for _, v := range p.partnerSlots {
		if v.PartnerUid > 0 {
			if par := p.getPartnerInfo(v.PartnerUid); par != nil {
				TmpPartnerLevel += par.Level()
			}
			break
		}
	}
	// 查询pvp排名
	rankInfo1 := &protocol.RankOperateRequest{
		Uid:  p.uid,
		Type: 3,
		All:  3,
	}
	res1, err := provide.RankList(rankInfo1)
	tmp_pvp1_rank := int64(0)
	if err == nil {
		if res1.RankOwn.Num > 0 {
			tmp_pvp1_rank = res1.RankOwn.Num
		}
	}

	// 查询pvp排名
	tmp_pvp2_rank := int64(0)
	// rankInfo2 := &protocol.RankOperateRequest{
	// 	Uid:  p.uid,
	// 	Type: 3,
	// 	All:  3,
	// }
	// res2, err := provide.RankList(rankInfo2)
	// if err == nil {
	// 	if res2.RankOwn.Num > 0 {
	// 		tmp_pvp1_rank = res2.RankOwn.Num
	// 	}
	// }

	// 查询任务
	tmp_task_id := 0
	for _, v := range p.tasks.tasks {
		if v.Class == 7 {
			if v.Status == pb.TaskStatus_IN_PROGRESS {
				tmp_task_id = v.ID
				break
			}
		}
	}
	return map[string]interface{}{
		"channel":              "", // 填写渠道信息
		"sever_id":             strconv.Itoa(int(p.serverID)),
		"nick_name":            p.name,
		"tmp_power":            int32(p.totalCombat),
		"tmp_level":            int32(p.Level()),
		"tmp_box_level":        int32(p.box.NowLevel),
		"tmp_main_id":          p.InstanceData.BattleMap.BattleId,
		"tmp_task_id":          tmp_task_id,
		"tmp_coin":             int32(p.tokens[constant.ItemTypeCoin]),
		"tmp_coin_cost":        int32(p.tokenDeletes[constant.ItemTypeCoin]),
		"tmp_ingot":            int32(p.tokens[constant.ItemTypeCoupon]),
		"tmp_ingot_cost":       int32(p.tokenDeletes[constant.ItemTypeCoupon]),
		"tmp_battle_soul":      int32(p.tokens[constant.ItemTypeSoul]),
		"tmp_battle_soul_cost": int32(p.tokenDeletes[constant.ItemTypeSoul]),
		"tmp_trial_coin":       int32(p.tokens[constant.ItemTypeTrialVoucher]),
		"tmp_trial_coin_cost":  int32(p.tokenDeletes[constant.ItemTypeTrialVoucher]),
		"tmp_payment_amount":   float64(p.recharge),
		"tmp_days":             int32(p.nowDay),
		"tmp_vip":              int32(p.vip),
		"tmp_guild_id":         int64(p.guildId),
		"tmp_time":             int64(p.totalOnlineTime),
		"tmp_role_job":         constant.JobName[p.job],
		"tmp_account":          p.accountName,
		"tmp_device":           "",
		"tmp_role_id":          p.uid,
		"tmp_pet_id":           TmpPetID,
		"tmp_pet_name":         TmpPetName, // 填写宠物名信息
		"tmp_pet_level":        TmpPetLevel,
		"tmp_pet_rank":         TmpPetQuality,
		"tmp_partner_level":    TmpPartnerLevel,
		"tmp_pvp1_rank":        tmp_pvp1_rank,
		"tmp_pvp2_rank":        tmp_pvp2_rank,
		"tmp_month_card":       TmpMonthCard,
		"tmp_lifetime_card":    TmpLifetimeCard,
		"tmp_pet_zun":          zun,
	}
}

func (p *Player) OfflineAdd() error {
	if err := p.syncBoxFromDB(); err != nil {
		return err
	}
	if err := p.syncPetFromDB(); err != nil {
		return err
	}
	// 工会信息
	if err := p.syncGuildFromDB(); err != nil {
		return err
	}
	return nil
}

// 添加挑战记录
func (p *Player) addChallengeRecord(roomtype int32) {
	p.InstanceData.ChallengeRecord.Records[roomtype]++
}

// 查询是否有挑战记录
func (p *Player) getChallengeRecord(roomtype int32) int32 {
	if r, ok := p.InstanceData.ChallengeRecord.Records[roomtype]; ok {
		return r
	}
	return 0
}

func (p *Player) SetMailCounter(op constant.MailCounterOPType, num int) error {
	switch op {
	case constant.MailCounterOPTypeAdd:
		p.mailCounter += num
	case constant.MailCounterOPTypeSub:
		p.mailCounter -= num
	case constant.MailCounterOPTypeSet:
		p.mailCounter = num
	}
	if p.mailCounter < 0 {
		p.mailCounter = 0
	}
	return nil
}

// GuardIslandMultiplier 挂机副本
func (p *Player) GuardIslandMultiplier(id int64) error {
	guardIslandRecord, exist := global.GuardIslandCfg[id]
	if !exist {
		return errutil.NewBizError(-1, fmt.Errorf("config.GuardIsland.id not found: %d, uid: %d", id, p.uid), errutil.ErrConfigNotFound.Error())
	}

	if p.AfkMultiplierID != -1 {
		return errutil.NewBizError(-1, fmt.Errorf("config.GuardIsland already set, id: %d, uid: %d", p.AfkMultiplierID, p.uid), errutil.ErrRequestFailed.Error())
	}

	logger.Debugf("config.GuardIsland: %+v, uid: %d", guardIslandRecord, p.uid)

	// 处理逻辑
	switch constant.GuardIsLandType(guardIslandRecord.Type) {
	case constant.GuardIsLandTypeFree:
		p.AfkMultiplierID = int64(guardIslandRecord.ID)
	case constant.GuardIsLandTypeAd:
		// TODO: 处理广告逻辑, 现在点了直接过
		p.AfkMultiplierID = int64(guardIslandRecord.ID)
	case constant.GuardIsLandTypeItem:
		// storeConfig, ok := global.StoreCfg[guardIslandRecord.ID]
		// if !ok {
		// 	return errutil.NewBizError(-1, fmt.Errorf("StoreCfg.id not found: %d, uid: %d", guardIslandRecord.ID, p.uid), errutil.ErrConfigNotFound.Error())
		// }
		// if !p.checkTakeSource(int32(storeConfig.Limit.NeedMoney), storeConfig.Limit.Num) {
		// 	return errutil.NewBizError(-1, fmt.Errorf("config.GuardIsland checkAddSourceBatch failed, item_id: %d, item_num: %d, uid: %d", storeConfig.Limit.NeedMoney, storeConfig.Limit.Num, p.uid), errutil.ErrItemNotEnough.Error())
		// }
		// p.takeSource(int32(storeConfig.Limit.NeedMoney), storeConfig.Limit.Num, constant.ItemDelGuardIsland)
		p.AfkMultiplierID = int64(guardIslandRecord.ID)
	case constant.GuardIsLandTypeRecharge:
		// TODO : 处理充值逻辑
		p.AfkMultiplierID = int64(guardIslandRecord.ID)
		// coreEnv := strings.ToLower(viper.GetString("core.env"))
		// if coreEnv == constant.EnvDev || coreEnv == constant.EnvTest {
		// 	p.AfkMultiplierID = int64(guardIslandRecord.ID)
		// } else {
		// 	return errutil.NewBizError(-1, fmt.Errorf("config.GuardIsland recharge not support in env: %s, uid: %d", coreEnv, p.uid), errutil.ErrRequestFailed.Error())
		// }
	default:
		return errutil.NewBizError(-1, fmt.Errorf("config.GuardIsland.type not found for id: %d, uid: %d", id, p.uid), errutil.ErrRequestFailed.Error())
	}
	return nil
}
