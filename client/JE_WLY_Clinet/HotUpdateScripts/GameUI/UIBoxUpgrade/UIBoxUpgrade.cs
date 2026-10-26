using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using JEngine.Event;
using pb;

public class UIBoxUpgrade : UIBase<UIModelBoxUpgrade, UIViewBoxUpgrade>
{
    /// <summary>
    /// 要显示的等级类型
    /// </summary>
    public enum QualityRateShowType
    {
        /// <summary>
        /// 显示当前等级
        /// </summary>
        CurrentLevel,

        /// <summary>
        /// 显示下一等级
        /// </summary>
        NextLevel,
    }

    /// <summary>
    /// 宝箱升级状态
    /// </summary>
    public enum UpgradeState
    {
        /// <summary>
        /// 宝箱可升级
        /// </summary>
        CanUpgrade,

        /// <summary>
        /// 宝箱升级中
        /// </summary>
        Upgrading,

        /// <summary>
        /// 道具不足，宝箱不可升级
        /// </summary>
        CannotUpgrade,
    }

    /// <summary>
    /// 当前等级宝箱配置
    /// </summary>
    CFGItem_BoxLevel cfgBoxLevelItem;

    /// <summary>
    /// 宝箱升级状态
    /// </summary>
    UpgradeState upgradeState = UpgradeState.CannotUpgrade;

    /// <summary>
    /// 宝箱最大等级
    /// </summary>
    int maxLevel;

    /// <summary>
    /// 宝箱升级剩余所用时间
    /// </summary>
    //long boxUpgradeRemainingTime;

