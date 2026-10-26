using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using HotUpdateScripts;
using JEngine.Event;

public class UIBuild : UIBase<UIModelBuild, UIViewBuild>
{
    public enum SelectOne
    {
        Skill,
        Equip,
        ToDo,
    }
    private SelectOne selectOne = SelectOne.Skill;
    private SelectTwo selectTwo = SelectTwo.Skill_UpLv;
    public enum SelectTwo
    {
        /// <summary>
        /// 装备强化
        /// </summary>
        Equip_Reinforce,
        /// <summary>
        /// 装备升星
        /// </summary>
        Equip_Star,
        /// <summary>
        /// 装备洗练
        /// </summary>
        Equip_Refine,

        /// <summary>
        /// 装备魂石 镶嵌
        /// </summary>
        Equip_Inset,

        /// <summary>
        /// 技能 升级
        /// </summary>
        Skill_UpLv,
        /// <summary>
        /// 技能 进阶
        /// </summary>
        Skill_Advance,
        /// <summary>
        /// 技能 设置
        /// </summary>
        Skill_ChangeSkill,

    }

    private Dictionary<SelectOne, SelectTwo[]> SelectOneDic = new () {
        [SelectOne.Skill] = new SelectTwo[] { SelectTwo.Skill_UpLv, SelectTwo.Skill_Advance, SelectTwo.Skill_ChangeSkill },
        [SelectOne.Equip] = new SelectTwo[] { SelectTwo.Equip_Reinforce, SelectTwo.Equip_Star, SelectTwo.Equip_Inset },
    };

    private Dictionary<SelectTwo,string> TitleNameDic = new () {
        [SelectTwo.Equip_Reinforce] = "强化",
        [SelectTwo.Equip_Star] = "升星",
        [SelectTwo.Equip_Refine] = "洗练",
        [SelectTwo.Equip_Inset] = "镶嵌",
        [SelectTwo.Skill_UpLv] = "升级",
        [SelectTwo.Skill_Advance] = "进阶",
        [SelectTwo.Skill_ChangeSkill] = "设置"
    };

