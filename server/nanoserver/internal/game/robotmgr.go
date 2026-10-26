package game

import (
	"sort"
	"sync"
	"time"

	"nanoserver/global"
	"nanoserver/internal/web/provide"
	"nanoserver/pkg/astar"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/maputil"
	"nanoserver/pkg/snowflake"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"

	"github.com/lonng/nano/session"
	log "github.com/sirupsen/logrus"
)

type RobotStatus int16

const (
	StatusFree RobotStatus = iota
	StatusReady
	StatusPlaying
	StatusPlayerOnline
	StatusDelete
)

type RobotType int16

const (
	RobotTypeRobot RobotType = iota

	RobotTypePlayer
)

// 1：代表即时赛排行榜
// 2：代表回合赛排行榜
// 3：代表一战到底专用的机器人

type RobotMark int16

const (
	RobotMarkNone RobotMark = iota
	RobotMark1v1
	RobotMarkTurnBased
	RobotMarkFightEnd
)

type RobotInfo struct {
	*Player
	robotId        int32
	status         RobotStatus
	searchTime     time.Time
	cfg            *global.RobotConfig
	attackDistance int

	rank          int64 // 机器人排名
	fightEndRobot bool
}

type RobotMgr struct {
	lock         sync.Mutex
	robots       map[int64]*RobotInfo
	rank1v1      map[int32]map[int64]*RobotInfo // 1v1排行榜 单服榜6
	rankTurnBase map[int32]map[int64]*RobotInfo // 回合制排行榜 战区榜
}

var robotMgr *RobotMgr

func init() {
	robotMgr = &RobotMgr{robots: make(map[int64]*RobotInfo), rank1v1: make(map[int32]map[int64]*RobotInfo), rankTurnBase: make(map[int32]map[int64]*RobotInfo)}

	for i := global.ServerIDRangeStart; i <= global.ServerIDRangeEnd; i++ {
		robotMgr.rank1v1[i] = make(map[int64]*RobotInfo)
	}
	// todo 回合制榜单按战区存储
	robotMgr.rankTurnBase[0] = make(map[int64]*RobotInfo)
}

func newRobotPlayer(uid int64, name string, sex, job, level, combat int32) *Player {
	r := &Player{
		sex:          int(sex),
		combat:       combat,
		energy:       int64(global.GlobalCfg.BaseValue.Energy.Fullvalue),
		logger:       log.WithField(fieldPlayer, uid),
		bag:          &pb.Bag{ItemList: []*pb.ItemInfo{}},
		curState:     PlayerStatusIdle,
		MapObject:    NewMapObject(ObjectTypeRobot, uid),
		PVPInfo:      &PVPInfo{PVPDuel: &PVPDuel{}, PVPMelee: &PVPMelee{}},
		partnerSlots: [MaxPartnerNum]*PartnerSlot{},
		partners:     map[int64]*Partner{},
		TurnBased:    &TurnBased{},
		InstanceData: &InstanceData{},
	}
	r.tasks = NewTask(r)
	r.name = name
	r.level = level
	r.job = constant.MowJob(job)
	r.PVPDuel.pvpScore = 1000
	r.PVPMelee.gvgScore = 1000
	r.uid = uid
	r.InstanceData.ChallengeRecord.Records = map[int32]int32{}
	r.bRobot = true

	r.stats.setStats(pb.Attribute_ATTACK_SPEED, global.GetRoleInitial(job).AttackSpeed)
	r.stats.setStats(pb.Attribute_MOVE_SPEED, global.GetRoleInitial(job).MoveSpeed)

	s := session.New(nil)
	s.Bind(uid)
	r.bindSession(s)
	return r
}

