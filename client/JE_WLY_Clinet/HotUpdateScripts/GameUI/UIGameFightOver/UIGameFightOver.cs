using System.Collections.Generic;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Helper;
using JEngine.UI;
using System;
using pb;
using HotUpdateScripts.ProtoM;
using HotUpdateScripts.Game;

public class UIGameFightOver : UIBase<UIModelGameFightOver, UIViewGameFightOver>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    public override void OnInit()
    {
        GetView().btn_blue.onClick.AddListener(OnBlueBtnClick);

        GetView().btn_yellow.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnBack();
        });

        for (int i = 0; i < GetView().root_Awards.childCount; i++)
        {
            int idx = i;
            GetView().root_Awards.GetChild(i).GetComponent<Button>()?.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                UITool.Open<UIItemShow>(null, gameResult.Items[idx].CfgId);
            });
        }
    }

    private void OnBlueBtnClick()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        if (GetView().txt_blue.text == "继续挑战" || GetView().txt_blue.text == "下一关")
        {
            if(gameResult.RoomId != 0 && CFG_Room.GetById(gameResult.RoomId).Roomtype == CFG_Room.RoomType.PVP_More)
            {
                TurnBasedFightCtrl.Instance.GoToNextFight();
                return;
            }

            int NextBattleId = gameResult.BattleId;

            if (CFG_Battle.GetById(gameResult.BattleId).Copytype == (int)CFG_Room.RoomType.Fight)
            {
                NextBattleId = CFG_Battle.GetNextFightById(NextBattleId).UID;
            }
            else
            {
                List<CFGItem_Battle> CFG_Battles = CFG_Battle.GetById(NextBattleId).Copytype == (int)CFG_Room.RoomType.Boss ? CFG_Battle.BossList : CFG_Battle.GetSLTowerList();
                if (CFG_Battle.GetById(NextBattleId).Copytype == (int)CFG_Room.RoomType.Boss)
                    CFG_Battles = CFG_Battle.BossList;
                else if (CFG_Battle.GetById(NextBattleId).Copytype == (int)CFG_Room.RoomType.YZDD)
                    CFG_Battles = CFG_Battle.YZDDList;
                else
                    CFG_Battles = CFG_Battle.GetSLTowerList();


                if (CFG_Battle.GetById(NextBattleId).Copytype == (int)CFG_Room.RoomType.YZDD)
                {
                    for (int i = 0; i < CFG_Battles.Count; i++)
                    {
                        if (CFG_Battles[i].UID > NextBattleId)
                        {
                            NextBattleId = CFG_Battles[i].UID;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = (CFG_Battles.Count - 1); i >= 0; i--)
                    {
                        if (CFG_Battles[i].UID > NextBattleId)
                        {
                            NextBattleId = CFG_Battles[i].UID;
                            break;
                        }
                    }
                }


            }
            if (NextBattleId == gameResult.BattleId)
            {
                UITool.ShowTips("您已挑战到最高关卡了！");
                return;
            }

            GameHelper.Instance.StarGame(NextBattleId);
        }
        else if (GetView().txt_blue.text == "再领一倍")
        {
            if (!GameUtil.Instance.ComCoinToCheck(uesdID, uesdNum, null, false))
            {
                UITool.ShowTips("货币不足");
                return;
            }
            string timeStr = GameUtil.Instance.GetDataTime(GameUtil.Instance.GetCurrentTimeSeconds()).DayOfYear.ToString();

            if (GameUtil.Instance.GetPlayerPrefs("DayDoAward") == timeStr)
            {
                TcpC.C2S_DoubleReward(true);
            }
            else
            {
                UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
                {
                    ShowStr = $"是否确定消耗{GameUtil.Instance.FormatNumWithUnit(uesdNum)}{GameUtil.Instance.GetItemName(uesdID)}进行双倍领取？",
                    isTogg = true,
                    TogNoFun = () =>
                    {
                        GameUtil.Instance.SetPlayerPrefs("DayDoAward", "");
                    },
                    TogYesFun = () =>
                    {
                        GameUtil.Instance.SetPlayerPrefs("DayDoAward", timeStr);
                    },
                    YesFun = delegate { TcpC.C2S_DoubleReward(true); },
                });
            }
        }
        else
        {
            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build))
                return;
            //TODo 我要变强
            OnBack(() => { UITool.Open<UIBuild>(); });
        }
    }

    private void OnBack(Action Jump = null)
    {
        UITool.Close<UIGameFightOver>();
        if (gameResult.RoomId != 0 && CFG_Room.GetById(gameResult.RoomId).Roomtype == CFG_Room.RoomType.PVP_More)
        {
            ClearJa();
            TurnBasedFightCtrl.Instance.OnExitGame(Jump);
            return;
        }

        if (GameRoomM.Instance._GameState == GameRoomM.GameState.Over)
        {
            ClearJa();
            FightController.Instance.OnExitGame(Jump);
        }
    }

    private TimeItem Ja;
    private void ClearJa()
    {
        if (Ja != null)
        {
            GameHelper.Instance.RemoveInvoke(Ja);
            Ja = null;
        }
    }


    GameResultBattlePush gameResult;

    private int uesdID;
    private int uesdNum;

    public override void OnEnter(params object[] args)
    {
        gameResult = (GameResultBattlePush)args[0];

        bool isDayDo = gameResult.RoomId != 0 && GameRoomM.Instance.IsDayDo((int)CFG_Room.GetById(gameResult.RoomId).Roomtype);

        bool IsWin = gameResult.OverType == GameOverType.GameOverTypeMDead || gameResult.OverType == GameOverType.GameOverTypeCopySweep || isDayDo;
        GetView().obj_lose.gameObject.SetActive(!IsWin);
        GetView().obj_win.gameObject.SetActive(IsWin);

        bool isFightEnd = false;    //是不是一战到底？

        if (IsWin)
        {
            string Str = gameResult.OverType == GameOverType.GameOverTypeCopySweep ? "恭喜扫荡获得" : "恭喜挑战获得";

            if (gameResult.OverType != GameOverType.GameOverTypeCopySweep && gameResult.BattleId != 0)
            {
                var CFGBattle = CFG_Battle.GetById(gameResult.BattleId);
                var RootType = CFG_Room.GetById(gameResult.RoomId).Roomtype;

                if (RootType == CFG_Room.RoomType.Fight)
                    Str = "已通关:战役" + CFGBattle.Chapter + "-" + CFGBattle.Layers + "\n" + Str;
                else if (RootType == CFG_Room.RoomType.Tower)
                    Str = "已通关:试炼塔" + CFGBattle.Layers + "层" + "\n" + Str;
                else if (RootType == CFG_Room.RoomType.Boss)
                    Str = "已通关:首领" + CFGBattle.Layers + "层" + "\n" + Str;
                else if (RootType == CFG_Room.RoomType.Relic)
                    Str = "已通关:遗迹" + CFGBattle.Chapter + "-" + CFGBattle.Layers + "\n" + Str;
                else if (RootType == CFG_Room.RoomType.YZDD)
                {
                    isFightEnd = true;
                    if (gameResult.BattleId <= FightPlayM.Instance.GetInfo().to_end.BattleId)
                        Str = "已通关:一战到底 第" + CFGBattle.Layers + "关";
                    else
                        Str = "已通关:一战到底 第" + CFGBattle.Layers + "关" + "\n" + Str;

                    GetView().txt_useTime.text = GameUtil.Instance.GetTimeToHMS(gameResult.CompletionTime);
                    GetView().obj_newRecord.gameObject.SetActive(FightPlayM.Instance.IsFightEndNewRecord(gameResult.BattleId, gameResult.CompletionTime));
                }

                GetView().txt_normalAwards.gameObject.SetActive(isFightEnd);
                GetView().root_useTime.gameObject.SetActive(RootType == CFG_Room.RoomType.YZDD);
            }
            GetView().txt_des.text = Str;
        }

        bool isTurnBased = gameResult.RoomId != 0 && CFG_Room.GetById(gameResult.RoomId).Roomtype == CFG_Room.RoomType.PVP_More;

        bool receivedAwards = isFightEnd && gameResult.BattleId <= FightPlayM.Instance.GetInfo().to_end.BattleId;

        if (isDayDo)
        {
            GetView().txt_blue.text = "再领一倍";
            GetView().btn_blue.gameObject.SetActive(true);
            GetView().txt_num.gameObject.SetActive(true);
            GetView().txt_point.gameObject.SetActive(false);

            uesdID = CFG_XMLConfig.xmlConfig.DayRoomDic[CFG_Room.GetById(gameResult.RoomId).Roomtype].doubleaccessID;
            uesdNum = CFG_XMLConfig.xmlConfig.DayRoomDic[CFG_Room.GetById(gameResult.RoomId).Roomtype].doubleaccessNum;

            GetView().txt_num.text = GameUtil.Instance.GetCheakNumStr(uesdID, uesdNum,
                "x" + GameUtil.Instance.FormatNumWithUnit(uesdNum));
        }
        else if (isTurnBased)
        {
            GetView().btn_blue.gameObject.SetActive(false);
            GetView().txt_point.gameObject.SetActive(true);
            string score = TurnBasedM.Instance.turnBasedBattleData.Result.Score < 0 ?
               "积分" + TurnBasedM.Instance.turnBasedBattleData.Result.Score.ToString() : "积分+" + TurnBasedM.Instance.turnBasedBattleData.Result.Score;
            GetView().txt_point.text = score;
        }
        else
        {
            GetView().txt_point.gameObject.SetActive(false);
            GetView().txt_num.gameObject.SetActive(false);
            string nextStr = isFightEnd ? "下一关" : "继续挑战";
            GetView().txt_blue.text = gameResult.OverType == GameOverType.GameOverTypeMDead ? nextStr : "我要变强";
            bool isShowNextButton = !isFightEnd || CFG_Battle.YZDDList[CFG_Battle.YZDDList.Count - 1].UID > gameResult.BattleId;
            GetView().btn_blue.gameObject.SetActive(gameResult.OverType != GameOverType.GameOverTypeCopySweep && CFG_Battle.GetById(gameResult.BattleId).Copytype != (int)CFG_Room.RoomType.Relic && isShowNextButton);
        }


        if (receivedAwards)
        {
            CFGItem_Battle.AwardItem[] items = CFG_Battle.GetById(gameResult.BattleId).Settlement;
            for (int i = 0; i < items.Length; i++)
            {
                var go = GetView().root_Awards.GetChild(i);
                go.gameObject.SetActive(items != null && i < items.Length);
                if (items != null && i < items.Length)
                {
                    GameUtil.Instance.SetItemIcon(items[i].id, JUIExtensions.img(go.transform.Find("icon")), JUIExtensions.img(go.transform), JUIExtensions.img(go.transform.Find("icon/nature")));
                    JUIExtensions.txt(go, "txt_num").text = GameUtil.Instance.FormatNumWithUnit(items[i].num);
                    go.Find("received").gameObject.SetActive(true);
                }
            }
        }
        else
        {
            for (int i = 0; i < GetView().root_Awards.childCount; i++)
            {
                var go = GetView().root_Awards.GetChild(i);
                go.gameObject.SetActive(gameResult.Items != null && i < gameResult.Items.Count);
                if (gameResult.Items != null && i < gameResult.Items.Count)
                {
                    GameUtil.Instance.SetItemIcon(gameResult.Items[i].CfgId, JUIExtensions.img(go.transform.Find("icon")), JUIExtensions.img(go.transform), JUIExtensions.img(go.transform.Find("icon/nature")));
                    JUIExtensions.txt(go, "txt_num").text = GameUtil.Instance.FormatNumWithUnit(gameResult.Items[i].Num);
                    go.Find("received").gameObject.SetActive(false);
                }
            }
        }

        GetView().txt_yellow.text = gameResult.OverType == GameOverType.GameOverTypeCopySweep ? "确定" : "退出";

        string StrTime = gameResult.OverType == GameOverType.GameOverTypeCopySweep ? "秒后将自动关闭" : "秒后将自动退出";
        int Count = 10;
        Ja = GameHelper.Instance.AddInvoke(new TimeItem(Count, 1, () =>
        {
            if (isNullGo) return;

            GetView().txt_Time.text = Count + StrTime;
            Count--;
            if (Count <= 0)
                OnBack();
        }));

        if (gameResult.BattleId > 0 && CFG_Battle.GetById(gameResult.BattleId).Copytype == (int)CFG_Room.RoomType.Boss && GameRoomM.Instance._GameState != GameRoomM.GameState.No)
        {
            TcpC.C2S_InstanceInfo();
        }
    }

    public override void OnExit()
    {
        ClearJa();
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}