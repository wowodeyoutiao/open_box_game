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

    /// <summary>
    /// 角色列表最多显示几个位置。
    ///
    /// prefab 里只有 itemRole0/itemRole1 两个节点（原服配置 rolenum=2），
    /// 所以多出来的位置在 OnEnter 时按 itemRole1 克隆，不动 prefab。
    /// 上限取 config.xml 的 rolenum，这里留一个兜底值。
    /// </summary>
    private const int DefaultRoleSlots = 4;

    private readonly List<GameObject> _roleItems = new List<GameObject>();

    /// <summary>取（必要时克隆出）角色列表条目，保证至少有 need 个。</summary>
    private void EnsureRoleItems(int need)
    {
        Transform root = GetView().Root_Btn;
        if (root == null)
            return;

        if (_roleItems.Count == 0)
        {
            // 收集 prefab 里现成的 itemRole*
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.StartsWith("itemRole"))
                    _roleItems.Add(child.gameObject);
            }
        }
        if (_roleItems.Count == 0)
            return;   // prefab 结构异常，保持原样

        if (need < _roleItems.Count)
            need = _roleItems.Count;

        // 不够就克隆最后一个，克隆体沿用同样的子结构（Text / txt_level / icon / on）
        while (_roleItems.Count < need)
        {
            GameObject src = _roleItems[_roleItems.Count - 1];
            GameObject clone = UnityEngine.Object.Instantiate(src, root);
            clone.name = "itemRole" + _roleItems.Count;
            clone.SetActive(false);
            _roleItems.Add(clone);
        }
    }

    private int RoleSlots
    {
        get { return _roleItems.Count > 0 ? _roleItems.Count : 2; }
    }

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
        for (int i = 0; i < RoleSlots; i++)
        {
            GameObject Item = _roleItems[i];
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
        // 角色为空时不调 OnSelectRoleBtnClick(0)，否则会越界
        if (RoleInfo != null && RoleInfo.Count > 0)
            OnSelectRoleBtnClick(0);

    }

    public override void OnLayerChange()
    {
        for (int i = 0; i < RoleSlots; i++)
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
        for (int i = 0; i < RoleSlots; i++)
        {
            Transform item = GetView().Root_Btn.transform.Find("itemRole" + i);
            if (item == null)
                continue;
            item.localScale = idx == i ? Vector3.one * 1.2f : Vector3.one;
            Transform on = item.Find("on");
            if (on != null)
                on.gameObject.SetActive(idx == i);
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
        int need = DefaultRoleSlots;
        try
        {
            int cfgMax = CFG_XMLConfig.xmlConfig.BaseValue.global.rolenum;
            if (cfgMax > 0)
                need = cfgMax;
        }
        catch { }
        EnsureRoleItems(need);

        RefreshRoles();
        SetBtnState();
    }

    private void SetBtnState()
    {
        GetView().Btn_Creat.gameObject.SetActive(CFG_XMLConfig.xmlConfig.BaseValue.global.rolenum > RoleM.Instance.LoginCharacterInfo.Count);

        // prefab 里 Button_Delete 的 m_IsActive 是 0（原服靠别处打开），
        // 单机版没有任何地方会开它，导致删除入口一直看不见。这里显式控制：
        // 有角色才允许删，一个都没有时隐藏。
        if (GetView().Btn_Delete != null)
            GetView().Btn_Delete.gameObject.SetActive(RoleM.Instance.LoginCharacterInfo.Count > 0);
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