using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using SuperScrollView;

public class UIViewRank : MonoBehaviour,IUIView
{
	public LoopListView2 InfiniteScroll_Rank;
	public Transform root_top3;
	public Transform root_main;
	public Transform root_btns;
	public Button btn_Close;
	public Text txt_Desc;

	public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIRank>();
	}
}