package dispatcher

// RechargeSuccessEvent 充值成功事件
type RechargeSuccessEvent struct {
	PID    int64  // 玩家ID
	Amount string // 充值金额
}

// ActivityPushEvent 活动推送事件
type ActivityPushEvent struct {
	Type ActivityType // 活动类型
	PID  int64        // 玩家ID
	Data interface{}  // 事件数据
}
