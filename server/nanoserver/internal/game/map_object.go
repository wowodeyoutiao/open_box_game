package game

import (
	"math"
	"sync"
	"time"

	"nanoserver/global"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/maputil"
	"nanoserver/pkg/snowflake"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"
)

type ObjectType int

const (
	ObjectTypePlayer  ObjectType = 1
	ObjectTypePartner ObjectType = 2
	ObjectTypeMonster ObjectType = 3
	ObjectTypeRobot   ObjectType = 4
	ObjectTypeBuff    ObjectType = 5
	ObjectTypeDecoy   ObjectType = 6
)

type ObjectAction int

const (
	ObjectActionStand  ObjectAction = 1
	ObjectActionMove   ObjectAction = 2
	ObjectActionAttack ObjectAction = 3
	ObjectActionDead   ObjectAction = 4
)

type IMapObject interface {
	PushMsg(route string, v interface{}) error

	ObjectType() ObjectType // 实例类型
	Location() *pb.Point    // 位置
	GetStats() *Stats       // 全部属性
	Uid() int64
	Name() string
	GetDC() int32    // 物攻
	GetMC() int32    // 魔攻
	GetAR() int32    // 物防
	GetMR() int32    // 魔防
	GetMaxHP() int32 // 最大血量
	GetCurHP() int32 // 当前血量
	Dodged()         // 闪避
	Critical()       // 暴击
	Reflect()        // 反伤
	Dead() bool
	SetLocation(l *pb.Point)
	SetCell(cell *Cell)
	GetCell() *Cell
	GetDirection() pb.Direction
	SettDirection(dir pb.Direction)
	SetDesk(desk *Desk)
	Desk() *Desk
	HaveBuff(pretype constant.BuffType) bool
	CfgID() int32
	Owner() IMapObject
	GetBuff(pretype constant.BuffType) *BuffInfo
	GetObState() ObjectAction
	SetObState(state ObjectAction)
	Level() int32
	Job() constant.MowJob
	IsWarrior() bool
	IsRobot() bool
	ServerId() int32

	AttackAttributes() pb.Attribute // 攻击属性 怪物:根据配表确定物攻/魔攻， 玩家:根据职业/技能确定物攻/魔攻
	Attacked(attacker IMapObject, skillId int32, skillAdvanceLevel int32, power int32, bReflect bool)
	Revive(skill *PlayerSkill, skillAdvanceLevel, reviveHp int32)
	CleanDebuff()
	AddBuff(id int32, owner IMapObject, addSec int32, exStats *Stats, addRate int32, skillID int32)
	Target() IMapObject
	ClearTarget()
	Friends() []IMapObject
	SkillBuffTrigger(skill *PlayerSkill)
	ChangeHp(hp int32, obj IMapObject)
}

type BuffInfo struct {
	Id            int32      // buffID
	BuffType      int32      // 类型 1代表为增益效果 2代表为减益效果
	Mark          int32      // 1 被动，1000 技能引用的效果
	BuffStats     Stats      // 属性
	StartTime     time.Time  // 创建时间
	OverTime      time.Time  // 结束时间
	TickTime      time.Time  // 下次生效时间
	CDTime        time.Time  // 冷却时间
	TickFrequency int32      // 生效间隔
	Pretype       int32      // 大类
	Owner         IMapObject // 释放着
	Status        int32      // 状态
	Rate          int32      // 触发概率
	SkillID       int32      // 触发buff的原技能ID
}

type HpDisplay struct {
	// 血量变化相关
	currentHP           int32
	displayCrit         bool // 暴击
	displayMiss         bool // 闪避
	displayReflect      bool // 反伤
	displayHP           int32
	attackId            int64
	displaySkillId      int32
	displaySuckingBlood bool
}

// 地图实例基类
type MapObject struct {
	IMapObject
	*HpDisplay
	objectType   ObjectType
	cfgId        int32 // 配置id
	uid          int64
	account_name string // 账号名
	name         string // 名字
	location     *pb.Point
	direction    pb.Direction
	cell         *Cell
	attackTime   time.Time     // 攻击时间
	moveTime     time.Time     // 移动时间
	dead         bool          // 是否死亡
	deadTime     time.Time     // 副本内死亡时间
	reviveTime   time.Time     // 副本内复活时间
	bornLocation global.PointI // 出生位置
	reviveTimes  int
	createTime   time.Time // 创建时间
	delTime      time.Time // 消失时间

	target      IMapObject // 目标
	block       sync.Mutex
	buffs       map[int32]*BuffInfo // buff
	slock       sync.Mutex
	stats       *Stats     // 数据统计
	desk        *Desk      // 当前桌
	owner       IMapObject // 主人
	linkedSkill int32
	obAction    ObjectAction // 当前动作
	level       int32
	skills      []*PlayerSkill  // 技能
	job         constant.MowJob // 职业
	serverID    int32           // 实例所在服务器
	bRobot      bool
	robotType   RobotType //机器人类型
}

func NewMapObject(t ObjectType, uid int64) *MapObject {
	return &MapObject{
		objectType:   t,
		uid:          uid,
		location:     &pb.Point{},
		attackTime:   time.Now(),
		moveTime:     time.Now(),
		dead:         false,
		direction:    pb.Direction_UP,
		bornLocation: global.PointI{},
		HpDisplay:    &HpDisplay{},

		buffs:       map[int32]*BuffInfo{},
		cell:        &Cell{},
		reviveTimes: 0,
		stats:       &Stats{Attrs: make(map[pb.Attribute]int32)},
		// desk:        &Desk{},
		createTime:  time.Now(),
		linkedSkill: 0,
		obAction:    ObjectActionStand,
		skills:      []*PlayerSkill{},
	}
}

func CreateBuffObject(cfgId int32, obType ObjectType, owner IMapObject, addMilliSec int32, linkedSkill int32, exStats *Stats) *MapObject {
	if buffCfg, err := global.GetBuffCfg(cfgId); err == nil {
		uid := snowflake.GenID()
		mob := NewMapObject(obType, uid)
		mob.cfgId = buffCfg.ID
		mob.owner = owner
		mob.linkedSkill = linkedSkill

		if exStats != nil {
			for k, v := range exStats.Attrs {
				mob.stats.setStats(k, v)
				mob.stats.setStats(pb.Attribute_HEALTH, mob.stat(pb.Attribute_MAX_HEALTH))
			}
			mob.currentHP = exStats.getStats(pb.Attribute_MAX_HEALTH)
		}

		mob.delTime = time.Now().Add(time.Duration(addMilliSec) * time.Millisecond)

		return mob
	}
	return nil
}

func (o *MapObject) CanAttack() bool {
	if o.dead {
		return false
	}

	if time.Now().Before(o.attackTime) {
		return false
	}

	if time.Now().Before(o.moveTime) {
		return false
	}
	if o.getBuffByType(constant.BuffFreeze) != nil {
		return false
	}

	return true
}

func (o *MapObject) CanAttackTarget(target IMapObject) bool {
	if target == nil {
		return false
	}

	if target.Desk() == nil {
		return false
	}

	if target.Dead() {
		// logger.Debugf(" 无法攻击  目标已死 uid=%d ", o.uid)
		return false
	}

	if o.uid == target.Uid() {
		return false
	}

	if o.dead && o.objectType != ObjectTypeDecoy {
		return false
	}

	if o.getBuffByType(constant.BuffFreeze) != nil {
		// logger.Debugf(" 无法攻击 身中眩晕 uid=%d ", o.uid)
		return false
	}

	if target.ObjectType() == ObjectTypeMonster {
		return o.objectType != ObjectTypeMonster
	}

	if (target.ObjectType() == ObjectTypeDecoy || target.ObjectType() == ObjectTypePartner) && target.Owner() != nil {
		if target.Owner().Uid() == o.uid {
			return false
		}
		if o.Owner() != nil && target.Owner().Uid() == o.owner.Uid() {
			return false
		}
		if o.Owner() != nil && target.Owner() != nil && target.Owner().Owner() != nil && target.Owner().Owner().Uid() == o.Owner().Uid() {
			return false
		}
	}

	if o.objectType == ObjectTypeDecoy && o.owner != nil {
		if o.owner.Uid() == target.Uid() {
			return false
		}
	}

	if target.HaveBuff(constant.BuffGodMode) {
		return false
	}

	return true
}

