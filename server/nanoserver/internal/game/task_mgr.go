package game

import (
	"strconv"
	"strings"
	"time"

	"nanoserver/db"
	"nanoserver/global"
	shushumain "nanoserver/internal/shushu"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/errutil"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"
)

type (
	TargetInfo struct {
		TargetType pb.TargetType
		TargetId   int
		TargetNum  int
		Amount     int
		Completed  bool
	}
	TaskInfo struct {
		ID        int
		Class     pb.TaskClass
		Status    pb.TaskStatus
		Target    *TargetInfo
		ResetTime time.Time
	}
	TaskMgr struct {
		*Player
		tasks map[int32]*TaskInfo // 任务
	}
)

func NewTask(p *Player) *TaskMgr {
	return &TaskMgr{tasks: make(map[int32]*TaskInfo), Player: p}
}

func (tm *TaskMgr) dispatch(k pb.TargetType, i int32, v int32) {
	if tm.isRobot() {
		return
	}

	if k <= pb.TargetType_TASK_TARGET_TYPE_INVALID || k > pb.TargetType_TASK_TARGET_TYPE_MAX {
		return
	}

	for _, t := range tm.tasks {

		if t.Status != pb.TaskStatus_IN_PROGRESS && t.Class != pb.TaskClass_DAILY_ACTIVITY && t.Class != pb.TaskClass_WEEKLY_ACTIVITY {
			continue
		}
		if t.Target.Completed && t.Class != pb.TaskClass_DAILY_ACTIVITY && t.Class != pb.TaskClass_WEEKLY_ACTIVITY {
			continue
		}
		if t.Target.TargetType != k {
			continue
		}

		switch k {

		case pb.TargetType_ROLE_POWER_REQUIREMENT,
			pb.TargetType_ROLE_LEVEL_REQUIREMENT,
			pb.TargetType_EQUIPMENT_STRENGTHENING_LEVEL_REQUIREMENT,
			pb.TargetType_EQUIPMENT_UPGRADING_STAR_LEVEL_REQUIREMENT,
			pb.TargetType_TOTAL_ATTEMPTS_IN_ENDLESS_TRIALS,
			pb.TargetType_VIP_LEVEL,
			pb.TargetType_ANY_PET_UPGRADED_TO_LEVEL,
			pb.TargetType_UNLOCK_PARTNER_SLOT,
			pb.TargetType_CHEST_LEVEL_UPGRADED:
			if t.Target.Amount < int(v) {
				t.Target.Amount = int(v)
				tm.update(t)
			}

		case pb.TargetType_KILL_MONSTER_REQUIREMENT,
			pb.TargetType_PVE_INSTANCE_ENTRY_REQUIREMENT,
			pb.TargetType_TOTAL_PHYSICAL_STRENGTH_CONSUMED_REQUIREMENT,
			pb.TargetType_TOTAL_GOLD_COINS_CONSUMED_REQUIREMENT,
			pb.TargetType_DAILY_ACTIVITY_LEVEL,
			pb.TargetType_WEEKLY_ACTIVITY_LEVEL,
			pb.TargetType_TOTAL_BOOST_PURCHASES,
			pb.TargetType_TOTAL_CHALLENGES_IN_ARENA,
			pb.TargetType_TOTAL_KILLS_OF_NORMAL_MONSTERS,
			pb.TargetType_TOTAL_KILLS_OF_BOSS_MONSTERS,
			pb.TargetType_TOTAL_SHOP_PURCHASES_WITH_GOLD,
			pb.TargetType_TOTAL_SHOP_PURCHASES_WITH_COUPONS,
			pb.TargetType_TOTAL_SHOP_PURCHASES_WITH_SOULS,
			pb.TargetType_TOTAL_SHOP_PURCHASES_WITH_TRIAL_TOKENS,
			pb.TargetType_TOTAL_ACQUIRED_SOULS,
			pb.TargetType_TOTAL_ACQUIRED_TRIAL_TOKENS,
			pb.TargetType_TOTAL_BOX_OPENED,
			pb.TargetType_TOTAL_MATERIAL_INSTANCE_CHALLENGE,
			pb.TargetType_TOTAL_MULTI_VS_MULTI_ARENA_CHALLENGE,
			pb.TargetType_TOTAL_BOSS_CHALLENGE,
			pb.TargetType_TOTAL_RELIC_CHALLENGE,
			pb.TargetType_TOTAL_PET_EGGS_OPENED,
			pb.TargetType_TOTAL_EXPERIENCE_FB_CLEARED,
			pb.TargetType_TOTAL_GOLD_FB_CLEARED,
			pb.TargetType_ANY_EQUIP_STAR_UP_TIMES,
			pb.TargetType_SKILL_UPGRADING_TIMES,
			pb.TargetType_SEVENDAY_TASK_GIFT_PURCHASED,
			pb.TargetType_SEVENDAY_TASK_ALL_COMPLETED:
			t.Target.Amount += int(v)
			tm.update(t)

		case pb.TargetType_SKILL_UPGRADING_REQUIREMENT:
			if t.Target.TargetId == int(i) {
				t.Target.Amount = int(v)
			}

		case pb.TargetType_TOTAL_ITEM_CONSUMPTION,
			pb.TargetType_BATTLE_CHAPTERS_CLEARED,
			pb.TargetType_BOSS_LEVELS_CLEARED,
			pb.TargetType_TRIAL_TOWER_FLOORS_CLEARED,
			pb.TargetType_RELIC_CHAPTERS_CLEARED,
			pb.TargetType_UPGRADE_ANY_PARTNER_LEVEL,
			pb.TargetType_ANY_EQUIP_STRENGTHENED_TO_LEVEL:
			if t.Target.TargetId == int(i) {
				t.Target.Amount += int(v)
			}
		default:

		}

		tm.checkProgress(t)
	}
}

