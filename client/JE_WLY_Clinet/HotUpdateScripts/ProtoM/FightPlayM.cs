using JEngine.Helper;
using JEngine.Singleton;
using MetaFramework.UI;
using pb;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace HotUpdateScripts.ProtoM
{
    /// <summary>
    /// 玩法信息
    /// </summary>
    public class FightPlayM : SingletonTemplate<FightPlayM>
    {
        //玩法信息
        private InstanceInfoResponse _instanceInfo;

        private Dictionary<int,int> ReclicLevelStarDic = new Dictionary<int,int>();
        private Dictionary<int, int> ReclicChapterStarGetDic = new Dictionary<int, int>();

        public void SetInfo(InstanceInfoResponse data)
        {
            _instanceInfo = data;
            ReclicLevelStarDic.Clear();
            for (int i = 0; i < data.relic_map.Evaluation.Count; i++)
            {
                ReclicLevelStarDic.Add(data.relic_map.Evaluation[i].K, data.relic_map.Evaluation[i].V);
            }

            // ⚠️ 这里原来错把 ChapterReward 写进了 ReclicLevelStarDic（关卡星级表），
            //    导致 GetRelicChapterStarGet 读到的永远是 0、章节宝箱图标领完也不亮。
            //    ChapterReward 的 K 是**章节号**（1..20），和关卡号（1701001）不在一个量纲上，
            //    混进星级表还会让「章节 1」被当成「关卡 1」。
            ReclicChapterStarGetDic.Clear();
            for (int i = 0; i < data.relic_map.ChapterReward.Count; i++)
            {
                ReclicChapterStarGetDic[data.relic_map.ChapterReward[i].K] = data.relic_map.ChapterReward[i].V;
            }
        }

        public InstanceInfoResponse GetInfo()
        { 
            return _instanceInfo;
        }

        public int GetTrainLayers()
        {
            var battid = GetInfo().trial_tower_map.BattleId;
            if (battid == 0)
                return 0;
            return CFG_Battle.GetById(battid).Layers;
        }

        //获取遗迹关卡评价
        public int GetRelicLevelStar(int level)
        {
            if (!ReclicLevelStarDic.ContainsKey(level))
                return 0;
            return ReclicLevelStarDic[level];
        }

        //获取遗迹章节已经领取的章节
        public int GetRelicChapterStarGet(int chapter)
        {
            if (!ReclicChapterStarGetDic.ContainsKey(chapter))
                return 0;
            return ReclicChapterStarGetDic[chapter];
        }

        //战役点击红点
        public int GetChallenge_FightRedNum()
        {
            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play, false))
                return 0;
            return RedPointManager.Instance.GetRedNum(RedPointConst.Challenge_Fight_Click.ToString());
        }

        //战役领取红点
        public int GetChallenge_FightAwardRedNum()
        {
            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play, false))
                return 0;

            for (int i = 0; i < CFG_Battle.AllFightChapterAwardList.Count; i++)
            {
                if (GetInfo().battle_map.RewardStage < CFG_Battle.AllFightChapterAwardList[i].UID && CFG_Battle.AllFightChapterAwardList[i].UID <= GetInfo().battle_map.BattleId)
                    return 1;
            }
            return 0;
        }

        //首领副本红点
        public int GetChallenge_BossRedNum()
        {
            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_Boss, false))
                return 0;
            if (GetInfo().boss_map.ChallengeTime > 0)
                return 1;
            return 0;
        }

        //试炼塔副本红点
        public int GetChallenge_TowerRedNum()
        {
            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_SlTower, false))
                return 0;
            return RedPointManager.Instance.GetRedNum(RedPointConst.Challenge_Tower_Click.ToString());
        }

        //遗迹副本红点
        public int GetChallenge_ReliceRedNum()
        {
            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_Relice, false))
                return 0;
            if (GetInfo().relic_map.ChallengeTime > 0)
                return 1;
            return 0;
        }

        // 挂机升级红点
        public int GetChallenge_OnHook()
        {
            if (GameUtil.Instance.CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.DefendIsland, false))
                return 0;
            if (GetInfo().afk.DailyTime > 0)
                return 1;
            return 0;
        }

        public int GetFightEndUseTime(int battleId)
        {
            List<KeyValue> fightEnd = _instanceInfo.to_end.Evaluation;
            var cfg_battle = CFG_Battle.GetById(battleId);
            if (cfg_battle.Layers > fightEnd.Count)
                return 0;
            return _instanceInfo.to_end.Evaluation[cfg_battle.Layers - 1].K;
        }

        public bool IsFightEndNewRecord(int battleId, long useTime)
        {
            int time = GetFightEndUseTime(battleId);
            return time == 0 || useTime < time;
        }
    }

}

