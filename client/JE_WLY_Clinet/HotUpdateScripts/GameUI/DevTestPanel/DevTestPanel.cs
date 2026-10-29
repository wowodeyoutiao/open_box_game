using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using HotUpdateScripts.LocalNet;
using HotUpdateScripts.ProtoM;

/// <summary>
/// 内部真机测试面板（Dev/QA 专用）。
///
/// ══════════════════════════════════════════════════════════════════
/// 这是一个**完全独立的单元**，设计目标是「整块删掉不留痕」：
///
///   · UI 全部运行时动态创建（不新增/不修改任何 Prefab）
///   · 不依赖 MonoBehaviour（纯静态类 + Button.onClick 回调），
///     避免 ILRuntime 下 AddComponent&lt;热更类型&gt; 的适配问题
///   · 测试项直接操作 LocalData.Instance.Current（存档对象），
///     不往 PlayerService / ItemService 等业务服务里加方法
///   · 唯一的外部侵入点：宿主里一行 DevTestPanel.Ensure()
///
/// 要彻底下线时：
///   1. 删掉整个 GameUI/DevTestPanel/ 目录
///   2. 删掉宿主里那一行 DevTestPanel.Ensure()
///   3. 删掉 csproj 里对应的 Compile 项
///
/// 要临时屏蔽（不删代码）时：
///   把 Enabled 改成 false 即可，Ensure() 会直接返回。
/// ══════════════════════════════════════════════════════════════════
/// </summary>
public static class DevTestPanel
{
    /// <summary>总开关。改成 false 就整块禁用，不用改别的地方。</summary>
    public static bool Enabled = true;

    private static GameObject _root;
    private static GameObject _panel;
    private static Font _font;
    private static readonly List<DevTestItem> _items = new List<DevTestItem>();

    /// <summary>一条测试项。</summary>
    private sealed class DevTestItem
    {
        public string Label;
        public Action OnClick;
    }

    /// <summary>
    /// 确保面板已创建。宿主在进入主界面时调一次即可，重复调用无副作用。
    /// </summary>
    public static void Ensure()
    {
        if (!Enabled)
            return;
        if (_root != null)
            return;

        try
        {
            RegisterBuiltinTests();
            BuildUI();
        }
        catch (Exception e)
        {
            Debug.LogError("[DevTestPanel] 创建失败: " + e);
        }
    }

