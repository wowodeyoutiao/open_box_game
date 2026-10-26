package protocol

type WindowLtvListRequest struct {
	PageIndex int  `json:"page_index"`
	PageSize  int  `json:"page_size"`
	Export    bool `json:"export"`
}
type WindowLtvResponse struct {
	Code int           `json:"code"` //状态码
	Data WindowLtvList `json:"data"` //备注信息
}

type WindowLtvList struct {
	Count     int               `json:"count"`      // 总数
	PageIndex int               `json:"page_index"` // 次日
	PageSize  int               `json:"page_size"`  //
	Detail    []WindowLtvDetail `json:"detail"`     // 总数
}

// Ltv
type WindowLtvDetail struct {
	Date   string `json:"date"`   // 日期
	Today  int    `json:"today"`  // 当天
	Two    int    `json:"two"`    // 次日
	Three  int    `json:"three"`  // 3日
	Seven  int    `json:"seven"`  // 7日
	Thirty int    `json:"thirty"` // 7日
}
type WindowShushuRequest struct {
	Name string `json:"name"`
}
type WindowShushuRegisterRequest struct {
	Name     string `json:"name"`
	Register int64  `json:"register"`
}
type WindowShushuHumpHideRequest struct {
	Name     string `json:"name"`
	Duration int    `json:"duration"`
}
type WindowShushuCompletedRequest struct {
	Name       string `json:"name"`
	Guide_step int    `json:"guide_step"`
	Guide_name string `json:"guide_name"`
}
type WindowShusOpenBoxRequest struct {
	Name             string        `json:"name"`
	BoxItemInfo      []BoxItemInfo `json:"box_item_info"`
	IsAuto           bool          `json:"is_auto"`
	PowerUp          int           `json:"power_up"`
	OpenNum          int           `json:"open_num"`
	RolePowerStop    bool          `json:"role_power_stop"`
	PartnerPowerStop bool          `json:"partner_power_stop"`
	BoxNumBefore     int           `json:"box_num_before"`
	BoxNumAfter      int           `json:"box_num_after"`
	BoxJobLimited    string        `json:"box_job_limited"`
	BoxLevelLimited  int           `json:"box_level_limited"`
}
type BoxItemInfo struct {
	ItemID      string `json:"item_id"`
	ItemLevel   int    `json:"item_level"`
	ItemQuality int    `json:"item_quality"`
	ItemName    string `json:"item_name"`
	Count       int    `json:"count"`
}
type WindowShushuResponse struct {
	Code int `json:"code"` //状态码
}
