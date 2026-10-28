using JEngine.Helper;

public class CFGItem_ItemUse : CFGItemBase
{
    public int ID;

    public override long GetIdx()
    {
        return ID;
    }

    public string Name;

    /// <summary>使用后给的东西（道具使用配置的 Givegoods 列）。</summary>
    public GiveGoodsItem[] Givegoods;

    public class GiveGoodsItem
    {
        public int id;
        public int num;
    }
}

/// <summary>
/// 道具使用配置表（itemuse.json）。
/// 只包含「可以主动使用」的道具：宝箱类、经验/体力药等。
/// 没有出现在这张表里的道具（装备/材料/魂石）就是「不能使用」。
/// </summary>
public class CFG_ItemUse : CFGTableBase<CFGItem_ItemUse>
{
    private static CFG_ItemUse _AllCFG;
    private static CFG_ItemUse AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_ItemUse, CFGItem_ItemUse>();
            }
            return _AllCFG;
        }
    }

    public static CFG_ItemUse Get()
    {
        return AllCFG;
    }

    /// <summary>取道具的使用配置；道具不可使用时返回 null。</summary>
    public static CFGItem_ItemUse GetById(int id)
    {
        CFG_ItemUse table = AllCFG;
        if (table == null || table.DicDatas == null)
            return null;
        CFGItem_ItemUse item;
        return table.DicDatas.TryGetValue(id, out item) ? item : null;
    }
}
