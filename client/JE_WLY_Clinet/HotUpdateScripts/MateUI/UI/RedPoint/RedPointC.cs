using HotUpdateScripts.ProtoM;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MetaFramework.UI
{
    public class RedPointC
    {
        public static Dictionary<RedPointConst, Func<int>> RedPointNumDic = new Dictionary<RedPointConst, Func<int>>
        {
            [RedPointConst.Role_Equip] = () => RedDotM.Instance.GetBeatEquipRed(),
            [RedPointConst.Pet_Award] = () => RedDotM.Instance.GetPetAwardNum(),
            [RedPointConst.Pet_UpStar_1] = () => RedDotM.Instance.GetPetUpStar(),

            [RedPointConst.Partner_Into] = () => RedDotM.Instance.PartnerIntoRedDot(),
            [RedPointConst.Partner_Call] = () => RedDotM.Instance.GetPartnerCallRedDot(),
            [RedPointConst.Partner_UpStar] = () => RedDotM.Instance.GetPartnerUpStarRedDot(),

            [RedPointConst.Equip_Reinforce] = () => RedDotM.Instance.GetAllEquipSlotReinforceRedNum(),
            [RedPointConst.Equip_Insert] = () => RedDotM.Instance.GetAllEquipInsetRedNum(),
            [RedPointConst.Equip_Star] = () => RedDotM.Instance.GetEquipStarRedNum(),

            [RedPointConst.Skill_UpLv] = () => RedDotM.Instance.GetAllSkillUpLvRedNum(),
            [RedPointConst.Skill_Advance] = () => RedDotM.Instance.GetAllSkillAdvanceRedNum(),
            [RedPointConst.Skill_Setting] = () => RedDotM.Instance.GetSkillSettingRedNum(),

            [RedPointConst.ExpPool] = () => RedDotM.Instance.GetExpPoolRedNum(),

            [RedPointConst.Challenge_Fight_Click] = () => FightPlayM.Instance.GetChallenge_FightRedNum(),
            [RedPointConst.Challenge_Fight_Award] = () => FightPlayM.Instance.GetChallenge_FightAwardRedNum(),
            [RedPointConst.Challenge_Boss] = () => FightPlayM.Instance.GetChallenge_BossRedNum(),
            [RedPointConst.Challenge_Tower_Click] = () => FightPlayM.Instance.GetChallenge_TowerRedNum(),
            [RedPointConst.Challenge_Relice] = () => FightPlayM.Instance.GetChallenge_ReliceRedNum(),
            [RedPointConst.Challenge_OnHook] = () => FightPlayM.Instance.GetChallenge_OnHook(),

            [RedPointConst.Bag_Syn] = () => RedDotM.Instance.GetSynRedNum(),
        };
    }
}
