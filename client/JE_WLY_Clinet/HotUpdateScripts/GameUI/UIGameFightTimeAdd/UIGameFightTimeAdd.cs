using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.MyNet;
using pb;
using JEngine.Helper;

public class UIGameFightTimeAdd : UIBase<UIModelGameFightTimeAdd, UIViewGameFightTimeAdd>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    private bool _AutoBuy = false;
    public override void OnInit()
    {
        GetView().Btn_Close.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIGameFightTimeAdd>();
        });

        GetView().Btn_Yes.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            int consumeEnergy = GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.PVE_Peace ? CFG_XMLConfig.xmlConfig.BaseValue.energy.consumeEnergy_Peace : CFG_XMLConfig.xmlConfig.BaseValue.energy.consumeEnergy_War;
            if (consumeEnergy > RoleM.Instance.Energy)
            {
                GameUtil.Instance.OnEnegerAdd(null);
                return;
            }
            TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_InstanceTimeAdd, MType.Request,
               TCPClientMgr.ProtoSerialize(new InstanceTimeAddRequest
               {
                   EnergyNum = _AutoBuy ? 0:1,
                   AutoBuy = _AutoBuy
               })) ;

            UITool.Close<UIGameFightTimeAdd>();

        });

        GetView().tog_2.onValueChanged.AddListener((isOn) => {
            if (isOn)
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                _AutoBuy = true;
            }
        });

        GetView().tog_1.onValueChanged.AddListener((isOn) => {
            if (isOn)
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                _AutoBuy = false;
            }
        });

    }

    public override void OnEnter(params object[] args)
    {
        int consumeEnergy = GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.PVE_Peace ? CFG_XMLConfig.xmlConfig.BaseValue.energy.consumeEnergy_Peace : CFG_XMLConfig.xmlConfig.BaseValue.energy.consumeEnergy_War;
        int buytime = GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.PVE_Peace ? CFG_XMLConfig.xmlConfig.BaseValue.energy.buyTime_Peace : CFG_XMLConfig.xmlConfig.BaseValue.energy.buyTime_War;
        GetView().txt_buyAdd.text = $"消耗<color=#E84D2E>{consumeEnergy}</color>点体力延长<color=#3B63D3>{buytime / 60}</color>分钟";
        GetView().tog_2.isOn = GameRoomM.Instance.TimeAutoBuy;
        GetView().tog_1.isOn = ! GameRoomM.Instance.TimeAutoBuy;
        _AutoBuy = GameRoomM.Instance.TimeAutoBuy;
    }

    public override void OnExit()
    {
        
    }

     public override void OnDestroy()
    {

    }
}