func (o *MapObject) CanHelpTarget(target IMapObject) bool {
	if target == nil {
		return false
	}
	if target.Desk() == nil {
		return false
	}

	if o.dead {
		return false
	}

	// 不是我的伙伴
	if target.ObjectType() == ObjectTypePartner && target.Owner() != nil {
		if o.Owner() != nil && target.Owner().Uid() != o.owner.Uid() {
			return false
		}
	}

	// 不是我的主人
	if target.ObjectType() == ObjectTypePlayer && o.Owner() != nil {
		if o.Owner().Uid() != target.Uid() {
			return false
		}
	}

	if o.objectType == ObjectTypeDecoy || o.objectType == ObjectTypeMonster {
		return false
	}

	if target.HaveBuff(constant.BuffGodMode) {
		return false
	}

	return true
}

// 能否移动
func (o *MapObject) CanMove() bool {
	if o.dead {
		return false
	}

	if time.Now().Before(o.moveTime) {
		return false
	}
	// if time.Now().Before(o.attackTime) {
	// 	return false
	// }

	// warning 可能需要加锁
	if o.getBuffByType(constant.BuffFreeze) != nil {
		return false
	}

	return true
}

func (o *MapObject) Location() *pb.Point {
	return o.location
}

// 攻击范围
func (o *MapObject) inAttackRange(distance int) bool {
	return maputil.InRange(o.location, o.target.Location(), distance)
}

func (o *MapObject) stat(k pb.Attribute) int32 {
	return o.stats.getStats(k)
}

func (o *MapObject) refreshBaseStatus() {
	now := time.Now()
	o.stats.clear()

	// buff
	for _, buff := range o.buffs {
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

		for i, e := range buff.BuffStats.Attrs {
			if buff.BuffType == constant.BuffTypeBuff {
				o.stats.setStats(i, e)
			} else {
				o.stats.setStats(i, -e)
			}
		}

	}

	o.stats.changeStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE, int32(global.GlobalCfg.BaseValue.Crit.CriticalDamage))
	o.stats.changeStats(pb.Attribute_HEALTH, o.currentHP)

	// <0  攻速移速 增益，其他属性减益 ; >0 攻速移速 减益，其他属性增益
	o.stats.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(o.stats.getStats(pb.Attribute_PHYSICAL_ATTACK))*(float64(o.stats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(o.stats.getStats(pb.Attribute_MAGIC_ATTACK))*(float64(o.stats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(o.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE))*(float64(o.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(o.stats.getStats(pb.Attribute_MAGIC_DEFENSE))*(float64(o.stats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))

	o.stats.setStats(pb.Attribute_DODGE, int32(float64(o.stats.getStats(pb.Attribute_DODGE))*(float64(o.stats.getStats(pb.Attribute_DODGE_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_HIT, int32(float64(o.stats.getStats(pb.Attribute_HIT))*(float64(o.stats.getStats(pb.Attribute_HIT_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_CRITICAL_STRIKE, int32(float64(o.stats.getStats(pb.Attribute_CRITICAL_STRIKE))*(float64(o.stats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_TOUGHNESS, int32(float64(o.stats.getStats(pb.Attribute_TOUGHNESS))*(float64(o.stats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_ATTACK_SPEED, int32(float64(o.stats.getStats(pb.Attribute_ATTACK_SPEED))*(float64(o.stats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_MOVE_SPEED, int32(float64(o.stats.getStats(pb.Attribute_MOVE_SPEED))*(float64(o.stats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(o.stats.getStats(pb.Attribute_MAX_HEALTH))*(float64(o.stats.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate)))

	// 基础属性 >= 0
	o.stats.changeStats(pb.Attribute_PHYSICAL_ATTACK, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_PHYSICAL_ATTACK)))))
	o.stats.changeStats(pb.Attribute_MAGIC_ATTACK, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_MAGIC_ATTACK)))))
	o.stats.changeStats(pb.Attribute_PHYSICAL_DEFENSE, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE)))))
	o.stats.changeStats(pb.Attribute_MAGIC_DEFENSE, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_MAGIC_DEFENSE)))))

	o.stats.changeStats(pb.Attribute_DODGE, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_DODGE)))))
	o.stats.changeStats(pb.Attribute_HIT, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_HIT)))))
	o.stats.changeStats(pb.Attribute_CRITICAL_STRIKE, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_CRITICAL_STRIKE)))))
	o.stats.changeStats(pb.Attribute_TOUGHNESS, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_TOUGHNESS)))))
	o.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_ATTACK_SPEED)))))
	o.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_MOVE_SPEED)))))
	o.stats.changeStats(pb.Attribute_MAX_HEALTH, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_MAX_HEALTH)))))
	o.stats.changeStats(pb.Attribute_HEALTH, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_MAX_HEALTH)))))
}

func (o *MapObject) getBuffByType(pretype constant.BuffType) *BuffInfo {
	defer func() {
		if err := recover(); err != nil {
			logger.Errorf("Error=%v", err)
			println(stack())
		}
	}()
	if o.block.TryLock() {
		defer o.block.Unlock()
		for _, v := range o.buffs {
			if v.Pretype == int32(pretype) {
				if time.Now().Before(v.OverTime) {
					return v
				}
			}
		}
	}

	return nil
}

// client buff list
func (o *MapObject) getPBBuffList() []*pb.BuffInfo {
	defer func() {
		if err := recover(); err != nil {
			logger.Errorf("Error=%v", err)
			println(stack())
		}
	}()
	o.block.Lock()
	defer o.block.Unlock()

	var buffs []*pb.BuffInfo
	for _, v := range o.buffs {
		one := &pb.BuffInfo{
			BuffId:        v.Id,
			RemainingTime: int32(v.OverTime.UnixMilli() - v.StartTime.UnixMilli()),
			BuffStats:     []*pb.Stats{},
		}

		for bk, bv := range v.BuffStats.Attrs {
			one.BuffStats = append(one.BuffStats, &pb.Stats{Attr: bk, Num: bv})
		}

		buffs = append(buffs, one)
	}

	return buffs
}

// 血量变化
func (o *MapObject) changeHp(hp int32, mapobj IMapObject) int32 {
	var realHp float64
	if hp > 0 {
		if o.currentHP == o.GetMaxHP() {
			return o.currentHP
		}
		realHp = float64(hp) * (1 + float64(o.stat(pb.Attribute_LIFE_RECOVERY_INCREASE_PERCENTAGE))/BaseRate)
		realHp = float64(realHp) * (1 - float64(o.stat(pb.Attribute_LIFE_RECOVERY_DECREASE_PERCENTAGE))/BaseRate)
		if realHp <= 0 {
			return o.currentHP
		}
	} else {
		realHp = float64(hp)
	}

	// 同步世界boss血量
	if o.ObjectType() == ObjectTypeMonster {
		if cfg := global.GetMonsterCfg(o.cfgId); cfg != nil && cfg.MType == MTypeWorldBoss {
			wb := worldBossMgr.getBoss(o.serverID)
			if wb != nil && mapobj != nil {
				wb.changeHp(int32(realHp), mapobj)
			}
		}
	}

	// debug code
	// if o.uid == 2576246359023689728 || o.uid == 2576264663478050817 || o.uid == 2577693926488023040 || o.uid == 2576293365498781696 || o.uid == 2576293512907595776 {
	// 	var objUid int64
	// 	var objName string
	// 	var objType ObjectType
	// 	if mapobj != nil {
	// 		objUid = mapobj.Uid()
	// 		objName = mapobj.Name()
	// 		objType = mapobj.ObjectType()
	// 	}
	// 	logger.Warningf("测试log changeHp[%d:%s]  伤害 %f  攻击者ID:%d  名字:%s  类型:%v", o.uid, o.name, realHp, objUid, objName, objType)
	// }

	o.currentHP += int32(realHp)
	if o.currentHP <= 0 {
		o.currentHP = 0
		o.processHp()
		o.die(mapobj)
		return 0
	}
	if o.currentHP > o.GetMaxHP() {
		o.currentHP = o.GetMaxHP()
	}

	o.stats.changeStats(pb.Attribute_HEALTH, o.currentHP)
	return o.currentHP
}