func deepCopyRobotPlayer(p *Player) *Player {
	r := newPlayer(nil, p.uid, p.name, "", "")

	r.vip = p.vip
	r.sex = int(p.sex)
	r.level = p.level
	r.job = constant.MowJob(p.job)
	r.bag = p.bag
	r.equips = p.equips
	r.skills = p.skills
	r.skillsSlot = p.skillsSlot

	r.totalCombat = p.totalCombat
	r.bRobot = true
	r.curState = PlayerStatusIdle
	r.objectType = ObjectTypeRobot

	for k, v := range p.stats.copyStats() {
		r.stats.setStats(k, v)
	}

	// 复制伙伴
	r.partners = make(map[int64]*Partner)
	r.partnerSlots = [MaxPartnerNum]*PartnerSlot{}
	for i := 0; i < int(MaxPartnerNum); i++ {
		sid_ := i + 1
		r.partnerSlots[i] = &PartnerSlot{SlotId: int32(sid_)}
		for j := 0; j < int(pb.EquipSlot_MaxEquipSlot); j++ {
			r.partnerSlots[i].Equips[j] = Slot{}
		}
	}

	for k, v := range p.partnerSlots {
		if v.PartnerUid == 0 {
			continue
		}
		basePar := p.getPartnerInfo(v.PartnerUid)
		if basePar == nil {
			continue
		}
		copyPar := basePar.deepCopy()
		copyPar.owner = r

		r.partnerSlots[k].PartnerUid = copyPar.uid

		for ei := 0; ei < len(v.Equips); ei++ {
			if v.Equips[ei].ItemId == 0 {
				continue
			}
			r.partnerSlots[k].Equips[ei].ItemId = v.Equips[ei].ItemId
			r.partnerSlots[k].Equips[ei].Stars = v.Equips[ei].Stars
			r.partnerSlots[k].Equips[ei].Enhance = v.Equips[ei].Enhance
			r.partnerSlots[k].Equips[ei].Quality = v.Equips[ei].Quality
			r.partnerSlots[k].Equips[ei].Gemstone = v.Equips[ei].Gemstone
		}

		r.partners[copyPar.uid] = copyPar
	}

	// 复制宠物
	if len(p.pet) > 0 && p.pet[0].ItemId > 0 && len(r.pet) > 0 {
		r.pet[0].PetUid = p.pet[0].PetUid
		r.pet[0].PetKind = p.pet[0].PetKind
		r.pet[0].PetCfgId = p.pet[0].PetCfgId
		r.pet[0].Level = p.pet[0].Level
		r.pet[0].Star = p.pet[0].Star
		r.pet[0].Enhance = p.pet[0].Enhance
		r.pet[0].Quality = p.pet[0].Quality
		r.pet[0].Exp = p.pet[0].Exp
		r.pet[0].ExpStar = p.pet[0].ExpStar
		r.pet[0].ItemId = p.pet[0].ItemId
	}

	for i := 0; i < int(TurnBasedMax); i++ {
		r.turnBasedSlot[i] = p.turnBasedSlot[i]
	}

	s := session.New(nil)
	s.Bind(p.uid)
	r.bindSession(s)
	return r
}

func (rm *RobotMgr) loadRobot() {
	if robotMgr == nil {
		robotMgr = &RobotMgr{robots: make(map[int64]*RobotInfo), rank1v1: make(map[int32]map[int64]*RobotInfo), rankTurnBase: make(map[int32]map[int64]*RobotInfo)}

		for i := global.ServerIDRangeStart; i < global.ServerIDRangeEnd; i++ {
			robotMgr.rank1v1[i] = make(map[int64]*RobotInfo)
		}
		// todo 回合制榜单按战区存储
		robotMgr.rankTurnBase[0] = make(map[int64]*RobotInfo)
	}

	sort.Slice(global.RobotCfg, func(i, j int) bool {
		return global.RobotCfg[i].Level < global.RobotCfg[j].Level
	})

	for _, v := range global.RobotCfg {
		for i := global.ServerIDRangeStart; i < global.ServerIDRangeEnd; i++ {
			if v.RankMark == int32(RobotMark1v1) {
				rm.createRobotByServerAndNum(i, 1, v)
			} else if v.RankMark == int32(RobotMarkFightEnd) {
				rm.createRobotByServerAndNum(i, 50, v)
			}
		}
		if v.RankMark == int32(RobotMark1v1) {
			rm.createRobotByServerAndNum(0, 1, v)
		} else if v.RankMark == int32(RobotMarkFightEnd) {
			rm.createRobotByServerAndNum(0, 50, v)
		}
	}
	logger.Debugf("机器人登录完毕, 共【%d】", len(rm.robots))

	rm.updateAllServerRank()
}

func (rm *RobotMgr) createRobotByServerAndNum(serverId, num int32, v *global.RobotConfig) {
	for i := 0; i < int(num); i++ {
		info := &RobotInfo{Player: &Player{}}

		uid := snowflake.GenID()
		rName := v.Name
		if rName == "" {
			rName = global.GenRandName()
		}
		info.Player = newRobotPlayer(uid, rName, 1, v.Job, v.Level, v.Power)

		for _, attr := range v.Feature {
			info.stats.setStats(pb.Attribute(attr.Key), attr.Value)
		}
		info.stats.setStats(pb.Attribute_HEALTH, info.stat(pb.Attribute_MAX_HEALTH))

		for _, skill := range v.Skill {
			info.addSkill(int32(skill))
		}

		for i := 0; i < int(MaxPartnerNum); i++ {
			sid_ := i + 1
			info.partnerSlots[i] = &PartnerSlot{SlotId: int32(sid_)}
			for j := 0; j < int(pb.EquipSlot_MaxEquipSlot); j++ {
				info.partnerSlots[i].Equips[j] = Slot{}
			}
		}

		// 伙伴装备到槽位
		slot := int32(1)
		for _, par := range v.Partners {
			if err := info.addPartner(par.Key); err == nil {
				if partner := info.getPartnerInfoByCfgId(par.Key); partner != nil {
					partner.attrRatio = float64(par.Num)
					partner.skills = append(partner.skills, &PlayerSkill{CfgId: par.ID})
					info.partnerCombat(slot, partner.uid)
					slot++
				}
			}
		}

		// 回合制伙伴站位
		tbslot := int32(0)
		info.turnBasedSlot[tbslot] = info.uid
		for _, v := range info.partnerSlots {
			tbslot++
			info.turnBasedSlot[tbslot] = v.PartnerUid
		}

		info.robotId = v.UUID
		info.cfg = v
		info.cfgId = info.cfg.UUID
		info.currentHP = info.stat(pb.Attribute_HEALTH)
		info.robotType = RobotTypeRobot
		info.fightEndRobot = false

		if constant.Warrior == constant.MowJob(v.Job) || constant.PartnerWarrior == constant.MowJob(v.Job) {
			info.attackDistance = 1
		} else {
			info.attackDistance = 6
		}
		info.serverID = serverId

		rm.robots[info.Uid()] = info
	}
}

