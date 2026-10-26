using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 伙伴领域服务：伙伴列表、上阵、升星、重构等。
    /// 阶段1 只提供列表，其余接口随协议接入逐步补齐。
    /// </summary>
    public class PartnerService : SingletonTemplate<PartnerService>
    {
        /// <summary>伙伴列表（登录成功后客户端会立刻请求）</summary>
        public object GetList()
        {
            LocalData d = LocalData.Instance;
            var resp = new PartnerListResponse { Code = 0, Error = "" };
            if (d.Partners != null && d.Partners.Count > 0)
                resp.List.AddRange(d.Partners);
            return resp;
        }
    }
}
