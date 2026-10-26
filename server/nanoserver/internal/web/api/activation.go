package api

import (
	"context"
	"github.com/gorilla/mux"
	"github.com/lonng/nex"
	"nanoserver/db"
	"nanoserver/pkg/errutil"
	"nanoserver/pkg/my_jwt"
	"nanoserver/protocol"
	"net/http"
)

func MakeActivationService() http.Handler {
	router := mux.NewRouter()
	router.Handle("/v1/activation/check", nex.Handler(checkAdd)).Methods("Put") //兑换验证码

	return router
}
func checkAdd(ctx context.Context, r *http.Request, Activation *protocol.ActivationListRequest) (*protocol.ActivationOperateResponse, error) {
	Uid := ctx.Value("userDate").(my_jwt.CustomClaims).UserId
	request := &protocol.MailAddRequest{}

	//request.Title = resulta.Title
	//request.Desc = resulta.Desc
	//request.Attachment = resulta.Attachment

	//校验激活码ok不ok
	channelCode := Activation.ActivationCode

	//查询激活码是否有效
	resulta, err := db.ActivationDetail(channelCode)
	if channelCode == "" {
		return &protocol.ActivationOperateResponse{Data: "激活码不存在"}, errutil.ErrActivation
	}
	if len(resulta) == 0 {
		return &protocol.ActivationOperateResponse{Code: -1, Data: "激活码不存在"}, nil
	}

	resultb, err := db.MailTaskCheck(Uid, "Activation-"+channelCode)
	if len(resultb) > 0 {
		return &protocol.ActivationOperateResponse{Code: -1, Data: "已激活过"}, nil
	}
	if resulta[0].UseType == 2 {
		resultc, err3 := db.ActivationUse(channelCode)
		if !resultc {
			return &protocol.ActivationOperateResponse{Code: -1, Data: "服务器故障"}, err3
		}
	}
	request.Uid = -1
	request.Title = "系统邮件"
	request.Desc = "激活码兑换成功"
	request.RecipientUid = Uid
	request.ChannelCode = "Activation-" + channelCode
	request.Attachment = make([]protocol.Attachment, len(resulta[0].Attachments))
	for i, v2 := range resulta[0].Attachments {
		request.Attachment[i] = protocol.Attachment{
			UserPropsId: v2.PropsId,
			Num:         v2.Num,
		}
	}
	result, err := Mailadd(request)
	if err != nil || !result {
		return nil, err
	}
	return &protocol.ActivationOperateResponse{Data: "成功"}, nil

}
