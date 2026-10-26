package game

import (
	"math"
	"math/rand"
	"sync"
	"time"

	"nanoserver/global"
	"nanoserver/pkg/astar"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/errutil"
	"nanoserver/pkg/maputil"
	"nanoserver/pkg/snowflake"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"
)

const NormalSkillID = 999900

const (
	MTypeNormal     int32 = iota // 0：代表不是boss
	MTypeCommonBoss              // 1：代表普通boss
	MTypeWorldBoss               // 2：代表世界boss
	MTypeGathering               // 3：代表采集怪物
)

type MSkill struct {
	CfgId    int32     `json:"cfgid"`
	Cooldown time.Time `json:"-"`
	// Cfg      *global.SkillConfig
}

type MonsterInfo struct {
	lock sync.Mutex
	*MapObject
	level      int32 // 等级
	createTime int64 // 创建时间(毫秒时间戳) time.Now().UnixNano()/1e6
	reliveTime int64 // 重生时间
	cfg        *global.MonsterConfig
	roamPath   []*astar.APoint
	searchTime time.Time
	roamTime   time.Time
	skills     []*MSkill
}

func newMonster(cfgId int32) *MonsterInfo {
	now := time.Now().Unix()
	uid := snowflake.GenID()

	cfg := global.GetMonsterCfg(cfgId)
	if cfg == nil {
		return nil
	}

	m := &MonsterInfo{
		level:      cfg.Level,
		createTime: now,
		reliveTime: int64(cfg.Relive),
		MapObject:  NewMapObject(ObjectTypeMonster, uid),
		cfg:        cfg,
		roamPath:   []*astar.APoint{},
		skills:     []*MSkill{},
	}

	for _, v := range cfg.BassProperty {
		m.stats.setStats(pb.Attribute(v.Key), v.Value)
	}
	for _, v := range cfg.Skills {
		m.skills = append(m.skills, &MSkill{CfgId: int32(v)})
	}

	m.cfgId = cfgId
	m.name = cfg.Name
	m.currentHP = m.stats.getStats(pb.Attribute_MAX_HEALTH)
	m.displayHP = m.currentHP
	m.stats.setStats(pb.Attribute_HEALTH, m.stats.getStats(pb.Attribute_MAX_HEALTH))
	m.roamTime = time.Now().Add(time.Second * time.Duration(global.RandIn(5)+1))
	return m
}

// IMapObject interface

// IMapObject interface end

// 计算怪物属性
func (m *MonsterInfo) RefreshStats() {
	m.stats.clear()

	// 基础属性
	for _, v := range m.cfg.BassProperty {
		m.stats.setStats(pb.Attribute(v.Key), v.Value)
	}

	// buff
	for _, v := range m.buffs {
		switch constant.BuffType(v.Pretype) {
		case constant.BuffPercentageDefenseBoost,
			constant.BuffPercentageAttackBoost,
			constant.BuffPercentageAttackSpeedIncrease,
			constant.BuffPercentageMovementSpeedIncrease,
			constant.BuffPercentageAttackBothDefenseIncrease:
			for key, value := range v.BuffStats.Attrs {
				m.stats.setStats(key, value)
			}
		case constant.BuffPercentageDefenseDecrease,
			constant.BuffPercentageAttackSpeedDecrease,
			constant.BuffPercentageMovementSpeedDecrease,
			constant.BuffPercentageAttackDecrease:
			for key, value := range v.BuffStats.Attrs {
				m.stats.setStats(key, value)
			}
		}
	}

	// buff
	for _, buff := range m.buffs {
		buffCfg, err := global.GetBuffCfg(buff.Id)
		if err != nil {
			continue
		}

		if buffCfg.EffectTime != 0 {
			continue
		}

		if time.Now().After(buff.OverTime) {
			continue
		}

		for i, e := range buff.BuffStats.Attrs {
			m.stats.setStats(i, e)
		}
	}

	// < 0  攻速移速 增益，其他属性减益 ; >0 攻速移速 减益，其他属性增益
	m.stats.changeStats(pb.Attribute_HEALTH, m.currentHP)

	m.stats.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(m.GetDC())*(float64(m.stats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	m.stats.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(m.GetMC())*(float64(m.stats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	m.stats.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(m.GetAR())*(float64(m.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	m.stats.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(m.GetMR())*(float64(m.stats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))

	m.stats.setStats(pb.Attribute_DODGE, int32(float64(m.stats.getStats(pb.Attribute_DODGE))*(float64(m.stats.getStats(pb.Attribute_DODGE_PERCENTAGE))/BaseRate)))
	m.stats.setStats(pb.Attribute_HIT, int32(float64(m.stats.getStats(pb.Attribute_HIT))*(float64(m.stats.getStats(pb.Attribute_HIT_PERCENTAGE))/BaseRate)))
	m.stats.setStats(pb.Attribute_CRITICAL_STRIKE, int32(float64(m.stats.getStats(pb.Attribute_CRITICAL_STRIKE))*(float64(m.stats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE))/BaseRate)))
	m.stats.setStats(pb.Attribute_TOUGHNESS, int32(float64(m.stats.getStats(pb.Attribute_TOUGHNESS))*(float64(m.stats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE))/BaseRate)))
	m.stats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(m.GetMaxHP())*(float64(m.stats.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate)))

	// 攻速移速增加  基础/(1+加成)；攻速移速降低  基础*(1+加成)
	m.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(float64(m.stats.getStats(pb.Attribute_ATTACK_SPEED))/(1+(float64(m.stats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE))/BaseRate))))
	m.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(float64(m.stats.getStats(pb.Attribute_MOVE_SPEED))/(1+(float64(m.stats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE))/BaseRate))))

	// 基础属性 >= 0
	m.stats.changeStats(pb.Attribute_PHYSICAL_ATTACK, int32(math.Max(0, float64(m.GetDC()))))
	m.stats.changeStats(pb.Attribute_MAGIC_ATTACK, int32(math.Max(0, float64(m.GetMC()))))
	m.stats.changeStats(pb.Attribute_PHYSICAL_DEFENSE, int32(math.Max(0, float64(m.GetAR()))))
	m.stats.changeStats(pb.Attribute_MAGIC_DEFENSE, int32(math.Max(0, float64(m.GetMR()))))

	m.stats.changeStats(pb.Attribute_DODGE, int32(math.Max(0, float64(m.stats.getStats(pb.Attribute_DODGE)))))
	m.stats.changeStats(pb.Attribute_HIT, int32(math.Max(0, float64(m.stats.getStats(pb.Attribute_HIT)))))
	m.stats.changeStats(pb.Attribute_CRITICAL_STRIKE, int32(math.Max(0, float64(m.stats.getStats(pb.Attribute_CRITICAL_STRIKE)))))
	m.stats.changeStats(pb.Attribute_TOUGHNESS, int32(math.Max(0, float64(m.stats.getStats(pb.Attribute_TOUGHNESS)))))
	m.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(math.Max(0, float64(m.stats.getStats(pb.Attribute_ATTACK_SPEED)))))
	m.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(math.Max(0, float64(m.stats.getStats(pb.Attribute_MOVE_SPEED)))))
	m.stats.changeStats(pb.Attribute_MAX_HEALTH, int32(math.Max(0, float64(m.GetMaxHP()))))
}