// 更新血量
func (o *MapObject) processHp() {
	if o.dead {
		return
	}
	if o.displayCrit || o.displayMiss || o.displayReflect || o.displayHP != o.currentHP {
		change := o.currentHP - o.displayHP

		o.displayHP = o.currentHP

		if o.desk != nil {
			o.desk.Broadcast(protocol.RouteObjectHpChangePush, &pb.ObjectHpChangePush{
				TargetId:   o.uid,
				AttackerId: o.attackId,
				ChangeHp:   change,
				SkillId:    o.displaySkillId,
				Critical:   o.displayCrit,
				Miss:       o.displayMiss,
				Reflect:    o.displayReflect,
				MaxHp:      o.GetMaxHP(),
			})
		}

		o.displaySkillId = 0
		o.displayCrit = false
		o.displayMiss = false
		o.displayReflect = false
		o.attackId = 0
	}
}

func (o *MapObject) processBuff() {
	defer func() {
		if err := recover(); err != nil {
			logger.Errorf("Error=%v", err)
			println(stack())
		}
	}()
	o.block.Lock()
	defer o.block.Unlock()

	if o.dead {
		return
	}
	now := time.Now()
	var delbufIds []int32
	var bRefreshStats bool
	for k, v := range o.buffs {
		// CD 和 持续时间都到， 删除buff
		if now.After(v.OverTime) && now.After(v.CDTime) && v.Mark != constant.BuffMarkPassive {
			delbufIds = append(delbufIds, k)
			continue
		}
		// 持续时间到 , 清除效果保留至CD结束
		if now.After(v.OverTime) && v.Mark != constant.BuffMarkPassive {
			if o.buffs[k].Status == int32(constant.BuffStatusStart) {
				o.buffs[k].BuffStats.clear()
				bRefreshStats = true
				o.buffs[k].Status = int32(constant.BuffStatusStop)
			}
			continue
		}

		// 嘲讽状态 释放者退出 死亡 超过6米 均接触嘲讽状态
		if v.BuffType == constant.BuffTaunt {
			if v.Owner == nil || v.Owner.Dead() || maputil.Distance(o.location, v.Owner.Location()) > 6 {
				delbufIds = append(delbufIds, k)
				continue
			}
		}

		if v.TickFrequency == 0 {
			continue
		}

		if v.Rate != 0 && global.RandIn(int(BaseRate)) >= int(v.Rate) {
			continue
		}

		// 间隔未到
		if !now.After(v.TickTime) {
			continue
		}

		var damage, recover float64
		switch constant.BuffType(v.Pretype) {
		case constant.BuffFixedHeal, constant.BuffPercentageHeal:
			for i, e := range v.BuffStats.Attrs {
				if i == pb.Attribute_HEALTH {
					recover += float64(e)
				}
				if i == pb.Attribute_HEALTH_PERCENTAGE {
					recover += float64(o.GetMaxHP()) * float64(e) / BaseRate
				}
			}
		case constant.BuffFixedDamage, constant.BuffPercentageDamage, constant.BuffFixedDamage2:
			for i, e := range v.BuffStats.Attrs {
				if i == pb.Attribute_HEALTH {
					damage += float64(e)
				}
				if i == pb.Attribute_HEALTH_PERCENTAGE {
					damage += float64(o.GetMaxHP()) * float64(e) / BaseRate
				}
			}
		case constant.BuffFixedHealOnece:
			for i, e := range v.BuffStats.Attrs {
				if i == pb.Attribute_HEALTH {
					recover += float64(e)
				}
				if i == pb.Attribute_HEALTH_PERCENTAGE {
					recover += float64(o.GetMaxHP()) * float64(e) / BaseRate
				}
			}
			delbufIds = append(delbufIds, k)
		case constant.BuffAcidRain:
			damageRain := float64(o.GetMC()) * float64(v.BuffStats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE)) / BaseRate
			damageRain += damageRain * (1 + float64(v.BuffStats.getStats(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE))/BaseRate)
			o.magicAcidRain(damageRain, v.SkillID)
		}

		// 挫折意志 减伤效果只针对sype = 4 或 5
		var damage_percentage_decrease, flat_damage_decrease int32
		if v.SkillID > 0 {
			if skillCfg, err := global.GetSkillCfg(v.SkillID); err == nil {
				if skillCfg.SType == constant.SkillTypeCareerSpecial {
					damage_percentage_decrease += o.stat(pb.Attribute_CONTROL_DAMAGE_REDUCTION_PERCENTAGE)
					flat_damage_decrease += o.stat(pb.Attribute_CONTROL_DAMAGE_REDUCTION_FIXED_VALUE)
				}
			}
		}
		damage -= damage * float64(damage_percentage_decrease) / BaseRate
		damage -= float64(flat_damage_decrease)

		if damage > 0 {
			if o.changeHp(-int32(damage), v.Owner) == 0 {
				return
			}
		}
		if recover > 0 {
			if o.changeHp(int32(recover), v.Owner) == 0 {
				return
			}
		}
		o.buffs[k].TickTime = time.Now().Add(time.Duration(v.TickFrequency) * time.Millisecond)

		// if o.uid == 2576246359023689728 || o.uid == 2576264663478050817 || o.uid == 2577693926488023040 || o.uid == 2576293365498781696 || o.uid == 2576293512907595776 {
		// 	logger.Debugf("测试log processBuff[ %d:%s ] 触发定时buff  uid=%d ,  buffid=%d , damage=%f, recover=%f, 总血量= %d", o.uid, o.name, o.uid, v.Id, damage, recover, o.GetMaxHP())
		// }
	}

	if len(delbufIds) > 0 {
		for _, v := range delbufIds {
			o.delBuff(int32(v))
		}
		bRefreshStats = true
	}

	if bRefreshStats {
		o.refreshStats()
	}
}

