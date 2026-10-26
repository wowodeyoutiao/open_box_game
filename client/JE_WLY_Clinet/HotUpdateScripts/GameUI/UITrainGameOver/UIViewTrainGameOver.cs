using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewTrainGameOver : MonoBehaviour,IUIView
{
	public GameObject obj_Win;
	public GameObject obj_Lose;

	public Transform root_Awards;
	public Button btn_back;
	public Text txt_Time;

	public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UITrainGameOver>();
	}
}