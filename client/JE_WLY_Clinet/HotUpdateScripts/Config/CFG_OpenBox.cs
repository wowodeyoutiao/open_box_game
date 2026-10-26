using System.Collections.Generic;
using JEngine.Helper;

/// <summary>
/// 大厅开箱掉落配置（openbox.json）。
/// </summary>
public class CFGItem_OpenBox : CFGItemBase
{
    public int ID;
    public LevelRange[] Level;
    public EquipRandom[] Equiprandom;

    public override long GetIdx()
    {
        return ID;
    }

    public struct LevelRange
    {
        public int minlevel;
        public int maxlevel;
    }

    public struct EquipRandom
    {
        public int id;
        public int rate;
        public int job;
    }
}

public class CFG_OpenBox : CFGTableBase<CFGItem_OpenBox>
{
    private static CFG_OpenBox _AllCFG;

    private static CFG_OpenBox AllCFG
    {
        get
        {
            if (_AllCFG == null)
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_OpenBox, CFGItem_OpenBox>();
            return _AllCFG;
        }
    }

    public static CFG_OpenBox Get()
    {
        return AllCFG;
    }

    public static CFGItem_OpenBox GetById(long id)
    {
        return AllCFG.DicDatas[id];
    }

    public static CFGItem_OpenBox GetByLevel(int level)
    {
        if (AllCFG == null || AllCFG.DicDatas == null)
            return null;

        foreach (KeyValuePair<long, CFGItem_OpenBox> kv in AllCFG.DicDatas)
        {
            CFGItem_OpenBox cfg = kv.Value;
            if (cfg == null || cfg.Level == null)
                continue;

            for (int i = 0; i < cfg.Level.Length; i++)
            {
                CFGItem_OpenBox.LevelRange range = cfg.Level[i];
                if (level >= range.minlevel && level <= range.maxlevel)
                    return cfg;
            }
        }
        return null;
    }
}
