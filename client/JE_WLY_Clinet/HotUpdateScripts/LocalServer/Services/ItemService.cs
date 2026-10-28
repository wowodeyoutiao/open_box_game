using System.Collections.Generic;
using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Helper;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 背包领域服务：背包、使用、合成、分解、装备穿脱。
    /// </summary>
    public class ItemService : SingletonTemplate<ItemService>
    {
        /// <summary>
        /// 背包。
        /// ⚠️ Bag.ItemList 必须是构造出来的空列表，否则 BagM.SetBagInfo 会空引用
        /// </summary>
        public object GetBag()
        {
            var bag = new Bag();
            bag.ItemList.AddRange(LocalData.Instance.BagData.ItemList);
            return new GetBagResponse { ItemList = bag };
        }

        /// <summary>
        /// 装备穿脱。
        ///
        /// 请求：ChangeEquipRequest { OpType(1=穿/其他=脱), Slot, ItemUid, PartnerSlotId }
        /// 响应：ChangeEquipResponse { Code, Error, OpType, Slot, Item, PartnerSlotId }
        ///
        /// 客户端 Sever_ChangeEquipResponse 会自行调 RoleM.SetRoleOneEquip 更新显示，
        /// 这里必须同步维护 LocalData（权威数据），否则下次推送会把客户端改回去。
        /// </summary>
        public object ChangeEquip(byte[] req)
        {
            ChangeEquipRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<ChangeEquipRequest>(req);
            }
            catch (System.Exception e)
            {
                Log.PrintWarning($"[ItemService] 穿脱请求解析失败: {e.Message}");
                return new ChangeEquipResponse { Code = -1, Error = "请求解析失败" };
            }

            LocalData d = LocalData.Instance;
            d.EnsureInit();
            LocalRole role = d.Current;
            if (role == null)
                return new ChangeEquipResponse { Code = -1, Error = "没有角色" };

            if (request.PartnerSlotId != 0)
                return ChangePartnerEquip(role, request);
            if (role.Equips == null)
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
            if (role.BagData == null)
                role.BagData = new Bag { ItemList = new List<ItemInfo>() };

            bool isOn = request.OpType == 1;
            ItemInfo item = null;
            ItemInfo oldItem = null;
            bool fromBox = false;

            if (isOn)
            {
                // 穿上：先完成所有校验，校验失败不修改背包/宝箱结果。
                item = FindInBag(role.BagData, request.ItemUid);
                if (item == null)
                {
                    item = FindInBox(role, request.ItemUid);
                    fromBox = item != null;
                }
                if (item == null)
                    return new ChangeEquipResponse { Code = 1, Error = "背包和宝箱结果里都没有该装备" };

                string validateError;
                if (!EquipRuleService.TryValidateEquip(role, item, request.Slot, out validateError))
                    return new ChangeEquipResponse { Code = 3, Error = validateError };

                role.Equips.TryGetValue(request.Slot, out oldItem);
                if (fromBox)
                {
                    // 从开箱结果直接穿戴：旧装备回到待领取列表，不进入背包。
                    role.BoxOpenItems.Remove(item);
                    if (oldItem != null)
                        role.BoxOpenItems.Add(oldItem);
                }
                else
                {
                    role.BagData.ItemList.Remove(item);
                    if (oldItem != null)
                        role.BagData.ItemList.Add(oldItem);
                }

                role.Equips[request.Slot] = item;
            }
            else
            {
                // 脱下：先校验容量，失败不修改装备和背包。
                string validateError;
                if (!EquipRuleService.TryValidateUnequip(role, request.Slot, out item, out validateError))
                    return new ChangeEquipResponse { Code = 2, Error = validateError };

                role.Equips.Remove(request.Slot);
                role.BagData.ItemList.Add(item);
            }

            RoleStatService.Instance.RecalculateCurrent();

            Log.Print(
                $"[ItemService] 装备{(isOn ? "穿戴" : "脱下")} slot={request.Slot} uid={item?.Uid} " +
                $"背包剩 {role.BagData.ItemList.Count} 件");

            // 装备变化属于关键进度，立即落盘而不是只等自动保存。
            SaveCoordinator.Instance.SaveNow();

            // 让战力、血条和属性面板立即刷新，不必重登。
            LocalPusher.Instance.PushStatsNow();

            return new ChangeEquipResponse
            {
                Code = 0,
                Error = "",
                OpType = request.OpType,
                Slot = request.Slot,
                Item = item ?? new ItemInfo(),
                OldItem = oldItem ?? (fromBox ? item : new ItemInfo()),
                PartnerSlotId = 0,
            };
        }

        /// <summary>
        /// 伙伴装备穿脱（对齐原服 changeEquip 的 partnerSlotId != 0 分支）。
        ///
        /// 三处与原服一致、容易做错的地方：
        ///   ① 校验用**伙伴**的职业（cfg.Job % 10）和等级，不是玩家的；
        ///   ② 强化/升星/宝石挂在**槽位**上，换装备不清零（原服 Slot 结构就是这样）；
        ///   ③ 同一件装备不能同时穿在玩家和伙伴身上（原服 hasEquipment 两边都查）。
        /// </summary>
        private static ChangeEquipResponse ChangePartnerEquip(LocalRole role, ChangeEquipRequest request)
        {
            var fail = new ChangeEquipResponse
            {
                Code = -1,
                Error = "",
                OpType = request.OpType,
                Slot = request.Slot,
                PartnerSlotId = request.PartnerSlotId,
            };

            if (!PartnerService.IsValidSlot(request.PartnerSlotId))
            {
                fail.Error = "伙伴槽位不合法";
                return fail;
            }

            role.EnsurePartnerEquipSlots();
            PartnerSlotInfo partnerSlot = role.FindPartnerSlot(request.PartnerSlotId);
            if (partnerSlot == null || partnerSlot.PartnerUid <= 0)
            {
                fail.Error = "该槽位没有伙伴";
                return fail;
            }

            PartnerInfo partner = null;
            if (role.Partners != null)
            {
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    if (role.Partners[i] != null && role.Partners[i].Uid == partnerSlot.PartnerUid)
                    {
                        partner = role.Partners[i];
                        break;
                    }
                }
            }
            if (partner == null)
            {
                fail.Error = "伙伴不存在";
                return fail;
            }

            int partnerJob = 0;
            int partnerLevel = partner.Level > 0 ? partner.Level : 1;
            try
            {
                CFGItem_Partner partnerCfg = CFG_Partner.GetById(partner.CfgId);
                if (partnerCfg != null)
                    partnerJob = EquipRuleService.ToInt(partnerCfg.job) % 10;
            }
            catch { partnerJob = 0; }

            int slotIndex = (int)request.Slot;
            if (slotIndex <= (int)EquipSlot.INVALID_EquipSlot
                || slotIndex >= (int)EquipSlot.MaxEquipSlot)
            {
                fail.Error = "装备部位无效";
                return fail;
            }

            SlotInfo target = partnerSlot.Equips[slotIndex];
            if (target == null)
            {
                target = new SlotInfo { Item = null, Gemstone = new int[0] };
                partnerSlot.Equips[slotIndex] = target;
            }

            bool isOn = request.OpType == 1;
            ItemInfo item = null;
            ItemInfo oldItem = target.Item;
            bool fromBox = false;

            if (isOn)
            {
                // 「已被穿戴」必须先判：装备穿上后就不在背包里了，
                // 先查背包会误报"背包和宝箱里都没有该装备"（原服是先 getItemByUid 再 isOnEquip）。
                if (IsEquipped(role, request.ItemUid))
                {
                    fail.Error = "该装备已被穿戴";
                    return fail;
                }

                item = FindInBag(role.BagData, request.ItemUid);
                if (item == null)
                {
                    item = FindInBox(role, request.ItemUid);
                    fromBox = item != null;
                }
                if (item == null)
                {
                    fail.Error = "背包和宝箱结果里都没有该装备";
                    return fail;
                }

                string validateError;
                if (!EquipRuleService.TryValidatePartnerEquip(
                        item, request.Slot, partnerJob, partnerLevel, out validateError))
                {
                    fail.Code = 3;
                    fail.Error = validateError;
                    return fail;
                }

                if (fromBox)
                {
                    role.BoxOpenItems.Remove(item);
                    if (oldItem != null)
                        role.BoxOpenItems.Add(oldItem);
                }
                else
                {
                    role.BagData.ItemList.Remove(item);
                    if (oldItem != null)
                        role.BagData.ItemList.Add(oldItem);
                }
                target.Item = item;
            }
            else
            {
                if (oldItem == null)
                {
                    fail.Error = "该部位没有装备";
                    return fail;
                }
                string bagError;
                if (!EquipRuleService.CanAddToBag(role, oldItem, out bagError))
                {
                    fail.Code = 2;
                    fail.Error = bagError;
                    return fail;
                }
                role.BagData.ItemList.Add(oldItem);
                target.Item = null;
            }

            RoleStatService.Instance.RecalculateCurrent();
            SaveCoordinator.Instance.SaveNow();

            Log.Print("[ItemService] 伙伴装备" + (isOn ? "穿戴" : "脱下")
                + " partnerUid=" + partnerSlot.PartnerUid + " slot=" + slotIndex);

            LocalPusher.Instance.PushPartnerSlot();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushStatsNow();

            return new ChangeEquipResponse
            {
                Code = 0,
                Error = "",
                OpType = request.OpType,
                Slot = request.Slot,
                Item = item ?? oldItem ?? new ItemInfo(),
                OldItem = oldItem ?? new ItemInfo(),
                PartnerSlotId = request.PartnerSlotId,
            };
        }

        /// <summary>
        /// 本地一键最优装备。只比较背包中合法装备与当前部位装备的装备评分，
        /// 所有替换完成后统一重算、保存和推送一次。
        /// </summary>
        public object EquipBest(byte[] req)
        {
            EquipBestRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<EquipBestRequest>(req);
            }
            catch (System.Exception e)
            {
                Log.PrintWarning($"[ItemService] 一键装备请求解析失败: {e.Message}");
                return new CommonResponse
                {
                    Code = -1,
                    Error = "请求解析失败",
                    RequestCmdId = (uint)CmdId.C2S_EquipBest,
                };
            }

            LocalData d = LocalData.Instance;
            d.EnsureInit();
            LocalRole role = d.Current;
            if (role == null)
                return new CommonResponse
                {
                    Code = -1,
                    Error = "没有角色",
                    RequestCmdId = (uint)CmdId.C2S_EquipBest,
                };
            if (request.PartnerSlotId != 0)
                return EquipBestPartner(role, request);
            if (request.Type != ObjectType.PLAYER)
                return new CommonResponse
                {
                    Code = -1,
                    Error = "一键装备对象类型不合法",
                    RequestCmdId = (uint)CmdId.C2S_EquipBest,
                };
            if (role.Equips == null)
                role.Equips = new Dictionary<EquipSlot, ItemInfo>();
            if (role.BagData == null)
                role.BagData = new Bag { ItemList = new List<ItemInfo>() };

            var bestBySlot = new Dictionary<EquipSlot, ItemInfo>();
            var bestPowerBySlot = new Dictionary<EquipSlot, int>();
            if (role.BagData != null && role.BagData.ItemList != null)
            {
                // 复制候选列表，避免替换过程中修改正在遍历的背包。
                var candidates = new List<ItemInfo>(role.BagData.ItemList);
                for (int i = 0; i < candidates.Count; i++)
                {
                    ItemInfo candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    CFGItem_Item cfg;
                    try
                    {
                        cfg = CFG_Item.GetById(candidate.CfgId);
                    }
                    catch
                    {
                        continue;
                    }
                    if (cfg == null || EquipRuleService.ToInt(cfg.Type) != (int)ItemType.EQUIPMENT)
                        continue;

                    int slotValue = EquipRuleService.ToInt(cfg.Equiptype);
                    if (slotValue <= (int)EquipSlot.INVALID_EquipSlot
                        || slotValue >= (int)EquipSlot.MaxEquipSlot)
                        continue;

                    EquipSlot slot = (EquipSlot)slotValue;
                    string error;
                    if (!EquipRuleService.TryValidateEquip(role, candidate, slot, out error))
                        continue;

                    int candidatePower;
                    try
                    {
                        candidatePower = GameUtil.Instance.GetEquipPower(candidate, role.Job);
                    }
                    catch
                    {
                        continue;
                    }

                    int currentPower = 0;
                    ItemInfo current;
                    if (role.Equips != null && role.Equips.TryGetValue(slot, out current) && current != null)
                    {
                        try
                        {
                            currentPower = GameUtil.Instance.GetEquipPower(current, role.Job);
                        }
                        catch
                        {
                            currentPower = 0;
                        }
                    }

                    int bestPower;
                    if (!bestPowerBySlot.TryGetValue(slot, out bestPower) || candidatePower > bestPower)
                    {
                        bestBySlot[slot] = candidate;
                        bestPowerBySlot[slot] = candidatePower;
                    }
                }
            }

            int changed = 0;
            foreach (KeyValuePair<EquipSlot, ItemInfo> kv in bestBySlot)
            {
                ItemInfo current = null;
                if (role.Equips != null && role.Equips.TryGetValue(kv.Key, out current) && current != null)
                {
                    int currentPower;
                    try
                    {
                        currentPower = GameUtil.Instance.GetEquipPower(current, role.Job);
                    }
                    catch
                    {
                        currentPower = 0;
                    }
                    if (bestPowerBySlot[kv.Key] <= currentPower)
                        continue;
                }

                role.BagData.ItemList.Remove(kv.Value);
                if (current != null)
                    role.BagData.ItemList.Add(current);
                role.Equips[kv.Key] = kv.Value;
                changed++;
            }

            if (changed > 0)
            {
                RoleStatService.Instance.RecalculateCurrent();
                SaveCoordinator.Instance.SaveNow();
                LocalPusher.Instance.PushEquipsNow();
                LocalPusher.Instance.PushStatsNow();
            }

            Log.Print("[ItemService] 一键装备完成 changed=" + changed);

            return new CommonResponse
            {
                Code = 0,
                Error = "",
                RequestCmdId = (uint)CmdId.C2S_EquipBest,
            };
        }

        /// <summary>
        /// 伙伴一键最优装备（对齐原服 equipBest 的 partnerSlotID 分支）：
        /// 逐部位挑战力最高的合法装备，比当前槽内装备高就换上。
        /// 每件都走 ChangePartnerEquip，校验/换装/推送逻辑只维护一份。
        /// </summary>
        private static CommonResponse EquipBestPartner(LocalRole role, EquipBestRequest request)
        {
            var resp = new CommonResponse
            {
                Code = 0,
                Error = "",
                RequestCmdId = (uint)CmdId.C2S_EquipBest,
            };
            if (!PartnerService.IsValidSlot(request.PartnerSlotId))
            {
                resp.Code = -1;
                resp.Error = "伙伴槽位不合法";
                return resp;
            }

            role.EnsurePartnerEquipSlots();
            PartnerSlotInfo slot = role.FindPartnerSlot(request.PartnerSlotId);
            if (slot == null || slot.PartnerUid <= 0)
            {
                resp.Code = -1;
                resp.Error = "该槽位没有伙伴";
                return resp;
            }

            PartnerInfo partner = null;
            if (role.Partners != null)
            {
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    if (role.Partners[i] != null && role.Partners[i].Uid == slot.PartnerUid)
                    {
                        partner = role.Partners[i];
                        break;
                    }
                }
            }
            if (partner == null)
            {
                resp.Code = -1;
                resp.Error = "伙伴不存在";
                return resp;
            }

            int partnerJob = 0;
            int partnerLevel = partner.Level > 0 ? partner.Level : 1;
            try
            {
                CFGItem_Partner partnerCfg = CFG_Partner.GetById(partner.CfgId);
                if (partnerCfg != null)
                    partnerJob = EquipRuleService.ToInt(partnerCfg.job) % 10;
            }
            catch { partnerJob = 0; }

            if (role.BagData == null || role.BagData.ItemList == null)
                return resp;

            int changed = 0;
            for (int i = 1; i < LocalRole.PartnerEquipSlotCount; i++)
            {
                EquipSlot equipSlot = (EquipSlot)i;
                ItemInfo best = null;
                int bestPower = 0;
                var candidates = new List<ItemInfo>(role.BagData.ItemList);
                for (int c = 0; c < candidates.Count; c++)
                {
                    ItemInfo candidate = candidates[c];
                    if (candidate == null)
                        continue;

                    CFGItem_Item cfg;
                    try { cfg = CFG_Item.GetById(candidate.CfgId); }
                    catch { continue; }
                    if (cfg == null || EquipRuleService.ToInt(cfg.Type) != (int)ItemType.EQUIPMENT)
                        continue;
                    if (EquipRuleService.ToInt(cfg.Equiptype) != i)
                        continue;
                    if (IsEquipped(role, candidate.Uid))
                        continue;

                    string error;
                    if (!EquipRuleService.TryValidatePartnerEquip(
                            candidate, equipSlot, partnerJob, partnerLevel, out error))
                        continue;

                    int power;
                    try { power = GameUtil.Instance.GetEquipPower(candidate, partnerJob); }
                    catch { continue; }
                    if (best == null || power > bestPower)
                    {
                        best = candidate;
                        bestPower = power;
                    }
                }

                if (best == null)
                    continue;

                int currentPower = 0;
                SlotInfo current = i < slot.Equips.Count ? slot.Equips[i] : null;
                if (current != null && current.Item != null)
                {
                    try { currentPower = GameUtil.Instance.GetEquipPower(current.Item, partnerJob); }
                    catch { currentPower = 0; }
                }
                if (bestPower <= currentPower)
                    continue;

                ChangeEquipResponse equipped = ChangePartnerEquip(role, new ChangeEquipRequest
                {
                    OpType = 1,
                    Slot = equipSlot,
                    ItemUid = best.Uid,
                    PartnerSlotId = request.PartnerSlotId,
                });
                if (equipped != null && equipped.Code == 0)
                    changed++;
            }

            Log.Print("[ItemService] 伙伴一键装备 partnerUid=" + slot.PartnerUid + " changed=" + changed);
            return resp;
        }

        // ==================== 分解 / 回收 ====================

        public object ItemDisassembly(byte[] req)
        {
            ItemDisassemblyRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<ItemDisassemblyRequest>(req);
            }
            catch (System.Exception e)
            {
                return new ItemDisassemblyResponse
                {
                    Code = -1,
                    Error = "分解请求解析失败: " + e.Message,
                };
            }

            var response = new ItemDisassemblyResponse
            {
                Code = 0,
                Error = "",
                ItemUids = request == null ? new long[0] : request.ItemUids,
            };
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Code = -1;
                response.Error = "没有角色";
                return response;
            }
            if (request == null || request.ItemUids == null || request.ItemUids.Length == 0)
                return response;

            // 原服是两段式：先收集 getitem 列表，循环结束后才统一 takeSourceByUidBatch + addSourceBatch。
            // 所以中途遇到「穿戴中的装备」时原服一件都不会少；单机版早期实现是在循环里直接删背包，
            // 第 N 件报错 return 时前 N-1 件已被删、奖励却还没发 ⇒ 物品凭空蒸发。这里同样分两段。
            var targets = new List<ItemInfo>();
            var targetCfgs = new List<CFGItem_Item>();
            for (int i = 0; i < request.ItemUids.Length; i++)
            {
                long uid = request.ItemUids[i];
                if (IsEquipped(role, uid))
                {
                    response.Code = -3;
                    response.Error = "装备穿戴中，不能回收";
                    return response;
                }

                ItemInfo item = FindInBag(role.BagData, uid);
                if (item == null)
                    item = FindInBox(role, uid);
                if (item == null)
                    continue;                 // 原服：取不到该 uid 就 continue，不中断整批

                CFGItem_Item cfg;
                try { cfg = CFG_Item.GetById(item.CfgId); }
                catch { cfg = null; }
                if (cfg == null)
                    continue;

                targets.Add(item);
                targetCfgs.Add(cfg);
            }

            var rewards = new Dictionary<int, long>();
            for (int i = 0; i < targets.Count; i++)
            {
                ItemInfo item = targets[i];
                // 同一个 uid 在请求里出现多次时按件数扣；已扣光的跳过，不能扣成负数。
                if (item.Num <= 0)
                    continue;
                CollectDecompose(targetCfgs[i], rewards);

                // 原服 takeSourceByUidBatch 对每个 uid 只扣 1 件。
                // 早期实现直接整条 Remove ⇒ 分解一叠 5 个强化石只给 1 份奖励、另外 4 个凭空消失。
                item.Num -= 1;
                if (item.Num > 0)
                    continue;
                if (role.BagData != null && role.BagData.ItemList != null
                    && role.BagData.ItemList.Remove(item))
                    continue;
                if (role.BoxOpenItems != null)
                    role.BoxOpenItems.Remove(item);
            }

            ApplyRewards(role, rewards);
            AfterItemChanged(role);
            Log.Print("[ItemService] 分解完成 uid数=" + request.ItemUids.Length + " 实际分解=" + targets.Count);
            return response;
        }

        public object ItemDestroy(byte[] req)
        {
            ItemDestroyRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<ItemDestroyRequest>(req);
            }
            catch (System.Exception e)
            {
                return new ItemDestroyResponse
                {
                    Code = -1,
                    Error = "道具销毁请求解析失败: " + e.Message,
                };
            }

            LocalRole role = LocalData.Instance.Current;
            var response = new ItemDestroyResponse
            {
                Code = 0,
                Error = "",
                Item = new ItemInfo
                {
                    Uid = request == null ? 0 : request.ItemUid,
                    Num = request == null ? 0 : request.Num,
                },
            };
            if (role == null)
            {
                response.Code = -1;
                response.Error = "没有角色";
                return response;
            }
            if (request == null || request.ItemUid <= 0 || request.Num <= 0)
            {
                response.Code = -1;
                response.Error = "参数错误";
                return response;
            }
            if (IsEquipped(role, request.ItemUid))
            {
                response.Code = -3;
                response.Error = "装备穿戴中，不能销毁";
                return response;
            }

            ItemInfo item = FindInBag(role.BagData, request.ItemUid);
            if (item == null || item.Num < request.Num)
            {
                response.Code = -2;
                response.Error = "道具数量不足";
                return response;
            }

            CFGItem_Item cfg;
            try { cfg = CFG_Item.GetById(item.CfgId); }
            catch { cfg = null; }
            if (cfg == null)
            {
                response.Code = -2;
                response.Error = "道具配置不存在";
                return response;
            }

            // 原服 ItemDestroy 走的是「无品质系数」的简单累加（gi.Value += des.Value），
            // 和 ItemDisassembly 那条带 Equipenhances 系数的路径不是同一份规则。
            var rewards = new Dictionary<int, long>();
            CollectDecomposePlain(cfg, rewards);
            if (request.Num > 1)
            {
                var scaled = new Dictionary<int, long>();
                foreach (KeyValuePair<int, long> kv in rewards)
                    scaled[kv.Key] = kv.Value * request.Num;
                rewards = scaled;
            }

            item.Num -= request.Num;
            if (item.Num <= 0)
                role.BagData.ItemList.Remove(item);
            ApplyRewards(role, rewards);
            AfterItemChanged(role);
            return response;
        }

        /// <summary>
        /// 使用道具（对齐原服 Manager.ItemUse）。
        ///
        /// 走 itemuse.json：只有配在这张表里的道具能主动使用，
        /// 效果 = Givegoods（每用一个发一份）。
        /// 不能使用的道具（装备/材料/魂石）返回错误，避免出现"点了没反应"。
        /// </summary>
        public object ItemUse(byte[] req)
        {
            ItemUseRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<ItemUseRequest>(req);
            }
            catch (System.Exception e)
            {
                return new ItemUseResponse
                {
                    Code = -1,
                    Error = "道具使用请求解析失败: " + e.Message,
                };
            }

            LocalRole role = LocalData.Instance.Current;
            var response = new ItemUseResponse { Code = 0, Error = "" };
            if (role == null)
            {
                response.Code = -1;
                response.Error = "没有角色";
                return response;
            }
            if (request == null || request.ItemUid <= 0 || request.Num <= 0)
            {
                response.Code = -1;
                response.Error = "参数错误";
                return response;
            }

            ItemInfo item = FindInBag(role.BagData, request.ItemUid);
            if (item == null || item.Num < request.Num)
            {
                response.Code = -2;
                response.Error = "道具数量不足";
                return response;
            }

            CFGItem_Item cfg;
            try { cfg = CFG_Item.GetById(item.CfgId); }
            catch { cfg = null; }
            if (cfg == null)
            {
                response.Code = -2;
                response.Error = "道具配置不存在";
                return response;
            }

            // 原服：p.level < itemCfg.Levellimit 时拒绝
            if (role.Level < cfg.Levellimit)
            {
                response.Code = -3;
                response.Error = "等级不足";
                return response;
            }

            CFGItem_ItemUse useCfg = CFG_ItemUse.GetById(item.CfgId);
            if (useCfg == null || useCfg.Givegoods == null || useCfg.Givegoods.Length == 0)
            {
                response.Code = -5;
                response.Error = "该道具无法使用";
                return response;
            }

            // 一次性使用多个时，效果按数量翻倍（原服 GiveGoods × useNum）
            var rewards = new Dictionary<int, long>();
            for (int i = 0; i < useCfg.Givegoods.Length; i++)
            {
                CFGItem_ItemUse.GiveGoodsItem give = useCfg.Givegoods[i];
                if (give == null || give.id <= 0 || give.num <= 0)
                    continue;
                long total = (long)give.num * request.Num;
                long exist;
                rewards[give.id] = rewards.TryGetValue(give.id, out exist) ? exist + total : total;
            }
            if (rewards.Count == 0)
            {
                response.Code = -5;
                response.Error = "该道具无法使用";
                return response;
            }

            item.Num -= request.Num;
            if (item.Num <= 0)
                role.BagData.ItemList.Remove(item);

            ApplyRewards(role, rewards);
            AfterItemChanged(role);
            return response;
        }

        public object AutoDisassembly(byte[] req)
        {
            AutoDisassemblyRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<AutoDisassemblyRequest>(req);
            }
            catch (System.Exception e)
            {
                return new AutoDisassemblyResponse
                {
                    Code = -1,
                    Error = "自动分解设置解析失败: " + e.Message,
                };
            }

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return new AutoDisassemblyResponse { Code = -1, Error = "没有角色" };

            int needVip = 0;
            int needLevel = 0;
            try
            {
                if (CFG_XMLConfig.xmlConfig != null && CFG_XMLConfig.xmlConfig.BaseValue != null)
                {
                    needVip = CFG_XMLConfig.xmlConfig.BaseValue.autoDelVipLv;
                    needLevel = CFG_XMLConfig.xmlConfig.BaseValue.autoDelLv;
                }
            }
            catch { }
            if (role.Vip < needVip && role.Level < needLevel)
                return new AutoDisassemblyResponse { Code = -1, Error = "自动分解尚未解锁" };

            if (request == null || request.Info == null)
                return new AutoDisassemblyResponse { Code = -1, Error = "参数错误" };
            if (request.Info.Level <= 0
                || request.Info.Quality <= ItemQuality.INVALID_Quality
                || request.Info.Quality > ItemQuality.RED)
                return new AutoDisassemblyResponse { Code = -1, Error = "参数错误" };

            if (role.AutoDisassembly == null)
                role.AutoDisassembly = new AutoDisassemblyInfo();
            AutoDisassemblyInfo info = role.AutoDisassembly;
            info.Open = request.Info.Open;
            info.Level = request.Info.Level;
            info.Quality = request.Info.Quality;
            info.LevelSwitch = request.Info.LevelSwitch;
            info.QualitySwitch = request.Info.QualitySwitch;
            info.HighCombat = request.Info.HighCombat;

            SaveCoordinator.Instance.MarkDirty();
            LocalPusher.Instance.PushAutoDisassemblyNow();
            return new AutoDisassemblyResponse
            {
                Code = 0,
                Error = "",
                Open = info.Open,
                Level = info.Level,
                Quality = info.Quality,
                LevelSwitch = info.LevelSwitch,
                QualitySwitch = info.QualitySwitch,
                HighCombat = info.HighCombat,
            };
        }

        /// <summary>
        /// 该装备是否已被穿戴。**玩家和伙伴都要查** —— 对齐原服 hasEquipment：
        /// 只查玩家会导致伙伴身上的装备能被分解/销毁，装备凭空消失。
        /// </summary>
        private static bool IsEquipped(LocalRole role, long uid)
        {
            if (role == null)
                return false;
            if (role.Equips != null)
            {
                foreach (KeyValuePair<EquipSlot, ItemInfo> kv in role.Equips)
                {
                    if (kv.Value != null && kv.Value.Uid == uid)
                        return true;
                }
            }
            if (role.PartnerSlots != null)
            {
                for (int i = 0; i < role.PartnerSlots.Count; i++)
                {
                    PartnerSlotInfo slot = role.PartnerSlots[i];
                    if (slot == null || slot.Equips == null)
                        continue;
                    for (int s = 0; s < slot.Equips.Count; s++)
                    {
                        SlotInfo equip = slot.Equips[s];
                        if (equip != null && equip.Item != null && equip.Item.Uid == uid)
                            return true;
                    }
                }
            }
            return false;
        }

        private static void CollectDecompose(CFGItem_Item cfg, Dictionary<int, long> rewards)
        {
            if (cfg == null || cfg.Decompose == null)
                return;

            int quality = cfg.Quality == null ? 0 : (int)cfg.Quality.Value;
            float coefficient = 1f;
            if (quality > 0 && quality <= 7)
            {
                try
                {
                    if (CFG_XMLConfig.xmlConfig != null
                        && CFG_XMLConfig.xmlConfig.EquipEnhanceDic != null
                        && CFG_XMLConfig.xmlConfig.EquipEnhanceDic.ContainsKey(quality))
                        coefficient = CFG_XMLConfig.xmlConfig.EquipEnhanceDic[quality];
                }
                catch { }
            }

            for (int i = 0; i < cfg.Decompose.Length; i++)
            {
                Dictionary<string, int> entry = cfg.Decompose[i];
                if (entry == null) continue;
                foreach (KeyValuePair<string, int> kv in entry)
                {
                    int id;
                    if (!int.TryParse(kv.Key, out id) || id <= 0 || kv.Value <= 0)
                        continue;
                    long num = kv.Value;
                    if (rewards.ContainsKey(id))
                        num = (long)(num * coefficient);
                    if (rewards.ContainsKey(id))
                        rewards[id] += num;
                    else
                        rewards[id] = num;
                }
            }
        }

        /// <summary>
        /// 无品质系数的分解表累加。对齐原服 ItemDestroy 与自动分解内部的 p.ItemDisassembly(itemInfo)：
        /// 那两处都是 `gi.Value += des.Value` 的简单累加，不带 Equipenhances 系数。
        /// </summary>
        private static void CollectDecomposePlain(CFGItem_Item cfg, Dictionary<int, long> rewards)
        {
            if (cfg == null || cfg.Decompose == null || rewards == null)
                return;
            for (int i = 0; i < cfg.Decompose.Length; i++)
            {
                Dictionary<string, int> entry = cfg.Decompose[i];
                if (entry == null) continue;
                foreach (KeyValuePair<string, int> kv in entry)
                {
                    int id;
                    if (!int.TryParse(kv.Key, out id) || id <= 0 || kv.Value <= 0)
                        continue;
                    if (rewards.ContainsKey(id))
                        rewards[id] += kv.Value;
                    else
                        rewards[id] = kv.Value;
                }
            }
        }

        /// <summary>
        /// 把 config.xml 的自动分解出厂开关灌进角色。
        ///
        /// 原服在角色初始化时用 global.GlobalCfg.AutoDecompose 填 ADLevelSwitch / ADQualitySwitch /
        /// ADHighCombat / ADLevel / ADQuality（player.go 里的 InitAutoDisassembly 段），
        /// 单机版之前完全没解析这一段，所以本地角色这几个字段恒为 false/0。
        ///
        /// 只填「从未设置过」的角色：接口 AutoDisassembly 对 Level / Quality 做了强校验
        /// （Level > 0 且 INVALID_Quality < Quality <= RED），所以「Level == 0 或 Quality 非法」
        /// 不可能是玩家保存下来的合法设置，可以安全地按出厂值补齐。
        /// Open 一律保持原值 —— 出厂不自动开启，避免玩家在不知情的情况下把装备碎掉。
        /// </summary>
        public static void ApplyAutoDisassemblyDefaults(LocalRole role)
        {
            if (role == null)
                return;
            if (role.AutoDisassembly == null)
                role.AutoDisassembly = new AutoDisassemblyInfo();
            AutoDisassemblyInfo ad = role.AutoDisassembly;

            if (ad.Level > 0 && ad.Quality > ItemQuality.INVALID_Quality)
                return;

            // ⚠️ 类型名是两层嵌套：CFG_XMLConfig → XML_Config → XML_Item_AutoDecompose。
            //    裸写 XML_Config 在这里解析不到（它不是顶层类型），少一层就是 CS0426。
            CFG_XMLConfig.XML_Config.XML_Item_AutoDecompose cfg = null;
            try
            {
                if (CFG_XMLConfig.xmlConfig != null)
                    cfg = CFG_XMLConfig.xmlConfig.AutoDecompose;
            }
            catch { cfg = null; }
            if (cfg == null)
                return;

            if (cfg.Level > 0)
                ad.Level = cfg.Level;
            if (cfg.Quality > (int)ItemQuality.INVALID_Quality && cfg.Quality <= (int)ItemQuality.RED)
                ad.Quality = (ItemQuality)cfg.Quality;
            ad.LevelSwitch = cfg.LevelSwitch == 1;
            ad.QualitySwitch = cfg.QualitySwitch == 1;
            ad.HighCombat = cfg.KeepBetterSwitch == 1;
        }

        /// <summary>
        /// 掉落自动分解（对齐原服 addSource 里的 ADOpen 分支）。
        ///
        /// 原服只在「掉落来源 reason == ItemAddDrop + 装备(Type 5) + 已开启」时判定：
        ///   LevelSwitch   &amp;&amp; 装备等级限制 &lt;= ADLevel   → 分解
        ///   QualitySwitch &amp;&amp; 装备品质     &lt;= ADQuality → 分解
        ///   HighCombat    &amp;&amp; 新装备战力 &gt; 当前同部位战力 → 不分解（保底，后判覆盖前判）
        /// 宝箱开箱 / 副本结算发的装备**不参与**（原服那两条路径 reason 不是 ItemAddDrop）。
        ///
        /// ⚠️ 现状：monster.json 全表掉落里没有任何 Type=5 装备（装备只从宝箱与副本 Settlement 出），
        ///   所以这条链路是「接线完成、当前不会触发」。保留它是为了和原服语义对齐：
        ///   以后一旦把装备配进掉落表就自动生效。单测直接调本方法验证判定逻辑。
        ///
        /// 返回 true = 已就地分解，调用方不要再把它塞进背包 / 掉落包。
        /// </summary>
        public bool TryAutoDecomposeDrop(LocalRole role, int cfgId, long num)
        {
            if (role == null || cfgId <= 0 || num <= 0)
                return false;

            AutoDisassemblyInfo ad = role.AutoDisassembly;
            if (ad == null || !ad.Open)
                return false;

            CFGItem_Item cfg;
            try { cfg = CFG_Item.GetById(cfgId); }
            catch { cfg = null; }
            if (cfg == null || EquipRuleService.ToInt(cfg.Type) != (int)ItemType.EQUIPMENT)
                return false;

            int level = EquipRuleService.ToInt(cfg.Levellimit);
            int quality = cfg.Quality == null ? 0 : (int)cfg.Quality.Value;

            bool decompose = false;
            if (ad.LevelSwitch && level <= ad.Level)
                decompose = true;
            if (ad.QualitySwitch && quality > 0 && quality <= (int)ad.Quality)
                decompose = true;
            if (!decompose)
                return false;

            if (ad.HighCombat && IsBetterThanEquipped(role, cfg))
                return false;

            var rewards = new Dictionary<int, long>();
            CollectDecomposePlain(cfg, rewards);
            if (rewards.Count == 0)
                return false;                  // 没配分解表：不拦，正常进包

            if (num > 1)
            {
                var scaled = new Dictionary<int, long>();
                foreach (KeyValuePair<int, long> kv in rewards)
                    scaled[kv.Key] = kv.Value * num;
                rewards = scaled;
            }

            ApplyRewards(role, rewards);
            Log.Print("[ItemService] 自动分解 cfgId=" + cfgId + " num=" + num);
            return true;
        }

        /// <summary>
        /// 该装备的战力是否高于当前同部位已穿戴装备（原服 ADHighCombat 保底判定）。
        /// 部位为空时视为「更高」——原服拿 nil 装备算战力得 0，任何装备都比它强，所以不分解。
        /// </summary>
        private static bool IsBetterThanEquipped(LocalRole role, CFGItem_Item cfg)
        {
            int slotValue = EquipRuleService.ToInt(cfg.Equiptype);
            if (slotValue <= (int)EquipSlot.INVALID_EquipSlot || slotValue >= (int)EquipSlot.MaxEquipSlot)
                return false;                  // 部位非法：没法比较，按「不比身上好」处理

            ItemInfo current = null;
            if (role.Equips != null)
                role.Equips.TryGetValue((EquipSlot)slotValue, out current);
            if (current == null)
                return true;

            int newPower;
            int curPower;
            try
            {
                var temp = new ItemInfo
                {
                    CfgId = cfg.ID,
                    Num = 1,
                    Type = ItemType.EQUIPMENT,
                    Equip = new EquipInfo
                    {
                        Quality = ToQuality(EquipRuleService.ToInt(cfg.Quality)),
                        Attributes = new List<Stats>(),
                    },
                };
                newPower = GameUtil.Instance.GetEquipPower(temp, role.Job);
            }
            catch { newPower = 0; }
            try { curPower = GameUtil.Instance.GetEquipPower(current, role.Job); }
            catch { curPower = 0; }
            return newPower > curPower;
        }

        private static void ApplyRewards(LocalRole role, Dictionary<int, long> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return;
            foreach (KeyValuePair<int, long> kv in rewards)
            {
                int id = kv.Key;
                long num = kv.Value;
                if (id <= 0 || num <= 0) continue;
                if (id == 1)
                {
                    role.Coin += num;
                    LocalData.Instance.Gold = role.Coin;
                }
                else if (id == 2)
                {
                    LevelUpService.Instance.AddExp(role, num);
                    LocalData.Instance.Exp = role.Exp;
                    LocalData.Instance.Level = role.Level;
                }
                else if (id == 14)
                {
                    role.TreasuerBox += num;
                    LocalData.Instance.TreasuerBox = role.TreasuerBox;
                }
                else
                {
                    AddBagItem(role, id, num);
                }
            }
        }

        public bool GrantReward(LocalRole role, int cfgId, long num)
        {
            if (role == null || cfgId <= 0 || num <= 0)
                return false;
            if (cfgId == 1)
            {
                role.Coin += num;
                LocalData.Instance.Gold = role.Coin;
                return true;
            }
            if (cfgId == 2)
            {
                LevelUpService.Instance.AddExp(role, num);
                LocalData.Instance.Exp = role.Exp;
                LocalData.Instance.Level = role.Level;
                return true;
            }
            if (cfgId == 14)
            {
                role.TreasuerBox += num;
                LocalData.Instance.TreasuerBox = role.TreasuerBox;
                return true;
            }
            switch (cfgId)
            {
                case 4:
                    role.Soul += num;
                    LocalData.Instance.Soul = role.Soul;
                    return true;
                case 5:
                    role.Coupon += num;
                    LocalData.Instance.Coupon = role.Coupon;
                    return true;
                case 8:
                    role.TrialVoucher += num;
                    LocalData.Instance.TrialVoucher = role.TrialVoucher;
                    return true;
                case 99:
                    role.BoxQuick += num;
                    LocalData.Instance.BoxQuick = role.BoxQuick;
                    return true;
            }
            // 战宠道具（item.Type = 9，如 50001）**不落背包，直接变成宠物**：
            // 对齐原服 addSource 的 `itemCfg.MainType == constant.ItemPet → addPet` 分支。
            if (IsPetItem(cfgId))
                return PetService.Instance.GrantPetByItem(role, cfgId, num);

            // 伙伴整卡（item.Type = 8，如 40001）**不落背包，直接变成伙伴**：
            // 对齐原服 addSource 的 `itemCfg.MainType == constant.ItemPartner → addPartner` 分支。
            // 注意伙伴**碎片**（Type = 7）是正常进背包的，它靠「伙伴合成」消耗，别一起拦掉。
            int partnerCfgId = GetPartnerCfgIdByItem(cfgId);
            if (partnerCfgId > 0)
            {
                int partnerCount = num > 20 ? 20 : (int)num;
                for (int i = 0; i < partnerCount; i++)
                    PartnerService.Instance.AddPartner(role, partnerCfgId);
                return true;
            }

            AddBagItem(role, cfgId, num);
            return true;
        }

        /// <summary>
        /// 伙伴整卡道具（item.json 的 Type = 8，原服 constant.ItemPartner）对应的伙伴配置 ID。
        /// 返回 0 表示不是伙伴整卡（含伙伴碎片 Type = 7，那类要正常进背包）。
        /// </summary>
        public static int GetPartnerCfgIdByItem(int cfgId)
        {
            if (cfgId <= 0)
                return 0;
            try
            {
                CFGItem_Item itemCfg = CFG_Item.GetById(cfgId);
                if (itemCfg == null || EquipRuleService.ToInt(itemCfg.Type) != 8)
                    return 0;
                // ⚠️ 必须用 GetByItemCfgID（比的是伙伴配置的 ItemID = 整卡道具 id）。
                //    CFG_Partner.GetByItemID 看着像「按道具找伙伴」，实际比的是 ChipGoodID
                //    （**碎片**道具 id），用错会永远返回 null。
                CFGItem_Partner partnerCfg = CFG_Partner.GetByItemCfgID(cfgId);
                return partnerCfg == null ? 0 : partnerCfg.ID;
            }
            catch { return 0; }
        }

        /// <summary>
        /// 是否是「战宠道具」（item.json 的 Type = 9，原服 constant.ItemPet）。
        /// 这类道具进背包时会直接变成宠物，见 GrantReward。
        /// </summary>
        public static bool IsPetItem(int cfgId)
        {
            if (cfgId <= 0)
                return false;
            try
            {
                CFGItem_Item cfg = CFG_Item.GetById(cfgId);
                // Type 枚举：0 不进包 / 1..5 普通道具与装备 / 6 魂石 / 7 伙伴碎片 / 8 伙伴整卡 / 9 战宠
                return cfg != null && EquipRuleService.ToInt(cfg.Type) == 9;
            }
            catch { return false; }
        }

        /// <summary>
        /// 指定道具的数量（伙伴升星等消耗校验用）。
        /// id=1 金币 / id=14 宝箱不在背包里，单独取角色字段。
        /// </summary>
        public static long CountItem(LocalRole role, int cfgId)
        {
            if (role == null)
                return 0;
            // 货币类不落在背包里，走角色字段（对齐客户端 BagM.GetNumByCfgID）
            switch (cfgId)
            {
                case 1: return role.Coin;          // Gold 金币
                case 2: return role.Exp;           // EXP
                case 4: return role.Soul;          // ZhanHun 战魂
                case 5: return role.Coupon;        // DianQuan 点券/元宝
                case 8: return role.TrialVoucher;  // SLPZ 试炼积分
                case 14: return role.TreasuerBox;
                case 99: return role.BoxQuick;     // JSJZ
            }
            if (role.BagData == null || role.BagData.ItemList == null)
                return 0;
            long total = 0;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item != null && item.CfgId == cfgId)
                    total += item.Num;
            }
            return total;
        }

        /// <summary>扣除道具，数量不足返回 false 且不做任何修改。</summary>
        public static bool ConsumeItem(LocalRole role, int cfgId, long num)
        {
            if (role == null || num <= 0)
                return false;
            if (CountItem(role, cfgId) < num)
                return false;

            switch (cfgId)
            {
                case 1:
                    role.Coin -= num;
                    LocalData.Instance.Gold = role.Coin;
                    return true;
                case 4:
                    role.Soul -= num;
                    LocalData.Instance.Soul = role.Soul;
                    return true;
                case 5:
                    role.Coupon -= num;
                    LocalData.Instance.Coupon = role.Coupon;
                    return true;
                case 8:
                    role.TrialVoucher -= num;
                    LocalData.Instance.TrialVoucher = role.TrialVoucher;
                    return true;
                case 14:
                    role.TreasuerBox -= num;
                    LocalData.Instance.TreasuerBox = role.TreasuerBox;
                    return true;
                case 99:
                    role.BoxQuick -= num;
                    LocalData.Instance.BoxQuick = role.BoxQuick;
                    return true;
            }

            long left = num;
            for (int i = role.BagData.ItemList.Count - 1; i >= 0 && left > 0; i--)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item == null || item.CfgId != cfgId)
                    continue;
                if (item.Num <= left)
                {
                    left -= item.Num;
                    role.BagData.ItemList.RemoveAt(i);
                }
                else
                {
                    item.Num -= left;
                    left = 0;
                }
            }
            return true;
        }

        private static void AddBagItem(LocalRole role, int cfgId, long num)
        {
            if (role.BagData == null)
                role.BagData = new Bag { ItemList = new List<ItemInfo>() };
            if (role.BagData.ItemList == null)
                role.BagData.ItemList = new List<ItemInfo>();

            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo old = role.BagData.ItemList[i];
                if (old != null && old.CfgId == cfgId && !EquipRuleService.IsEquipment(old))
                {
                    old.Num += num;
                    return;
                }
            }

            CFGItem_Item cfg;
            try { cfg = CFG_Item.GetById(cfgId); }
            catch { return; }
            if (cfg == null) return;
            int type = EquipRuleService.ToInt(cfg.Type);
            var item = new ItemInfo
            {
                CfgId = cfgId,
                Uid = LocalData.Instance.AllocateItemUid(),
                Num = num,
                CreateTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Type = (ItemType)type,
            };
            if (type == 5)
            {
                item.Equip = new EquipInfo
                {
                    Quality = ToQuality(EquipRuleService.ToInt(cfg.Quality)),
                    Attributes = new List<Stats>(),
                };
            }
            role.BagData.ItemList.Add(item);
        }

        private static ItemQuality ToQuality(int value)
        {
            if (value < (int)ItemQuality.WHITE) return ItemQuality.WHITE;
            if (value > (int)ItemQuality.Rainbow) return ItemQuality.Rainbow;
            return (ItemQuality)value;
        }

        private static void AfterItemChanged(LocalRole role)
        {
            if (role == null) return;
            RoleStatService.Instance.Recalculate(role, true);
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushStatsNow();
        }

        private static ItemInfo FindInBag(Bag bag, long uid)
        {
            if (bag == null || bag.ItemList == null)
                return null;
            for (int i = 0; i < bag.ItemList.Count; i++)
            {
                if (bag.ItemList[i].Uid == uid)
                    return bag.ItemList[i];
            }
            return null;
        }

        private static ItemInfo FindInBox(LocalRole role, long uid)
        {
            if (role == null || role.BoxOpenItems == null)
                return null;
            for (int i = 0; i < role.BoxOpenItems.Count; i++)
            {
                if (role.BoxOpenItems[i] != null && role.BoxOpenItems[i].Uid == uid)
                    return role.BoxOpenItems[i];
            }
            return null;
        }
    }
}
