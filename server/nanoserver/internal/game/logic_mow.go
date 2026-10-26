package game

import (
	"nanoserver/global"
	"nanoserver/pkg/aoi9"
	"nanoserver/pkg/constant"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"
	"time"
)

const chLen int = 100

// World interface
type MowWorld struct {
	gameType protocol.GameType
}

func NewMowWorld() *MowWorld {
	return &MowWorld{gameType: protocol.GameTypeMow}
}

func (w *MowWorld) NewDesk(roomNo int64, roomID int32) *Desk {
	d := NewBaseDesk(roomNo, roomID)
	d.gameLogic = &Mow{desk: d}

	roomCfg := global.GetRoomCfg(roomID)
	if roomCfg == nil {
		logger.Errorf("创建房间失败,roomcfg 不存在 RoomId: %d", roomID)
		return nil
	}
	mapCfg := global.GetMapCfg(roomCfg.MapId)
	if mapCfg == nil {
		logger.Errorf("创建房间失败,mapCfg 不存在 MapId: %d", roomCfg.MapId)
		return nil
	}

	d.astarMgr = &mapCfg.AstarMgr

	gh := mapCfg.High / 10
	gw := mapCfg.Wide / 10
	d.aoiMgr = aoi9.NewAOIManager(0, mapCfg.Wide, gh, 0, mapCfg.High, gw)
	d.mapId = mapCfg.ID
	d.mapCfg = mapCfg
	d.roomCfg = roomCfg
	d.mapInfo = InitMapInfo(mapCfg)

	d.gameLogic.OnCreate()

	return d
}

func (w *MowWorld) GetName() string {
	return "Mow"
}

// ****************************************************************************************************

type Mow struct {
	desk *Desk

	additionList map[int32]interface{} //房间自定义参数
}

// IGameLogic interface Begin
func (mow *Mow) OnCreate() {
	mow.desk.chMove = make(map[int64]chan *pb.Point, chLen)
	mow.desk.chAttack = make(map[int64]chan *pb.AttackRequest, chLen)
	mow.desk.chMining = make(map[int64]chan int64, chLen)
	mow.desk.chDelayAttack = make(chan *DelayAction, chLen)
	mow.desk.chRelive = make(chan *MonRelive, chLen)
	mow.desk.chHpChanged = make(chan int32, chLen)
}

func (mow *Mow) OnEnter(p *Player) {
	mow.desk.chMove[p.Uid()] = make(chan *pb.Point, chLen)
	mow.desk.chAttack[p.Uid()] = make(chan *pb.AttackRequest, chLen)
	mow.desk.chMining[p.Uid()] = make(chan int64, chLen)
	mow.desk.serverID = p.serverID
	p.setPlayerStatus(PlayerStatusPlaying)
	p.stats.changeStats(pb.Attribute_HEALTH, p.GetMaxHP())
	p.currentHP = p.GetMaxHP()
	p.displayHP = p.currentHP
	p.reviveTimes = 0
	if p.isRobot() {
		robotMgr.onEnterDesk(p.serverID, p.Uid())
	}
}

func (mow *Mow) OnExit(p *Player) {
	mow.desk.moveLock.Lock()
	defer mow.desk.moveLock.Unlock()
	delete(mow.desk.chMove, p.uid)

	mow.desk.attackLock.Lock()
	defer mow.desk.attackLock.Unlock()
	delete(mow.desk.chAttack, p.uid)

	mow.desk.miningLock.Lock()
	defer mow.desk.miningLock.Unlock()
	delete(mow.desk.chMining, p.uid)

	if p.isRobot() {
		robotMgr.onExitDesk(p.serverID, p.Uid())
	} else {
		p.setPlayerStatus(PlayerStatusIdle)
		p.asyncSaveDb()
	}
	p.stats.setStats(pb.Attribute_HEALTH, p.GetMaxHP())
	p.currentHP = p.GetMaxHP()
	p.displayHP = p.currentHP
	//删除地图数据
	mow.desk.mapInfo.removeObject(p, p.GetCell().location)

	for _, v := range p.partnerSlots {
		if v == nil {
			continue
		}
		if v.PartnerUid == 0 {
			continue
		}
		//删除地图数据
		mow.desk.delObject(v.PartnerUid)

		if par := p.getPartnerInfoBySlotId(v.SlotId); par != nil {
			par.stats.setStats(pb.Attribute_HEALTH, par.GetMaxHP())
			par.currentHP = par.GetMaxHP()
			par.displayHP = par.currentHP
			par.target = nil
			par.desk = nil
		}
	}

	//房间内真实玩家全部退出，房间线退出. 行会副本只有等到时间结束才结束
	if mow.desk.realPlayerCount() == 0 {
		if mow.desk.roomCfg.Roomtype != constant.GuildMap {
			mow.desk.setStatus(constant.DeskStatusIdle)
		}
	}
}

