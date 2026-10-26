package provide

import (
	"bytes"
	"encoding/json"
	"io"
	"net/http"
	"strconv"
	"sync"
	"time"

	"nanoserver/db"
	"nanoserver/global"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/errutil"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"

	"github.com/gomodule/redigo/redis"
	"github.com/lonng/nano/serialize/protobuf"
	log "github.com/sirupsen/logrus"
	"github.com/spf13/viper"
)

var (
	logger = log.WithField("component", "game")
	config protocol.ClientConfig // 远程配置

)

type Rank struct {
	LampLock sync.RWMutex
	chLamp   chan *pb.LampPush
}

func PrivilegeListTCP(uid int64) ([]protocol.PrivilegeDetailTCP, error) {
	privilegeCfg := global.PrivilegeCfg
	// result := make(map[int32][]protocol.SourceInfo)
	result := make([]protocol.PrivilegeDetailTCP, 0)
	// 特权卡
	for _, privilegen := range privilegeCfg {
		endtime := time.Now()
		goodsId := make([]int64, 0)
		request2 := &protocol.StoreListRequest{}
		// request2.Uid = uid
		request2.StoreType = privilegen.Sid / 1000
		request2.GoodsUuid = privilegen.Sid
		// request.StoreType = r.StoreType
		// request.GoodsId = r.GoodsId
		// 查询用户购买情况
		day := 30
		switch privilegen.Pid {
		case 1:
			day = 7
		case 2:
			day = 30
		case 3:
			day = 30 * 3
		}
		goodsId = append(goodsId, int64(privilegen.Sid))
		resultb, _ := db.StoreList(
			uid,
			request2.StoreType,
			int32(day),
			goodsId,
		)
		if len(resultb) > 0 {
			originalTime := time.Unix(resultb[0].CreateTime, 0)
			nextDayMidnight := originalTime.AddDate(0, 0, int(privilegen.Duration)) // 添加N天
			nextDayMidnight = time.Date(nextDayMidnight.Year(), nextDayMidnight.Month(), nextDayMidnight.Day(), 0, 0, 0, 0, nextDayMidnight.Location())
			// 转换回时间戳
			endtime = nextDayMidnight

			result = append(result, protocol.PrivilegeDetailTCP{
				Pid:     privilegen.Pid,
				EndTime: endtime,
			})
		}

	}
	return result, nil
}

// 是不是第一名
func IsFirst(Uid int64, Rtype int, serverId int32) []int {
	returnRankList := make([]int, 0)
	// rTypeName := map[int]string{
	// 	1: "等级", 2: "战力", 3: "乱斗", 4: "试炼", 6: "PVP", 7: "赛季", 20: "世界boss"}
	if Rtype == 0 {
		for k := range constant.RankTypeName {
			rank := strconv.Itoa(int(serverId)) + "rank:"
			// 查看第一名
			resultScores5, _ := CmdCache("zrevrange", rank+strconv.Itoa(int(k)), "0", "0", "WITHSCORES")
			resultsValue5, ok := resultScores5.([]interface{})
			if !ok {
				// logger.Error("resultsValue5 is not a slice of interfaces")
			}
			// 遍历结果，每两个元素是一个成员和它的分数
			for i := 0; i < len(resultsValue5); i += 2 {
				// 成员
				member, err := redis.Int64(resultsValue5[i], nil)
				if err != nil {
					// logger.Error("Error converting member to string:", err)
					continue
				}
				if strconv.FormatInt(member, 10) == strconv.FormatInt(Uid, 10) {
					// 发送消息给所有在线用户
					returnRankList = append(returnRankList, k)
				}

			}
			// if Firstscore == r.Value {

			// 	// strSend := []string{r.Name, rTypeName[int(r.Type)]}

			// 	// 发送消息给所有在线用户
			// 	// game.Lamp(int32(constant.LampTopRankChanged), strSend)
			// 	// 发送消息给所有在线用户
			// 	// desk :=
			// 	// desk.chLamp <- &pb.LampPush{
			// 	// 	Type: pb.LampType_LampTopRankChanged,
			// 	// 	Data: fmt.Sprintf("%s", r.Name),
			// 	// }
		}
	} else {

		rank := strconv.Itoa(int(serverId)) + "rank:"
		// 查看第一名
		resultScores5, _ := CmdCache("zrevrange", rank+strconv.Itoa(int(Rtype)), "0", "0", "WITHSCORES")
		resultsValue5, ok := resultScores5.([]interface{})
		if !ok {
			// logger.Error("resultsValue5 is not a slice of interfaces")
		}
		// 遍历结果，每两个元素是一个成员和它的分数
		for i := 0; i < len(resultsValue5); i += 2 {
			// 成员
			member, err := redis.Int64(resultsValue5[i], nil)
			if err != nil {
				// logger.Error("Error converting member to string:", err)
				continue
			}
			if strconv.FormatInt(member, 10) == strconv.FormatInt(Uid, 10) {
				// 发送消息给所有在线用户
				returnRankList = append(returnRankList, Rtype)
			}
		}
	}
	return returnRankList
}

