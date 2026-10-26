using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using System;
using Spine.Unity;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using HotUpdateScripts.Game;

public class UIViewHall : MonoBehaviour, IUIView
{
	//背景
	public Image bg_0;
	public Image bg_1;

    public class DressItem 
	{
		public Button _btn;
		public Image _imgBG;
		public Image _icon;
		public Text _EquipLv;
		public Text _txtDes;
		public GameObject _objLock;
		public Text _txtLv;
        public Text _txtStar;
		public Image[] insetImgs;
    }
	public RectTransform img_BG;

    public GameObject UI_Team;
    public GameObject UI_TeamRoot;
    public Button btn_TeamUI;
    public GameObject obj_TeamUIUp;
    public GameObject obj_TeamUIDown;

	public Button Btn_Task;
	public Slider sli_Task;
	public Text txt_TaskPro;
	public Text txt_TaskDes;
	public Text txt_TaskState;
	public Image img_Task;

    //穿戴展示
    public Transform root_Dress;
	public Transform root_DressAdd1;
	public Transform root_DressAdd2;
    public Transform root_BtnTeam;

	public DressItem[] BaseDressItems;
    public DressItem[] Add1DressItems;
    public DressItem[] Add2DressItems;

    //宝箱
    public RectTransform trans_GuideOpenBoxPosition;
    public Transform root_Box;
	public SkeletonGraphic spine_Box;
	public Transform root_BoxHide;
	public Button btn_BoxOpen;
	public Text txt_BoxNum;
	public Button btn_BoxSet;
	public Button btn_BoxAdd;
	public Button btn_BoxJian;
	public Text txt_BoxOpenNum;
	public Button btn_BoxLv;
    public Text txt_BoxLv;
	public GameObject BoxShowUI;
    public Button btn_BoxDel;
    public Button btn_BoxClothing;
	public Transform root_BoxOpen;
	public GameObject obj_OpenOne;

    public Transform root_Base;
	public Transform root_topBtns;
	public Transform root_leftBtns;
	public GameObject obj_leftBtns;

	public Button btn_Chat;
	public Button btn_Chat2;

	public GameObject obj_leftDot;

	public Transform root_Pet;
	public Transform root_Event;
	public Transform root_spine;


	public Transform[] SpintRoots;

    public Transform obj_fly;

    public Text txt_boxTime;
	public GameObject obj_boxTimeon;
	public GameObject obj_boxTimeoff;

	public Animator ani_Btn_BoxSet;

    public Vector3 UpPos;

    public void Awake()
    {
		ani_Btn_BoxSet = btn_BoxSet.GetComponent<Animator>();
        BaseDressItems = new DressItem[root_Dress.childCount];
		for (int i = 0; i < root_Dress.childCount; i++)
		{
			var obj = root_Dress.GetChild(i);
			BaseDressItems[i] = new DressItem
			{
				_btn = obj.GetComponent<Button>(),
				_icon = obj.Find("icon").GetComponent<Image>(),
				_imgBG = obj.GetComponent<Image>(),
				_txtDes = obj.Find("txt").GetComponent<Text>(),
                _txtLv = obj.Find("txt_lv").GetComponent<Text>(),
                _txtStar = obj.Find("txt_Starlv").GetComponent<Text>(),
                _EquipLv = obj.Find("icon/txt_lv").GetComponent<Text>(),
            };
            BaseDressItems[i].insetImgs = new Image[4];

			for (int j = 0; j < 4; j++)
			{
				BaseDressItems[i].insetImgs[j] = obj.Find("inset").GetChild(j).GetComponent<Image>();
            }
        }

        Add1DressItems = new DressItem[root_DressAdd1.childCount];
        for (int i = 0; i < root_DressAdd1.childCount; i++)
        {
            var obj = root_DressAdd1.GetChild(i);
            Add1DressItems[i] = new DressItem
            {
                _btn = obj.GetComponent<Button>(),
                _icon = obj.Find("icon").GetComponent<Image>(),
                _imgBG = obj.GetComponent<Image>(),
				_objLock = obj.Find("lock").gameObject,
            };
        }

        Add2DressItems = new DressItem[root_DressAdd2.childCount];
        for (int i = 0; i < root_DressAdd2.childCount; i++)
        {
            var obj = root_DressAdd1.GetChild(i);
            Add2DressItems[i] = new DressItem
            {
                _btn = obj.GetComponent<Button>(),
                _icon = obj.Find("icon").GetComponent<Image>(),
                _imgBG = obj.GetComponent<Image>(),
                _objLock = obj.Find("lock").gameObject,
            };
        }


        UpPos = root_Box.transform.position;

        SpintRoots = new Transform[5];
		for (int i = 0; i < SpintRoots.Length; i++)
		{
			SpintRoots[i] = root_spine.Find("pos_"+i);
        }

    }
	public void Init(GameObject go)
	{
	}

    public void OnDestroy()
    {
		UITool.RemoveUI<UIHall>();
	}
}