func (tm *TaskMgr) reset() {
	delTask := []int32{}
	for _, v := range tm.tasks {
		// 每日福利
		if v.Class == pb.TaskClass_ROUTINE {
			v.Target.Amount = 0
			v.Target.Completed = false
			v.Status = pb.TaskStatus_IN_PROGRESS

			tm.update(v)
			continue
		}

		// 日常任务
		if v.Class == pb.TaskClass_DAILY || v.Class == pb.TaskClass_DAILY_ACTIVITY {

			if vCfg, err := global.GetTaskCfg(int32(v.ID)); err == nil {
				if len(vCfg.Level) == 2 {
					if tm.level >= vCfg.Level[0] && tm.level <= vCfg.Level[1] {
						v.Target.Amount = 0
						v.Target.Completed = false
						v.Status = pb.TaskStatus_IN_PROGRESS

						tm.update(v)
						continue
					}
				}
			}

		}

		// 周常任务
		if v.Class == pb.TaskClass_WEEKLY || v.Class == pb.TaskClass_WEEKLY_ACTIVITY {
			if time.Now().After(v.ResetTime) {
				v.Target.Amount = 0
				v.Target.Completed = false
				v.Status = pb.TaskStatus_IN_PROGRESS

				tm.update(v)

				now := time.Now()
				// 获取当前日期的0点时间
				midnight := time.Date(now.Year(), now.Month(), now.Day(), 0, 0, 0, 0, now.Location())
				// 计算7天后的0点时间
				nextMidnightInSevenDays := midnight.AddDate(0, 0, 7)
				v.ResetTime = nextMidnightInSevenDays
			}
		}

		if v.Class == pb.TaskClass_DAILY || v.Class == pb.TaskClass_WEEKLY ||
			v.Class == pb.TaskClass_DAILY_ACTIVITY || v.Class == pb.TaskClass_WEEKLY_ACTIVITY {

			vCfg, err := global.GetTaskCfg(int32(v.ID))
			if err == nil {
				if len(vCfg.Level) == 2 {
					if tm.level < vCfg.Level[0] || tm.level > vCfg.Level[1] {
						delTask = append(delTask, int32(v.ID))

					}
				}
			}
		}
		if v.Class == pb.TaskClass_SEVENDAY || v.Class == pb.TaskClass_SEVENDAY_ACTIVITY {
			//查询这个任务对应活动表的时间，如果时间已过，直接删掉

			vCfg, err := global.GetTaskCfg(int32(v.ID))
			if err == nil {
				actCfg, err := global.GetActivityCfg(int32(vCfg.ActivID))
				if err != nil {
					continue
				}
				if tm.CreateDay > actCfg.ResetTime {
					//如果任务完成了，直接给他发奖励
					//但是也要删掉他的任务
					if v.Status == pb.TaskStatus_COMPLETED {
						result3 := make([]protocol.Attachment, len(vCfg.Reward))
						// MailId := strconv.FormatInt(snowflake.GenID(), 10)
						for i, Attachment := range vCfg.Reward {
							result3[i] = protocol.Attachment{
								MailId:      "",
								UserPropsId: int64(Attachment.Key),
								Num:         int64(Attachment.Value),
							}
						}
						taskName := vCfg.TaskDes
						taskName = strings.Replace(taskName, "{0}", strconv.Itoa(int(vCfg.Limit)), -1)
						_, err := db.MailAdd(
							-1,
							tm.uid,
							"系统邮件：任务奖励未领取",
							taskName,
							result3,
							constant.ItemAddTask,
						)
						if err != nil {
							logger.Error(err)
						}
					}
					delTask = append(delTask, int32(v.ID))
				}
			}
		}
	}

	for _, v := range delTask {
		tm.del(v)
	}

	tm.check()
}

