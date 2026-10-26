package game

import (
	"math"
	"math/rand"
	"nanoserver/global"
	shushumain "nanoserver/internal/shushu"
	"nanoserver/pkg/astar"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/errutil"
	"nanoserver/pkg/maputil"
	"nanoserver/pkg/snowflake"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"
	"sort"
	"time"
)

// const PartnerWarriorNormalSkillID = 2000000
// const PartnerWizardNormalSkillID = 2000100
// const PartnerRitualistNormalSkillID = 2000200

type Partner struct {
	*MapObject
	star   int32 //星级
	cfg    *global.PartnerConfig
	combat int32

	searchTime time.Time
	roamPath   []*astar.APoint
	attrRatio  float64
}

func CreatePartner(cid int32) *Partner {
	uid := snowflake.GenID()
	partner := &Partner{
		MapObject: NewMapObject(ObjectTypePartner, uid),
		star:      0,
	}
	partner.cfg = global.GetPartnerCfg(cid)
	partner.cfgId = cid
	partner.level = 1
	partner.name = partner.cfg.Name
	partner.job = constant.MowJob(partner.cfg.Job)
	partner.attrRatio = BaseRate

	return partner
}

func (par *Partner) levelUp() bool {

	player := par.owner.(*Player)
	if player == nil {
		return false

	}
	if par.level >= global.GlobalCfg.BaseValue.Rolelevel.Maxlevel {
		return false
	}

	pcfg := global.GetPartnerCfg(par.cfgId)
	if pcfg == nil {
		return false
	}

	cfg, err := global.GetRoleCfg(int32(pcfg.Job), par.level)
	if err != nil {
		player.logger.Errorf("升级失败 userId: %d,  err: %s", par.uid, err.Error())
		return false
	}

	// var bUpgrade bool
	// for player.exp >= cfg.NeedExp {
	// 	player.exp -= cfg.NeedExp
	// 	if player.exp <= 0 {
	// 		player.exp = 0
	// 	}

	// 	cfg, err = global.GetRoleCfg(int32(pcfg.Job), par.level+1)
	// 	if err != nil {
	// 		player.logger.Errorf("升级失败 userId: %d,  err: %s", par.uid, err.Error())
	// 		return false
	// 	}
	// 	par.level++
	// 	bUpgrade = true
	// }
	var bUpgrade bool
	if player.exp >= cfg.NeedExp {
		player.exp -= cfg.NeedExp
		if player.exp <= 0 {
			player.exp = 0
		}
		par.level++
		bUpgrade = true
	}
	oldtotalCombat := par.combat
	if bUpgrade {
		//par.logger.Debugf("升级 userId: %d,  level: %d  vip: %d  exp: %d", par.uid, par.level, par.vip, par.exp)
		player.refreshStats()
		newtotalCombat := par.combat
		par.upPartnerLevelShushuDate(player, par.level, newtotalCombat-oldtotalCombat)
		par.currentHP = par.GetMaxHP()
		par.stats.setStats(pb.Attribute_HEALTH, par.currentHP)
		player.pushMsg(protocol.RouteLevelUpPush, &pb.LevelUpPush{NewLevel: par.level, Uid: par.uid})
		player.tasks.dispatch(pb.TargetType_UPGRADE_ANY_PARTNER_LEVEL, par.level, 1)
	}
	return true
}
func (par *Partner) upPartnerLevelShushuDate(player *Player, level int32, combat int32) {
	//创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	//upBaseInfo的数据合并进去
	baseInfo := player.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	//在合并点其他的数据
	shushuDate["name"] = "partner_level_upgrade"
	shushuDate["level_before"] = level - 1
	shushuDate["level_after"] = level
	shushuDate["partner_job"] = constant.JobName[par.job]
	shushuDate["partner_power"] = int32(par.combat)
	shushuDate["partner_quality"] = par.cfg.Quality
	shushuDate["partner_star"] = par.cfg.Star
	shushuDate["partner_power_up"] = combat
	shushuDate["partner_name"] = par.name
	shushumain.ReportData(shushuDate)

}

// 技能学习
func (par *Partner) learnSkill() {
	for _, v := range par.cfg.Skills {
		if par.star >= v.Key {
			if skill := par.getSkill(v.Value); skill != nil {
				continue
			}
			if skill := par.getSkillByType(v.Value); skill != nil {
				continue
			}
			_, err := global.GetSkillCfg(v.Value)
			if err == nil {
				par.skills = append(par.skills, &PlayerSkill{CfgId: v.Value})
			}
		}
	}
}