func (o *MapObject) addBuff(id int32, owner IMapObject, addSec int32, exStats *Stats, addRate int32, skillID int32) {
	defer func() {
		if err := recover(); err != nil {
			logger.Errorf("Error=%v", err)
			println(stack())
		}
	}()
	o.block.Lock()
	defer o.block.Unlock()

	cfg, err := global.GetBuffCfg(id)
	if err != nil {
		return
	}
	now := time.Now()

	if buff, ok := o.buffs[id]; ok {
		if now.Before(buff.CDTime) {
			return
		}
	}

	// 同类型buff 效果强的替换掉效果弱的
	bRcover := false
	recoverId := 0
	for i, old_buff := range o.buffs {
		if old_buff.Pretype == cfg.Pretype {
			for old_i, old_v := range old_buff.BuffStats.Attrs {
				for _, cfg_v := range cfg.Value {
					if cfg_v.Key == int32(old_i) && old_v < cfg_v.Value {
						bRcover = true
						recoverId = int(i)
						break
					}
				}
				if exStats != nil {
					for ex_k, ex_v := range exStats.Attrs {
						if ex_k == old_i && old_v < ex_v {
							bRcover = true
							recoverId = int(i)
							break
						}
					}
				}
			}
			break
		}
	}
	if bRcover && recoverId != 0 {
		o.delBuff(int32(recoverId))
	}

	// 免疫时间 >= buff时间
	if cfg.LastTime+addSec <= 0 {
		return
	}

	buff := &BuffInfo{
		Id:            id,
		BuffType:      int32(cfg.Type),
		Mark:          cfg.Mark,
		StartTime:     now,
		TickTime:      now.Add(time.Duration(cfg.EffectTime) * time.Millisecond),
		OverTime:      now.Add(time.Duration(cfg.LastTime+addSec+100) * time.Millisecond),
		CDTime:        now.Add(time.Duration(cfg.CDTime) * time.Millisecond),
		Pretype:       cfg.Pretype,
		Owner:         owner,
		Status:        int32(constant.BuffStatusStart),
		BuffStats:     Stats{Attrs: make(map[pb.Attribute]int32)},
		TickFrequency: cfg.EffectTime,
		Rate:          cfg.Rate + addRate,
		SkillID:       skillID,
	}

	switch constant.BuffType(buff.Pretype) {
	case constant.BuffDecoy:
		return
	case constant.BuffPercentageDefenseDecrease,
		constant.BuffPercentageAttackDecrease,
		constant.BuffPercentageDefenseHealthDecrease,
		constant.BuffPercentageCritChanceDecrease,
		constant.BuffPercentageAccuracyDecrease:
		for _, v := range cfg.Value {
			if v.Key == int32(pb.Attribute_LIFE_RECOVERY_DECREASE_PERCENTAGE) {
				buff.BuffStats.setStats(pb.Attribute(v.Key), v.Value)
			} else {
				buff.BuffStats.setStats(pb.Attribute(v.Key), -v.Value)
			}
		}
		if exStats != nil {
			for k, v := range exStats.Attrs {
				if k == pb.Attribute_LIFE_RECOVERY_DECREASE_PERCENTAGE {
					buff.BuffStats.setStats(k, v)
				} else {
					buff.BuffStats.setStats(k, -v)
				}
			}
		}

	case constant.BuffShield:
		shieldHp := float64(o.GetMaxHP()) * float64(exStats.getStats(pb.Attribute_HEALTH_PERCENTAGE)) / BaseRate
		shieldHp = math.Max(shieldHp, 1)
		buff.BuffStats.setStats(pb.Attribute_HEALTH, int32(shieldHp))

	case constant.BuffTaunt:
		o.target = owner // 嘲讽强制改变攻击目标
	case constant.BuffCommonDebuff: // 通用减益
		for _, v := range cfg.Value {
			if v.Key == int32(pb.Attribute_ATTACK_SPEED_PERCENTAGE) || v.Key == int32(pb.Attribute_MOVE_SPEED_PERCENTAGE) {
				buff.BuffStats.setStats(pb.Attribute(v.Key), v.Value)
			} else {
				buff.BuffStats.setStats(pb.Attribute(v.Key), -v.Value)
			}
		}
		if exStats != nil {
			for k, v := range exStats.Attrs {
				if k == pb.Attribute_ATTACK_SPEED_PERCENTAGE || k == pb.Attribute_MOVE_SPEED_PERCENTAGE {
					buff.BuffStats.setStats(k, v)
				} else {
					buff.BuffStats.setStats(k, -v)
				}
			}
		}

	case constant.BuffCommonBuff: // 通用增益
		for _, v := range cfg.Value {
			if v.Key == int32(pb.Attribute_ATTACK_SPEED_PERCENTAGE) || v.Key == int32(pb.Attribute_MOVE_SPEED_PERCENTAGE) {
				buff.BuffStats.setStats(pb.Attribute(v.Key), -v.Value)
			} else {
				buff.BuffStats.setStats(pb.Attribute(v.Key), v.Value)
			}
		}
		if exStats != nil {
			for k, v := range exStats.Attrs {
				if k == pb.Attribute_ATTACK_SPEED_PERCENTAGE || k == pb.Attribute_MOVE_SPEED_PERCENTAGE {
					buff.BuffStats.setStats(k, -v)
				} else {
					buff.BuffStats.setStats(k, v)
				}
			}
		}

	default:
		for _, v := range cfg.Value {
			buff.BuffStats.setStats(pb.Attribute(v.Key), v.Value)
		}
		logger.Debugf("[%s-%v] 增加buff: %+v", o.name, o.uid, cfg.Value)
		if exStats != nil {
			for k, v := range exStats.Attrs {
				buff.BuffStats.setStats(k, v)
			}
		}
	}

	o.buffs[id] = buff

	packet := &pb.AddBuffPush{
		TargetId: o.Uid(),
		Buff: &pb.BuffInfo{
			BuffId:        id,
			RemainingTime: cfg.LastTime + addSec,
			BuffStats:     []*pb.Stats{},
			OwnerUid:      owner.Uid(),
		},
	}
	for bk, bv := range buff.BuffStats.Attrs {
		packet.Buff.BuffStats = append(packet.Buff.BuffStats, &pb.Stats{Attr: bk, Num: bv})
	}

	if o.desk != nil {
		o.desk.Broadcast(protocol.RouteAddBuffPush, packet)
	} else {
		o.pushMsg(protocol.RouteAddBuffPush, packet)
	}

	// logger.Debugf("++++[%s]++++ 增加buff,  BuffId=%d  skillId= %d  addSec= %d , stats = %v", o.name, buff.Id, skillID, addSec, buff.BuffStats.Attrs)
}

func (o *MapObject) delBuff(id int32) {
	// 装备升星几率的buf如果没有过期则不清理
	if buff, ok := o.buffs[id]; ok {
		if buff.Pretype == constant.BuffPercentEquipStarIncrease {
			if time.Now().Before(buff.OverTime) {
				return
			}
		}
	}

	delete(o.buffs, id)
	packet := &pb.RemoveBuffPush{BuffId: id, TargetId: o.Uid()}
	if o.desk != nil {
		o.desk.Broadcast(protocol.RouteRemoveBuffPush, packet)
	} else {
		o.pushMsg(protocol.RouteRemoveBuffPush, packet)
	}

	// logger.Debugf("----[%s]---- 删除buff ,BuffId=%d  ", o.name, id)
}

// 清理游戏内产生的buff
func (o *MapObject) resetBuff() {
	defer func() {
		if err := recover(); err != nil {
			logger.Errorf("Error=%v", err)
			println(stack())
		}
	}()
	if o.block.TryLock() {
		defer o.block.Unlock()
		whileBuff := make([]int32, 0)
		if player, ok := defaultManager.player(o.uid); ok {
			if len(player.pet) != 0 {
				if player.pet[0].PetCfgId != 0 {
					// 查询宠物buff
					petInfo := global.GetPetCfg(player.pet[0].PetCfgId)
					for _, v := range petInfo.Talent {
						whileBuff = append(whileBuff, v.ID)
					}
				}
			}
		}
		var delbuffs []int32
		for k := range o.buffs {
			if o.buffs[k].Pretype == constant.BuffPercentEquipStarIncrease {
				continue
			}

			if o.buffs[k].Mark == constant.BuffMarkPassive {
				continue
			}
			// 在白名单的buff也要跳过
			in := false
			for _, v := range whileBuff {
				if v == k {
					in = true
					break
				}
			}
			if in {
				continue
			}
			delbuffs = append(delbuffs, k)
		}

		for i := 0; i < len(delbuffs); i++ {
			o.delBuff(delbuffs[i])
		}
	}

	for i := range o.skills {
		o.skills[i].Cooldown = time.Now()
	}
}

func (o *MapObject) pushMsg(route string, msg interface{}) {
	if o.objectType == ObjectTypePlayer {
		if player, ok := defaultManager.player(o.uid); ok {
			player.PushMsg(route, msg)
		}
	} else if o.objectType == ObjectTypePartner {
		if player, ok := defaultManager.player(o.Owner().Uid()); ok {
			player.PushMsg(route, msg)
		}
	}
}