    private Dictionary<SelectTwo, CFG_XMLConfig.XML_Config.SwitchOpne> SelectTwoSwitchDic = new()
    {
        [SelectTwo.Equip_Reinforce] = CFG_XMLConfig.XML_Config.SwitchOpne.Build,
        [SelectTwo.Equip_Star] = CFG_XMLConfig.XML_Config.SwitchOpne.Build_EquipStar,
        [SelectTwo.Equip_Inset] = CFG_XMLConfig.XML_Config.SwitchOpne.Build_EquipInster,
        [SelectTwo.Skill_UpLv] = CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv,
        [SelectTwo.Skill_Advance] = CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillStar,
    };

    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() => { 
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIBuild>();
        });

        for (int i = 0; i < GetView().selectOneBtns.Length; i++)
        {
            SelectOne idx = (SelectOne)i;
            GetView().selectOneBtns[i]._btn.onClick.AddListener(() => {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnSelectOne(idx,true);
            });
        }

        for (int i = 0; i < GetView().selectTwoBtns.Length; i++)
        {
            int idx = i;
            GetView().selectTwoBtns[i]._btn.onClick.AddListener(() => {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnSelectTwo(SelectOneDic[selectOne][idx]);
            });
        }

        for (int i = 0; i < GetView().teamItems.Length; i++)
        {
            int idx = i;
            GetView().teamItems[i]._btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnTeamItemClick(idx);
            });
        }

    }

    private void SetBaseRole(bool isInit = false)
    {
        for (int i = 0; i < GetView().teamItems.Length; i++)
        {
            if (i == 0)
            {
                if (isInit)
                {
                    GameUtil.Instance.SetJobIcon(RoleM.Instance.Job, GetView().teamItems[i]._img_job);
                    ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.RoleHeadAtlas, "img_" + RoleM.Instance.Job + RoleM.Instance.Sex, GetView().teamItems[i]._img_head);
                }
                GetView().teamItems[i]._txt_lv.text = RoleM.Instance.Level.ToString();
            }
            else
            {
                var solt = PartnerM.Instance.GetOnePartnerSlotInfo(i);
                bool isLock = false;
                GetView().teamItems[i]._obj_lock.SetActive(isLock);

                GetView().teamItems[i]._img_head.gameObject.SetActive(!isLock && solt.PartnerUid != 0);
                GetView().teamItems[i]._img_job.gameObject.SetActive(!isLock && solt.PartnerUid != 0);
                GetView().teamItems[i]._obj_Lv.gameObject.SetActive(!isLock && solt.PartnerUid != 0);
                GetView().teamItems[i].txt_Star.gameObject.SetActive(!isLock && solt.PartnerUid != 0);
                if (solt.PartnerUid != 0)
                {
                    var partner = PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid);
                    GetView().teamItems[i]._txt_lv.text = partner.Level.ToString();
                    GetView().teamItems[i].txt_Star.text = partner.Star.ToString();

                    if (isInit)
                    {
                        GameUtil.Instance.SetJobIcon(CFG_Partner.GetById(partner.CfgId).job % 10, GetView().teamItems[i]._img_job);
                        GameUtil.Instance.SetItemIcon(CFG_Partner.GetById(partner.CfgId).ItemID, GetView().teamItems[i]._img_head);
                    }
                }
            }

        }
    }

    private void OnSelectOne(SelectOne idx,bool isBtn)
    {
        if (idx == SelectOne.ToDo)
        {
            UITool.ShowTips("敬请期待");
            return;
        }
        else if (idx == SelectOne.Equip)
        {
            if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_EquipInster, isBtn))
                return;
        }
        else if (idx == SelectOne.Skill)
        {
            if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv, isBtn))
                return;
        }
        if (SelectTwoSwitchDic.ContainsKey(SelectOneDic[idx][0]) && GameUtil.Instance.CheakIsLock(SelectTwoSwitchDic[SelectOneDic[idx][0]]))
            return;

        selectOne = idx;
        for (int i = 0; i < GetView().selectOneBtns.Length; i++)
        {
            GetView().selectOneBtns[i]._objOn.SetActive(idx == (SelectOne)i);
        }
        //设置二级按钮描述
        for (int i = 0; i < GetView().selectTwoBtns.Length; i++)
        {
            GetView().selectTwoBtns[i]._btn.gameObject.SetActive(i < SelectOneDic[selectOne].Length);
            if (i < SelectOneDic[selectOne].Length)
            {
                GetView().selectTwoBtns[i]._txtOn.text = TitleNameDic[SelectOneDic[selectOne][i]];
            }
        }

        if (isBtn)
            selectTwo = SelectOneDic[selectOne][0];

        if (idx == SelectOne.Equip)
        {
            if (_UIBuild_Equip == null)
            {
                UITool.LoadUII<UIBuild_Equip>(GetView().root_UI, (ui) =>
                {
                    _UIBuild_Equip = ui as UIBuild_Equip;
                    _UIBuild_Equip.OnShow(true);
                    OnSelectTwo(selectTwo);
                });
            }
            else
            {
                _UIBuild_Equip.OnShow(true);
                OnSelectTwo(selectTwo);
            }
        }
        else if (idx == SelectOne.Skill)
        {
            if (_UIBuild_Skill == null)
            {
                UITool.LoadUII<UIBuild_Skill>(GetView().root_UI, (ui) =>
                {
                    _UIBuild_Skill = ui as UIBuild_Skill;
                    _UIBuild_Skill.OnShow(true);
                    OnSelectTwo(selectTwo);
                });
            }
            else
            {
                _UIBuild_Skill.OnShow(true);
                OnSelectTwo(selectTwo);
            }
        }

        if (idx != SelectOne.Equip && _UIBuild_Equip != null)
            _UIBuild_Equip.OnShow(false);

        if (idx != SelectOne.Skill && _UIBuild_Skill != null)
            _UIBuild_Skill.OnShow(false);

        OnBindRed(false);
    }

    private UIBuild_Equip _UIBuild_Equip;
    private UIBuild_Skill _UIBuild_Skill;
    private void OnSelectTwo(SelectTwo idx)
    {
        if (SelectTwoSwitchDic.ContainsKey(idx) && GameUtil.Instance.CheakIsLock(SelectTwoSwitchDic[idx]))
            return;

        for (int i = 0; i < GetView().selectTwoBtns.Length; i++)
        {
            if (i < SelectOneDic[selectOne].Length)
                GetView().selectTwoBtns[i]._objOn.SetActive(idx == SelectOneDic[selectOne][i]);
        }

        selectTwo = idx;

        OnTeamItemClick(0);
        OnCheakRedTeamDot();
    }

    private void SelectBtnLockCheck()
    {

        bool isLockEquip = GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_EquipInster, false);
        bool isLockSkill = GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv, false);
        string lockEquipTip = "";
        string lockSkillTip = "";

        if (isLockEquip)
            lockEquipTip = $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Build_EquipInster)}天后开启";
        if (isLockSkill)
            lockSkillTip = $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv)}天后开启";

        SetSelectOneButtonLock(SelectOne.Equip, isLockEquip, lockEquipTip);
        SetSelectOneButtonLock(SelectOne.Skill, isLockSkill, lockSkillTip);
    }

    private void SetSelectOneButtonLock(SelectOne selectOne, bool isLock, string lockString = "")
    {
        GetView().selectOneBtns[(int)selectOne]._objMask.SetActive(isLock);
        if (isLock)
            GetView().selectOneBtns[(int)selectOne]._txtOpen.text = lockString;
    }

    public override void OnLayerChange()
    {
        for (int i = 0; i < GetView().teamItems.Length; i++)
        {
            GameUtil.Instance.SetEffectLayer(GetView().teamItems[i]._obj_On.transform, _canvasOrder + 1);
        }
    }

    private void SetPartnerLock()
    {
        for (int soltIdx = 1; soltIdx < GetView().teamItems.Length; soltIdx++)
        {
            bool isLock = CFG_XMLConfig.CheckPartnerSoltIsOpenServer(soltIdx, false);
            if (!isLock)
                isLock = CFG_XMLConfig.GetOpenSoltLv(soltIdx) > RoleM.Instance.Level;

            GetView().teamItems[soltIdx]._obj_lock.SetActive(isLock);
            if (isLock)
            {
                if (CFG_XMLConfig.CheckPartnerSoltIsOpenServer(soltIdx, false))
                    GetView().teamItems[soltIdx]._txt_Lock.text = $"{CFG_XMLConfig.RemainingDaysPartnerSlotUnlockOpen(soltIdx)}天后解锁";
                else
                    GetView().teamItems[soltIdx]._txt_Lock.text = $"{CFG_XMLConfig.GetOpenSoltLv(soltIdx)}级解锁";
            }
        }
    }

    private void OnTeamItemClick(int Idx)
    {
        if (Idx > 0 && CFG_XMLConfig.CheckPartnerSoltIsOpenServer(Idx))
        {
            UITool.ShowTips($"{CFG_XMLConfig.RemainingDaysPartnerSlotUnlockOpen(Idx)}天后开启");
            return;
        }
        if (Idx > 0 && CFG_XMLConfig.GetOpenSoltLv(Idx) > RoleM.Instance.Level)
        {
            UITool.ShowTips("角色等级不足");
            return;
        }

        if (Idx != 0 && PartnerM.Instance.GetOnePartnerSlotInfo(Idx).PartnerUid == 0)
        {
            UITool.ShowTips("请上阵伙伴");
            return;
        }

        for (int i = 0; i < GetView().teamItems.Length; i++)
        {
            GetView().teamItems[i]._obj_On.SetActive(Idx == i);
        }

        if (selectOne == SelectOne.Equip)
        {
            _UIBuild_Equip.OnClickShow(Idx, selectTwo);
        }
        else if (selectOne == SelectOne.Skill)
        {
            _UIBuild_Skill.OnSelcetUIBtnClick(selectTwo, Idx);
        }
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
        SetBaseRole(true);
        selectOne = args.Length > 0 ? (SelectOne)args[0] : SelectOne.Skill;
        selectTwo = args.Length > 1 ? (SelectTwo)args[1] : SelectTwo.Skill_UpLv;
        OnSelectOne(selectOne,false);
        SetPartnerLock();
        SelectBtnLockCheck();
        OnBindRed();
    }
    private void OnBindRed(bool isInit= true)
    {
        if (isInit)
        {

            RedPointManager.Instance.RPS.Register(RedPointConst.Skill.ToString(), (node) =>
            {
                if (isNullGo) return;
                GetView().selectOneBtns[0]._objRed.SetActive(node.pointCount > 0);
            });
            RedPointManager.Instance.RPS.Register(RedPointConst.Equip.ToString(), (node) =>
            {
                if (isNullGo) return;
                GetView().selectOneBtns[1]._objRed.SetActive(node.pointCount > 0);
            });

        }

        RedPointManager.Instance.RPS.Register(RedPointConst.Equip_Reinforce.ToString(), (node) =>
        {
            if (isNullGo || selectOne != SelectOne.Equip) return;
            OnCheakRedTeamDot();
            GetView().selectTwoBtns[0]._objRed.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Equip_Star.ToString(), (node) =>
        {
            if (isNullGo || selectOne != SelectOne.Equip) return;
            GetView().selectTwoBtns[1]._objRed.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Skill_Setting.ToString(), (node) =>
        {
            if (isNullGo || selectOne != SelectOne.Skill) return;
            GetView().selectTwoBtns[2]._objRed.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Equip_Insert.ToString(), (node) =>
        {
            if (isNullGo || selectOne != SelectOne.Equip) return;
            OnCheakRedTeamDot();
            GetView().selectTwoBtns[2]._objRed.SetActive(node.pointCount > 0 );
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Skill_UpLv.ToString(), (node) =>
        {
            if (isNullGo || selectOne != SelectOne.Skill) return;
            OnCheakRedTeamDot();
            GetView().selectTwoBtns[0]._objRed.SetActive(node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Skill_Advance.ToString(), (node) =>
        {
            if (isNullGo || selectOne != SelectOne.Skill) return;
            OnCheakRedTeamDot();
            GetView().selectTwoBtns[1]._objRed.SetActive(node.pointCount > 0);
        });
    }

    private void OnCheakRedTeamDot()
    {
        for (int i = 0; i < GetView().teamItems.Length; i++)
        {
            GetView().teamItems[i]._obj_RedDot.SetActive(RedNum(i) > 0);
        }

        int RedNum(int Idx)
        {
            if (selectTwo == SelectTwo.Equip_Reinforce)
                return RedDotM.Instance.GetOneTeamAllEquipSlotReinforceRedNum(Idx);
            else if (selectTwo == SelectTwo.Equip_Inset)
                return RedDotM.Instance.GetOneAllEquipInsetRedNum(Idx);
            else if (selectTwo == SelectTwo.Skill_UpLv)
                return RedDotM.Instance.GetOneTeamAllSkillUpLvRedNum(Idx);
            else if (selectTwo == SelectTwo.Skill_Advance)
                return RedDotM.Instance.GetOneTeamAllSkillAdvanceRedNum(Idx);
            else
                return 0;
        }
    }

    private void SetSelectBtnLock(int index, bool isOpen, string openCondition)
    {
        for (int i = 0; i < GetView().selectOneBtns.Length; i++)
        {
            if (index == i)
            {
                GetView().selectOneBtns[i]._objMask.SetActive(isOpen);
                if (isOpen)
                {
                    GetView().selectOneBtns[i]._txtOpen.text = openCondition;
                }
            }
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_CheckOpenTime(GameEvent.Refresh_ZeroTime _e)
    {
        SetPartnerLock();
        SelectBtnLockCheck();
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
        if (_UIBuild_Equip != null)
            _UIBuild_Equip.OnExit();

        if (_UIBuild_Skill != null)
            _UIBuild_Skill.OnExit();
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}