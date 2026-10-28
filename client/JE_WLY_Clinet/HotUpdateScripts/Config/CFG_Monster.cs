using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class CFGItem_Monster : CFGItemBase
{
    public int ID;

    public override long GetIdx()
    {
        return ID;
    }

    /// <summary>
    /// Monster名称
    /// </summary>
    public string Name;

    /// <summary>
    /// 等级
    /// </summary>
    public int Level;

    /// <summary>
    /// boss标识
    /// </summary>
    public int Isboss;

    /// <summary>
    /// 是否显示血条
    /// </summary>
    public int Isdisplaybar;

    /// <summary>
    /// 是否显示等级
    /// </summary>
    public int Isdisplaylevel;

    public int AttackDistance;

    /// <summary>追击半径上限，离出生点超过该距离就脱战回位（原服 monsterCfg.Chase）。</summary>
    public int Chase;

    /// <summary>索敌/出生点散布范围（原服 monsterCfg.ViewRange）。</summary>
    public int ViewRange;

    public string Model;

    public string AttackEffect;

    public string Photo;
    public string Photoname { get; set; }

    public string Icon;

    public string Iconname { get; set; }

    public int Delay;

    /// <summary>怪物攻击技能 ID，服务端 MonsterConfig.Skills 同名。</summary>
    public int[] Skills;

    public Dictionary<string, int>[] BassProperty;

    public int Quality;

    public decimal Barnum;

    /// <summary>击杀经验。</summary>
    public int Experience;

    /// <summary>
    /// 重生间隔（**毫秒**）。对齐原服 MonsterConfig.Relive，
    /// monster_mgr 里是 time.AfterFunc(millisecond * reliveTime) 原地复活。
    /// 只有 PVEPeacefulMap(守卫海岛) / StarUpStoneFBMap / SoulStoneFBMap 这类
    /// 刷怪房间会用到；战役/首领/试炼塔是一次性刷怪，不读这个值。
    ///
    /// ⚠️ 配置里是浮点写法（30000.0），所以用 decimal 接收；
    ///    ILRuntime 下 decimal 只能转 int/long，读的时候写 (int)cfg.Relive。
    /// </summary>
    public decimal Relive;

    /// <summary>掉落配置。</summary>
    public DorpItem[] Dorp;

    [System.Serializable]
    public class DorpItem
    {
        public int[] id;
        public DorpNum num;
        public int ratio;
    }

    [System.Serializable]
    public class DorpNum
    {
        public int min;
        public int max;
    }
}

/// <summary>
/// Monster 配置表
/// </summary>
public class CFG_Monster : CFGTableBase<CFGItem_Monster>
{
    private static CFG_Monster _AllCFG;
    private static CFG_Monster AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_Monster, CFGItem_Monster>();
            }
            return _AllCFG;
        }
    }
    public static CFG_Monster Get()
    {
        return AllCFG;
    }

    public static CFGItem_Monster GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }

    public static string GetHUDNameById(int _cfgID)
    {
        CFGItem_Monster info = GetById(_cfgID); 
        //return $"{(info.Isdisplaylevel == 1 ? "Lv" + info.Level : "")}{GameUtil.Instance.GetStrByQuality(info.Quality, info.Name)}";
        return $"{GameUtil.Instance.GetFightStrByQuality(info.Quality, info.Name)}";
    }

    public static string GetMonsterNameById(int _cfgID)
    {
        CFGItem_Monster info = GetById(_cfgID);
        return $"{GameUtil.Instance.GetStrByQuality(info.Quality, info.Name)}";
    }
    public static string GetMonsterNameByLv(int _cfgID)
    {
        CFGItem_Monster info = GetById(_cfgID);
        return $"{(info.Isdisplaylevel == 1 ? "Lv" + info.Level : "")}";
    }
}
