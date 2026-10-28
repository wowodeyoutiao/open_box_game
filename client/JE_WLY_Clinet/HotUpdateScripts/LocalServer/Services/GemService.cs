using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 魂石镶嵌 / 拆卸。对齐原服 Player.genstoneChange：
    ///
    ///   1) 槽位校验：GemstoneSlot 下标 0..3（客户端 SelectIdx 直接用下标）
    ///   2) 伙伴装备不支持（单人版直接拒绝，伙伴装备不走本地属性结算）
    ///   3) 开槽数：config.xml 的 Diamond/openlevel/stage（num = 槽位序号, level = 需要等级）
    ///      取所有「等级已满足」里最大的 num，下标 >= canSlot 就是没开
    ///   4) 镶嵌：校验背包有这颗魂石 → 校验 DiamondPosition 含该装备部位
    ///            → 槽位已有魂石先退回背包 → 扣 1 颗 → 写入槽位
    ///   5) 拆卸：槽位为空原服直接 return nil（Code=0）→ 否则退回背包 → 清 0
    ///   6) 无论镶嵌还是拆卸都 refreshStats（属性在 RoleStatService 里结算）
    ///
    /// 注意：请求里的 ItemUid 在客户端发的其实是**道具配置 ID**
    /// （UIJewelSelect 传 selectItem.CfgId），这里同样按配置 ID 处理。
    /// </summary>
    public class GemService : SingletonTemplate<GemService>
    {
        /// <summary>穿(1)脱(2)魂石。</summary>
        public GemstoneChangeResponse Change(GemstoneChangeRequest req)
        {
            var response = new GemstoneChangeResponse
            {
                Code = -1,
                OpType = req != null ? req.OpType : 0,
                Slot = req != null ? req.Slot : EquipSlot.INVALID_EquipSlot,
                GemstoneSlot = req != null ? req.GemstoneSlot : GemstoneSlot.INVALID_GemstoneSlot,
                ItemUid = req != null ? req.ItemUid : 0,
                PartnerSlotId = req != null ? req.PartnerSlotId : 0,
            };

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "没有角色";
                return response;
            }

            int slotValue = (int)response.Slot;
            if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                || slotValue >= (int)EquipSlot.MaxEquipSlot)
            {
                response.Error = "装备槽位不合法";
                return response;
            }
            if (response.PartnerSlotId != 0)
            {
                // 原服 genstoneChange 的 partnerslotid 分支：equipsInfo 指向伙伴槽的 Slot
                if (!PartnerService.IsValidSlot(response.PartnerSlotId)
                    || ResolvePartner(role, response.PartnerSlotId) == null)
                {
                    response.Error = "该槽位没有伙伴";
                    return response;
                }
            }

            int gemIndex = (int)response.GemstoneSlot;
            if (gemIndex < 0 || gemIndex >= LocalRole.GemSlotCount)
            {
                response.Error = "魂石槽位不合法";
                return response;
            }

            int[] gems = ResolveGemArray(role, response.Slot, response.PartnerSlotId);
            if (gems == null)
            {
                response.Error = "该部位没有装备";
                return response;
            }

            if (response.OpType == 1)
                return Inlay(response, role, gemIndex, gems);
            if (response.OpType == 2)
                return Remove(response, role, gemIndex, gems);

            response.Error = "魂石操作类型不合法";
            return response;
        }

        /// <summary>
        /// 取某个装备位的魂石数组（长度 4）。
        /// 玩家：role.EquipGems[slot]；伙伴：PartnerSlot.Equips[slot].Gemstone（原服 Slot.Gemstone）。
        /// 返回 null 表示该部位没有装备（调用方报"该部位没有装备"）。
        /// 返回的是**内部数组引用**，直接改元素即生效。
        /// </summary>
        private static int[] ResolveGemArray(LocalRole role, EquipSlot slot, int partnerSlotId)
        {
            if (role == null)
                return null;

            if (partnerSlotId == 0)
            {
                if (role.Equips == null || !role.Equips.ContainsKey(slot)
                    || role.Equips[slot] == null)
                    return null;
                return role.GetEquipGems(slot);
            }

            PartnerSlotInfo partnerSlot = role.FindPartnerSlot(partnerSlotId);
            if (partnerSlot == null || partnerSlot.PartnerUid <= 0 || partnerSlot.Equips == null)
                return null;
            int index = (int)slot;
            if (index <= (int)EquipSlot.INVALID_EquipSlot || index >= partnerSlot.Equips.Count)
                return null;
            SlotInfo equip = partnerSlot.Equips[index];
            if (equip == null || equip.Item == null)
                return null;

            if (equip.Gemstone == null || equip.Gemstone.Length != LocalRole.GemSlotCount)
            {
                var gems = new int[LocalRole.GemSlotCount];
                if (equip.Gemstone != null)
                {
                    int copy = equip.Gemstone.Length < LocalRole.GemSlotCount
                        ? equip.Gemstone.Length : LocalRole.GemSlotCount;
                    System.Array.Copy(equip.Gemstone, gems, copy);
                }
                equip.Gemstone = gems;
            }
            return equip.Gemstone;
        }

        /// <summary>伙伴槽上的伙伴（无效返回 null）。</summary>
        private static PartnerInfo ResolvePartner(LocalRole role, int partnerSlotId)
        {
            if (role == null)
                return null;
            PartnerSlotInfo partnerSlot = role.FindPartnerSlot(partnerSlotId);
            if (partnerSlot == null || partnerSlot.PartnerUid <= 0)
                return null;
            return PartnerService.Instance.FindPartnerByUid(role, partnerSlot.PartnerUid);
        }

        /// <summary>
        /// 魂石槽开启数按**装备所有者**的等级算：玩家 → 玩家等级；伙伴 → 伙伴等级
        /// （原服 genstoneChangeAll 里 `delLevel = p.getPartnerInfoBySlotId(...).level`）。
        /// </summary>
        private static int ResolveOwnerLevel(LocalRole role, int partnerSlotId)
        {
            if (role == null)
                return 0;
            if (partnerSlotId == 0)
                return role.Level;
            PartnerInfo partner = ResolvePartner(role, partnerSlotId);
            return partner == null ? 0 : (partner.Level > 0 ? partner.Level : 1);
        }

        /// <summary>穿上魂石（原服 genstoneChange opType == 1）。</summary>
        private GemstoneChangeResponse Inlay(GemstoneChangeResponse response, LocalRole role,
            int gemIndex, int[] gems)
        {
            int canSlot = GetOpenGemSlotCount(ResolveOwnerLevel(role, response.PartnerSlotId));
            if (gemIndex >= canSlot)
            {
                response.Error = "魂石槽位未开启";
                return response;
            }

            int gemCfgId = response.ItemUid;
            if (gemCfgId <= 0 || ItemService.CountItem(role, gemCfgId) <= 0)
            {
                response.Error = "魂石数量不足";
                return response;
            }

            CFGItem_Item gemCfg;
            try { gemCfg = CFG_Item.GetById(gemCfgId); }
            catch { gemCfg = null; }
            if (gemCfg == null)
            {
                response.Error = "魂石数量不足";
                return response;
            }

            // 该魂石能不能镶到这个部位（item.json 的 DiamondPosition）
            bool can = false;
            if (gemCfg.DiamondPosition != null)
            {
                int slotValue = (int)response.Slot;
                for (int i = 0; i < gemCfg.DiamondPosition.Length; i++)
                {
                    if (gemCfg.DiamondPosition[i] == slotValue)
                    {
                        can = true;
                        break;
                    }
                }
            }
            if (!can)
            {
                response.Error = "该部位不能镶嵌此魂石";
                return response;
            }

            int[] innerGems = gems;
            // 原服：槽位已有宝石先把旧宝石退回背包，再扣新宝石
            if (innerGems[gemIndex] > 0)
                ItemService.Instance.GrantReward(role, innerGems[gemIndex], 1);
            ItemService.ConsumeItem(role, gemCfgId, 1);
            innerGems[gemIndex] = gemCfgId;

            Commit(role);
            response.Code = 0;
            Log.Print("[GemService] inlay slot=" + (int)response.Slot + " gemSlot=" + gemIndex
                + " cfg=" + gemCfgId);
            return response;
        }

        /// <summary>拆下魂石（原服 genstoneChange opType == 2）。空槽原服直接返回成功。</summary>
        private GemstoneChangeResponse Remove(GemstoneChangeResponse response, LocalRole role,
            int gemIndex, int[] gems)
        {
            if (gems[gemIndex] <= 0)
            {
                // 原服：equipsInfo.Gemstone[stoneSlot] == 0 → return nil（Code=0）
                response.Code = 0;
                return response;
            }

            int gemCfgId = gems[gemIndex];
            ItemService.Instance.GrantReward(role, gemCfgId, 1);
            gems[gemIndex] = 0;

            Commit(role);
            response.Code = 0;
            Log.Print("[GemService] remove slot=" + (int)response.Slot + " gemSlot=" + gemIndex
                + " cfg=" + gemCfgId + " partner=" + response.PartnerSlotId);
            return response;
        }

        /// <summary>镶嵌/拆卸后的统一收尾：重算属性、存档、推送。</summary>
        private static void Commit(LocalRole role)
        {
            RoleStatService.Instance.Recalculate(role, false);

            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushStatsNow();
            LocalPusher.Instance.PushEquipsNow();
        }

        /// <summary>
        /// 一键镶嵌，对齐原服 Player.genstoneChangeAll。
        ///
        /// 和单颗镶嵌是两套逻辑，不是「循环调用单颗镶嵌」：
        ///   1) 从背包把「DiamondPosition 含该部位」的魂石按**堆叠数量展开**成一条条候选
        ///      （一条 = 一颗，Num 存的是魂石等级 itemlevel）
        ///   2) 身上 4 个槽位整理成 old[]（空槽 {0,0}）
        ///   3) 循环 canSlot 次：每次取等级最高的候选，在 old 里找**第一个等级更低**的
        ///      已开槽位（j &lt; canSlot）做交换；旧宝石退回背包，并重新丢回候选池
        ///
        /// 三个照抄原服的点：
        ///   · 只换「等级更高」的，同级不换（`newInfo.Num > olditem.Num`）
        ///   · 每次只处理一个槽位就 break，下一轮重新取最高候选（所以一个槽位可能被多次升级）
        ///   · 原服有一行 `int32(olditem.Num) >= newInfo.ID`：拿「魂石等级 1~15」比
        ///     「魂石 ID 10001+」，恒为 false，等于没有判断。这里**保留原样**保证行为一致。
        ///
        /// 原服每换一颗都 refreshStats（那是为了上报埋点），本地没有埋点，
        /// 循环结束统一重算一次，最终属性结果相同。
        /// </summary>
        public GemstoneChangeAllResponse ChangeAll(GemstoneChangeAllRequest req)
        {
            var response = new GemstoneChangeAllResponse
            {
                Code = -1,
                Error = "",
                Slot = req != null ? req.Slot : EquipSlot.INVALID_EquipSlot,
                PartnerSlotId = req != null ? req.PartnerSlotId : 0,
                GemStoneSlot = new int[0],
            };

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "没有角色";
                return response;
            }

            int slotValue = (int)response.Slot;
            if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                || slotValue >= (int)EquipSlot.MaxEquipSlot)
            {
                response.Error = "装备槽位不合法";
                return response;
            }
            if (response.PartnerSlotId != 0
                && (!PartnerService.IsValidSlot(response.PartnerSlotId)
                    || ResolvePartner(role, response.PartnerSlotId) == null))
            {
                response.Error = "该槽位没有伙伴";
                return response;
            }

            int[] allGems = ResolveGemArray(role, response.Slot, response.PartnerSlotId);
            if (allGems == null)
            {
                response.Error = "该部位没有装备";
                return response;
            }

            // ① 背包候选池：按堆叠数量展开（一条 = 一颗）
            var candidates = new List<GemCandidate>();
            if (role.BagData != null && role.BagData.ItemList != null)
            {
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    ItemInfo item = role.BagData.ItemList[i];
                    if (item == null || item.CfgId <= 0 || item.Num <= 0)
                        continue;
                    CFGItem_Item cfg;
                    try { cfg = CFG_Item.GetById(item.CfgId); }
                    catch { cfg = null; }
                    if (cfg == null || cfg.DiamondPosition == null || cfg.DiamondPosition.Length == 0)
                        continue;
                    bool match = false;
                    for (int d = 0; d < cfg.DiamondPosition.Length; d++)
                    {
                        if (cfg.DiamondPosition[d] == slotValue)
                        {
                            match = true;
                            break;
                        }
                    }
                    if (!match)
                        continue;
                    int level = (int)cfg.itemlevel;
                    for (int n = 0; n < item.Num; n++)
                        candidates.Add(new GemCandidate { Id = item.CfgId, Level = level, FromBag = true });
                }
            }

            int canSlot = GetOpenGemSlotCount(ResolveOwnerLevel(role, response.PartnerSlotId));
            int[] gems = allGems;

            // ② 身上 4 槽整理成 old[]
            var old = new List<GemCandidate>();
            for (int i = 0; i < LocalRole.GemSlotCount; i++)
            {
                int oldId = gems[i];
                if (oldId > 0)
                {
                    int oldLevel = 0;
                    CFGItem_Item oldCfg;
                    try { oldCfg = CFG_Item.GetById(oldId); }
                    catch { oldCfg = null; }
                    if (oldCfg != null)
                        oldLevel = (int)oldCfg.itemlevel;
                    old.Add(new GemCandidate { Id = oldId, Level = oldLevel, FromBag = false });
                }
                else
                {
                    old.Add(new GemCandidate { Id = 0, Level = 0, FromBag = false });
                }
            }

            // ③ 逐个槽位轮换
            for (int round = 1; round <= canSlot; round++)
            {
                if (candidates.Count == 0)
                    continue;
                GemCandidate best = TakeBest(candidates);
                if (best == null)
                    continue;

                for (int j = 0; j < old.Count; j++)
                {
                    GemCandidate oldItem = old[j];
                    if (best.Level <= oldItem.Level)
                        continue;

                    // 原服笔误保留：等级(1~15) 与 宝石ID(10001+) 比较，恒 false
                    if (oldItem.Level >= best.Id)
                        continue;
                    if (j >= canSlot)
                        continue;

                    if (oldItem.Id != 0)
                    {
                        // 旧宝石退回背包，并重新丢回候选池（所以它可能被再次镶到别的槽位）
                        ItemService.Instance.GrantReward(role, oldItem.Id, 1);
                        ItemService.ConsumeItem(role, best.Id, 1);
                        RemoveCandidate(candidates, best.Id);
                        candidates.Add(new GemCandidate
                        {
                            Id = oldItem.Id,
                            Level = oldItem.Level,
                            FromBag = true,
                        });
                    }
                    else
                    {
                        ItemService.ConsumeItem(role, best.Id, 1);
                        RemoveCandidate(candidates, best.Id);
                    }

                    gems[j] = best.Id;
                    old[j] = new GemCandidate { Id = best.Id, Level = best.Level, FromBag = false };
                    break;
                }
            }

            RoleStatService.Instance.Recalculate(role, false);
            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushStatsNow();
            LocalPusher.Instance.PushEquipsNow();

            // 原服回包是 4 个槽位的最终结果
            var result = new int[LocalRole.GemSlotCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = gems[i];
            response.GemStoneSlot = result;
            response.Code = 0;
            Log.Print("[GemService] changeAll slot=" + slotValue + " canSlot=" + canSlot
                + " -> " + result[0] + "," + result[1] + "," + result[2] + "," + result[3]);
            return response;
        }

        /// <summary>一键镶嵌的候选（一条 = 一颗魂石）。</summary>
        private sealed class GemCandidate
        {
            public int Id;        // 魂石配置 ID，0 = 空槽
            public int Level;     // itemlevel
            public bool FromBag;  // true = 来自背包/回收，false = 身上
        }

        /// <summary>
        /// 取等级最高的候选（对齐原服 topFourByField(items,1,1)：按 Num 降序、同 Num 按 Key 降序，
        /// 取前 1 个）。**只排序取值、不删除元素** —— 原服这个函数也只返回切片前 N 个，
        /// 删除是调用方后面显式 `append(canDell[:k], canDell[k+1:]...)` 做的。
        /// 早先这里写成「取出即删除」再加 RemoveCandidate，导致每轮多吃掉一颗候选
        /// （3 颗宝石只镶上 2 颗），测试 gem_all.fillOrder 抓出来的。
        /// </summary>
        private static GemCandidate TakeBest(List<GemCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;
            GemCandidate best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (best == null || candidates[i].Level > best.Level)
                    best = candidates[i];
            }
            return best;
        }

        /// <summary>从候选池移除一颗指定配置的魂石（一个条目代表一颗）。</summary>
        private static void RemoveCandidate(List<GemCandidate> candidates, int cfgId)
        {
            if (candidates == null)
                return;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Id == cfgId)
                {
                    candidates.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 当前等级开几个魂石槽（对齐原服 canSlot 计算）：
        ///   遍历 Diamond/openlevel/stage，等级 >= stage.level 的取最大的 stage.num。
        /// </summary>
        public static int GetOpenGemSlotCount(int level)
        {
            int canSlot = 0;
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config == null || config.Diamond_OpenlvDic == null)
                    return 0;
                foreach (System.Collections.Generic.KeyValuePair<int, int> kv in config.Diamond_OpenlvDic)
                {
                    // key = 槽位序号(num)，value = 开启等级
                    if (level >= kv.Value && kv.Key > canSlot)
                        canSlot = kv.Key;
                }
            }
            catch
            {
                return 0;
            }
            return canSlot;
        }
    }
}