    /// <summary>外部可以继续加测试项（比如某个系统的临时验证按钮）。</summary>
    public static void AddTest(string label, Action onClick)
    {
        if (string.IsNullOrEmpty(label) || onClick == null)
            return;

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Label == label)
                return;
        }
        _items.Add(new DevTestItem { Label = label, OnClick = onClick });

        // 面板已经建好时，补一个按钮进去
        if (_panel != null)
            RebuildItemList();
    }

    // ══════════════════ 内置测试项 ══════════════════

    private static void RegisterBuiltinTests()
    {
        AddTest("清除引导存档（重新触发引导）", ClearGuidanceSave);
    }

    /// <summary>
    /// 清掉新手引导的完成记录，让引导可以重新走一遍。
    ///
    /// 同时清三处，缺一处就会「看起来清了但没生效」：
    ///   1. LocalRole.GuideTypes  服务端存档（下次登录不会再回填）
    ///   2. RoleM.GuideTypes      客户端内存（CFG_Guidance 的完成判断读这个）
    ///   3. RoleM.GuideId         当前引导步号（不清会停在上次的步骤）
    /// </summary>
    private static void ClearGuidanceSave()
    {
        try
        {
            LocalRole role = LocalData.Instance.Current;
            if (role != null)
            {
                if (role.GuideTypes == null)
                    role.GuideTypes = new List<int>();
                else
                    role.GuideTypes.Clear();

                SaveCoordinator.Instance.MarkDirty();
            }

            RoleM.Instance.GuideTypes = null;
            RoleM.Instance.GuideId = 0;
            RoleM.Instance.isGuiding = false;

            Toast("已清除引导存档，重进主界面即可重新触发");
        }
        catch (Exception e)
        {
            Toast("清除失败: " + e.Message);
        }
    }

    // ══════════════════ UI 构建 ══════════════════

    private static void BuildUI()
    {
        _font = GetFont();

        // 根 Canvas（独立于游戏其它 UI，sortingOrder 拉到很高保证在最上层）
        _root = new GameObject("DevTestCanvas");
        UnityEngine.Object.DontDestroyOnLoad(_root);

        Canvas canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1334f, 750f);
        scaler.matchWidthOrHeight = 0.5f;

        _root.AddComponent<GraphicRaycaster>();

        // 左侧悬浮按钮（首页左边空白处）
        CreateButton(_root.transform, "DevTestEntryBtn", "TEST",
            new Vector2(0f, 0.5f), new Vector2(90f, 60f), new Vector2(70f, -260f),
            new Color(0.85f, 0.35f, 0.15f, 0.9f), TogglePanel);

        // 面板（默认隐藏）
        _panel = CreatePanel(_root.transform);
        _panel.SetActive(false);
    }

    private static GameObject CreatePanel(Transform parent)
    {
        var panelGo = new GameObject("DevTestPanel");
        panelGo.transform.SetParent(parent, false);

        RectTransform rt = panelGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(520f, 640f);

        Image bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);

        // 标题
        CreateText(panelGo.transform, "Title", "内部测试面板", 24,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(260f, 30f), new Vector2(0f, -22f),
            TextAnchor.MiddleCenter, Color.white);

        // 关闭按钮
        CreateButton(panelGo.transform, "CloseBtn", "×",
            new Vector2(1f, 1f), new Vector2(48f, 48f), new Vector2(-32f, -32f),
            new Color(0.5f, 0.2f, 0.2f, 0.9f), () => _panel.SetActive(false));

        // 底部状态行
        _statusText = CreateText(panelGo.transform, "Status", "", 15,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 24f), new Vector2(0f, 18f),
            TextAnchor.MiddleCenter, new Color(0.7f, 0.9f, 0.7f));

        // 测试项容器
        var listGo = new GameObject("ItemList");
        listGo.transform.SetParent(panelGo.transform, false);
        RectTransform listRt = listGo.AddComponent<RectTransform>();
        listRt.anchorMin = new Vector2(0f, 0f);
        listRt.anchorMax = new Vector2(1f, 1f);
        listRt.offsetMin = new Vector2(16f, 16f);
        listRt.offsetMax = new Vector2(-16f, -64f);

        RebuildItemList();
        return panelGo;
    }

    /// <summary>按 _items 重建按钮列表（重新创建，最简单也最不容易出错）。</summary>
    private static void RebuildItemList()
    {
        if (_panel == null)
            return;

        Transform listT = _panel.transform.Find("ItemList");
        if (listT == null)
            return;

        for (int i = listT.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(listT.GetChild(i).gameObject);

        for (int i = 0; i < _items.Count; i++)
        {
            DevTestItem item = _items[i];
            float y = -20f - i * 68f;
            CreateButton(listT, "item_" + i, item.Label,
                new Vector2(0f, 1f), new Vector2(470f, 56f), new Vector2(250f, y),
                new Color(0.20f, 0.42f, 0.65f, 0.95f), item.OnClick);
        }
    }

    private static void TogglePanel()
    {
        if (_panel == null)
            return;
        _panel.SetActive(!_panel.activeSelf);
        if (_panel.activeSelf)
            RebuildItemList();
    }

    // ══════════════════ 基础控件 ══════════════════

    private static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 size, Vector2 pos, Color color, Action onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image img = go.AddComponent<Image>();
        img.color = color;

        Button btn = go.AddComponent<Button>();
        if (onClick != null)
            btn.onClick.AddListener(new UnityAction(onClick));

        CreateText(go.transform, "Label", label, 18,
            new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
            TextAnchor.MiddleCenter, Color.white, true);

        return go;
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 pos,
        TextAnchor align, Color color, bool stretch = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);

        if (stretch)
        {
            rt.offsetMin = new Vector2(6f, 4f);
            rt.offsetMax = new Vector2(-6f, -4f);
        }
        else
        {
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        Text txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = _font;
        txt.fontSize = fontSize;
        txt.alignment = align;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.raycastTarget = false;
        return txt;
    }

    /// <summary>
    /// 取一个可用字体。
    ///
    /// 优先**从场景里已有 UI 借**——游戏界面正在显示中文，说明一定有可用字体，
    /// 比自己 CreateDynamicFontFromOSFont 可靠得多（后者在 ILRuntime 下会拿到 null，
    /// 表现为按钮只有底色、文字全空）。
    /// </summary>
    private static Font GetFont()
    {
        try
        {
            Text[] texts = UnityEngine.Object.FindObjectsOfType<Text>();
            if (texts != null)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null && texts[i].font != null)
                        return texts[i].font;
                }
            }
        }
        catch { }

        try
        {
            Font f = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 20);
            if (f != null)
                return f;
        }
        catch { }

        try
        {
            Font f2 = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f2 != null)
                return f2;
        }
        catch { }

        try
        {
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        catch { }

        return null;
    }

    /// <summary>面板内状态行（不依赖外部 UI，保证任何上下文都能给反馈）。</summary>
    private static Text _statusText;

    private static void Toast(string msg)
    {
        Debug.Log("[DevTestPanel] " + msg);
        if (_statusText != null)
            _statusText.text = msg;
    }
}
