using HotUpdateScripts.ProtoM;

using pb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 定义广播监听自定义类或者结构体
/// </summary>
public class GameEvent
{
    //用户信息刷新
    public struct Sync_UserBase { };

    //玩家属性同步
    public struct Sync_UserStatsPush { };

    //玩家高级信息同步
    public struct Sync_UserAdvanceInfoPush { };

    //邮件刷新
    public struct Refresh_MailUI { };

    //活动状态开关
    public struct Refresh_ActiveState { };

    //装备穿戴
    public struct ChangeEquip {
        public ItemInfo item;
        public int soldIdx;
    };

    //任务刷新
    public struct Refresh_Task { public bool IsErr; };

    //洗练返回
    public struct Refresh_EquipRefine {
        public pb.Stats PbArrt;
    };

    //资源分解
    public struct Sync_TakeSource { }


    //洗练选择
    public struct Replace_EquipRefine
    {
    };

    //强化 装备
    public struct Replace_EquipEnhance { };

    //升星
    public struct Replace_EquipStar {
        public bool isUp;
    };

    //换名
    public struct Sync_ChangeName
    {
        public bool isWin;
    };

    public struct Sync_ComposeResponse
    {
        public pb.ComposeResponse data;
    };

    public struct Sync_HomeAutoEncounter
    {
        public pb.HomeAutoEncounterPush data;
    };

    public struct Sync_HomeOpenBox
    {
        public pb.HomeOpenBoxResponse data;
    };

    public struct Refresh_HomeOpen
    {
    };

    public struct Refresh_ShowBoxUI { };

    public struct CommonResponse
    {
        public int code;
        public CmdId cid;
    };


    //接收聊天
    public struct Sync_ChatPush { public bool isOnlyHall; };

    //聊天发送返回
    public struct Sync_ChatSendBack { public bool isWin; };

    //技能 升级
    public struct Sync_SkillUpgrade { public bool isWin; }

    //技能 升阶
    public struct Sync_SkillAdvanced { public bool isWin; }

    //技能 装备
    public struct Sync_SkillChange { }

    //新技能添加
    public struct Sync_GetNewSkill { }

    //PVP 信息获取
    public struct Sync_PVPInfo { }

    //无尽试炼
    public struct Sync_Endlesstrials { }

    //战斗 死亡
    public struct Fight_Die {

        public long id;
    }

    /// <summary>
    /// 战斗复活
    /// </summary>
    public struct Fight_Revive
    {
        public long id;
    }

    //怪物加入
    public struct Fight_MonsterAdd { }

    //战斗血量变化
    public struct Fight_HPChange
    {
        public long tid;
        public int changeNum;
        public bool Miss;
        public bool Critical;
        public bool isTurnBased;
    }

    //技能刷新冷却
    public struct Fight_SkillRefresh
    {
        public int cfgId;
    }

    //匹配结果
    public struct PVP_MatchResult
    {
        public List<pb.PlayerInfo> Enemys;
        public bool IsBegin;
    }

    //实时击杀信息
    public struct PVP_MeleeKillNum { public List<pb.KillInfo> killInfos; }

    // 回合制阵容信息
    public struct PVP_TurnBasedSlot { public long[] uids; }

    // 回合制战斗信息
    public struct PVP_TurnBasedChallenge { public TurnBasedBattle info; }

    public struct PVP_TurnBasedRoundShow { public int currentRound; public int totalRound; }

    /// <summary>
    /// 回合开始后，进入开启跳过战斗功能倒计时
    /// </summary>
    public struct PVP_JumpGameCountdown { }

    public struct ShopInfo_Get { };

    //魂石 镶嵌
    public struct EquipInsetRefresh { };

    //Boss信息
    public struct Check_WorldBoss {
        public int HP;
        public int UsedCount;
    };

    //评分
    public struct DailyMap_Score
    {
        public long TotalDamage;
        public string Scoring;
        public int Round;
        public int MonsterNum;
    }

    //每日必做
    public struct Daily_MapInfo { }

    //自动分解
    public struct AutoDelSet { }

    /// <summary>
    /// 伙伴列表刷新
    /// </summary>
    public struct Refresh_PartnerList {
        public bool isOne;
    }

    /// <summary>
    /// 伙伴槽位刷新
    /// </summary>
    public struct Refresh_PartnerSlot { }


    /// <summary>
    /// 宠物刷新
    /// </summary>
    public struct Refresh_PetsInfo { }

    /// <summary>
    /// 宠物合成位移
    /// </summary>
    public struct Refresh_PetMove {
        public int petCfgId;
        public int idx;
    }

    //宠物升星
    public struct Refresh_PetStar
    {
        public int Code;
        public int ResPer;
    }

    //副本信息刷新
    public struct Refresh_InstanceInfo { }

    public struct Sync_UserBoxInfo { }

    public struct Sync_UserSkillSlot { }

    // 工会列表
    public struct Sync_GuildList
    {
        public List<GuildInfo> guildsInfo;
    }

    // 创建工会
    public struct Sync_GuildCreate { }

    // 申请加入工会
    public struct Sync_GuildApply { }

    // 取消申请加入工会
    public struct Sync_GuildCancelApply { }

    // 解散工会
    public struct Sync_GuildDismiss { }

    // 离开工会
    public struct Sync_GuildLeave { }

    // 剔除工会
    public struct Sync_GuildKick { }

    // 提升工会职位
    public struct Sync_GuildPromote { }

    // 申请列表
    public struct Sync_GuildApplyList
    {
        public List<GuildApplyInfo> applyList;
    }

    // 处理申请
    public struct Sync_GuildHandleApply { }

    // 工会信息
    public struct Sync_GuildInfo
    {
        public GuildInfo guildInfo;
        public int Role;
    }

    // 工会成员列表
    public struct Sync_GuildMemberList
    {
        public List<GuildMemberInfo> membersInfo;
    }

    // 工会捐献信息
    public struct Sync_GuildDonateInfo { public int num; }

    // 工会商城购买情况
    public struct Sync_GuildShopBuyInfo
    {
        public List<GuildShopBuyInfo> buyInfo;
    }

    /// <summary>
    /// 工会副本奖励 
    /// </summary>
    public struct Refrsh_GuildCopyReward { };

    /// <summary>
    /// 观察玩家详情
    /// </summary>
    public struct Sync_ObservePlayerInfo 
    {
        public ObservePlayerInfo observePlayerInfo;
    }

    /// <summary>
    /// 刷新大厅活动按钮显示吟唱
    /// </summary>
    public struct Refresh_HallActivityButton
    {
        public int index;
        public bool forceHide;
    }

    /// <summary>
    /// 升级
    /// </summary>
    public struct Refresh_LvUp
    {
        public long uid;
    }

    /// <summary>
    /// 掉落
    /// </summary>
    public struct Refresh_ItemDrop { }

    /// <summary>
    /// 刷新零点检测
    /// </summary>
    public struct Refresh_ZeroTime { }

    /// <summary>
    /// App 焦点切换
    /// </summary>
    public struct OnApplicationFocus
    {
        public bool focus;
    }

    /// <summary>
    /// App 前后台切换
    /// </summary>
    public struct OnApplicationPause
    {
        public bool pause;
    }

    public struct BackUIHall { public bool isInHall; }

    public struct Refresh_Ping { public int ping; }

    public struct Refresh_Buffer { public long Uid; public bool isAdd; }

}

/// <summary>
/// 资源变动
/// </summary>
public struct Event_Source_Refresh { 
    public List<pb.ItemInfo> _ItemInfo;
    public bool isChange;
}