// 计算角色属性
func (par *Partner) refreshStats(packet *pb.SyncUserStatsPush, owner *Player) {
	par.slock.Lock()
	defer par.slock.Unlock()

	now := time.Now()
	par.stats.clear()
	// 基础属性
	roleCfg, err := global.GetRoleCfg(int32(par.cfg.Job), par.level)
	if err != nil {
		owner.logger.Errorf("获取角色等级配置失败 level %d", par.level)
		return
	}

	quality_rate := global.GetpartnerQualityRate(par.cfg.Quality)
	for _, rc := range roleCfg.BassProperty {
		par.stats.setStats(pb.Attribute(rc.Key), int32(math.Round(float64(rc.Value)*quality_rate*par.attrRatio/BaseRate)))
	}

	// 星级
	for _, v := range par.cfg.StarAttribute {
		if v.Id == par.star {
			for _, v2 := range v.Values {
				par.stats.setStats(pb.Attribute(v2.Key), v2.Value)
			}
			break
		}
	}

	for _, v := range par.cfg.BornValue {
		par.stats.setStats(pb.Attribute(v.Key), v.Value)
	}

	par.stats.setStats(pb.Attribute_ATTACK_SPEED, global.GetRoleInitial(int32(par.cfg.Job)).AttackSpeed)
	par.stats.setStats(pb.Attribute_MOVE_SPEED, global.GetRoleInitial(int32(par.cfg.Job)).MoveSpeed)

	// 魂石增加生命值基数=角色基础属性+命格
	gemHealthAdd := par.stat(pb.Attribute_MAX_HEALTH)

	// 装备 强化大师
	equipStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	gemStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	var maxEnhance, maxStars int32
	maxEnhance = math.MaxInt32
	maxStars = math.MaxInt32
	var starCfg, enhanceCfg *global.EquipMasterConfig

	slot_equip := [pb.EquipSlot_MaxEquipSlot]Slot{}

	allGems := []int32{}
	for _, slot := range owner.partnerSlots {
		if slot == nil {
			continue
		}
		if slot.PartnerUid == par.uid {
			slot_equip = slot.Equips
		}
	}
	for slot, equip := range slot_equip {
		if slot <= int(pb.EquipSlot_INVALID_EquipSlot) || slot >= int(pb.EquipSlot_MaxEquipSlot) {
			continue
		}
		gemstoneNature := make([]int32, 0)
		for GemstoneSlot, Gemstone := range equip.Gemstone {
			if GemstoneSlot < int(pb.GemstoneSlot_INVALID_GemstoneSlot) || GemstoneSlot > int(pb.GemstoneSlot_MaxGemstoneSlot) {
				continue
			}
			genstoneCfg, err := global.GetItemCfg(int32(Gemstone))
			if err != nil {
				continue
			}

			if genstoneCfg.NatureMark > 0 {
				gemstoneNature = append(gemstoneNature, genstoneCfg.NatureMark)
			}
			//对应宝石等级的数目加1
			allGems = append(allGems, genstoneCfg.Itemlevel)

			for _, attr := range genstoneCfg.Stats {
				if attr.Key == pb.Attribute_HEALTH_PERCENTAGE {
					gemStatus.setStats(attr.Key, attr.Value)
				} else {
					par.stats.setStats(attr.Key, attr.Value)
				}
			}
		}

		// 魂石自然属性
		if len(gemstoneNature) == int(pb.GemstoneSlot_MaxGemstoneSlot) && gemstoneNature[0] == gemstoneNature[1] && gemstoneNature[1] == gemstoneNature[2] && gemstoneNature[2] == gemstoneNature[3] {
			gemNatureCfg, ok := global.GetEquipMaster(constant.EquipMasterCategoryGemStoneNature*1000 + gemstoneNature[0])
			if ok != nil {
				continue
			}
			for _, v := range gemNatureCfg.Attribute {
				par.stats.setStats(pb.Attribute(v.Key), v.Value)
			}
		}

		if equip.Enhance < maxEnhance {
			maxEnhance = equip.Enhance
		}
		if equip.Stars < maxStars {
			maxStars = equip.Stars
		}
		if equip.ItemId == 0 {
			continue
		}
		equipInfo := owner.getItemByUid(equip.ItemId)
		if equipInfo == nil {
			continue
		}
		equipCfg, err := global.GetItemCfg(equipInfo.CfgId)
		if err != nil {
			continue
		}

		// 基础值攻\防: （装备基础值+槽位强化基础值)*升星百分比 , 基础属性: 装备基础值*品质百分比
		for _, attr := range equipCfg.Stats {
			if attr.Key == pb.Attribute_PHYSICAL_ATTACK || attr.Key == pb.Attribute_MAGIC_ATTACK ||
				attr.Key == pb.Attribute_PHYSICAL_DEFENSE || attr.Key == pb.Attribute_MAGIC_DEFENSE ||
				attr.Key == pb.Attribute_MAX_HEALTH {

				equipStatus.setStats(attr.Key, int32(math.Round(float64(attr.Value)*global.GetEquipqualityCoefficient(equipInfo.Equip.Quality))))
			} else {
				par.stats.setStats(attr.Key, attr.Value)
			}
		}
		// 极品属性直接计算
		if equipInfo.Equip != nil {
			for _, attr := range equipInfo.Equip.Attributes {
				par.stats.setStats(attr.Attr, attr.Num)
			}
		}

		star_uid := int32(par.cfg.Job%10*100) + equip.Stars
		if equipStarCfg, err := global.GetStarsCfg(star_uid); err == nil {
			for _, star_attr := range equipStarCfg.Attribute {
				if star_attr.Slot == int32(slot) {
					equipStatus.setStats(pb.Attribute(star_attr.Attribute), star_attr.Value)
				}
			}
		}

		enhanc_uid := int32(par.cfg.Job%10*1000) + equip.Enhance
		if equipEnhancCfg, err := global.GetEnhanceCfg(enhanc_uid); err == nil {
			for _, star_attr := range equipEnhancCfg.Attribute {
				if star_attr.Slot == int32(slot) {
					if equipCfg.Quality > 0 && equipCfg.Quality <= 7 {
						equipStatus.setStats(pb.Attribute(star_attr.Attribute), int32(float32(star_attr.Value)*global.GlobalCfg.Equipenhances[equipCfg.Quality-1].Value))
					}
				}
			}
		}
	}
	equipStatus.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_ATTACK))*(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(equipStatus.getStats(pb.Attribute_MAGIC_ATTACK))*(float64(equipStatus.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE))*(float64(equipStatus.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(equipStatus.getStats(pb.Attribute_MAGIC_DEFENSE))*(float64(equipStatus.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))
	equipStatus.setStats(pb.Attribute_MAX_HEALTH, int32(float64(equipStatus.getStats(pb.Attribute_MAX_HEALTH))*(float64(equipStatus.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate)))

	for ek, ev := range equipStatus.Attrs {
		if ek == pb.Attribute_PHYSICAL_ATTACK || ek == pb.Attribute_MAGIC_ATTACK ||
			ek == pb.Attribute_PHYSICAL_DEFENSE || ek == pb.Attribute_MAGIC_DEFENSE ||
			ek == pb.Attribute_MAX_HEALTH {
			par.stats.setStats(ek, ev)
		}
	}

	// 魂石增加生命百分比
	par.stats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(gemHealthAdd)*(float64(gemStatus.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate)))

	for _, equipmaster := range global.EquipMasterCfg {
		if equipmaster.Category == constant.EquipMasterCategoryEnhance && maxEnhance >= equipmaster.Limit {
			enhanceCfg = equipmaster
		}
		if equipmaster.Category == constant.EquipMasterCategoryStars && maxStars >= equipmaster.Limit {
			starCfg = equipmaster
		}
	}

	//魂石大师
	sort.Slice(allGems, func(i, j int) bool {
		return allGems[i] > allGems[j]
	})
	for i := len(global.GlobalCfg.Diamond.Master.Stage) - 1; i >= 0; i-- {
		if global.GlobalCfg.Diamond.Master.Stage[i].Num > 0 && len(allGems) >= int(global.GlobalCfg.Diamond.Master.Stage[i].Num) {
			equipMasterCfg, err := global.GetEquipMaster(global.GlobalCfg.Diamond.Master.Stage[i].ID)
			if err != nil {
				continue
			}
			if allGems[global.GlobalCfg.Diamond.Master.Stage[i].Num-1] >= equipMasterCfg.Limit {
				for _, v := range equipMasterCfg.Attribute {
					par.stats.setStats(pb.Attribute(v.Key), v.Value)
				}
				break
			}
		}
	}

	//强化大师
	if enhanceCfg != nil {
		for _, v := range enhanceCfg.Attribute {
			par.stats.setStats(pb.Attribute(v.Key), v.Value)
		}
	}

	//升星大师
	if starCfg != nil {
		for _, v := range starCfg.Attribute {
			par.stats.setStats(pb.Attribute(v.Key), v.Value)
		}
	}

	//特权卡
	privilegeStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	for _, v := range owner.privilege {
		cfg, ok := global.PrivilegeCfg[int32(v.Pid)]
		if !ok {
			continue
		}
		for _, att := range cfg.Pcontent {
			av := privilegeStatus.getStats(pb.Attribute(att.Key))
			if att.Value > av {
				privilegeStatus.changeStats(pb.Attribute(att.Key), att.Value)
			}
		}
	}
	for k, v := range privilegeStatus.Attrs {
		par.stats.setStats(k, v)
	}

	// VIP
	vipStatus := Stats{Attrs: make(map[pb.Attribute]int32)}
	for _, gv := range global.VIPCfg {
		if gv.VIPLevel <= owner.vip {
			for _, att := range gv.Pcontent {
				av := vipStatus.getStats(pb.Attribute(att.Key))
				if att.Value > av {
					vipStatus.changeStats(pb.Attribute(att.Key), att.Value)
				}
			}
		}
	}
	for k, v := range vipStatus.Attrs {
		par.stats.setStats(k, v)
	}

	par.stats.setStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE, int32(global.GlobalCfg.BaseValue.Crit.CriticalDamage))

	//被动技能
	for _, v := range par.skills {
		if skillCfg, err := global.GetSkillCfg(v.CfgId); err == nil {
			if skillCfg.ActiveorPassive == PassiveSkill {
				for _, e := range skillCfg.Effect {
					par.stats.setStats(pb.Attribute(e.Key), e.Value)
				}
			}
		}
	}

	// 参与计算战斗力的属性写在上面!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
	combatStats := Stats{Attrs: par.stats.copyStats()}

	for _, buff := range par.buffs {
		buffCfg, err := global.GetBuffCfg(buff.Id)
		if err != nil {
			continue
		}

		if buffCfg.EffectTime != 0 {
			continue
		}

		if now.After(buff.OverTime) {
			continue
		}

		// 魔法酸雨的增伤只针对技能
		if buffCfg.Pretype == constant.SkillTypeMagicAcidRain {
			continue
		}

		for i, e := range buff.BuffStats.Attrs {
			par.stats.setStats(i, e)
		}
	}

	// <0  减益 ; >0 增益
	par.stats.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(par.GetDC())*(float64(par.stats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	par.stats.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(par.GetMC())*(float64(par.stats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	par.stats.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(par.GetAR())*(float64(par.stats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	par.stats.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(par.GetMR())*(float64(par.stats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))

	par.stats.setStats(pb.Attribute_DODGE, int32(float64(par.stats.getStats(pb.Attribute_DODGE))*(float64(par.stats.getStats(pb.Attribute_DODGE_PERCENTAGE))/BaseRate)))
	par.stats.setStats(pb.Attribute_HIT, int32(float64(par.stats.getStats(pb.Attribute_HIT))*(float64(par.stats.getStats(pb.Attribute_HIT_PERCENTAGE))/BaseRate)))
	par.stats.setStats(pb.Attribute_CRITICAL_STRIKE, int32(float64(par.stats.getStats(pb.Attribute_CRITICAL_STRIKE))*(float64(par.stats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE))/BaseRate)))
	par.stats.setStats(pb.Attribute_TOUGHNESS, int32(float64(par.stats.getStats(pb.Attribute_TOUGHNESS))*(float64(par.stats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE))/BaseRate)))
	par.stats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(par.GetMaxHP())*float64(par.stats.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate))

	// 攻速移速增加  基础/(1+加成)；攻速移速降低  基础*(1+加成)
	par.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(float64(par.stats.getStats(pb.Attribute_ATTACK_SPEED))/(1+float64(par.stats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE))/BaseRate)))
	par.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(float64(par.stats.getStats(pb.Attribute_MOVE_SPEED))/(1+(float64(par.stats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE))/BaseRate))))

	// 基础属性 >= 0
	par.stats.changeStats(pb.Attribute_PHYSICAL_ATTACK, int32(math.Max(0, float64(par.GetDC()))))
	par.stats.changeStats(pb.Attribute_MAGIC_ATTACK, int32(math.Max(0, float64(par.GetMC()))))
	par.stats.changeStats(pb.Attribute_PHYSICAL_DEFENSE, int32(math.Max(0, float64(par.GetAR()))))
	par.stats.changeStats(pb.Attribute_MAGIC_DEFENSE, int32(math.Max(0, float64(par.GetMR()))))

	par.stats.changeStats(pb.Attribute_DODGE, int32(math.Max(0, float64(par.stats.getStats(pb.Attribute_DODGE)))))
	par.stats.changeStats(pb.Attribute_HIT, int32(math.Max(0, float64(par.stats.getStats(pb.Attribute_HIT)))))
	par.stats.changeStats(pb.Attribute_CRITICAL_STRIKE, int32(math.Max(0, float64(par.stats.getStats(pb.Attribute_CRITICAL_STRIKE)))))
	par.stats.changeStats(pb.Attribute_TOUGHNESS, int32(math.Max(0, float64(par.stats.getStats(pb.Attribute_TOUGHNESS)))))
	par.stats.changeStats(pb.Attribute_ATTACK_SPEED, int32(math.Max(0, float64(par.stats.getStats(pb.Attribute_ATTACK_SPEED)))))
	par.stats.changeStats(pb.Attribute_MOVE_SPEED, int32(math.Max(0, float64(par.stats.getStats(pb.Attribute_MOVE_SPEED)))))
	par.stats.changeStats(pb.Attribute_MAX_HEALTH, int32(math.Max(0, float64(par.GetMaxHP()))))
	if owner.curState == PlayerStatusPlaying {
		if par.currentHP > par.GetMaxHP() {
			par.currentHP = par.GetMaxHP()
		}
		par.stats.changeStats(pb.Attribute_HEALTH, par.currentHP)
		combatStats.changeStats(pb.Attribute_HEALTH, par.currentHP)
	} else {
		par.stats.changeStats(pb.Attribute_HEALTH, par.GetMaxHP())
		par.currentHP = par.GetMaxHP()
		combatStats.changeStats(pb.Attribute_HEALTH, par.GetMaxHP())
	}

	combat := par.calcCombatReal(&combatStats)

	//par.logger.Debugf(" [%s] 真实战力 %d , 虚假战力  %d", par.name, combat, par.calcCombat())

	tmpStats2 := par.stats.copyStats()

	ps := &pb.SyncUserStatsPush_PartnerStats{Attributes: []*pb.Stats{}, PartnerUid: par.uid, Combat: combat}
	for k, v := range tmpStats2 {
		if v <= 0 {
			continue
		}
		ps.Attributes = append(ps.Attributes, &pb.Stats{Attr: k, Num: v})
	}
	if packet != nil {
		packet.Partners = append(packet.Partners, ps)
	}

	//par.logger.Debugf("属性计算 当前生命:%d  最大生命:%d  物攻: %d 魔攻: %d", par.currentHP, par.GetMaxHP(), par.GetDC(), par.GetMC())
}

