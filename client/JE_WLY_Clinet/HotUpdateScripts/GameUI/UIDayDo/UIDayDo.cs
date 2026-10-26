using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using JEngine.Event;

public class UIDayDo : UIBase<UIModelDayDo, UIViewDayDo>
{
    public override UILayer GetLayer()
    {
        return UILayer.NoLayer;
    }

    private CFG_Room.RoomType[] AllRoomInfo = new CFG_Room.RoomType[] {
        CFG_Room.RoomType.QHS,
        CFG_Room.RoomType.SXS,
        CFG_Room.RoomType.HS,
        CFG_Room.RoomType.SY,
        CFG_Room.RoomType.Gold,
        CFG_Room.RoomType.EXP,
    };

    public static Dictionary<CFG_Room.RoomType, CFG_XMLConfig.XML_Config.SwitchOpne> RoomToLockDic = new Dictionary<CFG_Room.RoomType, CFG_XMLConfig.XML_Config.SwitchOpne>
    {
        [CFG_Room.RoomType.QHS] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_QHSAndGold,
        [CFG_Room.RoomType.SXS] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_ExpAndSXS,
        [CFG_Room.RoomType.HS] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_HS,
        [CFG_Room.RoomType.SY] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_SY,
        [CFG_Room.RoomType.Gold] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_QHSAndGold,
        [CFG_Room.RoomType.EXP] = CFG_XMLConfig.XML_Config.SwitchOpne.Play_ExpAndSXS,
    };
    public override void OnInit()
    {
        GetView().btn_AutoPlay.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            if (GameUtil.Instance.CheakIsLock(RoomToLockDic[OnSelectIdx]))
                return;

            if (GetView().obj_vip.activeSelf)
            {
                UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack { ShowStr = "当前VIP等级不足，是否前往提升？",
                    YesFun = () => {
                        UITool.Close<UIFightChoice>();
                        UITool.Open<UIVIP>();
                    },
                });
                return;
            }

