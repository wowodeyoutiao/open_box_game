using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UINotice : UIBase<UIModelNotice, UIViewNotice>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UINotice>();
        });
    }

    public override void OnEnter(params object[] args)
    {
        
    }

    public override void OnExit()
    {
        
    }

     public override void OnDestroy()
    {

    }
}