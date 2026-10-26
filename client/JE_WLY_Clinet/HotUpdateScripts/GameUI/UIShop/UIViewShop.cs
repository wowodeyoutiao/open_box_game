using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using SuperScrollView;

public class UIViewShop :MonoBehaviour, IUIView
{
	public Button Btn_Close;

	public Button Btn_CouponAdd;
	public Button Btn_DiamantaAdd;
	public Button Btn_CoinAdd;
	public Text txt_Coupon;
	public Text txt_Diamanta;
	public Text txt_Coin;

	public Text txt_SLPZ;
	public Button btn_SLPZ;
	public GameObject obj_SLPZ;
	public GameObject obj_ZanHun;

	public Transform Btns;
	public Button[] TopBtns;

	public Image img_AD;
	public LoopGridView infiniteScroll;

	public Button btn_Banner;

    public void Awake()
    {

    }
	public void Init(GameObject go)
	{
		TopBtns = new Button[Btns.childCount];
        for (int i = 0; i < Btns.childCount; i++)
        {
			TopBtns[i] = Btns.GetChild(i).GetComponent<Button>();
		}
	}

    private void OnDestroy()
    {
		UITool.RemoveUI<UIShop>();
    }

}