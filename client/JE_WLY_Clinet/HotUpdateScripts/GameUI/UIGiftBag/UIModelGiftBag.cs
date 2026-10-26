using HotUpdateScripts.ProtoM;
using MetaFramework.UI;
using System.Collections.Generic;
using UnityEngine;

public class UIModelGiftBag : IUIModel
{
    public struct ActiveTitle
    {
        public ActivityType activeType;
        public int cfgId;
        public bool ActiveOpen;
        public GameObject BtnOn;
        public string RedKey;
        public string activeName;
        public string iconName;
        public int shopType;
    }

    public Dictionary<ActivityType, ActiveTitle> ActiveTitlesDic = new Dictionary<ActivityType, ActiveTitle>()
    {
        [ActivityType.DailyGift] = new ActiveTitle
        {
            activeType = ActivityType.DailyGift,
            cfgId = 13,
            ActiveOpen = true,
            RedKey = RedPointConst.GiftBag_DailyGift.ToString(),
            activeName = "每日礼包",
            shopType = 20,
            iconName = "bg_Gift_Package_banner_1",
        },
        [ActivityType.GiftPay] = new ActiveTitle
        {
            activeType = ActivityType.GiftPay,
            cfgId = 4,
            ActiveOpen = true,
            RedKey = RedPointConst.GiftBag_GiftPay.ToString(),
            activeName = "每日特价", // 原特惠礼包
            shopType = 7,
            iconName = "bg_Gift_Package_banner_3",
        },
        [ActivityType.GrowthFund] = new ActiveTitle
        {
            activeType = ActivityType.GrowthFund,
            cfgId = 15,
            ActiveOpen = true,
            RedKey = RedPointConst.GiftBag_GrowthFund.ToString(),
            activeName = "成长基金"
        },
        [ActivityType.PartnerGift] = new ActiveTitle
        {
            activeType = ActivityType.PartnerGift,
            cfgId = 11,
            ActiveOpen = true,
            RedKey = RedPointConst.GiftBag_PartnerGift.ToString(),
            activeName = "伙伴礼包",
            shopType = 17,
            iconName = "bg_Gift_Package_banner_4",
        },
        [ActivityType.PetGift] = new ActiveTitle
        {
            activeType = ActivityType.PetGift,
            cfgId = 12,
            ActiveOpen = true,
            RedKey = RedPointConst.GiftBag_PetGift.ToString(),
            activeName = "宠物礼包",
            shopType = 18,
            iconName = "bg_Gift_Package_banner_2",
        },
        [ActivityType.Month] = new ActiveTitle
        {
            activeType = ActivityType.Month,
            ActiveOpen = false,
            RedKey = RedPointConst.GiftBag_Month.ToString(),
            activeName = "月卡",
            iconName = "",
        },
        [ActivityType.Gemstone] = new ActiveTitle
        {
            activeType = ActivityType.Gemstone,
            cfgId = 14,
            ActiveOpen = true,
            RedKey = RedPointConst.GiftBag_Gemstone.ToString(),
            activeName = "魂石礼包",
            shopType = 21,
            iconName = "bg_Gift_Package_banner_5",
        },
    };
}