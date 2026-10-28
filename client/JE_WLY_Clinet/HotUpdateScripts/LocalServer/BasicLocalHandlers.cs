using System.Collections.Generic;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// TCP 协议路由表（Adapter 层）。
    ///
    /// 【铁律】这里只做「CmdId → Service 方法」的映射，不写任何业务计算。
    /// 新增协议时：先在对应 Service 里写方法，再在这里加一行注册。
    /// </summary>
    public static class BasicLocalHandlers
    {
        public static void Register(Dictionary<short, LocalServer.Handler> dict)
        {
            // 伙伴
            LocalServer.Reg(dict, CmdId.C2S_PartnerList, OnPartnerList);
            LocalServer.Reg(dict, CmdId.C2S_PartnerSlotInfo, OnPartnerSlotInfo);
            LocalServer.Reg(dict, CmdId.C2S_PartnerCombat, OnPartnerCombat);
            LocalServer.Reg(dict, CmdId.C2S_PartnerRest, OnPartnerRest);
            LocalServer.Reg(dict, CmdId.C2S_PartnerReplace, OnPartnerReplace);
            LocalServer.Reg(dict, CmdId.C2S_PartnerStar, OnPartnerStar);

            // 背包
            LocalServer.Reg(dict, CmdId.C2S_GetBag, OnGetBag);
            LocalServer.Reg(dict, CmdId.C2S_ChangeEquip, OnChangeEquip);
            LocalServer.Reg(dict, CmdId.C2S_EquipBest, OnEquipBest);
            LocalServer.Reg(dict, CmdId.C2S_ItemDestroy, OnItemDestroy);
            LocalServer.Reg(dict, CmdId.C2S_ItemUse, OnItemUse);
            LocalServer.Reg(dict, CmdId.C2S_ItemDisassembly, OnItemDisassembly);
            LocalServer.Reg(dict, CmdId.C2S_AutoDisassembly, OnAutoDisassembly);

            // 任务
            LocalServer.Reg(dict, CmdId.C2S_TaskComplate, OnTaskComplete);

            // PVE-01 普通战役
            LocalServer.Reg(dict, CmdId.C2S_Join, OnBattleJoin);
            LocalServer.Reg(dict, CmdId.C2S_Move, OnBattleMove);
            LocalServer.Reg(dict, CmdId.C2S_Attack, OnBattleAttack);
            LocalServer.Reg(dict, CmdId.C2S_Exit, OnBattleExit);
            LocalServer.Reg(dict, CmdId.C2S_Revive, OnBattleRevive);
            LocalServer.Reg(dict, CmdId.C2S_BattleReward, OnBattleReward);

            // 大厅宝箱
            LocalServer.Reg(dict, CmdId.C2S_HomeBoxSetting, OnBoxSetting);
            LocalServer.Reg(dict, CmdId.C2S_HomeOpenBox, OnHomeOpenBox);
            LocalServer.Reg(dict, CmdId.C2S_BoxResIntoBag, OnBoxIntoBag);
            LocalServer.Reg(dict, CmdId.C2S_HomeUpgradeBox, OnUpgradeBox);
            LocalServer.Reg(dict, CmdId.C2S_HomeUpgradeBoxQuick, OnUpgradeBoxQuick);

            // 角色 · 副本 / 日常
            LocalServer.Reg(dict, CmdId.C2S_InstanceInfo, OnInstanceInfo);
            LocalServer.Reg(dict, CmdId.C2S_DailyMapInfo, OnDailyMapInfo);

            // PVP（多人内容，阶段3 删除）
            LocalServer.Reg(dict, CmdId.C2S_PVPInfo, OnPvpInfo);
        }

        private static object OnPartnerList(byte[] req) { return PartnerService.Instance.GetList(); }
        private static object OnPartnerSlotInfo(byte[] req) { return PartnerService.Instance.GetSlotInfo(); }

        private static object OnPartnerCombat(byte[] req)
        {
            PartnerCombatRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<PartnerCombatRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CommonResponse
                {
                    Code = -1,
                    Error = "伙伴上阵请求解析失败: " + e.Message,
                    RequestCmdId = (uint)CmdId.C2S_PartnerCombat,
                };
            }
            return PartnerService.Instance.Combat(request);
        }

        private static object OnPartnerRest(byte[] req)
        {
            PartnerRestRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<PartnerRestRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CommonResponse
                {
                    Code = -1,
                    Error = "伙伴下阵请求解析失败: " + e.Message,
                    RequestCmdId = (uint)CmdId.C2S_PartnerRest,
                };
            }
            return PartnerService.Instance.Rest(request);
        }

        private static object OnPartnerReplace(byte[] req)
        {
            PartnerReplaceRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<PartnerReplaceRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CommonResponse
                {
                    Code = -1,
                    Error = "伙伴替换请求解析失败: " + e.Message,
                    RequestCmdId = (uint)CmdId.C2S_PartnerReplace,
                };
            }
            return PartnerService.Instance.Replace(request);
        }

        private static object OnPartnerStar(byte[] req)
        {
            PartnerStarRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<PartnerStarRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CommonResponse
                {
                    Code = -1,
                    Error = "伙伴升星请求解析失败: " + e.Message,
                    RequestCmdId = (uint)CmdId.C2S_PartnerStar,
                };
            }
            return PartnerService.Instance.Star(request);
        }
        private static object OnGetBag(byte[] req) { return ItemService.Instance.GetBag(); }
        private static object OnChangeEquip(byte[] req) { return ItemService.Instance.ChangeEquip(req); }
        private static object OnEquipBest(byte[] req) { return ItemService.Instance.EquipBest(req); }
        private static object OnItemDestroy(byte[] req) { return ItemService.Instance.ItemDestroy(req); }
        private static object OnItemUse(byte[] req) { return ItemService.Instance.ItemUse(req); }
        private static object OnItemDisassembly(byte[] req) { return ItemService.Instance.ItemDisassembly(req); }
        private static object OnAutoDisassembly(byte[] req) { return ItemService.Instance.AutoDisassembly(req); }
        private static object OnBattleJoin(byte[] req)
        {
            JoinDeskRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<JoinDeskRequest>(req);
            }
            catch (System.Exception e)
            {
                return new JoinDeskResponse { Code = -1, Error = "进入战斗请求解析失败: " + e.Message };
            }
            return BattleService.Instance.Enter(request);
        }

        private static object OnBattleMove(byte[] req)
        {
            MoveRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<MoveRequest>(req);
            }
            catch (System.Exception e)
            {
                return new MoveResponse { Code = -1, Error = "移动请求解析失败: " + e.Message };
            }
            return BattleService.Instance.Move(request);
        }

        private static object OnBattleAttack(byte[] req)
        {
            AttackRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<AttackRequest>(req);
            }
            catch (System.Exception e)
            {
                return new AttackResponse { Code = -1, Error = "攻击请求解析失败: " + e.Message };
            }
            return BattleService.Instance.Attack(request);
        }

        private static object OnBattleRevive(byte[] req) { return BattleService.Instance.Revive(req); }
        private static object OnBattleExit(byte[] req)
        {
            return BattleService.Instance.Exit();
        }

        private static object OnBattleReward(byte[] req)
        {
            BattleRewardRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<BattleRewardRequest>(req);
            }
            catch (System.Exception e)
            {
                return new CommonResponse
                {
                    Code = -1,
                    Error = "章节奖励请求解析失败: " + e.Message,
                    RequestCmdId = (uint)CmdId.C2S_BattleReward,
                };
            }
            return BattleService.Instance.ClaimChapterReward(request);
        }

        private static object OnTaskComplete(byte[] req)
        {
            TaskComplateRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<TaskComplateRequest>(req);
            }
            catch (System.Exception e)
            {
                return new TaskComplateResponse { Code = -1, Error = "任务完成请求解析失败: " + e.Message };
            }
            return TaskService.Instance.Complete(request.TaskId);
        }
        private static object OnBoxSetting(byte[] req) { return BoxService.Instance.BoxSetting(req); }
        private static object OnHomeOpenBox(byte[] req) { return BoxService.Instance.OpenBox(req); }
        private static object OnBoxIntoBag(byte[] req) { return BoxService.Instance.BoxIntoBag(req); }
        private static object OnUpgradeBox(byte[] req) { return BoxService.Instance.UpgradeBox(req); }
        private static object OnUpgradeBoxQuick(byte[] req) { return BoxService.Instance.UpgradeBoxQuick(req); }
        private static object OnInstanceInfo(byte[] req) { return PlayerService.Instance.GetInstanceInfo(); }
        private static object OnDailyMapInfo(byte[] req) { return PlayerService.Instance.GetDailyMapInfo(); }
        private static object OnPvpInfo(byte[] req) { return PlayerService.Instance.GetPvpInfo(); }
    }
}
