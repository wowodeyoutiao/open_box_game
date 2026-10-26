using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using System;
using HotUpdateScripts.ProtoM;

public class UIExpAdd : UIBase<UIModelExpAdd, UIViewExpAdd>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btnClose.onClick.AddListener(CloseUI);

        for (int i = 0; i < GetView().Root_Btns.childCount; i++)
        {
            int idx = i;
            GetView().Root_Btns.GetChild(i).Find("Btn_Go").GetComponent<Button>().onClick.AddListener(() => {
                OnJumpBtnClick(idx);
            });
        }
    }

    private void OnJumpBtnClick(int idx)
    {
        callBack?.Invoke();
        CloseUI();
        switch (idx)
        {
            case 0:
                if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
                {
                    UITool.ShowTips("已在战斗中");
                    return;
                }
                UITool.Open<UIHall>(null,UIHall.HallType.PVE);
                //副本选择界面;
                break;
            case 1:
                UITool.Open<UIActive>(null, HotUpdateScripts.ProtoM.ActivityType.GiftPay);
                break;
            case 2:
                UITool.Open<UIActive>(null);
                break;
            case 3:
                UITool.Open<UIShop>();
                break;
            case 4:
                UITool.Open<UIVIP>();
                break;
            default:
                break;
        }
        
    }

    private void CloseUI()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        UITool.Close<UIExpAdd>();
    }

    private Action callBack;
    public override void OnEnter(params object[] args)
    {
        if (args.Length > 0)
        {
            callBack = args[0] as Action;
        }
    }

    public override void OnExit()
    {
        
    }

     public override void OnDestroy()
    {

    }
}