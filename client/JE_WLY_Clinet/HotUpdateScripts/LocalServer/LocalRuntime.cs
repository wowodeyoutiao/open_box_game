using System;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 单机运行模式。
    /// </summary>
    public enum LocalRuntimeMode
    {
        /// <summary>连接真实服务器。</summary>
        Remote = 0,

        /// <summary>本地优先；未命中时允许真实网络并抓包，仅用于开发对照。</summary>
        LocalCompare = 1,

        /// <summary>发布单机模式。所有保留功能必须本地处理，禁止回退真实网络。</summary>
        LocalStrict = 2
    }

    /// <summary>
    /// 全局单机运行配置。
    ///
    /// 约束：
    /// - 发布版本默认必须是 LocalStrict；
    /// - 不为多个模块分别维护“是否单机”的开关；
    /// - 需要抓包时只能显式切到 LocalCompare。
    /// </summary>
    public static class LocalRuntime
    {
        private static LocalRuntimeMode _mode = LocalRuntimeMode.LocalStrict;

        public static LocalRuntimeMode Mode
        {
            get { return _mode; }
        }

        /// <summary>是否处于本地模式（严格离线或开发对照）。</summary>
        public static bool IsLocalMode
        {
            get { return _mode != LocalRuntimeMode.Remote; }
        }

        /// <summary>严格离线：未命中本地路由不得访问真实网络。</summary>
        public static bool IsStrictOffline
        {
            get { return _mode == LocalRuntimeMode.LocalStrict; }
        }

        /// <summary>是否允许本地未命中时访问真实网络。</summary>
        public static bool CanFallbackToNetwork
        {
            get { return _mode == LocalRuntimeMode.LocalCompare; }
        }

        /// <summary>
        /// 显式切换运行模式。只建议在启动入口或开发工具中调用。
        /// </summary>
        public static void SetMode(LocalRuntimeMode mode)
        {
            if (_mode == mode)
                return;

            int value = (int)mode;
            if (value < (int)LocalRuntimeMode.Remote || value > (int)LocalRuntimeMode.LocalStrict)
                throw new ArgumentOutOfRangeException("mode");

            _mode = mode;
        }
    }
}
