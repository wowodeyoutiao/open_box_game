using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using SuperScrollView;

public class UIViewTurnBoxRule : MonoBehaviour,IUIView
{
	public Button Btn_Close;

	public LoopListView2 infiniteScroll;
	public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UITurnBoxRule>();
	}
}