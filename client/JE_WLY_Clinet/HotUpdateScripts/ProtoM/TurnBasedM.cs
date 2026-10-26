using JEngine.Singleton;

using pb;

using System;
using System.Collections.Generic;

using UnityEngine;


namespace HotUpdateScripts.ProtoM
{
    public class TurnBasedM : SingletonTemplate<TurnBasedM>
    {
        public enum CampType
        {
            INVALID_CAMP,
            BLUE_CAMP,
            RED_CAMP,
        }

        public int roomId => 22;

        public Vector3 mapPosition => new Vector3(-0.4f, -5.4f, 0);

        public TurnBasedBattle turnBasedBattleData { get; set; }

        /// <summary>
        /// 对手uid
        /// </summary>
        public long opponentUid { get; set; }

        public TurnBasedBattle.TBPlayer GetPlayerByCampType(CampType campType)
        {
            return campType switch
            {
                CampType.BLUE_CAMP => turnBasedBattleData.OriginBlue,
                CampType.RED_CAMP => turnBasedBattleData.OriginRed,
                _ => null,
            };
        }

        public TurnBasedBattle.TBObject GetRoleInfo(long uid)
        {
            foreach (var roleInfo in turnBasedBattleData.OriginBlue.Objs)
            {
                if (roleInfo.Uid == uid)
                    return roleInfo;
            }

            foreach (var roleInfo in turnBasedBattleData.OriginRed.Objs)
            {
                if (roleInfo.Uid == uid)
                    return roleInfo;
            }

            return null;
        }

        /// <summary>
        /// 获取回合内的所有攻击
        /// </summary>
        /// <param name="round">回合数</param>
        /// <returns></returns>
        public List<TurnBasedBattle.Round> GetAttackActionsByRound(int round)
        {
            List<TurnBasedBattle.Round> actions = new List<TurnBasedBattle.Round>();
            foreach (var value in turnBasedBattleData.Rounds)
            {
                if (value.RoundId == round)
                    actions.Add(value);
            }
            return actions;
        }

        /// <summary>
        /// 获取攻击数据吧
        /// </summary>
        /// <param name="attackActionNum">攻击序号</param>
        /// <returns></returns>
        public TurnBasedBattle.Round GetAttackAction(int attackActionNum)
        {
            if (attackActionNum < 1 || attackActionNum > turnBasedBattleData.Rounds.Count)
                return null;

            return turnBasedBattleData.Rounds[attackActionNum - 1];
        }

        public CampType GetFirstAttackCamp()
        {
            if (turnBasedBattleData.Rounds.Count == 0)
                return CampType.INVALID_CAMP;

            return turnBasedBattleData.Rounds[0].ActionCamp == TurnBasedBattle.ActionCamp.BLUE_CAMP ? CampType.BLUE_CAMP : CampType.RED_CAMP;
        }

        public TurnBasedBattle.TBResult GetFightResult()
        {
            return turnBasedBattleData.Result;
        }
    }
}