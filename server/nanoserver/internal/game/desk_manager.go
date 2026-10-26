package game

import (
	"fmt"
	"strings"
	"time"

	"nanoserver/global"
	shushumain "nanoserver/internal/shushu"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/errutil"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"

	"github.com/lonng/nano/component"
	"github.com/lonng/nano/session"
)

//const deskOpBacklog = 64

type (
	DeskManager struct {
		component.Base
		opt *global.RoomConfig
		//桌子数据
		desks  map[int64]*Desk // 所有桌子
		active int64
	}
)

func NewDeskManager(opt *global.RoomConfig) *DeskManager {
	return &DeskManager{
		desks: map[int64]*Desk{},
		opt:   opt,
	}
}

func (manager *DeskManager) ID() int32 {
	return manager.opt.ID
}

func (manager *DeskManager) dumpDeskInfo() {
	c := len(manager.desks)
	if c < 1 {
		return
	}

	logger.Infof("[%d:%s] 剩余房间数量: %d 在线人数: %d  当前时间: %s", manager.opt.ID, manager.opt.Name, c, defaultManager.sessionCount(), time.Now().Format("2006-01-02 15:04:05"))
	for no, d := range manager.desks {
		if len(d.players) > 0 {
			logger.Debugf("房号: %d, 创建时间: %s,  状态: %s, 玩家数: %d",
				no, time.Unix(d.createdAt, 0).String(), d.status().String(), d.realPlayerCount())
		}
	}
}

// 设置桌号对应的牌桌数据
func (manager *DeskManager) setDesk(number int64, desk *Desk) {
	if desk == nil {
		delete(manager.desks, number)
		logger.WithField(fieldDesk, number).Debugf("清除房间: 剩余: %d", len(manager.desks))
	} else {
		manager.desks[number] = desk
	}
}

func (manager *DeskManager) getActiveDesk(p *Player) *Desk {
	var d *Desk
	if manager.opt.Roomtype == constant.GuildMap {
		for _, fd := range manager.desks {
			if fd.GuildId == p.guildId {
				d = fd
			}
		}
	} else {
		if manager.active > 0 {
			if ad, ok := manager.desks[manager.active]; ok {
				if len(ad.players) < ad.totalPlayerCount() {
					d = ad
					d.reset()
				}
			}
		}
	}

	if d == nil {
		for _, fd := range manager.desks {
			if fd.mapInfo == nil {
				fd.setStatus(constant.DeskStatusDestory)
				continue
			}
			if fd.status() == constant.DeskStatusDestory {
				continue
			}

			if len(fd.players) >= fd.totalPlayerCount() {
				continue
			}
			d = fd
			d.reset()
			manager.active = d.deskID
			break
		}
	}
	return d
}

