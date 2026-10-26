package game

import (
	"math"
	"strconv"
	"strings"
	"sync"
	"time"

	"nanoserver/db/model"
	"nanoserver/db/token"
	"nanoserver/global"
	shushumain "nanoserver/internal/shushu"
	"nanoserver/internal/web/api/provider"
	"nanoserver/internal/web/provide"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/errutil"
	"nanoserver/pkg/sensitive"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"

	"github.com/lonng/nano/scheduler"
	"github.com/spf13/viper"

	"github.com/lonng/nano"
	"github.com/lonng/nano/component"
	"github.com/lonng/nano/session"
	log "github.com/sirupsen/logrus"
)

const (
	kickResetBacklog = 8
)

var defaultManager = NewManager()

type (
	Manager struct {
		lock sync.Mutex

		component.Base
		group       *nano.Group                // 广播channel
		players     map[int64]*Player          // 所有玩家
		chKick      chan int64                 // 退出队列
		chReset     chan int64                 // 重置队列
		chRecharge  chan protocol.RechargeInfo // 充值信息
		chAddSource chan protocol.AddSource
		chGmCmd     chan protocol.GmCmd
		history     map[int32]map[int32]map[int64][]*pb.ChatHistory // 历史记录
	}
)

func NewManager() *Manager {
	return &Manager{
		group:       nano.NewGroup("_SYSTEM_MESSAGE_BROADCAST"),
		players:     map[int64]*Player{},
		chKick:      make(chan int64, kickResetBacklog),
		chReset:     make(chan int64, kickResetBacklog),
		chRecharge:  make(chan protocol.RechargeInfo, 32),
		chAddSource: make(chan protocol.AddSource, 32),
		chGmCmd:     make(chan protocol.GmCmd, 32),
		history:     map[int32]map[int32]map[int64][]*pb.ChatHistory{},
	}
}

// 添加到聊天记录
func (m *Manager) addChatHistory(serverID int32, ptype int32, uid int64, msg string, name string, guildId int64) {
	msgg := &pb.ChatHistory{
		Uid:  uid,
		Name: name,
		Time: time.Now().Unix(),
		Msg:  msg,
	}

	serverID32 := int32(serverID)

	if _, exists := defaultManager.history[serverID32]; !exists {
		defaultManager.history[serverID32] = make(map[int32]map[int64][]*pb.ChatHistory)
	}

	if _, exists := defaultManager.history[serverID32][ptype]; !exists {
		defaultManager.history[serverID32][ptype] = make(map[int64][]*pb.ChatHistory)
	}

	if _, exists := defaultManager.history[serverID32][ptype][guildId]; !exists {
		defaultManager.history[serverID32][ptype][guildId] = make([]*pb.ChatHistory, 0)
	}

	defaultManager.history[serverID32][ptype][guildId] = append(defaultManager.history[serverID32][ptype][guildId], msgg)

	if len(defaultManager.history[serverID32][ptype][guildId]) > 30 {
		defaultManager.history[serverID32][ptype][guildId] = defaultManager.history[serverID32][ptype][guildId][1:]
	}
}

// getChatHistory 安全地获取聊天历史记录
func (m *Manager) getChatHistory(serverID int32, ptype int32, guildId int64) []*pb.ChatHistory {
	// 逐层检查map是否存在
	if serverMap, exists := defaultManager.history[serverID]; exists {
		if ptypeMap, exists := serverMap[ptype]; exists {
			if history, exists := ptypeMap[guildId]; exists {
				return history
			}
		}
	}
	// 如果不存在，返回空slice
	return []*pb.ChatHistory{}
}

// 查找聊天记录
func (m *Manager) ChatHistory(s *session.Session, req *pb.ChatHistoryRequest) error {
	res := &pb.ChatHistoryResponse{Code: 0}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	if req.Channel == 0 {
		// 获取所有频道的历史记录
		for i := 1; i <= 6; i++ {
			var guildId int64
			if i == 2 {
				guildId = 0 // 世界频道使用guildId=0
			} else {
				guildId = p.guildId
			}

			history := m.getChatHistory(p.serverID, int32(i), guildId)
			res.ChannelHistories = append(res.ChannelHistories, &pb.ChatHistoryResponse_ChatChannelHistory{
				Channel: int32(i),
				History: history,
			})
		}
	} else {
		// 获取指定频道的历史记录
		var guildId int64
		if req.Channel == 2 {
			guildId = 0 // 世界频道使用guildId=0
		} else {
			guildId = p.guildId
		}

		history := m.getChatHistory(p.serverID, req.Channel, guildId)
		res.ChannelHistories = append(res.ChannelHistories, &pb.ChatHistoryResponse_ChatChannelHistory{
			Channel: req.Channel,
			History: history,
		})
	}

	return s.Response(res)
}

func (m *Manager) AfterInit() {
	session.Lifetime.OnClosed(func(s *session.Session) {
		m.group.Leave(s)
	})

	// 处理踢出玩家和重置玩家消息(来自http)
	scheduler.NewTimer(time.Second, func() {
	ctrl:
		for {
			select {
			case uid := <-m.chKick:
				p, ok := defaultManager.player(uid)
				if !ok {
					logger.Errorf("玩家不存在 userId: %d", uid)
					continue
				}
				if p.session == nil {
					logger.Errorf("玩家不在线 userId: %d", uid)
					continue
				}
				p.session.Close()
				logger.Infof("踢出玩家, UID=%d", uid)
			case uid := <-m.chReset:
				p, ok := defaultManager.player(uid)
				if !ok {
					return
				}
				if p.session != nil {
					logger.Errorf("玩家正在游戏中，不能重置 userId: %d", uid)
					return
				}
				p.desk = nil
				logger.Infof("重置玩家, UID=%d", uid)

				// case ri := <-m.chRecharge:
				// 	player, ok := m.player(ri.Uid)
				// 	if ok {
				// 		player.recharge4vip(ri.Coin)
				// 	} else {
				// 		async.Run(func() {
				// 			u, err := db.QueryUser(ri.Uid)
				// 			if err != nil {
				// 				logger.Errorf("玩家chRecharge同步错误,userId: %d Error=%v", ri.Uid, err)
				// 				return
				// 			}
				// 			u.Recharge += ri.Coin

				// 			if err := db.UpdateUser(u); err != nil {
				// 				logger.Errorf("保存角色信息错误, Error=%v", err)
				// 				return
				// 			}
				// 		})
				// 	}

			case ads := <-m.chAddSource:
				player, ok := m.player(ads.Uid)
				if ok {
					items := []global.IdNum{}
					for _, source := range ads.Source {
						if source.Num == 0 {
							continue
						}
						if source.Num > 0 {
							items = append(items, global.IdNum{ID: source.Id, Num: int32(source.Num)})
							if ads.ShopType > constant.ShopTypeUnknown && ads.ShopType < constant.ShopTypeAll {
								if shopType, ok := constant.ShopMap[ads.ShopType]; ok {
									player.tasks.dispatch(shopType, 0, 1)
								}
							}
						} else {
							player.takeSource(source.Id, int64(math.Abs(float64(source.Num))), constant.ItemDelWeb)
						}
					}
					if err := player.checkAddSourceBatch(items); err == nil {
						player.addSourceBatch(items, constant.ItemAddWeb)
					} else {
						attachment := []protocol.Attachment{}
						for _, item := range items {
							attachment = append(attachment, protocol.Attachment{UserPropsId: int64(item.ID), Num: int64(item.Num)})
						}
						player.SendMail(constant.ItemAddGame, attachment)
						logger.Errorf("Web Interface 发放道具失败,包裹空间不足,邮件发送 userId:%d  道具数量 :%d", ads.Uid, len(items))
					}

				} else {
					logger.Errorf("Web Interface 发放道具失败,玩家未找到userId:%d  SourceNum : %d", ads.Uid, len(ads.Source))
				}

			case cmd := <-m.chGmCmd:
				m.GmCmd(cmd)

			default:
				break ctrl
			}
		}
	})

	// 每分钟保存一次数据
	scheduler.NewTimer(time.Minute, func() {
		for _, v := range m.players {
			if v.curState != PlayerStatusIdle {
				continue
			}
			if !v.bDirty {
				continue
			}
			v.asyncSaveDb()
		}
	})
	scheduler.NewTimer(time.Second, func() {
		kickIds := []int64{}
		for _, v := range m.players {
			// if v.curState >= PlayerStatusDisconnect && v.curState < PlayerStatusRobot {
			// 	//离线10分钟加入离线机器人
			// 	if time.Now().After(v.disconnectTime.Add(10 * time.Minute)) {
			// 		m.offline(v.uid)
			// 	}
			// }
			if v.curState == PlayerStatusRobot {
				// 离线5天清理内存
				if time.Now().After(v.disconnectTime.Add(5 * 24 * 60 * time.Minute)) {
					kickIds = append(kickIds, v.uid)
					continue
				}
			}
			v.update()
		}
		// 离线超过x分钟清理内存
		for _, v := range kickIds {
			m.offline(v)
		}
	})
}

func (m *Manager) TimerFor0Oclock() {
	logger.Println("触发每日0点任务")
	for _, v := range m.players {
		if v.curState >= PlayerStatusDisconnect {
			continue
		}
		v.newDay()
	}
	// 数据库清理
	// db.DeleteUserItem()
}

// 每日凌晨清楚当日在线时长，累加在线时长
func (m *Manager) DayOnlineTime() {
	logger.Println("触发每日凌晨任务")
}

func (m *Manager) player(uid int64) (*Player, bool) {
	m.lock.Lock()
	defer m.lock.Unlock()

	p, ok := m.players[uid]

	return p, ok
}

func (m *Manager) setPlayer(uid int64, p *Player) {
	if _, ok := m.players[uid]; ok {
		log.Warnf("玩家已经存在，正在覆盖玩家， UID=%d", uid)
	}
	m.players[uid] = p
}

func (m *Manager) sessionCount() int {
	return len(m.players)
}

func (m *Manager) offline(uid int64) {
	if _, ok := m.player(uid); ok {
		delete(m.players, uid)
		log.Infof("玩家: %d 从在线列表中删除, 剩余：%d", uid, len(m.players))
	}
}

// (弃用)
func (m *Manager) reconnect(p *Player) {
	logger.Debugf("断线重连 Uid=%d , 断线前房间ID=%d", p.uid, p.roomId)
	if p.desk != nil {
		p.desk.playerKick(p, constant.GameOverType)
		return
	}
	if room, ok := roomManager.rooms[p.roomId]; ok {
		room.ReJoin(p)
	}
}

// web interface
// 检查是否能送道具
func (m *Manager) CheckSourceAdd(source *protocol.AddSource) error {
	p, ok := m.player(source.Uid)

	if !ok {
		return errutil.ErrPlayerNotFound
	}
	items := []global.IdNum{}
	for _, v := range source.Source {
		items = append(items, global.IdNum{ID: v.Id, Num: int32(v.Num)})
	}
	return p.checkAddSourceBatch(items)
}