// 更新排行榜
func (rm *RobotMgr) rankChangeTCP(r *RobotInfo, k int, v int64) {
	provide.RankChangeTCP(&protocol.RankUserInfoInput{
		Uid:      r.uid,
		Type:     int32(k),
		Avatar:   r.head,
		Name:     r.name,
		Value:    v,
		Sex:      int32(r.sex),
		Job:      int32(r.job),
		Mysql:    true,
		Compare:  k <= constant.RankTypeTrial,
		ServerId: r.serverID,
		RobotCId: r.cfgId,
	})
}

// 获取对应等级的机器人，level=0 表示不限制等级
func (rm *RobotMgr) getFreeRobot(level int32) *RobotInfo {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	var r *RobotInfo = nil

	for _, v := range rm.robots {
		if v.status != StatusFree {
			continue
		}
		if v.robotType != RobotTypePlayer {
			continue
		}
		if level == 0 {
			v.status = StatusReady
			r = v
			// return v
		}
		if maputil.ABS(level-v.level) <= 5 {
			v.status = StatusReady
			r = v
			// return v
		}
	}
	if r != nil {

		logger.Debugf(" PVP匹配到离线数据 名字:[%s] 等级[%d]  ", r.name, r.level)
		return r
	} else {
		for _, v := range rm.robots {
			if v.status != StatusFree {
				continue
			}

			if level == 0 {
				v.status = StatusReady
				return v
			}
			if maputil.ABS(level-v.level) <= 5 {
				v.status = StatusReady
				return v
			}
		}
	}
	if r := rm.createRobot(level); r != nil {
		r.status = StatusReady
		return r
	}
	return nil
}

func (rm *RobotMgr) createRobot(level int32) *RobotInfo {
	for _, v := range global.RobotCfg {

		if maputil.ABS(level-v.Level) > 5 {
			continue
		}

		info := &RobotInfo{Player: &Player{}}

		uid := snowflake.GenID()
		info.Player = newRobotPlayer(uid, global.GenRandName(), 1, v.Job, v.Level, v.Power)
		for _, attr := range v.Feature {
			info.stats.setStats(pb.Attribute(attr.Key), attr.Value)
		}

		for _, skill := range v.Skill {
			info.addSkill(int32(skill))
		}

		sort.Slice(info.skills, func(i, j int) bool {
			return info.skills[i].Distance < info.skills[j].Distance
		})

		info.stats.changeStats(pb.Attribute_HEALTH, info.stat(pb.Attribute_MAX_HEALTH))

		info.robotId = v.UUID
		info.cfg = v
		info.currentHP = info.stat(pb.Attribute_HEALTH)
		rm.robots[info.Uid()] = info
		return info
	}
	return nil
}

func (rm *RobotMgr) getRobotByCfgId(serverId int32, cid int32) *RobotInfo {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	for _, v := range rm.robots {
		if v.cfgId == cid && v.serverID == serverId {
			return v
		}
	}
	return nil
}

func (rm *RobotMgr) getFreeRobotByCfgId(cid int32) *RobotInfo {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	for _, v := range rm.robots {
		if v.cfgId == cid && v.status == StatusFree {
			return v
		}
	}

	return nil
}

func (rm *RobotMgr) onGameOver(robotID int64) {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	r, ok := rm.robots[robotID]
	if ok {

		logger.Debugf("[%s] -- [机器人类型 : %d] 游戏结束  改变机器人游戏状态 [%d] ", r.name, r.robotType, r.status)
		r.status = StatusFree

		if r.desk != nil {
			r.desk = nil
		}
		r.currentHP = r.stat(pb.Attribute_MAX_HEALTH)
		r.dead = false
		r.stats.setStats(pb.Attribute_HEALTH, r.stat(pb.Attribute_MAX_HEALTH))
	}
}

