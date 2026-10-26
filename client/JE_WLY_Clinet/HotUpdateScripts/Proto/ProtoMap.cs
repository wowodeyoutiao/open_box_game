using System;
using System.Collections.Generic;
using JEngine;
using JEngine.Core;
using JEngine.MyNet;
using JEngine.Singleton;

namespace pb
{
    /// <summary>
    /// 固定协议Map
    /// </summary>
    public class ProtoMap : SingletonTemplate<ProtoMap>
    {
        private static Dictionary<short, Action<byte[]>> ProtoDic = new Dictionary<short, Action<byte[]>> {

            //登录 id
            [(short)CmdId.C2S_Login] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<LoginToGameServerResponse>(bytes);
            },

            //玩家进入
            [(short)CmdId.C2S_Join] = (byte[] bytes) => {                    
                TCPClientMgr.ProtoDeSerializePost<JoinDeskResponse>(bytes);
            },

            //玩家退出
            [(short)CmdId.C2S_Exit] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ExitResponse>(bytes);
            },

            //移动（主动） C2S_Move 
            [(short)CmdId.C2S_Move] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MoveResponse>(bytes);
            },

            //攻击（主动） C2S_Attack 
            [(short)CmdId.C2S_Attack] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<AttackResponse>(bytes);
            },

            //使用道具  
            [(short)CmdId.C2S_ItemUse] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ItemUseResponse>(bytes);
            },

            //销毁道具  
            [(short)CmdId.C2S_ItemDestroy] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ItemDestroyResponse>(bytes);
            },


            //玩家进入
            [(short)CmdId.S2C_PlayerEnter] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PlayerEnterDeskPush>(bytes);
            },

            //玩家退出
            [(short)CmdId.S2C_PlayerExit] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ExitPush>(bytes);
            },


            //移动
            [(short)CmdId.S2C_Move] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MovePush>(bytes);
            },

            //攻击
            [(short)CmdId.S2C_Attack] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<AttackPush>(bytes);
            },

            //怪物进入
            [(short)CmdId.S2C_MonsterIn] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MonsterInPush>(bytes);
            },

            //怪物消失
            [(short)CmdId.S2C_MonsterOut] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MonsterOutPush>(bytes);
            },

            //活动对象死亡
            [(short)CmdId.S2C_ObjectDie] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ObjectDiePush>(bytes);
            },


            //房间内断线重连
            [(short)CmdId.S2C_ReJoinDesk] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ReJoinDeskPush>(bytes);
            },

            //血量变化
            [(short)CmdId.S2C_ObjectHpChange] = (byte[] bytes) =>
            {
                TCPClientMgr.ProtoDeSerializePost<ObjectHpChangePush>(bytes);
            },

            //技能冷却
            [(short)CmdId.S2C_SkillCooldown] = (byte[] bytes) =>
            {
                TCPClientMgr.ProtoDeSerializePost<SkillCooldownPush>(bytes);
            },

            //角色升级
            [(short)CmdId.S2C_LevelUp] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<LevelUpPush>(bytes);
            },

            //角色基础信息
            [(short)CmdId.S2C_SyncUserBase] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SyncUserBasePush>(bytes);
            },

            //同步角色属性
            [(short)CmdId.S2C_SyncUserStats] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SyncUserStatsPush>(bytes);
            },

            //同步角色属性
            [(short)CmdId.S2C_SyncUserAdvanceInfo] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SyncUserAdvanceInfoPush>(bytes);
            },

            //add buffer
            [(short)CmdId.S2C_AddBuff] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<AddBuffPush>(bytes);
            },

            // Buff列表 S2C_BuffList
            [(short)CmdId.S2C_BuffList] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<BuffListPush>(bytes);
            },

            [(short)CmdId.S2C_RemoveBuff] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<RemoveBuffPush>(bytes);
            },

            //复活 C2S_Revive
            [(short)CmdId.C2S_Revive] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ReviveResponse>(bytes);
            },

            //复活 S2C_Revive
            [(short)CmdId.S2C_Revive] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<RevivePush>(bytes);
            },

            //增加副本时间 C2S_InstanceTimeAdd
            [(short)CmdId.C2S_InstanceTimeAdd] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<InstanceTimeAddResponse>(bytes);
            },

            //增加副本时间 S2C_InstanceTimeAdd
            [(short)CmdId.S2C_InstanceTimeAdd] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<InstanceTimeAddPush>(bytes);
            },

            //死亡掉落 S2C_ItemDrop
            [(short)CmdId.S2C_ItemDrop] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ItemDropPush>(bytes);
            },

            //挖矿 C2S_Mining
            [(short)CmdId.C2S_Mining] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MiningResponse>(bytes);
            },

            //每日副本挑战次数 
            [(short)CmdId.C2S_DailyMapInfo] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<DailyMapInfoResponse>(bytes);
            },

            //每日必做副本评价 
            [(short)CmdId.S2C_DailyMapScore] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<DailyMapScorePush>(bytes);
            },

            //双倍领取 
            [(short)CmdId.C2S_DoubleReward] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<DoubleRewardResponse>(bytes);
            },

            //每日必做 结算  
            [(short)CmdId.S2C_GameResultDailyMap] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GameResultDailyMapPush>(bytes);
            },

            //扫荡 
            [(short)CmdId.C2S_CopySweep] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CopySweepResponse>(bytes);
            },

            //添加实例 S2C_GameObjectAdd
            [(short)CmdId.S2C_GameObjectAdd] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GameObjectAddPush>(bytes);
            },

            //删除实例 S2C_GameObjectDel
            [(short)CmdId.S2C_GameObjectDel] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GameObjectDelPush>(bytes);
            },

            // 混战击杀排行
            [(short)CmdId.S2C_PVPMeleeKill] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PVPMeleeKillNumPush>(bytes);
            },

            // 游戏结算 混战
            [(short)CmdId.S2C_GameResultMatchMelee] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GameResultMatchMeleePush>(bytes);
            },

            #region PVP

            //pvp 匹配对手列表信息
            [(short)CmdId.C2S_PvpMatchingRequest] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PvpMatchingResponse>(bytes);
            },

            //pvp 挑战对手 
            [(short)CmdId.C2S_PVPChallengeRequest] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PVPChallengeResponse>(bytes);
            },

            //pvp 匹配 
            [(short)CmdId.C2S_MatchingRequest] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MatchingResponse>(bytes);
            },
            //pvp 匹配结果
            [(short)CmdId.S2C_MatchingResult] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MatchingResultPush>(bytes);
            },
            //pvp 准备 
            [(short)CmdId.S2C_MatchingStart] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MatchingStartPush>(bytes);
            },
            //pvp 加入比赛房间
            [(short)CmdId.S2C_MatchRoomJoin] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<MatchRoomJoinPush>(bytes);
            },

            //pvp 游戏结算
            [(short)CmdId.S2C_GameResultMatch] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GameResultMatchPush>(bytes);
            },

            // pvp信息
            [(short)CmdId.C2S_PVPInfo] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PVPInfoResponse>(bytes);
            },

            #endregion

            //爬塔 离线奖励
            [(short)CmdId.C2S_EndlesstrialsReward] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<EndlesstrialsRewardResponse>(bytes);
            },

            //爬塔 游戏结算
            [(short)CmdId.S2C_GameResultEndlesstrials] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GameResultEndlesstrialsPush>(bytes);
            },

            //传送
            [(short)CmdId.S2C_Teleport] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<TeleportPush>(bytes);
            },

            // 如果在房间内升级，广播此消息
            [(short)CmdId.S2C_LevelUp] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<LevelUpPush>(bytes);
            },

            // 查看boss信息  C2S_CheckBoss
            [(short)CmdId.C2S_CheckBoss] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CheckBossResponse>(bytes);
            },

            //改名
            [(short)CmdId.C2S_ChangeRoleName] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ChangeRoleNameResponse>(bytes);
            },

            //系统通知
            [(short)CmdId.S2C_SystemNotification] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SystemNotificationPush>(bytes);
            },

            //自动战斗
            [(short)CmdId.C2S_AutoPlay] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<AutoPlayResponse>(bytes);
            },

            //跑马灯配置
            [(short)CmdId.S2C_Lamp] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<LampPush>(bytes);
            },

            //强制掉线
            [(short)CmdId.S2C_CompulsoryWithdrawalPush] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CompulsoryWithdrawalPush>(bytes);
            },

            #region 海岛挂机
            //海岛挂机 倍率
            [(short)CmdId.C2S_GuardIslandMultiplier] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuardIslandMultiplierResponse>(bytes);
            },
            #endregion

            #region 包裹 资源信息
            //包裹信息
            [(short)CmdId.C2S_GetBag] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GetBagResponse>(bytes);
            },

            //使用道具
            [(short)CmdId.C2S_ItemUse] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ItemUseResponse>(bytes);
            },

            //销毁道具
            [(short)CmdId.C2S_ItemDestroy] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ItemDestroyResponse>(bytes);
            },

            //增加资源
            [(short)CmdId.S2C_AddSource] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<AddSourcePush>(bytes);
            },

            //扣除资源
            [(short)CmdId.S2C_TakeSource] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<TakeSourcePush>(bytes);
            },

            //合成
            [(short)CmdId.C2S_Compose] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ComposeResponse>(bytes);
            },

            #endregion

            #region 装备

            ///一键穿戴最好装备
            [(short)CmdId.C2S_EquipBest] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //装备自动分解
            [(short)CmdId.C2S_AutoDisassembly] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<AutoDisassemblyResponse>(bytes);
            },

            //装备分解
            [(short)CmdId.C2S_ItemDisassembly] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ItemDisassemblyResponse>(bytes);
            },

            ///穿/脱装备
            [(short)CmdId.C2S_ChangeEquip] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ChangeEquipResponse>(bytes);
            },

            //洗炼 装备
            [(short)CmdId.C2S_EquipRefine] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<EquipRefineResponse>(bytes);
            },

            //保留洗炼属性 装备
            [(short)CmdId.C2S_EquipRefineReplace] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<EquipRefineReplaceResponse>(bytes);
            },

            //强化 装备
            [(short)CmdId.C2S_Enhance] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<EnhanceResponse>(bytes);
            },

            //升星 装备
            [(short)CmdId.C2S_Stars] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<StarsResponse>(bytes);
            },

            //同步角色装备信息
            [(short)CmdId.S2C_SyncUserEquipsInfo] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SyncUserEquipsInfoPush>(bytes);
            },

            //宝石
            [(short)CmdId.C2S_GemstoneChange] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GemstoneChangeResponse>(bytes);
            },

            //一键镶嵌穿
            [(short)CmdId.C2S_GemstoneChangeAll] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GemstoneChangeAllResponse>(bytes);
            },

            #endregion

            #region 技能
            //新技能
            [(short)CmdId.S2C_NewSkill] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<NewSkillPush>(bytes);
            },

            //技能 升级
            [(short)CmdId.C2S_SkillUpgrade] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SkillUpgradeResponse>(bytes);
            },

            //技能 穿脱
            [(short)CmdId.C2S_SkillChange] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SkillChangeResponse>(bytes);
            },

            //技能 升级
            [(short)CmdId.C2S_SkillAdvanced] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SkillAdvancedResponse>(bytes);
            },

            //同步角色技能槽位
            [(short)CmdId.S2C_SyncUserSkillSlot] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SyncUserSkillSlotPush>(bytes);
            },
            #endregion

            #region 任务

            //接任务
            [(short)CmdId.S2C_TaskAccept] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<TaskAcceptPush>(bytes);
            },

            //删任务
            [(short)CmdId.S2C_TaskRemove] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<TaskRemovePush>(bytes);
            },

            //更新任务
            [(short)CmdId.S2C_TaskUpdate] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<TaskUpdatePush>(bytes);
            },

            //完成任务
            [(short)CmdId.C2S_TaskComplate] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<TaskComplateResponse>(bytes);
            },

            #endregion

            #region 聊天

            //聊天
            [(short)CmdId.C2S_Chat] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ChatResponse>(bytes);
            },

            //聊天
            [(short)CmdId.S2C_Chat] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ChatPush>(bytes);
            },

            #endregion

            #region 大厅掉落开箱

            [(short)CmdId.S2C_HomeAutoEncounter] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<HomeAutoEncounterPush>(bytes);
            },

            [(short)CmdId.C2S_HomeOpenBox] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<HomeOpenBoxResponse>(bytes);
            },

            [(short)CmdId.C2S_BoxResIntoBag] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<BoxResIntoBagResponse>(bytes);
            },

            // 升级箱子
            [(short)CmdId.C2S_HomeUpgradeBox] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            // 加速升级
            [(short)CmdId.C2S_HomeUpgradeBoxQuick] = (byte[] bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            // 开箱指定等级、职业
            [(short)CmdId.C2S_HomeBoxSetting] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            #endregion

            #region 伙伴

            //伙伴列表
            [(short)CmdId.S2C_SyncPartnerListPush] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SyncPartnerListPush>(bytes);
            },

            //伙伴列表
            [(short)CmdId.C2S_PartnerList] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PartnerListResponse>(bytes);
            },

            //伙伴更新
            [(short)CmdId.S2C_PartnerUpdate] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PartnerUpdatePush>(bytes);
            },

            //伙伴槽位信息 主动拉取
            [(short)CmdId.C2S_PartnerSlotInfo] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PartnerSlotInfoResponse>(bytes);
            },

            //伙伴槽位更新
            [(short)CmdId.S2C_PartnerSlotUpdate] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PartnerSlotUpdatePush>(bytes);
            },

            //伙伴升级
            [(short)CmdId.C2S_UpgradeLevel] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //伙伴升星
            [(short)CmdId.C2S_PartnerStar] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //伙伴上阵
            [(short)CmdId.C2S_PartnerCombat] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //伙伴取消上阵
            [(short)CmdId.C2S_PartnerRest] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //伙伴碎片使用
            [(short)CmdId.C2S_PartnerCompose] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //伙伴重铸
            [(short)CmdId.C2S_PartnerReforge] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //伙伴分解
            [(short)CmdId.C2S_PartnerDisassemble] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //伙伴替换
            [(short)CmdId.C2S_PartnerReplace] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            #endregion

            #region 战宠

            //战宠列表
            [(short)CmdId.S2C_SyncUserPetInfo] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SyncUserPetInfoPush>(bytes);
            },

            //宠物升星
            [(short)CmdId.C2S_PetStar] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PetStarResponse>(bytes);
            },

            //宠物升级 
            [(short)CmdId.C2S_PetLevel] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //宠物移动
            [(short)CmdId.C2S_PetMove] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PetMoveResponse>(bytes);
            },

            #endregion

            #region 玩法选择信息
            //副本信息
            [(short)CmdId.C2S_InstanceInfo] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<InstanceInfoResponse>(bytes);
            },

            //领取章节奖励
            [(short)CmdId.C2S_BattleReward] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //游戏结算
            [(short)CmdId.S2C_GameResultBattleMap] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GameResultBattlePush>(bytes);
            },

            //宝箱信息
            [(short)CmdId.S2C_SyncUserBoxInfoPush] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<SyncUserBoxInfoPush>(bytes);
            },

            #endregion

            #region 工会
            //工会列表
            [(short)CmdId.C2S_GuildList] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildListResponse>(bytes);
            },

            //创建工会
            [(short)CmdId.C2S_GuildCreate] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildCreateResponse>(bytes);
            },

            //申请加入工会
            [(short)CmdId.C2S_GuildApply] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildApplyResponse>(bytes);
            },

            //取消申请
            [(short)CmdId.C2S_GuildCancelApply] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildCancelApplyResponse>(bytes);
            },

            //解散工会
            [(short)CmdId.C2S_GuildDismiss] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildDismissResponse>(bytes);
            },

            //离开工会
            [(short)CmdId.C2S_GuildLeave] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildLeaveResponse>(bytes);
            },

            //踢出工会
            [(short)CmdId.C2S_GuildKick] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildKickResponse>(bytes);
            },

            //提升工会职位
            [(short)CmdId.C2S_GuildPromote] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildPromoteResponse>(bytes);
            },

            //申请列表
            [(short)CmdId.C2S_GuildApplyList] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildApplyListResponse>(bytes);
            },

            //处理申请
            [(short)CmdId.C2S_GuildHandleApply] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildHandleApplyResponse>(bytes);
            },

            //工会信息
            [(short)CmdId.C2S_GuildInfo] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildInfoResponse>(bytes);
            },

            //工会成员列表
            [(short)CmdId.C2S_GuildMemberList] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildMemberListResponse>(bytes);
            },

            //工会捐献信息
            [(short)CmdId.C2S_GuildDonateInfo] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildDonateInfoResponse>(bytes);
            },
            //给工会捐献/签到
            [(short)CmdId.C2S_GuildDonate] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildDonateInfoResponse>(bytes);
            },

            //工会商城购买情况
            [(short)CmdId.C2S_GuildShopBuyInfo] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildShopBuyInfoResponse>(bytes);
            },

            //工会商城购买
            [(short)CmdId.C2S_GuildShopBuy] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //工会升级技能
            [(short)CmdId.C2S_GuildUpgradeSkill] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<CommonResponse>(bytes);
            },

            //工会副本奖励推送
            [(short)CmdId.S2C_GuildCopyRewardPush] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuildCopyRewardPush>(bytes);
            },

            //观察玩家详情
            [(short)CmdId.C2S_ObservePlayerInfo] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ObservePlayerInfoResponse>(bytes);
            },

            //观察玩家详情
            [(short)CmdId.C2S_PvpRecoder] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<PvpRecorderResponse>(bytes);
            },

            //玩家引导记录
            [(short)CmdId.C2S_GuidanceUpdate] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<GuidanceUpdateResponse>(bytes);
            },

            //聊天记录
            [(short)CmdId.C2S_ChatHistory] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<ChatHistoryResponse>(bytes);
            },

            //回合制阵容设置
            [(short)CmdId.C2S_TurnBasedSlot] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<TurnBasedSlotResponse>(bytes);
            },

            //回合制挑战
            [(short)CmdId.C2S_TurnBasedChallenge] = (bytes) => {
                TCPClientMgr.ProtoDeSerializePost<TurnBasedChallengeResponse>(bytes);
            },

            #endregion

        };
        
        private void AddDic<T>(CmdId _CmdId) where T : class
        {
            ProtoDic.Add((short)_CmdId, (byte[] bytes) =>
            {
                TCPClientMgr.ProtoDeSerializePost<T>(bytes);
            });
        }
      

        public void GetProro(short ID,byte[] bytes)
        {
            if (!ProtoDic.ContainsKey(ID))
            {
               // Log.PrintError($"ProtoMap Miss {ID}");
                return;
            }
            ProtoDic[ID]?.Invoke(bytes);
        }

        public bool CheakIsKey (short ID)
        {
           return ProtoDic.ContainsKey(ID);
        }
    }

    /// <summary>
    /// 自增协议Map
    /// </summary>
    public class TempProtoMap : SingletonTemplate<TempProtoMap>
    {
        public static Dictionary<long, Action<byte[]>> ProtoDic = new Dictionary<long, Action<byte[]>>();

        private static Dictionary<long, short> SendToProtoDic = new Dictionary<long, short>();

        public void AddProto(long sendID, short pbID)
        {
            if (!SendToProtoDic.ContainsKey(sendID))
            {
                if (ProtoMap.Instance.CheakIsKey(pbID))
                {
                    SendToProtoDic.Add(sendID, pbID);
                }
                else
                {
                    Log.PrintError("Miss Prpto " + pbID);
                }
            }
        }


        public bool CheckID(long ID)
        {
            return SendToProtoDic.ContainsKey(ID);
        }

        public void GetProro(long ID,byte[] bytes)
        {
            if (!SendToProtoDic.ContainsKey(ID))
            {
                Log.PrintError($"TempProtoMap Miss {ID}");
            }
            ProtoDic.Remove(ID);
            ProtoMap.Instance.GetProro(SendToProtoDic[ID], bytes);
        }

    }
}
