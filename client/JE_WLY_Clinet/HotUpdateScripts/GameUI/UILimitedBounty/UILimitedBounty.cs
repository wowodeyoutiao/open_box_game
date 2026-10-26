using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Helper;
using HotUpdateScripts.ProtoM;
using JEngine.UI;
using JEngine.Event;
using System;
using SuperScrollView;
using System.Reflection;

public class UILimitedBounty : UIBase<UIModelLimitedBounty, UIViewLimitedBounty>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UILimitedBounty>();
        });

        GetView().btn_Help.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIComRule>(null, 13);
        });

        GetView().infiniteScroll.InitListView(0, OnFillItem);
    }

    private LoopListViewItem2 OnFillItem(LoopListView2 listView, int idx)
    {
        if (idx < 0 || idx >= _ShowActivityList.Count)
        {
            return null;
        }
        int shopId = _ShowActivityList[idx].PreLimit;
        var cfgStore = CFG_Store.GetById(shopId);
        LoopListViewItem2 item = listView.NewListViewItem("Item");
        var go = item.gameObject;
        JUIExtensions.txt(go.transform.Find("txt_base")).text = GameUtil.Instance.GetItemName(cfgStore.Limite.needmoney);


        JUIExtensions.txt(go.transform.Find("Item_1/txt_num")).text = GameUtil.Instance.FormatNumWithUnit(cfgStore.Limite.Num);
        GameUtil.Instance.SetItemIcon(cfgStore.Limite.needmoney, JUIExtensions.img(go.transform.Find("Item_1/icon")), JUIExtensions.img(go.transform.Find("Item_1")));

        JUIExtensions.btn(go.transform.Find("Item_1")).onClick.RemoveAllListeners();
        JUIExtensions.btn(go.transform.Find("Item_1")).onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIItemShow>(null, cfgStore.Limite.needmoney);
        });

        JUIExtensions.txt(go.transform.Find("Item_2/txt_num")).text = GameUtil.Instance.FormatNumWithUnit(cfgStore.Solditems[0].Num);
        GameUtil.Instance.SetItemIcon(cfgStore.Solditems[0].itemID, JUIExtensions.img(go.transform.Find("Item_2/icon")), JUIExtensions.img(go.transform.Find("Item_2")));

        JUIExtensions.btn(go.transform.Find("Item_2")).onClick.RemoveAllListeners();
        JUIExtensions.btn(go.transform.Find("Item_2")).onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIItemShow>(null, cfgStore.Solditems[0].itemID);
        });

        JUIExtensions.txt(go.transform.Find("txt_red")).text = StoreM.Instance.GetInventoryNumAddByID(shopId) == -1 ? "红包余量:无限制" : "红包余量:" + StoreM.Instance.GetInventoryNumAddByID(shopId);

        JUIExtensions.txt(go.transform.Find("txt_pro")).text = GetLimetDes(cfgStore.Limite.limittype, cfgStore.Limite.limitnum, StoreM.Instance.GetBuyNumByID(shopId));

        JUIExtensions.txt(go.transform.Find("txt_tip")).text = "<color=#E1E690>VIP" + cfgStore.Prelimit + "</color>可兑换";


        go.transform.Find("btn/dot").gameObject.SetActive(GameUtil.Instance.ComCoinToCheck(shopId, null, false) &&
            StoreM.Instance.CheckInventory(shopId) &&
            RoleM.Instance.Vip >= cfgStore.Prelimit);

        JUIExtensions.btn(go.transform.Find("btn")).onClick.RemoveAllListeners();
        JUIExtensions.btn(go.transform.Find("btn")).onClick.AddListener(() => {
            OnBtnClick(shopId);
        });

        return item;
    }

    private void OnBtnClick(int shopId)
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        var cfgStore = CFG_Store.GetById(shopId);
        if (string.IsNullOrEmpty(StoreM.Instance.GetExtractAccount()))
        {
            //System.Action yesFun = () => {
            //    HttpC.OnExtractAccountUpdate(2, "test");
            //};
            UITool.Open<UIPayBind>();
            return;
        }
        if (RoleM.Instance.Vip < cfgStore.Prelimit)
        {
            System.Action yesFun = () => {
                UITool.Open<UIShop>(null, CFG_Store.StoreType.RMB);
            };
            UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
            {
                ShowStr = "当前VIP等级不足，是否前往充值？",
                YesFun = yesFun,
            });
            return;
        }

        if (!GameUtil.Instance.ComCoinToCheck(shopId, () => { UITool.Close<UILimitedBounty>(); }))
            return;

        if (!StoreM.Instance.CheckInventory(shopId))
        {
            UITool.ShowTips("库存已空");
            return;
        }

        Action CallBack = delegate
        {
            GameUtil.Instance.GameToBuy(shopId, () => {
                HttpC.OnGetShopInfo(0, (_r) => {
                    if (!isNullGo)
                    { 
                        OnRefreshUI();
                        UITool.Open<UIMessageBoxOne>(null, new UIMessageBoxOne.CallBack
                        {
                            cType = 0,
                            showStr = "兑换成功，请前往支付宝界面查看，若领取失败，道具将在24小时后返还"
                        });
                    }
                });
                PlayerPrefsExtend.SetInt(StoreM.Instance.GetExtractAccount(), 1);
            });
        };

        if (PlayerPrefsExtend.GetInt(StoreM.Instance.GetExtractAccount()) == 0)
            UITool.Open<UILimitedBountyTip>(null, CallBack);
        else
            CallBack();
    }

    private string GetLimetDes(int limittype, int AllCount, long pro)
    {
        string str = "";
        if (limittype == 0)
        {
            str = $"账号终生:{ AllCount - pro}/{AllCount}";
        }
        else if (limittype == 1)
        {
            str = $"账号每日:{ AllCount - pro}/{AllCount}";
        }
        else if (limittype == 2)
        {
            str = $"每周:{ AllCount - pro}/{AllCount}";
        }
        else if (limittype == 3)
        {
            str = $"每月:{ AllCount - pro}/{AllCount}";
        }
        return str;
    }

    private List<CFGItem_Activity> _ShowActivityList = CFG_Activity.GetActiveListByType(CFG_Activity.Type.LimitBounty);
    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);

        GetView().txt_Time.text = GameUtil.Instance.GetTimeSpan(ActivityM.Instance.GetAcitvityEndTime(ActivityType.LimitBounty)).Days == 0 ?
            "距离结束不足1天"
            :
            "距离结束还剩" + GameUtil.Instance.GetTimeSpan(ActivityM.Instance.GetAcitvityEndTime(ActivityType.LimitBounty)).Days + "天";
        OnRefreshUI();

        // RoleM.Instance.SetGuide(new UIGuide.Guide { 
        //     ID = 7,
        //     GuidePos = GetView().root_content.GetChild(0).Find("btn").transform.position,
        //     callBack = delegate { OnBtnClick(_ShowActivityList[0].PreLimit); }
        // });
    }
    private void OnRefreshUI()
    {
        GetView().infiniteScroll.SetListItemCount(_ShowActivityList.Count);
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_TakeSource(GameEvent.Sync_TakeSource _e)
    {
        if (isNullGo)
            return;

        OnRefreshUI();
    }

    public override void OnDestroy()
    {
        JEvent.defaultEvent.Unregister(this);
    }
}