func (rm *RobotMgr) onExitDesk(serverID int32, robotID int64) {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	if r, ok := rm.rank1v1[serverID][robotID]; ok {
		r.status = StatusFree
		r.desk = nil
		return
	}
	if r, ok := rm.robots[robotID]; ok {
		r.status = StatusFree
		r.desk = nil
		return
	}
}

func (rm *RobotMgr) onEnterDesk(serverID int32, robotID int64) {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	if r, ok := rm.rank1v1[serverID][robotID]; ok {
		r.status = StatusPlaying
		return
	}
	if r, ok := rm.robots[robotID]; ok {
		r.status = StatusPlaying
		return
	}
}

func (rm *RobotMgr) OnGameOpMsg(uid int64, route string, msg interface{}) {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	r, ok := rm.robots[uid]
	if !ok {
		logger.Warningf("R 不在线 ID : [%d]", uid)
		return
	}

	if r.desk == nil {
		logger.Warningf("R 不在桌子上 ID : [%d]", uid)
		return
	}

	switch route {
	// case protocol.RouteObjectHpChangePush:
	// 	logger.Debugf("R 血量变化 %v", msg.(*pb.ObjectHpChangePush))
	case protocol.RouteMatchRoomJoinPush:
		data := msg.(*pb.MatchRoomJoinPush)
		logger.Debugf("R 加入比赛房间 roomid=%d  rUid =%d  matchID=%d , %v", r.desk.deskID, r.uid, r.desk.matchUid, data)

	default:

	}
}

// 获取排行榜机器人
func (rm *RobotMgr) getRankRobotInfo(serverID int32, uid int64) *RobotInfo {
	rm.lock.Lock()
	defer rm.lock.Unlock()
	if list, ok := rm.rank1v1[serverID]; ok {
		r, ok := list[uid]
		if ok {
			return r
		}
	}
	if list, ok := rm.rankTurnBase[0]; ok {
		r, ok := list[uid]
		if ok {
			return r
		}
	}
	if r, ok := rm.robots[uid]; ok {
		return r
	}

	return nil
}

// 获取排行榜机器人
func (rm *RobotMgr) getRankRobotInfoByRank(rankType int32, serverID int32, rank int64) *RobotInfo {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	switch rankType {
	case constant.RankTypeSoul:
		if list, ok := rm.rank1v1[serverID]; ok {
			for _, v := range list {
				if v.rank == rank {
					return v
				}
			}
		} else {
			logger.Errorf("获取即时赛机器人失败, RankType= %d serverID = %d , rank = %d", rankType, serverID, rank)
		}

	case constant.RankTypeTurnBased:
		if list, ok := rm.rankTurnBase[0]; ok {
			for _, v := range list {
				if v.rank == rank {
					return v
				}
			}
		} else {
			logger.Errorf("获取回合制机器人失败, RankType= %d serverID = %d , rank = %d", rankType, serverID, rank)
		}
	}

	return nil
}

// 获取排行榜机器人
func (rm *RobotMgr) getRankRobotInfoByUid(rankType int32, serverID int32, uid int64) *RobotInfo {
	rm.lock.Lock()
	defer rm.lock.Unlock()

	switch rankType {
	case constant.RankTypeSoul:
		if r, ok := rm.rank1v1[serverID][uid]; ok {
			return r
		}
	case constant.RankTypeTurnBased:
		if r, ok := rm.rankTurnBase[0][uid]; ok {
			return r
		}
	}

	return nil
}

// 获取排行榜十个名次相近的对手
func (rm *RobotMgr) getTenClosestPlayers(rankType int32, serverID int32, uid int64) []*RobotInfo {
	var players []*RobotInfo
	self := rm.getRankRobotInfoByUid(rankType, serverID, uid)
	if self == nil {
		for i := 100; i > 90; i-- {
			if rp := rm.getRankRobotInfoByRank(rankType, serverID, int64(i)); rp != nil {
				players = append(players, rp)
			}
		}
	} else {

		// 获取前面的玩家
		for i := self.rank - 1; i > 0 && len(players) < 5; i-- {
			if player := rm.getRankRobotInfoByRank(rankType, serverID, int64(i)); player != nil {
				if player.curState != PlayerStatus(StatusFree) {
					continue
				}
				players = append(players, player)
			}
		}

		// 获取后面的玩家
		for i := self.rank + 1; i <= int64(len(rm.rank1v1[serverID])) && len(players) < 10; i++ {
			if player := rm.getRankRobotInfoByRank(rankType, serverID, i); player != nil {
				if player.curState != PlayerStatus(StatusFree) {
					continue
				}
				players = append(players, player)
			}
		}
	}

	next := 90
	for len(players) < 10 {

		if rp := rm.getRankRobotInfoByRank(rankType, serverID, int64(next)); rp != nil {
			players = append(players, rp)
		}
		next--
	}
	return players
}

