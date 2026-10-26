using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using JEngine.LitJson;
using JEngine.MyNet;
using pb;
using System;
using JEngine.Event;
using UnityEngine.SceneManagement;
using JEngine.UI;
using Spine.Unity;
using System.Collections.Generic;
using System.IO;
using HotUpdateScripts;

public class UISelectRole : UIBase<UIModelSelectRole, UIViewSelectRole>
{
    public override UILayer GetLayer()
    {
        return UILayer.Base;
    }

    private int Select_Idx = -1;
    public override void OnInit()
    {
        GetView().Btn_Creat.onClick.AddListener(delegate { 
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnCreatBtnClick();
        } );

        GetView().Btn_Delete.onClick.AddListener(OnDeleteBtnClick);
        GetView().Btn_StarGame.onClick.AddListener(OnStarGameBtnClick);

        GetView().btn_Back.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UISelectRole>();
        });
    }
    

    private void OnCreatBtnClick()
    {
        UITool.Open<UICreatRole>();
       // UITool.Close<UISelectRole>();
    }

    private void OnDeleteBtnClick()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        if (Select_Idx > -1)
        {
            long deleteUid = RoleM.Instance.LoginCharacterInfo[Select_Idx].uid;
            string selectInfo = JsonMapper.ToJson(new HTTP_LoginrateRequest()
            {
                uid = deleteUid
            });
            Action yesFun = () => {
                HttpRequestHelper.SendPost<HTTP_LoginrateResponse>(GameM.Instance.HttpUrl + GameM.Instance.Role_DeleteUrl, NetworkHelper.HTTPMethod.DELETE, RoleM.Instance.Login_Token, selectInfo,
                (response) => {
                    if (response.code == 0)
                    {
                        int dIdx = RoleM.Instance.LoginCharacterInfo.FindIndex(x => x.uid == deleteUid);
                        RoleM.Instance.LoginCharacterInfo.RemoveAt(dIdx);
                        if (RoleM.Instance.LoginCharacterInfo.Count == 0)
                        {
                            OnCreatBtnClick();
                            UITool.Close<UISelectRole>();
                        }
                        else
                        {
                            RefreshRoles();
                            SetBtnState();
                        }

                    }
                });
            };
            UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack { 
                ShowStr = $"是否确定删除当前角色:{RoleM.Instance.LoginCharacterInfo[Select_Idx].name}?",
                YesFun = () => {
                    UITool.Open<UIMessageDestoryRole>(null, yesFun);
                },
            });
        }
    }

    private void RefreshRoles()
    {
        var RoleInfo = RoleM.Instance.LoginCharacterInfo;
        for (int i = 0; i < 2; i++)
        {
            GameObject Item = GetView().Root_Btn.transform.Find("itemRole" + i).gameObject;
            Item.SetActive(i < RoleInfo.Count);
            if (i < RoleInfo.Count)
            {
                Item.transform.Find("Text").GetComponent<Text>().text = RoleInfo[i].name;
                Item.transform.Find("txt_level").GetComponent<Text>().text = RoleInfo[i].level + "级";
                Item.transform.Find("icon/txt_job").GetComponent<Text>().text = CFG_Item.GetJobStrById(RoleInfo[i].job);
                ResouseHelper.Instance.LoadSpriteByAtlas(SelectRoleAtlas, JobIconName[RoleInfo[i].job], JUIExtensions.img(Item.transform, "icon"));
                Item.GetComponent<Button>().onClick.RemoveAllListeners();
                var idx = i;
                Item.GetComponent<Button>().onClick.AddListener(() => {
                    OnSelectRoleBtnClick(idx);
                });
            }

        }
        OnSelectRoleBtnClick(0);

    }

    public override void OnLayerChange()
    {
        for (int i = 0; i < 2; i++)
        {
             GameUtil.Instance.SetEffectLayer(GetView().Root_Btn.transform.Find("itemRole" + i + "/on"), _canvasOrder + 1);
        }
        GameUtil.Instance.SetEffectLayer(GetView().img_job.transform, _canvasOrder + 1);
    }

    //开始游戏
    private void OnStarGameBtnClick()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        string selectInfo = JsonMapper.ToJson(new HTTP_SelectCharacterRequest() { 
            uid = RoleM.Instance.LoginCharacterInfo[Select_Idx].uid 
        });

        RoleM.Instance.Post = RoleM.Instance.LoginCharacterInfo[Select_Idx].port.ToString();
        RoleM.Instance.IP = RoleM.Instance.LoginCharacterInfo[Select_Idx].ip;

        UITool.Open<UILoading>(null, new UILoading.Loading { _time = 5f });

        HttpRequestHelper.SendPost<HTTP_SelectCharacterResponse>(GameM.Instance.HttpUrl + GameM.Instance.Role_SelectUrl, NetworkHelper.HTTPMethod.POST, RoleM.Instance.Login_Token, selectInfo,
            (response) => {
                if (response.code == 0 && !isNullGo)
                {
                    RoleM.Instance.Token = response.token;
                    RoleM.Instance.SetSelectRole(RoleM.Instance.LoginCharacterInfo[Select_Idx]);
                    OnConnecdTCP();
                }
                else
                {
                    UITool.ShowTips("err code" + response.code);
                }
            });

    }

    public static string SelectRoleAtlas = "Common/Atlas/CreatRole";
    public static string[] JobIconName = { "", "icon_1", "icon_2", "icon_3" };
    private void OnSelectRoleBtnClick(int idx)
    {
        for (int i = 0; i < 2; i++)
        {
            GetView().Root_Btn.transform.Find("itemRole" + i).transform.localScale = idx == i ? Vector3.one * 1.2f : Vector3.one;
            GetView().Root_Btn.transform.Find("itemRole" + i+ "/on").gameObject.SetActive(idx == i);
        }

        Select_Idx = idx;
        int job = RoleM.Instance.LoginCharacterInfo[Select_Idx].job;
        ResouseHelper.Instance.LoadSpriteByAtlas(SelectRoleAtlas, JobIconName[job], GetView().img_job);
        GetView().txt_JobName.text = CFG_Item.GetJobStrById(job);

        var strs = UIModelCreatRole.JobDesDic[(JobType)job];
        for (int i = 0; i < strs.Length; i++)
        {
            GetView().JobDesTxts[i].text = strs[i];
        }
        SetRolec((JobType)job, (SexType)RoleM.Instance.LoginCharacterInfo[Select_Idx].sex);
    }

    private void SetRolec(JobType jobType, SexType sexType)
    {
        if (GetView().root_Role.childCount > 0)
        {
             for (int i = 0; i < GetView().root_Role.childCount; i++)
            {
                PoolHelper.Instance.RecycObj(GetView().root_Role.GetChild(i).gameObject);
            }
        }

        _ = GameHelper.Instance.SetRoleAsync(jobType, sexType, (_mainRole) =>
        {
            _mainRole.transform.SetParent(GetView().root_Role);
            _mainRole.transform.localScale = Vector3.one;
            _mainRole.transform.localPosition = Vector3.zero;
            _mainRole.SetActive(true);

            SpineUtil.Instance.PlayQueue(_mainRole.transform.Find("spine").GetComponent<SkeletonGraphic>(), new List<string> { "skill2", "skill3", "stand" }, null, true);
        });

    }
    public override void OnEnter(params object[] args)
    {
        RefreshRoles();
        SetBtnState();
    }

    private void SetBtnState()
    {
        GetView().Btn_Creat.gameObject.SetActive(CFG_XMLConfig.xmlConfig.BaseValue.global.rolenum > RoleM.Instance.LoginCharacterInfo.Count);
    }

    public override void OnExit()
    {
        
    }

    private int ConnectCount = 0;
    /// <summary>
    /// 开启TCP链接
    /// </summary>
    private void OnConnecdTCP()
    {
        // 严格离线不建立真实 Socket；LocalCompare 仍需连接本地 Go 服做协议对照。
        if (HotUpdateScripts.LocalNet.LocalRuntime.IsStrictOffline)
        {
            Debug.Log("[LocalServer] 严格离线模式，跳过 Socket 连接");
            OnLoginToSever(null);
            return;
        }

        ConnectCount++;
        TCPClientMgr.Instance.AddIpToPool(RoleM.Instance.IP, RoleM.Instance.Post);
        TCPClientMgr.Instance.OnSocketConnectResult = null;
        TCPClientMgr.Instance.OnSocketConnectResult += (_t)=>  { Debug.Log("OnSocketConnectResult-----");
            OnLoginToSever(_t);
        };
        TCPClientMgr.Instance.OnSocketHasError = null;
        TCPClientMgr.Instance.OnSocketHasError += (_t)=> {
            Debug.Log("OnSocketHasError-----");
            UITool.Close<UILoading>();
            TCPClientMgr.Instance.ClearIpPool();
            bool isLogin = SceneManager.GetActiveScene().name == "Login";
            Action yesFun = delegate { OnConnecdTCP(); };

            Action NoFun = delegate {
                ConnectCount = 0;
                TCPClientMgr.Instance.ClearIpPool();
                if (isLogin)
                {
                    UITool.Close<UISelectRole>();
                    UITool.Open<UILogin>();
                }
                else
                {
                    JEvent.defaultEvent.Post(new SenceChange()
                    {
                        scencePath = GameConst.LoginPath,
                    });
                }
            };

            if (GameM.Instance.isSeverClose)
            {
                return;
            }

            if (ConnectCount > 3)
            {
                var info = new UIMessageBoxOne.CallBack
                {
                    showStr = "网络异常，请重新登录",
                    cType = 0,
                    endFun = NoFun,
                };
                UITool.Open<UIMessageBoxOne>(null, info);
            }
            else
            {
                UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack { ShowStr = "已断开连接，请重新连接", NoFun = NoFun, YesFun = yesFun });
            }

        };
        UITool.Open<UILoading>(null, new UILoading.Loading { _time = 0 });
        TCPClientMgr.Instance.ConnectSocket();
    }

    private bool _IsConnect = false;
    /// <summary>
    /// 检测链接信息
    /// </summary>
    /// <param name="group"></param>
    private void OnLoginToSever(WebClientGroup group)
    {
        UITool.Close<UILoading>();
        // 严格离线视为已连接；LocalCompare 必须等真实 Socket 建连。
        bool connected = HotUpdateScripts.LocalNet.LocalRuntime.IsStrictOffline
                         || (group != null && group.isConnect);
        if (connected)
        {
            _IsConnect = false;
            var aaa = new LoginToGameServerRequest
            {
                Name = RoleM.Instance.Nickname,
                Uid = RoleM.Instance.Uid,
                HeadUrl = RoleM.Instance.HeadUrl,
                Token = RoleM.Instance.Token,
            };
            byte[] aa = TCPClientMgr.ProtoSerialize(aaa);
            Debug.Log("Send ---C2S_Login");
            TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_Login, MType.Request, aa);
        }
        else
        {

            Debug.Log("isConnect  ----- False");
            TCPClientMgr.Instance.ClearIpPool();
            if (!_IsConnect)
            {
                _IsConnect = true;
                var callBack = new UIMessageBox.CallBack { ShowStr = "服务器连接失败，请重试", YesFun = delegate { OnConnecdTCP(); } };
                if (SceneManager.GetActiveScene().name != "Login")
                {
                    callBack.NoFun = delegate { GameHelper.Instance.BackToLogin(); };
                }
                UITool.Open<UIMessageBox>(null, callBack);
            }
            else
            {
                _IsConnect = false;
                UITool.Open<UIMessageBoxOne>(null, new UIMessageBoxOne.CallBack { 
                    showStr = "服务器连接失败，请稍候再试",
                    endFun = delegate { GameHelper.Instance.BackToLogin(); },
                });
            }
        }
    }
}