func (tm *TaskMgr) init() {
	for _, cfg := range global.TaskCfg {

		if cfg.Job > 0 && tm.job != constant.MowJob(cfg.Job) {
			continue
		}

		// 非首轮任务
		if !cfg.FirstRound {
			continue
		}

		if task := tm.getTaskById(cfg.UUID); task == nil {

			if len(cfg.Level) == 2 {
				if tm.level < cfg.Level[0] || tm.level > cfg.Level[1] {
					continue
				}
			}
			// vip等级对应的任务
			if cfg.Class == int32(pb.TaskClass_VIP) {
				if tm.vip < cfg.VipLevel-1 {
					continue
				}
			}

			//七日狂欢对应的任务
			if cfg.Class == int32(pb.TaskClass_SEVENDAY) {
				if tm.CreateDay == 0 {
					tm.CreateDay = 1
				}
				if int32(tm.CreateDay) < int32(cfg.SevenDay) {
					continue
				}
			}
			t := &TaskInfo{
				ID: int(cfg.UUID), Class: pb.TaskClass(cfg.Class), Status: pb.TaskStatus_IN_PROGRESS,
				Target: &TargetInfo{
					TargetType: pb.TargetType(cfg.Type), TargetId: int(cfg.Typeson), TargetNum: int(cfg.Limit),
					Amount: 0, Completed: false,
				},
			}

			if t.Class == pb.TaskClass_WEEKLY || t.Class == pb.TaskClass_WEEKLY_ACTIVITY {
				now := time.Now()
				// 获取当前日期的0点时间
				midnight := time.Date(now.Year(), now.Month(), now.Day(), 0, 0, 0, 0, now.Location())
				// 计算7天后的0点时间
				nextMidnightInSevenDays := midnight.AddDate(0, 0, 7)
				t.ResetTime = nextMidnightInSevenDays
			}
			if t.Class == pb.TaskClass_DAILY || t.Class == pb.TaskClass_ROUTINE || t.Class == pb.TaskClass_WEEKLY_ACTIVITY {
				now := time.Now()
				// 获取当前日期的0点时间
				midnight := time.Date(now.Year(), now.Month(), now.Day(), 0, 0, 0, 0, now.Location())
				// 计算1天后的0点时间
				nextMidnightInSevenDays := midnight.AddDate(0, 0, 1)
				t.ResetTime = nextMidnightInSevenDays
			}

			switch t.Target.TargetType {
			case pb.TargetType_ROLE_POWER_REQUIREMENT:
				t.Target.Amount = int(tm.combat)
			case pb.TargetType_ROLE_LEVEL_REQUIREMENT:
				t.Target.Amount = int(tm.level)
			}

			tm.tasks[int32(t.ID)] = t
		}
	}
}