    //const string tip_CannotUpgrade = "角色等级不足，无法升级！";

    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    public override void OnInit()
    {
        GetView().btn_Upgrade.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnUpgrade();
        }); 
        GetView().btn_Close.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIBoxUpgrade>();
        });
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
        maxLevel = CFG_BoxLevel.GetMaxLevel();
        RefreshUI();
    }

    private void RefreshUI()
    {
        cfgBoxLevelItem = CFG_BoxLevel.GetById(RoleM.Instance.BoxNowlevel);
        GetUpgredeState();

        ShowBoxLevelAndLimit();
        ShowQualityRate();
        ShowRoleLevelLimitTip();
        ShowButtonNameAndConsume();
        ShowUpgradeTime();
    }

    private void GetUpgredeState()
    {
        if (RoleM.Instance.BoxNextRecoveryTime > 0) // 升级中
        {
            upgradeState = UpgradeState.Upgrading;
        }
        else if (GameUtil.Instance.ComCoinToCheck(cfgBoxLevelItem.Consume[0].id, cfgBoxLevelItem.Consume[0].num, null, false)) // 检查道具是否够升级使用
        {
            upgradeState = UpgradeState.CanUpgrade;
        }
        else
        {
            upgradeState = UpgradeState.CannotUpgrade;
        }
    }

    /// <summary>
    /// 显示宝箱等级和开箱次数限制
    /// </summary>
    private void ShowBoxLevelAndLimit()
    {
        bool isMax = RoleM.Instance.BoxNowlevel >= maxLevel;
        int nextLv = isMax ? RoleM.Instance.BoxNowlevel : RoleM.Instance.BoxNowlevel + 1;

        GetView().txt_CurrentLevelValue.text = "当前等级：" + RoleM.Instance.BoxNowlevel;
        GetView().txt_NextLevelValue.text = "下一等级：" + nextLv;
        GetView().txt_CurrentOpenBoxLimit.text = cfgBoxLevelItem.Maxtime.ToString();
        GetView().txt_NextOpenBoxLimit.text = isMax ? cfgBoxLevelItem.Maxtime.ToString() : CFG_BoxLevel.GetById(nextLv).Maxtime.ToString();
    }

    /// <summary>
    /// 显示宝箱品质概率
    /// </summary>
    private void ShowQualityRate()
    {
        ShowQualityRateItem(QualityRateShowType.CurrentLevel, cfgBoxLevelItem.qualityrandom);
        if (RoleM.Instance.BoxNowlevel < maxLevel)
        {
            CFGItem_BoxLevel.QualityRandomItem[] nextLvRateItems = CFG_BoxLevel.GetById(RoleM.Instance.BoxNowlevel + 1).qualityrandom;
            ShowQualityRateItem(QualityRateShowType.NextLevel, nextLvRateItems);
        }
        else 
        {
            ShowQualityRateItem(QualityRateShowType.NextLevel, cfgBoxLevelItem.qualityrandom);
        }
    }

    private void ShowQualityRateItem(QualityRateShowType qualityRateShowType, CFGItem_BoxLevel.QualityRandomItem[] rateItems)
    {
        switch (qualityRateShowType)
        {
            case QualityRateShowType.CurrentLevel:
                for (int i = 0; i < GetView().txtList_CurrentQualityRandom.Count; i++)
                {
                    GetView().txtList_CurrentQualityRandom[i].text = GetRateString(i, rateItems);
                }
                break;
            case QualityRateShowType.NextLevel:
                for (int i = 0; i < GetView().txtList_NextQualityRandom.Count; i++)
                {
                    GetView().txtList_NextQualityRandom[i].text = GetRateString(i, rateItems);
                }
                break;
            default:
                break;
        }
    }

    private string GetRateString(int index, CFGItem_BoxLevel.QualityRandomItem[] qualityRandoms)
    {
        int totalRate = 0;
        foreach (var item in qualityRandoms)
        {
            totalRate += item.rate;
        }
        float rate = (qualityRandoms[index].rate / (float)totalRate) * 100;
        return rate.ToString("0.0") + "%";
    }

    TimeItem EggTime;

    private void ShowUpgradeTime()
    {
        ClearEggTime();
        int needTime = cfgBoxLevelItem.Needtime;

        switch (upgradeState)
        {
            case UpgradeState.CanUpgrade:
            case UpgradeState.CannotUpgrade:
                //显示配置的时间
                GetView().txt_UpgradeTime.text = GameUtil.Instance.GetTimeSToDHMS(needTime);
                break;

            case UpgradeState.Upgrading: //显示倒计时
                ShowTimeRemaining();
                if (RoleM.Instance.BoxNextRecoveryTime - GameUtil.Instance.GetCurrentTimeSeconds() <= 0) return;
                EggTime = GameHelper.Instance.AddInvoke(new TimeItem(-1, 1, ShowTimeRemaining, true, true));
                break;
            default:
                break;
        }

        void ShowTimeRemaining()
        {
           long _seconds = RoleM.Instance.BoxNextRecoveryTime - GameUtil.Instance.GetCurrentTimeSeconds();
            if (_seconds < 0) _seconds = 0;
            GetView().txt_UpgradeTime.text = GameUtil.Instance.GetTimeSToDHMS(_seconds);
            // 显示需要消耗的加速道券数量
            ShowButtonNameAndConsume();
        }

    }

    private void ClearEggTime()
    {
        if (GameHelper.Instance != null && EggTime != null)
        {
            GameHelper.Instance.RemoveInvoke(EggTime);
            EggTime = null;
        }
    }

    private void ShowRoleLevelLimitTip()
    {
        GetView().txt_CannotUpgrade.text = IsRoleLvEnough() ? null : $"角色等级不足，需要达到{cfgBoxLevelItem.Rolelevel}级。";
    }

    private bool IsRoleLvEnough()
    {
        return RoleM.Instance.Level >= cfgBoxLevelItem.Rolelevel;
    }

    /// <summary>
    /// 需要消耗的加速卷轴数量
    /// </summary>
    /// <returns></returns>
    private int GetConsume_JSJZ()
    {
        long seconds = RoleM.Instance.BoxNextRecoveryTime - GameUtil.Instance.GetCurrentTimeSeconds();
        return (int)Mathf.Ceil((float)seconds / CFG_XMLConfig.xmlConfig.BaseValue.BoxLevelUp_accelerate);
    }

    /// <summary>
    /// 显示升级宝箱需要的物品数量
    /// </summary>
    private void ShowButtonNameAndConsume()
    {
        if (upgradeState == UpgradeState.Upgrading)
        {
            GameUtil.Instance.SetItemIcon(CFG_XMLConfig.xmlConfig.BaseValue.BoxLevelUp_itemId, GetView().img_Coin);
            int needItemNum = GetConsume_JSJZ();
            GetView().txt_Coin.text = GameUtil.Instance.SetAndCheckStr(GameUtil.Instance.FormatNumWithUnit(needItemNum), CFG_XMLConfig.xmlConfig.BaseValue.BoxLevelUp_itemId, needItemNum) + " （持有：" + GameUtil.Instance.FormatNumWithUnit(RoleM.Instance.BoxQuick) + "）";
            GetView().txt_UpgradeButtonName.text = "加速";
            GetView().txt_ExchangeTime.text = $"（1张加速券={CFG_XMLConfig.xmlConfig.BaseValue.BoxLevelUp_accelerate / 60}分钟加速）";
        }
        else
        {
            GameUtil.Instance.SetItemIcon(cfgBoxLevelItem.Consume[0].id, GetView().img_Coin);
            GetView().txt_Coin.text = GameUtil.Instance.SetAndCheckStr(GameUtil.Instance.FormatNumWithUnit(cfgBoxLevelItem.Consume[0].num), cfgBoxLevelItem.Consume[0].id, cfgBoxLevelItem.Consume[0].num);
            GetView().txt_UpgradeButtonName.text = "升级";
        }
        GetView().txt_ExchangeTime.gameObject.SetActive(upgradeState == UpgradeState.Upgrading);

    }

    private void OnUpgrade()
    {
        if (StateM.Instance.isSendUpgradeOrSpeedUpBox) return;

        StateM.Instance.isSendUpgradeOrSpeedUpBox = true;

        if (RoleM.Instance.BoxNowlevel >= maxLevel)
        {
            UITool.Open<UIMessageBoxOne>(null, new UIMessageBoxOne.CallBack()
            {
                showStr = "宝箱等级已达到最大，无需升级。",
            });
            return;
        }

        if (!IsRoleLvEnough())
        {
            UITool.Open<UIMessageBoxOne>(null, new UIMessageBoxOne.CallBack()
            {
                showStr = $"角色等级不足，需要达到{cfgBoxLevelItem.Rolelevel}级。",
            });
            return;
        }

        // 升级中, 点击可加速
        if (upgradeState == UpgradeState.Upgrading)
        {
            //int needItemNum = GetConsume_JSJZ();
            //if (GameUtil.Instance.ComCoinToCheck(CFG_XMLConfig.xmlConfig.BaseValue.BoxLevelUp_itemId, needItemNum, () => { UITool.Close<UIBoxUpgrade>(); }, true))
            //{
            //    TcpC.C2S_HomeUpgradeBoxQuick(needItemNum);
            //}
            UITool.Open<UICommonItemUse>(null, CFG_XMLConfig.xmlConfig.BaseValue.BoxLevelUp_itemId);
        }
        else // 检查是否可以升级
        {
            if (GameUtil.Instance.ComCoinToCheck(cfgBoxLevelItem.Consume[0].id, cfgBoxLevelItem.Consume[0].num, null, false))
            {
                TcpC.C2S_HomeUpgradeBox();
            }
            else
            {
                UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
                {
                    ShowStr = "金币不足，是否前往商店购买金币？",
                    YesFun = () => {
                        UITool.Open<UIShop>(null, CFG_Store.StoreType.Coupon);
                    },
                });
            }
        }
    }

    [Subscriber(ThreadMode.Main)]
    private void OnRefresh_CommonResponse(GameEvent.CommonResponse _e)
    {
        if (isNullGo) return;

        if (_e.code == 0 && _e.cid == CmdId.C2S_HomeUpgradeBoxQuick)
        {
            ShowButtonNameAndConsume();
            StateM.Instance.isSendUpgradeOrSpeedUpBox = false;
        }

        if (_e.code == 0 && _e.cid == CmdId.C2S_HomeUpgradeBox)
        {
            StateM.Instance.isSendUpgradeOrSpeedUpBox = false;
        }
    }

    [Subscriber(ThreadMode.Main)]
    // 更新时间状态
    private void OnSync_UserBase(GameEvent.Sync_UserBoxInfo _e)
    {
        if (isNullGo) return;
        RefreshUI();
    }

    [Subscriber(ThreadMode.Main)]
    // 更新时间状态
    private void OnSync_FocusChanged(GameEvent.OnApplicationFocus _e)
    {
        if (isNullGo || !_e.focus) return;
        RefreshUI();
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
        ClearEggTime();
        cfgBoxLevelItem = null;
    }

    public override void OnDestroy()
    {
        ClearEggTime();
    }
}