package game

import (
	"fmt"
	"math/rand"
	"time"

	"nanoserver/db"
	"nanoserver/dispatcher"
	"nanoserver/pkg/redis_factory"
	"nanoserver/protocol"

	"github.com/asaskevich/EventBus"
	"github.com/lonng/nano"
	"github.com/lonng/nano/component"
	"github.com/lonng/nano/pipeline"
	"github.com/lonng/nano/serialize/protobuf"
	log "github.com/sirupsen/logrus"
	"github.com/spf13/viper"
)

var (
	version     = "" // 游戏版本
	forceUpdate = false
	logger      = log.WithField("component", "game")
)

// Startup 初始化游戏服务器
func Startup() {
	rand.Seed(time.Now().Unix())
	version = viper.GetString("update.version")

	heartbeat := viper.GetInt("core.heartbeat")
	if heartbeat < 5 {
		heartbeat = 5
	}

	// 游戏币消耗配置
	forceUpdate = viper.GetBool("update.force")

	logger.Infof("当前游戏服务器版本: %s, 是否强制更新: %t, 当前心跳时间间隔: %d秒", version, forceUpdate, heartbeat)
	logger.Info("game service starup")

	// 初始化世界管理器
	worldMgr.InitWorld()

	// register game handler
	comps := &component.Components{}
	comps.Register(defaultManager)
	comps.Register(roomManager)
	comps.Register(matchManager)
	comps.Register(worldBossMgr)
	comps.Register(guildManager)

	InitTimer()

	redis_factory.Init()

	var (
		event EventBus.Bus
		err   error
	)
	if event, err = setupEventHandlers(); err != nil {
		logger.Panicf("Failed to setup event handlers: %v", err)
	}
	defer event.WaitAsync()

	// 加密管道
	c := newCrypto()
	pip := pipeline.New()
	pip.Inbound().PushBack(c.inbound)
	pip.Outbound().PushBack(c.outbound)

	addr := fmt.Sprintf(":%d", viper.GetInt("game-server.listen_port"))
	// cert := viper.GetString("webserver.certificates.cert")
	// key := viper.GetString("webserver.certificates.key")
	nano.Listen(addr,
		// nano.WithPipeline(pip),
		nano.WithHeartbeatInterval(time.Duration(heartbeat)*time.Second),
		nano.WithLogger(log.WithField("component", "nano")),
		nano.WithSerializer(protobuf.NewSerializer()),
		nano.WithComponents(comps),
		// nano.WithDebugMode(),
		nano.WithIsWebsocket(true),
		// nano.WithTSLConfig(cert, key),
		nano.WithDictionary(protocol.Routemap),
	)
	logger.Info("save data befor service stop")

	defaultManager.GmCmd(protocol.GmCmd{Cmd: "shutdown"})

	logger.Info("save data finish, now stop server")
	db.MustClose()
}

func setupEventHandlers() (EventBus.Bus, error) {
	event := dispatcher.GetInstance()

	subscriptions := []struct {
		topic   string
		handler interface{}
		async   bool
	}{
		{dispatcher.TopicRechargeSuccess.String(), ActivityCumulativeRechargeHandler, false},
		{dispatcher.TopicActivityPush.String(), ActivityPushHandler, true},
	}

	var errors []error
	for _, sub := range subscriptions {
		var err error
		if sub.async {
			err = event.SubscribeAsync(sub.topic, sub.handler, false)
		} else {
			err = event.Subscribe(sub.topic, sub.handler)
		}

		if err != nil {
			errors = append(errors, fmt.Errorf("failed to subscribe to %s: %w", sub.topic, err))
		}
	}

	if len(errors) > 0 {
		var errMsg string
		for i, err := range errors {
			if i > 0 {
				errMsg += "; "
			}
			errMsg += err.Error()
		}
		return nil, fmt.Errorf("event subscription errors: %s", errMsg)
	}

	return event, nil
}
