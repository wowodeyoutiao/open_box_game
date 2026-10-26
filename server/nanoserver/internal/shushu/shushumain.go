package shushumain

import (
	"fmt"
	"nanoserver/pkg/shushu/src/thinkingdata"
	"strconv"
	"sync"

	"github.com/spf13/viper"
)

type Closer func()

var (
	te              thinkingdata.TDAnalytics
	DefaultReporter Reporter
)

// GetTE 返回 te 的指针
func GetTE() *thinkingdata.TDAnalytics {
	return &te
}

func Startup() {
	thinkingdata.SetLogLevel(thinkingdata.TDLogLevelError)

	// 单机版：未配置上报地址时直接跳过，不创建 consumer、不起上报协程。
	// 之前指向已停服的 shushu.shbinqigame.com，每 20 秒产生一条 404，纯属白耗 IO。
	if viper.GetString("shushu.shushuUrl") == "" {
		fmt.Println("[shushu] 未配置上报地址(shushu.shushuUrl)，已跳过数据上报")
		return
	}

	// e.g. init consumer, you can choose different consumer
	//consumer, err := generateDebugConsumer() // TDDebugConsumer
	// consumer, err := generateBatchConsumer() // TDBatchConsumer
	// consumer, err := generateLogConsumer() // TDLogConsumer
	config := thinkingdata.TDBatchConfig{
		ServerUrl: viper.GetString("shushu.shushuUrl"),
		AppId:     viper.GetString("shushu.shushuAppId"),
		AutoFlush: viper.GetBool("shushu.shushuAutoFlush"),
		BatchSize: viper.GetInt("shushu.shushuBatchSize"),
		Interval:  viper.GetInt("shushu.shushuInterval"),
	}
	consumer, err := thinkingdata.NewBatchConsumerWithConfig(config)
	if err != nil {
		// consumer init error
	}
	te = thinkingdata.New(consumer)
	DefaultReporter = *NewReporter(3, 1024)
	DefaultReporter.Start()
}
func Stop() {
	DefaultReporter.Stop()
}

// Reporter 用于管理数据上报的通道和相关逻辑
type Reporter struct {
	reportChan      chan map[string]interface{} // 数据上报通道
	userSetChan     chan map[string]interface{} // UserSet数据上报通道
	userSetOnceChan chan map[string]interface{} // UserSetOnce数据上报通道
	wg              sync.WaitGroup              // 等待组，用于等待所有处理协程完成
	numWorkers      int                         // 处理协程的数量
}

// NewReporter 创建一个新的 Reporter 实例
func NewReporter(numWorkers int, bufferSize int) *Reporter {
	return &Reporter{
		reportChan:      make(chan map[string]interface{}, bufferSize),
		userSetChan:     make(chan map[string]interface{}, bufferSize),
		userSetOnceChan: make(chan map[string]interface{}, bufferSize),
		numWorkers:      numWorkers,
	}
}

// Start 启动 Reporter，创建指定数量的处理协程
func (r *Reporter) Start() {
	for i := 0; i < r.numWorkers; i++ {
		r.wg.Add(1)
		go r.processReports()
		r.wg.Add(1)
		go r.processUserSetChannel()
		r.wg.Add(1)
		go r.processUserSetOnceChannel()
	}
}

// Stop 停止 Reporter，等待所有处理协程完成并关闭通道
func (r *Reporter) Stop() {
	r.wg.Wait()              // 等待所有处理协程完成
	close(r.reportChan)      // 关闭旧通道
	close(r.userSetChan)     // 关闭UserSet通道
	close(r.userSetOnceChan) // 关闭UserSetOnce通道
	fmt.Println("R关闭了")
	te.Close()
}

func (r *Reporter) reportData(data map[string]interface{}) {
	// 上报未启动（通道为 nil）时静默丢弃，避免 49 处调用点刷屏
	if r.reportChan == nil {
		return
	}
	select {
	case r.reportChan <- data:
	default:
		fmt.Println("Report channel is full, dropping data")
	}
}

// reportUserSetData 向UserSet通道发送数据
func (r *Reporter) reportUserSetData(data map[string]interface{}) {
	if r.userSetChan == nil {
		return
	}
	select {
	case r.userSetChan <- data:
	default:
		fmt.Println("UserSet channel is full, dropping data")
	}
}