// 战力（不计算buff）
func (par *Partner) calcCombatReal(stats *Stats) int32 {
	combatStats := par.stats4Combat(stats)
	if combatStats == nil {
		return 0
	}

	var skillCombat float64
	for _, v := range par.skills {
		if cfg, err := global.GetSkillCfg(v.CfgId); err == nil {
			skillCombat += float64(cfg.Powerfactor) * (1 + float64(v.AdvancedLevel))
		}
	}

	// 减伤阈值75%
	dmt_reduce := combatStats.getStats(pb.Attribute_DMT_REDUCE_PERCENTAGE_PLAYER) + combatStats.getStats(pb.Attribute_DMT_REDUCE_PERCENTAGE_MONSTER) + combatStats.getStats(pb.Attribute_DMT_FINAL_REDUCE_PERCENTAGE)
	if dmt_reduce > 7500 {
		dmt_reduce = 7500
	}

	// 有效生命
	effective_hp := (float64(combatStats.getStats(pb.Attribute_MAX_HEALTH))+global.GlobalCfg.BaseValue.Power.DefValue*(float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE))+float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE)))/2.0)/(1-float64(dmt_reduce)/BaseRate) +
		(float64(combatStats.getStats(pb.Attribute_DMT_REDUCE_FIXED_PLAYER))+float64(combatStats.getStats(pb.Attribute_DMT_REDUCE_FIXED_MONSTER)))/2.0 +
		float64(combatStats.getStats(pb.Attribute_DMT_FINAL_REDUCE_FIXED))
	// par.logger.Debugf("  第1部分 %f   第2部分 %f    第3部分 %f   ===有效生命 %f ", (float64(combatStats.getStats(pb.Attribute_MAX_HEALTH))+global.GlobalCfg.BaseValue.Power.DefValue*(float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE))+float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE)))/2.0)/(1-float64(dmt_reduce)/BaseRate),
	// 	(float64(combatStats.getStats(pb.Attribute_DMT_REDUCE_FIXED_PLAYER))+float64(combatStats.getStats(pb.Attribute_DMT_REDUCE_FIXED_MONSTER)))/2.0,
	// 	float64(combatStats.getStats(pb.Attribute_DMT_FINAL_REDUCE_FIXED)), effective_hp)
	// 有效输出
	effective_power := float64(combatStats.getStats(par.AttackAttributes()))*
		(1+float64(combatStats.getStats(pb.Attribute_PVP_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_PVE_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE))/BaseRate)*(1+skillCombat/100) +
		(float64(combatStats.getStats(pb.Attribute_DMT_INCREASE_FIXED_PLAYER))+float64(combatStats.getStats(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)))/2.0 +
		float64(combatStats.getStats(pb.Attribute_DMT_FINAL_INCREASE_FIXED))

	// par.logger.Debugf(" 面板攻击 %f  (1+增伤百分比+爆伤) %f  技能加成 %f   固定值 %f   ===有效输出 %f ", float64(combatStats.getStats(par.AttackAttributes())),
	// 	(1 + float64(combatStats.getStats(pb.Attribute_PVP_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_PVE_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)+combatStats.getStats(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE))/BaseRate),
	// 	(1 + skillCombat/100), (float64(combatStats.getStats(pb.Attribute_DMT_INCREASE_FIXED_PLAYER))+float64(combatStats.getStats(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)))/2.0, effective_power)

	combat := math.Sqrt(effective_hp*global.GlobalCfg.BaseValue.Power.LifeValue)*math.Sqrt(effective_power*global.GlobalCfg.BaseValue.Power.AttackValue) +
		float64(combatStats.getStats(pb.Attribute_HIT))*global.GlobalCfg.BaseValue.Power.Hitvalue +
		float64(combatStats.getStats(pb.Attribute_DODGE))*global.GlobalCfg.BaseValue.Power.Dodgevalue +
		float64(combatStats.getStats(pb.Attribute_CRITICAL_STRIKE))*global.GlobalCfg.BaseValue.Power.Critvalue +
		float64(combatStats.getStats(pb.Attribute_TOUGHNESS))*global.GlobalCfg.BaseValue.Power.Toughness

	//par.logger.Debugf(" 最终战力 %f", combat)
	par.combat = int32(math.Round(combat))

	return par.combat
}

