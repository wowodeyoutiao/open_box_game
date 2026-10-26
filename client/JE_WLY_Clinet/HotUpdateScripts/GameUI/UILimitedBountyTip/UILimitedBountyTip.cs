using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using System;
using HotUpdateScripts.ProtoM;

public class UILimitedBountyTip : UIBase<UIModelLimitedBountyTip, UIViewLimitedBountyTip>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
     public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(delegate {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UILimitedBountyTip>();
        });

        GetView().btn_Change.onClick.AddListener(delegate {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UILimitedBountyTip>();
            UITool.Open<UIPayBind>();
        });

        GetView().btn_Sure.onClick.AddListener(delegate {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UILimitedBountyTip>();
            CallBack?.Invoke();
        });
    }


    private Action CallBack;

    public override void OnEnter(params object[] args)
    {
        CallBack = args[0] as Action;
        GetView().txt_des.text = $"姓名：{StoreM.Instance.GetExtractAccountName()}\n账号：{StoreM.Instance.GetExtractAccount()}";
    }

    public override void OnExit()
    {
        
    }

     public override void OnDestroy()
    {

    }
}