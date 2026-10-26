package main

import (
	"fmt"
	"os"
	"runtime/pprof"
	"sync"
	"time"

	"nanoserver/bootstrap"
	"nanoserver/global"
	"nanoserver/internal/game"
	shushumain "nanoserver/internal/shushu"
	"nanoserver/internal/web"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/snowflake"

	_ "github.com/go-sql-driver/mysql"
	rotatelogs "github.com/lestrrat-go/file-rotatelogs"
	log "github.com/sirupsen/logrus"
	"github.com/spf13/viper"
	"github.com/urfave/cli"
)

func main() {
	app := cli.NewApp()

	// base application info
	app.Name = "mow server"
	app.Author = "MOW"
	app.Version = "0.0.1"
	app.Copyright = "mow team reserved"
	app.Usage = "mow server"

	// flags
	app.Flags = []cli.Flag{
		cli.StringFlag{
			Name:  "config, c",
			Value: "./configs/config.toml",
			Usage: "load configuration from `FILE`",
		},
		cli.BoolFlag{
			Name:  "cpuprofile",
			Usage: "enable cpu profile",
		},
	}

	app.Action = serve
	app.Run(os.Args)
}

func serve(c *cli.Context) error {
	viper.SetConfigType("toml")
	viper.SetConfigFile(c.String("config"))
	if err := viper.ReadInConfig(); err != nil {
		panic(fmt.Errorf("fatal error config file: %w", err))
	}

	coreEnv := viper.GetString("core.env")

	// 根据环境配置日志
	if err := setupLogging(coreEnv); err != nil {
		log.Fatalf("Failed to setup logging: %v", err)
	}

	if c.Bool("cpuprofile") {
		filename := fmt.Sprintf("cpuprofile-%d.pprof", time.Now().Unix())
		f, err := os.OpenFile(filename, os.O_WRONLY|os.O_CREATE, os.ModePerm)
		if err != nil {
			panic(err)
		}
		pprof.StartCPUProfile(f)
		defer pprof.StopCPUProfile()
	}

	global.ServerIDRangeStart = viper.GetInt32("core.server_id_range_start")
	global.ServerIDRangeEnd = viper.GetInt32("core.server_id_range_end")
	snowflake.Init(time.DateOnly, int64(global.ServerIDRangeStart))

	bootstrap.Initialize()

	if err := global.NewServerList(); err != nil {
		log.Fatalf("Failed to initialize server list: %v", err)
	}
	defer global.ServerListMgrInstance.Close()

	// 先同步完成数据库连接与自动建表（Sync2），
	// 避免 game 服务并发启动时表还不存在（历史上 guild 表报过不存在）
	dbCloser := web.DBStartup()
	defer dbCloser()

	wg := sync.WaitGroup{}
	wg.Add(3)

	go func() { defer wg.Done(); game.Startup() }()       // 开启游戏服
	go func() { defer wg.Done(); web.Startup() }()        // 开启web服务器
	go func() { defer wg.Done(); shushumain.Startup() }() // 启动数据上报
	wg.Wait()
	return nil
}

// setupLogging 根据环境配置日志输出
func setupLogging(env string) error {
	switch env {
	case constant.EnvDev, constant.EnvTest:
		log.SetFormatter(&log.TextFormatter{
			DisableColors: false,
			FullTimestamp: true,
			ForceColors:   true,
		})
		log.SetOutput(os.Stdout)
		log.SetLevel(log.DebugLevel)
	default:
		log.SetFormatter(&log.JSONFormatter{
			TimestampFormat: "2006-01-02 15:04:05",
		})

		if err := os.MkdirAll("logs", 0o755); err != nil {
			return fmt.Errorf("failed to create logs directory: %w", err)
		}

		writer, err := rotatelogs.New(
			"logs/app_%Y%m%d.log",
			rotatelogs.WithRotationTime(24*time.Hour), // 每天轮转
			rotatelogs.WithMaxAge(7*24*time.Hour),     // 保留30天
			rotatelogs.WithRotationCount(0),           // 不限制文件数量，按时间清理
		)
		if err != nil {
			return fmt.Errorf("failed to create log rotator: %w", err)
		}

		log.SetOutput(writer)
		log.SetLevel(log.InfoLevel)

	}

	log.SetReportCaller(true)

	return nil
}
