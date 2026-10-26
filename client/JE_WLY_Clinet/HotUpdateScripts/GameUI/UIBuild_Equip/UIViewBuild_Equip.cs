using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewBuild_Equip : MonoBehaviour,IUIView
{

	public Transform root_res;

	public Button btn_Help;

	public Transform root_allEquip;
	public Transform tip_equipTip;


	// 强化
	public GameObject ReinforceUI;
	public Button btn_Reinforce;

	public Image img_ItemBgReinforce;
	public Image img_ItemReinforce;
	public Text txt_StarReinforce;
	public Text txt_ReinforceLv;
	public Text txt_ReinforceItemName;

	public Text txt_ReinforceNowLv;
	public Text txt_ReinforceNowDes;

	public Text txt_ReinforceNextLv;
	public Text txt_ReinforceNextDes;

	public Image img_ReinforceGold;
	public Text txt_ReinforceGoldNum;
	public Image img_ReinforceQHS;
	public Text txt_ReinforceQHSNum;

	public Button btn_AddReinforceOne;
	public Button btn_AddReinforceTen;
	public Button btn_AddReinforceEnd;
	[HideInInspector] public GameObject Obj_AddReinforceEnd;

	public Text txt_ReinforceTip;

	//升星
	public GameObject StarUI;

	public Button btn_Star;

	public Image img_Star_ItemBg;
	public Image img_Star_Item;
	public Text txt_Star_StarLv;
	public Text txt_Star_ReinforceLv;
	public Text txt_Star_ItemName;

	public Text txt_Star_Des;

	public Text txt_Star_NowLv;
	public Text txt_Star_NowDes;

	public Text txt_Star_NextLv;
	public Text txt_Star_NextDes;

	public Image img_Star_GoldNum;
	public Text txt_Star_GoldNum;
	public Image img_Star_SXSNum;
	public Text txt_Star_SXSNum;

	public Button btn_AddStarOne;
	public Button btn_AddStarTop;
	public Dropdown dropDown_Star;

	public GameObject obj_StarTip;
	public GameObject obj_EndStar;
	public Button btn_EndStar;

	//洗练
	public GameObject RefineUI;

	public Image img_Refine_ItemBg;
	public Image img_Refine_Item;
	public Text txt_Refine_ItemName;
	public Text txt_Refine_Star;
	public Text txt_Refine_Reinfoce;

	public Transform root_RefineArrt;
	public Text txt_Target;

	public Text txt_RefineNum;
	public Image img_Refine_Icon;

	public Toggle tog_Refine1;
	public Toggle tog_Refine2;

	public Text txt_RefineTip;
	public Button btn_Refine;

	public Button btn_RefineNo;
	public Button btn_RefineYes;
	public GameObject obj_RefineEnd;

	//镶嵌 魂石
	public GameObject InsetUI;
	public Transform Inset_Item;
	public Button btn_InsetInfo;
	public Button btn_InsetNatrue;
	public Button btn_AutoInset;
	public Text txt_InsetTip;
	public Transform root_Inset;

	public struct UsedItem
	{
		public Image img;
		public Text txt;
		public Button btn;
		public GameObject obj;
	}

    [HideInInspector] public UsedItem[] usedItems;

    public void Awake()
    {

    }
	public void Init(GameObject go)
	{
		Obj_AddReinforceEnd = btn_AddReinforceEnd.transform.parent.gameObject;

		usedItems = new UsedItem[root_res.childCount];
		for (int i = 0; i < root_res.childCount; i++)
		{
			usedItems[i] = new UsedItem {
                obj = root_res.GetChild(i).gameObject,
                btn = root_res.GetChild(i).Find("add").GetComponent<Button>(),
                img = root_res.GetChild(i).Find("tip").GetComponent<Image>(),
				txt = root_res.GetChild(i).Find("txt").GetComponent<Text>()
            };

        }

    }
	public void OnDestroy()
	{
		 //UITool.RemoveUI<UIBuild_Equip>();
	}
}