using JEngine.Helper;
using JEngine.Singleton;
using MetaFramework.UI;

using pb;
using Spine;

using System;
using System.Collections.Generic;
using UnityEngine;

using static UIBoxUpgrade;

namespace HotUpdateScripts.ProtoM
{
    /// <summary>
    /// 玩家总信息
    /// </summary>
    public class RoleM : SingletonTemplate<RoleM>
    {
        public string Nickname { get; set; }
        public long Uid { get; set; }
        public string HeadUrl { get; set; }
        public int Sex { get; set; }
        public string IP { get; set; }
        public string Post { get; set; }

        public string Token { get; set; }

        public string Login_Token { get; set; }

        /// <summary>
        /// 战魂 游戏内的兑换货币，对应钻石
        /// </summary>
        public long Diamanta { get; set; }

        /// <summary>
        /// 战魂 游戏内的兑换货币，对应钻石
        /// </summary>
        public long Soul { get; set; }

        /// <summary>
        /// 点券 游戏内的充值货币，对应点券
        /// </summary>
        public long Coupon { get; set; }
        /// <summary>
        /// 体力 进入副本消耗
        /// </summary>
        public long Energy { get; set; } 
        /// <summary>
        /// 职业
        /// </summary>
        public int Job { get; set; }

        /// <summary>
        /// 经验
        /// </summary>
        public long Exp { get; set; }

        /// <summary>
        /// 等级
        /// </summary>
        public int Level { get; set; } 

        /// <summary>
        /// vip等级
        /// </summary>
        public int Vip { get; set; }

        /// <summary>
        /// vip经验
        /// </summary>
        public long VipExp { get; set; }

        /// <summary>
        /// 下次体力恢复剩余时间
        /// </summary>
        public int EnergyRecoveryTime { get; set; }

        /// <summary>
        /// 金币 游戏内的通用货币
        /// </summary>
        public long Coin { get; set; }

        /// <summary>
        /// 个人战力
        /// </summary>
        public int Combat { get; set; }

        /// <summary>
        /// 总战力
        /// </summary>
        public int TotalCombat { get; set; }

        /// <summary>
        /// 命格等级
        /// </summary>
        public int FateLevel { get; set; }

        /// <summary>
        /// 命格经验
        /// </summary>
        public int FateExp { get; set; }

        /// <summary>
        /// 试炼凭证
        /// </summary>
        public long TrialVoucher { get; set; }

        /// <summary>
        /// 宝箱数量
        /// </summary>
        public long TreasuerBox { get; set; }


        /// <summary>
        /// 新手引导
        /// </summary>
        public bool isGuiding { get; set; }

        /// <summary>
        /// 新手引导步骤
        /// </summary>
        public int GuideId { get; set; }

        /// <summary>
        /// 引导已完成的类型列表
        /// </summary>
        public List<int> GuideTypes { get; set; }

        /// <summary>
        /// 开箱数据缓存
        /// </summary>
        public List<ItemInfo> OpenBoxItems { get; set; }

        /// <summary>
        /// 开箱当前等级
        /// </summary>
        public int BoxNowlevel { get; set; }

        /// <summary>
        /// 宝箱升级结束时间
        /// </summary>
        public long BoxNextRecoveryTime {  get; set; }

        /// <summary>
        /// 开箱剩余时间
        /// </summary>
        public int BoxTimeAllSecound { get; set; }

        /// <summary>
        /// 宠物可用经验数值
        /// </summary>
        public long PetUsedExp { get; set; }

        /// <summary>
        /// 加速卷轴道具
        /// </summary>
        public long BoxQuick { get; set; }

        /// <summary>
        /// 所在的行会ID
        /// </summary>
        public long GuildId { get; set; }

        /// <summary>
        /// 工会经验
        /// </summary>
        public long GuildContribution { get; set; }

        /// <summary>
        /// 工会许愿点
        /// </summary>
        public long GuildWish { get; set; }

