using JEngine.Core;
using JEngine.Singleton;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 存档协调器。
    ///
    /// 职责：
    /// - 启动时读取存档并应用到 LocalData；
    /// - 保存时把 LocalData 映射成 SaveDocument；
    /// - 合并短时间内的多次脏标记；
    /// - 在关键操作、定时器和前后台切换时落盘。
    /// </summary>
    public class SaveCoordinator : SingletonTemplate<SaveCoordinator>
    {
        private ISaveProvider _provider;
        private bool _ready;
        private bool _dirty;
        private bool _saving;

        public SaveLoadStatus LoadStatus { get; private set; }
        public string LoadMessage { get; private set; }
        public bool IsReady { get; private set; }

        public SaveCoordinator()
        {
            LoadStatus = SaveLoadStatus.None;
            LoadMessage = "";
        }

        /// <summary>可在测试时注入自定义 Provider。</summary>
        public void ConfigureProvider(ISaveProvider provider)
        {
            if (_ready)
                throw new System.InvalidOperationException("存档协调器已初始化，不能替换 Provider");
            _provider = provider;
        }

        public void Initialize()
        {
            if (_ready)
                return;

            if (_provider == null)
                _provider = new LocalFileSaveProvider();

            LoadStatus = SaveLoadStatus.Failed;
            LoadMessage = "";

            try
            {
                SaveLoadResult result = _provider.Load();
                LoadStatus = result.Status;
                LoadMessage = result.Message;

                if (result.Status == SaveLoadStatus.Loaded && result.Document != null)
                {
                    var roles = SaveDocumentMapper.ToLocalRoles(result.Document);
                    LocalData.Instance.ApplyLoadedRoles(
                        roles,
                        result.Document.CurrentRoleUid,
                        result.Document.NextRoleUid,
                        result.Document.NextItemUid);

                    if (LocalData.Instance.Current != null)
                        RoleStatService.Instance.RecalculateCurrent(true);

                    Log.Print("[SaveCoordinator] 存档加载成功 roles=" + roles.Count
                        + " currentUid=" + result.Document.CurrentRoleUid
                        + (result.UsedBackup ? "（来自备份）" : ""));
                }
                else
                {
                    LocalData.Instance.InitializeEmpty();

                    if (result.Status == SaveLoadStatus.Corrupt
                        || result.Status == SaveLoadStatus.UnsupportedVersion
                        || result.Status == SaveLoadStatus.Failed)
                    {
                        LocalDiagnostics.RecordError("SaveCoordinator", result.Message);
                    }
                    else
                    {
                        Log.Print("[SaveCoordinator] 无存档，进入新账号流程");
                    }
                }
            }
            catch (System.Exception e)
            {
                LoadStatus = SaveLoadStatus.Failed;
                LoadMessage = e.ToString();
                LocalData.Instance.InitializeEmpty();
                LocalDiagnostics.RecordError("SaveCoordinator", "初始化异常: " + e);
            }

            _ready = true;
            IsReady = true;
        }

        public void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>立即保存。失败时保留脏标记，下一次自动保存会重试。</summary>
        public bool SaveNow()
        {
            if (!_ready)
            {
                Log.PrintWarning("[SaveCoordinator] 尚未初始化，跳过保存");
                return false;
            }

            if (_saving)
                return false;

            // 保存失败时必须保留脏标记，让下一次 Tick 自动重试。
            _dirty = true;
            _saving = true;
            try
            {
                LocalData data = LocalData.Instance;
                UpdateLastSeen(data);

                SaveDocument document = SaveDocumentMapper.FromLocalData(data);
                bool ok = _provider.Save(document);
                if (ok)
                {
                    _dirty = false;
                    Log.Print("[SaveCoordinator] 保存成功: " + _provider.GetStorageDescription());
                }
                else
                {
                    LocalDiagnostics.RecordError("SaveCoordinator", "保存失败");
                }
                return ok;
            }
            catch (System.Exception e)
            {
                LocalDiagnostics.RecordError("SaveCoordinator", "保存异常: " + e);
                return false;
            }
            finally
            {
                _saving = false;
            }
        }

        /// <summary>按固定间隔调用，只有脏数据才写盘。</summary>
        public void Tick()
        {
            if (_dirty)
                SaveNow();
        }

        public bool HasUnsavedChanges
        {
            get { return _dirty; }
        }

        public void DeleteSave()
        {
            if (_provider == null)
                _provider = new LocalFileSaveProvider();
            _provider.Delete();
            _dirty = false;
        }

        private static void UpdateLastSeen(LocalData data)
        {
            if (data == null || data.Roles == null)
                return;

            long now = SaveDocumentMapper.GetUnixSeconds();
            for (int i = 0; i < data.Roles.Count; i++)
            {
                LocalRole role = data.Roles[i];
                if (role != null)
                    role.LastSeenUnix = now;
            }
        }
    }
}
