package game

import (
	"errors"

	"nanoserver/pkg/errutil"
	"nanoserver/protocol/protobuf/pb"

	"github.com/lonng/nano/session"
)

func (m *Manager) GuardIslandMultiplier(s *session.Session, req *pb.GuardIslandMultiplierRequest) error {
	res := &pb.GuardIslandMultiplierResponse{
		Code:  0,
		Error: "",
		ID:    req.GetID(),
	}

	logger.Debugf("GuardIslandMultiplierService request.ID: %+v, session: %d", req.GetID(), s.ID())

	// 获取玩家对象
	player, err := playerWithSession(s)
	if err != nil {
		res.Code = -1
		res.Error = err.Error()
		logger.Errorf("GuardIslandMultiplier playerWithSession error: %v", err)
		return s.Response(res)
	}

	// 执行业务逻辑
	err = player.GuardIslandMultiplier(req.ID)
	if err != nil {
		var bizErr *errutil.BizError
		if errors.As(err, &bizErr) {
			res.Code = bizErr.Code
			res.Error = bizErr.Error()
			logger.Warnf("GuardIslandMultiplier business error, pid: %d, err: %+v", player.Uid(), err)
		} else {
			res.Code = -1
			res.Error = err.Error()
			logger.Errorf("GuardIslandMultiplier error, pid: %d, err: %+v", player.Uid(), err)
		}
		return s.Response(res)
	}

	// 返回响应
	return s.Response(res)
}
