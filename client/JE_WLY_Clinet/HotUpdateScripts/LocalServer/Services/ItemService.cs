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
                return new ChangeEquipResponse { Code = -1, Error = "单机版暂不支持伙伴装备" };
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
            if (request.Type != ObjectType.PLAYER || request.PartnerSlotId != 0)
                return new CommonResponse
                {
                    Code = -1,
                    Error = "单机版暂不支持伙伴一键装备",
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
        /// ⚠️ 直接构造对象：data 必须是空数组而非 null，否则 UI 遍历崩
        /// </summary>
        public HTTP_StoreListResponse GetStoreList()
        {
            return new HTTP_StoreListResponse
            {
                code = 0,
                data = new HTTP_StoreList[0],
            };
        }
    }
}
