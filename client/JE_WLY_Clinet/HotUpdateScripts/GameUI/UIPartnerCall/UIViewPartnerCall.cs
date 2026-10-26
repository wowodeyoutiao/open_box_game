using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewPartnerCall : MonoBehaviour,IUIView
{
	public Toggle tog_Anim;
    public Button btn_help;
    public Button btn_Close;

	public Transform root_res;
    public Transform root_Btns;

	public Text txt_tip;

	public Button btn_Book;
	public Button btn_PartnerGift;

    public Button btnOneGet;
	public Button btnMoreGet;

	public void Awake()
    {

    }
	public void Init(GameObject go)
	{
		btnOneGet = go.transform.Find("center/Btn_OneGet").GetComponent<Button>();
		btnMoreGet = go.transform.Find("center/Btn_MoreGet").GetComponent<Button>();

	}
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIPartnerCall>();
	}
}