using MetaFramework.UI;
using System;
using UnityEngine;
using UnityEngine.UI;
using JEngine.Core;
using JEngine.MyNet;
using JEngine.Helper;
using HotUpdateScripts.ProtoM;
using UnityEngine.SceneManagement;
using Spine.Unity;
using UpdateMode = JEngine.Core.Updater.UpdateMode;

public class UIModelLogin : IUIModel
{

}

public class UIViewLogin :MonoBehaviour, IUIView
{
    public Button Btn_GongGao;
    public Button btn_ChatCustomer;


    public Button Btn_Login_Accout;
    public Button Btn_Login_Phone;
    public InputField intput_account;
    public Text txt_test;
    public Text txt_Version;

    public Button btn_WXLogin;

    public Toggle toggle_Arg;
    public Button btn_Argee;
    public Button btn_YinSi;

    public Image img_SeverNew;
    public Button btn_SeverChoice;
    public Image img_Sever;
    public Text txt_severName;
    public Text txt_SeverDes;
    public Text txt_NewSever;

    public Button Btn_GetSever;

    public GameObject obj_Root;

    public Text txt_Load;
    public Slider slider_Load;

    public void Awake()
    {
    }

    public void Init(GameObject go)
    {
    }
    private void OnDestroy()
    {
        UITool.RemoveUI<UILogin>();
    }
}

public class UILogin :UIBase<UIModelLogin, UIViewLogin>
{
    public override UILayer GetLayer()
    {
        return UILayer.Base;
    }

