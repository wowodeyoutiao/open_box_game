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

            // 背包
            LocalServer.Reg(dict, CmdId.C2S_GetBag, OnGetBag);
            LocalServer.Reg(dict, CmdId.C2S_ChangeEquip, OnChangeEquip);
            LocalServer.Reg(dict, CmdId.C2S_EquipBest, OnEquipBest);

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
        private static object OnGetBag(byte[] req) { return ItemService.Instance.GetBag(); }
        private static object OnChangeEquip(byte[] req) { return ItemService.Instance.ChangeEquip(req); }
        private static object OnEquipBest(byte[] req) { return ItemService.Instance.EquipBest(req); }
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
