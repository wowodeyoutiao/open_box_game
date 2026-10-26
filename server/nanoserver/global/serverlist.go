package global

import (
	"context"
	"fmt"
	"sync"
	"time"

	"github.com/go-resty/resty/v2"
	"github.com/sirupsen/logrus"
	"github.com/spf13/viper"
)

var (
	logger                = logrus.WithField("component", "global")
	ServerListMgrInstance *ServerListMgr
)

type ServerListResponse struct {
	ErrCode int32       `json:"code"`
	ErrMsg  string      `json:"error"`
	Data    ServerLists `json:"data"`
}

type ServerLists struct {
	Count int32        `json:"count"`
	List  []ServerInfo `json:"list"`
}

type ServerInfo struct {
	ID        int64  `json:"id"`
	Name      string `json:"name"`
	IPAddr    string `json:"ipaddr"`
	ServerID  int    `json:"server_id"`
	OpenBy    int64  `json:"open_by"` // 时间戳（毫秒）
	Status    int    `json:"status"`
	IsShow    int    `json:"is_show"`
	CreatedAt string `json:"createdAt"` // ISO8601 时间格式
	UpdatedAt string `json:"updatedAt"` // ISO8601 时间格式
	CreateBy  int64  `json:"createBy"`
	UpdateBy  int64  `json:"updateBy"`
}

type ServerListMgr struct {
	ctx         context.Context
	cancel      context.CancelFunc
	httpClient  *resty.Client
	serverInfos map[int]*ServerInfo
	mu          sync.RWMutex
	lastUpdate  time.Time
	refreshURL  string
	wg          sync.WaitGroup
}

// LocalServerInfos 返回内置的本地/单机区服列表。
// 单机部署时不依赖运营后台（admin.adminUrl），直接用下面这条假数据。
func LocalServerInfos() map[int]*ServerInfo {
	return map[int]*ServerInfo{
		1: {
			ID:        1,
			Name:      "单机服",
			IPAddr:    fmt.Sprintf("%s:%d", viper.GetString("game-server.host"), viper.GetInt("game-server.port")),
			ServerID:  1,
			OpenBy:    0,
			Status:    1,
			IsShow:    1,
			CreatedAt: "",
			UpdatedAt: "",
		},
	}
}

// NewServerList 创建并初始化 ServerListMgr
// 后台地址未配置或拉取失败时，退化为内置本地区服列表（单机模式），不再阻断启动。
func NewServerList() error {
	adminURL := viper.GetString("admin.adminUrl")

	ctx, cancel := context.WithCancel(context.Background())

	mgr := &ServerListMgr{
		ctx:         ctx,
		cancel:      cancel,
		httpClient:  resty.New().SetTimeout(10 * time.Second),
		serverInfos: make(map[int]*ServerInfo),
		refreshURL:  fmt.Sprintf("%s/api/v1/servername", adminURL),
	}

	// 单机模式：没有运营后台，直接用内置区服，且不启动远端轮询
	if adminURL == "" {
		mgr.serverInfos = LocalServerInfos()
		mgr.lastUpdate = time.Now()
		ServerListMgrInstance = mgr
		logger.Warn("admin.adminUrl 未配置，已启用内置本地区服列表（单机模式）")
		return nil
	}

	// 首次同步拉取数据，失败则退化为内置本地区服（不阻断启动）
	if err := mgr.fetchAndUpdate(); err != nil {
		mgr.serverInfos = LocalServerInfos()
		mgr.lastUpdate = time.Now()
		ServerListMgrInstance = mgr
		logger.Warnf("拉取远端区服列表失败(%v)，已启用内置本地区服列表（单机模式）", err)
		return nil
	}

	// 启动后台刷新任务
	mgr.wg.Add(1)
	go mgr.startAutoRefresh()

	ServerListMgrInstance = mgr
	return nil
}

// Close 优雅关闭 ServerListMgr
func (s *ServerListMgr) Close() {
	if s.cancel != nil {
		s.cancel()
	}
	s.wg.Wait()
}

// fetchAndUpdate 拉取数据并更新缓存
func (s *ServerListMgr) fetchAndUpdate() error {
	serverInfos, err := s.fetchServerList()
	if err != nil {
		return err
	}

	s.mu.Lock()
	s.serverInfos = serverInfos
	s.lastUpdate = time.Now()
	s.mu.Unlock()

	return nil
}

// startAutoRefresh 启动自动刷新任务
func (s *ServerListMgr) startAutoRefresh() {
	defer s.wg.Done()

	ticker := time.NewTicker(1 * time.Minute)
	defer ticker.Stop()

	for {
		select {
		case <-s.ctx.Done():
			return
		case <-ticker.C:
			if err := s.fetchAndUpdate(); err != nil {
				logger.Errorf("failed to refresh server list: %v", err)
			}
		}
	}
}

// fetchServerList 从API拉取服务器列表
func (s *ServerListMgr) fetchServerList() (map[int]*ServerInfo, error) {
	ctx, cancel := context.WithTimeout(s.ctx, 10*time.Second)
	defer cancel()

	var resp ServerListResponse

	response, err := s.httpClient.R().
		SetContext(ctx).
		SetResult(&resp).
		Post(s.refreshURL)
	if err != nil {
		return nil, fmt.Errorf("http request failed: %w", err)
	}

	if response.StatusCode() != 200 {
		return nil, fmt.Errorf("http request failed with status code: %d", response.StatusCode())
	}

	// if resp.ErrCode != 0 {
	// 	return nil, fmt.Errorf("api error: %s (code: %d)", resp.ErrMsg, resp.ErrCode)
	// }

	serverInfos := make(map[int]*ServerInfo, len(resp.Data.List))
	for i := range resp.Data.List {
		info := resp.Data.List[i]
		serverInfos[info.ServerID] = &info
	}

	return serverInfos, nil
}

// Get 非阻塞获取服务器信息
// 如果缓存中没有该 serverId，则返回错误
func (s *ServerListMgr) Get(serverId int) (ServerInfo, error) {
	s.mu.RLock()
	info, exists := s.serverInfos[serverId]
	s.mu.RUnlock()

	if !exists {
		return ServerInfo{}, fmt.Errorf("server with ID %d not found", serverId)
	}

	return *info, nil
}

// GetAll 获取所有服务器信息的副本
func (s *ServerListMgr) GetAll() map[int]ServerInfo {
	s.mu.RLock()
	defer s.mu.RUnlock()

	result := make(map[int]ServerInfo, len(s.serverInfos))
	for id, info := range s.serverInfos {
		result[id] = *info
	}
	return result
}

// GetLastUpdateTime 获取最后更新时间
func (s *ServerListMgr) GetLastUpdateTime() time.Time {
	s.mu.RLock()
	defer s.mu.RUnlock()
	return s.lastUpdate
}
