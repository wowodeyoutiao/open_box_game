using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Helper;
using JEngine.UI;
using pb;
using SuperScrollView;
using HotUpdateScripts.ProtoM;
using JEngine.Event;

public class UIFightAward : UIBase<UIModelFightAward, UIViewFightAward>
{
    public override UILayer GetLayer()
    {
        return UILayer.Base;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIFightAward>();
        });

        GetView().infiniteScroll.InitGridView(0, OnFillItem);
    }

    LoopGridViewItem OnFillItem(LoopGridView gridView, int sordIdx, int row, int column)
    {
        LoopGridViewItem _LoopGridViewItem = gridView.NewListViewItem("Item");
        GameObject go = _LoopGridViewItem.gameObject;
        JUIExtensions.btn(go.transform).onClick.RemoveAllListeners();
       
        ItemInfo _item = GameRoomM.Instance.GetDropItemInfo()[sordIdx];
        var cfg_item = CFG_Item.GetById(_item.CfgId);
        go.transform.Find("icon").gameObject.SetActive(true);
        GameUtil.Instance.SetItemIcon(_item.CfgId, JUIExtensions.img(go.transform, "icon"), JUIExtensions.img(go.transform), JUIExtensions.img(go.transform, "icon/nature"));
        GameUtil.Instance.SetItemJobIcon(_item.CfgId, JUIExtensions.img(go.transform, "icon/img_job"));
        JUIExtensions.btn(go.transform).onClick.AddListener(
            () => {
                UITool.Open<UIItemShow>(null, _item.CfgId);
            });

        JUIExtensions.txt(go.transform.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(_item.Num);
        return _LoopGridViewItem;
    }


    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
        OnRefresh();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_GetNewSkill(GameEvent.Refresh_ItemDrop _e)
    {
        if (isNullGo)
            return;
        OnRefresh();
    }

    private void OnRefresh()
    {
        if (isNullGo)
            return;
        GetView().infiniteScroll.SetListItemCount(GameRoomM.Instance.GetDropItemInfo().Count);
        GetView().infiniteScroll.RefreshAllShownItem();
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}