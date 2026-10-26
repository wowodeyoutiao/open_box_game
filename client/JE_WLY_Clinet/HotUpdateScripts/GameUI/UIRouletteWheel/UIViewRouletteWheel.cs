using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewRouletteWheel : MonoBehaviour,IUIView
{
	public Button btn_Close;
	public Text txt_Time;
	public Text txt_Des;

	public Transform root_arrow;
	public CanvasGroup group_light;
	public Transform root_Item;

	public Transform txt_Over;

	public Button btn_One;

	public Image img_Icon;
	public Text txt_Num;
	public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIRouletteWheel>();
	}
}