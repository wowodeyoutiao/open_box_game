using DG.Tweening;
using HotUpdateScripts;
using HotUpdateScripts.Game;
using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Event;
using JEngine.Helper;
using JEngine.MyNet;
using JEngine.UI;
using MetaFramework.UI;
using pb;
using UnityEngine;
using UnityEngine.UI;

public class UIGameDungeons : UIBase<UIModelGameDungeons, UIViewGameDungeons>
{
    public override UILayer GetLayer()
    {
        return UILayer.Base;
    }

    public override void OnInit()
    {

        SetJoystickInit();

        GetView().btnExti.onClick.AddListener(exitDungeons);

        for (int i = 0; i < 13; i++)
        {
            int SkillIdx = i;

            var touch = JUIExtensions.touch(GetView().SkillBtns[i].rect);
            touch.AddClickListener((p) =>
            {
                if (UITool.Check<UIItemShow>())
                {
                    return;
                }
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnSkillClick(SkillIdx);
            });

            touch.AddPointerDownListener((p) =>
            {
                int cfgId = SkillM.Instance.GetSkillEquipBySolt(SkillIdx);
                if (cfgId != 0)
                    _TouchTime = GameHelper.Instance.AddInvoke(new TimeItem(GameConst.TouchTime, () => {
                        UITool.Open<UIItemShow>(null, CFG_Skill.GetById(cfgId).SkillIcon);
                    }));
            });

            touch.AddPointerUpListener((p) =>
            {
                ClearTouch();
            });

            touch.AddDragBeginListener(OnDragBegin);
            touch.AddDragEndListener(OnDragEnd);
        }

        GetView().btn_SkillPanel.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnSkillPanelOpen(!GetView().obj_SkillPanelOn.activeSelf);
        });

        GetView().btn_Attack.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //if (RoleM.Instance.Guidance)
            //{
            //    UITool.ShowTips("引导中不可操作");
            //    return;
            //}
            UITool.Open<UIGameFightSelect>();
        });

        GetView().btn_AutoPlay.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            //if (RoleM.Instance.Guidance)
            //{
            //    UITool.ShowTips("引导中不可操作");
            //    return;
            //}
            if (!GetView().obj_AutoPlayLock.activeSelf)
            {
                //if (!GameRoomM.Instance.AutoPlayLock)
                //{
                //    UITool.ShowTips("功能未解锁");
                //    return;
                //}
                OnAutoPlayClick(!GameRoomM.Instance.AutoPlay);
            }
            else
                UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
                {
                    ShowStr = "自动战斗功能需要VIP"+ CFG_XMLConfig.xmlConfig.BaseValue.autoPlayVipLv +"开启，是否前往提升？",
                    YesFun = () => {
                        UITool.Open<UIVIP>();
                    },
                });
        });


        GetView().touch_Skill.AddDragBeginListener(OnDragBegin);
        GetView().touch_Skill.AddDragEndListener(OnDragEnd);

        GetView().btn_FightAward.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIFightAward>(null);
        });

        OnSkillShow();
        OnAutoLockSet();
        OnAutoPlayClick(GameRoomM.Instance.AutoPlay);

        if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.PVP_More)
            UITool.LoadUII<UICom_GVGBattle>(GetView().root_Base, (ui) => {
                _uICom_GVGBattle = ui as UICom_GVGBattle;
            });
        else if (GameRoomM.Instance.IsDayDo())
            UITool.LoadUII<UIGameDayDoPro>(GetView().root_Base, (ui) => {
                _uIGameDayDoPro = ui as UIGameDayDoPro;
            });

        // else if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.Guide)
        //     RoleM.Instance.SetGuide(new UIGuide.Guide { ID = 3, GuidePos = Vector3.zero });

        else if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.Relic)
            UITool.LoadUII<UIGameRelicPro>(GetView().root_Base, (ui) => {
                _uIUIGameRelicPro = ui as UIGameRelicPro;

                _uIUIGameRelicPro.OnRelicInfoShow();
            });
        else if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.Guild)
            UITool.LoadUII<UIGameGuildPro>(GetView().root_Base, (ui) =>
            {
                _uIGameGuildPro = ui as UIGameGuildPro;
            });


        GameRoomM.Instance.ShortAttactToPlayer = GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.PVP_One;

        GetView().obj_Touch.SetActive(GameRoomM.Instance.CFGRoom.Roomtype != CFG_Room.RoomType.PVP_One);
        //UITool.LoadUII<UICom_DownBtns>(GetView().root_Base, (ui) =>
        //{
        //    DownUI = ui as UICom_DownBtns;
        //});

        GetView().btn_FightAward.gameObject.SetActive(GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.PVE_Peace);

        GetView().btn_Chat.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIChat>();
        });

        GetView().btn_Chat.gameObject.SetActive(GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.Guild);
    }

    public override void OnLayerChange()
    {
        if (TopUI == null)
        {
            UITool.LoadUII<UICom_TopRes>(GetView().root_Base, (ui) =>
            {
                TopUI = ui as UICom_TopRes;
                TopUI.OnSetLayer(_canvasOrder);
            });
        }
    }

    private UICom_DownBtns DownUI = null;
    private UICom_TopRes TopUI = null;
    private UICom_GVGBattle _uICom_GVGBattle;
    private UIGameDayDoPro _uIGameDayDoPro;
    private UIGameRelicPro _uIUIGameRelicPro = null;
    private UIGameGuildPro _uIGameGuildPro = null;

    //自动战斗VIP状态
    private void OnAutoLockSet()
    {
        GetView().obj_AutoPlayLock.SetActive(RoleM.Instance.Vip < CFG_XMLConfig.xmlConfig.BaseValue.autoPlayVipLv);
        if (!GameRoomM.Instance.AutoPlayLock)
        {
            TcpC.C2S_AutoPlay();
        }
    }

    public void OnSkillPanelOpen(bool isOpne)
    {
        GetView().obj_SkillPanelOn.SetActive(isOpne);
        GetView().obj_SkillPaneloOff.SetActive(!isOpne);
        GetView().touch_Skill.gameObject.SetActive(isOpne);
    }

    private void OnAutoPlayClick(bool isPaly)
    {
        GameRoomM.Instance.AutoPlay = isPaly;
        GetView().obj_AutoPlayOn.SetActive(GameRoomM.Instance.AutoPlay);
        //GetView().txt_AutoPlay.text = isPaly ? "停止\n自动" : "自动\n战斗";
    }


    //技能面板滑动
    Vector2 TouchPos;
    private void OnDragBegin(UnityEngine.EventSystems.PointerEventData pEvent)
    {
        TouchPos = pEvent.position;
    }

    private void OnDragEnd(UnityEngine.EventSystems.PointerEventData pEvent)
    {
        if (Mathf.Abs(TouchPos.y - pEvent.position.y) > 3)
        {
            if (TouchPos.y - pEvent.position.y < 0)
                BaseRotate -= 90;
            else
                BaseRotate += 90;

            BaseRotate %= 360;
            SkillRotal(BaseRotate);
        }
    }

    private int BaseRotate = 0;
    private void SkillRotal(int _rot)
    {
        GetView().root_Skill.transform.DOKill();
        GetView().root_Skill.transform.DORotate(Vector3.forward * _rot, 0.3f).SetEase(Ease.Linear).OnComplete(() => {
            for (int i = 1; i <= 12; i++)
            {
                GetView().SkillBtns[i].rect.localEulerAngles = Vector3.forward * (-_rot);
            }
        });
        int idx = Mathf.Abs((_rot > 0 ? _rot - 360 : _rot) / 90) +1;
        GetView().txt_TouchNum.text = idx.ToString();
    }

    private void OnTimeShow()
    {
        ClearTime();
        ShowTime = GameHelper.Instance.AddInvoke(new TimeItem(GameRoomM.Instance.GameFightTime, 1, () => {
            if (isNullGo)
                return;

            OnSkillCDRefresh();
            GetView().txt_time.text = $"倒计时:<color=#F5EB36>{GameUtil.Instance.GetTimeSToHMS(GameRoomM.Instance.GameFightTime - GameUtil.Instance.GetCurrentTimeSeconds())}</color>";
            if (_uIGameDayDoPro != null)
                _uIGameDayDoPro.OnTimeShow();

            if (_uIUIGameRelicPro != null)
                _uIUIGameRelicPro.OnRelicInfoShow();

            if (GameRoomM.Instance.GameFightTime - GameUtil.Instance.GetCurrentTimeSeconds() <= 0)
            {
                ClearTime();
            }
        },true));

    }

    private TimeItem ShowTime;

    private void ClearTime()
    {
        if (ShowTime != null && GameHelper.Instance != null)
        {
            GameHelper.Instance.RemoveInvoke(ShowTime);
            ShowTime = null;
        }
    }

    // 技能槽位同步：服务端登录/换技能后会推 SyncUserSkillSlotPush，
    // 原来这里没有订阅者，导致界面打开时数据还没到就一直显示成锁住的空槽
    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserSkillSlot(GameEvent.Sync_UserSkillSlot _e)
    {
        if (isNullGo)
            return;
        OnSkillShow();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_GetNewSkill(GameEvent.Sync_GetNewSkill _e)
    {
        if (isNullGo)
            return;
        OnSkillShow();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_SkillUpgrade(GameEvent.Sync_SkillUpgrade _e)
    {
        if (isNullGo)
            return;
        SkillM.Instance.OnInitGameSkill(false);
        OnSkillShow();
    }

    private void OnSkillShow()
    {
        //特技暂时隐藏
        for (int i = 0; i < 13; i++)
        {
            int cfgId = SkillM.Instance.GetSkillEquipBySolt(i);

            var skillObj = GetView().SkillBtns[i];
            skillObj.img_icon.enabled = cfgId != 0 && !GameRoomM.Instance.GetCanNoAttackGame();
            if (cfgId != 0 && !GameRoomM.Instance.GetCanNoAttackGame())
            {
                var cfgInfo = CFG_Skill.GetById(cfgId);
                GameUtil.Instance.SetItemIcon((int)cfgInfo.SkillIcon, skillObj.img_icon);
                skillObj.obj_on.SetActive(SkillM.Instance.GetGameSkillOpen(cfgId, i));
                skillObj.img_cd.gameObject.SetActive(false);
                skillObj.obj_lock.SetActive(false);

                var skillInfo = SkillM.Instance.GetSkillInfoByCFGID(cfgId);
                if (skillInfo == default)
                    skillInfo = PartnerM.Instance.GetSkillInfoBySlotSkillID(cfgId);

                skillObj.txt_Reinforce.text = cfgInfo.SkillLevel == 0 ? "" : cfgInfo.SkillLevel.ToString();
                skillObj.txt_Star.text = skillInfo.Advanced == 0 ? "" : skillInfo.Advanced.ToString();
            }
            else
            {
                int openLevel = CFG_XMLConfig.xmlConfig.SkillSlotDic[i];
                bool isLock = i == 0 ||(openLevel > 0 && RoleM.Instance.Level < openLevel) ;
                skillObj.obj_on.SetActive(false);
                skillObj.img_cd.gameObject.SetActive(true);
                skillObj.obj_Dead.SetActive(false);
                skillObj.obj_lock.SetActive(isLock);
                skillObj.txt_Time.text = isLock ? (i == 0 ? "特技暂未开放" : $"{openLevel}级\n解锁"): "";
            }
        }
    }

    private TimeItem _TouchTime;
    private void ClearTouch()
    {
        if (_TouchTime != null && GameHelper.Instance != null)
        {
            GameHelper.Instance.RemoveInvoke(_TouchTime);
            _TouchTime = null;
        }
    }

    //技能按钮点击
    private void OnSkillClick(int skillIdx)
    {
        int cfgId = SkillM.Instance.GetSkillEquipBySolt(skillIdx);
        if (cfgId != 0 && !GameRoomM.Instance.GetCanNoAttackGame())
        {
            SkillM.Instance.SetGameSkillOpen(cfgId, !SkillM.Instance.GetGameSkillOpen(cfgId, skillIdx), skillIdx);
            GetView().SkillBtns[skillIdx].obj_on.SetActive(SkillM.Instance.GetGameSkillOpen(cfgId, skillIdx));
        }
       
    }
     

    public void SetJoystickInit()
    {
        //设置摇杆位移和绑定跟随
        GetView()._ETCJoystick.onMove.AddListener(OnJoyMove);
        GetView()._ETCJoystick.onMoveStart.AddListener(OnJoyMoveStart);
        GetView()._ETCJoystick.onMoveEnd.AddListener(OnJoyMoveEnd);
    }

    private void OnJoyMoveStart()
    {
        if (MainRole._MainRole.targetID != 0)
        {
            if (RoleM.Instance.CheakBuffId((int)CFG_BuffEffect.BufferID.XuanYun, (int)CFG_BuffEffect.BufferID.BingDong, (int)CFG_BuffEffect.BufferID.CaoFeng))
                return;

            if (GameRoomM.Instance.GetCanNoAttackGame())
            {
                var Monster = MonsterManager.Instance.getOneMonster(MainRole._MainRole.targetID);
                if (Monster != null)
                {
                    Monster._monser.curLifeTime = 0;
                }
            }
            MainRole._MainRole.targetID = 0;
        }
    }

    private void OnJoyMove(Vector2 moveVec)
    {
        if (RoleM.Instance.CheakBuffId((int)CFG_BuffEffect.BufferID.XuanYun, (int)CFG_BuffEffect.BufferID.BingDong, (int)CFG_BuffEffect.BufferID.CaoFeng)) 
            return;
        Direction _dir = GetDirection(moveVec);
        MainRole._MainRole._joyDir = _dir;
    }

    private void OnJoyMoveEnd()
    {
        MainRole._MainRole._joyDir = Direction.NONE;
    }

    private Direction GetDirection(Vector2 position)
    {
        //Debug.Log("GetDirection position:" + position);
        if (position.x > 0.1)
        {
            if (position.y > 0.1) return Direction.UP_RIGHT;
            if (position.y < -0.1) return Direction.DOWN_RIGHT ;
            return Direction.RIGHT;
        }
        else if (position.x < -0.1)
        {
            if (position.y > 0.1) return Direction.UP_LEFT;
            if (position.y < -0.1) return Direction.DOWN_LEFT;
            return Direction.LEFT;
        }
        return position.y > 0 ? Direction.UP : Direction.DOWN;
    }

    //退出
    private void exitDungeons()
    { 
        string showStr = "提前退出副本会直接判定挑战失败，是否确认退出？";
        if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.PVE_Peace)
            showStr = "离开副本之后当前获得的战利品将会直接发放到包裹中，副本剩余时间会保留，是否确定离开?";
        else if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.Guild || GameRoomM.Instance.IsDayDo())
            showStr = "是否确定离开?";

        UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
            {
                ShowStr = showStr,
                YesFun = () =>
                {
                    //if (RoleM.Instance.Guidance)
                    //    RoleM.Instance.Guidance = false;

                    TcpC.C2S_Exit();
                },
                BtnYes = "直接退出",
                BtnNo = "继续游戏",
            });
    }

    public override void OnEnter(params object[] args)
    {

        JEvent.defaultEvent.Register(this);
        //OnRoleRefresh();
        OnTimeShow();

        BaseRotate = 0;
        SkillRotal(BaseRotate);

        OnSkillPanelOpen(true);

        GetView().txt_MonsterTip.text = ""; 
        OnMonsterShow();
    }

    public override void OnDestroy()
    {
        JEvent.defaultEvent.Unregister(this);
        //ClearTime();
        ClearTouch();

        if (DownUI != null)
            DownUI.OnDestroy();

        if (TopUI != null)
            TopUI.OnDestroy();

        if (_uICom_GVGBattle != null)
            _uICom_GVGBattle.OnDestroy();

        if (_uIGameDayDoPro != null)
            _uIGameDayDoPro.OnDestroy();

        if (_uIGameGuildPro != null)
            _uIGameGuildPro.OnDestroy();
}


    [Subscriber(ThreadMode.Main)]
    private void OnFight_Die(GameEvent.Fight_Die _e)
    {
        if (isNullGo)
            return;

        OnMonsterShow();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnFight_MonsterAdd(GameEvent.Fight_MonsterAdd _e)
    {
        if (isNullGo)
            return;

        OnMonsterShow();
    }

    private void OnMonsterShow()
    {
        if (GameRoomM.Instance.CFGRoom.Roomtype != CFG_Room.RoomType.Tower)
        {
            return;
        }
        GetView().txt_MonsterTip.text = "剩余怪物数量:"+ HotUpdateScripts.Game.MonsterManager.Instance.MonesterCount;
    }


    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserBase(GameEvent.Sync_UserBase _e)
    {
        if (isNullGo)
            return;

        OnAutoLockSet();

    }

    private void OnSkillCDRefresh()
    {
        for (int i = 0; i < 13; i++)
        {
            int cfgId = SkillM.Instance.GetSkillEquipBySolt(i);
            var skillObj = GetView().SkillBtns[i];
            if (cfgId != 0)
            {
                int _time = (int)(SkillM.Instance.GetGameSkillDealy(cfgId) - GameUtil.Instance.GetCurrentTimeSeconds());
                skillObj.txt_Time.text = "" + _time;
                skillObj.img_cd.gameObject.SetActive(_time > 0);

                long uid = SkillM.Instance.GetUiDBySkillId(cfgId);
                skillObj.obj_Dead.SetActive(uid != 0 && GameRoomM.Instance.GetPlayerHP(uid) == 0 && GameRoomM.Instance._GameState == GameRoomM.GameState.Play);

            }
        }
    }

}

