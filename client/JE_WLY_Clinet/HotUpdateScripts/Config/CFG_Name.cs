using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class CFGItem_Name : CFGItemBase
{
    public int ID;
    public string Firstname; 
    public string Familyname;
    public string Lastname;

    public override long GetIdx()
    {
        return ID;
    }
   
}

/// <summary>
/// CFG_Name 配置表
/// </summary>
public class CFG_Name : CFGTableBase<CFGItem_Name>
{

    private static CFG_Name _AllCFG;
    private static CFG_Name AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_Name, CFGItem_Name>();
            }
            return _AllCFG;
        }
    }
    public static CFG_Name Get()
    {
        return AllCFG;
    }

    public static Dictionary<long, CFGItem_Name> GetDic()
    {
        return AllCFG.DicDatas;
    }


    public static CFGItem_Name GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }
}
