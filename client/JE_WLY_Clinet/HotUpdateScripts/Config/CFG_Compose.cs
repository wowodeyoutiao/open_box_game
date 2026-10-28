using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class CFGItem_Compose : CFGItemBase
{
    public int ComposeID;

    public override long GetIdx()
    {
        return ComposeID;
    }

    public int[] Raw;

    public class NeedGoldItem {
        public int id;
        public int num;
    }
    public NeedGoldItem NeedGold;

    /// <summary>
    /// 产出结果（对齐原服 ComposeConfig.Produced）。
    ///
    /// 多段按 ratio 累加分段，段内随机取一个 id：
    ///   魂石合成 ComposeID 30..85 → 9000 段 = 高一级「无自然系」单 id，
    ///                              1000 段 = 高一级 4 种自然系随机一个
    /// ⚠️ 原来这个字段没定义（json 有、类里没有），解析出来永远是 null，
    ///    合成必然算不出产出，属于 48 节配置对账扫出来的缺口。
    /// </summary>
    public ProducedItem[] Produced;

    [System.Serializable]
    public class ProducedItem
    {
        public int[] id;
        public int ratio;
    }

    public decimal NeedNum { get; set; }
    public decimal SuccessRate { get; set; }

}

/// <summary>
/// Compose 配置表
/// </summary>
public class CFG_Compose : CFGTableBase<CFGItem_Compose>
{

    private static CFG_Compose _AllCFG;
    private static CFG_Compose AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_Compose, CFGItem_Compose>();
            }
            return _AllCFG;
        }
    }
    public static CFG_Compose Get()
    {
        return AllCFG; 
    }

    public static CFGItem_Compose GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }

    public static CFGItem_Compose GetByRawId(int id)
    {
        var AllList =  Get().DicList;
        for (int i = 0; i < AllList.Count; i++)
        {
            var raws = AllList[i].Raw;
            for (int j = 0; j < raws.Length; j++)
            {
                if (id == raws[j])
                    return AllList[i];
            }
        }
        return null;
    }

}
