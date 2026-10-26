using pb;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MetaFramework.UI
{
    public class RedPointManager
    {
        public RedPointSystems RPS;

        private static RedPointManager _instance;
        public static RedPointManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new RedPointManager();
                    _instance.RPS = new RedPointSystems();
                    _instance.InitNode();
                    _instance.RPS.Init(_instance.baseNode);

                }
                return _instance;
            }
        }
        private RedPointNode baseNode;

        private void InitNode()
        {
            baseNode = new RedPointNode(RedPointConst.BaseNode.ToString());
            {
                RedPointNode SetNode = baseNode.AddChildNode(RedPointConst.Set.ToString());
                {
                    SetNode.AddChildNode(RedPointConst.Set_Email.ToString());
                }

                RedPointNode Hall_TopBtnNode = baseNode.AddChildNode(RedPointConst.Hall_TopBtn.ToString());
                {
                    // 活动（七日签到，七日狂欢）
                    RedPointNode ActiveCenterNode = Hall_TopBtnNode.AddChildNode(RedPointConst.ActiveCenter.ToString());
                    {
                        ActiveCenterNode.AddChildNode(RedPointConst.ActiveCenter_Sign7.ToString());
                        ActiveCenterNode.AddChildNode(TaskClass.SEVENDAY.ToString());
                    }

                    // 每日礼包、每日特价、成长基金、宠物礼包、伙伴礼包、月卡
                    RedPointNode GiftBagNode = Hall_TopBtnNode.AddChildNode(RedPointConst.GiftBag.ToString());
                    {
                        GiftBagNode.AddChildNode(RedPointConst.GiftBag_DailyGift.ToString());
                        GiftBagNode.AddChildNode(RedPointConst.GiftBag_GiftPay.ToString());
                        GiftBagNode.AddChildNode(RedPointConst.GiftBag_GrowthFund.ToString());
                        GiftBagNode.AddChildNode(RedPointConst.GiftBag_PetGift.ToString());
                        GiftBagNode.AddChildNode(RedPointConst.GiftBag_PartnerGift.ToString());
                        GiftBagNode.AddChildNode(RedPointConst.GiftBag_Month.ToString());
                        GiftBagNode.AddChildNode(RedPointConst.GiftBag_Gemstone.ToString());
                    }

                    RedPointNode RankNode = Hall_TopBtnNode.AddChildNode(RedPointConst.Rank.ToString());
                    {
                        RankNode.AddChildNode(RedPointConst.Rank_Level.ToString());
                        RankNode.AddChildNode(RedPointConst.Rank_Power.ToString());
                        RankNode.AddChildNode(RedPointConst.Rank_SiLian.ToString());
                        RankNode.AddChildNode(RedPointConst.Rank_ZhanHun.ToString());
                        RankNode.AddChildNode(RedPointConst.Rank_HuiHe.ToString());
                    }

                    //Hall_TopBtnNode.AddChildNode(RedPointConst.SignDay.ToString());
                    Hall_TopBtnNode.AddChildNode(RedPointConst.PrivilegeCard.ToString());
                    Hall_TopBtnNode.AddChildNode(RedPointConst.FirstPay.ToString());

                    //Hall_TopBtnNode.AddChildNode(RedPointConst.WorldBoss.ToString());
                    //Hall_TopBtnNode.AddChildNode(RedPointConst.DayDo.ToString());
                    //Hall_TopBtnNode.AddChildNode(RedPointConst.TurnBox.ToString());
                }

                RedPointNode Hall_LeftBtnNode = baseNode.AddChildNode(RedPointConst.Hall_LeftBtn.ToString());
                {
                    Hall_LeftBtnNode.AddChildNode(RedPointConst.LimitedBounty.ToString());
                    Hall_LeftBtnNode.AddChildNode(RedPointConst.RouletteWheel.ToString());
                }


                RedPointNode Hall_VipBtnNode = baseNode.AddChildNode(RedPointConst.VIP_Enter.ToString());
                {
                    //Hall_VipBtnNode.AddChildNode(RedPointConst.VIP_Buy.ToString());
                    Hall_VipBtnNode.AddChildNode(TaskClass.VIP.ToString());
                }

                // 任务
                RedPointNode TaskNode = baseNode.AddChildNode(RedPointConst.Task.ToString());
                {
                    TaskNode.AddChildNode(RedPointConst.SignDay.ToString());
                    TaskNode.AddChildNode(pb.TaskClass.DAILY.ToString());
                    //TaskNode.AddChildNode(pb.TaskClass.ROUTINE.ToString());
                    TaskNode.AddChildNode(pb.TaskClass.WEEKLY.ToString());
                    TaskNode.AddChildNode(pb.TaskClass.ACHIEVEMENT.ToString());
                }

                baseNode.AddChildNode(RedPointConst.BoxLevel.ToString());

                baseNode.AddChildNode(RedPointConst.PVE.ToString());
                baseNode.AddChildNode(RedPointConst.Tower_Get.ToString());

                RedPointNode BagNode = baseNode.AddChildNode(RedPointConst.Bag.ToString());
                {
                    BagNode.AddChildNode(RedPointConst.Bag_Syn.ToString());
                }

                RedPointNode RoleNode = baseNode.AddChildNode(RedPointConst.Role.ToString());
                {
                    //一键装备
                    RoleNode.AddChildNode(RedPointConst.Role_Equip.ToString());

                    //宠物
                    RedPointNode Pet = RoleNode.AddChildNode(RedPointConst.Role_Pet.ToString());
                    {
                        Pet.AddChildNode(RedPointConst.Pet_Up.ToString());
                        Pet.AddChildNode(RedPointConst.Pet_Award.ToString());

                        RedPointNode PetNode = Pet.AddChildNode(RedPointConst.Pet_UpStar.ToString());
                        {
                            PetNode.AddChildNode(RedPointConst.Pet_UpStar_1.ToString());
                        }
                    }

                    //伙伴
                    RoleNode.AddChildNode(RedPointConst.Partner_Into.ToString());
                    RoleNode.AddChildNode(RedPointConst.Partner_Call.ToString());
                    RoleNode.AddChildNode(RedPointConst.Partner_UpStar.ToString());

                    //经验池
                    RedPointNode ExpPool_First = RoleNode.AddChildNode(RedPointConst.ExpPool_First.ToString());
                    {
                        ExpPool_First.AddChildNode(RedPointConst.ExpPool.ToString());
                    }

                    //打造
                    RedPointNode Build = RoleNode.AddChildNode(RedPointConst.Build.ToString());
                    {
                        RedPointNode Build2 = Build.AddChildNode(RedPointConst.Build2.ToString());
                        {
                            RedPointNode Build3 = Build2.AddChildNode(RedPointConst.Build3.ToString());
                            {
                                RedPointNode EquipNode = Build3.AddChildNode(RedPointConst.Equip.ToString());
                                {
                                    EquipNode.AddChildNode(RedPointConst.Equip_Reinforce.ToString());
                                    EquipNode.AddChildNode(RedPointConst.Equip_Star.ToString());
                                    EquipNode.AddChildNode(RedPointConst.Equip_Insert.ToString());
                                }

                                RedPointNode SkillNode = Build3.AddChildNode(RedPointConst.Skill.ToString());
                                {
                                    SkillNode.AddChildNode(RedPointConst.Skill_Advance.ToString());
                                    SkillNode.AddChildNode(RedPointConst.Skill_UpLv.ToString());
                                    SkillNode.AddChildNode(RedPointConst.Skill_Setting.ToString());
                                }
                            }
                        }
                    }
                }

                RedPointNode GuildNode = baseNode.AddChildNode(RedPointConst.Guild.ToString());
                {
                    RedPointNode GuildTagNode = GuildNode.AddChildNode(RedPointConst.Guild_Tag.ToString());
                    {
                        GuildTagNode.AddChildNode(RedPointConst.Guild_InGuild.ToString());
                        GuildTagNode.AddChildNode(RedPointConst.Guild_Info.ToString());
                        RedPointNode GuildMember = GuildTagNode.AddChildNode(RedPointConst.Guild_Member.ToString());
                        {
                            GuildMember.AddChildNode(RedPointConst.Guild_Apply.ToString());
                        }
                        GuildTagNode.AddChildNode(RedPointConst.Guild_Skill.ToString());
                        GuildTagNode.AddChildNode(RedPointConst.Guild_Store.ToString());
                        GuildTagNode.AddChildNode(RedPointConst.Guild_Map.ToString());
                        //GuildTagNode.AddChildNode(RedPointConst.Guild_List.ToString());
                    }

                    RedPointNode GuildFriendTagNode = GuildNode.AddChildNode(RedPointConst.GuildFirend_Tag.ToString());

                    RedPointNode GuildExtendTagNode = GuildNode.AddChildNode(RedPointConst.GuildExtend_Tag.ToString());
                }


                RedPointNode PlayNode = baseNode.AddChildNode(RedPointConst.Play.ToString());
                {
                    RedPointNode ChallengeNode = PlayNode.AddChildNode(RedPointConst.Challenge.ToString());
                    {
                        RedPointNode Challenge_FightNode = ChallengeNode.AddChildNode(RedPointConst.Challenge_Fight.ToString());
                        {
                            Challenge_FightNode.AddChildNode(RedPointConst.Challenge_Fight_Click.ToString());
                            Challenge_FightNode.AddChildNode(RedPointConst.Challenge_Fight_Award.ToString());
                        }
                       
                        ChallengeNode.AddChildNode(RedPointConst.Challenge_Boss.ToString());
                        ChallengeNode.AddChildNode(RedPointConst.Challenge_Relice.ToString());
                        ChallengeNode.AddChildNode(RedPointConst.Challenge_OnHook.ToString());
                        RedPointNode Challenge_TowerNode = ChallengeNode.AddChildNode(RedPointConst.Challenge_Tower.ToString());
                        {
                            //Challenge_TowerNode.AddChildNode(RedPointConst.Challenge_Tower_Award.ToString());
                            Challenge_TowerNode.AddChildNode(RedPointConst.Challenge_Tower_Click.ToString());
                        }
                    }

                    RedPointNode LimetNode = PlayNode.AddChildNode(RedPointConst.Limet.ToString());
                    {
                        LimetNode.AddChildNode(RedPointConst.PVP_More.ToString());
                        LimetNode.AddChildNode(RedPointConst.PVP_One.ToString());
                    }
                    PlayNode.AddChildNode(RedPointConst.DayDo.ToString());
                }

            }
        }

        //本地存储红点事件
        private Dictionary<string, int> DicLocalRed = new Dictionary<string, int>
        {
            //[RedPointConst.GiftBag_GiftPay.ToString()] = 1,
            //[RedPointConst.GiftBag_PetGift.ToString()] = 1,
            //[RedPointConst.GiftBag_PartnerGift.ToString()] = 1,
            [RedPointConst.Equip_Star.ToString()] = 1,
            [RedPointConst.Skill_Setting.ToString()] = 1,

            [RedPointConst.Rank_Level.ToString()] = 1,
            [RedPointConst.Rank_Power.ToString()] = 1,
            [RedPointConst.Rank_ZhanHun.ToString()] = 1,
            [RedPointConst.Rank_SiLian.ToString()] = 1,
            [RedPointConst.Rank_HuiHe.ToString()] = 1,

            [RedPointConst.Guild_InGuild.ToString()] = 1,
            [RedPointConst.Guild_Store.ToString()] = 1,
            
            [RedPointConst.Challenge_Fight_Click.ToString()] = 1,
            [RedPointConst.Challenge_Tower_Click.ToString()] = 1,

            [RedPointConst.Bag_Syn.ToString()] = 1,

        };

        public void ClearRed()
        {
            foreach (var item in DicLocalRed.Keys.ToList())
            {
                DicLocalRed[item] = 1;
            }
        }

        public int GetRedNum(string sKey)
        {
            return DicLocalRed[sKey];
        }

        public void SetRedClose(string sKey,Action callBack)
        {
            DicLocalRed[sKey] = 0;
            callBack?.Invoke();
        }
    }
}