// 检查道具数量
func (m *Manager) CheckSourceNum(source *protocol.AddSource) error {
	p, ok := m.player(source.Uid)

	if !ok {
		return errutil.ErrPlayerNotFound
	}

	for _, v := range source.Source {
		if v.Num <= 0 {
			logger.Warningf("Web CheckSourceNum 参数错误 ID:%d Num:%d", v.Id, v.Num)
			return errutil.ErrIllegalParameter
		}
		if !p.checkTakeSource(v.Id, v.Num) {
			return errutil.ErrItemNotEnough
		}
	}
	return nil
}

// 增加特权
func (m *Manager) PrivilegeAdd(shopType int32, uid int64, privilegeId int32, expirationTime time.Time) error {
	p, ok := m.player(uid)

	if !ok {
		return errutil.ErrPlayerNotFound
	}
	return p.addPrivilege(shopType, privilegeId, expirationTime)
}

// Client Message Route
func (m *Manager) Login(s *session.Session, req *pb.LoginToGameServerRequest) error {
	uid := req.Uid
	s.Bind(uid)

	logger.Debugf("玩家: %d 登录: %+v", uid, req)

	res := &pb.LoginToGameServerResponse{
		Uid:        s.UID(),
		HeadUrl:    req.HeadUrl,
		Name:       req.Name,
		Timestamp:  time.Now().Unix(),
		GameStatus: 0,
	}
	if !global.LoginOpen {
		res.Code = -1
		res.Error = errutil.ServerIsBusy
		return s.Response(res)
	}

	// 校验token
	tokenFactory := token.CreateUserFactory()
	if check := tokenFactory.IsEffective(req.Token); !check {
		logger.Debugf("Token 无效,校验失败: Uid %d 登录: %s", uid, req.Token)
		res.Code = -2
		res.Error = errutil.ErrInvalidToken.Error()
		return s.Response(res)
	}
	if userInfo, err := tokenFactory.ParseToken(req.Token); err != nil {
		logger.Debugf("Token 解析失败: Uid %d 登录: %s", uid, req.Token)
		res.Code = -3
		res.Error = errutil.ErrInvalidToken.Error()
		return s.Response(res)
	} else if userInfo.UserId != req.Uid {
		logger.Debugf("Token UID不一致,校验失败: Uid %d 登录: %s", uid, req.Token)
		res.Code = -4
		res.Error = errutil.ErrInvalidToken.Error()
		return s.Response(res)
	}

	if p, ok := m.player(uid); !ok {

		logger.Debugf("玩家: %d 登录: 缓存无数据，加载数据  ", uid)
		var ip string
		if parts := strings.Split(s.RemoteAddr().String(), ":"); len(parts) > 0 {
			ip = parts[0]
		}

		p = newPlayer(s, uid, req.Name, req.HeadUrl, ip)
		p.bindSession(s)

		if !p.syncFromDB() {
			res.Code = -1
			res.Error = errutil.ErrDBOperation.Error()
			logger.Errorf("玩家: %d 登录失败,数据加载失败", uid)
			return s.Response(res)
		}

		s.Response(res)
		// 隔天登录
		// 查询他是第几天
		p.syncUserDay()
		if p.lastLoginAt.Format(time.DateOnly) != time.Now().Format(time.DateOnly) {
			p.newDay()
		}

		p.online()

		m.setPlayer(uid, p)
		p.reset()

	} else {
		// 重置之前的session
		if prevSession := p.session; prevSession != nil && prevSession != s {
			logger.Warningf("玩家:%d 已经在线,顶号", uid)
			// 告知这个用户，他被顶号了
			packet := &pb.CompulsoryWithdrawalPush{
				Status: pb.CompulsoryWithdrawalPush_WithdrawalStatus(pb.CompulsoryWithdrawalPush_Pushoff),
			}
			p.pushMsg(protocol.RouteCompulsoryWithdrawalPush, packet)
			// 移除广播频道
			m.group.Leave(prevSession)
			// 如果之前房间存在，则退出来
			if p, err := playerWithSession(prevSession); err == nil && p != nil && p.desk != nil && p.desk.group != nil {
				p.desk.playerExit(prevSession, true)
				p.setPlayerStatus(PlayerStatusDisconnect)
			}
			time.AfterFunc(time.Millisecond*10, func() {
				prevSession.Clear()
				prevSession.Close()
			})
		}

		// 绑定新session
		p.bindSession(s)
		// 查询他是第几天
		p.syncUserDay()
		// 隔天登录
		if p.lastLoginAt.Format(time.DateOnly) != time.Now().Format(time.DateOnly) {
			p.newDay()
		}
		p.OfflineAdd()
		p.online()

		// 断线重连
		if p.roomId != 0 && p.desk != nil && p.curState == PlayerStatusDisconnect && time.Now().Before(p.disconnectTime.Add(time.Second*15)) {
			res.GameStatus = int32(PlayerStatusPlaying)
			s.Response(res)
			defer func() {
				p.desk.playerRejoin(p)
			}()
			p.logger.Debugf("玩家: %d : %s 断线重连: 房间号 %d ", uid, p.name, p.roomId)
		} else {
			if p.desk != nil {
				p.desk.playerKick(p, constant.GameOverTypeTime)

				p.logger.Debugf("玩家: %d : %s 断线重连: 房间号 %d 超过短线重连时间", uid, p.name, p.roomId)
			}
			p.reset()
			s.Response(res)
			p.logger.Debugf("玩家: %d : %s 登录成功，读取缓存内数据，进入大厅", uid, p.name)
		}
	}

	// 添加到广播频道
	m.group.Add(s)

	return nil
}

func (m *Manager) CheckOrder(s *session.Session, msg *protocol.CheckOrderReqeust) error {
	log.Infof("%+v", msg)

	return s.Response(&protocol.CheckOrderResponse{
		FangKa: 20,
	})
}

// 包裹信息
func (m *Manager) GetBag(s *session.Session, _ []byte) error {
	p, err := playerWithSession(s)
	if err != nil {
		return err
	}

	packet := &pb.GetBagResponse{ItemList: &pb.Bag{ItemList: []*pb.ItemInfo{}}}

	// 背包检测道具过期
	for _, item := range p.bag.GetItemList() {
		itemCfg, err := global.GetItemCfg(item.CfgId)
		if err != nil {
			p.logger.Errorf("道具配置不存在， userId: %d, itemid: %d", p.Uid(), item.CfgId)
			continue
		}
		if itemCfg.Timeliness.Type == constant.ItemTimelinessShiChang {
			expired := time.Unix(item.CreateTime, 0).Add(time.Duration(itemCfg.Timeliness.Time) * time.Hour * 24)
			if expired.Before(time.Now()) {
				p.takeSourceByUid(item.Uid, 1, constant.ItemDelExpired)
				break
			}
		} else if itemCfg.Timeliness.Type == constant.ItemTimelinessQuJian {
			if time.Now().Unix() < itemCfg.Timeliness.StarTime || time.Now().Unix() > itemCfg.Timeliness.EndTime {
				p.takeSourceByUid(item.Uid, 1, constant.ItemDelExpired)
				break
			}
		}

		// 过滤穿戴装备
		if p.hasEquipment(item.Uid) {
			continue
		}

		// 过滤宝箱候选区装备
		if p.hasHomeOpenBox(item.Uid) {
			continue
		}
		packet.ItemList.ItemList = append(packet.ItemList.ItemList, item)
	}

	return s.Response(packet)
}

