using JEngine.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotUpdateScripts.ProtoM
{
    public class StateM : Singleton<StateM>
    {
        /// <summary>
        /// 是否发送了升级或加速宝箱的请求？
        /// 升级或加速成功后重置为false, 用于防止重复请求。
        /// </summary>
        public bool isSendUpgradeOrSpeedUpBox;
    }
}
