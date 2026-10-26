using System.Collections.Generic;
using JEngine.Singleton;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 邮件领域服务。
    ///
    /// ⚠️ 响应类型 HTTP_MailListResponse 是 struct 且含 List/数组字段，
    /// 必须直接构造对象，不能走 JsonMapper（LitJson 解析热更 struct 会丢字段）。
    /// </summary>
    public class MailService : SingletonTemplate<MailService>
    {
        /// <summary>邮件列表：本地返回空列表（单机不做社交邮件，后续改成系统信）</summary>
        public HotUpdateScripts.ProtoM.HTTP_MailListResponse GetList()
        {
            return new HotUpdateScripts.ProtoM.HTTP_MailListResponse
            {
                code = 0,
                total = 0,
                data = new List<HotUpdateScripts.ProtoM.HTTP_MailInfo>(),
            };
        }
    }
}