// 使用道具
func (m *Manager) ItemUse(s *session.Session, req *pb.ItemUseRequest) error {
	res := &pb.ItemUseResponse{ItemList: []*pb.ItemInfo{}}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	itemInfo := p.getItemByUid(req.ItemUid)
	if itemInfo == nil {
		res.Code = -2
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}

	itemCfg, err := global.GetItemCfg(itemInfo.CfgId)
	if err != nil {
		res.Code = -2
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}

	if p.level < int32(itemCfg.Levellimit) {
		res.Code = -3
		res.Error = errutil.ErrLevel.Error()
		return s.Response(res)
	}

	if !p.checkTakeSourceByUid(req.ItemUid, int64(req.Num)) {
		res.Code = -4
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}

	if itemUseCfg, ok := global.ItemUseCfg[itemInfo.CfgId]; ok {
		var itembagcheck, equipbagcheck bool
		for _, v := range itemUseCfg.GiveGoods {
			if getItemCfg, err := global.GetItemCfg(v.ID); err == nil {
				if getItemCfg.SonType == constant.ItemTypeEquipment {
					equipbagcheck = true
				}

				if getItemCfg.SonType == constant.ItemTypeMaterial ||
					getItemCfg.SonType == constant.ItemTypeGem ||
					getItemCfg.SonType == constant.ItemTypeConsumable {
					itembagcheck = true
				}
			}
		}

		if equipbagcheck && p.bagLeftCount(pb.ItemType_EQUIPMENT) == 0 {
			res.Code = -6
			res.Error = errutil.ErrBagFull.Error()
			return s.Response(res)
		}

		if itembagcheck && p.bagLeftCount(pb.ItemType_IN_BAG_DISPOSABLE) == 0 {
			res.Code = -6
			res.Error = errutil.ErrBagFull.Error()
			return s.Response(res)
		}
	}

	reward := []global.IdNum{}
	buffs := []global.IdKeyNum{}
	useNum := req.Num // 实际使用数量
	if itemUseCfg, ok := global.ItemUseCfg[itemInfo.CfgId]; ok {
		// 检查使用道具的上限
		for _, v := range itemUseCfg.GiveGoods {
			if v.ID == constant.ItemTypeEnergy {
				if p.energy >= int64(global.GlobalCfg.BaseValue.Energy.Maxvalue) {
					res.Code = -6
					res.Error = errutil.EnergyMax
					return s.Response(res)
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

		buffs = itemUseCfg.Buffeffect
		// 如身上有高级buff，则不添加低级buff
		if len(buffs) > 0 {
			for _, ib := range buffs {
				for _, v := range p.buffs {
					if ib.ID == v.Id {
						for _, attr := range v.BuffStats.Attrs {
							if ib.Key == int32(pb.Attribute_EQUIP_STAR_PERCENTAGE) && attr >= ib.Num {
								logger.Warnf("身上有高级buff，不能添加低级buff, buffId: %d, buffKey: %d, buffNum: %d, uid: %d", ib.ID, ib.Key, ib.Num, p.Uid())
								res.Error = errutil.ErrBuffAlreadyExist.Error()
								return s.Response(res)
							}
						}
					}
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
	res.ItemList = append(res.ItemList, &pb.ItemInfo{Uid: req.ItemUid, Num: int64(useNum), CfgId: itemInfo.CfgId, CreateTime: itemInfo.CreateTime})

	// 升星石
	num, err2 := p.IsStarsLevel(itemInfo.CfgId, useNum)
	if err2 != nil && num == 0 {
		res.Code = -6
		res.Error = err2.Error()
		return s.Response(res)
	}
	// 剩余的
	useNum = num
	// 命格
	_, num, err = p.IsFate(itemInfo.CfgId, useNum)
	if err != nil && num == 0 {
		res.Code = -7
		res.Error = err.Error()
		return s.Response(res)
	}
	// 剩余的
	useNum = num
	// 删除道具
	p.takeSourceByUid(req.ItemUid, int64(useNum), constant.ItemDelGame)

	// 发放奖励
	if len(reward) > 0 {
		if err = p.checkAddSourceBatch(reward); err == nil {
			p.addSourceBatch(reward, constant.ItemAddDisintegrate)
		} else {
			attachment := []protocol.Attachment{}
			for _, item := range reward {
				attachment = append(attachment, protocol.Attachment{UserPropsId: int64(item.ID), Num: int64(item.Num)})
			}
			p.SendMail(constant.ItemAddDisintegrate, attachment)
		}
		// for _, v := range reward {
		// 	p.addSource(v.ID, int64(v.Num), constant.ItemAddGame)
		// 	res.ItemList = append(res.ItemList, &pb.ItemInfo{CfgId: v.ID, Num: int64(v.Num)})
		// }
	}

	// 道具buff
	for _, ib := range buffs {
		bufStats := &Stats{Attrs: make(map[pb.Attribute]int32)}
		bufStats.setStats(pb.Attribute(ib.Key), ib.Num)
		p.AddBuff(ib.ID, p, 0, bufStats, 0, 0)
	}

	return s.Response(res)
}

// 销毁道具
func (m *Manager) ItemDestroy(s *session.Session, req *pb.ItemDestroyRequest) error {
	res := &pb.ItemDestroyResponse{Item: &pb.ItemInfo{Uid: req.ItemUid, Num: int64(req.Num)}}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	if p.isOnEquip(req.ItemUid) {
		res.Code = -3
		res.Error = errutil.EquipOnUse
		return s.Response(res)
	}

	if !p.checkTakeSourceByUid(req.ItemUid, int64(req.Num)) {
		res.Code = -2
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}

	var getitem []global.KeyValue

	itemInfo := p.getItemByUid(req.ItemUid)
	if itemInfo == nil {
		res.Code = -3
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}
	itemCfg, err := global.GetItemCfg(itemInfo.CfgId)
	if err != nil {
		res.Code = -4
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}

	for _, des := range itemCfg.Decompose {
		bFind := false
		for _, gi := range getitem {
			if gi.Key == des.Key {
				gi.Value += des.Value
				bFind = true
				break
			}
		}
		if !bFind {
			getitem = append(getitem, des)
		}
	}

	p.takeSourceByUid(req.ItemUid, int64(req.Num), constant.ItemDelDistroy)
	for _, v := range getitem {
		p.addSource(v.Key, int64(v.Value*req.Num), constant.ItemAddDistroy)
	}
	return s.Response(res)
}

// 分解道具
func (m *Manager) ItemDisassembly(s *session.Session, req *pb.ItemDisassemblyRequest) error {
	res := &pb.ItemDisassemblyResponse{}
	res.ItemUids = req.ItemUids

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	var getitem []global.KeyValue
	for _, v := range req.ItemUids {
		if !p.checkTakeSourceByUid(v, 1) {
			p.boxResIntoBag(v)
			continue
			// res.Code = -2
			// res.Error = errutil.ErrItemNotEnough.Error()
			// return s.Response(res)
		}

		if p.isOnEquip(v) {
			p.boxResIntoBag(v)
			res.Code = -3
			res.Error = errutil.EquipOnUse
			return s.Response(res)
		}

		itemInfo := p.getItemByUid(v)
		if itemInfo == nil {
			continue
		}
		itemCfg, err := global.GetItemCfg(itemInfo.CfgId)
		if err != nil {
			continue
		}

		for _, des := range itemCfg.Decompose {
			bFind := false
			for i, gi := range getitem {
				if gi.Key == des.Key {
					if itemCfg.Quality > 0 && itemCfg.Quality <= 7 {
						getitem[i].Value += des.Value * int32(global.GlobalCfg.Equipenhances[itemCfg.Quality-1].Value)
						bFind = true
						break
					}
				}
			}
			if !bFind {
				getitem = append(getitem, des)
			}
		}
		// 如果分解的是首页的装备，需要把首页的装备删掉
		p.boxResIntoBag(v)
	}

	delSource := []global.UUIdNum{}
	for _, v := range req.ItemUids {
		delSource = append(delSource, global.UUIdNum{UID: v, Num: 1})
	}
	p.takeSourceByUidBatch(delSource, constant.ItemDelDisintegrate)

	addSource := []global.IdNum{}
	for _, v := range getitem {
		addSource = append(addSource, global.IdNum{ID: v.Key, Num: v.Value})
	}
	p.addSourceBatch(addSource, constant.ItemAddDisintegrate)

	return s.Response(res)
}

// 自动分解
func (m *Manager) AutoDisassembly(s *session.Session, req *pb.AutoDisassemblyRequest) error {
	p, err := playerWithSession(s)
	if err != nil {
		return nil
	}

	res := &pb.AutoDisassemblyResponse{}
	if p.vip < global.GlobalCfg.BaseValue.Autodecompose.Viplevel && p.level < global.GlobalCfg.BaseValue.Autodecompose.Rolelevel {
		res.Code = -1
		res.Error = errutil.ErrLevel.Error()
		return s.Response(res)
	}

	if req.Info.Level <= 0 || req.Info.Quality <= pb.ItemQuality_INVALID_Quality || req.Info.Quality > pb.ItemQuality_RED {
		res.Code = -1
		res.Error = errutil.ErrIllegalParameter.Error()
		return s.Response(res)
	}

	p.ADLevel = req.Info.Level
	p.ADQuality = int32(req.Info.Quality)
	p.ADOpen = req.Info.Open
	p.ADHighCombat = req.Info.HighCombat
	p.ADLevelSwitch = req.Info.LevelSwitch
	p.ADQualitySwitch = req.Info.QualitySwitch

	res.Open = p.ADOpen
	res.Level = p.ADLevel
	res.Quality = pb.ItemQuality(p.ADQuality)
	res.LevelSwitch = p.ADLevelSwitch
	res.QualitySwitch = p.ADQualitySwitch
	res.HighCombat = p.ADHighCombat

	return s.Response(res)
}

// 穿脱装备
func (m *Manager) ChangeEquip(s *session.Session, req *pb.ChangeEquipRequest) error {
	res := &pb.ChangeEquipResponse{OpType: req.OpType, Slot: req.Slot, PartnerSlotId: req.PartnerSlotId, Item: &pb.ItemInfo{}, OldItem: &pb.ItemInfo{}}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	if req.Slot <= pb.EquipSlot_INVALID_EquipSlot || req.Slot >= pb.EquipSlot_MaxEquipSlot {
		res.Code = -2
		res.Error = errutil.ErrItemNotEnough.Error()
		logger.Debugf("玩家: %d 穿脱装备伙伴失败,道具不存在了, uid=%d", p.Uid(), req.ItemUid)
		return s.Response(res)
	}
	ItemInfo := p.getItemByUid(req.ItemUid)
	if ItemInfo == nil {
		logger.Debugf("玩家: %d 穿脱装备失败,道具不存在, uid=%d", p.Uid(), req.ItemUid)
		res.Code = -3
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}
	res.Item = ItemInfo
	if req.PartnerSlotId == 0 {
		// 前端要求如果身上有装备，那么老装备就是身上装备，如果身上没装备，老装备就是自身
		if p.equips[req.Slot].ItemId != 0 {
			res.OldItem = p.getItemByUid(p.equips[req.Slot].ItemId)
		} else {
			res.OldItem = res.Item
		}
	} else {
		// 前端要求如果身上有装备，那么老装备就是身上装备，如果身上没装备，老装备就是自身
		partnerSlotInfo := p.getPartnerSlotInfo(req.PartnerSlotId)
		if partnerSlotInfo == nil {
			res.Code = -4
			res.Error = errutil.ErrItemNotEnough.Error()
			logger.Debugf("玩家: %d 穿脱装备伙伴失败,道具不存在, uid=%d", p.Uid(), req.ItemUid)
			return s.Response(res)
		}
		if len(partnerSlotInfo.Equips) == 0 {
			res.Code = -5
			res.Error = errutil.ErrItemNotEnough.Error()
			logger.Debugf("玩家: %d 穿脱装备伙伴失败,道具不存在了, uid=%d", p.Uid(), req.ItemUid)
			return s.Response(res)
		}
		equipSlot := partnerSlotInfo.Equips[req.Slot]
		if equipSlot.ItemId != 0 {
			endInfo := p.getItemByUid(equipSlot.ItemId)
			if endInfo == nil {
				res.Code = -6
				res.Error = errutil.ErrItemNotEnough.Error()
				logger.Debugf("玩家: %d 穿脱装备伙伴失败了,道具不存在, uid=%d", p.Uid(), req.ItemUid)
				return s.Response(res)
			}
			res.OldItem = endInfo
		} else {
			res.OldItem = res.Item
		}
	}
	if err = p.changeEquip(req.OpType, req.Slot, req.ItemUid, req.PartnerSlotId); err != nil {
		res.Code = -7
		res.Error = err.Error()
		return s.Response(res)
	}
	if req.PartnerSlotId == 0 {
		p.SyncUserEquipsInfo()
	} else {
		p.SyncUserPartnerSlot()
	}

	p.refreshStats()
	return s.Response(res)
}

// 完成任务
func (m *Manager) TaskComplate(s *session.Session, req *pb.TaskComplateRequest) error {
	res := &pb.TaskComplateResponse{}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	if err = p.tasks.complate(req.TaskId); err != nil {
		res.Code = -2
		res.Error = err.Error()
	}

	return s.Response(res)
}

// 更新角色姓名
func (m *Manager) ChangeRoleName(s *session.Session, req *pb.ChangeRoleNameRequest) error {
	res := &pb.ChangeRoleNameResponse{}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	name, err := p.changeRoleName(req.RoleName)
	if err != nil {
		res.Code = -2
		res.Error = err.Error()
		res.RoleName = name
		return s.Response(res)
	}
	res.RoleName = name
	return s.Response(res)
}

// 洗炼
func (m *Manager) EquipRefine(s *session.Session, req *pb.EquipRefineRequest) error {
	res := &pb.EquipRefineResponse{NewStats: &pb.Stats{}}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	if err = p.equipRefine(req.ItemUid, req.Attr, req.Random); err != nil {
		res.Code = -2
		res.Error = err.Error()
		return s.Response(res)
	}

	res.NewStats = &p.lastEquipRefine.newStat

	return s.Response(res)
}

func (m *Manager) EquipRefineReplace(s *session.Session, req *pb.EquipRefineReplaceRequest) error {
	res := &pb.EquipRefineReplaceResponse{}

	p, err := playerWithSession(s)
	if err != nil {
		return err
	}

	if item, err := p.equipRefineReplace(req.Replace); err != nil {
		res.Code = -2
		res.Error = err.Error()
		return s.Response(res)
	} else {
		res.Item = item
	}

	return s.Response(res)
}

// 聊天
func (m *Manager) Chat(s *session.Session, req *pb.ChatRequest) error {
	res := &pb.ChatResponse{}
	p, err := playerWithSession(s)
	if err != nil {
		return err
	}

	if strings.ToLower(viper.GetString("core.env")) == constant.EnvProd {
		if err := provider.WeChatV2.CheckMessageSecurity(p.accountName, req.Context); err != nil {
			res.Code = -1
			res.Error = err.Error()
			return s.Response(res)
		}
	}
	// gm指令
	if len(req.Context) > 0 && req.Context[0] == '@' && strings.ToLower(viper.GetString("core.env")) != constant.EnvProd {
		cmd := strings.Split(req.Context, " ")
		var id int
		var num int
		var err error
		switch cmd[0] {
		case "@add":
			if len(cmd) == 3 {
				if id, err = strconv.Atoi(cmd[1]); err != nil {
					logger.Debugf("@参数错误 cmd=%s ", req.Context)
					res.Code = -1
					break
				}
				if num, err = strconv.Atoi(cmd[2]); err != nil {
					logger.Debugf("@参数错误 cmd=%s ", req.Context)
					res.Code = -2
					break
				}
				p.addSource(int32(id), int64(num), constant.ItemAddGM)
			}
		case "@clear":
			newbag := &pb.Bag{ItemList: []*pb.ItemInfo{}}
			packet := pb.TakeSourcePush{Item: []*pb.ItemInfo{}}
			for _, v := range p.bag.ItemList {
				if p.hasEquipment(v.Uid) {
					newbag.ItemList = append(newbag.ItemList, v)
					continue
				}
				packet.Item = append(packet.Item, &pb.ItemInfo{CfgId: v.CfgId, Uid: v.Uid, Num: v.Num})
			}
			p.pushMsg(protocol.RouteTakeSourcePush, &packet)
			p.bag = newbag
		case "@god":
			godLevel := int(global.GlobalCfg.BaseValue.Rolelevel.Maxlevel)
			if len(cmd) == 2 {
				if godLevel, err = strconv.Atoi(cmd[1]); err != nil {
					logger.Debugf("@参数错误 cmd=%s ", req.Context)
					res.Code = -3
					break
				}
			}

			if godLevel > int(global.GlobalCfg.BaseValue.Rolelevel.Maxlevel) {
				godLevel = int(global.GlobalCfg.BaseValue.Rolelevel.Maxlevel)
			}
			if godLevel < int(p.level) {
				logger.Debugf("@参数错误 cmd=%s ", req.Context)
				res.Code = -4
				break
			}

			roleCfg, err := global.GetRoleCfg(int32(p.job), int32(godLevel))
			if err != nil {
				logger.Debugf("@参数错误 cmd=%s ", req.Context)
				res.Code = -5
				break
			}

			// p.coin = 200000
			// p.soul = 0
			// p.coupon = 0
			p.tokens[constant.ItemTypeCoin] = 200000
			p.tokens[constant.ItemTypeSoul] = 10
			p.tokens[constant.ItemTypeCoupon] = 10
			p.tokens[constant.ItemTypeTrialVoucher] = 120

			p.level = int32(godLevel - 1)
			p.exp = roleCfg.NeedExp
			if p.level >= global.SwitchCfg[ModuleDestinyFunction] {
				p.fateLevel = int32(godLevel)
			}

			// p.vip = 3
			p.energy = 120
			p.levelUp()

			for _, v := range p.partnerSlots {
				if partner := p.getPartnerInfoBySlotId(v.SlotId); partner != nil {
					partner.level = int32(godLevel)
					partner.SyncUserPartner()
				}
			}

			items := []global.IdNum{}
			for _, v := range global.ItemCfg {
				if v.Itemlevel == int32(math.Min(15, 15*float64(godLevel)/float64(global.GlobalCfg.BaseValue.Rolelevel.Maxlevel))) {
					items = append(items, global.IdNum{ID: v.ID, Num: 15})
				}
				if v.MainType != 5 {
					continue
				}
				if v.Occupylimit != 0 && v.Occupylimit != int(p.job) {
					continue
				}
				if v.Levellimit != int(p.level/10*10) {
					continue
				}

				items = append(items, global.IdNum{ID: v.ID, Num: 1})
			}
			p.addSourceBatch(items, constant.ItemAddGM)

			// if p.level >= global.SwitchCfg[ModuleEquipmentSystemSoulFB] {
			// 	for i := pb.EquipSlot_Weapon; i < pb.EquipSlot_MaxEquipSlot; i++ {
			// 		p.genstoneChangeAll(i, 0)
			// 	}
			// }

			for i := 1; i < len(p.equips); i++ {
				if p.level >= global.SwitchCfg[ModuleZhuangBeiShengXing] {
					p.equips[i].Stars = int32(math.Min(30, 30*float64(godLevel)/float64(global.GlobalCfg.BaseValue.Rolelevel.Maxlevel-int32(global.SwitchCfg[ModuleZhuangBeiQiangHua]))))
				}
				if p.level >= global.SwitchCfg[ModuleZhuangBeiQiangHua] {
					p.equips[i].Enhance = p.level / 2 // int32(math.Min(300, 300*float64(int32(godLevel)-global.SwitchCfg[ModuleZhuangBeiQiangHua])/float64(global.GlobalCfg.BaseValue.Rolelevel.Maxlevel-int32(global.SwitchCfg[ModuleZhuangBeiQiangHua]))))
				}

				for _, parslot := range p.partnerSlots {
					parslot.Equips[i].Stars = p.equips[i].Stars
					parslot.Equips[i].Enhance = p.equips[i].Enhance
				}

				for _, v := range p.bag.ItemList {
					if equipCfg, err := global.GetItemCfg(v.CfgId); err == nil {
						if equipCfg.Equiptype == i {
							p.equips[i].ItemId = v.Uid
						}
					}
				}
			}

			p.skillsSlot = [pb.SkillsSlot_MaxSkillsSlot]int32{}
			for i := 0; i < len(p.skills); i++ {
				if scfg, err := global.GetSkillCfg(p.skills[i].CfgId); err == nil {
					if scfg.Level == 0 || scfg.ActiveorPassive == PassiveSkill {
						continue
					}
					if p.level >= global.SwitchCfg[ModuleJiNengShengJi] {
						p.skills[i].CfgId = scfg.SkillID*100 + int32(math.Min(30, 30*float64(godLevel)/float64(global.GlobalCfg.BaseValue.Rolelevel.Maxlevel)))
					}
					if p.level >= global.SwitchCfg[ModuleJiNengJinJie] {
						p.skills[i].AdvancedLevel = int32(math.Min(6, 6*float64(godLevel)/float64(global.GlobalCfg.BaseValue.Rolelevel.Maxlevel)))
					}
					if i < int(pb.SkillsSlot_MaxSkillsSlot) && i != 0 {
						p.skillsSlot[i] = p.skills[i].CfgId
					}

				}
			}
		case "@addpartner":
			if len(cmd) == 2 {
				if id, err = strconv.Atoi(cmd[1]); err != nil {
					logger.Debugf("@参数错误 cmd=%s ", req.Context)
					res.Code = -1
					break
				}

				if err := p.addPartner(int32(id)); err == nil {
					logger.Debugf("@添加伙伴成功 cmd=%s partnerId=%d ", req.Context, id)
				}
			} else {
				for _, v := range global.PartnerCfg {
					if err := p.addPartner(v.Cid); err == nil {
						logger.Debugf("@添加伙伴成功 cmd=%s  partnerId=%d ", req.Context, v.Cid)
					}
				}
				for _, v := range p.partners {
					v.level = 80
					v.star = 20

					v.learnSkill()

					v.SyncUserPartner()
				}
			}
		case "@addpet":
			if len(cmd) == 3 {
				if id, err = strconv.Atoi(cmd[1]); err != nil {
					logger.Debugf("@参数错误 cmd=%s ", req.Context)
					res.Code = -1
					break
				}
				if num, err = strconv.Atoi(cmd[2]); err != nil {
					logger.Debugf("@参数错误 cmd=%s ", req.Context)
					res.Code = -2
					break
				}
				if _, err := p.addPet(int32(id), 0, 0, int32(num), 0, "gm"); err == nil {
					logger.Debugf("@添加宠物成功 cmd=%s petId=%d ", req.Context, id)
				}
			}
		case "@setbat":
			if len(cmd) == 2 {
				if num, err = strconv.Atoi(cmd[1]); err != nil {
					logger.Debugf("@参数错误 cmd=%s ", req.Context)
					res.Code = -1
				}
				// if num, err = strconv.Atoi(cmd[2]); err != nil {
				// 	logger.Debugf("@参数错误 cmd=%s ", req.Context)
				// 	res.Code = -2
				// 	break
				// }
				id := num / 100000
				cfg := global.GetBattleMapCfg(int32(num))
				if cfg == nil {
					logger.Debugf("@参数错误 cmd=%s ", req.Context)
					res.Code = -3
					break
				}
				if id == int(constant.BattleMap) {
					p.InstanceData.BattleMap.BattleId = int32(num)
				} else if id == int(constant.BossMap) {
					p.InstanceData.BossMap.BattleId = int32(num)
				} else if id == int(constant.TrialTowerMap) {
					p.InstanceData.TrialTowerMap.BattleId = int32(num)
				} else if id == int(constant.RelicMap) {
					for _, v := range global.BattleCfg {
						if v.CopyType != int32(id) {
							continue
						}
						p.InstanceData.RelicMap.Evaluation = append(p.InstanceData.RelicMap.Evaluation, global.KeyValue{Key: v.UID, Value: 3})
					}
					p.InstanceData.RelicMap.BattleId = int32(num)
				}
			}
		}

		p.syncUserBase()
		p.SyncUserAdvance()
		p.syncUserSkillSlot()
		p.refreshStats()
		p.SyncUserPartnerSlot()

		return s.Response(res)
	}

	// 聊天
	if req.Type == pb.ChatType_WORLD {
		if p.getItemCountInBag(constant.ItemTypeLoudspeaker) >= 1 {
			p.takeSource(constant.ItemTypeLoudspeaker, 1)
		} else {
			if !p.checkTakeSource(global.GlobalCfg.BaseValue.Chatconsume.ID, global.GlobalCfg.BaseValue.Chatconsume.Num) {
				res.Code = -1
				res.Error = errutil.ErrItemNotEnough.Error()
				return s.Response(res)
			}
			p.takeSource(global.GlobalCfg.BaseValue.Chatconsume.ID, global.GlobalCfg.BaseValue.Chatconsume.Num, constant.ItemDelChat)
		}
		packet := &pb.ChatPush{Type: req.Type, Context: sensitive.ReplaceSensitiveWords(req.Context), Uid: p.uid, Name: p.name}
		if p.serverID != 0 {
			for _, v := range m.players {
				if v.serverID == p.serverID {
					v.pushMsg(protocol.RouteChatPush, packet)
					// 添加到聊天记录
					// if v.serverID == p.serverID {
					// 	v.pushMsg(protocol.RouteChatPush, packet)
				}
			}
			defaultManager.addChatHistory(p.serverID, int32(pb.ChatType_WORLD), p.uid, req.Context, p.name, 0)

		}
	}
	// //如果是行会聊天
	if req.Type == pb.ChatType_GUILD {
		if p.guildId == 0 {
			res.Code = -1
			res.Error = errutil.ErrGuildNotFound.Error()
			return s.Response(res)
		}

		packet := &pb.ChatPush{Type: req.Type, Context: sensitive.ReplaceSensitiveWords(req.Context), Uid: p.uid, Name: p.name}
		if guildManager.guilds[p.guildId] != nil {
			for _, v := range guildManager.guilds[p.guildId].Members {
				// 在线的才给推送
				// v.MemberId
				// 判断是否在线
				if pe, ok := m.players[v.MemberId]; ok {
					if pe.curState <= PlayerStatusDisconnect {
						pe.pushMsg(protocol.RouteChatPush, packet)
					}
				}
			}
			defaultManager.addChatHistory(p.serverID, int32(pb.ChatType_GUILD), p.uid, req.Context, p.name, p.guildId)
		}

	}

	return s.Response(res)
}

// 强化
func (m *Manager) Enhance(s *session.Session, req *pb.EnhanceRequest) error {
	res := &pb.EnhanceResponse{Code: 0, Slot: req.Slot, PartnerSlotId: req.PartnerSlotId}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	if req.Slot <= pb.EquipSlot_INVALID_EquipSlot || req.Slot >= pb.EquipSlot_MaxEquipSlot {
		res.Code = -2
		res.Error = errutil.ErrEquipSlot.Error()
		return s.Response(res)
	}
	_, level, err := p.enhance(req.Slot, req.PartnerSlotId)
	if err != nil {
		res.Code = -3
		res.Error = err.Error()
		res.NewLevel = level
		res.Slot = req.Slot
		return s.Response(res)
	}

	res.NewLevel = level
	res.Slot = req.Slot
	return s.Response(res)
}

// 升星
func (m *Manager) Stars(s *session.Session, req *pb.StarsRequest) error {
	res := &pb.StarsResponse{Code: 0, Slot: req.Slot, PartnerSlotId: req.PartnerSlotId}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	if req.Slot <= pb.EquipSlot_INVALID_EquipSlot || req.Slot >= pb.EquipSlot_MaxEquipSlot {
		res.Code = -2
		res.Error = errutil.ErrEquipSlot.Error()
		return s.Response(res)
	}
	check, level, err := p.stars(req.Slot, req.PartnerSlotId)
	if err != nil {
		res.Code = -3
		res.Error = err.Error()
		res.NewLevel = level
		res.Slot = req.Slot
		return s.Response(res)
	}
	if check {
		res.Error = errutil.StarsSuccess.Error()
	} else {
		res.Code = 1
		res.Error = errutil.StarsError.Error()
	}
	res.NewLevel = level
	res.Slot = req.Slot
	return s.Response(res)
}

// 使用升星道具
func (m *Manager) StarsLevel(s *session.Session, req *pb.ItemUseRequest) error {
	res := &pb.StarsLevelResponse{}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	if !p.checkTakeSourceByUid(req.ItemUid, int64(req.Num)) {
		res.Code = -3
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}
	Starlevel := global.GlobalCfg.Starstone.Starlevel
	if len(Starlevel) == 0 {
		res.Code = -4
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}
	checkId := int32(-1)
	slot := int32(0)
	for _, v := range Starlevel {
		if int64(v.ID) == req.ItemUid {
			checkId = v.Level
			slot = v.Slot
		}
	}
	if checkId == -1 {
		res.Code = -4
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}
	// selectId := int64(-1)
	if slot != 0 {
		if p.equips[slot].Stars < checkId {
			p.equips[slot].Stars = checkId
		} else {
			res.Code = -5
			res.Error = errutil.StarsNoUseError.Error()
			return s.Response(res)
		}
	} else {
		selectId := make([]int, 0)
		for i, v := range p.equips {
			// 需要随机给他一个槽位升满
			if v.Stars < checkId {
				selectId = append(selectId, i)
			}
		}
		if len(selectId) == 0 {
			res.Code = -5
			res.Error = errutil.StarsNoUseError.Error()
			return s.Response(res)
		}
		weight := global.RandIn(len(selectId))
		p.equips[weight].Stars = checkId
	}

	// 删除道具
	p.takeSourceByUid(req.ItemUid, int64(req.Num), constant.ItemDelGame)

	return s.Response(res)
}

// 穿脱技能
func (m *Manager) SkillChange(s *session.Session, req *pb.SkillChangeRequest) error {
	res := &pb.SkillChangeResponse{OpType: req.OpType, Slot: req.Slot, SkillId: req.SkillId}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	if err = p.skillChange(req.OpType, req.Slot, req.SkillId); err != nil {
		res.Code = -2
		res.Error = err.Error()
		return s.Response(res)
	}
	return s.Response(res)
}

// 升级技能
func (m *Manager) SkillUpgrade(s *session.Session, req *pb.SkillUpgradeRequest) error {
	res := &pb.SkillUpgradeResponse{Code: 0, PartnerUid: req.PartnerUid}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	newSkillId, err := p.skillsUpgrade(req.SkillId, req.PartnerUid)
	if err != nil {
		res.Code = -3
		res.Error = err.Error()
		res.NewSkillId = newSkillId
		return s.Response(res)
	}
	res.OldSkillId = req.SkillId
	res.NewSkillId = newSkillId
	return s.Response(res)
}

// 进阶技能
func (m *Manager) SkillAdvanced(s *session.Session, req *pb.SkillAdvancedRequest) error {
	res := &pb.SkillAdvancedResponse{Code: 0, PartnerUid: req.PartnerUid}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	newSkillId, err := p.skillsAdvanced(req.SkillId, req.PartnerUid)
	if err != nil {
		res.Code = -3
		res.Error = err.Error()
		res.NewLevel = newSkillId
		return s.Response(res)
	}
	res.OldSkillId = req.SkillId
	res.NewLevel = newSkillId
	return s.Response(res)
}

// 个人信息
func (m *Manager) GetPlayerInfo(uid int64) (*protocol.UserInfo, error) {
	p, ok := m.player(uid)
	if ok {
		userInfo := &protocol.UserInfo{
			Name:            p.name,
			Level:           p.level,
			Vip:             p.vip,
			BagNumEquip:     p.bagEquipLeftCount(),
			BagNumItem:      p.bagItemLeftCount(),
			FateLevel:       p.fateLevel,
			FateExp:         p.fateExp,
			Status:          int(p.curState),
			TrialTowerLevel: p.InstanceData.TrialTowerMap.BattleId % 1000,
			TotalCombat:     p.totalCombat,
			GuildId:         p.guildId,
			ServerId:        p.serverID,
			AccountName:     p.accountName,
		}
		return userInfo, nil
	}
	return nil, errutil.ErrPlayerNotFound
}

// 个人信息
func (m *Manager) SetPlayerInfo(uid int64, fateLevel int32, fateExp int64) error {
	p, ok := m.player(uid)
	if !ok || p.session == nil {
		return errutil.ErrPlayerNotFound
	}
	p.SetPlayerInfo(fateLevel, fateExp)
	return nil
}

// 设置命格属性
func (m *Manager) userFate(uid int64, cfgId int32, num int32) ([]int, int32, error) {
	// 手动使用
	p, ok := m.player(uid)
	if !ok || p.session == nil {
		return nil, num, errutil.ErrPlayerNotFound
	}
	res, resnum, err := p.IsFate(cfgId, num)
	if err != nil {
		return nil, resnum, err
	}
	return res, resnum, nil
}

func (m *Manager) Notification(uid int64, Type pb.Notification) {
	p, ok := m.player(uid)
	if !ok || p.session == nil {
		return
	}
	packet := &pb.SystemNotificationPush{
		Type: Type,
	}
	p.pushMsg(protocol.RouteSystemNotificationPush, packet)
}

// 获取宠物空闲位置数量
func (m *Manager) GetPetEmptySlot(uid int64) (int, error) {
	p, ok := m.player(uid)
	if !ok || p.session == nil {
		return 0, errutil.ErrPlayerNotFound
	}
	emptySlot := p.getEmptyPetSlotNum()
	if emptySlot == 0 {
		return 0, errutil.ErrPetFull
	}
	return int(emptySlot), nil
}

// 合成
func (m *Manager) Compose(s *session.Session, req *pb.ComposeRequest) error {
	res := &pb.ComposeResponse{}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	newComposeId, err := p.compose(req.Info)
	log.Debug("err", err)
	if err != nil {
		res.Code = -2
		res.Error = err.Error()
		res.NewConpose = newComposeId
		return s.Response(res)
	}
	res.NewConpose = newComposeId
	return s.Response(res)
}

// 穿脱宝石
func (m *Manager) GemstoneChange(s *session.Session, req *pb.GemstoneChangeRequest) error {
	res := &pb.GemstoneChangeResponse{OpType: req.OpType, Slot: req.Slot, PartnerSlotId: req.PartnerSlotId}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	if err = p.genstoneChange(req.OpType, req.Slot, req.GemstoneSlot, req.ItemUid, req.PartnerSlotId); err != nil {
		res.Code = -2
		res.Error = err.Error()
		return s.Response(res)
	}
	res.GemstoneSlot = req.GemstoneSlot
	res.ItemUid = req.ItemUid

	return s.Response(res)
}

// 一键穿脱宝石
func (m *Manager) GemstoneChangeAll(s *session.Session, req *pb.GemstoneChangeAllRequest) error {
	res := &pb.GemstoneChangeAllResponse{Code: 0, Error: "", Slot: req.Slot, PartnerSlotId: req.PartnerSlotId}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.GemStoneSlot = nil
		res.Error = err.Error()
		return s.Response(res)
	}
	newdate, err := p.genstoneChangeAll(req.Slot, req.PartnerSlotId)
	if err != nil {
		res.Code = -2
		res.Error = err.Error()
		res.GemStoneSlot = nil
		return s.Response(res)
	}
	res.GemStoneSlot = newdate
	return s.Response(res)
}

// 跑马灯
func (m *Manager) Lamp(id int32, serverId int32, context []string) {
	packet := &pb.LampPush{
		Uid:     id,
		Context: context,
	}
	if cfg, ok := global.LampCfg[id]; ok {
		if cfg.Receptor == 1 {
			if serverId == 0 {
				m.group.Broadcast(protocol.RouteLampPush, packet)
			} else {
				for _, v := range m.players {
					if v.serverID == serverId {
						v.pushMsg(protocol.RouteLampPush, packet)
					}
				}
			}
		}
	}
}

func (m *Manager) SendLamp(lamp *pb.LampPush) {
	packet := &pb.LampPush{
		Uid:     lamp.Uid,
		Context: lamp.Context,
	}
	if cfg, ok := global.LampCfg[lamp.Uid]; ok {
		if cfg.Receptor == 1 {
			m.group.Broadcast(protocol.RouteLampPush, packet)
		}
	}
}

func (m *Manager) GmCmd(cmd protocol.GmCmd) {
	switch cmd.Cmd {

	case "shutdown":
		global.LoginOpen = false

		for _, r := range roomManager.rooms {
			for _, d := range r.desks {
				d.setStatus(constant.DeskStatusDestory)
			}
		}

		for _, p := range m.players {
			p.syncSaveDb()

			delete(m.players, p.uid)
		}
	case "login":
		if len(cmd.Param) == 1 {
			if cmd.Param[0] == "on" {
				global.LoginOpen = true
			} else if cmd.Param[0] == "off" {
				global.LoginOpen = false
			}

			logger.Debugf("GM设置服务器登录状态 %t ", global.LoginOpen)
		}
	}
}

// 伙伴
func (m *Manager) PartnerList(s *session.Session, req *pb.PartnerListRequest) error {
	res := &pb.PartnerListResponse{List: []*pb.PartnerInfo{}}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

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

		res.List = append(res.List, partner)
	}
	return s.Response(res)
}

func (m *Manager) PartnerSlotInfo(s *session.Session, req *pb.PartnerSlotInfoRequest) error {
	res := &pb.PartnerSlotInfoResponse{List: []*pb.PartnerSlotInfo{}}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	for _, v := range p.partnerSlots {
		res.List = append(res.List, &pb.PartnerSlotInfo{
			SlotId:     v.SlotId,
			PartnerUid: v.PartnerUid,
			Equips:     p.FillEquipInfo(v.Equips),
		})
	}

	return s.Response(res)
}

// 首页宝箱设置
func (m *Manager) HomeBoxSetting(s *session.Session, req *pb.HomeBoxSettingRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_HomeBoxSetting)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	err = p.homeBoxSetting(req.Level, req.Job)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	return s.Response(res)
}

// 首页开宝箱
func (m *Manager) HomeOpenBox(s *session.Session, req *pb.HomeOpenBoxRequest) error {
	res := &pb.HomeOpenBoxResponse{}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	// 判断之前开的宝箱有没有数据
	if len(p.box.NowOpenRes) > 0 {
		res.Code = -2
		res.Error = errutil.ErrOpenBoxEnough.Error()
		return s.Response(res)
	}
	if !p.checkTakeSource(constant.ItemTypeTreasureNum, int64(req.Num)) {
		res.Code = -3
		res.Error = errutil.ErrItemNotEnough.Error()
		return s.Response(res)
	}
	if req.Level > p.level {
		res.Code = -4
		res.Error = errutil.ErrMaxLevel.Error()
	}
	openbox, err := p.openbox(req.Level, req.Num, req.Job)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	p.takeSource(constant.ItemTypeTreasureNum, int64(req.Num), constant.ItemDelHomeOpenBox)
	res.ItemInfo = openbox
	return s.Response(res)
}

// 首页宝箱升级
func (m *Manager) HomeUpgradeBox(s *session.Session, req *pb.HomeUpgradeBoxRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_HomeUpgradeBox)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	err = p.upgradeBox()
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	return s.Response(res)
}

// 加快首页宝箱升级
func (m *Manager) HomeUpgradeBoxQuick(s *session.Session, req *pb.HomeUpgradeBoxQuickRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_HomeUpgradeBoxQuick)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	err = p.UpgradeBoxQuick(req.Num)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	return s.Response(res)
}

// 装备入背包
func (m *Manager) BoxResIntoBag(s *session.Session, req *pb.BoxResIntoBagRequest) error {
	res := &pb.BoxResIntoBagResponse{}
	res.Uid = req.Uid
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	err = p.boxResIntoBag(req.Uid)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	return s.Response(res)
}

func (m *Manager) UpgradeLevel(s *session.Session, req *pb.UpgradeLevelRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_UpgradeLevel)}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	if req.Type == pb.ObjectType_PLAYER {
		p.levelUp()
	} else {
		partner := p.getPartnerInfo(req.Uid)
		if partner == nil {
			res.Code = -2
			return s.Response(res)
		}
		if partner.level >= p.level {
			res.Code = -3
			res.Error = errutil.ErrMaxLevel.Error()
			return s.Response(res)
		}
		if !partner.levelUp() {
			res.Code = -3
			return s.Response(res)
		}
	}
	p.syncUserBase()

	return s.Response(res)
}

