using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.UI;
using System.Linq;
using JEngine.Helper;
using SuperScrollView;

public class UITurnBoxRule : UIBase<UIModelTurnBoxRule, UIViewTurnBoxRule>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().Btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UITurnBoxRule>();
        });
        
        GetView().infiniteScroll.InitListView(CFG_XMLConfig.xmlConfig.TreasureboxDic.Count, OnFillItem);
    }

    private LoopListViewItem2 OnFillItem(LoopListView2 listView, int idx)
    {
        var _item = listView.NewListViewItem("Item");
        GameObject go = _item.gameObject;
        var item = listView.NewListViewItem("Item");
        JUIExtensions.txt(go.transform.Find("txt_Des")).text = GameUtil.Instance.GetItemName(CFG_XMLConfig.xmlConfig.TreasureboxDic.ElementAt(idx).Key) ;
        JUIExtensions.txt(go.transform.Find("txt_pro")).text = CFG_XMLConfig.xmlConfig.TreasureboxDic.ElementAt(idx).Value;
        return _item;
    }

    //private string[] strDess;
    public override void OnEnter(params object[] args)
    {
    }

}