func (tm *TaskMgr) check() {
	for _, v := range global.TaskCfg {

		if v.Job > 0 && tm.job != constant.MowJob(v.Job) {
			continue
		}
		// 非首轮任务
		if !v.FirstRound {
			continue
		}
		if task := tm.getTaskById(v.UUID); task == nil {
			tm.add(v.UUID, 0)
		}
	}
}

func (tm *TaskMgr) getTaskById(id int32) *TaskInfo {
	t, ok := tm.tasks[id]
	if ok {
		return t
	}
	return nil
}

func (tm *TaskMgr) toPB() *pb.DBTask {
	dbtask := &pb.DBTask{}
	for _, v := range tm.tasks {
		dbtask.Tasks = append(dbtask.Tasks, &pb.TaskInfo{
			CfgId: int32(v.ID), Class: v.Class, Status: v.Status, RefreshTime: v.ResetTime.Unix(),
			Target: &pb.TargetInfo{
				Type: v.Target.TargetType, TargetId: int32(v.Target.TargetId),
				TargetNum: int32(v.Target.TargetNum), Amount: int32(v.Target.Amount), Completed: v.Target.Completed,
			},
		})
	}
	return dbtask
}

func (tm *TaskMgr) toPBTaskInfo(t *TaskInfo) *pb.TaskInfo {
	return &pb.TaskInfo{
		CfgId: int32(t.ID), Class: t.Class, Status: t.Status,
		Target: &pb.TargetInfo{
			Type: t.Target.TargetType, TargetId: int32(t.Target.TargetId),
			TargetNum: int32(t.Target.TargetNum),
			Amount:    int32(t.Target.Amount), Completed: t.Target.Completed,
		},
	}
}

func (tm *TaskMgr) fromPB(dbt *pb.DBTask) {
	for _, v := range dbt.Tasks {
		rt := time.Unix(v.RefreshTime, 0)
		tm.tasks[v.CfgId] = &TaskInfo{
			ID: int(v.CfgId), Class: v.Class, Status: v.Status, ResetTime: rt,
			Target: &TargetInfo{
				TargetType: v.Target.Type, TargetId: int(v.Target.TargetId),
				TargetNum: int(v.Target.TargetNum), Amount: int(v.Target.Amount), Completed: v.Target.Completed,
			},
		}
	}
}