func (par *Partner) stats4Combat(combatStats *Stats) *Stats {

	// <0  减益 ; >0 增益
	combatStats.setStats(pb.Attribute_PHYSICAL_ATTACK, int32(float64(combatStats.getStats(pb.Attribute_PHYSICAL_ATTACK))*(float64(combatStats.getStats(pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_MAGIC_ATTACK, int32(float64(combatStats.getStats(pb.Attribute_MAGIC_ATTACK))*(float64(combatStats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_PHYSICAL_DEFENSE, int32(float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE))*(float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_MAGIC_DEFENSE, int32(float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE))*(float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE_PERCENTAGE))/BaseRate)))

	combatStats.setStats(pb.Attribute_DODGE, int32(float64(combatStats.getStats(pb.Attribute_DODGE))*(float64(combatStats.getStats(pb.Attribute_DODGE_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_HIT, int32(float64(combatStats.getStats(pb.Attribute_HIT))*(float64(combatStats.getStats(pb.Attribute_HIT_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_CRITICAL_STRIKE, int32(float64(combatStats.getStats(pb.Attribute_CRITICAL_STRIKE))*(float64(combatStats.getStats(pb.Attribute_CRITICAL_STRIKE_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_TOUGHNESS, int32(float64(combatStats.getStats(pb.Attribute_TOUGHNESS))*(float64(combatStats.getStats(pb.Attribute_TOUGHNESS_PERCENTAGE))/BaseRate)))
	combatStats.setStats(pb.Attribute_MAX_HEALTH, int32(float64(combatStats.getStats(pb.Attribute_MAX_HEALTH))*float64(combatStats.getStats(pb.Attribute_HEALTH_PERCENTAGE))/BaseRate))

	// 攻速移速增加  基础/(1+加成)；攻速移速降低  基础*(1+加成)
	combatStats.changeStats(pb.Attribute_ATTACK_SPEED, int32(float64(combatStats.getStats(pb.Attribute_ATTACK_SPEED))/(1+float64(combatStats.getStats(pb.Attribute_ATTACK_SPEED_PERCENTAGE))/BaseRate)))
	combatStats.changeStats(pb.Attribute_MOVE_SPEED, int32(float64(combatStats.getStats(pb.Attribute_MOVE_SPEED))/(1+(float64(combatStats.getStats(pb.Attribute_MOVE_SPEED_PERCENTAGE))/BaseRate))))

	// 基础属性 >= 0
	combatStats.changeStats(pb.Attribute_PHYSICAL_ATTACK, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_PHYSICAL_ATTACK)))))
	combatStats.changeStats(pb.Attribute_MAGIC_ATTACK, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_MAGIC_ATTACK)))))
	combatStats.changeStats(pb.Attribute_PHYSICAL_DEFENSE, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_PHYSICAL_DEFENSE)))))
	combatStats.changeStats(pb.Attribute_MAGIC_DEFENSE, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_MAGIC_DEFENSE)))))

	combatStats.changeStats(pb.Attribute_DODGE, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_DODGE)))))
	combatStats.changeStats(pb.Attribute_HIT, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_HIT)))))
	combatStats.changeStats(pb.Attribute_CRITICAL_STRIKE, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_CRITICAL_STRIKE)))))
	combatStats.changeStats(pb.Attribute_TOUGHNESS, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_TOUGHNESS)))))
	combatStats.changeStats(pb.Attribute_ATTACK_SPEED, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_ATTACK_SPEED)))))
	combatStats.changeStats(pb.Attribute_MOVE_SPEED, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_MOVE_SPEED)))))
	combatStats.changeStats(pb.Attribute_MAX_HEALTH, int32(math.Max(0, float64(combatStats.getStats(pb.Attribute_MAX_HEALTH)))))

	return combatStats
}

func (par *Partner) starUp() error {
	if par.cfg == nil {
		return errutil.ErrPartnerInvalid
	}

	var consume *global.KeyValueEx
	for _, v := range par.cfg.StarConsume {
		if v.Id == par.star+1 {
			consume = &v
			break
		}
	}

	if consume == nil {
		return errutil.ErrPartnerInvalid
	}

	owner := par.owner.(*Player)
	if owner == nil {
		return errutil.ErrPartnerInvalid
	}

	for _, v := range consume.Values {
		if !owner.checkTakeSource(v.Key, int64(v.Value)) {
			return errutil.ErrItemNotEnough
		}
	}

	for _, v := range consume.Values {
		owner.takeSource(v.Key, int64(v.Value))
	}

	par.star++

	//解锁技能
	par.learnSkill()

	par.SyncUserPartner()
	oldcombat := par.combat
	owner.refreshStats()
	newcombat := par.combat
	par.upPartnerStarShushuDate(owner, par.star, newcombat-oldcombat)
	return nil
}
func (par *Partner) upPartnerStarShushuDate(player *Player, star int32, combat int32) {
	//创建一interface{}类型的map
	shushuDate := make(map[string]interface{}, 0)
	//upBaseInfo的数据合并进去
	baseInfo := player.ToMapForShushu()
	for k, v := range baseInfo {
		shushuDate[k] = v
	}
	//在合并点其他的数据
	shushuDate["name"] = "partner_star_upgrade"
	shushuDate["star_before"] = star - 1
	shushuDate["star_after"] = star
	shushuDate["partner_job"] = constant.JobName[par.job]
	shushuDate["partner_power"] = int32(par.combat)
	shushuDate["partner_quality"] = par.cfg.Quality
	shushuDate["partner_power_up"] = combat
	shushuDate["partner_star"] = par.star
	shushuDate["partner_name"] = par.name
	shushumain.ReportData(shushuDate)
}
func (par *Partner) SyncUserPartner() {

	packet := &pb.PartnerUpdatePush{Partner: &pb.PartnerInfo{}}
	packet.Partner.Uid = par.uid
	packet.Partner.CfgId = par.cfgId
	packet.Partner.Level = par.level
	packet.Partner.Star = par.star
	packet.Partner.Skills = make([]*pb.SkillInfo, 0)
	for _, v := range par.skills {
		packet.Partner.Skills = append(packet.Partner.Skills, &pb.SkillInfo{CfgId: v.CfgId, Advanced: v.AdvancedLevel})
	}

	par.owner.PushMsg(protocol.RoutePartnerUpdatePush, packet)
}

