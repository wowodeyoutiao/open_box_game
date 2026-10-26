using JEngine.Core;
using UnityEngine;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 本地单机模式的装配入口。
    ///
    /// 必须在配置表加载完成、进入登录/建角前调用一次：
    /// 1. 读取存档并恢复 LocalData；
    /// 2. 注册 TCP / HTTP 本地路由；
    /// 3. 启动存档定时器。
    /// </summary>
    public static class LocalBootstrap
    {
        private const float AutoSaveIntervalSec = 15f;

        private static bool _inited;
        private static TimeItem _saveTimer;

        public static bool IsInitialized
        {
            get { return _inited; }
        }

        public static void Init()
        {
            if (_inited)
                return;
            _inited = true;

            LocalDiagnostics.Reset();

            // 数据层：先加载存档；没有存档时建立空账号，等待玩家建角。
            SaveCoordinator.Instance.Initialize();

            // Adapter 层：路由只指向 Service，不在这里执行业务。
            LocalServer.Instance.Init();
            LocalHttpRouter.Init();

            EnsureAutoSaveTimer();
            ShowLoadWarningIfNeeded();
            LocalSelfCheck.Run();

            Log.Print("[LocalNet] 装配完成 mode=" + LocalRuntime.Mode
                + " save=" + SaveCoordinator.Instance.LoadStatus
                + " roles=" + LocalData.Instance.Roles.Count);
            LocalDiagnostics.Dump();
        }

        /// <summary>前后台切换或退出前强制落盘。</summary>
        public static void SaveOnPause()
        {
            if (!_inited)
                return;
            SaveCoordinator.Instance.SaveNow();
        }

        /// <summary>场景切换后再次调用可补挂自动保存定时器。</summary>
        public static void EnsureAutoSaveTimer()
        {
            if (_saveTimer != null)
                return;

            if (GameHelper.Instance == null)
            {
                Log.PrintWarning("[LocalBootstrap] GameHelper 尚未就绪，自动存档定时器未启动");
                return;
            }

            _saveTimer = new TimeItem(0f, AutoSaveIntervalSec, SaveCoordinator.Instance.Tick, true, true);
            GameHelper.Instance.AddInvoke(_saveTimer);
        }

        private static void ShowLoadWarningIfNeeded()
        {
            SaveLoadStatus status = SaveCoordinator.Instance.LoadStatus;
            if (status != SaveLoadStatus.Corrupt
                && status != SaveLoadStatus.UnsupportedVersion
                && status != SaveLoadStatus.Failed)
                return;

            try
            {
                MetaFramework.UI.UITool.ShowTips("本地存档读取失败，原档已保留");
            }
            catch
            {
                // UI 尚未就绪时只保留日志。
            }
        }
    }
}
