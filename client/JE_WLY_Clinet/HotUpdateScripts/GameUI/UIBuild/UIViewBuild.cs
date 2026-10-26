using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewBuild : MonoBehaviour,IUIView
{
    public Text txt_Title;
    public Button btn_Close;
    public Transform root_UI;

    public Transform root_SelectOne;
    public Transform root_SelectTwo;

    public Transform root_Head;
    public Transform root_Myhead;
    public Transform root_Team;

    public struct SelectBtn
    {
        public Button _btn;
        public Text _txtOn;
        public Text _txtOff;
        public GameObject _objOn;
        public GameObject _objRed;
        public GameObject _objMask;
        public Text _txtOpen;
    }
    public SelectBtn[] selectOneBtns;
    public SelectBtn[] selectTwoBtns;

    public struct TeamItem
    {
        public Image _img_head;
        public Image _img_job;
        public GameObject _obj_Lv;
        public Text _txt_lv;
        public Button _btn;
        public GameObject _obj_lock;
        public Text _txt_Lock;
        public Text txt_Star;
        public GameObject _obj_On;
        public GameObject _obj_RedDot;
    }

    public TeamItem[] teamItems;
    public void Awake()
    {

    }
	public void Init(GameObject go)
	{
        selectOneBtns = new SelectBtn[3];

        for (int i = 0; i < root_SelectOne.childCount; i++)
        {
            selectOneBtns[i] = new SelectBtn { 
                _btn = root_SelectOne.GetChild(i).GetComponent<Button>(),
                _objOn = root_SelectOne.GetChild(i).Find("Select").gameObject,
                _objRed = root_SelectOne.GetChild(i).Find("dot").gameObject,
                _objMask = root_SelectOne.GetChild(i).Find("Mask").gameObject,
                _txtOpen = root_SelectOne.GetChild(i).Find("Mask/txt").GetComponent<Text>(),
            };
        }

        selectTwoBtns = new SelectBtn[root_SelectTwo.childCount];

        for (int i = 0; i < root_SelectTwo.childCount; i++)
        {
            selectTwoBtns[i] = new SelectBtn { 
                _btn = root_SelectTwo.GetChild(i).GetComponent<Button>(),
                _objOn = root_SelectTwo.GetChild(i).Find("Select").gameObject,
                _txtOn = root_SelectTwo.GetChild(i).Find("Label").GetComponent<Text>(),
                _objRed = root_SelectTwo.GetChild(i).Find("dot").gameObject,
            };
        }

        teamItems = new TeamItem[5];
        for (int i = 1; i <= root_Team.childCount; i++)
        {
            SetTeamItem(i, root_Team.GetChild(i - 1));
            teamItems[i].txt_Star = root_Team.GetChild(i - 1).Find("root_Star").GetComponent<Text>();
        }
        SetTeamItem(0, root_Myhead);
        void SetTeamItem(int i, Transform root)
        {
            teamItems[i] = new TeamItem
            {
                _btn = root.GetComponent<Button>(),
                _img_head = root.Find(i == 0 ?"img_head/img": "img_head").GetComponent<Image>(),
                _img_job = root.Find("img_job").GetComponent<Image>(),
                _obj_Lv = root.Find("img_Lv").gameObject,
                _txt_lv = root.Find("img_Lv/Txt_lv").GetComponent<Text>(),
                _obj_lock = root.Find("obj_Lock").gameObject,
                _txt_Lock = root.Find("obj_Lock/txt_Lock").GetComponent<Text>(),
                _obj_On = root.Find("on").gameObject,
                _obj_RedDot = root.Find("dot").gameObject,
            };
        }
    }
	public void OnDestroy()
	{
		 UITool.RemoveUI<UIBuild>();
	}
}