func (par *Partner) processAI() {
	if par.desk == nil {
		return
	}
	if par.dead {
		owner_, _ := par.desk.playerWithId(par.owner.Uid())
		if owner_ == nil {
			return
		}
		if skill, caster := owner_.getSlotSkillByType(constant.SkillTypeReborn); skill != nil {
			if time.Now().After(skill.Cooldown) && !caster.dead {
				caster.lastGasp(par, skill.CfgId)
			}
		}
		return
	}

	par.processHp()
	par.processRoam()
	par.processSearch()
	par.processTarget()
	par.processBuff()
}

// 巡逻
func (par *Partner) processRoam() {
	owner_, _ := par.desk.playerWithId(par.owner.Uid())
	if owner_ == nil {
		return
	}

	if !par.CanMove() {
		return
	}

	if par.desk.roomCfg.Roomtype == constant.PVPOneOnOneMap {
		if maputil.Distance(par.location, owner_.Location()) <= 10 && par.target != nil {
			return
		}
	} else {
		if maputil.Distance(par.location, owner_.Location()) <= 4 && par.target != nil {
			return
		}
	}

	self_slot := 0
	flow_slot := -1
	var flow_point *pb.Point
	for i := 1; i <= 4; i++ {
		if spar := owner_.getPartnerInfoBySlotId(int32(i)); spar != nil && spar.uid == par.uid {
			self_slot = i
			break
		}
	}

	for i := 4; i >= 1; i-- {
		if fpar := owner_.getPartnerInfoBySlotId(int32(i)); fpar != nil && fpar.uid != par.uid && !fpar.dead {
			if self_slot > i {
				flow_point = fpar.Location()
				flow_slot = i
				break
			}
		}
	}
	if flow_point == nil {
		flow_point = owner_.Location()
	}
	distance := 2
	if par.desk.roomCfg.Roomtype == constant.PVPOneOnOneMap {
		distance = 8
	}

	if owner_.GetObState() == ObjectActionMove && maputil.Distance(par.location, flow_point) > distance {
		//logger.Debugf("[走走走]  我是【%d】号 , 跟随[%d] 号  终点【%v】, 距离 【%d】", self_slot, flow_slot, flow_point, maputil.Distance(par.location, flow_point))
		par.target = nil
		if path := maputil.GetPath(par.location, flow_point, &par.desk.mapCfg.AstarMgr); len(path) > 1 {
			par.moveTo(path[1])
		} else {
			logger.Debugf("Par 回到原点寻路失败 起点=%v  终点=%v  跟随[%d]", par.location, flow_point, flow_slot)
		}
	}

	if owner_.GetObState() == ObjectActionStand {
		owner_.target = nil
		if stand_cell := par.standLocation(self_slot); stand_cell != nil {

			if maputil.Distance(par.location, global.ToPointF(&stand_cell.location)) > 1 {

				//logger.Debugf("[停停停]   我是【%d】号,坐标[%v]  距离主人 【%d】 我要移动到 [%v]", self_slot, par.location, maputil.Distance(par.location, flow_point), stand_cell.location)
				if path := maputil.GetPath(par.location, global.ToPointF(&stand_cell.location), &par.desk.mapCfg.AstarMgr); len(path) > 1 {
					par.moveTo(path[1])
				} else if len(path) == 1 {
					par.moveTo((*astar.APoint)(&stand_cell.location))
				} else {
					logger.Debugf("Par 回到原点寻路失败 起点=%v  终点=%v", par.location, flow_point)
				}
			}
		} else if maputil.Distance(par.location, owner_.Location()) > distance {
			cell := par.desk.mapInfo.getAvailableCell(&owner_.GetCell().location, 2)
			if cell != nil {
				if path := maputil.GetPath(par.location, global.ToPointF(&cell.location), &par.desk.mapCfg.AstarMgr); len(path) > 1 {
					par.moveTo(path[1])
				} else {
					logger.Debugf("Par 回到原点寻路失败 起点=%v  终点=%v", par.location, flow_point)
				}
			}
		}

	}
}

func (par *Partner) standLocation(par_slot int) *Cell {
	if par.owner == nil {
		return nil
	}

	var cell *Cell
	var point *global.PointI
	dir := par.owner.GetDirection()

	desk := par.owner.Desk()
	if desk == nil {
		return nil
	}
	switch par_slot {
	case 1:
		point = maputil.Move(&par.owner.GetCell().location, maputil.ShiftDirection(dir, 1), 3)
		if cell = desk.mapInfo.getCell(point); cell != nil && !cell.block {
			return cell
		}
	case 2:
		point = maputil.Move(&par.owner.GetCell().location, maputil.ShiftDirection(dir, -1), 3)
		if cell = desk.mapInfo.getCell(point); cell != nil && !cell.block {
			return cell
		}
	case 3:
		point = maputil.Move(&par.owner.GetCell().location, maputil.ShiftDirection(dir, 3), 2)
		if cell = desk.mapInfo.getCell(point); cell != nil && !cell.block {
			return cell
		}
	case 4:
		point = maputil.Move(&par.owner.GetCell().location, maputil.ShiftDirection(dir, -3), 2)
		if cell = desk.mapInfo.getCell(point); cell != nil && !cell.block {
			return cell
		}
	default:
		return nil
	}
	return nil
}