func (tm *TaskMgr) add(id int32, preTaskId int32) *TaskInfo {
	cfg, err := global.GetTaskCfg(id)
	if err != nil {
		return nil
	}

	for _, v := range tm.tasks {
		if v.ID == int(id) {
			return nil
		}
		vCfg, err := global.GetTaskCfg(int32(v.ID))
		if err != nil {
			return nil
		}
		//日活跃 周活跃任务不同等级互斥 Class相同&&Typeson相同
		if vCfg.Class == int32(pb.TaskClass_DAILY_ACTIVITY) || vCfg.Class == int32(pb.TaskClass_WEEKLY_ACTIVITY) ||
			vCfg.Class == int32(pb.TaskClass_DAILY) || vCfg.Class == int32(pb.TaskClass_WEEKLY) {
			if cfg.Typeson == vCfg.Typeson && cfg.Class == vCfg.Class && cfg.Type == vCfg.Type {
				return nil
			}
		}

	}

	if len(cfg.Level) == 2 {
		if tm.level < cfg.Level[0] || tm.level > cfg.Level[1] {
			return nil
		}
	}
	// vip等级对应的任务
	if cfg.Class == int32(pb.TaskClass_VIP) {
		if tm.vip < cfg.VipLevel-1 {
			return nil
		}
	}
	//七日狂欢对应的任务
	if cfg.Class == int32(pb.TaskClass_SEVENDAY) {
		if tm.CreateDay == 0 {
			tm.CreateDay = 1
		}
		//如果活动已经结束了，就不要给他派了
		actCfg, err := global.GetActivityCfg(int32(cfg.ActivID))
		if err != nil {
			return nil
		}
		if tm.CreateDay > actCfg.ResetTime {
			return nil
		}
		if int32(tm.CreateDay) < int32(cfg.SevenDay) {
			return nil
		}
	}
	t := &TaskInfo{
		ID: int(cfg.UUID), Class: pb.TaskClass(cfg.Class), Status: pb.TaskStatus_IN_PROGRESS,
		Target: &TargetInfo{TargetType: pb.TargetType(cfg.Type), TargetId: int(cfg.Typeson), TargetNum: int(cfg.Limit), Amount: 0, Completed: false},
	}

	if t.Class == pb.TaskClass_WEEKLY || t.Class == pb.TaskClass_WEEKLY_ACTIVITY {
		now := time.Now()
		// 获取当前日期的0点时间
		midnight := time.Date(now.Year(), now.Month(), now.Day(), 0, 0, 0, 0, now.Location())
		// 计算7天后的0点时间
		nextMidnightInSevenDays := midnight.AddDate(0, 0, 7)
		t.ResetTime = nextMidnightInSevenDays
	}
	if t.Class == pb.TaskClass_DAILY || t.Class == pb.TaskClass_ROUTINE || t.Class == pb.TaskClass_DAILY_ACTIVITY {
		now := time.Now()
		// 获取当前日期的0点时间
		midnight := time.Date(now.Year(), now.Month(), now.Day(), 0, 0, 0, 0, now.Location())
		// 计算1天后的0点时间
		nextMidnightInSevenDays := midnight.AddDate(0, 0, 1)
		t.ResetTime = nextMidnightInSevenDays
	}

	// 当前任务的继承任务是否需要重置计数条件
	var preTask *TaskInfo
	if preTaskId > 0 && !cfg.ResetMark {
		if preTask = tm.getTaskById(preTaskId); preTask != nil {
			t.Target.Amount = preTask.Target.Amount
		}
	}

	switch t.Target.TargetType {
	case pb.TargetType_ROLE_POWER_REQUIREMENT:
		t.Target.Amount = int(tm.totalCombat)
	case pb.TargetType_ROLE_LEVEL_REQUIREMENT:
		t.Target.Amount = int(tm.level)
	}
	tm.tasks[int32(t.ID)] = t

	packet := &pb.TaskAcceptPush{Task: tm.toPBTaskInfo(t)}
	tm.pushMsg(protocol.RouteTaskAcceptPush, packet)

	tm.checkProgress(t)
	return t
}

func (tm *TaskMgr) del(id int32) {
	if t := tm.getTaskById(id); t != nil {

		packet := &pb.TaskRemovePush{Task: tm.toPBTaskInfo(t)}

		tm.pushMsg(protocol.RouteTaskRemovePush, packet)
		//删除任务
		delete(tm.tasks, id)
	}
}

