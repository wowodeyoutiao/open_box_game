using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using SuperScrollView;

public class UIViewFightChapterAward : MonoBehaviour,IUIView
{
	public Button btnClose;

	public LoopListView2 loopListView;


    public void Awake()
    {

    }
	public void Init(GameObject go)
	{
		btnClose = go.transform.Find("Center/Btn_Close").GetComponent<Button>();

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIFightChapterAward>();
	}
}