// 新join在session的context中尚未有desk的cache
func (manager *DeskManager) Join(p *Player, data *pb.JoinDeskRequest) error {
	if d := manager.getActiveDesk(p); d != nil {
		if err := d.playerJoin(p.session, false, data.BattleId); err != nil {
			return err
		}

		d.checkStart()

		p, err := d.playerWithId(p.Uid())
		if err != nil {
			return err
		}
		robotInfo := &RobotInfo{}
		res := &pb.JoinDeskResponse{
			TableInfo: &pb.TableInfo{
				RoomCfgId:   d.cfgID,
				RoomUid:     d.deskID,
				MonsterList: []*pb.MonsterInfo{},
				BattleId:    data.BattleId,
			},
			Location:  p.location,
			Direction: p.direction,
		}
		if d.roomCfg.Roomtype == constant.PVEPeacefulMap || d.roomCfg.Roomtype == constant.PVEWarMap {
			res.LeftTime = int64(p.afkLeftTime())
		} else if d.roomCfg.Roomtype == constant.SoloTowerMap {
			res.LeftTime = int64(d.SoloTowerCfg.TimeLimit)
		} else if d.roomCfg.Roomtype == constant.WorldBossMap {
			res.LeftTime = int64(global.GlobalCfg.WorldBoss.Challengetime.Duration)
		} else if d.roomCfg.Roomtype >= constant.ExperienceFBMap && d.roomCfg.Roomtype <= constant.SoulStoneFBMap {
			res.LeftTime = int64(global.GetDailyMapTime(d.roomCfg.Roomtype).Duration)

			// 同步评级
			packet := &pb.DailyMapScorePush{
				MType:      pb.MapType(d.roomCfg.Roomtype),
				MonsterNum: int32(len(d.monsters)),
				Round:      d.round}
			d.Broadcast(protocol.RouteDailyMapScorePush, packet)
		} else if d.roomCfg.Roomtype == constant.GuidanceFBMap {
			res.LeftTime = int64(global.GlobalCfg.BaseValue.Guidemap.LastTime)
		} else if d.roomCfg.Roomtype >= constant.BattleMap && d.roomCfg.Roomtype <= constant.RelicMap {
			res.LeftTime = int64(d.BattleCfg.TimeLimit)
		} else if d.roomCfg.Roomtype == constant.GuildMap {
			pass := time.Now().Unix() - d.beginTime.Unix()
			res.LeftTime = int64(global.GlobalCfg.Guild.GuildCopy.Lasttime)*60 - pass
			if res.LeftTime < 0 {
				res.LeftTime = 0
			}
			p.pushMsg(protocol.RouteGuildCopyRewardPush, d.GuildReward)
		} else if d.roomCfg.Roomtype == constant.FightEndMap {
			res.LeftTime = int64(d.BattleCfg.TimeLimit)
			if btcfg := global.GetBattleMapCfg(d.BattleId); btcfg != nil {
				for _, v := range btcfg.Monster {
					robotInfo = robotMgr.getRobotByCfgId(p.serverID, v.ID)
					break
				}
			}
		}

		p.expirationTime = time.Now().Add(time.Duration(res.LeftTime) * time.Second)

		// 发送怪物列表
		d.lock.Lock()
		for _, m := range d.monsters {
			res.TableInfo.MonsterList = append(res.TableInfo.MonsterList, &pb.MonsterInfo{
				CfgId: m.cfgId,
				Uid:   m.uid,
				//Name:      m.name,
				MaxHp:     m.GetMaxHP(),
				CurHp:     m.displayHP,
				Location:  m.location,
				Direction: m.direction,
				BuffList:  m.getPBBuffList()})
		}
		manager.upBattleBeginShushuDate(p, data.BattleId, robotInfo, d.roomCfg.Roomtype)
		defer d.lock.Unlock()

		err = p.session.Response(res)

		d.syncDeskStatus()

		return err
	}

	return errutil.ErrDeskNotFound
}
func (manager *DeskManager) upBattleBeginShushuDate(p *Player, BattleId int32, robotInfo *RobotInfo, roomType int32) {
	shushuDate := make(map[string]interface{}, 0)
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	// 主角信息
	//查看战斗信息
	main_type := roomType
	main_name := ""
	if cfg := global.GetBattleMapCfg(BattleId); cfg != nil {
		main_name = fmt.Sprintf("%v", cfg.Chapter) + fmt.Sprintf("%v", cfg.Layers)
	}
	shushuDate["main_name"] = main_name
	shushuDate["main_id"] = BattleId
	shushuDate["main_type"] = main_type
	//查询是否是首次挑战
	getChallengeRecord := p.getChallengeRecord(roomType)
	if getChallengeRecord == 0 {
		shushuDate["is_first_try"] = true
	} else {
		shushuDate["is_first_try"] = false
	}
	partnerInfo := make([]map[string]interface{}, 0)
	switch roomType {
	case constant.GuildMap:
		shushuDate["name"] = "guild_boss_start"
		if p.guildId > 0 {
			shushuDate["guild_id"] = p.guildId
			//查询工会信息
			res, err := guildManager.validateGuild(p.guildId)
			if err == nil {
				shushuDate["guild_level"] = res.Guild.Level
				shushuDate["guild_name"] = res.Guild.Name
			}
			// if NewGuildManager().guilds[p.guildId] != nil {
			// 	shushuDate["guild_level"] = NewGuildManager().guilds[p.guildId].Guild.Level
			// 	shushuDate["guild_name"] = NewGuildManager().guilds[p.guildId].Guild.Name
			// }
		}
	case constant.WorldBossMap:

		shushuDate["name"] = "world_boss_start"
	case constant.FightEndMap:
		shushuDate["name"] = "yizhandaodi_start"
		// shushuDate["role_rank"] = p.
		if robotInfo != nil {
			if robotInfo.name != "" {
				shushuDate["opponent_name"] = robotInfo.name
			}
			if robotInfo.uid != 0 {
				shushuDate["opponent_id"] = robotInfo.uid
			}
			if robotInfo.combat != 0 {
				shushuDate["opponent_power"] = robotInfo.combat
			}
			shushuDate["opponent_rank"] = robotInfo.rank
			if robotInfo.bRobot {
				shushuDate["is_robot"] = true
			} else {
				shushuDate["is_robot"] = false
			}
		}
	case constant.PVEPeacefulMap:
		shushuDate["name"] = "guarding_island_start"
		if p.level >= 110 {
			shushuDate["main_id"] = "高级"
			shushuDate["main_name"] = "高级"
		} else {
			shushuDate["main_id"] = "初级"
			shushuDate["main_name"] = "初级"
		}
		if p.afkDailyTime == 0 {
			shushuDate["first_time_daily"] = true
		} else {
			shushuDate["first_time_daily"] = false
		}
		shushuDate["remaining_time"] = 3600 - p.afkDailyTime
	default:
		shushuDate["name"] = "challenge_battle_start"
	}

	// 伙伴信息
	for _, v := range p.partnerSlots {
		if v != nil && v.PartnerUid > 0 {
			partner := p.getPartnerInfo(v.PartnerUid)
			if partner != nil {
				partnerInfo = append(partnerInfo, map[string]interface{}{
					"partner_id":      partner.cfgId,
					"partner_name":    partner.name,
					"partner_quality": partner.cfg.Quality,
					"partner_level":   partner.level,
					"partner_job":     int32(partner.job),
					"partner_star":    partner.star,
					"partner_power":   partner.combat,
				})
			}
		}
	}

	shushuDate["partner_info"] = partnerInfo

	// 是否首次尝试
	// shushuDate["is_first_try"] = p.isFirstTry

	// 技能信息
	skillInfo := make([]map[string]interface{}, 0)
	// 伙伴信息
	for _, v := range p.skillsSlot {
		if v != 0 && v > 0 {
			if scfg, err := global.GetSkillCfg(v); err == nil {
				skill := map[string]interface{}{
					"skill_id":    v,
					"skill_name":  scfg.Name,
					"skill_level": scfg.Level,
				}
				if getSkill := p.getSkill(v); getSkill != nil {
					skill["skill_grade"] = getSkill.AdvancedLevel
				}
				skillInfo = append(skillInfo, skill)
			}
		}
	}
	shushuDate["skill_info"] = skillInfo
	shushumain.ReportData(shushuDate)

}

