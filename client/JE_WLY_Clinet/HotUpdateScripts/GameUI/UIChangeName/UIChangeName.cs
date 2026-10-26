using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Event;
using JEngine.Helper;
using System;
using JEngine.MyNet;
using pb;

public class UIChangeName : UIBase<UIModelChangeName, UIViewChangeName>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().Btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIChangeName>();
        });

        GetView().Btn_Yes.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (string.IsNullOrEmpty(GetView().Input_Name.text))
            {
                UITool.ShowTips("名字不可为空");
                return;
            }

            Action Ac = () => { UITool.Close<UIChangeName>(); UITool.Close<UIRole>(); };
            if (!GameUtil.Instance.ComCoinToCheck(CFG_XMLConfig.xmlConfig.BaseValue.ReName.itemid, CFG_XMLConfig.xmlConfig.BaseValue.ReName.num, Ac))
            {
                return;
            }

            TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_ChangeRoleName, MType.Request, TCPClientMgr.ProtoSerialize(new ChangeRoleNameRequest { RoleName = GetView().Input_Name.text }));
        });

        GameUtil.Instance.SetItemIcon(CFG_XMLConfig.xmlConfig.BaseValue.ReName.itemid, GetView().img_icon);
        GetView().txt_icon.text = GameUtil.Instance.GetCheakNumStr(CFG_XMLConfig.xmlConfig.BaseValue.ReName.itemid, CFG_XMLConfig.xmlConfig.BaseValue.ReName.num);

    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);

        GetView().Input_Name.text = "";
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_Refresh(GameEvent.Sync_ChangeName _e)
    {
        if (_e.isWin)
        {
            UITool.Close<UIChangeName>();
        }
        else
        {
            GetView().Input_Name.text = "";
        }
    }

    public override void OnDestroy()
    {
        JEvent.defaultEvent.Unregister(this);
    }
}