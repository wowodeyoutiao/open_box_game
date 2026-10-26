package game

import (
	"fmt"
	"time"

	"nanoserver/global"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/maputil"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"

	"github.com/lonng/nano/component"
	"github.com/lonng/nano/session"
)

type WorldBoss struct {
	*MonsterInfo
	bloodNum       int32 // 血条数量
	oneBlood       int32 // 一条血量
	dead           bool
	nextSyncHpTime time.Time
}

func (wb *WorldBoss) changeHp(hp int32, attacker IMapObject) {
	var player *Player
	if attacker.ObjectType() == ObjectTypePlayer {
		player, _ = defaultManager.player(attacker.Uid())
	} else if attacker.ObjectType() == ObjectTypePartner && attacker.Owner() != nil {
		player, _ = defaultManager.player(attacker.Uid())
	}

	if player == nil {
		return
	}
	player.worldBossDamage(int64(maputil.ABS(hp)))

	wb.stats.setStats(pb.Attribute_HEALTH, hp)
	if wb.stat(pb.Attribute_HEALTH) <= 0 {
		// 发放单条血量奖励
		rewardNum := maputil.ABS(hp) / (wb.stat(pb.Attribute_MAX_HEALTH) / wb.bloodNum)
		// if attacker != nil && attacker.ObjectType() == ObjectTypePlayer {
		// 	player := attacker.(*Player)
		// 	for i := 0; i <= int(rewardNum); i++ {
		// 		wb.worldboosDrop(player)
		// 	}
		// }
		for i := 0; i <= int(rewardNum); i++ {
			wb.worldboosDrop(player)
		}
		wb.die()
		return
	}

	wb.oneBlood += hp
	if wb.oneBlood <= 0 {
		rewardNum := maputil.ABS(hp) / (wb.stat(pb.Attribute_MAX_HEALTH) / wb.bloodNum)
		// if attacker != nil && attacker.ObjectType() == ObjectTypePlayer {
		// 	player := attacker.(*Player)
		// 	for i := 0; i <= int(rewardNum); i++ {
		// 		wb.worldboosDrop(player)
		// 	}
		// }

		for i := 0; i <= int(rewardNum); i++ {
			wb.worldboosDrop(player)
		}

		wb.oneBlood = wb.stat(pb.Attribute_MAX_HEALTH) / wb.bloodNum

		logger.Debugf(" worldboss 单条血量奖励")
	}

	if time.Now().After(wb.nextSyncHpTime) {
		// 立即同步boss血量
		if desks := roomManager.getRoomByType(constant.WorldBossMap, 1); desks != nil {
			for _, v := range desks.desks {
				if len(v.players) > 0 {
					if len(v.chHpChanged) < chLen {
						v.chHpChanged <- wb.stat(pb.Attribute_HEALTH)
					}
				}
			}
		}
		wb.nextSyncHpTime = time.Now().Add(time.Second)
	}
}

func (wb *WorldBoss) die() {
	wb.dead = true
	wb.stats.changeStats(pb.Attribute_HEALTH, 0)
	wb.currentHP = 0
	wb.displayHP = 0

	logger.Debugf("boss 死亡... ServerID: %d", wb.serverID)
	if desks := roomManager.getRoomByType(constant.WorldBossMap, 1); desks != nil {
		for _, v := range desks.desks {
			v.worldBossGameOver(constant.GameOverTypeMDead)
		}
	}
}

type WorldBossManager struct {
	component.Base
	boos               map[int32]*WorldBoss
	starttime, endtime time.Time
	Open               bool
	serverDamage       map[int32]int64 // 全服伤害
}

var worldBossMgr = NewWorldBossManager()

func NewWorldBossManager() *WorldBossManager {
	mgr := &WorldBossManager{boos: map[int32]*WorldBoss{}, serverDamage: map[int32]int64{}}

	return mgr
}

