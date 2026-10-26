package api

import (
	"context"
	"nanoserver/internal/game"
	"nanoserver/pkg/my_jwt"
	"nanoserver/protocol"
	"net/http"

	"github.com/gorilla/mux"
	"github.com/lonng/nex"
)

func MakeWindowService() http.Handler {
	router := mux.NewRouter()
	router.Handle("/v1/window/register_remain", nex.Handler(registerRemain)).Methods("GET") //列表
	router.Handle("/v1/window/payment_remain", nex.Handler(paymentRemain)).Methods("GET")   //列表
	router.Handle("/v1/window/pay_per", nex.Handler(payPer)).Methods("GET")                 //列表
	router.Handle("/v1/window/ltv", nex.Handler(ltv)).Methods("GET")                        //列表
	router.Handle("/v1/window/online_log", nex.Handler(online)).Methods("GET")              //列表
	router.Handle("/v1/window/ingot", nex.Handler(ingot)).Methods("GET")                    //列表
	router.Handle("/v1/window/have_money", nex.Handler(haveMoney)).Methods("GET")           //列表
	router.Handle("/v1/window/reward", nex.Handler(reward)).Methods("GET")                  //列表
	router.Handle("/v1/window/gold", nex.Handler(gold)).Methods("GET")                      //列表
	router.Handle("/v1/window/soul", nex.Handler(soul)).Methods("GET")                      //列表
	router.Handle("/v1/window/shushu", nex.Handler(shushu)).Methods("POST")
	router.Handle("/v1/window/shushuregister", nex.Handler(shushuRegister)).Methods("POST")
	//列表
	router.Handle("/v1/window/shushuno", nex.Handler(shushuno)).Methods("POST")   //列表
	router.Handle("/v1/window/ta_mp_hide", nex.Handler(mpHide)).Methods("POST")   //列表
	router.Handle("/v1/window/completed", nex.Handler(completed)).Methods("POST") //列表
	router.Handle("/v1/window/openBox", nex.Handler(openbox)).Methods("POST")     //列表

	return router
}
func registerRemain(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  1,
			Two:    2,
			Three:  3,
			Seven:  4,
			Thirty: 5,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  6,
			Two:    7,
			Three:  8,
			Seven:  9,
			Thirty: 10,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  11,
			Two:    12,
			Three:  13,
			Seven:  14,
			Thirty: 15,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  16,
			Two:    17,
			Three:  18,
			Seven:  19,
			Thirty: 20,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}
func paymentRemain(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  13,
			Two:    24,
			Three:  35,
			Seven:  46,
			Thirty: 57,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  68,
			Two:    79,
			Three:  810,
			Seven:  911,
			Thirty: 1012,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  113,
			Two:    214,
			Three:  315,
			Seven:  44,
			Thirty: 55,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66,
			Two:    77,
			Three:  88,
			Seven:  99,
			Thirty: 1010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}
func payPer(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  111,
			Two:    222,
			Three:  333,
			Seven:  444,
			Thirty: 555,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  666,
			Two:    777,
			Three:  888,
			Seven:  999,
			Thirty: 101010,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  1111,
			Two:    2222,
			Three:  3333,
			Seven:  4444,
			Thirty: 5555,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66666,
			Two:    77777,
			Three:  88888,
			Seven:  99999,
			Thirty: 10101010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}
func ltv(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  111111,
			Two:    1111112,
			Three:  1111113,
			Seven:  1111114,
			Thirty: 1111115,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  16,
			Two:    17,
			Three:  18,
			Seven:  19,
			Thirty: 110,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  11,
			Two:    22,
			Three:  33,
			Seven:  44,
			Thirty: 55,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66,
			Two:    77,
			Three:  88,
			Seven:  99,
			Thirty: 1010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}