func (mow *Mow) OnStart() {

	roomCfg := global.GetRoomCfg(mow.desk.cfgID)
	if roomCfg == nil {
		return
	}

	mow.desk.chDelayAttack = make(chan *DelayAction, chLen)
	mow.desk.chRelive = make(chan *MonRelive, chLen)
	mow.desk.chHpChanged = make(chan int32, chLen)
	mow.desk.GuildRewardMember = make(map[int64]int64, 0)

	switch mow.desk.roomCfg.Roomtype {
	case constant.SoloTowerMap:
		mow.desk.logger.Debugln("玩家进入无尽试炼，开始刷怪 ")
		if stcfg := global.GetEndlesstrialsCfg(mow.desk.SoloTower.Layers); stcfg != nil {
			for _, v := range stcfg.Monster {
				monlist := monsterMgr.CreateMonsterBatch(v.ID, v.Num, &v.Point)
				//mow.desk.logger.Debugf("爬塔地【%d】层 ，开始刷怪 %+v ", mow.desk.SoloTowerCfg.Layers, v)
				for _, mon := range monlist {
					if cell := mow.desk.mapInfo.getAvailableCell(&mon.bornLocation, int32(mon.cfg.ViewRange)); cell != nil {
						if mon.cell = mow.desk.mapInfo.addObject(mon, &cell.location); mon.cell != nil {
							mon.setDesk(mow.desk)
							mow.desk.addMonster(mon)
						} else {
							mow.desk.logger.Debugf("爬塔地【%d】层 ，刷怪失败 ,未找到出生点 ", mow.desk.SoloTowerCfg.Layers)
						}
					}
				}
			}
		}
		mow.desk.logger.Debugln("无尽试炼刷怪结束")

	case constant.PVEPeacefulMap, constant.PVEWarMap, constant.GuidanceFBMap:
		mow.desk.logger.Debugln("第一个玩家进入PVE房间,开始刷怪 ")
		for _, v := range mow.desk.mapCfg.MonsterIds {
			monlist := monsterMgr.CreateMonsterBatch(v.ID, v.Num, &v.Point)

			for _, mon := range monlist {
				if cell := mow.desk.mapInfo.getAvailableCell(&mon.bornLocation, int32(mon.cfg.ViewRange)); cell != nil {
					if mon.cell = mow.desk.mapInfo.addObject(mon, &cell.location); mon.cell != nil {
						mon.setDesk(mow.desk)
						mow.desk.addMonster(mon)
					}
				}
			}
		}
		mow.desk.logger.Debugln("PVE刷怪结束")

	case constant.WorldBossMap:
		for _, v := range mow.desk.mapCfg.MonsterIds {
			if boss := monsterMgr.CreateMonsterByLocation(v.ID, &v.Point); boss != nil {
				boss.stats.changeStats(pb.Attribute_HEALTH, worldBossMgr.getBossCurHp(mow.desk.serverID))
				boss.currentHP = boss.stat(pb.Attribute_HEALTH)
				boss.displayHP = boss.currentHP
				boss.serverID = mow.desk.serverID
				if cell := mow.desk.mapInfo.getAvailableCell(&v.Point, int32(boss.cfg.ViewRange)); cell != nil {
					if boss.cell = mow.desk.mapInfo.addObject(boss, &cell.location); boss.cell != nil {
						boss.SettDirection(pb.Direction_DOWN)
						boss.setDesk(mow.desk)
						mow.desk.addMonster(boss)
						for i, v := range boss.skills {
							if scfg, err := global.GetSkillCfg(v.CfgId); err == nil {
								if scfg.SkillID == constant.SkillTypeNormalAttackMonster {
									continue
								}
								boss.skills[i].Cooldown = time.Now().Add(time.Duration(scfg.CoolDown) * time.Millisecond)
							}
						}
						mow.desk.nextBuffTime = time.Now().Add(time.Duration(global.GlobalCfg.WorldBoss.Accommodation.Num) * time.Second)
					}
				}
			}
		}
		mow.desk.logger.Debugln("世界boss刷怪结束")

	case constant.ExperienceFBMap, constant.GoldFBMap, constant.EnhanceStoneFBMap, constant.BookPageFBMap, constant.StarUpStoneFBMap, constant.SoulStoneFBMap:
		mow.desk.DDailyMap.mingCount = 0
		for _, v := range mow.desk.mapCfg.MonsterIds {
			monlist := monsterMgr.CreateMonsterBatch(v.ID, v.Num, &v.Point)

			for _, mon := range monlist {
				if cell := mow.desk.mapInfo.getAvailableCell(&mon.bornLocation, int32(mon.cfg.ViewRange)); cell != nil {
					if mon.cell = mow.desk.mapInfo.addObject(mon, &cell.location); mon.cell != nil {
						mon.setDesk(mow.desk)
						mow.desk.addMonster(mon)
					}
				}
			}
		}
		mow.desk.logger.Debugln("每日必做副本刷怪结束")

	case constant.BattleMap, constant.BossMap, constant.TrialTowerMap, constant.RelicMap:
		mow.desk.logger.Debugln("玩家进入副本，开始刷怪 ")
		if btcfg := global.GetBattleMapCfg(mow.desk.BattleId); btcfg != nil {
			for _, v := range btcfg.Monster {
				monlist := monsterMgr.CreateMonsterBatch(v.ID, v.Num, &v.Point)
				//mow.desk.logger.Debugf("爬塔地【%d】层 ，开始刷怪 %+v ", mow.desk.SoloTowerCfg.Layers, v)
				for _, mon := range monlist {
					if cell := mow.desk.mapInfo.getAvailableCell(&mon.bornLocation, int32(mon.cfg.ViewRange)); cell != nil {
						if mon.cell = mow.desk.mapInfo.addObject(mon, &cell.location); mon.cell != nil {
							mon.setDesk(mow.desk)
							mow.desk.addMonster(mon)
						} else {
							mow.desk.logger.Debugf("战役副本【%d】 ，刷怪失败 ,未找到出生点 ", mow.desk.SoloTowerCfg.Layers)
						}
					}
				}
			}
		}
	case constant.GuildMap:
		mow.desk.logger.Debugln("玩家进入行会副本，开始刷怪 ")
		beginId := constant.GuildMap*100000 + 1
		mow.desk.BattleId = beginId
		if btcfg := global.GetBattleMapCfg(beginId); btcfg != nil {
			for _, v := range btcfg.Monster {
				monlist := monsterMgr.CreateMonsterBatch(v.ID, v.Num, &v.Point)
				for _, mon := range monlist {
					if cell := mow.desk.mapInfo.getAvailableCell(&mon.bornLocation, int32(mon.cfg.ViewRange)); cell != nil {
						if mon.cell = mow.desk.mapInfo.addObject(mon, &cell.location); mon.cell != nil {
							mon.setDesk(mow.desk)
							mow.desk.addMonster(mon)
						} else {
							mow.desk.logger.Debugf("行会副本【%d】 ，刷怪失败 ,未找到出生点 ", mow.desk.SoloTowerCfg.Layers)
						}
					}
				}
			}

			for _, v := range btcfg.Settlement {
				mow.desk.GuildReward.NexReward = append(mow.desk.GuildReward.NexReward, &pb.KeyValue{K: v.ID, V: v.Num})
			}
		}
		mow.desk.logger.Debugln("行会副本刷怪结束")

	case constant.FightEndMap:
		if btcfg := global.GetBattleMapCfg(mow.desk.BattleId); btcfg != nil {
			for _, v := range btcfg.Monster {
				if robot := robotMgr.getFreeRobotByCfgId(v.ID); robot != nil {
					robot.refreshStats(WithRefreshStatsPushMessage(false))
					robot.dead = false
					robot.currentHP = robot.GetMaxHP()
					robot.displayHP = robot.currentHP

					logger.Debugf("一战到底对手 %d:%s Uid: %d  ", robot.cfg.UUID, robot.cfg.Name, robot.uid)
					robot.target = nil
					for _, v := range robot.partners {
						v.target = nil
					}
					mow.desk.playerJoin(robot.session, false, mow.desk.BattleId)
				}
			}
		}
		mow.desk.logger.Debugln("一战到底副本刷怪结束")
	}

}

func (mow *Mow) OnGameOver() {
	for k := range mow.desk.chMove {
		delete(mow.desk.chMove, k)
	}
	for k := range mow.desk.chAttack {
		delete(mow.desk.chAttack, k)
	}
	for k := range mow.desk.chMining {
		delete(mow.desk.chMining, k)
	}
	mow.desk.mapInfo.clear()
}

// IGameLogic Interface End

func (mow *Mow) GetAddition(name int32) interface{} {
	unit, ok := mow.additionList[name]
	if ok {
		return unit
	}
	return nil
}

func (mow *Mow) SetAddition(name int32, v interface{}) {
	mow.additionList[name] = v
}