func (rm *RobotMgr) updateAllServerRank() {
	for i := global.ServerIDRangeStart; i < global.ServerIDRangeEnd; i++ {
		rm.updateRank(int32(i), constant.RankTypeSoul)
	}
	rm.updateRank(0, constant.RankTypeTurnBased)
}

func (rm *RobotMgr) updateRank(serverId int32, rankType int64) {
	// 加载排行榜
	req := protocol.RankOperateRequest{Type: rankType, All: 1, ServerId: serverId}
	if rankList, err := provide.RankList(&req); err == nil {
		if len(rankList.RankAll) > 0 {
			for _, v := range rankList.RankAll {
				if v.RobotCId > 0 {
					if r := rm.getRankRobotInfoByUid(int32(rankType), serverId, v.Uid); r != nil {
						r.rank = v.Num
						r.name = v.Name
						r.pvpScore = int32(v.Value)
						r.robotType = RobotTypeRobot
						r.uid = v.Uid
						r.job = constant.MowJob(v.Job)
						r.sex = int(v.Sex)
						r.cfgId = v.RobotCId
						r.serverID = serverId

						s := session.New(nil)
						s.Bind(r.uid)
						r.bindSession(s)

						if rankType == constant.RankTypeSoul {
							if _, ok := rm.rank1v1[r.serverID]; ok {
								rm.rank1v1[r.serverID][r.uid] = r
							} else {
								rm.rank1v1[r.serverID] = make(map[int64]*RobotInfo)
								rm.rank1v1[r.serverID][r.uid] = r
							}
						} else if rankType == constant.RankTypeTurnBased {
							if _, ok := rm.rankTurnBase[0]; ok {
								rm.rankTurnBase[0][r.uid] = r
							} else {
								rm.rankTurnBase[0] = make(map[int64]*RobotInfo)
								rm.rankTurnBase[0][r.uid] = r
							}
						}

						r.refreshStats()
					} else if r := rm.getRobotByCfgId(serverId, v.RobotCId); r != nil {
						r.rank = v.Num
						r.name = v.Name
						r.pvpScore = int32(v.Value)
						r.robotType = RobotTypeRobot
						r.uid = v.Uid
						r.job = constant.MowJob(v.Job)
						r.sex = int(v.Sex)
						r.cfgId = v.RobotCId
						r.serverID = serverId

						tbslot := int32(0)
						r.turnBasedSlot[tbslot] = r.uid
						for _, v := range r.partnerSlots {
							tbslot++
							r.turnBasedSlot[tbslot] = v.PartnerUid
						}

						s := session.New(nil)
						s.Bind(r.uid)
						r.bindSession(s)

						if rankType == constant.RankTypeSoul {
							if _, ok := rm.rank1v1[r.serverID]; ok {
								rm.rank1v1[r.serverID][r.uid] = r
							} else {
								rm.rank1v1[r.serverID] = make(map[int64]*RobotInfo)
								rm.rank1v1[r.serverID][r.uid] = r
							}
						} else if rankType == constant.RankTypeTurnBased {
							if _, ok := rm.rankTurnBase[0]; ok {
								rm.rankTurnBase[0][r.uid] = r
							} else {
								rm.rankTurnBase[0] = make(map[int64]*RobotInfo)
								rm.rankTurnBase[0][r.uid] = r
							}
						}

						r.refreshStats()
						// logger.Debugf(" 服务器ID: %d 排行榜机器人数据 UID : %d  Name : %s 积分r:%d  Rank : %d", serverId, r.Uid(), r.name, r.pvpScore, r.rank)
					}
				} else {
					if player := defaultManager.ObserveUserBaseInfo(serverId, v.Uid); player != nil {
						player.pvpScore = int32(v.Value)

						if r := rm.getRankRobotInfoByUid(int32(rankType), serverId, v.Uid); r != nil {
							if r.level == player.level {
								r.totalCombat = player.totalCombat
								r.pvpScore = int32(v.Value)
								r.rank = v.Num
								continue
							}
						}

						dp := deepCopyRobotPlayer(player)
						r := &RobotInfo{Player: dp}
						r.rank = v.Num
						r.pvpScore = int32(v.Value)
						r.robotType = RobotTypePlayer
						r.job = constant.MowJob(v.Job)
						r.sex = int(v.Sex)
						if r.job == constant.Warrior || r.job == constant.PartnerWarrior {
							r.attackDistance = 2
						} else {
							r.attackDistance = 6
						}
						r.serverID = serverId

						if rankType == constant.RankTypeSoul {
							if _, ok := rm.rank1v1[r.serverID]; ok {
								rm.rank1v1[r.serverID][r.uid] = r
							} else {
								rm.rank1v1[r.serverID] = make(map[int64]*RobotInfo)
								rm.rank1v1[r.serverID][r.uid] = r
							}
						} else if rankType == constant.RankTypeTurnBased {
							if _, ok := rm.rankTurnBase[0]; ok {
								rm.rankTurnBase[0][r.uid] = r
							} else {
								rm.rankTurnBase[0] = make(map[int64]*RobotInfo)
								rm.rankTurnBase[0][r.uid] = r
							}
						}

						// logger.Debugf(" 服务器ID: %d 排行榜真实玩家数据 UID : %d  Name : %s 积分r:%d  Rank : %d", serverId, r.Uid(), r.name, r.pvpScore, r.rank)
					}
				}
			}
		} else {
			rankRobot := []*RobotInfo{}
			for _, v := range rm.robots {
				if v.cfg.RankMark == int32(RobotMark1v1) && v.serverID == serverId {
					rankRobot = append(rankRobot, v)
				}
			}

			sort.Slice(rankRobot, func(i, j int) bool {
				return rankRobot[i].cfg.Level > rankRobot[j].cfg.Level
			})

			for i := 0; i < len(rankRobot); i++ {
				rm.rankChangeTCP(rankRobot[i], int(rankType), int64(rankRobot[i].pvpScore))

				logger.Debugf("服务器ID:%d 首次加载排行榜， 加入机器人%d : %s : %d  积分 : %d  性别 %d  职业 %d", serverId, i, rankRobot[i].name, rankRobot[i].uid, rankRobot[i].pvpScore, rankRobot[i].sex, rankRobot[i].job)
				time.Sleep(time.Millisecond)
			}

			time.AfterFunc(time.Millisecond*time.Duration(serverId*10), func() {
				rm.updateRank(serverId, rankType)
			})
		}
	}
}

