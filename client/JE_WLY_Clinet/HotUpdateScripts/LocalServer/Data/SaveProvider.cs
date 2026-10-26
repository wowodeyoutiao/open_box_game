using System;
using System.IO;
using System.Text;
using JEngine.Core;
using JEngine.LitJson;
using UnityEngine;

namespace HotUpdateScripts.LocalNet
{
    public enum SaveLoadStatus
    {
        None = 0,
        Loaded = 1,
        Corrupt = 2,
        UnsupportedVersion = 3,
        Failed = 4
    }

    public sealed class SaveLoadResult
    {
        public SaveLoadStatus Status;
        public SaveDocument Document;
        public string Message = "";
        public bool UsedBackup;
    }

    /// <summary>
    /// 存档存储抽象。只认 SaveDocument，不认识 LocalData / Proto / Manager。
    /// </summary>
    public interface ISaveProvider
    {
        SaveLoadResult Load();
        bool Save(SaveDocument document);
        void Delete();
        bool Exists();
        string GetStorageDescription();
    }

    /// <summary>
    /// 本地 JSON 存档。
    ///
    /// - 主文件：save_local.json
    /// - 备份：save_local.bak.json
    /// - 临时：save_local.tmp.json
    /// - 损坏原档隔离：save_local.corrupt.&lt;ticks&gt;.json
    ///
    /// 不使用 JsonUtility（不支持 Dictionary），改用显式的 SaveDocument DTO + LitJson。
    /// </summary>
    public class LocalFileSaveProvider : ISaveProvider
    {
        private const string FileName = "save_local.json";
        private const string BackupFileName = "save_local.bak.json";
        private const string TempFileName = "save_local.tmp.json";

        private readonly string _root;
        private string _lastError = "";

        public LocalFileSaveProvider()
            : this(Application.persistentDataPath)
        {
        }

        /// <summary>测试或调试可指定目录。</summary>
        public LocalFileSaveProvider(string root)
        {
            _root = string.IsNullOrEmpty(root) ? Application.persistentDataPath : root;
        }

        public string SavePath
        {
            get { return Path.Combine(_root, FileName); }
        }

        public string BackupPath
        {
            get { return Path.Combine(_root, BackupFileName); }
        }

        public string TempPath
        {
            get { return Path.Combine(_root, TempFileName); }
        }

        public string LastError
        {
            get { return _lastError; }
        }

        public bool Exists()
        {
            return File.Exists(SavePath);
        }

        public SaveLoadResult Load()
        {
            _lastError = "";
            Directory.CreateDirectory(_root);

            if (!File.Exists(SavePath))
            {
                return new SaveLoadResult
                {
                    Status = SaveLoadStatus.None,
                    Message = "没有本地存档"
                };
            }

            SaveDocument document;
            string error;
            SaveLoadStatus errorStatus;
            if (TryLoadPath(SavePath, out document, out error, out errorStatus))
            {
                return new SaveLoadResult
                {
                    Status = SaveLoadStatus.Loaded,
                    Document = document,
                    Message = "读取主存档成功"
                };
            }

            string primaryError = error;
            SaveLoadStatus primaryErrorStatus = errorStatus;
            QuarantineCorruptFile();

            string backupError = "";
            SaveLoadStatus backupErrorStatus;
            if (File.Exists(BackupPath)
                && TryLoadPath(BackupPath, out document, out backupError, out backupErrorStatus))
            {
                TryRestoreBackupToMain();
                return new SaveLoadResult
                {
                    Status = SaveLoadStatus.Loaded,
                    Document = document,
                    Message = "主存档损坏，已从备份恢复",
                    UsedBackup = true
                };
            }

            _lastError = primaryError + (string.IsNullOrEmpty(backupError) ? "" : "；备份读取失败: " + backupError);
            return new SaveLoadResult
            {
                Status = primaryErrorStatus == SaveLoadStatus.UnsupportedVersion
                    ? SaveLoadStatus.UnsupportedVersion
                    : SaveLoadStatus.Corrupt,
                Message = _lastError
            };
        }

        public bool Save(SaveDocument document)
        {
            _lastError = "";
            if (document == null)
            {
                _lastError = "SaveDocument 为 null";
                return false;
            }

            try
            {
                Directory.CreateDirectory(_root);
                string json = JsonMapper.ToJson(document);
                if (string.IsNullOrEmpty(json))
                {
                    _lastError = "序列化结果为空";
                    return false;
                }

                File.WriteAllText(TempPath, json, new UTF8Encoding(false));

                if (File.Exists(SavePath))
                    File.Copy(SavePath, BackupPath, true);

                if (File.Exists(SavePath))
                    File.Delete(SavePath);

                try
                {
                    File.Move(TempPath, SavePath);
                }
                catch
                {
                    // 某些运行环境不支持原子 Move，降级为覆盖写入。
                    File.Copy(TempPath, SavePath, true);
                    File.Delete(TempPath);
                }

                return true;
            }
            catch (Exception e)
            {
                _lastError = e.ToString();
                Log.PrintError("[SaveProvider] 保存失败: " + e);
                return false;
            }
        }

        public void Delete()
        {
            TryDeleteFile(TempPath);
            TryDeleteFile(SavePath);
            TryDeleteFile(BackupPath);
        }

        public string GetStorageDescription()
        {
            return SavePath;
        }

        private bool TryLoadPath(
            string path,
            out SaveDocument document,
            out string error,
            out SaveLoadStatus errorStatus)
        {
            document = null;
            error = "";
            errorStatus = SaveLoadStatus.Corrupt;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrEmpty(json))
                {
                    error = "文件为空";
                    return false;
                }

                document = JsonMapper.ToObject<SaveDocument>(json);
                if (document == null)
                {
                    error = "反序列化结果为 null";
                    return false;
                }

                if (document.SchemaVersion <= 0)
                {
                    error = "SchemaVersion 无效";
                    return false;
                }
                if (document.SchemaVersion > SaveDocumentMapper.CurrentSchemaVersion)
                {
                    errorStatus = SaveLoadStatus.UnsupportedVersion;
                    error = "存档版本 " + document.SchemaVersion
                        + " 高于当前支持版本 " + SaveDocumentMapper.CurrentSchemaVersion;
                    return false;
                }
                if (document.SchemaVersion == 1)
                    document = SaveDocumentMapper.MigrateV1ToV2(document);
                if (document.SchemaVersion != SaveDocumentMapper.CurrentSchemaVersion)
                {
                    errorStatus = SaveLoadStatus.UnsupportedVersion;
                    error = "缺少存档迁移: " + document.SchemaVersion;
                    return false;
                }

                if (document.Roles == null)
                    document.Roles = new System.Collections.Generic.List<RoleSaveV1>();

                return true;
            }
            catch (Exception e)
            {
                error = e.ToString();
                return false;
            }
        }

        private void QuarantineCorruptFile()
        {
            try
            {
                if (!File.Exists(SavePath))
                    return;
                string target = Path.Combine(_root, "save_local.corrupt." + DateTime.UtcNow.Ticks + ".json");
                File.Copy(SavePath, target, true);
            }
            catch (Exception e)
            {
                Log.PrintWarning("[SaveProvider] 隔离损坏存档失败: " + e.Message);
            }
        }

        private void TryRestoreBackupToMain()
        {
            try
            {
                File.Copy(BackupPath, SavePath, true);
            }
            catch (Exception e)
            {
                Log.PrintWarning("[SaveProvider] 备份已读取，但恢复主文件失败: " + e.Message);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // 删除失败不阻断其他文件清理。
            }
        }
    }
}