func (o *MapObject) refreshStats() {
	// 主角和伙伴属性独立计算
	uid := o.uid
	if o.objectType == ObjectTypePartner {
		uid = o.Owner().Uid()
	}
	if player, ok := defaultManager.player(uid); ok {
		player.refreshStats()
		return
	}

	// 怪物 机器人
	now := time.Now()
	o.stats.clear()
	baseStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	if o.objectType == ObjectTypeMonster && o.cfgId != 0 {
		cfg := global.GetMonsterCfg(o.cfgId)
		if cfg == nil {
			return
		}
		// 基础属性
		for _, v := range cfg.BassProperty {
			baseStatus.setStats(pb.Attribute(v.Key), v.Value)
		}
	} else if o.objectType == ObjectTypeRobot && o.cfgId != 0 {
		if r := robotMgr.getRankRobotInfo(o.serverID, o.uid); r != nil {
			if r.robotType == RobotTypePlayer {
				r.refreshStats()
				return
			} else {
				if r.cfg != nil {
					for _, attr := range r.cfg.Feature {
						baseStatus.setStats(pb.Attribute(attr.Key), attr.Value)
					}

					baseStatus.changeStats(pb.Attribute_ATTACK_SPEED, global.GetRoleInitial(int32(r.job)).AttackSpeed)
					baseStatus.changeStats(pb.Attribute_MOVE_SPEED, global.GetRoleInitial(int32(r.job)).MoveSpeed)

					// 刷新伙伴属性
					for _, v := range r.partnerSlots {
						if v == nil {
							continue
						}
						partner := r.getPartnerInfo(v.PartnerUid)
						if partner == nil {
							continue
						}
						partner.refreshStats(nil, r.Player)
					}
				}
			}
		}
	}

	for bk, bv := range baseStatus.Attrs {
		o.stats.setStats(bk, bv)
	}

	for _, buff := range o.buffs {
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

		for i, e := range buff.BuffStats.Attrs {
			o.stats.setStats(i, e)
		}
	}

	// < 0  攻速移速 增益，其他属性减益 ; >0 攻速移速 减益，其他属性增益
	o.stats.changeStats(pb.Attribute_HEALTH, o.currentHP)

	o.stats.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(o.GetDC())*(float64(o.stats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(o.GetMC())*(float64(o.stats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(o.GetAR())*(float64(o.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(o.GetMR())*(float64(o.stats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))

	o.stats.setStats(pb.Attribute_DODGE, int32(float64(o.stats.getStats(pb.Attribute_DODGE))*(float64(o.stats.getStats(pb.Attribute_DODGE_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_HIT, int32(float64(o.stats.getStats(pb.Attribute_HIT))*(float64(o.stats.getStats(pb.Attribute_HIT_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_CRITICAL_STRIKE, int32(float64(o.stats.getStats(pb.Attribute_CRITICAL_STRIKE))*(float64(o.stats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_TOUGHNESS, int32(float64(o.stats.getStats(pb.Attribute_TOUGHNESS))*(float64(o.stats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE))/BaseRate)))
	o.stats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(o.GetMaxHP())*(float64(o.stats.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate)))

	// 攻速移速增加  基础/(1+加成)；攻速移速降低  基础*(1+加成)
	o.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(float64(o.stats.getStats(pb.Attribute_ATTACK_SPEED))/(1+(float64(o.stats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE))/BaseRate))))
	o.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(float64(o.stats.getStats(pb.Attribute_MOVE_SPEED))/(1+(float64(o.stats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE))/BaseRate))))

	// 基础属性 >= 0
	o.stats.changeStats(pb.Attribute_PHYSICAL_ATTACK, int32(math.Max(0, float64(o.GetDC()))))
	o.stats.changeStats(pb.Attribute_MAGIC_ATTACK, int32(math.Max(0, float64(o.GetMC()))))
	o.stats.changeStats(pb.Attribute_PHYSICAL_DEFENSE, int32(math.Max(0, float64(o.GetAR()))))
	o.stats.changeStats(pb.Attribute_MAGIC_DEFENSE, int32(math.Max(0, float64(o.GetMR()))))

	o.stats.changeStats(pb.Attribute_DODGE, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_DODGE)))))
	o.stats.changeStats(pb.Attribute_HIT, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_HIT)))))
	o.stats.changeStats(pb.Attribute_CRITICAL_STRIKE, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_CRITICAL_STRIKE)))))
	o.stats.changeStats(pb.Attribute_TOUGHNESS, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_TOUGHNESS)))))
	o.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_ATTACK_SPEED)))))
	o.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(math.Max(0, float64(o.stats.getStats(pb.Attribute_MOVE_SPEED)))))
	o.stats.changeStats(pb.Attribute_MAX_HEALTH, int32(math.Max(0, float64(o.GetMaxHP()))))
}

// 死亡
func (o *MapObject) die(object IMapObject) {
	if o.desk == nil {
		logger.Errorf("%d 死亡异常 , 桌子已经被清理 uid %d", o.objectType, o.uid)
		return
	}

	if o.dead {
		return
	}

	if object != nil {
		object.ClearTarget()
		if object.ObjectType() == ObjectTypePartner {
			object.Owner().ClearTarget()
		}
	}

	// 替身死亡AOE
	if o.linkedSkill/100 == constant.SkillTypeDecoy && o.objectType == ObjectTypeDecoy {
		o.DecoyExplosionEnd()
	} else if o.objectType == ObjectTypePlayer {
		if player, ok := defaultManager.player(o.uid); ok {
			player.die(object)

			if buff := player.getBuffByType(constant.BuffReviveOnce); buff != nil {
				hp := float64(player.GetMaxHP()) * float64(buff.BuffStats.getStats(pb.Attribute_HEALTH_PERCENTAGE)) / BaseRate
				time.AfterFunc(500*time.Millisecond, func() {
					player.Revive(nil, 0, int32(hp))
				})
				player.delBuff(buff.Id)
				logger.Debugf("宠物复活 目标[%s] 复活血量%d  总血量 %d", o.name, int32(hp), player.GetMaxHP())
			} else if skill, caster := player.getSlotSkillByType(constant.SkillTypeReborn); skill != nil {
				if caster != nil && !caster.dead && caster.uid != o.uid {
					caster.lastGasp(o, skill.CfgId)
				}
			}
		}
		return
	} else if o.objectType == ObjectTypePartner {
		if owner, err := o.desk.playerWithId(o.Owner().Uid()); err == nil {
			if skill, caster := owner.getSlotSkillByType(constant.SkillTypeReborn); skill != nil {
				if caster != nil && !caster.dead && caster.uid != o.uid {
					caster.lastGasp(o, skill.CfgId)
				}
			}
		}
	} else if o.objectType == ObjectTypeMonster {
		if m, ok := o.desk.monsters[o.uid]; ok {
			m.die(object)
		}
		return
	} else if o.objectType == ObjectTypeRobot {
		if r := robotMgr.getRankRobotInfo(o.serverID, o.uid); r != nil {
			r.die(object)
			if skill, caster := r.getSlotSkillByType(constant.SkillTypeReborn); skill != nil {
				caster.lastGasp(o, skill.CfgId)
			}
		}
	}

	o.dead = true
	o.deadTime = time.Now()
	o.target = nil

	packet := pb.ObjectDiePush{ObjectId: o.uid, ReviveTip: false}
	o.desk.Broadcast(protocol.RouteObjectDiePush, &packet)

	// logger.Errorf(" ==死亡== OBType [%d],  uid %d", o.objectType, o.uid)
}

func (o *MapObject) reset() {
	o.desk = nil
	o.target = nil
	o.dead = false

	o.stats.setStats(pb.Attribute_HEALTH, o.stats.getStats(pb.Attribute_MAX_HEALTH))

	o.currentHP = o.GetMaxHP()
	o.displayHP = o.currentHP
	o.reviveTimes = 0

	o.resetBuff()
}

// 延迟攻击
func (o *MapObject) delayAttack(target IMapObject, skillId int32) {
	if !o.CanAttackTarget(target) {
		return
	}
	var power float64

	power += math.Max(float64(o.GetMC()), float64(o.GetDC()))
	// 攻击-防御
	if tstats := target.GetStats(); tstats != nil {
		power -= float64(tstats.getStats(maputil.DefenseAttributes(o.owner.AttackAttributes())))
	}

	if power < 1 {
		power = float64(global.RandIn(2)) + 1
	}
	var attacker IMapObject
	if o.objectType >= ObjectTypeBuff && o.owner != nil {
		attacker = o.owner
	} else {
		attacker = o
	}
	target.Attacked(attacker, skillId, 0, int32(power), true)
	// logger.Debugf("[%v] 攻击 targetID: %d  目标位置: %v   Power:%f ", o.objectType, target.Uid(), target.Location(), power)
}

func (o *MapObject) getMoveSpeed() int32 { return o.stats.getStats(pb.Attribute_MOVE_SPEED) }

func (o *MapObject) getAttackSpeed() int32 { return o.stats.getStats(pb.Attribute_ATTACK_SPEED) }

