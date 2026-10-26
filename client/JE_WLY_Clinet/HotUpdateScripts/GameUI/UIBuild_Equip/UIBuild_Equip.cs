using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Helper;
using JEngine.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Event;
using pb;
using JEngine.MyNet;
using JEngine.Core;
using Spine;
using Unity.Jobs;

public class UIBuild_Equip : UIBase<UIModelBuild_Equip, UIViewBuild_Equip>
{
   
    //资源ID
    private enum ResID 
    {
        Coin = 1,
        DianQuan = 5,
        QHS = 21,
        SXS = 22,
    }

    //洗练类型
    private enum RefineType 
    {
        Random,
        Select,
    }

    public class BuyInfo 
    {
        public int buyId;
        public int num;
    }

    private BuyInfo _BuyInfo = new BuyInfo();


    private int AllEquipCount = 12;

    public override UILayer GetLayer()
    {
        return UILayer.NoLayer;
    }
    public override void OnInit()
    {

        GetView().btn_Help.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnHelpBtnClick();
        });

        //装备选择
        for (int i = 1; i <= AllEquipCount; i++)
        {
            GetView().root_allEquip.Find("item_" + i + "/txt_Name").GetComponent<Text>().text = GameM.Instance.EquipName[i];
            EquipSlot eSlot = (EquipSlot)i;
            GetView().root_allEquip.Find("item_" + i).GetComponent<Button>().onClick.AddListener(()=> {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnSelectEquipClick(eSlot);
            });
        }

        for (int i = 0; i < GetView().usedItems.Length; i++)
        {
            int idx = i;
            GetView().usedItems[i].btn.onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                UITool.Close<UIBuild>();
                UITool.Open<UIShop>(null, (idx == GetView().usedItems.Length -1 ? CFG_Store.StoreType.RMB: CFG_Store.StoreType.Coupon));
            });
        }

        GetView().btn_Reinforce.onClick.AddListener(()=>{
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //强化大师
            UITool.Open<UIEquipMaster>(null, CFG_EquipMaster.CategoryClass.Reinforce,TeamIdx);
        });

        GetView().btn_AddReinforceOne.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //强化一次
            OnAddRefinforceClick(false);
        });

        GetView().btn_AddReinforceTen.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //强化到顶
            EquipEnhanceTimes++;
            OnAddRefinforceClick(true);
        });

        GetView().btn_AddReinforceEnd.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //强化停止
            OnStopAutoRefinforce();
        });

        GetView().btn_Star.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //升星大师
            UITool.Open<UIEquipMaster>(null, CFG_EquipMaster.CategoryClass.UpStar, TeamIdx);
        });

        GetView().btn_AddStarOne.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //升星一次
            OnAddStarClick(false);
        });

        GetView().btn_AddStarTop.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //自动升星
            OnAddStarClick(true);
        });

        GetView().btn_EndStar.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //升星停止
            OnStopAutoUpStar();
        });

        GetView().btn_EndStar.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //升星停止
        });

        GetView().dropDown_Star.onValueChanged.AddListener(SetUpStar);


        //洗练
        GetView().btn_Refine.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (GameUtil.Instance.ComCoinToCheck(_BuyInfo.buyId, _BuyInfo.num, () => { UITool.Close<UIBuild_Equip>(); }))
            {
                TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_EquipRefine, MType.Request, TCPClientMgr.ProtoSerialize(_SendRefine));
            }
        });

        //洗练l类型
        GetView().tog_Refine1.onValueChanged.AddListener((isOn) =>
        {
            if (isOn)
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnRefineRandomOrDirect(RefineType.Random);
            }
        });

        //洗练l类型
        GetView().tog_Refine2.onValueChanged.AddListener((isOn) =>
        {
            if (isOn)
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnRefineRandomOrDirect(RefineType.Select);
            }
        });

        GetView().btn_RefineNo.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //洗练放弃
            OnRefineEquipRefine(false);
        });

        GetView().btn_RefineYes.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //洗练选择
            OnRefineEquipRefine(true);
        });

        //魂石 镶嵌

        GetView().btn_AutoInset.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //一键镶嵌
            TcpC.SetAllGemstoneChange(_SelectEquipSlot,TeamIdx);
        });

        GetView().btn_InsetNatrue.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //自然大师
            UITool.Open<UIEqipInsetRule>(null, UIEqipInsetRule.UISelect.NatureManster, _SelectEquipSlot,TeamIdx);
        });

        GetView().btn_InsetInfo.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //魂石大师
            UITool.Open<UIEqipInsetRule>(null, UIEqipInsetRule.UISelect.InsetMonster, _SelectEquipSlot, TeamIdx);
        });

        for (int i = 0; i < 4; i++)
        {
            int idex = i;
            JUIExtensions.btn(GetView().root_Inset.GetChild(i)).onClick.AddListener(() => {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnInsetOnClick(idex,true);
            });

            JUIExtensions.btn(GetView().root_Inset.GetChild(i).Find("icon/del")).onClick.AddListener(() => {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnInsetOnClick(idex,false);
            });
        }

    }

    //装备基础信息
    private void OnEquipUIBase()
    {
        for (int i = 1; i <= AllEquipCount; i++)
        {
            EquipSlot eSlot = (EquipSlot)i;
            var go = GetView().root_allEquip.Find("item_" + i);

            var itemInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(eSlot).Item : PartnerM.Instance.GetOnePartnerOneEquip(TeamIdx, i);
            bool isNullEquip = itemInfo == null;
            go.Find("icon").gameObject.SetActive(!isNullEquip);
            go.Find("txt_Name").gameObject.SetActive(isNullEquip);

            if (!isNullEquip)
            {
                GameUtil.Instance.SetItemIcon(itemInfo.CfgId, JUIExtensions.img(go.Find("icon")), JUIExtensions.img(go),null, (int)itemInfo.Equip.Quality);
            }
            else
            {
                GameUtil.Instance.SetItemBg(0, JUIExtensions.img(go));
            }
        }

        OnEquipLvUIRefresh();
    }


    //等级 级刷新
    private void OnEquipLvUIRefresh()
    {
        for (int i = 1; i <= AllEquipCount; i++)
        {
            var go = GetView().root_allEquip.Find("item_" + i);
            
            var EquipInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt((EquipSlot)i) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, i);
            JUIExtensions.txt(go.Find("txt_Reinforce")).text = "+"+ EquipInfo.Enhance;
            JUIExtensions.txt(go.Find("txt_Star")).text = EquipInfo.Stars + "";
            
            for (int j = 0; j < EquipInfo.Gemstone.Length; j++)
            {
                go.Find("inset").GetChild(j).gameObject.SetActive((int)EquipInfo.Gemstone[j] != 0);
                if ((int)EquipInfo.Gemstone[j] != 0)
                {
                    GameUtil.Instance.SetItemIcon((int)EquipInfo.Gemstone[j], JUIExtensions.img(go.Find("inset").GetChild(j)));
                }
            }
        }
    }

    // 装备选择按钮红点
    private void OnEquipSoltBtnRedCheak()
    {
        for (int i = 1; i <= AllEquipCount; i++)
        {
            GetView().root_allEquip.Find("item_" + i).Find("dot").gameObject.SetActive(RedNum(i) > 0);
        }

        int RedNum(int Idx)
        {
            if (_SelectEquipClass == UIBuild.SelectTwo.Equip_Reinforce)
                return RedDotM.Instance.GetOneEquipSlotReinforceRedNum((EquipSlot)Idx,TeamIdx);
            else if (_SelectEquipClass == UIBuild.SelectTwo.Equip_Inset)
                return RedDotM.Instance.GetOneEquipInsetRedNum(Idx, TeamIdx);
            else
                return 0;
        }
    }

    private EquipSlot _SelectEquipSlot;
    //装备选择
    private void OnSelectEquipClick(EquipSlot eSlot)
    {
        _SelectEquipSlot = eSlot;

        for (int i = 1; i <= AllEquipCount; i++)
        {
            GetView().root_allEquip.Find("item_" + i).Find("select").gameObject.SetActive(i == (int)_SelectEquipSlot);
        }


        switch (_SelectEquipClass)
        {
            case UIBuild.SelectTwo.Equip_Reinforce:
                OnReinforceShow();
                break;
            case UIBuild.SelectTwo.Equip_Star:
                OnUpStarShow();

                if (RedPointManager.Instance.GetRedNum(RedPointConst.Equip_Star.ToString()) != 0)
                {
                    RedPointManager.Instance.SetRedClose(RedPointConst.Equip_Star.ToString(), () => {
                        RedPointManager.Instance.RPS.Dispatch(RedPointConst.Equip_Star);
                    });
                }
              
                break;
            case UIBuild.SelectTwo.Equip_Refine:
                OnRefineShow();
                break;
            case UIBuild.SelectTwo.Equip_Inset:
                OnInSetUI();
                break;
            default:
                break;
        }
    }

    #region 装备强化

    /// <summary>
    /// 强化
    /// </summary>
    private void OnReinforceShow()
    {
        bool isNullEquip = TeamIdx == 0 ? RoleM.Instance.CheckIsNullEquip(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquip(TeamIdx,(int)_SelectEquipSlot) == null;
        var _SoltInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot);

        GetView().img_ItemReinforce.enabled = !isNullEquip;
        if (isNullEquip)
        {
            GameUtil.Instance.SetItemBg(0, GetView().img_ItemBgReinforce);
            GetView().txt_ReinforceItemName.text = GameM.Instance.EquipName[(int)_SelectEquipSlot] + "·槽位";
            JUIExtensions.txt(GetView().img_ItemBgReinforce.transform, "txt_soltName").text = GameM.Instance.EquipName[(int)_SelectEquipSlot];
        }
        else 
        {
            GameUtil.Instance.SetItemIcon(_SoltInfo.Item.CfgId, GetView().img_ItemReinforce, GetView().img_ItemBgReinforce,null, (int)_SoltInfo.Item.Equip.Quality);
            GetView().txt_ReinforceItemName.text = GameUtil.Instance.GetItemName(_SoltInfo.Item.CfgId);
            JUIExtensions.txt(GetView().img_ItemBgReinforce.transform, "txt_soltName").text = "";
        }

        GetView().txt_ReinforceLv.text = "+"+ _SoltInfo.Enhance;
        GetView().txt_StarReinforce.text = _SoltInfo.Stars + "";

        GetView().txt_ReinforceNowLv.text = _SoltInfo.Enhance.ToString();
        int Quali = isNullEquip ? 1 : (int)CFG_Item.GetById(_SoltInfo.Item.CfgId).Quality;

        int job = TeamIdx == 0 ? RoleM.Instance.Job : CFG_Partner.GetById(_PartnerInfo.CfgId).job % 10;
        //int job = RoleM.Instance.Job;
        var _cfgInfo = CFG_EquipStrengthen.GetByJobAndLv(job, _SoltInfo.Enhance);
        var Solt = CFG_EquipStrengthen.GetAttrByJobAndLvSolt(job, _SoltInfo.Enhance,(int)_SelectEquipSlot);
        GetView().txt_ReinforceNowDes.text = _SoltInfo.Enhance == 0 ? "无": CFG_XMLConfig.GetAttributeStrByID(Solt.attribute, (int)(CFG_XMLConfig.xmlConfig.EquipEnhanceDic[Quali] * Solt.value));

       
        GameUtil.Instance.SetItemIcon(_cfgInfo.Consume[0].id, GetView().img_ReinforceGold);
        GetView().txt_ReinforceGoldNum.text = GameUtil.Instance.SetAndCheckStr(GameUtil.Instance.FormatNumWithUnit(_cfgInfo.Consume[0].num), _cfgInfo.Consume[0].id, _cfgInfo.Consume[0].num) ;
        
        GameUtil.Instance.SetItemIcon(_cfgInfo.Consume[1].id, GetView().img_ReinforceQHS);
        GetView().txt_ReinforceQHSNum.text = GameUtil.Instance.SetAndCheckStr(GameUtil.Instance.FormatNumWithUnit(_cfgInfo.Consume[1].num), _cfgInfo.Consume[1].id, _cfgInfo.Consume[1].num);
        
        int nextLv = CFG_EquipStrengthen.JobStLvDic[job].Count - 1 == _SoltInfo.Enhance ? _SoltInfo.Enhance :_SoltInfo.Enhance + 1;
        GetView().txt_ReinforceNextLv.text = nextLv.ToString();
        var nextSolt = CFG_EquipStrengthen.GetAttrByJobAndLvSolt(job, nextLv, (int)_SelectEquipSlot);


        GetView().txt_ReinforceNextDes.text = CFG_XMLConfig.GetAttributeStrByID(nextSolt.attribute, (int)(CFG_XMLConfig.xmlConfig.EquipEnhanceDic[Quali] * nextSolt.value));

        int canMaxlv = TeamIdx == 0 ? RoleM.Instance.Level : _PartnerInfo.Level;
        canMaxlv = canMaxlv * CFG_XMLConfig.xmlConfig.BaseValue.equipStrenghthen;

        bool isCanEnhance = _SoltInfo.Enhance < canMaxlv  && nextLv != _SoltInfo.Enhance;

        bool isRedDot = true;

        for (int i = 0; i < _cfgInfo.Consume.Length; i++)
        {
            if (!GameUtil.Instance.ComCoinToCheck(_cfgInfo.Consume[i].id, _cfgInfo.Consume[i].num, null, false))
            {
                isRedDot = false;
                break;
            }
        }
        GetView().btn_AddReinforceOne.transform.Find("dot").gameObject.SetActive(isRedDot);
        GetView().btn_AddReinforceTen.transform.Find("dot").gameObject.SetActive(isRedDot);
        if (!isAutoReinforce)
        {
            GetView().btn_AddReinforceOne.gameObject.SetActive(isCanEnhance);
            GetView().btn_AddReinforceTen.gameObject.SetActive(isCanEnhance);
            GetView().Obj_AddReinforceEnd.SetActive(false);
        }
        GetView().txt_ReinforceTip.text = nextLv == _SoltInfo.Enhance ? "已满级" : (_SoltInfo.Enhance < canMaxlv  ? "":
            "需" + (TeamIdx == 0 ? "主角" : "当前伙伴") + "提升至"
        + nextLv / CFG_XMLConfig.xmlConfig.BaseValue.equipStrenghthen + "级");

    }

    //自动强化（10次）
    private bool isAutoReinforce = false;
    private void OnAddRefinforceClick(bool isAuto)
    {
        isAutoReinforce = isAuto;


        var _SoltInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot);
        int job = TeamIdx == 0 ? RoleM.Instance.Job : CFG_Partner.GetById(_PartnerInfo.CfgId).job % 10;

        var _cfgInfo = CFG_EquipStrengthen.GetByJobAndLv(job, _SoltInfo.Enhance);
        //检测货币
        for (int i = 0; i < _cfgInfo.Consume.Length; i++)
        {
            if (!GameUtil.Instance.ComCoinToCheck(_cfgInfo.Consume[i].id, _cfgInfo.Consume[i].num, () => { UITool.Close<UIBuild>(); }))
            {
                if (isAutoReinforce)
                {
                    GetView().btn_AddReinforceOne.gameObject.SetActive(true);
                    GetView().btn_AddReinforceTen.gameObject.SetActive(true);
                    GetView().Obj_AddReinforceEnd.SetActive(false);
                    isAutoReinforce = false;
                }
                return;
            }
        }

        if (isAutoReinforce)
        {
            GetView().btn_AddReinforceOne.gameObject.SetActive(false);
            GetView().btn_AddReinforceTen.gameObject.SetActive(false);
            GetView().Obj_AddReinforceEnd.SetActive(true);
        }

        TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_Enhance, MType.Request, TCPClientMgr.ProtoSerialize(new EnhanceRequest {  Slot = _SelectEquipSlot,PartnerSlotId = TeamIdx}));

    }

    //终止强化
    private void OnStopAutoRefinforce()
    {
        if (TimeItemRefinforece != null)
        {
            GameHelper.Instance.RemoveInvoke(TimeItemRefinforece);
            TimeItemRefinforece = null;
        }
        isAutoReinforce = false;
        OnReinforceShow();
    }

    private int GetNextSolt(int Solt)
    {
        int nextSolt = Solt + 1;
        if (nextSolt > AllEquipCount)
            nextSolt = 1;

        int Enhance = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt((EquipSlot)nextSolt).Enhance : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, nextSolt).Enhance;
        int canMaxlv = TeamIdx == 0 ? RoleM.Instance.Level : _PartnerInfo.Level;
        if (Enhance >= CFG_XMLConfig.xmlConfig.BaseValue.equipStrenghthen * canMaxlv)
        {
            nextSolt = GetNextSolt(nextSolt);
        }
        return nextSolt;
    }

    private TimeItem TimeItemRefinforece = null;
    private int EquipEnhanceTimes;

    [Subscriber(ThreadMode.Main)]
    private void OnRefinforeceCallBack(GameEvent.Replace_EquipEnhance _e)
    {
        OnEquipLvUIRefresh();
        if (isAutoReinforce)
        {
            //自动强化判定
            bool isAllTarget = true;
            int lv = TeamIdx == 0 ? RoleM.Instance.Level : _PartnerInfo.Level;

            //for (int i = 1; i <= AllEquipCount; i++)
            //{
            //    int Enhance = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt((EquipSlot)i).Enhance : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, i).Enhance;
            //    if (Enhance < lv * CFG_XMLConfig.xmlConfig.BaseValue.equipStrenghthen)
            //    {
            //        isAllTarget = false;
            //        break;
            //    }
            //}
            //展示当前强化面板
            // OnReinforceShow();

            int Enhance = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot).Enhance : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot).Enhance;
            if (Enhance < lv * CFG_XMLConfig.xmlConfig.BaseValue.equipStrenghthen)
            {
                isAllTarget = false;
            }

            if (!isAllTarget && EquipEnhanceTimes < 10)
            {
                //_SelectEquipSlot = (EquipSlot)GetNextSolt((int)_SelectEquipSlot);
                //展示下一个强化界面
                OnSelectEquipClick(_SelectEquipSlot);

                TimeItemRefinforece = GameHelper.Instance.AddInvoke(new TimeItem(0.2f, delegate
                {
                    OnClearTime();
                    //点击下一个强化按钮
                    EquipEnhanceTimes++;
                    OnAddRefinforceClick(isAutoReinforce);
                }));
            }
            else
            {
                EquipEnhanceTimes = 0;
                OnStopAutoRefinforce();
            }
        }
        else
        {
            OnSelectEquipClick(_SelectEquipSlot);
        }

    }