func online(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  111111,
			Two:    1111112,
			Three:  1111113,
			Seven:  1111114,
			Thirty: 1111115,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  16,
			Two:    17,
			Three:  18,
			Seven:  19,
			Thirty: 110,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  11,
			Two:    22,
			Three:  33,
			Seven:  44,
			Thirty: 55,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66,
			Two:    77,
			Three:  88,
			Seven:  99,
			Thirty: 1010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}
func ingot(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  111111,
			Two:    1111112,
			Three:  1111113,
			Seven:  1111114,
			Thirty: 1111115,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  16,
			Two:    17,
			Three:  18,
			Seven:  19,
			Thirty: 110,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  11,
			Two:    22,
			Three:  33,
			Seven:  44,
			Thirty: 55,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66,
			Two:    77,
			Three:  88,
			Seven:  99,
			Thirty: 1010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}

func haveMoney(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  111111,
			Two:    1111112,
			Three:  1111113,
			Seven:  1111114,
			Thirty: 1111115,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  16,
			Two:    17,
			Three:  18,
			Seven:  19,
			Thirty: 110,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  11,
			Two:    22,
			Three:  33,
			Seven:  44,
			Thirty: 55,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66,
			Two:    77,
			Three:  88,
			Seven:  99,
			Thirty: 1010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}
func reward(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  111111,
			Two:    1111112,
			Three:  1111113,
			Seven:  1111114,
			Thirty: 1111115,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  16,
			Two:    17,
			Three:  18,
			Seven:  19,
			Thirty: 110,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  11,
			Two:    22,
			Three:  33,
			Seven:  44,
			Thirty: 55,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66,
			Two:    77,
			Three:  88,
			Seven:  99,
			Thirty: 1010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}

func gold(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  111111,
			Two:    1111112,
			Three:  1111113,
			Seven:  1111114,
			Thirty: 1111115,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  16,
			Two:    17,
			Three:  18,
			Seven:  19,
			Thirty: 110,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  11,
			Two:    22,
			Three:  33,
			Seven:  44,
			Thirty: 55,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66,
			Two:    77,
			Three:  88,
			Seven:  99,
			Thirty: 1010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}
func soul(r *protocol.WindowLtvListRequest) (*protocol.WindowLtvResponse, error) {
	count := 2
	RankListDetail := protocol.WindowLtvList{
		Count:  count,
		Detail: make([]protocol.WindowLtvDetail, count),
	}
	if !r.Export {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  111111,
			Two:    1111112,
			Three:  1111113,
			Seven:  1111114,
			Thirty: 1111115,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  16,
			Two:    17,
			Three:  18,
			Seven:  19,
			Thirty: 110,
		}
	} else {
		RankListDetail.Detail[0] = protocol.WindowLtvDetail{
			Date:   "2024-09-01",
			Today:  11,
			Two:    22,
			Three:  33,
			Seven:  44,
			Thirty: 55,
		}
		RankListDetail.Detail[1] = protocol.WindowLtvDetail{
			Date:   "2024-09-02",
			Today:  66,
			Two:    77,
			Three:  88,
			Seven:  99,
			Thirty: 1010,
		}
	}

	return &protocol.WindowLtvResponse{Data: RankListDetail}, nil
}