// (弃用)
func (manager *DeskManager) ReJoin(p *Player) {
	if d := manager.getActiveDesk(p); d != nil {
		if err := d.playerJoin(p.session, true, 0); err != nil {
			return
		}

		d.checkStart()
		leftTime := p.expirationTime.Unix() - time.Now().Unix()
		packet := &pb.ReJoinDeskPush{
			TableInfo: &pb.TableInfo{
				RoomCfgId:   d.cfgID,
				RoomUid:     d.deskID,
				MonsterList: []*pb.MonsterInfo{},
				BattleId:    d.BattleId,
			},
			Location:  p.location,
			Direction: p.direction,
			LeftTime:  leftTime,
		}

		// 发送怪物列表
		d.lock.Lock()
		for _, m := range d.monsters {
			packet.TableInfo.MonsterList = append(packet.TableInfo.MonsterList, &pb.MonsterInfo{
				CfgId:     m.cfgId,
				Uid:       m.uid,
				Name:      m.name,
				MaxHp:     m.GetMaxHP(),
				CurHp:     m.displayHP,
				Location:  m.location,
				Direction: m.direction,
			})
		}
		defer d.lock.Unlock()
		p.pushMsg(protocol.RouteReJoinDeskPush, packet)
		p.syncBuff()

		d.syncDeskStatus()

		logger.Debugf("断线重连,加入房间成功 Uid=%d , 重连后房间id=%d , 新的桌子uid= %d", p.uid, p.roomId, p.desk.deskID)
	}
}

// *************************** 	暂留未处理消息   *************************************

// 网络断开后, 重新连接网络
func (manager *DeskManager) ReConnect(s *session.Session, req *protocol.ReConnect) error {
	uid := req.Uid

	// 绑定UID
	if err := s.Bind(uid); err != nil {
		return err
	}

	logger.Infof("玩家重新连接服务器: UID=%d", uid)
	var ip string
	// 设置用户
	p, ok := defaultManager.player(uid)
	if !ok {
		logger.Infof("玩家之前用户信息已被清除，重新初始化用户信息: UID=%d", uid)

		if parts := strings.Split(s.RemoteAddr().String(), ":"); len(parts) > 0 {
			ip = parts[0]
		}
		p = newPlayer(s, uid, req.Name, req.HeadUrl, ip)
		defaultManager.setPlayer(uid, p)
	} else {
		logger.Infof("玩家之前用户信息存在服务器上，替换session: UID=%d", uid)

		// 重置之前的session
		prevSession := p.session
		if prevSession != nil {
			prevSession.Clear()
			prevSession.Close()
		}

		// 绑定新session
		p.bindSession(s)

		// 移除广播频道
		if d := p.desk; d != nil && prevSession != nil {
			d.group.Leave(prevSession)
		}
	}

	return nil
}

// 应用退出后重新进入房间
func (manager *DeskManager) ReEnter(s *session.Session, msg *protocol.ReEnterDeskRequest) error {

	return nil
}

// ************************************************************************************************************