func (tm *TaskMgr) checkProgress(t *TaskInfo) {
	if t.Status == pb.TaskStatus_COMPLETED || t.Status == pb.TaskStatus_REWARDED {
		return
	}

	if t.Target.Completed {
		t.Status = pb.TaskStatus_COMPLETED
	}

	if t.Class == pb.TaskClass_ROUTINE {
		if tm.checkTime(t) {
			t.Target.Completed = true
			t.Status = pb.TaskStatus_COMPLETED
			tm.update(t)
		}
		return
	}
	if t.Target.TargetType == pb.TargetType_SKILL_UPGRADING_REQUIREMENT {
		if skill := tm.getSkill(int32(t.Target.TargetId)); skill != nil {
			t.Target.Amount = t.Target.TargetNum
			t.Target.Completed = true
			t.Status = pb.TaskStatus_COMPLETED
		} else {
			for _, v := range tm.skills {
				if t.Target.TargetId/100 == int(v.CfgId)/100 {
					if int(v.CfgId)%100 >= t.Target.TargetId%100 {
						t.Target.Amount = t.Target.TargetNum
						t.Target.Completed = true
						t.Status = pb.TaskStatus_COMPLETED
					}
				}
			}
		}
	}
	if t.Target.TargetType == pb.TargetType_VIP_LEVEL {
		t.Target.Amount = int(tm.vip)
		tm.update(t)
	}
	if t.Target.TargetType == pb.TargetType_BATTLE_CHAPTERS_CLEARED {
		// 查询当前章节
		if tm.InstanceData != nil {
			if int(tm.InstanceData.BattleMap.BattleId) >= t.Target.TargetId {
				t.Target.Amount = t.Target.TargetNum
				t.Target.Completed = true
			}
		}
	}
	if t.Target.TargetType == pb.TargetType_TRIAL_TOWER_FLOORS_CLEARED {
		// 查询当前章节
		if tm.InstanceData != nil {
			if int(tm.InstanceData.TrialTowerMap.BattleId) >= t.Target.TargetId {
				t.Target.Amount = t.Target.TargetNum
				t.Target.Completed = true
			}
		}
	}

	if t.Target.TargetType == pb.TargetType_RELIC_CHAPTERS_CLEARED {
		// 查询当前章节
		if tm.InstanceData != nil {
			if int(tm.InstanceData.RelicMap.BattleId) >= t.Target.TargetId {
				t.Target.Amount = t.Target.TargetNum
				t.Target.Completed = true
			}
		}
	}
	if t.Target.TargetType == pb.TargetType_BOSS_LEVELS_CLEARED {
		// 查询当前章节
		if tm.InstanceData != nil {
			if int(tm.InstanceData.BossMap.BattleId) >= t.Target.TargetId {
				t.Target.Amount = t.Target.TargetNum
				t.Target.Completed = true
			}
		}
	}
	//任务：伙伴等级
	if t.Target.TargetType == pb.TargetType_UPGRADE_ANY_PARTNER_LEVEL {
		if tm.InstanceData != nil {
			for _, v := range tm.partnerSlots {
				if v.PartnerUid > 0 {
					if par := tm.getPartnerInfo(v.PartnerUid); par != nil {
						if par.Level() >= int32(t.Target.TargetId) {
							t.Target.Amount = t.Target.TargetNum
							t.Target.Completed = true
							break
						}
					}
				}
			}

		}
	}
	//解锁伙伴槽位
	if t.Target.TargetType == pb.TargetType_UNLOCK_PARTNER_SLOT {
		if tm.InstanceData != nil {
			num := 0
			for _, v := range global.GlobalCfg.Openslot.IDLevel {
				if tm.level >= v.Needlevel {
					num++
				}
			}
			t.Target.Amount = num
		}
		tm.update(t)
	}
	//装备强化等级
	if t.Target.TargetType == pb.TargetType_ANY_EQUIP_STRENGTHENED_TO_LEVEL {
		if tm.InstanceData != nil {
			for _, v := range tm.equips {
				if v.Enhance >= int32(t.Target.TargetId) {
					t.Target.Amount = t.Target.TargetNum
					t.Target.Completed = true
					break
				}
			}
		}
	}
	//宠物等级
	if t.Target.TargetType == pb.TargetType_ANY_PET_UPGRADED_TO_LEVEL {
		if tm.InstanceData != nil {
			for _, v := range tm.pet {
				if v.Level >= int32(t.Target.TargetNum) {
					t.Target.Amount = t.Target.TargetNum
					t.Target.Completed = true
					break
				}
			}
		}
		tm.update(t)
	}
	//宝箱等级
	if t.Target.TargetType == pb.TargetType_CHEST_LEVEL_UPGRADED {
		if tm.InstanceData != nil {
			t.Target.Amount = int(tm.box.NowLevel)
		}
		tm.update(t)
	}
	//查询购买次数
	if pb.TargetType(t.Target.TargetType) == pb.TargetType_SEVENDAY_TASK_GIFT_PURCHASED {
		if tm.InstanceData != nil {
			//查询购买次数
			ress, err := db.StoreTotal(tm.uid, constant.ShopTypeSevenDaysFun)
			if err != nil {
				return
			}
			t.Target.Amount = len(ress)
		}
		tm.update(t)
	}
	if t.Target.TargetNum <= t.Target.Amount || t.Status == pb.TaskStatus_COMPLETED {
		t.Target.Completed = true
		t.Status = pb.TaskStatus_COMPLETED
		tm.update(t)
	}
	// logger.Debugf("任务进度 ID=%d  Amount=%d  TargetNum=%d", t.ID, t.Target.Amount, t.Target.TargetNum)
}

func (tm *TaskMgr) update(t *TaskInfo) {
	packet := &pb.TaskUpdatePush{Task: tm.toPBTaskInfo(t)}
	tm.pushMsg(protocol.RouteTaskUpdatePush, packet)
}

