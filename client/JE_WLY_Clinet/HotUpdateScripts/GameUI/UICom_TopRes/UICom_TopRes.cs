using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Event;
using JEngine.Helper;
using HotUpdateScripts.ProtoM;
using JEngine.UI;
using HotUpdateScripts;
using pb;
using static UIGame3D;
using System;
using JEngine.Core;

public class UICom_TopRes : UIBase<UIModelCom_TopRes, UIViewCom_TopRes>
{
    public override UILayer GetLayer()
    {
        return UILayer.NoLayer;
    }

    public override void OnInit()
    {
        JEvent.defaultEvent.Register(this);
        GetView().Btn_Set.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnSetUIBtnCkick();
        });

        GetView().obj_SetUI.GetComponent<Button>().onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnSetUIBtnCkick();
        });

        for (int i = 0; i < 5; i++)
        {
            GameObject obj = GetView().root_Set.GetChild(i).gameObject;
            JUIExtensions.btn(obj.transform).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnSetBtnCkicl(obj);
            });
        }

        GetView().btn_coupon.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnShopClick(CFG_Store.StoreType.RMB);
        });

        GetView().btn_gold.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnShopClick(CFG_Store.StoreType.Coupon);
        });
        
        GetView().btn_Vip.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIVIP>();
        });

        for (int i = 0; i < GetView().TeamItems.Length; i++)
        {
            int soltIdx = i + 1;
            GetView().TeamItems[i]._btn.onClick.AddListener(() => {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                if (soltIdx > 0 && CFG_XMLConfig.CheckPartnerSoltIsOpenServer(soltIdx))
                {
                    UITool.ShowTips($"{CFG_XMLConfig.RemainingDaysPartnerSlotUnlockOpen(soltIdx)}天后开启");
                    return;
                }
                if (CFG_XMLConfig.GetOpenSoltLv(soltIdx) > RoleM.Instance.Level)
                {
                    UITool.ShowTips("角色等级不足");
                    return;
                }
                OnTeamBtnClick(soltIdx);
            });
        }
        GetView().btn_MyHead.onClick.AddListener(() => {
            OnTeamBtnClick(0);
        });

        OnResRefresh(true);
        OnBindDot();
        SetPartnerInfo();
        SetPartnerLock();
        OnShopRequest(CFG_Store.StoreType.VIPGift);

    }

    private void OnTeamBtnClick(int idx)
    {
        if (UITool.Check<UIHall>())
        {
            UITool.Get<UIHall>().OnTeamChoie(idx);
            OnHeadOnShow(idx);
            UITool.Open<UIRole>(null, idx);
        }
    }

    public void OnSetLayer(int _canvasOrder)
    {
        GameUtil.Instance.SetEffectLayer(GetView().obj_MyOn.transform, _canvasOrder + 1);
        for (int i = 0; i < GetView().TeamItems.Length; i++)
        {
            GameUtil.Instance.SetEffectLayer(GetView().TeamItems[i]._obj_on.transform, _canvasOrder + 1);
        }
        GameUtil.Instance.SetEffectLayer(GetView().obj_SetUI.transform, _canvasOrder + 1);
    }

    public void OnHeadOnShow(int TeamIdx)
    {
        GetView().obj_MyOn.SetActive(TeamIdx == 0);
        for (int i = 0; i < GetView().TeamItems.Length; i++)
        {
            GetView().TeamItems[i]._obj_on.SetActive((i + 1) == TeamIdx);
        }
    }

    private void OnBindDot()
    {
        RedPointManager.Instance.RPS.Register(RedPointConst.Set.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().Btn_Set.transform.Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Set_Email.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().root_Set.transform.Find("Btn_Mail/dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.VIP_Enter.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().btn_Vip.transform.Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

    }

    public override void OnEnter(params object[] args)
    {
       
    }

    #region 设置面板
    private void OnSetUIBtnCkick()
    {
        bool isShow = !GetView().obj_SetUI.activeSelf;
        GetView().obj_SetUI.SetActive(isShow);

        var soical = GetView().root_Set.Find("Btn_Social").GetComponent<RectTransform>();
        Vector2 vector2 = GetUITopLeftCorner(soical);
        SDKCtr.Instance.OpenWXGamePag(vector2, soical, isShow);
    }

    private void OnSetBtnCkicl(GameObject go)
    {

        string goName = go.gameObject.name;
        switch (goName)
        {
            case "Btn_Setting":
                UITool.Open<UISetting>();
                break;
            case "Btn_Mail":
                OnEamilBtnClick();
                break;
            case "Btn_ActivationCode":
                UITool.ShowTips("功能暂未开放");
                break;
            case "Btn_Social":
                //UITool.ShowTips("功能暂未开放");
                break;
            case "Btn_Help":
                SDKCtr.Instance.OnWXCustomerService("customer service", null, null);
                //UITool.ShowTips("功能暂未开放");
                break;
            default:
                break;
        }
        OnSetUIBtnCkick();
    }

    public Vector2 GetUITopLeftCorner(RectTransform uiElement)
    {
        Canvas canvas = GameObject.Find("UICanvas").GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("UI element is not under a canvas.");
            return Vector2.zero;
        }
        Vector3[] corners = new Vector3[4];
        uiElement.GetWorldCorners(corners);
        // 将 UI 元素的四个角的世界坐标转换为屏幕坐标
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, corners[i]);
        }
        // 获取左上角横坐标和纵坐标
        float leftX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float topY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        return new Vector2(leftX, topY);

    }
    private void OnEamilBtnClick()
    {
        MailM.Instance.SetMailNew(false);
        HttpC.OnGetMailInfo((_response) =>
        {
            if (_response.code == 0)
            {
                UITool.Open<UIMail>();
            }
            else
            {
                UITool.ShowTips($"err code {_response.code}");
            }
        });
    }

    #endregion

    private void OnResRefresh(bool isInit = false,bool isRes = true,bool isBaseChange = true,bool isPower = true)
    {
        if (isRes)
        {
            GetView().txt_gold.text = GameUtil.Instance.FormatNumWithUnit(RoleM.Instance.Coin);
            GetView().txt_coupon.text = GameUtil.Instance.FormatNumWithUnit(RoleM.Instance.Coupon);
        }

        if (isBaseChange)
        {
            GetView().txt_Lv.text = RoleM.Instance.Level.ToString();
            GetView().txt_VIP.text = "VIP" + RoleM.Instance.Vip;
            //CheckPartnerSlotUnlockGuide();
        }
        
        if (isPower)
            GetView().txt_Power.text = GameUtil.Instance.FormatPowerWithUnit(RoleM.Instance.TotalCombat);
        
        if (isInit)
        {
            GetView().txt_MyName.text = RoleM.Instance.Nickname;
            GameUtil.Instance.SetJobIcon((int)RoleM.Instance.Job, GetView().img_MyJob);
            ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.RoleHeadAtlas, "img_"+ RoleM.Instance.Job + RoleM.Instance.Sex, GetView().img_MyHead);
        }
    }

    //伙伴槽位解锁
    private void SetPartnerLock()
    {
        for (int i = 0; i < GetView().TeamItems.Length; i++)
        {
            int soltIdx = i + 1;
            bool isLock = !CheckPartnerSoltIsUnlock(soltIdx);
            GetView().TeamItems[i].obj_Lock.SetActive(isLock);
            if (isLock)
            {
                if (CFG_XMLConfig.CheckPartnerSoltIsOpenServer(soltIdx, false))
                    GetView().TeamItems[i].txt_Lock.text = $"{CFG_XMLConfig.RemainingDaysPartnerSlotUnlockOpen(soltIdx)}天后解锁";
                else
                    GetView().TeamItems[i].txt_Lock.text = $"{CFG_XMLConfig.GetOpenSoltLv(soltIdx)}级解锁";
            }
        }
    }

    public bool CheckPartnerSoltIsUnlock(int soltIdx)
    {
        bool isLock = CFG_XMLConfig.CheckPartnerSoltIsOpenServer(soltIdx, false);
        if (!isLock)
            isLock = CFG_XMLConfig.GetOpenSoltLv(soltIdx) > RoleM.Instance.Level;

        return !isLock;
    }

    //伙伴信息展示
    private void SetPartnerInfo(bool isBaseData = false)
    {
        for (int i = 0; i < GetView().TeamItems.Length; i++)
        {
            int soltIdx = i + 1;
            var solt = PartnerM.Instance.GetOnePartnerSlotInfo(soltIdx);
            int quality = 0;
            if (solt.PartnerUid != 0)
            {
                var partner = PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid);
                GetView().TeamItems[i].txt_Lv.text = partner.Level.ToString();

                GetView().TeamItems[i].root_Star.gameObject.SetActive(true);
                GetView().TeamItems[i].root_Star.text = partner.Star.ToString();

                if (isBaseData)
                    continue;

                GetView().TeamItems[i].img_Head.gameObject.SetActive(true);
                GameUtil.Instance.SetItemIcon(CFG_Partner.GetById(partner.CfgId).ItemID, GetView().TeamItems[i].img_Head);
                GetView().TeamItems[i].obj_Lv.gameObject.SetActive(true);
                GetView().TeamItems[i].img_Job.gameObject.SetActive(true);
                GameUtil.Instance.SetJobIcon(CFG_Partner.GetById(partner.CfgId).job % 10, GetView().TeamItems[i].img_Job);
                quality = CFG_Partner.GetById(partner.CfgId).Quality;
            }
            else
            {
                GetView().TeamItems[i].img_Head.gameObject.SetActive(false);
                GetView().TeamItems[i].obj_Lv.gameObject.SetActive(false);
                GetView().TeamItems[i].img_Job.gameObject.SetActive(false);
                GetView().TeamItems[i].root_Star.gameObject.SetActive(false);
            }
            GameUtil.Instance.SetPartnetQualityFrame(quality, GetView().TeamItems[i].img_quality);
        }
    }


    private void OnShopClick(CFG_Store.StoreType storeType)
    {
        //if (RoleM.Instance.Guidance)
        //{
        //    UITool.ShowTips("引导中不可操作");
        //    return;
        //}

        OnShopRequest(storeType);
    }

    private void OnShopRequest(CFG_Store.StoreType storeType)
    {
        HttpC.OnGetShopInfo(0, (_response) =>
        {
            if (_response.code == 0 && !isNullGo)
            {
                switch (storeType)
                {
                    case CFG_Store.StoreType.No:
                        break;
                    case CFG_Store.StoreType.RMB:
                    case CFG_Store.StoreType.Coupon:
                        UITool.Open<UIShop>(null, storeType);
                        break;
                    case CFG_Store.StoreType.Coin:
                        break;
                    case CFG_Store.StoreType.Diamanta:
                        break;
                    case CFG_Store.StoreType.SiLian:
                        break;
                    case CFG_Store.StoreType.VIPGift:
                        //RedPointManager.Instance.RPS.Dispatch(RedPointConst.VIP_Buy.ToString(), TaskM.Instance.VIPCanBuyGiftCount());
                        break;
                    default:
                        break;
                }
            }
            else
            {
                UITool.ShowTips($"失败 code {_response.code}");
            }
        });
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserBase(GameEvent.Sync_UserBase _e)
    {
        if (isNullGo)
            return;
        OnResRefresh(true, false, true, false);
        SetPartnerLock();
    }
    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserStatsPush(GameEvent.Sync_UserStatsPush _e)
    {
        if (isNullGo)
            return;
        OnResRefresh(false, false, false, true);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnEvent_Source_Refresh(Event_Source_Refresh _e)
    {
        if (isNullGo)
            return;
        OnResRefresh(false, true, false, false);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_TakeSource(GameEvent.Sync_TakeSource _e)
    {
        if (isNullGo)
            return;
        OnResRefresh(false, true, false, false);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_PartnerSlot(GameEvent.Refresh_PartnerSlot _e)
    {
        if (isNullGo) return;

        SetPartnerInfo();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_PartnerList(GameEvent.Refresh_PartnerList _e)
    {
        if (isNullGo) return;

        SetPartnerInfo(true);
    }


    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_CommonResponse(GameEvent.CommonResponse _e)
    {
        if (isNullGo) return;

        if (_e.code == 0 && _e.cid == CmdId.C2S_UpgradeLevel)
        {
            SetPartnerInfo(true);
        }

        // 伙伴重铸
        if (_e.code == 0 && _e.cid == CmdId.C2S_PartnerReforge)
        {
            SetPartnerInfo();
        }

        // 伙伴分解
        if (_e.code == 0 && _e.cid == CmdId.C2S_PartnerDisassemble)
        {
            SetPartnerInfo();
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_LvUp(GameEvent.Refresh_LvUp _e)
    {
        if (isNullGo) return;
        int effIdx = -2;
        if (_e.uid == RoleM.Instance.Uid)
            effIdx = -1;
        else
            effIdx = PartnerM.Instance.GetSoltIdxByPartnerUid(_e.uid) - 1 ;

        if (effIdx < -1) return;

        _ = GameHelper.Instance.SetAsyncObj("UI_LvUP", GameConst.GetEffPath("UI_LvUP"), (go) => {
            go.transform.SetParent(effIdx == -1 ? GetView().img_MyHead.transform : GetView().TeamItems[effIdx]._btn.transform);
            go.gameObject.SetActive(true);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            GameHelper.Instance.SetDeayTimeDo(()=>{
                if (isNullGo) return;
                PoolHelper.Instance.RecycObj(go);
            },1.36f);
        }, true);

    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_CheckOpenTime(GameEvent.Refresh_ZeroTime _e)
    {
        SetPartnerLock();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_Ping(GameEvent.Refresh_Ping _e)
    {
        if (isNullGo) return;
        //GetView().txt_ping.text = "PING:"+ _e.ping + "ms";
    }

    [Subscriber(ThreadMode.Main)]
    private void OnFight_Die(GameEvent.Fight_Die _e)
    {
        OnTeamDeBufferOrDie(_e.id);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnFight_Revive(GameEvent.Fight_Revive _e)
    {
        OnTeamDeBufferOrDie(_e.id);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnFight_Revive(GameEvent.Refresh_Buffer _e)
    {
        OnTeamDeBufferOrDie(_e.Uid);
    }

    private string BufferAtlas = "Common/Atlas/Fight";

    private Dictionary<int, string> BufferIconNameDic = new() {
        [(int)CFG_BuffEffect.BufferID.DuSha] = "word_zhongdu",
        [(int)CFG_BuffEffect.BufferID.DuGu] = "word_zhongdu",
        [(int)CFG_BuffEffect.BufferID.BingDong] = "word_bingdong",
        [(int)CFG_BuffEffect.BufferID.XuanYun] = "word_yunxuan",
    };

    private void OnTeamDeBufferOrDie(long uid)
    {
        if (isNullGo || GameRoomM.Instance._GameState == GameRoomM.GameState.No) return;
        int idx = GetIdxByUid(uid);
        if (idx < 0) return;
        GameObject bufferObj = idx == 0 ? GetView().obj_MyBuferr : GetView().TeamItems[idx - 1].obj_Buffer;
        Image img_buffer = idx == 0 ? GetView().img_MyBuferr : GetView().TeamItems[idx - 1].img_Buffer;
        if (GameRoomM.Instance.DeathHashSet.Contains(uid))
        {
            bufferObj.SetActive(true);
            ResouseHelper.Instance.LoadSpriteByAtlas(BufferAtlas, "word_Battle_sw", img_buffer);
            return;
        }

        var buffer = GameRoomM.Instance.GetBuffInfosByUid(uid);
        string iconName = "";
        if (buffer != null)
        {
            for (int i = buffer.Count - 1; i >= 0; i--)
            {
                if (BufferIconNameDic.ContainsKey(buffer[i].BuffId))
                {
                    iconName = BufferIconNameDic[buffer[i].BuffId];
                    break;
                }
            }
        }
        bufferObj.SetActive(!string.IsNullOrEmpty(iconName));
        if (!string.IsNullOrEmpty(iconName))
        {
            ResouseHelper.Instance.LoadSpriteByAtlas(BufferAtlas, iconName, img_buffer);
        }
    }

    private int GetIdxByUid(long uid)
    {
        if (RoleM.Instance.Uid == uid)
            return 0;
        else
            return PartnerM.Instance.GetSoltIdxByPartnerUid(uid);
    }

    public override void OnExit()
    {
        SDKCtr.Instance.OpenWXGamePag(Vector2.zero, null, false);
    }

    public override void OnDestroy()
    {
        OnExit();
        JEvent.defaultEvent.Unregister(this);
    }
}