using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewTurnBox : MonoBehaviour,IUIView
{
	public Button btn_Close;
	public Button btn_Help;
	public Button btn_Rule;

	public Text txt_Count;
	public Text txt_pro;
	public Transform root_loop;
	public Transform root_loopEff;

	public Transform root_Result;

	public Button btn_One;
	public Transform btn_OneTip;

	public Button btn_Ten;
	public Transform btn_TenTip;

	public Toggle tog_anim;

	public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UITurnBox>();
	}
}