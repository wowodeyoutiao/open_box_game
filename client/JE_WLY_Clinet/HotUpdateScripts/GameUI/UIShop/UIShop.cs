using HotUpdateScripts;
using HotUpdateScripts.ProtoM;

using JEngine.Event;
using JEngine.Helper;
using JEngine.UI;

using MetaFramework.UI;

using SuperScrollView;

using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

public class UIShop : UIBase<UIModelShop, UIViewShop>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    private CFG_Store.StoreType _SelectShop = CFG_Store.StoreType.No;
    public override void OnInit()
    {

        for (int i = 0; i < GetView().TopBtns.Length; i++)
        {
            CFG_Store.StoreType idx = (CFG_Store.StoreType)(i + 1);
            GetView().TopBtns[i].transform.Find("Label").GetComponent<Text>().text = CFG_Store.StoreNameDic[idx];
            GetView().TopBtns[i].transform.Find("Select/Label").GetComponent<Text>().text = CFG_Store.StoreNameDic[idx];
            GetView().TopBtns[i].onClick.AddListener(() =>
            {

                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

                if (CFG_Store.StoreType.Diamanta == idx && GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Shop_ZH))
                    return;
                else if (CFG_Store.StoreType.SiLian == idx && GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Shop_SL))
                    return;

                OnShopToggleClick(idx);
            });
        }

        GetView().Btn_Close.onClick.AddListener(OnClose);

        GetView().Btn_CoinAdd.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIShop>(null, CFG_Store.StoreType.Coupon);
        });

        //战魂
        GetView().Btn_DiamantaAdd.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_PVP))
                return;

            //跳转到乱斗功能的副本选择界面。
            if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
            {
                UITool.ShowTips("已在战斗中");
                return;
            }
            UITool.Close<UIShop>();
            UITool.Open<UIFightChoice>(null, UIFightChoice.SelectType.Limit, UIFightChoice.ChanllengeType.PvP);
        });

        //试炼凭证
        GetView().btn_SLPZ.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_SlTower))
                return;

            if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
            {
                UITool.ShowTips("已在战斗中");
                return;
            }
            UITool.Close<UIShop>();
            UITool.Open<UIFightChoice>(null, UIFightChoice.SelectType.Challange, UIFightChoice.ChanllengeType.SLTower);
        });

        GetView().Btn_CouponAdd.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnShopToggleClick(CFG_Store.StoreType.RMB);
        });

        GetView().infiniteScroll.InitGridView(0, OnFillItem);


        GetView().btn_Banner.onClick.AddListener(() =>
        {
            OnBannerClick();
        });
    }

    LoopGridViewItem OnFillItem(LoopGridView gridView, int sordIdx, int row, int column)
    {
        LoopGridViewItem _LoopGridViewItem = gridView.NewListViewItem("Item");
        GameObject go = _LoopGridViewItem.gameObject;

        go.SetActive(true);
        var _item = ShopList[sordIdx];
        var cfg_item = CFG_Item.GetById(_item.Solditems[0].itemID);

        bool IsRMB = _SelectShop == CFG_Store.StoreType.RMB;
        if (IsRMB)
        {
            JUIExtensions.txt(go.transform.Find("txt_Name")).text = GameUtil.Instance.FormatNumWithUnit(_item.Solditems[0].Num) + cfg_item.Name;
            JUIExtensions.outline(go.transform.Find("txt_Name")).effectColor = new Color(0.23f, 0.42f, 0.66f);
            ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Shop", "rnb_" + sordIdx, JUIExtensions.img(go.transform, "Item/icon"));
            GameUtil.Instance.SetItemIcon(cfg_item.ID, null, JUIExtensions.img(go.transform.Find("Item")));
            JUIExtensions.img(go.transform.Find("Item/bg")).enabled = false;
            JUIExtensions.txt(go.transform.Find("Item/txt_num")).text = "";
            go.transform.Find("tip_FirstPay").gameObject.SetActive(StoreM.Instance.GetBuyNumByID(_item.ID) == 0);
        }
        else
        {
            int Quality = (int)cfg_item.Quality;
            JUIExtensions.txt(go.transform.Find("txt_Name")).text = GameUtil.Instance.GetItemName(cfg_item.ID, Quality);
            JUIExtensions.outline(go.transform.Find("txt_Name")).effectColor = GameUtil.Instance.GetQualityOutlineColor(Quality, new Color(0.42f, 0.33f, 0.27f));
            GameUtil.Instance.SetItemIcon(cfg_item.ID, JUIExtensions.img(go.transform.Find("Item/icon")), JUIExtensions.img(go.transform.Find("Item")));
            JUIExtensions.img(go.transform.Find("Item/bg")).enabled = true;
            JUIExtensions.txt(go.transform.Find("Item/txt_num")).text = GameUtil.Instance.FormatNumWithUnit(_item.Solditems[0].Num);
            go.transform.Find("tip_FirstPay").gameObject.SetActive(false);
        }

        string PStr = IsRMB ? "¥" : "";
        bool isEnough = GameUtil.Instance.ComCoinToCheck(_item.ID, null, false);
        JUIExtensions.txt(go.transform.Find("price/txt_price")).text = isEnough ? PStr + GameUtil.Instance.FormatNumWithUnit(_item.Limite.Num) : $"<color=#DA4E38>{PStr}{GameUtil.Instance.FormatNumWithUnit(_item.Limite.Num)}</color>";
        JUIExtensions.outline(go.transform.Find("price/txt_price")).effectColor = isEnough ? new Color(0.082f, 0.19f, 0.47f) : new Color(0.23f, 0.42f, 0.66f);

        go.transform.Find("price/icon").gameObject.SetActive(!IsRMB);

        if (!IsRMB)
            GameUtil.Instance.SetItemIcon(_item.Limite.needmoney, JUIExtensions.img(go.transform.Find("price/icon")));

        bool IsLock = false;
        if (IsLock)
        {
            JUIExtensions.txt(go.transform.Find("lock/txt_Lime")).text = $"通过{_item.Prelimit}层解锁";
        }
        else
        {
            JUIExtensions.txt(go.transform.Find("txt_Lime")).text = GetLimetDes(_item.Limite.limittype, _item.Limite.limitnum, StoreM.Instance.GetBuyNumByID(_item.ID));
        }
        go.transform.Find("lock").gameObject.SetActive(IsLock);
        go.transform.Find("txt_Lime").gameObject.SetActive(!IsLock);

        JUIExtensions.touch(go.transform).RemoveAllListeners();

        JUIExtensions.touch(go.transform).AddClickListener(p =>
        {
            if (!p.dragging)
                OnShopToBuyClick(_item);
        });

        JUIExtensions.touch(go.transform).AddPointerDownListener(p =>
        {
            _TouchTime = GameHelper.Instance.AddInvoke(new TimeItem(GameConst.TouchTime, () =>
            {
                if (!p.dragging)
                {
                    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                    UITool.Open<UIItemShow>(null, cfg_item.ID);
                }
            }));
        });

        JUIExtensions.touch(go.transform).AddPointerUpListener(p =>
        {
            ClearTouch();
        });

        return _LoopGridViewItem;
    }

    private TimeItem _TouchTime;
    private void ClearTouch()
    {
        if (_TouchTime != null)
        {
            GameHelper.Instance.RemoveInvoke(_TouchTime);
            _TouchTime = null;
        }
    }

    private void OnShopToBuyClick(CFGItem_Store _itemStore)
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        if (_itemStore.Limite.limittype != 999 && _itemStore.Limite.limitnum <= StoreM.Instance.GetBuyNumByID(_itemStore.ID))
        {
            UITool.ShowTips("库存已空");
            return;
        }

        Action JumpFun = null;
        if (_itemStore.Limite.needmoney == 8 || _itemStore.Limite.needmoney == 4)
        {
            JumpFun = delegate { UITool.Close<UIShop>(); };
        }

        if (!GameUtil.Instance.ComCoinToCheck(_itemStore.Limite.needmoney, _itemStore.Limite.Num, JumpFun, true))
        {
            return;
        }

        if (_SelectShop == CFG_Store.StoreType.RMB)
        {
            GameUtil.Instance.GameToBuy(_itemStore.ID, delegate
            {
                OnShopToggleClick(_SelectShop);
            });
        }
        else
        {
            string titleStr = $"{CFG_Item.GetById(_itemStore.Solditems[0].itemID).Name}购买";

            UITool.Open<UICommonItemBuy>(null, _itemStore.ID, titleStr, () =>
            {
                OnShopToggleClick(_SelectShop);
            });
        }
    }

    private string GetLimetDes(int limittype, int AllCount, long pro)
    {
        string str = "无限制";
        if (limittype == 0)
        {
            str = $"终生:{AllCount - pro}/{AllCount}";
        }
        else if (limittype == 1)
        {
            str = $"每日:{AllCount - pro}/{AllCount}";
        }
        else if (limittype == 2)
        {
            str = $"每周:{AllCount - pro}/{AllCount}";
        }
        else if (limittype == 3)
        {
            str = $"每月:{AllCount - pro}/{AllCount}";
        }
        return str;
    }
    private void OnClose()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        UITool.Close<UIShop>();
    }

    private List<CFGItem_Store> ShopList;

    private void OnShopToggleClick(CFG_Store.StoreType _sType)
    {
        //暂时屏蔽RMB商店
        if ( _sType == CFG_Store.StoreType.RMB)
            _sType = CFG_Store.StoreType.Coupon;

        for (int i = 0; i < GetView().TopBtns.Length; i++)
        {
            GetView().TopBtns[i].transform.Find("Select").gameObject.SetActive((i + 1) == (int)_sType);
        }

        _SelectShop = _sType;
        ShopList = CFG_Store.GetListByStoreType(_SelectShop);

        GetView().obj_SLPZ.SetActive(_sType == CFG_Store.StoreType.SiLian);
        GetView().obj_ZanHun.SetActive(_sType == CFG_Store.StoreType.Diamanta);
        GetView().infiniteScroll.SetListItemCount(ShopList.Count);
        GetView().infiniteScroll.RefreshAllShownItem();
        ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Shop", "img_" + (int)_SelectShop, GetView().img_AD);
    }

    private void OnRefreshTop()
    {
        GetView().txt_Coin.text = GameUtil.Instance.FormatNumWithUnit(RoleM.Instance.Coin);
        GetView().txt_Coupon.text = GameUtil.Instance.FormatNumWithUnit(RoleM.Instance.Coupon);
        GetView().txt_Diamanta.text = GameUtil.Instance.FormatNumWithUnit(RoleM.Instance.Soul);
        GetView().txt_SLPZ.text = GameUtil.Instance.FormatNumWithUnit(BagM.Instance.GetNumByCfgID((int)CFG_Item.ItemID.SLPZ));
    }

    private void OnBannerClick()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
        switch (_SelectShop)
        {
            case CFG_Store.StoreType.RMB:
                if (ActivityM.Instance.GetAcitvityState(ActivityType.FirstPay))
                {
                    HttpC.GetFirstPayInfo((req) =>
                    {
                        if (req.code == 0 && !isNullGo)
                        {
                            UITool.Close<UIShop>();
                            UITool.Open<UIFirstPay>(null, req);
                        }
                    });
                }
                else
                {
                    UITool.ShowTips("首充活动已结束");
                }
                break;
            case CFG_Store.StoreType.Coupon:
                if (CFG_XMLConfig.GetOpenSolt(RoleM.Instance.Level) > 0)
                {
                    HttpC.GetPrivilegeDetail(1, (_r) =>
                    {
                        UITool.Close<UIShop>();
                        UITool.Open<UIPartnerCall>(null, _r);
                    });
                }
                break;
            case CFG_Store.StoreType.Coin:
                if (GameUtil.Instance.CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv))
                    return;
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Build_SkillLv))
                    return;
                UITool.Close<UIShop>();
                UITool.Open<UIBuild>();
                break;
            case CFG_Store.StoreType.Diamanta:
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_PVP))
                    return;

                if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
                {
                    UITool.ShowTips("已在战斗中");
                    return;
                }
                UITool.Close<UIShop>();
                UITool.Open<UIFightChoice>(null, UIFightChoice.SelectType.Limit, UIFightChoice.ChanllengeType.PvP);
                break;
            case CFG_Store.StoreType.SiLian:
                if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_SlTower))
                    return;

                if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
                {
                    UITool.ShowTips("已在战斗中");
                    return;
                }
                UITool.Close<UIShop>();
                UITool.Open<UIFightChoice>(null, UIFightChoice.SelectType.Challange, UIFightChoice.ChanllengeType.SLTower);
                break;
            default:
                break;
        }

    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);

        OnRefreshTop();
        if (args.Length > 0)
            OnShopToggleClick((CFG_Store.StoreType)args[0]);
        else
            OnShopToggleClick(CFG_Store.StoreType.Coupon);
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_Refresh(Event_Source_Refresh _e)
    {
        if (isNullGo) return;
        OnShopToggleClick(_SelectShop);
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_RefreshTop(GameEvent.Sync_UserBase _e)
    {
        if (isNullGo) return;
        OnRefreshTop();
        OnShopToggleClick(_SelectShop);
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_ZeroRefresh(GameEvent.Refresh_ZeroTime _e)
    {
        if (isNullGo) return;

        HttpC.OnGetShopInfo(0, _r =>
        {
            if (!isNullGo)
            {
                OnRefreshTop();
                OnShopToggleClick(_SelectShop);
            }
        });
    }

    public override void OnExit()
    {
        _SelectShop = CFG_Store.StoreType.No;
        JEvent.defaultEvent.Unregister(this);
    }

    public override void OnDestroy()
    {
        JEvent.defaultEvent.Unregister(this);
    }
}