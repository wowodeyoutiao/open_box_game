using System.Collections.Generic;
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
    /// - 伙伴装备 / 宝石暂不接（Equips 保持空数组）。
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
        /// 伙伴装备属性，对齐原服 Partner.refreshStats 的装备段：
        ///   攻/防/生命基础值 × **实例**品质系数；其余配置属性（命中/闪避/暴击/韧性/增伤/生命%）
        ///   取原值；槽位强化（uuid = job%10*1000 + Enhance）与升星（job%10*100 + Stars）
        ///   取配置里 slot 匹配的项；实例极品属性直接加。
        /// 暂不含：宝石属性、宝石大师、强化大师、升星大师（后续 B 阶段补）。
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

            for (int slot = 1; slot < LocalRole.PartnerEquipSlotCount && slot < slotInfo.Equips.Count; slot++)
            {
                SlotInfo equip = slotInfo.Equips[slot];
                if (equip == null || equip.Item == null)
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