// 排行榜数据录入
func RankChangeTCP(r *protocol.RankUserInfoInput) (bool, error) {
	// 如果是世界boss，需要录入到排行榜
	rankMember := r.Uid
	if r.Type == constant.RankTypeWorldBossKf {
		rank := strconv.Itoa(int(r.ServerId)) + "rank:"
		user := strconv.Itoa(int(r.ServerId)) + "user:"
		rankEnd := "99"

		name := "name"
		cmd := "hmset"
		rname := r.Name
		key := rank + strconv.Itoa(int(r.Type))
		key2 := user + strconv.FormatInt(rankMember, 10)
		_, err2 := CmdCache(cmd, key2, name, rname)
		if err2 != nil {
			return true, err2
		}
		// 用户的分数
		// resultScores, err5 := CmdCache("zscore", key, strconv.FormatInt(r.Uid, 10))
		// if resultScores == nil {
		// 	err := ZAddCache(key, r.Value, r.Uid)
		// 	if err != nil {
		// 		return true, err
		// 	}
		// } else {
		// 	if err5 != nil {
		// 		return false, err5
		// 	}
		// 	resultScores2, ok2 := resultScores.(interface{})
		// 	if !ok2 {
		// 		//logger.Error("resultScores is not a slice of interfaces")
		// 		return false, nil
		// 	}
		// 	OwnScore, err7 := redis.Int64(resultScores2, nil)
		// 	if err7 != nil {
		// 		//logger.Error("Error converting member to string:", err7)
		// 	}
		// 	err := ZincrbyCache(key, r.Value+OwnScore, r.Uid)
		// 	if err != nil {
		// 		return true, err
		// 	}
		// }
		err := ZincrbyCache(key, r.Value, rankMember)
		if err != nil {
			return true, err
		}
		// 落地到数据库
		// 如果大于第100个人，那么就更新到数据库
		resultScores3, _ := CmdCache("zrevrange", rank, rankEnd, rankEnd, "WITHSCORES")
		resultsValue, ok := resultScores3.([]interface{})
		if !ok {
			// logger.Error("resultsValue3 is not a slice of interfaces")
		}
		score := int64(0)
		// 遍历结果，每两个元素是一个成员和它的分数
		for i := 0; i < len(resultsValue); i += 2 {
			UserScore, err := redis.Int64(resultsValue[i+1], nil)
			if err != nil {
				// logger.Error("Error converting score to float64:", err)
				continue
			}
			// if UserScore < score {
			// 	score = UserScore
			// }
			score = UserScore
		}
		if r.Value > score {
			rankInfo := &protocol.RankOperateRequest{
				Type: int64(r.Type),
				All:  2,
			}
			res, _ := RankList(rankInfo)
			zip := pb.RankInfo{
				List: []*pb.Rank{},
			}
			for _, v := range res.RankAll {
				zip.List = append(
					zip.List,
					&pb.Rank{
						Uid:      v.Uid,
						Num:      v.Num,
						Avatar:   v.Avatar,
						Name:     v.Name,
						Value:    v.Value,
						Sex:      v.Sex,
						Job:      v.Job,
						RobotCId: v.RobotCId,
					})
			}
			jsonBytes, err := protobuf.NewSerializer().Marshal(&zip)
			if err != nil {
				// logger.Error("Error Marshal:", err)
			}
			_, err = db.RankUpdate(r.Type, jsonBytes)
			if err != nil {
				// logger.Error("Error Update Mysql:", err)
			}
		}
		return true, nil
	} else {
		rank := strconv.Itoa(int(r.ServerId)) + "rank:"
		user := strconv.Itoa(int(r.ServerId)) + "user:"
		rankEnd := "99"
		name := "name"
		avatar := "avatar"
		sex := "sex"
		job := "job"
		robot_cid := "robot_cid"
		cmd := "hmset"
		rname := r.Name
		ravatar := r.Avatar
		rsex := strconv.Itoa(int(r.Sex))
		rjob := strconv.Itoa(int(r.Job))
		rrobot_cid := strconv.Itoa(int(r.RobotCId))
		key := rank + strconv.Itoa(int(r.Type))
		// 需要比较就查询比大小
		if r.Type == constant.RankTypeSeason {
			for _, v := range global.GlobalCfg.PVPSeason.Season {
				if start, err := time.Parse(time.DateOnly, v.StartTime); err == nil {
					if time.Now().Before(start) {
						continue
					}
				} else {
					continue
				}
				if end, err := time.Parse(time.DateOnly, v.EndTime); err == nil {
					if time.Now().After(end) {
						continue
					}
				}
				r.Type = r.Type*10000 + int32(v.ID)
				break
			}
		}

		key2 := user + strconv.Itoa(int(rankMember))
		_, err2 := CmdCache(cmd, key2, name, rname, avatar, ravatar, sex, rsex, job, rjob, robot_cid, rrobot_cid)
		if err2 != nil {
			return true, err2
		}
		if r.Compare {
			// 用户的分数
			resultScores, err5 := CmdCache("zscore", rank+strconv.Itoa(int(r.Type)), strconv.FormatInt(rankMember, 10))
			if resultScores == nil {
				err := ZAddCache(key, r.Value, rankMember)
				if err != nil {
					return true, err
				}
			} else {
				if err5 != nil {
					return false, err5
				}
				resultScores2, ok2 := resultScores.(interface{})
				if !ok2 {
					// logger.Error("resultScores is not a slice of interfaces")
					return false, nil
				}
				OwnScore, err7 := redis.Int64(resultScores2, nil)
				if err7 != nil {
					// logger.Error("Error converting member to string:", err7)
				}
				if OwnScore < r.Value {
					err := ZAddCache(key, r.Value, rankMember)
					if err != nil {
						return true, err
					}
				}
			}

		} else {
			err := ZAddCache(key, r.Value, rankMember)
			if err != nil {
				return true, err
			}
		}

		// 落地到数据库
		// 如果大于第100个人，那么就更新到数据库
		resultScores3, _ := CmdCache("zrevrange", rank+strconv.Itoa(int(r.Type)), rankEnd, rankEnd, "WITHSCORES")
		resultsValue, ok := resultScores3.([]interface{})
		if !ok {
			// logger.Error("resultsValue3 is not a slice of interfaces")
		}
		score := int64(0)
		// 遍历结果，每两个元素是一个成员和它的分数
		for i := 0; i < len(resultsValue); i += 2 {
			UserScore, err := redis.Int64(resultsValue[i+1], nil)
			if err != nil {
				// logger.Error("Error converting score to float64:", err)
				continue
			}
			// if UserScore < score {
			// 	score = UserScore
			// }
			score = UserScore
		}
		if r.Value > score {
			rankInfo := &protocol.RankOperateRequest{
				Type: int64(r.Type),
				All:  2,
			}
			res, _ := RankList(rankInfo)
			zip := pb.RankInfo{
				List: []*pb.Rank{},
			}
			for _, v := range res.RankAll {
				zip.List = append(
					zip.List,
					&pb.Rank{
						Uid:      v.Uid,
						Num:      v.Num,
						Avatar:   v.Avatar,
						Name:     v.Name,
						Value:    v.Value,
						Sex:      v.Sex,
						Job:      v.Job,
						RobotCId: v.RobotCId,
					})
			}
			jsonBytes, err := protobuf.NewSerializer().Marshal(&zip)
			if err != nil {
				// logger.Error("Error Marshal:", err)
			}
			_, err = db.RankUpdate(r.Type, jsonBytes)
			if err != nil {
				// logger.Error("Error Update Mysql:", err)
			}
		}

		if r.Type == constant.RankTypeWorldBoss {
			// 发送跨服消息
			RankChangeTCP(&protocol.RankUserInfoInput{
				Type:     21,
				Uid:      int64(r.RankServerId),
				Name:     r.ServerName,
				Value:    r.Value,
				Avatar:   r.Avatar,
				Sex:      r.Sex,
				Job:      r.Job,
				RobotCId: r.RobotCId,
				ServerId: 0,
			})
		}
		return true, nil
	}
}

