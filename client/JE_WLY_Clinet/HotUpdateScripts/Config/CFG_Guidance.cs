using HotUpdateScripts.ProtoM;

using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 引导
/// </summary>
public class CFGItem_Guidance : CFGItemBase
{
    public int ID;

    public override long GetIdx()
    {
        return (int)ID;
    }

    public int type; // 1: 开箱引导 2: 伙伴上阵引导
    
    public string describe;

}

/// <summary>
/// Guidance 配置表
/// </summary>
public class CFG_Guidance : CFGTableBase<CFGItem_Guidance>
{
    private static CFG_Guidance _AllCFG;
    private static CFG_Guidance AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_Guidance, CFGItem_Guidance>();
            }
            return _AllCFG;
        }
    }

    public static CFG_Guidance Get()
    {
        return AllCFG;
    }

    public static Dictionary<long, CFGItem_Guidance> GetDic()
    {
        return AllCFG.DicDatas;
    }

    public static CFGItem_Guidance GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }

    public static int GetType(int id)
    {
        return AllCFG.DicDatas[id].type;
    }

    /// <summary>
    /// 最后一个引导ID
    /// </summary>
    /// <returns></returns>
    public static int LastGuideId()
    {
        return AllCFG.DicList.Count;
    }

    /// <summary>
    /// 根据类型获取引导步骤数
    /// </summary>
    /// <param name="type">引导类型</param>
    /// <returns></returns>
    public static int GetStepCountByType(GuideType type)
    {
        int stepCount = 0;
        for (int i = 0; i < AllCFG.DicList.Count; i++)
        {
            if (AllCFG.DicList[i].type == (int)type)
            {
                stepCount++;
            }
        }
        return stepCount;
    }

    /// <summary>
    /// 根据类型获取引导列表
    /// </summary>
    /// <param name="type">引导类型</param>
    /// <returns></returns>
    public static List<CFGItem_Guidance> GetGuidancs(GuideType type)
    {
        List<CFGItem_Guidance> guidancs = new List<CFGItem_Guidance>();
        for (int i = 0; i < AllCFG.DicList.Count; i++)
        {
            if (AllCFG.DicList[i].type == (int)type)
            {
                guidancs.Add(AllCFG.DicList[i]);
            }
        }
        return guidancs;
    }

    /// <summary>
    /// 引导全部完成
    /// </summary>
    /// <returns></returns>
    public static bool AllGuideISCompleted()
    {
        return RoleM.Instance.GuideId > AllCFG.DicList.Count;
    }

    /// <summary>
    /// 某个类型的第一个引导ID
    /// </summary>
    /// <param name="type">引导类型</param>
    /// <returns></returns>
    public static int GetFirstIdByType(GuideType type)
    {
        for (int i = 0; i < AllCFG.DicList.Count - 1; i++)
        {
            if (AllCFG.DicList[i].type == (int)type)
            {
                return AllCFG.DicList[i].ID;
            }
        }
        return 0;
    }

    /// <summary>
    /// 某个类型的最后一个引导ID
    /// </summary>
    /// <param name="type">引导类型</param>
    /// <returns></returns>
    public static int GetLastSetpIdByType(GuideType type)
    {
        for (int i = AllCFG.DicList.Count - 1; i >= 0; i--)
        {
            if (AllCFG.DicList[i].type == (int)type)
            {
                return AllCFG.DicList[i].ID;
            }
        }
        return 0;
    }

    /// <summary>
    /// 是否跳过引导。如果玩家跳过引导，或者引导过程中直接退出了游戏，再次登录之后就跳过这个模块的引导。
    /// </summary>
    /// <param name="type">引导类型</param>
    /// <returns></returns>
    public static bool IsNeedJumpThisTypeGuide(GuideType type)
    {
        return IsNeedJumpThisTypeGuide((int)type);
    }

    /// <summary>
    /// 重新登陆游戏后，是否跳过引导。如果玩家跳过引导，或者引导过程中直接退出游戏了，再次登录之后就跳过这个模块的引导。
    /// </summary>
    /// <param name="type">引导类型</param>
    /// <returns></returns>
    public static bool IsNeedJumpThisTypeGuide(int type)
    {
        if (RoleM.Instance.GuideTypes == null || RoleM.Instance.GuideTypes.Count == 0)
            return false;

        return RoleM.Instance.GuideTypes.Contains(type);
    }

    /// <summary>
    /// 显示摇杆
    /// </summary>
    /// <param name="id">id</param>
    /// <returns></returns>
    public static bool IsNeedShowJoy(int id)
    {
        return id == 0;
    }

    public enum GuideType
    {
        None = 0,
        OpenBox = 1, // 开箱引导
        Partner = 2, // 伙伴上阵引导
    }
}