// /伙伴
func (m *Manager) PartnerCombat(s *session.Session, req *pb.PartnerCombatRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PartnerCombat)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}
	if p.desk != nil {
		res.Code = -2
		res.Error = errutil.IllegalInBattle
		return s.Response(res)
	}

	if req.SlotId < 1 || req.SlotId > 4 {
		res.Code = -3
		res.Error = errutil.ErrIllegalParameter.Error()
		return s.Response(res)
	}
	needLevel := int32(0)
	for _, v := range global.GlobalCfg.Openslot.IDLevel {
		if v.ID == req.SlotId {
			needLevel = v.Needlevel
			break
		}
	}
	if p.level < needLevel {
		res.Code = -4
		res.Error = errutil.ErrLevel.Error()
		return s.Response(res)
	}

	if err := p.partnerCombat(req.SlotId, req.PartnerUid); err != nil {
		res.Code = -5
		res.Error = err.Error()
	}
	return s.Response(res)
}

func (m *Manager) PartnerRest(s *session.Session, req *pb.PartnerRestRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PartnerRest)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
	}

	if p.desk != nil {
		res.Code = -2
		res.Error = errutil.IllegalInBattle
		return s.Response(res)
	}
	if err := p.partnerRest(req.SlotId); err != nil {
		res.Code = -3
		res.Error = err.Error()
	}
	return s.Response(res)
}

