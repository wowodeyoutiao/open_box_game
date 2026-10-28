using System;
using System.Collections.Generic;
using JEngine.Core;
using pb;
using PBAttribute = pb.Attribute;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 存档根对象。
    ///
    /// 刻意不复用 pb.Bag / pb.ItemInfo：协议结构不是存档契约。
    /// 未来协议字段变化时，只调整 Mapper，不改变已有存档格式。
    /// </summary>
    [Serializable]
    public sealed class SaveDocument
    {
        public int SchemaVersion = SaveDocumentMapper.CurrentSchemaVersion;
        public long SavedAtUnix;
        public long CurrentRoleUid;
        public long NextRoleUid = LocalData.DefaultNextRoleUid;
        public long NextItemUid = LocalData.DefaultNextItemUid;
        public List<RoleSaveV1> Roles = new List<RoleSaveV1>();
    }

    [Serializable]
    public sealed class RoleSaveV1
    {
        public long Uid;
        public string Name = "";
        public string HeadUrl = "";
        public int Job = 1;
        public int Sex = 1;
        public int Level = 1;
        public int Vip;
        public long Exp;

        public long Coin;
        public long Diamanta;
        public long Coupon;
        public long Soul;
        public long TrialVoucher;
        public long Energy = 100;

        public int Attack = 10;
        public int Defense = 5;
        public int MaxHp = 100;
        public int CurHp = 100;
        public int TotalCombat = 200;

        public long HomeAFKUsedSec;
        public long HomeAFKAnchorUnix;
        public int HomeAFKResetDay;
        public long LastSeenUnix;

        public int BoxLevel = 1;
        public long TreasuerBox;
        public long BoxQuick;
        public long BoxUpgradeEndUnix;
        public List<ItemSaveV1> BoxOpenRes = new List<ItemSaveV1>();

        public List<ItemSaveV1> Bag = new List<ItemSaveV1>();
        public List<EquipSaveV1> Equips = new List<EquipSaveV1>();
        public List<EquipEnhanceSaveV1> EquipEnhances = new List<EquipEnhanceSaveV1>();
        public List<EquipEnhanceSaveV1> EquipStars = new List<EquipEnhanceSaveV1>();
        public List<EquipGemSaveV1> EquipGems = new List<EquipGemSaveV1>();
        public List<PartnerSaveV1> Partners = new List<PartnerSaveV1>();
        public List<PartnerSlotSaveV1> PartnerSlots = new List<PartnerSlotSaveV1>();
        public List<long> StoreBuyLog = new List<long>();
        public List<MailSaveV1> Mails = new List<MailSaveV1>();
        public List<SkillSaveV1> Skills = new List<SkillSaveV1>();
        public int[] SkillSlots = new int[13];
        public AutoDisassemblySaveV1 AutoDisassembly = new AutoDisassemblySaveV1();
        public List<TaskSaveV1> Tasks = new List<TaskSaveV1>();
        public BattleSaveV1 Battle = new BattleSaveV1();
        public BossSaveV1 Boss = new BossSaveV1();
        public TrialTowerSaveV1 TrialTower = new TrialTowerSaveV1();
        public GemDailySaveV1 GemDaily = new GemDailySaveV1();
        public AfkSaveV1 Afk = new AfkSaveV1();
        public RelicSaveV1 Relic = new RelicSaveV1();
        public List<PetSaveV1> Pets = new List<PetSaveV1>();
        public List<int> PetEverHave = new List<int>();
        public PetAwardSaveV1 PetAward = new PetAwardSaveV1();
    }

    /// <summary>上阵宠物离线收益的结算锚点与累计产出（对齐原服 petGold / petExp）。</summary>
    [Serializable]
    public sealed class PetAwardSaveV1
    {
        public long AnchorUnix;
        public long ExpTotal;
        public long GoldTotal;
    }

    /// <summary>宠物槽位。SlotId 0 = 上阵位，1..20 = 包裹格。</summary>
    [Serializable]
    public sealed class PetSaveV1
    {
        public int SlotId;
        public long Uid;
        public int CfgId;
        public int Level;
        public int Star;
        public int Exp;
        public int ExpStar;
        public int Quality;
    }

    [Serializable]
    public sealed class BossSaveV1
    {
        public int BossId;
        public int ChallengeTime;
        public int ResetDay;
    }

    [Serializable]
    public sealed class TrialTowerSaveV1
    {
        public int BattleId;
    }

    /// <summary>每日副本每日次数（RoomType 7..12）。</summary>
    [Serializable]
    public sealed class GemDailySaveV1
    {
        public List<DailyMapTimeSaveV1> Times = new List<DailyMapTimeSaveV1>();
        public int ResetDay;
    }

    [Serializable]
    public sealed class DailyMapTimeSaveV1
    {
        public int RoomType;
        public int Times;
    }

    /// <summary>遗迹进度 / 次数 / 通关评价。</summary>
    [Serializable]
    public sealed class RelicSaveV1
    {
        public int BattleId;
        public int ChallengeTime;
        public int ResetDay;
        public List<RelicEvalSaveV1> Evaluation = new List<RelicEvalSaveV1>();
        /// <summary>章节星级宝箱已领取档位（BattleId 字段复用为 Chapter）。旧档没有这段，读出来就是空列表。</summary>
        public List<RelicEvalSaveV1> ChapterReward = new List<RelicEvalSaveV1>();
    }

    [Serializable]
    public sealed class RelicEvalSaveV1
    {
        public int BattleId;
        public int Star;
    }

    /// <summary>挂机副本（守卫海岛）每日时长。</summary>
    [Serializable]
    public sealed class AfkSaveV1
    {
        public int UsedSec;
        public int ResetDay;
        public long MultiplierID;
    }

    [Serializable]
    public sealed class BattleSaveV1
    {
        public int BattleId;
        public int RewardStage;
    }

    [Serializable]
    public sealed class TaskSaveV1
    {
        public int CfgId;
        public int Class;
        public int Status;
        public int TargetType;
        public int TargetId;
        public int TargetNum;
        public int Amount;
        public bool Completed;
        public long RefreshTime;
    }

    [Serializable]
    public sealed class AutoDisassemblySaveV1
    {
        public bool Open;
        public bool LevelSwitch;
        public int Level;
        public bool QualitySwitch;
        public int Quality;
        public bool HighCombat;
    }

    [Serializable]
    public sealed class MailSaveV1
    {
        public string MailId = "";
        public string Title = "";
        public string Desc = "";
        public int IsRead;
        public int IsExtract;
        public int IsAttachments;
        public long CreatedAt;
        public long ExpiryAt;
        public List<ItemSaveV1> Attachments = new List<ItemSaveV1>();
    }

    [Serializable]
    public sealed class EquipSaveV1
    {
        public int Slot;
        public ItemSaveV1 Item = new ItemSaveV1();
    }

    /// <summary>槽位强化等级 / 星级。跟槽位绑定，不含装备实例。</summary>
    [Serializable]
    public sealed class EquipEnhanceSaveV1
    {
        public int Slot;
        public int Level;
    }

    /// <summary>槽位上镶嵌的 4 颗魂石（存魂石道具配置 ID，0 = 空槽）。同样跟槽位绑定。</summary>
    [Serializable]
    public sealed class EquipGemSaveV1
    {
        public int Slot;
        public int[] Gems = new int[4];
    }

    [Serializable]
    public sealed class ItemSaveV1
    {
        public int CfgId;
        public long Uid;
        public long Num;
        public long CreateTime;
        public int Type;
        public int Quality;
        public List<StatSaveV1> Attributes = new List<StatSaveV1>();
    }

    [Serializable]
    public sealed class StatSaveV1
    {
        public int Attr;
        public int Num;
    }

    [Serializable]
    public sealed class PartnerSaveV1
    {
        public long Uid;
        public int CfgId;
        public int Level;
        public int Star;
        public List<SkillSaveV1> Skills = new List<SkillSaveV1>();
    }

    [Serializable]
    public sealed class PartnerSlotSaveV1
    {
        public int SlotId;
        public long PartnerUid;
        /// <summary>伙伴身上的装备（槽位 1..12）。旧档没有这段，读出来是空列表。</summary>
        public List<PartnerEquipSaveV1> Equips = new List<PartnerEquipSaveV1>();
    }

    /// <summary>
    /// 伙伴装备槽。强化/升星/宝石都存在**槽位**上（换装备不清零），
    /// 与玩家 EquipEnhance/EquipStar/EquipGem 的语义一致，对齐原服 `Slot{ItemId,Stars,Enhance,Quality,Gemstone}`。
    /// </summary>
    [Serializable]
    public sealed class PartnerEquipSaveV1
    {
        public int Slot;                 // EquipSlot 1..12
        public ItemSaveV1 Item;          // null = 该部位空着
        public int Enhance;
        public int Stars;
        public List<int> Gemstone = new List<int>();
    }

    [Serializable]
    public sealed class SkillSaveV1
    {
        public int CfgId;
        public long Cooldown;
        public int Advanced;
    }

    public static class SaveDocumentMapper
    {
        public const int CurrentSchemaVersion = 2;

        /// <summary>
        /// V1 把装备配置基础属性复制进了 Equip.Attributes。
        /// V2 改为：基础属性只来自配置，实例 Attributes 仅存随机/极品属性。
        /// 当前 V1 仅由本地建角产生，没有真实的随机属性，因此迁移时可以安全清空。
        /// </summary>
        public static SaveDocument MigrateV1ToV2(SaveDocument document)
        {
            if (document == null || document.SchemaVersion != 1)
                return document;

            if (document.Roles != null)
            {
                for (int i = 0; i < document.Roles.Count; i++)
                {
                    RoleSaveV1 role = document.Roles[i];
                    if (role == null)
                        continue;

                    ClearItemAttributes(role.Bag);
                    if (role.Equips != null)
                    {
                        for (int j = 0; j < role.Equips.Count; j++)
                        {
                            if (role.Equips[j] != null)
                                ClearItemAttributes(role.Equips[j].Item);
                        }
                    }
                }
            }

            document.SchemaVersion = 2;
            Log.Print("[SaveDocument] 存档 V1 → V2：清理实例中的基础属性副本");
            return document;
        }

        private static void ClearItemAttributes(ItemSaveV1 item)
        {
            if (item != null && item.Attributes != null)
            {
                item.Attributes.Clear();
            }
        }

        private static void ClearItemAttributes(List<ItemSaveV1> items)
        {
            if (items == null)
                return;
            for (int i = 0; i < items.Count; i++)
                ClearItemAttributes(items[i]);
        }

        public static SaveDocument FromLocalData(LocalData data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            var document = new SaveDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                SavedAtUnix = GetUnixSeconds(),
                CurrentRoleUid = data.Current != null ? data.Current.Uid : 0,
                NextRoleUid = data.NextRoleUid,
                NextItemUid = data.NextItemUid,
                Roles = new List<RoleSaveV1>()
            };

            if (data.Roles != null)
            {
                for (int i = 0; i < data.Roles.Count; i++)
                {
                    LocalRole role = data.Roles[i];
                    if (role != null)
                        document.Roles.Add(ToRoleSave(role));
                }
            }

            return document;
        }

        public static List<LocalRole> ToLocalRoles(SaveDocument document)
        {
            var roles = new List<LocalRole>();
            if (document == null || document.Roles == null)
                return roles;

            for (int i = 0; i < document.Roles.Count; i++)
            {
                RoleSaveV1 role = document.Roles[i];
                if (role != null)
                    roles.Add(ToLocalRole(role));
            }
            return roles;
        }

        private static RoleSaveV1 ToRoleSave(LocalRole role)
        {
            var save = new RoleSaveV1
            {
                Uid = role.Uid,
                Name = role.Name,
                HeadUrl = role.HeadUrl,
                Job = role.Job,
                Sex = role.Sex,
                Level = role.Level,
                Vip = role.Vip,
                Exp = role.Exp,
                Coin = role.Coin,
                Diamanta = role.Diamanta,
                Coupon = role.Coupon,
                Soul = role.Soul,
                TrialVoucher = role.TrialVoucher,
                Energy = role.Energy,
                Attack = role.Attack,
                Defense = role.Defense,
                MaxHp = role.MaxHp,
                CurHp = role.CurHp,
                TotalCombat = role.TotalCombat,
                HomeAFKUsedSec = role.HomeAFKUsedSec,
                HomeAFKAnchorUnix = role.HomeAFKAnchorUnix,
                HomeAFKResetDay = role.HomeAFKResetDay,
                LastSeenUnix = role.LastSeenUnix,
                BoxLevel = role.BoxLevel,
                TreasuerBox = role.TreasuerBox,
                BoxQuick = role.BoxQuick,
                BoxUpgradeEndUnix = role.BoxUpgradeEndUnix,
                BoxOpenRes = new List<ItemSaveV1>(),
                Bag = new List<ItemSaveV1>(),
                Equips = new List<EquipSaveV1>(),
                EquipEnhances = new List<EquipEnhanceSaveV1>(),
                EquipStars = new List<EquipEnhanceSaveV1>(),
                EquipGems = new List<EquipGemSaveV1>(),
                Partners = new List<PartnerSaveV1>(),
                PartnerSlots = new List<PartnerSlotSaveV1>(),
                StoreBuyLog = new List<long>(),
                Mails = new List<MailSaveV1>(),
                Skills = new List<SkillSaveV1>(),
                SkillSlots = new int[13],
                AutoDisassembly = new AutoDisassemblySaveV1(),
                Tasks = new List<TaskSaveV1>(),
                Battle = new BattleSaveV1()
            };

            if (role.BoxOpenItems != null)
            {
                for (int i = 0; i < role.BoxOpenItems.Count; i++)
                {
                    if (role.BoxOpenItems[i] == null)
                        continue;
                    save.BoxOpenRes.Add(ToItemSave(role.BoxOpenItems[i]));
                }
            }

            if (role.BagData != null && role.BagData.ItemList != null)
            {
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    if (role.BagData.ItemList[i] == null)
                        continue;
                    save.Bag.Add(ToItemSave(role.BagData.ItemList[i]));
                }
            }

            if (role.Equips != null)
            {
                foreach (KeyValuePair<EquipSlot, ItemInfo> kv in role.Equips)
                {
                    if (kv.Value == null)
                        continue;
                    save.Equips.Add(new EquipSaveV1
                    {
                        Slot = (int)kv.Key,
                        Item = ToItemSave(kv.Value)
                    });
                }
            }

            if (role.EquipEnhance != null)
            {
                foreach (KeyValuePair<EquipSlot, int> kv in role.EquipEnhance)
                {
                    if (kv.Value <= 0)
                        continue;
                    save.EquipEnhances.Add(new EquipEnhanceSaveV1
                    {
                        Slot = (int)kv.Key,
                        Level = kv.Value,
                    });
                }
            }

            if (role.EquipStars != null)
            {
                foreach (KeyValuePair<EquipSlot, int> kv in role.EquipStars)
                {
                    if (kv.Value <= 0)
                        continue;
                    save.EquipStars.Add(new EquipEnhanceSaveV1
                    {
                        Slot = (int)kv.Key,
                        Level = kv.Value,
                    });
                }
            }

            if (role.EquipGems != null)
            {
                foreach (KeyValuePair<EquipSlot, int[]> kv in role.EquipGems)
                {
                    if (kv.Value == null)
                        continue;
                    bool any = false;
                    var gems = new int[LocalRole.GemSlotCount];
                    for (int i = 0; i < gems.Length && i < kv.Value.Length; i++)
                    {
                        gems[i] = kv.Value[i];
                        if (gems[i] > 0)
                            any = true;
                    }
                    if (!any)
                        continue;
                    save.EquipGems.Add(new EquipGemSaveV1
                    {
                        Slot = (int)kv.Key,
                        Gems = gems,
                    });
                }
            }

            if (role.Partners != null)
            {
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    if (role.Partners[i] == null)
                        continue;
                    save.Partners.Add(ToPartnerSave(role.Partners[i]));
                }
            }

            if (role.StoreBuyLog != null)
            {
                for (int i = 0; i < role.StoreBuyLog.Count; i++)
                    save.StoreBuyLog.Add(role.StoreBuyLog[i]);
            }

            if (role.PartnerSlots != null)
            {
                for (int i = 0; i < role.PartnerSlots.Count; i++)
                {
                    PartnerSlotInfo slot = role.PartnerSlots[i];
                    if (slot == null)
                        continue;
                    var slotSave = new PartnerSlotSaveV1
                    {
                        SlotId = slot.SlotId,
                        PartnerUid = slot.PartnerUid,
                    };
                    if (slot.Equips != null)
                    {
                        for (int s = 0; s < slot.Equips.Count; s++)
                        {
                            SlotInfo equip = slot.Equips[s];
                            if (equip == null)
                                continue;
                            // 空槽且没有任何强化/升星/宝石进度就不用落盘，读档时补位
                            bool hasGem = equip.Gemstone != null && equip.Gemstone.Length > 0;
                            if (equip.Item == null && equip.Enhance == 0 && equip.Stars == 0 && !hasGem)
                                continue;
                            var equipSave = new PartnerEquipSaveV1
                            {
                                Slot = s,
                                Enhance = equip.Enhance,
                                Stars = equip.Stars,
                                Gemstone = new List<int>(),
                            };
                            if (equip.Item != null)
                                equipSave.Item = ToItemSave(equip.Item);
                            if (hasGem)
                                equipSave.Gemstone.AddRange(equip.Gemstone);
                            slotSave.Equips.Add(equipSave);
                        }
                    }
                    save.PartnerSlots.Add(slotSave);
                }
            }

            if (role.Mails != null)
            {
                for (int i = 0; i < role.Mails.Count; i++)
                    save.Mails.Add(ToMailSave(role.Mails[i]));
            }

            if (role.Skills != null)
            {
                for (int i = 0; i < role.Skills.Count; i++)
                {
                    SkillInfo skill = role.Skills[i];
                    if (skill == null) continue;
                    save.Skills.Add(new SkillSaveV1
                    {
                        CfgId = skill.CfgId,
                        Cooldown = skill.Cooldown,
                        Advanced = skill.Advanced,
                    });
                }
            }
            if (role.SkillSlots != null)
                save.SkillSlots = (int[])role.SkillSlots.Clone();
            if (role.AutoDisassembly != null)
            {
                save.AutoDisassembly = new AutoDisassemblySaveV1
                {
                    Open = role.AutoDisassembly.Open,
                    LevelSwitch = role.AutoDisassembly.LevelSwitch,
                    Level = role.AutoDisassembly.Level,
                    QualitySwitch = role.AutoDisassembly.QualitySwitch,
                    Quality = (int)role.AutoDisassembly.Quality,
                    HighCombat = role.AutoDisassembly.HighCombat,
                };
            }

            save.Battle = new BattleSaveV1
            {
                BattleId = role.BattleId,
                RewardStage = role.BattleRewardStage,
            };

            save.Boss = new BossSaveV1
            {
                BossId = role.BossId,
                ChallengeTime = role.BossChallengeTime,
                ResetDay = role.BossResetDay,
            };

            save.TrialTower = new TrialTowerSaveV1
            {
                BattleId = role.TrialTowerId,
            };

            save.Relic = new RelicSaveV1
            {
                BattleId = role.RelicId,
                ChallengeTime = role.RelicChallengeTime,
                ResetDay = role.RelicResetDay,
            };
            if (role.RelicEvaluation != null)
            {
                for (int i = 0; i < role.RelicEvaluation.Count; i++)
                {
                    KeyValue eval = role.RelicEvaluation[i];
                    if (eval == null)
                        continue;
                    save.Relic.Evaluation.Add(new RelicEvalSaveV1
                    {
                        BattleId = eval.K,
                        Star = eval.V,
                    });
                }
            }
            if (role.RelicChapterReward != null)
            {
                for (int i = 0; i < role.RelicChapterReward.Count; i++)
                {
                    KeyValue chapterReward = role.RelicChapterReward[i];
                    if (chapterReward == null)
                        continue;
                    save.Relic.ChapterReward.Add(new RelicEvalSaveV1
                    {
                        BattleId = chapterReward.K,   // 这里存的是章节号，不是关卡号
                        Star = chapterReward.V,
                    });
                }
            }

            save.Afk = new AfkSaveV1
            {
                UsedSec = role.AfkDailySec,
                ResetDay = role.AfkResetDay,
                MultiplierID = role.AfkMultiplierID,
            };

            // 宠物槽位：只存有宠物的格子（空槽 CfgId=0 不落盘，读档时补位）
            role.EnsurePetSlots();
            for (int i = 0; i < role.PetSlots.Count; i++)
            {
                PetSlotInfo pet = role.PetSlots[i];
                if (pet == null || pet.CfgId <= 0)
                    continue;
                save.Pets.Add(new PetSaveV1
                {
                    SlotId = i,
                    Uid = pet.Uid,
                    CfgId = pet.CfgId,
                    Level = pet.Level,
                    Star = pet.Star,
                    Exp = pet.Exp,
                    ExpStar = pet.ExpStar,
                    Quality = pet.Quality,
                });
            }
            if (role.PetEverHave != null)
                save.PetEverHave.AddRange(role.PetEverHave);

            save.PetAward = new PetAwardSaveV1
            {
                AnchorUnix = role.PetAwardAnchorUnix,
                ExpTotal = role.PetAwardExpTotal,
                GoldTotal = role.PetAwardGoldTotal,
            };

            save.GemDaily = new GemDailySaveV1
            {
                ResetDay = role.DailyMapResetDay,
            };
            if (role.DailyMapTimes != null)
            {
                foreach (KeyValuePair<int, int> kv in role.DailyMapTimes)
                {
                    save.GemDaily.Times.Add(new DailyMapTimeSaveV1
                    {
                        RoomType = kv.Key,
                        Times = kv.Value,
                    });
                }
            }

            if (role.Tasks != null)
            {
                for (int i = 0; i < role.Tasks.Count; i++)
                {
                    TaskInfo task = role.Tasks[i];
                    if (task == null || task.Target == null) continue;
                    save.Tasks.Add(new TaskSaveV1
                    {
                        CfgId = task.CfgId,
                        Class = (int)task.Class,
                        Status = (int)task.Status,
                        TargetType = (int)task.Target.Type,
                        TargetId = task.Target.TargetId,
                        TargetNum = task.Target.TargetNum,
                        Amount = task.Target.Amount,
                        Completed = task.Target.Completed,
                        RefreshTime = task.RefreshTime,
                    });
                }
            }

            return save;
        }

        private static LocalRole ToLocalRole(RoleSaveV1 save)
        {
            var role = new LocalRole
            {
                Uid = save.Uid,
                Name = save.Name,
                HeadUrl = save.HeadUrl,
                Job = save.Job,
                Sex = save.Sex,
                Level = save.Level,
                Vip = save.Vip,
                Exp = save.Exp,
                Coin = save.Coin,
                Diamanta = save.Diamanta,
                Coupon = save.Coupon,
                Soul = save.Soul,
                TrialVoucher = save.TrialVoucher,
                Energy = save.Energy,
                Attack = save.Attack,
                Defense = save.Defense,
                MaxHp = save.MaxHp,
                CurHp = save.CurHp,
                TotalCombat = save.TotalCombat,
                HomeAFKUsedSec = save.HomeAFKUsedSec,
                HomeAFKAnchorUnix = save.HomeAFKAnchorUnix,
                HomeAFKResetDay = save.HomeAFKResetDay,
                LastSeenUnix = save.LastSeenUnix,
                BoxLevel = save.BoxLevel > 0 ? save.BoxLevel : 1,
                TreasuerBox = save.TreasuerBox,
                BoxQuick = save.BoxQuick,
                BoxUpgradeEndUnix = save.BoxUpgradeEndUnix,
                BoxOpenItems = new List<ItemInfo>(),
                BagData = new Bag { ItemList = new List<ItemInfo>() },
                Equips = new Dictionary<EquipSlot, ItemInfo>(),
                Partners = new List<PartnerInfo>(),
                Mails = new List<LocalMail>(),
                Skills = new List<SkillInfo>(),
                SkillSlots = new int[13],
                AutoDisassembly = new AutoDisassemblyInfo(),
                Tasks = new List<TaskInfo>(),
                BattleId = 0,
                BattleRewardStage = 0
            };

            if (save.BoxOpenRes != null)
            {
                for (int i = 0; i < save.BoxOpenRes.Count; i++)
                {
                    ItemInfo item = ToItem(save.BoxOpenRes[i]);
                    if (item != null)
                        role.BoxOpenItems.Add(item);
                }
            }

            if (save.Bag != null)
            {
                for (int i = 0; i < save.Bag.Count; i++)
                {
                    ItemInfo item = ToItem(save.Bag[i]);
                    if (item != null)
                        role.BagData.ItemList.Add(item);
                }
            }

            if (save.Equips != null)
            {
                for (int i = 0; i < save.Equips.Count; i++)
                {
                    EquipSaveV1 equip = save.Equips[i];
                    if (equip == null || equip.Item == null)
                        continue;
                    EquipSlot slot = (EquipSlot)equip.Slot;
                    int slotValue = (int)slot;
                    if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                        || slotValue >= (int)EquipSlot.MaxEquipSlot)
                        continue;
                    ItemInfo item = ToItem(equip.Item);
                    if (item != null)
                        role.Equips[slot] = item;
                }
            }

            if (save.EquipEnhances != null)
            {
                for (int i = 0; i < save.EquipEnhances.Count; i++)
                {
                    EquipEnhanceSaveV1 item = save.EquipEnhances[i];
                    if (item == null || item.Level <= 0)
                        continue;
                    EquipSlot slot = (EquipSlot)item.Slot;
                    int slotValue = (int)slot;
                    if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                        || slotValue >= (int)EquipSlot.MaxEquipSlot)
                        continue;
                    role.EquipEnhance[slot] = item.Level;
                }
            }

            if (save.EquipStars != null)
            {
                for (int i = 0; i < save.EquipStars.Count; i++)
                {
                    EquipEnhanceSaveV1 item = save.EquipStars[i];
                    if (item == null || item.Level <= 0)
                        continue;
                    EquipSlot slot = (EquipSlot)item.Slot;
                    int slotValue = (int)slot;
                    if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                        || slotValue >= (int)EquipSlot.MaxEquipSlot)
                        continue;
                    role.EquipStars[slot] = item.Level;
                }
            }

            if (save.EquipGems != null)
            {
                for (int i = 0; i < save.EquipGems.Count; i++)
                {
                    EquipGemSaveV1 item = save.EquipGems[i];
                    if (item == null || item.Gems == null)
                        continue;
                    EquipSlot slot = (EquipSlot)item.Slot;
                    int slotValue = (int)slot;
                    if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                        || slotValue >= (int)EquipSlot.MaxEquipSlot)
                        continue;
                    var gems = new int[LocalRole.GemSlotCount];
                    bool any = false;
                    for (int g = 0; g < gems.Length && g < item.Gems.Length; g++)
                    {
                        if (item.Gems[g] <= 0)
                            continue;
                        gems[g] = item.Gems[g];
                        any = true;
                    }
                    if (any)
                        role.EquipGems[slot] = gems;
                }
            }

            if (save.Partners != null)
            {
                for (int i = 0; i < save.Partners.Count; i++)
                {
                    PartnerInfo partner = ToPartner(save.Partners[i]);
                    if (partner != null)
                        role.Partners.Add(partner);
                }
            }

            if (save.StoreBuyLog != null)
            {
                for (int i = 0; i < save.StoreBuyLog.Count; i++)
                    role.StoreBuyLog.Add(save.StoreBuyLog[i]);
            }

            if (save.PartnerSlots != null)
            {
                for (int i = 0; i < save.PartnerSlots.Count; i++)
                {
                    PartnerSlotSaveV1 slot = save.PartnerSlots[i];
                    if (slot == null)
                        continue;
                    role.PartnerSlots.Add(new PartnerSlotInfo
                    {
                        SlotId = slot.SlotId,
                        PartnerUid = slot.PartnerUid,
                        Equips = new List<SlotInfo>(),
                    });
                    // 伙伴装备：先补满 13 个空槽再按 Slot 回填
                    PartnerSlotInfo restored = role.PartnerSlots[role.PartnerSlots.Count - 1];
                    role.EnsurePartnerEquipSlots();
                    if (slot.Equips != null)
                    {
                        for (int e = 0; e < slot.Equips.Count; e++)
                        {
                            PartnerEquipSaveV1 equipSave = slot.Equips[e];
                            if (equipSave == null)
                                continue;
                            if (equipSave.Slot <= 0 || equipSave.Slot >= LocalRole.PartnerEquipSlotCount)
                                continue;
                            restored.Equips[equipSave.Slot] = new SlotInfo
                            {
                                Item = equipSave.Item == null ? null : ToItem(equipSave.Item),
                                Enhance = equipSave.Enhance,
                                Stars = equipSave.Stars,
                                Gemstone = equipSave.Gemstone == null
                                    ? new int[0]
                                    : equipSave.Gemstone.ToArray(),
                            };
                        }
                    }
                }
            }

            if (save.Mails != null)
            {
                for (int i = 0; i < save.Mails.Count; i++)
                {
                    LocalMail mail = ToLocalMail(save.Mails[i]);
                    if (mail != null)
                        role.Mails.Add(mail);
                }
            }

            if (save.Skills != null)
            {
                for (int i = 0; i < save.Skills.Count; i++)
                {
                    SkillSaveV1 skill = save.Skills[i];
                    if (skill == null || skill.CfgId <= 0) continue;
                    role.Skills.Add(new SkillInfo
                    {
                        CfgId = skill.CfgId,
                        Cooldown = skill.Cooldown,
                        Advanced = skill.Advanced,
                    });
                }
            }
            if (save.SkillSlots != null && save.SkillSlots.Length == 13)
                role.SkillSlots = (int[])save.SkillSlots.Clone();
            if (save.AutoDisassembly != null)
            {
                role.AutoDisassembly = new AutoDisassemblyInfo
                {
                    Open = save.AutoDisassembly.Open,
                    LevelSwitch = save.AutoDisassembly.LevelSwitch,
                    Level = save.AutoDisassembly.Level,
                    QualitySwitch = save.AutoDisassembly.QualitySwitch,
                    Quality = (ItemQuality)save.AutoDisassembly.Quality,
                    HighCombat = save.AutoDisassembly.HighCombat,
                };
            }

            if (save.Battle != null)
            {
                role.BattleId = save.Battle.BattleId;
                role.BattleRewardStage = save.Battle.RewardStage;
                if (save.Boss != null)
                {
                    role.BossId = save.Boss.BossId;
                    role.BossChallengeTime = save.Boss.ChallengeTime;
                    role.BossResetDay = save.Boss.ResetDay;
                }
                if (save.Relic != null)
                {
                    role.RelicId = save.Relic.BattleId;
                    role.RelicChallengeTime = save.Relic.ChallengeTime;
                    role.RelicResetDay = save.Relic.ResetDay;
                    if (save.Relic.ChapterReward != null)
                    {
                        for (int i = 0; i < save.Relic.ChapterReward.Count; i++)
                        {
                            RelicEvalSaveV1 chapterReward = save.Relic.ChapterReward[i];
                            if (chapterReward == null || chapterReward.BattleId <= 0)
                                continue;
                            role.RelicChapterReward.Add(new KeyValue
                            {
                                K = chapterReward.BattleId,   // 章节号
                                V = chapterReward.Star,
                            });
                        }
                    }
                    if (save.Relic.Evaluation != null)
                    {
                        for (int i = 0; i < save.Relic.Evaluation.Count; i++)
                        {
                            RelicEvalSaveV1 eval = save.Relic.Evaluation[i];
                            if (eval == null || eval.BattleId <= 0)
                                continue;
                            role.RelicEvaluation.Add(new KeyValue
                            {
                                K = eval.BattleId,
                                V = eval.Star,
                            });
                        }
                    }
                }
                if (save.Afk != null)
                {
                    role.AfkDailySec = save.Afk.UsedSec;
                    role.AfkResetDay = save.Afk.ResetDay;
                    role.AfkMultiplierID = save.Afk.MultiplierID;
                }
                // 宠物：先补满 21 个槽位再按 SlotId 回填
                role.EnsurePetSlots();
                if (save.Pets != null)
                {
                    for (int i = 0; i < save.Pets.Count; i++)
                    {
                        PetSaveV1 pet = save.Pets[i];
                        if (pet == null || pet.CfgId <= 0)
                            continue;
                        if (pet.SlotId < 0 || pet.SlotId >= LocalRole.PetSlotCount)
                            continue;
                        role.PetSlots[pet.SlotId] = new PetSlotInfo
                        {
                            Uid = pet.Uid,
                            CfgId = pet.CfgId,
                            Level = pet.Level,
                            Star = pet.Star,
                            Exp = pet.Exp,
                            ExpStar = pet.ExpStar,
                            Quality = pet.Quality,
                        };
                    }
                }
                if (save.PetEverHave != null)
                    role.PetEverHave.AddRange(save.PetEverHave);
                if (save.PetAward != null)
                {
                    role.PetAwardAnchorUnix = save.PetAward.AnchorUnix;
                    role.PetAwardExpTotal = save.PetAward.ExpTotal;
                    role.PetAwardGoldTotal = save.PetAward.GoldTotal;
                }
                if (save.GemDaily != null)
                {
                    role.DailyMapResetDay = save.GemDaily.ResetDay;
                    if (save.GemDaily.Times != null)
                    {
                        for (int i = 0; i < save.GemDaily.Times.Count; i++)
                        {
                            DailyMapTimeSaveV1 item = save.GemDaily.Times[i];
                            if (item == null || item.RoomType <= 0)
                                continue;
                            role.DailyMapTimes[item.RoomType] = item.Times;
                        }
                    }
                }
                if (save.TrialTower != null)
                    role.TrialTowerId = save.TrialTower.BattleId;
            }

            if (save.Tasks != null)
            {
                for (int i = 0; i < save.Tasks.Count; i++)
                {
                    TaskSaveV1 task = save.Tasks[i];
                    if (task == null || task.CfgId <= 0) continue;
                    role.Tasks.Add(new TaskInfo
                    {
                        CfgId = task.CfgId,
                        Class = (TaskClass)task.Class,
                        Status = (TaskStatus)task.Status,
                        RefreshTime = task.RefreshTime,
                        Target = new TargetInfo
                        {
                            Type = (TargetType)task.TargetType,
                            TargetId = task.TargetId,
                            TargetNum = task.TargetNum,
                            Amount = task.Amount,
                            Completed = task.Completed,
                        },
                    });
                }
            }

            // 旧档里的自动分解设置是「全 0 空壳」（那时还没接 config.xml 的出厂开关），
            // 这里按配置补齐；玩家真正保存过的设置不会被覆盖。
            ItemService.ApplyAutoDisassemblyDefaults(role);

            return role;
        }

        private static MailSaveV1 ToMailSave(LocalMail mail)
        {
            var save = new MailSaveV1();
            if (mail == null)
                return save;

            save.MailId = mail.MailId;
            save.Title = mail.Title;
            save.Desc = mail.Desc;
            save.IsRead = mail.IsRead;
            save.IsExtract = mail.IsExtract;
            save.IsAttachments = mail.IsAttachments;
            save.CreatedAt = mail.CreatedAt;
            save.ExpiryAt = mail.ExpiryAt;
            save.Attachments = new List<ItemSaveV1>();
            if (mail.Attachments != null)
            {
                for (int i = 0; i < mail.Attachments.Count; i++)
                {
                    if (mail.Attachments[i] != null)
                        save.Attachments.Add(ToItemSave(mail.Attachments[i]));
                }
            }
            return save;
        }

        private static LocalMail ToLocalMail(MailSaveV1 save)
        {
            if (save == null)
                return null;
            var mail = new LocalMail
            {
                MailId = save.MailId ?? "",
                Title = save.Title ?? "",
                Desc = save.Desc ?? "",
                IsRead = save.IsRead,
                IsExtract = save.IsExtract,
                IsAttachments = save.IsAttachments,
                CreatedAt = save.CreatedAt,
                ExpiryAt = save.ExpiryAt,
                Attachments = new List<ItemInfo>()
            };
            if (save.Attachments != null)
            {
                for (int i = 0; i < save.Attachments.Count; i++)
                {
                    ItemInfo item = ToItem(save.Attachments[i]);
                    if (item != null)
                        mail.Attachments.Add(item);
                }
            }
            return mail;
        }

        private static ItemSaveV1 ToItemSave(ItemInfo item)
        {
            var save = new ItemSaveV1();
            if (item == null)
                return save;

            save.CfgId = item.CfgId;
            save.Uid = item.Uid;
            save.Num = item.Num;
            save.CreateTime = item.CreateTime;
            save.Type = (int)item.Type;
            save.Quality = item.Equip != null ? (int)item.Equip.Quality : 0;
            save.Attributes = new List<StatSaveV1>();

            if (item.Equip != null && item.Equip.Attributes != null)
            {
                for (int i = 0; i < item.Equip.Attributes.Count; i++)
                {
                    Stats stat = item.Equip.Attributes[i];
                    if (stat == null)
                        continue;
                    save.Attributes.Add(new StatSaveV1
                    {
                        Attr = (int)stat.attr,
                        Num = stat.Num
                    });
                }
            }

            return save;
        }

        private static ItemInfo ToItem(ItemSaveV1 save)
        {
            if (save == null || save.CfgId <= 0 || save.Uid <= 0)
                return null;

            var item = new ItemInfo
            {
                CfgId = save.CfgId,
                Uid = save.Uid,
                Num = save.Num,
                CreateTime = save.CreateTime,
                Type = (ItemType)save.Type,
                Equip = null
            };

            if (save.Quality > 0 || (save.Attributes != null && save.Attributes.Count > 0))
            {
                item.Equip = new EquipInfo
                {
                    Quality = (ItemQuality)save.Quality,
                    Attributes = new List<Stats>()
                };

                if (save.Attributes != null)
                {
                    for (int i = 0; i < save.Attributes.Count; i++)
                    {
                        StatSaveV1 stat = save.Attributes[i];
                        if (stat == null)
                            continue;
                        item.Equip.Attributes.Add(new Stats
                        {
                            attr = (PBAttribute)stat.Attr,
                            Num = stat.Num
                        });
                    }
                }
            }

            return item;
        }

        private static PartnerSaveV1 ToPartnerSave(PartnerInfo partner)
        {
            var save = new PartnerSaveV1();
            if (partner == null)
                return save;

            save.Uid = partner.Uid;
            save.CfgId = partner.CfgId;
            save.Level = partner.Level;
            save.Star = partner.Star;
            save.Skills = new List<SkillSaveV1>();

            if (partner.Skills != null)
            {
                for (int i = 0; i < partner.Skills.Count; i++)
                {
                    SkillInfo skill = partner.Skills[i];
                    if (skill == null)
                        continue;
                    save.Skills.Add(new SkillSaveV1
                    {
                        CfgId = skill.CfgId,
                        Cooldown = skill.Cooldown,
                        Advanced = skill.Advanced
                    });
                }
            }

            return save;
        }

        private static PartnerInfo ToPartner(PartnerSaveV1 save)
        {
            if (save == null || save.CfgId <= 0 || save.Uid <= 0)
                return null;

            var partner = new PartnerInfo
            {
                Uid = save.Uid,
                CfgId = save.CfgId,
                Level = save.Level,
                Star = save.Star,
                Skills = new List<SkillInfo>()
            };

            if (save.Skills != null)
            {
                for (int i = 0; i < save.Skills.Count; i++)
                {
                    SkillSaveV1 skill = save.Skills[i];
                    if (skill == null)
                        continue;
                    partner.Skills.Add(new SkillInfo
                    {
                        CfgId = skill.CfgId,
                        Cooldown = skill.Cooldown,
                        Advanced = skill.Advanced
                    });
                }
            }

            return partner;
        }

        public static long GetUnixSeconds()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}
