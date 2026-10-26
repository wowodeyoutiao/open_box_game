using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.MyNet;
using pb;
using JEngine.Helper;
using HotUpdateScripts.ProtoM;

public class UIGameFightRevive : UIBase<UIModelGameFightRevive, UIViewGameFightRevive>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().Btn_Revive.onClick.AddListener(()=> {

            if (!GameUtil.Instance.ComCoinToCheck(CFG_Info.needMoneyID, CFG_Info.num, null))
            {
                return;
            }
            TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_Revive, MType.Request,
               TCPClientMgr.ProtoSerialize(new ReviveRequest()));

            UITool.Close<UIGameFightRevive>();
        });

        GetView().Btn_Back.onClick.AddListener(() => {
            TcpC.C2S_Exit();
        });
    }

    private CFG_XMLConfig.XML_Config.XML_Item_Revive CFG_Info;
    private TimeItem TimeJaction;

    private void ClearTime()
    {
        if (TimeJaction != null)
        {
            GameHelper.Instance.RemoveInvoke(TimeJaction);
            TimeJaction = null;
        }
    }
    public override void OnEnter(params object[] args)
    {
        var info = args[0] as ObjectDiePush;

        GetView().Btn_Revive.gameObject.SetActive(info.CanRevive);
        int CDTime = info.Countdown;
        string ShowStr = "";
        if (info.CanRevive)
        {
            CFG_Info = CFG_XMLConfig.xmlConfig.ReviveDic[1 + info.ReviveTimes];
            ShowStr = "角色已死亡，是否消耗" + CFG_Info.num + GameUtil.Instance.GetItemName(CFG_Info.needMoneyID) + "立即原地复活 ?\n {0}秒内无操作将自动退出副本";
        }
        else
        {
            ShowStr = "当前复活次数已经用完,是否立即退出副本?{0}秒后无将自动退出副本";
        }

        GetView().Text_Tip.text = $"提示：当前还可复活{3 - info.ReviveTimes}次";
        ClearTime();
        GetView().Text_des.text = string.Format(ShowStr, CDTime);
        TimeJaction = GameHelper.Instance.AddInvoke(new TimeItem (CDTime,1, () =>
        {
            GetView().Text_des.text = string.Format(ShowStr, CDTime);
            CDTime--;
        }));
    }

    public override void OnExit()
    {
        ClearTime();
    }

    public override void OnDestroy()
    {
        ClearTime();
    }
}