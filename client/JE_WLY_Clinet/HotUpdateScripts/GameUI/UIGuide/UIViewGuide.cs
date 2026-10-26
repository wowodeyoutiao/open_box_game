using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewGuide : MonoBehaviour,IUIView
{
	public GameObject obj_bgMask;
	public GameObject obj_Center;
	public Transform root_Focus;
	public Button btn_Focus;

	public GameObject obj_Touch;
	public ETCJoystick _ETCJoystick;

	public Button btn_Jump;
	public Text txt_Des;
	public Canvas canvas_Joy;

	public RectTransform root_Des;

	public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIGuide>();
	}
}