// 死亡
func (m *MonsterInfo) die(attacker IMapObject) {
	if m.desk == nil {
		logger.Errorf("异常 怪物死亡,房间nil , MonsterUId : %d , MonsterCfgID: %d ", m.uid, m.cfgId)
		return
	}
	desk := m.desk
	cfgId := m.cfgId

	var player *Player
	// 计算掉落
	if attacker != nil && m.cfg.MType != MTypeWorldBoss {
		var uid int64

		if attacker.ObjectType() == ObjectTypePlayer {
			uid = attacker.Uid()
		} else if attacker.ObjectType() == ObjectTypePartner && attacker.Owner() != nil {
			uid = attacker.Owner().Uid()
		}
		if player, err := m.desk.playerWithId(uid); err == nil {
			m.drop(player)
		}
	}

	packet := pb.ObjectDiePush{ObjectId: m.uid, ReviveTip: false}
	desk.Broadcast(protocol.RouteObjectDiePush, &packet)

	logger.Debugf("怪物死亡。MonsterUId:%d , MonsterCfgID:%d", m.uid, m.cfgId)

	// 怪物重生
	if desk.roomCfg.Roomtype == constant.PVEPeacefulMap || desk.roomCfg.Roomtype == constant.PVEWarMap ||
		desk.roomCfg.Roomtype == constant.StarUpStoneFBMap || desk.roomCfg.Roomtype == constant.SoulStoneFBMap {
		mdesk := desk
		time.AfterFunc(time.Millisecond*time.Duration(m.reliveTime), func() {
			if mdesk != nil && mdesk.status() == constant.DeskStatusPlaying {
				mdesk.reliveLock.Lock()
				defer mdesk.reliveLock.Unlock()
				if len(mdesk.chRelive) >= chLen {
					return
				}
				mdesk.chRelive <- &MonRelive{CfgID: cfgId, Location: global.PointI{X: m.bornLocation.X, Y: m.bornLocation.Y}}
			}
		})

		if player != nil {
			player.tasks.dispatch(pb.TargetType_KILL_MONSTER_REQUIREMENT, 0, 1)
			if m.cfg.MType == MTypeCommonBoss {
				player.tasks.dispatch(pb.TargetType_TOTAL_KILLS_OF_BOSS_MONSTERS, 0, 1)
			} else {
				player.tasks.dispatch(pb.TargetType_TOTAL_KILLS_OF_NORMAL_MONSTERS, 0, 1)
			}
		}
	}
	if desk.roomCfg.Roomtype == constant.GuildMap {
		for _, nr := range desk.GuildReward.NexReward {
			bFinde := false
			for _, cr := range desk.GuildReward.CurReward {
				if nr.K == cr.K {
					cr.V += nr.V
					bFinde = true
					break
				}
			}
			if !bFinde {
				desk.GuildReward.CurReward = append(desk.GuildReward.CurReward, nr)
			}
		}

		desk.GuildReward.NexReward = []*pb.KeyValue{}
		if bcfg := global.GetBattleMapCfg(desk.BattleId + 1); bcfg != nil {
			for _, v := range bcfg.Settlement {
				desk.GuildReward.NexReward = append(desk.GuildReward.NexReward, &pb.KeyValue{K: v.ID, V: v.Num})
			}
		}
		desk.Broadcast(protocol.RouteGuildCopyRewardPush, desk.GuildReward)
	}
	// 重置并归还
	desk.delMonster(m)
	m.reset()
	monsterMgr.GiveBackMonster(m)
}

