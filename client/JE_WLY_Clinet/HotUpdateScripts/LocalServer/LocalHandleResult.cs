namespace HotUpdateScripts.LocalNet
{
    public enum LocalHandleOutcome
    {
        Handled = 0,
        NotImplemented = 1,
        Failed = 2
    }

    /// <summary>
    /// TCP 本地处理结果。
    ///
    /// 不再使用 bool + null response 表达“已接管但没有响应”，
    /// 避免调用方把未实现命令误判为成功。
    /// </summary>
    public sealed class LocalHandleResult
    {
        public LocalHandleOutcome Outcome;
        public object Response;
        public string Error;

        public static LocalHandleResult Handled(object response)
        {
            return new LocalHandleResult
            {
                Outcome = LocalHandleOutcome.Handled,
                Response = response,
                Error = ""
            };
        }

        public static LocalHandleResult NotImplemented()
        {
            return new LocalHandleResult
            {
                Outcome = LocalHandleOutcome.NotImplemented,
                Response = null,
                Error = "协议尚未本地实现"
            };
        }

        public static LocalHandleResult Failed(string error)
        {
            return new LocalHandleResult
            {
                Outcome = LocalHandleOutcome.Failed,
                Response = null,
                Error = string.IsNullOrEmpty(error) ? "本地处理失败" : error
            };
        }
    }
}