        /// <summary>
        /// 工会许愿点恢复时间
        /// </summary>
        public int WishRecoveryTime { get; set; }

        /// <summary>
        /// 默认开箱装备等级
        /// </summary>
        public int NowOpenLevel { get; set; }

        /// <summary>
        /// 默认开箱装备职业
        /// </summary>
        public int NowOpenJob { get; set; }

        /// <summary>
        /// 是否收到了引导数据
        /// </summary>
        public bool IsReceivedGuideData { get; set; }


        /// <summary>
        /// 角色登录时的初始等级，用于判断伙伴引导
        /// </summary>
        public int guidePartnerRecordLevel { get; set; }

        /// <summary>
        /// 是否为登录后第一次收到角色基础信息
        /// </summary>
        public bool IsFirstReceiveBaseInfo { get; set; } = true;

        /// <summary>
        /// 回合制阵容占位
        /// </summary>
        /// 
        public long[] TurnBasedUids { get; set; }

        private Dictionary<int,BuffInfo> BuffInfos = new Dictionary<int, BuffInfo>();

        public void RefreshBuffInfo(List<BuffInfo> buffs)
        {
            if (buffs != null)
            {
                BuffInfos.Clear();
                for (int i = 0; i < buffs.Count; i++)
                {
                    BuffInfos.Add(buffs[i].BuffId, buffs[i]);
                }
            }

        }

        public void AddBuffInfo(BuffInfo buff)
        {
            if (!BuffInfos.ContainsKey(buff.BuffId))
            {
                BuffInfos.Add(buff.BuffId, buff);
            }
            else
            {
                BuffInfos[buff.BuffId] = buff;
            }
        }

        public void RemoveBuffInfo(int buffID)
        {
            if (BuffInfos.ContainsKey(buffID))
            {
                BuffInfos.Remove(buffID);
            }
        }

        public bool CheakBuffId(int buffID)
        {
            return BuffInfos.ContainsKey(buffID);
        }

