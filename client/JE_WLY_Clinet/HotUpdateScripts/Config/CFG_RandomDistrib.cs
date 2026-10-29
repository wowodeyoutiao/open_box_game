using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class CFGItem_RandomDistrib : CFGItemBase
{
    public int Custommark;

    public override long GetIdx()
    {
        return Custommark;
    }

    public int UUID;

    public int Kind;

    public int ValueID;

    public int PieceMax_Blue;
    public int PieceMin_Blue;

    public int PieceMin_Purple;
    public int PieceMax_Purple;

    public int PieceMin_Pink;
    public int PieceMax_Pink;

    public int PieceMin_Gold;
    public int PieceMax_Gold;

    // 红 / 彩虹品质的区间（原服 bootstrap/init.go 把 PieceMin_Red / PieceMin_Yellow
    // 分别映射到 ItemQuality_RED(7) / ItemQuality_Rainbow(8)）。之前没解析，洗炼到这两档会取不到区间。
    public int PieceMin_Red;
    public int PieceMax_Red;
    public int PieceMin_Yellow;
    public int PieceMax_Yellow;

    public int Color;

}

/// <summary>
/// RandomDistrib 配置表
/// </summary>
public class CFG_RandomDistrib : CFGTableBase<CFGItem_RandomDistrib>
{

    private static CFG_RandomDistrib _AllCFG;
    private static CFG_RandomDistrib AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_RandomDistrib, CFGItem_RandomDistrib>();
            }
            return _AllCFG;
        }
    }
    public static CFG_RandomDistrib Get()
    {
        return AllCFG;
    }

    public static CFGItem_RandomDistrib GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }
    private static Dictionary<int, List<CFGItem_RandomDistrib>> _DicRandom;
    private static Dictionary<int, List<CFGItem_RandomDistrib>> DicRandom { 
        get 
        {
            if (_DicRandom == null)
            {
                _DicRandom = new Dictionary<int, List<CFGItem_RandomDistrib>>();
                foreach (var item in Get().DicDatas.Values)
                {
                    if (!_DicRandom.ContainsKey(item.Kind))
                    {
                        _DicRandom.Add(item.Kind, new List<CFGItem_RandomDistrib>());
                    }
                    _DicRandom[item.Kind].Add(item);
                }
                foreach (var item in _DicRandom)
                {
                    item.Value.Sort((a, b) => { return a.ValueID.CompareTo(b.ValueID); });
                }
            }
            return _DicRandom;
        } 
    }

    public static int GetColor(int equipQuile,int arrt,int value)
    {
        int color = 1;
        if (DicRandom.ContainsKey(arrt) && equipQuile > 2)
        {
            foreach (var item in DicRandom[arrt])
            {
                if (equipQuile == 3)
                {
                    if (value >= item.PieceMin_Blue && value <= item.PieceMax_Blue)
                    {
                        color = item.Color;
                        break;
                    }
                }
                else if (equipQuile == 4)
                {
                    if (value >= item.PieceMin_Purple && value <= item.PieceMax_Purple)
                    {
                        color = item.Color;
                        break;
                    }
                }
                else if (equipQuile == 5)
                {
                    if (value >= item.PieceMin_Pink && value <= item.PieceMax_Pink)
                    {
                        color = item.Color;
                        break;
                    }
                }
                else if (equipQuile == 6)
                {
                    if (value >= item.PieceMin_Gold && value <= item.PieceMax_Gold)
                    {
                        color = item.Color;
                        break;
                    }
                }
            }
        }
        return color;
    }

    public static int GetMaxValue(int equipQuile, int arrt)
    {
        int num = 0;
        if (DicRandom.ContainsKey(arrt) && equipQuile > 2)
        {
            var maxItem = DicRandom[arrt][DicRandom[arrt].Count - 1];
           
            if (equipQuile == 3)
            {
                num = maxItem.PieceMax_Blue;
            }
            else if (equipQuile == 4)
            {
                num = maxItem.PieceMax_Purple;
            }
            else if (equipQuile == 5)
            {
                num = maxItem.PieceMax_Pink;
            }
            else if (equipQuile == 6)
            {
                num = maxItem.PieceMax_Gold;
            }
        }
        return num;
    }

    // ==================== 洗炼用的直查索引 ====================
    // 对齐原服 GetRandomDistribCfg(UUID, part) / GetRandomDistribCfgByKind(kind, part)。
    // 用 Dictionary.TryGetValue 直查，不遍历 DicDatas 的 KeyValuePair。

    private static Dictionary<int, CFGItem_RandomDistrib> _byUuidPart;
    private static Dictionary<int, CFGItem_RandomDistrib> ByUuidPart
    {
        get
        {
            if (_byUuidPart == null)
            {
                _byUuidPart = new Dictionary<int, CFGItem_RandomDistrib>();
                foreach (CFGItem_RandomDistrib cfg in Get().DicDatas.Values)
                {
                    if (cfg == null) continue;
                    int key = cfg.UUID * 10000 + cfg.ValueID;
                    if (!_byUuidPart.ContainsKey(key))
                        _byUuidPart.Add(key, cfg);
                }
            }
            return _byUuidPart;
        }
    }

    private static Dictionary<int, CFGItem_RandomDistrib> _byKindPart;
    private static Dictionary<int, CFGItem_RandomDistrib> ByKindPart
    {
        get
        {
            if (_byKindPart == null)
            {
                _byKindPart = new Dictionary<int, CFGItem_RandomDistrib>();
                foreach (CFGItem_RandomDistrib cfg in Get().DicDatas.Values)
                {
                    if (cfg == null) continue;
                    int key = cfg.Kind * 10000 + cfg.ValueID;
                    if (!_byKindPart.ContainsKey(key))
                        _byKindPart.Add(key, cfg);
                }
            }
            return _byKindPart;
        }
    }

    /// <summary>按 (UUID, 档位) 查，对齐原服 GetRandomDistribCfg。</summary>
    public static CFGItem_RandomDistrib GetByUuidAndPart(int uuid, int valuePart)
    {
        Dictionary<int, CFGItem_RandomDistrib> idx = ByUuidPart;
        CFGItem_RandomDistrib cfg;
        return idx != null && idx.TryGetValue(uuid * 10000 + valuePart, out cfg) ? cfg : null;
    }

    /// <summary>按 (属性种类, 档位) 查，对齐原服 GetRandomDistribCfgByKind。</summary>
    public static CFGItem_RandomDistrib GetByKindAndPart(int kind, int valuePart)
    {
        Dictionary<int, CFGItem_RandomDistrib> idx = ByKindPart;
        CFGItem_RandomDistrib cfg;
        return idx != null && idx.TryGetValue(kind * 10000 + valuePart, out cfg) ? cfg : null;
    }
}
