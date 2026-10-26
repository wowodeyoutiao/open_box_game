using System.Collections.Generic;
using System.Text;
using JEngine.Core;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 单机适配诊断台账。
    ///
    /// 只记录“哪些功能没有真正本地处理”，不参与任何玩法计算。
    /// 运行时可通过 Dump() 输出，后续可在此扩展导出到 persistentDataPath。
    /// </summary>
    public static class LocalDiagnostics
    {
        private const int MaxErrorEntries = 100;

        private static readonly HashSet<short> _missingTcpCmds = new HashSet<short>();
        private static readonly HashSet<short> _unsupportedActionCmds = new HashSet<short>();
        private static readonly HashSet<string> _missingHttpUrls = new HashSet<string>();
        private static readonly List<string> _errors = new List<string>();

        public static IReadOnlyCollection<short> MissingTcpCmds
        {
            get { return _missingTcpCmds; }
        }

        public static IReadOnlyCollection<short> UnsupportedActionCmds
        {
            get { return _unsupportedActionCmds; }
        }

        public static IReadOnlyCollection<string> MissingHttpUrls
        {
            get { return _missingHttpUrls; }
        }

        public static IReadOnlyCollection<string> Errors
        {
            get { return _errors; }
        }

        public static void RecordMissingTcp(short cmdId)
        {
            if (_missingTcpCmds.Add(cmdId))
                Log.PrintWarning("[LocalDiagnostics] 未实现 TCP 协议 cmd=" + cmdId);
        }

        /// <summary>启动注册路由时声明哪些动作协议仍是未实现状态。</summary>
        public static void DeclareUnsupportedAction(short cmdId)
        {
            _unsupportedActionCmds.Add(cmdId);
        }

        public static void RecordUnsupportedAction(short cmdId, string reason)
        {
            if (_unsupportedActionCmds.Add(cmdId))
                Log.PrintWarning("[LocalDiagnostics] 动作协议未实现 cmd=" + cmdId + " reason=" + reason);
        }

        public static void RecordMissingHttp(string url)
        {
            if (!string.IsNullOrEmpty(url) && _missingHttpUrls.Add(url))
                Log.PrintWarning("[LocalDiagnostics] 未本地化 HTTP: " + url);
        }

        public static void RecordError(string source, string message)
        {
            string item = source + ": " + message;
            if (_errors.Count >= MaxErrorEntries)
                _errors.RemoveAt(0);
            _errors.Add(item);
            Log.PrintError("[LocalDiagnostics] " + item);
        }

        /// <summary>输出当前诊断快照。用于排查“界面没数据/按钮没反应”。</summary>
        public static string Snapshot()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[LocalDiagnostics] mode=" + LocalRuntime.Mode);
            sb.AppendLine("missingTcp=" + FormatCmds(_missingTcpCmds));
            sb.AppendLine("unsupportedActions=" + FormatCmds(_unsupportedActionCmds));
            sb.AppendLine("missingHttp=" + (_missingHttpUrls.Count == 0 ? "(none)" : string.Join(", ", ToArray(_missingHttpUrls))));
            sb.AppendLine("errors=" + (_errors.Count == 0 ? "(none)" : string.Join(" | ", _errors.ToArray())));
            return sb.ToString();
        }

        public static void Dump()
        {
            Log.Print(Snapshot());
        }

        public static void Reset()
        {
            _missingTcpCmds.Clear();
            _unsupportedActionCmds.Clear();
            _missingHttpUrls.Clear();
            _errors.Clear();
        }

        private static string FormatCmds(HashSet<short> values)
        {
            if (values.Count == 0)
                return "(none)";

            var sb = new StringBuilder();
            foreach (short value in values)
            {
                if (sb.Length > 0)
                    sb.Append(',');
                sb.Append(value);
            }
            return sb.ToString();
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var list = new List<string>(values);
            list.Sort();
            return list.ToArray();
        }
    }
}