        public bool CheakBuffId(params int[] buffIDs)
        {
            for (int i = 0; i < buffIDs.Length; i++)
            {
                if (BuffInfos.ContainsKey(buffIDs[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public long GetBuffOwnerUid(int buffID)
        {
            if (BuffInfos.ContainsKey(buffID))
            {
                return BuffInfos[buffID].OwnerUid;
            }
            return 0;
        }

        public void SetSelectRole(HTTP_CharacterInfo selctInfo)
        {
            Uid = selctInfo.uid;
            HeadUrl = selctInfo.headUrl;
            Sex = selctInfo.sex;
            Nickname = selctInfo.name;
            Coin = selctInfo.coin;
            Soul = selctInfo.soul;
            Coupon = selctInfo.coupon;
            Energy = selctInfo.energy;
            Job = selctInfo.job;
            Exp = selctInfo.exp;
            Level = selctInfo.level;
            Vip = selctInfo.vip;
            VipExp = selctInfo.recharge;
        }

        /// <summary>
        /// 登录返回信息
        /// </summary>
        public List<HTTP_CharacterInfo> LoginCharacterInfo { get; set; }
        /// <summary>
        /// Http 获取个人信息
        /// </summary>
        /// <param name="_loginRes"></param>
        public void SetHttpGetInfo(HTTP_LoginResponse _loginRes )
        {
            LoginCharacterInfo = _loginRes.characterList;
            Login_Token = _loginRes.token.ToString();
        }

        public Dictionary<pb.Attribute, long> RoleAttributeDic = new Dictionary<pb.Attribute, long>();

        public List<Stats> Attributes = new List<Stats>();
        /// <summary>
        /// 设置角色属性
        /// </summary>
        /// <param name="_Attributes"></param>
        public void SetRoleStats(List<Stats> _Attributes)
        {
            RoleAttributeDic.Clear();
            Attributes = _Attributes;
            for (int i = 0; i < _Attributes.Count; i++)
            {
                RoleAttributeDic.Add(_Attributes[i].attr, _Attributes[i].Num);
            }
        }

        public List<Stats> GetRoleAllStats()
        {
            return Attributes;
        }

        //获取角色属性数值
        public long GetRoleAttrValueByAttr(pb.Attribute _Attributes)
        {
            long _value = 0;
            if (!RoleAttributeDic.TryGetValue(_Attributes, out _value) )
            {
                Debug.LogWarning($"属性{_Attributes}为空");
            }
            return _value;
        }

        //角色装备信息
        public Dictionary<EquipSlot, SlotInfo> RoleEquipDic = new Dictionary<EquipSlot, SlotInfo>();

        public void SetRoleEquipDic(List<SlotInfo> equipInfo)
        {
            RoleEquipDic.Clear();
            for (int i = 1; i < (int)EquipSlot.MaxEquipSlot; i++)
            {
                RoleEquipDic.Add((EquipSlot)i, equipInfo[i]);
            }
        }

        public bool isEquip(int equipSlot)
        {
            return RoleEquipDic.ContainsKey((EquipSlot)equipSlot);
        }

        //刷新某个部位的装备
        public void SetRoleOneEquip(ItemInfo ItemInfo, EquipSlot equipSlot,bool isAdd)
        {
            if (isAdd)
            {
                if (RoleEquipDic.ContainsKey(equipSlot))
                {
                    RoleEquipDic[equipSlot].Item = ItemInfo;
                }
            }
            else
            {
                if (RoleEquipDic.ContainsKey(equipSlot))
                {
                    RoleEquipDic[equipSlot].Item = default;
                }
            }
        }

        //刷新某个部位的宝石镶嵌
        public void SetRoleOneEquipInset(int cid, EquipSlot equipSlot,int insetIdx, bool isAdd)
        {
            if (isAdd)
            {
                if (RoleEquipDic.ContainsKey(equipSlot))
                {
                    RoleEquipDic[equipSlot].Gemstone[insetIdx] = cid;
                }
            }
            else
            {
                if (RoleEquipDic.ContainsKey(equipSlot))
                {
                    RoleEquipDic[equipSlot].Gemstone[insetIdx] = 0;
                }
            }
        }

        //检测该部位是为空装备
        public bool CheckIsNullEquip(EquipSlot eSlot)
        {
            if (!RoleEquipDic.ContainsKey(eSlot))
            {
                return true;
            }
            return RoleEquipDic[eSlot].Item == null ||  RoleEquipDic[eSlot].Item.CfgId == 0;
        }

        public bool CheckIsBestEquip(EquipSlot eSlot)
        {
            if (CheckIsNullEquip(eSlot))
            {
                return false;
            }
            return RoleEquipDic[eSlot].Item.Equip != null && RoleEquipDic[eSlot].Item.Equip.Attributes.Count != 0;
        }

        //获取某个部位的信息
        public SlotInfo GetSlotInfoBySolt(EquipSlot eSlot)
        {
            return RoleEquipDic[eSlot];
        }

        //设置装备强化
        public void SetEquipeReinforeceLv(EquipSlot equipSlot, int Lv)
        {
            RoleEquipDic[equipSlot].Enhance = Lv;
        }

        //设置装备升星
        public void SetEquipeStarLv(EquipSlot equipSlot, int Lv)
        {
            RoleEquipDic[equipSlot].Stars = Lv;
        }

        //获取全身装备的最小等级（用来比较强化大师）
        public int GetEquipReinforce()
        {
            int reinforceLv = 9999;
            for (int i = 1; i < (int)EquipSlot.MaxEquipSlot; i++)
            {
                if (RoleEquipDic[(EquipSlot)i].Enhance < reinforceLv)
                {
                    reinforceLv = RoleEquipDic[(EquipSlot)i].Enhance;
                }
            }
            return reinforceLv;
        }

        //获取全身装备的最小星级（用来比较升星大师）
        public int GetEquipStar()
        {
            int Lv = 9999;
            for (int i = 1; i < (int)EquipSlot.MaxEquipSlot; i++)
            {
                if (RoleEquipDic[(EquipSlot)i].Stars < Lv)
                {
                    Lv = RoleEquipDic[(EquipSlot)i].Stars;
                }
            }
            return Lv;
        }


        #region PVP

        public class PVPClass
        {
            public MatchType matchType;
            /// <summary>
            /// 今日一挑战次数
            /// </summary>
            public int DailyMatchTimes;

            /// <summary>
            /// 今日已购买次数
            /// </summary>
            public int DailyBuyNum;

            public int CanMatchCount;
        }

        private Dictionary<MatchType, PVPClass> PVPDic = new Dictionary<MatchType, PVPClass>();

        public void SetAddPVPInfo(MatchType _matchType,int dailyMatchTimes,int dailyBuyNum)
        {
            if (!PVPDic.ContainsKey(_matchType))
            {
                PVPDic.Add(_matchType, new PVPClass { matchType = _matchType });
            }
            PVPDic[_matchType].DailyBuyNum = dailyBuyNum;
            PVPDic[_matchType].DailyMatchTimes = dailyMatchTimes;

            int dailyTime = _matchType == MatchType.SINGLE_COMBAT ? CFG_XMLConfig.xmlConfig.PVPInfo.dailyTime : CFG_XMLConfig.xmlConfig.GVGInfo.dailyTime;
            PVPDic[_matchType].CanMatchCount = dailyTime + dailyBuyNum - dailyMatchTimes;
        }

        //获取可挑战次数
        public int GetPVPMatchTimes(int _matchType)
        {
            if (!PVPDic.ContainsKey((MatchType)_matchType))
                return 0;
            else
                return PVPDic[(MatchType)_matchType].CanMatchCount;
        }

        public int GetPVPRedNum()
        {
            if (RoleM.Instance.Level < CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Play_PVP])
                return 0;
            if (RoleM.Instance.GetPVPOneSeason() == null)
                return 0;

            if ( !JEngine.Helper.GameUtil.Instance.CheakCurTime(CFG_XMLConfig.xmlConfig.PVPInfo.dailyBegin, CFG_XMLConfig.xmlConfig.PVPInfo.dailyEnd))
                return 0;

            return GetPVPMatchTimes((int)MatchType.SINGLE_COMBAT);
        }

        public int GetGVGRedNum()
        {
            if (RoleM.Instance.Level < CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[CFG_XMLConfig.XML_Config.SwitchOpne.Play_GVG])
                return 0;

            if (!JEngine.Helper.GameUtil.Instance.CheakCurTime(CFG_XMLConfig.xmlConfig.GVGInfo.dailyBegin, CFG_XMLConfig.xmlConfig.PVPInfo.dailyEnd))
                return 0;

            return GetPVPMatchTimes((int)MatchType.FREE_FOR_ALL);
        }

        //检测是否可以购买
        public int GetCanNumBuyPVP(int _matchType)
        {
            int storeId = (MatchType)_matchType == MatchType.SINGLE_COMBAT ? CFG_XMLConfig.xmlConfig.PVPInfo.storeId : CFG_XMLConfig.xmlConfig.GVGInfo.storeId;
            return CFG_Store.GetById(storeId).Limite.limitnum - PVPDic[(MatchType)_matchType].DailyBuyNum;
        }
        
        public CFG_XMLConfig.XML_Config.XML_Item_PVP.PVPseason GetPVPOneSeason()
        {
            foreach (var item in CFG_XMLConfig.xmlConfig.PVPInfo.PVPSeasonDic.Values)
            {
                if (JEngine.Helper.GameUtil.Instance.CheakCurTimeDay(item.starttime,item.endtime))
                    return item;
            }
            return null;
        }



        #endregion


        #region Train

        public class TrainClass 
        {
            /// <summary>
            /// 是否可以领取
            /// </summary>
            public bool Ready;
            public List<ItemInfo> Reward;

            /// <summary>
            /// 已完成
            /// </summary>
            public int Layers;
        }

        private TrainClass _trainClass = new TrainClass();

        public void SetTrainInfo(bool _ready,int _layer,List<ItemInfo> _reward)
        {
            _trainClass.Ready = _ready;
            _trainClass.Layers = _layer;
            _trainClass.Reward = _reward;
        }

        public int GetTrainLayers()
        {
            return _trainClass.Layers;
        }

        public TrainClass GetTrainInfo()
        {
            return _trainClass;
        }


        #endregion


        #region 每日必做
        private Dictionary<int, int> ChallengeDic = new Dictionary<int, int>();

        public void AddChallengeInfo(List<ChallengeInfo> list)
        {
            ChallengeDic.Clear();
            foreach (var item in list)
            {
                ChallengeDic.Add((int)item.MType, item.Times);
            }
        }

        public int GetCanTimeByRoomType(int roomType)
        {
            int allCount = CFG_XMLConfig.xmlConfig.DayRoomDic[(CFG_Room.RoomType)roomType].challengeTimeNum + CFG_VIP.GetAddRoomCount(Vip, roomType);
            return ChallengeDic.ContainsKey(roomType) ? allCount - ChallengeDic[roomType] : allCount;
        }

        #endregion


        #region 新手引导
        public void SetGuide(UIGuide.GuideData data)
        {
            if (UITool.IsOpen<UIGuide>())
                UITool.Get<UIGuide>().StartGuide(data);
            else
                UITool.Open<UIGuide>(null, data);
        }

        public void SaveGuide()
        {
            TcpC.C2S_GuidanceUpdateRequest(GuideTypes);
        }

        public bool IsGuiding(CFG_Guidance.GuideType guideType)
        {
            return isGuiding && (CFG_Guidance.GuideType)CFG_Guidance.GetType(GuideId) == guideType;
        }

        #endregion

        /// <summary>
        /// 是否加入了行会
        /// </summary>
        /// <returns></returns>
        public bool IsJoinedGuild()
        {
            return GuildId != 0;
        }


        /// <summary>
        /// 是否解锁了伙伴槽位
        /// </summary>
        /// <returns></returns>
        public bool IsUnlockedPartnerSlot()
        {
            return Level >= CFG_XMLConfig.xmlConfig.OpenSlotDic[1];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public int GetDefaultOpenBoxEquipLevel()
        {
            return NowOpenLevel == 0 ? Level : NowOpenLevel;
        }

        /// <summary>
        /// 获取默认开箱装备职业。
        /// 只在开箱请求时传参使用，宝箱设置界面默认值需要显示默认全部。
        /// </summary>
        /// <returns></returns>
        public int GetDefaultOpenBoxEquipJob()
        {
            int unlockedJob = IsUnlockedPartnerSlot() ? NowOpenJob : Job;
            return unlockedJob;
            //return true ? NowOpenJob : unlockedJob;
            //return ActivityM.Instance.IsBuyFirstPayGift() ? NowOpenJob : unlockedJob;
        }

        /// <summary>
        /// 宝箱是否可以升级
        /// </summary>
        /// <returns></returns>
        public bool IsCanUpgradeBox()
        {
            if (BoxNowlevel == 0)
                return false;

            var cfgBoxLevelItem = CFG_BoxLevel.GetById(BoxNowlevel);
            return BoxNextRecoveryTime <= 0 && RoleM.Instance.Level >= cfgBoxLevelItem.Rolelevel && GameUtil.Instance.ComCoinToCheck(cfgBoxLevelItem.Consume[0].id, cfgBoxLevelItem.Consume[0].num, null, false);
        }

        public void CheckBoxLevelRed()
        {
            RedPointManager.Instance.RPS.Dispatch(RedPointConst.BoxLevel.ToString(), IsCanUpgradeBox() ? 1 : 0);
        }
    }

}

