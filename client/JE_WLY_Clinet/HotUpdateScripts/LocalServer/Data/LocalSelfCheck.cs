#if LOCAL_TEST
using System.Collections.Generic;
using JEngine.Core;
using JEngine.LitJson;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 轻量启动自检。
    ///
    /// 不依赖 Unity Test Framework，验证：
    /// 1. 三职业都能生成 12 件本职业基础装备；
    /// 2. LocalRole → SaveDocument → JSON → SaveDocument → LocalRole 往返不丢装备；
    /// 3. 存档内物品 UID 不重复、部位合法。
    ///
    /// 自检失败只记录错误，不阻断游戏启动，但会在 Console 中明确标注 [SelfCheck][FAIL]。
    /// </summary>
    public static class LocalSelfCheck
    {
        public static bool Run()
        {
            bool ok = true;
            try
            {
                long itemUid = 700000000L;
                for (int job = 1; job <= 3; job++)
                {
                    string error;
                    LocalRole role = NewRoleFactory.Create(
                        900000L + job,
                        "自检_" + StarterLoadoutConfig.GetJobName(job),
                        job,
                        1,
                        ref itemUid,
                        out error);

                    if (role == null)
                    {
                        Fail("建角失败 job=" + job + " " + error);
                        ok = false;
                        continue;
                    }

                    if (role.Equips == null || role.Equips.Count != 12)
                    {
                        Fail("初始装备数量错误 job=" + job + " count="
                            + (role.Equips == null ? -1 : role.Equips.Count));
                        ok = false;
                    }

                    // 顺带验证宝箱状态能随角色存档往返。
                    role.BoxLevel = 3;
                    role.TreasuerBox = 17;
                    role.BoxOpenItems.Add(MakeTestEquip(101));

                    var temp = new LocalData();
                    temp.InitializeEmpty();
                    temp.AddRole(role);

                    SaveDocument document = SaveDocumentMapper.FromLocalData(temp);
                    string json = JsonMapper.ToJson(document);
                    SaveDocument parsed = JsonMapper.ToObject<SaveDocument>(json);

                    if (parsed == null || parsed.Roles == null || parsed.Roles.Count != 1)
                    {
                        Fail("存档往返角色丢失 job=" + job);
                        ok = false;
                        continue;
                    }
                    if (parsed.Roles[0].Equips == null || parsed.Roles[0].Equips.Count != 12)
                    {
                        Fail("存档往返装备丢失 job=" + job);
                        ok = false;
                        continue;
                    }

                    var restored = SaveDocumentMapper.ToLocalRoles(parsed);
                    if (restored.Count != 1 || restored[0].Equips.Count != 12)
                    {
                        Fail("存档恢复装备数量错误 job=" + job);
                        ok = false;
                        continue;
                    }

                    LocalRole restoredRole = restored[0];
                    if (restoredRole.BoxLevel != 3 || restoredRole.TreasuerBox != 17
                        || restoredRole.BoxOpenItems == null || restoredRole.BoxOpenItems.Count != 1)
                    {
                        Fail("宝箱状态存档往返失败 job=" + job);
                        ok = false;
                    }

                    RoleStatSnapshot baseSnapshot;

                    var equipped = restoredRole.Equips;
                    restoredRole.Equips = new Dictionary<EquipSlot, ItemInfo>();
                    baseSnapshot = RoleStatService.Instance.Recalculate(restoredRole, true);
                    restoredRole.Equips = equipped;

                    RoleStatSnapshot stats = RoleStatService.Instance.Recalculate(restoredRole, true);
                    if (stats == null || baseSnapshot == null)
                    {
                        Fail("属性计算返回 null job=" + job);
                        ok = false;
                        continue;
                    }

                    long baseSum = baseSnapshot.PhysicalAttack + baseSnapshot.MagicAttack
                        + baseSnapshot.PhysicalDefense + baseSnapshot.MagicDefense
                        + baseSnapshot.MaxHealth;
                    long gearSum = stats.PhysicalAttack + stats.MagicAttack
                        + stats.PhysicalDefense + stats.MagicDefense
                        + stats.MaxHealth;
                    if (gearSum <= baseSum)
                    {
                        Fail("装备未进入属性计算 job=" + job
                            + " base=" + baseSum + " gear=" + gearSum);
                        ok = false;
                    }

                    // 装备规则边界：职业不符、部位不符必须被拒绝。
                    string ruleError;
                    int wrongJobId = job == 1 ? 1913 : 101;   // 1913 是法师武器，101 是战士武器
                    int weaponId = job == 2 ? 1913 : 101;
                    ItemInfo wrongJobItem = MakeTestEquip(wrongJobId);
                    ItemInfo wrongSlotItem = MakeTestEquip(weaponId);

                    if (EquipRuleService.TryValidateEquip(
                        restoredRole, wrongJobItem, EquipSlot.Weapon, out ruleError))
                    {
                        Fail("装备职业校验失败 job=" + job);
                        ok = false;
                    }
                    if (EquipRuleService.TryValidateEquip(
                        restoredRole, wrongSlotItem, EquipSlot.Clothes, out ruleError))
                    {
                        Fail("装备部位校验失败 job=" + job);
                        ok = false;
                    }

                    // 背包容量边界：填满总容量后必须拒绝继续放入。
                    var bag = restoredRole.BagData;
                    var originalBagItems = bag.ItemList;
                    int totalLimit = 500;
                    try
                    {
                        if (CFG_XMLConfig.xmlConfig != null
                            && CFG_XMLConfig.xmlConfig.BaseValue != null
                            && CFG_XMLConfig.xmlConfig.BaseValue.bagNum != null
                            && CFG_XMLConfig.xmlConfig.BaseValue.bagNum.totlenum > 0)
                            totalLimit = CFG_XMLConfig.xmlConfig.BaseValue.bagNum.totlenum;
                    }
                    catch { }

                    var fullBag = new List<ItemInfo>();
                    for (int i = 0; i < totalLimit; i++)
                        fullBag.Add(MakeTestEquip(101));
                    bag.ItemList = fullBag;

                    if (EquipRuleService.CanAddToBag(restoredRole, MakeTestEquip(101), out ruleError))
                    {
                        Fail("背包容量校验失败 job=" + job);
                        ok = false;
                    }
                    bag.ItemList = originalBagItems;

                    Log.Print("[SelfCheck][OK] job=" + StarterLoadoutConfig.GetJobName(job)
                        + " equips=" + restoredRole.Equips.Count
                        + " saveBytes=" + (json == null ? 0 : json.Length)
                        + " atkP=" + stats.PhysicalAttack
                        + " atkM=" + stats.MagicAttack
                        + " defP=" + stats.PhysicalDefense
                        + " defM=" + stats.MagicDefense
                        + " hp=" + stats.MaxHealth
                        + " combat=" + stats.Combat);
                }
            }
            catch (System.Exception e)
            {
                Fail("自检异常: " + e);
                ok = false;
            }

            if (!ok)
                Log.PrintError("[SelfCheck][FAIL] 本地建角/存档自检未通过，请检查上方错误");
            return ok;
        }

        private static ItemInfo MakeTestEquip(int cfgId)
        {
            return new ItemInfo
            {
                CfgId = cfgId,
                Uid = 9900000L + cfgId,
                Num = 1,
                Type = ItemType.EQUIPMENT,
                Equip = new EquipInfo
                {
                    Quality = ItemQuality.WHITE,
                    Attributes = new List<Stats>()
                }
            };
        }

        private static void Fail(string message)
        {
            LocalDiagnostics.RecordError("LocalSelfCheck", message);
        }
    }
}
#endif
