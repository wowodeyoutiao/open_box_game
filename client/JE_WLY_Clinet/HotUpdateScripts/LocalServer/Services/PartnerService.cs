using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 伙伴领域服务。
    ///
    /// 对应原服 partner.go / player.go 的伙伴部分：
    /// - addPartner：重复获得转碎片，新建伙伴并按星级解锁技能
    /// - partnerCombat / partnerRest / partnerReplace：上阵、下阵、替换
    /// - partnerStar：升星（消耗 StarConsume，升星后解锁新技能）
    /// - 上阵伙伴的属性会算进主角（原服 refreshStats 里遍历 partnerSlots）
    ///
    /// 单机取舍：
    /// - 槽位固定 MaxPartnerSlots = 4（对齐原服 MaxPartnerNum），不按 VIP 卡；
    /// - 伙伴装备 / 魂石 / 强化 / 升星 / 合成 / 分解 / 洗练均已接入。
    /// </summary>
    public class PartnerService : SingletonTemplate<PartnerService>
    {
        /// <summary>伙伴槽位数量，对齐原服 MaxPartnerNum = 4。</summary>
        public const int MaxPartnerSlots = 4;

        /// <summary>
        /// 首个伙伴槽位编号。**槽位是 1..4，不是 0..3**：
        /// - 原服 Manager.PartnerCombat 校验 `req.SlotId < 1 || req.SlotId > 4`；
        /// - 客户端 UIRole 的 TeamIdx 0 = 主角、1..4 = 伙伴槽位；
        /// - UIExpPool / UIPartnerUp 都按 1..4 取 GetOnePartnerSlotInfo。
        /// </summary>
        public const int FirstPartnerSlotId = 1;

        /// <summary>
        /// 道具 id + 数量。原服 global.IdNum 的本地等价物，
        /// 只用于分解/洗练内部聚合奖励，不走协议。
        /// </summary>
        private sealed class IdNum
        {
            public int Id;
            public long Num;
        }

        /// <summary>槽位编号是否合法（1..MaxPartnerSlots）。</summary>
        public static bool IsValidSlot(int slotId)
        {
            return slotId >= FirstPartnerSlotId && slotId <= MaxPartnerSlots;
        }

        /// <summary>
        /// 该槽位需要的主角等级。配置在 config.xml 的 openslot：
        ///   1 → 20 级 / 2 → 28 级 / 3 → 35 级 / 4 → 40 级
        /// 读不到配置时返回 0（不限制）。
        /// </summary>
        public static int GetSlotNeedLevel(int slotId)
        {
            try { return CFG_XMLConfig.GetOpenSoltLv(slotId); }
            catch { return 0; }
        }

        /// <summary>当前等级已解锁的槽位数量（原服用它派发 UNLOCK_PARTNER_SLOT）。</summary>
        public int CountUnlockedSlots(LocalRole role)
        {
            if (role == null)
                return 0;
            int count = 0;
            for (int slotId = FirstPartnerSlotId; slotId <= MaxPartnerSlots; slotId++)
            {
                if (role.Level >= GetSlotNeedLevel(slotId))
                    count++;
            }
            return count;
        }

        /// <summary>槽位是否已解锁（等级够）。</summary>
        public bool IsSlotUnlocked(LocalRole role, int slotId)
        {
            return role != null && role.Level >= GetSlotNeedLevel(slotId);
        }

        /// <summary>伙伴列表（登录成功后客户端会立刻请求）</summary>
        public object GetList()
        {
            LocalData d = LocalData.Instance;
            LocalRole role = d.Current;
            EnsureSlots(role);
            var resp = new PartnerListResponse { Code = 0, Error = "" };
            if (d.Partners != null && d.Partners.Count > 0)
                resp.List.AddRange(d.Partners);
            return resp;
        }

        /// <summary>伙伴槽位信息（客户端主动拉取）</summary>
        public object GetSlotInfo()
        {
            LocalRole role = LocalData.Instance.Current;
            var resp = new PartnerSlotInfoResponse { Code = 0, Error = "" };
            if (role == null)
            {
                resp.Code = -1;
                resp.Error = "没有角色";
                return resp;
            }

            EnsureSlots(role);
            for (int i = 0; i < role.PartnerSlots.Count; i++)
            {
                PartnerSlotInfo slot = role.PartnerSlots[i];
                if (slot == null)
                    continue;
                var info = new PartnerSlotInfo
                {
                    SlotId = slot.SlotId,
                    PartnerUid = slot.PartnerUid,
                    Equips = new List<SlotInfo>(),
                };
                if (slot.Equips != null)
                {
                    for (int s = 0; s < slot.Equips.Count; s++)
                    {
                        SlotInfo equip = slot.Equips[s];
                        info.Equips.Add(equip == null
                            ? new SlotInfo { Gemstone = new int[0] }
                            : new SlotInfo
                            {
                                Item = equip.Item,
                                Enhance = equip.Enhance,
                                Stars = equip.Stars,
                                Gemstone = equip.Gemstone == null ? new int[0] : (int[])equip.Gemstone.Clone(),
                            });
                    }
                }
                resp.List.Add(info);
            }
            return resp;
        }

        /// <summary>伙伴上阵</summary>
        public CommonResponse Combat(PartnerCombatRequest req)
        {
            var resp = NewResponse(CmdId.C2S_PartnerCombat);
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                resp.Error = "没有角色";
                return resp;
            }
            EnsureSlots(role);

            // 原服 Manager.PartnerCombat：p.desk != nil → IllegalInBattle
            if (BattleService.Instance.InBattle)
            {
                resp.Error = "战斗中不能更换伙伴";
                return resp;
            }

            if (req == null)
            {
                resp.Error = "上阵请求为空";
                return resp;
            }

            // 原服 Manager.PartnerCombat 先校验 SlotId 范围，再校验槽位解锁等级
            if (!IsValidSlot(req.SlotId))
            {
                resp.Error = "槽位编号不合法";
                return resp;
            }
            if (!IsSlotUnlocked(role, req.SlotId))
            {
                resp.Error = "槽位未解锁（需要 " + GetSlotNeedLevel(req.SlotId) + " 级）";
                return resp;
            }
            PartnerSlotInfo slot = FindSlot(role, req.SlotId);
            if (slot == null)
            {
                resp.Error = "槽位不存在";
                return resp;
            }
            if (slot.PartnerUid > 0)
            {
                resp.Error = "槽位已被占用";
                return resp;
            }

            PartnerInfo partner = FindPartner(role, req.PartnerUid);
            if (partner == null)
            {
                resp.Error = "伙伴不存在";
                return resp;
            }

            // 原服 partnerCombat：伙伴不能重复上阵，同配置伙伴也不能同时上阵
            for (int i = 0; i < role.PartnerSlots.Count; i++)
            {
                PartnerSlotInfo other = role.PartnerSlots[i];
                if (other == null || other.PartnerUid <= 0)
                    continue;
                if (other.PartnerUid == req.PartnerUid)
                {
                    resp.Error = "该伙伴已经上阵";
                    return resp;
                }
                PartnerInfo otherPartner = FindPartner(role, other.PartnerUid);
                if (otherPartner != null && otherPartner.CfgId == partner.CfgId)
                {
                    resp.Error = "同一个伙伴不能重复上阵";
                    return resp;
                }
            }

            slot.PartnerUid = req.PartnerUid;
            Refresh(role);
            resp.Code = 0;
            return resp;
        }

        /// <summary>伙伴下阵</summary>
        public CommonResponse Rest(PartnerRestRequest req)
        {
            var resp = NewResponse(CmdId.C2S_PartnerRest);
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                resp.Error = "没有角色";
                return resp;
            }
            EnsureSlots(role);

            if (BattleService.Instance.InBattle)
            {
                resp.Error = "战斗中不能更换伙伴";
                return resp;
            }

            if (req == null)
            {
                resp.Error = "下阵请求为空";
                return resp;
            }

            PartnerSlotInfo slot = FindSlot(role, req.SlotId);
            if (slot == null || slot.PartnerUid <= 0)
            {
                resp.Error = "该槽位没有伙伴";
                return resp;
            }

            PartnerInfo partner = FindPartner(role, slot.PartnerUid);
            string error;
            // 原服 partnerRest：先校验装备背包够不够，再逐件脱下，最后清技能槽
            if (!TakeOffAllEquips(role, slot, out error))
            {
                resp.Error = error;
                return resp;
            }
            ClearPartnerSkillSlots(role, partner);

            slot.PartnerUid = 0;
            Refresh(role);
            LocalPusher.Instance.PushPartnerSlot();
            LocalPusher.Instance.PushBagInfoNow();
            resp.Code = 0;
            return resp;
        }

        /// <summary>
        /// 把伙伴身上的装备全部脱回背包（原服 partnerRest / PartnerReplace 的下阵前置处理）。
        /// ⚠️ 不脱的话：槽位清空后装备既不在背包也不在身上，还会被 IsEquipped 拦住分解 —— **永久丢失**。
        /// 槽位的强化/升星/宝石保留（原服 changeEquip(2) 只把 ItemId 置 0）。
        /// 校验失败时不修改任何数据。
        /// </summary>
        private static bool TakeOffAllEquips(LocalRole role, PartnerSlotInfo slot, out string error,
            bool checkCapacity = true)
        {
            error = "";
            if (role == null || slot == null || slot.Equips == null)
                return true;
            if (role.BagData == null)
                role.BagData = new Bag { ItemList = new List<ItemInfo>() };

            int count = 0;
            for (int i = 1; i < slot.Equips.Count; i++)
            {
                SlotInfo equip = slot.Equips[i];
                if (equip != null && equip.Item != null)
                    count++;
            }
            if (count == 0)
                return true;

            // 原服：equipnum > p.bagEquipLeftCount() → ErrBagFull
            if (checkCapacity && !EquipRuleService.CanAddEquipCount(role, count, out error))
                return false;

            for (int i = 1; i < slot.Equips.Count; i++)
            {
                SlotInfo equip = slot.Equips[i];
                if (equip == null || equip.Item == null)
                    continue;
                role.BagData.ItemList.Add(equip.Item);
                equip.Item = null;
            }
            return true;
        }

        /// <summary>
        /// 下阵时把该伙伴的技能从技能槽里清掉（原服 partnerRest 的"自动下阵伙伴技能"）。
        /// 不清的话技能槽会残留一个已经不在场上的伙伴技能。
        /// </summary>
        private static void ClearPartnerSkillSlots(LocalRole role, PartnerInfo partner)
        {
            if (role == null || partner == null)
                return;
            if (partner.Skills == null || partner.Skills.Count == 0)
                return;
            if (role.SkillSlots == null)
                return;

            bool changed = false;
            for (int s = 0; s < role.SkillSlots.Length; s++)
            {
                if (role.SkillSlots[s] <= 0)
                    continue;
                for (int k = 0; k < partner.Skills.Count; k++)
                {
                    SkillInfo skill = partner.Skills[k];
                    if (skill != null && skill.CfgId == role.SkillSlots[s])
                    {
                        role.SkillSlots[s] = 0;
                        changed = true;
                        break;
                    }
                }
            }
            if (changed)
                LocalPusher.Instance.PushSkillSlotNow();
        }

        /// <summary>伙伴替换：直接把槽位换成指定伙伴（原槽位伙伴自动下阵）</summary>
        public CommonResponse Replace(PartnerReplaceRequest req)
        {
            var resp = NewResponse(CmdId.C2S_PartnerReplace);
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                resp.Error = "没有角色";
                return resp;
            }
            EnsureSlots(role);

            if (BattleService.Instance.InBattle)
            {
                resp.Error = "战斗中不能更换伙伴";
                return resp;
            }

            if (req == null)
            {
                resp.Error = "替换请求为空";
                return resp;
            }

            if (!IsValidSlot(req.SlotId))
            {
                resp.Error = "槽位编号不合法";
                return resp;
            }
            if (!IsSlotUnlocked(role, req.SlotId))
            {
                resp.Error = "槽位未解锁（需要 " + GetSlotNeedLevel(req.SlotId) + " 级）";
                return resp;
            }
            PartnerSlotInfo slot = FindSlot(role, req.SlotId);
            if (slot == null)
            {
                resp.Error = "槽位不存在";
                return resp;
            }

            PartnerInfo partner = FindPartner(role, req.PartnerUid);
            if (partner == null)
            {
                resp.Error = "伙伴不存在";
                return resp;
            }

            // 同配置伙伴不能同时上阵
            for (int i = 0; i < role.PartnerSlots.Count; i++)
            {
                PartnerSlotInfo other = role.PartnerSlots[i];
                if (other == null || other.SlotId == slot.SlotId || other.PartnerUid <= 0)
                    continue;
                PartnerInfo otherPartner = FindPartner(role, other.PartnerUid);
                if (otherPartner != null && otherPartner.CfgId == partner.CfgId)
                {
                    resp.Error = "同一个伙伴不能重复上阵";
                    return resp;
                }
            }

            // 原服 PartnerReplace = 先走一遍"下阵"（脱光装备 + 清技能槽）再上阵新伙伴。
            // 要腾空的槽位：本槽的旧伙伴 + 目标伙伴可能已在的其他槽。
            var toRest = new List<PartnerSlotInfo>();
            if (slot.PartnerUid > 0 && slot.PartnerUid != req.PartnerUid)
                toRest.Add(slot);
            for (int i = 0; i < role.PartnerSlots.Count; i++)
            {
                PartnerSlotInfo other = role.PartnerSlots[i];
                if (other == null || other.SlotId == slot.SlotId)
                    continue;
                if (other.PartnerUid == req.PartnerUid)
                    toRest.Add(other);
            }

            // 先按合计件数校验一次背包空间，避免脱一半失败
            int equipNeed = 0;
            for (int i = 0; i < toRest.Count; i++)
            {
                PartnerSlotInfo s = toRest[i];
                if (s.Equips == null)
                    continue;
                for (int k = 1; k < s.Equips.Count; k++)
                {
                    if (s.Equips[k] != null && s.Equips[k].Item != null)
                        equipNeed++;
                }
            }
            string bagError;
            if (equipNeed > 0 && !EquipRuleService.CanAddEquipCount(role, equipNeed, out bagError))
            {
                resp.Error = bagError;
                return resp;
            }

            for (int i = 0; i < toRest.Count; i++)
            {
                PartnerSlotInfo s = toRest[i];
                string error;
                // 已在上面按总数校验过，这里不再逐槽校验，避免重复计数误判
                TakeOffAllEquips(role, s, out error, false);
                ClearPartnerSkillSlots(role, FindPartner(role, s.PartnerUid));
                s.PartnerUid = 0;
            }

            slot.PartnerUid = req.PartnerUid;
            Refresh(role);
            LocalPusher.Instance.PushPartnerSlot();
            LocalPusher.Instance.PushBagInfoNow();
            resp.Code = 0;
            return resp;
        }

        /// <summary>伙伴升星：消耗 StarConsume，升星后按星级解锁新技能</summary>
        public CommonResponse Star(PartnerStarRequest req)
        {
            var resp = NewResponse(CmdId.C2S_PartnerStar);
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                resp.Error = "没有角色";
                return resp;
            }

            if (req == null)
            {
                resp.Error = "升星请求为空";
                return resp;
            }

            PartnerInfo partner = FindPartner(role, req.PartnerUid);
            if (partner == null)
            {
                resp.Error = "伙伴不存在";
                return resp;
            }

            CFGItem_Partner cfg = GetPartnerCfg(partner.CfgId);
            if (cfg == null)
            {
                resp.Error = "伙伴配置不存在";
                return resp;
            }

            int nextStar = partner.Star + 1;
            CFGItem_Partner.StarConsumeItem consume = FindStarConsume(cfg, nextStar);
            if (consume == null || consume.values == null || consume.values.Length == 0)
            {
                resp.Error = "已经升到最高星级";
                return resp;
            }

            // 先校验再扣，避免扣了材料却没升上去
            for (int i = 0; i < consume.values.Length; i++)
            {
                CFGItem_Partner.StarConsumeItem.Item need = consume.values[i];
                if (need == null || need.key <= 0 || need.value <= 0)
                    continue;
                if (ItemService.CountItem(role, need.key) < need.value)
                {
                    resp.Error = "升星材料不足";
                    return resp;
                }
            }
            for (int i = 0; i < consume.values.Length; i++)
            {
                CFGItem_Partner.StarConsumeItem.Item need = consume.values[i];
                if (need == null || need.key <= 0 || need.value <= 0)
                    continue;
                ItemService.ConsumeItem(role, need.key, need.value);
            }

            partner.Star = nextStar;
            LearnSkills(partner);

            Refresh(role);
            LocalPusher.Instance.PushBagInfoNow();
            resp.Code = 0;
            return resp;
        }

        // ==================== 伙伴合成 / 分解 / 洗练 ====================
        // 对齐原服 player.go partnerCompose / partnerDisassemble / partnerReforge。
        // 注意原服命名有坑：「分解」和「洗练」都不是把伙伴删掉，
        // 而是把该伙伴**重置回 1 级 0 星**，退还此前投入的星级材料与经验。

        /// <summary>
        /// 伙伴合成：对齐原服 partnerCompose。消耗 ChipNum 碎片换整卡。
        /// 已经拥有该配置的伙伴时原服直接报 ErrPartnerInvalid（不会重复给）。
        /// </summary>
        public CommonResponse Compose(PartnerComposeRequest req)
        {
            var resp = NewResponse(CmdId.C2S_PartnerCompose);
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                resp.Error = "没有角色";
                return resp;
            }
            if (req == null || req.PartnerCfgId <= 0)
            {
                resp.Error = "伙伴配置不合法";
                return resp;
            }

            int cfgId = (int)req.PartnerCfgId;
            CFGItem_Partner cfg = GetPartnerCfg(cfgId);
            if (cfg == null)
            {
                resp.Error = "伙伴配置不存在";
                return resp;
            }

            // 原服：遍历 p.partners 只要有同 cfg.Cid 就返回 ErrPartnerInvalid
            if (role.Partners != null)
            {
                for (int i = 0; i < role.Partners.Count; i++)
                {
                    PartnerInfo owned = role.Partners[i];
                    if (owned != null && owned.CfgId == cfgId)
                    {
                        resp.Error = "已经拥有该伙伴";
                        return resp;
                    }
                }
            }

            if (cfg.ChipNum == null || cfg.ChipNum.Length == 0)
            {
                resp.Error = "该伙伴没有配置碎片需求";
                return resp;
            }

            // 先全量校验再扣，避免扣了一半材料却合成失败
            for (int i = 0; i < cfg.ChipNum.Length; i++)
            {
                CFGItem_Partner.StarConsumeItem.Item chip = cfg.ChipNum[i];
                if (chip == null || chip.key <= 0 || chip.value <= 0)
                    continue;
                if (ItemService.CountItem(role, chip.key) < chip.value)
                {
                    resp.Error = "伙伴碎片不足";
                    return resp;
                }
            }
            for (int i = 0; i < cfg.ChipNum.Length; i++)
            {
                CFGItem_Partner.StarConsumeItem.Item chip = cfg.ChipNum[i];
                if (chip == null || chip.key <= 0 || chip.value <= 0)
                    continue;
                ItemService.ConsumeItem(role, chip.key, chip.value);
            }

            int added = AddPartner(role, cfgId);
            if (added != 1)
            {
                // 理论上到不了（上面已排除重复），真到了说明 AddPartner 内部异常，
                // 此时材料已扣，必须回滚，否则玩家白丢碎片。
                for (int i = 0; i < cfg.ChipNum.Length; i++)
                {
                    CFGItem_Partner.StarConsumeItem.Item chip = cfg.ChipNum[i];
                    if (chip == null || chip.key <= 0 || chip.value <= 0)
                        continue;
                    ItemService.Instance.GrantReward(role, chip.key, chip.value);
                }
                resp.Error = "伙伴合成失败";
                return resp;
            }

            resp.Code = 0;
            Refresh(role);
            LocalPusher.Instance.PushBagInfoNow();
            Log.Print("[PartnerService] compose cfg=" + cfgId);
            return resp;
        }

        /// <summary>
        /// 伙伴分解：对齐原服 partnerDisassemble。
        /// 门槛 Quality / Starlevel（config.xml 的 partener/decompose/stage，当前 7 / 5）。
        /// 退还：星级材料（碎片类按 Rate% 折算成通用碎片 id，非碎片原样退）+ 升级经验珠。
        /// 之后把伙伴重置成 1 级 0 星、卸下装备、重算技能。
        /// </summary>
        public CommonResponse Disassemble(PartnerDisassembleRequest req)
        {
            var resp = NewResponse(CmdId.C2S_PartnerDisassemble);
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                resp.Error = "没有角色";
                return resp;
            }
            if (req == null || req.PartnerUid <= 0)
            {
                resp.Error = "伙伴不合法";
                return resp;
            }

            PartnerInfo partner = FindPartner(role, req.PartnerUid);
            if (partner == null)
            {
                resp.Error = "伙伴不存在";
                return resp;
            }
            CFGItem_Partner cfg = GetPartnerCfg(partner.CfgId);
            if (cfg == null)
            {
                resp.Error = "伙伴配置不存在";
                return resp;
            }

            CFG_XMLConfig.XML_Config.XML_Item_Decompose dec = GetDecomposeCfg();
            if (dec == null)
            {
                resp.Error = "伙伴分解配置缺失";
                return resp;
            }
            if (cfg.Quality < dec.quality)
            {
                resp.Error = "该品质伙伴不可分解（需品质 " + dec.quality + "）";
                return resp;
            }
            if (partner.Star < dec.starlevel)
            {
                resp.Error = "星级不足，无法分解（需 " + dec.starlevel + " 星）";
                return resp;
            }

            // 装备必须先能全部收回背包，否则重置后装备会滞留在空槽（原服 changeEquip 会失败）
            if (!CanTakeOffAllEquips(role, partner, out string bagError))
            {
                resp.Error = bagError;
                return resp;
            }

            // 星级材料：碎片类按 Rate% 折算成通用碎片，其它原样退
            List<IdNum> rewards = CollectStarConsumeItems(cfg, partner.Star);
            for (int i = 0; i < rewards.Count; i++)
            {
                IdNum one = rewards[i];
                if (!IsPartnerFragment(one.Id))
                    continue;
                long num = CeilPercent(one.Num, dec.rate);
                if (num > 0)
                {
                    // 原服把这一项**累加**到通用碎片 id 上，而不是替换
                    rewards[i] = new IdNum { Id = one.Id, Num = 0 };
                    AddIdNum(rewards, dec.id, num);
                }
            }

            // 升级投入的经验 → 经验珠
            long exp = CFG_Role.GetAllNeedExpToUpLevel(cfg.job, partner.Level);
            List<IdNum> expItems = ExpToItems(exp);
            for (int i = 0; i < expItems.Count; i++)
                AddIdNum(rewards, expItems[i].Id, expItems[i].Num);

            GrantIdNums(role, rewards);
            ResetPartner(role, partner);

            resp.Code = 0;
            Refresh(role);
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushSkillSlotNow();
            Log.Print("[PartnerService] disassemble uid=" + partner.Uid + " cfg=" + partner.CfgId
                + " rewardKinds=" + rewards.Count);
            return resp;
        }

        /// <summary>
        /// 伙伴洗练（重铸）：对齐原服 partnerReforge。
        /// 免费重铸只退 Forge.Free.Rate%（当前 70）；付费重铸按品质扣 Charge 道具后退全额。
        /// 与分解的区别：星级材料**不做碎片折算**，原样退还。
        /// </summary>
        public CommonResponse Reforge(PartnerReforgeRequest req)
        {
            var resp = NewResponse(CmdId.C2S_PartnerReforge);
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                resp.Error = "没有角色";
                return resp;
            }
            if (req == null || req.PartnerUid <= 0)
            {
                resp.Error = "伙伴不合法";
                return resp;
            }

            PartnerInfo partner = FindPartner(role, req.PartnerUid);
            if (partner == null)
            {
                resp.Error = "伙伴不存在";
                return resp;
            }
            if (partner.Level <= 1)
            {
                resp.Error = "伙伴等级为 1 级，无需洗练";
                return resp;
            }
            CFGItem_Partner cfg = GetPartnerCfg(partner.CfgId);
            if (cfg == null)
            {
                resp.Error = "伙伴配置不存在";
                return resp;
            }

            if (!CanTakeOffAllEquips(role, partner, out string bagError))
            {
                resp.Error = bagError;
                return resp;
            }

            // 付费洗练：按伙伴品质扣对应道具（config.xml partener/forge/charge）
            int chargeId = 0;
            int chargeNum = 0;
            if (!req.IsFree)
            {
                // 手写遍历，不用 TryGetValue + ValueTuple out（ILRuntime 对元组 out 参数不友好）
                Dictionary<int, (int id, int num)> charges = GetForgeChargeDict();
                if (charges != null)
                {
                    foreach (KeyValuePair<int, (int id, int num)> kv in charges)
                    {
                        if (kv.Key != cfg.Quality)
                            continue;
                        chargeId = kv.Value.id;
                        chargeNum = kv.Value.num;
                        break;
                    }
                }
                if (chargeId > 0 && chargeNum > 0
                    && ItemService.CountItem(role, chargeId) < chargeNum)
                {
                    resp.Error = "洗练消耗道具不足";
                    return resp;
                }
                if (chargeId > 0 && chargeNum > 0)
                    ItemService.ConsumeItem(role, chargeId, chargeNum);
            }

            List<IdNum> rewards = CollectStarConsumeItems(cfg, partner.Star);
            long exp = CFG_Role.GetAllNeedExpToUpLevel(cfg.job, partner.Level);

            if (req.IsFree)
            {
                int rate = GetForgeFreeRate();
                for (int i = 0; i < rewards.Count; i++)
                    rewards[i] = new IdNum { Id = rewards[i].Id, Num = CeilPercent(rewards[i].Num, rate) };
                exp = CeilPercent(exp, rate);
            }

            List<IdNum> expItems = ExpToItems(exp);
            for (int i = 0; i < expItems.Count; i++)
                AddIdNum(rewards, expItems[i].Id, expItems[i].Num);

            GrantIdNums(role, rewards);
            ResetPartner(role, partner);

            resp.Code = 0;
            Refresh(role);
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushSkillSlotNow();
            Log.Print("[PartnerService] reforge uid=" + partner.Uid + " free=" + req.IsFree
                + " charge=" + chargeId + "x" + chargeNum);
            return resp;
        }

        /// <summary>把伙伴重置回 1 级 0 星：卸装备、清技能槽、重学技能。</summary>
        private static void ResetPartner(LocalRole role, PartnerInfo partner)
        {
            if (role == null || partner == null)
                return;

            ClearPartnerSkillSlots(role, partner);

            partner.Star = 0;
            partner.Level = 1;

            PartnerSlotInfo slot = FindSlotByUid(role, partner.Uid);
            if (slot != null)
            {
                // 容量上面已校验过，这里不再拦，保证装备一定回到背包
                TakeOffAllEquips(role, slot, out _, false);
            }

            // 原服：par.skills = [] 后重新 learnSkill（星级归 0 后只剩 0 星技能）
            if (partner.Skills == null)
                partner.Skills = new List<SkillInfo>();
            else
                partner.Skills.Clear();
            PartnerService.Instance.LearnSkills(partner);
        }

        /// <summary>汇总 1..star 级所有 StarConsume 消耗（同 id 累加），对齐原服 tmp 聚合。</summary>
        private static List<IdNum> CollectStarConsumeItems(CFGItem_Partner cfg, int star)
        {
            var list = new List<IdNum>();
            if (cfg == null || cfg.StarConsume == null || star <= 0)
                return list;

            // 原服 for (i := star; i > 0; i--) 找 v.Id == i，等价于 id <= star
            for (int s = star; s > 0; s--)
            {
                CFGItem_Partner.StarConsumeItem one = FindStarConsume(cfg, s);
                if (one == null || one.values == null)
                    continue;
                for (int i = 0; i < one.values.Length; i++)
                {
                    CFGItem_Partner.StarConsumeItem.Item cost = one.values[i];
                    if (cost == null || cost.key <= 0 || cost.value <= 0)
                        continue;
                    AddIdNum(list, cost.key, cost.value);
                }
            }
            return list;
        }

        /// <summary>经验按「经验珠」面额贪心换算（原服 global.Exp2ExpItems，从大到小取，余数丢弃）。</summary>
        private static List<IdNum> ExpToItems(long exp)
        {
            var list = new List<IdNum>();
            if (exp <= 0)
                return list;

            Dictionary<int, int> beads = GetExpBeadDict();
            if (beads == null || beads.Count == 0)
            {
                // 没配经验珠就退经验本身（cfgId 2 走 GrantReward 的 exp 分支）
                list.Add(new IdNum { Id = 2, Num = exp });
                return list;
            }

            var ordered = new List<KeyValuePair<int, int>>(beads);
            // ⚠️ 不能写 ordered.Sort((a, b) => ...)：ILRuntime 里 Comparison<T> 需要注册委托适配器，
            //    未注册会直接抛 "Cannot find Delegate Adapter"。这里手写插入排序（面额降序）。
            for (int i = 1; i < ordered.Count; i++)
            {
                KeyValuePair<int, int> cur = ordered[i];
                int j = i - 1;
                while (j >= 0 && ordered[j].Value < cur.Value)
                {
                    ordered[j + 1] = ordered[j];
                    j--;
                }
                ordered[j + 1] = cur;
            }

            long left = exp;
            for (int i = 0; i < ordered.Count; i++)
            {
                int num = ordered[i].Value;
                if (num <= 0)
                    continue;
                long count = left / num;
                left = left % num;
                if (count > 0)
                    list.Add(new IdNum { Id = ordered[i].Key, Num = count });
                if (left <= 0)
                    break;
            }
            return list;
        }

        private static void AddIdNum(List<IdNum> list, int id, long num)
        {
            if (list == null || id <= 0 || num <= 0)
                return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == id)
                {
                    list[i] = new IdNum { Id = id, Num = list[i].Num + num };
                    return;
                }
            }
            list.Add(new IdNum { Id = id, Num = num });
        }

        private static void GrantIdNums(LocalRole role, List<IdNum> list)
        {
            if (role == null || list == null)
                return;
            for (int i = 0; i < list.Count; i++)
            {
                IdNum one = list[i];
                if (one == null || one.Id <= 0 || one.Num <= 0)
                    continue;
                ItemService.Instance.GrantReward(role, one.Id, one.Num);
            }
        }

        /// <summary>按百分比放大后向上取整（原服 math.Ceil(num * rate / 100)）。</summary>
        private static long CeilPercent(long num, int rate)
        {
            if (num <= 0 || rate <= 0)
                return 0;
            return (long)System.Math.Ceiling(num * (rate / 100.0));
        }

        /// <summary>item.json 的 Type = 7（伙伴碎片），原服 constant.PartnerFragment。</summary>
        private static bool IsPartnerFragment(int cfgId)
        {
            if (cfgId <= 0)
                return false;
            try
            {
                CFGItem_Item cfg = CFG_Item.GetById(cfgId);
                return cfg != null && EquipRuleService.ToInt(cfg.Type) == 7;
            }
            catch { return false; }
        }

        /// <summary>伙伴身上装备能否全部收回背包（不满足时给明确文案）。</summary>
        private static bool CanTakeOffAllEquips(LocalRole role, PartnerInfo partner, out string error)
        {
            error = "";
            PartnerSlotInfo slot = FindSlotByUid(role, partner.Uid);
            if (slot == null || slot.Equips == null)
                return true;

            int count = 0;
            for (int i = 1; i < slot.Equips.Count; i++)
            {
                SlotInfo equip = slot.Equips[i];
                if (equip != null && equip.Item != null)
                    count++;
            }
            if (count == 0)
                return true;

            if (!EquipRuleService.CanAddEquipCount(role, count, out error))
            {
                if (string.IsNullOrEmpty(error))
                    error = "背包空间不足，请先清理背包";
                return false;
            }
            return true;
        }

        private static PartnerSlotInfo FindSlotByUid(LocalRole role, long partnerUid)
        {
            if (role == null || role.PartnerSlots == null || partnerUid <= 0)
                return null;
            for (int i = 0; i < role.PartnerSlots.Count; i++)
            {
                PartnerSlotInfo slot = role.PartnerSlots[i];
                if (slot != null && slot.PartnerUid == partnerUid)
                    return slot;
            }
            return null;
        }

        private static CFG_XMLConfig.XML_Config.XML_Item_Decompose GetDecomposeCfg()
        {
            try
            {
                if (CFG_XMLConfig.xmlConfig != null && CFG_XMLConfig.xmlConfig.Partener != null)
                    return CFG_XMLConfig.xmlConfig.Partener.decompose;
            }
            catch { }
            return null;
        }

        private static Dictionary<int, (int id, int num)> GetForgeChargeDict()
        {
            try
            {
                if (CFG_XMLConfig.xmlConfig != null)
                    return CFG_XMLConfig.xmlConfig.PartenerPayRecastingDic;
            }
            catch { }
            return null;
        }

        private static Dictionary<int, int> GetExpBeadDict()
        {
            try
            {
                if (CFG_XMLConfig.xmlConfig != null)
                    return CFG_XMLConfig.xmlConfig.PartenerEXPBeadDic;
            }
            catch { }
            return null;
        }

        private static int GetForgeFreeRate()
        {
            try
            {
                if (CFG_XMLConfig.xmlConfig != null && CFG_XMLConfig.xmlConfig.Partener != null)
                    return CFG_XMLConfig.xmlConfig.Partener.forgeRate;
            }
            catch { }
            return 100;
        }

        /// <summary>
        /// 确保槽位表存在。原服是 [MaxPartnerNum] 固定数组，这里按 SlotId 1..4 补齐。
        /// </summary>
        public void EnsureSlots(LocalRole role)
        {
            if (role == null)
                return;
            if (role.PartnerSlots == null)
                role.PartnerSlots = new List<PartnerSlotInfo>();

            // 兼容旧存档：早期单机版按 0..3 建槽，这里清掉非法槽位。
            for (int i = role.PartnerSlots.Count - 1; i >= 0; i--)
            {
                PartnerSlotInfo oldSlot = role.PartnerSlots[i];
                if (oldSlot == null || !IsValidSlot(oldSlot.SlotId))
                    role.PartnerSlots.RemoveAt(i);
            }

            for (int slotId = FirstPartnerSlotId; slotId <= MaxPartnerSlots; slotId++)
            {
                if (FindSlot(role, slotId) != null)
                    continue;
                role.PartnerSlots.Add(new PartnerSlotInfo
                {
                    SlotId = slotId,
                    PartnerUid = 0,
                    Equips = new List<SlotInfo>(),
                });
            }

            // 伙伴装备位（1..12）：旧档/新槽都要补满
            role.EnsurePartnerEquipSlots();
        }

        /// <summary>
        /// 获得伙伴。原服 addPartner：
        /// 已拥有同配置伙伴时按 ChipNum 转成碎片，否则新建并按星级解锁技能。
        /// 返回 1 表示新获得，2 表示转碎片，0 表示失败。
        /// </summary>
        public int AddPartner(LocalRole role, int cfgId)
        {
            if (role == null || cfgId <= 0)
                return 0;

            CFGItem_Partner cfg = GetPartnerCfg(cfgId);
            if (cfg == null)
                return 0;

            if (role.Partners == null)
                role.Partners = new List<PartnerInfo>();
            EnsureSlots(role);

            for (int i = 0; i < role.Partners.Count; i++)
            {
                PartnerInfo owned = role.Partners[i];
                if (owned == null || owned.CfgId != cfgId)
                    continue;

                // 重复获得：按碎片转换
                if (cfg.ChipNum != null)
                {
                    for (int c = 0; c < cfg.ChipNum.Length; c++)
                    {
                        CFGItem_Partner.StarConsumeItem.Item chip = cfg.ChipNum[c];
                        if (chip == null || chip.key <= 0 || chip.value <= 0)
                            continue;
                        ItemService.Instance.GrantReward(role, chip.key, chip.value);
                    }
                }
                return 2;
            }

            var partner = new PartnerInfo
            {
                Uid = LocalData.Instance.AllocateItemUid(),
                CfgId = cfgId,
                Level = 1,
                Star = cfg.Star,
                Skills = new List<SkillInfo>(),
            };
            LearnSkills(partner);
            role.Partners.Add(partner);
            return 1;
        }

        /// <summary>
        /// 按星级解锁技能。原服 Partner.learnSkill：
        /// `cfg.Skills` 是 {星级: 技能UUID}，`star >= 星级` 才学。
        /// </summary>
        public void LearnSkills(PartnerInfo partner)
        {
            if (partner == null)
                return;
            if (partner.Skills == null)
                partner.Skills = new List<SkillInfo>();

            CFGItem_Partner cfg = GetPartnerCfg(partner.CfgId);
            if (cfg == null || cfg.Skills == null)
                return;

            for (int i = 0; i < cfg.Skills.Length; i++)
            {
                Dictionary<string, int> entry = cfg.Skills[i];
                if (entry == null)
                    continue;
                foreach (KeyValuePair<string, int> kv in entry)
                {
                    int needStar;
                    if (!int.TryParse(kv.Key, out needStar))
                        continue;
                    if (partner.Star < needStar)
                        continue;
                    if (kv.Value <= 0)
                        continue;
                    if (HasSkill(partner, kv.Value))
                        continue;

                    CFGItem_Skill skillCfg = GetSkillCfg(kv.Value);
                    if (skillCfg == null)
                        continue;

                    partner.Skills.Add(new SkillInfo
                    {
                        CfgId = kv.Value,
                        Cooldown = 0,
                        Advanced = 0,
                    });
                }
            }
        }

        /// <summary>取上阵伙伴（按 SlotId 升序），战斗与属性计算用。</summary>
        public List<PartnerInfo> GetBattlePartners(LocalRole role)
        {
            var result = new List<PartnerInfo>();
            if (role == null)
                return result;
            EnsureSlots(role);

            for (int slotId = FirstPartnerSlotId; slotId <= MaxPartnerSlots; slotId++)
            {
                // 等级没到的槽位不生效（原服 refreshStats 遍历 partnerSlots 时
                // 这些槽位本来就是 nil）
                if (!IsSlotUnlocked(role, slotId))
                    continue;
                PartnerSlotInfo slot = FindSlot(role, slotId);
                if (slot == null || slot.PartnerUid <= 0)
                    continue;
                PartnerInfo partner = FindPartner(role, slot.PartnerUid);
                if (partner != null)
                    result.Add(partner);
            }
            return result;
        }

        /// <summary>
        /// 把上阵伙伴的属性累加进 values。
        /// 对齐原服 Partner.refreshStats（不含装备/宝石）：
        ///   roleCfg.BassProperty × 品质系数 + 星级属性
        /// </summary>
        public void AddPartnerStats(Dictionary<Attribute, int> values)
        {
            if (values == null)
                return;
            LocalRole role = LocalData.Instance.Current;
            List<PartnerInfo> partners = GetBattlePartners(role);
            for (int i = 0; i < partners.Count; i++)
                AddOnePartnerStats(values, partners[i]);
        }

        /// <summary>
        /// 单独算一个伙伴的属性表（战斗里建伙伴单位用）。
        /// 公式与 AddOnePartnerStats 一致。
        /// </summary>
        public Dictionary<Attribute, int> BuildPartnerStats(PartnerInfo partner)
        {
            var values = new Dictionary<Attribute, int>();
            AddOnePartnerStats(values, partner);
            return values;
        }

        private void AddOnePartnerStats(Dictionary<Attribute, int> values, PartnerInfo partner)
        {
            if (partner == null)
                return;
            CFGItem_Partner cfg = GetPartnerCfg(partner.CfgId);
            if (cfg == null)
                return;

            float qualityRate = GetQualityRate(cfg.Quality);
            int level = partner.Level > 0 ? partner.Level : 1;

            CFGItem_Role roleCfg = null;
            try { roleCfg = CFG_Role.GetByJobAndLevel(cfg.job, level); }
            catch { roleCfg = null; }

            if (roleCfg != null && roleCfg.Property != null)
            {
                for (int i = 0; i < roleCfg.Property.Length; i++)
                {
                    CFGItem_Role.PropertyItem item = roleCfg.Property[i];
                    if (item.Id <= 0 || item.Num == 0)
                        continue;
                    int scaled = (int)(item.Num * qualityRate);
                    if (scaled == 0)
                        continue;
                    AddValue(values, (Attribute)item.Id, scaled);
                }
            }

            // 星级属性：StarAttribute 里 id == star 的那一组
            CFGItem_Partner.StarConsumeItem.Item[] starAttrs = null;
            try { starAttrs = cfg.GetStarAttributeByLv(partner.Star); }
            catch { starAttrs = null; }
            if (starAttrs != null)
            {
                for (int i = 0; i < starAttrs.Length; i++)
                {
                    CFGItem_Partner.StarConsumeItem.Item attr = starAttrs[i];
                    if (attr == null || attr.key <= 0 || attr.value == 0)
                        continue;
                    AddValue(values, (Attribute)attr.key, attr.value);
                }
            }

            // 出生（固定）属性：原服 refreshStats 里 BornValue 那段
            if (cfg.BornValue != null)
            {
                for (int i = 0; i < cfg.BornValue.Length; i++)
                {
                    CFGItem_Partner.StarConsumeItem.Item born = cfg.BornValue[i];
                    if (born == null || born.key <= 0 || born.value == 0)
                        continue;
                    AddValue(values, (Attribute)born.key, born.value);
                }
            }

            // 装备（原服 refreshStats 的装备段）
            AddPartnerEquipStats(values, partner);
        }

        /// <summary>
        /// 伙伴装备属性，对齐原服 Partner.refreshStats 的「装备 + 魂石 + 四段大师」：
        ///   ① 装备：攻/防/生命基础值 × **实例**品质系数；其余配置属性（命中/闪避/暴击/韧性/增伤/生命%）
        ///      取原值；槽位强化（uuid = job%10*1000 + Enhance）与升星（job%10*100 + Stars）
        ///      取配置里 slot 匹配的项；实例极品属性直接加。
        ///   ② 魂石：属性不乘品质系数；详见 AddPartnerGemstones。
        ///   ③ 魂石大师：全身魂石等级降序，按 Diamond/master/stage 从高到低取第一个满足的档。
        ///   ④ 强化 / 升星大师：取**全身最低**强化等级 / 星级，按 EquipMaster 同 Category
        ///      且 Limit 达标的最高一档加成。
        /// </summary>
        private void AddPartnerEquipStats(Dictionary<Attribute, int> values, PartnerInfo partner)
        {
            if (values == null || partner == null)
                return;
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return;
            PartnerSlotInfo slotInfo = role.FindPartnerSlotByUid(partner.Uid);
            if (slotInfo == null || slotInfo.Equips == null)
                return;

            CFGItem_Partner partnerCfg = GetPartnerCfg(partner.CfgId);
            int partnerJob = partnerCfg == null ? 0 : PartnerToInt(partnerCfg.job) % 10;

            // 魂石大师要看全身魂石等级；强化/升星大师要看全身**最低**强化等级/星级
            var allGemLevels = new List<int>();
            int minEnhance = int.MaxValue;
            int minStars = int.MaxValue;

            // ⚠️ 遍历全部 13 个槽位（0 号空置），不能只遍历有装备的：
            //    强化/升星大师用的是「全身最低」，原服固定数组里空槽 Enhance/Stars 都是 0，
            //    所以少穿一件或某件没强化，强化大师就不生效 —— 这是原服语义。
            for (int slot = 1; slot < LocalRole.PartnerEquipSlotCount; slot++)
            {
                SlotInfo equip = slot < slotInfo.Equips.Count ? slotInfo.Equips[slot] : null;

                int slotEnhance = equip == null ? 0 : equip.Enhance;
                int slotStars = equip == null ? 0 : equip.Stars;
                if (slotEnhance < minEnhance)
                    minEnhance = slotEnhance;
                if (slotStars < minStars)
                    minStars = slotStars;

                if (equip == null)
                    continue;

                // ── 魂石段：原服在「装备是否存在」判断之前，宝石属性不依赖装备基础值 ──
                AddPartnerGemstones(values, equip, allGemLevels);

                if (equip.Item == null)
                    continue;

                CFGItem_Item cfg;
                try { cfg = CFG_Item.GetById(equip.Item.CfgId); }
                catch { continue; }
                if (cfg == null)
                    continue;

                int itemQuality = equip.Item.Equip != null
                    ? (int)equip.Item.Equip.Quality
                    : (int)ItemQuality.WHITE;
                float quality = GetEquipQualityCoef(itemQuality);

                AddValue(values, Attribute.PHYSICAL_ATTACK, PartnerScale(cfg.Phyatk, quality));
                AddValue(values, Attribute.MAGIC_ATTACK, PartnerScale(cfg.Magatk, quality));
                AddValue(values, Attribute.PHYSICAL_DEFENSE, PartnerScale(cfg.Phydef, quality));
                AddValue(values, Attribute.MAGIC_DEFENSE, PartnerScale(cfg.Magdef, quality));
                AddValue(values, Attribute.MAX_HEALTH, PartnerScale(cfg.HP, quality));

                AddValue(values, Attribute.HIT, PartnerToInt(cfg.Hit));
                AddValue(values, Attribute.DODGE, PartnerToInt(cfg.Miss));
                AddValue(values, Attribute.CRITICAL_STRIKE, PartnerToInt(cfg.Crit));
                AddValue(values, Attribute.TOUGHNESS, PartnerToInt(cfg.Decrit));
                AddValue(values, Attribute.PVP_DAMAGE_PERCENTAGE, PartnerToInt(cfg.PVPdamage));
                AddValue(values, Attribute.PVE_DAMAGE_PERCENTAGE, PartnerToInt(cfg.PVEdamage));
                AddValue(values, Attribute.HEALTH_PERCENTAGE, PartnerToInt(cfg.HPPercent));

                // 槽位强化属性
                if (equip.Enhance > 0)
                {
                    CFGItem_EquipStrengthen enhanceCfg = null;
                    try { enhanceCfg = CFG_EquipStrengthen.GetByJobAndLv(partnerJob, equip.Enhance); }
                    catch { enhanceCfg = null; }
                    if (enhanceCfg != null && enhanceCfg.Attribute != null)
                    {
                        float enhanceCoef = GetEnhanceQualityCoef(PartnerToInt(cfg.Quality));
                        for (int e = 0; e < enhanceCfg.Attribute.Length; e++)
                        {
                            CFGItem_EquipStrengthen.Attribute_Item attr = enhanceCfg.Attribute[e];
                            if (attr.slot != slot || attr.attribute <= 0)
                                continue;
                            AddValue(values, (Attribute)attr.attribute, (int)(attr.value * enhanceCoef));
                        }
                    }
                }

                // 槽位升星属性（配置里是百分比，原值加入）
                if (equip.Stars > 0)
                {
                    CFGItem_EquipStar starCfg = null;
                    try { starCfg = CFG_EquipStar.GetByJobAndLv(partnerJob, equip.Stars); }
                    catch { starCfg = null; }
                    if (starCfg != null && starCfg.Attribute != null)
                    {
                        for (int st = 0; st < starCfg.Attribute.Length; st++)
                        {
                            CFGItem_EquipStar.Attribute_Item attr = starCfg.Attribute[st];
                            if (attr.slot != slot || attr.attribute <= 0)
                                continue;
                            AddValue(values, (Attribute)attr.attribute, attr.value);
                        }
                    }
                }

                // 实例极品属性
                if (equip.Item.Equip != null && equip.Item.Equip.Attributes != null)
                {
                    for (int a = 0; a < equip.Item.Equip.Attributes.Count; a++)
                    {
                        Stats stat = equip.Item.Equip.Attributes[a];
                        if (stat == null)
                            continue;
                        AddValue(values, stat.attr, stat.Num);
                    }
                }
            }

            if (minEnhance == int.MaxValue)
                minEnhance = 0;
            if (minStars == int.MaxValue)
                minStars = 0;

            // 魂石大师：与玩家侧共用同一套判定（RoleStatService.AddGemstoneMaster）
            RoleStatService.AddGemstoneMaster(values, allGemLevels);
            // 强化大师 / 升星大师：同上，共用 RoleStatService.AddEquipMasterByLimit
            RoleStatService.AddEquipMasterByLimit(values, CFG_EquipMaster.CategoryClass.Reinforce, minEnhance);
            RoleStatService.AddEquipMasterByLimit(values, CFG_EquipMaster.CategoryClass.UpStar, minStars);
        }

        /// <summary>
        /// 单件伙伴装备的魂石段：魂石属性 + 「同系四连」自然加成。
        ///
        /// 对齐原服 Partner.refreshStats 的宝石循环：
        ///   - 魂石属性**不乘品质系数**（玩家侧同样如此），直接进总属性；
        ///   - 同时把每颗魂石的 itemlevel 收进 allGemLevels，供魂石大师判定；
        ///   - 4 个魂石槽的 NatureMark 全部相同 → EquipMaster(4000 + NatureMark) 的属性。
        ///
        /// ⚠️ 原服这段写成 `if err != nil { continue }`（连后面的装备属性一起跳过），
        ///    是明显的笔误；这里只跳过自然加成本身。
        /// </summary>
        private static void AddPartnerGemstones(Dictionary<Attribute, int> values,
            SlotInfo equip, List<int> allGemLevels)
        {
            if (values == null || equip == null || equip.Gemstone == null)
                return;

            var natures = new List<int>();
            for (int g = 0; g < equip.Gemstone.Length && g < LocalRole.GemSlotCount; g++)
            {
                int gemId = equip.Gemstone[g];
                if (gemId <= 0)
                    continue;
                CFGItem_Item gemCfg;
                try { gemCfg = CFG_Item.GetById(gemId); }
                catch { gemCfg = null; }
                if (gemCfg == null)
                    continue;

                if (gemCfg.NatureMark > 0)
                    natures.Add(gemCfg.NatureMark);
                if (allGemLevels != null)
                    allGemLevels.Add(PartnerToInt(gemCfg.itemlevel));

                AddValue(values, Attribute.PHYSICAL_ATTACK, PartnerToInt(gemCfg.Phyatk));
                AddValue(values, Attribute.MAGIC_ATTACK, PartnerToInt(gemCfg.Magatk));
                AddValue(values, Attribute.PHYSICAL_DEFENSE, PartnerToInt(gemCfg.Phydef));
                AddValue(values, Attribute.MAGIC_DEFENSE, PartnerToInt(gemCfg.Magdef));
                AddValue(values, Attribute.MAX_HEALTH, PartnerToInt(gemCfg.HP));
                AddValue(values, Attribute.HIT, PartnerToInt(gemCfg.Hit));
                AddValue(values, Attribute.DODGE, PartnerToInt(gemCfg.Miss));
                AddValue(values, Attribute.CRITICAL_STRIKE, PartnerToInt(gemCfg.Crit));
                AddValue(values, Attribute.TOUGHNESS, PartnerToInt(gemCfg.Decrit));
                AddValue(values, Attribute.PVP_DAMAGE_PERCENTAGE, PartnerToInt(gemCfg.PVPdamage));
                AddValue(values, Attribute.PVE_DAMAGE_PERCENTAGE, PartnerToInt(gemCfg.PVEdamage));
                AddValue(values, Attribute.HEALTH_PERCENTAGE, PartnerToInt(gemCfg.HPPercent));
            }

            // 魂石自然加成：同一件装备 4 个魂石槽全是同一种自然系才生效
            if (natures.Count == LocalRole.GemSlotCount
                && natures[0] == natures[1]
                && natures[1] == natures[2]
                && natures[2] == natures[3])
            {
                CFGItem_EquipMaster natureCfg;
                try { natureCfg = CFG_EquipMaster.GetById(RoleStatService.GemStoneNatureMasterBase + natures[0]); }
                catch { natureCfg = null; }
                if (natureCfg != null && natureCfg.Attribute != null)
                {
                    for (int a = 0; a < natureCfg.Attribute.Length; a++)
                    {
                        // struct 数组元素先拷局部变量再读字段（ILRuntime）
                        CFGItem_EquipMaster.AttributeItem attr = natureCfg.Attribute[a];
                        AddValue(values, (Attribute)attr.key, attr.value);
                    }
                }
            }
        }

        private static int PartnerToInt(decimal? value)
        {
            return value == null ? 0 : (int)value.Value;
        }

        private static int PartnerScale(decimal? value, float coefficient)
        {
            if (value == null || value.Value == 0)
                return 0;
            return (int)System.Math.Round((double)value.Value * coefficient);
        }

        /// <summary>装备实例品质系数（config.xml 的 Equipquality）。</summary>
        private static float GetEquipQualityCoef(int quality)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.EquipQualityDic != null && config.EquipQualityDic.ContainsKey(quality))
                    return config.EquipQualityDic[quality]._value;
            }
            catch { }
            return 1f;
        }

        /// <summary>强化属性要乘的品质系数（config.xml 的 Equipenhance）。</summary>
        private static float GetEnhanceQualityCoef(int cfgQuality)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.EquipEnhanceDic != null
                    && config.EquipEnhanceDic.ContainsKey(cfgQuality))
                    return config.EquipEnhanceDic[cfgQuality];
            }
            catch { }
            return 1f;
        }

        private static void AddValue(Dictionary<Attribute, int> values, Attribute attr, int num)
        {
            int old;
            if (values.TryGetValue(attr, out old))
                values[attr] = old + num;
            else
                values[attr] = num;
        }

        private static float GetQualityRate(int quality)
        {
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.PartnerQualityDic != null)
                {
                    float rate;
                    if (config.PartnerQualityDic.TryGetValue(quality, out rate) && rate > 0)
                        return rate;
                }
            }
            catch { }
            return 1f;
        }

        private void Refresh(LocalRole role)
        {
            RoleStatService.Instance.Recalculate(role, true);
            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushStatsNow();
            LocalPusher.Instance.PushUserBaseNow();

            var slotResp = GetSlotInfo() as PartnerSlotInfoResponse;
            if (slotResp != null)
                LocalPusher.Instance.PushPartnerSlotNow(slotResp);
        }

        private static CommonResponse NewResponse(CmdId cmd)
        {
            return new CommonResponse
            {
                Code = -1,
                Error = "",
                RequestCmdId = (uint)cmd,
            };
        }

        private static PartnerSlotInfo FindSlot(LocalRole role, int slotId)
        {
            if (role == null || role.PartnerSlots == null)
                return null;
            for (int i = 0; i < role.PartnerSlots.Count; i++)
            {
                PartnerSlotInfo slot = role.PartnerSlots[i];
                if (slot != null && slot.SlotId == slotId)
                    return slot;
            }
            return null;
        }

        /// <summary>伙伴配置的职业（11/12/13），升级取 NeedEXP 要用。</summary>
        public static int GetPartnerJob(int cfgId)
        {
            CFGItem_Partner cfg = GetPartnerCfg(cfgId);
            return cfg == null ? 0 : cfg.job;
        }

        /// <summary>按 Uid 找伙伴（协议层用，比如 C2S_UpgradeLevel 的 PARTNER 分支）。</summary>
        public PartnerInfo FindPartnerByUid(LocalRole role, long uid)
        {
            return FindPartner(role, uid);
        }

        private static PartnerInfo FindPartner(LocalRole role, long uid)
        {
            if (role == null || role.Partners == null || uid <= 0)
                return null;
            for (int i = 0; i < role.Partners.Count; i++)
            {
                PartnerInfo partner = role.Partners[i];
                if (partner != null && partner.Uid == uid)
                    return partner;
            }
            return null;
        }

        private static bool HasSkill(PartnerInfo partner, int skillCfgId)
        {
            if (partner.Skills == null)
                return false;
            for (int i = 0; i < partner.Skills.Count; i++)
            {
                SkillInfo skill = partner.Skills[i];
                if (skill != null && skill.CfgId == skillCfgId)
                    return true;
            }
            return false;
        }

        private static CFGItem_Partner GetPartnerCfg(int cfgId)
        {
            try { return CFG_Partner.GetById(cfgId); }
            catch { return null; }
        }

        private static CFGItem_Skill GetSkillCfg(int cfgId)
        {
            try { return CFG_Skill.GetById(cfgId); }
            catch { return null; }
        }

        private static CFGItem_Partner.StarConsumeItem FindStarConsume(CFGItem_Partner cfg, int star)
        {
            if (cfg == null || cfg.StarConsume == null)
                return null;
            for (int i = 0; i < cfg.StarConsume.Length; i++)
            {
                CFGItem_Partner.StarConsumeItem item = cfg.StarConsume[i];
                if (item != null && item.id == star)
                    return item;
            }
            return null;
        }
    }
}