// // 排行榜
// const (
// RankTypeLevel           = 1  // 等级榜
// RankTypeFight           = 2  // 战力榜
// RankTypeSoul            = 3  // PVP胜场数量
// RankTypeTrial           = 4  // 试炼榜
// RankTypePvp             = 6  // 每日9点的计算(当天战魂)
// RankTypeSeason          = 7  // 赛季排行榜
// RankTypeWorldBoss       = 20 // 世界boss
// RankTypeWorldBossKf     = 21 // 世界boss的跨服榜
// RankTypeGightEnd        = 22 // 一战到底总榜
// RankTypeGightEndlayers  = 23 // 一战到底每一层榜单
// RankTypeTurnBased       = 24 // 回合制排行榜
// RankTypeSoulWeekly      = 25 // pvp胜场周榜
// RankTypeTurnBasedWeekly = 26 // 回合制周榜
// )

// 一站到底
func CronSendRankAwardOne() {
	rtype := []int64{22}
	err := SendRankAward(rtype, false)
	logger.Debug("发奖励了", rtype)
	if err != nil {
		// logger.Error("Rank err CronSendRankAwardOne", err)
	}
}

// 战力榜和即时赛榜，晚上10点发
func CronSendRankAwardPvp() {
	rtype := []int64{2, 3, 24}
	logger.Debug("发奖励了", rtype)
	err := SendRankAward(rtype, false)
	if err != nil {
		// logger.Error("Rank err CronSendRankAwardPvp", err)
	}
}

// 世界boss榜，晚上9点发
func CronSendRankAwardBoss() {
	rtype := []int64{20, 21}
	logger.Debug("发奖励了", rtype)
	err := SendRankAwardAll(rtype, false)
	if err != nil {
		// logger.Error("Rank err CronSendRankAwardBoss", err)
	}
}

// 每周一0点发放周结奖励
func CronSendRankAwardWeekly() {
	rtype := []int64{2, 3}
	logger.Debug("发奖励了", rtype)
	err := SendRankAward(rtype, true)
	if err != nil {
		// logger.Error("Rank err CronSendRankAwardBoss", err)
	}

	rtype2 := []int64{24}
	logger.Debug("发奖励了", rtype2)
	err = SendRankAwardAll(rtype2, true)
	if err != nil {
		// logger.Error("Rank err CronSendRankAwardBoss", err)
	}
	rankType := []int64{
		constant.RankTypeSoul,
		constant.RankTypeTurnBased,
	}
	if err := DelRank(rankType); err != nil {
		logger.Error("Failed to delete rank:", err)
	}
	// provide.DelRank(rankType)
}