func (m *Manager) PartnerReplace(s *session.Session, req *pb.PartnerReplaceRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PartnerReplace)}

	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
	}

	if req.SlotId < 1 || req.SlotId > 4 {
		res.Code = -3
		res.Error = errutil.ErrIllegalParameter.Error()
		return s.Response(res)
	}

	needLevel := int32(0)
	for _, v := range global.GlobalCfg.Openslot.IDLevel {
		if v.ID == req.SlotId {
			needLevel = v.Needlevel
			break
		}
	}

	if p.level < needLevel {
		res.Code = -4
		res.Error = errutil.ErrLevel.Error()
		return s.Response(res)
	}

	// 下阵伙伴
	if p.desk != nil {
		res.Code = -2
		res.Error = errutil.IllegalInBattle
		return s.Response(res)
	}

	slotInfo := p.getPartnerSlotInfo(req.SlotId)
	if slotInfo == nil {
		return errutil.ErrPartnerInvalid
	}
	partner := p.getPartnerInfoBySlotId(req.SlotId)
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

	partnerSlot := p.getPartnerSlotInfo(req.SlotId)
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
	p.SyncUserPartnerSlot()

	// 上阵伙伴
	if err := p.partnerCombat(req.SlotId, req.PartnerUid); err != nil {
		res.Code = -5
		res.Error = err.Error()
	}
	return s.Response(res)
}