func (o *MapObject) getSkill(skillId int32) *PlayerSkill {
	for _, v := range o.skills {
		if v.CfgId == skillId {
			return v
		}
	}
	return nil
}

func (o *MapObject) setAttackTime(delay int32) {
	// o.attackTime = time.Now().Add(time.Duration(delay-50) * time.Millisecond)
	o.attackTime = time.Now().Add(time.Duration(o.getAttackSpeed()) * time.Millisecond)
}

// Base Interface
func (o *MapObject) HaveBuff(pretype constant.BuffType) bool     { return o.getBuffByType(pretype) != nil }
func (o *MapObject) GetBuff(pretype constant.BuffType) *BuffInfo { return o.getBuffByType(pretype) }
func (o *MapObject) CfgID() int32                                { return o.cfgId }
func (o *MapObject) GetCell() *Cell                              { return o.cell }
func (o *MapObject) SetCell(cell *Cell)                          { o.cell = cell }
func (o *MapObject) SettDirection(dir pb.Direction)              { o.direction = dir }
func (o *MapObject) GetDirection() pb.Direction                  { return o.direction }
func (o *MapObject) SetLocation(l *pb.Point)                     { o.location = l }
func (o *MapObject) Reflect()                                    { o.displayReflect = true }
func (o *MapObject) SuckingBlood()                               { o.displaySuckingBlood = true }
func (o *MapObject) Dead() bool                                  { return o.dead }
func (o *MapObject) SetDesk(desk *Desk)                          { o.desk = desk }
func (o *MapObject) Desk() *Desk                                 { return o.desk }
func (o *MapObject) Uid() int64                                  { return o.uid }
func (o *MapObject) Name() string                                { return o.name }
func (o *MapObject) ObjectType() ObjectType                      { return o.objectType }
func (o *MapObject) GetStats() *Stats                            { return o.stats }
func (o *MapObject) GetDC() int32                                { return o.stats.getStats(pb.Attribute_PHYSICAL_ATTACK) }
func (o *MapObject) GetMC() int32                                { return o.stats.getStats(pb.Attribute_MAGIC_ATTACK) }
func (o *MapObject) GetAR() int32                                { return o.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE) }
func (o *MapObject) GetMR() int32                                { return o.stats.getStats(pb.Attribute_MAGIC_DEFENSE) }
func (o *MapObject) GetMaxHP() int32                             { return o.stats.getStats(pb.Attribute_MAX_HEALTH) }
func (o *MapObject) GetCurHP() int32                             { return o.stats.getStats(pb.Attribute_HEALTH) }
func (o *MapObject) Dodged()                                     { o.displayMiss = true }
func (o *MapObject) Critical()                                   { o.displayCrit = true }
func (o *MapObject) Owner() IMapObject                           { return o.owner }
func (o *MapObject) GetObState() ObjectAction                    { return o.obAction }
func (o *MapObject) SetObState(state ObjectAction)               { o.obAction = state }
func (o *MapObject) Level() int32                                { return o.level }
func (o *MapObject) Job() constant.MowJob                        { return o.job }
func (o *MapObject) ServerId() int32                             { return o.serverID }

func (o *MapObject) IsWarrior() bool {
	return o.job == constant.Warrior || o.job == constant.PartnerWarrior
}

func (o *MapObject) IsRobot() bool {
	return o.bRobot
}

func (o *MapObject) AttackAttributes() pb.Attribute {
	if o.job == constant.Warrior || o.job == constant.PartnerWarrior {
		return pb.Attribute_PHYSICAL_ATTACK
	} else if o.job == constant.Wizard || o.job == constant.PartnerWizard || o.job == constant.Ritualist || o.job == constant.PartnerRitualist {
		return pb.Attribute_MAGIC_ATTACK
	} else {
		if o.GetDC() >= o.GetMC() {
			return pb.Attribute_PHYSICAL_ATTACK
		} else {
			return pb.Attribute_MAGIC_ATTACK
		}
	}
}

