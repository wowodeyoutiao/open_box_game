using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class CFGItem_RankingAward : CFGItemBase
{
    public int UUID;

    public override long GetIdx()
    {
        return UUID;
    }

    /// <summary>
    /// 类别
    /// </summary>
    public int type;

    /// <summary>
    /// 排名
    /// </summary>
    public int rank;

    /// <summary>
    /// 排名奖励
    /// </summary>
    public Dictionary<string,int>[] award;

}

/// <summary>
/// RankingAward 配置表
/// </summary>
public class CFG_RankingAward : CFGTableBase<CFGItem_RankingAward>
{

    private static CFG_RankingAward _AllCFG;
    private static CFG_RankingAward AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_RankingAward, CFGItem_RankingAward>();
            }
            return _AllCFG;
        }
    }
    public static CFG_RankingAward Get()
    {
        return AllCFG;
    }

    public static CFGItem_RankingAward GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }


    public enum RankClass 
    {
        Level = 1,
        Power = 2,
        ZhanHun = 3, //即时赛榜
        SiLian = 4,
        WorldBoss = 20,
        WorldBossAreaServer = 21,
        FightEnd = 22,
        ServerRecord = 23,

        // 下面4个只用于奖励发放和现实，不参与排行
        TurnBasedDay = 24,   // 回合赛日榜奖励
        RealTimeWeek = 25,   // 即时赛周榜奖励
        TurnBasedWeek = 26,   // 回合赛周榜奖励
        PowerWeek = 27,   // 战力周榜奖励
    }

    private static Dictionary<int, Dictionary<int, CFGItem_RankingAward>> RankDic;

    public static Dictionary<int,CFGItem_RankingAward> GetRankDicByRankClass(RankClass rank)
    {
        if (RankDic == null)
        {
            RankDic = new Dictionary<int, Dictionary<int, CFGItem_RankingAward>>();

            Dictionary<int, CFGItem_RankingAward> _list = new Dictionary<int, CFGItem_RankingAward>();
            foreach (var item in Get().DicList)
            {
                if (!RankDic.ContainsKey(item.type))
                {
                    RankDic.Add(item.type, new Dictionary<int, CFGItem_RankingAward>());
                }
                RankDic[item.type].Add(item.rank, item);
            }
        }
        return RankDic[(int)rank];
    }

}