func (m *Manager) PartnerStar(s *session.Session, req *pb.PartnerStarRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PartnerStar)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
	}

	partner := p.getPartnerInfo(req.PartnerUid)
	if partner == nil {
		res.Code = -2
		res.Error = errutil.ErrPartnerInvalid.Error()
		return s.Response(res)
	}

	if err = partner.starUp(); err != nil {
		res.Code = -3
		res.Error = err.Error()
		return s.Response(res)
	}
	p.syncSavePartnerDb(partner.uid, model.Update)
	return s.Response(res)
}

func (m *Manager) PartnerCompose(s *session.Session, req *pb.PartnerComposeRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PartnerCompose)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
	}

	if err = p.partnerCompose(int32(req.PartnerCfgId)); err != nil {
		res.Code = -2
		res.Error = err.Error()
		return s.Response(res)
	}

	return s.Response(res)
}

// PartnerReforge
func (m *Manager) PartnerReforge(s *session.Session, req *pb.PartnerReforgeRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PartnerReforge)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
	}

	if err = p.partnerReforge(req.PartnerUid, req.IsFree); err != nil {
		res.Code = -2
		res.Error = err.Error()
		return s.Response(res)
	}

	return s.Response(res)
}

// PartnerDisassemble
func (m *Manager) PartnerDisassemble(s *session.Session, req *pb.PartnerDisassembleRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PartnerDisassemble)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
	}

	if err = p.partnerDisassemble(req.PartnerUid); err != nil {
		res.Code = -2
		res.Error = err.Error()
		return s.Response(res)
	}

	return s.Response(res)
}

func (m *Manager) EquipBest(s *session.Session, req *pb.EquipBestRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PartnerStar)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
	}

	if req.Type == pb.ObjectType_PARTNER && req.PartnerSlotId == 0 {
		res.Code = -2
		res.Error = errutil.ErrPartnerInvalid.Error()
		return s.Response(res)
	}
	p.equipBest(req.Type, req.PartnerSlotId)
	return s.Response(res)
}

// 批量添加伙伴
func (m *Manager) addPartnerBatch(AddSource *protocol.AddSource) error {
	p, ok := m.player(AddSource.Uid)
	if !ok {
		return errutil.ErrPlayerNotFound
	}
	for _, v := range AddSource.Source {
		if err := p.addPartner(v.Id); err != nil {
			return errutil.ErrPartnerInvalid
		}
	}
	return nil
}

// 批量添加宠物
func (m *Manager) addPetBatch(AddSource *protocol.AddSource, NeedDel int32, reason string) error {
	p, ok := m.player(AddSource.Uid)
	if !ok {
		return errutil.ErrPlayerNotFound
	}
	for _, v := range AddSource.Source {
		p.tasks.dispatch(pb.TargetType_TOTAL_PET_EGGS_OPENED, 0, 1)
		if _, err := p.addPet(v.Id, 0, 0, 0, NeedDel, reason); err != nil {
			return err
		}
	}
	return nil
}

func (m *Manager) PetStar(s *session.Session, req *pb.PetStarRequest) error {
	res := &pb.PetStarResponse{}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
	}

	resnum, err := p.petStar(req.SlotId, req.CfgId, req.Num)
	if err != nil {
		res.Code = -3
		res.Error = err.Error()
		return s.Response(res)
	}
	res.ResPer = resnum
	return s.Response(res)
}

