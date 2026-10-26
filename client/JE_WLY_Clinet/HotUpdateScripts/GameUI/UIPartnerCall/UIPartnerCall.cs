using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using JEngine.Helper;
using JEngine.UI;
using JEngine.Event;
using HotUpdateScripts.ProtoM;
using HotUpdateScripts;
using Spine.Unity;
using System;

public class UIPartnerCall : UIBase<UIModelPartnerCall, UIViewPartnerCall>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() =>
        {
            if (isAnim) { UITool.ShowTips("操作频繁"); return; }
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIPartnerCall>();
        });

        GetView().btnOneGet.onClick.AddListener(() =>
        {
            if(isAnim) { UITool.ShowTips("操作频繁"); return; }
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnOneOrMoreBtnClick(true);
        });

        GetView().btnMoreGet.onClick.AddListener(() =>
        {
            if (isAnim) { UITool.ShowTips("操作频繁"); return; }
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnOneOrMoreBtnClick(false);
        });

        GetView().btn_help.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            //TODO
            UITool.Open<UIComRule>(null,31,"概率公示");
        });

        GetView().tog_Anim.onValueChanged.AddListener((isOn) =>
        {
            if (isAnim) { UITool.ShowTips("操作频繁"); return; }
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            IsJumpAnim = isOn;
        });

        GetView().btn_Book.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIPartnerBook>();
        });
        
        GetView().btn_PartnerGift.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIActive>(null, ActivityType.PartnerGift);
        });

        for (int i = 0; i < GetView().root_Btns.childCount; i++)
        {
            int idx = i;
            GetView().root_Btns.GetChild(i).GetComponent<Button>().onClick.AddListener(() =>
            {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                OnSelectBtnClick(idx);
            });
        }
    }

    private bool IsJumpAnim = false;
    private bool isAnim = false;
    private GameObject Eff;
    private void OnOneOrMoreBtnClick(bool isOne)
    {
        if ((isOne && CanPlayNum < 1) || (!isOne && CanPlayNum < 9))
        {
            UITool.ShowTips("抽奖次数不足,仅剩"+ CanPlayNum+"次");
            return;
        }
        isAnim = true;
        GameHelper.Instance.SetDeayTimeDo(() => { isAnim = false; }, 0.5f);

        HttpC.GetPrivilegeAward(isOne ? 1 : 2, (SelectType + 1),  (_r) =>
        {
            HttpC.GetPrivilegeDetail(1, (_d) =>
            {
                PrizedrawList = _d;
                OnBaseInfoShow();
            });
            if (!IsJumpAnim)
            {
                if (Eff == null)
                {
                    _ = GameHelper.Instance.SetAsyncObj("UIPartnerCall", GameConst.GetEffPath("UIPartnerCall"), (go) =>
                    {
                        go.transform.SetParent(uiGo.transform);
                        go.transform.localScale = Vector3.one;
                        go.transform.localPosition = Vector3.zero;
                        Eff = go;
                        ShowAinm();
                    }, true);
                }
                else
                    ShowAinm();

                void ShowAinm()
                {
                    Eff.gameObject.SetActive(true);
                    SpineUtil.Instance.PlayOnce(Eff.transform, "animation", () =>
                        {
                            if (isNullGo) return;
                            Eff.gameObject.SetActive(false);
                        });
                        GameHelper.Instance.SetDeayTimeDo(AwardCall, 1.5f);
                 }
            }
            else
            {
                AwardCall();
            }

            void AwardCall()
            {
                //isAnim = false;
                var awards = new UIAwards.AwardsInfo
                {
                    des = "伙伴召唤获得",
                    TimeCount = 20,
                };

                awards.Awards = new UIAwards.AwardsInfo.AwardItem[_r.award.Length];
                for (int i = 0; i < _r.award.Length; i++)
                {
                    awards.Awards[i] = new UIAwards.AwardsInfo.AwardItem
                    {
                        id = _r.award[i].kind == 2 ? CFG_Partner.GetById(_r.award[i].id).ItemID : _r.award[i].id,
                        num = _r.award[i].num,
                    };

                }
                UITool.Open<UIAwards>(null, awards);
            }
             
        });
    }

    private CFGItem_Summon[] CFGItems;
    private void OnSelectBtnClick(int idex)
    {
        SelectType = idex;
        for (int i = 0; i < GetView().root_Btns.childCount; i++)
        {
            GetView().root_Btns.GetChild(i).Find("on").gameObject.SetActive(i == SelectType);
        }
        OnBtnConsumeShow(true);
        OnBaseInfoShow();
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);

        PrizedrawList = (HTTP_PrizedrawListResponse)args[0] ;

        GetView().tog_Anim.isOn = IsJumpAnim;
        CFGItems = CFG_Summon.GetPartenrItems();
        OnSelectBtnClick(SelectType);
        OnResouseShow(true);
        CheckPartnerCallGuide();
    }

    private void CheckPartnerCallGuide()
    {
        if (RoleM.Instance.IsGuiding(CFG_Guidance.GuideType.Partner))
        {
            var guide = new UIGuide.GuideData
            {
                ID = RoleM.Instance.GuideId,
                ButtonPos = JUIExtensions.GetUILocalPositionRelativeCanvas(GetView().btnOneGet.transform),
                callBack = () =>
                {
                    OnOneOrMoreBtnClick(true);
                },
            };
            RoleM.Instance.SetGuide(guide);
        }
    }

    private int CanPlayNum = 0;
    private int SelectType = 0;

    HTTP_PrizedrawListResponse PrizedrawList ;
    private void OnBaseInfoShow()
    {
        int needNum = 0;
        for (int i = 0; i < PrizedrawList.canplay.Length; i++) {
            if (PrizedrawList.canplay[i].type == (SelectType + 1))
            {
                CanPlayNum = PrizedrawList.canplay[i].can_play;
                needNum = PrizedrawList.canplay[i].need_num;
            }
        }

        for (int i = 0; i < GetView().root_Btns.childCount; i++)
        {
            JUIExtensions.txt(GetView().root_Btns.GetChild(i), "txt_Limit").text = "每日上限:"+ PartnerM.Instance.CanGetNums[i];
            GetView().root_Btns.GetChild(i).Find("on").gameObject.SetActive(i == SelectType);
        }

        if (CanPlayNum > 0)
        {
            GetView().txt_tip.text = SelectType == 2 ? needNum + "次后必得红整卡伙伴" : needNum + "次后必得整卡伙伴";
        }
        else
        {
            GetView().txt_tip.text = "今日抽奖次数已用完";
        }

    }

    private void OnBtnConsumeShow(bool isInit)
    {
        if (isInit)
        {
            GameUtil.Instance.SetItemIcon(CFGItems[SelectType].Consume[0].id, JUIExtensions.img(GetView().btnOneGet.transform, "img"));
            GameUtil.Instance.SetItemIcon(CFGItems[SelectType].Consume[1].id, JUIExtensions.img(GetView().btnMoreGet.transform, "img"));
        }

        GetView().btnOneGet.transform.Find("dot").gameObject.SetActive(PartnerM.Instance.CanGetNums[SelectType] > 0 && GameUtil.Instance.ComCoinToCheck(CFGItems[SelectType].Consume[0].id, CFGItems[SelectType].Consume[0].num, null, false));
        JUIExtensions.txt(GetView().btnOneGet.transform, "img/txt_num").text = GameUtil.Instance.GetCheakNumStr(CFGItems[SelectType].Consume[0].id, CFGItems[SelectType].Consume[0].num);

        GetView().btnMoreGet.transform.Find("dot").gameObject.SetActive(PartnerM.Instance.CanGetNums[SelectType] > 0 && GameUtil.Instance.ComCoinToCheck(CFGItems[SelectType].Consume[1].id, CFGItems[SelectType].Consume[1].num, null, false));
        JUIExtensions.txt(GetView().btnMoreGet.transform, "img/txt_num").text = GameUtil.Instance.GetCheakNumStr(CFGItems[SelectType].Consume[1].id, CFGItems[SelectType].Consume[1].num);
    }

    public static int[] UsedItemIDs = { 77, 78, 79 };
    private void OnResouseShow(bool isInit)
    {
        for (int i = 0; i < GetView().root_res.childCount; i++)
        {
            var go = GetView().root_res.GetChild(i);
            if (isInit)
                GameUtil.Instance.SetItemIcon(UsedItemIDs[i], JUIExtensions.img(go, "img"));

            JUIExtensions.txt(go, "txt_num").text = GameUtil.Instance.FormatNumWithUnit(HotUpdateScripts.ProtoM.BagM.Instance.GetNumByCfgID(UsedItemIDs[i]));

            GetView().root_Btns.GetChild(i).Find("dot").gameObject.SetActive(BagM.Instance.GetNumByCfgID(UsedItemIDs[i]) > 0);
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_Refresh(Event_Source_Refresh _e)
    {
        OnResouseShow(false);
        OnBtnConsumeShow(false);
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_Refresh(GameEvent.Sync_TakeSource _e)
    {
        OnResouseShow(false);
        OnBtnConsumeShow(false);
    }

    public override void OnExit()
    {
        if (Eff != null)
        {
            PoolHelper.Instance.RecycObj(Eff);
            Eff = null;
        }
        JEvent.defaultEvent.Unregister(this);
    }

    public override void OnDestroy()
    {
        JEvent.defaultEvent.Unregister(this);
    }
}