// 重生
func Relive(desk *Desk, mr *MonRelive) {
	if desk == nil {
		return
	}

	m, err := monsterMgr.TakeMonster(mr.CfgID)
	if err == nil {
		m.bornLocation = mr.Location

		if cell := desk.mapInfo.getAvailableCell(&m.bornLocation, int32(m.cfg.ViewRange)); cell != nil {
			m.uid = snowflake.GenID()

			if m.cell = desk.mapInfo.addObject(m, &cell.location); m.cell != nil {
				m.setDesk(desk)
				desk.addMonster(m)
			}

			// desk.logger.Debugf("怪物重生  MonsterCfgID: %d  位置%v ", mr.CfgID, m.location)

			packet := pb.MonsterInPush{MonsterList: []*pb.MonsterInfo{}}

			packet.MonsterList = append(packet.MonsterList,
				&pb.MonsterInfo{
					CfgId:     m.cfgId,
					Uid:       m.uid,
					Name:      m.name,
					MaxHp:     m.GetMaxHP(),
					CurHp:     m.currentHP,
					Location:  &pb.Point{X: m.location.X, Y: m.location.Y},
					Direction: m.direction,
				})

			desk.Broadcast(protocol.RouteMonsterInPush, &packet)
		}
	}
}

// 掉落
func (m *MonsterInfo) drop(attacker *Player) {
	if attacker == nil {
		return
	}
	monCfg := global.GetMonsterCfg(m.cfgId)
	if monCfg == nil {
		logger.Debugf("获取怪物掉落配置失败 cfgId :%d", m.cfgId)
		return
	}

	packet := &pb.ItemDropPush{Items: []*pb.ItemInfo{}}
	packet.Location = &pb.Point{X: m.location.X, Y: m.location.Y}
	packet.ObjectId = m.uid
	reason := constant.ItemAddDrop

	// 根据房间类型设置掉落原因
	switch m.desk.roomCfg.Roomtype {
	case constant.PVEPeacefulMap:
		reason = constant.ItemAddDropHaiDao
	case constant.StarUpStoneFBMap:
		reason = constant.ItemAddDropStarUpStoneFB
	case constant.SoulStoneFBMap:
		reason = constant.ItemAddDropSoulStoneFBMap
	}

	// 是否需要处理掉落
	shouldProcessDrop := m.desk.roomCfg.Roomtype == constant.PVEWarMap ||
		m.desk.roomCfg.Roomtype == constant.WorldBossMap ||
		m.desk.roomCfg.Roomtype == constant.StarUpStoneFBMap ||
		m.desk.roomCfg.Roomtype == constant.SoulStoneFBMap ||
		m.desk.roomCfg.Roomtype == constant.GuidanceFBMap ||
		m.desk.roomCfg.Roomtype == constant.PVEPeacefulMap

	var multiplier int32 = 0

	if shouldProcessDrop {
		items := []global.IdNum{}

		for _, v := range monCfg.Drop {
			if len(v.ID) == 0 {
				continue
			}

			// 计算掉落倍率
			if m.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
				// 守卫海岛挂机倍率
				if guardIslandHangup, exist := global.GuardIslandCfg[attacker.AfkMultiplierID]; exist {
					// 当倍率为-1时，代表前端需要弹窗, 这个时候倍率设置为0
					if guardIslandHangup.Multiplier == -1 {
						multiplier = 0
					} else {
						multiplier = guardIslandHangup.Multiplier
					}
				} else {
					logger.Errorf("守卫海岛挂机倍率配置不存在, UserId:%d, MonsterCfgID: %d , MonsterUid: %d", attacker.Uid(), m.cfgId, m.uid)
				}
			}

			r := global.RandIn(int(BaseRate))
			ratio := float64(v.Ratio) * (1 + float64(attacker.stat(pb.Attribute_ITEM_DROP_RATE_PERCENTAGE)+multiplier)/BaseRate)

			if r < int(ratio) {
				if m.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
					logger.Debugf("守卫海岛怪物掉落, UserId:%d, MonsterCfgID: %d , MonsterUid: %d ,ratio: %v", attacker.Uid(), m.cfgId, m.uid, ratio)
				}

				index := global.RandIn(len(v.ID))
				if index >= len(v.ID) {
					index = 0
				}
				id := v.ID[index]
				num := v.Num.Min + int32(global.RandIn(int(v.Num.Max)-int(v.Num.Min)))

				if !itemRecoderMgr.check(attacker.uid, attacker.level, id, num, m.desk.roomCfg.Roomtype) {
					logger.Infof("掉落控制 drop UserId:%d, MonsterCfgID: %d ,  ItemID:%d ItemNum:%d", attacker.Uid(), m.cfgId, id, num)
					continue
				}

				items = append(items, global.IdNum{ID: id, Num: num})
				packet.Items = append(packet.Items, &pb.ItemInfo{CfgId: id, Num: int64(num)})
			}
		}

		// 处理物品添加或邮件发送
		if err := attacker.checkAddSourceBatch(items); err == nil {
			attacker.addSourceBatch(items, reason)
		} else {
			attachment := []protocol.Attachment{}
			for _, item := range items {
				if item.ID == constant.ItemTypeCoin {
					attacker.addSource(item.ID, int64(item.Num), reason)
				} else {
					attachment = append(attachment, protocol.Attachment{UserPropsId: int64(item.ID), Num: int64(item.Num)})
				}
			}
			attacker.SendMail(reason, attachment)
		}
	}

	// 经验处理
	if monCfg.Experience > 0 {
		num := int64(float64(monCfg.Experience) * (1 + float64(attacker.stat(pb.Attribute_EXP_INCREASE_PERCENTAGE)+multiplier)/BaseRate))
		attacker.addSource(constant.ItemTypeExp, num, reason)
		packet.Items = append(packet.Items, &pb.ItemInfo{CfgId: constant.ItemTypeExp, Num: int64(num)})
	}

	if len(packet.Items) > 0 {
		attacker.pushMsg(protocol.RouteItemDropPush, packet)
	}
}

