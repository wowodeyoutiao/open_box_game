using System.Collections.Generic;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 尚未实现的单人动作协议。
    ///
    /// 这些协议会真实修改等级、背包、伙伴、宠物或战斗进度，不能再用 Code=0
    /// 伪装成功。当前统一返回明确失败，并从诊断台账中单独列出，后续接一个
    /// Service 就从这里移除一个。
    /// </summary>
    public static class GamePlayLocalHandlers
    {
        public static void Register(Dictionary<short, LocalServer.Handler> dict)
        {
            // 角色成长
            RegUnsupported(dict, CmdId.C2S_UpgradeLevel);

            // 伙伴
            RegUnsupported(dict, CmdId.C2S_PartnerCombat);
            RegUnsupported(dict, CmdId.C2S_PartnerCompose);
            RegUnsupported(dict, CmdId.C2S_PartnerDisassemble);
            RegUnsupported(dict, CmdId.C2S_PartnerReforge);
            RegUnsupported(dict, CmdId.C2S_PartnerReplace);
            RegUnsupported(dict, CmdId.C2S_PartnerRest);
            RegUnsupported(dict, CmdId.C2S_PartnerStar);

            // 宠物
            RegUnsupported(dict, CmdId.C2S_PetLevel);

            // 装备 / 战斗
            RegUnsupported(dict, CmdId.C2S_BattleReward);
        }

        /// <summary>
        /// 动作协议尚未实现：回失败并保留 RequestCmdId。
        /// 不修改 LocalData，不发送成功推送。
        /// </summary>
        private static void RegUnsupported(Dictionary<short, LocalServer.Handler> dict, CmdId cmd)
        {
            short id = (short)cmd;
            LocalDiagnostics.DeclareUnsupportedAction(id);
            LocalServer.Reg(dict, cmd, (req) =>
            {
                LocalDiagnostics.RecordUnsupportedAction(id, "动作协议尚未迁移到本地 Service");
                return new CommonResponse
                {
                    Code = -1,
                    Error = "单机版尚未实现该功能",
                    RequestCmdId = (uint)cmd,
                };
            });
        }
    }
}