func SendRankAward(rType []int64, weekly bool) error {
	// rankAwardCfg := global.RankingAwardCfg
	// rTypeName := map[int64]string{
	// 	1: "等级", 2: "战力", 3: "乱斗", 4: "试炼", 6: "PVP", 7: "赛季", 20: "世界boss"}
	// result := make(map[int32][]protocol.SourceInfo)
	// 活动内容
	// activity := make(map[int32][]global.KeyValue)
	// for _, activityn := range rankAwardCfg {
	// 	activity[activityn.UUID] = activityn.Award
	// }

	// ServerIds, err := GetAllServerId()
	// if err != nil {
	// 	return err
	// }
	// if len(ServerIds) == 0 {
	// 	return nil
	// }
	ServerIds := make([]int32, 0)
	for i := int(global.ServerIDRangeStart); i < int(global.ServerIDRangeEnd); i++ {
		ServerIds = append(ServerIds, int32(i))
	}
	for _, serverId := range ServerIds {
		// 查询所有服务器Id
		for _, item := range rType {
			oldType := item
			NewType := item
			if weekly {
				if constant.RankTypeWeekly[item] != 0 {
					NewType = constant.RankTypeWeekly[item]
				}
			}
			if item == 7 {
				for _, v := range global.GlobalCfg.PVPSeason.Season {
					if start, err := time.Parse(time.DateOnly, v.StartTime); err == nil {
						if time.Now().Before(start) {
							continue
						}
					} else {
						continue
					}
					if end, err := time.Parse(time.DateOnly, v.EndTime); err == nil {
						if time.Now().After(end) {
							continue
						}
					}
					item = oldType*10000 + int64(v.ID)
					NewType = item + int64(v.ID)
					break
				}
			}
			rank := strconv.Itoa(int(serverId)) + "rank:"
			rankBegin := "0"
			rankEnd := "99"
			RankAll := make([]protocol.RankUserInfo, 0)

			results, err2 := CmdCache("zrevrange", rank+strconv.Itoa(int(item)), rankBegin, rankEnd, "WITHSCORES")
			if results == nil {
				continue
			}
			if err2 != nil {
				continue
			}
			// 初始化一个映射来存储每个人及其对应的分数
			// 检查结果是否为 []interface{}
			resultsValue, ok := results.([]interface{})
			if !ok {
				// logger.Error("resultsValue is not a slice of interfaces")
			}
			// 遍历结果，每两个元素是一个成员和它的分数
			for i := 0; i < len(resultsValue); i += 2 {
				member, err := redis.Int64(resultsValue[i], nil)
				if err != nil {
					// logger.Error("Error converting member to string:", err)
					continue
				}
				score, err := redis.Int64(resultsValue[i+1], nil)
				if err != nil {
					// logger.Error("Error converting score to float64:", err)
					continue
				}

				//if err != nil {
				//	continue
				//}
				RankAll = append(RankAll, protocol.RankUserInfo{
					Uid:   member,
					Num:   int64(i/2 + 1),
					Value: score,
				})
			}
			for _, item2 := range RankAll {
				// if activity[int32(NewType)*10000+int32(item2.Num)] == nil {
				// 	continue
				// }
				// result2 := activity[int32(NewType)*10000+int32(item2.Num)]
				// MailId := strconv.FormatInt(snowflake.GenID(), 10)
				result2 := global.GetRankAwardCfg(int32(NewType)*10000 + int32(item2.Num))
				result3 := make([]protocol.Attachment, len(result2.Award))
				// MailId := strconv.FormatInt(snowflake.GenID(), 10)
				for i, Attachment := range result2.Award {
					result3[i] = protocol.Attachment{
						MailId:      "",
						UserPropsId: int64(Attachment.Key),
						Num:         int64(Attachment.Value),
					}
				}
				resultc, err3 := db.MailAdd(
					-1,
					item2.Uid,
					"系统邮件：排行榜奖励",
					constant.RankTypeName[int(oldType)]+"排行榜奖励",
					result3,
					"RankAward-"+strconv.Itoa(int(item)),
				)
				if !resultc {
					return err3
				}
			}
			time.Sleep(time.Millisecond)
		}
	}

	return nil
}

func SendRankAwardAll(rType []int64, weekly bool) error {
	// rankAwardCfg := global.RankingAwardCfg
	// rTypeName := map[int64]string{
	// 	1: "等级", 2: "战力", 3: "乱斗", 4: "试炼", 6: "PVP", 7: "赛季", 20: "世界boss"}
	// result := make(map[int32][]protocol.SourceInfo)
	// 活动内容
	// activity := make(map[int32][]global.KeyValue)
	// for _, activityn := range rankAwardCfg {
	// 	activity[activityn.UUID] = activityn.Award
	// }

	// ServerIds, err := GetAllServerId()
	// if err != nil {
	// 	return err
	// }
	// if len(ServerIds) == 0 {
	// 	return nil
	// }
	// 查询所有服务器Id
	for _, item := range rType {
		oldType := item
		NewType := item
		if weekly {
			if constant.RankTypeWeekly[item] != 0 {
				NewType = constant.RankTypeWeekly[item]
			}
		}
		rank := strconv.Itoa(int(0)) + "rank:"
		rankBegin := "0"
		rankEnd := "99"
		RankAll := make([]protocol.RankUserInfo, 0)

		results, err2 := CmdCache("zrevrange", rank+strconv.Itoa(int(item)), rankBegin, rankEnd, "WITHSCORES")
		if results == nil {
			// logger.Error("results1 is not a slice of interfaces")
			continue
		}
		if err2 != nil {
			// logger.Error("results1 is not a slice of interfaces")
			continue
		}
		// 初始化一个映射来存储每个人及其对应的分数
		// 检查结果是否为 []interface{}
		resultsValue, ok := results.([]interface{})
		if !ok {
			// logger.Error("resultsValue is not a slice of interfaces")
			continue
		}
		// 遍历结果，每两个元素是一个成员和它的分数
		for i := 0; i < len(resultsValue); i += 2 {
			member, err := redis.Int64(resultsValue[i], nil)
			if err != nil {
				// logger.Error("Error converting member to string:", err)
				continue
			}
			score, err := redis.Int64(resultsValue[i+1], nil)
			if err != nil {
				// logger.Error("Error converting score to float64:", err)
				continue
			}

			//if err != nil {
			//	continue
			//}
			RankAll = append(RankAll, protocol.RankUserInfo{
				Uid:   member,
				Num:   int64(i/2 + 1),
				Value: score,
			})
		}
		for _, item2 := range RankAll {
			result2 := global.GetRankAwardCfg(int32(NewType)*10000 + int32(item2.Num))
			result3 := make([]protocol.Attachment, len(result2.Award))
			// MailId := strconv.FormatInt(snowflake.GenID(), 10)
			for i, Attachment := range result2.Award {
				result3[i] = protocol.Attachment{
					MailId:      "",
					UserPropsId: int64(Attachment.Key),
					Num:         int64(Attachment.Value),
				}
			}
			// 这里的item是一个服务器的

			// if activity[int32(NewType)*10000+int32(item2.Num)] == nil {
			// 	continue
			// }
			// result2 := activity[int32(NewType)*10000+int32(item2.Num)]
			// MailId := strconv.FormatInt(snowflake.GenID(), 10)

			// 查询20的榜单，在榜单的上的发奖励
			newrank := strconv.Itoa(int(item2.Uid)) + "rank:"
			results, err2 := CmdCache("zrevrange", newrank+strconv.Itoa(int(20)), rankBegin, rankEnd, "WITHSCORES")
			if results == nil {
				// logger.Error("results2 is not a slice of interfaces")
				continue
			}
			if err2 != nil {
				// logger.Error("results2 is not a slice of interfaces")
				continue
			}
			// 初始化一个映射来存储每个人及其对应的分数
			// 检查结果是否为 []interface{}
			resultsValue2, ok := results.([]interface{})
			if !ok {
				// logger.Error("resultsValue2 is not a slice of interfaces")
			}
			// 遍历结果，每两个元素是一个成员和它的分数
			for i := 0; i < len(resultsValue2); i += 2 {
				member, err := redis.Int64(resultsValue2[i], nil)
				if err != nil {
					// logger.Error("Error converting member to string:", err)
					continue
				}

				resultc, err3 := db.MailAdd(
					-1,
					member,
					"系统邮件：排行榜奖励",
					constant.RankTypeName[int(oldType)]+"排行榜奖励",
					result3,
					"RankAward-"+strconv.Itoa(int(item)),
				)
				if !resultc {
					// logger.Error("Error resultc member to string:", err)
					return err3
				}
			}

		}
		time.Sleep(time.Millisecond)
	}

	return nil
}

