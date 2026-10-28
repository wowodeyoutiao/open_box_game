using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class CFGItem_EquipStar : CFGItemBase
{
    public int UUID;

    public override long GetIdx()
    {
        return UUID;
    }


    public int Job;

    public int StarLevel;

    /// <summary>升星成功概率（万分比，10000 = 100%）。</summary>
    public int SuccessRate;

    /// <summary>
    /// 升星失败后掉到的星级（原服 stars() 里 `equipsInfo.Stars = StarsNeed.FailLevel`）。
    /// 注意不是「掉几级」，而是「掉到第几星」。
    /// </summary>
    public int FailLevel;

    /// <summary>配置里的失败降级列，原服逻辑未使用（只进埋点），保留以对齐配置。</summary>
    public int DecLevel;

    public Material_Item[] MaterialID;

    public struct Material_Item
    {
        public int id;
        public int num;
    }

    public Attribute_Item[] Attribute;

    public struct Attribute_Item
    {
        public int slot;
        public int attribute;
        public int value;
    }


}

/// <summary>
/// EquipStar 配置表
/// </summary>
public class CFG_EquipStar : CFGTableBase<CFGItem_EquipStar>
{

    private static CFG_EquipStar _AllCFG;
    private static CFG_EquipStar AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_EquipStar, CFGItem_EquipStar>();
            }
            return _AllCFG;
        }
    }
    public static CFG_EquipStar Get()
    {
        return AllCFG;
    }

    public static CFGItem_EquipStar GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }

    public static CFGItem_EquipStar GetByJobAndLv(int job,int lv)
    {
        foreach (var item in Get().DicDatas.Values)
        {
            if (item.Job == job && item.StarLevel == lv)
                return item;
        }
        return default;
    }

    public static CFGItem_EquipStar.Attribute_Item GetAttrByJobAndLvSolt(int job, int lv,int solt)
    {
        foreach (var item in Get().DicDatas.Values)
        {
            if (item.Job == job && item.StarLevel == lv && item.Attribute != null)
            {
                foreach (var att in item.Attribute)
                {
                    if (att.slot == solt)
                        return att;
                }
            }
        }
        return default;
    }


    public static int GetMaxStar(int job)
    {
        int Count = 0;
        foreach (var item in Get().DicDatas)
        {
            if (item.Value.Job == job)
                Count ++;
        }
        return Count - 1;
    }

}
