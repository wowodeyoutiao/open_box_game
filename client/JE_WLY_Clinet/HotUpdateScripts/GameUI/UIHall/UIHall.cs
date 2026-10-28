using DG.Tweening;
using HotUpdateScripts;
using HotUpdateScripts.ProtoM;
using JEngine.Event;
using JEngine.Helper;
using JEngine.UI;
using MetaFramework.UI;
using pb;
using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class UIHall : UIBase<UIModelHall, UIViewHall>
{
    public override UILayer GetLayer()
    {
        return UILayer.Base;
    }

    public override void OnLayerChange()
    {
        //GameUtil.Instance.SetEffectLayer(GetView().eff1, _canvasOrder + 1);
    }

    public enum HallType
    {
        Hall,
        PVE,
        PVP,
        Train,
    }

    /// <summary>
    /// 自动开箱状态
    /// </summary>
    public enum AutoOpenBoxState
    {
        Enabled,    // 已启用
        Paused,     // 已暂停
        Stopped,   // 已停用
    }

    public enum LeftButton
    {
        /// <summary>
        /// 限时悬赏
        /// </summary>
        Limit,

        /// <summary>
        /// 福利
        /// </summary>
        Welfare,

        /// <summary>
        /// 开服庆典
        /// </summary>
        OpenServer,

        /// <summary>
        /// 世界BOSS
        /// </summary>
        WorldBoss,

        /// <summary>
        /// 一战到底
        /// </summary>
        FightEnd,
    }

    AutoOpenBoxState autoOpenBoxState = AutoOpenBoxState.Stopped;

    // 开箱后，在处理奖品完成前不能再次开箱。
    bool boxIsOpened = false;

    //如果单个开箱开启详情面板前，不许回收
    private bool BoxIsDel = true;

    private Vector3 buttomUIPosition;

    // 自动开箱后是否以经手动回收了物品？是的话，自动回收是要跳过回收请求。
    private bool isManualRecycled = false;

    public override void OnInit()
    {
        JEvent.defaultEvent.Register(this);

        //for (int i = 1; i <= GetView().root_leftBtns.childCount; i++)
        //{
        //    var obj = GetView().root_leftBtns.Find("Btn_" + i);
        //    if (obj != null)
        //    {
        //        int idx = i;
        //        JUIExtensions.btn(obj).onClick.AddListener(() =>
        //        {
        //            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        //            OnRightBtnClick(idx);
        //        });
        //    }
        //}

        for (int i = 1; i <= GetView().root_topBtns.childCount; i++)
        {
            var obj = GetView().root_topBtns.Find("Btn_" + i);
            if (obj != null)
            {
                int idx = i;
                JUIExtensions.btn(obj).onClick.AddListener(() =>
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    OnTopBtnClick(idx);
                });
            }
        }

        GetView().btn_Chat.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIChat>();
        });

        GetView().btn_Chat2.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIChat>();
        });

        //团队穿戴
        for (int i = 0; i < GetView().root_BtnTeam.childCount; i++)
        {
            int idx = i;
            GetView().root_BtnTeam.GetChild(i).GetComponent<Button>().onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnTeamChoie(idx);
            });
        }

        //装备展示
        for (int i = 0; i < GetView().BaseDressItems.Length; i++)
        {
            int dressidx = i + 1;
            GetView().BaseDressItems[i]._btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                if (SelectTeamIdx == 0)
                {
                    bool isNullEquip = RoleM.Instance.CheckIsNullEquip((EquipSlot)dressidx);
                    if (!isNullEquip)
                    {
                        var ItemInfo = RoleM.Instance.GetSlotInfoBySolt((EquipSlot)dressidx).Item;
                        UITool.Open<UIItemShow>(null, ItemInfo.CfgId, ItemInfo.Equip);
                    }
                }
                else
                {
                    var ItemInfo = PartnerM.Instance.GetOnePartnerOneEquip(SelectTeamIdx, dressidx);
                    if (ItemInfo != null)
                    {
                        UITool.Open<UIItemShow>(null, ItemInfo.CfgId, ItemInfo.Equip, SelectTeamIdx);
                    }
                }
            });
        }

        GetView().btn_BoxSet.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnOpenBoxSettingClick();
        });

        GetView().btn_BoxLv.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIBoxUpgrade>();
        });

        GetView().btn_BoxAdd.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnBoxOpneNumChange(true);
        });
        GetView().btn_BoxJian.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnBoxOpneNumChange(false);
        });

        GetView().btn_BoxOpen.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            // 自动开箱开启或暂停过程中，点击开箱后关闭
            if (autoOpenBoxState == AutoOpenBoxState.Enabled || autoOpenBoxState == AutoOpenBoxState.Paused)
                DisableAutoOpenBox();
            else
                OpenBoxRequest();
        });

        GetView().btn_BoxDel.onClick.AddListener(() =>
        {

            // 正在一键穿装备
            if (isAutoClothing)
                return;

            if (!BoxIsDel)
                return;
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            long[] uids = new long[RoleM.Instance.OpenBoxItems.Count];
            for (int i = 0; i < RoleM.Instance.OpenBoxItems.Count; i++)
            {
                uids[i] = RoleM.Instance.OpenBoxItems[i].Uid;
            }
            isManualRecycled = true;
            RecycleItems(uids);
        });

        GetView().btn_BoxClothing.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            closeCount = 0;
            OnClothingEquip();
        });

        GetView().btn_TeamUI.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnTeamUICtr(!isShowBottomUI);
        });

        GetView().root_Pet.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (GetView().root_Pet.childCount == 0) return;
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIPet>();
        });


        GetView().Btn_Task.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            TaskInfo task = TaskM.Instance.GetTasksByClass(TaskClass.MAINLINE)[0];

            if (task.Status == TaskStatus.COMPLETED)
            {
                TcpC.C2S_TaskComplate(TaskM.Instance.GetTasksByClass(TaskClass.MAINLINE)[0].CfgId);
            }
            else
            {
                CFG_Task.JumpUI(task.CfgId);
            }
        });

        _ = GameHelper.Instance.SetAsyncObj("UIFly", GameConst.GetEffPath("UIFly"), (go) =>
        {
            go.transform.SetParent(uiGo.transform);
            go.gameObject.SetActive(false);
            go.transform.localScale = Vector3.one * 0.8f;
            GetView().obj_fly = go.transform;
        }, true);
        OnBindRed();

        buttomUIPosition = GetView().UI_Team.transform.position;
        offy = GetView().UpPos.y - GetView().root_BoxHide.position.y;
    }

    private float offy = 0f;

    private void OnTeamUICtr(bool isShow)
    {
        GetView().obj_TeamUIDown.SetActive(isShow);
        GetView().obj_TeamUIUp.SetActive(!isShow);
        GetView().UI_TeamRoot.SetActive(isShow);

        GetView().root_Box.transform.position = isShow ? GetView().UpPos : GetView().root_BoxHide.position;

        GetView().UI_Team.transform.position = isShow ? buttomUIPosition : new Vector3(buttomUIPosition.x, buttomUIPosition.y - offy, buttomUIPosition.z);


        GetView().root_spine.localPosition = isShow ? Vector3.zero : new Vector3(0, -353, 0);
        isShowBottomUI = isShow;
    }

    private bool isShowBottomUI = true;

    private UICom_TopRes TopUI;
    private Dictionary<RedPointConst, GameObject> RedDic;

    private void OnBindRed()
    {
        RedDic = new Dictionary<RedPointConst, GameObject>()
        {
            //[RedPointConst.Hall_LeftBtn] = GetView().obj_leftDot,
            //[RedPointConst.Hall_TopBtn] = GetView().obj_topDot,
            [RedPointConst.GiftBag] = GetView().root_topBtns.transform.Find("Btn_1/dot").gameObject, // 礼包
            [RedPointConst.FirstPay] = GetView().root_topBtns.transform.Find("Btn_2/dot").gameObject, // 首充
            [RedPointConst.ActiveCenter] = GetView().root_topBtns.transform.Find("Btn_3/dot").gameObject, // 开服七天乐
            [RedPointConst.Task] = GetView().root_topBtns.transform.Find("Btn_4/dot").gameObject, // 日常
            [RedPointConst.Rank] = GetView().root_topBtns.transform.Find("Btn_5/dot").gameObject, // 排行榜
            //[RedPointConst.SignDay] = GetView().root_topBtns.transform.Find("Btn_6/dot").gameObject,
            //[RedPointConst.LimitedBounty] = GetView().root_leftBtns.transform.Find("Btn_1/dot").gameObject,
            //[RedPointConst.RouletteWheel] = GetView().root_leftBtns.transform.Find("Btn_2/dot").gameObject,
            //[RedPointConst.VIP] = GetView().btn_Vip.transform.Find("dot").gameObject,
            //[RedPointConst.PVP] = GetView().btn_PVP.transform.Find("dot").gameObject,
            //[RedPointConst.PVP_One] = GetView().root_PVP.transform.Find("Btn_0/btn/dot").gameObject,
            //[RedPointConst.PVP_More] = GetView().root_PVP.transform.Find("Btn_1/btn/dot").gameObject,
            //[RedPointConst.PVE] = GetView().btn_PVE.transform.Find("dot").gameObject,
            //[RedPointConst.WorldBoss] = GetView().root_topBtns.transform.Find("Btn_6/dot").gameObject,
            //[RedPointConst.DayDo] = GetView().root_topBtns.transform.Find("Btn_7/dot").gameObject,
            [RedPointConst.BoxLevel] = GetView().btn_BoxLv.transform.Find("dot").gameObject,
        };

        foreach (var item in RedDic)
        {
            RedPointManager.Instance.RPS.Register(item.Key.ToString(), (node) =>
            {
                item.Value.SetActive(node.pointCount > 0);
            });
        }


        // RedPointManager.Instance.RPS.Register(RedPointConst.Tower_Get.ToString(), (node) =>
        // {
        //     GetView().root_Train.transform.Find("Btn_0/btn_Get/dot").gameObject.SetActive(node.pointCount > 0);
        //     GetView().btn_Train.transform.Find("dot").gameObject.SetActive(node.pointCount > 0);
        // });
    }

    private void ClearBindRed()
    {
        if (RedDic != null)
        {
            foreach (var item in RedDic)
            {
                RedPointManager.Instance.RPS.Register(item.Key.ToString(), null);
            }
        }

        RedPointManager.Instance.RPS.Register(RedPointConst.Tower_Get.ToString(), null);
    }

    private void SetLeftButtonDescription(LeftButton leftButtonType, string content)
    {
        for (int i = 1; i <= GetView().root_leftBtns.childCount; i++)
        {
            if ((LeftButton)i == leftButtonType)
            {
                JUIExtensions.txt(GetView().root_leftBtns.GetChild(i).Find("desc")).text = content;
            }
        }
    }

    private void CheckSetLeftOpenStateInfo()
    {
        ShowWorldBossButtonOpenStateInfo();
        ShowFightEndButtonOpenStateInfo();
    }

    private void ShowWorldBossButtonOpenStateInfo()
    {
        // 【单机版不做世界Boss】原服那套是「异步多人」活动：
        //   房间 100 配置 PlayerNum=1（每人独立战场、互相看不见），
        //   但 boss 血条由全服共享（worldboss_mgr.changeHp 广播给该服所有 desk），
        //   100 条血 + 每天 5 次 × 60 秒，单人最多打掉 2%，
        //   掉落规则又是「耗尽该条血槽的玩家获得」，单机永远拿不到。
        // 核心乐趣（抢最后一条血、伤害排名）在单机全部失效，缩水后与首领副本重叠，
        // 所以整块不迁移，入口统一显示未开放（详见 doc 65 节）。
        SetLeftButtonDescription(LeftButton.WorldBoss, "未开放");
    }

    private void ShowFightEndButtonOpenStateInfo()
    {
        if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.FightEnd, false))
        {
            SetLeftButtonDescription(LeftButton.FightEnd, $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.FightEnd)}天后开启");
            return;
        }
        if (GameUtil.Instance.CheakCurTime(CFG_XMLConfig.xmlConfig.FightEnd.startTime, CFG_XMLConfig.xmlConfig.FightEnd.endTime, CFG_XMLConfig.xmlConfig.FightEnd.weekSet))
        {
            ShowFightEndTimeRemaining();
        }
        else
        {
            string fightEndOpenTime = $"{GameUtil.Instance.GetWeekDayName(CFG_XMLConfig.xmlConfig.FightEnd.weekSet)}开启";
            SetLeftButtonDescription(LeftButton.FightEnd, fightEndOpenTime);
        }
    }


    TimeItem WorldBossButtonRefresh;
    TimeItem WorldBossTimeRefresh;
    TimeItem FightEndButtonRefresh;
    TimeItem FightEndTimeRefresh;

    /// <summary>
    /// 世界BOSS按钮活动信息显示
    /// </summary>
    private void CheckWorldBossButtonOpenState()
    {
        ClearWorldBossButtonTime();
        int seconds = 0;
        if (GameUtil.Instance.CheakCurTime(CFG_XMLConfig.xmlConfig.WorldBoss.startTime, CFG_XMLConfig.xmlConfig.WorldBoss.endTime, CFG_XMLConfig.xmlConfig.WorldBoss.weekSet))
        {
            seconds = GameUtil.Instance.CheckNextEndTime(CFG_XMLConfig.xmlConfig.WorldBoss.endTime);
        }
        else
        {
            seconds = GameUtil.Instance.CheckNextOpenTime(CFG_XMLConfig.xmlConfig.WorldBoss.startTime, CFG_XMLConfig.xmlConfig.WorldBoss.endTime, CFG_XMLConfig.xmlConfig.WorldBoss.weekSet);
        }

        WorldBossButtonRefresh = GameHelper.Instance.AddInvoke(new TimeItem(seconds, () =>
        {
            if (isNullGo) return;
            CheckSetLeftOpenStateInfo();
            CheckWorldBossButtonOpenState();
        }));
    }

    private void ShowWorldBossTimeRemaining()
    {
        ClearWorldBossCountingTime();
        int seconds = GameUtil.Instance.CheckNextEndTime(CFG_XMLConfig.xmlConfig.WorldBoss.endTime);

        WorldBossTimeRefresh = GameHelper.Instance.AddInvoke(new TimeItem(seconds, 1, () =>
        {
            seconds--;
            ShowTime();
        }, true));

        void ShowTime()
        {
            SetLeftButtonDescription(LeftButton.WorldBoss, GameUtil.Instance.GetTimeSToDHMS(seconds));
            if (seconds <= 0)
            {
                ClearWorldBossCountingTime();
                SetLeftButtonDescription(LeftButton.WorldBoss, "");
            }
        }
    }

    /// <summary>
    /// 一战到底按钮活动信息显示
    /// </summary>
    private void CheckFightEndButtonOpenState()
    {
        ClearFightEndButtonTime();
        int seconds = 0;
        if (GameUtil.Instance.CheakCurTime(CFG_XMLConfig.xmlConfig.FightEnd.startTime, CFG_XMLConfig.xmlConfig.FightEnd.endTime, CFG_XMLConfig.xmlConfig.FightEnd.weekSet))
            seconds = GameUtil.Instance.CheckNextEndTime(CFG_XMLConfig.xmlConfig.FightEnd.endTime);
        else
            seconds = GameUtil.Instance.CheckNextOpenTime(CFG_XMLConfig.xmlConfig.FightEnd.startTime, CFG_XMLConfig.xmlConfig.FightEnd.endTime, CFG_XMLConfig.xmlConfig.FightEnd.weekSet);

        FightEndButtonRefresh = GameHelper.Instance.AddInvoke(new TimeItem(seconds, () =>
        {
            if (isNullGo) return;
            CheckSetLeftOpenStateInfo();
            CheckFightEndButtonOpenState();
        }));
    }

    private void ShowFightEndTimeRemaining()
    {
        ClearFightEndCountingTime();
        int seconds = GameUtil.Instance.CheckNextEndTime(CFG_XMLConfig.xmlConfig.FightEnd.endTime);
        ShowTime();
        FightEndTimeRefresh = GameHelper.Instance.AddInvoke(new TimeItem(seconds, 1, () =>
        {
            seconds--;
            ShowTime();
        }, true, true));

        void ShowTime()
        {
            SetLeftButtonDescription(LeftButton.FightEnd, GameUtil.Instance.GetTimeSToDHMS(seconds));
            if (seconds <= 0)
            {
                ClearFightEndCountingTime();
                SetLeftButtonDescription(LeftButton.FightEnd, "");
            }
        }
    }

    private void ClearWorldBossButtonTime()
    {
        if (GameHelper.Instance != null && WorldBossButtonRefresh != null)
        {
            GameHelper.Instance.RemoveInvoke(WorldBossButtonRefresh);
            WorldBossButtonRefresh = null;
        }
    }

    private void ClearWorldBossCountingTime()
    {
        if (GameHelper.Instance != null && WorldBossTimeRefresh != null)
        {
            GameHelper.Instance.RemoveInvoke(WorldBossTimeRefresh);
            WorldBossTimeRefresh = null;
        }
    }

    private void ClearFightEndButtonTime()
    {
        if (GameHelper.Instance != null && FightEndButtonRefresh != null)
        {
            GameHelper.Instance.RemoveInvoke(FightEndButtonRefresh);
            FightEndButtonRefresh = null;
        }
    }

    private void ClearFightEndCountingTime()
    {
        if (GameHelper.Instance != null && FightEndTimeRefresh != null)
        {
            GameHelper.Instance.RemoveInvoke(FightEndTimeRefresh);
            FightEndTimeRefresh = null;
        }
    }

    #region 大厅装备显示
    private int SelectTeamIdx = 0;
    public void OnTeamChoie(int idx)
    {
        if (idx > 0 && CFG_XMLConfig.GetOpenSoltLv(idx) > RoleM.Instance.Level)
        {
            UITool.ShowTips("角色等级不足");
            return;
        }
        SelectTeamIdx = idx;
        for (int i = 0; i < GetView().root_BtnTeam.childCount; i++)
        {
            GetView().root_BtnTeam.GetChild(i).Find("on").gameObject.SetActive(idx == i);
        }
        ShowDressUI(idx);
        OnEquipSoltsShow(idx);

        if (TopUI != null)
            TopUI.OnHeadOnShow(idx);
    }

    /// <summary>
    /// 装备信息
    /// </summary>
    /// <param name="idx"></param>
    private void ShowDressUI(int idx)
    {
        for (int i = 0; i < GetView().BaseDressItems.Length; i++)
        {
            int dressIDx = i + 1;

            var equipInfo = idx == 0 ? RoleM.Instance.GetSlotInfoBySolt((EquipSlot)dressIDx).Item : PartnerM.Instance.GetOnePartnerOneEquip(idx, dressIDx);
            GetView().BaseDressItems[i]._icon.gameObject.SetActive(equipInfo != null);
            GetView().BaseDressItems[i]._txtDes.text = equipInfo == null ? GameM.Instance.EquipName[dressIDx] : "";
            if (equipInfo != null)
            {
                GameUtil.Instance.SetItemIcon(equipInfo.CfgId, GetView().BaseDressItems[i]._icon, GetView().BaseDressItems[i]._imgBG, null, (int)equipInfo.Equip.Quality);
                GetView().BaseDressItems[i]._EquipLv.text = "Lv" + CFG_Item.GetById(equipInfo.CfgId).Levellimit;
            }
            else
            {
                GameUtil.Instance.SetItemBg(0, GetView().BaseDressItems[i]._imgBG);
            }
        }

    }

    public Dictionary<int, GameObject> equipEffectsDic = new Dictionary<int, GameObject>();

    private void SetDressEff(int solt)
    {
        _ = GameHelper.Instance.SetAsyncObj("UIBoom", GameConst.GetEffPath("UIBoom"), (go) =>
        {
            go.transform.SetParent(GetView().BaseDressItems[solt - 1]._icon.transform);
            go.gameObject.SetActive(true);
            go.transform.localScale = Vector3.one * 30;
            go.transform.localPosition = Vector3.zero;
            equipEffectsDic.Add(go.GetInstanceID(), go);

            GameHelper.Instance.SetDeayTimeDo(() =>
            {
                if (isNullGo) return;
                PoolHelper.Instance.RecycObj(go);
                equipEffectsDic.Remove(go.GetInstanceID());
            }, 0.5f);
        }, true);
    }

    /// <summary>
    /// 装备槽位信息
    /// </summary>
    /// <param name="TeamIdx"></param>
    private void OnEquipSoltsShow(int TeamIdx)
    {
        for (int i = 0; i < GetView().BaseDressItems.Length; i++)
        {
            int soltidx = i + 1;
            var equipInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt((EquipSlot)soltidx) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, soltidx);
            GetView().BaseDressItems[i]._txtStar.text = equipInfo.Stars.ToString();
            GetView().BaseDressItems[i]._txtStar.gameObject.SetActive(equipInfo.Stars > 0);
            GetView().BaseDressItems[i]._txtLv.text = equipInfo.Enhance == 0 ? "" : "+" + equipInfo.Enhance;

            for (int j = 0; j < equipInfo.Gemstone.Length; j++)
            {
                GetView().BaseDressItems[i].insetImgs[j].gameObject.SetActive(equipInfo.Gemstone[j] != 0);
                if (equipInfo.Gemstone[j] != 0)
                {
                    GameUtil.Instance.SetItemIcon(equipInfo.Gemstone[j], GetView().BaseDressItems[i].insetImgs[j]);
                }
            }
        }
    }

    [Subscriber(ThreadMode.Main)]
    //伙伴槽位信息刷新
    private void OnRefresh_PartnerSlot(GameEvent.Refresh_PartnerSlot _e)
    {
        if (isNullGo) return;

        SetRoleSpine();
        if (SelectTeamIdx != 0)
            ShowDressUI(SelectTeamIdx);
    }


    [Subscriber(ThreadMode.Main)]
    //槽位魂石镶嵌
    private void EquipInsetRefresh(GameEvent.EquipInsetRefresh _e)
    {
        OnEquipSoltsShow(SelectTeamIdx);
    }

    [Subscriber(ThreadMode.Main)]
    //槽位强化
    private void Replace_EquipEnhance(GameEvent.Replace_EquipEnhance _e)
    {
        OnEquipSoltsShow(SelectTeamIdx);
    }

    [Subscriber(ThreadMode.Main)]
    //槽位升星
    private void Replace_EquipStar(GameEvent.Replace_EquipStar _e)
    {
        OnEquipSoltsShow(SelectTeamIdx);
    }

    #endregion 

    //按钮开启
    private void OnBtnLock()
    {
        //var SwitchOpne = CFG_XMLConfig.XML_Config.SwitchOpne.PVP_One;
        //GetView().root_PVP.Find("Btn_0/btn/Lock").gameObject.SetActive(CFG_XMLConfig.CheakLock(SwitchOpne));
        //JUIExtensions.txt(GetView().root_PVP.Find("Btn_0/btn/Lock/Label")).text = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[SwitchOpne] + "级开启";

    }

    #region 右部按钮
    private void OnRightBtnClick(int idx)
    {
        if (idx == 4)
        {
            // 单机版不做世界Boss（异步多人活动，见 ShowWorldBossButtonOpenStateInfo 的说明）
            UITool.ShowTips("暂未开放");
            return;
        }
        else if (idx == 5)
        {
            //if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.FightEnd))
            //    return;
            //if (!GameUtil.Instance.CheakCurTime(CFG_XMLConfig.xmlConfig.FightEnd.startTime, CFG_XMLConfig.xmlConfig.FightEnd.endTime, CFG_XMLConfig.xmlConfig.FightEnd.weekSet))
            //{
            //    UITool.ShowTips($"{GameUtil.Instance.GetWeekDayName(CFG_XMLConfig.xmlConfig.FightEnd.weekSet)}开启");
            //    return;
            //}
            if (!GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.FightEnd))
                UITool.Open<UIFightToTheEnd>();
        }
        else
        {
            UITool.ShowTips("暂未开放");
        }
    }
    #endregion

    #region 顶部按钮
    private void OnTopBtnClick(int idx)
    {

        //var SwitchOpne = CFG_XMLConfig.XML_Config.SwitchOpne.World_Boss;
        switch (idx)
        {
            case 1:
                UITool.Open<UIGiftBag>();
                break;
            case 3:
                UITool.Open<UIActive>();
                break;
            case 2:
                OpenFirstPay();
                break;
            case 4:
                UITool.Open<UITask>();
                break;
            case 5:
                UITool.Open<UIRank>();
                break;
            case 6:
                //HttpC.GetSignList((_r) =>
                //  {
                //      if (!isNullGo)
                //          UITool.Open<UISignDay>(null, _r.data);
                //  });
                break;
            default:
                UITool.ShowTips("功能暂未开放");
                break;
        }
    }

    private void SetFirstLoginTopButtonRed()
    {
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank.ToString(), RedPointManager.Instance.GetRedNum(RedPointConst.Rank.ToString()));
    }

    private void OpenFirstPay()
    {
        if (!ActivityM.Instance.GetAcitvityState(ActivityType.FirstPay))
            UITool.ShowTips("首充活动已结束");

        HttpC.GetFirstPayInfo((req) =>
        {
            if (req.code == 0 && !isNullGo)
                UITool.Open<UIFirstPay>(null, req);
        });
    }
    #endregion

    #region 大厅任务
    private void SetMainTaskInfo()
    {
        // 单机暂无任务数据时 GetTasksByClass 返回空列表，直接 [0] 会 ArgumentOutOfRange
        var mainTasks = TaskM.Instance.GetTasksByClass(TaskClass.MAINLINE);
        if (mainTasks == null || mainTasks.Count == 0)
        {
            GetView().txt_TaskDes.text = "";
            GetView().txt_TaskPro.text = "";
            GetView().txt_TaskState.text = "";
            return;
        }

        var task = mainTasks[0];
        GetView().txt_TaskDes.text = CFG_Task.StrTaskDes(task.CfgId);
        (int amout, int targetnum) = CFG_Task.GetTaskPro(task.CfgId, task.Target.Amount, task.Target.TargetNum);
        GetView().sli_Task.value = (float)amout / targetnum;
        GetView().txt_TaskPro.text = $"{GameUtil.Instance.FormatNumWithUnit(amout)}/{GameUtil.Instance.FormatNumWithUnit(targetnum)}";
        //GetView().Btn_Task.enabled = task.Status == pb.TaskStatus.COMPLETED;
        GetView().txt_TaskState.text = task.Status == pb.TaskStatus.COMPLETED ? "<color=#6ED661>(已完成)</color>" : "(未完成)";

        GameUtil.Instance.SetItemIcon(int.Parse(CFG_Task.GetById(task.CfgId).Reward[0].ElementAt(0).Key), GetView().img_Task);
    }

    [Subscriber(ThreadMode.Main)]
    private void Refresh_Task(GameEvent.Refresh_Task _e)
    {
        SetMainTaskInfo();
    }
    #endregion

    private void CheckRed()
    {
        RoleM.Instance.CheckBoxLevelRed();
    }


    private void OnSendTCPAndHTTP()
    {
        HttpC.GetActivityState(() => 
        {
            CheckSignState();
            CheckFirstPayState();
        });

        HttpC.OnGetShopInfo(YesFun:(res)=> 
        {
            CheckActivityRedPoint();
        });
        // HttpC.OnExtractAccount();
        HttpC.OnGetMailInfo();
        //TcpC.GetTrainInfo(1);
        TcpC.GetBagInfo();
        TcpC.C2S_InstanceInfo();

        TcpC.OnPVPInfo((int)MatchType.SINGLE_COMBAT);
        TcpC.OnPVPInfo((int)MatchType.FREE_FOR_ALL);
        TcpC.C2S_DailyMapInfo();
        //TcpC.C2S_CheckBoss(500);

        HttpC.GetSignList();

        HttpC.GetPrivilegeDetail(1);
        HttpC.GetPrivilegeDetail(2);
        HttpC.GetTrialFundAwardInfo();
        HttpC.OnGetGrowthFundInfo();
    }

    private void CheckActivityRedPoint()
    {
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.GiftBag_DailyGift.ToString(), GiftBagM.Instance.GetGiftRedNum(13));
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.GiftBag_GiftPay.ToString(), GiftBagM.Instance.GetGiftRedNum(4));
    }

    private void CheckSignState()
    {
        if (!ActivityM.Instance.GetAcitvityState(ActivityType.Sign7) && !ActivityM.Instance.GetAcitvityState(ActivityType.SignHappy7))
            return;

        GetView().root_topBtns.Find("Btn_3").gameObject.SetActive(true && !GameM.Instance.IsAudit);
    }

    private void CheckFirstPayState()
    {
        if (!ActivityM.Instance.GetAcitvityState(ActivityType.FirstPay))
        {
            GetView().root_topBtns.Find("Btn_2").gameObject.SetActive(false);
            ActivityM.Instance.CloseAcitvity(ActivityType.FirstPay);
            return;
        }

        HttpC.GetFirstPayInfo((req) =>
        {
            if (req.code == 0 && req.day == 3 && req.status == (int)ActivityM.SignState.Geted)
            {
                GetView().root_topBtns.Find("Btn_2").gameObject.SetActive(false);
                ActivityM.Instance.CloseAcitvity(ActivityType.FirstPay);
            }else
                GetView().root_topBtns.Find("Btn_2").gameObject.SetActive(true && !GameM.Instance.IsAudit);
        });
    }

    private UICom_DownBtns DownUI = null;

    public override void OnEnter(params object[] args)
    {
        if (TopUI == null)
        {
            UITool.LoadUII<UICom_TopRes>(GetView().root_Base, (a) =>
            {
                TopUI = a as UICom_TopRes;
                ShowUI();
                TopUI.OnSetLayer(_canvasOrder);
                TopUI.OnHeadOnShow(SelectTeamIdx);

            });

            UITool.LoadUII<UICom_DownBtns>(GetView().root_Base, (ui) =>
            {
                DownUI = ui as UICom_DownBtns;

                GameHelper.Instance.AddInvoke(new TimeItem(0.1f, () =>
                {
                    CheckStartGuidePartnetUp(DownUI);
                }));
            });
        }
        else
            ShowUI();

        // 兜底：不管 ShowUI 内部哪一步抛异常，都保证主角立绘被尝试加载一次。
        // SetRoleSpine 内部有 SpineNames[i] != poolKey 判断，重复调用是幂等的。
        GameHelper.Instance.SetDeayFrame(() =>
        {
            try { SetRoleSpine(); }
            catch (Exception e) { Debug.LogError($"[UIHall] 兜底 SetRoleSpine 失败: {e}"); }
        });

        // 单机大厅挂机：本地驱动遇怪，不依赖服务端推送链路
        StartLocalHomeEncounter();

        // 预热怪物资源。
        // 「有时候看得到怪、很多时候看不到」的根因：
        // SetAsyncObj 内部 CheckPool 首次为 false → 走异步加载；若加载未在本轮 6 秒内完成，
        // 下一轮清理时又发起一次加载，形成「永远追不上」的竞态。
        // 预热一次把常用的 Partner_1 提前装进池，之后再刷就都是同步取用了。
        GameHelper.Instance.SetDeayFrame(() =>
        {
            _ = GameHelper.Instance.SetAsyncObj("Partner_1", "Role/Prefabs/Partner_1", (go) =>
            {
                if (go != null)
                    PoolHelper.Instance.RecycObj(go);   // 还回池，只为了让池里有对象
                JEngine.Core.Log.Print("[UIHall] 怪物资源预热完成（Partner_1），后续可同步取用");
            });
        });

        void ShowUI()
        {
            // ⚠️ 这两步原来裸调用，且排在 SetDeayFrame 之前：
            // 任一抛异常 → 延迟回调根本不注册 → 里面所有步骤（含主角 Spine）全都不执行，
            // 且不会打印任何「异常已跳过」，极难定位。
            SafeRun(() => OnTeamUICtr(true), nameof(OnTeamUICtr));
            SafeRun(ShowBoxLevel, nameof(ShowBoxLevel));

            GameHelper.Instance.SetDeayFrame(() =>
            {
                // 大厅初始化是顺序执行的：任何一步抛异常，后面的步骤全都不会执行。
                // 单机化后很多依赖服务端数据（聊天/宝箱/队伍/任务/红点），
                // 所以每一步单独兜住，避免一个失败把主角加载也拖死。
                SafeRun(() => SetBg(), nameof(SetBg));
                SafeRun(OnBtnLock, nameof(OnBtnLock));
                //SetFirstLoginTopButtonRed();
                SafeRun(OnSendTCPAndHTTP, nameof(OnSendTCPAndHTTP));
                SafeRun(OnHallChatUI, nameof(OnHallChatUI));
                SafeRun(OnBoxShow, nameof(OnBoxShow));
                SafeRun(SetDefaultOpenNum, nameof(SetDefaultOpenNum));
                SafeRun(SetRoleSpine, nameof(SetRoleSpine));
                SafeRun(SetPetSpine, nameof(SetPetSpine));
                SafeRun(() => OnHomeAutoEvent(0, 0), "OnHomeAutoEvent");
                //CheckSetLeftOpenStateInfo();
                //CheckWorldBossButtonOpenState();
                //CheckFightEndButtonOpenState();
                SafeRun(() => OnTeamChoie(0), "OnTeamChoie");
                SafeRun(SetMainTaskInfo, nameof(SetMainTaskInfo));
                SafeRun(CheckRed, nameof(CheckRed));
                if (RoleM.Instance.OpenBoxItems != null)
                    SafeRun(() => OnRefreshHomeBox(default), "OnRefreshHomeBox");

                // 开服七天乐`
                GetView().root_topBtns.Find("Btn_1").gameObject.SetActive(!GameM.Instance.IsAudit);
            });
        }

        // 单步异常保护：不让一个步骤的异常截断整个大厅初始化
        void SafeRun(Action act, string tag)
        {
            try
            {
                act();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIHall.ShowUI] {tag} 异常已跳过: {e}");
            }
        }
    }

    int moveTime = 12;
    private void SetBg(bool isInit = true, bool isMove = true)
    {
        float bgWidth = GetView().img_BG.rect.width;
        if (isInit)
        {
            GetView().bg_0.transform.localPosition = Vector3.zero;
            GetView().bg_1.transform.localPosition = Vector3.right * GetView().img_BG.rect.width;
            SetBgSizeDelta();
        }
        if (isMove)
        {
            GetView().bg_0.transform.DOLocalMoveX(-GetView().img_BG.rect.width, moveTime / 2).SetEase(Ease.Linear).OnComplete(() =>
            {
                GetView().bg_0.transform.localPosition = Vector3.right * GetView().img_BG.rect.width;
                GetView().bg_0.transform.DOLocalMoveX(-GetView().img_BG.rect.width, moveTime).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
                if (bgWidth != GetView().img_BG.rect.width)
                {
                    // 重新设置背景大小 防止加载满时数据异常
                    SetBg(true, isBGMove);
                }
            });
            GetView().bg_1.transform.DOLocalMoveX(-GetView().img_BG.rect.width, moveTime).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart);
        }


        void SetBgSizeDelta()
        {
            //GetView().bg_0.GetComponent<RectTransform>().sizeDelta = new Vector2(GetView().img_BG.rect.width + 5, GetView().img_BG.rect.height);
            //GetView().bg_1.GetComponent<RectTransform>().sizeDelta = new Vector2(GetView().img_BG.rect.width + 5, GetView().img_BG.rect.height);
        }
        OnSetBgSprit();
    }

    private int BGIDx = -1;
    private void OnSetBgSprit()
    {
        int idx = (RoleM.Instance.Level / 10) % 7;
        if (idx != BGIDx)
        {
            BGIDx = idx;
            _ = ResouseHelper.Instance.LoadSprite("Common/Texture/hall_bg" + BGIDx, GetView().bg_0);
            _ = ResouseHelper.Instance.LoadSprite("Common/Texture/hall_bg" + BGIDx, GetView().bg_1);
        }

    }

    private bool isBGMove = true;
    private void SetBgMove(bool isMove)
    {
        isBGMove = isMove;
        if (isMove)
        {
            GetView().bg_0.transform.DOPlay();
            GetView().bg_1.transform.DOPlay();
        }
        else
        {
            GetView().bg_0.transform.DOPause();
            GetView().bg_1.transform.DOPause();
        }

    }

    public bool CheckStartGuideOpenBox()
    {
        bool needJump = CFG_Guidance.IsNeedJumpThisTypeGuide(CFG_Guidance.GuideType.OpenBox);
        if (!needJump)
        {
            int id = CFG_Guidance.GetFirstIdByType(CFG_Guidance.GuideType.OpenBox);
            // 开箱引导
            var guide = new UIGuide.GuideData
            {
                ID = id,
                ButtonPos = JEngine.UI.JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().trans_GuideOpenBoxPosition),
                callBack = () =>
                {
                    OpenBoxRequest();
                }
            };
            RoleM.Instance.SetGuide(guide);
        }
        return !needJump;
    }

    private void CheckStartGuidePartnetUp(UICom_DownBtns uiHallDown)
    {
        if (!CFG_Guidance.IsNeedJumpThisTypeGuide(CFG_Guidance.GuideType.Partner) 
            && !RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner) 
            && !GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Role) 
            && IsLockToUnlock()
            && CheckPartnerSoltIsUnlock(1)
            && UITool.IsInHall())
        {
            if (autoOpenBoxState == AutoOpenBoxState.Enabled || autoOpenBoxState == AutoOpenBoxState.Paused)
            {
                DisableAutoOpenBox();
                GameHelper.Instance.AddInvoke(new TimeItem(1f, () =>
                {
                    if (isShowBoxUI)
                    {
                        GetView().BoxShowUI.gameObject.SetActive(false);
                    }
                }));
            }

            RoleM.Instance.guidePartnerRecordLevel = RoleM.Instance.Level;
            uiHallDown.StartGuide();
        }

        bool CheckPartnerSoltIsUnlock(int soltIdx)
        {
            bool isLock = CFG_XMLConfig.CheckPartnerSoltIsOpenServer(soltIdx, false);
            if (!isLock)
                isLock = CFG_XMLConfig.GetOpenSoltLv(soltIdx) > RoleM.Instance.Level;

            return !isLock;
        }

        bool IsLockToUnlock()
        {
           return RoleM.Instance.guidePartnerRecordLevel < CFG_XMLConfig.GetOpenSoltLv(1);
        }
    }

    public void CheckNewFunctionTip()
    {
        int theDayOpenServer = GameUtil.Instance.GetTheDayOpenServer();
        if (GameUtil.Instance.HaveNewFunctionTip(theDayOpenServer) && !GameUtil.Instance.CheckIsTipNewFunction(theDayOpenServer))
        {
            GameUtil.Instance.ShowNewFunctionTip(theDayOpenServer);
            GameUtil.Instance.SetIsTipNewFunction(theDayOpenServer, true);
        }
    }

    public void LoadChatHistyory()
    {
        TcpC.C2S_ChatHistoryRequest(0);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_ActiveState(GameEvent.Refresh_ActiveState _e)
    {
        if (isNullGo)
            return;
        HttpC.OnLevelGiftInfo();
        HttpC.OnSign30Info();
        HttpC.OnSign7Info();
        HttpC.GetFirstPayInfo();
        HttpC.GetPrivilegeList();

        SetTopActiveButton(default);

        //JUIExtensions.txt(GetView().root_leftBtns.Find("Btn_1/txtTime")).text = "剩余"+ GameUtil.Instance.GetTimeToDHM3(ActivityM.Instance.GetAcitvityEndTime(ActivityType.LimitBounty)).Days + "天";

        //bool isShow = false;
        //for (int i = 0; i < GetView().root_leftBtns.childCount; i++)
        //{
        //    var obj = GetView().root_leftBtns.Find("Btn_" + i);
        //    if (obj != null && obj.gameObject.activeSelf)
        //    {
        //        isShow = true;
        //    }
        //}
        //GetView().obj_leftBtns.SetActive(isShow);
        //if (ActivityM.Instance.GetAcitvityState(ActivityType.LimitBounty))
        //{
        //    RoleM.Instance.SetGuide(new UIGuilde.Guide
        //    {
        //        ID = 6,
        //        GuidePos = GetView().root_leftBtns.Find("Btn_1/root_guide").transform.position,
        //        callBack = delegate { UITool.Open<UILimitedBounty>(); },
        //    });

        //}

    }

    [Subscriber(ThreadMode.Main)]
    // 设置大厅活动开关显示和隐藏
    private void SetTopActiveButton(GameEvent.Refresh_HallActivityButton _e)
    {
        ShowOrHideActiveButton();

        if (_e.forceHide)
        {
            GetView().root_topBtns.Find("Btn_" + _e.index).gameObject.SetActive(false);
            ActivityM.Instance.CloseAcitvity(ActivityType.FirstPay);
        }
    }

    [Subscriber(ThreadMode.Main)]
    // 设置大厅活动开关显示和隐藏
    private void RefreshTopActiveButton(GameEvent.Refresh_ZeroTime _e)
    {
        HttpC.OnGetShopInfo();
        ShowOrHideActiveButton();
    }

    private void ShowOrHideActiveButton()
    {
        // 首充
        GetView().root_topBtns.Find("Btn_2").gameObject.SetActive(ActivityM.Instance.GetAcitvityState(ActivityType.FirstPay) && !GameM.Instance.IsAudit);
    }

    public override void OnDestroy()
    {
        JEvent.defaultEvent.Unregister(this);
        StopLocalHomeEncounter();

        ClearBindRed();
        ClearTime();
        ClearAutoOpenTime();
        ClearTimeOpenResult();
        ClearTimeCompareEquip();
        ClearWorldBossButtonTime();
        ClearWorldBossCountingTime();
        ClearFightEndButtonTime();
        ClearFightEndCountingTime();

        autoOpenBoxState = AutoOpenBoxState.Stopped;

        if (TopUI != null)
            TopUI.OnDestroy();

        if (DownUI != null)
            DownUI.OnDestroy();
    }

    //大厅聊天
    private void OnHallChatUI()
    {
        // 最多两个聊天信息
        List<ChatM.ChatInfo> chatInfo = ChatM.Instance.GetLastTwoChatOfAllType();
        for (int i = 0; i < 2; i++)
        {
            GetView().btn_Chat.transform.GetChild(i).gameObject.SetActive(false);
        }
        for (int i = 0; i < chatInfo.Count; i++)
        {
            GameObject go = GetView().btn_Chat.transform.GetChild(i).gameObject;
            go.SetActive(true);
            var oneChat = chatInfo[i];
            string content = "<color=#ffffff00>           </color>" + ChatM.Instance.GetChatByType(oneChat.ChatType, oneChat.Context, oneChat.Name);
            JUIExtensions.txt(go.transform).text = content;
            bool isEmjoy = ChatM.Instance.CheckIsEmoji(oneChat.Context);
            go.transform.Find("emjoy").gameObject.SetActive(isEmjoy);
            LayoutElement layoutElement = go.GetComponent<LayoutElement>();
            if (isEmjoy)
            {
                content += "\n";
                ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Chat", oneChat.Context, JUIExtensions.img(go.transform.Find("emjoy")));
                layoutElement.minHeight = 60;
            }
            else
            {
                layoutElement.minHeight = 30;
            }

            (string atlas, string icon) = ChatM.Instance.GetChatTypeImg(oneChat.ChatType);
            ResouseHelper.Instance.LoadSpriteByAtlas(atlas, icon, JUIExtensions.img(go.transform.Find("img_Tip")));
        }
    }


    [Subscriber(ThreadMode.Main)]
    private void OnRefreshChatUI(GameEvent.Sync_ChatPush _e)
    {
        if (uiGo == null || uiGo.Equals(null))
            return;
        OnHallChatUI();
    }

    bool isCheckGuide = false;

    [Subscriber(ThreadMode.Main)]
    private void OnSync_AdvanceInfo(GameEvent.Sync_UserAdvanceInfoPush _e)
    {
        if (!isCheckGuide) 
        {
            if (!RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.OpenBox))
            {
                CheckStartGuideOpenBox();
            }
            isCheckGuide = true;
        }
    }

    #region 大厅宝箱


    int closeCount = 0;
    bool isShowBoxUI;

    [Subscriber(ThreadMode.Main)]
    private void OnSync_Sync_UserBoxInfo(GameEvent.Sync_UserBoxInfo _e)
    {
        GetCurrentLevelBoxConfig();
        ShowBoxLevel();
        SetOpenBoxNumOnBoxLevelUp();
        // 宝箱红点
        RoleM.Instance.CheckBoxLevelRed();
    }

    TimeItem waitOpenBoxResult;

    private void ClearTimeOpenResult()
    {
        if (GameHelper.Instance != null && waitOpenBoxResult != null)
        {
            GameHelper.Instance.RemoveInvoke(waitOpenBoxResult);
            waitOpenBoxResult = null;
        }
    }

    TimeItem waitPupopCompareEquip;

    private void ClearTimeCompareEquip()
    {
        if (GameHelper.Instance != null && waitPupopCompareEquip != null)
        {
            GameHelper.Instance.RemoveInvoke(waitPupopCompareEquip);
            waitPupopCompareEquip = null;
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_PartnetChanged(GameEvent.CommonResponse _e)
    {
        if (isNullGo) return;

        if (_e.code == 0 && (_e.cid == CmdId.C2S_PartnerReplace || _e.cid == CmdId.C2S_PartnerCombat))
        {
            // 上阵的伙伴有变化时，刷新开箱结果的装备可穿戴状态
            OnRefreshHomeBox(default);

        }
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_HomeOpenBox(GameEvent.Sync_HomeOpenBox _e)
    {

        if (isNullGo) return;

        if (_e.data.Code == 0)
        {
            if (isNullGo) return;

            if (isRequiredOpenBox)
            {
                SpineUtil.Instance.PlayOnce(GetView().spine_Box, GameConst.ModelAnim.dead.ToString(), null);
                isRequiredOpenBox = false;
            }

            waitOpenBoxResult = GameHelper.Instance.AddInvoke(new TimeItem(0.8f, () =>
            {
                if (isNullGo) return;

                OnRefreshHomeBox(default);
            }));

            // 上报开箱数据
            ShuShuSendOpenBox();
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void CheckShowBoxUI(GameEvent.Refresh_ShowBoxUI _e)
    {
        if (isShowBoxUI)
            GetView().BoxShowUI.gameObject.SetActive(true);
    }

    [Subscriber(ThreadMode.Main)]
    //宝箱开启刷新
    private void OnRefreshHomeBox(GameEvent.Refresh_HomeOpen _e)
    {
        GetView().obj_OpenOne.gameObject.SetActive(RoleM.Instance.OpenBoxItems.Count == 1);
        GetView().root_BoxOpen.gameObject.SetActive(RoleM.Instance.OpenBoxItems.Count > 1);

        if (RoleM.Instance.OpenBoxItems.Count > 1)
        {
            TimeItem timeItem = null;

            for (int i = 0; i < GetView().root_BoxOpen.childCount; i++)
            {
                var root = GetView().root_BoxOpen.GetChild(i);
                root.gameObject.SetActive(i < RoleM.Instance.OpenBoxItems.Count);
                if (i < RoleM.Instance.OpenBoxItems.Count)
                {
                    var item = RoleM.Instance.OpenBoxItems[i];

                    GameUtil.Instance.SetItemIcon(item.CfgId, JUIExtensions.img(root, "icon"), JUIExtensions.img(root), null, (int)item.Equip.Quality);
                    GameUtil.Instance.SetItemJobIcon(item.CfgId, JUIExtensions.img(root, "icon_job"));

                    (int powerNum, int soltIdx) = GameUtil.Instance.GetEquipIsBest(item);
                    root.Find("up").gameObject.SetActive(powerNum > 0 && soltIdx >= 0);
                    root.Find("down").gameObject.SetActive(powerNum < 0 && soltIdx >= 0);
                    JUIExtensions.btn(root).onClick.RemoveAllListeners();

                    JUIExtensions.btn(root).onClick.AddListener(() =>
                    {
                        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                        // 正在一键穿装备
                        if (isAutoClothing)
                            return;

                        if (autoOpenBoxState == AutoOpenBoxState.Enabled)
                        {
                            if (timeItem != null)
                            {
                                GameHelper.Instance.RemoveInvoke(timeItem);
                            }
                            PauseAutoOpenBox();
                        }

                        UITool.Open<UIItemUseOrDel>(null, item, true);
                        if (soltIdx >= 0 && soltIdx != SelectTeamIdx)
                            OnTeamChoie(soltIdx);
                    });
                }
            }

            if (autoOpenBoxState == AutoOpenBoxState.Enabled)
            {
                List<ItemInfo>[] allItems = GetNeedSaveAndRecycleItems();
                if (IsForcePauseAutoOpen() || allItems[0].Count > 0)
                {
                    PauseAutoOpenBox();
                }
                else
                {
                    if (allItems[1].Count > 0)
                    {
                        long[] uids = new long[allItems[1].Count];
                        for (int i = 0; i < allItems[1].Count; i++)
                        {
                            uids[i] = allItems[1][i].Uid;
                        }
                        timeItem = GameHelper.Instance.AddInvoke(new TimeItem(0.6f, () =>
                        {
                            if (!isManualRecycled)
                            {
                                RecycleItems(uids);
                            }
                        }));
                    }
                }
            }
        }
        else if (RoleM.Instance.OpenBoxItems.Count == 1)
        {
            TimeItem timeItem = null;
            var item = RoleM.Instance.OpenBoxItems[0];
            GameUtil.Instance.SetItemJobIcon(item.CfgId, JUIExtensions.img(GetView().obj_OpenOne.transform, "icon_job"));
            GameUtil.Instance.SetItemIcon(item.CfgId, JUIExtensions.img(GetView().obj_OpenOne.transform, "icon"), JUIExtensions.img(GetView().obj_OpenOne.transform), null, (int)item.Equip.Quality);

            (int powerNum, int soltIdx) = GameUtil.Instance.GetEquipIsBest(item);
            GetView().obj_OpenOne.transform.Find("up").gameObject.SetActive(powerNum > 0 && soltIdx >= 0);
            GetView().obj_OpenOne.transform.Find("down").gameObject.SetActive(powerNum < 0 && soltIdx >= 0);

            JUIExtensions.btn(GetView().obj_OpenOne.transform).onClick.RemoveAllListeners();
            JUIExtensions.btn(GetView().obj_OpenOne.transform).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                // 正在一键穿装备
                if (isAutoClothing)
                    return;

                if (autoOpenBoxState == AutoOpenBoxState.Enabled)
                {
                    if (timeItem != null)
                    {
                        GameHelper.Instance.RemoveInvoke(timeItem);
                    }
                    PauseAutoOpenBox();
                }

                UITool.Open<UIItemUseOrDel>(null, item, true);
                if (soltIdx >= 0 && soltIdx != SelectTeamIdx)
                    OnTeamChoie(soltIdx);
            });

            if (autoOpenBoxState == AutoOpenBoxState.Enabled)
            {
                List<ItemInfo>[] allItems = GetNeedSaveAndRecycleItems();
                if (IsForcePauseAutoOpen() || allItems[0].Count > 0)
                {
                    PauseAutoOpenBox();
                    AutoOpenItemOP();
                }
                else
                {
                    BoxIsDel = true;
                    if (allItems[1].Count > 0)
                    {
                        long[] uids = new long[allItems[1].Count];
                        for (int i = 0; i < allItems[1].Count; i++)
                        {
                            uids[i] = allItems[1][i].Uid;
                        }
                        timeItem = GameHelper.Instance.AddInvoke(new TimeItem(0.6f, () =>
                        {
                            if (!isManualRecycled)
                            {
                                RecycleItems(uids);
                            }
                        }));
                    }
                }
            }
            else
            {
                if (!RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.OpenBox) && !RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner))
                    AutoOpenItemOP();
                else
                    BoxIsDel = true;
            }

            // 如果只开出一个物品，就自动打开道具处理界面
            void AutoOpenItemOP()
            {
                BoxIsDel = false;
                if (!IsHaveUpEquip() || !isAutoClothing)
                {
                    waitPupopCompareEquip = GameHelper.Instance.AddInvoke(new TimeItem(0.5f, () =>
                    {
                        if (isNullGo) return;
                        BoxIsDel = true;
                        UITool.Open<UIItemUseOrDel>(null, item, true);
                        if (soltIdx >= 0 && soltIdx != SelectTeamIdx)
                            OnTeamChoie(soltIdx);
                    }));
                }
                else
                {
                    BoxIsDel = true;
                }
            }
        }
        else // 所有开箱物品都已处理完毕。
        {
            // 如果自动开箱是暂停状态，在这里恢复一下。
            if (autoOpenBoxState == AutoOpenBoxState.Paused)
            {
                GameHelper.Instance.AddInvoke(new TimeItem(1f, () =>
                {
                    // 如果在等待延迟的过程中关闭或开启了自动开箱，就忽略此操作。
                    if (autoOpenBoxState == AutoOpenBoxState.Paused)
                    {
                        EnableAutoOpenBox();
                    }
                }));
            }

            SpineUtil.Instance.PlayLoop(GetView().spine_Box, GameConst.ModelAnim.stand.ToString());
        }

        GetView().btn_BoxClothing.gameObject.SetActive(GetOpenUpEquipCount() > 0 && !isAutoClothing);

        if (GetOpenUpEquipCount() > 0 && isAutoClothing)
        {
            ClothingEquip();
        }

        if (GetOpenUpEquipCount() == 0 && isAutoClothing)
            isAutoClothing = false;

        isShowBoxUI = RoleM.Instance.OpenBoxItems.Count > 0;
        if (isShowBoxUI)
        {
            if (!RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner))
            {
                GetView().BoxShowUI.gameObject.SetActive(true);
            }
        }
        else
        {
            GetView().BoxShowUI.gameObject.SetActive(false);
        }

        if (RoleM.Instance.OpenBoxItems.Count == 1 && autoOpenBoxState != AutoOpenBoxState.Enabled && RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.OpenBox))
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetView().BoxShowUI.transform as RectTransform);
            // 开箱引导
            var guide = new UIGuide.GuideData
            {
                ID = RoleM.Instance.GuideId,
                ButtonPos = JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().obj_OpenOne.transform),
                callBack = () =>
                {
                    UITool.Open<UIItemUseOrDel>(ui =>
                    {
                        if (RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.OpenBox))
                        {
                            var _ui = ui as UIItemUseOrDel;
                            Vector2 pos = _ui.GetUseButtonPositionRelativeCanvas();
                            RoleM.Instance.SetGuide(new UIGuide.GuideData
                            {
                                ID = RoleM.Instance.GuideId,
                                ButtonPos = pos,
                                callBack = () =>
                                {
                                    _ui.ClothEquip();
                                }
                            });
                        }
                    }, RoleM.Instance.OpenBoxItems[0], true);
                },
            };
            RoleM.Instance.SetGuide(guide);
        }
    }

    // 回收所有开箱开出的道具或装备
    private void RecycleItems(long[] uids)
    {
        TcpC.C2S_ItemDisassembly(uids);
    }

    public void CheckAutoOpenBoxGuide()
    {
        if (RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner))
        {
            var guide = new UIGuide.GuideData
            {
                ID = RoleM.Instance.GuideId,
                ButtonPos = JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().btn_BoxSet.transform),
                callBack = () =>
                {
                    OnOpenBoxSettingClick();
                },
            };
            RoleM.Instance.SetGuide(guide);
        }
    }

    bool isAutoClothing = false;

    private void OnClothingEquip()
    {
        isAutoClothing = true;
        GetView().btn_BoxClothing.gameObject.SetActive(false);
        ClothingEquip();
    }

    private void ClothingEquip()
    {
        ItemInfo item = GetOpenFirstUpEquip();
        (_, int soltIdx) = GameUtil.Instance.GetEquipIsBest(item);
        closeCount++;
        TcpC.C2S_ChangeEquip(item.Uid, 1, (int)CFG_Item.GetById(item.CfgId).Equiptype, soltIdx);
    }

    private int GetOpenUpEquipCount()
    {
        int count = 0;
        for (int i = 0; i < RoleM.Instance.OpenBoxItems.Count; i++)
        {
            var item = RoleM.Instance.OpenBoxItems[i];
            (int powerNum, int soltIdx) = GameUtil.Instance.GetEquipIsBest(item);
            if (powerNum > 0 && soltIdx >= 0)
            {
                count++;
            }
        }
        return count;
    }

    private bool IsHaveUpEquip()
    {
        for (int i = 0; i < RoleM.Instance.OpenBoxItems.Count; i++)
        {
            var item = RoleM.Instance.OpenBoxItems[i];
            (int powerNum, int soltIdx) = GameUtil.Instance.GetEquipIsBest(item);
            if (powerNum > 0 && soltIdx >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private ItemInfo GetOpenFirstUpEquip()
    {
        for (int i = 0; i < RoleM.Instance.OpenBoxItems.Count; i++)
        {
            var item = RoleM.Instance.OpenBoxItems[i];
            (int powerNum, int soltIdx) = GameUtil.Instance.GetEquipIsBest(item);
            if (powerNum > 0 && soltIdx >= 0)
            {
                return item;
            }
        }
        return null;
    }

    // 是否需要暂停自动开箱
    private bool IsForcePauseAutoOpen()
    {
        bool autoRecycleItemForBaseCondition = Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.AutoRecycleToggle.ToString()));
        if (!autoRecycleItemForBaseCondition)
        {
            return true;
        }

        bool isVacancyEnough = BagM.Instance.CheckEquipVacancy(NowIOpenNum);
        if (!isVacancyEnough)
        {
            return true;
        }

        return false;
    }

    // 获取需要保留和回收的道具或装备。 [0]. 需要保留的，[1]. 需要回收的。
    private List<ItemInfo>[] GetNeedSaveAndRecycleItems()
    {
        bool isSaveQuality = Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.QualityToggle.ToString()));
        bool isSaveLevel = Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.LevelToggle.ToString()));
        bool isSaveOccupation = Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.OccupationToggle.ToString()));

        int saveQuality = GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.Quality.ToString());
        int saveLevel = GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.Level.ToString());
        int saveOccupation = GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.Occupation.ToString());

        bool isHigherEquippedProtagonist = Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.HigherThenProtagonistEquippedToggle.ToString()));
        bool isHigherEquippedPartner = Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.HigherThenPartnerEquippedToggle.ToString()));

        List<ItemInfo>[] itemsArray = new List<ItemInfo>[2]
        {
            new List<ItemInfo>(),
            new List<ItemInfo>(),
        };

        for (int i = 0; i < RoleM.Instance.OpenBoxItems.Count; i++)
        {
            //* 基础条件 *//
            ItemInfo itemInfo = RoleM.Instance.OpenBoxItems[i];
            CFGItem_Item cfg_item = CFG_Item.GetById(itemInfo.CfgId);
            int quality = itemInfo.Type == pb.ItemType.EQUIPMENT ? (int)itemInfo.Equip.Quality : (int)cfg_item.Quality;
            bool qualityCondition = !isSaveQuality || quality >= saveQuality;
            bool levelCondition = !isSaveLevel || cfg_item.Levellimit >= saveLevel;
            bool occupationCondition = !isSaveOccupation || cfg_item.Occupylimit == saveOccupation;
            // 如果3个基础选项都没有勾选，就忽略这3个保留条件，进行其他条件判断。
            bool checkBaseToggle = isSaveQuality || isSaveLevel || isSaveOccupation;
            bool checkBaseCondition = checkBaseToggle && qualityCondition && levelCondition && occupationCondition;

            //* 高级条件 *//
            // powerNum: >0 up、<0 down, soltIdx:=0 主角、>0 伙伴
            (int powerNum, int soltIdx) = GameUtil.Instance.GetEquipIsBest(itemInfo);
            bool cpProtagonistCondition = isHigherEquippedProtagonist && soltIdx == 0 && powerNum > 0;
            bool cpPartnerCondition = isHigherEquippedPartner && soltIdx > 0 && powerNum > 0;

            // 满足保留条件的道具
            if (checkBaseCondition || cpProtagonistCondition || cpPartnerCondition)
            {
                itemsArray[0].Add(itemInfo);
            }
            else
            {
                itemsArray[1].Add(itemInfo);
            }
        }

        return itemsArray;
    }


    private int MaxOpenNum = 1;
    private int NowIOpenNum = 1;
    private int boxLevel = 0;
    CFGItem_BoxLevel cfgBoxLevelItem;

    private void GetCurrentLevelBoxConfig()
    {
        // 服务端新角色可能下发 BoxNowlevel=0，而 boxlevel.json 的 id 从 1 开始，
        // GetById(0) 会抛 KeyNotFoundException 并截断 ShowUI 里后续的全部初始化
        int boxLv = RoleM.Instance.BoxNowlevel;
        if (boxLv <= 0)
            boxLv = 1;

        cfgBoxLevelItem = CFG_BoxLevel.GetById(boxLv);
        MaxOpenNum = cfgBoxLevelItem.Maxtime;
    }

    private void OnBoxShow()
    {
        GetCurrentLevelBoxConfig();
        GetView().txt_BoxNum.text = RoleM.Instance.TreasuerBox.ToString();
        SetBoxTime();
    }

    private void SetDefaultOpenNum()
    {
        NowIOpenNum = MaxOpenNum;
        GetView().txt_BoxOpenNum.text = NowIOpenNum.ToString();
    }

    private void SetOpenBoxNumOnBoxLevelUp()
    {
        if (RoleM.Instance.BoxNowlevel > boxLevel)
        {
            boxLevel = RoleM.Instance.BoxNowlevel;
            SetDefaultOpenNum();
        }
    }

    private TimeItem BoxTime;

    private void ClearTime()
    {
        if (GameHelper.Instance != null && BoxTime != null)
        {
            GameHelper.Instance.RemoveInvoke(BoxTime);
            BoxTime = null;
        }
    }

    private void SetBoxTime()
    {
        ClearTime();
        if (RoleM.Instance.BoxTimeAllSecound <= 0)
        {
            OnTimeEnd();
            OnHomeAutoEvent(0, 0);
            return;
        }
        BoxTime = GameHelper.Instance.AddInvoke(new TimeItem(RoleM.Instance.BoxTimeAllSecound, 1f, () =>
        {
            if (isNullGo) return;

            GetView().txt_boxTime.text = $"({GameUtil.Instance.GetTimeSToHMS(RoleM.Instance.BoxTimeAllSecound)})";
            RoleM.Instance.BoxTimeAllSecound--;
            GetView().obj_boxTimeoff.SetActive(RoleM.Instance.BoxTimeAllSecound <= 0);
            GetView().obj_boxTimeon.SetActive(RoleM.Instance.BoxTimeAllSecound > 0);
            if (RoleM.Instance.BoxTimeAllSecound <= 0)
            {
                SetBoxTime();
            }
        }));

        void OnTimeEnd()
        {
            GetView().obj_boxTimeoff.SetActive(RoleM.Instance.BoxTimeAllSecound <= 0);
            GetView().obj_boxTimeon.SetActive(RoleM.Instance.BoxTimeAllSecound > 0);
            GetView().txt_boxTime.text = "";
        }
    }

    // 设置按钮按下时操作
    private void OnOpenBoxSettingClick()
    {
        switch (autoOpenBoxState)
        {
            case AutoOpenBoxState.Enabled:
            case AutoOpenBoxState.Paused:
                DisableAutoOpenBox();
                break;
            case AutoOpenBoxState.Stopped:
                UITool.Open<UIOpenBoxSetting>(null, new UIOpenBoxSetting.Callback(EnableAutoOpenBox));
                break;
            default:
                break;
        }
    }

    // 自动开箱频率
    private float autoOpenBoxInterval = 2.5f;

    private TimeItem AutoOpenBoxTimeItem;

    private void ClearAutoOpenTime()
    {
        if (GameHelper.Instance != null && AutoOpenBoxTimeItem != null)
        {
            GameHelper.Instance.RemoveInvoke(AutoOpenBoxTimeItem);
            AutoOpenBoxTimeItem = null;
        }
    }

    // 启用自动开箱
    private void EnableAutoOpenBox()
    {
        if (isNullGo) return;
        ClearAutoOpenTime();
        AutoOpenBoxTimeItem = GameHelper.Instance.AddInvoke(new TimeItem(-1, autoOpenBoxInterval, OpenBoxRequest, true));
        OpenBoxSettingButtonAnimation(true);
        autoOpenBoxState = AutoOpenBoxState.Enabled;
    }

    // 关闭自动开箱
    private void DisableAutoOpenBox()
    {
        ClearAutoOpenTime();
        OpenBoxSettingButtonAnimation(false);
        autoOpenBoxState = AutoOpenBoxState.Stopped;
    }

    // 暂停自动开箱。达到某些条件时需要手动操作开箱
    private void PauseAutoOpenBox()
    {
        ClearAutoOpenTime();
        // 只有在自动开启宝箱启用的时候才能进入暂停状态
        if (autoOpenBoxState == AutoOpenBoxState.Enabled)
        {
            autoOpenBoxState = AutoOpenBoxState.Paused;
        }
    }

    private void OnBoxOpneNumChange(bool isAdd)
    {
        if (isAdd)
            NowIOpenNum++;
        else
            NowIOpenNum--;

        NowIOpenNum = Mathf.Clamp(NowIOpenNum, 1, MaxOpenNum);
        GetView().txt_BoxOpenNum.text = NowIOpenNum.ToString();
    }

    private void ShowBoxLevel()
    {
        GetView().txt_BoxLv.text = "Lv." + RoleM.Instance.BoxNowlevel;
    }

    private void OpenBoxSettingButtonAnimation(bool play)
    {
        GetView().ani_Btn_BoxSet.SetBool("AutoOpen", play);
    }

    bool isRequiredOpenBox = false;

    long boxNumAfterOpen;

    /// <summary>
    /// 开箱请求
    /// </summary>
    public void OpenBoxRequest()
    {
        if (RoleM.Instance.OpenBoxItems.Count > 0)
        {
            return;
        }

        if (!BagM.Instance.CheckEquipVacancy(NowIOpenNum))
        {
            UITool.ShowTips("背包空间置不足，无法开箱。请清理背包装备后再试。");
            return;
        }

        if (RoleM.Instance.TreasuerBox <= 0)
        {
            UITool.ShowTips("开启次数不足");
            DisableAutoOpenBox();
            return;
        }

        isRequiredOpenBox = true;
        int openNum = (int)RoleM.Instance.TreasuerBox < NowIOpenNum ? (int)RoleM.Instance.TreasuerBox : NowIOpenNum;
        int openLevel = RoleM.Instance.GetDefaultOpenBoxEquipLevel();
        int openJob = RoleM.Instance.GetDefaultOpenBoxEquipJob();
        isManualRecycled = false;
        boxNumAfterOpen = RoleM.Instance.TreasuerBox;
        TcpC.C2S_HomeOpenBox(openNum, openLevel, openJob);
    }

    #endregion

    private GameObject MonsterObj;

    private string[] SpineNames = new string[5];
    private void SetRoleSpine()
    {
        JEngine.Core.Log.Print($"[UIHall] SetRoleSpine job={RoleM.Instance.Job} sex={RoleM.Instance.Sex} roots={(GetView().SpintRoots == null ? -1 : GetView().SpintRoots.Length)}");

        for (int i = 0; i < GetView().SpintRoots.Length; i++)
        {
            string poolKey = null, poolPath = null;
            int idx = i;
            if (i == 0)
            {
                poolKey = "MainRole" + RoleM.Instance.Job + RoleM.Instance.Sex;
                poolPath = GameConst.GetModelByJobAndSex((JobType)RoleM.Instance.Job, (SexType)RoleM.Instance.Sex);
                JEngine.Core.Log.Print($"[UIHall] 主角模型 key={poolKey} path={poolPath}");
            }
            else
            {
                // 单机模式下伙伴槽可能为空，GetOnePartnerSlotInfo 会返回 null，
                // 原代码直接访问 solt.PartnerUid 会 NRE，进而中断整条初始化链
                var solt = PartnerM.Instance.GetOnePartnerSlotInfo(idx);
                if (solt != null && solt.PartnerUid != 0)
                {
                    poolKey = "Partner_" + PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid).CfgId;
                    poolPath = GameConst.GetMonsterModelByID(poolKey);
                }
            }

            if (string.IsNullOrEmpty(poolKey))
            {
                ClearRoot();
                SpineNames[i] = poolKey;
            }
            else
            {
                if (SpineNames[i] != poolKey)
                {
                    _ = GameHelper.Instance.SetAsyncObj(poolKey, poolPath, (go) =>
                    {
                        SpineNames[idx] = poolKey;
                        ClearRoot();
                        go.SetActive(true);
                        go.transform.SetParent(GetView().SpintRoots[idx]);
                        go.transform.localPosition = Vector3.zero;
                        go.transform.localScale = Vector3.one;
                        go.transform.localScale = new Vector3(-1, 1, 1);
                        OnSetAnim(go);
                    });
                }
            }
            void ClearRoot()
            {
                GameHelper.Instance.ClearObjToPool(GetView().SpintRoots[idx]);
            }
        }
    }

    /// <summary>
    /// 大厅挂机时主角的出手序列。
    ///
    /// 不同职业的 Spine 动画集不一样（战士有 skill2，法师没有），
    /// 播放 Spine 里不存在的动画会被静默忽略、白白空转一段时间，
    /// 所以按职业区分，映射参考 UICreatRole.RoleBronAnim。
    /// </summary>
    private List<string> BuildHallAttackAnims()
    {
        var list = new List<string>();
        int job = RoleM.Instance.Job;

        if (job == (int)JobType.Warrior)
        {
            list.Add("skill2");
            list.Add("skill3");
        }
        else if (job == (int)JobType.Mage)
        {
            list.Add("skill1");
            list.Add("skill3");
        }
        else
        {
            // Sacrifice（祭祀）
            list.Add("skill1");
        }

        // 技能之后补一记普攻，避免全程都在放技能显得单调
        list.Add("attack");
        return list;
    }

    private void OnSetAnim(GameObject AnimRole = null)
    {
        if (AnimRole != null)
        {
            PlayAnim(AnimRole);
        }
        else
        {
            for (int i = 0; i < GetView().SpintRoots.Length; i++)
            {
                if (!string.IsNullOrEmpty(SpineNames[i]))
                    PlayAnim(GetView().SpintRoots[i].GetChild(0).gameObject);
            }
        }

        void PlayAnim(GameObject TeamObj)
        {
            if (_AnimType == AnimType.Move)
                SetBgMove(true);
            else
                SetBgMove(false);

            if (_AnimType == AnimType.Idle || _AnimType == AnimType.Move)
            {
                Transform spine = TeamObj.transform.Find("spine");
                if (spine != null)
                    SpineUtil.Instance.PlayLoop(spine.GetComponent<SkeletonGraphic>(), (_AnimType == AnimType.Idle ? "stand" : "walk"));
            }
            else
            {
                // 攻击序列由 BeginHomeEncounter / StartHeroAttackSequence 统一调度。
                // 这里只负责停止背景滚动，避免同一个动作被多个入口重复播放。
                SetBgMove(false);
            }
        }
    }

    private enum AnimType { Idle, Move, Attack }
    private AnimType _AnimType = AnimType.Idle;
    private TimeItem _homeEncounterTimer;

    /// <summary>上一只怪是否还在异步加载中（防止多轮重叠发起，导致竞态）</summary>
    private bool _monsterLoading;

    /// <summary>遇怪表现序列号，用于丢弃过期的异步回调和特效。</summary>
    private int _encounterSeq;

    /// <summary>当前怪物视觉配置。</summary>
    private HallMonsterCatalog.Visual _currentMonsterVisual;

    /// <summary>
    /// 单机大厅挂机：本地驱动遇怪，**不依赖** JEvent → ProtoC → UIHall 那条推送链路。
    ///
    /// 之前把驱动放在 LocalPusher 里用 Post 推，实测大厅始终刷不出怪
    /// （主角停在 Idle，说明 OnHomeAutoEvent 压根没被调用）。
    /// 大厅挂机的表现本来就是 UI 层的职责，单机下由 UI 自己 tick 最直接。
    ///
    /// 按 onlinebattle.triggertime 触发：选怪 → 异步加载 → 待机
    /// → 玩家出手/技能特效 → 怪物反击 → 死亡 → 回收，再继续走路。
    /// </summary>
    private void StartLocalHomeEncounter()
    {
        if (!HotUpdateScripts.LocalNet.LocalServer.Enabled)
            return;
        if (_homeEncounterTimer != null)
            return;

        float interval = 10f;
        try
        {
            if (CFG_XMLConfig.xmlConfig != null
                && CFG_XMLConfig.xmlConfig.BaseValue != null
                && CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle != null
                && CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle.triggerTime > 0)
                interval = CFG_XMLConfig.xmlConfig.BaseValue.onlineBattle.triggerTime;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[UIHall] 读取 onlinebattle.triggertime 失败，使用 10 秒: " + e.Message);
        }

        _homeEncounterTimer = new TimeItem(0f, interval, () =>
        {
            if (isNullGo || MonsterObj != null || _monsterLoading)
                return;
            OnHomeAutoEvent(2, 0);
        }, true, true);

        GameHelper.Instance.AddInvoke(_homeEncounterTimer);
        JEngine.Core.Log.Print("[UIHall] 本地挂机遇怪已启动（间隔 " + interval + "s）");
    }

    private void StopLocalHomeEncounter()
    {
        _encounterSeq++;
        _monsterLoading = false;

        if (_homeEncounterTimer != null && GameHelper.Instance != null)
        {
            GameHelper.Instance.RemoveInvoke(_homeEncounterTimer);
            _homeEncounterTimer = null;
        }

        if (MonsterObj != null && PoolHelper.Instance != null)
        {
            PoolHelper.Instance.RecycObj(MonsterObj);
            MonsterObj = null;
        }
        _currentMonsterVisual = null;
    }

    private void OnHomeAutoEvent(int _Type, int cfg_ID = 0, List<ItemInfo> PropInfo = null)
    {
        JEngine.Core.Log.Print($"[UIHall] OnHomeAutoEvent Type={_Type} cfgId={cfg_ID}");

        // 遇怪不是“立即播一段动画”，而是先加载怪物并建立一条可取消的表现序列。
        if (_Type == 2)
        {
            BeginHomeEncounter();
            return;
        }

        // 上一只怪现在挂在主角层（不在 root_Event 下），必须单独回收，否则会堆积
        if (MonsterObj != null)
        {
            PoolHelper.Instance.RecycObj(MonsterObj);
            MonsterObj = null;
        }

        if (GetView().root_Event != null)
            GameHelper.Instance.ClearObjToPool(GetView().root_Event);   // 清掉落物
        else
            Debug.LogWarning("[UIHall] root_Event 未绑定，跳过掉落物清理");
        if (_Type == 1)
        {
            _ = GameHelper.Instance.SetAsyncObj("Event_Item", "UI/Modle/Event_Item", (go) =>
            {
                go.gameObject.SetActive(true);
                go.transform.SetParent(GetView().root_Event);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one * 4;
                GameUtil.Instance.SetItemIcon(cfg_ID, JUIExtensions.img(go.transform));
                GameHelper.Instance.AddInvoke(new TimeItem(1.5f, () =>
                {
                    if (isNullGo || GetView().obj_fly == null) return;
                    GetView().obj_fly.transform.position = go.transform.position;
                    PoolHelper.Instance.RecycObj(go);
                    if (DownUI != null)
                    {
                        GetView().obj_fly.gameObject.SetActive(true);
                        var endPos = (cfg_ID >= 30 && cfg_ID <= 34) ? DownUI.GetView().img_JyPro.transform.position : GetView().btn_BoxOpen.transform.position;
                        float timefly = (cfg_ID >= 30 && cfg_ID <= 34) ? 1.2f : 0.5f;
                        GetView().obj_fly.transform.DOMove(endPos, timefly).OnComplete(() =>
                        {
                            GetView().obj_fly.gameObject.SetActive(false);
                        });
                    }
                }));
            });
        }
        if (RoleM.Instance.BoxTimeAllSecound <= 0)
            _AnimType = AnimType.Idle;
        else
            _AnimType = AnimType.Move;

        OnSetAnim();
    }

    /// <summary>
    /// 开始一轮完整遇怪：选择外观 → 异步加载 → 待机 → 玩家攻击。
    /// 在怪物真正加载完成前，不会提前播放攻击特效。
    /// </summary>
    private void BeginHomeEncounter()
    {
        if (isNullGo || _monsterLoading || MonsterObj != null)
            return;

        if (RoleM.Instance.BoxTimeAllSecound <= 0)
        {
            _AnimType = AnimType.Idle;
            OnSetAnim();
            return;
        }

        int seq = ++_encounterSeq;
        _monsterLoading = true;

        HallMonsterCatalog.Visual visual = HallMonsterCatalog.Pick();
        _currentMonsterVisual = visual;
        float scal = UnityEngine.Random.Range(visual.MinScale, visual.MaxScale);

        // 加载失败时不会执行回调，兜底释放锁，下一轮可以重试。
        GameHelper.Instance.AddInvoke(new TimeItem(4f, () =>
        {
            if (seq == _encounterSeq)
                _monsterLoading = false;
        }));

        string poolKey = visual.Key;
        string poolPath = visual.Path;
        _ = GameHelper.Instance.SetAsyncObj(poolKey, poolPath, (go) =>
        {
            if (go == null)
            {
                if (seq == _encounterSeq)
                    _monsterLoading = false;
                return;
            }
            if (isNullGo)
            {
                PoolHelper.Instance.RecycObj(go);
                return;
            }

            // 过期的异步回调不能覆盖当前怪物。
            if (seq != _encounterSeq)
            {
                PoolHelper.Instance.RecycObj(go);
                return;
            }

            _monsterLoading = false;

            Transform mainRoot = GetView().SpintRoots != null && GetView().SpintRoots.Length > 0
                ? GetView().SpintRoots[0] : null;

            Transform parent;
            Vector3 localPos;

            if (mainRoot != null && mainRoot.parent != null)
            {
                parent = mainRoot.parent;
                localPos = mainRoot.localPosition + new Vector3(280f, -60f, 0f);
            }
            else if (GetView().root_Event != null)
            {
                parent = GetView().root_Event;
                localPos = Vector3.zero;
                if (!GetView().root_Event.gameObject.activeSelf)
                    GetView().root_Event.gameObject.SetActive(true);
            }
            else
            {
                PoolHelper.Instance.RecycObj(go);
                return;
            }

            // 先停用再挂载，确保对象池取出的粒子/Spine 从头开始。
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(
                visual.DefaultFlipX ? -scal : scal,
                scal,
                1f);
            go.transform.SetAsLastSibling();
            go.SetActive(true);

            MonsterObj = go;
            PlayMonsterLoop(go, visual.IdleAnim);

            JEngine.Core.Log.Print(
                "[UIHall] 刷怪 seq=" + seq + " key=" + visual.Key
                + " boss=" + visual.IsBoss
                + " scal=" + scal.ToString("F2")
                + " flip=" + visual.DefaultFlipX
                + " pos=" + localPos);

            StartHeroAttackSequence(seq, go);
        });
    }

    /// <summary>玩家出手与怪物反击的主序列。</summary>
    private void StartHeroAttackSequence(int seq, GameObject monster)
    {
        if (isNullGo || monster == null || seq != _encounterSeq || MonsterObj != monster)
            return;

        GameObject teamObj = null;
        if (GetView().SpintRoots != null && GetView().SpintRoots.Length > 0
            && GetView().SpintRoots[0] != null && GetView().SpintRoots[0].childCount > 0)
            teamObj = GetView().SpintRoots[0].GetChild(0).gameObject;

        if (teamObj == null)
        {
            FinishMonster(seq, monster);
            return;
        }

        Transform spineRoot = teamObj.transform.Find("spine");
        SkeletonGraphic playerSkeleton = spineRoot != null
            ? spineRoot.GetComponent<SkeletonGraphic>() : null;
        if (playerSkeleton == null)
        {
            FinishMonster(seq, monster);
            return;
        }

        _AnimType = AnimType.Attack;
        SetBgMove(false);

        // 技能刀光在玩家开始出手后出现；怪物攻击放在中段，形成有来有回的感觉。
        GameHelper.Instance.AddInvoke(new TimeItem(0.12f, () =>
        {
            if (seq == _encounterSeq && MonsterObj == monster)
                PlayHallSkillEffect(seq, monster);
        }));
        GameHelper.Instance.AddInvoke(new TimeItem(0.65f, () =>
        {
            if (seq == _encounterSeq && MonsterObj == monster)
                PlayMonsterAttack(seq, monster);
        }));

        var animList = BuildHallAttackAnims();
        animList.Add("walk");
        SpineUtil.Instance.PlayQueue(playerSkeleton, animList, () =>
        {
            FinishMonster(seq, monster);
        }, true);
    }

    /// <summary>
    /// 播放职业技能特效。
    ///
    /// 关键约束：
    /// - 使用 GameConst.EffDic 中已有的职业技能特效；
    /// - 不覆盖 Prefab 根缩放，保留美术配置；
    /// - 父层与怪物同级并置顶，避免被角色/场景压住；
    /// - 池化对象重新激活后显式重启粒子与 Spine。
    /// </summary>
    private void PlayHallSkillEffect(int seq, GameObject target)
    {
        if (isNullGo || target == null || seq != _encounterSeq || MonsterObj != target)
            return;

        string effectKey = GetJobAttackEffectKey();
        if (string.IsNullOrEmpty(effectKey))
            return;

        string effectPath = GameConst.GetEffPath(effectKey);
        Transform parent = target.transform.parent;
        Vector3 localPos = target.transform.localPosition;

        _ = GameHelper.Instance.SetAsyncObj("hall_" + effectKey, effectPath, (go) =>
        {
            if (go == null)
                return;
            if (isNullGo)
            {
                PoolHelper.Instance.RecycObj(go);
                return;
            }

            if (seq != _encounterSeq || MonsterObj != target || parent == null)
            {
                PoolHelper.Instance.RecycObj(go);
                return;
            }

            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.SetAsLastSibling();
            go.SetActive(true);
            RestartHallEffect(go);

            JEngine.Core.Log.Print("[UIHall] 技能特效 " + effectKey
                + " parent=" + parent.name + " pos=" + localPos);

            GameObject effectObj = go;
            int effectSeq = seq;
            GameHelper.Instance.AddInvoke(new TimeItem(1.6f, () =>
            {
                if (effectObj == null)
                    return;
                if (effectSeq != _encounterSeq || MonsterObj != target)
                {
                    PoolHelper.Instance.RecycObj(effectObj);
                    return;
                }
                PoolHelper.Instance.RecycObj(effectObj);
            }));
        }, true);
    }

    private string GetJobAttackEffectKey()
    {
        int job = RoleM.Instance.Job;
        if (job == (int)JobType.Warrior)
            return "eff_ZSAttack";
        if (job == (int)JobType.Mage)
            return "eff_FSAttack";
        return "eff_JSAttack";
    }

    private void RestartHallEffect(GameObject effect)
    {
        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null)
                continue;
            particles[i].gameObject.SetActive(true);
            particles[i].Clear(true);
            particles[i].Play(true);
        }

        SkeletonGraphic[] graphics = effect.GetComponentsInChildren<SkeletonGraphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null)
                continue;
            graphics[i].gameObject.SetActive(true);
            graphics[i].AnimationState.ClearTracks();

            // Zs_Attack / FS_Attack / JS_Attack 的骨架动画名是 hit；
            // 兼容其他特效 Prefab 使用 animation 命名的情况。
            string effectAnim = ResolveMonsterAnimation(graphics[i], "hit", "animation");
            if (!string.IsNullOrEmpty(effectAnim))
                graphics[i].AnimationState.SetAnimation(0, effectAnim, false);
        }
    }

    private void PlayMonsterLoop(GameObject monster, string animationName)
    {
        SkeletonGraphic skeleton = GetMonsterSkeleton(monster);
        if (skeleton == null || !HasMonsterAnimation(skeleton, animationName))
            return;
        SpineUtil.Instance.PlayLoop(skeleton, animationName);
    }

    private void PlayMonsterAttack(int seq, GameObject monster)
    {
        if (isNullGo || monster == null || seq != _encounterSeq || MonsterObj != monster)
            return;

        SkeletonGraphic skeleton = GetMonsterSkeleton(monster);
        HallMonsterCatalog.Visual visual = _currentMonsterVisual;
        if (skeleton == null || visual == null)
            return;

        string attackAnim = ResolveMonsterAnimation(skeleton, visual.AttackAnim, "attack");
        if (string.IsNullOrEmpty(attackAnim))
            return;

        SpineUtil.Instance.PlayOnce(skeleton, attackAnim, () =>
        {
            if (seq == _encounterSeq && MonsterObj == monster)
                PlayMonsterLoop(monster, visual.IdleAnim);
        });
    }

    private void FinishMonster(int seq, GameObject monster)
    {
        if (isNullGo || monster == null || seq != _encounterSeq || MonsterObj != monster)
            return;

        SkeletonGraphic skeleton = GetMonsterSkeleton(monster);
        HallMonsterCatalog.Visual visual = _currentMonsterVisual;
        string deadAnim = skeleton != null && visual != null
            ? ResolveMonsterAnimation(skeleton, visual.DeadAnim, "dead")
            : null;

        if (string.IsNullOrEmpty(deadAnim))
        {
            GameHelper.Instance.AddInvoke(new TimeItem(0.2f, () =>
            {
                RecycleEncounterMonster(seq, monster);
            }));
            return;
        }

        SpineUtil.Instance.PlayOnce(skeleton, deadAnim, () =>
        {
            RecycleEncounterMonster(seq, monster);
        });

        // Complete 事件丢失时兜底回收，避免怪物永久停留。
        GameHelper.Instance.AddInvoke(new TimeItem(1.2f, () =>
        {
            RecycleEncounterMonster(seq, monster);
        }));
    }

    private void RecycleEncounterMonster(int seq, GameObject monster)
    {
        if (isNullGo || seq != _encounterSeq || monster == null || MonsterObj != monster)
            return;

        Vector3 monsterPos = monster.transform.position;
        PoolHelper.Instance.RecycObj(monster);
        MonsterObj = null;
        _currentMonsterVisual = null;
        _AnimType = AnimType.Move;
        SetBgMove(true);

        if (HotUpdateScripts.LocalNet.LocalServer.Enabled)
        {
            HotUpdateScripts.LocalNet.LocalRole localRole =
                HotUpdateScripts.LocalNet.LocalData.Instance.Current;
            if (localRole != null)
            {
                HotUpdateScripts.LocalNet.TaskService.Instance.Dispatch(
                    localRole, TargetType.KILL_MONSTER_REQUIREMENT, 0, 1);
                HotUpdateScripts.LocalNet.TaskService.Instance.Dispatch(
                    localRole, TargetType.TOTAL_KILLS_OF_NORMAL_MONSTERS, 0, 1);
            }
            HotUpdateScripts.LocalNet.BoxService.Instance.AddTreasure(1);

            var idle = HotUpdateScripts.LocalNet.IdleRewardService.Instance.SettleCurrent();
            if (idle != null && idle.Applied)
            {
                HotUpdateScripts.LocalNet.LocalPusher.Instance.PushUserBaseNow();
                HotUpdateScripts.LocalNet.LocalPusher.Instance.PushBagInfoNow();
                if (idle.LevelsGained > 0)
                {
                    HotUpdateScripts.LocalNet.LocalPusher.Instance.PushStatsNow();
                    HotUpdateScripts.LocalNet.LocalPusher.Instance.PushLevelUpNow(idle.NewLevel);
                }
                if (idle.Mailed)
                    UITool.ShowTips("背包已满，奖励已转邮件");
            }
            else if (idle != null && idle.BagFull)
            {
                UITool.ShowTips(idle.Error);
            }
        }

        if (DownUI != null && GetView().obj_fly != null)
        {
            GetView().obj_fly.transform.position = monsterPos;
            Vector3 endPos = DownUI.GetView().transform.Find("Btn_5/icon").position;
            GetView().obj_fly.gameObject.SetActive(true);
            GetView().obj_fly.transform.DOMove(endPos, 0.5f).OnComplete(() =>
            {
                GetView().obj_fly.gameObject.SetActive(false);
            });
        }
    }

    private static SkeletonGraphic GetMonsterSkeleton(GameObject monster)
    {
        if (monster == null)
            return null;
        return monster.GetComponentInChildren<SkeletonGraphic>(true);
    }

    private static bool HasMonsterAnimation(SkeletonGraphic skeleton, string animationName)
    {
        if (skeleton == null || skeleton.Skeleton == null || skeleton.Skeleton.Data == null
            || string.IsNullOrEmpty(animationName))
            return false;
        return skeleton.Skeleton.Data.FindAnimation(animationName) != null;
    }

    private static string ResolveMonsterAnimation(
        SkeletonGraphic skeleton,
        string preferred,
        string fallback)
    {
        if (!string.IsNullOrEmpty(preferred) && HasMonsterAnimation(skeleton, preferred))
            return preferred;
        if (!string.IsNullOrEmpty(fallback) && HasMonsterAnimation(skeleton, fallback))
            return fallback;
        return null;
    }

    private string PetPool = string.Empty;
    private void SetPetSpine()
    {
        var myPet = PetM.Instance.GetOnePetByIdx(0);
        if (myPet.CfgId == 0)
        {
            PetPool = string.Empty;
            GameHelper.Instance.ClearObjToPool(GetView().root_Pet);
        }
        else
        {
            string pooName = "Pet_" + myPet.CfgId;
            if (pooName == PetPool) return;
            _ = GameHelper.Instance.SetAsyncObj(pooName, GameConst.GetMonsterModelByID(pooName), (_mainRole) =>
            {
                PetPool = pooName;
                GameHelper.Instance.ClearObjToPool(GetView().root_Pet);
                _mainRole.transform.SetParent(GetView().root_Pet);
                _mainRole.transform.localScale = Vector3.one;
                _mainRole.transform.localPosition = Vector3.zero;
                _mainRole.SetActive(true);
            });
        }

    }

    //宠物刷新
    [Subscriber(ThreadMode.Main)]
    private void Refresh_PetsInfo(GameEvent.Refresh_PetsInfo _e)
    {
        if (isNullGo) return;
        SetPetSpine();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnHomeAutoEncounter(GameEvent.Sync_HomeAutoEncounter _e)
    {
        if (isNullGo)
            return;
        OnHomeAutoEvent(_e.data.Type, _e.data.Id, _e.data.ItemInfo);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_SyncUserBasePush(GameEvent.Sync_UserBase _e)
    {
        if (isNullGo)
            return;

        OnBoxShow();
        RoleM.Instance.CheckBoxLevelRed();

        if (GuildM.Instance.IsGuildOpen())
            GuildM.Instance.CheckGuildSkillRed();

    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_Refresh_LvUp(GameEvent.Refresh_LvUp _e)
    {
        if (isNullGo)
            return;
        OnSetBgSprit();
        HttpC.OnGetGrowthFundInfo();
        if (DownUI == null) return;
        CheckStartGuidePartnetUp(DownUI);
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_ChangeEquip(GameEvent.ChangeEquip _e)
    {
        if (isNullGo) return;
        if (SelectTeamIdx == 0)
            ShowDressUI(SelectTeamIdx);

        (int powerNum, int soltIdx) = GameUtil.Instance.GetEquipIsBest(_e.item);

        if (soltIdx >= 0 && soltIdx != SelectTeamIdx)
        {
            OnTeamChoie(soltIdx);

            foreach (var item in equipEffectsDic)
                PoolHelper.Instance.RecycObj(item.Value);

            equipEffectsDic.Clear();
        }

        SetDressEff(_e.soldIdx);
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_BackHall(GameEvent.BackUIHall _e)
    {
        if (_e.isInHall)
        {
            if (isNullGo || DownUI == null) return;
            CheckStartGuidePartnetUp(DownUI);
        }
    }

    #region 埋点数据，开箱物品

    private void ShuShuSendOpenBox()
    {
        int openLevel = RoleM.Instance.GetDefaultOpenBoxEquipLevel();
        int openJob = RoleM.Instance.GetDefaultOpenBoxEquipJob();

        string job = "";
        switch (openJob)
        {
            case 0:
                job = "全部默认";
                break;
            case 1:
                job = "战士";
                break;
            case 2:
                job = "法师";
                break;
            case 3:
                job = "祭祀";
                break;
            default:
                break;
        }

        HttpC.ShuShuSendOpenBox(new HTTP_WindowShusOpenBoxRequest
        {
            name = "open_box",
            box_item_info = ShuShuGetOpenItems(),
            is_auto = autoOpenBoxState != AutoOpenBoxState.Stopped,
            power_up = 0,
            open_num = RoleM.Instance.OpenBoxItems.Count,
            role_power_stop = Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.HigherThenProtagonistEquippedToggle.ToString(), 1)),
            partner_power_stop = Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.HigherThenPartnerEquippedToggle.ToString(), 1)),
            box_num_before = (int)boxNumAfterOpen,
            box_num_after = (int)RoleM.Instance.TreasuerBox,
            box_job_limited = job,
            box_level_limited = openLevel,
        });
    }

    private HTTP_BoxItemInfo[] ShuShuGetOpenItems()
    {
        List<HTTP_BoxItemInfo> boxItems = new List<HTTP_BoxItemInfo>();
        foreach (var item in RoleM.Instance.OpenBoxItems)
        {
            var cfgItem = CFG_Item.GetById(item.CfgId);
            boxItems.Add(new HTTP_BoxItemInfo
            {
                item_id = item.CfgId.ToString(),
                item_level = int.TryParse(cfgItem.Levellimit.ToString(), out int level) ? level : 0,
                item_quality = item.Type == pb.ItemType.EQUIPMENT ? (int)item.Equip.Quality : (int)cfgItem.Quality,
                item_name = cfgItem.Name,
                count = (int)item.Num,
            });
        }
        
        return boxItems.ToArray();
    }

    /*
    private void TDSendOpenItemData()
    {
        Dictionary<string, object> openBoxData = new Dictionary<string, object>();
        openBoxData.Add("box_item_info", TDGetOpenItemInfo().ToArray());
        openBoxData.Add("is_auto", autoOpenBoxState != AutoOpenBoxState.Stopped);
        openBoxData.Add("power_up", 0);
        openBoxData.Add("open_num", RoleM.Instance.OpenBoxItems.Count);
        openBoxData.Add("role_power_stop", Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.HigherThenProtagonistEquippedToggle.ToString(), 1)));
        openBoxData.Add("partner_power_stop", Convert.ToBoolean(GameUtil.Instance.GetPlayerPrefsInt(GameConst.AutoOpenBoxOption.HigherThenPartnerEquippedToggle.ToString())));
        openBoxData.Add("box_num_before", boxNumAfterOpen);
        openBoxData.Add("box_num_after", RoleM.Instance.TreasuerBox);

        int openLevel = RoleM.Instance.GetDefaultOpenBoxEquipLevel();
        int openJob = RoleM.Instance.GetDefaultOpenBoxEquipJob();
        openBoxData.Add("box_job_limited", openLevel + "级");

        string job = "";
        switch (openJob)
        {
            case 0:
                job = "全部默认";
                break; 
            case 1:
                job = "战士";
                break;       
            case 2:
                job = "法师";
                break;
            case 3:
                job = "祭祀";
                break;
            default:
                break;
        }

        openBoxData.Add("box_level_limited", job);
        TDAnalyticsUtil.Track("open_box", openBoxData);
    }

    private List<Dictionary<string, object>> TDGetOpenItemInfo()
    {
        List<Dictionary<string, object>> objectGroup = new List<Dictionary<string, object>>();
        foreach (var item in RoleM.Instance.OpenBoxItems)
        {
            var cfgItem = CFG_Item.GetById(item.CfgId);
            Dictionary<string, object> obj = new Dictionary<string, object>();
            obj.Add("item_id", item.CfgId.ToString());
            obj.Add("item_level", (int.TryParse(cfgItem.itemlevel.ToString(), out int level) ? level : 0) + "级");
            obj.Add("item_quality", GameUtil.Instance.GetQualityColorName(item.Type == pb.ItemType.EQUIPMENT ? (int)item.Equip.Quality : (int)cfgItem.Quality));
            obj.Add("item_name", cfgItem.Name);
            objectGroup.Add(obj);
        }
        return objectGroup;
    }*/

    #endregion
}