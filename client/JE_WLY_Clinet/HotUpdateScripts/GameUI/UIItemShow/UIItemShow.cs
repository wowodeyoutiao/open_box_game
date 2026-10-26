using System.Collections.Generic;
using MetaFramework.UI;
using pb;
using JEngine.Helper;
using HotUpdateScripts.ProtoM;
using UnityEngine;
using System;
using JEngine.UI;

public class UIItemShow : UIBase<UIModelItemShow, UIViewItemShow>
{
    private int CfgId { get; set; }

    public override UILayer GetLayer()
    {
        return UILayer.Top;
    }
    public override void OnInit()
    {
        GetView().Btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIItemShow>();
        });

        GetView().Btn_PreviewStarInfo.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            var cfgPartner= CFG_Partner.GetByItemCfgID(CfgId);
            UITool.Open<UIStarDetails>(null, cfgPartner);
            UITool.Close<UIItemShow>();
        });
    }
    private void BaseItemInfo()
    {
        var cfgInfo = CFG_Item.GetById(CfgId);

        int Quality = ( cfgInfo.Type == (int)CFG_Item.ItemType.Equip && equipInfo !=null) ? (int)equipInfo.Quality : -1;

        GetView().txt_name.text = GameUtil.Instance.GetItemName(CfgId, Quality);
        JUIExtensions.outline(GetView().txt_name.transform).effectColor = GameUtil.Instance.GetQualityOutlineColor(Quality == -1 ? (int)cfgInfo.Quality : Quality);

        GetView().txt_Des.text = cfgInfo.Desc == null ? "" : cfgInfo.Desc;
        GetView().obj_Des.SetActive(cfgInfo.Desc != null);

        GameUtil.Instance.SetItemIcon(CfgId, GetView().Img_icon,GetView().Img_Icon_bg,GetView().Img_Nature, Quality);

        GetView().txt_LevelLimit.text = (cfgInfo.Levellimit == 0 ? $"无" 
            : $"{cfgInfo.Levellimit}");

        //GetView().txt_GenderLimit.text = "性别限制：              " + (cfgInfo.Genderlimit == 0 ? $"<color=#4EC823>无</color>" 
        //    : $"<color=#{(cfgInfo.Genderlimit != RoleM.Instance.Sex ? "CE4031" : "4EC823")}>{CFG_Item.GetSexStrById(cfgInfo.Genderlimit)}</color>");

        int Job = TeamIdx == 0 ? RoleM.Instance.Job : (CFG_Partner.GetById(PartnerM.Instance.GetOnePartnerInfoByUid(PartnerM.Instance.GetOnePartnerSlotInfo(TeamIdx).PartnerUid).CfgId).job %10) ;
        GetView().txt_OccupyLimit.text = cfgInfo.Occupylimit == 0 ? $"无" 
            : $"<color=#{(cfgInfo.Occupylimit != Job ? "CE4031" : "4EC823")}>{CFG_Item.GetJobStrById((int)cfgInfo.Occupylimit)}</color>";


        bool IsEquip = (int)cfgInfo.Type == (int)CFG_Item.ItemType.Equip;
        GetView().obj_Bestattribute.gameObject.SetActive(IsEquip && equipInfo != null);

        (GetView().txt_attribute0.text, GetView().txt_attribute1.text) = GetItemBaseAttribute(CfgId, Quality);
        GetView().obj_attribute.gameObject.SetActive(GetView().txt_attribute0.text != "");
        if (equipInfo != null)
        {
            (GetView().txt_Bestattribute0.text, GetView().txt_Bestattribute1.text) = GetItemBestAttribute(CfgId, equipInfo);
            GetView().obj_Bestattribute.gameObject.SetActive(GetView().txt_Bestattribute0.text != "");
        }

        GetView().Btn_PreviewStarInfo.gameObject.SetActive(cfgInfo.Type == 8);

    }

    private (string, string) GetItemBaseAttribute(int cfgID,int Quality)
    {
        var cfgInfo = CFG_Item.GetById(cfgID);

        float equipValue = Quality > 0? CFG_XMLConfig.xmlConfig.EquipQualityDic[Quality]._value: 1;
        string Str1 = "";
        string Str2 = "";
        int idx = 0;
        foreach (var item in CFG_XMLConfig.xmlConfig.EquipDisplayDic)
        {
            if (cfgInfo.GetType().GetField(item.Key).GetValue(cfgInfo) != null && (int)cfgInfo.GetType().GetField(item.Key).GetValue(cfgInfo) > 0)
            {
                int _value = (int)Math.Round((int)cfgInfo.GetType().GetField(item.Key).GetValue(cfgInfo) * equipValue);
                //int qulite = CFG_RandomDistrib.GetColor(Quality > 0 ? Quality :(int)cfgInfo.Quality , CFG_XMLConfig.xmlConfig.EquipDisplayToSeverDic[item.Key], _value);
                string arrtStr = CFG_XMLConfig.GetAttributeStrByID((int)CFG_XMLConfig.xmlConfig.EquipDisplayToSeverDic[item.Key], _value, true);
                if (idx % 2 == 0)
                    Str1 = $"{Str1}{arrtStr}\n";
                else
                    Str2 = $"{Str2}{arrtStr}\n";
            }
        }
        return (Str1, Str2);
    }

    private (string, string) GetItemBestAttribute(int cfgID, EquipInfo eInfo)
    {
        var cfgInfo = CFG_Item.GetById(cfgID);
        string bestStr1 = "";
        string bestStr2 = "";
        int idx = 0;
        if (eInfo != null)
        {
            Dictionary<int, int> BestDic = new Dictionary<int, int>();
            foreach (var item in eInfo.Attributes)
            {
                BestDic.Add((int)item.attr, item.Num);
            }

            foreach (var item in CFG_XMLConfig.xmlConfig.RolePanelList)
            {
                if (BestDic.ContainsKey(item.SID) && BestDic[item.SID] > 0)
                {
                    //int qulite = CFG_RandomDistrib.GetColor((int)eInfo.Quality, item.SID, BestDic[item.SID]);
                    string arrtStr = CFG_XMLConfig.GetAttributeStrByID(item.SID, BestDic[item.SID],true);
                    if (idx % 2 == 0)
                        bestStr1 = $"{bestStr1}{arrtStr}\n";
                    else
                        bestStr2 = $"{bestStr2}{arrtStr}\n";
                }
            }
        }
        return (bestStr1, bestStr2);
    }

    private EquipInfo equipInfo;

    private int TeamIdx;
    public override void OnEnter(params object[] args)
    {
        CfgId = (int)args[0] ;
        if (args.Length > 1)
            equipInfo = args[1] as EquipInfo;
        else
            equipInfo = null;

        TeamIdx = args.Length > 2 ?(int)args[2] : 0;
        BaseItemInfo();
    }

    public override void OnExit()
    {
        
    }

     public override void OnDestroy()
    {

    }
}