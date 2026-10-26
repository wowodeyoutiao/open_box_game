using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using SuperScrollView;

public class UIViewLimitedBounty : MonoBehaviour,IUIView
{
	public LoopListView2 infiniteScroll;
	public Button btn_Close;
	public Text txt_Time;
	public Button btn_Help;

	public Transform root_content;

	public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UILimitedBounty>();
	}
}