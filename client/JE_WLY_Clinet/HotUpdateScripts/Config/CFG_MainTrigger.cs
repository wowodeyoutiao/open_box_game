using JEngine.Helper;
using System.Collections.Generic;

/// <summary>
/// 大厅挂机触发表：maintrigger.json
/// </summary>
public class CFGItem_MainTrigger : CFGItemBase
{
    public int ID;

    /// <summary>适用等级区间。</summary>
    public LevelRange[] Level;

    /// <summary>触发事件池。</summary>
    public MonsterWeight[] MonsterIds;

    /// <summary>背景地图，当前单机不使用。</summary>
    public int BackgroundmapID;

    public override long GetIdx()
    {
        return ID;
    }

    [System.Serializable]
    public struct LevelRange
    {
        public int minlevel;
        public int maxlevel;
    }

    [System.Serializable]
    public struct MonsterWeight
    {
        /// <summary>1=普通奖励，2=怪物掉落。</summary>
        public int type;
        public int id;
        public int num;
        public int rate;
    }
}

/// <summary>
/// 大厅挂机触发表。
/// </summary>
public class CFG_MainTrigger : CFGTableBase<CFGItem_MainTrigger>
{
    private static CFG_MainTrigger _AllCFG;

    private static CFG_MainTrigger AllCFG
    {
        get
        {
            if (_AllCFG == null)
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_MainTrigger, CFGItem_MainTrigger>();
            return _AllCFG;
        }
    }

    public static CFG_MainTrigger Get()
    {
        return AllCFG;
    }

    public static CFGItem_MainTrigger GetById(long id)
    {
        return AllCFG.DicDatas[id];
    }

    public static CFGItem_MainTrigger GetByLevel(int level)
    {
        if (AllCFG == null || AllCFG.DicDatas == null)
            return null;

        foreach (KeyValuePair<long, CFGItem_MainTrigger> kv in AllCFG.DicDatas)
        {
            CFGItem_MainTrigger cfg = kv.Value;
            if (cfg == null || cfg.Level == null)
                continue;
            for (int i = 0; i < cfg.Level.Length; i++)
            {
                if (level >= cfg.Level[i].minlevel && level <= cfg.Level[i].maxlevel)
                    return cfg;
            }
        }
        return null;
    }
}