// 掉落
// func (m *MonsterInfo) drop(attacker *Player) {
// 	if attacker == nil {
// 		return
// 	}
// 	monCfg := global.GetMonsterCfg(m.cfgId)
// 	if monCfg == nil {
// 		logger.Debugf("获取怪物掉落配置失败 cfgId :%d", m.cfgId)
// 		return
// 	}

// 	packet := &pb.ItemDropPush{Items: []*pb.ItemInfo{}}
// 	packet.Location = &pb.Point{X: m.location.X, Y: m.location.Y}
// 	packet.ObjectId = m.uid
// 	reason := constant.ItemAddDrop

// 	if m.desk.roomCfg.Roomtype == constant.PVEPeacefulMap || m.desk.roomCfg.Roomtype == constant.PVEWarMap ||
// 		m.desk.roomCfg.Roomtype == constant.WorldBossMap || m.desk.roomCfg.Roomtype == constant.StarUpStoneFBMap ||
// 		m.desk.roomCfg.Roomtype == constant.SoulStoneFBMap || m.desk.roomCfg.Roomtype == constant.GuidanceFBMap {

// 		items := []global.IdNum{}

// 		for _, v := range monCfg.Drop {
// 			if len(v.ID) == 0 {
// 				continue
// 			}
// 			r := global.RandIn(int(BaseRate))
// 			ratio := float64(v.Ratio) * (1 + float64(attacker.stat(pb.Attribute_ITEM_DROP_RATE_PERCENTAGE))/BaseRate)
// 			if r < int(ratio) {
// 				//logger.Infof("怪物掉落, UserId:%d, MonsterCfgID: %d , MonsterUid: %d ,Drop: %v", attacker.Uid(), m.cfgId, m.uid, v)
// 				index := global.RandIn(len(v.ID))
// 				if index >= len(v.ID) {
// 					index = 0
// 				}
// 				id := v.ID[index]
// 				num := v.Num.Min + int32(global.RandIn(int(v.Num.Max)-int(v.Num.Min)))

// 				if !itemRecoderMgr.check(attacker.uid, attacker.level, id, num, m.desk.roomCfg.Roomtype) {
// 					logger.Infof("掉落控制 drop UserId:%d, MonsterCfgID: %d ,  ItemID:%d ItemNum:%d", attacker.Uid(), m.cfgId, id, num)
// 					continue
// 				}

// 				items = append(items, global.IdNum{ID: id, Num: num})

// 				packet.Items = append(packet.Items, &pb.ItemInfo{CfgId: id, Num: int64(num)})
// 			}
// 		}

// 		if m.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
// 			reason = constant.ItemAddDropHaiDao
// 		} else if m.desk.roomCfg.Roomtype == constant.StarUpStoneFBMap {
// 			reason = constant.ItemAddDropStarUpStoneFB
// 		} else if m.desk.roomCfg.Roomtype == constant.SoulStoneFBMap {
// 			reason = constant.ItemAddDropSoulStoneFBMap
// 		}

// 		if err := attacker.checkAddSourceBatch(items); err == nil {
// 			attacker.addSourceBatch(items, reason)
// 		} else {
// 			attachment := []protocol.Attachment{}
// 			for _, item := range items {
// 				if item.ID == constant.ItemTypeCoin {
// 					attacker.addSource(item.ID, int64(item.Num), reason)
// 				} else {
// 					attachment = append(attachment, protocol.Attachment{UserPropsId: int64(item.ID), Num: int64(item.Num)})
// 				}
// 			}
// 			attacker.SendMail(reason, attachment)
// 		}
// 	}

// 	if monCfg.Experience > 0 {
// 		// 经验加成
// 		num := int64(float64(monCfg.Experience) * (1 + float64(attacker.stat(pb.Attribute_EXP_INCREASE_PERCENTAGE))/BaseRate))
// 		attacker.addSource(constant.ItemTypeExp, num, reason)
// 		packet.Items = append(packet.Items, &pb.ItemInfo{CfgId: constant.ItemTypeExp, Num: int64(num)})
// 	}

// 	if len(packet.Items) > 0 {
// 		attacker.pushMsg(protocol.RouteItemDropPush, packet)
// 	}
// }

// 掉落
func (m *MonsterInfo) worldboosDrop(attacker *Player) {
	if attacker == nil {
		return
	}
	monCfg := global.GetMonsterCfg(m.cfgId)
	if monCfg == nil {
		logger.Debugf("获取怪物掉落配置失败 cfgId :%d", m.cfgId)
		return
	}

	packet := &pb.ItemDropPush{Items: []*pb.ItemInfo{}}
	packet.Location = &pb.Point{X: m.location.X, Y: m.location.Y}
	packet.ObjectId = m.uid

	items := []global.IdNum{}

	for _, v := range monCfg.Drop {
		if len(v.ID) == 0 {
			continue
		}
		r := global.RandIn(int(BaseRate))
		ratio := float64(v.Ratio) * (1 + float64(attacker.stat(pb.Attribute_ITEM_DROP_RATE_PERCENTAGE))/BaseRate)
		if r < int(ratio) {
			logger.Infof("世界boss怪物掉落, UserId:%d, MonsterCfgID: %d , MonsterUid: %d ,ratio: %v", attacker.Uid(), m.cfgId, m.uid, ratio)
			index := global.RandIn(len(v.ID))
			if index >= len(v.ID) {
				index = 0
			}
			id := v.ID[index]
			num := v.Num.Min + int32(global.RandIn(int(v.Num.Max)-int(v.Num.Min)))

			if !itemRecoderMgr.check(attacker.uid, attacker.level, id, num, constant.WorldBossMap) {
				logger.Infof("掉落控制 worldboosDrop UserId:%d, MonsterCfgID: %d ,  ItemID:%d ItemNum:%d", attacker.Uid(), m.cfgId, id, num)
				continue
			}

			items = append(items, global.IdNum{ID: id, Num: num})

			packet.Items = append(packet.Items, &pb.ItemInfo{CfgId: id, Num: int64(num)})
		}
	}

	if err := attacker.checkAddSourceBatch(items); err == nil {
		attacker.addSourceBatch(items, constant.ItemAddDrop)
	} else {
		attachment := []protocol.Attachment{}
		for _, item := range items {
			if item.ID == constant.ItemTypeCoin {
				attacker.addSource(item.ID, int64(item.Num), constant.ItemAddDrop)
			} else {
				attachment = append(attachment, protocol.Attachment{UserPropsId: int64(item.ID), Num: int64(item.Num)})
			}
		}
		attacker.SendMail(constant.ItemAddDrop, attachment)
	}

	if monCfg.Experience > 0 {
		// 经验加成
		num := int64(float64(monCfg.Experience) * (1 + float64(attacker.stat(pb.Attribute_EXP_INCREASE_PERCENTAGE))/BaseRate))
		attacker.addSource(constant.ItemTypeExp, num, constant.ItemAddDrop)
		packet.Items = append(packet.Items, &pb.ItemInfo{CfgId: constant.ItemTypeExp, Num: int64(num)})
	}

	if len(packet.Items) > 0 {
		attacker.pushMsg(protocol.RouteItemDropPush, packet)
	}
}

