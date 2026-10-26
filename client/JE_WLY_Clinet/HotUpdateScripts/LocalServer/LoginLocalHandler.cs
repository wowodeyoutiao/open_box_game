using System.Collections.Generic;
using JEngine.Core;
using JEngine.Helper;
using JEngine.MyNet;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 登录相关本地协议。
    /// </summary>
    public static class LoginLocalHandler
    {
        public static void Register(Dictionary<short, LocalServer.Handler> dict)
        {
            LocalServer.Reg(dict, CmdId.C2S_Login, OnLogin);
        }

        private static object OnLogin(byte[] req)
        {
            // 数据流已改为单向：选角时 select_char 就把 Roles[uid] 展开到 LocalData 镜像字段，
            // 这里只做校验/日志，不再从 RoleM 反向回填（避免覆盖玩法进度）
            var d = LocalData.Instance;
            d.EnsureInit();

            if (d.Current == null)
                Log.PrintWarning("[LoginLocalHandler] 登录时没有选中角色，LocalData 处于默认状态");

            Log.Print($"[LoginLocalHandler] TCP 登录 uid={d.Uid} name={d.Nickname} job={d.Job} sex={d.Sex} lv={d.Level}");

            // Adapter 只做转发，业务在 PlayerService
            return PlayerService.Instance.BuildLoginTcpResponse();
        }
    }
}