/*
---------------------------------------
   RobotMgr End
---------------------------------------
*/

func (r *RobotInfo) processAI() {
	r.processSearch()
	r.processTarget()
}

// 寻找
func (r *RobotInfo) processSearch() {
	if r.desk == nil {
		return
	}

	if time.Now().Before(r.searchTime) {
		return
	}
	r.searchTime = time.Now().Add(time.Second)

	if !r.CanAttack() {
		return
	}

	// 被嘲讽不能转换目标
	if r.getBuffByType(constant.BuffTaunt) != nil && r.target != nil {
		return
	}

	if r.Target() != nil && maputil.Distance(r.Location(), r.Target().Location()) <= 3 {
		// logger.Debugf("R 目标不变 自己ID: %d , 自身坐标:%v, 目标ID: %d  目标坐标: %v 目标类型: %v", r.uid, r.location, r.Target().Uid(), r.target.Location(), r.target.ObjectType())
		return
	}

	var distance int
	var closest IMapObject
	if r.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
		for _, v := range r.desk.monsters {
			if v.dead {
				continue
			}
			if v.uid == r.uid {
				continue
			}
			tmpDistance := maputil.Distance(r.location, v.Location())
			if distance == 0 || tmpDistance < distance {
				distance = tmpDistance
				closest = v
			}
		}
	} else {
		for _, v := range r.desk.players {
			if v.dead {
				continue
			}
			if v.uid == r.uid {
				continue
			}
			tmpDistance := maputil.Distance(r.location, v.Location())
			if distance == 0 || tmpDistance < distance {
				distance = tmpDistance
				closest = v
			}
		}
		for _, v := range r.desk.objects {
			if v.objectType == ObjectTypeDecoy {
				tmpDistance := maputil.Distance(r.location, v.Location())
				if tmpDistance > r.attackDistance+20 {
					continue
				}
				if !r.CanAttackTarget(v) {
					continue
				}

				if distance == 0 || tmpDistance < distance {
					distance = tmpDistance
					closest = v
				}
			}
		}
	}

	if closest != nil {
		r.target = closest

		// logger.Debugf("R 锁定目标 mUid: %d , 自身坐标:%v, 目标ID: %d  目标坐标: %v 目标类型: %v", r.uid, r.location, r.target.Uid(), r.target.Location(), r.target.ObjectType())
	}
}

