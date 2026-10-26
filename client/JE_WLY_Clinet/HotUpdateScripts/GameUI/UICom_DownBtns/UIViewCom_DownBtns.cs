using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.UI;

public class UIViewCom_DownBtns : MonoBehaviour,IUIView
{
	public Image img_JyPro;
	public Text txt_JyPro;
	public Slider slider_Jy;
	public BtnItem[] Btns;

    public class BtnItem 
	{
		public Button _btn;
		public GameObject _objLock;
		public Text _txtLock;
		public GameObject _objDot;
	}
    public void Awake()
    {

    }
	public void Init(GameObject go)
	{
        Btns = new BtnItem[7];
        for (int i = 1; i <= Btns.Length; i++)
        {
            var item = go.transform.Find("Btn_" + i);
            Btns[i-1] = new BtnItem {
                _btn = JUIExtensions.btn(item),
                _objDot = item.Find("dot").gameObject,
                _objLock = item.Find("lock").gameObject,
                _txtLock = JUIExtensions.txt(item, "lock/Text")
            };
           
        }
    }
	//public void OnDestroy()
	//{
	//	 UITool.RemoveUI<UICom_DownBtns>();
	//}
}