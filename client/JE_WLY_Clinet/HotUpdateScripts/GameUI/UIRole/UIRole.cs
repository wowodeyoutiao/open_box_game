using HotUpdateScripts;
using HotUpdateScripts.ProtoM;

using JEngine.Event;
using JEngine.Helper;
using JEngine.UI;

using MetaFramework.UI;

using pb;

using Spine;
using Spine.Unity;

using System.Collections.Generic;
using System.Reflection;

using UnityEngine;
using UnityEngine.UI;

public class UIRole : UIBase<UIModelRole, UIViewRole>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    public override void OnInit()
    {

        for (int i = 0; i < GetView().selectBtns.Length; i++)
        {
            int idx = i;
            GetView().selectBtns[i]._btn.onClick.AddListener(delegate 
            { 
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button); 
                OnSelectBtnClick(idx); 
            });
        }

        GetView().btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIRole>();
        });

        for (int i = 0; i < GetView().teamItems.Length; i++)
        {
            int idx = i;
            GetView().teamItems[i]._btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                if(idx > 0 && CFG_XMLConfig.CheckPartnerSoltIsOpenServer(idx))
                {
                    UITool.ShowTips($"{CFG_XMLConfig.RemainingDaysPartnerSlotUnlockOpen(idx)}天后开启");
                    return;
                }
                if (idx > 0 && CFG_XMLConfig.GetOpenSoltLv(idx) > RoleM.Instance.Level)
                {
                    UITool.ShowTips("角色等级不足");
                    return;
                }

                OnTeamItemClick(idx);
            });
        }


        GetView().btn_AllEquip.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            TcpC.C2S_EquipBest(TeamIdx == 0 ? ObjectType.PLAYER : ObjectType.PARTNER, TeamIdx);
        });

        GetView().btn_Attribute.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            SetArrtInfo(!AttrShow);
        });

        GetView().btn_PartnerSet.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
            {
                UITool.ShowTips("战斗中不可操作");
                return;
            }

            UITool.Open<UIPartnerUp>(null, TeamIdx);
        });

        GetView().btn_Pet.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Pet))
            {
                UITool.Open<UIComRule>(null, 42);
                return;
            }
            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Pet, false))
            {
                UITool.Open<UIComRule>(null, 42);
                return;
            }

            UITool.Open<UIPet>();
        });

        for (int i = 0; i < GetView().selectBtns.Length; i++)
        {
            var txt = GetView().selectBtns[i]._txtOff;
            GetView().selectBtns[i]._btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnSelectBtnClick(txt.text);
            });
        }

        for (int i = 0; i < GetView().root_Equips.childCount; i++)
        {
            int idx = i + 1;
            JUIExtensions.btn(GetView().root_Equips.Find("equip_" + idx)).onClick.AddListener(() => {
                var itemInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt((EquipSlot)idx).Item : PartnerM.Instance.GetOnePartnerOneEquip(TeamIdx, idx);
                if (itemInfo != null)
                {
                    UITool.Open<UIItemShow>(null, itemInfo.CfgId, itemInfo.Equip, TeamIdx);
                }
            });

           
        }

        GetView().btn_Recast.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            long partnerUid = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid;
            var partner = PartnerM.Instance.GetOnePartnerInfoByUid(partnerUid);

            if(partner.Level < PartnerM.recastOpenLevel)
            {
                UITool.ShowTips($"伙伴等级超过{PartnerM.recastOpenLevel}级可重铸");
                return;
            }

            OnRecastPartner();
        });

        GetView().btn_Disassembling.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnDisassemblingPartner();
        });

        GetView().btn_PreviewStarInfo.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            var partnerUid = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid;
            var partner = PartnerM.Instance.GetOnePartnerInfoByUid(partnerUid);
            UITool.Open<UIStarDetails>(null, CFG_Partner.GetById(partner.CfgId));
        });
    }

    private void OnBindRed()
    {
        RedPointManager.Instance.RPS.Register(RedPointConst.Role_Equip.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().obj_EqiuipDot.gameObject.SetActive(RedDotM.Instance.GetOneTeamBeatEquipRed(TeamIdx) > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Role_Pet.ToString(), (node) =>
        {
            if (isNullGo) return;
            if (TeamIdx == 0)
                GetView().selectBtns[2]._objDot.SetActive( node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Partner_Into.ToString(), (node) =>
        {
            if (isNullGo) return;
            GetView().btn_PartnerSet.transform.Find("dot").gameObject.SetActive(TeamIdx > 0 && RedDotM.Instance.OnePartnerIntoRedDot(TeamIdx) > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Partner_Call.ToString(), (node) =>
        {
            if (isNullGo) return;
            if (TeamIdx != 0)
                GetView().selectBtns[0]._objDot.SetActive(TeamIdx > 0 && node.pointCount > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Partner_UpStar.ToString(), (node) =>
        {
            if (isNullGo) return;
            if (TeamIdx > 0)
                GetView().selectBtns[3]._objDot.SetActive(RedDotM.Instance.GetOnePartnerUpStarRedDot(TeamIdx) > 0);
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.ExpPool.ToString(), (node) =>
        {
            if (isNullGo) return;
            if (TeamIdx == 0)
            {
                GetView().selectBtns[0]._objDot.SetActive(node.pointCount > 0);
            }
            else
            {
                GetView().selectBtns[1]._objDot.SetActive(node.pointCount > 0);
            }
        });

        RedPointManager.Instance.RPS.Register(RedPointConst.Build2.ToString(), (node) =>
        {
            if (isNullGo) return;
            if (TeamIdx == 0)
            {
                GetView().selectBtns[1]._objDot.SetActive(node.pointCount > 0);
                GetView().selectBtns[3]._objDot.SetActive(node.pointCount > 0);
            }
            else
            {
                GetView().selectBtns[2]._objDot.SetActive(node.pointCount > 0);
                GetView().selectBtns[4]._objDot.SetActive(node.pointCount > 0);
            }
        });
    }

    private string[] RoleBtns = {"升级","技能", "宠物", "打造"};
    private string[] PartnerBtns = { "召唤", "升级", "技能", "升星", "打造"};
    private string[] PartnerNoBtns = { "召唤" };

    private void OnSelectBtnClick(string btnStr)
    {
        switch (btnStr)
        {
            case "升级":
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.ExpPool))
                    return;
                UITool.Open<UIExpPool>();
                break;
            case "宠物":
                if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Pet))
                {
                    UITool.Open<UIComRule>(null, 42);
                    return;
                }
                        
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Pet, false))
                {
                    UITool.Open<UIComRule>(null, 42);
                    return;
                }
                    
                UITool.Open<UIPet>();
                break;
            case "打造":
                if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv))
                    return;
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv))
                    return;
                UITool.Open<UIBuild>();
                break;
            case "技能":
                if (TeamIdx != 0 && GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Role_SkillAndCall))
                    return;
                UITool.Open<UIRole_Skill>(null,TeamIdx);
                break;
            case "升星":
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Partner_Star))
                    return;
                UITool.Open<UIPartnerUpStar>(null, TeamIdx);
                break;
            case "召唤":
                if (TeamIdx != 0 && GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Role_SkillAndCall))
                    return;
                UITool.Open<UILoading>(null, new UILoading.Loading { _time = 0 });
                HttpC.GetPrivilegeDetail(1, (_r) =>
                {
                    UITool.Close<UILoading>();
                    UITool.Open<UIPartnerCall>(null, _r);
                }, () => { UITool.Close<UILoading>(); });
                break;
            default:
                UITool.ShowTips("暂未开放");
                break;
        }
    }


    private void SetSelectBtnState()
    {
        var btns = TeamIdx == 0 ? RoleBtns : (PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid != 0 ? PartnerBtns : PartnerNoBtns);
        for (int i = 0; i < GetView().selectBtns.Length; i++)
        {
            GetView().selectBtns[i]._btn.gameObject.SetActive(i < btns.Length);
            if (i < btns.Length)
            {
                GetView().selectBtns[i]._txtOff.text = btns[i];
            }
        }
    }

    private void SetSelectBtnLock(int index, bool isLock, string openCondition = "")
    {
        for (int i = 0; i < GetView().selectBtns.Length; i++)
        {
            if(index == i)
            {
                GetView().selectBtns[i]._objMask.SetActive(isLock);
                if (isLock)
                {
                    GetView().selectBtns[i]._txtOpen.text = openCondition;
                }
            }
        }
    }

    private void ClearSelectButtonLock()
    {
        for (int i = 0; i < GetView().selectBtns.Length; i++)
        {
            GetView().selectBtns[i]._objMask.SetActive(false);
            GetView().selectBtns[i]._txtOpen.text = "";
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

    //设置基础信息
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
                GetView().teamItems[i]._img_head.gameObject.SetActive( solt.PartnerUid != 0);
                GetView().teamItems[i]._img_job.gameObject.SetActive(solt.PartnerUid != 0);
                GetView().teamItems[i]._obj_Lv.gameObject.SetActive(solt.PartnerUid != 0);
                GetView().teamItems[i].txt_Star.gameObject.SetActive(solt.PartnerUid != 0);

                int quality = 0;
                if (solt.PartnerUid != 0)
                {
                    var partner = PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid);
                    GetView().teamItems[i]._txt_lv.text = partner.Level.ToString();
                    GetView().teamItems[i].txt_Star.text = partner.Star.ToString();

                    quality = CFG_Partner.GetById(partner.CfgId).Quality;

                    if (isInit)
                    {
                        GameUtil.Instance.SetJobIcon(CFG_Partner.GetById(partner.CfgId).job % 10, GetView().teamItems[i]._img_job);
                        GameUtil.Instance.SetItemIcon(CFG_Partner.GetById(partner.CfgId).ItemID, GetView().teamItems[i]._img_head);
                    }
                }

                GameUtil.Instance.SetPartnetQualityFrame(quality, GetView().teamItems[i].img_quality);
            }

        }
        GetView().txt_allPower.text = GameUtil.Instance.FormatPowerWithUnit(RoleM.Instance.TotalCombat);

        if (isInit)
        {
            OnTeamItemClick(TeamIdx);
        }
    }

    private void OnPetShow()
    {
        var myPet = PetM.Instance.GetOnePetByIdx(0);
        GetView().btn_Pet.transform.Find("noPet").gameObject.SetActive(myPet.CfgId == 0);
        GetView().btn_Pet.transform.Find("Pet").gameObject.SetActive(myPet.CfgId != 0);

        if (myPet.CfgId != 0)
        {
            var root = GetView().btn_Pet.transform.Find("Pet");
            string pooName = "Pet_"+ myPet.CfgId;
            _ = GameHelper.Instance.SetAsyncObj(pooName, GameConst.GetMonsterModelByID(pooName), (_mainRole) =>
            {
                if (root.childCount > 0)
                {
                    PoolHelper.Instance.RecycObj(root.GetChild(0).gameObject);
                }
                _mainRole.transform.SetParent(root);
                _mainRole.transform.localScale = Vector3.one;
                _mainRole.transform.localPosition = Vector3.zero;
                _mainRole.SetActive(true);
            });
        }
        
    }

    public override void OnLayerChange()
    {
        for (int i = 0; i < GetView().teamItems.Length; i++)
        {
            GameUtil.Instance.SetEffectLayer(GetView().teamItems[i]._obj_on.transform, _canvasOrder + 1);
        }
    }

    private int TeamIdx = -1;
    private void OnTeamItemClick(int idx)
    {
        TeamIdx = idx;
        OnLockCheak();
        OnBindRed();
        OnBaseInfo();

        for (int i = 0; i < GetView().teamItems.Length; i++)
        {
            GetView().teamItems[i]._obj_on.SetActive(i == idx);
        }

        GetView().btn_Pet.gameObject.SetActive(TeamIdx == 0);
        GetView().btn_PartnerSet.gameObject.SetActive(TeamIdx != 0);
        GetView().btn_AllEquip.gameObject.SetActive(TeamIdx == 0 || PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid != 0);

        if (TeamIdx != 0)
        {

            GetView().txt_PartnerSet.text = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid == 0 ? "上阵" : "替换";
            GetView().btn_PreviewStarInfo.gameObject.SetActive(PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid != 0);
        }
        else
        {
            OnPetShow();
            GetView().btn_PreviewStarInfo.gameObject.SetActive(false);
        }

        ShowRecastDisassembleButton();

        ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Role", TeamIdx == 0 ? "bg_juese" : "bg_huoban", GetView().img_Role);
    }

    private void ShowRecastDisassembleButton()
    {
        bool isShow = TeamIdx != 0 && PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid != 0;
        GetView().root_RecastDisassemblingBtn.gameObject.SetActive(isShow);
        if (isShow)
        {
            long partnerUid = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid;
            var partner = PartnerM.Instance.GetOnePartnerInfoByUid(partnerUid);
            GetView().btn_Recast.gameObject.SetActive(partner.Level > 1 || partner.Star > 0);
            var cfg_Partner = CFG_Partner.GetById(partner.CfgId);
            GetView().btn_Disassembling.gameObject.SetActive(partner.Star >= 5 && cfg_Partner.Quality >= 7);
        }
    }

    //装备槽位信息
    private void OnBaseInfo(bool isRefreshBase = false)
    {
        string Str = "";
        if (TeamIdx == 0)
        {
            Str = RoleM.Instance.Nickname;
        }
        else
        {
            var solt = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx);
            if (solt.PartnerUid != 0)
            {
                var cfg_Partner = CFG_Partner.GetById(PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid).CfgId);
                string name = cfg_Partner.Name;
                Str = GameUtil.Instance.GetFightStrByQuality(cfg_Partner.Quality, name);
            }
        }

        GetView().txt_RoleName.text = Str;

        for (int i = 0; i < GetView().root_Equips.childCount; i++)
        {
            int idx = i + 1;
            var go = GetView().root_Equips.Find("equip_" + idx);

            var equipInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt((EquipSlot)idx) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, idx);
            JUIExtensions.txt(go.Find("txt_Starlv")).text = equipInfo.Stars.ToString();
            go.Find("txt_Starlv").gameObject.SetActive(equipInfo.Stars > 0);
            JUIExtensions.txt(go.Find("txt_lv")).text = equipInfo.Enhance == 0 ? "" : "+" + equipInfo.Enhance;

            for (int j = 0; j < equipInfo.Gemstone.Length; j++)
            {
                go.Find("inset").GetChild(j).gameObject.SetActive(equipInfo.Gemstone[j] != 0);
                if (equipInfo.Gemstone[j] != 0)
                {
                    GameUtil.Instance.SetItemIcon(equipInfo.Gemstone[j], JUIExtensions.img(go.Find("inset").GetChild(j)));
                }
            }
        }
        if (!isRefreshBase)
            OnEquipShow();
    }

    //设置装备信息
    private void OnEquipShow(bool isRefsh = false)
    {
        string Str = "";
        string lvStr = "";
        if (TeamIdx == 0)
        {
            Str = GameUtil.Instance.FormatPowerWithUnit(RoleM.Instance.Combat);
            lvStr = RoleM.Instance.Level.ToString();

            string poolKey = "MainRole" + RoleM.Instance.Job + RoleM.Instance.Sex;
            SetRoleSpine(poolKey, GameConst.GetModelByJobAndSex((HotUpdateScripts.ProtoM.JobType)RoleM.Instance.Job, (SexType)RoleM.Instance.Sex));
        }
        else
        {
            var solt = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx);
            string poolKey = "";
            if (solt.PartnerUid != 0 && PartnerM.Instance.GetOnePartnerStats(solt.PartnerUid) != null)
            {
                Str = GameUtil.Instance.FormatPowerWithUnit(PartnerM.Instance.GetOnePartnerStats(solt.PartnerUid).Combat);
                lvStr = PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid).Level.ToString();
                poolKey = "Partner_" + PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid).CfgId;
                //poolKey = "Partner_1";
            }

            if (!isRefsh)
                SetRoleSpine(poolKey, poolKey == "" ? "" :GameConst.GetMonsterModelByID(poolKey));
        }

        GetView().txt_RolePower.text = Str;
        GetView().txt_RolePower.gameObject.SetActive(!string.IsNullOrEmpty(Str));

        GetView().txt_lv.text = lvStr;
        GetView().txt_lv.gameObject.SetActive(!string.IsNullOrEmpty(lvStr));


        for (int i = 0; i < GetView().root_Equips.childCount; i++)
        {
            int idx = i + 1;
            var go = GetView().root_Equips.Find("equip_" + idx);

            var itemInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt((EquipSlot)idx).Item : PartnerM.Instance.GetOnePartnerOneEquip(TeamIdx,idx);
            JUIExtensions.img(go.Find("icon")).enabled = itemInfo != null;
            JUIExtensions.img(go.Find("iconbg")).enabled = itemInfo != null;
            JUIExtensions.btn(go).enabled = itemInfo != null;
            if (itemInfo != null)
            {
                JUIExtensions.txt(go.Find("txt_num")).text = "";
                GameUtil.Instance.SetItemIcon(itemInfo.CfgId, JUIExtensions.img(go, "icon"), JUIExtensions.img(go, "iconbg"),null,(int)itemInfo.Equip.Quality);
            }
            else
            {
                JUIExtensions.txt(go.Find("txt_num")).text = GameM.Instance.EquipName[idx];
            }
        }
        SetArrtInfo(false);
        SetSelectBtnState();
    }

    private bool AttrShow = false;

    //设置玩家属性信息
    private void SetArrtInfo(bool isShow)
    {
        AttrShow = isShow;
        if (TeamIdx == 0)
        {
            GetView().btn_Attribute.gameObject.SetActive(true);
            (GetView().txt_attr0.text, GetView().txt_attr1.text, GetView().txt_attr2.text, GetView().txt_attr3.text) = GetRoleAttribute((isShow ? -1 :4), RoleM.Instance.GetRoleAllStats());
        }
        else
        {
            var solt = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx);
            GetView().btn_Attribute.gameObject.SetActive(solt.PartnerUid != 0);
            if (solt.PartnerUid != 0 && PartnerM.Instance.GetOnePartnerStats(solt.PartnerUid) != null)
                (GetView().txt_attr0.text, GetView().txt_attr1.text, GetView().txt_attr2.text, GetView().txt_attr3.text) = GetRoleAttribute((isShow ? -1 : 4), PartnerM.Instance.GetOnePartnerStats(solt.PartnerUid).Attributes);
        }
        GetView().img_Arrow.transform.localScale = !isShow ? Vector3.one : new Vector3(1, -1, 1);
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetView().btn_Attribute.GetComponent<RectTransform>());
    }


    private void SetRoleSpine(string poolKey,string path)
    {

        if (GetView().root_Role.childCount > 0)
        {
            PoolHelper.Instance.RecycObj(GetView().root_Role.GetChild(0).gameObject);
        }

        if (!string.IsNullOrEmpty(poolKey))
        {
            _ = GameHelper.Instance.SetAsyncObj(poolKey, path, (_mainRole) =>
            {
                _mainRole.transform.SetParent(GetView().root_Role);
                _mainRole.transform.localScale = Vector3.one;
                _mainRole.transform.localPosition = Vector3.zero;
                _mainRole.SetActive(true);

                SpineUtil.Instance.PlayQueue(_mainRole.transform.Find("spine").GetComponent<SkeletonGraphic>(), new List<string> { "stand" }, null, true);
            });
        }
       
    }


    private int SelectIdx = 0;
    private  void OnSelectBtnClick(int idx)
    {
        SelectIdx = idx;
    }

    private void OnSelectButtonClick(int index)
    {
        var txt = GetView().selectBtns[index]._txtOff;
        OnSelectBtnClick(txt.text);
    }

    private (string,string, string, string) GetRoleAttribute(int Count, List<Stats> StatsList)
    {
        int idx = 0;
        string Str0 = "";
        string Str1 = "";
        string Str2 = "";
        string Str3 = "";

        int Amount = 4;
        for (int i = 0; i < CFG_XMLConfig.xmlConfig.RolePanelList.Count; i++)
        {
            var item = CFG_XMLConfig.xmlConfig.RolePanelList[i];
            if (item.Show == 0)
                continue;

            if (idx % Amount == 0)
            {
                Str0 = $"{Str0}{item.Name}:<color=#ffffff>{CFG_XMLConfig.GetAttrValueToStr(item.Type, GetValue(item.SID))}</color>\n";
            }
            else if(idx % Amount == 1)
            {
                Str1 = $"{Str1}{item.Name}:<color=#ffffff>{CFG_XMLConfig.GetAttrValueToStr(item.Type, GetValue(item.SID))}</color>\n";
            }
            else if (idx % Amount == 2)
            {
                Str2 = $"{Str2}{item.Name}:<color=#ffffff>{CFG_XMLConfig.GetAttrValueToStr(item.Type, GetValue(item.SID))}</color>\n";
            }
            else
            {
                Str3 = $"{Str3}{item.Name}:<color=#ffffff>{CFG_XMLConfig.GetAttrValueToStr(item.Type, GetValue(item.SID))}</color>\n";
            }
            idx++;

            if (idx == Count)
                break;
        }

        long GetValue(int SId)
        {
            for (int i = 0; i < StatsList.Count; i++)
            {
                if (StatsList[i].attr == (Attribute) SId)
                    return StatsList[i].Num;
            }
            return 0;
        }
        
        return (Str0, Str1, Str2, Str3);
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);

        if (args.Length > 0)
            TeamIdx = (int)args[0];
        else
            TeamIdx = 0;

        SetPartnerLock();
        SetBaseRole(true);

        CheckStartGuide();
    }

    private void OnLockCheak()
    {
        ClearSelectButtonLock();
        if (TeamIdx == 0)
        {
            if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv, false))
            {
                SetSelectBtnLock(3, true, $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv)}天后开启");
            }
            else
            {
                int skillLockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv];
                SetSelectBtnLock(3, RoleM.Instance.Level < skillLockLv, skillLockLv + "级解锁");
            }

            bool isLockOpenDayPet = GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Pet, false);
            if (isLockOpenDayPet)
            {
                GetView().obj_PetLock.SetActive(true);
                GetView().txt_petLock.text = $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Pet)}天后开启";
                SetSelectBtnLock(2, true, $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Pet)}天后开启");
            }
            else
            {
                int lockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Pet];
                GetView().obj_PetLock.SetActive(RoleM.Instance.Level < lockLv);
                GetView().txt_petLock.text = lockLv + "级解锁";
                SetSelectBtnLock(2, RoleM.Instance.Level < lockLv, lockLv + "级解锁");
            }
        }
        else
        {
            if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv, false))
            {
                SetSelectBtnLock(4, true, $"{GameUtil.Instance.RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv)}天后开启");
            }
            else
            {
                int skillLockLv = CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv];
                SetSelectBtnLock(4, RoleM.Instance.Level < skillLockLv, skillLockLv + "级解锁");
            }
        }

    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_LvUp(GameEvent.Refresh_LvUp _e)
    {
        if (isNullGo)
            return;
        OnLockCheak();
    }

    private void OnRecastPartner()
    {
        long partnerUid = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid;
        UITool.Open<UIPartnetRecastDisassembling>(null, partnerUid, TeamIdx, UIPartnetRecastDisassembling.PartnerOptionType.Recast);
    }

    private void OnDisassemblingPartner()
    {
        long partnerUid = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid;
        UITool.Open<UIPartnetRecastDisassembling>(null, partnerUid, TeamIdx, UIPartnetRecastDisassembling.PartnerOptionType.Disassembling);
    }

    /// <summary>
    /// 伙伴槽位点击引导
    /// </summary>
    private void CheckStartGuide()
    {
        if (RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner))
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetView().root_Team as RectTransform);
            var guide = new UIGuide.GuideData
            {
                ID = RoleM.Instance.GuideId,
                ButtonPos = JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().teamItems[1]._btn.transform),
                callBack = () =>
                {
                    OnTeamItemClick(1);
                    CheckPartnerGetGuide();
                },
            };
            RoleM.Instance.SetGuide(guide);
        }
    }

    /// <summary>
    /// 伙伴召唤引导
    /// </summary>
    private void CheckPartnerGetGuide()
    {
        if (RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner))
        {
            var guide = new UIGuide.GuideData
            {
                ID = RoleM.Instance.GuideId,
                ButtonPos = JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().selectBtns[0]._btn.transform),
                callBack = () =>
                {
                    OnSelectButtonClick(0);
                },
            };
            RoleM.Instance.SetGuide(guide);
        }
    }

    /// <summary>
    /// 伙伴上阵引导
    /// </summary>
    public void CheckPartnerUpGuide()
    {
        if (RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner))
        {
            var guide = new UIGuide.GuideData
            {
                ID = RoleM.Instance.GuideId,
                ButtonPos = JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().btn_PartnerSet.transform),
                callBack = () =>
                {
                    UITool.Open<UIPartnerUp>(null, TeamIdx);
                },
            };
            RoleM.Instance.SetGuide(guide);
        }
    }

    public void CheckClosePanelGuide()
    {
        if (RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner))
        {
            var guide = new UIGuide.GuideData
            {
                ID = RoleM.Instance.GuideId,
                ButtonPos = JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().btn_Close.transform),
                callBack = () =>
                {
                    UITool.Get<UIHall>().CheckAutoOpenBoxGuide();
                    UITool.Close<UIRole>();
                },
            };
            RoleM.Instance.SetGuide(guide);
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserBase(GameEvent.Sync_UserBase _e)
    {
        SetPartnerLock();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserStatsPush(GameEvent.Sync_UserStatsPush _e)
    {
        if (isNullGo)return;

        SetBaseRole(false);
        OnEquipShow(true);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_PartnerSlot(GameEvent.Refresh_PartnerSlot _e)
    {
        if (isNullGo) return;
        SetBaseRole(true);
        OnBindRed();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_PartnerList(GameEvent.Refresh_PartnerList _e)
    {
        if (isNullGo && _e.isOne && TeamIdx != 0) return;
            SetBaseRole(true);
    }

    //宠物刷新
    [Subscriber(ThreadMode.Main)]
    private void Refresh_PetsInfo(GameEvent.Refresh_PetsInfo _e)
    {
        if (isNullGo && TeamIdx != 0) return;
        OnPetShow();
        OnBindRed();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_CommonResponse(GameEvent.CommonResponse _e)
    {
        if (isNullGo) return;

        if (_e.code == 0 && _e.cid == CmdId.C2S_UpgradeLevel)
        {
            SetBaseRole(true); 
            OnBindRed();
        }

        // 伙伴重铸
        if (_e.code == 0 && _e.cid == CmdId.C2S_PartnerReforge)
        {
            SetBaseRole(true);
        }

        // 伙伴分解
        if (_e.code == 0 && _e.cid == CmdId.C2S_PartnerDisassemble)
        {
            SetBaseRole(true);
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_CheckOpenTime(GameEvent.Refresh_ZeroTime _e)
    {
        if (isNullGo) return;
        SetPartnerLock();
        OnLockCheak();
    }

    //槽位魂石镶嵌
    [Subscriber(ThreadMode.Main)]
    private void EquipInsetRefresh(GameEvent.EquipInsetRefresh _e)
    {
        if (isNullGo) return;
        OnBaseInfo(true);
    }

    [Subscriber(ThreadMode.Main)]
    //槽位强化
    private void Replace_EquipEnhance(GameEvent.Replace_EquipEnhance _e)
    {
        if (isNullGo) return;
        OnBaseInfo(true);
    }

    [Subscriber(ThreadMode.Main)]
    //槽位升星
    private void Replace_EquipStar(GameEvent.Replace_EquipStar _e)
    {
        if (isNullGo) return;
        OnBaseInfo(true);
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}