using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class CFGItem_Map : CFGItemBase
{
    public int ID;
    public int High; // 高
    public int Wide; // 宽
    public List<CFGItem_MonsterId> MonsterIds; // 怪物列表
    public decimal MaxMonster; // 最大怪物数量

    public int MapID;
    public override long GetIdx()
    {
        return ID;
    }

    /// <summary>
    /// map 中怪物列表对应
    /// </summary>
    public class CFGItem_MonsterId
    {
        public int id;
        public int num;
    }
}

/// <summary>
/// Map 配置表
/// </summary>
public class CFG_Map : CFGTableBase<CFGItem_Map>
{
    private static CFG_Map _AllCFG;
    private static CFG_Map AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_Map, CFGItem_Map>();
            }
            return _AllCFG;
        }
    }
    public static CFG_Map Get()
    {
        return AllCFG;
    }

    public static CFGItem_Map GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }
}
