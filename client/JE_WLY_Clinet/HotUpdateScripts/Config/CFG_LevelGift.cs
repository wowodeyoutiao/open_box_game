using JEngine.Helper;
using System.Collections.Generic;

/// <summary>
/// 升级礼包表：levelgift.json
/// </summary>
public class CFGItem_LevelGift : CFGItemBase
{
    public int UID;
    public int Level;
    public GiftItem[] Gift;

    public override long GetIdx()
    {
        return UID;
    }

    [System.Serializable]
    public struct GiftItem
    {
        public int id;
        public long num;
        public int job;
        public int gender;
    }
}

public class CFG_LevelGift : CFGTableBase<CFGItem_LevelGift>
{
    private static CFG_LevelGift _AllCFG;

    private static CFG_LevelGift AllCFG
    {
        get
        {
            if (_AllCFG == null)
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_LevelGift, CFGItem_LevelGift>();
            return _AllCFG;
        }
    }

    public static CFG_LevelGift Get()
    {
        return AllCFG;
    }

    public static CFGItem_LevelGift GetByLevel(int level)
    {
        if (AllCFG == null || AllCFG.DicDatas == null)
            return null;
        foreach (KeyValuePair<long, CFGItem_LevelGift> kv in AllCFG.DicDatas)
        {
            if (kv.Value != null && kv.Value.Level == level)
                return kv.Value;
        }
        return null;
    }
}
