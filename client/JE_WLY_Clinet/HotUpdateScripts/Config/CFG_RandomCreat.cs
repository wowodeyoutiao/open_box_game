using JEngine.Helper;
using System.Collections.Generic;

/// <summary>
/// 洗炼随机配置（randomcreat.json）。
///
/// ⚠️ 客户端这份配置被预处理过（原版服务端用原始格式）：
///   1. 加了一个自增 ID 当主键 —— 原表 RandomID 只有 25 个唯一值，
///      250 行里大量重复，直接当主键会让 CFGTableBase 建索引时冲突，
///      表现出来就是整张表加载失败 + ILRuntime 栈溢出。
///   2. KindNum / RefingSet 这些 {"1030":2000} 的动态 key 对象，
///      压平成平行 int 数组 —— 动态 key 映射到 Dictionary 在 ILRuntime 下取不到值。
///   转换脚本见 doc 68 节。
/// </summary>
public class CFGItem_RandomCreat : CFGItemBase
{
    /// <summary>客户端专用自增主键（1..250，唯一）</summary>
    public int ID;

    public override long GetIdx()
    {
        return ID;
    }

    /// <summary>原表主键，语义是「适用到多少级」，同一档会有多行（不同品质/部位）。</summary>
    public int RandomID;

    public int quality;

    /// <summary>适用部位（对应 item 配置的 Equiptype）</summary>
    public int[] SetID;

    /// <summary>属性种类权重：key = 属性ID*1000 + 分档</summary>
    public int[] KindNumKeys;
    public int[] KindNumWeights;

    /// <summary>洗炼档位权重（原表 RefingSet，注意少一个 n）</summary>
    public int[] RefingKeys;
    public int[] RefingWeights;
}

/// <summary>
/// 洗炼随机配置表。
///
/// 查询一律走「预建索引 + TryGetValue / 小列表遍历」，
/// **不遍历 DicDatas 的 KeyValuePair** —— 项目里能正常工作的配置表
/// （CFG_ItemUse / CFG_RandomDistrib）都是这么做的。
/// </summary>
public class CFG_RandomCreat : CFGTableBase<CFGItem_RandomCreat>
{
    private static CFG_RandomCreat _AllCFG;
    private static CFG_RandomCreat AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_RandomCreat, CFGItem_RandomCreat>();
            }
            return _AllCFG;
        }
    }

    public static CFG_RandomCreat Get()
    {
        return AllCFG;
    }

    private static Dictionary<int, List<CFGItem_RandomCreat>> _byQuality;
    private static Dictionary<int, List<CFGItem_RandomCreat>> ByQuality
    {
        get
        {
            if (_byQuality == null)
            {
                _byQuality = new Dictionary<int, List<CFGItem_RandomCreat>>();
                CFG_RandomCreat table = AllCFG;
                if (table != null && table.DicDatas != null)
                {
                    // 用 .Values 而不是 KeyValuePair 遍历
                    foreach (CFGItem_RandomCreat cfg in table.DicDatas.Values)
                    {
                        if (cfg == null)
                            continue;
                        List<CFGItem_RandomCreat> list;
                        if (!_byQuality.TryGetValue(cfg.quality, out list))
                        {
                            list = new List<CFGItem_RandomCreat>();
                            _byQuality.Add(cfg.quality, list);
                        }
                        list.Add(cfg);
                    }
                }
            }
            return _byQuality;
        }
    }

    /// <summary>
    /// 按品质 + 部位 + 等级取一行（对齐原服 GetRandomCreatCfg）：
    ///   quality 相等 → SetID 含 setid → RandomID >= level。
    /// 只在同品质的小列表里遍历。
    /// </summary>
    public static CFGItem_RandomCreat GetByQualitySet(int quality, int level, int setid)
    {
        Dictionary<int, List<CFGItem_RandomCreat>> idx = ByQuality;
        List<CFGItem_RandomCreat> list;
        if (idx == null || !idx.TryGetValue(quality, out list) || list == null)
            return null;

        CFGItem_RandomCreat best = null;
        for (int i = 0; i < list.Count; i++)
        {
            CFGItem_RandomCreat cfg = list[i];
            if (cfg == null || cfg.SetID == null || cfg.RandomID < level)
                continue;

            bool match = false;
            for (int j = 0; j < cfg.SetID.Length; j++)
            {
                if (cfg.SetID[j] == setid)
                {
                    match = true;
                    break;
                }
            }
            if (!match)
                continue;

            // 原服取遍历到的第一条；这里取 RandomID 最小的那条，结果更稳定
            if (best == null || cfg.RandomID < best.RandomID)
                best = cfg;
        }
        return best;
    }

    /// <summary>只读：索引里共有多少个品质分组，测试/诊断用。</summary>
    public static int QualityGroupCount
    {
        get { return ByQuality == null ? 0 : ByQuality.Count; }
    }
}