// 目标
func (r *RobotInfo) processTarget() {
	if r.desk == nil {
		return
	}

	if r.target == nil {
		return
	}

	if r.dead || r.target.Dead() {
		r.target = nil
		return
	}

	if !r.CanAttackTarget(r.target) {
		return
	}

	var castSkill *PlayerSkill
	for _, v := range r.skills {
		skillCfg, err := global.GetSkillCfg(int32(v.CfgId))
		if err != nil {
			return
		}

		if skillCfg.SType == constant.SkillTypeNormal {
			continue
		}

		if skillCfg.CoolDown > 0 && time.Now().Before(v.Cooldown) {
			continue
		}

		if r.inAttackRange(int(v.Distance)) {
			castSkill = v
			break
		}
	}

	distance := r.attackDistance
	if castSkill != nil {
		distance = int(castSkill.Distance)
	}

	if !r.inAttackRange(distance) {
		if !r.CanMove() {
			return
		}
		var notargetSkill *PlayerSkill
		for _, v := range r.skills {
			skillCfg, err := global.GetSkillCfg(int32(v.CfgId))
			if err != nil {
				return
			}

			if skillCfg.SType == constant.SkillTypeNormal {
				continue
			}

			if skillCfg.CoolDown > 0 && time.Now().Before(v.Cooldown) {
				continue
			}
			if skillCfg.SkillID == constant.SkillTypeDecoy || skillCfg.SkillID == constant.SkillTypeDecoyBoss || skillCfg.SkillID == constant.SkillTypeInstantaneousMove {
				continue
			}

			if skillCfg.SkillID == constant.SkillTypeSwiftStrike {
				notargetSkill = v
				break
			}
			// 30% 几率给自己上buff
			if skillCfg.Needaim == 0 && global.RandIn(100) < 30 {
				notargetSkill = v
				break
			}
		}
		if notargetSkill != nil {
			castSkill = notargetSkill
		} else {
			if path := maputil.GetPath(r.location, r.target.Location(), &r.desk.mapCfg.AstarMgr); len(path) > 1 {
				r.moveTo(path[1])
			} else {
				logger.Debugf("R 寻路失败 起点=%v  终点=%v", r.location, r.target.Location())
			}
			return
		}

	} else {
		if (r.job == constant.Wizard || r.job == constant.Ritualist) && maputil.Distance(r.location, r.target.Location()) < 2 && r.getBuffByType(constant.BuffKnockback) == nil {
			if r.CanMove() && r.getBuffByType(constant.BuffTaunt) == nil {
				var nearSkill *PlayerSkill
				for _, v := range r.skills {
					skillCfg, err := global.GetSkillCfg(int32(v.CfgId))
					if err != nil {
						return
					}
					if skillCfg.CoolDown > 0 && time.Now().Before(v.Cooldown) {
						continue
					}

					if skillCfg.SkillID == constant.SkillTypeDecoy || skillCfg.SkillID == constant.SkillTypeDecoyBoss || skillCfg.SkillID == constant.SkillTypeInstantaneousMove {
						nearSkill = v
						break
					}

					if skillCfg.SkillID == constant.SkillTypeManaExpel || skillCfg.SkillID == constant.SkillTypeMagicBarrier {
						nearSkill = v
						break
					}
				}

				if nearSkill != nil {
					castSkill = nearSkill
				} else {
					var cell *Cell
					var point *global.PointI
					dir := maputil.DirectionFromPointI(&r.cell.location, &r.target.GetCell().location)
					point = maputil.Move(&r.cell.location, maputil.ShiftDirection(dir, 4), 1)
					if cell = r.desk.mapInfo.getCell(point); cell != nil && !cell.block {
						r.moveTo(&astar.APoint{X: cell.location.X, Y: cell.location.Y})
						r.moveTime = r.moveTime.Add(time.Duration(r.stat(pb.Attribute_MOVE_SPEED)*3) * time.Millisecond)
						return
					}
					point = maputil.Move(&r.cell.location, maputil.ShiftDirection(dir, 2), 1)
					if cell = r.desk.mapInfo.getCell(point); cell != nil && !cell.block {
						r.moveTo(&astar.APoint{X: cell.location.X, Y: cell.location.Y})
						r.moveTime = r.moveTime.Add(time.Duration(r.stat(pb.Attribute_MOVE_SPEED)*2) * time.Millisecond)
						return
					}
					point = maputil.Move(&r.cell.location, maputil.ShiftDirection(dir, -2), 1)
					if cell = r.desk.mapInfo.getCell(point); cell != nil && !cell.block {
						r.moveTo(&astar.APoint{X: cell.location.X, Y: cell.location.Y})
						r.moveTime = r.moveTime.Add(time.Duration(r.stat(pb.Attribute_MOVE_SPEED)*2) * time.Millisecond)
						return
					}
				}
			}
		}
		if len(r.cell.objects) > 1 {
			cell := r.desk.mapInfo.getAvailableCell(global.ToPointI(r.target.Location()), int32(r.attackDistance))
			if cell != nil {
				if path := maputil.GetPath(r.location, &pb.Point{X: float64(cell.location.X), Y: float64(cell.location.Y)}, &r.desk.mapCfg.AstarMgr); len(path) > 1 {
					r.moveTo(path[1])
					return
				}
			}
		}
	}
	r.attack(castSkill)
}