//	func DelRedis(rType []int64) error {
//		for _, item := range rType {
//			rank := "rank:"
//			rankBegin := "0"
//			rankEnd := "99"
//			results, err2 := CmdCache("zrevrange", rank+strconv.Itoa(int(item)), rankBegin, rankEnd, "WITHSCORES")
//			if err2 != nil {
//			}
//		}
//	}

// 查询所有服务器Id
func GetAllServerId() ([]int32, error) {
	respJson, err := RequestWeb(http.MethodPost, viper.GetString("admin.adminUrl")+"/api/v1/servername", BaseRequest{})
	if err != nil {
		return nil, err
	}
	var resp respParam
	err = json.Unmarshal(respJson, &resp)
	if err != nil {
		return nil, err
	}
	ServerId := make([]int32, 0)
	for _, server := range resp.Date.List {
		ServerId = append(ServerId, server.ServerId)
	}
	return ServerId, nil
}

// 查询所有服务器信息
func GetAllServerInfo() ([]ServerInfo, error) {
	respJson, err := RequestWeb(http.MethodPost, viper.GetString("admin.adminUrl")+"/api/v1/servername", BaseRequest{Show: 1})
	if err != nil {
		return nil, err
	}
	var resp respParam
	err = json.Unmarshal(respJson, &resp)
	if err != nil {
		return nil, err
	}
	ServerInfo := make([]ServerInfo, 0)
	ServerInfo = append(ServerInfo, resp.Date.List...)
	return ServerInfo, nil
}

// 根据具体返回的值填写
type respParam struct {
	ErrCode int32      `json:"code"`
	ErrMsg  string     `json:"error"`
	Date    ServerList `json:"data"`
}
type ServerList struct {
	Count int32        `json:"count"`
	List  []ServerInfo `json:"list"`
}
type ServerInfo struct {
	ServerId   int32  `json:"server_id"`
	ServerName string `json:"name"`
}

func DelRank(rType []int64) error {
	// 查询所有服务器Id
	// ServerIds, err := GetAllServerId()
	// if err != nil {
	// 	return err
	// }
	ServerIds := make([]int32, 0)
	for i := int(global.ServerIDRangeStart); i < int(global.ServerIDRangeEnd); i++ {
		ServerIds = append(ServerIds, int32(i))
	}
	if len(ServerIds) == 0 {
		return nil
	}
	// 遍历所有服务器Id
	ServerIds = append(ServerIds, 0)
	for _, serverId := range ServerIds {
		for _, item := range rType {
			rank := strconv.Itoa(int(serverId)) + "rank:"
			_, err2 := CmdCache("del", rank+strconv.Itoa(int(item)))
			if err2 != nil {
			}
		}
	}

	return nil
}

