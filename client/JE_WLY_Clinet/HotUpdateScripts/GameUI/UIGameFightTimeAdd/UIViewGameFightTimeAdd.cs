using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewGameFightTimeAdd : MonoBehaviour,IUIView
{
	public Button Btn_Close;
	public Button Btn_Yes;

	public Text txt_buyAdd;

	public Toggle tog_1;
	public Toggle tog_2;

	public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIGameFightTimeAdd>();
	}
}