// reportUserSetOnceData 向UserSetOnce通道发送数据
func (r *Reporter) reportUserSetOnceData(data map[string]interface{}) {
	if r.userSetOnceChan == nil {
		return
	}
	select {
	case r.userSetOnceChan <- data:
	default:
		fmt.Println("UserSetOnce channel is full, dropping data")
	}
}

// ReportData 调用默认 Reporter 发送数据
func ReportData(data map[string]interface{}) {
	DefaultReporter.reportData(data)
}

// ReportUserSetData 调用默认 Reporter 向UserSet通道发送数据
func ReportUserSetData(data map[string]interface{}) {
	DefaultReporter.reportUserSetData(data)
}

// ReportUserSetOnceData 调用默认 Reporter 向UserSetOnce通道发送数据
func ReportUserSetOnceData(data map[string]interface{}) {
	DefaultReporter.reportUserSetOnceData(data)
}

// processUserSetChannel 处理UserSet通道数据的协程函数
func (r *Reporter) processUserSetChannel() {
	defer r.wg.Done()
	for data := range r.userSetChan {
		UploadUserSetDate(GetTE(), data)
	}
}

// processUserSetOnceChannel 处理UserSetOnce通道数据的协程函数
func (r *Reporter) processUserSetOnceChannel() {
	defer r.wg.Done()
	for data := range r.userSetOnceChan {
		UploadUserSetOnceDate(GetTE(), data)
	}
}

func UploadUserSetDate(te *thinkingdata.TDAnalytics, properties map[string]interface{}) {
	accountId := ""
	distinctId := ""
	// 优先使用 tmp_role_id
	if roleID, ok := properties["tmp_role_id"].(int64); ok && roleID != 0 {
		distinctId = strconv.FormatInt(roleID, 10)
	}
	if account, ok := properties["tmp_account"].(string); ok && account != "" {
		accountId = account
	}

	for j := 0; j < 1; j++ {
		if accountId != "" && distinctId != "" {
			err := te.UserSet(accountId, distinctId, properties)
			if err != nil {
				fmt.Printf("Track error: %v\n", err)
			}
		}
	}
}

func UploadUserSetOnceDate(te *thinkingdata.TDAnalytics, properties map[string]interface{}) {
	accountId := ""
	distinctId := ""
	// 优先使用 tmp_role_id
	if roleID, ok := properties["tmp_role_id"].(int64); ok && roleID != 0 {
		distinctId = strconv.FormatInt(roleID, 10)
	}
	if account, ok := properties["tmp_account"].(string); ok && account != "" {
		accountId = account
	}

	for j := 0; j < 1; j++ {
		if accountId != "" && distinctId != "" {
			err := te.UserSetOnce(accountId, distinctId, properties)
			if err != nil {
				fmt.Printf("Track error: %v\n", err)
			}
		}
	}
}

// processReports 处理数据上报的协程函数
func (r *Reporter) processReports() {
	defer r.wg.Done()
	for data := range r.reportChan {
		// name, ok := data["_name"].(string)
		// if !ok {
		// 	fmt.Printf("Invalid data format: missing or invalid _name field\n")
		// 	continue
		// }
		// delete(data, "_name")
		UploadDate(GetTE(), data)
	}
}

func UploadDate(te *thinkingdata.TDAnalytics, properties map[string]interface{}) {
	accountId := ""
	distinctId := ""
	name := ""
	// 优先使用 tmp_role_id
	if roleID, ok := properties["tmp_role_id"].(int64); ok && roleID != 0 {
		distinctId = strconv.FormatInt(roleID, 10)
	}
	if account, ok := properties["tmp_account"].(string); ok && account != "" {
		accountId = account
	}
	if nameInfo, ok := properties["name"].(string); ok && nameInfo != "" {
		name = nameInfo
	}

	for j := 0; j < 1; j++ {
		if accountId != "" && distinctId != "" {
			err := te.Track(accountId, distinctId, name, properties)
			if err != nil {
				fmt.Printf("Track error: %v\n", err)
			}
		}
	}
}
