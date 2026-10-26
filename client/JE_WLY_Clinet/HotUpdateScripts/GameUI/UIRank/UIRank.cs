using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Tsp;

using HotUpdateScripts;
using HotUpdateScripts.ProtoM;

using JEngine.Core;
using JEngine.Helper;
using JEngine.UI;

using MetaFramework.UI;

using SuperScrollView;

using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

public class UIRank : UIBase<UIModelRank, UIViewRank>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIRank>();
        });

        for (int i = 0; i < GetView().root_btns.childCount; i++)
        {
            var trans = GetView().root_btns.GetChild(i);
            int idx = i;
            trans.GetComponent<Button>().onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnRankShow(rankClasses[idx]);
            });
        }

        GetView().InfiniteScroll_Rank.InitListView(0, OnFillRank);
        OnBindRed();
    }

    private CFG_RankingAward.RankClass[] rankClasses =
    {
        CFG_RankingAward.RankClass.Power,
        CFG_RankingAward.RankClass.ZhanHun,//即时赛 PVP
        //CFG_RankingAward.RankClass.SiLian,
        CFG_RankingAward.RankClass.TurnBasedDay,//即时赛 PVP
    };

    //private Dictionary<CFG_RankingAward.RankClass, CFG_RankingAward.RankClass> RankAwardDayDic = new Dictionary<CFG_RankingAward.RankClass, CFG_RankingAward.RankClass>
    //{
    //    [CFG_RankingAward.RankClass.ZhanHun] = CFG_RankingAward.RankClass.RealTimeDay,
    //};

    private Dictionary<CFG_RankingAward.RankClass, CFG_RankingAward.RankClass> RankAwardWeekDic = new Dictionary<CFG_RankingAward.RankClass, CFG_RankingAward.RankClass>
    {
        [CFG_RankingAward.RankClass.Power] = CFG_RankingAward.RankClass.PowerWeek,
        [CFG_RankingAward.RankClass.ZhanHun] = CFG_RankingAward.RankClass.RealTimeWeek,
        [CFG_RankingAward.RankClass.TurnBasedDay] = CFG_RankingAward.RankClass.TurnBasedWeek,
    };

    private Dictionary<CFG_RankingAward.RankClass, RedPointConst> RedDic = new Dictionary<CFG_RankingAward.RankClass, RedPointConst>
    {
        //[CFG_RankingAward.RankClass.Level] = RedPointConst.Rank_Level,
        [CFG_RankingAward.RankClass.Power] = RedPointConst.Rank_Power,
        //[CFG_RankingAward.RankClass.SiLian] = RedPointConst.Rank_SiLian,
        [CFG_RankingAward.RankClass.ZhanHun] = RedPointConst.Rank_ZhanHun,
        [CFG_RankingAward.RankClass.TurnBasedDay] = RedPointConst.Rank_HuiHe,
    };

    private void OnBindRed()
    {
        for (int i = 0; i < RedDic.Count; i++)
        {
            int idx = i;
            RedPointManager.Instance.RPS.Register(RedDic.ElementAt(idx).Value.ToString(), (node) =>
            {
                GetView().root_btns.GetChild(idx).Find("dot").gameObject.SetActive(node.pointCount > 0);
            });
        }
    }

    private void ClearRed()
    {
        foreach (var item in RedDic)
        {
            RedPointManager.Instance.RPS.Register(item.Value.ToString(), null);
        }
    }

    private void ShowRed()
    {
        foreach (var item in RedDic)
        {
            RedPointManager.Instance.RPS.Dispatch(item.Value.ToString(), RedPointManager.Instance.GetRedNum(item.Value.ToString()));
        }
    }

    private Dictionary<int, CFGItem_RankingAward> DicCfgRank;
    private HTTP_RankDetail RankListInfo;

    private CFG_RankingAward.RankClass selectRank = CFG_RankingAward.RankClass.Power;

    private List<UIBoxInfoShow.Buttons> awardTypesButton = new List<UIBoxInfoShow.Buttons>
    {
        new UIBoxInfoShow.Buttons { Name = "日结奖励" },
        new UIBoxInfoShow.Buttons { Name = "周结奖励" },
    };

    private void OpenAwardUI(int Rankidx)
    {
        if (Rankidx <= 0)
            return;

        // 日结奖励
        List<UIBoxInfoShow.BoxItem[]> items = new List<UIBoxInfoShow.BoxItem[]>();
        UIBoxInfoShow.BoxItem[] boxItemsDay = new UIBoxInfoShow.BoxItem[DicCfgRank[Rankidx].award.Length];
        for (int j = 0; j < DicCfgRank[Rankidx].award.Length; j++)
        {
            boxItemsDay[j] = new UIBoxInfoShow.BoxItem { Id = int.Parse(DicCfgRank[Rankidx].award[j].ElementAt(0).Key), Num = DicCfgRank[Rankidx].award[j].ElementAt(0).Value };
        }

        // 周结奖励
        var _DicCfgRank = CFG_RankingAward.GetRankDicByRankClass(RankAwardWeekDic[selectRank]);
        UIBoxInfoShow.BoxItem[] boxItemsWeek = new UIBoxInfoShow.BoxItem[_DicCfgRank[Rankidx].award.Length];
        for (int j = 0; j < _DicCfgRank[Rankidx].award.Length; j++)
        {
            boxItemsWeek[j] = new UIBoxInfoShow.BoxItem { Id = int.Parse(_DicCfgRank[Rankidx].award[j].ElementAt(0).Key), Num = _DicCfgRank[Rankidx].award[j].ElementAt(0).Value };
        }

        items.Add(boxItemsDay);
        items.Add(boxItemsWeek);

        string tip = "提示：日结奖励每晚22点发放；周结奖励每周一0点发放。";
        UITool.Open<UIBoxInfoShow>(null, awardTypesButton, items, tip);
    }

    private void OnRankShow(CFG_RankingAward.RankClass rankClass)
    {
        bool hasKey = RedDic.ContainsKey(rankClass);
        if (hasKey)
        {
            RedPointManager.Instance.SetRedClose(RedDic[rankClass].ToString(), () =>
            {
                RedPointManager.Instance.RPS.Dispatch(RedDic[rankClass].ToString(), RedPointManager.Instance.GetRedNum(RedDic[rankClass].ToString()));
            });
        }

        HttpC.GetRankList(rankClass, (_r) =>
        {
            if (isNullGo)
                return;

            selectRank = rankClass;

            for (int i = 0; i < GetView().root_btns.childCount; i++)
            {
                GetView().root_btns.GetChild(i).Find("Select").gameObject.SetActive(selectRank == rankClasses[i]);
            }

            RankListInfo = _r.data;

            //if (rankClass == CFG_RankingAward.RankClass.ZhanHun)
            //    DicCfgRank = CFG_RankingAward.GetRankDicByRankClass(RankAwardDayDic[rankClass]);
            //else
            //    DicCfgRank = CFG_RankingAward.GetRankDicByRankClass(rankClass);

            DicCfgRank = CFG_RankingAward.GetRankDicByRankClass(rankClass);

            OnRefreshUIShow();

            //RedDotM.Instance.SetRandRedNumByType(RedDic[rankClass].ToString(), () =>
            //{
            //    RedPointManager.Instance.RPS.Dispatch(RedDic[rankClass].ToString(), RedDotM.Instance.GetRandRedNumByType(RedDic[rankClass].ToString()));
            //});
        });

    }

    private void OnRefreshUIShow()
    {
        for (int i = 0; i < 3; i++)
        {
            HTTP_RankUserInfo userInfo = RankListInfo.rank_all.Length > i ? RankListInfo.rank_all[i] : default;
            OnRankItemShow(userInfo, (i + 1), GetView().root_top3.Find("Rank_" + (i + 1)).gameObject, RankListInfo.rank_all.Length <= i);
        }

        OnRankItemShow(RankListInfo.rank_own, RankListInfo.rank_own.num, GetView().root_main.Find("Rank_my").gameObject);

        GetView().InfiniteScroll_Rank.SetListItemCount(96, true);
        GetView().InfiniteScroll_Rank.RefreshAllShownItem();
        GetView().InfiniteScroll_Rank.MovePanelToItemIndex(0, 0);
    }

    private void OnRankItemShow(HTTP_RankUserInfo useInfo, int Rankidx, GameObject go, bool isNull = false)
    {
        JUIExtensions.txt(go.transform.Find("txt_Rank")).text = Rankidx <= 0 ? "未上榜" : Rankidx.ToString();

        for (int i = 0; i < 1; i++)
        {
            go.transform.Find("root_award").gameObject.SetActive(!isNull && DicCfgRank.ContainsKey(Rankidx));
            if (go.transform.Find("root_award/award_" + i) != null && DicCfgRank.ContainsKey(Rankidx))
            {
                go.transform.Find("root_award/award_" + i).gameObject.SetActive(Rankidx > 0 && DicCfgRank[Rankidx].award.Length > i);
                if (Rankidx > 0 && DicCfgRank[Rankidx].award.Length > i)
                {
                    GameUtil.Instance.SetItemIcon(int.Parse(DicCfgRank[Rankidx].award[i].ElementAt(0).Key), JUIExtensions.img(go.transform.Find("root_award/award_" + i + "/icon")));
                    JUIExtensions.txt(go.transform.Find("root_award/award_" + i + "/txt_Num")).text = GameUtil.Instance.FormatNumWithUnit(DicCfgRank[Rankidx].award[i].ElementAt(0).Value);
                }
            }
        }

        go.transform.Find("HeadBG").gameObject.SetActive(!isNull);
        go.transform.Find("img_des").gameObject.SetActive(!isNull);

        if (go.transform.Find("emptySeat"))
            go.transform.Find("emptySeat").gameObject.SetActive(isNull);

        if (isNull)
        {
            JUIExtensions.txt(go.transform.Find("txt_Name")).text = "";
            JUIExtensions.txt(go.transform.Find("img_des/txt_des")).text = "";
        }
        else
        {
            var HeadBtn = JUIExtensions.btn(go.transform.Find("HeadBG"));
            HeadBtn.onClick.RemoveAllListeners();
            HeadBtn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                if (useInfo.uid > 0 && useInfo.uid != RoleM.Instance.Uid)
                {
                    TcpC.C2S_ObservePlayerInfoRequest(useInfo.uid);
                }
            });
            JUIExtensions.txt(go.transform.Find("txt_Name")).text = useInfo.name;
            JUIExtensions.txt(go.transform.Find("img_des/txt_des")).text = GetStrValueDes(useInfo.value);

            int job = useInfo.job;
            int sex = useInfo.sex;
            if (Rankidx == 0)
            {
                job = RoleM.Instance.Job;
                sex = RoleM.Instance.Sex;
                JUIExtensions.txt(go.transform.Find("txt_Name")).text = RoleM.Instance.Nickname;
            }
            ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.RoleHeadAtlas, "img_" + job + sex, JUIExtensions.img(go.transform, "HeadBG/img_head"));
        }

        JUIExtensions.btn(go.transform.Find("root_award/btn_info")).onClick.RemoveAllListeners();
        JUIExtensions.btn(go.transform.Find("root_award/btn_info")).onClick.AddListener(delegate
        {
            OpenAwardUI(Rankidx);
        });
    }

    private string GetStrValueDes(int value)
    {
        string str = "";
        switch (selectRank)
        {
            case CFG_RankingAward.RankClass.Level:
                str = "等级  ";
                break;
            case CFG_RankingAward.RankClass.Power:
                str = "战力  ";
                break;
            case CFG_RankingAward.RankClass.ZhanHun:
                str = "积分  ";
                break;
            case CFG_RankingAward.RankClass.SiLian:
                str = "最高层数  ";
                break;
            default:
                break;
        }
        if (selectRank == CFG_RankingAward.RankClass.Power)
            str += GameUtil.Instance.FormatPowerWithUnit(value);
        else
            str += GameUtil.Instance.FormatNumWithUnit(value);
        return str;
    }

    private LoopListViewItem2 OnFillRank(LoopListView2 loopListView, int idx)
    {
        LoopListViewItem2 item = loopListView.NewListViewItem("Item");
        GameObject go = item.gameObject;
        int rankIdx = idx + 3;
        HTTP_RankUserInfo userInfo = RankListInfo.rank_all.Length > rankIdx ? RankListInfo.rank_all[rankIdx] : default;
        OnRankItemShow(userInfo, rankIdx + 1, go, RankListInfo.rank_all.Length <= rankIdx);
        return item;
    }

    private TimeItem RefreshUILoop;

    public override void OnEnter(params object[] args)
    {
        ShowRed();
        if (args.Length > 0)
            selectRank = (CFG_RankingAward.RankClass)(args[0]);
        else
            selectRank = CFG_RankingAward.RankClass.Power;

        GetView().txt_Desc.text = $"榜单每5分钟更新一次。日结奖励每晚22点发放；周结奖励每周一0点发放。";
        RefreshUILoop = GameHelper.Instance.AddInvoke(new TimeItem(0, 300, () =>
        {
            if (isNullGo)
                return;

            OnRankShow(selectRank);
        }, _isLoop: true));
    }

    public override void OnExit()
    {
        GameHelper.Instance.RemoveInvoke(RefreshUILoop);
        RefreshUILoop = null;
    }

    public override void OnDestroy()
    {
        ClearRed();
    }
}