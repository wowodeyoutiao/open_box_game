using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;

public class UIHPAdd : UIBase<UIModelHPAdd, UIViewHPAdd>
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
                //副本选择界面;
                if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
                {
                    UITool.ShowTips("已在战斗中");
                    return;
                }
                UITool.Open<UIHall>(null, UIHall.HallType.PVE);
                break;
            case 1:
                //装备洗练;
                UITool.Open<UIBuild>();
                break;
            default:
                break;
        }

    }

    private void CloseUI()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        UITool.Close<UIHPAdd>();
    }

    private System.Action callBack;
    public override void OnEnter(params object[] args)
    {
        if (args.Length > 0)
        {
            callBack = args[0] as System.Action;
        }
    }

     public override void OnDestroy()
    {

    }
}