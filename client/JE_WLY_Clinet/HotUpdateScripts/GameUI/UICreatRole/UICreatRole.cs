using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using JEngine.LitJson;
using JEngine.Core;
using HotUpdateScripts;
using Spine.Unity;
using System.Collections.Generic;
using System.Linq;

public class UICreatRole : UIBase<UIModelCreatRole, UIViewCreatRole>
{
    public override UILayer GetLayer()
    {
        return UILayer.Base;
    }
    private HTTP_CreateCharacterRequest CreatInfo = new HTTP_CreateCharacterRequest { sex = (int)SexType.SexGril};
    public override void OnInit()
    {
        GetView().btn_Creat.onClick.AddListener(OnCreatBtnClick);
        GetView().btn_randomName.onClick.AddListener(delegate { 
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnRandmNameBtnClick();
        });

        for (int i = 0; i < GetView().JobSelectBtns.Length; i++)
        {
            int job = i + 1;
            GetView().JobSelectBtns[i].onClick.AddListener(delegate {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnJobChoiscClick((JobType)job); 
            });
        }

        GetView().btn_Gril.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnSexChoiscClick(SexType.SexGril);
        });

        GetView().btn_Boy.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnSexChoiscClick(SexType.SexBoy);
        });

        GetView().btn_back.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UICreatRole>();
        });
    }

    private void SetSex(SexType sexIDx)
    {
        if (IsClick)
        {
            UITool.ShowTips("操作频繁，请稍后");
            return;
        }
        if (!IsClick)
            IsClick = true;

        GetView().btn_Boy.transform.Find("on").gameObject.SetActive(sexIDx == SexType.SexBoy);
        GetView().btn_Gril.transform.Find("on").gameObject.SetActive(sexIDx == SexType.SexGril);
        CreatInfo.sex = (int)sexIDx;

        SetRole((JobType)CreatInfo.job, sexIDx);
    }

    bool IsClick = false;

    private void OnJobChoiscClick(JobType jobIdx)
    {
        if (IsClick)
        {
            UITool.ShowTips("操作频繁，请稍后");
            return ;
        }
        int idx = (int)jobIdx - 1;
        for (int i = 0; i < GetView().JobSelectBtns.Length; i++)
        {
            GetView().JobSelectBtns[i].transform.localScale = idx == i ? Vector3.one : Vector3.one * 0.8f;
            GetView().JobSelectBtns[i].transform.Find("On").gameObject.SetActive(idx == i);
        }
        CreatInfo.job = (int)jobIdx;

        GetView().txt_Job.text = CFG_Item.GetJobStrById((int)jobIdx);
        var strs = UIModelCreatRole.JobDesDic[jobIdx];
        for (int i = 0; i < strs.Length; i++)
        {
            GetView().JobDesTxts[i].text = strs[i];
        }
        ResouseHelper.Instance.LoadSpriteByAtlas(UISelectRole.SelectRoleAtlas, UISelectRole.JobIconName[(int)jobIdx], GetView().img_job);
        SetSex(SexType.SexBoy);
    }

    private Dictionary<string, string[]> RoleBronAnim = new Dictionary<string, string[]>
    {
        [$"{JobType.Warrior}{SexType.SexBoy}"] = new string[] { GameConst.ModelAnim.skill2.ToString(), GameConst.ModelAnim.skill3.ToString(),GameConst.ModelAnim.attack.ToString(), GameConst.ModelAnim.stand.ToString() },
        [$"{JobType.Warrior}{SexType.SexGril}"] = new string[] { GameConst.ModelAnim.skill2.ToString(), GameConst.ModelAnim.skill3.ToString(), GameConst.ModelAnim.skill1.ToString(), GameConst.ModelAnim.stand.ToString() },
        [$"{JobType.Mage}{SexType.SexBoy}"] = new string[] { GameConst.ModelAnim.skill1.ToString(), GameConst.ModelAnim.skill3.ToString(), GameConst.ModelAnim.stand.ToString() },
        [$"{JobType.Mage}{SexType.SexGril}"] = new string[] { GameConst.ModelAnim.skill1.ToString(), GameConst.ModelAnim.skill3.ToString(), GameConst.ModelAnim.stand.ToString() },
        [$"{JobType.Sacrifice}{SexType.SexBoy}"] = new string[] { GameConst.ModelAnim.skill1.ToString(), GameConst.ModelAnim.attack.ToString(),  GameConst.ModelAnim.stand.ToString() },
        [$"{JobType.Sacrifice}{SexType.SexGril}"] = new string[] { GameConst.ModelAnim.skill1.ToString(), GameConst.ModelAnim.attack.ToString(), GameConst.ModelAnim.stand.ToString() },

    };

    private void SetRole(JobType jobType, SexType sexType)
    {
        int Count = GetView().root_role.childCount;
        if (Count > 0)
        {
            for (int i = 0; i < Count; i++)
            {
                PoolHelper.Instance.RecycObj(GetView().root_role.GetChild(0).gameObject);
            }
        }
        
        GameObject Eff = null;
        _ = GameHelper.Instance.SetRoleAsync(jobType, sexType, (_mainRole) =>
        {
           
            _mainRole.transform.SetParent(GetView().root_role);
            _mainRole.transform.localScale = Vector3.one;
            _mainRole.transform.localPosition = Vector3.zero;
            _mainRole.SetActive(true);

            string booEff = $"{jobType.ToString()}{sexType.ToString()}_Bron";
            string booEffPath = GameConst.GetBornPatchByJobAndSex(jobType, sexType);
            if (!string.IsNullOrEmpty(booEffPath))
            {

                _ = GameHelper.Instance.SetAsyncObj(booEff, booEffPath, (go) =>
                {
                    go.SetActive(true);
                    go.transform.SetParent(GetView().root_role);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localScale = Vector3.one;

                    Eff = go;
                },true);
            }

            SpineUtil.Instance.PlayQueue(_mainRole.transform.Find("spine").GetComponent<SkeletonGraphic>(), RoleBronAnim[$"{jobType}{sexType}"].ToList(), () => {
                if (Eff != null)
                {
                    PoolHelper.Instance.RecycObj(Eff);
                    Eff = null;
                }
            }, true);
        });
            
        GameHelper.Instance.SetDeayTimeDo(() => { IsClick = false; },1f);
    }

    private void OnSexChoiscClick(SexType SexIdx)
    {
        CreatInfo.sex = (int)SexIdx;
        SetSex((SexType) SexIdx);
    }
    private void OnCreatBtnClick()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        if (GetView().input_Name.text == "")
        {
            UITool.ShowTips("名字为空");
            return;
        }
        CreatInfo.name = GetView().input_Name.text;

        HttpRequestHelper.SendPost<HTTP_CreateCharacterResponse>(GameM.Instance.HttpUrl + GameM.Instance.Role_CreatUrl, NetworkHelper.HTTPMethod.PUT, RoleM.Instance.Login_Token, JsonMapper.ToJson(CreatInfo),
            (_creat) => {
                if (_creat.code == 0 && !isNullGo)
                {
                    
                    RoleM.Instance.LoginCharacterInfo.Insert(0,_creat.characterList);
                    UITool.Open<UISelectRole>((ui) =>{
                        UITool.Close<UICreatRole>();
                    });
                }
                else
                {
                    UITool.ShowTips(_creat.date);
                }
            }, 
            () =>{ UITool.ShowTips("失败"); });
    }

    private void OnRandmNameBtnClick()
    {
        // 单机化兜底：CFG_Name 未加载 / 字典为空 / 条目过少时，
        // 原代码直接索引 [Random.Range(1, count)] 会 NullReference 或 KeyNotFound
        var nameDic = CFG_Name.GetDic();
        if (nameDic == null || nameDic.Count < 2)
        {
            GetView().input_Name.text = "本地勇者";
            return;
        }

        int dicCount = nameDic.Count;
        string nameStr = $"{nameDic[UnityEngine.Random.Range(1, dicCount)].Firstname}{nameDic[UnityEngine.Random.Range(1, dicCount)].Familyname}{nameDic[UnityEngine.Random.Range(1, dicCount)].Lastname}";
        GetView().input_Name.text = nameStr;
    }
    public override void OnEnter(params object[] args)
    {
        AudioManager.Instance.PlayBgMusic(AudioKey.Sound.BGM_Creat);
        IsClick = false;
        OnJobChoiscClick(JobType.Warrior);
        OnRandmNameBtnClick();
    }


    public override void OnExit()
    {
        AudioManager.Instance.PlayBgMusic(AudioKey.Sound.BGM_Slect);
    }
}