package dispatcher

type Topic string

const (
	TopicRechargeSuccess Topic = "recharge.success" // 充值成功

	TopicActivityPush Topic = "activity.push" // 活动推送
)

func (t Topic) String() string {
	return string(t)
}

type ActivityType int

const (
	ActivityCumulativeRecharge ActivityType = iota + 1 // 累计充值活动
	ActivityServerCelebration                          // 开服狂欢活动
)
