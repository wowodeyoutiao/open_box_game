using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewExpAdd : MonoBehaviour,IUIView
{
	public Button btnClose;
	public Transform Root_Btns;

	public void Awake()
    {

    }
	public void Init(GameObject go)
	{
		btnClose = go.transform.Find("BG/Center/titleBg/Btn_Close").GetComponent<Button>();
		

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIExpAdd>();
	}
}