    public struct Event_Test{}
    public override  void OnInit()
    {
        GetView().Btn_GongGao.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UINotice>();
        });

        GetView().btn_ChatCustomer.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            SDKCtr.Instance.OnWXCustomerService("customer service",null,null);
        });

        GetView().Btn_Login_Accout.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            SDKCtr.Instance.OnUpCheak(OnHttp_Accout_Login);
        });
        GetView().Btn_Login_Phone.onClick.AddListener(PhoneLogin);

        GetView().btn_SeverChoice.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIChoiceSever>(null, () => { OnSeverInfoShow(false); });
        });

        AudioManager.Instance.PlayBgMusic(AudioKey.Sound.BGM_Slect);
        RedPointManager.Instance.ClearRed();

        GetView().txt_Version.text = PlayerPrefsExtend.GetString("Version");

        GetView().btn_WXLogin.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            if (PlayerPrefsExtend.GetInt("Agree") == 0)
            {
                UITool.ShowTips("请先阅读并同意《用户协议》、《隐私政策》");
                return;
            }
        });

        GetView().btn_Argee.onClick.AddListener(delegate
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UILoginDes>(null, UILoginDes.DesType.Agree);
        });

        GetView().btn_YinSi.onClick.AddListener(delegate
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UILoginDes>(null, UILoginDes.DesType.Privacy);
        });

        GetView().toggle_Arg.onValueChanged.AddListener((isOn) =>
        {
            PlayerPrefsExtend.SetInt("Agree", isOn ? 1 : 0);
        });

        GetView().Btn_GetSever.onClick.AddListener(() => {
            if (string.IsNullOrEmpty(GetView().intput_account.text))
            {
                UITool.ShowTips("请输入账号");
                return;
            }
            PlayerPrefsExtend.SetString("TextAcount", GetView().intput_account.text);
            GetSeverInfo();

        });

        OnInitUserData();
    }

    public override void OnEnter(params object[] args)
    {
        //GetView().toggle_Arg.isOn = PlayerPrefsExtend.GetInt("Agree") == 0;
        GetView().intput_account.text = PlayerPrefsExtend.GetString("TextAcount");
        OnLoadShow();
        if (!ExcelDataManager.Instance.IsLoad)
            OnLoadingShow();
    }

    private void OnLoadingShow()
    {
        ExcelDataManager.Instance.LoadAllCFG((loadCount, totalCount) =>
        {
            GetView().txt_Load.text = $"编译中...{loadCount}/{totalCount}";
            GetView().slider_Load.value = loadCount/ (float)totalCount;
            if (loadCount == totalCount)
                OnLoadShow(); 
        });
        SDKCtr.Instance.OnAuthorize(null,null);
    }


    private void OnLoadShow()
    {
        GetView().slider_Load.gameObject.SetActive(!ExcelDataManager.Instance.IsLoad);
        GetView().intput_account.gameObject.SetActive(InitJEngine.Instance.IsFreeLogin);

        if (SDKCtr.Instance.CurrentPlatform == SDKCtr.PlatformType.WebGL_WX && ExcelDataManager.Instance.IsLoad)
        {
            GetView().obj_Root.SetActive(false);
            if (ExcelDataManager.Instance.IsLoad)
            {
                UITool.Open<UILoading>(null, new UILoading.Loading { _time = 0 });
                SDKCtr.Instance.OnWXWebLogin((code) => {
                    GameM.Instance.WXCode = code;
                    HttpC.OnGetOpenID(code, (msg) => {
                        GameM.Instance.WXOpneID = msg.data.openid;
                        PlayerPrefsExtend.SetString("TextAcount", GameM.Instance.WXOpneID);
                        GetView().intput_account.text = GameM.Instance.WXOpneID;
                        GetSeverInfo();
                    });
                }, null);
            }
        }
        else
        {
            GetView().obj_Root.SetActive(ExcelDataManager.Instance.IsLoad);
            if (ExcelDataManager.Instance.IsLoad)
                GetSeverInfo();
        }
    }


    private void GetSeverInfo()
    {
        // 配置表已加载完成。这里必须先恢复本地存档并装配本地路由，
        // 否则 HTTP 登录会先进入 LocalData 的隐式空初始化。
        if (HotUpdateScripts.LocalNet.LocalRuntime.IsLocalMode)
            HotUpdateScripts.LocalNet.LocalBootstrap.Init();

        // 本地单机模式：远端区服后台（shbinqigame.com:8080）已下线，
        // 不再发起请求，直接初始化本地服列表并刷新 UI。
        // 否则请求必然失败 → SeverInfoDic 为空 → 点登录时取 [0][0] 抛 KeyNotFoundException。
        if (GameM.IsLocalMode)
        {
            GameM.Instance.SeverIdx = 0;
            GameM.Instance.SeverDicIdx = 0;
            GameM.Instance.InitLocalSeverInfo();
            UITool.Close<UILoading>();
            GetView().obj_Root.SetActive(true);
            OnSeverInfoShow(true);
            Debug.Log("GetSeverInfo: 本地单机模式 → " + GameM.LocalSeverUrl);
            return;
        }

        if (!string.IsNullOrEmpty(PlayerPrefsExtend.GetString("TextAcount")))
        {
            GameM.Instance.SeverIdx = 0;
            GameM.Instance.SeverDicIdx = 0;
            HttpC.OnGetSeverInfo(new HTTP_SeverRequest { pageIndex = 1, pageSize = 20 }, (_r) =>
            {
                if (SDKCtr.Instance.CurrentPlatform == SDKCtr.PlatformType.WebGL_WX)
                {
                    UITool.Close<UILoading>();
                    GetView().obj_Root.SetActive(true);
                }
                UITool.Close<UILoading>();
                OnSeverInfoShow(true);
            }, () => {
                if (SDKCtr.Instance.CurrentPlatform == SDKCtr.PlatformType.WebGL_WX)
                    GetSeverInfo();
            });
        }

    }

    public void OnSeverInfoShow(bool isInit)
    {
        // 防御：下面直接读 SeverInfoDic[1][0]，若列表尚未初始化（例如 UIChoiceSever 先于
        // GetSeverInfo 打开、或远端请求失败）会 KeyNotFoundException，这里先补一份本地列表。
        if (GameM.IsLocalMode
            && (!GameM.Instance.SeverInfoDic.ContainsKey(1) || GameM.Instance.SeverInfoDic[1].Count == 0))
        {
            GameM.Instance.InitLocalSeverInfo();
        }

        GetView().txt_NewSever.text = GameM.Instance.SeverInfoDic[1][0].name;
        ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/CreatRole", "img_" + GameM.Instance.SeverInfoDic[1][0].status, GetView().img_SeverNew);

        if (GameM.Instance.SeverInfoDic[0].Count == 0 && isInit)
        {
            GameM.Instance.SeverIdx = 0;
            GameM.Instance.SeverDicIdx = 1;
        }
        var SelectSever = GameM.Instance.SeverInfoDic[GameM.Instance.SeverDicIdx][GameM.Instance.SeverIdx];
        GetView().txt_severName.text = SelectSever.name;

        Debug.Log("SelectSever.ipaddr --- " + SelectSever.ipaddr);
        
        ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/CreatRole", "img_" + SelectSever.status, GetView().img_Sever);
    }

    private void OnHttp_Accout_Login()
    {
        //if (GetView().intput_account.text == "" || string.IsNullOrEmpty(GetView().txt_severName.text))
        //{
        //    UITool.ShowTips("输入账号,获取服务器后再登录");
        //    return;
        //}

        if (SDKCtr.Instance.CurrentPlatform == SDKCtr.PlatformType.WebGL_WX  && GameM.Instance.SeverInfoDic[GameM.Instance.SeverDicIdx][GameM.Instance.SeverIdx].status == 0)
        {
            UITool.ShowTips("服务器已关闭");
            return;
        }
        //PlayerPrefsExtend.SetString("TextAcount",GetView().intput_account.text);

#if UNITY_EDITOR
        if (GetView().intput_account.text.Contains('@'))
        {
            Debug.Log("wc Test Login");
            var loginStr = GetView().intput_account.text.Split('@');
            if (loginStr.Length == 2)
            {
                HttpC.OnLoginSever(new HTTP_LoginRequest
                {
                    platform = "unity",
                    appid = "mow",
                    openid = loginStr[1],
                }, HttpC.LoginType.Login3rd);
                return;
            }
        }
#endif
        UITool.Open<UILoading>((ui) => {
            // 服务端 guest 登录会校验 IMEI，为空直接拒绝；兜底成固定值保证本地可登录
            var imei = GetView().intput_account.text;
            if (string.IsNullOrEmpty(imei))
                imei = PlayerPrefsExtend.GetString("TextAcount");
            if (string.IsNullOrEmpty(imei))
            {
                imei = "editor_local";
                PlayerPrefsExtend.SetString("TextAcount", imei);
                GetView().intput_account.text = imei;
            }

            var loginRequest = new HTTP_LoginRequest
            {
                appid = "wxChat",
                channelId = "wx",
                imei = imei,
                serverId = GameM.Instance.SeverInfoDic[GameM.Instance.SeverDicIdx][GameM.Instance.SeverIdx].server_id,
            };
            Debug.Log($"OnHttp_Accout_Login: imei={imei}, serverId={loginRequest.serverId}, url={GameM.Instance.HttpUrl}");
            HttpC.OnLoginSever(loginRequest, HttpC.LoginType.Account);
        }, new UILoading.Loading { _time = 0 });
        
    }

    private void PhoneLogin()
    {
        //UITool.Open<UIPhoneLogin>();
    }

    private void OnInitUserData()
    {
        RoleM.Instance.NowOpenLevel = 0;
        RoleM.Instance.NowOpenJob = 0;
        RoleM.Instance.IsReceivedGuideData = false;
        RoleM.Instance.IsFirstReceiveBaseInfo = true;
        RoleM.Instance.GuideTypes = null;
        StateM.Instance.isSendUpgradeOrSpeedUpBox = false;
        ChatM.Instance.ClearAllChat();
    }

    //更新检测
    private void CheakUpData(Action BackCall)
    {
        if (Updater.Mode != UpdateMode.Remote)
        {
            BackCall?.Invoke();
            return;
        }

        HttpRequestHelper.GetVersion(()=>{
            UITool.Open<UIMessageBoxOne>(null, new UIMessageBoxOne.CallBack
            {
                showStr = "有新的资源需要更新,点击确定即可进行下载更新",
                endFun = delegate
                {

                    UnityEngine.Object.Destroy(PoolHelper.Instance.gameObject);
                    UnityEngine.Object.Destroy(TCPClientMgr.Instance.gameObject);
                    UnityEngine.Object.Destroy(AudioManager.Instance.gameObject);
                    UnityEngine.Object.Destroy(GameHelper.Instance.gameObject);

                    ResouseHelper.Instance.Clear();
                    ExcelDataManager.Instance.Clear();

                    UnityEngine.Object.Destroy(ClassBindMgr.Instance.gameObject);

                    //YooAssets.Destroy();
                    SceneManager.LoadScene("Init");
                }
            });
        }, BackCall);
    }



}

