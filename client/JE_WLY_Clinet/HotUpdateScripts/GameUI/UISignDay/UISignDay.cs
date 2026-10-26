//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.UI;
//using MetaFramework.UI;
//using JEngine.UI;
//using JEngine.Helper;
//using System.Linq;
//using HotUpdateScripts.ProtoM;
//using pb;
//using SuperScrollView;

//public class UISignDay : UIBase<UIModelSignDay, UIViewSignDay>
//{
//    public override UILayer GetLayer()
//    {
//        return UILayer.Normal;
//    }
//    public override void OnInit()
//    {
//        GetView().btn_Close.onClick.AddListener(()=> {
//            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
//            UITool.Close<UISignDay>();
//        });
//        GetView().btn_VIPUp.onClick.AddListener(() =>
//        {
//            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
//            UITool.Close<UISignDay>();
//            UITool.Open<UIVIP>();
//        });

//        GetView().ScrollView_Sign.InitGridView(0, OnFillItem);
//        GetView().ScrollView_Cumulative.InitListView(0, OnFillAccumulateItem);
//    }

//    LoopGridViewItem OnFillItem(LoopGridView gridView, int index, int row, int column)
//    {
//        int sordIdx = (index);

//        LoopGridViewItem _LoopGridViewItem = gridView.NewListViewItem("Item");
//        GameObject Item = _LoopGridViewItem.gameObject;

//        var signInfo = singList[index];
//        JUIExtensions.txt(Item.transform.Find("txt_Name")).text = $"第{signInfo.Days}天";
//        JUIExtensions.txt(Item.transform.Find("Vip/txt_Viptip")).text = $"V{signInfo.VIPLimit}";

//        GameUtil.Instance.SetItemIcon(signInfo.Award[0].id, JUIExtensions.img(Item.transform.Find("Item/icon")), JUIExtensions.img(Item.transform.Find("Item")));
//        JUIExtensions.btn(Item.transform.Find("Item")).onClick.RemoveAllListeners();

//        JUIExtensions.btn(Item.transform.Find("Item")).onClick.AddListener(() => {
//            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
//            UITool.Open<UIItemShow>(null, signInfo.Award[0].id);
//        });

//        JUIExtensions.txt(Item.transform.Find("txt_num")).text = "x" + GameUtil.Instance.FormatNumWithUnit(signInfo.Award[0].num);
//        Item.transform.Find("geted").gameObject.SetActive(httpData.day_award.day > signInfo.Days || httpData.day_award.day == signInfo.Days && httpData.day_award.sign);
//        Item.transform.Find("select").gameObject.SetActive(httpData.day_award.day == signInfo.Days && !httpData.day_award.sign);
//        JUIExtensions.touch(Item.transform).RemoveAllListeners();
//        if (httpData.day_award.day == signInfo.Days && !httpData.day_award.sign)
//        {
//            JUIExtensions.btn(Item.transform).onClick.AddListener(() => {
//                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
//                HttpC.GetSignOneDay((_r) => {
//                    if (!isNullGo)
//                        OnRefresh();
//                });
//            });
//        }

//        return _LoopGridViewItem;
//    }

//    private LoopListViewItem2 OnFillAccumulateItem(LoopListView2 loopListView, int idx)
//    {
//        LoopListViewItem2 item = loopListView.NewListViewItem("Item");
//        GameObject Item = item.gameObject;

//        var signInfo = accumulateList[idx];
//        JUIExtensions.txt(Item.transform.Find("txt_Name")).text = $"累计{signInfo.Days}天";
//        JUIExtensions.txt(Item.transform.Find("Vip/txt_Viptip")).text = $"V{signInfo.VIPLimit}";

//        for (int i = 0; i < 2; i++)
//        {
//            Transform awardRoot = Item.transform.Find("award").GetChild(i);
//            awardRoot.gameObject.SetActive(i < signInfo.AccumulateAward.Length);
//            if (i < signInfo.AccumulateAward.Length)
//            {
//                GameUtil.Instance.SetItemIcon(signInfo.AccumulateAward[i].id, JUIExtensions.img(awardRoot, "icon"), JUIExtensions.img(awardRoot));
//                JUIExtensions.txt(awardRoot, "txt_num").text = GameUtil.Instance.FormatNumWithUnit(signInfo.AccumulateAward[i].num);
//            }

//            JUIExtensions.btn(awardRoot).onClick.RemoveAllListeners();

//            int cfgID = signInfo.AccumulateAward[i].id;
//            JUIExtensions.btn(awardRoot).onClick.AddListener(() => {
//                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
//                UITool.Open<UIItemShow>(null, cfgID);
//            });
//        }
        

//        Item.transform.Find("geted").gameObject.SetActive(aSign.Contains(signInfo.Days));
//        Item.transform.Find("select").gameObject.SetActive(!aSign.Contains(signInfo.Days) && AllSignCount >= signInfo.Days);

//        JUIExtensions.btn(Item.transform).onClick.RemoveAllListeners();
//        if (!aSign.Contains(signInfo.Days) && AllSignCount >= signInfo.Days)
//        {
//            ShowCount = ShowCount == 0 ? idx : ShowCount;
//            JUIExtensions.btn(Item.transform).onClick.AddListener(() => {
//                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

//                HttpC.GetSignAccumulateDay(signInfo.Round, signInfo.Days, (_r) => {
//                    if (!isNullGo)
//                        OnRefresh();
//                });

//            });
//        }
//        return item;
//    }


//    private List<CFGItem_Sign> singList;
//    private List<CFGItem_Sign> accumulateList;
//    private HTTP_SignAwsard httpData;
//    private HashSet<int> aSign;
//    private int AllSignCount;
//    private int ShowCount = 0;
//    private void OnSignShow(HTTP_SignAwsard data)
//    {
//        httpData = data;

//        (singList, accumulateList) = CFG_Sign.GetSignAndAccumulateListByRound(data.day_award.round);

//        if (GetView().ScrollView_Sign.ItemTotalCount != singList.Count)
//            GetView().ScrollView_Sign.SetListItemCount(singList.Count);

//        GetView().ScrollView_Sign.RefreshAllShownItem();
//        GetView().ScrollView_Sign.MovePanelToItemByRowColumn(httpData.day_award.day / 3, 0);

//        AllSignCount = data.day_award.sign ? data.day_award.day : data.day_award.day - 1;
//        aSign = new HashSet<int>(); 
//        for (int i = 0; i < data.accumulate_award.Length; i++)
//        {
//            aSign.Add(data.accumulate_award[i].day);
//        }
//         ShowCount = 0;

//        if (GetView().ScrollView_Cumulative.ItemTotalCount != accumulateList.Count)
//            GetView().ScrollView_Cumulative.SetListItemCount(accumulateList.Count);

//        GetView().ScrollView_Cumulative.RefreshAllShownItem();
//        GetView().ScrollView_Cumulative.MovePanelToItemIndex(ShowCount, 0);

//    }

//    public override void OnEnter(params object[] args)
//    {
//        OnSignShow((HTTP_SignAwsard)args[0]);

//        GetView().btn_VIPUp.gameObject.SetActive(!CFG_VIP.IsMaxVipLevel(RoleM.Instance.Vip));
//    }

//    private void OnRefresh()
//    {
//        HttpC.GetSignList((_r) =>
//        {
//            if (!isNullGo)
//            OnSignShow(_r.data);
//        });
//    }

//    public override void OnExit()
//    {
        
//    }

//     public override void OnDestroy()
//    {

//    }
//}