// 被攻击
func (o *MapObject) Attacked(attacker IMapObject, skillId int32, skillAdvanceLevel int32, power int32, bReflect bool) {
	if o.dead || o.desk == nil || o.uid == 0 {
		logger.Warningf(" ===异常===  [%v] 被攻击失败 dead:%t  muid: %d", o.objectType, o.dead, o.uid)
		return
	}

	// debug code
	// if o.uid == 2576246359023689728 || o.uid == 2576264663478050817 || o.uid == 2577693926488023040 || o.uid == 2576293365498781696 || o.uid == 2576293512907595776 {
	// 	logger.Warningf("测试log Attacked[%d:%s] 被攻击 伤害 %d 技能ID %d, 攻击者ID:%d  名字:%s  类型:%v", o.uid, o.name, power, skillId, attacker.Uid(), attacker.Name(), attacker.ObjectType())
	// }

	skillCfg, err := global.GetSkillCfg(skillId)
	if err != nil {
		return
	}

	// 斩杀先判断
	if skillId/100 == constant.SkillTypeExecute {
		if float64(o.currentHP)/float64(o.GetMaxHP()) <= float64(skillCfg.SpecialEffect.Slay)/BaseRate {
			o.changeHp(-o.currentHP, attacker)
			return
		}
	}

	if power > 0 {
		critical := attacker.GetStats().getStats(pb.Attribute_CRITICAL_STRIKE)
		tenacity := o.stats.getStats(pb.Attribute_TOUGHNESS)
		criticalRate := (math.Max(0, float64(critical-tenacity))*global.GlobalCfg.BaseValue.Crit.K + float64(global.GlobalCfg.BaseValue.Crit.PreValue))

		var damage, finalDamage float64
		damage = float64(power)
		if global.RandIn(100) < int(criticalRate) {
			criticalDamageRate := float64(attacker.GetStats().getStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)) / BaseRate
			damage = float64(power) * (1 + criticalDamageRate)
			o.Critical()
		}

		// 减伤,包含buff中的减伤
		damage -= float64(o.stat(pb.Attribute_DMT_FINAL_REDUCE_FIXED))
		if attacker.ObjectType() == ObjectTypeMonster {
			damage -= float64(o.stat(pb.Attribute_DMT_REDUCE_FIXED_MONSTER))
			damage -= damage * float64(o.stat(pb.Attribute_DMT_REDUCE_PERCENTAGE_MONSTER)) / BaseRate
		} else {
			damage -= float64(o.stat(pb.Attribute_DMT_REDUCE_FIXED_PLAYER))
			damage -= damage * float64(o.stat(pb.Attribute_DMT_REDUCE_PERCENTAGE_PLAYER)) / BaseRate
		}
		damage -= damage * float64(o.stat(pb.Attribute_DMT_FINAL_REDUCE_PERCENTAGE)) / BaseRate

		damage = math.Max(damage, float64(global.RandIn(2)+1))

		// 护盾
		if buff := o.getBuffByType(constant.BuffShield); buff != nil {
			if buff.BuffStats.getStats(pb.Attribute_HEALTH) > int32(damage) {
				buff.BuffStats.setStats(pb.Attribute_HEALTH, -int32(damage))
				damage = 0
			} else {
				damage = damage - float64(buff.BuffStats.getStats(pb.Attribute_HEALTH))
				o.delBuff(buff.Id)
			}
		}
		// 如果有神圣天赋，豁免超过生命值百分比的伤害
		if buff := o.getBuffByType(constant.BuffDamageLimit); buff != nil {
			if damage > float64(o.GetMaxHP())*float64(buff.BuffStats.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate {
				damage = float64(o.GetMaxHP()) * float64(buff.BuffStats.getStats(pb.Attribute_HEALTH_PERCENTAGE)) / BaseRate
			}
		}

		finalDamage = damage

		if buff := o.getBuffByType(constant.BuffImmune); buff != nil {
			finalDamage = 0
		}

		o.attackId = attacker.Uid()
		o.displaySkillId = skillId
		// logger.Debugf("... ObType = [%v:%s]  ID= %d 被攻击。 攻击者Uid: %d--%s ,  技能ID: %d  伤害: %f  暴击：%t  闪避：%t",
		//	o.objectType, o.name, o.uid, attacker.Uid(), attacker.Name(), skillId, damage, o.displayCrit, o.displayMiss)

		if o.desk.roomCfg.Roomtype == constant.GuildMap && attacker.ObjectType() == ObjectTypePlayer {
			o.desk.recordGuildBossDamage(attacker.Uid(), int64(damage))
		}

		if finalDamage > 0 && o.changeHp(-int32(finalDamage), attacker) == 0 {
			return
		}

		if o.objectType == ObjectTypeMonster && o.CanAttackTarget(attacker) && o.target == nil {
			o.target = attacker
		}

		// 反伤
		if bReflect && o.stat(pb.Attribute_REFLECT_DAMAGE_PERCENTAGE) > 0 {
			if buff := o.getBuffByType(constant.BuffReflectDamage); buff != nil {
				if global.RandIn(int(BaseRate)) < int(buff.Rate) {
					reflectDamage := damage * float64(o.stat(pb.Attribute_REFLECT_DAMAGE_PERCENTAGE)) / BaseRate
					if reflectDamage >= 1 {
						attacker.ChangeHp(-int32(reflectDamage), o)
						attacker.Reflect()
						// logger.Debugf(" 反伤  ID= %d:%s 伤害: %f  攻击者 %d:%s  ", o.uid, o.name, reflectDamage, attacker.Uid(), attacker.Name())
					}
				}
			}
		}
		// 胆怯
		if buff := o.getBuffByType(constant.BuffPercentageAttackDefenseCritDecrease); buff != nil {
			attacker.AddBuff(100, o, 10000000, &Stats{Attrs: buff.BuffStats.Attrs}, 0, 0)
		}
		// 吸血
		if o.stat(pb.Attribute_DAMAGE_CONVERT_PERCENTAGE) > 0 {
			suckingBlood := damage * float64(o.stat(pb.Attribute_DAMAGE_CONVERT_PERCENTAGE)/int32(BaseRate))
			suckingBlood = math.Max(suckingBlood, 1)
			if suckingBlood > 0 {
				o.changeHp(int32(suckingBlood), attacker)
			}
		}
	}

	// 技能关联buff触发
	if !o.dead {
		for _, v := range skillCfg.TriggerBuff {
			bufCfg, err := global.GetBuffCfg(v)
			if err != nil {
				continue
			}
			if bufCfg.Type == constant.BuffTypeBuff {
				continue
			}

			// 免疫debuff
			if immuneBuff := o.getBuffByType(constant.BuffNoNegativeEffect); immuneBuff != nil {
				// 有几率免疫
				if global.RandIn(int(BaseRate)) < int(immuneBuff.Rate) {
					continue
				}
			}
			if immuneBuff := o.getBuffByType(constant.BuffImmune); immuneBuff != nil {
				continue
			}

			var addSec, addRate, addDam, addDistance, subCtl int32
			// 基础buff持续时间 与触发概率
			addSec += skillCfg.Bufftime
			addRate += skillCfg.Effecttrigger
			// 技能进阶效果
			if skillUpperlevel, err := global.GetUpperlevelSettingCfg(skillCfg.UID, skillAdvanceLevel); err == nil {
				subCtl += skillUpperlevel.Control
				addDam += skillUpperlevel.DamagePer

				addSec += skillUpperlevel.DurationCon
				addRate += skillUpperlevel.Trigger
				addDistance += skillUpperlevel.Distance
			}

			if skillCfg.Skilltrigger > 0 && global.RandIn(int(BaseRate)) >= int(skillCfg.Skilltrigger+addRate) {
				continue
			}

			var bufStats *Stats
			if bufCfg.Mark == constant.BuffTypeChainSkill {
				bufStats = &Stats{Attrs: make(map[pb.Attribute]int32)}

				if bufCfg.Pretype == constant.BuffFixedDamage2 {
					for _, v := range skillCfg.Skillbuff {
						bleeding := float64(attacker.GetStats().getStats(attacker.AttackAttributes())) * float64(v.Value) / BaseRate
						bleeding = math.Max(1, bleeding-float64(o.stat(maputil.DefenseAttributes(attacker.AttackAttributes()))))
						bufStats.setStats(pb.Attribute_HEALTH, int32(bleeding))
					}
				} else if bufCfg.Pretype == int32(constant.BuffPercentageAttackDecrease) { // 减双攻
					for _, v := range skillCfg.Skillbuff {
						if v.Key != int32(pb.Attribute_PHYSICAL_ATTACK) && v.Key != int32(pb.Attribute_HEALTH_PERCENTAGE) &&
							v.Key != int32(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE) && v.Key != int32(pb.Attribute_MAGIC_ATTACK_PERCENTAGE) {
							continue
						}
						bufStats.setStats(pb.Attribute(v.Key), v.Value)
					}
				} else if bufCfg.Pretype == int32(constant.BuffPercentageDefenseHealthDecrease) { // 减双防 减回血
					for _, v := range skillCfg.Skillbuff {
						if v.Key != int32(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE) && v.Key != int32(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE) && v.Key != int32(pb.Attribute_LIFE_RECOVERY_DECREASE_PERCENTAGE) {
							continue
						}
						bufStats.setStats(pb.Attribute(v.Key), v.Value)
					}
				} else if bufCfg.Pretype == int32(constant.BuffPercentageAttackSpeedDecrease) { // 减攻击速度
					for _, v := range skillCfg.Skillbuff {
						if v.Key != int32(pb.Attribute_ATTACK_SPEED_PERCENTAGE) {
							continue
						}
						bufStats.setStats(pb.Attribute(v.Key), v.Value)
					}
				} else if bufCfg.Pretype == int32(constant.BuffPercentageAccuracyDecrease) { // 减命中
					for _, v := range skillCfg.Skillbuff {
						if v.Key != int32(pb.Attribute_HIT_PERCENTAGE) {
							continue
						}
						bufStats.setStats(pb.Attribute(v.Key), v.Value)
					}
				} else {
					for _, v := range skillCfg.Skillbuff {
						bufStats.setStats(pb.Attribute(v.Key), v.Value)
					}
				}

				// 控制时间减去角色免控时间
				if bufCfg.Pretype == constant.BuffFreeze || bufCfg.Pretype == constant.BuffTaunt {
					if myBuff := o.getBuffByType(constant.BuffTauntTimeReduce); myBuff != nil {
						redeceSec := float64(addSec+bufCfg.LastTime) * float64(o.stats.getStats(pb.Attribute_REDUCE_CONTROL_TIME_PERCENTAGE)) / BaseRate
						addSec -= int32(redeceSec)
					}
					if skillCfg.SType == constant.SkillTypeCareerSpecial4 || skillCfg.SType == constant.SkillTypeCareerSpecial5 {
						redeceSec := float64(o.stat(pb.Attribute_CONTROL_TIME_REDUCTION_FIXED_VALUE))
						redeceSec += float64(addSec) * float64(o.stat(pb.Attribute_CONTROL_TIME_REDUCTION_PERCENTAGE)) / BaseRate
						addSec -= int32(redeceSec)
					}
				}
			}
			if addSec > 0 {
				o.AddBuff(bufCfg.ID, attacker, addSec, bufStats, addRate, skillId)
			}
		}
	}

	// 技能特殊效果触发
	switch skillId / 100 {
	case constant.SkillTypeRepelFoe:
		o.repelFoeEnd(skillId, attacker)

	case constant.SkillTypeNormalAttackWizard:
		if attacker.HaveBuff(constant.BuffKnockback) {
			if o.HaveBuff(constant.BuffFreeze) {
				break
			}

			if buff := attacker.GetBuff(constant.BuffKnockback); buff != nil {
				if buffSkillCfg, err := global.GetSkillCfg(buff.SkillID); err == nil {
					if global.RandIn(int(BaseRate)) >= int(buff.Rate) {
						return
					}

					var backDir pb.Direction
					var endCell *Cell
					backDir = maputil.DirectionFromPointI(&attacker.GetCell().location, &o.cell.location)
					for i := 1; i <= int(buffSkillCfg.SpecialEffect.BackDistence); i++ {
						point := maputil.Move(&o.cell.location, backDir, int(i))
						if cell := o.desk.mapInfo.getCell(point); cell != nil {
							if !cell.block {
								endCell = cell
								continue
							}
						}
						break
					}
					if endCell == nil {
						return
					}
					if o.desk.mapInfo.locationChange(o, &pb.Point{X: float64(endCell.location.X), Y: float64(endCell.location.Y)}) {
						o.desk.Broadcast(protocol.RouteTeleportPush, &pb.TeleportPush{Uid: o.uid, Location: o.location, Direction: o.direction})
					}
				}
			}
		}

	case constant.SkillTypeApocalypse:
		dmg := float64(o.GetMaxHP()) * float64(skillCfg.SpecialEffect.HPDam) / BaseRate
		o.changeHp(int32(-dmg), attacker)
	}
}

