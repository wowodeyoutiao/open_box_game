using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;

public class UIGameSweepAndBuy : UIBase<UIModelGameSweepAndBuy, UIViewGameSweepAndBuy>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().BtnClose.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIGameSweepAndBuy>();
        });

        GetView().btnAdd.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnNumChang(true);
        });

        GetView().btnJian.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnNumChang(false);
        });

        GetView().btnMax.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnNumMaxOrMin(true);
        });

        GetView().btnMin.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnNumMaxOrMin(false);
        });

        GetView().inputField.onValueChanged.AddListener((string value) =>
        {
            if (int.Parse(value) > MaxNum)
                GetView().inputField.text = MaxNum.ToString();
            else if (int.Parse(value) <= 1) 
                GetView().inputField.text = "1";
        } );

        GetView().btnyes.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (BattleID == 0)
            {
                int shopID = opType == OpType.RelicBuy ? (int)CFG_Store.StroeID.RelicChange : (int)CFG_Store.StroeID.BossChange;
                int num = int.Parse(GetView().inputField.text);
                if (!GameUtil.Instance.ComCoinToCheck(shopID, delegate { UITool.Close<UIGameSweepAndBuy>(); },true, num))
                {
                    return;
                }
                GameUtil.Instance.GameToBuy(shopID, num, () =>
                {
                    GameHelper.Instance.SetDeayTimeDo(delegate {
                        TcpC.C2S_InstanceInfo();
                    },1.5f);
                    
                });
            }
            else
            {
                TcpC.C2S_CopySweep(BattleID, int.Parse(GetView().inputField.text));
            }
            UITool.Close<UIGameSweepAndBuy>();
        });
    }

    public enum OpType 
    {
        RelicSweep,
        RelicBuy,
        BossBuy,
        BossSweep,
    }

    private void OnNumMaxOrMin(bool isMax)
    {
        int num = isMax ? MaxNum : 1;
        GetView().inputField.text = num.ToString();
        OnUsedNumShow();
    }

    private void OnNumChang(bool isAdd)
    {
        int num = int.Parse(GetView().inputField.text);
        if (isAdd && num < MaxNum)
        {
            num ++;
        }
        else if (!isAdd && num > 1)
        {
            num -- ;
        }
        GetView().inputField.text = num.ToString();
        OnUsedNumShow();
    }

    private int MaxNum = 0;

    private int BattleID = 0;
    OpType opType ;
    public override void OnEnter(params object[] args)
    {
         opType = (OpType)args[0];
        if (opType == OpType.RelicSweep)
        {
            MaxNum = FightPlayM.Instance.GetInfo().relic_map.ChallengeTime;
            GetView().txt_des.text = "可扫荡次数：" + MaxNum;
            GetView().txt_btn.text = "扫荡";
            BattleID = (int)args[1];
        }
        else if (opType == OpType.BossSweep)
        {
            MaxNum = FightPlayM.Instance.GetInfo().boss_map.ChallengeTime;
            GetView().txt_des.text = "可扫荡次数：" + MaxNum;
            GetView().txt_btn.text = "扫荡";
            BattleID = (int)args[1];
        }
        else
        {
            BattleID = 0;
            MaxNum = StoreM.Instance.GetCanBuyNum((opType == OpType.RelicBuy ? (int)CFG_Store.StroeID.RelicChange : (int)CFG_Store.StroeID.BossChange));
            GetView().txt_des.text = "今日可购次数：" + MaxNum;
            GetView().txt_btn.text = "购买";
        }
        GetView().obj_used.SetActive(opType != OpType.RelicSweep && opType != OpType.BossSweep);
        GameHelper.Instance.SetDeayFrame(() => {
            OnNumMaxOrMin(true);
        });

    }

    private void OnUsedNumShow()
    {
        if (opType == OpType.RelicSweep)
            return;

        var cfgStroe = CFG_Store.GetById((opType == OpType.RelicBuy ? (int)CFG_Store.StroeID.RelicChange : (int)CFG_Store.StroeID.BossChange));
        GameUtil.Instance.SetItemIcon(cfgStroe.Limite.needmoney, GetView().img_used);
        GetView().txt_usedNum.text = GameUtil.Instance.GetCheakNumStr(cfgStroe.Limite.needmoney, cfgStroe.Limite.Num * int.Parse(GetView().inputField.text));
    }

    public override void OnExit()
    {
        
    }

     public override void OnDestroy()
    {

    }
}