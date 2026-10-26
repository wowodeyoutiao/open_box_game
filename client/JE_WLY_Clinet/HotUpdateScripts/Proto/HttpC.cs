using JEngine.Core;
using JEngine.Event;
using JEngine.Helper;
using JEngine.LitJson;
using MetaFramework.UI;
using System;
using UnityEngine;

namespace HotUpdateScripts.ProtoM
{
    public class HttpC
    {

        public enum LoginType
        {
            Account,
            Login3rd,
        }
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="YesFun"></param>
        public static void OnLoginSever(HTTP_LoginRequest loginRequest, LoginType loginType)
        {
            // 单机模式：直接构造响应对象，完全绕开 HTTP 与 LitJson 解析。
            // （JsonMapper.ToObject<T> 对 struct + 嵌套 List 的解析结果不可靠，
            //   曾导致 characterList 为空 → 后续按空列表处理 → 配置字典查 key 0 崩溃）
            if (HotUpdateScripts.LocalNet.LocalServer.Enabled)
            {
                HandleLoginResponse(HotUpdateScripts.LocalNet.LocalHttpRouter.BuildLoginResponse());
                return;
            }

            string url = GameM.Instance.HttpUrl + GameM.Instance.LoginUrl;
            if (loginType == LoginType.Login3rd)
                url = GameM.Instance.HttpUrl + GameM.Instance.Login3rdUrl;

            HttpRequestHelper.SendPost<HTTP_LoginResponse>(url, NetworkHelper.HTTPMethod.POST, JsonMapper.ToJson(loginRequest),
            (_response) => {
                HandleLoginResponse(_response);
            }, () => {
                UITool.Close<UILoading>();
                UITool.ShowTips("登录失败，服务器关闭");
            });
        }

        private static void HandleLoginResponse(HTTP_LoginResponse _response)
        {
            UITool.Close<UILoading>();
            if (_response.code == 0)
            {
                RoleM.Instance.SetHttpGetInfo(_response);
                if (_response.characterList == null || _response.characterList.Count == 0)
                {
                    UITool.Open<UICreatRole>();
                }
                else
                {
                    UITool.Open<UISelectRole>();
                }

                if (!_response.realname)
                {
                    UITool.Open<UIRealName>();
                }
            }
            else
            {
                UITool.ShowTips(_response.messages != null && _response.messages.Count > 0
                    ? _response.messages[0]
                    : "登录失败");
            }
        }

        /// <summary>
        /// 获取WX OpenID
        /// </summary>
        /// <param name="codeStr"></param>
        /// <param name="YesFun"></param>
        public static void OnGetOpenID(string codeStr, Action<HTTP_WXLoginResponse> YesFun = null)
        {
            HttpRequestHelper.SendPost<HTTP_WXLoginResponse>(GameM.Instance.WXInfoUrl , NetworkHelper.HTTPMethod.POST, JsonMapper.ToJson(new HTTP_WXLoginRequest {code = codeStr }),
            (_response) => {

                if (_response.code == 200)
                {
                    YesFun?.Invoke(_response);
                }
                else
                    UITool.ShowTips(_response.msg);
            }, () => { UITool.ShowTips("网络异常"); });
        }

        /// <summary>
        /// 校验sessionkey
        /// </summary>
        /// <param name="codeStr"></param>
        /// <param name="YesFun"></param>
        public static void OnCheakSeeion( Action<HTTP_WXSeeionResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_WXSeeionRequest()
            {
                openid = GameM.Instance.WXOpneID,
            });
            HttpRequestHelper.SendPost<HTTP_WXSeeionResponse>(GameM.Instance.SessionsCheakUrl, NetworkHelper.HTTPMethod.PUT, strRequest,
            (_response) => {
                YesFun?.Invoke(_response);
            }, () => { UITool.ShowTips("登录失败，服务器关闭"); });
        }