// 移动
func (r *RobotInfo) moveTo(point *astar.APoint) {
	if r.desk == nil {
		return
	}

	if !r.CanMove() {
		return
	}
	// from := r.location
	to := &pb.Point{X: float64(point.X), Y: float64(point.Y)}
	if !r.desk.mapInfo.locationChange(r, to) {
		return
	}

	r.moveTime = time.Now().Add(time.Duration(r.getMoveSpeed()+100) * time.Millisecond)

	packet := &pb.MovePush{Uid: r.uid, Location: r.location, Direction: r.direction}
	r.desk.Broadcast(protocol.RouteMovePush, packet)
	r.SetObState(ObjectActionMove)

	// logger.Debugf("%s 移动 rUid: %d , 起点:%v , 目标: %v  与目标距离 %d  移动速度 %d", r.name, r.uid, from, to, maputil.Distance(r.location, r.target.Location()), r.getMoveSpeed())
}

// 攻击
func (r *RobotInfo) attack(castSkill *PlayerSkill) {
	if r.desk == nil {
		logger.Errorf("机器人攻击失败，不在房间中 %d:%s", r.uid, r.name)
		return
	}

	if !r.CanAttack() {
		return
	}

	if r.stat(pb.Attribute_ATTACK_SPEED) <= 100 {
		logger.Errorf("机器人攻击失败，攻击速度异常 %d:%s", r.uid, r.name)
		return
	}

	var skillId, skillIndex int32
	if castSkill != nil {
		skillId = castSkill.CfgId
	} else {
		if r.job == constant.Warrior {
			skillId = constant.SkillTypeNormalAttackWarrior * 100
		} else if r.job == constant.Wizard {
			skillId = constant.SkillTypeNormalAttackWizard * 100
		} else if r.job == constant.Ritualist {
			skillId = constant.SkillTypeNormalAttackRitualist * 100
		}
	}
	skillCfg, err := global.GetSkillCfg(int32(skillId))
	if err != nil {
		return
	}
	for i := range r.skills {
		if r.skills[i].CfgId == skillId {
			skillIndex = int32(i)
		}
	}

	r.direction = maputil.DirectionFromPointF(r.location, r.target.Location())

	// 安全区内不能攻击玩家
	if r.desk.roomCfg.Roomtype == constant.PVEPeacefulMap || r.desk.roomCfg.Roomtype == constant.PVEWarMap {
		if r.target.ObjectType() == ObjectTypePlayer && (r.desk.mapInfo.safeArea(r.location) || r.desk.mapInfo.safeArea(r.target.Location())) {
			logger.Debugf("R 攻击失败, 不能攻击安全区内玩家: 目标: %d", r.target.Uid())
			return
		}
	}

	packet := &pb.AttackPush{
		AttackerId: r.uid,
		TargetUids: []int64{r.target.Uid()},
		SkillId:    int32(skillId),
		Location:   r.target.Location(),
		Direction:  r.direction,
	}
	r.desk.Broadcast(protocol.RouteAttackPush, packet)

	rdesk := r.desk
	rtarget := r.Target()
	time.AfterFunc(time.Duration(skillCfg.DelayTime)*time.Millisecond, func() {
		if rdesk != nil && rdesk.status() == constant.DeskStatusPlaying {
			rdesk.delayAttackLock.Lock()
			defer rdesk.delayAttackLock.Unlock()

			if len(rdesk.chDelayAttack) >= chLen {
				logger.Warningln("R chDelayAttack 通道已满")
				return
			}
			rdesk.chDelayAttack <- &DelayAction{ObjType: ObjectTypeRobot, ObjUid: r.uid, SkillId: int32(skillId), Target: rtarget}
		}
	})

	// r.attackTime = time.Now().Add(time.Duration(r.stat(pb.Attribute_ATTACK_SPEED)) * time.Millisecond)

	r.setAttackTime(global.GetActionsetCfg(skillCfg.SkillID))

	// 技能CD
	if skillCfg.CoolDown > 0 {
		r.skills[skillIndex].Cooldown = time.Now().Add(time.Duration(skillCfg.CoolDown) * time.Millisecond)
	}

	// 技能关联buff
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
		if skillUpperlevel, err := global.GetUpperlevelSettingCfg(skillCfg.UID, castSkill.AdvancedLevel); err == nil {
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
				bufStats.setStats(pb.Attribute_REDUCE_CONTROL_TIME_PERCENTAGE, subCtl)
			}
		}
		r.AddBuff(bufCfg.ID, r, addSec, bufStats, addRate, skillId)
	}

	r.SetObState(ObjectActionAttack)
	// logger.Debugf("%s 攻击 attack rUid: %d , 目标: %d  技能 %d  ", r.name, r.uid, r.Target().Uid(), skillId)
}