// 重置
func (m *MonsterInfo) reset() {
	m.uid = 0
	m.currentHP = m.GetMaxHP()
	m.displayHP = m.GetMaxHP()
	m.location.X = 0
	m.location.Y = 0
	m.createTime = 0
	m.moveTime = time.Now()
	m.attackTime = time.Now()
	m.searchTime = time.Now()
	m.dead = false
	m.desk = nil
	m.target = nil
	m.roamPath = []*astar.APoint{}
	m.buffs = make(map[int32]*BuffInfo)
}

func (m *MonsterInfo) setDesk(d *Desk) {
	m.desk = d
}

// 巡逻
func (m *MonsterInfo) processRoam() {
	if time.Now().Before(m.roamTime) {
		return
	}

	if !m.CanMove() {
		return
	}
	if m.desk == nil {
		return
	}

	if m.target != nil {
		return
	}

	if m.cfg.ViewRange == 0 {
		return
	}

	if m.cfg.MType == MTypeGathering {
		return
	}

	if len(m.roamPath) == 0 {

		cell := m.desk.mapInfo.getAvailableCell(&m.bornLocation, int32(m.cfg.ViewRange))

		if cell != nil {
			if path := maputil.GetPath(m.location, &pb.Point{X: float64(cell.location.X), Y: float64(cell.location.Y)}, &m.desk.mapCfg.AstarMgr); len(path) > 1 {
				m.roamPath = path[1:]
			}
		}
	}
	if len(m.roamPath) > 0 {
		if m.moveTo(m.roamPath[0]) {
			if len(m.roamPath) >= 1 {
				m.roamPath = m.roamPath[1:]
			}
		}
	}

	m.roamTime = time.Now().Add(time.Second * time.Duration(global.RandIn(5)+1))
}

// 寻找
func (m *MonsterInfo) processSearch() {
	// 嘲讽状态不在攻击其他目标
	if m.getBuffByType(constant.BuffTaunt) != nil && m.target != nil {
		return
	}

	if time.Now().Before(m.searchTime) {
		return
	}
	m.searchTime = time.Now().Add(time.Second)

	if !m.CanAttack() {
		return
	}

	if m.cfg.AttackRange == 0 {
		return
	}

	var distance int
	var closest IMapObject
	// objs := m.desk.aoiMgr.GetPidsByPos(float32(m.location.X), float32(m.location.Y))
	// for _, v := range objs {
	// 	obj := m.desk.objectByUid(int64(v))
	// 	if obj.ObjectType() == ObjectTypeMonster {
	// 		continue
	// 	}
	// 	tmpDistance := maputil.Distance(m.location, obj.Location())
	// 	if tmpDistance > m.cfg.AttackRange {
	// 		continue
	// 	}
	// 	if closest == nil || tmpDistance < distance {
	// 		closest = obj
	// 	}
	// }
	for _, v := range m.desk.objects {
		if v.objectType == ObjectTypeDecoy || v.objectType == ObjectTypePartner {
			if v.dead {
				continue
			}

			tmpDistance := maputil.Distance(m.location, v.Location())
			if tmpDistance > m.cfg.AttackRange {
				continue
			}

			// 优先以战士职业位目标
			if closest != nil {
				if closest.IsWarrior() {
					if distance-tmpDistance < 3 {
						continue
					}
				} else {
					if v.IsWarrior() && distance-tmpDistance < 2 {
						distance = tmpDistance
						if closest.Job() == constant.PartnerWarrior {
							continue
						}
						closest = v
						continue
					}
				}
			}

			if distance == 0 || tmpDistance < distance {
				distance = tmpDistance
				closest = v
			}
		}
	}

	if closest == nil || !closest.IsWarrior() {
		for _, v := range m.desk.players {
			if v.dead {
				continue
			}
			tmpDistance := maputil.Distance(m.location, v.Location())
			if tmpDistance > m.cfg.AttackRange {
				continue
			}
			if m.desk.roomCfg.Roomtype == constant.PVEPeacefulMap || m.desk.roomCfg.Roomtype == constant.PVEWarMap {
				if m.desk.mapInfo.safeArea(v.Location()) {
					continue
				}
			}
			if distance == 0 || tmpDistance < distance || v.IsWarrior() {
				distance = tmpDistance
				closest = v
			}
		}
	}

	if closest != nil {
		m.target = closest
		m.roamPath = []*astar.APoint{}

		// logger.Debugf("M 锁定目标 mUid: %d , 自身坐标:%v, 目标ID: %d  目标坐标: %v  目标类型:%v", m.uid, m.location, m.target.Uid(), m.target.Location(), m.target.ObjectType())
	}
}