// 寻找
func (par *Partner) processSearch() {
	// 嘲讽状态不在攻击其他目标
	if par.getBuffByType(constant.BuffTaunt) != nil && par.target != nil {
		return
	}

	if time.Now().Before(par.searchTime) {
		return
	}
	par.searchTime = time.Now().Add(time.Millisecond * 300)

	if !par.CanAttack() {
		return
	}

	if par.cfg.AttackRange == 0 {
		return
	}

	var distance int
	var closest IMapObject

	if par.owner.Target() != nil && par.owner.Target().Desk() != nil && par.desk.roomCfg.Roomtype != constant.PVPOneOnOneMap {
		par.target = par.owner.Target()
	} else {

		if par.desk.roomCfg.Roomtype == constant.PVEPeacefulMap || par.desk.roomCfg.Roomtype == constant.BattleMap || par.desk.roomCfg.Roomtype == constant.BossMap ||
			par.desk.roomCfg.Roomtype == constant.TrialTowerMap || par.desk.roomCfg.Roomtype == constant.RelicMap || par.desk.roomCfg.Roomtype == constant.GuildMap {

			for _, v := range par.desk.monsters {
				if v.dead {
					continue
				}
				if v.uid == par.uid {
					continue
				}
				tmpDistance := maputil.Distance(par.owner.Location(), v.Location())
				if tmpDistance > par.cfg.AttackDistance {
					continue
				}

				if distance == 0 || tmpDistance < distance {
					distance = tmpDistance
					closest = v
					if distance <= 2 {
						break
					}
				}
			}
		} else {
			for _, v := range par.desk.players {
				if v.dead {
					continue
				}
				if v.uid == par.uid {
					continue
				}
				if par.owner == v {
					continue
				}

				tmpDistance := maputil.Distance(par.location, v.Location())
				if distance == 0 || tmpDistance < distance {
					distance = tmpDistance
					closest = v
				}
			}
			for _, v := range par.desk.objects {
				if v.objectType == ObjectTypeDecoy || v.objectType == ObjectTypePartner {
					tmpDistance := maputil.Distance(par.location, v.Location())
					//pvp 房间伙伴扩大索敌范围
					if tmpDistance > par.cfg.AttackDistance+20 {
						continue
					}
					if !par.CanAttackTarget(v) {
						continue
					}

					//优先以战士职业位目标
					if closest != nil {
						if closest.IsWarrior() {
							if distance-tmpDistance < 3 {
								continue
							}
						} else {
							if v.IsWarrior() && distance-tmpDistance < 2 {
								distance = tmpDistance
								if closest.Job() == constant.PartnerWarrior {
									continue
								}
								closest = v
								continue
							}
						}
					}

					if distance == 0 || tmpDistance < distance {
						distance = tmpDistance
						closest = v
					}
				}
			}
		}

	}
	if closest != nil {
		par.target = closest
		par.roamPath = []*astar.APoint{}

		//logger.Debugf("Par 锁定目标 mUid: %d , 自身坐标:%v, 目标ID: %d  目标坐标: %v  目标类型:%v  距离 %d",
		//par.uid, par.location, par.target.Uid(), par.target.Location(), par.target.ObjectType(), maputil.Distance(par.location, closest.Location()))
	}

}

// 目标
func (par *Partner) processTarget() {

	if par.desk == nil {
		return
	}

	if par.target == nil {
		return
	}

	if par.target.Dead() || par.target.Uid() == 0 || par.target.Desk() == nil {
		par.target = nil
		return
	}

	if par.target.Desk() != par.desk {
		par.target = nil
		return
	}

	// 目标如果离主人过远 ，则不攻击
	if maputil.Distance(par.owner.Location(), par.target.Location()) > 8 {
		par.target = nil
		return
	}

	if !par.CanAttackTarget(par.target) {
		return
	}
	if par.desk.roomCfg.Roomtype == constant.PVEPeacefulMap || par.desk.roomCfg.Roomtype == constant.PVEWarMap {
		if par.desk.mapInfo.safeArea(par.target.Location()) {
			par.target = nil
			return
		}
	}

	if !par.inAttackRange(int(par.cfg.AttackDistance)) {
		if !par.CanMove() {
			return
		}

		if path := maputil.GetPath(par.location, par.target.Location(), &par.desk.mapCfg.AstarMgr); len(path) > 1 {
			par.moveTo(path[1])
		} else {
			logger.Debugf("Par 寻路失败 起点=%v  终点=%v  目标坐标 %v", par.location, par.target.Location(), par.target.Location())
		}

	} else {
		if len(par.cell.objects) > 1 {
			cell := par.desk.mapInfo.getAvailableCell(global.ToPointI(par.target.Location()), int32(par.cfg.AttackDistance))
			if cell != nil {
				if path := maputil.GetPath(par.location, &pb.Point{X: float64(cell.location.X), Y: float64(cell.location.Y)}, &par.desk.mapCfg.AstarMgr); len(path) > 1 {
					par.moveTo(path[1])
					return
				}
			}
		}

		if !par.IsWarrior() && maputil.Distance(par.location, par.target.Location()) < 2 && par.getBuffByType(constant.BuffKnockback) == nil {
			if par.CanMove() && par.getBuffByType(constant.BuffTaunt) == nil {
				var cell *Cell
				var point *global.PointI
				dir := maputil.DirectionFromPointI(&par.cell.location, &par.target.GetCell().location)
				point = maputil.Move(&par.cell.location, maputil.ShiftDirection(dir, 4), 1)
				if cell = par.desk.mapInfo.getCell(point); cell != nil && !cell.block {
					par.moveTo(&astar.APoint{X: cell.location.X, Y: cell.location.Y})
					par.moveTime = par.moveTime.Add(time.Duration(par.stat(pb.Attribute_MOVE_SPEED)*4) * time.Millisecond)
					return
				}
				point = maputil.Move(&par.cell.location, maputil.ShiftDirection(dir, 2), 1)
				if cell = par.desk.mapInfo.getCell(point); cell != nil && !cell.block {
					par.moveTo(&astar.APoint{X: cell.location.X, Y: cell.location.Y})
					par.moveTime = par.moveTime.Add(time.Duration(par.stat(pb.Attribute_MOVE_SPEED)*4) * time.Millisecond)
					return
				}
				point = maputil.Move(&par.cell.location, maputil.ShiftDirection(dir, -2), 1)
				if cell = par.desk.mapInfo.getCell(point); cell != nil && !cell.block {
					par.moveTo(&astar.APoint{X: cell.location.X, Y: cell.location.Y})
					par.moveTime = par.moveTime.Add(time.Duration(par.stat(pb.Attribute_MOVE_SPEED)*4) * time.Millisecond)
					return
				}
			}
		}

		par.attack()
	}
}

// 移动
func (par *Partner) moveTo(point *astar.APoint) bool {
	if par.desk == nil {
		return false
	}

	if !par.CanMove() {
		return false
	}

	//from := par.location
	to := &pb.Point{X: float64(point.X), Y: float64(point.Y)}
	if !par.desk.mapInfo.locationChange(par, to) {
		return false
	}

	par.moveTime = time.Now().Add(time.Duration(par.getMoveSpeed()) * time.Millisecond)

	packet := &pb.MovePush{Uid: par.uid, Location: par.location, Direction: par.direction}
	par.desk.Broadcast(protocol.RouteMovePush, packet)

	// if par.target != nil {
	// 	logger.Debugf("Par 向目标移动 mUid: %d  起点 %v, =====目标位置: %v   目标ID %d", par.uid, from, par.target.Location(), par.target.Uid())
	// } else {
	// 	logger.Debugf("Par 没有目标移动 mUid: %d  起点 %v, 目标位置: %v", par.uid, from, to)
	// }

	return true
}

