#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// batchmode POC：无需打开编辑器 GUI，加载 Init 场景并自动进入 Play。
/// 测试入口由 UILogin 本地模式下的 LocalTestBootstrap.TryRunStandalone() 触发。
/// </summary>
public static class LocalTestBatchRunner
{
    private static DateTime _startedAt;
    private static bool _running;

    public static void Run()
    {
        if (_running)
            return;
        _running = true;
        _startedAt = DateTime.Now;

        Debug.Log("[LocalTestBatch] start, isBatchMode=" + Application.isBatchMode);
        string scene = "Assets/Init.unity";
        if (!File.Exists(scene))
        {
            Debug.LogError("[LocalTestBatch] Init scene not found: " + scene);
            EditorApplication.Exit(2);
            return;
        }

        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        EditorApplication.update += Poll;
        EditorApplication.isPlaying = true;
    }

    private static void Poll()
    {
        if (!_running)
            return;

        string report = Path.GetFullPath(Path.Combine(
            Application.dataPath, "../../../LocalTestHarness/reports/last_report.json"));
        if (File.Exists(report) && File.GetLastWriteTime(report) > _startedAt)
        {
            Debug.Log("[LocalTestBatch] report ready: " + report);
            Finish(0);
            return;
        }

        if ((DateTime.Now - _startedAt).TotalSeconds > 180)
        {
            Debug.LogError("[LocalTestBatch] timeout waiting for report: " + report);
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        _running = false;
        EditorApplication.update -= Poll;
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }
}
#endif
