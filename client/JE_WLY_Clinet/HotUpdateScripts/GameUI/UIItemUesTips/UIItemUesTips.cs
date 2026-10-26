using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Core;
using HotUpdateScripts.ProtoM;
using pb;
using JEngine.Helper;
using JEngine.MyNet;
using JEngine.UI;

/// <summary>
/// 道具快捷使用
/// </summary>
public class UIItemUesTips : UIBase<UIModelItemUesTips, UIViewItemUesTips>
{
    public static bool IsOpen = false;
    public override UILayer GetLayer()
    {
        return UILayer.Top;
    }

    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(OnItemUseClose);
        GetView().btn_Use.onClick.AddListener(OnItemUseClick);
        GetView().btn_Item.onClick.AddListener(OnItemShowClick);
    }

    private void OnItemUseClose()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        RemoveTime();
    }

    private bool isClick = false;
    private void OnItemUseClick()
    {
        if (isClick)
            return;
        isClick = true;
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        RemoveTime(true);
    }

    private void RemoveTime(bool isUsed = false)
    {
        if (isUsed)
        {
            //Debug.Log($"使用道具: {_ItemInfo.CfgId}  {GameUtil.Instance.GetItemName(_ItemInfo.CfgId)}");
            if (IsEquip)
                TcpC.C2S_ChangeEquip(_ItemInfo.Uid, 1, (int)CFG_Item.GetById(_ItemInfo.CfgId).Equiptype, SoltIdx);

            else
                TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_ItemUse, MType.Request, 
                    TCPClientMgr.ProtoSerialize(new ItemUseRequest() 
                    { 
                        ItemUid = _ItemInfo.Uid, Num = (int)BagM.Instance.GetNumByCfgID(_ItemInfo.CfgId)
                    })
                    );
        }

        if (GameHelper.Instance != null && TimeJAction != null)
        {
            GameHelper.Instance.RemoveInvoke(TimeJAction);
            TimeJAction = null;
            OnItemShow();
        }
    }

    private void OnItemShowClick()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
    }

    public override void OnEnter(params object[] args)
    {
        OnItemShow();
    }

    private int TimeNum = 5;
    private TimeItem TimeJAction;
    private ItemInfo _ItemInfo;
    private bool IsEquip = false;

    private int SoltIdx = 0;
    private void OnItemShow()
    {
        if (!TipM.Instance.CheckItemUse())
        {
            UITool.Close<UIItemUesTips>();
            return;
        }
        isClick = false;

        TimeNum = 6;
        _ItemInfo = TipM.Instance.GetItemUse();

        var CfgItem = CFG_Item.GetById(_ItemInfo.CfgId);

        IsEquip = CfgItem.Type == (int)CFG_Item.ItemType.Equip;

        GetView().txt_Name.text = GameUtil.Instance.GetItemName(_ItemInfo.CfgId);
        JUIExtensions.outline(GetView().txt_Name.transform).effectColor = GameUtil.Instance.GetQualityOutlineColor((int)CfgItem.Quality);

        GameUtil.Instance.SetItemIcon(_ItemInfo.CfgId, GetView().img_Icon, GetView().img_BgIcon);

        GetView().txt_btn.text = IsEquip ? "立即穿戴" : "立即使用";

        GetView().txt_Num.text = IsEquip ? "" : GameUtil.Instance.FormatNumWithUnit(BagM.Instance.GetNumByCfgID(_ItemInfo.CfgId));

        (int powerChange, SoltIdx) = GameUtil.Instance.GetEquipIsBest(_ItemInfo);
        GetView().obj_Up.gameObject.SetActive(powerChange > 0);

        Debug.Log($"道具: {_ItemInfo.CfgId}  {GameUtil.Instance.GetItemName(_ItemInfo.CfgId)}  {BagM.Instance.GetNumByCfgID(_ItemInfo.CfgId) > 0} {((!IsEquip && CfgItem.Levellimit <= RoleM.Instance.Level) || SoltIdx >= 0)}");
        if (BagM.Instance.GetNumByCfgID(_ItemInfo.CfgId) > 0  && ((!IsEquip && CfgItem.Levellimit <= RoleM.Instance.Level )|| SoltIdx >= 0))
        {
            RemoveTime();
            string Str = "秒后需在包裹中使用";
            TimeJAction = GameHelper.Instance.AddInvoke(new TimeItem(TimeNum, 1, () => {
                if (isNullGo)
                    return;

                TimeNum--;
                GetView().txt_Time.text = $"{TimeNum}{Str}";
                if (TimeNum <= 0)
                {
                    RemoveTime();
                }

            }));
        }
        else
        {
            OnItemShow();
        }
    }

    public override void OnExit()
    {
        IsOpen = false;
        RemoveTime();
    }

    public override void OnDestroy()
    {
        OnExit();
        TipM.Instance.ClearItemUse();
    }
}