func shushu(ctx context.Context, r *protocol.WindowShushuRequest) (*protocol.WindowShushuResponse, error) {
	Uid := ctx.Value("userDate").(my_jwt.CustomClaims).UserId
	PostDate := make(map[string]interface{}, 0)
	PostDate["name"] = r.Name
	upPkgShushu(Uid, PostDate)
	// 这里可以根据需要处理 params
	// 例如：fmt.Println(params)
	return &protocol.WindowShushuResponse{Code: 0}, nil
}
func shushuRegister(ctx context.Context, r *protocol.WindowShushuRegisterRequest) (*protocol.WindowShushuResponse, error) {
	Uid := ctx.Value("userDate").(my_jwt.CustomClaims).UserId
	data := make(map[string]interface{}, 0)
	data["name"] = "register"
	data["register_type"] = "wechat"
	game.Upshushu(Uid, data, false)

	userSet := make(map[string]interface{}, 0)
	userSet["ip"] = ip
	userSet["register_time"] = r.Register
	game.UpshushuSetOnce(Uid, userSet, false)
	// 这里可以根据需要处理 params
	// 例如：fmt.Println(params)
	return &protocol.WindowShushuResponse{Code: 0}, nil
}
func shushuno(ctx context.Context, r *protocol.WindowShushuRequest) (*protocol.WindowShushuResponse, error) {
	PostDate := make(map[string]interface{}, 0)
	PostDate["name"] = r.Name
	upPkgShushuNo(PostDate)
	// 这里可以根据需要处理 params
	// 例如：fmt.Println(params)
	return &protocol.WindowShushuResponse{Code: 0}, nil
}
func mpHide(ctx context.Context, r *protocol.WindowShushuHumpHideRequest) (*protocol.WindowShushuResponse, error) {
	Uid := ctx.Value("userDate").(my_jwt.CustomClaims).UserId
	PostDate := make(map[string]interface{}, 0)
	PostDate["name"] = r.Name
	PostDate["#duration"] = r.Duration
	upPkgShushu(Uid, PostDate)
	// 这里可以根据需要处理 params
	// 例如：fmt.Println(params)
	return &protocol.WindowShushuResponse{Code: 0}, nil
}
func completed(ctx context.Context, r *protocol.WindowShushuCompletedRequest) (*protocol.WindowShushuResponse, error) {
	Uid := ctx.Value("userDate").(my_jwt.CustomClaims).UserId
	PostDate := make(map[string]interface{}, 0)
	PostDate["name"] = r.Name
	PostDate["guide_step"] = r.Guide_step
	PostDate["guide_name"] = r.Guide_name
	upPkgShushu(Uid, PostDate)
	// 这里可以根据需要处理 params
	// 例如：fmt.Println(params)
	return &protocol.WindowShushuResponse{Code: 0}, nil
}
func openbox(ctx context.Context, r *protocol.WindowShusOpenBoxRequest) (*protocol.WindowShushuResponse, error) {
	Uid := ctx.Value("userDate").(my_jwt.CustomClaims).UserId
	PostDate := make(map[string]interface{}, 0)
	PostDate["name"] = r.Name
	PostDate["box_item_info"] = r.BoxItemInfo
	// 处理 box_item_info 为数组
	boxItemInfos := make([]map[string]interface{}, 0, len(r.BoxItemInfo))
	for _, item := range r.BoxItemInfo {
		boxItemInfos = append(boxItemInfos, map[string]interface{}{
			"item_id":      item.ItemID,
			"item_level":   item.ItemLevel,
			"item_quality": item.ItemQuality,
			"item_name":    item.ItemName,
			"count":        item.Count,
		})
	}
	PostDate["box_item_info"] = boxItemInfos
	PostDate["is_auto"] = r.IsAuto
	PostDate["power_up"] = r.PowerUp
	PostDate["open_num"] = r.OpenNum
	PostDate["role_power_stop"] = r.RolePowerStop
	PostDate["partner_power_stop"] = r.PartnerPowerStop
	PostDate["box_num_before"] = r.BoxNumBefore
	PostDate["box_num_after"] = r.BoxNumAfter
	PostDate["box_job_limited"] = r.BoxJobLimited
	PostDate["box_level_limited"] = r.BoxLevelLimited
	upPkgShushu(Uid, PostDate)
	// 这里可以根据需要处理 params
	// 例如：fmt.Println(params)
	return &protocol.WindowShushuResponse{Code: 0}, nil
}
func upPkgShushu(uid int64, postDate map[string]interface{}) {
	game.Upshushu(uid, postDate, true)
}
func upPkgShushuNo(postDate map[string]interface{}) {
	game.Upshushuno(postDate)
}