func (wbmgr *WorldBossManager) AfterInit() {
	var err error
	if wbmgr.starttime, err = time.Parse(time.TimeOnly, global.GlobalCfg.WorldBoss.Set.StartTime); err != nil {
		logger.Errorf("世界BOSS每日开启时间配置错误 %s", err.Error())
	}

	if wbmgr.endtime, err = time.Parse(time.TimeOnly, global.GlobalCfg.WorldBoss.Set.EndTime); err != nil {
		logger.Errorf("世界BOSS每日结束时间配置错误 %s", err.Error())
	}
	for i := global.ServerIDRangeStart; i <= global.ServerIDRangeEnd; i++ {
		for _, v := range roomManager.rooms {
			if v.opt.Roomtype == constant.WorldBossMap {
				if mapinfo := global.GetMapCfg(v.opt.MapId); mapinfo != nil {
					for _, m := range mapinfo.MonsterIds {
						wb := &WorldBoss{}
						wb.bloodNum = int32(global.GlobalCfg.WorldBoss.Blood.Num)

						wb.MonsterInfo = monsterMgr.CreateMonsterByLocation(m.ID, &m.Point)
						wb.oneBlood = wb.stat(pb.Attribute_MAX_HEALTH) / wb.bloodNum
						wb.nextSyncHpTime = time.Now().Add(time.Second)
						wb.serverID = i
						wb.name = wb.name + fmt.Sprintf("-%d", i)

						wbmgr.boos[i] = wb

						logger.Debugf("世界BOSS 初始化完成。CfgID:%d , MaxHP: %d , OneBlood:%d , BloodNum:%d  ServerId:%d",
							wb.cfgId, wb.stat(pb.Attribute_HEALTH), wb.oneBlood, wb.bloodNum, wb.serverID)
					}
				}
			}
		}
	}
}

func (wbmgr *WorldBossManager) reset() {
	for _, v := range wbmgr.boos {
		v.dead = false

		v.stats.changeStats(pb.Attribute_HEALTH, v.stat(pb.Attribute_MAX_HEALTH))
		v.bloodNum = int32(global.GlobalCfg.WorldBoss.Blood.Num)
		v.oneBlood = v.stat(pb.Attribute_MAX_HEALTH) / v.bloodNum
		v.nextSyncHpTime = time.Now().Add(time.Second)

		logger.Debugf("世界BOSS重置完成。CfgID:%d , MaxHP: %d , OneBlood:%d , BloodNum:%d ",
			v.cfgId, v.stat(pb.Attribute_HEALTH), v.oneBlood, v.bloodNum)
	}
}

func (wbmgr *WorldBossManager) checkWorldBossTime() {
	t := time.Now()
	// //需要周几开启
	// day, err := strconv.Atoi(global.GlobalCfg.WorldBoss.Set.Weekset)
	// if err != nil {
	// 	return
	// }
	// if t.Weekday() != time.Weekday(day) {
	// 	return
	// }
	startTime := time.Date(t.Year(), t.Month(), t.Day(), wbmgr.starttime.Hour(), wbmgr.starttime.Minute(), 0, 0, t.Location())
	endTime := time.Date(t.Year(), t.Month(), t.Day(), wbmgr.endtime.Hour(), wbmgr.endtime.Minute(), 0, 0, t.Location())
	if t.After(startTime) && t.Before(endTime) {
		if !wbmgr.Open {
			wbmgr.Open = true
			// 跑马灯
			context := []string{}
			defaultManager.Lamp(int32(constant.LampWorldBossChallenge), 0, context)
		}
	} else {
		wbmgr.Open = false
	}
}

func (wbmgr *WorldBossManager) checkWorldBossOpen() bool {
	return wbmgr.Open
}

func (wbmgr *WorldBossManager) checkWorldBossDead(serverId int32) bool {
	if b, ok := wbmgr.boos[serverId]; ok {
		return b.dead
	}
	return true
}

func (wbmgr *WorldBossManager) getBoss(cfgId int32) *WorldBoss {
	if b, ok := wbmgr.boos[cfgId]; ok {
		return b
	}
	return nil
}

func (wbmgr *WorldBossManager) getBossCurHp(serverId int32) int32 {
	if b, ok := wbmgr.boos[serverId]; ok {
		logger.Debugf(" 世界BOSS剩余血量 %d ServerID:%d  Name:%s", b.stat(pb.Attribute_HEALTH), b.serverID, b.name)
		return b.stat(pb.Attribute_HEALTH)
	}
	return 0
}

// 查询世界boss信息
func (wbmgr *WorldBossManager) CheckBoss(s *session.Session, req *pb.CheckBossRequest) error {
	p, err := playerWithSession(s)
	if err != nil {
		return err
	}
	packet := &pb.CheckBossResponse{}
	if boss := wbmgr.getBoss(p.serverID); boss != nil {
		packet.Hp = boss.stat(pb.Attribute_HEALTH)
		packet.Challengetime = p.dailyWorldBossTimes
		return p.pushMsg(protocol.RouteCheckBoss, packet)
	}
	return nil
}