// 攻击
func (par *Partner) attack() {
	if par.desk == nil {
		logger.Errorf("par 攻击失败，伙伴不在房间内 %d:%s ", par.uid, par.name)
		return
	}
	if !par.CanAttack() {
		return
	}

	if par.stat(pb.Attribute_ATTACK_SPEED) <= 100 {
		logger.Errorf("par 攻击失败，攻击速度异常 %d:%s ", par.uid, par.name)
		return
	}

	// 技能CD ,
	var castSkill *PlayerSkill
	var castSkillId int32
	normalSkillId := global.GetNormalSkillId(constant.MowJob(par.cfg.Job))
	if par.Owner() != nil && par.Owner().IsRobot() {
		for _, v := range par.skills {
			if v.CfgId == normalSkillId || v.CfgId == 0 {
				continue
			}
			if v.Cooldown.Before(time.Now()) {
				castSkill = v
				logger.Debugf("Par 攻击 attack pUid: %d , 目标: %d 选中技能 %d   ", par.uid, par.target.Uid(), castSkill.CfgId)
				break
			}
		}
	}
	if castSkill == nil {
		castSkillId = normalSkillId
	} else {
		castSkillId = castSkill.CfgId
	}

	skillCfg, _ := global.GetSkillCfg(castSkillId)
	if skillCfg == nil {
		return
	}
	//技能CD
	if castSkillId != NormalSkillID && skillCfg.CoolDown > 0 {
		castSkill.Cooldown = time.Now().Add(time.Duration(skillCfg.CoolDown) * time.Millisecond)
	}

	attackCells := []*Cell{}
	targets := []IMapObject{}
	targets = append(targets, par.target)

	for _, cell := range attackCells {
		if cell == nil || cell.block {
			continue
		}
		for _, obj := range cell.objects {
			bFind := false
			for _, have := range targets {
				if have.Uid() == obj.Uid() {
					bFind = true
					break
				}
			}
			if !bFind {
				if par.CanAttackTarget(obj) {

					if skillCfg.Object == constant.SkillEffectTypeMonster && obj.ObjectType() != ObjectTypeMonster {
						continue
					}
					if skillCfg.Object == constant.SkillEffectTypePlayer && obj.ObjectType() != ObjectTypePlayer {
						continue
					}
					targets = append(targets, obj)
				}
			}
		}
	}

	for _, t := range targets {
		if t.Uid() == par.uid {
			continue
		}

		// 安全区内不能攻击玩家
		if par.desk.roomCfg.Roomtype == constant.PVEPeacefulMap || par.desk.roomCfg.Roomtype == constant.PVEWarMap {
			if t.ObjectType() != ObjectTypeMonster && (par.desk.mapInfo.safeArea(par.location) || par.desk.mapInfo.safeArea(t.Location())) {
				continue
			}
		}

		// 技能距离
		if skillCfg.SType != constant.SkillTypeNormal && skillCfg.MaxDistance > 0 && maputil.Distance(par.location, t.Location()) > int(skillCfg.MaxDistance) {
			continue
		}

		//伤害延迟生效
		pardesk := par.desk
		part := t
		time.AfterFunc(time.Duration(skillCfg.DelayTime)*time.Millisecond, func() {
			if pardesk != nil && pardesk.status() == constant.DeskStatusPlaying {
				pardesk.delayAttackLock.Lock()
				defer pardesk.delayAttackLock.Unlock()

				if len(pardesk.chDelayAttack) >= chLen {
					logger.Warningln("Par chDelayAttck a通道已满")
					return
				}
				pardesk.chDelayAttack <- &DelayAction{ObjType: ObjectTypePartner,
					ObjUid:    par.uid,
					SkillId:   int32(castSkillId),
					Target:    part,
					Direction: par.direction,
					Location:  global.PointI{X: par.cell.location.X, Y: par.cell.location.Y}}
			}
		})
	}

	par.direction = maputil.DirectionFromPointF(par.location, par.target.Location())

	packet := &pb.AttackPush{AttackerId: par.uid,
		TargetUids: []int64{par.target.Uid()},
		SkillId:    int32(castSkillId),
		Location:   par.target.Location(),
		Direction:  par.direction}
	par.desk.Broadcast(protocol.RouteAttackPush, packet)

	par.setAttackTime(global.GetActionsetCfg(skillCfg.SkillID))

	for _, tri := range skillCfg.TriggerBuff {
		bufCfg, err := global.GetBuffCfg(tri)
		if err != nil {
			continue
		}
		if bufCfg.Type == constant.BuffTypeDebuff {
			continue
		}
		if global.RandIn(int(BaseRate)) >= int(skillCfg.Skilltrigger) {
			continue
		}

		var bufStats *Stats
		var addSec int32
		if bufCfg.Mark == constant.BuffTypeChainSkill {
			bufStats = &Stats{Attrs: make(map[pb.Attribute]int32)}
			for _, v := range skillCfg.Effect {
				bufStats.setStats(pb.Attribute(v.Key), v.Value)
			}

			addSec += skillCfg.UpperlevelSetting.DurationCon
		}
		par.AddBuff(bufCfg.ID, par, 0, nil, 0, int32(castSkillId))
	}

	//logger.Debugf("Par 攻击 attack mUid: %d , 目标: %d, 坐标 %v  目标坐标 %v 目标类型 %v ", par.uid, par.target.Uid(), par.location, par.target.Location(), par.target.ObjectType())
}

func (par *Partner) delayAttack(target IMapObject, skillId int32) {
	if !par.CanAttackTarget(target) {
		return
	}

	var advancedLevel int32
	skill := par.getSkill(skillId)
	if skill != nil {
		advancedLevel = skill.AdvancedLevel
	}
	skillCfg, err := global.GetSkillCfg(int32(skillId))
	if err != nil {
		return
	}

	// 命中-闪避
	hit := par.stats.getStats(pb.Attribute_HIT)
	if tstats := target.GetStats(); tstats != nil {
		dodge := target.GetStats().getStats(pb.Attribute_DODGE)
		if hit < dodge && global.RandIn(int(dodge)) > int(hit) {
			target.Dodged()
			return
		}
	}

	// 技能效果
	var power, basePower, skillPower float64

	basePower = float64(par.stats.getStats(par.AttackAttributes()))

	power += basePower
	for _, v := range skillCfg.Effect {
		switch pb.Attribute(v.Key) {
		case pb.Attribute_PHYSICAL_ATTACK, pb.Attribute_MAGIC_ATTACK:
			skillPower += float64(v.Value)
		case pb.Attribute_PHYSICAL_ATTACK_PERCENTAGE, pb.Attribute_MAGIC_ATTACK_PERCENTAGE, pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE:
			skillPower += float64(basePower) * (float64(v.Value) / BaseRate)
		case pb.Attribute_PVP_DAMAGE_PERCENTAGE:
			if target.ObjectType() == ObjectTypePlayer {
				skillPower += float64(basePower) * (float64(v.Value) / BaseRate)
			}
		case pb.Attribute_PVE_DAMAGE_PERCENTAGE:
			if target.ObjectType() == ObjectTypeMonster {
				skillPower += float64(basePower) * (float64(v.Value) / BaseRate)
			}
		case pb.Attribute_DMT_FINAL_INCREASE_FIXED:
			skillPower += float64(v.Value)
		}
	}

	power += skillPower

	var damage_percentage_increase, flat_damage_increase int32

	if target.ObjectType() == ObjectTypeMonster {
		flat_damage_increase += par.stat(pb.Attribute_DMT_INCREASE_FIXED_MONSTER)
		damage_percentage_increase += par.stat(pb.Attribute_PVE_DAMAGE_PERCENTAGE)
	} else if target.ObjectType() == ObjectTypePlayer {
		flat_damage_increase += par.stat(pb.Attribute_DMT_INCREASE_FIXED_PLAYER)
		damage_percentage_increase += par.stat(pb.Attribute_PVP_DAMAGE_PERCENTAGE)
	}
	damage_percentage_increase += par.stat(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE)
	flat_damage_increase += par.stat(pb.Attribute_DMT_FINAL_INCREASE_FIXED)

	//技能进阶效果
	if skillUpperlevel, err := global.GetUpperlevelSettingCfg(skillCfg.UID, advancedLevel); err == nil {
		damage_percentage_increase += skillUpperlevel.DamagePer
	}

	//暴击
	if len(skillCfg.Effect) > 0 || skillCfg.SType == constant.SkillTypeNormal {
		critical := par.stats.getStats(pb.Attribute_CRITICAL_STRIKE)
		tenacity := target.GetStats().getStats(pb.Attribute_TOUGHNESS)

		criticalRate := (math.Max(0, float64(critical-tenacity))*global.GlobalCfg.BaseValue.Crit.K + float64(global.GlobalCfg.BaseValue.Crit.PreValue))

		if global.RandIn(100) < int(criticalRate) {
			damage_percentage_increase += par.stat(pb.Attribute_CRITICAL_DAMAGE_PERCENTAGE)
			target.Critical()
		}
	}

	//攻击-防御
	if tstats := target.GetStats(); tstats != nil {
		power -= float64(tstats.getStats(maputil.DefenseAttributes(par.AttackAttributes())))
	}

	power += power * float64(damage_percentage_increase) / BaseRate
	power += float64(flat_damage_increase)

	if power <= 0 {
		power = float64(rand.Intn(2)) + 1
	}
	if len(skillCfg.Effect) == 0 && skillCfg.SType != constant.SkillTypeNormal {
		power = 0
	}

	suckblood := power * float64(par.stat(pb.Attribute_SUCKING_BLOOD_PERCENTAGE)) / float64(BaseRate)
	if skillCfg.SpecialEffect.DamTransfer > 0 {
		suckblood += power * float64(skillCfg.SpecialEffect.DamTransfer) / float64(BaseRate)
	}
	if suckblood > 0 {
		par.changeHp(int32(suckblood), par)
	}

	//par.logger.Debugf("Par 攻击 targetID: %d  目标位置: %v skillID: %d  Power:%f", target.Uid(), target.Location(), skillId, power)
	target.Attacked(par, skillId, advancedLevel, int32(power), true)
}

