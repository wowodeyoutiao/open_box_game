using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;

public class UIViewCreatRole :MonoBehaviour, IUIView
{
	public Transform root_role;
	public Transform root_JobSelect;
	public Button[] JobSelectBtns;

	public Button btn_Boy;
	public Button btn_Gril;

	public InputField input_Name;
	public Button btn_randomName;
	public Button btn_Creat;

	public Text txt_Job;
	public Image img_job;

    public Transform root_job;
    public Text[] JobDesTxts;
	public Button btn_back;

    public void Awake()
    {
		JobSelectBtns = new Button[root_JobSelect.childCount];
        for (int i = 0; i < root_JobSelect.childCount; i++)
        {
			JobSelectBtns[i] = root_JobSelect.GetChild(i).GetComponent<Button>();
		}

        JobDesTxts = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            JobDesTxts[i] = root_job.Find("txt_Des"+i).GetComponent<Text>();
        }

    }
	public void Init(GameObject go)
	{

	}
	void OnDestroy()
	{
		UITool.RemoveUI<UICreatRole>();
	}
}