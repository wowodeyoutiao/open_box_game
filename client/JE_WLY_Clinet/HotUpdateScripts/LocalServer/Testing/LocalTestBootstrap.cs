#if LOCAL_TEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JEngine.Core;
using JEngine.LitJson;
using JEngine.MyNet;
using MetaFramework.UI;
using pb;
using UnityEngine;

namespace HotUpdateScripts.LocalNet.Testing
{
    /// <summary>
    /// 只在 LOCAL_TEST 构建中存在的测试入口。
    ///
    /// 生产包不定义 LOCAL_TEST，本文件不产生任何类型；业务代码只有一个
    /// #if LOCAL_TEST 的调用点。
    /// </summary>
    public static class LocalTestBootstrap
    {
        private static bool _ran;

        public static bool HasBatchRequest()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args == null)
                return false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != null && args[i].StartsWith("-localTest=", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>供 batchmode 在登录 UI 之前调用，不依赖玩家点登录。</summary>
        public static void TryRunStandalone()
        {
            if (!HasBatchRequest() || _ran)
                return;

            LocalData data = LocalData.Instance;
            if (data != null && data.Current == null && data.Roles != null && data.Roles.Count > 0)
                data.SelectRole(0);

            TryRun(true);
        }

        public static void TryRun()
        {
            TryRun(false);
        }

        private static void TryRun(bool headless)
        {
            if (_ran)
                return;
            _ran = true;

            string scenario = ReadScenario();
            if (string.IsNullOrEmpty(scenario) || scenario == "none")
                return;

            var cases = new List<CaseResult>();
            try
            {
                if (scenario == "all" || scenario == "box")
                    cases.Add(RunBox());
                if (scenario == "all" || scenario == "box_guard")
                    cases.Add(RunBoxGuard());
                if (scenario == "all" || scenario == "equip")
                    cases.Add(RunEquip());
                if (scenario == "all" || scenario == "save_box")
                    cases.Add(RunSaveBox());
                if (scenario == "all" || scenario == "idle")
                    cases.Add(RunIdle());
                if (scenario == "all" || scenario == "level")
                    cases.Add(RunLevel());
                if (scenario == "all" || scenario == "mail")
                    cases.Add(RunMail());
                if (scenario == "all" || scenario == "bag_full")
                    cases.Add(RunBagFull());
                if (scenario == "all" || scenario == "skill")
                    cases.Add(RunSkill());
                if (scenario == "all" || scenario == "skill_action")
                    cases.Add(RunSkillAction());
                if (scenario == "all" || scenario == "recycle")
                    cases.Add(RunRecycle());
                if (scenario == "all" || scenario == "task")
                    cases.Add(RunTask());
                if (scenario == "all" || scenario == "task_reset")
                    cases.Add(RunTaskReset());
                if (scenario == "all" || scenario == "battle")
                    cases.Add(RunBattle());
                if (scenario == "all" || scenario == "partner")
                    cases.Add(RunPartner());
                if (scenario == "all" || scenario == "boss")
                    cases.Add(RunBoss());
                if (scenario == "all" || scenario == "tower")
                    cases.Add(RunTower());
                if (scenario == "all" || scenario == "exp_pool")
                    cases.Add(RunExpPool());
                if (scenario == "all" || scenario == "passive")
                    cases.Add(RunPassiveSkill());
                if (scenario == "all" || scenario == "store")
                    cases.Add(RunStore());
                if (scenario == "all" || scenario == "skill_advance")
                    cases.Add(RunSkillAdvance());
                if (scenario == "all" || scenario == "equip_enhance")
                    cases.Add(RunEquipEnhance());
                if (scenario == "all" || scenario == "equip_star")
                    cases.Add(RunEquipStar());
                if (scenario == "all" || scenario == "gem")
                    cases.Add(RunGem());
                if (scenario == "all" || scenario == "config_audit")
                    cases.Add(RunConfigAudit());
                if (scenario == "all" || scenario == "compose")
                    cases.Add(RunCompose());
                if (scenario == "all" || scenario == "gem_all")
                    cases.Add(RunGemAll());
                if (scenario == "all" || scenario == "gem_dungeon")
                    cases.Add(RunGemDungeon());
                if (scenario == "all" || scenario == "daily_map")
                    cases.Add(RunDailyMap());
                if (scenario == "all" || scenario == "guard_island")
                    cases.Add(RunGuardIsland());
                if (scenario == "all" || scenario == "relic")
                    cases.Add(RunRelic());
                if (scenario == "all" || scenario == "relic_reward")
                    cases.Add(RunRelicReward());
                if (scenario == "all" || scenario == "pet")
                    cases.Add(RunPet());
                if (scenario == "all" || scenario == "pet_up")
                    cases.Add(RunPetUp());
                if (scenario == "all" || scenario == "pet_award")
                    cases.Add(RunPetAward());
                if (scenario == "all" || scenario == "partner_item")
                    cases.Add(RunPartnerItem());
                if (scenario == "all" || scenario == "partner_equip")
                    cases.Add(RunPartnerEquip());
                if (scenario == "all" || scenario == "partner_gem")
                    cases.Add(RunPartnerGem());
                if (scenario == "all" || scenario == "partner_upgrade")
                    cases.Add(RunPartnerUpgrade());
                if (scenario == "all" || scenario == "partner_reset")
                    cases.Add(RunPartnerReset());
                if (scenario == "all" || scenario == "home_afk")
                    cases.Add(RunHomeAFKReset());

                if (cases.Count == 0)
                {
                    var unknown = new CaseResult(scenario);
                    unknown.Fail("未知测试场景: " + scenario);
                    cases.Add(unknown);
                }
            }
            catch (Exception e)
            {
                var failed = new CaseResult(scenario);
                failed.Fail(e.ToString());
                cases.Add(failed);
            }

            bool pass = true;
            for (int i = 0; i < cases.Count; i++)
            {
                if (!cases[i].Pass)
                    pass = false;
            }

            WriteReport(scenario, pass, cases);
            if (pass)
            {
                Log.Print("[LocalTest][PASS] scenario=" + scenario);
                if (!headless)
                    UITool.ShowTips("[本地测试] PASS  " + cases.Count + " 项");
            }
            else
            {
                Log.PrintError("[LocalTest][FAIL] scenario=" + scenario);
                if (!headless)
                    UITool.ShowTips("[本地测试] FAIL  查看 Console / Reports");
            }
        }

        private static CaseResult RunBox()
        {
            var result = new CaseResult("box");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalData data = LocalData.Instance;
                LocalRole role = snapshot.Role;
                role.BoxLevel = 1;
                role.TreasuerBox = 3;
                role.BoxOpenItems = new List<ItemInfo>();
                int bagBefore = role.BagData == null || role.BagData.ItemList == null
                    ? 0 : role.BagData.ItemList.Count;

                HomeOpenBoxResponse open = BoxService.Instance.OpenBox(
                    TCPClientMgr.ProtoSerialize(new HomeOpenBoxRequest
                    {
                        Num = 1,
                        level = role.Level,
                        Job = role.Job,
                    }));

                result.Check(open.Code == 0, "open.code=" + open.Code);
                if (open.Code == 0)
                {
                    result.Check(open.ItemInfo != null && open.ItemInfo.Count == 1,
                        "open.items=" + (open.ItemInfo == null ? -1 : open.ItemInfo.Count));
                    result.Check(role.BoxOpenItems.Count == 1, "pending=" + role.BoxOpenItems.Count);
                    result.Check(role.TreasuerBox == 2, "treasure=" + role.TreasuerBox);

                    SaveDocument doc = SaveDocumentMapper.FromLocalData(data);
                    int savedPending = doc != null && doc.Roles != null
                        && snapshot.Index >= 0 && snapshot.Index < doc.Roles.Count
                        && doc.Roles[snapshot.Index].BoxOpenRes != null
                        ? doc.Roles[snapshot.Index].BoxOpenRes.Count : -1;
                    result.Check(savedPending == 1, "save.pending=" + savedPending);

                    long uid = open.ItemInfo[0].Uid;
                    BoxResIntoBagResponse claim = BoxService.Instance.BoxIntoBag(
                        TCPClientMgr.ProtoSerialize(new BoxResIntoBagRequest { Uid = uid }));
                    result.Check(claim.Code == 0, "claim.code=" + claim.Code);
                    result.Check(role.BoxOpenItems.Count == 0, "after.pending=" + role.BoxOpenItems.Count);
                    int bagAfter = role.BagData == null || role.BagData.ItemList == null
                        ? -1 : role.BagData.ItemList.Count;
                    result.Check(bagAfter == bagBefore + 1, "after.bag=" + bagAfter);
                }
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunBoxGuard()
        {
            var result = new CaseResult("box_guard");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                role.BoxLevel = 1;
                role.TreasuerBox = 1;
                role.BoxOpenItems = new List<ItemInfo>();

                HomeOpenBoxRequest request = new HomeOpenBoxRequest
                {
                    Num = 1,
                    level = role.Level,
                    Job = role.Job,
                };

                HomeOpenBoxResponse first = BoxService.Instance.OpenBox(
                    TCPClientMgr.ProtoSerialize(request));
                result.Check(first.Code == 0, "first.open=" + first.Code);
                result.Check(role.BoxOpenItems.Count == 1, "first.pending=" + role.BoxOpenItems.Count);
                result.Check(role.TreasuerBox == 0, "first.treasure=" + role.TreasuerBox);

                HomeOpenBoxResponse duplicatePending = BoxService.Instance.OpenBox(
                    TCPClientMgr.ProtoSerialize(request));
                result.Check(duplicatePending.Code == -2,
                    "pending.block=" + duplicatePending.Code);

                long uid = first.Code == 0 && first.ItemInfo != null && first.ItemInfo.Count > 0
                    ? first.ItemInfo[0].Uid : 0;
                BoxResIntoBagResponse claim = BoxService.Instance.BoxIntoBag(
                    TCPClientMgr.ProtoSerialize(new BoxResIntoBagRequest { Uid = uid }));
                result.Check(claim.Code == 0, "claim=" + claim.Code);
                result.Check(role.BoxOpenItems.Count == 0, "claim.pending=" + role.BoxOpenItems.Count);

                BoxResIntoBagResponse claimAgain = BoxService.Instance.BoxIntoBag(
                    TCPClientMgr.ProtoSerialize(new BoxResIntoBagRequest { Uid = uid }));
                result.Check(claimAgain.Code == -2, "claim.again=" + claimAgain.Code);

                HomeOpenBoxResponse noTreasure = BoxService.Instance.OpenBox(
                    TCPClientMgr.ProtoSerialize(request));
                result.Check(noTreasure.Code == -3, "no.treasure=" + noTreasure.Code);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunEquip()
        {
            var result = new CaseResult("equip");
            long itemUid = 700000000L;
            string error;
            LocalRole role = NewRoleFactory.Create(
                900001L,
                "测试_装备",
                1,
                1,
                ref itemUid,
                out error);
            if (role == null)
            {
                result.Fail("建角失败: " + error);
                return result;
            }

            ItemInfo wrongJob = MakeTestEquip(1913);
            ItemInfo wrongSlot = MakeTestEquip(101);
            ItemInfo valid = MakeTestEquip(101);
            bool validJob = EquipRuleService.TryValidateEquip(role, valid, EquipSlot.Weapon, out error);
            result.Check(validJob, "valid.job");
            bool rejectJob = EquipRuleService.TryValidateEquip(role, wrongJob, EquipSlot.Weapon, out error);
            result.Check(!rejectJob, "reject.wrongJob");
            bool rejectSlot = EquipRuleService.TryValidateEquip(role, wrongSlot, EquipSlot.Clothes, out error);
            result.Check(!rejectSlot, "reject.wrongSlot");

            RoleStatSnapshot withGear = RoleStatService.Instance.Recalculate(role, true);
            role.Equips = new Dictionary<EquipSlot, ItemInfo>();
            RoleStatSnapshot bare = RoleStatService.Instance.Recalculate(role, true);
            result.Check(withGear != null && bare != null, "stats.notNull");
            if (withGear != null && bare != null)
                result.Check(withGear.Combat > bare.Combat, "stats.combat=" + withGear.Combat + ">" + bare.Combat);
            return result;
        }

        private static CaseResult RunIdle()
        {
            var result = new CaseResult("idle");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                role.HomeAFKUsedSec = 0;
                role.HomeAFKAnchorUnix = now - 30;

                IdleRewardService.IdleSettlementResult idle =
                    IdleRewardService.Instance.Settle(role, now, 12345);
                result.Check(idle != null && idle.Applied, "idle.applied=" + (idle == null ? "null" : idle.Applied.ToString()));
                if (idle != null && idle.Applied)
                {
                    result.Check(idle.Events == 3, "idle.events=" + idle.Events);
                    result.Check(role.HomeAFKUsedSec == 30, "idle.used=" + role.HomeAFKUsedSec);
                    result.Check(role.HomeAFKAnchorUnix == now, "idle.anchor=" + role.HomeAFKAnchorUnix);
                    result.Check(idle.Coin > 0 || idle.Exp > 0 || idle.Treasure > 0 || idle.Items.Count > 0,
                        "idle.reward=coin:" + idle.Coin + " exp:" + idle.Exp
                        + " treasure:" + idle.Treasure + " items:" + idle.Items.Count);
                }
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunLevel()
        {
            var result = new CaseResult("level");
            long itemUid = 700000200L;
            string error;
            LocalRole role = NewRoleFactory.Create(
                900003L,
                "测试_升级",
                1,
                1,
                ref itemUid,
                out error);
            if (role == null)
            {
                result.Fail("建角失败: " + error);
                return result;
            }

            LevelUpService.AddExpResult add =
                LevelUpService.Instance.AddExp(role, 2600);
            result.Check(add != null && add.LevelsGained == 2,
                "level.gained=" + (add == null ? -1 : add.LevelsGained));
            result.Check(role.Level == 3, "level.new=" + role.Level);
            result.Check(role.Exp == 100, "level.expRest=" + role.Exp);

            long need = LevelUpService.Instance.GetNeedExp(role.Job, role.Level);
            result.Check(need > 0, "level.needExp=" + need);

            // 原服 levelGift 在首次 online 时按当前等级发；这里单独验证礼包服务。
            LevelGiftService.Instance.GrantForLevel(role, 1);
            result.Check(role.BagData != null && role.BagData.ItemList != null
                && role.BagData.ItemList.Count > 0,
                "level.giftBag=" + (role.BagData == null || role.BagData.ItemList == null
                    ? -1 : role.BagData.ItemList.Count));
            return result;
        }

        private static CaseResult RunMail()
        {
            var result = new CaseResult("mail");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                role.Mails = new List<LocalMail>();
                role.BagData.ItemList = new List<ItemInfo>();

                var attachments = new List<MailService.MailAttachment>
                {
                    new MailService.MailAttachment { CfgId = 77, Num = 3 },
                };
                result.Check(MailService.Instance.SendSystemMail(role, "测试系统邮件", attachments),
                    "mail.sent");

                var list = MailService.Instance.GetList();
                result.Check(list.code == 0 && list.total == 1 && list.data.Count == 1,
                    "mail.list=" + list.total);
                SaveDocument doc = SaveDocumentMapper.FromLocalData(LocalData.Instance);
                int savedMails = doc != null && doc.Roles != null
                    && snapshot.Index >= 0 && snapshot.Index < doc.Roles.Count
                    && doc.Roles[snapshot.Index].Mails != null
                    ? doc.Roles[snapshot.Index].Mails.Count : -1;
                result.Check(savedMails == 1, "mail.save=" + savedMails);
                string mailId = list.data.Count > 0 ? list.data[0].mail_id : "";
                result.Check(list.data.Count > 0 && list.data[0].is_attachments == 1,
                    "mail.attachmentFlag");

                var extract = MailService.Instance.Extract(
                    "{\"mail_id\":\"" + mailId + "\"}");
                result.Check(extract.code == 0, "mail.extract=" + extract.code);
                result.Check(role.BagData.ItemList.Count > 0, "mail.bag=" + role.BagData.ItemList.Count);

                var again = MailService.Instance.Extract(
                    "{\"mail_id\":\"" + mailId + "\"}");
                result.Check(again.code != 0, "mail.repeat=" + again.code);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunBagFull()
        {
            var result = new CaseResult("bag_full");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                role.Mails = new List<LocalMail>();
                var fullBag = new ItemInfo[500];
                for (int i = 0; i < fullBag.Length; i++)
                    fullBag[i] = MakeTestEquip(101);
                role.BagData.ItemList = System.Linq.Enumerable.ToList(fullBag);

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                bool mailed = false;
                for (int seed = 1; seed <= 100 && !mailed; seed++)
                {
                    role.HomeAFKUsedSec = 0;
                    role.HomeAFKAnchorUnix = now - 30;
                    var idle = IdleRewardService.Instance.Settle(role, now, seed);
                    mailed = idle != null && idle.Mailed;
                }
                result.Check(mailed, "bag_full.mailed");
                result.Check(role.Mails.Count > 0, "bag_full.mailCount=" + role.Mails.Count);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunSkill()
        {
            var result = new CaseResult("skill");
            long itemUid = 700000300L;
            string error;
            LocalRole role = NewRoleFactory.Create(
                900004L,
                "测试_技能",
                1,
                1,
                ref itemUid,
                out error);
            if (role == null)
            {
                result.Fail("建角失败: " + error);
                return result;
            }

            int learned = SkillService.Instance.AutoLearn(role);
            result.Check(learned > 0, "skill.learned=" + learned);
            result.Check(role.Skills != null && role.Skills.Count > 0,
                "skill.count=" + (role.Skills == null ? -1 : role.Skills.Count));
            int equipped = 0;
            if (role.SkillSlots != null)
            {
                for (int i = 0; i < role.SkillSlots.Length; i++)
                    if (role.SkillSlots[i] > 0) equipped++;
            }
            result.Check(equipped > 0, "skill.equipped=" + equipped);
            return result;
        }

        private static CaseResult RunSkillAction()
        {
            var result = new CaseResult("skill_action");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                role.Coin = 1000000;
                role.Skills = new List<SkillInfo>
                {
                    new SkillInfo { CfgId = 201 },
                    new SkillInfo { CfgId = 205 },
                };
                role.SkillSlots = new int[13];
                role.BagData.ItemList = new List<ItemInfo>
                {
                    new ItemInfo
                    {
                        CfgId = 23, Uid = 700000401L, Num = 10,
                        CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Type = ItemType.IN_BAG_DISPOSABLE,
                    },
                    new ItemInfo
                    {
                        CfgId = 24, Uid = 700000402L, Num = 100,
                        CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Type = ItemType.IN_BAG_DISPOSABLE_AND_USABLE,
                    },
                };

                var upgrade = SkillService.Instance.Upgrade(201, 0);
                result.Check(upgrade != null && upgrade.Code == 0,
                    "skill_action.upgrade=" + (upgrade == null ? -1 : upgrade.Code));
                result.Check(upgrade != null && upgrade.NewSkillId == 202,
                    "skill_action.newId=" + (upgrade == null ? -1 : upgrade.NewSkillId));
                result.Check(role.Coin == 990000, "skill_action.coin=" + role.Coin);

                var change = SkillService.Instance.Change(
                    1, SkillsSlot.INVALID_SkillsSlot, 202);
                result.Check(change != null && change.Code != 0,
                    "skill_action.slot0Guard=" + (change == null ? -1 : change.Code));

                change = SkillService.Instance.Change(1, (SkillsSlot)1, 202);
                result.Check(change != null && change.Code == 0,
                    "skill_action.change=" + (change == null ? -1 : change.Code));

                var advance = SkillService.Instance.Advance(205, 0);
                result.Check(advance != null && advance.Code == 0,
                    "skill_action.advance=" + (advance == null ? -1 : advance.Code));
                result.Check(advance != null && advance.NewLevel == 1,
                    "skill_action.advancedLevel=" + (advance == null ? -1 : advance.NewLevel));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunRecycle()
        {
            var result = new CaseResult("recycle");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                role.Coin = 1000;
                role.Exp = 0;
                role.BagData.ItemList = new List<ItemInfo>
                {
                    new ItemInfo
                    {
                        CfgId = 101,
                        Uid = 700000501L,
                        Num = 1,
                        CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Type = ItemType.EQUIPMENT,
                    },
                    new ItemInfo
                    {
                        CfgId = 11,
                        Uid = 700000502L,
                        Num = 5,
                        CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Type = ItemType.IN_BAG_DISPOSABLE,
                    },
                };
                role.AutoDisassembly = new AutoDisassemblyInfo();

                var disassemble = ItemService.Instance.ItemDisassembly(
                    TCPClientMgr.ProtoSerialize(new ItemDisassemblyRequest
                    {
                        ItemUids = new long[] { 700000501L },
                    }));
                var disassembleResponse = disassemble as ItemDisassemblyResponse;
                result.Check(disassembleResponse != null && disassembleResponse.Code == 0,
                    "recycle.code=" + (disassembleResponse == null ? -1 : disassembleResponse.Code));
                result.Check(role.BagData.ItemList.Count == 1,
                    "recycle.bag=" + role.BagData.ItemList.Count);
                result.Check(role.Coin > 1000, "recycle.coin=" + role.Coin);

                var destroy = ItemService.Instance.ItemDestroy(
                    TCPClientMgr.ProtoSerialize(new ItemDestroyRequest
                    {
                        ItemUid = 700000502L,
                        Num = 2,
                    }));
                var destroyResponse = destroy as ItemDestroyResponse;
                result.Check(destroyResponse != null && destroyResponse.Code == 0,
                    "recycle.destroy=" + (destroyResponse == null ? -1 : destroyResponse.Code));
                result.Check(role.BagData.ItemList.Count == 1
                    && role.BagData.ItemList[0].Num == 3,
                    "recycle.destroyNum=" + (role.BagData.ItemList.Count == 1
                        ? role.BagData.ItemList[0].Num : -1));

                role.Vip = 2;
                var auto = ItemService.Instance.AutoDisassembly(
                    TCPClientMgr.ProtoSerialize(new AutoDisassemblyRequest
                    {
                        Info = new AutoDisassemblyInfo
                        {
                            Open = true,
                            Level = 10,
                            Quality = ItemQuality.GREEN,
                            LevelSwitch = true,
                            QualitySwitch = true,
                            HighCombat = true,
                        },
                    }));
                var autoResponse = auto as AutoDisassemblyResponse;
                result.Check(autoResponse != null && autoResponse.Code == 0,
                    "recycle.auto=" + (autoResponse == null ? -1 : autoResponse.Code));
                result.Check(role.AutoDisassembly != null && role.AutoDisassembly.Open,
                    "recycle.autoOpen=" + (role.AutoDisassembly == null ? "null" : role.AutoDisassembly.Open.ToString()));
                SaveDocument autoDoc = SaveDocumentMapper.FromLocalData(LocalData.Instance);
                bool autoSaved = autoDoc != null && autoDoc.Roles != null
                    && snapshot.Index >= 0 && snapshot.Index < autoDoc.Roles.Count
                    && autoDoc.Roles[snapshot.Index].AutoDisassembly != null
                    && autoDoc.Roles[snapshot.Index].AutoDisassembly.Open;
                result.Check(autoSaved, "recycle.autoSave=" + autoSaved);

                // ================= ③ 分解体系补全（2026-10-28） =================

                Func<int, long, ItemInfo> makeEquip = (cfgId, uid) => new ItemInfo
                {
                    CfgId = cfgId,
                    Uid = uid,
                    Num = 1,
                    CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Type = ItemType.EQUIPMENT,
                    Equip = new EquipInfo
                    {
                        Quality = ItemQuality.WHITE,
                        Attributes = new List<Stats>(),
                    },
                };

                // ① 堆叠道具：原服每个 uid 只扣 1 件。修复前是整条 Remove ⇒ 分解 5 个只给 1 份奖励、另外 4 个蒸发。
                role.AutoDisassembly = new AutoDisassemblyInfo();
                role.BagData.ItemList = new List<ItemInfo>
                {
                    new ItemInfo
                    {
                        CfgId = 11,
                        Uid = 700000503L,
                        Num = 5,
                        CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Type = ItemType.IN_BAG_DISPOSABLE,
                    },
                };
                long coinBefore = role.Coin;
                var stackRes = ItemService.Instance.ItemDisassembly(
                    TCPClientMgr.ProtoSerialize(new ItemDisassemblyRequest
                    {
                        ItemUids = new long[] { 700000503L },
                    })) as ItemDisassemblyResponse;
                result.Check(stackRes != null && stackRes.Code == 0,
                    "recycle.stackCode=" + (stackRes == null ? -1 : stackRes.Code));
                result.Check(role.BagData.ItemList.Count == 1 && role.BagData.ItemList[0].Num == 4,
                    "recycle.stackLeft=" + (role.BagData.ItemList.Count == 1
                        ? role.BagData.ItemList[0].Num : -1));
                result.Check(role.Coin == coinBefore + 100,
                    "recycle.stackCoin=" + (role.Coin - coinBefore));

                // ② 批量分解里夹一件「穿戴中」的装备：整批拒绝，且前面那件必须还在包里（修复前会被删掉）。
                role.Equips[EquipSlot.Weapon] = makeEquip(101, 700000506L);
                role.BagData.ItemList = new List<ItemInfo> { makeEquip(101, 700000504L) };
                coinBefore = role.Coin;
                var batchRes = ItemService.Instance.ItemDisassembly(
                    TCPClientMgr.ProtoSerialize(new ItemDisassemblyRequest
                    {
                        ItemUids = new long[] { 700000504L, 700000506L },
                    })) as ItemDisassemblyResponse;
                result.Check(batchRes != null && batchRes.Code == -3,
                    "recycle.wornReject=" + (batchRes == null ? -1 : batchRes.Code));
                result.Check(role.BagData.ItemList.Count == 1
                    && role.BagData.ItemList[0].Uid == 700000504L,
                    "recycle.wornKeep=" + role.BagData.ItemList.Count);
                result.Check(role.Coin == coinBefore,
                    "recycle.wornNoReward=" + (role.Coin - coinBefore));

                // ③ 自动分解出厂开关：config.xml /Config/autodecompose/key = quality1 level10
                //    qualityswitch0 levelswitch0 keepbetterswitch1；本地之前一个字都没解析。
                var probe = new LocalRole();
                ItemService.ApplyAutoDisassemblyDefaults(probe);
                result.Check(probe.AutoDisassembly != null
                    && probe.AutoDisassembly.Level == 10
                    && (int)probe.AutoDisassembly.Quality == (int)ItemQuality.WHITE,
                    "recycle.autoDefault=" + (probe.AutoDisassembly == null
                        ? "null"
                        : probe.AutoDisassembly.Level + "/" + (int)probe.AutoDisassembly.Quality));
                result.Check(probe.AutoDisassembly != null
                    && !probe.AutoDisassembly.LevelSwitch
                    && !probe.AutoDisassembly.QualitySwitch
                    && probe.AutoDisassembly.HighCombat
                    && !probe.AutoDisassembly.Open,
                    "recycle.autoSwitch=" + (probe.AutoDisassembly == null
                        ? "null"
                        : probe.AutoDisassembly.LevelSwitch + "," + probe.AutoDisassembly.QualitySwitch + ","
                          + probe.AutoDisassembly.HighCombat));

                // ④ 掉落自动分解：关着不动 / 开着（等级档）碎掉且不进包 / 保底档拦住
                role.BagData.ItemList = new List<ItemInfo>();
                role.AutoDisassembly = new AutoDisassemblyInfo
                {
                    Open = false,
                    Level = 10,
                    LevelSwitch = true,
                    Quality = ItemQuality.WHITE,
                    QualitySwitch = false,
                    HighCombat = false,
                };
                result.Check(!ItemService.Instance.TryAutoDecomposeDrop(role, 101, 1),
                    "recycle.autoOff");

                role.AutoDisassembly.Open = true;
                coinBefore = role.Coin;
                bool autoHit = ItemService.Instance.TryAutoDecomposeDrop(role, 101, 1);
                result.Check(autoHit, "recycle.autoHit=" + autoHit);
                result.Check(role.Coin == coinBefore + 100,
                    "recycle.autoCoin=" + (role.Coin - coinBefore));
                result.Check(ItemService.CountItem(role, 101) == 0,
                    "recycle.autoNoBag=" + ItemService.CountItem(role, 101));

                // 保底档：HighCombat 开着且该部位空着（原服空部位算"更好"）→ 不分解
                role.Equips.Remove(EquipSlot.Weapon);
                role.AutoDisassembly.HighCombat = true;
                coinBefore = role.Coin;
                result.Check(!ItemService.Instance.TryAutoDecomposeDrop(role, 101, 1),
                    "recycle.autoHighCombat");
                result.Check(role.Coin == coinBefore,
                    "recycle.autoHighCombatCoin=" + (role.Coin - coinBefore));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunTask()
        {
            var result = new CaseResult("task");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                role.Tasks = new List<TaskInfo>();
                int added = TaskService.Instance.EnsureInitialized(role);
                result.Check(added > 0 && role.Tasks.Count > 0,
                    "task.init=" + added + "/" + role.Tasks.Count);
                SaveDocument taskDoc = SaveDocumentMapper.FromLocalData(LocalData.Instance);
                int savedTasks = taskDoc != null && taskDoc.Roles != null
                    && snapshot.Index >= 0 && snapshot.Index < taskDoc.Roles.Count
                    && taskDoc.Roles[snapshot.Index].Tasks != null
                    ? taskDoc.Roles[snapshot.Index].Tasks.Count : -1;
                result.Check(savedTasks == role.Tasks.Count, "task.save=" + savedTasks);

                TaskInfo target = null;
                for (int i = 0; i < role.Tasks.Count; i++)
                {
                    if (role.Tasks[i] != null && role.Tasks[i].Target != null
                        && role.Tasks[i].Status == TaskStatus.IN_PROGRESS
                        && role.Tasks[i].Target.TargetNum > 0)
                    {
                        target = role.Tasks[i];
                        break;
                    }
                }
                result.Check(target != null, "task.target");
                if (target == null)
                    return result;

                TaskService.Instance.Dispatch(role, target.Target.Type,
                    target.Target.TargetId, target.Target.TargetNum + 100);
                result.Check(target.Status == TaskStatus.COMPLETED,
                    "task.completeStatus=" + target.Status);

                long coinBefore = role.Coin;
                TaskComplateResponse response = TaskService.Instance.Complete(target.CfgId);
                result.Check(response != null && response.Code == 0,
                    "task.claim=" + (response == null ? -1 : response.Code));
                result.Check(target.Status == TaskStatus.REWARDED,
                    "task.rewarded=" + target.Status);
                result.Check(role.Coin >= coinBefore, "task.coin=" + role.Coin);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunTaskReset()
        {
            var result = new CaseResult("task_reset");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                role.Tasks = new List<TaskInfo>();
                TaskService.Instance.EnsureInitialized(role);

                TaskInfo daily = null;
                for (int i = 0; i < role.Tasks.Count; i++)
                {
                    TaskInfo task = role.Tasks[i];
                    if (task != null && (task.Class == TaskClass.DAILY
                        || task.Class == TaskClass.DAILY_ACTIVITY))
                    {
                        daily = task;
                        break;
                    }
                }
                result.Check(daily != null, "task_reset.daily");
                if (daily == null)
                    return result;

                daily.Target.Amount = daily.Target.TargetNum;
                daily.Target.Completed = true;
                daily.Status = TaskStatus.COMPLETED;
                daily.RefreshTime = DateTimeOffset.Now.ToUnixTimeSeconds() - 10;
                int changed = TaskService.Instance.ResetIfNeeded(role);
                result.Check(changed > 0, "task_reset.changed=" + changed);
                result.Check(daily.Status == TaskStatus.IN_PROGRESS,
                    "task_reset.status=" + daily.Status);
                result.Check(daily.Target.Amount == 0,
                    "task_reset.amount=" + daily.Target.Amount);
                result.Check(daily.RefreshTime > DateTimeOffset.Now.ToUnixTimeSeconds(),
                    "task_reset.next=" + daily.RefreshTime);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunHomeAFKReset()
        {
            var result = new CaseResult("home_afk");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            try
            {
                LocalRole role = snapshot.Role;
                int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
                role.HomeAFKResetDay = int.Parse(DateTime.Now.AddDays(-1).ToString("yyyyMMdd"));
                role.HomeAFKUsedSec = 7200;
                LocalPusher.EnsureHomeAFKReset(role);
                result.Check(role.HomeAFKResetDay == today,
                    "home_afk.day=" + role.HomeAFKResetDay);
                result.Check(role.HomeAFKUsedSec == 0,
                    "home_afk.used=" + role.HomeAFKUsedSec);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        private static CaseResult RunBattle()
        {
            var result = new CaseResult("battle");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            List<PartnerInfo> oldPartners = snapshot.Role.Partners;
            List<PartnerSlotInfo> oldSlots = snapshot.Role.PartnerSlots;
            try
            {
                BattleService.Instance.Exit();
                EnsureBattlePartner(snapshot.Role);
                long coinBefore = snapshot.Role.Coin;
                JoinDeskResponse enter = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 600,
                    BattleId = 1401001,
                });
                result.Check(enter != null && enter.Code == 0,
                    "battle.enter=" + (enter == null ? -1 : enter.Code));
                int monsterCount = enter == null || enter.TableInfo == null
                    || enter.TableInfo.MonsterList == null
                    ? 0 : enter.TableInfo.MonsterList.Count;
                result.Check(monsterCount > 0, "battle.monsters=" + monsterCount);
                if (enter == null || enter.Code != 0 || enter.TableInfo == null)
                    return result;

                // 伙伴进战斗：先确保有一只上阵伙伴，再看是否生成战场单位
                result.Check(BattleService.Instance.BattlePartnerCount > 0,
                    "battle.partners=" + BattleService.Instance.BattlePartnerCount);

                // 出生点应来自 map.json 的 Born[0]（战役 1-1 地图 1 → 25,25）。
                result.Check(enter.Location != null && enter.Location.X == 25
                        && enter.Location.Y == 25,
                    "battle.born=" + (enter.Location == null
                        ? "null" : enter.Location.X + "," + enter.Location.Y));

                MoveResponse move = BattleService.Instance.Move(new MoveRequest
                {
                    Location = new Point { X = 30, Y = 30 },
                });
                result.Check(move != null && move.Code == 0,
                    "battle.move=" + (move == null ? -1 : move.Code));

                // 越界坐标应被钳制进可行走矩形（地图 1：50x50，内缩后 6..44 / 13..37）。
                MoveResponse clamp = BattleService.Instance.Move(new MoveRequest
                {
                    Location = new Point { X = -50, Y = 999 },
                });
                result.Check(clamp != null && clamp.Code == 0 && clamp.Location != null
                        && clamp.Location.X == 6 && clamp.Location.Y == 37,
                    "battle.clamp=" + (clamp == null || clamp.Location == null
                        ? "null" : clamp.Location.X + "," + clamp.Location.Y));
                BattleService.Instance.Move(new MoveRequest
                {
                    Location = new Point { X = 30, Y = 30 },
                });

                // 怪物被动仇恨：还没打到任何怪时，跑几秒 tick，玩家不应掉血、也不应有怪仇恨。
                int hpBeforeAggro = BattleService.Instance.PlayerHp;
                BattleService.Instance.Tick(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 3000);
                result.Check(BattleService.Instance.AggroMonsterCount == 0
                        && BattleService.Instance.PlayerHp == hpBeforeAggro,
                    "battle.passive=aggro" + BattleService.Instance.AggroMonsterCount
                        + "/hp" + BattleService.Instance.PlayerHp);


                // 技能伤害依赖 CFGItem_Skill.SkillEffect（[{属性id:数值}, ...]），
                // 这条断言用来确认 JSON 新字段确实解析成功。
                CFGItem_Skill yuehu = CFG_Skill.GetById(201);
                result.Check(yuehu != null && yuehu.SkillEffect != null
                        && yuehu.SkillEffect.Length == 2,
                    "battle.skilleffect=" + (yuehu == null || yuehu.SkillEffect == null
                        ? "null" : yuehu.SkillEffect.Length.ToString()));

                // 技能冷却：201 的 CoolDown=3000，连放第二次应被服务端拒绝。
                Point firstMonsterPos = enter.TableInfo.MonsterList[0].Location;
                long firstMonsterUid = enter.TableInfo.MonsterList[0].Uid;
                BattleService.Instance.Move(new MoveRequest
                {
                    Location = new Point { X = firstMonsterPos.X, Y = firstMonsterPos.Y },
                });
                AttackResponse skillFirst = BattleService.Instance.Attack(new AttackRequest
                {
                    SkillId = 201,
                    TargetUids = new long[] { firstMonsterUid },
                    Direction = Direction.RIGHT,
                    Location = firstMonsterPos,
                });
                AttackResponse skillAgain = BattleService.Instance.Attack(new AttackRequest
                {
                    SkillId = 201,
                    TargetUids = new long[] { firstMonsterUid },
                    Direction = Direction.RIGHT,
                    Location = firstMonsterPos,
                });
                result.Check(skillFirst != null && skillFirst.Code == 0
                        && skillAgain != null && skillAgain.Error == "技能冷却时间未到",
                    "battle.cooldown=" + (skillFirst == null ? -1 : skillFirst.Code)
                        + "/" + (skillAgain == null ? "null" : skillAgain.Error));
                // 伙伴单独输出验证：只把玩家挪到怪旁边，不主动攻击，靠伙伴 AI 打
                Point partnerTestPos = enter.TableInfo.MonsterList[0].Location;
                BattleService.Instance.Move(new MoveRequest
                {
                    Location = new Point { X = partnerTestPos.X, Y = partnerTestPos.Y },
                });
                int hpBeforePartner = BattleService.Instance.MonsterHpTotal;
                long partnerTickBase = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // 按 50ms 步进推进 6 秒，让伙伴能走过去并打出伤害
                // （Tick 内部 dt 上限 50ms，跳大步长会让移动失效）
                for (int i = 1; i <= 120; i++)
                    BattleService.Instance.Tick(partnerTickBase + 50L * i);
                result.Check(BattleService.Instance.MonsterHpTotal < hpBeforePartner,
                    "battle.partnerDamage=" + hpBeforePartner
                        + "->" + BattleService.Instance.MonsterHpTotal);

                for (int i = 0; i < enter.TableInfo.MonsterList.Count; i++)
                {
                    long uid = enter.TableInfo.MonsterList[i].Uid;
                    Point mpos = enter.TableInfo.MonsterList[i].Location;
                    // 普攻 MaxDistance=2，先贴到目标身边再打，避免被距离校验拒绝。
                    BattleService.Instance.Move(new MoveRequest
                    {
                        Location = new Point { X = mpos.X, Y = mpos.Y },
                    });
                    // 伤害是延迟生效的（对齐原服 DelayTime），所以每打一刀要推进时间让伤害落地，
                    // 同时避免同一帧堆积过多推送事件。
                    for (int hit = 0; hit < 20; hit++)
                    {
                        AttackResponse attack = BattleService.Instance.Attack(new AttackRequest
                        {
                            SkillId = 100,
                            TargetUids = new long[] { uid },
                            Direction = Direction.RIGHT,
                            Location = mpos,
                        });
                        if (attack == null || attack.Code != 0)
                            break;
                        BattleService.Instance.Tick(
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 700L * (hit + 1));
                        if (BattleService.Instance.AliveMonsterCount == 0)
                            break;
                    }
                }

                BattleService.Instance.Tick(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5000);
                BattleService.Instance.Tick(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 9000);
                result.Check(snapshot.Role.BattleId == 1401001,
                    "battle.clear=" + snapshot.Role.BattleId);
                result.Check(snapshot.Role.Coin > coinBefore,
                    "battle.coin=" + snapshot.Role.Coin);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                snapshot.Role.Partners = oldPartners;
                snapshot.Role.PartnerSlots = oldSlots;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
                LocalPusher.Instance.PushBoxNow();
            }
            return result;
        }

        /// <summary>按槽位号取已上阵的伙伴 Uid（0 表示空）。</summary>
        private static long FindSlotUid(LocalRole role, int slotId)
        {
            if (role == null || role.PartnerSlots == null)
                return 0;
            for (int i = 0; i < role.PartnerSlots.Count; i++)
            {
                PartnerSlotInfo slot = role.PartnerSlots[i];
                if (slot != null && slot.SlotId == slotId)
                    return slot.PartnerUid;
            }
            return 0;
        }

        /// <summary>
        /// 保证角色至少有一只上阵伙伴（战斗里验证伙伴 AI 用）。
        /// 伙伴槽位 1 需要主角 20 级（config.xml openslot），
        /// 测试角色可能还没到，这里临时提等级，BoxSnapshot 会自动恢复。
        /// </summary>
        private static void EnsureBattlePartner(LocalRole role)
        {
            if (role == null)
                return;
            int needLevel = PartnerService.GetSlotNeedLevel(PartnerService.FirstPartnerSlotId);
            if (role.Level < needLevel)
                role.Level = needLevel;
            PartnerService.Instance.EnsureSlots(role);
            if (PartnerService.Instance.GetBattlePartners(role).Count == 0)
            {
                if (role.Partners == null || role.Partners.Count == 0)
                    PartnerService.Instance.AddPartner(role, 10);
                if (role.Partners.Count == 0)
                    return;
                PartnerService.Instance.Combat(new PartnerCombatRequest
                {
                    SlotId = PartnerService.FirstPartnerSlotId,
                    PartnerUid = role.Partners[0].Uid,
                });
            }
            // 前面用例可能把伙伴槽位回滚过，TotalCombat 是缓存值，
            // 这里强制重算，保证后续战力门槛判断用的是当前真实战力。
            RoleStatService.Instance.Recalculate(role, true);
        }

        private static CaseResult RunPartner()
        {
            var result = new CaseResult("partner");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            List<PartnerInfo> oldPartners = snapshot.Role.Partners;
            List<PartnerSlotInfo> oldSlots = snapshot.Role.PartnerSlots;
            try
            {
                LocalRole role = snapshot.Role;
                role.Partners = new List<PartnerInfo>();
                role.PartnerSlots = new List<PartnerSlotInfo>();

                // 1) 槽位初始化：原服 MaxPartnerNum = 4
                PartnerService.Instance.EnsureSlots(role);
                result.Check(role.PartnerSlots.Count == PartnerService.MaxPartnerSlots,
                    "partner.slots=" + role.PartnerSlots.Count);

                // 1.1) 槽位按等级解锁（config.xml openslot：1=20 / 2=28 / 3=35 / 4=40）
                int savedLevel = role.Level;
                role.Level = 18;
                int unlocked18 = PartnerService.Instance.CountUnlockedSlots(role);
                CommonResponse lowCombat = PartnerService.Instance.Combat(new PartnerCombatRequest
                {
                    SlotId = 1,
                    PartnerUid = 0,
                });
                result.Check(unlocked18 == 0 && lowCombat != null && lowCombat.Code != 0,
                    "partner.slotLockedAtLv18=" + unlocked18 + "/"
                        + (lowCombat == null ? "null" : lowCombat.Error));
                role.Level = 40;
                result.Check(PartnerService.Instance.CountUnlockedSlots(role) == 4,
                    "partner.slotsAtLv40=" + PartnerService.Instance.CountUnlockedSlots(role));

                // 2) 获得伙伴（品质 6 战士伙伴 10，带 5 个技能）
                int added = PartnerService.Instance.AddPartner(role, 10);
                result.Check(added == 1 && role.Partners.Count == 1,
                    "partner.add=" + added + "/" + role.Partners.Count);
                if (role.Partners.Count == 0)
                    return result;

                // 3) 重复获得应转碎片
                int dup = PartnerService.Instance.AddPartner(role, 10);
                result.Check(dup == 2, "partner.dupAdd=" + dup);

                PartnerInfo partner = role.Partners[0];
                CFGItem_Partner dbgCfg = CFG_Partner.GetById(10);
                result.Check(dbgCfg != null && dbgCfg.Skills != null && dbgCfg.Skills.Length > 0,
                    "partner.cfgSkills=" + (dbgCfg == null || dbgCfg.Skills == null ? -1 : dbgCfg.Skills.Length));
                // 伙伴 10 的技能从 2 星开始解锁，所以 0 星时没有技能是配置决定的
                int skillCount = partner.Skills == null ? 0 : partner.Skills.Count;
                result.Check(skillCount == 0, "partner.skillsAtStar0=" + skillCount);

                // 4) 上阵：槽位写入 + 战力不应下降
                // ⚠️ role.TotalCombat 是**缓存值**：前面用例（battle 给经验、equip 换装）改了属性但
                //    不一定重算过，直接拿它当基线会虚高 —— 表现为 "6623>=8139" 这种假红。
                //    这里先各自重算再比较，比的是真实战力。
                RoleStatService.Instance.RecalculateCurrent();
                int combatBefore = role.TotalCombat;
                CommonResponse combat = PartnerService.Instance.Combat(new PartnerCombatRequest
                {
                    SlotId = 1,
                    PartnerUid = partner.Uid,
                });
                result.Check(combat != null && combat.Code == 0,
                    "partner.combat=" + (combat == null ? -1 : combat.Code));
                result.Check(FindSlotUid(role, 1) == partner.Uid,
                    "partner.slot1=" + FindSlotUid(role, 1));
                RoleStatService.Instance.RecalculateCurrent();
                result.Check(role.TotalCombat >= combatBefore,
                    "partner.combatPower=" + role.TotalCombat + ">=" + combatBefore);

                // 5) 同一伙伴不能重复上阵
                CommonResponse again = PartnerService.Instance.Combat(new PartnerCombatRequest
                {
                    SlotId = 2,
                    PartnerUid = partner.Uid,
                });
                result.Check(again != null && again.Code != 0,
                    "partner.dupCombatBlocked");

                // 6) 下阵
                CommonResponse rest = PartnerService.Instance.Rest(new PartnerRestRequest
                {
                    SlotId = 1,
                    PartnerUid = partner.Uid,
                });
                result.Check(rest != null && rest.Code == 0 && FindSlotUid(role, 1) == 0,
                    "partner.rest=" + (rest == null ? -1 : rest.Code));

                // 7) 升星材料不足时应明确失败：先清空金币（每级升星都消耗金币）
                long savedCoin = role.Coin;
                role.Coin = 0;
                LocalData.Instance.Gold = 0;
                CommonResponse starFail = PartnerService.Instance.Star(new PartnerStarRequest
                {
                    SlotId = 1,
                    PartnerUid = partner.Uid,
                });
                result.Check(starFail != null && starFail.Code != 0,
                    "partner.starBlocked=" + (starFail == null ? "null" : starFail.Error));
                result.Check(partner.Star == 0, "partner.starUnchanged=" + partner.Star);
                role.Coin = savedCoin;
                LocalData.Instance.Gold = savedCoin;

                // 8) 补足材料后连续升到 2 星（伙伴 10 的技能从 2 星开始解锁）
                for (int i = 0; i < 2; i++)
                {
                    GrantStarMaterial(role, partner);
                    CommonResponse ok = PartnerService.Instance.Star(new PartnerStarRequest
                    {
                        SlotId = 1,
                        PartnerUid = partner.Uid,
                    });
                    if (ok == null || ok.Code != 0)
                        break;
                }
                result.Check(partner.Star == 2, "partner.star=" + partner.Star);

                int skillsAfterStar = partner.Skills == null ? 0 : partner.Skills.Count;
                result.Check(skillsAfterStar > 0, "partner.skillsAfterStar=" + skillsAfterStar);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                snapshot.Role.Partners = oldPartners;
                snapshot.Role.PartnerSlots = oldSlots;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>按 partner.json 的 StarConsume 给下一星级补足材料。</summary>
        private static void GrantStarMaterial(LocalRole role, PartnerInfo partner)
        {
            if (role == null || partner == null)
                return;
            CFGItem_Partner cfg;
            try { cfg = CFG_Partner.GetById(partner.CfgId); }
            catch { return; }
            if (cfg == null || cfg.StarConsume == null)
                return;

            int nextStar = partner.Star + 1;
            for (int i = 0; i < cfg.StarConsume.Length; i++)
            {
                CFGItem_Partner.StarConsumeItem consume = cfg.StarConsume[i];
                if (consume == null || consume.id != nextStar || consume.values == null)
                    continue;
                for (int v = 0; v < consume.values.Length; v++)
                {
                    CFGItem_Partner.StarConsumeItem.Item need = consume.values[v];
                    if (need == null || need.key <= 0 || need.value <= 0)
                        continue;
                    long have = ItemService.CountItem(role, need.key);
                    if (have < need.value)
                        ItemService.Instance.GrantReward(role, need.key, need.value - have);
                }
                return;
            }
        }

        private static CaseResult RunBoss()
        {
            var result = new CaseResult("boss");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            int oldBossId = role.BossId;
            int oldTime = role.BossChallengeTime;
            int oldDay = role.BossResetDay;
            int oldCombat = role.TotalCombat;
            List<PartnerInfo> oldPartners = role.Partners;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            try
            {
                BattleService.Instance.Exit();
                role.BossId = 0;
                role.BossChallengeTime = 0;
                role.BossResetDay = 0;

                // 1) 次数按自然日惰性重置
                BattleService.Instance.EnsureBossChallenge(role);
                result.Check(role.BossChallengeTime == BattleService.DefaultBossChallengeTime,
                    "boss.dailyTime=" + role.BossChallengeTime);

                // 2) 战力不足应被拦（首领看 Powerlimit，不看等级）
                int savedCombat = role.TotalCombat;
                role.TotalCombat = 0;
                JoinDeskResponse weak = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 700,
                    BattleId = 1501001,
                });
                result.Check(weak != null && weak.Code != 0,
                    "boss.powerBlocked=" + (weak == null ? "null" : weak.Error));
                role.TotalCombat = savedCombat;

                // 3) 正常进入首领本
                EnsureBattlePartner(role);
                JoinDeskResponse enter = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 700,
                    BattleId = 1501001,
                });
                result.Check(enter != null && enter.Code == 0,
                    "boss.enter=" + (enter == null ? -1 : enter.Code));
                int monsterCount = enter == null || enter.TableInfo == null
                    || enter.TableInfo.MonsterList == null
                    ? 0 : enter.TableInfo.MonsterList.Count;
                result.Check(monsterCount == 1, "boss.monsters=" + monsterCount);
                if (enter == null || enter.Code != 0 || enter.TableInfo == null)
                    return result;

                // 4) 打死首领
                // Tick 时间必须单调递增，否则内部 LastTickMs 会"倒退"，
                // 结算判定（AllDeadAtMs）永远到不了，最后一次也结算不掉。
                long clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                for (int i = 0; i < enter.TableInfo.MonsterList.Count; i++)
                {
                    long uid = enter.TableInfo.MonsterList[i].Uid;
                    Point mpos = enter.TableInfo.MonsterList[i].Location;
                    BattleService.Instance.Move(new MoveRequest
                    {
                        Location = new Point { X = mpos.X, Y = mpos.Y },
                    });
                    for (int hit = 0; hit < 40; hit++)
                    {
                        AttackResponse attack = BattleService.Instance.Attack(new AttackRequest
                        {
                            SkillId = 100,
                            TargetUids = new long[] { uid },
                            Direction = Direction.RIGHT,
                            Location = mpos,
                        });
                        if (attack == null || attack.Code != 0)
                            break;
                        clock += 700;
                        BattleService.Instance.Tick(clock);
                        if (BattleService.Instance.AliveMonsterCount == 0)
                            break;
                    }
                }

                result.Check(BattleService.Instance.AliveMonsterCount == 0,
                    "boss.killed=" + BattleService.Instance.AliveMonsterCount
                        + "/hp" + BattleService.Instance.MonsterHpTotal
                        + "/playerHp" + BattleService.Instance.PlayerHp);

                // 清空后还要等 WinSettleDelayMs 才结算，这里连续推进直到结算完成
                for (int i = 0; i < 20; i++)
                {
                    clock += 1000;
                    BattleService.Instance.Tick(clock);
                    if (role.BossId == 1501001)
                        break;
                }

                // 5) 进度推进 + 次数扣减
                result.Check(role.BossId == 1501001, "boss.clear=" + role.BossId);
                result.Check(role.BossChallengeTime == BattleService.DefaultBossChallengeTime - 1,
                    "boss.timeAfter=" + role.BossChallengeTime);

                // 6) 次数用完应被拦
                role.BossChallengeTime = 0;
                JoinDeskResponse noTime = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 700,
                    BattleId = 1501001,
                });
                result.Check(noTime != null && noTime.Code != 0,
                    "boss.timeBlocked=" + (noTime == null ? "null" : noTime.Error));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.BossId = oldBossId;
                role.BossChallengeTime = oldTime;
                role.BossResetDay = oldDay;
                role.TotalCombat = oldCombat;
                role.Partners = oldPartners;
                role.PartnerSlots = oldSlots;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        private static CaseResult RunTower()
        {
            var result = new CaseResult("tower");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            int oldTowerId = role.TrialTowerId;
            int oldCombat = role.TotalCombat;
            List<PartnerInfo> oldPartners = role.Partners;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            try
            {
                BattleService.Instance.Exit();
                role.TrialTowerId = 0;

                // 1) 进度为 0 时，下一层应是首层 1601001
                result.Check(BattleService.GetNextTrialTowerId(role) == 1601001,
                    "tower.next=" + BattleService.GetNextTrialTowerId(role));

                // 2) 先上阵伙伴（战力来源，首层要求 3000）
                EnsureBattlePartner(role);

                // 3) 跳层应被拦，且报的必须是层数问题而不是战力问题
                //    （原服 TrialTowerMap 用 == 校验，只能打下一层）
                JoinDeskResponse skip = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 800,
                    BattleId = 1601003,
                });
                result.Check(skip != null && skip.Code != 0 && skip.Error == "试炼塔层数不合法",
                    "tower.skipBlocked=" + (skip == null ? "null" : skip.Error));

                // 4) 正常进入首层
                JoinDeskResponse enter = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 800,
                    BattleId = 1601001,
                });
                result.Check(enter != null && enter.Code == 0,
                    "tower.enter=" + (enter == null ? -1 : enter.Code));
                if (enter == null || enter.Code != 0 || enter.TableInfo == null)
                    return result;
                int monsterCount = enter.TableInfo.MonsterList == null
                    ? 0 : enter.TableInfo.MonsterList.Count;
                result.Check(monsterCount == 6, "tower.monsters=" + monsterCount);

                // 5) 清层。伙伴会自己打怪，所以"目标不存在"是正常现象，不算失败。
                long clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                for (int i = 0; i < enter.TableInfo.MonsterList.Count; i++)
                {
                    if (BattleService.Instance.AliveMonsterCount == 0)
                        break;
                    long uid = enter.TableInfo.MonsterList[i].Uid;
                    Point mpos = enter.TableInfo.MonsterList[i].Location;
                    BattleService.Instance.Move(new MoveRequest
                    {
                        Location = new Point { X = mpos.X, Y = mpos.Y },
                    });
                    for (int hit = 0; hit < 40; hit++)
                    {
                        if (BattleService.Instance.AliveMonsterCount == 0)
                            break;
                        AttackResponse attack = BattleService.Instance.Attack(new AttackRequest
                        {
                            SkillId = 100,
                            TargetUids = new long[] { uid },
                            Direction = Direction.RIGHT,
                            Location = mpos,
                        });
                        if (i == 0 && hit == 0)
                            result.Check(attack != null && attack.Code == 0,
                                "tower.firstHit=" + (attack == null ? "null" : attack.Code + "/" + attack.Error));
                        if (attack == null || attack.Code != 0)
                            break;
                        clock += 700;
                        BattleService.Instance.Tick(clock);
                    }
                }
                result.Check(BattleService.Instance.AliveMonsterCount == 0,
                    "tower.killed=" + BattleService.Instance.AliveMonsterCount
                        + "/hp" + BattleService.Instance.MonsterHpTotal
                        + "/playerHp" + BattleService.Instance.PlayerHp);

                // 6) 等结算
                for (int i = 0; i < 20; i++)
                {
                    clock += 1000;
                    BattleService.Instance.Tick(clock);
                    if (role.TrialTowerId == 1601001)
                        break;
                }
                result.Check(role.TrialTowerId == 1601001, "tower.clear=" + role.TrialTowerId);

                // 7) 下一层自动推进到 1601002
                result.Check(BattleService.GetNextTrialTowerId(role) == 1601002,
                    "tower.nextAfter=" + BattleService.GetNextTrialTowerId(role));

                // 8) 重复刷已通关的层应被拦
                JoinDeskResponse replay = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 800,
                    BattleId = 1601001,
                });
                result.Check(replay != null && replay.Code != 0,
                    "tower.replayBlocked=" + (replay == null ? "null" : replay.Error));

                // 9) 战力不足应被拦（首层 Powerlimit = 3000）
                role.TrialTowerId = 0;
                role.TotalCombat = 0;
                JoinDeskResponse weak = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 800,
                    BattleId = 1601001,
                });
                result.Check(weak != null && weak.Code != 0,
                    "tower.powerBlocked=" + (weak == null ? "null" : weak.Error));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.TrialTowerId = oldTowerId;
                role.TotalCombat = oldCombat;
                role.Partners = oldPartners;
                role.PartnerSlots = oldSlots;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 经验池（module 14，Lv20）：主角升级 + 伙伴升级。
        /// 核心规则来自原服 Player.levelUp / Partner.levelUp：
        /// - 两者都消耗**主角经验**；
        /// - 伙伴等级不能超过主角等级；
        /// - 上阵伙伴数 == 1 时主角可连升，否则一次只升 1 级。
        /// </summary>
        private static CaseResult RunExpPool()
        {
            var result = new CaseResult("exp_pool");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            long oldExp = role.Exp;
            List<PartnerInfo> oldPartners = role.Partners;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            try
            {
                EnsureBattlePartner(role);
                // 主角固定到 40 级，方便算经验门槛（槽位 1 也在 20 级解锁）
                role.Level = 40;
                role.Exp = 0;

                // 1) 经验不足时主角不升级
                int gained = LevelUpService.Instance.ProcessLevelUp(role);
                result.Check(gained == 0 && role.Level == 40,
                    "exp.heroNoExp=" + gained + "/lv" + role.Level);

                // 2) 给刚好够的经验，只升 1 级（上阵伙伴数 == 1 时理论上可连升，
                //    但经验只够一级，所以结果仍是 1）
                long need40 = LevelUpService.Instance.GetNeedExp(role.Job, 40);
                role.Exp = need40;
                gained = LevelUpService.Instance.ProcessLevelUp(role);
                result.Check(gained == 1 && role.Level == 41 && role.Exp == 0,
                    "exp.heroLevelUp=" + gained + "/lv" + role.Level + "/exp" + role.Exp);

                // 3) 伙伴等级不能超过主角
                List<PartnerInfo> battlePartners = PartnerService.Instance.GetBattlePartners(role);
                result.Check(battlePartners.Count == 1,
                    "exp.battlePartners=" + battlePartners.Count);
                if (battlePartners.Count == 0)
                    return result;

                PartnerInfo partner = battlePartners[0];
                partner.Level = role.Level;
                role.Exp = 999999999L;
                bool blocked = LevelUpService.Instance.LevelUpPartner(role, partner);
                result.Check(!blocked && partner.Level == role.Level,
                    "exp.partnerCapBlocked=" + blocked + "/lv" + partner.Level);

                // 4) 伙伴升级：消耗主角经验
                partner.Level = role.Level - 1;
                partner.CfgId = partner.CfgId > 0 ? partner.CfgId : 10;
                long needPartner = LevelUpService.Instance.GetNeedExp(
                    PartnerService.GetPartnerJob(partner.CfgId), partner.Level);
                result.Check(needPartner > 0, "exp.partnerNeed=" + needPartner);

                role.Exp = needPartner;
                int partnerOld = partner.Level;
                bool ok = LevelUpService.Instance.LevelUpPartner(role, partner);
                result.Check(ok && partner.Level == partnerOld + 1 && role.Exp == 0,
                    "exp.partnerLevelUp=" + ok + "/lv" + partner.Level + "/exp" + role.Exp);

                // 5) 经验不足时伙伴不升级，且经验不被扣
                partner.Level = role.Level - 1;
                role.Exp = 0;
                bool poor = LevelUpService.Instance.LevelUpPartner(role, partner);
                result.Check(!poor && partner.Level == role.Level - 1 && role.Exp == 0,
                    "exp.partnerNoExp=" + poor + "/lv" + partner.Level);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                role.Exp = oldExp;
                role.Partners = oldPartners;
                role.PartnerSlots = oldSlots;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 被动技能属性接入（对应原服 refreshStats 的「// 被动技能」段）。
        /// 战士三被动：
        ///   27000 [灵活] 20 级  DODGE +20
        ///   28000 [坚韧] 60 级  TOUGHNESS +30
        ///   29000 [厚泽] 100 级 HEALTH_PERCENTAGE +5%
        /// </summary>
        private static CaseResult RunPassiveSkill()
        {
            var result = new CaseResult("passive");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<SkillInfo> oldSkills = role.Skills;
            int oldLevel = role.Level;
            List<PartnerInfo> oldPartners = role.Partners;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            try
            {
                // 清空伙伴与技能，保证只有被动技能影响属性
                role.Partners = new List<PartnerInfo>();
                role.PartnerSlots = new List<PartnerSlotInfo>();
                role.Skills = new List<SkillInfo>();
                role.Level = 20;

                int dodgeBefore = RoleStatService.Instance.Recalculate(role, true)
                    .Get(pb.Attribute.DODGE);

                // 直接塞被动技能 2700001（[灵活] DODGE+20）
                role.Skills.Add(new SkillInfo { CfgId = 2700001 });
                int dodgeAfter = RoleStatService.Instance.Recalculate(role, true)
                    .Get(pb.Attribute.DODGE);
                result.Check(dodgeAfter == dodgeBefore + 20,
                    "passive.dodge=" + dodgeBefore + "->" + dodgeAfter);

                // 坚韧 2800001：TOUGHNESS +30
                int toughBefore = RoleStatService.Instance.Recalculate(role, true)
                    .Get(pb.Attribute.TOUGHNESS);
                role.Skills.Add(new SkillInfo { CfgId = 2800001 });
                int toughAfter = RoleStatService.Instance.Recalculate(role, true)
                    .Get(pb.Attribute.TOUGHNESS);
                result.Check(toughAfter == toughBefore + 30,
                    "passive.toughness=" + toughBefore + "->" + toughAfter);

                // 厚泽 2900001：HEALTH_PERCENTAGE +5% → 必须走百分比应用，最大生命上涨
                int hpBefore = RoleStatService.Instance.Recalculate(role, true)
                    .Get(pb.Attribute.MAX_HEALTH);
                role.Skills.Add(new SkillInfo { CfgId = 2900001 });
                int hpAfter = RoleStatService.Instance.Recalculate(role, true)
                    .Get(pb.Attribute.MAX_HEALTH);
                result.Check(hpAfter > hpBefore,
                    "passive.hpPercent=" + hpBefore + "->" + hpAfter);

                // 自动学习：20 级应该只学到 [灵活]，不该学到 [坚韧]/[厚泽]
                role.Skills = new List<SkillInfo>();
                SkillService.Instance.AutoLearn(role, false);
                bool hasLinghuo = HasSkillCfg(role, 2700001);
                bool hasJianren = HasSkillCfg(role, 2800001);
                result.Check(hasLinghuo && !hasJianren,
                    "passive.autoLearn20=" + hasLinghuo + "/" + hasJianren);

                // 100 级应把三个被动都学到
                role.Level = 100;
                SkillService.Instance.AutoLearn(role, false);
                result.Check(HasSkillCfg(role, 2700001) && HasSkillCfg(role, 2800001)
                        && HasSkillCfg(role, 2900001),
                    "passive.autoLearn100");
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                role.Skills = oldSkills;
                role.Level = oldLevel;
                role.Partners = oldPartners;
                role.PartnerSlots = oldSlots;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>角色是否已学会指定配置 UUID 的技能。</summary>
        private static bool HasSkillCfg(LocalRole role, int cfgId)
        {
            if (role == null || role.Skills == null)
                return false;
            for (int i = 0; i < role.Skills.Count; i++)
            {
                SkillInfo skill = role.Skills[i];
                if (skill != null && skill.CfgId == cfgId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 商城（/v1/store/list + /v1/store/buy）。
        /// 用试炼商城（StoreType=5，货币 = 试炼积分 item 8）验证：
        /// 列表返回、货币不足、购买成功、限购拦截。
        /// </summary>
        private static CaseResult RunStore()
        {
            var result = new CaseResult("store");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            long oldVoucher = role.TrialVoucher;
            List<long> oldBuys = role.StoreBuyLog;
            try
            {
                role.StoreBuyLog = new List<long>();
                role.TrialVoucher = 0;
                LocalData.Instance.TrialVoucher = 0;
                role.BagData.ItemList = new List<ItemInfo>();

                // 1) 列表：试炼商城 7 个商品
                var list = (HotUpdateScripts.ProtoM.HTTP_StoreListResponse)
                    StoreService.Instance.GetList("{\"store_type\":5}");
                result.Check(list.code == 0, "store.list=" + list.code);
                int count = list.data == null ? 0 : list.data.Length;
                result.Check(count == 7, "store.trialGoods=" + count);

                // 2) 货币不足应被拦
                var poor = (HotUpdateScripts.ProtoM.HTTP_StoreOperateResponse)
                    StoreService.Instance.Buy("{\"goods_uuid\":5001,\"num\":1}");
                result.Check(poor.code != 0, "store.noMoney=" + poor.error);

                // 3) 给够试炼积分后购买成功：500 积分换 item23 x50
                role.TrialVoucher = 500;
                LocalData.Instance.TrialVoucher = 500;
                var ok = (HotUpdateScripts.ProtoM.HTTP_StoreOperateResponse)
                    StoreService.Instance.Buy("{\"goods_uuid\":5001,\"num\":1}");
                result.Check(ok.code == 0, "store.buy=" + ok.code + "/" + ok.error);
                result.Check(role.TrialVoucher == 0,
                    "store.costVoucher=" + role.TrialVoucher);
                long got = ItemService.CountItem(role, 23);
                result.Check(got == 50, "store.gotItem=" + got);

                // 4) 列表里的已购数量应为 1
                var list2 = (HotUpdateScripts.ProtoM.HTTP_StoreListResponse)
                    StoreService.Instance.GetList("{\"store_type\":5}");
                long buyCount = -1;
                if (list2.data != null)
                {
                    for (int i = 0; i < list2.data.Length; i++)
                    {
                        // ILRuntime 读 struct 数组元素的属性会抛 NotImplementedException，
                        // 先拷到局部变量再读。
                        HotUpdateScripts.ProtoM.HTTP_StoreList row = list2.data[i];
                        if (row.goods_uuid == 5001)
                        {
                            buyCount = row.buy;
                            break;
                        }
                    }
                }
                result.Check(buyCount == 1, "store.buyCount=" + buyCount);

                // 5) 限购 20 次：直接塞满购买记录后应被拦
                long nowSec = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                for (int i = 0; i < 20; i++)
                {
                    role.StoreBuyLog.Add(5001L);
                    role.StoreBuyLog.Add(1L);
                    role.StoreBuyLog.Add(nowSec);
                }
                role.TrialVoucher = 100000;
                LocalData.Instance.TrialVoucher = 100000;
                var blocked = (HotUpdateScripts.ProtoM.HTTP_StoreOperateResponse)
                    StoreService.Instance.Buy("{\"goods_uuid\":5001,\"num\":1}");
                result.Check(blocked.code != 0 && blocked.error == "已达购买上限",
                    "store.limitBlocked=" + blocked.error);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                role.TrialVoucher = oldVoucher;
                LocalData.Instance.TrialVoucher = oldVoucher;
                role.StoreBuyLog = oldBuys;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 技能进阶（C2S_SkillAdvanced + UpperlevelSetting 战斗效果）。
        /// 战士月弧斩（SkillID=2）的进阶档位：
        ///   Lv5  CanupperLevel=1  DamagePer=500  材料 金币50万 + item24x100
        ///   Lv10 CanupperLevel=2  DamagePer=1000
        /// 进阶上限 = 当前技能等级所在的 CanupperLevel。
        /// </summary>
        private static CaseResult RunSkillAdvance()
        {
            var result = new CaseResult("skill_advance");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<SkillInfo> oldSkills = role.Skills;
            long oldCoin = role.Coin;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                // ---------- 1) 配置层：进阶档位与效果 ----------
                CFGItem_Skill.UpperlevelSettingItem up0 = CFG_Skill.GetUpperlevelSetting(205, 0);
                result.Check(up0 == null, "advance.cfg0=" + (up0 == null ? "null" : "notNull"));
                CFGItem_Skill.UpperlevelSettingItem up1 = CFG_Skill.GetUpperlevelSetting(205, 1);
                result.Check(up1 != null && up1.DamagePer == 500,
                    "advance.cfg1=" + (up1 == null ? "null" : up1.DamagePer));
                CFGItem_Skill.UpperlevelSettingItem up2 = CFG_Skill.GetUpperlevelSetting(210, 2);
                result.Check(up2 != null && up2.DamagePer == 1000,
                    "advance.cfg2=" + (up2 == null ? "null" : up2.DamagePer));

                // ---------- 2) 进阶材料不足 ----------
                role.Skills = new List<SkillInfo>();
                role.Skills.Add(new SkillInfo { CfgId = 205, Advanced = 0 });
                role.Coin = 0;
                LocalData.Instance.Gold = 0;
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();
                SkillAdvancedResponse poor = SkillService.Instance.Advance(205, 0);
                result.Check(poor != null && poor.Code != 0 && poor.NewLevel == 0,
                    "advance.noMaterial=" + (poor == null ? "null" : poor.NewLevel));

                // ---------- 3) 补足材料后进阶成功 ----------
                role.Coin = 500000;
                LocalData.Instance.Gold = 500000;
                ItemService.Instance.GrantReward(role, 24, 100);
                SkillAdvancedResponse ok = SkillService.Instance.Advance(205, 0);
                result.Check(ok != null && ok.Code == 0 && ok.NewLevel == 1,
                    "advance.ok=" + (ok == null ? "null" : ok.Code + "/" + ok.NewLevel));
                result.Check(role.Skills[0].Advanced == 1,
                    "advance.level=" + role.Skills[0].Advanced);
                result.Check(role.Coin == 0, "advance.coinCost=" + role.Coin);
                result.Check(ItemService.CountItem(role, 24) == 0,
                    "advance.itemCost=" + ItemService.CountItem(role, 24));

                // ---------- 4) 已到上限不可再进阶 ----------
                role.Coin = 5000000;
                ItemService.Instance.GrantReward(role, 24, 1000);
                SkillAdvancedResponse capped = SkillService.Instance.Advance(205, 0);
                result.Check(capped != null && capped.Code != 0 && capped.NewLevel == 1,
                    "advance.cap=" + (capped == null ? "null" : capped.NewLevel));

                // ---------- 5) 战斗侧：进阶后伤害 +5% ----------
                var attacker = new BattleService.BattleUnit
                {
                    Uid = role.Uid,
                    IsPlayer = true,
                    Attack = 1000,
                    Defense = 0,
                    Critical = 0,
                    Toughness = 0,
                    Hit = 0,
                    Dodge = 0,
                    CriticalDamage = 0,
                };
                var target = new BattleService.BattleUnit
                {
                    Uid = 1,
                    IsPlayer = false,
                    Attack = 0,
                    Defense = 100,
                    Hit = 0,
                    Dodge = 0,
                    Critical = 0,
                    Toughness = 0,
                };
                CFGItem_Skill cfg205 = CFG_Skill.GetById(205);
                int dmg0 = BattleService.Instance.ComputeFinalPower(attacker, target, cfg205, 0, false);
                int dmg1 = BattleService.Instance.ComputeFinalPower(attacker, target, cfg205, 1, false);
                result.Check(dmg1 == dmg0 + dmg0 * 5 / 100,
                    "advance.damage=" + dmg0 + "->" + dmg1);

                // ---------- 6) BattleService 能读到技能的进阶等级 ----------
                result.Check(BattleService.GetSkillAdvancedLevel(attacker, 205) == 1,
                    "advance.readLevel=" + BattleService.GetSkillAdvancedLevel(attacker, 205));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                role.Skills = oldSkills;
                role.Coin = oldCoin;
                LocalData.Instance.Gold = oldCoin;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 装备强化（C2S_Enhance）。
        /// 对齐原服 Player.enhance：
        ///   - 强化等级跟着**槽位**走，不跟装备实例走；
        ///   - 上限 = 角色等级 × config.xml equipstrenghthen.rate（当前 rate=1）；
        ///   - 用「当前强化等级」查 equipstrengthen 配置拿消耗；
        ///   - 强化属性 = 配置 Attribute.value × 装备配置品质对应的强化系数。
        /// </summary>
        private static CaseResult RunEquipEnhance()
        {
            var result = new CaseResult("equip_enhance");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            Dictionary<EquipSlot, ItemInfo> oldEquips = role.Equips;
            Dictionary<EquipSlot, int> oldEnhance = role.EquipEnhance;
            long oldCoin = role.Coin;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            int oldLevel = role.Level;
            try
            {
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
                role.EquipEnhance = new Dictionary<EquipSlot, int>();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();
                role.Level = 20;

                // 1) 空槽位应被拦
                role.Coin = 100000;
                LocalData.Instance.Gold = 100000;
                ItemService.Instance.GrantReward(role, 21, 1000);
                EnhanceResponse empty = EquipService.Instance.Enhance(new EnhanceRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(empty != null && empty.Code != 0 && empty.Error == "该部位没有装备",
                    "enhance.noEquip=" + (empty == null ? "null" : empty.Error));

                // 2) 穿上武器（item 101，配置品质 1 → 强化系数 1）
                role.Equips[EquipSlot.Weapon] = MakeTestEquip(101);
                int attackBefore = RoleStatService.Instance.Recalculate(role, true).PhysicalAttack;
                result.Check(role.GetEquipEnhance(EquipSlot.Weapon) == 0,
                    "enhance.start0=" + role.GetEquipEnhance(EquipSlot.Weapon));

                // 3) 材料不足（清空强化石）
                role.BagData.ItemList = new List<ItemInfo>();
                EnhanceResponse poor = EquipService.Instance.Enhance(new EnhanceRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(poor != null && poor.Code != 0 && poor.Error == "强化材料不足",
                    "enhance.noMaterial=" + (poor == null ? "null" : poor.Error));
                result.Check(role.GetEquipEnhance(EquipSlot.Weapon) == 0,
                    "enhance.unchanged=" + role.GetEquipEnhance(EquipSlot.Weapon));

                // 4) 补材料后强化到 1 级（0 级配置消耗 强化石x10 + 金币x1000）
                ItemService.Instance.GrantReward(role, 21, 10);
                role.Coin = 1000;
                LocalData.Instance.Gold = 1000;
                EnhanceResponse ok = EquipService.Instance.Enhance(new EnhanceRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(ok != null && ok.Code == 0 && ok.NewLevel == 1,
                    "enhance.ok=" + (ok == null ? "null" : ok.Code + "/" + ok.NewLevel));
                result.Check(role.GetEquipEnhance(EquipSlot.Weapon) == 1,
                    "enhance.level=" + role.GetEquipEnhance(EquipSlot.Weapon));
                result.Check(ItemService.CountItem(role, 21) == 0,
                    "enhance.stoneCost=" + ItemService.CountItem(role, 21));
                result.Check(role.Coin == 0, "enhance.coinCost=" + role.Coin);

                // 5) 强化属性生效：1 级配置武器槽 物攻 +2 × 系数 1
                int attackAfter = RoleStatService.Instance.Recalculate(role, true).PhysicalAttack;
                result.Check(attackAfter == attackBefore + 2,
                    "enhance.attack=" + attackBefore + "->" + attackAfter);

                // 6) 上限 = 等级 × rate：直接把等级拉到上限再试
                role.EquipEnhance[EquipSlot.Weapon] = role.Level;
                role.Coin = 100000000;
                LocalData.Instance.Gold = 100000000;
                ItemService.Instance.GrantReward(role, 21, 10000);
                EnhanceResponse capped = EquipService.Instance.Enhance(new EnhanceRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(capped != null && capped.Code != 0
                        && capped.Error == "已达最高强化等级",
                    "enhance.cap=" + (capped == null ? "null" : capped.Error));

                // 7) 换装备不重置强化等级（强化跟槽位走）
                role.EquipEnhance[EquipSlot.Weapon] = 3;
                role.Equips[EquipSlot.Weapon] = MakeTestEquip(113);
                result.Check(role.GetEquipEnhance(EquipSlot.Weapon) == 3,
                    "enhance.keepOnSwap=" + role.GetEquipEnhance(EquipSlot.Weapon));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                role.Equips = oldEquips;
                role.EquipEnhance = oldEnhance;
                role.Coin = oldCoin;
                LocalData.Instance.Gold = oldCoin;
                role.Level = oldLevel;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 装备升星（C2S_Stars）。
        /// 对齐原服 Player.stars：
        ///   - 星级跟着**槽位**走；
        ///   - 上限取配置最大星级（Job1 是 0..30，共 31 条）；
        ///   - 成功条件 weight &lt; SuccessRate × (1 + EQUIP_STAR_PERCENTAGE/10000)；
        ///   - **失败不是掉 1 星，而是掉到配置的 FailLevel**；
        ///   - 材料在判定前扣除，失败不退；
        ///   - 成败都重算属性。
        /// </summary>
        private static CaseResult RunEquipStar()
        {
            var result = new CaseResult("equip_star");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            Dictionary<EquipSlot, ItemInfo> oldEquips = role.Equips;
            Dictionary<EquipSlot, int> oldStars = role.EquipStars;
            long oldCoin = role.Coin;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            int oldForced = EquipService.Instance.ForcedStarWeight;
            try
            {
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
                role.EquipStars = new Dictionary<EquipSlot, int>();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                // 1) 空槽位
                EquipService.Instance.ForcedStarWeight = 0;
                StarsResponse empty = EquipService.Instance.Stars(new StarsRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(empty != null && empty.Code != 0 && empty.Error == "该部位没有装备",
                    "star.noEquip=" + (empty == null ? "null" : empty.Error));

                // 2) 穿上武器，材料不足
                role.Equips[EquipSlot.Weapon] = MakeTestEquip(101);
                StarsResponse poor = EquipService.Instance.Stars(new StarsRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(poor != null && poor.Code != 0 && poor.Error == "升星材料不足",
                    "star.noMaterial=" + (poor == null ? "null" : poor.Error));
                result.Check(role.GetEquipStars(EquipSlot.Weapon) == 0,
                    "star.unchanged=" + role.GetEquipStars(EquipSlot.Weapon));

                // 3) 成功率 100% 的 0 星档（weight=0）→ 成功到 1 星
                int attackBefore = RoleStatService.Instance.Recalculate(role, true).PhysicalAttack;
                role.Coin = 10000;
                LocalData.Instance.Gold = 10000;
                ItemService.Instance.GrantReward(role, 22, 5);
                StarsResponse ok = EquipService.Instance.Stars(new StarsRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(ok != null && ok.Code == 0 && ok.NewLevel == 1,
                    "star.ok=" + (ok == null ? "null" : ok.Code + "/" + ok.NewLevel));
                result.Check(role.GetEquipStars(EquipSlot.Weapon) == 1,
                    "star.level=" + role.GetEquipStars(EquipSlot.Weapon));
                result.Check(ItemService.CountItem(role, 22) == 0,
                    "star.stoneCost=" + ItemService.CountItem(role, 22));
                result.Check(role.Coin == 0, "star.coinCost=" + role.Coin);

                // 4) 1 星配置给武器槽物攻 +1%（attribute 13 value 100）
                // 注意：升星属性是**百分比**，当前角色物攻基数只有几十点，
                // 1% 四舍五入后是 0，所以这里断言「百分比属性进到了最终属性表」，
                // 而不是断言面板攻击力变高。
                RoleStatSnapshot afterSnap = RoleStatService.Instance.Recalculate(role, true);
                int attackPercent = afterSnap.Get(pb.Attribute.PHYSICAL_ATTACK_PERCENTAGE);
                result.Check(attackPercent == 100,
                    "star.attackPercent=" + attackPercent);
                result.Check(afterSnap.PhysicalAttack >= attackBefore,
                    "star.attackNotDrop=" + attackBefore + "->" + afterSnap.PhysicalAttack);

                // 5) 失败分支：1 星 → 2 星成功率 80%，强制 weight=9999 → 掉回 FailLevel(0)
                role.Coin = 10000;
                LocalData.Instance.Gold = 10000;
                ItemService.Instance.GrantReward(role, 22, 5);
                EquipService.Instance.ForcedStarWeight = 9999;
                StarsResponse fail = EquipService.Instance.Stars(new StarsRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(fail != null && fail.Code == 0 && fail.NewLevel == 0,
                    "star.failDemote=" + (fail == null ? "null" : fail.NewLevel));
                result.Check(role.GetEquipStars(EquipSlot.Weapon) == 0,
                    "star.demoted=" + role.GetEquipStars(EquipSlot.Weapon));
                result.Check(ItemService.CountItem(role, 22) == 0,
                    "star.failConsumed=" + ItemService.CountItem(role, 22));

                // 6) 上限
                role.EquipStars[EquipSlot.Weapon] = CFG_EquipStar.GetMaxStar(role.Job);
                role.Coin = 100000000;
                LocalData.Instance.Gold = 100000000;
                ItemService.Instance.GrantReward(role, 22, 10000);
                EquipService.Instance.ForcedStarWeight = 0;
                StarsResponse capped = EquipService.Instance.Stars(new StarsRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 0,
                });
                result.Check(capped != null && capped.Code != 0 && capped.Error == "已达最高星级",
                    "star.cap=" + (capped == null ? "null" : capped.Error));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                EquipService.Instance.ForcedStarWeight = oldForced;
                role.Equips = oldEquips;
                role.EquipStars = oldStars;
                role.Coin = oldCoin;
                LocalData.Instance.Gold = oldCoin;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 魂石镶嵌（对齐原服 genstoneChange）。
        ///
        /// 覆盖：无装备 / 槽位未开启 / 部位不符 / 数量不足 / 镶嵌成功 /
        ///       换装保留 / 拆卸回收 / 空槽拆卸 / 生命百分比 / 魂石大师。
        /// </summary>
        private static CaseResult RunGem()
        {
            var result = new CaseResult("gem");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            Dictionary<EquipSlot, ItemInfo> oldEquips = role.Equips;
            Dictionary<EquipSlot, int[]> oldGems = role.EquipGems;
            int oldLevel = role.Level;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
                role.EquipGems = new Dictionary<EquipSlot, int[]>();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                // 1) 空槽位
                GemstoneChangeResponse noEquip = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = (GemstoneSlot)0,
                    ItemUid = 10001,
                    PartnerSlotId = 0,
                });
                result.Check(noEquip != null && noEquip.Code != 0 && noEquip.Error == "该部位没有装备",
                    "gem.noEquip=" + (noEquip == null ? "null" : noEquip.Error));

                role.Equips[EquipSlot.Weapon] = MakeTestEquip(101);
                ItemService.Instance.GrantReward(role, 10001, 3);

                // 2) 第一个槽位 40 级才开
                role.Level = 39;
                GemstoneChangeResponse locked = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = (GemstoneSlot)0,
                    ItemUid = 10001,
                    PartnerSlotId = 0,
                });
                result.Check(locked != null && locked.Code != 0 && locked.Error == "魂石槽位未开启",
                    "gem.locked=" + (locked == null ? "null" : locked.Error));

                // 3) 部位不符：红魂石只能镶武器/戒指/靴子（item.json DiamondPosition = 1,5,6）
                role.Level = 40;
                role.Equips[EquipSlot.Helmet] = MakeTestEquip(101);
                GemstoneChangeResponse wrongSlot = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Helmet,
                    GemstoneSlot = (GemstoneSlot)0,
                    ItemUid = 10001,
                    PartnerSlotId = 0,
                });
                result.Check(wrongSlot != null && wrongSlot.Code != 0
                    && wrongSlot.Error == "该部位不能镶嵌此魂石",
                    "gem.wrongSlot=" + (wrongSlot == null ? "null" : wrongSlot.Error));

                // 4) 数量不足
                GemstoneChangeResponse poor = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = (GemstoneSlot)0,
                    ItemUid = 10005,
                    PartnerSlotId = 0,
                });
                result.Check(poor != null && poor.Code != 0 && poor.Error == "魂石数量不足",
                    "gem.noMaterial=" + (poor == null ? "null" : poor.Error));

                // 5) 镶嵌成功：1 级红魂石 物攻 +10
                int attackBefore = RoleStatService.Instance.Recalculate(role, true).PhysicalAttack;
                GemstoneChangeResponse ok = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = (GemstoneSlot)0,
                    ItemUid = 10001,
                    PartnerSlotId = 0,
                });
                result.Check(ok != null && ok.Code == 0, "gem.ok=" + (ok == null ? "null" : ok.Error));
                int[] weaponGems = role.GetEquipGems(EquipSlot.Weapon);
                result.Check(weaponGems[0] == 10001, "gem.inlaid=" + weaponGems[0]);
                result.Check(ItemService.CountItem(role, 10001) == 2,
                    "gem.cost=" + ItemService.CountItem(role, 10001));
                int attackAfter = RoleStatService.Instance.Recalculate(role, true).PhysicalAttack;
                result.Check(attackAfter == attackBefore + 10,
                    "gem.attack=" + attackBefore + "->" + attackAfter);

                // 6) 换装备后魂石保留（跟槽位走）
                role.Equips[EquipSlot.Weapon] = MakeTestEquip(102);
                result.Check(role.GetEquipGems(EquipSlot.Weapon)[0] == 10001,
                    "gem.keepOnSwap=" + role.GetEquipGems(EquipSlot.Weapon)[0]);

                // 7) 拆卸：回到背包
                GemstoneChangeResponse off = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 2,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = (GemstoneSlot)0,
                    ItemUid = 10001,
                    PartnerSlotId = 0,
                });
                result.Check(off != null && off.Code == 0, "gem.removeCode=" + (off == null ? "null" : off.Error));
                result.Check(role.GetEquipGems(EquipSlot.Weapon)[0] == 0,
                    "gem.cleared=" + role.GetEquipGems(EquipSlot.Weapon)[0]);
                result.Check(ItemService.CountItem(role, 10001) == 3,
                    "gem.refund=" + ItemService.CountItem(role, 10001));

                // 8) 空槽拆卸：原服静默返回成功
                GemstoneChangeResponse empty = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 2,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = (GemstoneSlot)0,
                    ItemUid = 0,
                    PartnerSlotId = 0,
                });
                result.Check(empty != null && empty.Code == 0,
                    "gem.emptyRemove=" + (empty == null ? "null" : empty.Code));

                // 9) 魂石生命百分比：黄魂石 3% 进 gemStatus，按当前总生命加成
                role.Level = 60;   // 4 个槽位全开
                role.Equips[EquipSlot.Necklace] = MakeTestEquip(101);
                ItemService.Instance.GrantReward(role, 10228, 1);
                int hpBefore = RoleStatService.Instance.Recalculate(role, true).MaxHealth;
                GemstoneChangeResponse hpOk = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Necklace,
                    GemstoneSlot = (GemstoneSlot)0,
                    ItemUid = 10228,
                    PartnerSlotId = 0,
                });
                int hpAfter = RoleStatService.Instance.Recalculate(role, true).MaxHealth;
                int hpExpect = hpBefore + (int)System.Math.Round(hpBefore * 300.0 / 10000.0);
                result.Check(hpOk != null && hpOk.Code == 0 && hpAfter == hpExpect,
                    "gem.hpPercent=" + hpBefore + "->" + hpAfter + " expect=" + hpExpect);

                // 10) 魂石大师：10 颗 3 级魂石触发 3003（Limit 3, 属性 攻6 魔攻4 物防4 魔防4）
                role.EquipGems = new Dictionary<EquipSlot, int[]>();
                role.Equips[EquipSlot.LeftRing] = MakeTestEquip(101);
                role.Equips[EquipSlot.RightRing] = MakeTestEquip(101);
                int[] w = role.GetEquipGems(EquipSlot.Weapon);
                for (int i = 0; i < 4; i++) w[i] = 10003;
                int[] l = role.GetEquipGems(EquipSlot.LeftRing);
                for (int i = 0; i < 4; i++) l[i] = 10003;
                int[] r = role.GetEquipGems(EquipSlot.RightRing);
                r[0] = 10003;
                int atk9 = RoleStatService.Instance.Recalculate(role, true).PhysicalAttack;
                r[1] = 10003;   // 第 10 颗
                RoleStatSnapshot masterSnap = RoleStatService.Instance.Recalculate(role, true);
                result.Check(masterSnap.PhysicalAttack == atk9 + 22 + 6,
                    "gem.masterAtk=" + atk9 + "->" + masterSnap.PhysicalAttack);
                result.Check(masterSnap.PhysicalDefense > 0,
                    "gem.masterDef=" + masterSnap.PhysicalDefense);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                role.Equips = oldEquips;
                role.EquipGems = oldGems;
                role.Level = oldLevel;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 配置表字段对账（第 48 节）。
        ///
        /// 起因：Config/CFG_*.cs 是手写解析类，反复出现「json 有字段、类里没定义」，
        /// 表现是运行时悄悄拿到 0 / null（已经踩了三次：EquipStar.FailLevel、
        /// Compose.Produced、Map.MonsterIds.point）。离线脚本在
        /// LocalTestHarness/tools/audit_cfg_fields.py，这里把「关键字段确实能解析出
        /// 数据」固化成断言，防止下次改配置类时再漏。
        ///
        /// 断言只覆盖下一步真要用的三个字段 + 掉落结构：
        ///   Compose.Produced / Map.MonsterIds[].point / Monster.Relive / Monster.Dorp
        /// </summary>
        private static CaseResult RunConfigAudit()
        {
            var result = new CaseResult("config_audit");

            // ① 合成产出（第 49 节 ComposeService 依赖）
            CFGItem_Compose compose = null;
            try { compose = CFG_Compose.GetById(30); }
            catch (Exception e) { result.Fail("CFG_Compose[30] 读取失败: " + e.Message); return result; }

            int producedLen = compose != null && compose.Produced != null ? compose.Produced.Length : -1;
            result.Check(producedLen == 2, "cfg.composeProduced=" + producedLen);
            if (producedLen > 0)
            {
                // struct/类数组元素先拷局部变量再读字段（ILRuntime）
                CFGItem_Compose.ProducedItem first = compose.Produced[0];
                int firstRatio = first != null ? first.ratio : -1;
                int firstId = first != null && first.id != null && first.id.Length > 0 ? first.id[0] : -1;
                result.Check(firstRatio == 9000 && firstId == 10002,
                    "cfg.composeFirst=" + firstRatio + "/" + firstId);
            }

            // ② Raw → ComposeID 反查（原服 whileAll 的等价实现）
            CFGItem_Compose byRaw = null;
            try { byRaw = CFG_Compose.GetByRawId(10016); }
            catch { byRaw = null; }
            int byRawId = byRaw != null ? byRaw.ComposeID : -1;
            result.Check(byRawId == 30, "cfg.composeByRaw=" + byRawId);

            // ③ 地图怪物锚点（第 51 节魂石副本靠它刷怪）
            CFGItem_Map map = null;
            try { map = CFG_Map.GetById(205); }
            catch { map = null; }
            int monsterTotal = 0;
            int pointX = -1;
            int pointY = -1;
            if (map != null && map.MonsterIds != null)
            {
                for (int i = 0; i < map.MonsterIds.Count; i++)
                {
                    CFGItem_Map.CFGItem_MonsterId entry = map.MonsterIds[i];
                    if (entry == null)
                        continue;
                    monsterTotal += entry.num;
                    if (i == 0)
                    {
                        CFGItem_Map.CFGItem_BornPoint p = entry.point;
                        pointX = p.x;
                        pointY = p.y;
                    }
                }
            }
            result.Check(monsterTotal == 10, "cfg.map205Count=" + monsterTotal);
            result.Check(pointX == 25 && pointY == 25, "cfg.map205Point=" + pointX + "," + pointY);

            // ④ 怪物重生时间 + 掉落结构（第 51 节刷怪/掉魂石）
            CFGItem_Monster blueBox = null;
            try { blueBox = CFG_Monster.GetById(607); }
            catch { blueBox = null; }
            int relive = blueBox != null ? (int)blueBox.Relive : -1;
            result.Check(relive == 1000, "cfg.monsterRelive=" + relive);

            int dropLen = blueBox != null && blueBox.Dorp != null ? blueBox.Dorp.Length : -1;
            result.Check(dropLen == 12, "cfg.monsterDrop=" + dropLen);
            if (dropLen > 0)
            {
                CFGItem_Monster.DorpItem drop = blueBox.Dorp[0];
                int dropRatio = drop != null ? drop.ratio : -1;
                int dropId = drop != null && drop.id != null && drop.id.Length > 0 ? drop.id[0] : -1;
                bool hasNum = drop != null && drop.num != null && drop.num.max > 0;
                result.Check(dropRatio == 1900 && dropId == 10001 && hasNum,
                    "cfg.dropItem=" + dropRatio + "/" + dropId + "/" + hasNum);
            }
            return result;
        }

        /// <summary>
        /// 一键镶嵌（对齐原服 genstoneChangeAll）。
        ///
        /// 覆盖：无装备 / 背包空 / 按等级降序填空槽 / 更高级替换且旧宝石回流再参与 / 槽位未开。
        /// 最关键的是第 4 步：它验证「旧宝石退回背包后重新进入候选池、可能被再镶到别的槽位」
        /// 这条链路 —— 这是 genstoneChangeAll 和「循环调单颗镶嵌」的本质区别。
        /// </summary>
        private static CaseResult RunGemAll()
        {
            var result = new CaseResult("gem_all");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            Dictionary<EquipSlot, ItemInfo> oldEquips = role.Equips;
            Dictionary<EquipSlot, int[]> oldGems = role.EquipGems;
            int oldLevel = role.Level;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
                role.EquipGems = new Dictionary<EquipSlot, int[]>();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();
                role.Level = 60;   // 4 个槽位全开

                // 1) 该部位没有装备
                GemstoneChangeAllResponse noEquip = GemService.Instance.ChangeAll(
                    new GemstoneChangeAllRequest { OpType = 1, Slot = EquipSlot.Weapon, PartnerSlotId = 0 });
                result.Check(noEquip != null && noEquip.Code != 0 && noEquip.Error == "该部位没有装备",
                    "gem_all.noEquip=" + (noEquip == null ? "null" : noEquip.Error));

                role.Equips[EquipSlot.Weapon] = MakeTestEquip(101);

                // 2) 背包没有魂石 → 成功但槽位不变
                GemstoneChangeAllResponse emptyBag = GemService.Instance.ChangeAll(
                    new GemstoneChangeAllRequest { OpType = 1, Slot = EquipSlot.Weapon, PartnerSlotId = 0 });
                int[] gems = role.GetEquipGems(EquipSlot.Weapon);
                result.Check(emptyBag != null && emptyBag.Code == 0 && gems[0] == 0,
                    "gem_all.emptyBag=" + (emptyBag == null ? "null" : emptyBag.Code + "/" + gems[0]));

                // 3) 背包 10003×2 + 10001×1 → 按等级降序填前 3 槽
                ItemService.Instance.GrantReward(role, 10003, 2);
                ItemService.Instance.GrantReward(role, 10001, 1);
                GemstoneChangeAllResponse fill = GemService.Instance.ChangeAll(
                    new GemstoneChangeAllRequest { OpType = 1, Slot = EquipSlot.Weapon, PartnerSlotId = 0 });
                result.Check(fill != null && fill.Code == 0,
                    "gem_all.fillCode=" + (fill == null ? "null" : fill.Error));
                result.Check(gems[0] == 10003 && gems[1] == 10003 && gems[2] == 10001 && gems[3] == 0,
                    "gem_all.fillOrder=" + gems[0] + "," + gems[1] + "," + gems[2] + "," + gems[3]);
                result.Check(ItemService.CountItem(role, 10003) == 0 && ItemService.CountItem(role, 10001) == 0,
                    "gem_all.fillCost=" + ItemService.CountItem(role, 10003) + "/"
                    + ItemService.CountItem(role, 10001));
                result.Check(fill != null && fill.GemStoneSlot != null && fill.GemStoneSlot.Length == 4,
                    "gem_all.resultLen=" + (fill == null || fill.GemStoneSlot == null ? -1 : fill.GemStoneSlot.Length));

                // 4) 再给 1 颗 5 级：替换最低槽 → 被换下来的宝石回流并继续参与，最终 4 槽填满
                //    期望 [10005, 10003, 10003, 10001]
                ItemService.Instance.GrantReward(role, 10005, 1);
                GemstoneChangeAllResponse swap = GemService.Instance.ChangeAll(
                    new GemstoneChangeAllRequest { OpType = 1, Slot = EquipSlot.Weapon, PartnerSlotId = 0 });
                result.Check(swap != null && swap.Code == 0,
                    "gem_all.swapCode=" + (swap == null ? "null" : swap.Error));
                result.Check(gems[0] == 10005 && gems[1] == 10003 && gems[2] == 10003 && gems[3] == 10001,
                    "gem_all.swapResult=" + gems[0] + "," + gems[1] + "," + gems[2] + "," + gems[3]);
                result.Check(ItemService.CountItem(role, 10005) == 0
                    && ItemService.CountItem(role, 10003) == 0
                    && ItemService.CountItem(role, 10001) == 0,
                    "gem_all.swapCost=" + ItemService.CountItem(role, 10003));

                // 5) 槽位未开：40 级只开第 1 个槽
                role.Level = 40;
                role.EquipGems = new Dictionary<EquipSlot, int[]>();
                role.BagData.ItemList = new List<ItemInfo>();
                ItemService.Instance.GrantReward(role, 10001, 2);
                int[] lockedGems = role.GetEquipGems(EquipSlot.Weapon);
                GemstoneChangeAllResponse locked = GemService.Instance.ChangeAll(
                    new GemstoneChangeAllRequest { OpType = 1, Slot = EquipSlot.Weapon, PartnerSlotId = 0 });
                result.Check(locked != null && locked.Code == 0 && lockedGems[0] == 10001 && lockedGems[1] == 0,
                    "gem_all.slotLock=" + lockedGems[0] + "," + lockedGems[1]);
                result.Check(ItemService.CountItem(role, 10001) == 1,
                    "gem_all.slotLockLeft=" + ItemService.CountItem(role, 10001));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                role.Equips = oldEquips;
                role.EquipGems = oldGems;
                role.Level = oldLevel;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 每日副本（第 52 节）：rooms 7..11 复用第 51 节的「无 battle 配置房间」平台。
        ///
        /// 覆盖：金币副本（单 boss + 成长倍率 + 评分档位 + 超时结算公式）/
        ///       强化石副本（波次制 + 结算公式）/ 各房间次数互相独立。
        /// 结算走「时间到」路径，这样不依赖把 100 万血的 boss 打死，跑得快也稳定。
        /// </summary>
        private static CaseResult RunDailyMap()
        {
            var result = new CaseResult("daily_map");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            int oldLevel = role.Level;
            int oldResetDay = role.DailyMapResetDay;
            Dictionary<int, int> oldTimes = role.DailyMapTimes;
            long oldCoin = role.Coin;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                BattleService.Instance.Exit();
                role.Level = 45;
                role.DailyMapResetDay = 0;
                role.DailyMapTimes = new Dictionary<int, int>();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();
                role.Coin = 0;
                LocalData.Instance.Gold = 0;

                int goldRoom = (int)CFG_Room.RoomType.Gold;
                int qhsRoom = (int)CFG_Room.RoomType.QHS;

                // 1) 金币副本：单只 boss，怪物属性按 accommodation 成长
                JoinDeskResponse goldEnter = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 201,
                    BattleId = 0,
                });
                result.Check(goldEnter != null && goldEnter.Code == 0,
                    "daily.goldEnter=" + (goldEnter == null ? "null" : goldEnter.Code + "/" + goldEnter.Error));
                if (goldEnter != null && goldEnter.Code == 0 && goldEnter.TableInfo != null)
                {
                    int goldMonsters = goldEnter.TableInfo.MonsterList == null
                        ? 0 : goldEnter.TableInfo.MonsterList.Count;
                    result.Check(goldMonsters == 1, "daily.goldMonsters=" + goldMonsters);

                    // 成长倍率 = INT(45/30)+1 = 2 → boss 血量翻倍
                    long bossHp = goldMonsters > 0 ? goldEnter.TableInfo.MonsterList[0].MaxHp : 0;
                    result.Check(bossHp == 2000000, "daily.goldBossHp=" + bossHp);
                    result.Check(BattleService.Instance.GetDailyMapTimes(role, goldRoom) == 0,
                        "daily.goldTimes=" + BattleService.Instance.GetDailyMapTimes(role, goldRoom));

                    // 2) 时间到结算：50000 × (1 + 0/10000) × 45 = 2,250,000 金币
                    BattleService.Instance.Tick(
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 200000);
                    result.Check(role.Coin == 2250000, "daily.goldReward=" + role.Coin);
                    result.Check(!BattleService.Instance.InBattle,
                        "daily.goldSettled=" + !BattleService.Instance.InBattle);
                }

                // 3) 强化石副本：10 只小怪，波次制；时间到按 1 波结算
                //    10 × 1 × 45 = 450 个强化石（item 21）
                JoinDeskResponse qhsEnter = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 202,
                    BattleId = 0,
                });
                result.Check(qhsEnter != null && qhsEnter.Code == 0,
                    "daily.qhsEnter=" + (qhsEnter == null ? "null" : qhsEnter.Code + "/" + qhsEnter.Error));
                if (qhsEnter != null && qhsEnter.Code == 0 && qhsEnter.TableInfo != null)
                {
                    int qhsMonsters = qhsEnter.TableInfo.MonsterList == null
                        ? 0 : qhsEnter.TableInfo.MonsterList.Count;
                    result.Check(qhsMonsters == 10, "daily.qhsMonsters=" + qhsMonsters);
                    // 金币副本用掉次数不影响强化石副本
                    result.Check(BattleService.Instance.GetDailyMapTimes(role, qhsRoom) == 0,
                        "daily.qhsTimes=" + BattleService.Instance.GetDailyMapTimes(role, qhsRoom));

                    BattleService.Instance.Tick(
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 200000);
                    result.Check(ItemService.CountItem(role, 21) == 450,
                        "daily.qhsReward=" + ItemService.CountItem(role, 21));
                }
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.Level = oldLevel;
                role.DailyMapTimes = oldTimes;
                role.DailyMapResetDay = oldResetDay;
                role.Coin = oldCoin;
                LocalData.Instance.Gold = oldCoin;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 守卫海岛（第 53 节，RoomType 1 / PVE_PeacefulMap）。
        ///
        /// 覆盖：等级门槛 / 进入（map 1：30 只树妖 + 5 只精英狼）/ 每日挂机时长作为本场时限 /
        ///       击杀精英狼必掉金币 / 退出时按停留时长扣每日挂机时间 / 时长用完拒绝进入。
        /// </summary>
        private static CaseResult RunGuardIsland()
        {
            var result = new CaseResult("guard_island");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            int oldLevel = role.Level;
            int oldAfkSec = role.AfkDailySec;
            int oldResetDay = role.AfkResetDay;
            long oldMultiplier = role.AfkMultiplierID;
            long oldCoin = role.Coin;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                BattleService.Instance.Exit();
                int today = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
                role.AfkResetDay = today;      // 不触发跨天重置，便于精确断言
                role.AfkDailySec = 0;
                role.AfkMultiplierID = 0;      // 免费档
                role.Coin = 0;
                LocalData.Instance.Gold = 0;
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                // 1) 等级不足（room 1 要求 50-60）
                role.Level = 45;
                JoinDeskResponse lowLevel = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 1,
                    BattleId = 0,
                });
                result.Check(lowLevel != null && lowLevel.Code != 0 && lowLevel.Error == "角色等级不足",
                    "afk.lowLevel=" + (lowLevel == null ? "null" : lowLevel.Error));

                // 2) 正常进入：怪物 35 只（801×30 + 851×5），时限 = 每日挂机时长 3600 秒
                role.Level = 55;
                JoinDeskResponse enter = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 1,
                    BattleId = 0,
                });
                result.Check(enter != null && enter.Code == 0,
                    "afk.enter=" + (enter == null ? "null" : enter.Code + "/" + enter.Error));
                if (enter == null || enter.Code != 0 || enter.TableInfo == null)
                    return result;

                int monsters = enter.TableInfo.MonsterList == null
                    ? 0 : enter.TableInfo.MonsterList.Count;
                result.Check(monsters == 35, "afk.monsters=" + monsters);
                result.Check(enter.LeftTime == BattleService.GetAfkDailyLimit(),
                    "afk.leftTime=" + enter.LeftTime + "/" + BattleService.GetAfkDailyLimit());

                // 3) 攻击链路：树妖 801 物防 97，测试角色攻击力可能压不过防御（伤害被夹到 1），
                //    所以这里只断言「能对怪物出手」（Attack 返回 Code=0）。
                //    掉落本身在 51 节 gem_dungeon 用 100 血的宝箱验证过，走的是同一段 OnMonsterKilled。
                long clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                MonsterInfo target = null;
                for (int i = 0; i < enter.TableInfo.MonsterList.Count; i++)
                {
                    if (enter.TableInfo.MonsterList[i].CfgId == 801)
                    {
                        target = enter.TableInfo.MonsterList[i];
                        break;
                    }
                }
                result.Check(target != null, "afk.targetFound=" + (target != null));
                if (target != null)
                {
                    BattleService.Instance.Move(new MoveRequest
                    {
                        Location = new Point { X = target.Location.X, Y = target.Location.Y },
                    });
                    AttackResponse attack = BattleService.Instance.Attack(new AttackRequest
                    {
                        SkillId = 100,
                        TargetUids = new long[] { target.Uid },
                        Direction = Direction.RIGHT,
                        Location = target.Location,
                    });
                    result.Check(attack != null && attack.Code == 0,
                        "afk.attack=" + (attack == null ? "null" : attack.Code + "/" + attack.Error));
                    clock += 1200;
                    BattleService.Instance.Tick(clock);
                }

                // 4) 退出：按停留时长扣每日挂机时间
                clock += 30000;                       // 模拟再挂 30 秒
                BattleService.Instance.Tick(clock);
                int usedBeforeExit = role.AfkDailySec;
                BattleService.Instance.Exit();
                result.Check(role.AfkDailySec > usedBeforeExit,
                    "afk.timeCost=" + usedBeforeExit + "->" + role.AfkDailySec);

                // 5) 时长用完 → 拒绝进入
                role.AfkDailySec = BattleService.GetAfkDailyLimit();
                JoinDeskResponse noTime = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 1,
                    BattleId = 0,
                });
                result.Check(noTime != null && noTime.Code != 0
                    && noTime.Error == "今日挂机时间已用完",
                    "afk.noTime=" + (noTime == null ? "null" : noTime.Error));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.Level = oldLevel;
                role.AfkDailySec = oldAfkSec;
                role.AfkResetDay = oldResetDay;
                role.AfkMultiplierID = oldMultiplier;
                role.Coin = oldCoin;
                LocalData.Instance.Gold = oldCoin;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 遗迹（第 54 节，RoomType 17）：battle.json 有 200 条 Copytype=17 的完整关卡，
        /// 属「有 battle 配置」的战役型房间，比每日副本简单。
        ///
        /// 覆盖：首关推导 / 跳关拦截 / 打通 1701001（7 只怪）/ 进度推进 /
        ///       成功通关扣次数 / 星级评价入档 / 重打已通关关卡合法 / 次数耗尽拒绝。
        /// </summary>
        private static CaseResult RunRelic()
        {
            var result = new CaseResult("relic");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            int oldRelicId = role.RelicId;
            int oldTimes = role.RelicChallengeTime;
            int oldResetDay = role.RelicResetDay;
            List<KeyValue> oldEval = role.RelicEvaluation;
            int oldLevel = role.Level;
            Dictionary<EquipSlot, ItemInfo> oldEquips = role.Equips;
            try
            {
                BattleService.Instance.Exit();
                // 测试角色基础攻击只有 51（战力全靠装备），所以给一把 55 级武器 + 高等级撑血量，
                // 否则 90 秒内打不完 7 只怪（遗迹首关总血约 3300）
                role.Level = 150;
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
                role.Equips[EquipSlot.Weapon] = MakeTestEquip(1889);
                role.RelicId = 0;
                role.RelicResetDay = 0;               // 强制日重置，次数按 Copyset 17 补满
                role.RelicChallengeTime = 0;
                role.RelicEvaluation = new List<KeyValue>();

                // 1) 首关推导
                int first = BattleService.GetNextRelicId(role);
                result.Check(first == 1701001, "relic.first=" + first);
                int copyTimes = BattleService.GetCopySetTimes((int)CFG_Room.RoomType.Relic);
                result.Check(copyTimes == 10, "relic.cfgTimes=" + copyTimes);

                // 2) 跳关拦截（进度 0 只能打 1701001）
                JoinDeskResponse skip = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 900,
                    BattleId = 1701002,
                });
                result.Check(skip != null && skip.Code != 0 && skip.Error == "遗迹关卡不合法",
                    "relic.skipBlocked=" + (skip == null ? "null" : skip.Error));

                // 3) 打通首关（7 只怪：5×196 血 + 2×1148 血）
                JoinDeskResponse enter = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 900,
                    BattleId = 1701001,
                });
                result.Check(enter != null && enter.Code == 0,
                    "relic.enter=" + (enter == null ? "null" : enter.Code + "/" + enter.Error));
                if (enter == null || enter.Code != 0 || enter.TableInfo == null)
                    return result;

                int monsters = enter.TableInfo.MonsterList == null
                    ? 0 : enter.TableInfo.MonsterList.Count;
                result.Check(monsters == 7, "relic.monsters=" + monsters);

                // 打法：怪物会追着玩家跑，所以每轮都要「追到最近的活怪 → 贴脸攻击」，
                // 拿进入时的静态坐标会一直落空。整轮卡在 80 秒内（关卡 Timelimit=90）。
                long clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                long deadline = clock + 80000;
                for (int tick = 0; tick < 400 && BattleService.Instance.InBattle
                    && clock < deadline; tick++)
                {
                    if (BattleService.Instance.AliveMonsterCount == 0)
                        break;

                    long uid;
                    double mx;
                    double my;
                    if (BattleService.Instance.TryGetNearestAliveMonster(out uid, out mx, out my))
                    {
                        BattleService.Instance.Move(new MoveRequest
                        {
                            Location = new Point { X = mx, Y = my },
                        });
                        BattleService.Instance.Attack(new AttackRequest
                        {
                            SkillId = 100,
                            TargetUids = new long[] { uid },
                            Direction = Direction.RIGHT,
                            Location = new Point { X = mx, Y = my },
                        });
                    }
                    clock += 300;
                    BattleService.Instance.Tick(clock);
                }
                result.Check(BattleService.Instance.InBattle
                    && BattleService.Instance.AliveMonsterCount == 0,
                    "relic.cleared=" + BattleService.Instance.AliveMonsterCount
                    + "/inBattle=" + BattleService.Instance.InBattle);

                // 4) 等结算延迟后推进，触发 Settle
                for (int i = 0; i < 10 && BattleService.Instance.InBattle; i++)
                {
                    clock += 1000;
                    BattleService.Instance.Tick(clock);
                }
                result.Check(!BattleService.Instance.InBattle, "relic.settled=" + !BattleService.Instance.InBattle);
                result.Check(role.RelicId == 1701001, "relic.progress=" + role.RelicId);
                result.Check(role.RelicChallengeTime == copyTimes - 1,
                    "relic.times=" + role.RelicChallengeTime + "/" + copyTimes);

                int star = 0;
                for (int i = 0; i < role.RelicEvaluation.Count; i++)
                {
                    if (role.RelicEvaluation[i] != null && role.RelicEvaluation[i].K == 1701001)
                        star = role.RelicEvaluation[i].V;
                }
                result.Check(star >= 1 && star <= 3, "relic.star=" + star);

                // 5) 下一关推导
                int next = BattleService.GetNextRelicId(role);
                result.Check(next == 1701002, "relic.next=" + next);

                // 6) 重打已通关关卡合法（原服用 > 判断，不拦历史关）
                JoinDeskResponse replay = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 900,
                    BattleId = 1701001,
                });
                result.Check(replay != null && replay.Code == 0,
                    "relic.replay=" + (replay == null ? "null" : replay.Code + "/" + replay.Error));
                BattleService.Instance.Exit();

                // 7) 次数耗尽
                role.RelicResetDay = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
                role.RelicChallengeTime = 0;
                JoinDeskResponse noTimes = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 900,
                    BattleId = 1701002,
                });
                result.Check(noTimes != null && noTimes.Code != 0
                    && noTimes.Error == "挑战次数不足",
                    "relic.noTimes=" + (noTimes == null ? "null" : noTimes.Error));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.RelicId = oldRelicId;
                role.RelicChallengeTime = oldTimes;
                role.RelicResetDay = oldResetDay;
                role.RelicEvaluation = oldEval;
                role.Level = oldLevel;
                role.Equips = oldEquips;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 遗迹章节星级宝箱（① 2026-10-28）：10/20/30 星三档，按「本章各关星级之和」解锁，
        /// 只能往前领、不重复发；同时校验 InstanceInfo 真的把 Evaluation / ChapterReward 下发。
        /// 第 1 章配置：10 关，StarAward = 10档(点券100+强化石3000) / 20档(150+3000) / 30档(200+3000)。
        /// </summary>
        private static CaseResult RunRelicReward()
        {
            var result = new CaseResult("relic_reward");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<KeyValue> oldEval = role.RelicEvaluation;
            List<KeyValue> oldChapterReward = role.RelicChapterReward;
            try
            {
                BattleService.Instance.Exit();
                role.RelicEvaluation = new List<KeyValue>();
                role.RelicChapterReward = new List<KeyValue>();

                Action<int, int> addEval = (battleId, star) =>
                    role.RelicEvaluation.Add(new KeyValue { K = battleId, V = star });
                Func<int, int> chapterGot = chapter =>
                {
                    for (int i = 0; i < role.RelicChapterReward.Count; i++)
                    {
                        if (role.RelicChapterReward[i] != null && role.RelicChapterReward[i].K == chapter)
                            return role.RelicChapterReward[i].V;
                    }
                    return 0;
                };

                // 3 关满星 = 9 星，还差 1 星 → 不能领
                addEval(1701001, 3);
                addEval(1701002, 3);
                addEval(1701003, 3);
                CommonResponse need10 = BattleService.Instance.ClaimChapterReward(
                    new BattleRewardRequest { MType = MapType.RELIC_MAP, ChapterId = 1 });
                result.Check(need10 != null && need10.Code != 0,
                    "rr.need10=" + (need10 == null ? "null" : need10.Code + "/" + need10.Error));

                // 12 星 → 领到 10 星档：点券 +100、强化石 +3000
                addEval(1701004, 3);
                long couponBefore = role.Coupon;
                long stoneBefore = ItemService.CountItem(role, 21);
                CommonResponse first = BattleService.Instance.ClaimChapterReward(
                    new BattleRewardRequest { MType = MapType.RELIC_MAP, ChapterId = 1 });
                result.Check(first != null && first.Code == 0,
                    "rr.first=" + (first == null ? "null" : first.Code + "/" + first.Error));
                result.Check(role.Coupon == couponBefore + 100,
                    "rr.firstCoupon=" + (role.Coupon - couponBefore));
                result.Check(ItemService.CountItem(role, 21) == stoneBefore + 3000,
                    "rr.firstStone=" + (ItemService.CountItem(role, 21) - stoneBefore));
                result.Check(chapterGot(1) == 10, "rr.tier=" + chapterGot(1));

                // 重复领取被拒（已领档位 >= 本章星数区间外的任何请求都不给）
                CommonResponse again = BattleService.Instance.ClaimChapterReward(
                    new BattleRewardRequest { MType = MapType.RELIC_MAP, ChapterId = 1 });
                result.Check(again != null && again.Code != 0,
                    "rr.again=" + (again == null ? "null" : again.Code));

                // 24 星 → 只补领 20 星档（+150），10 星档不重复发
                addEval(1701005, 3);
                addEval(1701006, 3);
                addEval(1701007, 3);
                addEval(1701008, 3);
                couponBefore = role.Coupon;
                stoneBefore = ItemService.CountItem(role, 21);
                CommonResponse second = BattleService.Instance.ClaimChapterReward(
                    new BattleRewardRequest { MType = MapType.RELIC_MAP, ChapterId = 1 });
                result.Check(second != null && second.Code == 0,
                    "rr.second=" + (second == null ? "null" : second.Code + "/" + second.Error));
                result.Check(role.Coupon == couponBefore + 150,
                    "rr.secondCoupon=" + (role.Coupon - couponBefore));
                result.Check(ItemService.CountItem(role, 21) == stoneBefore + 3000,
                    "rr.secondStone=" + (ItemService.CountItem(role, 21) - stoneBefore));
                result.Check(chapterGot(1) == 20, "rr.tier2=" + chapterGot(1));

                // 其他章节不受影响
                CommonResponse otherChapter = BattleService.Instance.ClaimChapterReward(
                    new BattleRewardRequest { MType = MapType.RELIC_MAP, ChapterId = 2 });
                result.Check(otherChapter != null && otherChapter.Code != 0,
                    "rr.chapter2=" + (otherChapter == null ? "null" : otherChapter.Code));

                // 下发校验：客户端 UIFightChoice 的星星/进度条/宝箱状态全靠这两个列表
                var info = PlayerService.Instance.GetInstanceInfo() as InstanceInfoResponse;
                int evalCount = info == null || info.relic_map == null ? -1 : info.relic_map.Evaluation.Count;
                int rewardCount = info == null || info.relic_map == null ? -1 : info.relic_map.ChapterReward.Count;
                result.Check(evalCount == 8, "rr.pushEval=" + evalCount);
                result.Check(rewardCount == 1, "rr.pushChapterReward=" + rewardCount);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.RelicEvaluation = oldEval;
                role.RelicChapterReward = oldChapterReward;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>统计背包里的魂石总数（cfgId 10001..10300）。</summary>
        private static int CountGemItems(LocalRole role)
        {
            if (role == null || role.BagData == null || role.BagData.ItemList == null)
                return 0;
            int total = 0;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item == null)
                    continue;
                if (item.CfgId >= 10001 && item.CfgId <= 10300)
                    total += (int)item.Num;
            }
            return total;
        }

        /// <summary>
        /// 魂石副本（第 51 节）：战斗域第一次支持「没有 battle 配置的房间」。
        ///
        /// 覆盖：进入（room 205 无 battle 配置，怪物来自 map.json）/ 每日次数进入即扣 /
        ///       击杀掉落魂石进背包 / 死亡按 Relive 毫秒原地重生 / 次数为 0 拒绝进入。
        /// </summary>
        private static CaseResult RunGemDungeon()
        {
            var result = new CaseResult("gem_dungeon");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            int oldLevel = role.Level;
            int oldResetDay = role.DailyMapResetDay;
            Dictionary<int, int> oldTimes = role.DailyMapTimes;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                BattleService.Instance.Exit();

                role.Level = 45;           // 魂石副本 MinEntryLimit = 40
                role.DailyMapResetDay = 0;              // 强制触发一次日重置
                role.DailyMapTimes = new Dictionary<int, int>();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                // 1) 进入：次数先补满到 5，再扣 1
                JoinDeskResponse enter = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 205,
                    BattleId = 0,
                });
                result.Check(enter != null && enter.Code == 0,
                    "gem_room.enter=" + (enter == null ? "null" : enter.Code + "/" + enter.Error));
                if (enter == null || enter.Code != 0 || enter.TableInfo == null)
                    return result;

                CFG_XMLConfig.XML_Config.XML_DailyMap hsCfg =
                    BattleService.GetDailyMapConfig((int)CFG_Room.RoomType.HS);
                int expectTimes = (hsCfg != null && hsCfg.challengeTimeNum > 0
                    ? hsCfg.challengeTimeNum : 1) - 1;
                int timesLeft = BattleService.Instance.GetDailyMapTimes(
                    role, (int)CFG_Room.RoomType.HS);
                result.Check(timesLeft == expectTimes, "gem_room.times=" + timesLeft);
                int monsterCount = enter.TableInfo.MonsterList == null
                    ? 0 : enter.TableInfo.MonsterList.Count;
                result.Check(monsterCount == 10, "gem_room.monsters=" + monsterCount);

                // 2) 打怪：逐只击杀，直到背包里出现魂石（掉落是概率的，多打几只保证稳定）
                long clock = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                int killedIndex = -1;
                for (int i = 0; i < enter.TableInfo.MonsterList.Count; i++)
                {
                    if (CountGemItems(role) > 0)
                        break;
                    MonsterInfo target = enter.TableInfo.MonsterList[i];
                    BattleService.Instance.Move(new MoveRequest
                    {
                        Location = new Point { X = target.Location.X, Y = target.Location.Y },
                    });
                    for (int hit = 0; hit < 40; hit++)
                    {
                        if (BattleService.Instance.AliveMonsterCount < monsterCount)
                            break;
                        AttackResponse attack = BattleService.Instance.Attack(new AttackRequest
                        {
                            SkillId = 100,
                            TargetUids = new long[] { target.Uid },
                            Direction = Direction.RIGHT,
                            Location = target.Location,
                        });
                        if (attack == null || attack.Code != 0)
                            break;
                        clock += 700;
                        BattleService.Instance.Tick(clock);
                    }
                    killedIndex = i;
                }
                int aliveAfterKill = BattleService.Instance.AliveMonsterCount;
                result.Check(aliveAfterKill < monsterCount, "gem_room.killed=" + aliveAfterKill + "/" + monsterCount);
                // 刷怪房不会因为"清光"而结束（怪会重生）
                result.Check(BattleService.Instance.InBattle, "gem_room.stillInBattle=" + BattleService.Instance.InBattle);

                int gemDropped = CountGemItems(role);
                result.Check(gemDropped > 0, "gem_room.drop=" + gemDropped);

                // 3) 重生：不同宝箱的 Relive 不同（蓝 1000ms / 紫 10000ms / 橙 30000ms），
                //    伙伴会自己打怪所以死的不一定是哪几只，这里只断言「有怪复活」。
                clock += 1500;
                BattleService.Instance.Tick(clock);
                int aliveAfterRevive = BattleService.Instance.AliveMonsterCount;
                result.Check(aliveAfterRevive > aliveAfterKill,
                    "gem_room.revive=" + aliveAfterKill + "->" + aliveAfterRevive);

                // 4) 次数用尽后拒绝进入
                BattleService.Instance.Exit();
                role.DailyMapResetDay = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
                role.DailyMapTimes[(int)CFG_Room.RoomType.HS] = 0;
                JoinDeskResponse noTimes = BattleService.Instance.Enter(new JoinDeskRequest
                {
                    RoomId = 205,
                    BattleId = 0,
                });
                result.Check(noTimes != null && noTimes.Code != 0 && noTimes.Error == "挑战次数不足",
                    "gem_room.noTimes=" + (noTimes == null ? "null" : noTimes.Error));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.Level = oldLevel;
                role.DailyMapTimes = oldTimes;
                role.DailyMapResetDay = oldResetDay;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>从背包里按配置 id 找第一个实例的 Uid（合成测试用）。</summary>
        private static long FindBagUid(LocalRole role, int cfgId)
        {
            if (role == null || role.BagData == null || role.BagData.ItemList == null)
                return 0;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item != null && item.CfgId == cfgId)
                    return item.Uid;
            }
            return 0;
        }

        /// <summary>
        /// 合成系统（对齐原服 Player.compose 的两条分支）。
        ///
        /// 覆盖：空请求 / 数量非整数倍 / 金币不足 / 魂石合成成功 / 必败不退材料 /
        ///       不同自然系混合同一档 / 装备合成（按 Uid 分支，金币只扣一份）/ 材料不存在。
        /// </summary>
        private static CaseResult RunCompose()
        {
            var result = new CaseResult("compose");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            long oldCoin = role.Coin;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            int oldForced = ComposeService.Instance.ForcedComposeWeight;
            try
            {
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();
                role.Coin = 0;
                LocalData.Instance.Gold = 0;
                ComposeService.Instance.ForcedComposeWeight = 0;

                // 1) 空请求
                ComposeResponse empty = ComposeService.Instance.Compose(new ComposeRequest());
                result.Check(empty != null && empty.Code != 0 && empty.Error == "合成材料为空",
                    "compose.empty=" + (empty == null ? "null" : empty.Error));

                // 2) 数量不是 NeedNum(3) 的整数倍
                ItemService.Instance.GrantReward(role, 10001, 2);
                long gemUid = FindBagUid(role, 10001);
                role.Coin = 100000;
                LocalData.Instance.Gold = 100000;
                ComposeResponse notMultiple = ComposeService.Instance.Compose(new ComposeRequest
                {
                    Info = new List<ComposeInfo> { new ComposeInfo { Uid = gemUid, Num = 2 } },
                });
                result.Check(notMultiple != null && notMultiple.Code != 0
                    && notMultiple.Error == "道具数量不足",
                    "compose.notMultiple=" + (notMultiple == null ? "null" : notMultiple.Error));
                result.Check(ItemService.CountItem(role, 10001) == 2,
                    "compose.notMultipleKeep=" + ItemService.CountItem(role, 10001));

                // 3) 金币不足（材料够也要拦，且不能扣材料）
                ItemService.Instance.GrantReward(role, 10001, 1);
                role.Coin = 0;
                LocalData.Instance.Gold = 0;
                ComposeResponse noGold = ComposeService.Instance.Compose(new ComposeRequest
                {
                    Info = new List<ComposeInfo> { new ComposeInfo { Uid = gemUid, Num = 3 } },
                });
                result.Check(noGold != null && noGold.Code != 0 && noGold.Error == "道具数量不足",
                    "compose.noGold=" + (noGold == null ? "null" : noGold.Error));
                result.Check(ItemService.CountItem(role, 10001) == 3,
                    "compose.noGoldKeep=" + ItemService.CountItem(role, 10001));

                // 4) 魂石合成成功：3 颗 1 级红魂石 → 1 颗 2 级红魂石（9000 段唯一 id）
                role.Coin = 10000;
                LocalData.Instance.Gold = 10000;
                ComposeResponse ok = ComposeService.Instance.Compose(new ComposeRequest
                {
                    Info = new List<ComposeInfo> { new ComposeInfo { Uid = gemUid, Num = 3 } },
                });
                result.Check(ok != null && ok.Code == 0, "compose.ok=" + (ok == null ? "null" : ok.Error));
                // ⚠️ 产物 id 是随机的：CFG_Compose.Produced 里 5 种自然系（红/风/水/火/土）
                //    的 2 级魂石都在同一个 id 列表里，RollProduced 会 _rng.Next(len) 取一个。
                //    所以这里**不能写死 10002**（曾因此偶发假红：拿到了 10017「2级红魂石(风)」）。
                //    改为校验「产出属于 2 级魂石这一档且数量为 1」。
                int productId = ok == null || ok.NewConpose.Count == 0 ? 0 : (int)ok.NewConpose[0].Uid;
                result.Check(ok != null && ok.NewConpose.Count == 1 && ok.NewConpose[0].Num == 1,
                    "compose.produced=" + (productId <= 0
                        ? "none" : productId + "x" + ok.NewConpose[0].Num));
                bool productInTier = false;
                try
                {
                    CFGItem_Item productCfg = CFG_Item.GetById(productId);
                    productInTier = productCfg != null
                        && EquipRuleService.ToInt(productCfg.Type) == 6
                        && productCfg.itemlevel == 2;
                }
                catch { productInTier = false; }
                result.Check(productInTier, "compose.producedTier=" + productId);
                result.Check(ItemService.CountItem(role, 10001) == 0,
                    "compose.materialCost=" + ItemService.CountItem(role, 10001));
                result.Check(productId > 0 && ItemService.CountItem(role, productId) == 1,
                    "compose.productGot=" + ItemService.CountItem(role, productId));
                result.Check(role.Coin == 0, "compose.goldCost=" + role.Coin);

                // 5) 装备合成失败分支：装备 SuccessRate=9000，强制 weight=9999 → 必败。
                //    原服是「先扣金币材料，再判定」，所以失败后两者都不退。
                //    （魂石那档 SuccessRate=10000，weight 最大 9999，天然不可能失败）
                role.BagData.ItemList = new List<ItemInfo>();
                role.Coin = 20000;
                LocalData.Instance.Gold = 20000;
                var failInfos = new List<ComposeInfo>();
                for (int i = 0; i < 6; i++)
                {
                    ItemService.Instance.GrantReward(role, 141 + i, 1);
                    failInfos.Add(new ComposeInfo { Uid = FindBagUid(role, 141 + i), Num = 1 });
                }
                ComposeService.Instance.ForcedComposeWeight = 9999;
                ComposeResponse fail = ComposeService.Instance.Compose(new ComposeRequest { Info = failInfos });
                result.Check(fail != null && fail.Code == -2,
                    "compose.failCode=" + (fail == null ? "null" : fail.Code));
                result.Check(fail != null && fail.NewConpose.Count == 0,
                    "compose.failProduced=" + (fail == null ? -1 : fail.NewConpose.Count));
                int failLeft = 0;
                for (int i = 0; i < 6; i++)
                    failLeft += (int)ItemService.CountItem(role, 141 + i);
                result.Check(failLeft == 0, "compose.failMaterialGone=" + failLeft);
                result.Check(role.Coin == 0, "compose.failGoldGone=" + role.Coin);

                // 6) 不同自然系混着合算同一档（Raw 里 5 种自然系都映射到 ComposeID 30）
                ComposeService.Instance.ForcedComposeWeight = 0;
                role.Coin = 10000;
                LocalData.Instance.Gold = 10000;
                ItemService.Instance.GrantReward(role, 10001, 1);
                ItemService.Instance.GrantReward(role, 10016, 2);
                long mixedA = FindBagUid(role, 10001);
                long mixedB = FindBagUid(role, 10016);
                ComposeResponse mixed = ComposeService.Instance.Compose(new ComposeRequest
                {
                    Info = new List<ComposeInfo>
                    {
                        new ComposeInfo { Uid = mixedA, Num = 1 },
                        new ComposeInfo { Uid = mixedB, Num = 2 },
                    },
                });
                result.Check(mixed != null && mixed.Code == 0,
                    "compose.mixedNature=" + (mixed == null ? "null" : mixed.Error));

                // 7) 装备合成（按 Uid 分支）：6 件 → 2 件产出，金币**只扣一份**
                role.BagData.ItemList = new List<ItemInfo>();
                role.Coin = 20000;
                LocalData.Instance.Gold = 20000;
                var equipInfos = new List<ComposeInfo>();
                for (int i = 0; i < 6; i++)
                {
                    ItemService.Instance.GrantReward(role, 141 + i, 1);
                    equipInfos.Add(new ComposeInfo { Uid = FindBagUid(role, 141 + i), Num = 1 });
                }
                ComposeResponse equip = ComposeService.Instance.Compose(new ComposeRequest
                {
                    Info = equipInfos,
                });
                int producedTotal = 0;
                if (equip != null && equip.NewConpose != null)
                {
                    for (int i = 0; i < equip.NewConpose.Count; i++)
                        producedTotal += equip.NewConpose[i].Num;
                }
                result.Check(equip != null && equip.Code == 0,
                    "compose.equipOk=" + (equip == null ? "null" : equip.Error));
                result.Check(producedTotal == 2, "compose.equipProduced=" + producedTotal);
                // 已扣 6 件材料
                int leftMaterial = 0;
                for (int i = 0; i < 6; i++)
                    leftMaterial += (int)ItemService.CountItem(role, 141 + i);
                result.Check(leftMaterial == 0, "compose.equipCost=" + leftMaterial);
                // 金币只扣 20000（可堆叠公式会扣 20000×6/3 = 40000，这里剩 0 说明只扣一份）
                result.Check(role.Coin == 0, "compose.equipGoldOnce=" + role.Coin);

                // 8) 材料不存在
                ComposeResponse missing = ComposeService.Instance.Compose(new ComposeRequest
                {
                    Info = new List<ComposeInfo> { new ComposeInfo { Uid = 999999999L, Num = 3 } },
                });
                result.Check(missing != null && missing.Code != 0 && missing.Error == "道具不存在",
                    "compose.missing=" + (missing == null ? "null" : missing.Error));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                ComposeService.Instance.ForcedComposeWeight = oldForced;
                role.Coin = oldCoin;
                LocalData.Instance.Gold = oldCoin;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 战宠数据与属性（第 55-A 节）。
        ///
        /// 断言的重点不是「有没有这个字段」，而是：
        ///   ① 只有 0 号上阵槽的宠物给属性，包裹里的不算；
        ///   ② 等级基础属性乘品质系数，且 .5 的舍入方向要跟原服 Go math.Round 一致（远离 0）；
        ///   ③ 星级属性是百分比，作用在宠物自己的 5 个属性上，不污染总属性的百分比桶；
        ///   ④ 经验宝宝（PetKind.key==2）品质系数强制 1；
        ///   ⑤ 21 个槽位 + 存档往返不丢数据。
        /// </summary>
        private static CaseResult RunPet()
        {
            var result = new CaseResult("pet");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            Dictionary<EquipSlot, ItemInfo> oldEquips = role.Equips;
            List<PetSlotInfo> oldPets = role.PetSlots;
            List<int> oldEver = role.PetEverHave;
            try
            {
                // 宠物属性只看上阵槽，装备/魂石清掉避免干扰断言
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
                role.EquipGems = new Dictionary<EquipSlot, int[]>();
                role.PetSlots = new List<PetSlotInfo>();
                role.PetEverHave = new List<int>();
                role.EnsurePetSlots();

                // 1) 配置解析
                result.Check(LocalRole.PetSlotCount == 21, "pet.slotCount=" + LocalRole.PetSlotCount);
                result.Check(PetService.GetBagTotal() == 20, "pet.bagTotal=" + PetService.GetBagTotal());
                result.Check(Math.Abs(PetService.GetQualityRate(7) - 3.5f) < 0.001f,
                    "pet.qualityRate=" + PetService.GetQualityRate(7));
                result.Check(Math.Abs(PetService.GetLevelRate(7) - 3.5f) < 0.001f,
                    "pet.levelRate=" + PetService.GetLevelRate(7));
                result.Check(PetService.GetUnlockedBagSlots(role) == 20,
                    "pet.unlockSlots=" + PetService.GetUnlockedBagSlots(role));

                RoleStatSnapshot before = RoleStatService.Instance.Recalculate(role, true);
                int atkBefore = before.PhysicalAttack;
                int hpBefore = before.MaxHealth;

                // 2) 获得宠物 → 落 1 号包裹格（0 号上阵位不自动占用）
                string error;
                PetSlotInfo granted = PetService.Instance.GrantPet(role, 1, 1, 0, out error);
                result.Check(granted != null && error == "",
                    "pet.grant=" + (error == "" ? "ok" : error));
                result.Check(role.PetSlots[1].CfgId == 1, "pet.slotCfg=" + role.PetSlots[1].CfgId);
                result.Check(role.PetSlots[0].CfgId == 0, "pet.activeEmpty=" + role.PetSlots[0].CfgId);
                result.Check(role.PetSlots[1].Quality == 7, "pet.quality=" + role.PetSlots[1].Quality);
                result.Check(role.PetEverHave != null && role.PetEverHave.Contains(1),
                    "pet.everHave=" + (role.PetEverHave == null ? -1 : role.PetEverHave.Count));

                // 3) 包裹里的宠物不给属性
                RoleStatSnapshot bagOnly = RoleStatService.Instance.Recalculate(role, true);
                result.Check(bagOnly.PhysicalAttack == atkBefore,
                    "pet.bagNoStat=" + atkBefore + "->" + bagOnly.PhysicalAttack);

                // 4) 上阵（挪到 0 号槽）后才加属性
                role.PetSlots[0] = role.PetSlots[1];
                role.PetSlots[1] = new PetSlotInfo();
                RoleStatSnapshot withPet = RoleStatService.Instance.Recalculate(role, true);
                // 宠物 1 号 1 级基础物攻 112 × 品质系数 3.5 = 392
                result.Check(withPet.PhysicalAttack - atkBefore == 392,
                    "pet.atk=+" + (withPet.PhysicalAttack - atkBefore));
                // 91 × 3.5 = 318.5 → 319（Go math.Round 远离 0；C# 默认银行家舍入会得 318）
                result.Check(withPet.MagicAttack - before.MagicAttack == 319,
                    "pet.matk=+" + (withPet.MagicAttack - before.MagicAttack));
                // 1540 × 3.5 = 5390
                result.Check(withPet.MaxHealth - hpBefore == 5390,
                    "pet.hp=+" + (withPet.MaxHealth - hpBefore));

                // 5) 星级 1 级：星级属性 +100（=1%）作用在宠物 5 个属性上
                role.PetSlots[0].Star = 1;
                RoleStatSnapshot star1 = RoleStatService.Instance.Recalculate(role, true);
                // 392 × 1.01 = 395.92 → 截断 395
                result.Check(star1.PhysicalAttack - atkBefore == 395,
                    "pet.starAtk=+" + (star1.PhysicalAttack - atkBefore));
                // 5390 × 1.01 = 5443.9 → 5443
                result.Check(star1.MaxHealth - hpBefore == 5443,
                    "pet.starHp=+" + (star1.MaxHealth - hpBefore));
                // 星级百分比不能漏进总属性的百分比桶（否则装备/魂石再乘一次）
                result.Check(star1.Get(pb.Attribute.PHYSICAL_ATTACK_PERCENTAGE) == 0,
                    "pet.noPercentLeak=" + star1.Get(pb.Attribute.PHYSICAL_ATTACK_PERCENTAGE));

                // 6) 经验宝宝（12 号，PetKind.key=2、品质 8）品质系数强制 1
                role.PetSlots[0] = new PetSlotInfo();
                PetSlotInfo expPet = PetService.Instance.GrantPet(role, 12, 1, 0, out error);
                result.Check(expPet != null, "pet.expGrant=" + error);
                int expSlot = -1;
                for (int i = 1; i < LocalRole.PetSlotCount; i++)
                {
                    if (role.PetSlots[i].CfgId == 12) { expSlot = i; break; }
                }
                result.Check(expSlot > 0, "pet.expSlot=" + expSlot);
                if (expSlot > 0)
                {
                    role.PetSlots[0] = role.PetSlots[expSlot];
                    role.PetSlots[expSlot] = new PetSlotInfo();
                    RoleStatSnapshot expStats = RoleStatService.Instance.Recalculate(role, true);
                    // 品质 8 的系数是 6，但经验宝宝强制 1 → 112 × 1 = 112
                    result.Check(expStats.PhysicalAttack - atkBefore == 112,
                        "pet.expPetAtk=+" + (expStats.PhysicalAttack - atkBefore));
                }

                // 7) 包裹满 20 格后给不了新宠物
                role.PetSlots = new List<PetSlotInfo>();
                role.EnsurePetSlots();
                int filled = 0;
                for (int i = 0; i < LocalRole.PetSlotCount; i++)
                {
                    if (PetService.Instance.GrantPet(role, 1, 1, 0, out error) != null)
                        filled++;
                }
                result.Check(filled == 20, "pet.fillBag=" + filled);
                PetSlotInfo overflow = PetService.Instance.GrantPet(role, 1, 1, 0, out error);
                result.Check(overflow == null && error == "宠物背包已满",
                    "pet.bagFull=" + (overflow == null ? error : "竟然成功了"));

                // 8) 存档往返：槽位 / 等级 / 星级 / 图鉴
                role.PetSlots = new List<PetSlotInfo>();
                role.EnsurePetSlots();
                role.PetEverHave = new List<int>();
                PetSlotInfo saved = PetService.Instance.GrantPet(role, 1, 1, 0, out error);
                if (saved != null)
                {
                    role.PetSlots[0] = saved;
                    role.PetSlots[1] = new PetSlotInfo();
                }
                role.PetSlots[0].Level = 30;
                role.PetSlots[0].Star = 3;
                role.PetSlots[0].ExpStar = 1234;

                var temp = new LocalData();
                temp.InitializeEmpty();
                temp.AddRole(role);
                SaveDocument document = SaveDocumentMapper.FromLocalData(temp);
                string json = JsonMapper.ToJson(document);
                SaveDocument parsed = JsonMapper.ToObject<SaveDocument>(json);
                List<LocalRole> restored = SaveDocumentMapper.ToLocalRoles(parsed);
                LocalRole r = (restored != null && restored.Count > 0) ? restored[0] : null;
                result.Check(r != null && r.PetSlots != null
                    && r.PetSlots.Count == LocalRole.PetSlotCount,
                    "pet.restoreSlots=" + (r == null || r.PetSlots == null ? -1 : r.PetSlots.Count));
                if (r != null && r.PetSlots != null && r.PetSlots.Count > 0)
                {
                    result.Check(r.PetSlots[0].CfgId == 1
                        && r.PetSlots[0].Level == 30
                        && r.PetSlots[0].Star == 3
                        && r.PetSlots[0].ExpStar == 1234,
                        "pet.restorePet=" + r.PetSlots[0].CfgId + "/" + r.PetSlots[0].Level
                        + "/" + r.PetSlots[0].Star + "/" + r.PetSlots[0].ExpStar);
                    result.Check(r.PetEverHave != null && r.PetEverHave.Count == 1,
                        "pet.restoreEver=" + (r.PetEverHave == null ? -1 : r.PetEverHave.Count));
                }
            }
            finally
            {
                role.Equips = oldEquips;
                role.PetSlots = oldPets;
                role.PetEverHave = oldEver;
                RoleStatService.Instance.Recalculate(role, false);
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 战宠养成操作（第 55-B 节）：升级 / 升星 / 换位 / 喂经验 / 合成。
        ///
        /// 断言对齐的是原服那些「看着像笔误但确实是线上行为」的地方：
        ///   · 升级的满级判定用的是星级（满星即满级），不是等级；
        ///   · 升星每次只加一份材料经验（Num&gt;1 也只加一份，但材料扣 Num 个）；
        ///   · 等级不够升星时经验会「补满停住」，攒满后再点才报等级不足；
        ///   · 品质相同的两只宠物拖动是**合成**（消耗两只产出一只），不是互换。
        /// </summary>
        private static CaseResult RunPetUp()
        {
            var result = new CaseResult("pet_up");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            Dictionary<EquipSlot, ItemInfo> oldEquips = role.Equips;
            List<PetSlotInfo> oldPets = role.PetSlots;
            List<int> oldEver = role.PetEverHave;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            int oldForced = PetService.Instance.ForcedStageRate;
            try
            {
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
                role.EquipGems = new Dictionary<EquipSlot, int[]>();
                role.PetSlots = new List<PetSlotInfo>();
                role.PetEverHave = new List<int>();
                role.EnsurePetSlots();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                // ===== 1) 升级：1 级神圣烈焰犬，本品质系数 3.5，1 级 NeedEXP=100 → 350 经验/级
                string error;
                PetSlotInfo pet = PetService.Instance.GrantPet(role, 1, 1, 0, out error);
                result.Check(pet != null, "pet_up.grant=" + error);
                role.PetSlots[0] = role.PetSlots[1];
                role.PetSlots[1] = new PetSlotInfo();

                ItemService.Instance.GrantReward(role, PetService.PetExpItemId, 400);
                CommonResponse up = PetService.Instance.LevelUp(0);
                result.Check(up != null && up.Code == 0,
                    "pet_up.levelCode=" + (up == null ? "null" : up.Code + "/" + up.Error));
                result.Check(role.PetSlots[0].Level == 2,
                    "pet_up.level=" + role.PetSlots[0].Level);
                result.Check(ItemService.CountItem(role, PetService.PetExpItemId) == 50,
                    "pet_up.expCost=" + ItemService.CountItem(role, PetService.PetExpItemId));

                // 2) 经验不足：2 级要 525，只剩 50
                CommonResponse poor = PetService.Instance.LevelUp(0);
                result.Check(poor != null && poor.Error == "宠物经验不足",
                    "pet_up.noExp=" + (poor == null ? "null" : poor.Error));
                result.Check(role.PetSlots[0].Level == 2,
                    "pet_up.noExpKeep=" + role.PetSlots[0].Level);

                // 3) 满星即满级（原服判定的是 Star >= len(StarConsume)，配置 10 档）
                role.PetSlots[0].Star = 10;
                CommonResponse maxed = PetService.Instance.LevelUp(0);
                result.Check(maxed != null && maxed.Error == "已满级",
                    "pet_up.maxLevel=" + (maxed == null ? "null" : maxed.Error));
                role.PetSlots[0].Star = 0;

                // ===== 4) 升星：10 级 + 无双突破丹(87, 基础 500) ×2 → 攒满 1000 升 1 星
                role.PetSlots[0].Level = 10;
                role.PetSlots[0].ExpStar = 0;
                ItemService.Instance.GrantReward(role, 87, 10);
                PetService.Instance.ForcedStageRate = 1;

                PetStarResponse s1 = PetService.Instance.StarUp(0, 87, 1);
                result.Check(s1 != null && s1.Code == 0 && s1.ResPer == 1,
                    "pet_up.starStep1=" + (s1 == null ? "null" : s1.Code + "/" + s1.ResPer));
                result.Check(role.PetSlots[0].ExpStar == 500 && role.PetSlots[0].Star == 0,
                    "pet_up.expStar=" + role.PetSlots[0].ExpStar + "/" + role.PetSlots[0].Star);

                PetStarResponse s2 = PetService.Instance.StarUp(0, 87, 1);
                result.Check(s2 != null && s2.Code == 0 && role.PetSlots[0].Star == 1,
                    "pet_up.star=" + role.PetSlots[0].Star);
                result.Check(role.PetSlots[0].ExpStar == 0,
                    "pet_up.expStarReset=" + role.PetSlots[0].ExpStar);
                result.Check(ItemService.CountItem(role, 87) == 8,
                    "pet_up.starCost=" + ItemService.CountItem(role, 87));

                // 5) 等级不够：5 级不能升 1 星（需要 10 级）→ 经验卡满 1000 不升星
                role.PetSlots[0].Level = 5;
                role.PetSlots[0].Star = 0;
                role.PetSlots[0].ExpStar = 0;
                PetService.Instance.StarUp(0, 87, 1);
                PetService.Instance.StarUp(0, 87, 1);
                result.Check(role.PetSlots[0].ExpStar == 1000 && role.PetSlots[0].Star == 0,
                    "pet_up.starLocked=" + role.PetSlots[0].ExpStar + "/" + role.PetSlots[0].Star);
                PetStarResponse locked = PetService.Instance.StarUp(0, 87, 1);
                result.Check(locked != null && locked.Error == "宠物等级不足",
                    "pet_up.starLockedMsg=" + (locked == null ? "null" : locked.Error));

                // ===== 6) 换位：上阵 → 包裹，属性应回落
                role.PetSlots = new List<PetSlotInfo>();
                role.EnsurePetSlots();
                PetSlotInfo p1 = PetService.Instance.GrantPet(role, 1, 1, 0, out error);
                role.PetSlots[0] = role.PetSlots[1];
                role.PetSlots[1] = new PetSlotInfo();
                RoleStatSnapshot withActive = RoleStatService.Instance.Recalculate(role, true);
                PetMoveResponse move = PetService.Instance.Move(0, 1);
                result.Check(move != null && move.Code == 0,
                    "pet_up.moveCode=" + (move == null ? "null" : move.Code + "/" + move.Error));
                result.Check(role.PetSlots[1].CfgId == 1 && role.PetSlots[0].CfgId == 0,
                    "pet_up.moveSwap=" + role.PetSlots[0].CfgId + "," + role.PetSlots[1].CfgId);
                RoleStatSnapshot noActive = RoleStatService.Instance.Recalculate(role, true);
                result.Check(noActive.PhysicalAttack < withActive.PhysicalAttack,
                    "pet_up.moveStats=" + withActive.PhysicalAttack + "->" + noActive.PhysicalAttack);

                // ===== 7) 喂经验：经验宝宝(12, 自带 8000 经验) 拖到 3 号宠物身上
                role.PetSlots = new List<PetSlotInfo>();
                role.EnsurePetSlots();
                PetService.Instance.GrantPet(role, 12, 1, 0, out error);   // → 1 号槽
                PetService.Instance.GrantPet(role, 3, 1, 0, out error);    // → 2 号槽
                int levelBefore = role.PetSlots[2].Level;
                PetMoveResponse feed = PetService.Instance.Move(1, 2);
                result.Check(feed != null && feed.Code == 0,
                    "pet_up.feedCode=" + (feed == null ? "null" : feed.Code + "/" + feed.Error));
                result.Check(role.PetSlots[1].CfgId == 0,
                    "pet_up.feedGone=" + role.PetSlots[1].CfgId);
                result.Check(role.PetSlots[2].Level > levelBefore,
                    "pet_up.feedLevel=" + levelBefore + "->" + role.PetSlots[2].Level);

                // ===== 8) 合成：同品质两只（50001 + 50002 → ComposeID 90）
                role.PetSlots = new List<PetSlotInfo>();
                role.EnsurePetSlots();
                PetService.Instance.GrantPet(role, 1, 1, 0, out error);   // → 1 号槽
                PetService.Instance.GrantPet(role, 2, 1, 0, out error);   // → 2 号槽
                result.Check(role.HasPet(1) && role.HasPet(2), "pet_up.composeBefore=2");
                PetMoveResponse compose = PetService.Instance.Move(1, 2);
                result.Check(compose != null && compose.Code == 0,
                    "pet_up.composeCode=" + (compose == null ? "null" : compose.Code + "/" + compose.Error));
                result.Check(role.PetSlots[1].CfgId == 0, "pet_up.composeFrom=" + role.PetSlots[1].CfgId);
                // 产出只会是 50013/50051/50052/50053 对应的宠物
                int newCfg = role.PetSlots[2].CfgId;
                result.Check(newCfg == 13 || newCfg == 51 || newCfg == 52 || newCfg == 53,
                    "pet_up.composeNew=" + newCfg);
            }
            finally
            {
                PetService.Instance.ForcedStageRate = oldForced;
                role.Equips = oldEquips;
                role.PetSlots = oldPets;
                role.PetEverHave = oldEver;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                RoleStatService.Instance.Recalculate(role, false);
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 宠物获取与挂机产出（第 55-C 节）：
        ///   · 战宠道具（item.Type=9）进背包时**直接变成宠物**，不落背包（对齐原服 addSource 分支）；
        ///   · 上阵宠物按 pet.json 的 OfflineAward 产出经验/金币（间隔 + 终身累计上限）；
        ///   · 没上阵宠物时只推进结算锚点，不产出。
        /// </summary>
        private static CaseResult RunPetAward()
        {
            var result = new CaseResult("pet_award");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<PetSlotInfo> oldPets = role.PetSlots;
            List<int> oldEver = role.PetEverHave;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            long oldAnchor = role.PetAwardAnchorUnix;
            long oldExpTotal = role.PetAwardExpTotal;
            long oldGoldTotal = role.PetAwardGoldTotal;
            try
            {
                role.PetSlots = new List<PetSlotInfo>();
                role.PetEverHave = new List<int>();
                role.EnsurePetSlots();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();
                role.PetAwardAnchorUnix = 0;
                role.PetAwardExpTotal = 0;
                role.PetAwardGoldTotal = 0;

                // 1) 战宠道具进包 → 直接变宠物，背包里不留道具
                result.Check(ItemService.IsPetItem(50001), "pet_award.isPetItem=" + ItemService.IsPetItem(50001));
                result.Check(!ItemService.IsPetItem(83), "pet_award.notPetItem=" + ItemService.IsPetItem(83));
                ItemService.Instance.GrantReward(role, 50001, 1);
                result.Check(ItemService.CountItem(role, 50001) == 0,
                    "pet_award.noBagItem=" + ItemService.CountItem(role, 50001));
                bool found = false;
                for (int i = 0; i < LocalRole.PetSlotCount; i++)
                {
                    if (role.PetSlots[i].CfgId == 1) { found = true; break; }
                }
                result.Check(found, "pet_award.gotPet=" + found);

                // 2) 普通道具仍然进背包（对照，确认拦截没误伤）
                ItemService.Instance.GrantReward(role, 83, 5);
                result.Check(ItemService.CountItem(role, 83) == 5,
                    "pet_award.normalItem=" + ItemService.CountItem(role, 83));

                // 3) 上阵宠物挂机产出：1 号宠物 0 星 = 每 60 秒 8000 经验
                for (int i = 0; i < LocalRole.PetSlotCount; i++)
                {
                    if (role.PetSlots[i].CfgId != 1)
                        continue;
                    role.PetSlots[0] = role.PetSlots[i];
                    if (i != 0)
                        role.PetSlots[i] = new PetSlotInfo();
                    break;
                }
                long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                role.PetAwardAnchorUnix = now - 180;      // 已经过了 3 个周期
                PetService.Instance.SettleOfflineAward(role);
                result.Check(role.PetAwardExpTotal == 24000,
                    "pet_award.expTotal=" + role.PetAwardExpTotal);
                result.Check(role.PetAwardAnchorUnix >= now,
                    "pet_award.anchor=" + (now - role.PetAwardAnchorUnix));

                // 4) 再调一次：时间只过了 0 秒，不该重复产出
                PetService.Instance.SettleOfflineAward(role);
                result.Check(role.PetAwardExpTotal == 24000,
                    "pet_award.noDouble=" + role.PetAwardExpTotal);

                // 5) 终身累计上限：把已产出顶到 max-1000，再结算 600 秒只能补 1000
                role.PetAwardExpTotal = 11520000 - 1000;
                role.PetAwardAnchorUnix = now - 600;
                PetService.Instance.SettleOfflineAward(role);
                result.Check(role.PetAwardExpTotal == 11520000,
                    "pet_award.cap=" + role.PetAwardExpTotal);

                // 6) 没有上阵宠物 → 只推进锚点，不产出
                role.PetSlots[0] = new PetSlotInfo();
                role.PetAwardExpTotal = 0;
                role.PetAwardAnchorUnix = now - 600;
                PetService.Instance.SettleOfflineAward(role);
                result.Check(role.PetAwardExpTotal == 0 && role.PetAwardAnchorUnix >= now,
                    "pet_award.noPet=" + role.PetAwardExpTotal + "/" + (now - role.PetAwardAnchorUnix));

                // 7) 存档往返：锚点与累计值
                role.PetAwardAnchorUnix = now;
                role.PetAwardExpTotal = 1234;
                role.PetAwardGoldTotal = 5678;
                var temp = new LocalData();
                temp.InitializeEmpty();
                temp.AddRole(role);
                SaveDocument document = SaveDocumentMapper.FromLocalData(temp);
                string json = JsonMapper.ToJson(document);
                SaveDocument parsed = JsonMapper.ToObject<SaveDocument>(json);
                List<LocalRole> restored = SaveDocumentMapper.ToLocalRoles(parsed);
                LocalRole r = (restored != null && restored.Count > 0) ? restored[0] : null;
                result.Check(r != null && r.PetAwardExpTotal == 1234 && r.PetAwardGoldTotal == 5678,
                    "pet_award.restore=" + (r == null ? "null"
                        : r.PetAwardExpTotal + "/" + r.PetAwardGoldTotal));
            }
            finally
            {
                role.PetSlots = oldPets;
                role.PetEverHave = oldEver;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                role.PetAwardAnchorUnix = oldAnchor;
                role.PetAwardExpTotal = oldExpTotal;
                role.PetAwardGoldTotal = oldGoldTotal;
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 伙伴整卡 / 碎片道具进背包的行为（第 55 节 ① 补强）。
        ///
        /// 原服 addSource 只对 **ItemPartner（item.Type = 8）** 做转换：
        /// 伙伴配置的 ItemID 命中就直接 addPartner，不落背包；
        /// 伙伴**碎片**（Type = 7）走正常入包（它要靠 C2S_PartnerCompose 消耗）。
        /// 重复获得的整卡 → AddPartner 内部转碎片（ChipNum）。
        /// </summary>
        /// <summary>
        /// 伙伴装备（② 2026-10-28）：穿脱 / 校验用伙伴的 job+level / 槽位保留强化 / 防误分解 / 存档往返。
        /// 用装备 101（武器，战士专属，lv1）：伙伴 1（哈迪曼）job 11 → %10 = 1，能穿；
        /// 1001（武器 lv76）用来验证"等级不足按伙伴等级判，不看玩家等级"。
        /// 伙伴属性会经 RoleStatService.AddPartnerStats 累加进玩家总战力，所以用 TotalCombat 验证生效。
        /// </summary>
        private static CaseResult RunPartnerEquip()
        {
            var result = new CaseResult("partner_equip");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                BattleService.Instance.Exit();
                // 干净的四空槽
                role.PartnerSlots = new List<PartnerSlotInfo>();
                PartnerService.Instance.EnsureSlots(role);
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                Func<int, long> bagUidOf = cfgId =>
                {
                    for (int i = 0; i < role.BagData.ItemList.Count; i++)
                    {
                        if (role.BagData.ItemList[i] != null && role.BagData.ItemList[i].CfgId == cfgId)
                            return role.BagData.ItemList[i].Uid;
                    }
                    return 0;
                };
                Func<long, bool> inBagByUid = uid =>
                {
                    for (int i = 0; i < role.BagData.ItemList.Count; i++)
                    {
                        ItemInfo bagItem = role.BagData.ItemList[i];
                        if (bagItem != null && bagItem.Uid == uid)
                            return true;
                    }
                    return false;
                };

                // 1) 槽位没伙伴 → 拒绝
                ItemService.Instance.GrantReward(role, 101, 1);
                long weaponUid = bagUidOf(101);
                var noPartner = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    ItemUid = weaponUid,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(noPartner != null && noPartner.Code != 0 && noPartner.Error == "该槽位没有伙伴",
                    "pe.noPartner=" + (noPartner == null ? "null" : noPartner.Error));

                // 2) 上阵伙伴 1（job 11 → %10 = 1，能穿战士装备）
                //    槽位 1 要 20 级才解锁（config.xml openslot），先把等级抬到 40 保证 4 槽全开
                role.Level = 40;
                PartnerService.Instance.AddPartner(role, 1);
                PartnerInfo testPartner = null;
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    if (role.Partners[i] != null && role.Partners[i].CfgId == 1)
                    {
                        testPartner = role.Partners[i];
                        break;
                    }
                }
                result.Check(testPartner != null, "pe.hasPartner=" + (testPartner == null ? "no" : "yes"));
                long partnerUid = testPartner != null ? testPartner.Uid : 0;
                CommonResponse combatOn = PartnerService.Instance.Combat(new PartnerCombatRequest
                {
                    SlotId = 1,
                    PartnerUid = partnerUid,
                });
                PartnerSlotInfo slot = role.FindPartnerSlot(1);
                result.Check(combatOn != null && combatOn.Code == 0
                    && slot != null && slot.PartnerUid == partnerUid,
                    "pe.onSlot=" + (combatOn == null ? "null" : combatOn.Code + "/" + combatOn.Error));

                RoleStatService.Instance.RecalculateCurrent();
                int combatBefore = role.TotalCombat;

                var equip = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    ItemUid = weaponUid,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(equip != null && equip.Code == 0,
                    "pe.equip=" + (equip == null ? "null" : equip.Code + "/" + equip.Error));

                SlotInfo weaponSlotInfo = slot != null && slot.Equips != null && slot.Equips.Count > 1
                    ? slot.Equips[1] : null;
                result.Check(weaponSlotInfo != null && weaponSlotInfo.Item != null
                    && weaponSlotInfo.Item.Uid == weaponUid,
                    "pe.slotItem=" + (weaponSlotInfo == null || weaponSlotInfo.Item == null
                        ? "null" : weaponSlotInfo.Item.CfgId));
                result.Check(bagUidOf(101) == 0, "pe.leftBag=" + (bagUidOf(101) == 0 ? 0 : 1));

                RoleStatService.Instance.RecalculateCurrent();
                result.Check(role.TotalCombat > combatBefore,
                    "pe.combat=" + role.TotalCombat + ">" + combatBefore);

                // 3) 部位不匹配（101 是武器，穿到衣服位）
                ItemService.Instance.GrantReward(role, 101, 1);
                long secondWeapon = bagUidOf(101);
                var wrongSlot = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Clothes,
                    ItemUid = secondWeapon,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(wrongSlot != null && wrongSlot.Code != 0 && wrongSlot.Error == "装备部位不匹配",
                    "pe.wrongSlot=" + (wrongSlot == null ? "null" : wrongSlot.Error));

                // 4) 等级不足：按**伙伴**等级判（1001 需要 lv76）
                ItemService.Instance.GrantReward(role, 1001, 1);
                long highLevel = bagUidOf(1001);
                var levelBlocked = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    ItemUid = highLevel,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(levelBlocked != null && levelBlocked.Code != 0
                    && levelBlocked.Error == "伙伴等级不足，无法穿戴",
                    "pe.levelLimit=" + (levelBlocked == null ? "null" : levelBlocked.Error));

                // 5) 同一件装备重复穿戴被拒
                var dup = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    ItemUid = weaponUid,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(dup != null && dup.Code != 0 && dup.Error == "该装备已被穿戴",
                    "pe.dup=" + (dup == null ? "null" : dup.Error));

                // 6) 伙伴身上的装备不能被分解
                var breakIt = ItemService.Instance.ItemDisassembly(TCPClientMgr.ProtoSerialize(
                    new ItemDisassemblyRequest { ItemUids = new long[] { weaponUid } })) as ItemDisassemblyResponse;
                result.Check(breakIt != null && breakIt.Code == -3,
                    "pe.noBreak=" + (breakIt == null ? "null" : breakIt.Code));
                result.Check(slot.Equips[1].Item != null && slot.Equips[1].Item.Uid == weaponUid,
                    "pe.noBreakKeep=" + (slot.Equips[1].Item == null ? "gone" : "kept"));

                // 7) 存档往返：Equips 要持久化
                SaveDocument doc = SaveDocumentMapper.FromLocalData(LocalData.Instance);
                string json = JEngine.LitJson.JsonMapper.ToJson(doc);
                SaveDocument parsed = JEngine.LitJson.JsonMapper.ToObject<SaveDocument>(json);
                List<LocalRole> restored = SaveDocumentMapper.ToLocalRoles(parsed);
                bool savedEquip = false;
                if (restored != null && restored.Count > 0)
                {
                    restored[0].EnsurePartnerEquipSlots();
                    PartnerSlotInfo restoredSlot = restored[0].FindPartnerSlot(1);
                    savedEquip = restoredSlot != null && restoredSlot.Equips != null
                        && restoredSlot.Equips.Count > 1 && restoredSlot.Equips[1].Item != null
                        && restoredSlot.Equips[1].Item.Uid == weaponUid;
                }
                result.Check(savedEquip, "pe.save=" + savedEquip);

                // 8) 脱下回背包
                var takeOff = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 2,
                    Slot = EquipSlot.Weapon,
                    ItemUid = weaponUid,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(takeOff != null && takeOff.Code == 0,
                    "pe.takeOff=" + (takeOff == null ? "null" : takeOff.Code + "/" + takeOff.Error));
                // 注意：这里不能用「背包里第一件 101」比对 —— 步骤 3 还往包里塞了一件 101，
                // 必须按 uid 精确找。
                bool backToBag = inBagByUid(weaponUid);
                result.Check(slot.Equips[1].Item == null && backToBag,
                    "pe.takeOffBag=" + (backToBag ? "back" : "missing"));

                // 9) 下阵必须自动脱装备（原服 partnerRest）：先穿回去，再整体下阵
                var reEquip = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    ItemUid = weaponUid,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(reEquip != null && reEquip.Code == 0,
                    "pe.reEquip=" + (reEquip == null ? "null" : reEquip.Code + "/" + reEquip.Error));
                result.Check(slot.Equips[1].Item != null, "pe.reEquipSlot=" + (slot.Equips[1].Item != null ? "on" : "off"));

                var rest = PartnerService.Instance.Rest(new PartnerRestRequest { SlotId = 1 });
                result.Check(rest != null && rest.Code == 0,
                    "pe.rest=" + (rest == null ? "null" : rest.Code + "/" + rest.Error));
                // 修复前：下阵只清 PartnerUid，装备留在空槽里 ⇒ 既不在背包也不在身上，永久丢失
                result.Check(slot.PartnerUid == 0 && slot.Equips[1].Item == null,
                    "pe.restSlot=" + (slot.Equips[1].Item == null ? "empty" : "stuck"));
                result.Check(inBagByUid(weaponUid),
                    "pe.restBag=" + (inBagByUid(weaponUid) ? "back" : "missing"));
                // 槽位的强化/升星进度要保留（原服 changeEquip(2) 只把 ItemId 置 0）
                result.Check(slot.Equips[1].Enhance == 0 && slot.Equips[1].Stars == 0,
                    "pe.restKeepSlot=ok");
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.PartnerSlots = oldSlots;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 伙伴魂石（②-B 2026-10-28）：Gemstone 存在 PartnerSlot.Equips[slot] 上（原服 Slot.Gemstone），
        /// 魂石槽开启数按**伙伴等级**算，不是玩家等级。
        /// 10001（1级红魂石）DiamondPosition=[1,5,6] 可镶武器位，+10 物攻。
        /// </summary>
        private static CaseResult RunPartnerGem()
        {
            var result = new CaseResult("partner_gem");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                BattleService.Instance.Exit();
                role.Level = 40;
                role.PartnerSlots = new List<PartnerSlotInfo>();
                PartnerService.Instance.EnsureSlots(role);
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                // 1) 槽位没有伙伴
                var noPartner = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = GemstoneSlot.INVALID_GemstoneSlot,
                    ItemUid = 10001,
                    PartnerSlotId = 1,
                });
                result.Check(noPartner != null && noPartner.Code != 0 && noPartner.Error == "该槽位没有伙伴",
                    "pg.noPartner=" + (noPartner == null ? "null" : noPartner.Error));

                // 2) 上阵伙伴（魂石槽按伙伴等级算，等级抬到 40 才有槽）
                PartnerService.Instance.AddPartner(role, 1);
                PartnerInfo partner = null;
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    if (role.Partners[i] != null && role.Partners[i].CfgId == 1)
                    {
                        partner = role.Partners[i];
                        break;
                    }
                }
                if (partner == null)
                {
                    result.Fail("伙伴创建失败");
                    return result;
                }
                partner.Level = 40;
                PartnerService.Instance.Combat(new PartnerCombatRequest { SlotId = 1, PartnerUid = partner.Uid });

                // 3) 伙伴没穿装备的部位不能镶
                var noEquip = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Clothes,
                    GemstoneSlot = GemstoneSlot.INVALID_GemstoneSlot,
                    ItemUid = 10001,
                    PartnerSlotId = 1,
                });
                result.Check(noEquip != null && noEquip.Code != 0 && noEquip.Error == "该部位没有装备",
                    "pg.noEquip=" + (noEquip == null ? "null" : noEquip.Error));

                // 4) 穿上武器再镶
                ItemService.Instance.GrantReward(role, 101, 1);
                long weaponUid = 0;
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    if (role.BagData.ItemList[i] != null && role.BagData.ItemList[i].CfgId == 101)
                    {
                        weaponUid = role.BagData.ItemList[i].Uid;
                        break;
                    }
                }
                var equip = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    ItemUid = weaponUid,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(equip != null && equip.Code == 0,
                    "pg.equip=" + (equip == null ? "null" : equip.Code + "/" + equip.Error));

                ItemService.Instance.GrantReward(role, 10001, 2);
                RoleStatService.Instance.RecalculateCurrent();
                int combatBefore = role.TotalCombat;

                var inlay = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = GemstoneSlot.INVALID_GemstoneSlot,
                    ItemUid = 10001,
                    PartnerSlotId = 1,
                });
                result.Check(inlay != null && inlay.Code == 0,
                    "pg.inlay=" + (inlay == null ? "null" : inlay.Code + "/" + inlay.Error));

                PartnerSlotInfo slot = role.FindPartnerSlot(1);
                int gemInSlot = slot != null && slot.Equips != null && slot.Equips.Count > 1
                    && slot.Equips[1] != null && slot.Equips[1].Gemstone != null
                    && slot.Equips[1].Gemstone.Length > 0
                    ? slot.Equips[1].Gemstone[0] : -1;
                result.Check(gemInSlot == 10001, "pg.gemSlot=" + gemInSlot);
                result.Check(ItemService.CountItem(role, 10001) == 1,
                    "pg.consumed=" + ItemService.CountItem(role, 10001));

                RoleStatService.Instance.RecalculateCurrent();
                result.Check(role.TotalCombat > combatBefore,
                    "pg.combat=" + role.TotalCombat + ">" + combatBefore);

                // 5) 玩家同部位宝石不受影响（两套存储互相独立）
                int[] playerGems = role.GetEquipGems(EquipSlot.Weapon);
                result.Check(playerGems[0] == 0, "pg.playerIsolated=" + playerGems[0]);

                // 6) 拆卸回背包
                var remove = GemService.Instance.Change(new GemstoneChangeRequest
                {
                    OpType = 2,
                    Slot = EquipSlot.Weapon,
                    GemstoneSlot = GemstoneSlot.INVALID_GemstoneSlot,
                    ItemUid = 10001,
                    PartnerSlotId = 1,
                });
                result.Check(remove != null && remove.Code == 0,
                    "pg.remove=" + (remove == null ? "null" : remove.Code + "/" + remove.Error));
                gemInSlot = slot.Equips[1].Gemstone[0];
                result.Check(gemInSlot == 0 && ItemService.CountItem(role, 10001) == 2,
                    "pg.removeBack=" + gemInSlot + "/" + ItemService.CountItem(role, 10001));
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.PartnerSlots = oldSlots;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 伙伴装备强化/升星（②-C 2026-10-28）。
        /// 槽位的 Enhance/Stars 存在 PartnerSlot.Equips[slot] 上：
        ///   · 强化上限按**伙伴等级** × Equipstrenghthen.rate（原服 enhance 的伙伴分支）
        ///   · 取配置的职业用伙伴 `cfg.job % 10`（原服这里用玩家职业，但属性计算用伙伴职业，两边不一致，这里统一）
        /// </summary>
        private static CaseResult RunPartnerUpgrade()
        {
            var result = new CaseResult("partner_upgrade");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            int oldForcedWeight = EquipService.Instance.ForcedStarWeight;
            try
            {
                BattleService.Instance.Exit();
                role.Level = 40;
                role.PartnerSlots = new List<PartnerSlotInfo>();
                PartnerService.Instance.EnsureSlots(role);
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                PartnerService.Instance.AddPartner(role, 1);
                PartnerInfo partner = null;
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    if (role.Partners[i] != null && role.Partners[i].CfgId == 1)
                    {
                        partner = role.Partners[i];
                        break;
                    }
                }
                if (partner == null)
                {
                    result.Fail("伙伴创建失败");
                    return result;
                }
                partner.Level = 40;
                PartnerService.Instance.Combat(new PartnerCombatRequest { SlotId = 1, PartnerUid = partner.Uid });

                ItemService.Instance.GrantReward(role, 101, 1);
                long weaponUid = 0;
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    if (role.BagData.ItemList[i] != null && role.BagData.ItemList[i].CfgId == 101)
                    {
                        weaponUid = role.BagData.ItemList[i].Uid;
                        break;
                    }
                }
                var equip = ItemService.Instance.ChangeEquip(TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = EquipSlot.Weapon,
                    ItemUid = weaponUid,
                    PartnerSlotId = 1,
                })) as ChangeEquipResponse;
                result.Check(equip != null && equip.Code == 0,
                    "pu.equip=" + (equip == null ? "null" : equip.Code + "/" + equip.Error));

                // 材料备足
                ItemService.Instance.GrantReward(role, 1, 100000000);
                ItemService.Instance.GrantReward(role, 21, 2000);
                ItemService.Instance.GrantReward(role, 22, 2000);

                PartnerSlotInfo slot = role.FindPartnerSlot(1);
                RoleStatService.Instance.RecalculateCurrent();
                int combatBefore = role.TotalCombat;

                // 1) 伙伴槽强化（空槽拒绝先从别的部位试）
                var noEquipEnhance = EquipService.Instance.Enhance(new EnhanceRequest
                {
                    Slot = EquipSlot.Clothes,
                    PartnerSlotId = 1,
                });
                result.Check(noEquipEnhance != null && noEquipEnhance.Code != 0
                    && noEquipEnhance.Error == "该部位没有装备",
                    "pu.enhanceNeedEquip=" + (noEquipEnhance == null ? "null" : noEquipEnhance.Error));

                var enhance = EquipService.Instance.Enhance(new EnhanceRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 1,
                });
                result.Check(enhance != null && enhance.Code == 0 && enhance.NewLevel == 1,
                    "pu.enhance=" + (enhance == null ? "null" : enhance.Code + "/" + enhance.NewLevel + "/" + enhance.Error));
                result.Check(slot.Equips[1].Enhance == 1, "pu.enhanceSlot=" + slot.Equips[1].Enhance);
                // 玩家侧同部位不受影响（两套存储互相独立）
                result.Check(role.GetEquipEnhance(EquipSlot.Weapon) == 0,
                    "pu.playerEnhanceIsolated=" + role.GetEquipEnhance(EquipSlot.Weapon));

                // 2) 伙伴槽升星（强制成功分支）
                EquipService.Instance.ForcedStarWeight = 0;
                var stars = EquipService.Instance.Stars(new StarsRequest
                {
                    Slot = EquipSlot.Weapon,
                    PartnerSlotId = 1,
                });
                result.Check(stars != null && stars.Code == 0 && stars.NewLevel == 1,
                    "pu.stars=" + (stars == null ? "null" : stars.Code + "/" + stars.NewLevel + "/" + stars.Error));
                result.Check(slot.Equips[1].Stars == 1, "pu.starsSlot=" + slot.Equips[1].Stars);
                result.Check(role.GetEquipStars(EquipSlot.Weapon) == 0,
                    "pu.playerStarsIsolated=" + role.GetEquipStars(EquipSlot.Weapon));

                // 3) 强化/升星要进战力
                RoleStatService.Instance.RecalculateCurrent();
                result.Check(role.TotalCombat > combatBefore,
                    "pu.combat=" + role.TotalCombat + ">" + combatBefore);

                // 4) 存档往返保留槽位进度
                SaveDocument doc = SaveDocumentMapper.FromLocalData(LocalData.Instance);
                string json = JEngine.LitJson.JsonMapper.ToJson(doc);
                List<LocalRole> restored = SaveDocumentMapper.ToLocalRoles(
                    JEngine.LitJson.JsonMapper.ToObject<SaveDocument>(json));
                bool saved = false;
                if (restored != null && restored.Count > 0)
                {
                    restored[0].EnsurePartnerEquipSlots();
                    PartnerSlotInfo rs = restored[0].FindPartnerSlot(1);
                    saved = rs != null && rs.Equips[1] != null
                        && rs.Equips[1].Enhance == 1 && rs.Equips[1].Stars == 1;
                }
                result.Check(saved, "pu.save=" + saved);
            }
            catch (Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                EquipService.Instance.ForcedStarWeight = oldForcedWeight;
                BattleService.Instance.Exit();
                role.PartnerSlots = oldSlots;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        /// <summary>
        /// 伙伴合成 / 分解 / 洗练。
        ///
        /// 数值来自配置：
        ///   partner 1（哈迪曼）Q7 job11，ChipNum 40034×120；
        ///   StarConsume 1..5 星各 40034×120 + 金币 1/2/3/4/5 万，共 600 碎片 + 15 万金币；
        ///   config.xml partener/decompose/stage → quality=7 starlevel=5 rate=50 id=95；
        ///   config.xml partener/forge/free rate=70，charge quality=7 → id 5 × 2500；
        ///   job11 升到 40 级共 12,851,500 经验 → 12 个传奇经验珠(34) + 1 个无双(33) + 3 个大型(32)
        ///                                        + 1 个中型(31) + 1 个微型(94)。
        /// </summary>
        private static CaseResult RunPartnerReset()
        {
            var result = new CaseResult("partner_reset");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<PartnerInfo> oldPartners = role.Partners;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                BattleService.Instance.Exit();
                role.Level = 40;
                role.Partners = new List<PartnerInfo>();
                role.PartnerSlots = new List<PartnerSlotInfo>();
                PartnerService.Instance.EnsureSlots(role);
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                // ---------- 1) 合成：碎片不足 ----------
                var lackChip = PartnerService.Instance.Compose(
                    new PartnerComposeRequest { PartnerCfgId = 1 });
                result.Check(lackChip != null && lackChip.Code != 0
                    && lackChip.Error == "伙伴碎片不足",
                    "pr.composeLack=" + (lackChip == null ? "null" : lackChip.Error));
                result.Check(role.Partners.Count == 0,
                    "pr.composeLackCount=" + role.Partners.Count);

                // ---------- 2) 合成成功：扣 120 碎片 ----------
                ItemService.Instance.GrantReward(role, 40034, 200);
                var composed = PartnerService.Instance.Compose(
                    new PartnerComposeRequest { PartnerCfgId = 1 });
                result.Check(composed != null && composed.Code == 0,
                    "pr.compose=" + (composed == null ? "null" : composed.Code + "/" + composed.Error));
                result.Check(role.Partners.Count == 1, "pr.composeCount=" + role.Partners.Count);
                result.Check(ItemService.CountItem(role, 40034) == 80,
                    "pr.composeChip=" + ItemService.CountItem(role, 40034));

                // ---------- 3) 重复合成被拒，且不扣碎片 ----------
                var dupCompose = PartnerService.Instance.Compose(
                    new PartnerComposeRequest { PartnerCfgId = 1 });
                result.Check(dupCompose != null && dupCompose.Code != 0
                    && dupCompose.Error == "已经拥有该伙伴",
                    "pr.composeDup=" + (dupCompose == null ? "null" : dupCompose.Error));
                result.Check(ItemService.CountItem(role, 40034) == 80,
                    "pr.composeDupChip=" + ItemService.CountItem(role, 40034));

                PartnerInfo partner = role.Partners[0];

                // ---------- 4) 星级不足（0 星 < 5 星门槛）不能分解 ----------
                var lowStar = PartnerService.Instance.Disassemble(
                    new PartnerDisassembleRequest { PartnerUid = partner.Uid });
                result.Check(lowStar != null && lowStar.Code != 0
                    && lowStar.Error == "星级不足，无法分解（需 5 星）",
                    "pr.disStar=" + (lowStar == null ? "null" : lowStar.Error));

                // ---------- 5) 上阵 + 穿装备 + 拉到 5 星 40 级 ----------
                partner.Level = 40;
                partner.Star = 5;
                PartnerService.Instance.Combat(
                    new PartnerCombatRequest { SlotId = 1, PartnerUid = partner.Uid });
                ItemService.Instance.GrantReward(role, 101, 1);
                long weaponUid = 0;
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    ItemInfo bagItem = role.BagData.ItemList[i];
                    if (bagItem != null && bagItem.CfgId == 101)
                    {
                        weaponUid = bagItem.Uid;
                        break;
                    }
                }
                var equip = ItemService.Instance.ChangeEquip(
                    TCPClientMgr.ProtoSerialize(new ChangeEquipRequest
                    {
                        OpType = 1,
                        Slot = EquipSlot.Weapon,
                        ItemUid = weaponUid,
                        PartnerSlotId = 1,
                    })) as ChangeEquipResponse;
                result.Check(equip != null && equip.Code == 0,
                    "pr.equip=" + (equip == null ? "null" : equip.Code + "/" + equip.Error));

                // ---------- 6) 分解：600 碎片 × 50% → 通用碎片 95 × 300 ----------
                long coinBefore = role.Coin;
                var dis = PartnerService.Instance.Disassemble(
                    new PartnerDisassembleRequest { PartnerUid = partner.Uid });
                result.Check(dis != null && dis.Code == 0,
                    "pr.dis=" + (dis == null ? "null" : dis.Code + "/" + dis.Error));
                result.Check(partner.Star == 0 && partner.Level == 1,
                    "pr.disReset=" + partner.Star + "/" + partner.Level);
                result.Check(ItemService.CountItem(role, 95) == 300,
                    "pr.disFragment=" + ItemService.CountItem(role, 95));
                // 碎片本体不退还（已折成通用碎片），仍是最初剩下的 80
                result.Check(ItemService.CountItem(role, 40034) == 80,
                    "pr.disChipKept=" + ItemService.CountItem(role, 40034));
                // 金币 1+2+3+4+5 万 = 15 万原样退
                result.Check(role.Coin - coinBefore == 150000,
                    "pr.disGold=" + (role.Coin - coinBefore));
                // 升级经验 → 经验珠
                result.Check(ItemService.CountItem(role, 34) == 12,
                    "pr.disExpBead=" + ItemService.CountItem(role, 34));

                PartnerSlotInfo slotAfter = role.FindPartnerSlot(1);
                bool weaponBack = false;
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    ItemInfo bagItem = role.BagData.ItemList[i];
                    if (bagItem != null && bagItem.Uid == weaponUid)
                    {
                        weaponBack = true;
                        break;
                    }
                }
                result.Check(weaponBack
                    && slotAfter != null && slotAfter.Equips != null
                    && slotAfter.Equips[1].Item == null,
                    "pr.disEquipBack=" + weaponBack);

                // ---------- 7) 洗练：1 级不能洗 ----------
                var lowLv = PartnerService.Instance.Reforge(
                    new PartnerReforgeRequest { PartnerUid = partner.Uid, IsFree = true });
                result.Check(lowLv != null && lowLv.Code != 0
                    && lowLv.Error == "伙伴等级为 1 级，无需洗练",
                    "pr.reforgeLowLv=" + (lowLv == null ? "null" : lowLv.Error));

                // ---------- 8) 免费洗练：退 70% ----------
                partner.Level = 40;
                partner.Star = 5;
                long chipBeforeFree = ItemService.CountItem(role, 40034);
                var freeReforge = PartnerService.Instance.Reforge(
                    new PartnerReforgeRequest { PartnerUid = partner.Uid, IsFree = true });
                result.Check(freeReforge != null && freeReforge.Code == 0,
                    "pr.reforgeFree=" + (freeReforge == null ? "null" : freeReforge.Code + "/" + freeReforge.Error));
                result.Check(partner.Star == 0 && partner.Level == 1,
                    "pr.reforgeFreeReset=" + partner.Star + "/" + partner.Level);
                // 600 × 70% = 420（洗练不折算碎片，原样退 40034）
                result.Check(ItemService.CountItem(role, 40034) - chipBeforeFree == 420,
                    "pr.reforgeFreeChip=" + (ItemService.CountItem(role, 40034) - chipBeforeFree));

                // ---------- 9) 付费洗练：扣元宝退全额 ----------
                partner.Level = 40;
                partner.Star = 5;
                ItemService.Instance.GrantReward(role, 5, 10000);
                long couponBefore = role.Coupon;
                long chipBeforePaid = ItemService.CountItem(role, 40034);
                var paidReforge = PartnerService.Instance.Reforge(
                    new PartnerReforgeRequest { PartnerUid = partner.Uid, IsFree = false });
                result.Check(paidReforge != null && paidReforge.Code == 0,
                    "pr.reforgePaid=" + (paidReforge == null ? "null" : paidReforge.Code + "/" + paidReforge.Error));
                result.Check(couponBefore - role.Coupon == 2500,
                    "pr.reforgeCharge=" + (couponBefore - role.Coupon));
                result.Check(ItemService.CountItem(role, 40034) - chipBeforePaid == 600,
                    "pr.reforgePaidChip=" + (ItemService.CountItem(role, 40034) - chipBeforePaid));

                // ---------- 10) 不存在 / 非法参数 ----------
                var noPartner = PartnerService.Instance.Disassemble(
                    new PartnerDisassembleRequest { PartnerUid = 99999999 });
                result.Check(noPartner != null && noPartner.Code != 0
                    && noPartner.Error == "伙伴不存在",
                    "pr.disMissing=" + (noPartner == null ? "null" : noPartner.Error));
            }
            catch (System.Exception e)
            {
                result.Fail(e.ToString());
            }
            finally
            {
                BattleService.Instance.Exit();
                role.Partners = oldPartners;
                role.PartnerSlots = oldSlots;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                snapshot.Restore();
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        private static CaseResult RunPartnerItem()
        {
            var result = new CaseResult("partner_item");
            BoxSnapshot snapshot = BoxSnapshot.Capture();
            if (snapshot == null)
            {
                result.Fail("当前没有角色");
                return result;
            }

            ISaveProvider oldProvider = SaveCoordinator.Instance.SwapProviderForTest(new NoopSaveProvider());
            LocalRole role = snapshot.Role;
            List<PartnerInfo> oldPartners = role.Partners;
            List<PartnerSlotInfo> oldSlots = role.PartnerSlots;
            List<ItemInfo> oldBag = role.BagData == null ? null : role.BagData.ItemList;
            try
            {
                role.Partners = new List<PartnerInfo>();
                role.PartnerSlots = new List<PartnerSlotInfo>();
                if (role.BagData == null)
                    role.BagData = new Bag { ItemList = new List<ItemInfo>() };
                role.BagData.ItemList = new List<ItemInfo>();

                // 1) 类型判定
                result.Check(ItemService.GetPartnerCfgIdByItem(40001) == 1,
                    "pitem.cfgId=" + ItemService.GetPartnerCfgIdByItem(40001));
                result.Check(ItemService.GetPartnerCfgIdByItem(40034) == 0,
                    "pitem.chipIgnored=" + ItemService.GetPartnerCfgIdByItem(40034));
                result.Check(ItemService.GetPartnerCfgIdByItem(50001) == 0,
                    "pitem.notPet=" + ItemService.GetPartnerCfgIdByItem(50001));

                // 2) 整卡 → 直接变伙伴，背包不留道具
                ItemService.Instance.GrantReward(role, 40001, 1);
                result.Check(ItemService.CountItem(role, 40001) == 0,
                    "pitem.noBagItem=" + ItemService.CountItem(role, 40001));
                bool owned = false;
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    if (role.Partners[i] != null && role.Partners[i].CfgId == 1)
                        owned = true;
                }
                result.Check(owned, "pitem.gotPartner=" + owned);

                // 3) 碎片道具仍然进背包（对照）
                ItemService.Instance.GrantReward(role, 40034, 3);
                result.Check(ItemService.CountItem(role, 40034) == 3,
                    "pitem.chipToBag=" + ItemService.CountItem(role, 40034));

                // 4) 重复获得整卡 → 转碎片（ChipNum: 40034 × 120），伙伴数量不变
                int before = role.Partners.Count;
                ItemService.Instance.GrantReward(role, 40001, 1);
                result.Check(role.Partners.Count == before,
                    "pitem.dupPartners=" + before + "->" + role.Partners.Count);
                result.Check(ItemService.CountItem(role, 40034) == 3 + 120,
                    "pitem.dupChip=" + ItemService.CountItem(role, 40034));
            }
            finally
            {
                role.Partners = oldPartners;
                role.PartnerSlots = oldSlots;
                if (role.BagData != null && oldBag != null)
                    role.BagData.ItemList = oldBag;
                SaveCoordinator.Instance.SwapProviderForTest(oldProvider);
            }
            return result;
        }

        private static CaseResult RunSaveBox()        {
            var result = new CaseResult("save_box");
            long itemUid = 700000100L;
            string error;
            LocalRole role = NewRoleFactory.Create(
                900002L,
                "测试_存档",
                2,
                1,
                ref itemUid,
                out error);
            if (role == null)
            {
                result.Fail("建角失败: " + error);
                return result;
            }

            role.BoxLevel = 4;
            role.TreasuerBox = 11;
            role.BoxOpenItems = AppendItem(new List<ItemInfo>(), MakeTestEquip(1913));

            var temp = new LocalData();
            temp.InitializeEmpty();
            temp.AddRole(role);

            SaveDocument document = SaveDocumentMapper.FromLocalData(temp);
            string json = JsonMapper.ToJson(document);
            SaveDocument parsed = JsonMapper.ToObject<SaveDocument>(json);
            List<LocalRole> restored = SaveDocumentMapper.ToLocalRoles(parsed);
            result.Check(restored != null && restored.Count == 1, "restore.roles=" + (restored == null ? -1 : restored.Count));
            if (restored != null && restored.Count == 1)
            {
                LocalRole r = restored[0];
                result.Check(r.BoxLevel == 4, "restore.level=" + r.BoxLevel);
                result.Check(r.TreasuerBox == 11, "restore.treasure=" + r.TreasuerBox);
                result.Check(r.BoxOpenItems != null && r.BoxOpenItems.Count == 1,
                    "restore.pending=" + (r.BoxOpenItems == null ? -1 : r.BoxOpenItems.Count));
                result.Check(r.Equips != null && r.Equips.Count == 12,
                    "restore.equips=" + (r.Equips == null ? -1 : r.Equips.Count));
            }
            return result;
        }

        private sealed class CaseResult
        {
            public readonly string Name;
            public bool Pass = true;
            public string Error = "";
            public readonly List<string> Checks = new List<string>();

            public CaseResult(string name)
            {
                Name = name;
            }

            public void Check(bool ok, string message)
            {
                Checks.Add((ok ? "PASS " : "FAIL ") + message);
                if (!ok)
                    Pass = false;
            }

            public void Fail(string message)
            {
                Pass = false;
                Error = message;
                Checks.Add("FAIL " + message);
            }
        }

        private sealed class BoxSnapshot
        {
            public LocalData Data;
            public LocalRole Role;
            public int Index;
            public int BoxLevel;
            public long Treasure;
            public long NextItemUid;
            public long Coin;
            public long Exp;
            public long HomeAFKUsedSec;
            public long HomeAFKAnchorUnix;
            public int HomeAFKResetDay;
            public List<ItemInfo> Pending;
            public List<LocalMail> Mails;
            public List<SkillInfo> Skills;
            public int[] SkillSlots;
            public AutoDisassemblyInfo AutoDisassembly;
            public List<TaskInfo> Tasks;
            public int BattleId;
            public int BattleRewardStage;
            public int Level;
            public ItemInfo[] BagItems;

            public static BoxSnapshot Capture()
            {
                LocalData data = LocalData.Instance;
                LocalRole role = data == null ? null : data.Current;
                if (role == null)
                    return null;

                List<ItemInfo> bag = role.BagData == null ? null : role.BagData.ItemList;
                var bagCopy = new ItemInfo[bag == null ? 0 : bag.Count];
                for (int i = 0; i < bagCopy.Length; i++)
                    bagCopy[i] = bag[i];

                return new BoxSnapshot
                {
                    Data = data,
                    Role = role,
                    Index = data.CurrentIdx,
                    BoxLevel = role.BoxLevel,
                    Treasure = role.TreasuerBox,
                    NextItemUid = data.NextItemUid,
                    Coin = role.Coin,
                    Exp = role.Exp,
                    HomeAFKUsedSec = role.HomeAFKUsedSec,
                    HomeAFKAnchorUnix = role.HomeAFKAnchorUnix,
                    HomeAFKResetDay = role.HomeAFKResetDay,
                    Pending = role.BoxOpenItems,
                    Mails = role.Mails,
                    Skills = role.Skills,
                    SkillSlots = role.SkillSlots,
                    AutoDisassembly = role.AutoDisassembly,
                    Tasks = role.Tasks,
                    BattleId = role.BattleId,
                    BattleRewardStage = role.BattleRewardStage,
                    Level = role.Level,
                    BagItems = bagCopy,
                };
            }

            public void Restore()
            {
                Role.BoxLevel = BoxLevel;
                Role.TreasuerBox = Treasure;
                Role.BoxOpenItems = Pending;
                Role.Mails = Mails;
                Role.Skills = Skills;
                Role.SkillSlots = SkillSlots;
                Role.AutoDisassembly = AutoDisassembly;
                Role.Tasks = Tasks;
                Role.BattleId = BattleId;
                Role.BattleRewardStage = BattleRewardStage;
                Role.Level = Level;
                Role.Coin = Coin;
                Role.Exp = Exp;
                Role.HomeAFKUsedSec = HomeAFKUsedSec;
                Role.HomeAFKAnchorUnix = HomeAFKAnchorUnix;
                Role.HomeAFKResetDay = HomeAFKResetDay;
                if (Role.BagData != null)
                    Role.BagData.ItemList = System.Linq.Enumerable.ToList(BagItems);
                Data.NextItemUid = NextItemUid;
                if (Index >= 0)
                    Data.SelectRole(Index);
            }
        }

        private static ItemInfo MakeTestEquip(int cfgId)
        {
            return new ItemInfo
            {
                CfgId = cfgId,
                Uid = 990000000L + cfgId,
                Num = 1,
                Type = ItemType.EQUIPMENT,
                Equip = new EquipInfo
                {
                    Quality = ItemQuality.WHITE,
                    Attributes = new List<Stats>(),
                },
            };
        }

        private static List<ItemInfo> AppendItem(List<ItemInfo> source, ItemInfo item)
        {
            int count = source == null ? 0 : source.Count;
            var items = new ItemInfo[count + 1];
            for (int i = 0; i < count; i++)
                items[i] = source[i];
            items[count] = item;
            return System.Linq.Enumerable.ToList(items);
        }

        private static string ReadScenario()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] != null && args[i].StartsWith("-localTest=", StringComparison.OrdinalIgnoreCase))
                        return args[i].Substring("-localTest=".Length).Trim();
                }
            }

            string flag = Path.Combine(HarnessRoot(), "active_scenario.txt");
            if (File.Exists(flag))
                return File.ReadAllText(flag, Encoding.UTF8).Trim();
            return "none";
        }

        private static void WriteReport(string scenario, bool pass, List<CaseResult> cases)
        {
            try
            {
                string dir = Path.Combine(HarnessRoot(), "reports");
                Directory.CreateDirectory(dir);
                string json = BuildReportJson(scenario, pass, cases);
                string text = BuildReportText(scenario, pass, cases);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.WriteAllText(Path.Combine(dir, "last_report.json"), json, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(dir, scenario + "_" + stamp + ".json"), json, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(dir, "last_report.txt"), text, new UTF8Encoding(false));
                Log.Print("[LocalTest] report=" + Path.Combine(dir, "last_report.json"));
            }
            catch (Exception e)
            {
                Log.PrintError("[LocalTest] 写报告失败: " + e);
            }
        }

        private static string BuildReportText(string scenario, bool pass, List<CaseResult> cases)
        {
            var sb = new StringBuilder();
            sb.AppendLine("scenario=" + scenario);
            sb.AppendLine("pass=" + (pass ? "true" : "false"));
            sb.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            for (int i = 0; i < cases.Count; i++)
            {
                CaseResult c = cases[i];
                sb.AppendLine((c.Pass ? "[PASS] " : "[FAIL] ") + c.Name);
                for (int j = 0; j < c.Checks.Count; j++)
                    sb.AppendLine("  " + c.Checks[j]);
                if (!string.IsNullOrEmpty(c.Error))
                    sb.AppendLine("  error=" + c.Error);
            }
            return sb.ToString();
        }

        private static string BuildReportJson(string scenario, bool pass, List<CaseResult> cases)
        {
            var sb = new StringBuilder();
            sb.Append("{\"scenario\":\"").Append(Escape(scenario))
                .Append("\",\"pass\":").Append(pass ? "true" : "false")
                .Append(",\"cases\":[");
            for (int i = 0; i < cases.Count; i++)
            {
                if (i > 0) sb.Append(',');
                CaseResult c = cases[i];
                sb.Append("{\"name\":\"").Append(Escape(c.Name))
                    .Append("\",\"pass\":").Append(c.Pass ? "true" : "false")
                    .Append(",\"checks\":[");
                for (int j = 0; j < c.Checks.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append('"').Append(Escape(c.Checks[j])).Append('"');
                }
                sb.Append("],\"error\":\"").Append(Escape(c.Error ?? "")).Append("\"}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string HarnessRoot()
        {
            // Assets -> JE_WLY_Clinet -> client -> game_machao
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../../../LocalTestHarness"));
        }

        private sealed class NoopSaveProvider : ISaveProvider
        {
            public SaveLoadResult Load()
            {
                return new SaveLoadResult { Status = SaveLoadStatus.None, Message = "test" };
            }

            public bool Save(SaveDocument document) { return true; }
            public void Delete() { }
            public bool Exists() { return false; }
            public string GetStorageDescription() { return "test-noop"; }
        }
    }
}
#endif
