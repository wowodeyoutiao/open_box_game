using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.UI;
using JEngine.MyNet;
using HotUpdateScripts.ProtoM;
using pb;
using JEngine.Event;
using JEngine.Helper;

public class UICom_DownBtns : UIBase<UIModelCom_DownBtns, UIViewCom_DownBtns>
{
    public override UILayer GetLayer()
    {
        return UILayer.NoLayer;
    }
    public override void OnInit()
    {
        JEvent.defaultEvent.Register(this);
        for (int i = 1; i <= 7; i++)
        {
            int idx = i;
            GetView().Btns[i-1]._btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnDownBtnCkick(idx);
            });
        }

        OnLockCheak(true);
        OnBindRed();
        OnLocalRedCheck();
        OnRoleRefresh();
    }

    private Dictionary<RedPointConst, GameObject> RedDic;

    private void OnBindRed()
    {
        RedDic = new Dictionary<RedPointConst, GameObject>()
        {
            [RedPointConst.Play] = GetView().Btns[0]._objDot,
            [RedPointConst.Bag] = GetView().Btns[4]._objDot,
            //[RedPointConst.Rank] = uiGo.transform.Find("Btn_6/dot").gameObject,
            [RedPointConst.Build] = GetView().Btns[2]._objDot,
            [RedPointConst.Role] = GetView().Btns[1]._objDot,
            [RedPointConst.Guild] = GetView().Btns[5]._objDot,
            [RedPointConst.ExpPool_First] = GetView().Btns[6]._objDot,
        };

        foreach (var item in RedDic)
        {
            RedPointManager.Instance.RPS.Register(item.Key.ToString(), (node) =>
            {
                if (isNullGo) return;
                item.Value.SetActive(node.pointCount > 0);
            });
        }

        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Bag_Syn);
    }


    private void OnLocalRedCheck()
    {
        //RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_Level.ToString(), RedDotM.Instance.GetRandRedNumByType(RedPointConst.Rank_Level.ToString()));
        //RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_Power.ToString(), RedDotM.Instance.GetRandRedNumByType(RedPointConst.Rank_Power.ToString()));
        //RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_SiLian.ToString(), RedDotM.Instance.GetRandRedNumByType(RedPointConst.Rank_SiLian.ToString()));
        //RedPointManager.Instance.RPS.Dispatch(RedPointConst.Rank_ZhanHun.ToString(), RedDotM.Instance.GetRandRedNumByType(RedPointConst.Rank_ZhanHun.ToString()));

        if (GuildM.Instance.IsGuildOpen())
            GuildM.Instance.CheckGuildRed();
    }

    public override void OnEnter(params object[] args)
    {

    }

    private void OnDownBtnCkick(int idx)
    {
        //UITool.ShowTips("功能暂未开发");
        switch (idx)
        {
            case 1:

                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play))
                    return;

                if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
                {
                    UITool.ShowTips("已在战斗中");
                    return;
                }
                UITool.Open<UIFightChoice>();
                break;
            case 2:
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Role))
                    return;
                UITool.Open<UIRole>();
                break;
            case 3:
                if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv))
                    return;
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build))
                    return;
                UITool.Open<UIBuild>();
                break;
            case 4:
                UITool.Open<UIShop>();
                break;
            case 5:
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Bag))
                    return;

                TcpC.GetBagInfo();
                UITool.Open<UIBag>();
                break;
            case 6:
                // 【单机版不做公会】公会需要真实成员/捐献/公会战，
                // 服务端 17 个 Guild* 协议全部没有单机实现，整块屏蔽。
                UITool.ShowTips("暂未开放");
                break;
            case 7:
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.ExpPool))
                    return;

                UITool.Open<UIExpPool>();
                break;
            default:
                break;
        }
    }

    private void OnShopClick()
    {
        HttpC.OnGetShopInfo(0, (_response) =>
        {
            if (_response.code == 0 && !isNullGo)
            {
                UITool.Open<UIShop>();
            }
            else
            {
                UITool.ShowTips($"失败 code {_response.code}");
            }
        });

    }

    private void OnRoleRefresh()
    {
        float proExp = (float)RoleM.Instance.Exp / CFG_Role.GetByJobAndLevel(RoleM.Instance.Job,RoleM.Instance.Level).NeedEXP;
        GetView().img_JyPro.fillAmount = proExp;
        GetView().slider_Jy.value = proExp;
        GetView().txt_JyPro.text = GameUtil.Instance.GetPercentStr(proExp);
    }

    private void OnLockCheak(bool isInit = false)
    {
        int lockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Play];
        GetView().Btns[0]._objLock.gameObject.SetActive(RoleM.Instance.Level < lockLv);
        if (isInit)
            GetView().Btns[0]._txtLock.text = lockLv + "级解锁";

        lockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Role];
        GetView().Btns[1]._objLock.gameObject.SetActive(RoleM.Instance.Level < lockLv);
        if (isInit)
            GetView().Btns[1]._txtLock.text = lockLv + "级解锁";

        lockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Bag];
        GetView().Btns[4]._objLock.gameObject.SetActive(RoleM.Instance.Level < lockLv);
        if (isInit)
            GetView().Btns[4]._txtLock.text = lockLv + "级解锁";

        lockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Build];
        GetView().Btns[2]._objLock.gameObject.SetActive(GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv, false) || RoleM.Instance.Level < lockLv);

        if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv, false))
        {
            GetView().Btns[2]._txtLock.text = $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv)}天后开启";
        }
        else
        {
            GetView().Btns[2]._txtLock.text = lockLv + "级解锁";
        }

        GetView().Btns[5]._objLock.gameObject.SetActive(GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Guild, false) || RoleM.Instance.Level < lockLv);
        if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Guild, false))
        {
            GetView().Btns[5]._txtLock.text = $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Guild)}天后开启";
        }
        else
        {
            lockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Guild];
            GetView().Btns[5]._txtLock.text = lockLv + "级解锁";
        }

        lockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.ExpPool];
        GetView().Btns[6]._objLock.gameObject.SetActive(RoleM.Instance.Level < lockLv);
        if (isInit)
            GetView().Btns[6]._txtLock.text = lockLv + "级解锁";
    }

    private void OnRedCheak()
    {
        //检测等级
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Partner_Into);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Equip_Reinforce);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Skill_UpLv);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Skill_Setting);

        //等级+经验
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.ExpPool);

        RedPointManager.Instance.RPS.Dispatch(RedPointConst.PVP_One.ToString(), RoleM.Instance.GetPVPRedNum());
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.PVP_More.ToString(), RoleM.Instance.GetGVGRedNum());

        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Role_Equip);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Equip_Reinforce);

        RedPointManager.Instance.RPS.Dispatch(RedPointConst.DayDo.ToString(), RedDotM.Instance.OnDayDoRedNum());
    }

    private void OnPlayRedDot()
    {
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_Fight_Click);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_Fight_Award);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_Boss);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_Tower_Click);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_Relice);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Challenge_OnHook);

        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Pet_Award);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Partner_Call);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Equip_Reinforce);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Equip_Star);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Equip_Insert);

        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Skill_UpLv);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Skill_Advance);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Skill_Setting);

    }

    public void StartGuide()
    {
        var data = new UIGuide.GuideData
        {
            ID = CFG_Guidance.GetFirstIdByType(CFG_Guidance.GuideType.Partner),
            ButtonPos = JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().Btns[1]._btn.transform),
            callBack = () =>
            {
                UITool.Open<UIRole>();
            }
        };
        RoleM.Instance.SetGuide(data);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnEvent_Source_Refresh(GameEvent.Refresh_InstanceInfo _e)
    {
        if (isNullGo)
            return;

        OnPlayRedDot();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnEvent_Source_Refresh(Event_Source_Refresh _e)
    {
        if (isNullGo)
            return;

        //OnLockCheak();

        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Bag_Syn);
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Pet_UpStar_1);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserBase(GameEvent.Sync_UserBase _e)
    {
        if (isNullGo)
            return;
        OnRoleRefresh();
        RedPointManager.Instance.RPS.Dispatch(RedPointConst.ExpPool);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_LvUp(GameEvent.Refresh_LvUp _e)
    {
        if (isNullGo)
            return;
        OnLockCheak();
        OnRedCheak();
        OnPlayRedDot();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_CheckOpenTime(GameEvent.Refresh_ZeroTime _e)
    {
        OnLockCheak();
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}