func (par *Partner) delayAction(target IMapObject, skillId int32, direction pb.Direction, Location global.PointI) {

	skillType := skillId / 100
	switch skillType {
	case constant.SkillTypeSwiftStrike:
		skillCfg, err := global.GetSkillCfg(int32(skillId))
		if err != nil {
			break
		}
		maxDistance := par.swiftStrikeEnd(skillId, direction)
		if maxDistance > 0 {
			attackCells := par.desk.mapInfo.getLineCells(&par.cell.location, direction, maxDistance)
			par.attackLocation(attackCells, skillCfg)
		}
	case constant.SkillTypeDecoy, constant.SkillTypeDecoyBoss:
		par.decoyEnd(skillId, direction)
	case constant.SkillTypeInstantaneousMove:
		par.instantaneousMoveEnd(skillId)
	case constant.SkillTypeSwordHeavenBreaker:
		if par.swordHeavenBreakerEnd(skillId) {
			skillCfg, err := global.GetSkillCfg(int32(skillId))
			if err != nil {
				break
			}

			attackCells := par.desk.mapInfo.getSquareCells(&Location, skillCfg.Radius)
			par.attackLocation(attackCells, skillCfg)
		}
	case constant.SkillTypeReborn: //回光返照
		par.lastGaspEnd(target, skillId)
	case constant.SkillTypeMassPurification,
		constant.SkillTypeMassPurificationBoss: //群体净化术
		par.MassPurificationEnd(skillId)
	// case constant.SkillTypeExecute:
	// 	par.ExecuteEnd(target, skillId)
	default:
		if target == nil {
			return
		}
		par.delayAttack(target, skillId)
	}
}

func (par *Partner) attackLocation(cells []*Cell, skillCfg *global.SkillConfig) {
	var targets []IMapObject
	for _, cell := range cells {
		if cell == nil || cell.block {
			continue
		}
		for _, obj := range cell.objects {
			bFind := false
			for _, have := range targets {
				if have.Uid() == obj.Uid() {
					bFind = true
					break
				}
			}
			if !bFind {
				if par.CanAttackTarget(obj) {

					if skillCfg.Object == constant.SkillEffectTypeMonster && obj.ObjectType() != ObjectTypeMonster {
						continue
					}
					if skillCfg.Object == constant.SkillEffectTypePlayer && obj.ObjectType() != ObjectTypePlayer {
						continue
					}
					targets = append(targets, obj)
				}
			}
		}
	}

	for _, t := range targets {
		if t.Uid() == par.uid {
			continue
		}

		if t.ObjectType() < ObjectTypePlayer && t.ObjectType() > ObjectTypeDecoy {
			continue
		}

		if t.ObjectType() == ObjectTypePlayer {
			if par.desk.roomCfg.Roomtype <= constant.PVEPeacefulMap || par.desk.roomCfg.Roomtype >= constant.SoloTowerMap {
				continue
			}
		}

		// 和平模式不能攻击除怪以外目标
		if par.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
			if t.ObjectType() != ObjectTypeMonster {
				continue
			}
		}

		// 安全区内不能攻击玩家
		if par.desk.roomCfg.Roomtype == constant.PVEWarMap {
			if t.ObjectType() != ObjectTypeMonster && (par.desk.mapInfo.safeArea(par.location) || par.desk.mapInfo.safeArea(t.Location())) {
				continue
			}
		}

		// 技能距离
		if skillCfg.MaxDistance > 0 && maputil.Distance(par.location, t.Location()) > int(skillCfg.MaxDistance) {
			continue
		}

		par.delayAttack(t, skillCfg.UID)
	}
}

func (par *Partner) getSkill(skillId int32) *PlayerSkill {
	for _, v := range par.skills {
		if v.CfgId == skillId {
			return v
		}
	}
	return nil
}

// 获取技能根据类型
func (par *Partner) getSkillByType(skillType int32) *PlayerSkill {
	for _, v := range par.skills {

		if v.CfgId/100 == skillType/100 {
			return v
		}
	}
	return nil
}

func (par *Partner) revive() {

	if par.desk == nil || par.Owner() == nil {
		logger.Errorf(" 伙伴复活失败 desk:[%t] 或者 owner:[%t] 为空 伙伴ID %d", par.desk == nil, par.Owner() == nil, par.uid)
		return
	}
	cell := par.desk.mapInfo.getAvailableCell(&par.Owner().GetCell().location, 3)
	if cell == nil {
		logger.Errorf("[%s] 伙伴复活失败,未找到复活点  伙伴ID %d", par.Owner().Name(), par.uid)
		return
	}
	par.SetLocation(global.ToPointF(&cell.location))
	par.SetCell(cell)

	par.dead = false
	par.reviveTimes++
	par.currentHP = par.stat(pb.Attribute_MAX_HEALTH)
	par.displayHP = par.currentHP
	par.stats.setStats(pb.Attribute_HEALTH, par.currentHP)
	par.resetBuff()
	par.desk.Broadcast(protocol.RouteRevivePush, &pb.RevivePush{
		Uid:       par.uid,
		Location:  par.location,
		Direction: par.direction,
		CurHp:     par.currentHP})

	par.AddBuff(int32(constant.GOD_MODE), par, int32(global.GlobalCfg.GVG.Relive.Protecttime*1000), nil, 0, 0)

	//logger.Debugf("[%s]伙伴复活！！！   当前血量 %d -- %d --- %d -- [%v]", par.name, par.currentHP, par.stat(pb.Attribute_HEALTH), par.GetMaxHP(), par.Location())
}

// 深度拷贝伙伴信息
func (par *Partner) deepCopy() *Partner {
	newPar := CreatePartner(par.cfgId)
	newPar.level = par.level
	newPar.star = par.star
	newPar.skills = make([]*PlayerSkill, len(par.skills))
	copy(newPar.skills, par.skills)

	for k, v := range par.stats.copyStats() {
		newPar.stats.setStats(k, v)
	}

	newPar.currentHP = par.GetMaxHP()
	newPar.displayHP = newPar.currentHP

	return newPar
}
