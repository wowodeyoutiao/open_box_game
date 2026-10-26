using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using JEngine.UI;
using JEngine.Core;

public class UIPVPCountBuy : UIBase<UIModelPVPCountBuy, UIViewPVPCountBuy>
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
            UITool.Close<UIPVPCountBuy>();
        });
        GetView().btn_buy.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            System.Action closeCall = () => {
                UITool.Close<UIPVPCountBuy>();
            };
            if (!GameUtil.Instance.ComCoinToCheck(_XmlInfo.storeId, closeCall,true, _BuyCount))
            {
                return;
            }

            System.Action backCall = () => {
                UITool.Close<UIPVPCountBuy>();
                GameHelper.Instance.SetDeayTimeDo(() => { TcpC.OnPVPInfo(_MatchCount); }, 1f);
            };
            GameUtil.Instance.GameToBuy(_XmlInfo.storeId, _BuyCount, backCall);
        });

        GetView().btn_jian.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            _BuyCount--;
            OnSetBuyCount(_BuyCount);
        });

        GetView().btn_add.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            _BuyCount++;
            OnSetBuyCount(_BuyCount);
        });

        GetView().input_Buy.onValueChanged.AddListener((num) =>
        {
            OnSetBuyCount(int.Parse(num));
        });
    }

    private void OnSetBuyCount(int num)
    {
        int minNum = Mathf.Min(RoleM.Instance.GetCanNumBuyPVP(_MatchCount), 1); 
        _BuyCount = Mathf.Clamp(num, minNum, RoleM.Instance.GetCanNumBuyPVP(_MatchCount));

        GameHelper.Instance.SetDeayFrame(() =>
        {
            GetView().input_Buy.text = "" + _BuyCount;
        });

        GameUtil.Instance.SetItemIcon(CFG_Store.GetById(_XmlInfo.storeId).Limite.needmoney, GetView().img_Icon);
        GetView().txt_Icon.text = "x" + GameUtil.Instance.FormatNumWithUnit(CFG_Store.GetById(_XmlInfo.storeId).Limite.Num * _BuyCount) ;
    }

    private int _BuyCount = 1;
    private int _MatchCount = (int)pb.MatchType.SINGLE_COMBAT;

    private CFG_XMLConfig.XML_Config.XML_Item_PVP _XmlInfo;
    public override void OnEnter(params object[] args)
    {
        _MatchCount = (int)args[0];
        _BuyCount = 1;

        _XmlInfo = _MatchCount == (int)pb.MatchType.SINGLE_COMBAT ? CFG_XMLConfig.xmlConfig.PVPInfo : CFG_XMLConfig.xmlConfig.GVGInfo;
        OnSetBuyCount(_BuyCount);
        GetView().txt_tip.text = $"提示:今日可购买次数<color=#CE4031>{RoleM.Instance.GetCanNumBuyPVP(_MatchCount)}</color>";
    }

    public override void OnExit()
    {
        
    }

     public override void OnDestroy()
    {

    }
}