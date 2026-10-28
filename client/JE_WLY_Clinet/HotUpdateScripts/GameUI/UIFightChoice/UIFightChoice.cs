using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using pb;
using System;
using SuperScrollView;
using JEngine.Event;
using JEngine.MyNet;
using HotUpdateScripts;

public class UIFightChoice : UIBase<UIModelFightChoice, UIViewFightChoice>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    //一级选择
    public enum SelectType { Challange, Limit, FuBen }
    private SelectType selectType = SelectType.Challange;

    //二级选择
    public enum ChanllengeType 
    {
        /// <summary>
        /// 战役
        /// </summary>
        Fight,

        /// <summary>
        /// 首领
        /// </summary>
        Boss, 

        /// <summary>
        /// 试练塔
        /// </summary>
        SLTower, 

        /// <summary>
        /// 遗迹
        /// </summary>
        Relic, 

        /// <summary>
        /// 乱斗之王
        /// </summary>
        PvP,

        /// <summary>
        /// 世界BOSS
        /// </summary>
        WorldBoss,

        /// <summary>
        /// 一战到底
        /// </summary>
        FightEnd,

        /// <summary>
        /// 每日必做
        /// </summary>
        DayDo,

        /// <summary>
        /// 守卫海岛
        /// </summary>
        HaiDao
    }
    private ChanllengeType chanllengeType = ChanllengeType.Fight;

    //页面标题
    private Dictionary<ChanllengeType, string> ChanllengeToStrDic = new Dictionary<ChanllengeType, string>()
    {
        { ChanllengeType.Fight, "战役" },
        { ChanllengeType.Boss, "首领" },
        { ChanllengeType.SLTower, "试炼\n塔" },
        { ChanllengeType.Relic, "遗迹" },
        { ChanllengeType.PvP, "乱斗\n之王" },
        { ChanllengeType.WorldBoss, "世界\nBOSS" },
        { ChanllengeType.FightEnd, "一战\n到底" },
        { ChanllengeType.DayDo, "每日\n必做" },
        { ChanllengeType.HaiDao, "挂机\n升级" },
    };

    private Dictionary<ChanllengeType, CFG_XMLConfig.XML_Config.SwitchOpne> ChanllengeToSwitchDic = new ()
    {
        [ChanllengeType.Fight] = CFG_XMLConfig.XML_Config.SwitchOpne.Play,
        [ChanllengeType.Boss] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_Boss,
        [ChanllengeType.SLTower] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_SlTower,
        [ChanllengeType.Relic] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_Relice,
        [ChanllengeType.PvP] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_PVP,
        [ChanllengeType.DayDo] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_QHSAndGold,
    };

    //二级按钮对应的挑战类型
    private Dictionary<SelectType, ChanllengeType[]> SelectToChallengeDic = new ()
    {
        { SelectType.Challange, new ChanllengeType[] { ChanllengeType.Fight, ChanllengeType.Boss, ChanllengeType.SLTower, ChanllengeType.Relic, ChanllengeType.HaiDao } },
        { SelectType.Limit, new ChanllengeType[] { ChanllengeType.PvP, ChanllengeType.WorldBoss, ChanllengeType.FightEnd } },
        { SelectType.FuBen, new ChanllengeType[] { ChanllengeType.DayDo } }
    };

    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIFightChoice>();
        });

        //主选择
        for (int i = 0; i < GetView().root_SelectBtns.childCount; i++)
        {
            int idx = i;
            JUIExtensions.btn(GetView().root_SelectBtns.GetChild(idx)).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnSlectBtnClick(idx);
            });
        }

        //挑战二级页面点击
        for (int i = 0; i < GetView().root_ChallengeBtns.childCount; i++)
        {
            int idx = i;
            JUIExtensions.btn(GetView().root_ChallengeBtns.GetChild(idx)).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnChallengeBtnClick((int)SelectToChallengeDic[selectType][idx]);
            });
        }

        GetView().btn_ZYPlay.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            GameHelper.Instance.StarGame(BattleId);
        });

        GetView().btn_FightAward.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIFightChapterAward>();
        });

        //试炼塔
        GetView().InfiniteScroll_SLTower.InitListView(0, OnFillSLTowerItem);

        GetView().btn_SLTower_Paly.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (FightPlayM.Instance.GetInfo().trial_tower_map.BattleId == NextSLTowerId)
            {
                UITool.ShowTips("已经达到最高层 ");
                return;
            }
            GameHelper.Instance.StarGame(NextSLTowerId);
        });
        //打开失试炼基金
        GetView().btn_SLTower_SLShop.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIShop>(null, CFG_Store.StoreType.SiLian);
        });

        GetView().btn_SLTower_SLJJ.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UITrialFund>();
        });

        //首领
        GetView().InfiniteScroll_SL.InitListView(0, OnFillBossItem) ;

        GetView().btn_SL_CountAdd.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (StoreM.Instance.GetCanBuyNum((int)CFG_Store.StroeID.BossChange) == 0)
            {
                UITool.ShowTips("今日购买已达到上限");
                return;
            }
            UITool.Open<UIGameSweepAndBuy>(null,UIGameSweepAndBuy.OpType.BossBuy);
        });

        GetView().btn_PVP_ZhanHun.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIShop>(null, CFG_Store.StoreType.Diamanta);
        });

        //遗迹
        GetView().btn_YJ_Add.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnRelicShow(RelicIdx +1);
        });
        GetView().btn_YJ_Jian.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnRelicShow(RelicIdx - 1);
        });

        GetView().btn_YJ_CountAdd.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (StoreM.Instance.GetCanBuyNum((int)CFG_Store.StroeID.RelicChange) == 0)
            {
                UITool.ShowTips("今日购买已达到上限");
                return;
            }
            UITool.Open<UIGameSweepAndBuy>(null, UIGameSweepAndBuy.OpType.RelicBuy);
        });

        GetView().btn_YJ_Play.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            GameHelper.Instance.StarGame(BattleId);
        });

        GetView().btn_YJ_SD.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIRelicChallange>(null, BattleId, RelicSlectID);
        });


        GetView().btn_help.onClick.AddListener(OnHelpOnClick);
    }

    private void HedeAllChallengeButtonRed()
    {
        for (int i = 0; i < GetView().root_ChallengeBtns.childCount; i++)
            GetView().root_ChallengeBtns.GetChild(i).Find("dot").gameObject.SetActive(false);

    }

    private void OnBindRed()
    {
        RedPointManager.Instance.RPS.Register(RedPointConst.Challenge.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().root_SelectBtns.GetChild(0).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Limet.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().root_SelectBtns.GetChild(1).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.DayDo.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().root_SelectBtns.GetChild(2).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Challenge_Fight.ToString(), (node) =>
        {
            if (isNullGo || selectType != SelectType.Challange) return;
            GetView().root_ChallengeBtns.GetChild(0).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Challenge_Boss.ToString(), (node) =>
        {
            if (isNullGo || selectType != SelectType.Challange) return;
            GetView().root_ChallengeBtns.GetChild(1).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Challenge_Tower.ToString(), (node) =>
        {
            if (isNullGo || selectType != SelectType.Challange) return;
            GetView().root_ChallengeBtns.GetChild(2).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Challenge_Relice.ToString(), (node) =>
        {
            if (isNullGo || selectType != SelectType.Challange) return;
            GetView().root_ChallengeBtns.GetChild(3).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Challenge_OnHook.ToString(), (node) =>
        {
            if (isNullGo || selectType != SelectType.Challange) return;
            GetView().root_ChallengeBtns.GetChild(4).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Limet.ToString(), (node) =>
        {
            if (isNullGo || selectType != SelectType.Limit) return;
            GetView().root_ChallengeBtns.GetChild(0).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.DayDo.ToString(), (node) =>
        {
            if (isNullGo || selectType != SelectType.FuBen) return;
            GetView().root_ChallengeBtns.GetChild(0).Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

        //RedPointManager.Instance.RPS.Register(RedPointConst.Challenge_Tower_Award.ToString(), (node) =>
        //{
        //    if (isNullGo || selectType != SelectType.Challange) return;
        //    GetView().btn_SLTower_SLJJ.transform.Find("dot").gameObject.SetActive(node.pointCount > 0);
        //});

        RedPointManager.Instance.RPS.Register(RedPointConst.Challenge_Fight_Award.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().btn_FightAward.transform.Find("dot").gameObject.SetActive(node.pointCount > 0);
        });

    }


    private void OnHelpOnClick()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        int helpIdx = 0;
        switch (chanllengeType)
        {
            case ChanllengeType.Fight:
                helpIdx = 26;
                break;
            case ChanllengeType.Boss:
                helpIdx = 27;
                break;
            case ChanllengeType.SLTower:
                helpIdx = 28;
                break;
            case ChanllengeType.Relic:
                helpIdx = 29;
                break;
            case ChanllengeType.PvP:
                break;
            case ChanllengeType.DayDo:
                helpIdx = 18;
                break;
            default:
                break;
        }
        UITool.Open<UIComRule>(null, helpIdx);
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
        selectType = args.Length > 0 ? (SelectType)args[0] : SelectType.Challange;
        chanllengeType = args.Length > 1 ? (ChanllengeType)args[1] : ChanllengeType.Fight;
        OnSlectBtnClick((int)selectType,true);
        SetTagButtonLockState();
    }

    private void SetTagButtonLockState()
    {
        for (int i = 0; i < GetView().root_SelectBtns.childCount; i++) 
        {
            if (i == 2)
            {
                Transform mask = GetView().root_SelectBtns.GetChild(i).Find("Mask");
                mask.gameObject.SetActive(GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_QHSAndGold, false));
                if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_QHSAndGold, false))
                {
                    JUIExtensions.txt(mask.Find("txt")).text = $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Play_QHSAndGold)}天后开启";
                }
            }
        }
    }

    private void OnSlectBtnClick(int idx,bool Jump = false)
    {
        if (SelectToChallengeDic[(SelectType)idx].Length <= 0)
        {
            UITool.ShowTips("暂未开放");
            return;
        }

        if (!Jump)
            chanllengeType = SelectToChallengeDic[(SelectType)idx][0];
        bool isLock = false;
        if(idx == 2)
        {
            isLock = GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_QHSAndGold);
            if (!isLock) 
            {
                isLock = ChanllengeToSwitchDic.ContainsKey(chanllengeType) && GameUtil.Instance.CheakIsLock(ChanllengeToSwitchDic[chanllengeType]);
            }
        }
        else
        {
            isLock = ChanllengeToSwitchDic.ContainsKey(chanllengeType) && GameUtil.Instance.CheakIsLock(ChanllengeToSwitchDic[chanllengeType]);
        }

        if (ChanllengeToSwitchDic.ContainsKey(chanllengeType) && isLock)
            return;

        //二级按钮显示状态

        for (int i = 0; i < GetView().root_ChallengeBtns.childCount; i++)
        {
            GetView().root_ChallengeBtns.GetChild(i).gameObject.SetActive(i < SelectToChallengeDic[(SelectType)idx].Length);
            if (i < SelectToChallengeDic[(SelectType)idx].Length)
            {
                JUIExtensions.txt(GetView().root_ChallengeBtns.GetChild(i), "Label").text = ChanllengeToStrDic[SelectToChallengeDic[(SelectType)idx][i]];
            }
        }

        //默认选中
        selectType = (SelectType)idx;
        for (int i = 0; i < GetView().root_SelectBtns.childCount; i++)
        {
            GetView().root_SelectBtns.GetChild(i).Find("Select").gameObject.SetActive(idx == i);
        }

        //二级按钮选择
        OnChallengeBtnClick((int)chanllengeType);
    }

    private UIDayDo _UIDayDo;
    //二级页面点击
    private void OnChallengeBtnClick(int idx, bool isRefresh = false)
    {
        if (ChanllengeToSwitchDic.ContainsKey((ChanllengeType)idx) && (GameUtil.Instance.CheckOpenServerIsLock(ChanllengeToSwitchDic[(ChanllengeType)idx], !isRefresh) || GameUtil.Instance.CheakIsLock(ChanllengeToSwitchDic[(ChanllengeType)idx], !isRefresh)))
            return;

        chanllengeType = (ChanllengeType)idx;
        for (int i = 0; i < SelectToChallengeDic[selectType].Length; i++)
        {
            GetView().root_ChallengeBtns.GetChild(i).Find("Select").gameObject.SetActive(chanllengeType == SelectToChallengeDic[selectType][i]);
        }

        GetView().txt_title.text = ChanllengeToStrDic[chanllengeType].Replace("\n", "");
        GetView().btn_help.gameObject.SetActive(chanllengeType != ChanllengeType.PvP && chanllengeType != ChanllengeType.HaiDao  );
        GetView().obj_SL.SetActive(chanllengeType == ChanllengeType.Boss);
        GetView().obj_SLTower.SetActive(chanllengeType == ChanllengeType.SLTower);
        GetView().obj_ZY.SetActive(chanllengeType == ChanllengeType.Fight);
        GetView().obj_YJ.SetActive(chanllengeType == ChanllengeType.Relic);
        GetView().obj_PvP.SetActive(chanllengeType == ChanllengeType.PvP);

        GetView().Obj_HaiDao.gameObject.SetActive(chanllengeType == ChanllengeType.HaiDao);

        if (_UIDayDo != null &&  chanllengeType != ChanllengeType.DayDo)
            _UIDayDo.OnShow(false);

        switch (chanllengeType)
        {
            case ChanllengeType.Fight:
                OnFightUI();

                RedPointManager.Instance.SetRedClose(RedPointConst.Challenge_Fight_Click.ToString(), () => {
                    RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_Fight_Click);
                });
                break;
            case ChanllengeType.Boss:
                OnBossUI();
                break;
            case ChanllengeType.SLTower:
                OnSLTowerUI();

                RedPointManager.Instance.SetRedClose(RedPointConst.Challenge_Tower_Click.ToString(), () => {
                    RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_Tower_Click);
                });
                break;
            case ChanllengeType.Relic:
                OnRelicUI(isRefresh);
                break;

            case ChanllengeType.PvP:
                // 单机版不做乱斗之王（PVP 匹配/录像，需要真实对手）
                UITool.ShowTips("暂未开放");
                break;
            case ChanllengeType.WorldBoss:
                // 单机版不做世界Boss（异步多人活动，全服共享 100 条血，单人无法成立）
                UITool.ShowTips("暂未开放");
                break;
            case ChanllengeType.FightEnd:
                // 单机版不做一战到底（YZDD 1v1 异步 PVP）
                UITool.ShowTips("暂未开放");
                break;
            case ChanllengeType.DayDo:

                if (_UIDayDo == null)
                {
                    UITool.LoadUII<UIDayDo>(GetView().root_UI, (ui) => {
                         _UIDayDo = (UIDayDo)ui ;
                         _UIDayDo.OnShow(true);
                     });
                }
                else
                {
                    _UIDayDo.OnShow(true);
                }
                break;
            case ChanllengeType.HaiDao:
                OnHaiDaoUI();
                break;
            default:
                break;
        }
        HedeAllChallengeButtonRed();
        OnBindRed();
    }

    #region 挑战


    private int BattleId = 0;
    //战役页面
    private void OnFightUI()
    {
        var fightInfo = FightPlayM.Instance.GetInfo().battle_map;
        var CFGBattle = CFG_Battle.GetNextFightById(fightInfo.BattleId);
        for (int i = 0; i < GetView().root_ZYAward.childCount; i++)
        { 
            int idx = i;
            var go = GetView().root_ZYAward.GetChild(idx);
            go.gameObject.SetActive(idx < CFGBattle.Preview.Length);
            if (idx < CFGBattle.Preview.Length)
            {
                GameUtil.Instance.SetItemIcon(CFGBattle.Preview[idx], JUIExtensions.img(go, "icon"), JUIExtensions.img(go));
                JUIExtensions.btn(go).onClick.RemoveAllListeners();
                JUIExtensions.btn(go).onClick.AddListener(() => { 
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, CFGBattle.Preview[idx]);
                } );
            }
        }

        var CFGMonster = CFG_Monster.GetById((int)CFGBattle.MonsterShow);
        ResouseHelper.Instance.LoadSpriteByAtlas(CFGMonster.Photo, CFGMonster.Photoname, GetView().img_ZYBoss);
        GetView().txt_ZYName.text = "关卡" + CFGBattle.Chapter + "-" + CFGBattle.Layers;
        GetView().txt_ZYlimet.text = $"角色达到{(RoleM.Instance.Level < CFGBattle.Levellimit ? $"<color=#DA4E38>{CFGBattle.Levellimit}</color>" : CFGBattle.Levellimit)}级";
        GetView().btn_ZYPlay.interactable = RoleM.Instance.Level >= CFGBattle.Levellimit && fightInfo.BattleId != CFGBattle.UID;
        BattleId = CFGBattle.UID;
    }


    private List<CFGItem_Battle> fightList;
    private int NextSLTowerId = 0;
    //试炼塔
    private void OnSLTowerUI()
    {
        var fightInfo = FightPlayM.Instance.GetInfo().trial_tower_map;
        fightList = CFG_Battle.GetSLTowerList();

        NextSLTowerId = fightList[0].UID;
        int moveIdx = 0;
        for (int i = 0; i < fightList.Count; i++)
        {
            if (fightList[i].UID > fightInfo.BattleId)
            {
                moveIdx = i;
                NextSLTowerId = fightList[i].UID;
            }
        }

        if(GetView().InfiniteScroll_SLTower.ItemTotalCount != fightList.Count)
            GetView().InfiniteScroll_SLTower.SetListItemCount(fightList.Count);
        else
            GetView().InfiniteScroll_SLTower.RefreshAllShownItem();

        GetView().InfiniteScroll_SLTower.MovePanelToItemIndex(moveIdx, 0);

        //GetView().btn_SLTower_Paly.interactable = NextSLTowerId != fightInfo.BattleId;

    }

    //试炼塔Item
    private LoopListViewItem2 OnFillSLTowerItem(LoopListView2 listView,int index)
    {
        LoopListViewItem2 viewitem = listView.NewListViewItem("Item");
        Transform item = viewitem.transform;

        var cfgInfo = fightList[index];
        JUIExtensions.txt(item, "bg_top/txt_Name").text = "第" + cfgInfo.Layers + "层";
        JUIExtensions.txt(item, "txt_limit").text = GameUtil.Instance.FormatNumWithUnit(cfgInfo.Powerlimit);

        JUIExtensions.txt(item,"txt_limit").color = JUIExtensions.ToColor(cfgInfo.Powerlimit > RoleM.Instance.TotalCombat ? "#E96C5C" : "#88DF5F");
        JUIExtensions.outline(item, "txt_limit").effectColor = JUIExtensions.ToColor(cfgInfo.Powerlimit > RoleM.Instance.TotalCombat ? "#9E2522" : "#42712C");

        var root_award = item.transform.Find("awards");
        for (int i = 0; i < root_award.childCount; i++)
        {
            int idx = i;
            var go = root_award.GetChild(idx);
            go.gameObject.SetActive(idx < cfgInfo.Preview.Length);
            if (idx < cfgInfo.Preview.Length)
            {
                GameUtil.Instance.SetItemIcon(cfgInfo.Preview[idx], JUIExtensions.img(go,"icon"), JUIExtensions.img(go));
                JUIExtensions.btn(go).onClick.RemoveAllListeners();
                JUIExtensions.btn(go).onClick.AddListener(() => {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, cfgInfo.Preview[idx]);
                });
            }
        }

        JUIExtensions.txt(item.transform, "txt_State").text = cfgInfo.UID > NextSLTowerId ? "" :(cfgInfo.UID < NextSLTowerId ? "已通关" :"当前关");
        item.transform.Find("Lock").gameObject.SetActive(cfgInfo.UID > NextSLTowerId);

        var CFGMonster = CFG_Monster.GetById((int)cfgInfo.MonsterShow);
        ResouseHelper.Instance.LoadSpriteByAtlas(CFGMonster.Icon, CFGMonster.Iconname, JUIExtensions.img(item.transform, "img_left/img_boss"));

        ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/FightChoice", cfgInfo.UID > NextSLTowerId ? "bg_titlt_0" : (cfgInfo.UID < NextSLTowerId ? "bg_titlt_2" : "bg_titlt_1"), JUIExtensions.img(item.transform, "img_left"));

        return viewitem;
    }

    private int NextBossId = 0;
    //首领
    private void OnBossUI()
    {
        var fightInfo = FightPlayM.Instance.GetInfo().boss_map;
        fightList = CFG_Battle.BossList;

        NextBossId = fightList[0].UID;
        int moveIdx = 0;
        for (int i = 0; i < fightList.Count; i++)
        {
            if (fightList[i].UID > fightInfo.BattleId)
            {
                moveIdx = i;
                NextBossId = fightList[i].UID;
            }
        }
        if (GetView().InfiniteScroll_SL.ItemTotalCount != fightList.Count)
            GetView().InfiniteScroll_SL.SetListItemCount(fightList.Count);

        GetView().InfiniteScroll_SL.RefreshAllShownItem();

        GetView().InfiniteScroll_SL.MovePanelToItemIndex(moveIdx,0);

        GetView().txt_SL_Count.text = "今日剩余次数:" + fightInfo.ChallengeTime;
    }

    //首领Item
    private LoopListViewItem2 OnFillBossItem(LoopListView2 loopListView,int index)
    {
        LoopListViewItem2 viewitem = loopListView.NewListViewItem("Item");
        GameObject item = viewitem.gameObject;

        var cfgInfo = fightList[index];
        JUIExtensions.txt(item.transform, "txt_Name").text = CFG_Monster.GetById(cfgInfo.Monster[0].id).Name;
        ResouseHelper.Instance.LoadSpriteByAtlas(CFG_Monster.GetById(cfgInfo.Monster[0].id).Icon, CFG_Monster.GetById(cfgInfo.Monster[0].id).Iconname, JUIExtensions.img(item.transform, "bg_head/img"));

        bool isLimit = RoleM.Instance.TotalCombat >= cfgInfo.Powerlimit;
        
        JUIExtensions.txt(item.transform, "txt_limit").text = "战力需求:"+ (isLimit ?  GameUtil.Instance.FormatNumWithUnit(cfgInfo.Powerlimit): $"<color=#DA4E38>{GameUtil.Instance.FormatNumWithUnit(cfgInfo.Powerlimit)}</color>");
        JUIExtensions.txt(item.transform, "txt_State").text = isLimit ? "":"战力未达标";

        var root_award = item.transform.Find("awards");
        for (int i = 0; i < root_award.childCount; i++)
        {
            int idx = i;
            var go = root_award.GetChild(idx);
            go.gameObject.SetActive(idx < cfgInfo.Preview.Length);
            if (idx < cfgInfo.Preview.Length)
            {
                GameUtil.Instance.SetItemIcon(cfgInfo.Preview[idx], JUIExtensions.img(go, "icon"), JUIExtensions.img(go));
                JUIExtensions.btn(go).onClick.RemoveAllListeners();
                JUIExtensions.btn(go).onClick.AddListener(() => {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, cfgInfo.Preview[idx]);
                });
            }
        }

        //ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/FightChoice", cfgInfo.UID > NextSLTowerId ? "bg_titlt_0" : (cfgInfo.UID < NextSLTowerId ? "bg_titlt_2" : "bg_titlt_1"), JUIExtensions.img(item.transform, "img_left"));

        item.transform.Find("btn_Play").gameObject.SetActive(cfgInfo.UID == NextBossId);
        item.transform.Find("btn_sd").gameObject.SetActive(cfgInfo.UID < NextBossId);

        JUIExtensions.btn(item.transform.Find("btn_sd")).onClick.RemoveAllListeners();
        JUIExtensions.btn(item.transform.Find("btn_Play")).onClick.RemoveAllListeners();
        JUIExtensions.btn(item.transform.Find("btn_Play")).onClick.AddListener( () =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            GameHelper.Instance.StarGame(cfgInfo.UID);
        });

        JUIExtensions.btn(item.transform.Find("btn_sd")).onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (FightPlayM.Instance.GetInfo().boss_map.ChallengeTime <= 0)
            {
                UITool.ShowTips("挑战次数不足");
                return;
            }
            UITool.Open<UIGameSweepAndBuy>(null, UIGameSweepAndBuy.OpType.BossSweep, cfgInfo.UID);
            //TcpC.C2S_CopySweep(cfgInfo.UID,1);
        });

        return viewitem;
    }

    private int RelicSlectID = 0;
    //遗迹页面
    private void OnRelicUI(bool isRefsh)
    {
        var fightInfo = FightPlayM.Instance.GetInfo().relic_map;
        GetView().txt_YJ_Num.text = "今日剩余次数:"+ fightInfo.ChallengeTime;
        int cfgID = fightInfo.BattleId == 0 ? CFG_Battle.RelicDic[1][0].UID : fightInfo.BattleId;

        RelicSlectID = cfgID;
        if (fightInfo.BattleId != 0)
        {
            for (int i = 0; i < CFG_Battle.RelicList.Count; i++)
            {
                if (RelicSlectID < CFG_Battle.RelicList[i].UID)
                {
                    RelicSlectID = CFG_Battle.RelicList[i].UID;
                    break;
                }
            }
        }
        
        OnRelicShow(isRefsh && RelicIdx != 0 ? RelicIdx: CFG_Battle.GetById(RelicSlectID).Chapter);
    }

    private int RelicIdx = 0;
    private void OnRelicShow(int pageidx)
    {
        RelicIdx = pageidx;
        GetView().txt_YJ_Name.text = "第" + RelicIdx + "章";
        var cfgPageInfo = CFG_Battle.RelicDic[pageidx];
        var fightInfo = FightPlayM.Instance.GetInfo().relic_map;

        int defaultID = 0;
        //获取每章默认选择
        if (pageidx <= CFG_Battle.GetById(RelicSlectID).Chapter )
        {
            for (int i = 0; i < cfgPageInfo.Count; i++)
            {
                if (FightPlayM.Instance.GetRelicLevelStar(cfgPageInfo[i].UID) < 3)
                {
                    defaultID = cfgPageInfo[i].UID;
                }
                if (RelicSlectID == cfgPageInfo[i].UID)
                {
                    defaultID = cfgPageInfo[i].UID;
                    break;
                }
            }
            defaultID = defaultID == 0 ? cfgPageInfo[cfgPageInfo.Count - 1].UID : defaultID;
        }
        
        BattleId = defaultID;

        int StarAllNum = 0;
        for (int i = 0; i < cfgPageInfo.Count; i++)
        {
            int battId = cfgPageInfo[i].UID;
            var go = GetView().root_YJLevel.Find("btn_zj_" + (i + 1));
            JUIExtensions.txt(go, "img_pro/txt_pro").text = cfgPageInfo[i].Chapter + "-" + cfgPageInfo[i].Layers;

            int StarNum = FightPlayM.Instance.GetRelicLevelStar(battId);
            StarAllNum += StarNum;
            for (int j = 0; j < 3; j++)
            {
                go.Find("root_star").GetChild(j).gameObject.SetActive(StarNum > j);
            }
            JUIExtensions.btn(go).onClick.RemoveAllListeners();
            ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/FightChoice", defaultID == battId ? "bg_yiji0" : "bg_yiji1", JUIExtensions.img(go));

            JUIExtensions.btn(go).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                if (battId > RelicSlectID)
                { 
                    UITool.ShowTips("尚未解锁");
                    return;
                }
                UITool.Open<UIRelicChallange>(null, battId, RelicSlectID);
            });
        }

        //遗迹进度条
        GetView().slider_YJ.value = StarAllNum / 30.0f;
        GetView().txt_YJ_StarNum.text = StarAllNum + "星";

        for (int i = 1; i <= 3; i++)
        {
            int StarNum = i * 10;
            ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.CommonAtlas, FightPlayM.Instance.GetRelicChapterStarGet(pageidx) >= StarNum  ? "icon_baoxiang1" : "icon_baoxiang2", JUIExtensions.img(GetView().slider_YJ.transform.Find("YJBox_" + i )));
            var btn = JUIExtensions.btn(GetView().slider_YJ.transform.Find("YJBox_" + i));
            GetView().slider_YJ.transform.Find("YJBox_" + i + "/dot").gameObject.SetActive(!(FightPlayM.Instance.GetRelicChapterStarGet(pageidx) >= StarNum) && StarAllNum >= StarNum);
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                if (!(FightPlayM.Instance.GetRelicChapterStarGet(pageidx) >= StarNum) && StarAllNum >= StarNum)
                    TcpC.C2S_BattleReward(CFG_Room.RoomType.Relic, cfgPageInfo[0].Chapter);
                else
                {
                    var StarInfo = cfgPageInfo[cfgPageInfo.Count -1 ].GetStarAwardByNum(StarNum);
                    List<UIBoxInfoShow.BoxItem[]> items = new List<UIBoxInfoShow.BoxItem[]>();
                    UIBoxInfoShow.BoxItem[] boxItems = new UIBoxInfoShow.BoxItem[StarInfo.Length];
                    for (int j = 0; j < StarInfo.Length; j++)
                    {
                        boxItems[j] = new UIBoxInfoShow.BoxItem { Id = StarInfo[j].id, Num = StarInfo[j].num };
                    }
                    items.Add(boxItems);
                    UITool.Open<UIBoxInfoShow>(null, null, items);
                }
            });
        }

        //遗迹按钮
        GetView().btn_YJ_Play.interactable = defaultID != 0;
        GetView().btn_YJ_Play.gameObject.SetActive(FightPlayM.Instance.GetRelicLevelStar(defaultID) != 3 && defaultID != 0);

        GetView().obj_YJLock.SetActive(!(FightPlayM.Instance.GetRelicLevelStar(defaultID) != 3 && defaultID != 0));

        GetView().btn_YJ_SD.gameObject.SetActive(FightPlayM.Instance.GetRelicLevelStar(defaultID) == 3);

        GetView().btn_YJ_Add.gameObject.SetActive(RelicIdx < CFG_Battle.RelicDic.Count);
        GetView().btn_YJ_Jian.gameObject.SetActive(RelicIdx > 1);
    }
    #endregion

    #region PVP

    private void OnPVPUI()
    {
        HttpC.GetRankList(CFG_RankingAward.RankClass.ZhanHun, (_r) =>
        {
            if (isNullGo)
                return;

            bool isNoOne = _r.data.rank_all.Length == 0;
            GetView().root_PvPOne.Find("bg_head/img_head").gameObject.SetActive(!isNoOne);
            if (!isNoOne)
            {
                ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.RoleHeadAtlas, "img_" + _r.data.rank_all[0].job + _r.data.rank_all[0].sex,JUIExtensions.img(GetView().root_PvPOne, "bg_head/img_head"));
            }
        });

        HttpC.GetRankList(CFG_RankingAward.RankClass.TurnBasedDay, (_r) =>
        {
            if (isNullGo)
                return;

            bool isNoOne = _r.data.rank_all.Length == 0;
            GetView().root_PvPMore.Find("bg_head/img_head").gameObject.SetActive(!isNoOne);
            ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.RoleHeadAtlas, "img_" + _r.data.rank_all[0].job + _r.data.rank_all[0].sex, JUIExtensions.img(GetView().root_PvPMore, "bg_head/img_head"));
        });

        void OnItem(Transform root, CFGItem_Room cfg)
        {
            Transform itemRoot = root.Find("img_award/awards");

            //奖励
            CFG_XMLConfig.XML_Config.XML_Item_PVP.PVPreward[] pVPrewards = null;
            if (cfg.Roomtype == CFG_Room.RoomType.PVP_One)
            {
                pVPrewards = CFG_XMLConfig.xmlConfig.PVPInfo.winAwards;
            }
            else if(cfg.Roomtype == CFG_Room.RoomType.PVP_More)
            {
                pVPrewards = CFG_XMLConfig.xmlConfig.PVPInfo.loseAwards;
            }

            for (int i = 0; i < itemRoot.childCount; i++)
            {
                var go = itemRoot.GetChild(i);
                int cfgId = pVPrewards[i].id;
                GameUtil.Instance.SetItemIcon(cfgId, JUIExtensions.img(go.Find("icon")), JUIExtensions.img(go));

                JUIExtensions.btn(go).onClick.RemoveAllListeners();
                JUIExtensions.btn(go).onClick.AddListener(() =>
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, cfgId);
                });
            }
            //else
            //{
            //    int jidu = (GameUtil.Instance.GetDataTime(GameUtil.Instance.GetCurrentTimeSeconds()).Month - 1) / 3 + 1;
            //    pVPrewards = CFG_XMLConfig.xmlConfig.GVGInfo.GVGReward[jidu];
            //}

            //挑战次数
            JUIExtensions.txt(root, "txt_Count").text = "" + RoleM.Instance.GetPVPMatchTimes((int)cfg.Roomtype);

            CFG_XMLConfig.XML_Config.XML_Item_PVP.PVPseason endPVPSeason = null;
            if (cfg.Roomtype == CFG_Room.RoomType.PVP_One)
            {
                endPVPSeason = RoleM.Instance.GetPVPOneSeason();
                JUIExtensions.txt(root, "img_time/txt_sjTime").text = endPVPSeason == null ? "赛季已结束" : $"赛季截至日期:<color=#CAFB04>{endPVPSeason.endtime}</color>";
            }
            else
            {
                JUIExtensions.txt(root, "img_time/txt_sjTime").text = "";
            }
            var baseInfo = cfg.Roomtype == CFG_Room.RoomType.PVP_One ? CFG_XMLConfig.xmlConfig.PVPInfo : CFG_XMLConfig.xmlConfig.GVGInfo;
            JUIExtensions.txt(root, "txt_time").text = $"(每日:{baseInfo.dailyBegin}-{baseInfo.dailyEnd})";

            JUIExtensions.btn(root.Find("Btn_help")).onClick.RemoveAllListeners();
            JUIExtensions.btn(root.Find("Btn_help")).onClick.AddListener(() => {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                UITool.Open<UIComRule>(null, (cfg.Roomtype == CFG_Room.RoomType.PVP_One ? 3 : 4));
            });

            var isOpne = (endPVPSeason != null || cfg.Roomtype == CFG_Room.RoomType.PVP_More) && GameUtil.Instance.CheakCurTime(baseInfo.dailyBegin, baseInfo.dailyEnd);
            root.Find("Btn_Play/off").gameObject.SetActive(!isOpne);
            root.Find("Btn_AddCount/off").gameObject.SetActive(!isOpne);

            root.Find("Btn_Play/dot").gameObject.SetActive(isOpne && RoleM.Instance.GetPVPMatchTimes((int)cfg.Roomtype) > 0);

            if (isOpne)
            {
                JUIExtensions.btn(root, "Btn_Play").onClick.RemoveAllListeners();
                JUIExtensions.btn(root, "Btn_Play").onClick.AddListener(() => {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

                    if (GameUtil.Instance.CheakIsLock(cfg.Roomtype == CFG_Room.RoomType.PVP_More ? CFG_XMLConfig.XML_Config.SwitchOpne.Play_GVG : CFG_XMLConfig.XML_Config.SwitchOpne.Play_PVP))
                        return;

                    if (RoleM.Instance.GetPVPMatchTimes((int)cfg.Roomtype) == 0)
                    {
                        UITool.ShowTips("挑战次数不足");
                        return;
                    }
                    OnEnterPVPGame((int)cfg.Roomtype);
                });

                root.Find("Btn_AddCount/off").gameObject.SetActive(RoleM.Instance.GetCanNumBuyPVP((int)cfg.Roomtype) == 0);
                JUIExtensions.btn(root, "Btn_AddCount").onClick.RemoveAllListeners();
                JUIExtensions.btn(root, "Btn_AddCount").onClick.AddListener(() => {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIPVPCountBuy>(null, (int)cfg.Roomtype);
                });
            }

            JUIExtensions.btn(root, "bg_head").onClick.RemoveAllListeners();
            JUIExtensions.btn(root, "bg_head").onClick.AddListener(() =>
            {
                if (cfg.Roomtype == CFG_Room.RoomType.PVP_One)
                    UITool.Open<UIRank>(null, CFG_RankingAward.RankClass.ZhanHun);
                else if(cfg.Roomtype == CFG_Room.RoomType.PVP_More)
                    UITool.Open<UIRank>(null, CFG_RankingAward.RankClass.TurnBasedDay);
            });
        }

        var cfg = CFG_Room.GetByRoomType(CFG_Room.RoomType.PVP_One, RoleM.Instance.Level);
        OnItem(GetView().root_PvPOne, cfg);

        cfg = CFG_Room.GetByRoomType(CFG_Room.RoomType.PVP_More, RoleM.Instance.Level);
        OnItem(GetView().root_PvPMore, cfg);
    }

    private void OnEnterPVPGame(int Roomtype)
    {
        GameRoomM.Instance._GameState = GameRoomM.GameState.No;

        if (Roomtype == (int)CFG_Room.RoomType.PVP_One)
            TcpC.C2S_PvpMatching(1);
        else
            TcpC.C2S_PvpMatching(2);
        //UITool.ShowTips("暂未开放");
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_PVP(GameEvent.Sync_PVPInfo _e)
    {
        if (isNullGo)
            return;
        OnPVPUI();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_CheckOpenTime(GameEvent.Refresh_ZeroTime _e)
    {
        SetTagButtonLockState();
        OnHaiDaoUI();
        TcpC.C2S_InstanceInfo();
    }

    #endregion

    #region 守卫海岛

    //TODO
    private void OnHaiDaoUI()
    {
        int idxID1 = 0;
        int idxID2 = 0;
        int playUid = CFG_Room.GetByRoomType(CFG_Room.RoomType.PVE_Peace, RoleM.Instance.Level) == null ? 1 : CFG_Room.GetByRoomType(CFG_Room.RoomType.PVE_Peace, RoleM.Instance.Level).ID;
        if (playUid > 5)
        {
            idxID1 = 5; 
            idxID2 = playUid;
        }
        else
        {
            idxID1 = playUid;
            idxID2 = 6;
        }

        ShowHaiDaoItem(idxID1, GetView().Obj_HaiDao.Find("root_1"));
        ShowHaiDaoItem(idxID2, GetView().Obj_HaiDao.Find("root_2"));

        void ShowHaiDaoItem(int uid,Transform root)
        {
            var cfgInfo = CFG_Room.GetById(uid);
            JUIExtensions.txt(root, "txt_Limit_Des").text = cfgInfo.Suggest;
            JUIExtensions.txt(root, "img_Des/txt_des").text = cfgInfo.CopyDes;

            //奖励显示
            Transform itemRoot = root.Find("img_award/awards");
            for (int i = 0; i < itemRoot.childCount; i++)
            {
                var go = itemRoot.GetChild(i);
                go.gameObject.SetActive(i < cfgInfo.Droppreview.Length);

                if (i < cfgInfo.Droppreview.Length)
                {
                    int cfgId = cfgInfo.Droppreview[i];
                    GameUtil.Instance.SetItemIcon(cfgId, JUIExtensions.img(go, "icon"), JUIExtensions.img(go));
                    JUIExtensions.btn(go).onClick.RemoveAllListeners();
                    JUIExtensions.btn(go).onClick.AddListener(() =>
                    {
                        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                        UITool.Open<UIItemShow>(null, cfgId);
                    });
                }
            }

            JUIExtensions.btn(root, "Btn_help").onClick.RemoveAllListeners();
            JUIExtensions.btn(root, "Btn_help").onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                UITool.Open<UIComRule>(null, idxID1 == uid ? 40 :41);
            });

            bool isOpenTime = GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.DefendIsland, false);
            bool isOpneLv = !isOpenTime && RoleM.Instance.Level >= cfgInfo.MinEntryLimit && RoleM.Instance.Level <= cfgInfo.MaxEntryLimit;
            root.Find("Btn_Play").gameObject.SetActive(isOpneLv);
            JUIExtensions.txt(root, "txt_ChanllengeTime").text = !isOpneLv ? "": "今日剩余时间:" + GameUtil.Instance.GetTimeSToHMS(FightPlayM.Instance.GetInfo().afk.DailyTime) ;

            JUIExtensions.btn( root.Find("Btn_Play")).onClick.RemoveAllListeners();
            JUIExtensions.btn(root.Find("Btn_Play")).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                if (FightPlayM.Instance.GetInfo().afk.DailyTime == 0)
                {
                    UITool.ShowTips("剩余时间不足！");
                    return;
                }

                if (FightPlayM.Instance.GetInfo().afk.AfkMultiplierID < 0)
                {
                    //UITool.ShowTips("挑战门票不足！");
                    UITool.Open<UIGuardIslandBox>();
                    return;
                }
                TcpC.GetGameJoin(CFG_Room.GetByRoomType(CFG_Room.RoomType.PVE_Peace, RoleM.Instance.Level).ID, 0);
            });

            //CFGItem_Store cFGItem_Store = CFG_Store.GetById((int)CFG_Store.StroeID.HaiDaoCard);

            //root.Find("Btn_AddCount").gameObject.SetActive(isOpneLv && StoreM.Instance.GetCanBuyNum((int)CFG_Store.StroeID.HaiDaoCard) > 0);

            root.Find("obj_Limit").gameObject.SetActive(!isOpneLv );
            if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.DefendIsland, false))
                JUIExtensions.txt(root.Find("obj_Limit")).text = $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.DefendIsland)}天后开启";
            else
                JUIExtensions.txt(root.Find("obj_Limit")).text = $"角色等级不满足需求";

            Debug.Log("---" + FightPlayM.Instance.GetInfo().afk.AfkMultiplierID);
            string cardStr = "";
            if (isOpneLv && FightPlayM.Instance.GetInfo().afk.AfkMultiplierID >= 0)
            {
                foreach (var item in CFG_XMLConfig.xmlConfig.GuardIslandDic.Values)
                {
                    if (item.id == (int)FightPlayM.Instance.GetInfo().afk.AfkMultiplierID)
                    {
                        cardStr = item.multiplier + "倍效率"; ;
                        break;
                    }
                }
            }

            JUIExtensions.txt(root, "txt_time").text = cardStr;
        }
    }

    #endregion

    [Subscriber(ThreadMode.Main)]
    private void Refresh_InstanceInfo(GameEvent.Refresh_InstanceInfo _e)
    {
        if (isNullGo) return;
        if (selectType == SelectType.Challange || chanllengeType == ChanllengeType.HaiDao)
            OnChallengeBtnClick((int)chanllengeType,true);
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);

        if (_UIDayDo != null)
            _UIDayDo.OnExit();
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}