// 目标
func (m *MonsterInfo) processTarget() {
	if m.desk == nil {
		return
	}

	if m.target == nil {
		return
	}

	if m.target.Dead() {
		m.target = nil
		return
	}

	if !m.CanAttackTarget(m.target) {
		return
	}
	if m.desk.roomCfg.Roomtype == constant.PVEPeacefulMap || m.desk.roomCfg.Roomtype == constant.PVEWarMap {
		if m.desk.mapInfo.safeArea(m.target.Location()) {
			m.target = nil
			return
		}
	}

	if !m.inAttackRange(m.cfg.AttackDistance) {
		if !m.CanMove() {
			return
		}
		if maputil.Distance(m.location, &pb.Point{X: float64(m.bornLocation.X), Y: float64(m.bornLocation.Y)}) > int(m.cfg.Chase) && m.cfg.MType == MTypeNormal {
			m.target = nil
			cell := m.desk.mapInfo.getAvailableCell(&m.bornLocation, int32(m.cfg.ViewRange))

			if cell != nil {
				if path := maputil.GetPath(m.location, &pb.Point{X: float64(cell.location.X), Y: float64(cell.location.Y)}, &m.desk.mapCfg.AstarMgr); len(path) > 1 {
					m.roamPath = path[1:]
					// logger.Debugf("回到原点 起点=%v  终点=%v", m.location, cell.location)
				} else {
					logger.Debugf("回到原点寻路失败 起点=%v  终点=%v", m.location, m.target.Location())
				}
			}
		} else {
			if path := maputil.GetPath(m.location, m.target.Location(), &m.desk.mapCfg.AstarMgr); len(path) > 1 {
				m.moveTo(path[1])
			} else {
				logger.Debugf("寻路失败 起点=%v  终点=%v", m.location, m.target.Location())
			}
		}
	} else {
		if len(m.cell.objects) > 1 {
			cell := m.desk.mapInfo.getAvailableCell(global.ToPointI(m.target.Location()), int32(m.cfg.AttackDistance))
			if cell != nil {
				if path := maputil.GetPath(m.location, &pb.Point{X: float64(cell.location.X), Y: float64(cell.location.Y)}, &m.desk.mapCfg.AstarMgr); len(path) > 1 {
					m.moveTo(path[1])
					return
				}
			} else {
				m.attack()
			}
		}

		m.attack()
	}
}

// BUFF
func (m *MonsterInfo) processBuff_bak() {
	now := time.Now()
	var delbufIds []int32
	var bRefreshStats bool
	for i, v := range m.buffs {
		// buff到期
		if now.After(v.OverTime) && now.After(v.CDTime) && v.Mark != constant.BuffMarkPassive {
			delbufIds = append(delbufIds, i)
			continue
		}

		if now.After(v.OverTime) && v.Mark != constant.BuffMarkPassive {
			if len(v.BuffStats.Attrs) > 0 {
				v.BuffStats.clear()
				bRefreshStats = true
			}
			continue
		}

		if v.TickFrequency == 0 {
			continue
		}

		if !now.After(v.TickTime) {
			continue
		}

		var damage, recover int32

		switch constant.BuffType(v.Pretype) {

		case constant.BuffFixedHeal, constant.BuffPercentageHeal:
			for i, e := range v.BuffStats.Attrs {
				if i == pb.Attribute_HEALTH {
					recover += e
				}
				if i == pb.Attribute_HEALTH_PERCENTAGE {
					recover += int32(float64(m.GetMaxHP()) * float64(e) / BaseRate)
				}
			}

		case constant.BuffFixedDamage, constant.BuffPercentageDamage, constant.BuffFixedDamage2:
			for i, e := range v.BuffStats.Attrs {
				if i == pb.Attribute_HEALTH {
					damage += e
				}
				if i == pb.Attribute_HEALTH_PERCENTAGE {
					damage += int32(float64(m.GetMaxHP()) * float64(e) / BaseRate)
				}
			}
		}

		if damage > 0 {
			if m.changeHp(-damage, v.Owner) == 0 {
				return
			}
		}
		if recover > 0 {
			if m.changeHp(recover, v.Owner) == 0 {
				return
			}
		}
		m.buffs[i].TickTime = time.Now().Add(time.Duration(v.TickFrequency) * time.Millisecond)

		// logger.Debugf("触发定时buff  uid=%d , name=%s , buffid=%d , damage=%d, recover=%d", m.uid, m.name, v.Id, damage, recover)
	}

	if len(delbufIds) > 0 {
		for _, v := range delbufIds {
			m.delBuff(int32(v))
		}
		bRefreshStats = true
	}

	if bRefreshStats {
		m.RefreshStats()
	}
}

// 移动
func (m *MonsterInfo) moveTo(point *astar.APoint) bool {
	if m.desk == nil {
		return false
	}

	if !m.CanMove() {
		return false
	}

	if m.cfg.MType == MTypeGathering {
		return false
	}

	// from := m.location
	to := &pb.Point{X: float64(point.X), Y: float64(point.Y)}
	if !m.desk.mapInfo.locationChange(m, to) {
		return false
	}

	m.moveTime = time.Now().Add(time.Duration(m.getMoveSpeed()) * time.Millisecond)

	packet := &pb.MovePush{Uid: m.uid, Location: m.location, Direction: m.direction}
	// m.boardcastMsg(protocol.RouteMovePush, packet)

	m.desk.Broadcast(protocol.RouteMovePush, packet)

	// logger.Debugf("M 移动 mUid: %d  起点 %v, 目标位置: %v", m.uid, from, to)
	return true
}

