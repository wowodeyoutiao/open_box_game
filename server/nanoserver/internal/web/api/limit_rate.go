package api

import (
	"net/http"
	"strings"
	"sync"

	"golang.org/x/time/rate"
)

var visitorIPs = make(map[string]*rate.Limiter)
var mu sync.Mutex

func limitHandler(next http.Handler, route string) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		var ip string
		ipaddr := strings.Split(r.RemoteAddr, ":")
		if len(ipaddr) == 2 {
			ip = ipaddr[0] + route
		} else {
			logger.Errorf("limitHandler: %s", route)
			// http.Error(w, http.StatusText(http.StatusBadRequest), http.StatusForbidden)
			return
		}

		mu.Lock()
		limiter, exists := visitorIPs[ip]
		if !exists {
			limiter = rate.NewLimiter(1, 1) //每秒产生一个令牌，桶容量 1. 即每秒允许一个请求通过
			visitorIPs[ip] = limiter
		}
		mu.Unlock()

		if !limiter.Allow() {
			logger.Errorf("limitHandler: %s", route)
			// http.Error(w, http.StatusText(http.StatusTooManyRequests), http.StatusTooManyRequests)
			return
		}

		next.ServeHTTP(w, r)
	})
}

func ClearLimitRate() {
	logger.Debugf("清理令牌桶...")
	if mu.TryLock() {
		defer mu.Unlock()
		for k := range visitorIPs {
			delete(visitorIPs, k)
		}
	}
}