func RankList(r *protocol.RankOperateRequest) (protocol.RankDetail, error) {
	RankDetail := protocol.RankDetail{}
	RankDetail.RankAll = make([]protocol.RankUserInfo, 0)

	if r.Type == constant.RankTypeWorldBossKf {
		rank := strconv.Itoa(int(r.ServerId)) + "rank:"
		user := strconv.Itoa(int(r.ServerId)) + "user:"
		name := "name"
		avatar := "avatar"
		sex := "sex"
		job := "job"
		rankBegin := "0"
		rankEnd := "99"
		if r.Count == 0 {
			rankEnd = strconv.Itoa(r.Count - 1)
		}
		Uid := int64(r.Uid)
		beginType := r.Type

		RankDetail.RankOwn = protocol.RankUserInfo{
			Uid:  Uid,
			Name: r.Name,
		}
		if r.Type == 7 {
			for _, v := range global.GlobalCfg.PVPSeason.Season {
				if start, err := time.Parse(time.DateOnly, v.StartTime); err == nil {
					if time.Now().Before(start) {
						continue
					}
				} else {
					continue
				}
				if end, err := time.Parse(time.DateOnly, v.EndTime); err == nil {
					if time.Now().After(end) {
						continue
					}
				}
				r.Type = r.Type*10000 + int64(v.ID)
				break
			}
		}
		if r.All == 1 || r.All == 2 {
			results, err2 := CmdCache("zrevrange", rank+strconv.Itoa(int(r.Type)), rankBegin, rankEnd, "WITHSCORES")
			if err2 != nil {
				return RankDetail, err2
			}
			// 初始化一个映射来存储每个人及其对应的分数
			// 检查结果是否为 []interface{}
			resultsValue, ok := results.([]interface{})
			if !ok {
				// logger.Error("resultsValue is not a slice of interfaces")
				return RankDetail, nil
			}
			// 遍历结果，每两个元素是一个成员和它的分数
			for i := 0; i < len(resultsValue); i += 2 {
				member, err := redis.Int64(resultsValue[i], nil)
				if err != nil {
					// logger.Error("Error converting member to string:", err)
					continue
				}
				score, err := redis.Int64(resultsValue[i+1], nil)
				if err != nil {
					// logger.Error("Error converting score to float64:", err)
					continue
				}
				Info, err := CmdCache("hgetall", user+strconv.Itoa(int(member)))
				InfoValues, ok2 := Info.([]interface{})
				if !ok2 {
					// logger.Error("InfoValues is not a slice of interfaces")
					continue
				}
				rname := ""
				ravatar := ""

				for j := 0; j < len(InfoValues); j += 2 {
					member2, err3 := redis.String(InfoValues[j], nil)
					if err3 != nil {
						// logger.Error("Error converting member to string:", err)
						continue
					}
					values, err3 := redis.String(InfoValues[j+1], nil)
					if err3 != nil {
						// logger.Error("Error converting score to String:", err)
						continue
					}
					if member2 == name {
						rname = values
					}
					if member2 == avatar {
						ravatar = values
					}
				}
				//if err != nil {
				//	continue
				//}

				RankDetail.RankAll = append(RankDetail.RankAll, protocol.RankUserInfo{
					Uid:    member,
					Num:    int64(i/2 + 1),
					Avatar: ravatar,
					Name:   rname,
					Value:  score,
					Type:   beginType,
				})
			}
		}
		if r.All == 1 || r.All == 3 {
			// 个人的排名
			resultRank, err3 := CmdCache("zrevrank", rank+strconv.Itoa(int(r.Type)), strconv.FormatInt(Uid, 10))
			if err3 != nil {
				return RankDetail, err3
			}
			resultsValue2, ok := resultRank.(int64)
			if !ok {
				// logger.Error("resultRank is not a slice of interfaces")
				return RankDetail, nil
			}
			// 用户的分数
			resultScores3, err5 := CmdCache("zscore", rank+strconv.Itoa(int(r.Type)), strconv.FormatInt(Uid, 10))
			if err5 != nil {
				return RankDetail, err5
			}
			OwnScore, err7 := redis.Int64(resultScores3, nil)
			if err7 != nil {
				return RankDetail, err3
				// logger.Error("Error converting member to string:", err7)
			}
			// 用户的基本信息
			Info, err4 := CmdCache("hgetall", user+strconv.Itoa(int(Uid)))
			if err4 != nil {
				return RankDetail, err3
				// logger.Error("InfoValues is not a slice of interfaces")
			}
			InfoValues2, ok2 := Info.([]interface{})
			if !ok2 {
				return RankDetail, err3
				// logger.Error("InfoValues is not a slice of interfaces")
			}
			rname2 := ""
			ravatar2 := ""
			rsex2 := ""
			rjob2 := ""
			for j := 0; j < len(InfoValues2); j += 2 {
				member2, err6 := redis.String(InfoValues2[j], nil)
				if err6 != nil {
					// logger.Error("Error converting member to string:", err6)
					continue
				}
				values, err6 := redis.String(InfoValues2[j+1], nil)
				if err6 != nil {
					// logger.Error("Error converting score to String:", err6)
					continue
				}
				if member2 == name {
					rname2 = values
				}
				if member2 == avatar {
					ravatar2 = values
				}

				if member2 == sex {
					rsex2 = values
				}

				if member2 == job {
					rjob2 = values
				}
			}
			//if err != nil {
			//	continue
			//}
			EndSex2, _ := strconv.Atoi(rsex2)
			EnJob2, _ := strconv.Atoi(rjob2)
			RankDetail.RankOwn = protocol.RankUserInfo{
				Uid:    Uid,
				Num:    resultsValue2 + 1,
				Avatar: ravatar2,
				Name:   rname2,
				Value:  OwnScore,
				Type:   beginType,
				Sex:    int32(EndSex2),
				Job:    int32(EnJob2),
			}
		}
	} else {
		rank := strconv.Itoa(int(r.ServerId)) + "rank:"
		user := strconv.Itoa(int(r.ServerId)) + "user:"
		name := "name"
		avatar := "avatar"
		sex := "sex"
		job := "job"
		robot_cid := "robot_cid"
		rankBegin := "0"
		rankEnd := "99"
		if r.Count == 0 {
			rankEnd = strconv.Itoa(r.Count - 1)
		}
		Uid := r.Uid
		beginType := r.Type
		value := int64(0)
		if r.Type == constant.RankTypeSoul || r.Type == constant.RankTypeTurnBased {
			value = int64(global.GlobalCfg.BaseValue.PVPinitscore.Num)
		}
		RankDetail.RankOwn = protocol.RankUserInfo{
			Value: value,
			Uid:   Uid,
			Name:  r.Name,
		}
		if r.Type == 7 {
			for _, v := range global.GlobalCfg.PVPSeason.Season {
				if start, err := time.Parse(time.DateOnly, v.StartTime); err == nil {
					if time.Now().Before(start) {
						continue
					}
				} else {
					continue
				}
				if end, err := time.Parse(time.DateOnly, v.EndTime); err == nil {
					if time.Now().After(end) {
						continue
					}
				}
				r.Type = r.Type*10000 + int64(v.ID)
				break
			}
		}
		if r.All == 1 || r.All == 2 {
			results, err2 := CmdCache("zrevrange", rank+strconv.Itoa(int(r.Type)), rankBegin, rankEnd, "WITHSCORES")
			if err2 != nil {
				return RankDetail, err2
			}
			// 初始化一个映射来存储每个人及其对应的分数
			// 检查结果是否为 []interface{}
			resultsValue, ok := results.([]interface{})
			if !ok {
				// logger.Error("resultsValue is not a slice of interfaces")
				return RankDetail, nil
			}
			// 遍历结果，每两个元素是一个成员和它的分数
			for i := 0; i < len(resultsValue); i += 2 {
				member, err := redis.Int64(resultsValue[i], nil)
				if err != nil {
					// logger.Error("Error converting member to string:", err)
					continue
				}
				score, err := redis.Int64(resultsValue[i+1], nil)
				if err != nil {
					// logger.Error("Error converting score to float64:", err)
					continue
				}
				Info, err := CmdCache("hgetall", user+strconv.Itoa(int(member)))
				InfoValues, ok2 := Info.([]interface{})
				if !ok2 {
					// logger.Error("InfoValues is not a slice of interfaces")
					continue
				}
				rname := ""
				ravatar := ""
				rsex := ""
				rjob := ""
				rrobot_cid := ""
				for j := 0; j < len(InfoValues); j += 2 {
					member2, err3 := redis.String(InfoValues[j], nil)
					if err3 != nil {
						// logger.Error("Error converting member to string:", err)
						continue
					}
					values, err3 := redis.String(InfoValues[j+1], nil)
					if err3 != nil {
						// logger.Error("Error converting score to String:", err)
						continue
					}
					if member2 == name {
						rname = values
					}
					if member2 == avatar {
						ravatar = values
					}
					if member2 == sex {
						rsex = values
					}
					if member2 == job {
						rjob = values
					}
					if member2 == robot_cid {
						rrobot_cid = values
					}
				}
				//if err != nil {
				//	continue
				//}
				EndSex, err := strconv.Atoi(rsex)
				if err != nil {
					continue
				}
				EnJob, err := strconv.Atoi(rjob)
				if err != nil {
					continue
				}
				EnRobotCId, err := strconv.Atoi(rrobot_cid)
				if err != nil {
					continue
				}
				RankDetail.RankAll = append(RankDetail.RankAll, protocol.RankUserInfo{
					Uid:      member,
					Num:      int64(i/2 + 1),
					Avatar:   ravatar,
					Name:     rname,
					Sex:      int32(EndSex),
					Job:      int32(EnJob),
					RobotCId: int32(EnRobotCId),
					Value:    score,
					Type:     beginType,
				})
			}
		}
		if r.All == 1 || r.All == 3 {
			// 个人的排名
			resultRank, err3 := CmdCache("zrevrank", rank+strconv.Itoa(int(r.Type)), strconv.FormatInt(Uid, 10))
			if err3 != nil {
				return RankDetail, err3
			}
			resultsValue2, ok := resultRank.(int64)
			if !ok {
				// logger.Error("resultRank is not a slice of interfaces")
				return RankDetail, nil
			}
			// 用户的分数
			resultScores3, err5 := CmdCache("zscore", rank+strconv.Itoa(int(r.Type)), strconv.FormatInt(Uid, 10))
			if err5 != nil {
				return RankDetail, err5
			}
			OwnScore, err7 := redis.Int64(resultScores3, nil)
			if err7 != nil {
				return RankDetail, err3
				// logger.Error("Error converting member to string:", err7)
			}
			// 用户的基本信息
			Info, err4 := CmdCache("hgetall", user+strconv.Itoa(int(Uid)))
			if err4 != nil {
				return RankDetail, err3
				// logger.Error("InfoValues is not a slice of interfaces")
			}
			InfoValues2, ok2 := Info.([]interface{})
			if !ok2 {
				return RankDetail, err3
				// logger.Error("InfoValues is not a slice of interfaces")
			}
			rname2 := ""
			ravatar2 := ""
			rsex2 := ""
			rjob2 := ""
			for j := 0; j < len(InfoValues2); j += 2 {
				member2, err6 := redis.String(InfoValues2[j], nil)
				if err6 != nil {
					// logger.Error("Error converting member to string:", err6)
					continue
				}
				values, err6 := redis.String(InfoValues2[j+1], nil)
				if err6 != nil {
					// logger.Error("Error converting score to String:", err6)
					continue
				}
				if member2 == name {
					rname2 = values
				}
				if member2 == avatar {
					ravatar2 = values
				}

				if member2 == sex {
					rsex2 = values
				}

				if member2 == job {
					rjob2 = values
				}
			}
			//if err != nil {
			//	continue
			//}
			EndSex2, _ := strconv.Atoi(rsex2)
			EnJob2, _ := strconv.Atoi(rjob2)
			RankDetail.RankOwn = protocol.RankUserInfo{
				Uid:    Uid,
				Num:    resultsValue2 + 1,
				Avatar: ravatar2,
				Name:   rname2,
				Value:  OwnScore,
				Type:   beginType,
				Sex:    int32(EndSex2),
				Job:    int32(EnJob2),
			}
		}
	}

	return RankDetail, nil
}