#endregion

    private bool IsAutoUpStar = false;
    //升星
    private void OnUpStarShow()
    {
        bool isNullEquip = TeamIdx == 0 ? RoleM.Instance.CheckIsNullEquip(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquip(TeamIdx, (int)_SelectEquipSlot) == null;
        var _SoltInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot);

        GetView().img_Star_Item.enabled = !isNullEquip;
        if (isNullEquip)
        {
            GameUtil.Instance.SetItemBg(0, GetView().img_Star_ItemBg);
            GetView().txt_Star_ItemName.text = GameM.Instance.EquipName[(int)_SelectEquipSlot] + "·槽位";
            JUIExtensions.txt(GetView().img_Star_ItemBg.transform, "txt_soltName").text = GameM.Instance.EquipName[(int)_SelectEquipSlot];
        }
        else
        {
            GameUtil.Instance.SetItemIcon(_SoltInfo.Item.CfgId, GetView().img_Star_Item, GetView().img_Star_ItemBg,null,(int) _SoltInfo.Item.Equip.Quality);
            GetView().txt_Star_ItemName.text = GameUtil.Instance.GetItemName(_SoltInfo.Item.CfgId);
            JUIExtensions.txt(GetView().img_Star_ItemBg.transform, "txt_soltName").text = "";
        }

        GetView().txt_Star_ReinforceLv.text = "+" + _SoltInfo.Enhance;
        GetView().txt_Star_StarLv.text = _SoltInfo.Stars + "";

        GetView().txt_Star_NowLv.text = _SoltInfo.Stars + "";

        int job = TeamIdx == 0 ? RoleM.Instance.Job : CFG_Partner.GetById(_PartnerInfo.CfgId).job % 10;
        //int job = RoleM.Instance.Job;
        var _cfgInfo = CFG_EquipStar.GetByJobAndLv(job, _SoltInfo.Stars);

        string SuccessRateStr = "成功率" + (_cfgInfo.SuccessRate / 100) + "%";

        if (RoleM.Instance.GetRoleAttrValueByAttr(pb.Attribute.EQUIP_STAR_PERCENTAGE) > 0)
            SuccessRateStr += "(升星符+" + (RoleM.Instance.GetRoleAttrValueByAttr(pb.Attribute.EQUIP_STAR_PERCENTAGE) / 10000f * (_cfgInfo.SuccessRate / 100)) + "%)";

        GetView().txt_Star_Des.text = SuccessRateStr;

        var Solt = CFG_EquipStar.GetAttrByJobAndLvSolt(job, _SoltInfo.Stars, (int)_SelectEquipSlot) ;
        GetView().txt_Star_NowDes.text = _SoltInfo.Stars == 0 ? "无" : CFG_XMLConfig.GetAttributeStrByID(Solt.attribute, Solt.value);

        GameUtil.Instance.SetItemIcon(_cfgInfo.MaterialID[0].id, GetView().img_Star_GoldNum);
        GetView().txt_Star_GoldNum.text = GameUtil.Instance.SetAndCheckStr(GameUtil.Instance.FormatNumWithUnit(_cfgInfo.MaterialID[0].num), _cfgInfo.MaterialID[0].id, _cfgInfo.MaterialID[0].num);
        
        GameUtil.Instance.SetItemIcon(_cfgInfo.MaterialID[1].id, GetView().img_Star_SXSNum);
        GetView().txt_Star_SXSNum.text = GameUtil.Instance.SetAndCheckStr(GameUtil.Instance.FormatNumWithUnit(_cfgInfo.MaterialID[1].num), _cfgInfo.MaterialID[1].id, _cfgInfo.MaterialID[1].num);

        int nextLv = _SoltInfo.Stars == CFG_EquipStar.GetMaxStar(job) ? _SoltInfo.Stars : _SoltInfo.Stars + 1;
        GetView().txt_Star_NextLv.text = nextLv + "";
        Solt = CFG_EquipStar.GetAttrByJobAndLvSolt(job, nextLv, (int)_SelectEquipSlot);
        GetView().txt_Star_NextDes.text = CFG_XMLConfig.GetAttributeStrByID(Solt.attribute, Solt.value);

        bool isCanUpStar = _SoltInfo.Stars < CFG_EquipStar.GetMaxStar(job);
        GetView().obj_StarTip.SetActive(!isCanUpStar);

        if (!IsAutoUpStar)
        {
            GetView().btn_AddStarOne.gameObject.SetActive(isCanUpStar);
            GetView().btn_AddStarTop.gameObject.SetActive(isCanUpStar);

            if (isCanUpStar)
            {
                List<string> _dropDown = new List<string>();
                for (int i = _SoltInfo.Stars + 1; i <= CFG_EquipStar.GetMaxStar(job); i++)
                {
                    _dropDown.Add(i + "星");
                }
                GetView().dropDown_Star.options.Clear();
                GetView().dropDown_Star.AddOptions(_dropDown);
                GetView().dropDown_Star.value = 0;
                TargetStar = _SoltInfo.Stars + 1;
            }

            GetView().dropDown_Star.gameObject.SetActive(isCanUpStar);
            GetView().obj_EndStar.SetActive(false);
        }
    }

    private int TargetStar;
    //选择目标升星
    private void SetUpStar(int idx)
    {
        var _SoltInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot);
        TargetStar = idx + 1 + _SoltInfo.Stars;
    }

    //升星选择点击
    private void OnAddStarClick(bool isAuto)
    {
        IsAutoUpStar = isAuto;

        var _SoltInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot);
        int job = TeamIdx == 0 ? RoleM.Instance.Job : CFG_Partner.GetById(_PartnerInfo.CfgId).job % 10;
        var _cfgInfo = CFG_EquipStar.GetByJobAndLv(job, _SoltInfo.Stars);
        //检测货币
        for (int i = 0; i < _cfgInfo.MaterialID.Length; i++)
        {
            if (!GameUtil.Instance.ComCoinToCheck(_cfgInfo.MaterialID[i].id, _cfgInfo.MaterialID[i].num, () => { UITool.Close<UIBuild>(); }))
            {
                if (IsAutoUpStar)
                {
                    GetView().obj_EndStar.SetActive(false);
                    GetView().btn_AddStarOne.gameObject.SetActive(true);
                    GetView().btn_AddStarTop.gameObject.SetActive(true);
                    IsAutoUpStar = false;
                }
                return;
            }
        }

        if (IsAutoUpStar)
        {
            GetView().obj_EndStar.SetActive(true);
            GetView().btn_AddStarOne.gameObject.SetActive(false);
            GetView().btn_AddStarTop.gameObject.SetActive(false);
        }

        TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_Stars, MType.Request, TCPClientMgr.ProtoSerialize(new StarsRequest { Slot = _SelectEquipSlot,PartnerSlotId = TeamIdx }));

    }

    private void OnClearTime()
    {
        if (TimeItemRefinforece != null)
        {
            GameHelper.Instance.RemoveInvoke(TimeItemRefinforece);
            TimeItemRefinforece = null;
        }
    }

    private void OnStopAutoUpStar()
    {
        OnClearTime();
        IsAutoUpStar = false;
        OnUpStarShow();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnStarCallBack(GameEvent.Replace_EquipStar _e)
    {
        OnEquipLvUIRefresh();
        if (IsAutoUpStar)
        {
            //自动升星判定
            var _SoltInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot);
            bool isCanUpStar = _SoltInfo.Stars < TargetStar;
            //展示当前升星面板
            OnUpStarShow();

            if (isCanUpStar)
            {
                TimeItemRefinforece = GameHelper.Instance.AddInvoke(new TimeItem ( 0.25f, delegate
                {
                    OnClearTime();
                    //点击下一个强化按钮
                    OnAddStarClick(IsAutoUpStar);
                }));
            }
            else
            {
                OnStopAutoUpStar();
            }
        }
        else
        {
            OnUpStarShow();
        }

    }

    #region 装备洗练
    /// <summary>
    /// 洗练
    /// </summary>
    private void OnRefineShow()
    {
        bool isNullEquip = !RoleM.Instance.CheckIsBestEquip(_SelectEquipSlot);
        var _SoltInfo = RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot);

        GetView().img_Refine_Icon.enabled = !isNullEquip;
        GetView().img_Refine_Item.enabled = !isNullEquip;
        GetView().txt_Target.text = "";
        if (isNullEquip)
        {
            GameUtil.Instance.SetItemBg(0, GetView().img_Refine_ItemBg);
            GetView().txt_Refine_ItemName.text = "";
            GetView().txt_RefineNum.text = "";
            for (int i = 1; i <= 4; i++)
            {
                GetView().root_RefineArrt.Find("Toggle_" + i).gameObject.SetActive(false);
            }

            GetView().obj_RefineEnd.SetActive(false);
            GetView().btn_Refine.gameObject.SetActive(false);
            GetView().txt_RefineTip.gameObject.SetActive(true);

            GetView().txt_Refine_Reinfoce.text = "";
            GetView().txt_Refine_Star.gameObject.SetActive(false);
        }
        else
        {
            GetView().txt_Refine_Reinfoce.text = "+" + _SoltInfo.Enhance;
            GetView().txt_Refine_Star.text = _SoltInfo.Stars.ToString();
            GetView().txt_Refine_Star.gameObject.SetActive(true);

            GameUtil.Instance.SetItemIcon(_SoltInfo.Item.CfgId, GetView().img_Refine_Item, GetView().img_Refine_ItemBg);
            GetView().txt_Refine_ItemName.text = GameUtil.Instance.GetItemName(_SoltInfo.Item.CfgId);

            GetView().obj_RefineEnd.SetActive(false);
            GetView().btn_Refine.gameObject.SetActive(true);
            GetView().txt_RefineTip.gameObject.SetActive(false);

            Stats baseStat = null;
            for (int i = 1; i <= 4; i++)
            {
                int idx = i;
                GetView().root_RefineArrt.Find("Toggle_" + i).gameObject.SetActive(_SoltInfo.Item.Equip != null && i <= _SoltInfo.Item.Equip.Attributes.Count  );
                if (_SoltInfo.Item.Equip != null &&  i <= _SoltInfo.Item.Equip.Attributes.Count)
                {
                    var arrt = _SoltInfo.Item.Equip.Attributes[i-1];
                    int Qulit = CFG_RandomDistrib.GetColor(CFG_Item.GetQulite(_SoltInfo.Item.CfgId),(int)arrt.attr, arrt.Num);
                    int maxValue = CFG_RandomDistrib.GetMaxValue(CFG_Item.GetQulite(_SoltInfo.Item.CfgId), (int)arrt.attr);
                    string arrtStr = maxValue == arrt.Num ? CFG_XMLConfig.GetAttributeStrByID((int)arrt.attr, arrt.Num) + "(Max)" : CFG_XMLConfig.GetAttributeStrByID((int)arrt.attr, arrt.Num);
                    JUIExtensions.txt(GetView().root_RefineArrt.Find("Toggle_" + i).Find("Label")).text = GameUtil.Instance.GetStrByQuality(Qulit,arrtStr);
                    
                    JUIExtensions.toggle(GetView().root_RefineArrt.Find("Toggle_" + i)).onValueChanged.RemoveAllListeners();
                    JUIExtensions.toggle(GetView().root_RefineArrt.Find("Toggle_" + i)).onValueChanged.AddListener((isOn)=> {
                        if (isOn)
                        {
                            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                            OnRefineArrtSelect(arrt, idx);
                        }
                    });

                    if (i == 1)
                    {
                        baseStat = arrt;
                    }
                  
                    if (_SelectAttr != null && arrt.attr == _SelectAttr.attr && _SelectAttr.Num == arrt.Num)
                    {
                        baseStat = null;
                        JUIExtensions.toggle(GetView().root_RefineArrt.Find("Toggle_" + idx)).isOn = true;
                        OnRefineArrtSelect(arrt, idx);
                    }
                }
            }

            if (baseStat != null)
            {
                JUIExtensions.toggle(GetView().root_RefineArrt.Find("Toggle_1")).isOn = true;
                OnRefineArrtSelect(baseStat, 1);
            }

            if (_SoltInfo.Item.Equip == null || _SoltInfo.Item.Equip.Attributes.Count == 0)
            {
                GetView().btn_Refine.gameObject.SetActive(false);
                GetView().txt_RefineTip.gameObject.SetActive(true);
            }

            GetView().tog_Refine1.isOn = (RefineType)PlayerPrefsExtend.GetInt("RefineType") == RefineType.Random;
            OnRefineRandomOrDirect((RefineType)PlayerPrefsExtend.GetInt("RefineType"));

            _SendRefine.ItemUid = _SoltInfo.Item.Uid;
        }
    }

    //洗练发送
    private EquipRefineRequest _SendRefine = default;

    //洗练选择发送
    private EquipRefineReplaceRequest _SendEquipRefine = default;

    private Stats _SelectAttr = null;
    //洗练属性选择
    private void OnRefineArrtSelect(Stats attr,int idx)
    {
        _SelectAttr = attr;
        _SendRefine.Attr = attr.attr;
    }

    private void OnRefineEquipRefine(bool isReplace)
    {
        _SendEquipRefine.Replace = isReplace;
        TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_EquipRefineReplace, MType.Request, TCPClientMgr.ProtoSerialize(_SendEquipRefine));
    }

    //洗练类型选择
    private void OnRefineRandomOrDirect(RefineType _refineType)
    {
        PlayerPrefsExtend.SetInt("RefineType",(int) _refineType);
        var CfgInfo = _refineType == RefineType.Random ? CFG_XMLConfig.xmlConfig.RifineRandomDic : CFG_XMLConfig.xmlConfig.RifineDirectDic;
        var _SoltInfo = RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot);

        if (RoleM.Instance.CheckIsNullEquip(_SelectEquipSlot))
            return;

        _BuyInfo.buyId = CfgInfo[(int)CFG_Item.GetById(_SoltInfo.Item.CfgId).Quality].itemid;
        _BuyInfo.num = CfgInfo[(int)CFG_Item.GetById(_SoltInfo.Item.CfgId).Quality].num;

        GameUtil.Instance.SetItemIcon(_BuyInfo.buyId, GetView().img_Refine_Icon);
        GetView().txt_RefineNum.text = GameUtil.Instance.SetAndCheckStr(GameUtil.Instance.FormatNumWithUnit(_BuyInfo.num), _BuyInfo.buyId, _BuyInfo.num);

        _SendRefine.Random = _refineType == RefineType.Random;
    }

    //洗练结果返回
    private void OnRefineCallBack(Stats attrInfo)
    {
        GetView().obj_RefineEnd.SetActive(true);
        GetView().btn_Refine.gameObject.SetActive(false);
        GetView().txt_RefineTip.gameObject.SetActive(false);

        var _SoltInfo = RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot);
        int Qulit = CFG_RandomDistrib.GetColor(CFG_Item.GetQulite(_SoltInfo.Item.CfgId), (int)attrInfo.attr, attrInfo.Num);
        int maxValue = CFG_RandomDistrib.GetMaxValue(CFG_Item.GetQulite(_SoltInfo.Item.CfgId), (int)attrInfo.attr);
        string arrtStr = maxValue == attrInfo.Num ? CFG_XMLConfig.GetAttributeStrByID((int)attrInfo.attr, attrInfo.Num) + "(Max)" : CFG_XMLConfig.GetAttributeStrByID((int)attrInfo.attr, attrInfo.Num);

        GetView().txt_Target.text = GameUtil.Instance.GetStrByQuality(Qulit, arrtStr);
    }

    [Subscriber(ThreadMode.Main)]
    //洗练消息返回
    private void OnRefresh_EquipRefine(GameEvent.Refresh_EquipRefine _e)
    {
        OnRefineCallBack(_e.PbArrt);
    }

    [Subscriber(ThreadMode.Main)]
    //洗练选择返回刷新
    private void OnReplace_EquipRefine(GameEvent.Replace_EquipRefine _e)
    {
        OnSelectEquipClick(_SelectEquipSlot);
    }

    #endregion

    //帮助
    private void OnHelpBtnClick()
    {
        int idx = 7;
        if (_SelectEquipClass == UIBuild.SelectTwo.Equip_Refine)
            idx = 9;
        else if (_SelectEquipClass == UIBuild.SelectTwo.Equip_Star)
            idx = 8;
        else if (_SelectEquipClass == UIBuild.SelectTwo.Equip_Inset)
            idx = 17;

        UITool.Open<UIComRule>(null, idx);
    }

    private UIBuild.SelectTwo _SelectEquipClass;

    //面板选择
    private void OnSelcetUIBtnClick(UIBuild.SelectTwo eClass)
    {
        _SelectEquipClass = eClass;

        GetView().ReinforceUI.SetActive(eClass == UIBuild.SelectTwo.Equip_Reinforce);
        GetView().RefineUI.SetActive(eClass == UIBuild.SelectTwo.Equip_Refine);
        GetView().StarUI.SetActive(eClass == UIBuild.SelectTwo.Equip_Star);
        GetView().InsetUI.SetActive(eClass == UIBuild.SelectTwo.Equip_Inset);

        GetView().usedItems[1].obj.SetActive(eClass == UIBuild.SelectTwo.Equip_Reinforce);
        GetView().usedItems[2].obj.SetActive(eClass == UIBuild.SelectTwo.Equip_Star);
        GetView().usedItems[3].obj.SetActive(eClass == UIBuild.SelectTwo.Equip_Refine);

        _SelectEquipSlot = OnEquipHideOrShow() ;

        switch (eClass)
        {
            case UIBuild.SelectTwo.Equip_Reinforce:
                isAutoReinforce = false;
                break;
            case UIBuild.SelectTwo.Equip_Star:
                IsAutoUpStar = false;

                //RedPointManager.Instance.SetRedClose(RedPointConst.Equip_Star.ToString(), () => {
                //    RedPointManager.Instance.RPS.Dispatch(RedPointConst.Equip_Star.ToString(),
                //        RedDotM.Instance.GetEquipStarRedNum());
                //});

                break;
                case UIBuild.SelectTwo.Equip_Refine:

                //RedPointManager.Instance.SetRedClose(RedPointConst.Equip_Refine.ToString(), () => {
                //    RedPointManager.Instance.RPS.Dispatch(RedPointConst.Equip_Refine.ToString(),
                //        RedDotM.Instance.GetEquipRefineRedNum());
                //});
                break;
            default:
                break;
        }

        OnSelectEquipClick(_SelectEquipSlot);
    }

    private EquipSlot OnEquipHideOrShow()
    {
        int idx = 0;
        for (int i = 1; i <= AllEquipCount; i++)   
        {
            EquipSlot eSlot = (EquipSlot)i;
            var go = GetView().root_allEquip.Find("item_" + i);
            bool isBest = RoleM.Instance.CheckIsBestEquip(eSlot);
            GetView().root_allEquip.Find("item_" + i).gameObject.SetActive(isBest || _SelectEquipClass != UIBuild.SelectTwo.Equip_Refine);
            if (isBest && _SelectEquipClass == UIBuild.SelectTwo.Equip_Refine && idx == 0)
            {
                idx = i;
            }
        }

        GetView().tip_equipTip.gameObject.SetActive(idx == 0 && _SelectEquipClass == UIBuild.SelectTwo.Equip_Refine);
        if (_SelectEquipClass != UIBuild.SelectTwo.Equip_Refine)
        {
            idx = (int)EquipSlot.Weapon;
        }

        return idx == 0 ? EquipSlot.Weapon : (EquipSlot)idx;
    }

    #region 魂石 镶嵌
    private void OnInSetUI()
    {
        bool isNullEquip = TeamIdx == 0 ? RoleM.Instance.CheckIsNullEquip(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquip(TeamIdx, (int)_SelectEquipSlot) == null;
        var _SoltInfo = TeamIdx == 0 ? RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot) : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot);

        JUIExtensions.img(GetView().Inset_Item.Find("icon")).enabled = !isNullEquip;
        if (isNullEquip)
        {
            GameUtil.Instance.SetItemBg(0, JUIExtensions.img(GetView().Inset_Item));
            JUIExtensions.txt(GetView().Inset_Item.Find("txt_Name")).text = GameM.Instance.EquipName[(int)_SelectEquipSlot];
        }
        else
        {
            GameUtil.Instance.SetItemIcon(_SoltInfo.Item.CfgId, JUIExtensions.img(GetView().Inset_Item.Find("icon")), JUIExtensions.img(GetView().Inset_Item),null, (int)_SoltInfo.Item.Equip.Quality);
            JUIExtensions.txt(GetView().Inset_Item.Find("txt_Name")).text = GameUtil.Instance.GetItemName(_SoltInfo.Item.CfgId);
        }

        int lv = TeamIdx == 0 ? RoleM.Instance.Level : _PartnerInfo.Level;
        for (int i = 0; i < 4; i++)
        {
            var go = GetView().root_Inset.GetChild(i);
            bool isOpen = CFG_XMLConfig.xmlConfig.Diamond_OpenlvDic[i + 1] <= lv;
            go.Find("lock").gameObject.SetActive(!isOpen);
            if (!isOpen)
            {
                JUIExtensions.txt(go.Find("lock/txt")).text = CFG_XMLConfig.xmlConfig.Diamond_OpenlvDic[i + 1] + "级\n开启";
            }

            bool isInset = _SoltInfo.Gemstone[i] != 0;
            go.Find("icon").gameObject.SetActive(isOpen && isInset);
            if (isInset)
            {
                JUIExtensions.txt(go, "icon/txt_Name").text = GameUtil.Instance.GetItemName(_SoltInfo.Gemstone[i]);
                GameUtil.Instance.SetItemIcon(_SoltInfo.Gemstone[i], JUIExtensions.img(go, "icon"), JUIExtensions.img(go), JUIExtensions.img(go, "icon/Img_natute"));
            }
            else
            {
                GameUtil.Instance.SetItemBg(0, JUIExtensions.img(go));
            }
            go.Find("add").gameObject.SetActive(isOpen && !isInset);
        }

        GetView().txt_InsetTip.text = "提示:"+ CFG_XMLConfig.xmlConfig.Diamond_PositionDes[(int)_SelectEquipSlot];
    }

    private void OnInsetOnClick(int SelectIdx,bool ItemShow)
    {
        int lv = TeamIdx == 0 ? RoleM.Instance.Level : _PartnerInfo.Level;
        bool isOpen = CFG_XMLConfig.xmlConfig.Diamond_OpenlvDic[SelectIdx + 1] <= lv;
        if (!isOpen)
            return;

        var cid = TeamIdx == 0 ? (int)RoleM.Instance.GetSlotInfoBySolt(_SelectEquipSlot).Gemstone[SelectIdx] : PartnerM.Instance.GetOnePartnerOneEquipSolt(TeamIdx, (int)_SelectEquipSlot).Gemstone[SelectIdx];

        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        if (cid != 0)
        {
            if (ItemShow)
            {
                UITool.Open<UIItemShow>(null, cid);
            }
            else
            {
                TcpC.SetGemstoneChange(new GemstoneChangeRequest { ItemUid = cid, GemstoneSlot = (GemstoneSlot)SelectIdx, Slot = _SelectEquipSlot, OpType = 2,PartnerSlotId = TeamIdx });
            }
        }
        else
        {
            UITool.Open<UIJewelSelect>(null, _SelectEquipSlot, SelectIdx,TeamIdx);
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_TopResouse(GameEvent.EquipInsetRefresh _e)
    {
        if (isNullGo)
            return;
        OnInSetUI();
        OnEquipLvUIRefresh();
        OnEquipSoltBtnRedCheak();
    }

    #endregion

    private int[] resIDs = new int[] { (int)ResID.Coin, (int)ResID.QHS, (int)ResID.SXS, (int)ResID.DianQuan };
    //顶部资源显示
    private void OnTopResouse(bool isInit = false)
    {

        for (int i = 0; i < GetView().usedItems.Length; i++)
        {
            if (isInit)
                GameUtil.Instance.SetItemIcon((int)resIDs[i], GetView().usedItems[i].img);
            GetView().usedItems[i].txt.text = GameUtil.Instance.FormatNumWithUnit(BagM.Instance.GetNumByCfgID(resIDs[i]));
        }
    }

    private int TeamIdx = 0;
    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
    }

    /// <summary>
    /// 槽位伙伴信息
    /// </summary>
    private PartnerSlotInfo _PartnerSlotInfo;

    /// <summary>
    /// 伙伴信息
    /// </summary>
    private PartnerInfo _PartnerInfo;
    public void OnClickShow(int idx, UIBuild.SelectTwo selectTwo)
    {
        TeamIdx = idx;
        if (TeamIdx != 0)
        {
            _PartnerSlotInfo = PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx);
            _PartnerInfo = PartnerM.Instance.GetOnePartnerInfoByUid(_PartnerSlotInfo.PartnerUid);
        }
        OnTopResouse(true);
        OnEquipUIBase();
        OnSelcetUIBtnClick(selectTwo);
        OnEquipSoltBtnRedCheak();
    }

    public void OnShow(bool isShow)
    {
        if (isShow)
        {
            OnEnter();
        }
        else
        {
            OnExit();
        }
        uiGo.SetActive(isShow);
    }

    public override void OnExit()
    {
        _PartnerInfo = null;
        _PartnerSlotInfo = null;
        EquipEnhanceTimes = 0;
        OnClearTime();
        JEvent.defaultEvent.Unregister(this);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_TopResouse(GameEvent.Sync_UserBase _e)
    {
        if (isNullGo)
            return;

        OnTopResouse();
        OnEquipSoltBtnRedCheak();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_BagResouse(Event_Source_Refresh _e)
    {
        if (isNullGo)
            return;

        OnTopResouse();
        OnEquipSoltBtnRedCheak();
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}