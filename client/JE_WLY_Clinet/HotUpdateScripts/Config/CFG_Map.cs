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

    /// <summary>
    /// 出生点。对应原服 mapCfg.BornPoints，desk.playerJoin 从这里取玩家出生位置。
    /// </summary>
    public CFGItem_BornPoint[] Born;

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

        /// <summary>
        /// 刷怪锚点（原服 global.MonsterIds.point）。
        /// 每日副本 / 魂石副本这类「不进 battle.json」的房间靠它定位刷怪范围，
        /// 原服 desk 是拿 point 去 mapInfo.getAvailableCell 找空地。
        /// ⚠️ 是 struct，ILRuntime 下要先把元素拷到局部变量再读字段。
        /// </summary>
        public CFGItem_BornPoint point;
    }

    /// <summary>
    /// 出生点坐标
    /// </summary>
    public struct CFGItem_BornPoint
    {
        public int x;
        public int y;
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