// 攻击
func (m *MonsterInfo) attack() {
	if !m.CanAttack() {
		return
	}

	if len(m.cfg.Skills) == 0 {
		return
	}

	skillIndex := global.RandIn(len(m.skills))
	castSkillId := m.skills[skillIndex].CfgId

	if castSkillId == 0 {
		return
	}
	if tmpCfg, err := global.GetSkillCfg(int32(castSkillId)); err == nil {
		if tmpCfg.CoolDown > 0 && time.Now().Before(m.skills[skillIndex].Cooldown) {
			castSkillId = NormalSkillID
		}
	}

	skillCfg, err := global.GetSkillCfg(int32(castSkillId))
	if err != nil {
		logger.Errorf("M 攻击 获取技能配置失败 mUid: %d, skillId: %d", m.uid, castSkillId)
		return
	}

	attackCells := []*Cell{}
	targets := []IMapObject{}
	targets = append(targets, m.target)
	switch skillCfg.SkillID {
	case constant.WorldBossSkill1:
		attackCells = m.desk.mapInfo.getSquareCells(&m.cell.location, skillCfg.Radius)
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
				if m.CanAttackTarget(obj) {

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
		if t.Uid() == m.uid {
			continue
		}

		if t.ObjectType() == ObjectTypeBuff || t.ObjectType() == ObjectTypeMonster {
			continue
		}

		// 安全区内不能攻击玩家
		if m.desk.roomCfg.Roomtype == constant.PVEPeacefulMap || m.desk.roomCfg.Roomtype == constant.PVEWarMap {
			if t.ObjectType() != ObjectTypeMonster && (m.desk.mapInfo.safeArea(m.location) || m.desk.mapInfo.safeArea(t.Location())) {
				continue
			}
		}

		// 技能距离
		if skillCfg.SType != constant.SkillTypeNormal && skillCfg.MaxDistance > 0 && maputil.Distance(m.location, t.Location()) > int(skillCfg.MaxDistance) {
			continue
		}

		// 伤害延迟生效
		mdesk := m.desk
		mt := t
		time.AfterFunc(time.Duration(skillCfg.DelayTime)*time.Millisecond, func() {
			if mdesk != nil && mdesk.status() == constant.DeskStatusPlaying {
				mdesk.delayAttackLock.Lock()
				defer mdesk.delayAttackLock.Unlock()

				if len(mdesk.chDelayAttack) >= chLen {
					logger.Warningln("M chDelayAttack 通道已满")
					return
				}
				mdesk.chDelayAttack <- &DelayAction{
					ObjType:   ObjectTypeMonster,
					ObjUid:    m.uid,
					SkillId:   int32(castSkillId),
					Target:    mt,
					Direction: m.direction,
					Location:  global.PointI{X: m.cell.location.X, Y: m.cell.location.Y},
				}
			}
		})
		// logger.Debugf("----------------M 攻击 attack mUid: %d , 技能 %d 目标: %d, 坐标 %v  目标坐标 %v 目标类型 %v ", m.uid, castSkillId, t.Uid(), m.location, t.Location(), t.ObjectType())
	}

	m.direction = maputil.DirectionFromPointF(m.location, m.target.Location())

	packet := &pb.AttackPush{
		AttackerId: m.uid,
		TargetUids: []int64{m.target.Uid()},
		SkillId:    castSkillId,
		Location:   m.target.Location(),
		Direction:  m.direction,
	}
	m.desk.Broadcast(protocol.RouteAttackPush, packet)

	// m.attackTime = time.Now().Add(time.Duration(m.getAttackSpeed()) * time.Millisecond)
	m.setAttackTime(global.GetActionsetCfg(skillCfg.SkillID))

	for _, tri := range skillCfg.TriggerBuff {
		bufCfg, err := global.GetBuffCfg(tri)
		if err != nil {
			continue
		}
		if bufCfg.Type == constant.BuffTypeDebuff {
			continue
		}
		if global.RandIn(int(BaseRate)) >= int(skillCfg.Skilltrigger) {
			continue
		}

		var bufStats *Stats
		var addSec int32
		if bufCfg.Mark == constant.BuffTypeChainSkill {
			bufStats = &Stats{Attrs: make(map[pb.Attribute]int32)}
			for _, v := range skillCfg.Effect {
				bufStats.setStats(pb.Attribute(v.Key), v.Value)
			}

			addSec += skillCfg.UpperlevelSetting.DurationCon
		}
		m.AddBuff(bufCfg.ID, m, 0, nil, 0, castSkillId)
	}

	// 技能CD
	if castSkillId != NormalSkillID && skillCfg.CoolDown > 0 {
		m.skills[skillIndex].Cooldown = time.Now().Add(time.Duration(skillCfg.CoolDown) * time.Millisecond)
	}
}

func (m *MonsterInfo) delayAttack(target IMapObject, skillId int32) {
	if !m.CanAttackTarget(target) {
		return
	}

	skillCfg, err := global.GetSkillCfg(skillId)
	if err != nil {
		return
	}
	hit := m.stats.getStats(pb.Attribute_HIT)
	dodge := target.GetStats().getStats(pb.Attribute_DODGE)
	if hit < dodge && global.RandIn(int(dodge)) > int(hit) {
		target.Dodged()
		return
	}

	var power int32

	basePower := math.Max(float64(m.GetDC()), float64(m.GetMC()))
	power += int32(basePower)
	for _, v := range skillCfg.Effect {
		switch pb.Attribute(v.Key) {
		case pb.Attribute_PHYSICAL_ATTACK, pb.Attribute_MAGIC_ATTACK:
			power += v.Value
		case pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE, pb.Attribute_MAGIC_ATTACK_PERCENTAGE:
			power += int32(float64(basePower) * (1 + float64(v.Value)/BaseRate))
		}
	}
	power -= target.GetStats().getStats(maputil.DefenseAttributes(m.AttackAttributes()))

	if power <= 0 {
		power = int32(rand.Intn(2)) + 1
	}
	if power <= 0 {
		target.Dodged()
		return
	}

	target.Attacked(m, skillId, 0, power, true)

	// logger.Debugf("M 攻击 delayAttack mUid: %d ,  技能ID: %d  伤害:%d  目标: %d, 坐标 %v  目标坐标 %v", m.uid, skillId, power, m.target.Uid(), m.location, m.target.Location())
}

func (m *MonsterInfo) processAI() {
	m.processRoam()
	m.processSearch()
	m.processTarget()
	m.processHp()
	m.processBuff()
}

///////////////////////////////////////////////////////////////////////////////////

type MonsterMgr struct {
	sync.Mutex
	increment int64

	monsterPool map[int32][]*MonsterInfo // 怪物回收池 key:怪物配置ID
}

var monsterMgr *MonsterMgr = NewMonsterMgr()

func NewMonsterMgr() *MonsterMgr {
	return &MonsterMgr{increment: 1, monsterPool: map[int32][]*MonsterInfo{}}
}

// 产怪
func (mgr *MonsterMgr) CreateMonsterByLocation(cfgId int32, l *global.PointI) *MonsterInfo {
	mgr.increment++
	cfg := global.GetMonsterCfg(cfgId)
	if cfg == nil {
		return nil
	}

	m := newMonster(cfgId)

	// rx := rand.Intn(cfg.ViewRange)
	// ry := rand.Intn(cfg.ViewRange)
	// m.location = &pb.Point{X: float64(l.X) + float64(rx), Y: float64(l.Y) + float64(ry)}

	m.bornLocation = *l
	return m
}

func (mgr *MonsterMgr) CreateMonster(cfgId int32) *MonsterInfo {
	mgr.increment++

	m := newMonster(cfgId)
	return m
}

// 批量产怪
func (mgr *MonsterMgr) CreateMonsterBatch(cfgId, num int32, l *global.PointI) []*MonsterInfo {
	mgr.Lock()
	defer mgr.Unlock()

	monlist := []*MonsterInfo{}
	for i := 0; i < int(num); i++ {
		m := mgr.CreateMonsterByLocation(cfgId, l)

		monlist = append(monlist, m)
	}

	return monlist
}

// 归还到怪物池
func (mgr *MonsterMgr) GiveBackMonster(mon *MonsterInfo) {
	mgr.Lock()
	defer mgr.Unlock()

	_, ok := mgr.monsterPool[mon.cfgId]
	if ok {
		// 每个类型的怪最多缓存1000只
		if len(mgr.monsterPool[mon.cfgId]) >= 1000 {
			logger.Debugf("归还到怪物池失败  Id : %d , 此类怪物剩余数量：%d", mon.cfgId, len(mgr.monsterPool[mon.cfgId]))
			return
		}
		mgr.monsterPool[mon.cfgId] = append(mgr.monsterPool[mon.cfgId], mon)
	} else {
		mgr.monsterPool[mon.cfgId] = []*MonsterInfo{}
		mgr.monsterPool[mon.cfgId] = append(mgr.monsterPool[mon.cfgId], mon)
	}

	// logger.Debugf("归还怪物池成功  CfgId : %d , 此类怪物剩余数量：%d", mon.cfgId, len(mgr.monsterPool[mon.cfgId]))
}

// 获取空闲怪物
func (mgr *MonsterMgr) TakeMonster(cfgId int32) (*MonsterInfo, error) {
	mgr.Lock()
	defer mgr.Unlock()

	var newMon *MonsterInfo
	_, ok := mgr.monsterPool[cfgId]
	if ok {
		if len(mgr.monsterPool[cfgId]) > 0 {
			newMon = mgr.monsterPool[cfgId][0]
			mgr.monsterPool[cfgId] = mgr.monsterPool[cfgId][1:]

			// logger.Debugf("取出怪物池成功  CfgId : %d , 此类怪物剩余数量：%d", cfgId, len(mgr.monsterPool[cfgId]))
		}
	} else {
		newMon = mgr.CreateMonster(cfgId)
	}
	if newMon != nil {
		return newMon, nil
	} else {
		return nil, errutil.ErrCreateMonter
	}
}

func (mgr *MonsterMgr) MonsterDorp(cfgId int32, num int32) []global.IdNum {
	items := []global.IdNum{}

	monCfg := global.GetMonsterCfg(cfgId)
	if monCfg == nil {
		logger.Debugf("获取怪物掉落配置失败 cfgId :%d", cfgId)
		return items
	}
	for i := int32(0); i < num; i++ {
		for _, v := range monCfg.Drop {
			if len(v.ID) == 0 {
				continue
			}
			r := global.RandIn(int(BaseRate))
			ratio := float64(v.Ratio)
			if r < int(ratio) {

				index := global.RandIn(len(v.ID))
				if index >= len(v.ID) {
					index = 0
				}
				id := v.ID[index]
				num := v.Num.Min + int32(global.RandIn(int(v.Num.Max)-int(v.Num.Min)))

				items = append(items, global.IdNum{ID: id, Num: num})
			}
		}
	}
	return items
}