        /// <summary>
        /// 获取当前账号服务器选择
        /// </summary>
        /// <param name="_requset"></param>
        /// <param name="YesFun"></param>
        public static void OnGetSeverInfo(HTTP_SeverRequest _requset, Action<HTTP_SeverResponse> YesFun = null,Action FailCall = null)
        {
            _requset.code = PlayerPrefsExtend.GetString("TextAcount");
            if (!InitJEngine.Instance.IsFreeLogin)
                 _requset.show = 1;
            _requset.version = Updater.WebVersion;
            HttpRequestHelper.SendPost<HTTP_SeverResponse>(GameM.Instance.SeverInfoUrl, NetworkHelper.HTTPMethod.POST, JsonMapper.ToJson(_requset),
            (_response) => {

                if (_response.code == 200)
                {
                    UITool.ShowTips("服务器列表获取成功");
                    Log.Print("OnGetSeverInfo" + _response.data.pageSize + " - "+ _response.data.count + "--"+ _response.data.version_status);
                    GameM.Instance.SeverCount = _response.data.count;
                    GameM.Instance.VersionStatus = _response.data.version_status;
                    if (GameM.Instance.SeverInfoDic.ContainsKey(0))
                        GameM.Instance.SeverInfoDic[0] = _response.data.mylist;
                    else
                        GameM.Instance.SeverInfoDic.Add(0, _response.data.mylist);

                    if (GameM.Instance.SeverInfoDic.ContainsKey(_response.data.pageIndex))
                        GameM.Instance.SeverInfoDic[_response.data.pageIndex] = _response.data.list;
                    else
                        GameM.Instance.SeverInfoDic.Add(_response.data.pageIndex, _response.data.list);


                    YesFun?.Invoke(_response);
                }
                else
                    UITool.ShowTips(_response.msg);
            }, () => { UITool.ShowTips("登录失败，服务器关闭"); FailCall?.Invoke(); });
        }

        /// <summary>
        /// 实名认证
        /// </summary>
        /// <param name="YesFun"></param>
        public static void OnRealName(HTTP_RealNameRequest realNameRequest, Action<HTTP_RealNameResponse> YesFun = null)
        {
            HttpRequestHelper.SendPost<HTTP_RealNameResponse>(GameM.Instance.HttpUrl + GameM.Instance.RealNameUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Login_Token, JsonMapper.ToJson(realNameRequest),
            (_response) => {
                if (_response.code == 0)
                {
                    UITool.ShowTips("验证成功");
                    UITool.Close<UIRealName>();
                }
                else
                    UITool.ShowTips(_response.message);
            }, () => { UITool.ShowTips("登录失败，服务器关闭"); });
        }