func RankMysql() ([]*pb.Rank, error) {
	RankUserInfo := []*pb.Rank{}
	res, _ := db.RankList()
	for _, item := range res {
		zip := pb.RankInfo{
			List: []*pb.Rank{},
		}
		if err := protobuf.NewSerializer().Unmarshal(item.List, &zip); err == nil {
			for _, item2 := range zip.List {
				input := protocol.RankUserInfoInput{
					Uid:     item2.Uid,
					Type:    item.Type,
					Compare: false,
					Name:    item2.Name,
					Avatar:  item2.Avatar,
					Value:   item2.Value,
				}
				_, err1 := RankChangeTCP(&input)
				if err1 != nil {
					return nil, err
				}
			}

			RankUserInfo = zip.List
		} else {
			// logger.Error("反序列化失败:", err)
		}
	}

	return RankUserInfo, nil
}

func CronSendRankAwardSeason() {
	// //查询所有服务器Id
	// ServerIds, err := GetAllServerId()
	// if err != nil {
	// 	return
	// }
	ServerIds := make([]int32, 0)
	for i := int(global.ServerIDRangeStart); i < int(global.ServerIDRangeEnd); i++ {
		ServerIds = append(ServerIds, int32(i))
	}

	if len(ServerIds) == 0 {
		return
	}
	// 遍历所有服务器Id
	for _, serverId := range ServerIds {
		rank := strconv.Itoa(int(serverId)) + "rankSend:"

		// 将字符串转换为time.Time类型，并定义时间段数组
		var timePeriods []struct {
			start time.Time
			end   time.Time
		}
		timePeriodsStr := global.GlobalCfg.PVPSeason.Season

		for _, periodStr := range timePeriodsStr {
			start, err := time.Parse(time.DateOnly, periodStr.StartTime)
			if err != nil {
				// logger.Error("Error parsing start time:", err)
			}
			end, err := time.Parse(time.DateOnly, periodStr.EndTime)
			if err != nil {
				// logger.Error("Error parsing end time:", err)
			}
			timePeriods = append(timePeriods, struct {
				start time.Time
				end   time.Time
			}{start, end})
		}
		// 获取当前时间
		currentTime := time.Now()
		awardStatus := 0
		// 检查当前时间是否大于第一个时间段的开始时间
		// 遍历时间段，检查当前时间是否在时间段的间隙中
		var lastPeriodIndex int
		triggered := false
		for i, period := range timePeriods {
			if currentTime.Before(period.start) {
				if i == 0 || currentTime.After(timePeriods[i-1].end) {
					// 当前时间在时间段间隙中，且是刚过去的时间段
					lastPeriodIndex = i - 1
					triggered = true
					break
				}
			} else if currentTime.After(period.end) {
				// 更新刚过去的时间段索引
				lastPeriodIndex = i
			}
		}
		if triggered {
			if lastPeriodIndex >= 0 {
				end := lastPeriodIndex + 1
				logger.Infof("当前时间在时间段间隙中，刚过完第 %d 个时间段。", end)
				// 查询是否已经发过
				res, err2 := CmdCache("get", rank+strconv.Itoa(int(end)))
				if err2 != nil {
					// logger.Error("获取redis失败", err2)
					return
				}
				if res == nil {
					// 查数据库
					res1, _ := db.RankList()
					if len(res1) == 0 {
						// logger.Error("数据出错。")
						return
					}
					for _, item := range res1 {
						if int(item.Type) == 70000+end {
							awardStatus = item.SendStatus
						}
					}
					if awardStatus == 1 {
						// logger.Error("已经更新过了。")
						return
					}
					_, err3 := CmdCache("set", rank+strconv.Itoa(int(end)), "1")
					if err3 != nil {
						// logger.Error("获取redis失败", err3)
						return
					}
					_, err4 := db.RankUpdateSend(int32(70000 + end))
					if err4 != nil {
						// logger.Error("更新到数据库失败")
						return
					}
					// logger.Error("成功。")
					rtype := []int64{7}
					err := SendRankAward(rtype, false)
					if err != nil {
					}
				}
			} else {
				// logger.Debug("当前时间在第一个时间段之前，没有刚过完的时间段。")
			}
		} else {
			// logger.Debug("当前时间位于时间段内，不触发间隙触发条件")
		}
	}

	return
	//results, err2 := CmdCache("set", rank+strconv.Itoa(int(item)), rankBegin, rankEnd, "WITHSCORES")
	//if results == nil {
	//	continue
	//}
	//rtype := []int64{7}
	//err := SendRankAward(rtype)
	//if err != nil {
	//}
}