func (m *Manager) PetLevel(s *session.Session, req *pb.PetLevelRequest) error {
	res := &pb.CommonResponse{Code: 0, RequestCmdId: uint32(pb.CmdId_C2S_PetLevel)}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	pet := p.getPetInfo(req.SlotId)
	if pet == nil {
		res.Code = -2
		res.Error = errutil.ErrPetInvalid.Error()
		return s.Response(res)
	}

	if err = p.petLevel(req.SlotId); err != nil {
		res.Code = -3
		res.Error = err.Error()
		return s.Response(res)
	}
	return s.Response(res)
}

func (m *Manager) PetMove(s *session.Session, req *pb.PetMoveRequest) error {
	res := &pb.PetMoveResponse{}
	p, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		return s.Response(res)
	}

	newSlot, newPet, newPetCfgId, err := p.petMove(req.SlotId, req.TargetSlotId)
	if err != nil {
		res.Code = -2
		res.Error = err.Error()
	}
	res.PetcfgId = newPet
	res.SlotId = newSlot
	res.NewPetCfgId = newPetCfgId
	return s.Response(res)
}

func (m *Manager) InstanceInfo(s *session.Session, req *pb.InstanceInfoRequest) error {
	res := &pb.InstanceInfoResponse{}
	p, err := playerWithSession(s)
	if err != nil {
		return s.Response(res)
	}

	p.fillInstanceInfo(res)

	logger.Debugf("InstanceInfoResponse.InstanceInfoResponse_AFK.AfkMultiplierID: %d, uid: %d", res.GetAfk().GetAfkMultiplierID(), p.Uid())

	return s.Response(res)
}

func (m *Manager) SendMail(uid int64, desc string, source string, attachment []protocol.Attachment) {
	mail := protocol.MailAddRequest{
		Uid:          -1,
		RecipientUid: uid,
		Title:        "系统邮件",
		Desc:         desc,
		ChannelCode:  source,
		Attachment:   attachment,
	}
	if err := provide.Mailadd(&mail); err != nil {
		if p, ok := m.player(uid); ok {
			packet := &pb.SystemNotificationPush{Type: pb.Notification_MAIL_FULL}
			p.pushMsg(protocol.RouteSystemNotificationPush, packet)
		}
	}
}

func (m *Manager) ObservePlayerInfo(s *session.Session, req *pb.ObservePlayerInfoRequest) error {
	res := &pb.ObservePlayerInfoResponse{}
	p, err := playerWithSession(s)
	if err != nil {
		return s.Response(res)
	}
	// 获取观察玩家信息
	observePlayer := m.ObserveUserBaseInfo(p.serverID, req.Uid)
	if observePlayer == nil {
		return s.Response(res)
	}

	// Fill response with observe player info
	res.ObservePlayerInfo = &pb.ObservePlayerInfo{
		TotalCombat: observePlayer.totalCombat, // Using combat as total combat since it's the main combat value
		Combat:      observePlayer.combat,
		VipLevel:    int32(observePlayer.vip),
		Sex:         int32(observePlayer.sex),
		Level:       int32(observePlayer.level),
		Job:         int32(observePlayer.job),
		Nickname:    observePlayer.name,
		Attributes: []*pb.Stats{
			{Attr: pb.Attribute_PHYSICAL_ATTACK, Num: observePlayer.stats.getStats(pb.Attribute_PHYSICAL_ATTACK)},
			{Attr: pb.Attribute_MAGIC_ATTACK, Num: observePlayer.stats.getStats(pb.Attribute_MAGIC_ATTACK)},
			{Attr: pb.Attribute_PHYSICAL_DEFENSE, Num: observePlayer.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE)},
			{Attr: pb.Attribute_MAGIC_DEFENSE, Num: observePlayer.stats.getStats(pb.Attribute_MAGIC_DEFENSE)},
			{Attr: pb.Attribute_MAX_HEALTH, Num: observePlayer.stats.getStats(pb.Attribute_MAX_HEALTH)},
			{Attr: pb.Attribute_HEALTH, Num: observePlayer.stats.getStats(pb.Attribute_HEALTH)},
			{Attr: pb.Attribute_DODGE, Num: observePlayer.stats.getStats(pb.Attribute_DODGE)},
			{Attr: pb.Attribute_HIT, Num: observePlayer.stats.getStats(pb.Attribute_HIT)},
			{Attr: pb.Attribute_CRITICAL_STRIKE, Num: observePlayer.stats.getStats(pb.Attribute_CRITICAL_STRIKE)},
			{Attr: pb.Attribute_TOUGHNESS, Num: observePlayer.stats.getStats(pb.Attribute_TOUGHNESS)},
			{Attr: pb.Attribute_ATTACK_SPEED, Num: observePlayer.stats.getStats(pb.Attribute_ATTACK_SPEED)},
			{Attr: pb.Attribute_MOVE_SPEED, Num: observePlayer.stats.getStats(pb.Attribute_MOVE_SPEED)},
			{Attr: pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE)},
			{Attr: pb.Attribute_MAGIC_ATTACK_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE)},
			{Attr: pb.Attribute_HEALTH_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_HEALTH_PERCENTAGE)},
			{Attr: pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE)},
			{Attr: pb.Attribute_MAGIC_DEFENSE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE)},
			{Attr: pb.Attribute_DODGE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_DODGE_PERCENTAGE)},
			{Attr: pb.Attribute_HIT_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_HIT_PERCENTAGE)},
			{Attr: pb.Attribute_CRITICAL_STRIKE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE)},
			{Attr: pb.Attribute_TOUGHNESS_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE)},
			{Attr: pb.Attribute_ATTACK_SPEED_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE)},
			{Attr: pb.Attribute_MOVE_SPEED_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE)},
			{Attr: pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)},
			{Attr: pb.Attribute_PVP_DAMAGE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_PVP_DAMAGE_PERCENTAGE)},
			{Attr: pb.Attribute_PVE_DAMAGE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_PVE_DAMAGE_PERCENTAGE)},
			{Attr: pb.Attribute_EXP_INCREASE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_EXP_INCREASE_PERCENTAGE)},
			{Attr: pb.Attribute_ENERGY_RECOVERY_SPEED_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_ENERGY_RECOVERY_SPEED_PERCENTAGE)},
			{Attr: pb.Attribute_ITEM_DROP_RATE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_ITEM_DROP_RATE_PERCENTAGE)},
			{Attr: pb.Attribute_DMT_REDUCE_FIXED_PLAYER, Num: observePlayer.stats.getStats(pb.Attribute_DMT_REDUCE_FIXED_PLAYER)},
			{Attr: pb.Attribute_DMT_REDUCE_FIXED_MONSTER, Num: observePlayer.stats.getStats(pb.Attribute_DMT_REDUCE_FIXED_MONSTER)},
			{Attr: pb.Attribute_DMT_REDUCE_PERCENTAGE_PLAYER, Num: observePlayer.stats.getStats(pb.Attribute_DMT_REDUCE_PERCENTAGE_PLAYER)},
			{Attr: pb.Attribute_DMT_REDUCE_PERCENTAGE_MONSTER, Num: observePlayer.stats.getStats(pb.Attribute_DMT_REDUCE_PERCENTAGE_MONSTER)},
			{Attr: pb.Attribute_DMT_FINAL_REDUCE_FIXED, Num: observePlayer.stats.getStats(pb.Attribute_DMT_FINAL_REDUCE_FIXED)},
			{Attr: pb.Attribute_DMT_FINAL_REDUCE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_DMT_FINAL_REDUCE_PERCENTAGE)},
			{Attr: pb.Attribute_DMT_INCREASE_FIXED_PLAYER, Num: observePlayer.stats.getStats(pb.Attribute_DMT_INCREASE_FIXED_PLAYER)},
			{Attr: pb.Attribute_DMT_INCREASE_FIXED_MONSTER, Num: observePlayer.stats.getStats(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)},
			{Attr: pb.Attribute_DMT_FINAL_INCREASE_FIXED, Num: observePlayer.stats.getStats(pb.Attribute_DMT_FINAL_INCREASE_FIXED)},
			{Attr: pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE)},
			{Attr: pb.Attribute_SUCKING_BLOOD_PERCENTAGE, Num: observePlayer.stats.getStats(pb.Attribute_SUCKING_BLOOD_PERCENTAGE)},
		},
		PartnersStats: make([]*pb.ObservePlayerInfo_PartnerStats, 0),
		Partners:      make([]*pb.PartnerSlotInfo, 0),
		Pets: &pb.PetSlotInfo{
			CfgId:   observePlayer.pet[0].PetCfgId,
			Level:   int32(observePlayer.pet[0].Level),
			Star:    int32(observePlayer.pet[0].Star),
			Enhance: int32(observePlayer.pet[0].Enhance),
			Quality: int32(observePlayer.pet[0].Quality),
			Exp:     int32(observePlayer.pet[0].Exp),
			ExpStar: int32(observePlayer.pet[0].ExpStar),
		},
		SlotInfo: make([]*pb.SlotInfo, 0),
	}

	// Add partners stats and info
	for _, partner := range observePlayer.partners {
		// Add partner stats
		partnerStats := &pb.ObservePlayerInfo_PartnerStats{
			Combat: partner.combat,
			Attributes: []*pb.Stats{
				{Attr: pb.Attribute_PHYSICAL_ATTACK, Num: partner.stats.getStats(pb.Attribute_PHYSICAL_ATTACK)},
				{Attr: pb.Attribute_MAGIC_ATTACK, Num: partner.stats.getStats(pb.Attribute_MAGIC_ATTACK)},
				{Attr: pb.Attribute_PHYSICAL_DEFENSE, Num: partner.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE)},
				{Attr: pb.Attribute_MAGIC_DEFENSE, Num: partner.stats.getStats(pb.Attribute_MAGIC_DEFENSE)},
				{Attr: pb.Attribute_MAX_HEALTH, Num: partner.stats.getStats(pb.Attribute_MAX_HEALTH)},
				{Attr: pb.Attribute_MAX_HEALTH, Num: partner.stats.getStats(pb.Attribute_MAX_HEALTH)},
				{Attr: pb.Attribute_HEALTH, Num: partner.stats.getStats(pb.Attribute_HEALTH)},
				{Attr: pb.Attribute_DODGE, Num: partner.stats.getStats(pb.Attribute_DODGE)},
				{Attr: pb.Attribute_HIT, Num: partner.stats.getStats(pb.Attribute_HIT)},
				{Attr: pb.Attribute_CRITICAL_STRIKE, Num: partner.stats.getStats(pb.Attribute_CRITICAL_STRIKE)},
				{Attr: pb.Attribute_TOUGHNESS, Num: partner.stats.getStats(pb.Attribute_TOUGHNESS)},
				{Attr: pb.Attribute_ATTACK_SPEED, Num: partner.stats.getStats(pb.Attribute_ATTACK_SPEED)},
				{Attr: pb.Attribute_MOVE_SPEED, Num: partner.stats.getStats(pb.Attribute_MOVE_SPEED)},
				{Attr: pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE)},
				{Attr: pb.Attribute_MAGIC_ATTACK_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE)},
				{Attr: pb.Attribute_HEALTH_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_HEALTH_PERCENTAGE)},
				{Attr: pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE)},
				{Attr: pb.Attribute_MAGIC_DEFENSE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE)},
				{Attr: pb.Attribute_DODGE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_DODGE_PERCENTAGE)},
				{Attr: pb.Attribute_HIT_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_HIT_PERCENTAGE)},
				{Attr: pb.Attribute_CRITICAL_STRIKE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE)},
				{Attr: pb.Attribute_TOUGHNESS_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE)},
				{Attr: pb.Attribute_ATTACK_SPEED_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE)},
				{Attr: pb.Attribute_MOVE_SPEED_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE)},
				{Attr: pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)},
				{Attr: pb.Attribute_PVP_DAMAGE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_PVP_DAMAGE_PERCENTAGE)},
				{Attr: pb.Attribute_PVE_DAMAGE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_PVE_DAMAGE_PERCENTAGE)},
				{Attr: pb.Attribute_EXP_INCREASE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_EXP_INCREASE_PERCENTAGE)},
				{Attr: pb.Attribute_ENERGY_RECOVERY_SPEED_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_ENERGY_RECOVERY_SPEED_PERCENTAGE)},
				{Attr: pb.Attribute_ITEM_DROP_RATE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_ITEM_DROP_RATE_PERCENTAGE)},
				{Attr: pb.Attribute_DMT_REDUCE_FIXED_PLAYER, Num: partner.stats.getStats(pb.Attribute_DMT_REDUCE_FIXED_PLAYER)},
				{Attr: pb.Attribute_DMT_REDUCE_FIXED_MONSTER, Num: partner.stats.getStats(pb.Attribute_DMT_REDUCE_FIXED_MONSTER)},
				{Attr: pb.Attribute_DMT_REDUCE_PERCENTAGE_PLAYER, Num: partner.stats.getStats(pb.Attribute_DMT_REDUCE_PERCENTAGE_PLAYER)},
				{Attr: pb.Attribute_DMT_REDUCE_PERCENTAGE_MONSTER, Num: partner.stats.getStats(pb.Attribute_DMT_REDUCE_PERCENTAGE_MONSTER)},
				{Attr: pb.Attribute_DMT_FINAL_REDUCE_FIXED, Num: partner.stats.getStats(pb.Attribute_DMT_FINAL_REDUCE_FIXED)},
				{Attr: pb.Attribute_DMT_FINAL_REDUCE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_DMT_FINAL_REDUCE_PERCENTAGE)},
				{Attr: pb.Attribute_DMT_INCREASE_FIXED_PLAYER, Num: partner.stats.getStats(pb.Attribute_DMT_INCREASE_FIXED_PLAYER)},
				{Attr: pb.Attribute_DMT_INCREASE_FIXED_MONSTER, Num: partner.stats.getStats(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)},
				{Attr: pb.Attribute_DMT_FINAL_INCREASE_FIXED, Num: partner.stats.getStats(pb.Attribute_DMT_FINAL_INCREASE_FIXED)},
				{Attr: pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE)},
				{Attr: pb.Attribute_SUCKING_BLOOD_PERCENTAGE, Num: partner.stats.getStats(pb.Attribute_SUCKING_BLOOD_PERCENTAGE)},
			},
			PartnerUid: partner.uid,
		}
		res.ObservePlayerInfo.PartnersStats = append(res.ObservePlayerInfo.PartnersStats, partnerStats)

		res.ObservePlayerInfo.PartnerInfo = append(res.ObservePlayerInfo.PartnerInfo, &pb.PartnerInfo{
			Uid:   partner.uid,
			CfgId: partner.cfgId,
			Level: partner.level,
			Star:  partner.star,
		})
		// Add partner slot info

	}
	for _, slot := range observePlayer.partnerSlots {
		partnerSlotInfo := &pb.PartnerSlotInfo{
			SlotId:     slot.SlotId,
			PartnerUid: slot.PartnerUid,
			Equips:     observePlayer.FillEquipInfo(slot.Equips),
		}
		res.ObservePlayerInfo.Partners = append(res.ObservePlayerInfo.Partners, partnerSlotInfo)
	}

	// Add  slot info

	for _, v := range observePlayer.equips {
		Gemstone := make([]int32, 0)
		for _, v2 := range v.Gemstone {
			Gemstone = append(Gemstone, int32(v2))
		}
		slot := &pb.SlotInfo{Stars: v.Stars, Enhance: v.Enhance, Item: observePlayer.getItemByUid(v.ItemId), Gemstone: Gemstone}
		res.ObservePlayerInfo.SlotInfo = append(res.ObservePlayerInfo.SlotInfo, slot)
	}
	return s.Response(res)
}

