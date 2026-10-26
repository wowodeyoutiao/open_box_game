using HotUpdateScripts;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using JEngine.UI;
using MetaFramework.UI;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UIGiftBag : UIBase<UIModelGiftBag, UIViewGiftBag>
{
    private UIViewGiftBag uIViewGiftBag;

    private UIModelGiftBag.ActiveTitle curActiveTitle;

    private List<CFGItem_Activity> showActivityList;
    private Dictionary<int, string> _GiftLimetDesDic = new Dictionary<int, string>
    {
        [1] = "每日限购",
        [2] = "每周限购",
        [3] = "每月限购",
    };

    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        uIViewGiftBag = GetView();

        uIViewGiftBag.btn_Close.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIGiftBag>();
        });

        // 按钮
        for (int i = 0; i < GetModel().ActiveTitlesDic.Count; i++)
        {
            var titleData = GetModel().ActiveTitlesDic.ElementAt(i).Value;

            uIViewGiftBag.tabBtns[i].transform.Find("Normal/Label").GetComponent<Text>().text = titleData.activeName;
            uIViewGiftBag.tabBtns[i].transform.Find("Select/Label").GetComponent<Text>().text = titleData.activeName;

            uIViewGiftBag.tabBtns[i].gameObject.name = titleData.activeType.ToString();

            uIViewGiftBag.tabBtns[i].gameObject.SetActive(titleData.ActiveOpen);
            uIViewGiftBag.tabBtns[i].onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnClickTabButton(titleData);
            });
        }

        // 
        uIViewGiftBag.infiniteScroll_Gifts.InitListView(0, OnActivityFill);
        uIViewGiftBag.infiniteScroll_GrowthFund.InitListView(0, OnGrowthFundFill);

        // 成长基金购买
        uIViewGiftBag.growthFundBuyBtn.gameObject.SetActive(false);
        uIViewGiftBag.growthFundBuyBtn.onClick.RemoveAllListeners();
        uIViewGiftBag.growthFundBuyBtn.onClick.AddListener(() =>
        {
            GameUtil.Instance.GameToBuy(storeId, () =>
            {
                if (activityAwardInfo != null)
                    activityAwardInfo.Clear();
                ShowGrowthFund();
            });
        });
        var storeInfo = CFG_Store.GetById(storeId);
        uIViewGiftBag.growthFundBuyBtn.transform.Find("Text").GetComponent<Text>().text = $"￥{GameUtil.Instance.FormatNumWithUnit(storeInfo.Limite.Num)}";
    }

    private void BindRed()
    {
        for (int i = 0; i < GetModel().ActiveTitlesDic.Count; i++)
        {
            var titleData = GetModel().ActiveTitlesDic.ElementAt(i).Value;
            if (titleData.ActiveOpen)
            {
                var index = i;
                RedPointManager.Instance.RPS.Register(titleData.RedKey, (node) =>
                {
                    uIViewGiftBag.tabBtns[index].transform.Find("Dot").gameObject.SetActive(node.pointCount > 0);
                });
            }
        }
    }

    private void ClearBindRed()
    {
        foreach (var item in GetModel().ActiveTitlesDic.Values)
        {
            if (!item.ActiveOpen) continue;
            RedPointManager.Instance.RPS.Register(item.RedKey, null);
        }
    }

    private void OnClickTabButton(UIModelGiftBag.ActiveTitle idex)
    {
        for (int i = 0; i < uIViewGiftBag.tabBtns.Length; i++)
        {
            var sel = uIViewGiftBag.tabBtns[i].gameObject.name == idex.activeType.ToString();
            uIViewGiftBag.tabBtns[i].transform.Find("Normal").gameObject.SetActive(!sel);
            uIViewGiftBag.tabBtns[i].transform.Find("Select").gameObject.SetActive(sel);
        }

        if(!string.IsNullOrEmpty(idex.iconName))
            ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.GiftBag, idex.iconName, uIViewGiftBag.logoImg);

        uIViewGiftBag.growthFundNodeGo.SetActive(idex.activeType == ActivityType.GrowthFund); // 成长基金
        uIViewGiftBag.monthNodeGo.SetActive(idex.activeType == ActivityType.Month); // 月卡
        uIViewGiftBag.normalNodeGo.SetActive(idex.activeType != ActivityType.GrowthFund && idex.activeType != ActivityType.Month);

        curActiveTitle = idex;

        if (idex.cfgId > 0)
            showActivityList = CFG_Activity.GetActiveListByType((CFG_Activity.Type)idex.cfgId);

        switch (idex.activeType)
        {
            case ActivityType.DailyGift: // 每日礼包
            case ActivityType.GiftPay: // 每日特价（特惠礼包）
            case ActivityType.PartnerGift:
            case ActivityType.PetGift:
            case ActivityType.Gemstone:
                OnGetShopInfo(idex.shopType);
                break;

            case ActivityType.GrowthFund:
                {
                    if (activityAwardInfo != null)
                        activityAwardInfo.Clear();
                    ShowGrowthFund();
                }
                break;

            case ActivityType.Month:
                ShowMonth();
                break;

            default:
                break;
        }
    }

    #region 每日礼包、每日特价、伙伴礼包、宠物礼包、元素礼包
    private void OnGetShopInfo(int shopType)
    {
        UITool.Open<UILoading>(null, new UILoading.Loading { _time = 0 });
        HttpC.OnGetShopInfo(shopType, (_response) =>
        {
            UITool.Close<UILoading>();
            if (_response.code == 0 && !isNullGo)
            {
                uIViewGiftBag.infiniteScroll_Gifts.SetListItemCount(showActivityList.Count);
                uIViewGiftBag.infiniteScroll_Gifts.RefreshAllShownItem();
            }
        });
    }

    private LoopListViewItem2 OnActivityFill(LoopListView2 loopListView, int idx)
    {
        if (idx < 0 || idx >= showActivityList.Count)
            return null;

        LoopListViewItem2 item = loopListView.NewListViewItem("Item");
        GameObject go = item.gameObject;
        int shopId = showActivityList[idx].PreLimit;
        var storeInfo = CFG_Store.GetById(shopId);

        JUIExtensions.txt(go.transform.Find("txt_Name")).text = showActivityList[idx].Name;

        string limitStr = string.Empty;
        if (storeInfo.Limite.limittype != 999)
        {
            if (storeInfo.Limite.times > 0)
            {
                var curTimes = StoreM.Instance.GetBuyAllNum(shopId);
                if (curTimes < storeInfo.Limite.times)
                    limitStr = $"<color=#a9865d>（再购买<color=#FF0000>{storeInfo.Limite.times - curTimes}</color>次后免费）</color>";
            }
            limitStr = $"{limitStr}{_GiftLimetDesDic[storeInfo.Limite.limittype]}:{StoreM.Instance.GetBuyNumByID(shopId)}/{storeInfo.Limite.limitnum}";
        }
        JUIExtensions.txt(go.transform.Find("txt_Limet")).text = limitStr;

        var obj = go.transform.Find("Awards");
        for (int i = 0; i < obj.childCount; i++)
        {
            var obj_Item = obj.GetChild(i);
            obj_Item.gameObject.SetActive(i < storeInfo.Solditems.Length);

            if (i < storeInfo.Solditems.Length)
            {
                int cfgID = storeInfo.Solditems[i].itemID;
                GameUtil.Instance.SetItemIcon(cfgID, JUIExtensions.img(obj_Item.transform.Find("icon")), JUIExtensions.img(obj_Item.transform));
                JUIExtensions.txt(obj_Item.transform.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(storeInfo.Solditems[i].Num);

                JUIExtensions.btn(obj_Item.transform).onClick.RemoveAllListeners();
                JUIExtensions.btn(obj_Item.transform).onClick.AddListener(() =>
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, cfgID);
                });
            }
        }

        var buyBtnTrans = go.transform.Find("Btn_Buy");
        var freeBtnTrans = go.transform.Find("Btn_Free");
        var videoBtnTrans = go.transform.Find("Btn_Video");

        bool soldOut = storeInfo.Limite.limittype != 999 && StoreM.Instance.GetBuyNumByID(shopId) >= storeInfo.Limite.limitnum;
        go.transform.Find("txt_tip").gameObject.SetActive(soldOut);
        buyBtnTrans.gameObject.SetActive(false);
        freeBtnTrans.gameObject.SetActive(false);
        videoBtnTrans.gameObject.SetActive(false);

        if(!soldOut)
        {
            // limittype 为 999
            if(storeInfo.Limite.limittype == 999)
            {
                ShowBuyButton(buyBtnTrans, storeInfo.Limite.needmoney, storeInfo.Limite.Num);
                AddButtonListeners(buyBtnTrans, storeInfo);
            }
            else
            {
                // 使用广告
                var useAd = storeInfo.Limite.needmoney == 101;
                // 判断总次数，购买总次数为X次后免费
                var isFree = (storeInfo.Limite.Num == 0) || (storeInfo.Limite.times != 0 && storeInfo.Limite.times <= StoreM.Instance.GetBuyAllNum(shopId));
                if(isFree)
                {
                    freeBtnTrans.gameObject.SetActive(true);
                    AddButtonListeners(freeBtnTrans, storeInfo, isFree:true);
                }
                else if(useAd)
                {
                    Debug.Log("广告未接入");
                    videoBtnTrans.gameObject.SetActive(true);
                    AddButtonListeners(videoBtnTrans, storeInfo, isVideo:true);
                }
                else
                {
                    ShowBuyButton(buyBtnTrans, storeInfo.Limite.needmoney, storeInfo.Limite.Num);
                    AddButtonListeners(buyBtnTrans, storeInfo);
                }
            }
        }

        return item;
    }

    private void ShowBuyButton(Transform trans, int needmoney, int num)
    {
        trans.gameObject.SetActive(true);
        JUIExtensions.txt(trans.Find("txt")).text = GameUtil.Instance.FormatNumWithUnit(num);
        GameUtil.Instance.SetShopItemIcon(needmoney, JUIExtensions.img(trans.Find("icon")));
    }

    private void AddButtonListeners(Transform trans, CFGItem_Store storeInfo, bool isVideo = false, bool isFree = false)
    {
        JUIExtensions.btn(trans).onClick.RemoveAllListeners();
        JUIExtensions.btn(trans).onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (!isVideo)
                OnGiftItemClick(storeInfo, isFree);
            else
                OnVideoButtonClick(storeInfo.ID);
        });
    }

    private void OnVideoButtonClick(int shopId)
    {
        GameUtil.Instance.GameToBuy(shopId, () =>
        {
            ShowAwards(shopId);
            OnGetShopInfo(curActiveTitle.shopType);
            RedPointManager.Instance.RPS.Dispatch(curActiveTitle.RedKey, GiftBagM.Instance.GetGiftRedNum(curActiveTitle.cfgId));
        });
    }

    private void OnGiftItemClick(CFGItem_Store storeInfo, bool isFree = false)
    {
        if(isFree || storeInfo.Limite.needmoney == 100)
        {
            GameUtil.Instance.GameToBuy(storeInfo.ID, () =>
            {
                ShowAwards(storeInfo.ID);
                OnGetShopInfo(curActiveTitle.shopType);
                RedPointManager.Instance.RPS.Dispatch(curActiveTitle.RedKey, GiftBagM.Instance.GetGiftRedNum(curActiveTitle.cfgId));
            });

            return;
        }

        if (GameUtil.Instance.ComCoinToCheck(storeInfo.Limite.needmoney, storeInfo.Limite.Num, null))
        {
            UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
            {
                ShowStr = $"是否消耗<color=#2C7E40>{GameUtil.Instance.FormatNumWithUnit(storeInfo.Limite.Num)}{CFG_Item.GetById(storeInfo.Limite.needmoney).Name}</color>购买该礼包？",
                YesFun = () =>
                {
                    GameUtil.Instance.GameToBuy(storeInfo.ID, () =>
                    {
                        ShowAwards(storeInfo.ID);
                        OnGetShopInfo(curActiveTitle.shopType);
                        RedPointManager.Instance.RPS.Dispatch(curActiveTitle.RedKey, GiftBagM.Instance.GetGiftRedNum(curActiveTitle.cfgId));
                    });
                },
            });
        }
    }

    private void ShowAwards(int shopId)
    {
        var storeInfo = CFG_Store.GetById(shopId);
        var info = new ActivityAwardInfo();
        for(int i = 0; i <storeInfo.Solditems.Length; i++)
        {
            info.AddAwardItem(storeInfo.Solditems[i].itemID, storeInfo.Solditems[i].Num);
        }
        info.des = string.Empty;
        UITool.Open<UIActivityAwards>(null, info);
    }
    #endregion

    #region 成长基金

    int storeId = 22001;
    private bool isBuy = false;
    private HTTP_GrowthFundInfo[] growthFundInfos;
    private Dictionary<string, bool> addedAwardItem = new Dictionary<string, bool>();
    private ActivityAwardInfo activityAwardInfo;

    private void ShowGrowthFund()
    {
        uIViewGiftBag.curRoleLevelText.text = "当前角色等级:" + RoleM.Instance.Level;
        UITool.Open<UILoading>(null, new UILoading.Loading { _time = 0 });
        HttpC.OnGetGrowthFundInfo((_response) =>
        {
            UITool.Close<UILoading>();
            if (_response.code == 0 && !isNullGo)
            {
                if (activityAwardInfo == null)
                    activityAwardInfo = new ActivityAwardInfo(5);

                addedAwardItem.Clear();
                uIViewGiftBag.titleLockGo.SetActive(_response.buy != 2);

                isBuy = _response.buy == 2;
                growthFundInfos = _response.data;
                if (growthFundInfos != null && growthFundInfos.Length > 0)
                {
                    Array.Sort(growthFundInfos, (a, b) => a.day.CompareTo(b.day));
                }
                uIViewGiftBag.infiniteScroll_GrowthFund.SetListItemCount(showActivityList.Count);
            }
            else
                uIViewGiftBag.infiniteScroll_GrowthFund.SetListItemCount(0);

            uIViewGiftBag.growthFundBuyBtn.gameObject.SetActive(!isBuy);
            uIViewGiftBag.infiniteScroll_GrowthFund.RefreshAllShownItem();
            GetView().infiniteScroll_GrowthFund.MovePanelToItemIndex(OnGrowthFundMovePosition(), 0);
        });
    }

    private LoopListViewItem2 OnGrowthFundFill(LoopListView2 loopListView, int idx)
    {
        if (idx < 0 || idx >= showActivityList.Count)
            return null;

        LoopListViewItem2 item = loopListView.NewListViewItem("Item");
        GameObject go = item.gameObject;
        var itemTrans = go.transform;

        var roleLevel = RoleM.Instance.Level;

        var activityData = showActivityList[idx];
        var isAchieve = activityData.PreLimit <= roleLevel;
        itemTrans.Find("Achieve").gameObject.SetActive(isAchieve);
        JUIExtensions.txt(itemTrans.Find("LevelImage/Text")).text = "Lv." + activityData.PreLimit;

        // 上下进度条
        itemTrans.Find("SliderUpImg").gameObject.SetActive(idx != 0);
        itemTrans.Find("SliderDownImg").gameObject.SetActive(idx != showActivityList.Count - 1);
        var sliderUpGo = itemTrans.Find("SliderUpImg/Image").gameObject;
        var sliderDownGo = itemTrans.Find("SliderDownImg/Image").gameObject;

        sliderUpGo.SetActive(idx != 0 && isAchieve);
        sliderDownGo.SetActive(isAchieve);

        var isLeftGeted = false;
        var isRightGeted = false;
        if (growthFundInfos != null && growthFundInfos.Length > idx)
        {
            isLeftGeted = growthFundInfos[idx].status == 2;
            isRightGeted = growthFundInfos[idx].status == 2 && growthFundInfos[idx].buy == 2;
        }

        // 奖励
        var obj = itemTrans.Find("Award1s");
        var key = $"{idx}_free";
        bool hasAdd = false;
        if(!addedAwardItem.TryGetValue(key, out hasAdd))
            addedAwardItem.Add(key, true);

        for (int i = 0; i < obj.childCount; i++)
        {
            var obj_Item = obj.GetChild(i);
            obj_Item.gameObject.SetActive(i < activityData.CommonItem.Length);

            if (i < activityData.CommonItem.Length)
            {
                int cfgID = int.Parse(activityData.CommonItem[i].ElementAt(0).Key);

                GameUtil.Instance.SetItemIcon(cfgID, JUIExtensions.img(obj_Item.transform.Find("icon")), JUIExtensions.img(obj_Item));
                JUIExtensions.txt(obj_Item.transform.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(activityData.CommonItem[i].ElementAt(0).Value);

                obj_Item.transform.Find("finishIconImg").gameObject.SetActive(isLeftGeted);

                var spineTrans = obj_Item.Find("spine");
                spineTrans.gameObject.SetActive(!isLeftGeted && isAchieve);
                if(!isLeftGeted && isAchieve)
                    SpineUtil.Instance.PlayItemSpineLoop(obj_Item.Find("spine"), (int)CFG_Item.GetById(cfgID).Quality);

                if (!isLeftGeted && isAchieve && !hasAdd)
                    activityAwardInfo.AddAwardItem(activityData.CommonItem[i].ElementAt(0).Key, activityData.CommonItem[i].ElementAt(0).Value);

                JUIExtensions.btn(obj_Item).onClick.RemoveAllListeners();
                JUIExtensions.btn(obj_Item).onClick.AddListener(() =>
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, cfgID);
                });
            }
        }

        obj = itemTrans.Find("Award2s");
        key = $"{idx}_buy";
        hasAdd = false;
        if (!addedAwardItem.TryGetValue(key, out hasAdd))
            addedAwardItem.Add(key, true);

        for (int i = 0; i < obj.childCount; i++)
        {
            var obj_Item = obj.GetChild(i);
            obj_Item.gameObject.SetActive(i < activityData.PayItem.Length);

            if (i < activityData.PayItem.Length)
            {
                int cfgID = int.Parse(activityData.PayItem[i].ElementAt(0).Key);

                GameUtil.Instance.SetItemIcon(cfgID, JUIExtensions.img(obj_Item.transform.Find("icon")), JUIExtensions.img(obj_Item));
                JUIExtensions.txt(obj_Item.transform.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(activityData.PayItem[i].ElementAt(0).Value);

                obj_Item.transform.Find("LockImg").gameObject.SetActive(!isBuy);
                obj_Item.transform.Find("finishIconImg").gameObject.SetActive(isRightGeted);

                var spineTrans = obj_Item.Find("spine");
                spineTrans.gameObject.SetActive(!isRightGeted && isAchieve && isBuy);
                if (!isRightGeted && isAchieve && isBuy)
                    SpineUtil.Instance.PlayItemSpineLoop(spineTrans, (int)CFG_Item.GetById(cfgID).Quality);

                if (!isRightGeted && isAchieve && isBuy && !hasAdd)
                    activityAwardInfo.AddAwardItem(activityData.PayItem[i].ElementAt(0).Key, activityData.PayItem[i].ElementAt(0).Value);

                JUIExtensions.btn(obj_Item).onClick.RemoveAllListeners();
                JUIExtensions.btn(obj_Item).onClick.AddListener(() =>
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, cfgID);
                });
            }
        }

        JUIExtensions.btn(itemTrans.Find("Achieve/AchieveButton")).onClick.RemoveAllListeners();
        JUIExtensions.btn(itemTrans.Find("Achieve/AchieveButton1")).onClick.RemoveAllListeners();
        JUIExtensions.btn(itemTrans.Find("BgImg_1")).onClick.RemoveAllListeners();
        // 按钮
        if (!isLeftGeted)
        {
            JUIExtensions.btn(itemTrans.Find("Achieve/AchieveButton")).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                GetAllGrowthFundAward();
            });
        }

        if(!isRightGeted)
        {
            if(!isBuy)
            {
                JUIExtensions.btn(itemTrans.Find("BgImg_1")).onClick.AddListener(() =>
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                     PayAndUnlock();
                });
            }

            JUIExtensions.btn(itemTrans.Find("Achieve/AchieveButton1")).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                if (isBuy)
                    GetAllGrowthFundAward();
                else
                    PayAndUnlock();
            });
        }

        return item;
    }

    // 一键领取奖励
    private void GetAllGrowthFundAward()
    {
        // 展示奖励
        HttpC.OnGetGrowthFundAward((_r) =>
        {
            if (isNullGo)
                return;

            // 展示奖励
            activityAwardInfo.des = string.Empty;
            UITool.Open<UIActivityAwards>(null, activityAwardInfo);

            ShowGrowthFund();
        });
    }

    // 购买
    private void PayAndUnlock()
    {
        var storeInfo = CFG_Store.GetById(storeId);
        UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
        {
            ShowStr = $"即将花费<color=#E03042>￥{GameUtil.Instance.FormatNumWithUnit(storeInfo.Limite.Num)}</color>解锁成长基金特权！",
            YesFun = () =>
            {
                GameUtil.Instance.GameToBuy(storeId, () =>
                {
                    if (activityAwardInfo != null)
                        activityAwardInfo.Clear();
                    ShowGrowthFund();
                });
            },
        });
    }

    private int OnGrowthFundMovePosition()
    {
        int position = 0;

        var roleLevel = RoleM.Instance.Level;
        for (int i = 0; i < showActivityList.Count; i++)
        {
            var isAchieve = showActivityList[i].PreLimit <= roleLevel;
            var isLeftGeted = false;
            var isRightGeted = false;
            if (growthFundInfos != null && growthFundInfos.Length > i)
            {
                isLeftGeted = growthFundInfos[i].status == 2;
                isRightGeted = growthFundInfos[i].status == 2 && growthFundInfos[i].buy == 2;
            }

            if ((!isLeftGeted && isAchieve) || (!isRightGeted && isAchieve && isBuy))
            {
                position = i;
                break;
            }else if(isLeftGeted )
                position = i;
        }

        return position;
    }
    #endregion

    #region 月卡
    public void ShowMonth()
    {
        HttpC.GetPrivilegeList((_r) => {
            if (!isNullGo)
                RefreshMothUI(_r.data);

        });
        uIViewGiftBag.monthNodeGo.SetActive(true);
    }

    private void RefreshMothUI(HTTP_PrivilegeDetailDay[] data)
    {

    }
    #endregion

    public override void OnEnter(params object[] args)
    {
        BindRed();
        OnClickTabButton(GetModel().ActiveTitlesDic[ActivityType.DailyGift]);
    }

    public override void OnExit()
    {
        ClearBindRed();
    }

    public override void OnDestroy()
    {

    }
}