            if (RoleM.Instance.GetCanTimeByRoomType((int)OnSelectIdx) == 0)
            {
                if (CFG_VIP.IsMaxVipLevel(RoleM.Instance.Vip))
                {
                    UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
                    {
                        ShowStr = "今日可挑战次数不足，是否提升VIP等级增加每日可挑战次数？",
                        YesFun = () => {
                            UITool.Close<UIFightChoice>();
                            UITool.Open<UIVIP>();
                        },
                    });
                }
                else
                {
                    UITool.ShowTips("今日挑战次数已用完");
                }
                return;
            }

            TcpC.C2S_CopySweep(SelectCFG.ID);
        });

        GetView().btn_Paly.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            if (GameUtil.Instance.CheakIsLock(RoomToLockDic[OnSelectIdx]))
                return;

            if (RoleM.Instance.GetCanTimeByRoomType((int)OnSelectIdx) == 0)
            {
                if (CFG_VIP.IsMaxVipLevel(RoleM.Instance.Vip))
                {
                    UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
                    {
                        ShowStr = "今日可挑战次数不足，是否提升VIP等级增加每日可挑战次数？",
                        YesFun = () => {
                            UITool.Close<UIFightChoice>();
                            UITool.Open<UIVIP>();
                        },
                    });
                }
                else 
                {
                    UITool.ShowTips("今日挑战次数已用完");
                }
                return;
            }

            TcpC.GetGameJoin(SelectCFG.ID,0);
        });

        for (int i = 0; i < GetView().root_Btns.childCount; i++)
        {
            int idx = i;
            JUIExtensions.btn(GetView().root_Btns.GetChild(i)).onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnRoomSelect(AllRoomInfo[idx]);
            });

            var cfgInfo = CFG_Room.GetByRoomType(AllRoomInfo[idx], -1);
            JUIExtensions.txt(GetView().root_Btns.GetChild(i).Find("txt_des")).text = cfgInfo.Suggest;
        }
    }

    private CFG_Room.RoomType OnSelectIdx ;
    private CFGItem_Room SelectCFG;

    private void OnRedRefrsh()
    {
        for (int i = 0; i < GetView().root_Btns.childCount; i++)
        {
            var idx = AllRoomInfo[i];
            
            GetView().root_Btns.GetChild(i).Find("dot").gameObject.SetActive(!GameUtil.Instance.CheakIsLock(RoomToLockDic[idx], false) && RoleM.Instance.GetCanTimeByRoomType((int)idx) > 0);

            GetView().root_Btns.GetChild(i).Find("lock").gameObject.SetActive(GameUtil.Instance.CheakIsLock(RoomToLockDic[idx], false));
            JUIExtensions.txt(GetView().root_Btns.GetChild(i), "lock/txt").text = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[RoomToLockDic[idx]] + "级解锁";
        }
    }
    private void OnRoomSelect(CFG_Room.RoomType roomType,bool isRefresh = false)
    {
        if (GameUtil.Instance.CheakIsLock(RoomToLockDic[roomType]))
            return;

        OnSelectIdx = roomType;
        SelectCFG = CFG_Room.GetByRoomType(roomType, -1);

        if (!isRefresh)
        {
            for (int i = 0; i < GetView().root_Btns.childCount; i++)
            {
                GetView().root_Btns.GetChild(i).Find("select").gameObject.SetActive(AllRoomInfo[i] == roomType);
            }

            GetView().txt_Title.text = SelectCFG.Name;
            GetView().txt_RoomDes.text = SelectCFG.CopyDes;

            for (int i = 0; i < GetView().root_Award.childCount; i++)
            {
                var go = GetView().root_Award.GetChild(i);
                go.gameObject.SetActive(i < SelectCFG.Droppreview.Length);
                if (i < SelectCFG.Droppreview.Length)
                {
                    int cfgId = SelectCFG.Droppreview[i];
                    GameUtil.Instance.SetItemIcon(cfgId, JUIExtensions.img(go.Find("icon")), JUIExtensions.img(go));
                    JUIExtensions.btn(go).onClick.RemoveAllListeners();
                    JUIExtensions.btn(go).onClick.AddListener(() => {
                        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                        UITool.Open<UIItemShow>(null, cfgId);
                    });
                }
            }
            GameUtil.Instance.SetMonsterIcon((int)SelectCFG.Show, GetView().img_Monster);

            GetView().obj_vip.SetActive(CFG_XMLConfig.xmlConfig.DayRoomDic[roomType].viplimit > RoleM.Instance.Vip);
            GetView().txt_vip.text = $"VIP{CFG_XMLConfig.xmlConfig.DayRoomDic[roomType].viplimit}开启";

            //GetView().obj_lock1.SetActive(GameUtil.Instance.CheakIsLock(RoomToLockDic[roomType],false));
            //GetView().txt_lock1.text = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[RoomToLockDic[roomType]] + "级开启";
            //GetView().obj_lock2.SetActive(GameUtil.Instance.CheakIsLock(RoomToLockDic[roomType], false));
            //GetView().txt_lock2.text = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[RoomToLockDic[roomType]] + "级开启";

        }

        GetView().txt_pro.text = $"今日可挑战次数:{RoleM.Instance.GetCanTimeByRoomType((int)roomType)}/{CFG_XMLConfig.xmlConfig.DayRoomDic[roomType].challengeTimeNum + + CFG_VIP.GetAddRoomCount(RoleM.Instance.Vip,(int) roomType)}";
    }

    public void OnShow(bool isShow)
    {
        if (isShow)
        {
            OnEnter();
        }
        else
        {
            OnExit();
        }
        uiGo.SetActive(isShow);
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
        OnSelectIdx = AllRoomInfo[0];
        OnRoomSelect(OnSelectIdx);
        OnRedRefrsh();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnDaily_MapInfo(GameEvent.Daily_MapInfo _e)
    {
        if (isNullGo)
            return;
        OnRoomSelect(OnSelectIdx,true);
        OnRedRefrsh();
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
    }
}