// 获取某个人的信息
func (m *Manager) ObserveUserBaseInfo(serverId int32, uid int64) *Player {
	// 获取用户信息
	// 检查玩家是否在线 defaultManager.players(uid)

	if p, ok := defaultManager.player(uid); ok {
		return p
	}

	if r := robotMgr.getRankRobotInfo(serverId, uid); r != nil {
		if r.robotType == RobotTypePlayer {
			roleCfg, err := global.GetRoleCfg(int32(r.job), r.level)
			if err != nil {
				logger.Errorf("获取角色等级配置失败 ")
			}

			for _, rc := range roleCfg.BassProperty {
				r.stats.setStats(pb.Attribute(rc.Key), rc.Value)
			}

			r.stats.setStats(pb.Attribute_ATTACK_SPEED, global.GetRoleInitial(int32(r.job)).AttackSpeed)
			r.stats.setStats(pb.Attribute_MOVE_SPEED, global.GetRoleInitial(int32(r.job)).MoveSpeed)
		}
		return r.Player
	}
	p := newPlayer(nil, uid, "", "", "")
	if !p.syncFromDB() {
		return nil
	}
	p.curState = PlayerStatusOffLine
	p.refreshStats(WithRefreshStatsPushMessage(false))
	defaultManager.setPlayer(uid, p)
	// 获取用户信息
	return p
}

func (m *Manager) RankLogin(serverID int32, uid int64) {
	// 获取观察玩家信息
	observePlayer := m.ObserveUserBaseInfo(serverID, uid)
	if observePlayer == nil {
		return
	}
	rank := provide.IsFirst(uid, 0, observePlayer.serverID)
	if len(rank) > 0 {
		for _, v := range rank {
			strSend := []string{constant.RankTypeName[v], observePlayer.name}
			// 发送消息给所有在线用户
			defaultManager.Lamp(int32(constant.LampTopRankOnline), observePlayer.serverID, strSend)
		}
	}
}

func (m *Manager) GetPlayer(uid int64) (*Player, error) {
	if p, ok := defaultManager.players[uid]; ok {
		return p, nil
	}
	return nil, errutil.ErrPlayerNotFound
}

func (m *Manager) ActivityRewardReceive(s *session.Session, req *pb.ActivityRewardReceiveRequest) error {
	resp := &pb.ActivityRewardReceiveResponse{}
	p, err := playerWithSession(s)
	if err != nil {
		resp.Code = -1
		resp.Error = err.Error()
		return s.Response(resp)
	}

	p.activityMgr.RewardReceive(s.UID(), p, req.GetRewards())

	return s.Response(resp)
}

// 新手引导
func (m *Manager) GuidanceUpdate(s *session.Session, req *pb.GuidanceUpdateRequest) error {
	resp := &pb.GuidanceUpdateResponse{}
	p, err := playerWithSession(s)
	if err != nil {
		resp.Code = -1
		resp.Error = err.Error()
		return s.Response(resp)
	}
	p.Guidance = []int32{}
	p.Guidance = append(p.Guidance, req.Guidance...)
	resp.Guidance = append(resp.Guidance, p.Guidance...)
	return s.Response(resp)
}

func (m *Manager) UpShushuDate(uid int64, date map[string]interface{}, have bool) {
	shushuDate := make(map[string]interface{}, 0)
	if have {
		if p, ok := defaultManager.players[uid]; ok {
			baseInfo := p.ToMapForShushu()
			for k, v := range baseInfo {
				shushuDate[k] = v
			}

		}
	}
	for k, v := range date {
		shushuDate[k] = v
	}
	shushumain.ReportData(shushuDate)
}

func (m *Manager) UpShushuDateSet(uid int64, date map[string]interface{}, have bool) {
	shushuDate := make(map[string]interface{}, 0)
	for k, v := range date {
		shushuDate[k] = v
	}
	shushumain.ReportUserSetData(shushuDate)
}

func (m *Manager) UpShushuDateSetOnce(uid int64, date map[string]interface{}, have bool) {
	shushuDate := make(map[string]interface{}, 0)
	for k, v := range date {
		shushuDate[k] = v
	}
	shushumain.ReportUserSetOnceData(shushuDate)
}

func (m *Manager) UpShushuDateNo(date map[string]interface{}) {
	shushuDate := make(map[string]interface{}, 0)

	for k, v := range date {
		shushuDate[k] = v
	}
	shushumain.ReportData(shushuDate)
}

// 获取背包内道具数量
func (m *Manager) GetItemCount(uid int64, itemUid int32) (int64, error) {
	p, err := m.GetPlayer(uid)
	if err != nil {
		return 0, errutil.ErrPlayerNotFound
	}
	petNormalCount := p.getItemCountInBag(itemUid)
	if petNormalCount < 0 {
		return 0, errutil.ErrItemNotFound
	}
	return petNormalCount, nil
}

// MailCounterSet 设置MailCounter
func (m *Manager) MailCounterSet(uid int64, op constant.MailCounterOPType, num int) error {
	p, ok := defaultManager.players[uid]
	if !ok {
		return errutil.ErrPlayerNotFound
	}

	if p == nil {
		return errutil.ErrPlayerNotFound
	}

	return p.SetMailCounter(op, num)
}

// 同步任务
func (m *Manager) TaskDispatch(uid int64, key pb.TargetType, Typeson int32, limit int32) error {
	p, ok := defaultManager.players[uid]
	if !ok {
		return errutil.ErrPlayerNotFound
	}
	p.tasks.dispatch(key, Typeson, limit)
	return nil
}

// GuardIslandMultiplier 设置守护岛倍数
func (m *Manager) GuardIslandMultiplierSvc(uid, guardIslandRecordID int64) error {
	p, ok := defaultManager.players[uid]
	if !ok {
		return errutil.ErrPlayerNotFound
	}

	if p == nil {
		return errutil.ErrPlayerNotFound
	}

	return p.GuardIslandMultiplier(guardIslandRecordID)
}