        /// <summary>
        /// 游戏购买
        /// </summary>
        /// <param name="strRequest"></param>
        /// <param name="YesFun"></param>
        public static void OnGameToBuy(string strRequest, Action<HTTP_StoreOperateResponse> YesFun = null)
        {
            HttpRequestHelper.SendPost<HTTP_StoreOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Store_BuyUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
            (_response) => {
                YesFun?.Invoke(_response);
            }, () => { UITool.ShowTips("登录失败，服务器关闭"); });
        }

        //获取邮件信息
        public static void OnGetMailInfo(Action<HTTP_MailListResponse> YesFun = null)
        {
            HttpRequestHelper.SendPost<HTTP_MailListResponse>(GameM.Instance.HttpUrl + GameM.Instance.Mail_GetListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, string.Empty,
            
            (_response) => {
                if (_response.code == 0)
                {
                    YesFun?.Invoke(_response);
                    MailM.Instance.SetMailLsit(_response.data);
                    JEvent.defaultEvent.Post(new GameEvent.Refresh_MailUI());
                    RedPointManager.Instance.RPS.Dispatch(RedPointConst.Set_Email.ToString(), MailM.Instance.GetDotNum());
                }
            });
        }

        //邮件全部删除
        public static void OnGetMailDelAll(Action<Http_MailOperateResponse> YesFun = null)
        {
            HttpRequestHelper.SendPost<Http_MailOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Mail_DelAllUrl,
                NetworkHelper.HTTPMethod.DELETE, RoleM.Instance.Token, string.Empty,
                 (_response) => {
                     if (_response.code == 0)
                     {
                         YesFun?.Invoke(_response);
                         RedPointManager.Instance.RPS.Dispatch(RedPointConst.Set_Email.ToString(), MailM.Instance.GetDotNum());
                     }
                     else
                     {
                         UITool.ShowTips(_response.data);
                     }
                 }, () => { UITool.ShowTips($"删除全部邮件失败"); });
        }

        //邮件删除一个
        public static void OnGetMailDelOne(string mail_id, Action<Http_MailOperateResponse> YesFun = null)
        {
            string str = JsonMapper.ToJson(new Http_MailOperateRequset { mail_id = mail_id });
            HttpRequestHelper.SendPost<Http_MailOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Mail_DelUrl, NetworkHelper.HTTPMethod.DELETE, RoleM.Instance.Token, str,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                      RedPointManager.Instance.RPS.Dispatch(RedPointConst.Set_Email.ToString(), MailM.Instance.GetDotNum());
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }
              }, () => { UITool.ShowTips($"删除一个邮件失败"); });
        }

        //邮件全部领取
        public static void OnGetMailGetAll(Action<Http_MailOperateResponse> YesFun = null)
        {
            HttpRequestHelper.SendPost<Http_MailOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Mail_ExtractAllUrl,
                NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, string.Empty,
               (_response) => {
                   if (_response.code == 0)
                   {
                       YesFun?.Invoke(_response);
                       RedPointManager.Instance.RPS.Dispatch(RedPointConst.Set_Email.ToString(), MailM.Instance.GetDotNum());
                   }
                   else
                   {
                       UITool.ShowTips(_response.data);
                   }
               }, () => { UITool.ShowTips($"邮件全部领取失败"); });
        }

        //邮件一个领取
        public static void OnGetMailGetOne(string mail_id, Action<Http_MailOperateResponse> YesFun = null)
        {
            string str = JsonMapper.ToJson(new Http_MailOperateRequset { mail_id = mail_id });
            HttpRequestHelper.SendPost<Http_MailOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Mail_ExtractUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, str,
            (_response) => {
                if (_response.code == 0)
                {
                    YesFun?.Invoke(_response);
                    RedPointManager.Instance.RPS.Dispatch(RedPointConst.Set_Email.ToString(), MailM.Instance.GetDotNum());
                }
                else
                {
                    UITool.ShowTips(_response.data);
                }
            }, () => { UITool.ShowTips($"邮件领取一个失败"); });
        }

        //邮件获取某一个
        public static void OnGetOneMail( string mail_id, Action<Http_MailDetailResponse> YesFun = null)
        {
            string str = JsonMapper.ToJson(new Http_MailDetailRequset()
            {
                mail_id = mail_id
            });
            HttpRequestHelper.SendPost<Http_MailDetailResponse>(GameM.Instance.HttpUrl + GameM.Instance.Mail_DetailUrl,
                NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, str,
                YesFun,
              () => { UITool.ShowTips("获取某一个邮件失败"); });
        }

        //获取商店信息
        public static void OnGetShopInfo(int _store_type = 0, Action<HTTP_StoreListResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_StoreListRequset()
            {
                store_type = 0
            });
            HttpRequestHelper.SendPost<HTTP_StoreListResponse>(GameM.Instance.HttpUrl + GameM.Instance.Store_GetListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
               (_response) => {
                   if (_response.code == 0)
                   {
                       StoreM.Instance.SetSoreInfo(_response.data);
                       YesFun?.Invoke(_response);


                       JEvent.defaultEvent.Post(new GameEvent.ShopInfo_Get());
                      
                   }
                   else
                   {
                       UITool.ShowTips($"失败 code {_response.code}");
                   }
               });
        }

        //提取账号 获取
        public static void OnExtractAccount(Action<HTTP_StoreExtractAccountResponse> YesFun = null)
        {
            HttpRequestHelper.SendPost<HTTP_StoreExtractAccountResponse>(GameM.Instance.HttpUrl + GameM.Instance.Store_ExtractAccount, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, "",
               (_response) => {
                   if (_response.code == 0)
                   {
                       StoreM.Instance.SetExtractAccount(_response.type, _response.detail, _response.name);
                       YesFun?.Invoke(_response);
                   }
                   else
                   {
                       UITool.ShowTips(_response.data);
                   }
               });
        }

        /// <summary>
        /// //提取账号 更新
        /// </summary>
        /// <param name="_type"> 1 - 微信，2 -支付宝</param>
        /// <param name="account"></param>
        /// <param name="YesFun"></param>
        public static void OnExtractAccountUpdate(int _type,string account,string name, Action<HTTP_StoreOperateResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_StoreExtractAccountUpdateResponse
            {
                type= _type,
                detail = account,
                name = name,
            });
            HttpRequestHelper.SendPost<HTTP_StoreOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Store_ExtractAccountUpdate, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
               (_response) => {
                   if (_response.code == 0)
                   {
                       StoreM.Instance.SetExtractAccount(_type, account, name);
                       YesFun?.Invoke(_response);
                   }
                   else
                   {
                       UITool.ShowTips(_response.error);
                   }
               });
        }

        //活动中心 7日签到领取
        public static void OnSign7Get(int id, bool isAd, Action<HTTP_ActivityReceiveDayResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityReceiveDayRequset
            {
                ad = 0,
                uuid = id
            });

            HttpRequestHelper.SendPost<HTTP_ActivityReceiveDayResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_SignUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
                (_response) => {
                    YesFun?.Invoke(_response);
                },
               null);
        }

        //活动中心 7日签到领取信息
        public static void OnSign7Info(Action<HTTP_ActivityListResponse> YesFun = null)
        {
            if (!ActivityM.Instance.GetAcitvityState(ActivityType.Sign7))
            {
                return;
            }
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityListRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_ActivityListResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_GetSignListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
               (_response) => {
                   if (_response.code == 0)
                   {
                       ActivityM.Instance.SetSignDic(_response);
                       YesFun?.Invoke(_response);
                       RedPointManager.Instance.RPS.Dispatch(RedPointConst.ActiveCenter_Sign7.ToString(), ActivityM.Instance.GetSign7DotNum());
                   }
                   else
                       UITool.ShowTips("err code" + _response.code);
               },
               null);
        }

        //活动中心 30日签到领取
        public static void OnSign30Get(Action<HTTP_ActivityReceiveDayResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityListRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_ActivityReceiveDayResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_Sign30GetUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
               (_response) => {
                   YesFun?.Invoke(_response);
               },
               null);
        }

        //活动中心 30日签到领取信息
        public static void OnSign30Info(Action<HTTP_ActivityListResponse> YesFun = null)
        {
            if (!ActivityM.Instance.GetAcitvityState(ActivityType.Sign30))
            {
                return;
            }
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityListRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_ActivityListResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_GetSign30ListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
               (_response) => {
                   if (_response.code == 0)
                   {
                       ActivityM.Instance.SetSignDic(_response);
                       YesFun?.Invoke(_response);
                       //RedPointManager.Instance.RPS.Dispatch(RedPointConst.ActiveCenter_Sign30.ToString(), ActivityM.Instance.GetSign30DotNum());
                   }
                   else
                       UITool.ShowTips("err code"+_response.code);
                   
               });
        }

        //活动中心 等级礼包
        public static void OnLevelGiftInfo(Action<HTTP_ActivityGradeResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityListRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_ActivityGradeResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_GetLevelGiftListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
               (_response) => {
                   if (_response.code == 0)
                   {
                       ActivityM.Instance.SetLevelGiftDic(_response.activity_list_grade);
                       YesFun?.Invoke(_response);

                       //RedPointManager.Instance.RPS.Dispatch(RedPointConst.ActiveCenter_Level.ToString(), ActivityM.Instance.GetLevelDotNum());
                   }
                   else
                       UITool.ShowTips(_response.data);
               });
        }

        //活动中心 等级礼包领取
        public static void OnLevelGiftGet(int uId,Action<HTTP_ActivityReceiveDayResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityGradeRequset
            {
                activity_id = 1,
                uuid = uId
            });
            HttpRequestHelper.SendPost<HTTP_ActivityReceiveDayResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_GetLevelGiftUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
               (_response) => {
                   YesFun?.Invoke(_response);
               });
        }

        //首充信息獲取
        public static void GetFirstPayInfo(Action<HTTP_ActivityChargeResponse> YesFun = null)
        {
            if (!ActivityM.Instance.GetAcitvityState(ActivityType.FirstPay))
            {
                return;
            }
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityListRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_ActivityChargeResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_FirstPayListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
               (_response) => {
                   if (_response.code == 0)
                   {
                       YesFun?.Invoke(_response);

                       int RedNum = _response.status == (int)ActivityM.SignState.CanGet ? 1 :0;
                       RedPointManager.Instance.RPS.Dispatch(RedPointConst.FirstPay.ToString(), RedNum);
                   }
                   else
                   {
                       UITool.ShowTips(_response.data);
                   }
               });
        }

        //首充领取
        public static void GetFirstPayGet(int Id, Action<HTTP_ActivityReceiveDayResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityListRequset
            {
                activity_id = Id
            });
            HttpRequestHelper.SendPost<HTTP_ActivityReceiveDayResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_FirstPayGetUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
               YesFun);
        }

        //活动开启状态
        public static void GetActivityState(Action callBack)
        {
            HttpRequestHelper.SendPost<HTTP_ActivityAllResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_ActiveStateUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, "",
              (_response) => {
                  if (_response.code == 0)
                  {
                      ActivityM.Instance.SetActivityState(_response.data);
                      JEvent.defaultEvent.Post(new GameEvent.Refresh_ActiveState());

                      //RedPointManager.Instance.RPS.Dispatch(RedPointConst.GiftBag_GiftPay.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.GiftBag_GiftPay.ToString()));
                      //RedPointManager.Instance.RPS.Dispatch(RedPointConst.GiftBag_PetGift.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.GiftBag_PetGift.ToString()));
                      //RedPointManager.Instance.RPS.Dispatch(RedPointConst.GiftBag_PartnerGift.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.GiftBag_PartnerGift.ToString()));

                      //RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_Level.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.Rank_Level.ToString()));
                      RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_Power.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.Rank_Power.ToString()));
                      //RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_SiLian.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.Rank_SiLian.ToString()));
                      RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_ZhanHun.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.Rank_ZhanHun.ToString()));
                      RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_HuiHe.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.Rank_HuiHe.ToString()));

                      if (callBack != null) callBack();
                  }
              });
        }

        //每日签到信息列表
        public static void GetSignList(Action<HTTP_SignListResponse> YesFun = null)
        {
            HttpRequestHelper.SendPost<HTTP_SignListResponse>(GameM.Instance.HttpUrl + GameM.Instance.Sign_ListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, "",
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);

                      int RedCount = 0;
                      var (singList, accumulateList) = CFG_Sign.GetSignAndAccumulateListByRound(_response.data.day_award.round);
                      if (!_response.data.day_award.sign)
                      {
                          RedCount = 1;
                      }
                      if (RedCount == 0)
                      {
                          if (accumulateList[_response.data.accumulate_award.Length].Days < _response.data.day_award.day)
                          {
                              RedCount = 1;
                          }
                      }
                      RedPointManager.Instance.RPS.Dispatch(RedPointConst.SignDay.ToString(), RedCount);
                  }
              });
        }

        //签到某天
        public static void GetSignOneDay( Action<HTTP_SignOperateResponse> YesFun = null)
        {
           
            HttpRequestHelper.SendPost<HTTP_SignOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Sign_SignDayUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, "",
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
              });
        }

        //累计签到某天
        public static void GetSignAccumulateDay(int _Round, int _Day, Action<HTTP_SignOperateResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_SignOperateRequest
            {
                round = _Round,
                day = _Day,
            });
            HttpRequestHelper.SendPost<HTTP_SignOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Sign_SignExtractUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
                  //else
                  //{
                  //    UITool.ShowTips(_response.data);
                  //}
              });
        }

        //特权卡 充值情况
        public static void GetPrivilegeList(Action<HTTP_PrivilegeListResponse> YesFun = null)
        {
            string strRequest = string.Empty;
            HttpRequestHelper.SendPost<HTTP_PrivilegeListResponse>(GameM.Instance.HttpUrl + GameM.Instance.Privilege_ListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);

                      int RedNum = 0;
                      foreach (var item in _response.data)
                      {
                          if (item.buy == 2 && item.get == 1)
                          {
                              RedNum = 1;
                              break;
                          }
                      }
                      RedPointManager.Instance.RPS.Dispatch(RedPointConst.PrivilegeCard.ToString(), RedNum);
                  }
              });
        }

        //特权卡 充值每日领取
        public static void GetPrivilegeSign(int pid,Action<HTTP_PrivilegeReceiveDayResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_PrivilegeReceiveDayRequset
            {
                pid = pid
            });
            HttpRequestHelper.SendPost<HTTP_PrivilegeReceiveDayResponse>(GameM.Instance.HttpUrl + GameM.Instance.Privilege_SignUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }
              });
        }

        //排行榜
        public static void GetRankList(CFG_RankingAward.RankClass rankClase, Action<HTTP_RankListResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_RankListRequest
            {
                 type = (int)rankClase,
                 all = 1,
            });
            HttpRequestHelper.SendPost<HTTP_RankListResponse>(GameM.Instance.HttpUrl + GameM.Instance.Rank_ListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
                  else
                  {
                      //UITool.ShowTips(_response.code);
                  }
              });
        }

        //百包箱抽奖
        public static void SetTreasureBox(int _type, Action<HTTP_ActivityTreasureBoxResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityTreasureBoxRequest
            {
                type = _type,
            });
            HttpRequestHelper.SendPost<HTTP_ActivityTreasureBoxResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_ReceivetreasureBoxUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }
              });
        }

        //百宝箱抽奖列表
        public static void GetTreasureBox(Action<HTTP_ActivityListTreasureBoxResponse> YesFun = null)
        {
            string strRequest = "";
            HttpRequestHelper.SendPost<HTTP_ActivityListTreasureBoxResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_GetData_ReceivetreasureBoxUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);

                      int RedCount = 0;
                      if (_response.can_play > 0 && GameUtil.Instance.ComCoinToCheck(CFG_TreasureBox.GetBox(RoleM.Instance.Level).Consume[0].id, CFG_TreasureBox.GetBox(RoleM.Instance.Level).Consume[0].num, null, false))
                      {
                          RedCount = 1;
                      }
                      RedPointManager.Instance.RPS.Dispatch(RedPointConst.TurnBox.ToString(), RedCount);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }
              });
        }

        //点券大转盘 信息
        public static void GetListTurnTable(Action<HTTP_ActivityTruntableResponse> YesFun = null)
        {
            if (!ActivityM.Instance.GetAcitvityState(ActivityType.TurnWheel))
                return;

            string strRequest = JsonMapper.ToJson(new HTTP_ActivityTruntableRequest
            {
                 activity_id = 1,
            });
            HttpRequestHelper.SendPost<HTTP_ActivityTruntableResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_ListTurntableUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      RedDotM.Instance.TurnWheelId = _response.content;
                      YesFun?.Invoke(_response);

                      RedPointManager.Instance.RPS.Dispatch(RedPointConst.RouletteWheel.ToString(), RedDotM.Instance.GetTurnWheelRedNum());
                  }
                  else
                  {
                      UITool.ShowTips(_response.error);
                  }
              });
        }

        //点券大转盘 抽奖
        public static void GetTurnTableClick(Action<HTTP_ActivityTruntableClickResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityTruntableRequest
            {
                activity_id = 1,
            });
            HttpRequestHelper.SendPost<HTTP_ActivityTruntableClickResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_ReceiveTurntableUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }
              });
        }

        /// <summary>
        /// 升级 命格
        /// </summary>
        public static void GetFateUpLv(int cfgId,long Uid, Action<HTTP_FateUpLvResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_FateUpLvRequest
            {
                num = 1,
                uid = Uid,
                config_id = cfgId,
            });
            HttpRequestHelper.SendPost<HTTP_FateUpLvResponse>(GameM.Instance.HttpUrl + GameM.Instance.Fate_UpLvUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) => {
                  YesFun?.Invoke(_response);
                  //if (_response.code == 0)
                  //{
                  //    YesFun?.Invoke(_response);
                  //}
                    
              });
        }

        /// <summary>
        /// 升阶 命格
        /// </summary>
        public static void GetFateUpAdvance( Action<HTTP_FateUpAdvanceResponse> YesFun = null)
        {
            string strRequest = string.Empty;
            HttpRequestHelper.SendPost<HTTP_FateUpAdvanceResponse>(GameM.Instance.HttpUrl + GameM.Instance.Fate_UpAdvanceUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }

              });
        }

        /// <summary>
        /// 伙伴抽取 列表
        /// </summary>
        public static void GetPrivilegeDetail(int module, Action<HTTP_PrizedrawListResponse> YesFun = null,Action NoFun= null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_PrizedrawListRequest
            {
                module = module,//1-伙伴，2-宠物
            }); 
            HttpRequestHelper.SendPost<HTTP_PrizedrawListResponse>(GameM.Instance.HttpUrl + HttpM.Instance.PrivilegeDetailUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                    for (int i = 0; i < _response.canplay.Length; i++)
                    {
                          int idx = _response.canplay[i].type - 1;
                            if (module == 1)
                                PartnerM.Instance.CanGetNums[idx] = _response.canplay[i].can_play;
                            else
                                PetM.Instance.CanGetNums[idx] = _response.canplay[i].can_play;

                    }
                      YesFun?.Invoke(_response);
                      if (module == 1)
                          RedPointManager.Instance.RPS.Dispatch(RedPointConst.Partner_Call);
                      else
                          RedPointManager.Instance.RPS.Dispatch(RedPointConst.Pet_Award);
                  }
                  else
                  {
                      NoFun?.Invoke();
                      UITool.ShowTips(_response.data);
                  }
              });
        }

        /// <summary>
        /// 伙伴抽取
        /// </summary>
        public static void GetPrivilegeAward(int _type,int _ptype, Action<HTTP_PrizedrawResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_PrizedrawRequest
            {
                type = _type,
                ptype = _ptype,
            });
            HttpRequestHelper.SendPost<HTTP_PrizedrawResponse>(GameM.Instance.HttpUrl + HttpM.Instance.PrivilegeAwardGetUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
              (_response) => {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }

              });
        }

        /// <summary>
        /// 宠物抽奖
        /// </summary>
        public static void GetPetReceive(int _type, int _ptype, Action<HTTP_PrizedrawResponse> YesFun = null,Action FallCallBack = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_PrizedrawRequest
            {
                type = _type,
                ptype = _ptype,
            });
            HttpRequestHelper.SendPost<HTTP_PrizedrawResponse>(GameM.Instance.HttpUrl + HttpM.Instance.PetReceiveGetUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
              (_response) => {
                  YesFun?.Invoke(_response);
                  if (_response.code == 0)
                  {
                      var cfg_Pet = CFG_Pet.GetById(_response.award[0].id);
                      if (cfg_Pet!= null && cfg_Pet.Mark == 1 && cfg_Pet.Quality >= 7)
                          UITool.Open<UIPetAward>(null, cfg_Pet.ID);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }

              }, FallCallBack);
        }

        /// <summary>
        /// 试炼基金 获取奖励列表
        /// </summary>
        /// <param name="YesFun">获取列表信息成功</param>
        public static void GetTrialFundAwardInfo(Action<HTTP_ActivityTrialFundAwardResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityTrialFundRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_ActivityTrialFundAwardResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_TrialFundListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
               (_response) => {
                   if (_response.code == 0)
                   {
                       //ActivityM.Instance.SetLevelGiftDic(_response);
                       YesFun?.Invoke(_response);
                       //int redNum = ActivityM.Instance.GetTrialFundCanGetAwardCount(_response);
                       //RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_Tower_Award.ToString(), redNum);
                   }
               });
        }

        /// <summary>
        /// 试炼基金 一键领取奖励
        /// </summary>
        /// <param name="YesFun">领取成功</param>
        public static void GetTrialFundAward(Action<HTTP_ActivityTrialFundGetAwardResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_ActivityTrialFundRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_ActivityTrialFundGetAwardResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_TrialFundGetUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
              (_response) =>
              {
                  if (_response.code == 0)
                  {
                      // 刷新试炼基金奖励列表
                      YesFun?.Invoke(_response);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }
              });
        }

        public static void ShuShuSendGuide(HTTP_WindowShushuCompletedRequest data)
        {
            string strRequest = JsonMapper.ToJson(data);
            HttpRequestHelper.SendPost<HTTP_WindowShushuResponse>(GameM.Instance.HttpUrl + GameM.Instance.ShuShu_GuideCompleted, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) =>
              {
              });
        }

        public static void ShuShuSendOpenBox(HTTP_WindowShusOpenBoxRequest data)
        {
            string strRequest = JsonMapper.ToJson(data);
            HttpRequestHelper.SendPost<HTTP_WindowShushuResponse>(GameM.Instance.HttpUrl + GameM.Instance.ShuShu_OpenBox, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
              (_response) =>
              {
              });
        }

        #region 成长基金
        public static void OnGetGrowthFundInfo(Action<HTTP_GrowthFundResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_GrowthFundRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_GrowthFundResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_GrowthFundListUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Token, strRequest,
               (_response) => {
                   if (_response.code == 0)
                   {
                       YesFun?.Invoke(_response);
                       int redNum = GiftBagM.Instance.GetGrowthFundCanGetAwardCount(_response);
                       RedPointManager.Instance.RPS.Dispatch(RedPointConst.GiftBag_GrowthFund.ToString(), redNum);
                   }
               });
        }

        /// <summary>
        /// 试炼基金 一键领取奖励
        /// </summary>
        /// <param name="YesFun">领取成功</param>
        public static void OnGetGrowthFundAward(Action<HTTP_GrowthFundGetAwardResponse> YesFun = null)
        {
            string strRequest = JsonMapper.ToJson(new HTTP_GrowthFundRequset
            {
                activity_id = 1
            });
            HttpRequestHelper.SendPost<HTTP_GrowthFundGetAwardResponse>(GameM.Instance.HttpUrl + GameM.Instance.Activity_GrowthFundGetUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
              (_response) =>
              {
                  if (_response.code == 0)
                  {
                      YesFun?.Invoke(_response);
                  }
                  else
                  {
                      UITool.ShowTips(_response.data);
                  }
              });
        }
        #endregion
    }
}