// 完成任务
func (tm *TaskMgr) complate(id int32) error {
	t := tm.getTaskById(id)
	if t == nil {
		return errutil.ErrTaskNotFound
	}

	if t.Status != pb.TaskStatus_COMPLETED {
		return errutil.ErrTaskStatus
	}
	cfg, err := global.GetTaskCfg(int32(t.ID))
	if err != nil {
		return errutil.ErrTaskNotFound
	}

	if cfg.Class == int32(pb.TaskClass_ACHIEVEMENT) {
		if !tm.checkTime(t) {
			return errutil.ErrTaskStatus
		}
	}
	t.Status = pb.TaskStatus_REWARDED
	tm.update(t)

	for _, v := range cfg.Reward {
		tm.addSource(v.Key, int64(v.Value), constant.ItemAddTask)
	}

	if cfg.NextAchievement != 0 {
		tm.add(cfg.NextAchievement, int32(t.ID))
	}
	// 活跃度任务
	if cfg.ActivityReward.Value > 0 {
		if t.Class == pb.TaskClass_DAILY {
			tm.dispatch(pb.TargetType_DAILY_ACTIVITY_LEVEL, 0, cfg.ActivityReward.Value)
		}
		if t.Class == pb.TaskClass_WEEKLY {
			tm.dispatch(pb.TargetType_WEEKLY_ACTIVITY_LEVEL, 0, cfg.ActivityReward.Value)
		}
		if t.Class == pb.TaskClass_SEVENDAY {
			tm.dispatch(pb.TargetType_SEVENDAY_TASK_ALL_COMPLETED, 0, cfg.ActivityReward.Value)
		}
	}
	//上报
	tm.upTaskShushuDate(cfg)
	return nil
}

func (p *Player) upTaskShushuDate(cfg *global.TaskConfig) {
	if p.bCreate {
		return
	}
	//创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	//upBaseInfo的数据合并进去
	baseInfo := p.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	//在合并点其他的数据

	// 替换cfg.TaskDes中的{0}为cfg.Limit的值
	taskName := cfg.TaskDes
	if cfg.Limit > 0 {
		taskName = strings.Replace(taskName, "{0}", strconv.Itoa(int(cfg.Limit)), -1)
	}
	shushuDate["task_name"] = taskName
	shushuDate["name"] = "task_complete"
	var taskType string
	classIdx := int32(cfg.Class)
	if classIdx >= 0 && int(classIdx) <= len(constant.TaskName) {
		taskType = constant.TaskName[classIdx]
	} else {
		taskType = ""
	}
	shushuDate["task_type"] = taskType
	shushuDate["task_id"] = cfg.UUID
	itemInfo := make([]map[string]interface{}, 0)
	for _, v := range cfg.Reward {
		if v.Value > 0 {
			//查询itemname
			if itemCfg, err := global.GetItemCfg(int32(v.Key)); err == nil {
				item := map[string]interface{}{
					"item_id":   itemCfg.ID,
					"item_name": itemCfg.Name,
					"item_num":  v.Value,
				}
				itemInfo = append(itemInfo, item)
			}
		}
	}
	shushuDate["get_item_info"] = itemInfo

	shushumain.ReportData(shushuDate)

}

// 时间段比较
func (tm *TaskMgr) checkTime(t *TaskInfo) bool {
	cfg, err := global.GetTaskCfg(int32(t.ID))
	if err != nil {
		return false
	}

	if cfg.Class == int32(pb.TaskClass_ROUTINE) {
		if start, err := time.Parse("15:04", cfg.LimitTime.Start); err == nil {
			if start.Hour() > time.Now().Hour() {
				return false
			} else if start.Hour() == time.Now().Hour() && start.Minute() > time.Now().Minute() {
				return false
			}
		}
		if end, err := time.Parse("15:04", cfg.LimitTime.End); err == nil {
			if end.Hour() < time.Now().Hour() {
				return false
			} else if end.Hour() == time.Now().Hour() && end.Minute() < time.Now().Minute() {
				return false
			}
		}
	}
	return true
}
