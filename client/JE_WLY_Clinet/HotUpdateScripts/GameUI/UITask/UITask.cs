using HotUpdateScripts;
using HotUpdateScripts.ProtoM;
using JEngine.Event;
using JEngine.Helper;
using JEngine.UI;
using MetaFramework.UI;
using pb;
using SuperScrollView;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UITask : UIBase<UIModelTask, UIViewTask>
{
    private string[] names = new string[] { "每日签到", "日常任务", "周常任务", "成就任务"};

    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UITask>();
        });

        GetView().signDayGo.SetActive(true);
        GetView().taskViewGo.SetActive(false);

        OnInitSign();

        GetView().root_topBtns.GetChild(0).GetComponent<Button>().onClick.AddListener(delegate
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnClickSignButton();
        });

        for (int i = 1; i < GetView().root_topBtns.childCount; i++)
        {
            TaskClass tc = (TaskClass)i;
            GetView().root_topBtns.GetChild(i).GetComponent<Button>().onClick.AddListener(delegate
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnTaskClassBtnClick(tc);
            });
        }

        GetView().InfiniteScroll_Task.InitListView(0, OnTaskFill);
    }

    private void OnBindRed()
    {
        RedPointManager.Instance.RPS.Register(RedPointConst.SignDay.ToString(), (node) => {

            GetView().root_topBtns.GetChild(0).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        for (int i = 1; i < GetView().root_topBtns.childCount; i++)
        {
            int idx = i;
            RedPointManager.Instance.RPS.Register(((TaskClass)idx).ToString(), (node) =>
            {
                GetView().root_topBtns.GetChild(idx).Find("dot").gameObject.SetActive(node.pointCount > 0);
            });
        }
    }

    private void ClearRed()
    {
        RedPointManager.Instance.RPS.Register(RedPointConst.SignDay.ToString(), null);
        for (int i = 1; i < GetView().root_topBtns.childCount; i++)
        {
            RedPointManager.Instance.RPS.Register(((TaskClass)i).ToString(),null);
        }
    }

    private void OpenAwardUI(int cfgId)
    {
        CFGItem_Task cfgInfo = CFG_Task.GetById(cfgId);
        List<UIBoxInfoShow.BoxItem[]> items = new List<UIBoxInfoShow.BoxItem[]>();

        UIBoxInfoShow.BoxItem[] boxItems = new UIBoxInfoShow.BoxItem[cfgInfo.Reward.Length];
        for (int j = 0; j < cfgInfo.Reward.Length; j++)
        {
            boxItems[j] = new UIBoxInfoShow.BoxItem { Id = int.Parse(cfgInfo.Reward[j].ElementAt(0).Key), Num = cfgInfo.Reward[j].ElementAt(0).Value };
        }
        items.Add(boxItems);
        UITool.Open<UIBoxInfoShow>(null, null, items);

    }

    private LoopListViewItem2 OnTaskFill(LoopListView2 loopListView, int idx)
    {
        LoopListViewItem2 item = loopListView.NewListViewItem("Item");
        GameObject go = item.gameObject;
        TaskInfo taskInfo = _OneClassTaskInfo[idx];
        var cfgInfo = CFG_Task.GetById(taskInfo.CfgId);

        string str = cfgInfo.TaskDes;

        JUIExtensions.txt(go.transform.Find("txt_Name")).text = CFG_Task.StrTaskDes(taskInfo.CfgId);

        for (int i = 0; i < 5; i++)
        {
            var award = go.transform.Find("awards").GetChild(i);
            award.gameObject.SetActive(i < cfgInfo.Reward.Length);
            if (i < cfgInfo.Reward.Length)
            {
                int CfgId = int.Parse(cfgInfo.Reward[i].ElementAt(0).Key);
                GameUtil.Instance.SetItemIcon(CfgId, JUIExtensions.img(award.Find("icon")), JUIExtensions.img(award), JUIExtensions.img(award.Find("icon/nature")));
                JUIExtensions.txt(award.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(cfgInfo.Reward[i].ElementAt(0).Value);

                JUIExtensions.btn(award).onClick.RemoveAllListeners();
                JUIExtensions.btn(award).onClick.AddListener(() => {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, CfgId);
                });
            }
        }

        string strLimet = string.Empty;
        bool showGetBtn = taskInfo.Status == TaskStatus.COMPLETED;
        bool isProgressing = false;

        (int amout,int targetnum ) = CFG_Task.GetTaskPro(taskInfo.CfgId, taskInfo.Target.Amount, taskInfo.Target.TargetNum);
        string strPro = $"进度{ GameUtil.Instance.FormatNumWithUnit(amout) }/{GameUtil.Instance.FormatNumWithUnit(targetnum) }";
        if (_TaskClass == TaskClass.ROUTINE)
        {
            int startHoure = int.Parse(cfgInfo.LimitTime.start.Split(':')[0]);
            int endHoure = int.Parse(cfgInfo.LimitTime.end.Split(':')[0]);
            bool isStart = GameUtil.Instance.GetNowToTimeSecoud(0, startHoure) > 0;
            bool isEnd = GameUtil.Instance.GetNowToTimeSecoud(0, endHoure) > 0;

            if (!isEnd)
            {
                strLimet = taskInfo.Status == TaskStatus.REWARDED ? "已领取" : "已过期";
            }
            else if (isStart)
            {
                strLimet = "未到时间";
            }
            else
            {
                strLimet = taskInfo.Status == TaskStatus.IN_PROGRESS ? "进行中" : "已领取";
                isProgressing = taskInfo.Status == TaskStatus.IN_PROGRESS;
            }
            strPro = "";
        }
        else
        {
            strLimet = taskInfo.Status == TaskStatus.IN_PROGRESS ? "进行中" : "已领取";
            isProgressing = taskInfo.Status == TaskStatus.IN_PROGRESS;
        }

        JUIExtensions.txt(go.transform.Find("txt_Pro")).text = strPro;

        JUIExtensions.txt(go.transform.Find("txt_tip")).text = strLimet;

        go.transform.Find("btn_Go").gameObject.SetActive(isProgressing);
        JUIExtensions.btn(go.transform.Find("btn_Go")).onClick.RemoveAllListeners();
        JUIExtensions.btn(go.transform.Find("btn_Go")).onClick.AddListener(() => {
            CFG_Task.JumpUI(taskInfo.CfgId, jumpHandle: () =>
            {
                UITool.Close<UITask>();
            });
        });

        go.transform.Find("btn_Get").gameObject.SetActive(showGetBtn);
        go.transform.Find("Geted").gameObject.SetActive(taskInfo.Status == TaskStatus.REWARDED);
        JUIExtensions.btn(go.transform.Find("btn_Get")).onClick.RemoveAllListeners();
        JUIExtensions.btn(go.transform.Find("btn_Get")).onClick.AddListener(() => {
            OnTaskGet(taskInfo.CfgId);
        });

        return item;
    }

    #region 每日签到
    private List<CFGItem_Sign> singList;
    private List<CFGItem_Sign> accumulateList;
    private HTTP_SignAwsard httpData;
    private HashSet<int> aSign;
    private int AllSignCount;
    private int ShowCount = 0;

    private void OnInitSign()
    {
        GetView().btn_VIPUp.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIActive>();
            UITool.Open<UIVIP>();
        });

        GetView().ScrollView_SignDay.InitGridView(0, OnSignDayItem);
        GetView().ScrollView_Cumulative.InitListView(0, OnSignDayFillAccumulateItem);
    }

    private void OnRefreshSignList()
    {
        UITool.Open<UILoading>(null, new UILoading.Loading { _time = 0 });
        HttpC.GetSignList((_r) =>
        {
            UITool.Close<UILoading>();
            OnSignShow(_r.data);
        });
    }

    private void OnSignShow(HTTP_SignAwsard data)
    {
        httpData = data;

        (singList, accumulateList) = CFG_Sign.GetSignAndAccumulateListByRound(data.day_award.round);

        if (GetView().ScrollView_SignDay.ItemTotalCount != singList.Count)
            GetView().ScrollView_SignDay.SetListItemCount(singList.Count);

        GetView().ScrollView_SignDay.RefreshAllShownItem();
        GetView().ScrollView_SignDay.MovePanelToItemByRowColumn((httpData.day_award.day - 1) / 3, 0);

        AllSignCount = data.day_award.sign ? data.day_award.day : data.day_award.day - 1;
        aSign = new HashSet<int>();
        for (int i = 0; i < data.accumulate_award.Length; i++)
        {
            aSign.Add(data.accumulate_award[i].day);
        }
        ShowCount = 0;

        if (GetView().ScrollView_Cumulative.ItemTotalCount != accumulateList.Count)
            GetView().ScrollView_Cumulative.SetListItemCount(accumulateList.Count);

        GetView().ScrollView_Cumulative.RefreshAllShownItem();
        GetView().ScrollView_Cumulative.MovePanelToItemIndex(ShowCount, 0);
        GetView().btn_VIPUp.gameObject.SetActive(RoleM.Instance.Vip < 1);
        GetView().obj_VipMax.gameObject.SetActive(CFG_VIP.IsMaxVipLevel(RoleM.Instance.Vip));
    }

    LoopGridViewItem OnSignDayItem(LoopGridView gridView, int index, int row, int column)
    {
        int sordIdx = (index);

        LoopGridViewItem _LoopGridViewItem = gridView.NewListViewItem("Item");
        GameObject Item = _LoopGridViewItem.gameObject;

        var signInfo = singList[index];
        JUIExtensions.txt(Item.transform.Find("txt_Name")).text = $"第{signInfo.Days}天";
        JUIExtensions.txt(Item.transform.Find("Vip/txt_Viptip")).text = $"V{signInfo.VIPLimit}";

        GameUtil.Instance.SetItemIcon(signInfo.Award[0].id, JUIExtensions.img(Item.transform.Find("Item/icon")), JUIExtensions.img(Item.transform.Find("Item")));

        JUIExtensions.touch(Item.transform).RemoveAllListeners();
        JUIExtensions.touch(Item.transform).AddPointerDownListener(p =>
        {
            _TouchTime_SignDay = GameHelper.Instance.AddInvoke(new TimeItem(GameConst.TouchTime, () =>
            {
                if (!p.dragging)
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, signInfo.Award[0].id);
                }
            }));
        });
        JUIExtensions.touch(Item.transform).AddPointerUpListener(p =>
        {
            ClearTouch_SignDay();
        });
        //JUIExtensions.btn(Item.transform.Find("Item")).onClick.RemoveAllListeners();

        //JUIExtensions.btn(Item.transform.Find("Item")).onClick.AddListener(() => {
        //    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        //    UITool.Open<UIItemShow>(null, signInfo.Award[0].id);
        //});

        JUIExtensions.txt(Item.transform.Find("txt_num")).text = "x" + GameUtil.Instance.FormatNumWithUnit(signInfo.Award[0].num);
        Item.transform.Find("geted").gameObject.SetActive(httpData.day_award.day > signInfo.Days || httpData.day_award.day == signInfo.Days && httpData.day_award.sign);
        Item.transform.Find("select").gameObject.SetActive(httpData.day_award.day == signInfo.Days && !httpData.day_award.sign);
        //JUIExtensions.touch(Item.transform).RemoveAllListeners();
        if (httpData.day_award.day == signInfo.Days && !httpData.day_award.sign)
        {
            //JUIExtensions.btn(Item.transform).onClick.AddListener(() => {
            //    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //    HttpC.GetSignOneDay((_r) => {
            //        if (!isNullGo)
            //            OnRefreshSignList();
            //    });
            //});

            JUIExtensions.touch(Item.transform).AddClickListener(p =>
            {
                if (!p.dragging)
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    HttpC.GetSignOneDay((_r) =>
                    {
                        if (!isNullGo)
                            OnRefreshSignList();
                    });
                }
            });
        }

        return _LoopGridViewItem;
    }

    private TimeItem _TouchTime_SignDay;
    private void ClearTouch_SignDay()
    {
        if (_TouchTime_SignDay != null)
        {
            GameHelper.Instance.RemoveInvoke(_TouchTime_SignDay);
            _TouchTime_SignDay = null;
        }
    }

    private LoopListViewItem2 OnSignDayFillAccumulateItem(LoopListView2 loopListView, int idx)
    {
        LoopListViewItem2 item = loopListView.NewListViewItem("Item");
        GameObject Item = item.gameObject;

        var signInfo = accumulateList[idx];
        JUIExtensions.txt(Item.transform.Find("txt_Name")).text = $"累计{signInfo.Days}天";
        JUIExtensions.txt(Item.transform.Find("Vip/txt_Viptip")).text = $"V{signInfo.VIPLimit}";

        for (int i = 0; i < 2; i++)
        {
            Transform awardRoot = Item.transform.Find("award").GetChild(i);
            awardRoot.gameObject.SetActive(i < signInfo.AccumulateAward.Length);
            if (i < signInfo.AccumulateAward.Length)
            {
                int _index = i;
                GameUtil.Instance.SetItemIcon(signInfo.AccumulateAward[i].id, JUIExtensions.img(awardRoot, "icon"), JUIExtensions.img(awardRoot));
                JUIExtensions.txt(awardRoot, "txt_num").text = GameUtil.Instance.FormatNumWithUnit(signInfo.AccumulateAward[i].num);

                JUIExtensions.touch(awardRoot).RemoveAllListeners();
                JUIExtensions.touch(awardRoot).AddPointerDownListener(p =>
                {
                    _TouchTime_SignDay = GameHelper.Instance.AddInvoke(new TimeItem(GameConst.TouchTime, () =>
                    {
                        if (!p.dragging)
                        {
                            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                            UITool.Open<UIItemShow>(null, signInfo.AccumulateAward[_index].id);
                        }
                    }));
                });

                JUIExtensions.touch(awardRoot).AddPointerUpListener(p =>
                {
                    ClearTouch_SignDay();
                });

                if (!aSign.Contains(signInfo.Days) && AllSignCount >= signInfo.Days)
                {
                    ShowCount = ShowCount == 0 ? idx : ShowCount;
                    JUIExtensions.touch(Item.transform).AddClickListener(p =>
                    {
                        if (!p.dragging)
                        {
                            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                            HttpC.GetSignAccumulateDay(signInfo.Round, signInfo.Days, (_r) =>
                            {
                                if (!isNullGo)
                                    OnRefreshSignList();
                            });
                        }
                    });
                }
            }

            int cfgID = signInfo.AccumulateAward[i].id;
        }


        Item.transform.Find("geted").gameObject.SetActive(aSign.Contains(signInfo.Days));
        Item.transform.Find("select").gameObject.SetActive(!aSign.Contains(signInfo.Days) && AllSignCount >= signInfo.Days);

        JUIExtensions.touch(Item.transform).RemoveAllListeners();
        JUIExtensions.touch(Item.transform).AddPointerDownListener(p =>
        {
            _TouchTime_SignDay = GameHelper.Instance.AddInvoke(new TimeItem(GameConst.TouchTime, () =>
            {
                if (!p.dragging)
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, signInfo.AccumulateAward[0].id);
                }
            }));
        });

        JUIExtensions.touch(Item.transform).AddPointerUpListener(p =>
        {
            ClearTouch_SignDay();
        });

        if (!aSign.Contains(signInfo.Days) && AllSignCount >= signInfo.Days)
        {
            ShowCount = ShowCount == 0 ? idx : ShowCount;
            JUIExtensions.touch(Item.transform).AddClickListener(p =>
            {
                if (!p.dragging)
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    HttpC.GetSignAccumulateDay(signInfo.Round, signInfo.Days, (_r) =>
                    {
                        if (!isNullGo)
                            OnRefreshSignList();
                    });
                }
            });
        }

        return item;
    }

    private TimeItem _TouchTime_SignDayAccumulate;
    private void ClearTouch_SignDayAccumulate()
    {
        if (_TouchTime_SignDayAccumulate != null)
        {
            GameHelper.Instance.RemoveInvoke(_TouchTime_SignDayAccumulate);
            _TouchTime_SignDayAccumulate = null;
        }
    }
    #endregion


    private List<TaskInfo> _OneClassTaskInfo;

    //活跃奖励
    private List<TaskInfo> _ProClassTaskInfo;

    private TaskClass _TaskClass;

    private void OnClickSignButton()
    {
        GetView().signDayGo.SetActive(true);
        GetView().taskViewGo.SetActive(false);
        GetView().titleText.text = names[0];

        for (int i = 0; i < GetView().root_topBtns.childCount; i++)
        {
            GetView().root_topBtns.GetChild(i).Find("On").gameObject.SetActive(i == 0);
        }

        OnRefreshSignList();
    }

    //任务分类按钮点击
    private void OnTaskClassBtnClick(TaskClass tClass)
    {
        if (tClass == TaskClass.INVALID_TaskClass)
            return;

        _TaskClass = tClass;

        GetView().titleText.text = names[(int)tClass];
        GetView().root_topBtns.GetChild(0).Find("On").gameObject.SetActive(false);
        for (int i = 1; i < GetView().root_topBtns.childCount; i++)
        {
            GetView().root_topBtns.GetChild(i).Find("On").gameObject.SetActive(tClass == (TaskClass)i);
        }

        GetView().signDayGo.SetActive(false);
        GetView().taskViewGo.SetActive(true);

        switch (tClass)
        {
            case TaskClass.DAILY:
                _ProClassTaskInfo = TaskM.Instance.GetTasksByClass(TaskClass.DAILY_ACTIVITY);
                break;
            case TaskClass.WEEKLY:
                _ProClassTaskInfo = TaskM.Instance.GetTasksByClass(TaskClass.WEEKLY_ACTIVITY);
                break;
            default:
                break;
        }
        _OneClassTaskInfo = TaskM.Instance.GetTasksByClass((TaskClass)(int)tClass);

        GetView().obj_DayAndWeek.SetActive(tClass == TaskClass.DAILY || tClass == TaskClass.WEEKLY);
        // 修改top值
        Vector2 offsetMax = GetView().InfiniteScroll_Rect.offsetMax;
        offsetMax.y = (tClass == TaskClass.DAILY || tClass == TaskClass.WEEKLY) ? -338 : -206; // 注意这里使用负值，因为top是从顶部向下计算的
        GetView().InfiniteScroll_Rect.offsetMax = offsetMax;

        if (tClass == TaskClass.DAILY || tClass == TaskClass.WEEKLY)
        {
            SetProInfo();
        }

        if(GetView().InfiniteScroll_Task.ItemTotalCount != _OneClassTaskInfo.Count)
            GetView().InfiniteScroll_Task.SetListItemCount(_OneClassTaskInfo.Count);
        GetView().InfiniteScroll_Task.RefreshAllShownItem();

    }

    private string ChatAtlas = "Common/Atlas/Shop";

    //设置活跃显示
    private void SetProInfo()
    {
        var info = _ProClassTaskInfo[_ProClassTaskInfo.Count - 1];
        GetView().txt_HyAll.text = _TaskClass == TaskClass.DAILY ? $"今日\n活跃值\n" : $"本周\n活跃值\n";
        GetView().txt_HyAllNum.text = info.Target.Amount.ToString();
        GetView().slider_Pro.value = (float)info.Target.Amount / info.Target.TargetNum;

        for (int i = 0; i < _ProClassTaskInfo.Count; i++)
        {
            JUIExtensions.txt(GetView().root_ProBtn.GetChild(i).Find("img_txt/txt")).text = _ProClassTaskInfo[i].Target.TargetNum.ToString();

            int idx = ((int)_ProClassTaskInfo[i].Status - 1) * 5 + i + 2;
            ResouseHelper.Instance.LoadSpriteByAtlas(ChatAtlas, "icon_" + idx, JUIExtensions.img(GetView().root_ProBtn.GetChild(i)));

            JUIExtensions.btn(GetView().root_ProBtn.GetChild(i)).onClick.RemoveAllListeners();
            var _TaskInfo = _ProClassTaskInfo[i];
            JUIExtensions.btn(GetView().root_ProBtn.GetChild(i)).interactable = _TaskInfo.Status != TaskStatus.REWARDED;
            if (_TaskInfo.Status != TaskStatus.REWARDED)
            {
                int taskId = _ProClassTaskInfo[i].CfgId;
                JUIExtensions.btn(GetView().root_ProBtn.GetChild(i)).onClick.AddListener(() => {

                    if (_TaskInfo.Status == TaskStatus.COMPLETED)
                        OnTaskGet(taskId);
                    else
                    {
                        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                        OpenAwardUI(taskId);
                    }
                
                });
            }
        }
    }

    //任务领取
    private void OnTaskGet(int taskId)
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        TcpC.C2S_TaskComplate(taskId);
    }

    private void OnJumpUI(TargetType taskTarget)
    {
        switch (taskTarget)
        {
            case TargetType.ROLE_POWER_REQUIREMENT:
                break;
            case TargetType.KILL_MONSTER_REQUIREMENT:
                break;
            case TargetType.ROLE_LEVEL_REQUIREMENT:
                break;
            case TargetType.PVE_INSTANCE_ENTRY_REQUIREMENT:
                break;
            case TargetType.TOTAL_PHYSICAL_STRENGTH_CONSUMED_REQUIREMENT:
                break;
            case TargetType.EQUIPMENT_STRENGTHENING_LEVEL_REQUIREMENT:
                break;
            case TargetType.EQUIPMENT_UPGRADING_STAR_LEVEL_REQUIREMENT:
                break;
            case TargetType.SKILL_UPGRADING_REQUIREMENT:
                break;
            case TargetType.TOTAL_GOLD_COINS_CONSUMED_REQUIREMENT:
                break;
            default:
                break;
        }
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
        OnBindRed();
        _TaskClass = TaskClass.INVALID_TaskClass;
        OnClickSignButton();
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_Refresh(GameEvent.Refresh_Task _e)
    {
        if (isNullGo) return;

        if (uiGo.activeSelf)
        {
            OnTaskClassBtnClick(_TaskClass);
        }
    }

    public override void OnExit()
    {
        ClearRed();
        JEvent.defaultEvent.Unregister(this);
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}