// 邮件
func Mailadd(r *protocol.MailAddRequest) error {
	if r == nil {
		return errutil.ErrIllegalParameter
	}
	result, err := db.MailAdd(
		r.Uid,
		r.RecipientUid,
		r.Title,
		r.Desc,
		r.Attachment,
		r.ChannelCode,
	)
	if err != nil || !result {
		return err
	}

	return nil
}

// 昨日充值入库
func Bounty(uid int64) ([]protocol.PrivilegeDetailTCP, error) {
	rank := strconv.Itoa(int(global.ServerIDRangeStart)) + "Bounty:"
	// 获取昨日总金额
	allMoney, err := db.Storetotal(0)
	if err != nil {
		return nil, err
	}
	BountyCfg := global.GlobalCfg.Bounty.Grade
	// result := make(map[int32][]protocol.SourceInfo)
	result := make([]protocol.PrivilegeDetailTCP, 0)
	// 特权卡
	allWeight := 0
	for _, privilegen := range BountyCfg {
		allWeight = allWeight + int(privilegen.Weight)
	}
	endMoney := allMoney[0].Num * int64(global.GlobalCfg.BaseValue.Bonus.Rate)
	for _, privilegen := range BountyCfg {
		key := rank + strconv.Itoa(int(privilegen.ID))
		num := int(int(endMoney) * (int(privilegen.Weight) / allWeight))
		err := ZAddCache(key, int64(num), int64(privilegen.ID))
		if err != nil {
			return nil, err
		}
	}
	return result, nil
}

// 昨日充值入库
func ltv(uid int64) ([]RankListDetail, error) {
	result := make([]RankListDetail, 0)
	return result, nil
}

type RankListDetail struct {
	Today  int  `json:"today"`  // 当天
	Two    int  `json:"two"`    // 次日
	Three  int  `json:"three"`  // 3日
	Seven  bool `json:"seven"`  // 7日
	Thirty bool `json:"thirty"` // 7日
}

func RequestWeb(methodPost string, url string, reqBody interface{}) ([]byte, error) {
	reqJson, _ := json.Marshal(&reqBody)
	urlReq, _ := http.NewRequest(methodPost, url, bytes.NewReader(reqJson))
	parma := urlReq.URL.Query()
	urlReq.URL.RawQuery = parma.Encode()
	res, err := HttpDo(urlReq)
	if err != nil {
		// logger.Error("请求失败", err)
		return nil, err
	}
	return res, nil
}

type BaseRequest struct {
	Code   string `form:"code"`
	Ipaddr string `form:"ipaddr"`
	Show   int    `form:"show"`
}

func HttpDo(req *http.Request) ([]byte, error) {
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}
	return body, nil
}
