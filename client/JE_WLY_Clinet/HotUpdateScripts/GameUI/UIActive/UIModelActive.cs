using HotUpdateScripts.ProtoM;
using MetaFramework.UI;
using pb;
using System.Collections.Generic;
using UnityEngine;

public class UIModelActive : IUIModel
{
    public struct ActiveTitle 
    {
        public ActivityType activeType;
        public int cfgId;
        public bool ActiveOpen;
        public GameObject BtnOn;
        public string RedKey;
        public string activeName;
        public int shopType;
    }

    public Dictionary<ActivityType, ActiveTitle> ActiveTitlesDic = new Dictionary<ActivityType, ActiveTitle>()
    {
        [ActivityType.Sign7] = new ActiveTitle
        {
            activeType = ActivityType.Sign7,
            cfgId = 1,
            ActiveOpen = ActivityM.Instance.GetAcitvityState(ActivityType.Sign7),
            RedKey = RedPointConst.ActiveCenter_Sign7.ToString(),
            activeName = "∆ﬂ»’«©µΩ",
        },
        [ActivityType.SignHappy7] = new ActiveTitle
        {
            activeType = ActivityType.SignHappy7,
            cfgId = 19,
            ActiveOpen = ActivityM.Instance.GetAcitvityState(ActivityType.SignHappy7),
            RedKey = TaskClass.SEVENDAY.ToString(),
            activeName = "∆ﬂ»’øÒª∂",
            shopType = 25,
        }
    };
}