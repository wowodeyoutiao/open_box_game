using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewGameFightOver : MonoBehaviour,IUIView
{
    public Transform root_Awards;
    public Text txt_Time;
	public Text txt_des;
	public Text txt_normalAwards;

	public Transform root_useTime;
	public Text txt_useTime;
	public GameObject obj_newRecord;

	public Text txt_point;

    public Button btn_yellow;
    public Text txt_yellow;

    public Button btn_blue;
    public Text txt_blue;

	public GameObject obj_win;
    public GameObject obj_lose;

	public Text txt_num;

    public void Awake()
    {

    }
	public void Init(GameObject go)
	{

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIGameFightOver>();
	}
}