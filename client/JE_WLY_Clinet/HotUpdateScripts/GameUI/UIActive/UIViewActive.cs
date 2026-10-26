using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using SuperScrollView;

public class UIViewActive : MonoBehaviour,IUIView
{
	public Button closeBtn;
	public Transform tabBtnTrans;
	[HideInInspector] public Button[] tabBtns;
    public Text activeTimeText;

    // 七日签到
    public GameObject sign7Go;
	public Text signDesText;
    public LoopListView2 infiniteScroll_Sign7;

    // 七日狂欢 
	// 大奖
    public GameObject happy7Go;
	public Slider happy7Slider;
	public Transform happy7SliderAwardTrans;
	public Text happy7SchduleText;

	public Transform happy7TabBtnTrans;
    [HideInInspector] public Button[] happy7TabBtns;
    public LoopListView2 infiniteScroll_happy7;

	// 特惠礼包
	public Transform happy7GiftAwardTrans;
	public Transform happy7GiftAwardBtn;
	public GameObject GetGo;

    public void Awake()
    {
        tabBtns = new Button[tabBtnTrans.childCount];
		for (int i = 0; i < tabBtnTrans.childCount; i++)
            tabBtns[i] = tabBtnTrans.GetChild(i).GetComponent<Button>();

        happy7TabBtns = new Button[happy7TabBtnTrans.childCount];
        for (int i = 0; i < happy7TabBtnTrans.childCount; i++)
			happy7TabBtns[i] = happy7TabBtnTrans.GetChild(i).GetComponent<Button>();
    }
	public void Init(GameObject go)
	{

	}

	public void OnDestroy()
	{
		 UITool.RemoveUI<UIActive>();
	}
}