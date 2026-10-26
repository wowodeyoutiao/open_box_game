using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using JEngine.UI;
using SuperScrollView;

public class UIPartnerBook : UIBase<UIModelPartnerBook, UIViewPartnerBook>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIPartnerBook>();
        });
        GetView().dropdown_Qulite.AddOptions(GetModel().DroStrList);
        GetView().dropdown_Qulite.onValueChanged.AddListener(SetSortQulite);

        GetView().infiniteScroll.InitGridView(0, OnFillItem);

    }

    public override void OnEnter(params object[] args)
    {
        select_Qulite = 0;
        GetView().dropdown_Qulite.value = 0;
        SetSortQulite(select_Qulite);
    }

    private LoopGridViewItem OnFillItem(LoopGridView gridView, int sordIdx, int row, int column)
    {
        LoopGridViewItem _LoopGridViewItem = gridView.NewListViewItem("Item");
        GameObject go = _LoopGridViewItem.gameObject;

        JUIExtensions.btn(go.transform).onClick.RemoveAllListeners();
        var _item = siftList[sordIdx];
        var PartnerInfo = PartnerM.Instance.GetOnePartnerInfoByCFGID(_item.ID);
        go.transform.Find("unGet").gameObject.SetActive(PartnerInfo == null);
        JUIExtensions.btn(go.transform).onClick.AddListener(() => {
            UITool.Open<UIItemShow>(null,_item.ItemID);
        });

        GameUtil.Instance.SetJobIcon(_item.job % 10, JUIExtensions.img(go.transform, "img_job"));
        go.transform.Find("img_lv").gameObject.SetActive(PartnerInfo != null);
        if (PartnerInfo != null)
        {
            JUIExtensions.txt(go.transform.Find("img_lv/txt_lv")).text = "lv"+ PartnerInfo.Level.ToString();
        }

        GameUtil.Instance.SetItemIcon(_item.ItemID, JUIExtensions.img(go.transform, "icon"), JUIExtensions.img(go.transform), null, _item.Quality);

        return _LoopGridViewItem;

    }

    private int select_Qulite = 0;
    private void SetSortQulite(int idx)
    {
        select_Qulite = GetModel().DroQualityList[idx];
        SetSiftList();
    }

    private List<CFGItem_Partner> siftList = new List<CFGItem_Partner>();
    private void SetSiftList()
    {
        siftList.Clear();

        for (int i = 0; i < CFG_Partner.Get().DicList.Count; i++)
        {
            if (CFG_Partner.Get().DicList[i].Quality >= select_Qulite || select_Qulite == 0)
            {
                siftList.Add(CFG_Partner.Get().DicList[i]);
            }
        }
        siftList.Sort((a, b) => {
            int qualityComparison = b.Quality.CompareTo(a.Quality);
            if (qualityComparison != 0)
                return qualityComparison;

            bool aExists = PetM.Instance.GetOnePetByCfgId(a.ID) != null;
            bool bExists = PetM.Instance.GetOnePetByCfgId(b.ID) != null;

            if (aExists != bExists)
                return aExists ? -1 : 1;

            return b.ID.CompareTo(a.ID);

        });

        if (siftList.Count != GetView().infiniteScroll.ItemTotalCount)
            GetView().infiniteScroll.SetListItemCount(siftList.Count);
        GetView().infiniteScroll.RefreshAllShownItem();
    }

    public override void OnExit()
    {

    }

    public override void OnDestroy()
    {

    }
}