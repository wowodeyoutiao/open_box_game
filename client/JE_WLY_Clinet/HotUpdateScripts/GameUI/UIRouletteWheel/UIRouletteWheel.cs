using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Event;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using JEngine.UI;
using System.Linq;
using DG.Tweening;
using JEngine.Core;

public class UIRouletteWheel : UIBase<UIModelRouletteWheel, UIViewRouletteWheel>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIRouletteWheel>();
        });

        GetView().btn_One.onClick.AddListener(() => {

            if (isPlayAnim)
                return;

            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            if (RoleM.Instance.Level < CFGAct.PreLimit)
            {
                UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
                {
                    ShowStr = "您当前的角色等级不足，是否前往副本进行提升？",
                    YesFun = () =>
                    {
                        UITool.Close<UIRouletteWheel>();
                        UITool.Open<UIHall>(null, UIHall.HallType.PVE);
                    },
                });
                return;
            }

            if (!GameUtil.Instance.ComCoinToCheck(int.Parse(CFGAct.PayItem[0].ElementAt(0).Key), CFGAct.PayItem[0].ElementAt(0).Value,
                () => { UITool.Close<UIRouletteWheel>(); }))
            {
                return;
            }
            HttpC.GetTurnTableClick((_r) =>
            {
                if (isNullGo)
                    return;

                if (_r.code == 0)
                {
                    OnAinmTurn(_r.puid);
                }
            });

        });
    }

    private void OnAinmTurn(int id)
    {
        isPlayAnim = true;
        var info = CFG_PrizeWheel.GetRounds(CFGAct.Content);
        int idx = 0;
        for (int i = 0; i < 8; i++)
        {
            if (info[i].UID == id)
            {
                idx = i;
                break;
            }
        }
        var allRota = -247.5f - idx * 45 - 720;

        GetView().root_arrow.DORotate(Vector3.forward * allRota, 2,RotateMode.LocalAxisAdd).SetEase(Ease.InOutExpo).OnComplete(() =>
         {
             GetView().group_light.gameObject.SetActive(true);
             GetView().group_light.alpha = 1;
             GetView().group_light.DOFade(0, 0.2f).SetLoops(10, LoopType.Yoyo).OnComplete(() => {
                 GetView().group_light.gameObject.SetActive(false);
                 OnGetInfo();
             });
         });
    }

    private TimeItem TimeJa;
    private void ClearTime()
    {
        if (TimeJa != null)
        {
            GameHelper.Instance.RemoveInvoke(TimeJa);
            TimeJa = null;
        }
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);

        OnGetInfo();

        ClearTime();
        TimeJa = GameHelper.Instance.AddInvoke(new TimeItem(-1, 60, () => {
            if (isNullGo) return;

            GetView().txt_Time.text = $"剩余时间:<color=#F8C15E>{GameUtil.Instance.GetTimeToDHM(ActivityM.Instance.GetAcitvityEndTime( ActivityType.TurnWheel))}</color>";
        },true));

    }

    private void OnGetInfo()
    {
        HttpC.GetListTurnTable((_r) => {
            if (isNullGo)
                return; 
            OnRefreshUI(_r.content);
        });
    }

    private bool isPlayAnim = false;
    private CFGItem_Activity CFGAct;
    private void OnRefreshUI(int content)
    {
        isPlayAnim = false;
        int aId = content == -1 ? 6105 : content;
        CFGAct = CFG_Activity.GetById(aId);

        var info = CFG_PrizeWheel.GetRounds(CFGAct.Content);
        int itemID = int.Parse(CFGAct.PayItem[0].ElementAt(0).Key);
        int itemNum = CFGAct.PayItem[0].ElementAt(0).Value;
        for (int i = 0; i < 8; i++)
        {
            var go = GetView().root_Item.transform.Find("item_" + i);
            GameUtil.Instance.SetItemIcon(itemID, JUIExtensions.img(go.Find("icon")), JUIExtensions.img(go));
            JUIExtensions.txt(go.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(Mathf.FloorToInt(info[i].rate * itemNum) );
        }

        GameUtil.Instance.SetItemIcon(itemID,GetView().img_Icon);

        GetView().btn_One.gameObject.SetActive(content != -1);
        GetView().txt_Over.gameObject.SetActive(content == -1);

        GetView().group_light.gameObject.SetActive(false);
        GetView().root_arrow.localEulerAngles = Vector3.zero;

        OnResush();
    }

    private void OnResush()
    {
        int itemNum = CFGAct.PayItem[0].ElementAt(0).Value;
        bool isEnoug = GameUtil.Instance.ComCoinToCheck(int.Parse(CFGAct.PayItem[0].ElementAt(0).Key), itemNum, null, false) ;
        GetView().txt_Num.text = isEnoug ? GameUtil.Instance.FormatNumWithUnit(itemNum) : "<color=#DA4E38>" + GameUtil.Instance.FormatNumWithUnit(itemNum) + "</color>";
        GetView().btn_One.transform.Find("dot").gameObject.SetActive(isEnoug && RoleM.Instance.Level >= CFGAct.PreLimit);
       
        GetView().txt_Des.text = CFGAct.PreLimit > RoleM.Instance.Level ?
            "角色等级达到<color=#DA4E38>" + CFGAct.PreLimit + "</color>级可抽奖"
            : "角色等级达到<color=#6fb469>" + CFGAct.PreLimit + "</color>级可抽奖";

    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
        ClearTime();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnEvent_Source_Refresh(Event_Source_Refresh _e)
    {
        if (uiGo == null || uiGo.Equals(null))
            return;
        OnResush();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserBase(GameEvent.Sync_UserBase _e)
    {
        if (uiGo == null || uiGo.Equals(null))
            return;
        OnResush();
    }


    public override void OnDestroy()
    {
        JEvent.defaultEvent.Unregister(this);
        ClearTime();
    }
}