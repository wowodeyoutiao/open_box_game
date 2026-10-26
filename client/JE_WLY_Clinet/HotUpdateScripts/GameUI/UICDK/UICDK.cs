using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.LitJson;
using JEngine.Helper;
using HotUpdateScripts.ProtoM;

public class UICDK : UIBase<UIModelCDK, UIViewCDK>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(OnCloseUI);
        GetView().btn_Sure.onClick.AddListener(OnBtnSureClick);
    }

    private void OnCloseUI()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        UITool.Close<UICDK>();
    }

    private void OnBtnSureClick()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        if (GetView().Input_CDK.text == "")
        {
            UITool.ShowTips("请输入兑换码");
            return;
        }

        string strRequest = JsonMapper.ToJson(new HTTP_ActivationOperateRequest()
        {
            activation_code = GetView().Input_CDK.text,
        });
        HttpRequestHelper.SendPost<HTTP_ActivationOperateResponse>(GameM.Instance.HttpUrl + GameM.Instance.CDK_Url, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Token, strRequest,
            (_r) => {
                if (_r.code == 0)
                {
                    UITool.ShowTips( "兑换成功");
                    UITool.Close<UICDK>();
                }
                Debug.Log(_r.data);
                if (!string.IsNullOrEmpty(_r.data))
                {
                    UITool.ShowTips(_r.data);
                    GetView().Input_CDK.text = "";
                }
            });

    }

    public override void OnEnter(params object[] args)
    {
        GetView().Input_CDK.text = "";
    }

    public override void OnExit()
    {
        
    }
}