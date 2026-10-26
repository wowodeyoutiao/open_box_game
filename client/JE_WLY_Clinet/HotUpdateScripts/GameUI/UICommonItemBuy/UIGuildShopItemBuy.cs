using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using System;
using JEngine.Helper;
using static UIViewExpPool;
using JEngine.UI;


public class UICommonItemBuy : UIBase<UIModelCommonItemBuy, UIViewCommonItemBuy>
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
            UITool.Close<UICommonItemBuy>();
        });

        GetView().btn_buy.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnGuildShopBuyRequest();
        });

        GetView().btn_jian.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            _BuyCount--;
            _BuyCount = Mathf.Clamp(_BuyCount, _MinCount, _MaxCount);
            GetView().input_Buy.text = _BuyCount.ToString();
            ShowConsume();
        });

        GetView().btn_add.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            _BuyCount++;
            _BuyCount = Mathf.Clamp(_BuyCount, _MinCount, _MaxCount);
            GetView().input_Buy.text = _BuyCount.ToString();
            ShowConsume();
        });

        GetView().input_Buy.onValueChanged.AddListener((num) =>
        {
            _BuyCount = Mathf.Clamp(int.Parse(num), _MinCount, _MaxCount);
            GetView().input_Buy.text = _BuyCount.ToString();
            ShowConsume();
        });
    }

    private int _BuyCount = 1;
    private int _MinCount = 1;
    private int _MaxCount = 1;

    //private bool isRMB = false;

    CFGItem_Store _ItemStore;
    Action _BuyCallback;

    /// <summary>
    /// 进入界面
    /// </summary>
    /// <param name="args">[0] storeId, [1] title text, [2] callback</param>
    public override void OnEnter(params object[] args)
    {
        _ItemStore = CFG_Store.GetById((int)args[0]);
        GetView().txt_Title.text = args.Length < 2 ? $"{CFG_Item.GetById(_ItemStore.Solditems[0].itemID).Name}道具购买" : (string)args[1];
        _BuyCallback = args.Length < 3 ? null : (Action)args[2];

        _BuyCount = 1;
        int maxBuyCount = Mathf.FloorToInt((float)BagM.Instance.GetNumByCfgID(_ItemStore.Limite.needmoney) / _ItemStore.Limite.Num);
        _MaxCount = _ItemStore.Limite.limittype == 999 ? maxBuyCount : Mathf.Min(maxBuyCount, _ItemStore.Limite.limitnum - (int)StoreM.Instance.GetBuyNumByID(_ItemStore.ID));
        _MaxCount = Mathf.Max(_MaxCount, _MinCount);

        GetView().input_Buy.text = _BuyCount.ToString();
        GameUtil.Instance.SetItemIcon(_ItemStore.Limite.needmoney, GetView().img_Consume);
        ShowConsume();
    }

    private void OnGuildShopBuyRequest()
    {
        if (!GameUtil.Instance.ComCoinToCheck(_ItemStore.ID, null))
        {
            return;
        }

        UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
        {
            ShowStr = $"是否确定消耗{_ItemStore.Limite.Num * _BuyCount}{CFG_Item.GetById(_ItemStore.Limite.needmoney).Name}道具兑换{CFG_Item.GetById(_ItemStore.Solditems[0].itemID).Name}？",
            YesFun = () =>
            {
                GameUtil.Instance.GameToBuy(_ItemStore.ID, _BuyCount, _BuyCallback);
                UITool.Close<UICommonItemBuy>();
            },
        });
    }

    private void ShowConsume()
    {
        int consume = _ItemStore.Limite.Num * _BuyCount;
        string price = GameUtil.Instance.ComCoinToCheck(_ItemStore.ID, null) ? $"{GameUtil.Instance.FormatNumWithUnit(consume)}" : $"<color=#CE4031>{GameUtil.Instance.FormatNumWithUnit(consume)}</color>";
        GetView().txt_Consume.text = price;
        GetView().btn_buy.interactable = GameUtil.Instance.ComCoinToCheck(_ItemStore.ID, null);
    }

    public override void OnExit()
    {
        
    }

    public override void OnDestroy()
    {

    }
}