// 死亡
func (o *MapObject) Die(attacker *MapObject) {
	if o.dead {
		return
	}
}

// 复活
func (o *MapObject) Revive(skill *PlayerSkill, skillAdvanceLevel, reviveHp int32) {
	if o.desk == nil {
		logger.Errorf("[%s]: %d 玩家复活！！！ desk为空", o.name, o.uid)
		return
	}

	o.dead = false
	o.reviveTimes++
	o.currentHP = reviveHp
	o.displayHP = o.currentHP
	o.stats.setStats(pb.Attribute_HEALTH, o.currentHP)
	o.resetBuff()

	o.SkillBuffTrigger(skill)

	o.desk.Broadcast(protocol.RouteRevivePush, &pb.RevivePush{
		Uid:       o.uid,
		Location:  o.location,
		Direction: o.direction,
		CurHp:     o.currentHP,
	})

	o.refreshStats()

	// logger.Debugf("玩家复活！！！   当前血量 %d -- %d --- %d", o.currentHP, o.stat(pb.Attribute_HEALTH), o.GetMaxHP())
}

func (o *MapObject) CleanDebuff() {
	if o.block.TryLock() {
		defer o.block.Unlock()

		var delbuffs []int32
		for k := range o.buffs {
			if o.buffs[k].BuffType == constant.BuffTypeDebuff {
				delbuffs = append(delbuffs, k)
			}
		}

		for i := 0; i < len(delbuffs); i++ {
			o.delBuff(delbuffs[i])
		}
	}
}

func (o *MapObject) AddBuff(id int32, owner IMapObject, addSec int32, exStats *Stats, addRate int32, skillID int32) {
	o.addBuff(id, owner, addSec, exStats, addRate, skillID)

	// logger.Debugf(" [%s] 增加buff 之前,  BuffId=%d  攻击 %d : %d  防御 %d:%d 暴击 %d", o.name, id, o.GetDC(), o.GetMC(), o.GetAR(), o.GetMR(), o.GetStats().getStats(pb.Attribute_CRITICAL_STRIKE))

	o.refreshStats()

	// logger.Debugf(" [%s] 增加buff 之后,  BuffId=%d  攻击 %d : %d  防御 %d:%d 暴击 %d", o.name, id, o.GetDC(), o.GetMC(), o.GetAR(), o.GetMR(), o.GetStats().getStats(pb.Attribute_CRITICAL_STRIKE))
}

func (o *MapObject) AddBuffNoRefresh(id int32, owner IMapObject, addSec int32, exStats *Stats, addRate int32, skillID int32) {
	o.addBuff(id, owner, addSec, exStats, addRate, skillID)
}

func (o *MapObject) Target() IMapObject {
	return o.target
}

func (o *MapObject) ClearTarget() {
	o.target = nil
}

func (o *MapObject) Friends() []IMapObject {
	var friends []IMapObject
	if o.desk == nil {
		return friends
	}

	if o.objectType == ObjectTypePlayer {
		friends = append(friends, o)
		if p, err := o.desk.playerWithId(o.uid); err == nil {
			for _, slot := range p.partnerSlots {
				if slot.PartnerUid == 0 {
					continue
				}
				if partner, err := o.desk.objectWithId(slot.PartnerUid); err == nil {
					friends = append(friends, partner)
				}
			}
		}
	} else if o.objectType == ObjectTypePartner {
		if p, err := o.desk.playerWithId(o.owner.Uid()); err == nil {
			for _, slot := range p.partnerSlots {
				if slot.PartnerUid == 0 {
					continue
				}
				if partner := p.getPartnerInfoBySlotId(slot.SlotId); partner != nil {
					friends = append(friends, partner)
				}
			}
			friends = append(friends, p)
		}
	}
	return friends
}

// 技能buff触发
func (o *MapObject) SkillBuffTrigger(skill *PlayerSkill) {
	if skill == nil {
		return
	}

	skillCfg, err := global.GetSkillCfg(skill.CfgId)
	if err == nil {
		for _, tri := range skillCfg.TriggerBuff {
			bufCfg, err := global.GetBuffCfg(tri)
			if err != nil {
				continue
			}
			if bufCfg.Type == constant.BuffTypeDebuff {
				continue
			}

			// 筛除特殊buff
			if bufCfg.Pretype == constant.BuffDecoy {
				continue
			}

			var addSec, addRate, addDam, addDistance, subCtl int32
			addRate += skillCfg.Effecttrigger
			addSec += skillCfg.Bufftime
			// 技能进阶效果
			if skillUpperlevel, err := global.GetUpperlevelSettingCfg(skillCfg.UID, skill.AdvancedLevel); err == nil {
				subCtl += skillUpperlevel.Control
				addDam += skillUpperlevel.DamagePer

				addSec += skillUpperlevel.DurationCon
				addRate += skillUpperlevel.Trigger
				addDistance += skillUpperlevel.Distance
			}

			// 技能关联的buff 并且 有触发几率 才计算概率
			if bufCfg.Mark == constant.BuffTypeChainSkill && skillCfg.Skilltrigger > 0 && global.RandIn(int(BaseRate)) >= int(skillCfg.Skilltrigger+addRate) {
				continue
			}

			var bufStats *Stats
			if bufCfg.Mark == constant.BuffTypeChainSkill {
				bufStats = &Stats{Attrs: make(map[pb.Attribute]int32)}
				for _, v := range skillCfg.Skillbuff {
					bufStats.setStats(pb.Attribute(v.Key), v.Value)
				}

				if addDam > 0 {
					bufStats.setStats(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE, addDam)
				}
				if subCtl > 0 {
					bufStats.setStats(pb.Attribute_CONTROL_TIME_REDUCTION_PERCENTAGE, subCtl)
				}
			}

			// 影响范围
			var target_friend []IMapObject
			if skillCfg.Effectnum == constant.SkillEffectnumSlef {
				target_friend = append(target_friend, o)
			} else if skillCfg.Effectnum == constant.SkillEffectnumAlliance {
				target_friend = o.Friends()
			} else if skillCfg.Effectnum == constant.SkillEffectnumAllianceLowHp {

				low_hp := float64(o.currentHP) / float64(o.GetMaxHP())
				var target IMapObject = o
				for _, v := range o.Friends() {
					if !o.CanHelpTarget(v) {
						continue
					}
					if v.Dead() {
						continue
					}

					tmp_low := float64(v.GetStats().getStats(pb.Attribute_HEALTH)) / float64(v.GetMaxHP())
					if tmp_low < low_hp {
						low_hp = tmp_low
						target = v
					}
				}
				target_friend = append(target_friend, target)
				logger.Debugf("%s 释放技能 [%d] 选择血量最低目标 %d:%s , 血量百分比 %.2f", o.name, skill.CfgId, target.Uid(), target.Name(), low_hp)
			}

			for _, v := range target_friend {
				if skillCfg.SkillID == constant.SkillTypeMassPurificationBoss && (v.Job() == constant.Warrior || v.Job() == constant.PartnerWarrior) {
					continue
				}
				v.AddBuff(bufCfg.ID, o, addSec, bufStats, addRate, skill.CfgId)
			}
		}
	}
}

func (o *MapObject) ChangeHp(hp int32, obj IMapObject) {
	o.changeHp(hp, obj)
}
