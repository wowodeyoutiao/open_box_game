using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Event;
using JEngine.Helper;
using JEngine.UI;
using MetaFramework.UI;
using pb;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UIActive : UIBase<UIModelActive, UIViewActive>
{
    private string curDateTime;
    private bool isHappyTab;
    private int curHappyTab;
    private UIViewActive uIViewActive;
    private List<CFGItem_Activity> showActivityList;
    private TimeItem timeItem;

    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    public override void OnInit()
    {
        uIViewActive = GetView();

        uIViewActive.closeBtn.onClick.AddListener(delegate
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIActive>();
        });

        // tab
        for (int i = 0; i < GetModel().ActiveTitlesDic.Count; i++)
        {
            var titleData = GetModel().ActiveTitlesDic.ElementAt(i).Value;

            uIViewActive.tabBtns[i].transform.Find("Normal/Label").GetComponent<Text>().text = titleData.activeName;
            uIViewActive.tabBtns[i].transform.Find("Select/Label").GetComponent<Text>().text = titleData.activeName;

            uIViewActive.tabBtns[i].gameObject.name = titleData.activeType.ToString();

            uIViewActive.tabBtns[i].gameObject.SetActive(titleData.ActiveOpen);
            uIViewActive.tabBtns[i].onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnClickTabButton(titleData);
            });
        }

        OninitSign();
        OninitSignHappy();
    }

    #region red Point
    private void BindRed()
    {
        for (int i = 0; i < GetModel().ActiveTitlesDic.Count; i++)
        {
            var titleData = GetModel().ActiveTitlesDic.ElementAt(i).Value;

            if (titleData.ActiveOpen)
            {
                int index = i;
                RedPointManager.Instance.RPS.Register(titleData.RedKey, (node) =>
                {
                    uIViewActive.tabBtns[index].transform.Find("Dot").gameObject.SetActive(node.pointCount > 0);
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

    #endregion

    private void OnClickTabButton(UIModelActive.ActiveTitle idex)
    {
        for (int i = 0; i < uIViewActive.tabBtns.Length; i++)
        {
            uIViewActive.tabBtns[i].transform.Find("Select").gameObject.SetActive(uIViewActive.tabBtns[i].gameObject.name == idex.activeType.ToString());
        }

        uIViewActive.sign7Go.SetActive(idex.activeType == ActivityType.Sign7);
        uIViewActive.happy7Go.SetActive(idex.activeType == ActivityType.SignHappy7);

        Clear();
        if (idex.cfgId > 0)
        {
            showActivityList = CFG_Activity.GetActiveListByType((CFG_Activity.Type)idex.cfgId);
            if (idex.activeType == ActivityType.Sign7)
                uIViewActive.signDesText.text = $"活动说明：{CFG_Activity.GetActiveTypeDic()[idex.cfgId].RuleDes}";

            SetActiveTimeText(idex.activeType);
            AddTimer();
        }
        else
        {
            if (idex.activeType == ActivityType.Sign7)
                uIViewActive.signDesText.text = $"";
            uIViewActive.activeTimeText.text = $"永久存在";
        }

        isHappyTab = idex.activeType == ActivityType.SignHappy7;
        switch (idex.activeType)
        {
            case ActivityType.Sign7:
                OnSign7Show();
                break;

            case ActivityType.SignHappy7:
                OnSignHappyShow();
                break;
            default:
                break;
        }
    }

    #region 七日签到
    private void OninitSign()
    {
        if (!GetModel().ActiveTitlesDic[ActivityType.Sign7].ActiveOpen)
            return;

        uIViewActive.infiniteScroll_Sign7.InitListView(0, OnSign7Fill);
    }

    private LoopListViewItem2 OnSign7Fill(LoopListView2 listView, int idx)
    {
        if (idx < 0 || idx >= showActivityList.Count)
        {
            return null;
        }
        var info = showActivityList[idx];
        var item = listView.NewListViewItem("Item");
        GameObject go = item.gameObject;
        var obj = go.transform.Find("Awards");
        JUIExtensions.txt(go.transform.Find("txt_day")).text = $"第{info.Content}天";
        for (int i = 0; i < obj.childCount; i++)
        {
            obj.GetChild(i).gameObject.SetActive(i < info.CommonItem.Length);

            if (i < info.CommonItem.Length)
            {
                int cfgID = int.Parse(info.CommonItem[i].ElementAt(0).Key);
                GameUtil.Instance.SetItemIcon(cfgID, JUIExtensions.img(obj.GetChild(i).transform.Find("icon")), JUIExtensions.img(obj.GetChild(i)));
                JUIExtensions.txt(obj.GetChild(i).transform.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(info.CommonItem[i].ElementAt(0).Value);

                JUIExtensions.btn(obj.GetChild(i)).onClick.RemoveAllListeners();
                JUIExtensions.btn(obj.GetChild(i)).onClick.AddListener(() =>
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, cfgID);
                });
            }
        }
        var _State = (ActivityM.SignState)ActivityM.Instance.GetSignStateByDay(info.Content).status;

        JUIExtensions.txt(go.transform.Find("txt_GetState")).text = _State == ActivityM.SignState.NoGet ? "时间未到" : "";

        go.transform.Find("Geted").gameObject.SetActive(_State == ActivityM.SignState.Geted);

        JUIExtensions.btn(go.transform.Find("Btn_Get")).gameObject.SetActive(_State == ActivityM.SignState.CanGet);
        JUIExtensions.btn(go.transform.Find("Btn_Get")).onClick.RemoveAllListeners();
        JUIExtensions.btn(go.transform.Find("Btn_Get")).onClick.AddListener(delegate { OnSignBtnClick(info, false); });

        var compensationConsumeId = int.Parse(info.CompensationConsume[0].ElementAt(0).Key);
        JUIExtensions.btn(go.transform.Find("Btn_GetPass")).gameObject.SetActive(_State == ActivityM.SignState.PayGet);
        JUIExtensions.btn(go.transform.Find("Btn_GetPass")).onClick.RemoveAllListeners();
        JUIExtensions.btn(go.transform.Find("Btn_GetPass")).onClick.AddListener(delegate { OnSignPassBtnClick(idx, info, compensationConsumeId == 51002); });

        GameUtil.Instance.SetItemIcon(compensationConsumeId, JUIExtensions.img(go.transform.Find("Btn_GetPass/icon")));
        if (compensationConsumeId == 51002)
            JUIExtensions.txt(go.transform.Find("Btn_GetPass/txt")).text = "";
        else
            JUIExtensions.txt(go.transform.Find("Btn_GetPass/txt")).text = GameUtil.Instance.FormatNumWithUnit(info.CompensationConsume[0].ElementAt(0).Value);

        return item;
    }

    public void OnSignBtnClick(CFGItem_Activity info, bool useVideo)
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        HttpC.OnSign7Get(info.UUID, useVideo, (_response) =>
        {
            if (_response.code == 0 && !isNullGo)
            {
                OnSign7Show();

                var awardInfo = new ActivityAwardInfo();
                for (int i = 0; i < info.CommonItem.Length; i++)
                {
                    awardInfo.AddAwardItem(info.CommonItem[i].ElementAt(0).Key, info.CommonItem[i].ElementAt(0).Value);
                }
                awardInfo.des = string.Empty;
                UITool.Open<UIActivityAwards>(null, awardInfo);
            }
            else
            {
                UITool.ShowTips(_response.data);
            }
        });
    }

    public void OnSignPassBtnClick(int idx, CFGItem_Activity info, bool useVideo)
    {
        if (!useVideo) // 使用非广告补签时
        {
            var used = showActivityList[idx].CompensationConsume[0].ElementAt(0);
            if (!GameUtil.Instance.ComCoinToCheck(int.Parse(used.Key), used.Value, delegate { UITool.Close<UIActive>(); }))
            {
                return;
            }
        }
        OnSignBtnClick(info, useVideo);
    }
    private void OnSign7Show()
    {
        UITool.Open<UILoading>(null, new UILoading.Loading { _time = 0 });
        HttpC.OnSign7Info((_response) =>
        {
            UITool.Close<UILoading>();
            if (_response.code == 0 && !isNullGo)
            {
                if (GetView().infiniteScroll_Sign7.ItemTotalCount != showActivityList.Count)
                    GetView().infiniteScroll_Sign7.SetListItemCount(showActivityList.Count);
                GetView().infiniteScroll_Sign7.RefreshAllShownItem();
                GetView().infiniteScroll_Sign7.MovePanelToItemIndex(OnSign7MovePosition(), 0);
            }
        });
    }

    private int OnSign7MovePosition()
    {
        int position = 0;

        for (int i = 0; i < showActivityList.Count; i++)
        {
            var state = (ActivityM.SignState)ActivityM.Instance.GetSignStateByDay(showActivityList[i].Content).status;
            if (state == ActivityM.SignState.CanGet)
            {
                position = i;
                break;
            }
            else if (state == ActivityM.SignState.Geted)
            {
                position = i;
            }
        }

        return position;
    }
    #endregion

    #region 七日狂欢
    private int curDay;
    private List<TaskInfo> allHappy7TaskInfos;
    private List<TaskInfo> happy7TaskInfos = new List<TaskInfo>();

    private Coroutine happyAwardCoroutine;

    private void OninitSignHappy()
    {
        for (int i = 0; i < uIViewActive.happy7TabBtns.Length; i++)
        {
            var index = i;
            uIViewActive.happy7TabBtns[i].onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnClickHappyTabButton(index);
            });
        }

        uIViewActive.infiniteScroll_happy7.InitListView(0, OnSignHappy7Fill);
    }

    private void ShowDayRedPoint(int index)
    {
        uIViewActive.happy7TabBtns[index].transform.GetChild(2).gameObject.SetActive(true);
    }

    private void OnClickHappyTabButton(int index)
    {
        if (curDay < index + 1)
        {
            UITool.ShowTips("活动开启时间未到，请明天再试！");
            return;
        }

        for (int i = 0; i < uIViewActive.happy7TabBtns.Length; i++)
        {
            uIViewActive.happy7TabBtns[i].transform.GetChild(1).gameObject.SetActive(index == i);
            uIViewActive.happy7TabBtns[i].transform.GetChild(2).gameObject.SetActive(false);
        }

        ShowHappyDailyGift(index);

        allHappy7TaskInfos = TaskM.Instance.GetTasksByClass(TaskClass.SEVENDAY);

        happy7TaskInfos.Clear();
        if (allHappy7TaskInfos != null && allHappy7TaskInfos.Count > 0)
        {
            for (int i = 0; i < allHappy7TaskInfos.Count; i++)
            {
                TaskInfo taskInfo = allHappy7TaskInfos[i];
                var cfgInfo = CFG_Task.GetById(taskInfo.CfgId);
                if (cfgInfo.SevenDay == index + 1)
                    happy7TaskInfos.Add(taskInfo);

                if (taskInfo.Status == TaskStatus.COMPLETED)
                    ShowDayRedPoint((int)cfgInfo.SevenDay - 1);

                if (cfgInfo.SevenDay > curDay)
                    curDay = (int)cfgInfo.SevenDay;
            }
        }

        curHappyTab = index;
        uIViewActive.infiniteScroll_happy7.SetListItemCount(happy7TaskInfos.Count);
        uIViewActive.infiniteScroll_happy7.RefreshAllShownItem();
    }

    private void OnSignHappyShow()
    {
        OnClickHappyTabButton(curHappyTab);

        // 大奖
        SignHappyBigAwardShow();
    }

    private void SignHappyBigAwardShow()
    {
        var bigAwards = TaskM.Instance.GetTasksByClass(TaskClass.SEVENDAY_ACTIVITY);
        var taskInfo = bigAwards[0];
        var taskCfg = CFG_Task.GetById(taskInfo.CfgId);
        (int amout, int targetnum) = CFG_Task.GetTaskPro(taskInfo.CfgId, taskInfo.Target.Amount, taskInfo.Target.TargetNum);
        uIViewActive.happy7Slider.value = amout * 1.0f / targetnum;
        uIViewActive.happy7SchduleText.text = $"{amout}/{targetnum}";

        var award = uIViewActive.happy7SliderAwardTrans;
        int CfgId = int.Parse(taskCfg.Reward[0].ElementAt(0).Key);
        GameUtil.Instance.SetItemIcon(CfgId, JUIExtensions.img(award.Find("icon")), JUIExtensions.img(award));
        JUIExtensions.txt(award.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(taskCfg.Reward[0].ElementAt(0).Value);

        award.Find("GetImg").gameObject.SetActive(taskInfo.Status == TaskStatus.REWARDED);
        JUIExtensions.btn(award).onClick.RemoveAllListeners();
        JUIExtensions.btn(award).onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            if (taskInfo.Status == TaskStatus.COMPLETED)
                OnClickHappyGetButton(taskInfo, taskCfg);
            else
                UITool.Open<UIItemShow>(null, CfgId);
        });

        ClearTween();
        var spineGo = award.Find("spine").gameObject;
        var ani = award.GetComponent<Animation>();
        award.localRotation = Quaternion.Euler(Vector3.zero);
        spineGo.SetActive(taskInfo.Status == TaskStatus.COMPLETED);
        if (taskInfo.Status == TaskStatus.COMPLETED)
        {
            happyAwardCoroutine = CoroutineMgr.Instance.StartCoroutine(SetHappyAwardCoroutine(award, ani));
        }
    }

    private System.Collections.IEnumerator SetHappyAwardCoroutine(Transform trans, Animation ani)
    {
        while(true)
        {
            ani.enabled = true;
            yield return new WaitForSeconds(1);
            trans.localRotation = Quaternion.Euler(Vector3.zero);
            ani.enabled = false;
            yield return new WaitForSeconds(1);
        }
    }

    // 特惠礼包
    private void ShowHappyDailyGift(int index)
    {
        int shopId = showActivityList[index].PreLimit;
        var storeInfo = CFG_Store.GetById(shopId);

        var obj = uIViewActive.happy7GiftAwardTrans;
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

        bool soldOut = storeInfo.Limite.limittype != 999 && StoreM.Instance.GetBuyNumByID(shopId) >= storeInfo.Limite.limitnum;
        uIViewActive.happy7GiftAwardBtn.gameObject.SetActive(!soldOut);
        uIViewActive.GetGo.SetActive(soldOut);

        if (!soldOut)
        {
            JUIExtensions.txt(uIViewActive.happy7GiftAwardBtn.transform.Find("txt")).text = $"￥{GameUtil.Instance.FormatNumWithUnit(storeInfo.Limite.Num)}";
            JUIExtensions.btn(uIViewActive.happy7GiftAwardBtn).onClick.RemoveAllListeners();
            JUIExtensions.btn(uIViewActive.happy7GiftAwardBtn).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnGiftItemClick(shopId);
            });
        }
    }

    private void OnGiftItemClick(int shopId, bool isFree = false)
    {
        GameUtil.Instance.GameToBuy(shopId, () =>
        {
            uIViewActive.happy7GiftAwardBtn.gameObject.SetActive(false);
            uIViewActive.GetGo.SetActive(true);

            var storeInfo = CFG_Store.GetById(shopId);
            var info = new ActivityAwardInfo();
            for (int i = 0; i < storeInfo.Solditems.Length; i++)
            {
                info.AddAwardItem(storeInfo.Solditems[i].itemID, storeInfo.Solditems[i].Num);
            }
            info.des = string.Empty;
            UITool.Open<UIActivityAwards>(null, info);
        });
    }

    private LoopListViewItem2 OnSignHappy7Fill(LoopListView2 listView, int idx)
    {
        var item = listView.NewListViewItem("Item");
        GameObject go = item.gameObject;
        var taskInfo = happy7TaskInfos[idx];
        CFGItem_Task taskCfg = CFG_Task.GetById(taskInfo.CfgId);

        // 标题
        JUIExtensions.txt(go.transform.Find("TitleText")).text = CFG_Task.StrTaskDes(taskInfo.CfgId);

        // 礼物
        var index = 0;
        //var cfgId = 0;
        var awardTrans = go.transform.Find("Awards");
        for (int i = 0; i < awardTrans.childCount; i++)
        {
            var award = awardTrans.GetChild(i);
            award.gameObject.SetActive(i < taskCfg.Reward.Length);
            if (i < taskCfg.Reward.Length)
            {
                index++;
                AddAward(award, int.Parse(taskCfg.Reward[i].ElementAt(0).Key), taskCfg.Reward[i].ElementAt(0).Value);
                //cfgId = int.Parse(taskCfg.Reward[i].ElementAt(0).Key);
                //GameUtil.Instance.SetItemIcon(cfgId, JUIExtensions.img(award.Find("icon")), JUIExtensions.img(award));
                //JUIExtensions.txt(award.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(taskCfg.Reward[i].ElementAt(0).Value);

                //JUIExtensions.btn(award).onClick.RemoveAllListeners();
                //JUIExtensions.btn(award).onClick.AddListener(() =>
                //{
                //    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                //    UITool.Open<UIItemShow>(null, cfgId);
                //});
            }
        }

        var activityRewardTrans = awardTrans.GetChild(index);
        activityRewardTrans.gameObject.SetActive(true);
        AddAward(activityRewardTrans, taskCfg.ActivityReward.key, taskCfg.ActivityReward.value);
        //cfgId = taskCfg.ActivityReward.key;
        //GameUtil.Instance.SetItemIcon(cfgId, JUIExtensions.img(activityReward.Find("icon")), JUIExtensions.img(activityReward));
        //JUIExtensions.txt(activityReward.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(taskCfg.ActivityReward.value);

        //JUIExtensions.btn(activityReward).onClick.RemoveAllListeners();
        //JUIExtensions.btn(activityReward).onClick.AddListener(() =>
        //{
        //    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        //    UITool.Open<UIItemShow>(null, cfgId);
        //});
        //Debug.LogError($"{taskInfo.CfgId}, {taskInfo.Target.Amount}, {taskInfo.Target.TargetNum}");
        // 进度
        (int amout, int targetnum) = CFG_Task.GetTaskPro(taskInfo.CfgId, taskInfo.Target.Amount, taskInfo.Target.TargetNum);
        if (amout > targetnum) amout = targetnum;
        JUIExtensions.txt(go.transform.Find("sliderText")).text = $"{GameUtil.Instance.FormatNumWithUnit(amout)}/{GameUtil.Instance.FormatNumWithUnit(targetnum)}";
        go.transform.Find("Slider").GetComponent<Slider>().value = amout * 1.0f / targetnum;

        var getBtnTrans = go.transform.Find("Btn_Get"); // 领取
        var goBtnTrans = go.transform.Find("Btn_Go"); // 前往
        var finishBtnTrans = go.transform.Find("Geted"); // 已完成

        if (taskInfo.Status == TaskStatus.IN_PROGRESS)
        {
            JUIExtensions.txt(go.transform.Find("TitleText")).color = new Color(169f / 255, 134f / 255, 93f / 255);
            JUIExtensions.txt(go.transform.Find("sliderText")).color = new Color(169f / 255, 134f / 255, 93f / 255);
        }
        else
        {
            JUIExtensions.txt(go.transform.Find("TitleText")).color = new Color(64f / 255, 176f / 255, 35f / 255);
            JUIExtensions.txt(go.transform.Find("sliderText")).color = new Color(64f / 255, 176f / 255, 35f / 255);
        }

        finishBtnTrans.gameObject.SetActive(taskInfo.Status == TaskStatus.REWARDED);
        goBtnTrans.gameObject.SetActive(taskInfo.Status == TaskStatus.IN_PROGRESS);
        getBtnTrans.gameObject.SetActive(taskInfo.Status == TaskStatus.COMPLETED);
        go.transform.Find("GetedImg").gameObject.SetActive(taskInfo.Status == TaskStatus.REWARDED);

        JUIExtensions.btn(goBtnTrans).onClick.RemoveAllListeners();
        JUIExtensions.btn(getBtnTrans).onClick.RemoveAllListeners();

        if (taskInfo.Status == TaskStatus.COMPLETED)
        {
            JUIExtensions.btn(getBtnTrans).onClick.AddListener(() =>
            {
                OnClickHappyGetButton(taskInfo, taskCfg);
            });
        }

        if (taskInfo.Status == TaskStatus.IN_PROGRESS)
        {
            JUIExtensions.btn(goBtnTrans).onClick.AddListener(() =>
            {
                OnClickHappyGoToButton(taskInfo.CfgId, taskCfg.Type);
            });
        }

        return item;
    }

    private void AddAward(Transform trans, int cfgId, int count)
    {
        GameUtil.Instance.SetItemIcon(cfgId, JUIExtensions.img(trans.Find("icon")), JUIExtensions.img(trans));
        JUIExtensions.txt(trans.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(count);

        JUIExtensions.btn(trans).onClick.RemoveAllListeners();
        JUIExtensions.btn(trans).onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIItemShow>(null, cfgId);
        });
    }
    private void OnClickHappyGetButton(TaskInfo taskInfo, CFGItem_Task taskCfg)
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        TcpC.C2S_TaskComplate(taskInfo.CfgId);

        var info = new ActivityAwardInfo();
        for (int i = 0; i < taskCfg.Reward.Length; i++)
        {
            info.AddAwardItem(taskCfg.Reward[i].ElementAt(0).Key, taskCfg.Reward[i].ElementAt(0).Value);
        }
        info.des = string.Empty;
        UITool.Open<UIActivityAwards>(null, info);
    }

    private void OnClickHappyGoToButton(int configID, int taskType)
    {
        if (taskType == (int)pb.TargetType.SEVENDAY_TASK_GIFT_PURCHASED)
        {
            // 购买七日任务礼包，查看那一天没有购买，跳转到那一天
            var dayIndex = -1;
            for (int i = 0; i < showActivityList.Count; i++)
            {
                int shopId = showActivityList[i].PreLimit;
                var storeInfo = CFG_Store.GetById(shopId);
                bool soldOut = storeInfo.Limite.limittype != 999 && StoreM.Instance.GetBuyNumByID(shopId) >= storeInfo.Limite.limitnum;
                if (!soldOut)
                {
                    dayIndex = i;
                    break;
                }
            }

            if (dayIndex >= 0)
                OnClickHappyTabButton(dayIndex);
        }
        else
        {
            CFG_Task.JumpUI(configID, jumpHandle: () =>
            {
                UITool.Close<UIActive>();
            });
        }
    }

    private void ClearTween()
    {
        if (happyAwardCoroutine != null)
        {
            CoroutineMgr.Instance.StopCoroutine(happyAwardCoroutine);
            happyAwardCoroutine = null;
        }
    }
    #endregion

    #region active time
    private void AddTimer()
    {
        var endTime = ActivityM.Instance.GetAcitvityEndTime(isHappyTab ? ActivityType.SignHappy7 : ActivityType.Sign7);
        var timeStr = GameUtil.Instance.GetTimeSToDHOrHMS(endTime);
        timeItem = GameHelper.Instance.AddInvoke(new TimeItem(endTime, 1, () =>
        {
            SetActiveTimeText(isHappyTab ? ActivityType.SignHappy7 : ActivityType.Sign7);
            endTime--;
            if (endTime <= 0)
                Clear();
        }));
    }

    private void SetActiveTimeText(ActivityType activeType)
    {
        var endTime = ActivityM.Instance.GetAcitvityEndTime(activeType);
        var timeStr = GameUtil.Instance.GetTimeSToDHOrHMS(endTime);
        //Debug.LogError($"{activeType},{endTime} {timeStr}");
        uIViewActive.activeTimeText.text = timeStr + "后结束";
    }

    private void Clear()
    {
        if (timeItem != null)
        {
            GameHelper.Instance.RemoveInvoke(timeItem);
            timeItem = null;
        }
    }
    #endregion

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
        BindRed();
        curDay = 1;
        curHappyTab = 0;
        isHappyTab = false;
        Clear();
        ClearTween();

        curDateTime = DateTime.Now.ToString("yyyyMMdd");
        if (ActivityM.Instance.GetAcitvityState(ActivityType.Sign7))
            OnClickTabButton(GetModel().ActiveTitlesDic[ActivityType.Sign7]);
        else
            OnClickTabButton(GetModel().ActiveTitlesDic[ActivityType.SignHappy7]);
    }

    [Subscriber(ThreadMode.Main)]
    private void Task_Refresh(GameEvent.Refresh_Task _e)
    {
        if (isNullGo) return;

        var str = DateTime.Now.ToString("yyyyMMdd");
        if (curDateTime != str)
        {
            curDay++;
            curDateTime = str;
        }

        if (uiGo.activeSelf && isHappyTab)
            OnSignHappyShow();
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
        Clear();
        ClearTween();
        ClearBindRed();
    }

    public override void OnDestroy()
    {
        ClearBindRed();
    }
}