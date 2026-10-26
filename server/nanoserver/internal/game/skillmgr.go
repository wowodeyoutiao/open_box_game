package game

import (
	"math"
	"math/rand"
	"nanoserver/global"
	"nanoserver/pkg/constant"
	"nanoserver/pkg/maputil"
	"nanoserver/protocol"
	"nanoserver/protocol/protobuf/pb"
	"time"
)

// 迅影追击 SkillTypeSwiftStrike
func (o *MapObject) swiftStrikeEnd(skillId int32, direction pb.Direction) int32 {

	var maxDistance int32 = 0
	if o.dead {
		return maxDistance
	}
	skill := o.getSkill(skillId)
	if skill == nil {
		return maxDistance
	}
	skillCfg, err := global.GetSkillCfg(int32(skillId))
	if err != nil {
		return maxDistance
	}

	var endCell *Cell
	for i := 0; i < int(skillCfg.Radius); i++ {
		point := maputil.Move(&o.cell.location, direction, int(i))
		if cell := o.desk.mapInfo.getCell(point); cell != nil {
			if !cell.block {
				maxDistance = int32(i + 1)
				endCell = cell
				continue
			}
		}
		break
	}
	if endCell == nil {
		return maxDistance
	}

	if o.desk.mapInfo.locationChange(o, &pb.Point{X: float64(endCell.location.X), Y: float64(endCell.location.Y)}) {
		o.desk.Broadcast(protocol.RouteTeleportPush, &pb.TeleportPush{Uid: o.uid, Location: o.location, Direction: o.direction})
	}
	return maxDistance
}

// 拒敌千里 SkillTypeRepelFoe 受击者后退
func (o *MapObject) repelFoeEnd(skillId int32, attacker IMapObject) {
	if o.dead {
		return
	}
	skillCfg, err := global.GetSkillCfg(int32(skillId))
	if err != nil {
		return
	}
	var backDir pb.Direction
	var endCell *Cell
	backDir = maputil.DirectionFromPointI(&attacker.GetCell().location, &o.cell.location)
	for i := 1; i <= int(skillCfg.SpecialEffect.BackDistence); i++ {
		point := maputil.Move(&o.cell.location, backDir, int(i))
		if cell := o.desk.mapInfo.getCell(point); cell != nil {
			if !cell.block {
				endCell = cell
				continue
			}
		}
		break
	}
	if endCell == nil {
		return
	}

	//p.logger.Debugf("拒敌千里 起始坐标 %v 终点坐标 %v  距离 %d", p.cell.location, endCell.location, maputil.Distance(p.location, &pb.Point{X: float64(endCell.location.X), Y: float64(endCell.location.Y)}))
	if o.desk.mapInfo.locationChange(o, &pb.Point{X: float64(endCell.location.X), Y: float64(endCell.location.Y)}) {
		o.desk.Broadcast(protocol.RouteTeleportPush, &pb.TeleportPush{Uid: o.uid, Location: o.location, Direction: o.direction})
	}
}

// 替身宝宝 SkillTypeDecoy
func (o *MapObject) decoyEnd(skillId int32, direction pb.Direction) {
	if o.dead {
		return
	}
	skill := o.getSkill(skillId)
	if skill == nil {
		return
	}
	skillCfg, err := global.GetSkillCfg(int32(skillId))
	if err != nil {
		return
	}
	location := o.cell.location
	// 人先飞走
	var endCell *Cell
	for i := 0; i < int(skillCfg.SpecialEffect.FlyDistence); i++ {
		point := maputil.Move(&o.cell.location, direction, int(i))
		if cell := o.desk.mapInfo.getCell(point); cell != nil {
			if !cell.block {
				endCell = cell
				continue
			}
		}
		break
	}

	if o.desk.mapInfo.locationChange(o, &pb.Point{X: float64(endCell.location.X), Y: float64(endCell.location.Y)}) {
		o.desk.Broadcast(protocol.RouteTeleportPush, &pb.TeleportPush{Uid: o.uid, Location: o.location, Direction: o.direction})
	}

	// 生成替身
	for _, tri := range skillCfg.TriggerBuff {
		bufCfg, err := global.GetBuffCfg(tri)
		if err != nil {
			continue
		}

		// 筛除特殊buff
		if bufCfg.Pretype == constant.BuffDecoy {
			stats := &Stats{Attrs: map[pb.Attribute]int32{}}
			for _, v := range skillCfg.Skillbuff {
				stats.setStats(pb.Attribute(v.Key), v.Value)
			}

			// 技能进阶效果
			if skillUpperlevel, err := global.GetUpperlevelSettingCfg(skillCfg.UID, skill.AdvancedLevel); err == nil {
				if skillUpperlevel.DamagePer > 0 {
					stats.setStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE, skillUpperlevel.DamagePer)
				}
			}

			// 替身的最大生命值
			decoyHp := float64(o.GetMaxHP()) * float64(stats.getStats(pb.Attribute_HEALTH_PERCENTAGE)) / BaseRate
			stats.setStats(pb.Attribute_HEALTH, int32(decoyHp))
			stats.changeStats(pb.Attribute_MAX_HEALTH, int32(decoyHp))
			mc := float64(o.GetMC()) * float64(stats.getStats(pb.Attribute_MAGIC_ATTACK_PERCENTAGE)) / BaseRate
			mc = math.Max(mc, 1)
			stats.setStats(pb.Attribute_MAGIC_ATTACK, int32(mc))

			if buffOb := CreateBuffObject(bufCfg.ID, ObjectTypeDecoy, o, skillCfg.Bufftime, skillId, stats); buffOb != nil {
				o.desk.addObject(buffOb, location)
			}
		}
	}
}

// 瞬移 SkillTypeInstantaneousMove
func (o *MapObject) instantaneousMoveEnd(skillId int32) {
	if o.dead {
		return
	}
	skill := o.getSkill(skillId)
	if skill == nil {
		return
	}
	skillCfg, err := global.GetSkillCfg(int32(skillId))
	if err != nil {
		return
	}

	var endCell *Cell
	if global.RandIn(int(BaseRate)) <= int(skillCfg.Skilltrigger) {
		endCell = o.desk.mapInfo.getAvailableCell(&o.cell.location, skillCfg.Radius)
	}
	if endCell == nil {
		endCell = o.cell
	}

	if o.desk.mapInfo.locationChange(o, &pb.Point{X: float64(endCell.location.X), Y: float64(endCell.location.Y)}) {
		o.desk.Broadcast(protocol.RouteTeleportPush, &pb.TeleportPush{Uid: o.uid, Location: o.location, Direction: o.direction})
	}
}

// 剑开天门（特技）SkillTypeSwordHeavenBreaker
func (o *MapObject) swordHeavenBreakerEnd(skillId int32) bool {
	if o.dead {
		return false
	}
	if skill := o.getSkill(skillId); skill == nil {
		return false
	}
	return true
}

// 魔法酸雨 SkillTypeMagicAcidRain
func (o *MapObject) magicAcidRain(damage float64, skillID int32) {
	if o.dead {
		return
	}

	skill := o.getSkill(skillID)
	if skill == nil {
		return
	}

	skillCfg, err := global.GetSkillCfg(int32(skillID))
	if err != nil {
		return
	}
	attackCells := o.desk.mapInfo.getSquareCells(&o.cell.location, skillCfg.Radius)
	var targets []IMapObject
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
				if o.CanAttackTarget(obj) {

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
		if t.Uid() == o.uid {
			continue
		}

		if t.ObjectType() < ObjectTypePlayer && t.ObjectType() > ObjectTypeDecoy {
			continue
		}

		if t.ObjectType() == ObjectTypePlayer {
			if o.desk.roomCfg.Roomtype <= constant.PVEPeacefulMap || o.desk.roomCfg.Roomtype >= constant.SoloTowerMap {
				continue
			}
		}

		// 和平模式不能攻击除怪以外目标
		if o.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
			if t.ObjectType() != ObjectTypeMonster {
				continue
			}
		}

		// 安全区内不能攻击玩家
		if o.desk.roomCfg.Roomtype == constant.PVEWarMap {
			if t.ObjectType() != ObjectTypeMonster && (o.desk.mapInfo.safeArea(o.location) || o.desk.mapInfo.safeArea(t.Location())) {
				continue
			}
		}

		// 命中-闪避
		hit := o.stats.getStats(pb.Attribute_HIT)
		if tstats := t.GetStats(); tstats != nil {
			dodge := t.GetStats().getStats(pb.Attribute_DODGE)
			if hit < dodge && global.RandIn(int(dodge)) > int(hit) {
				t.Dodged()
				continue
			}
		}

		if t.ObjectType() == ObjectTypeMonster {
			damage += float64(o.stat(pb.Attribute_DMT_INCREASE_FIXED_MONSTER))
			damage += damage * float64(o.stat(pb.Attribute_PVE_DAMAGE_PERCENTAGE)) / BaseRate
		} else if t.ObjectType() == ObjectTypePlayer {
			damage += float64(o.stat(pb.Attribute_DMT_INCREASE_FIXED_PLAYER))
			damage += damage * float64(o.stat(pb.Attribute_PVP_DAMAGE_PERCENTAGE)) / BaseRate
		}

		damage += float64(o.stat(pb.Attribute_DMT_FINAL_INCREASE_FIXED))
		damage += damage * float64(o.stat(pb.Attribute_DMT_FINAL_INCREASE_PERCENTAGE)) / BaseRate

		//攻击-防御
		if tstats := t.GetStats(); tstats != nil {
			damage -= float64(tstats.getStats(maputil.DefenseAttributes(o.AttackAttributes())))
		}

		if damage <= 0 {
			damage = float64(rand.Intn(2)) + 1
		}

		suckblood := damage * float64(o.stat(pb.Attribute_SUCKING_BLOOD_PERCENTAGE)) / float64(BaseRate)
		if suckblood > 0 {
			o.changeHp(int32(suckblood), o)
		}

		//p.logger.Debugf("P 魔法酸雨 targetID: %d  目标位置: %v skillID: %d  Power:%f", t.Uid(), t.Location(), skillID, damage)
		t.Attacked(o, skillID, skill.AdvancedLevel, int32(damage), true)
	}
}

// 回光返照
func (o *MapObject) lastGasp(target IMapObject, skillID int32) bool {
	if o.dead {
		return false
	}
	if !o.CanHelpTarget(target) {
		return false
	}

	var skill = o.getSkill(skillID)
	if skill == nil {
		return false
	}
	if time.Now().Before(skill.Cooldown) {
		return false
	}
	skillCfg, err := global.GetSkillCfg(int32(skillID))
	if err != nil {
		return false
	}

	if skillCfg.CoolDown > 0 {
		skill.Cooldown = time.Now().Add(time.Duration(float64(skillCfg.CoolDown)*0.99) * time.Millisecond)
		//o.pushMsg(protocol.RouteSkillCooldownPush, &pb.SkillCooldownPush{CfgId: skillID, Delay: skillCfg.CoolDown})
	}

	//延迟生效
	pdesk := o.desk
	pt := target
	time.AfterFunc(time.Duration(skillCfg.DelayTime)*time.Millisecond, func() {
		if pdesk != nil && pdesk.status() == constant.DeskStatusPlaying {
			pdesk.delayAttackLock.Lock()
			defer pdesk.delayAttackLock.Unlock()
			if len(pdesk.chDelayAttack) >= chLen {
				logger.Warningln("P3 chDelayAttack 通道已满")
				return
			}
			pdesk.chDelayAttack <- &DelayAction{ObjType: o.ObjectType(),
				ObjUid:    o.uid,
				SkillId:   skillID,
				Target:    pt,
				Direction: o.direction,
				Location:  global.PointI{X: o.cell.location.X, Y: o.cell.location.Y}}
		}
	})
	return true
}

func (o *MapObject) lastGaspEnd(target IMapObject, skillID int32) {
	if !target.Dead() {
		return
	}

	var skillAdvanceLevel int32
	if skill := o.getSkill(skillID); skill != nil {
		skillAdvanceLevel = skill.AdvancedLevel
		//技能CD
		if skillCfg, err := global.GetSkillCfg(skillID); err == nil {
			if skillCfg.CoolDown > 0 {
				skill.Cooldown = time.Now().Add(time.Duration(float64(skillCfg.CoolDown)*0.99) * time.Millisecond)
				o.pushMsg(protocol.RouteSkillCooldownPush, &pb.SkillCooldownPush{CfgId: skillID, Delay: skillCfg.CoolDown})
			}
			target.Revive(skill, skillAdvanceLevel, 1)
		}
		logger.Debugf(" %d:%s 伙伴复活 目标[%s] 复活后血量 %d 总血量 %d", o.uid, o.name, target.Name(), target.GetMaxHP(), target.GetMaxHP())
	}
}

// 群体净化
func (o *MapObject) MassPurificationEnd(skillID int32) {
	if o.dead {
		return
	}
	if skill := o.getSkill(skillID); skill != nil {

		for _, friend := range o.Friends() {
			friend.CleanDebuff()
		}
		o.SkillBuffTrigger(skill)
	}
}

// 幻象爆炸
func (o *MapObject) DecoyExplosionEnd() {
	var targets []IMapObject
	var targetsUids []int64
	Radius := 2
	if skillCfg, err := global.GetSkillCfg(o.linkedSkill); err == nil {
		Radius = int(skillCfg.Radius)
	}
	attackCells := o.desk.mapInfo.getSquareCells(&o.cell.location, int32(Radius))
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
				if o.CanAttackTarget(obj) {
					targets = append(targets, obj)
				}
			}
		}
	}

	for _, t := range targets {
		if t.Uid() == o.uid {
			continue
		}

		if t.ObjectType() < ObjectTypePlayer && t.ObjectType() > ObjectTypeMonster {
			continue
		}

		// 非PVP地图不能攻击玩家
		if t.ObjectType() == ObjectTypePlayer || t.ObjectType() == ObjectTypeDecoy {
			if o.desk.roomCfg.Roomtype <= constant.PVEPeacefulMap || o.desk.roomCfg.Roomtype >= constant.SoloTowerMap {
				logger.Debugf("目标无法攻击,房间类型%+v userId: %d , targetId:%d, targetType:%+v", o.desk.roomCfg.Roomtype, o.uid, t.Uid(), t.ObjectType())
				continue
			}
		}

		// 和平模式不能攻击除怪以外目标
		if o.desk.roomCfg.Roomtype == constant.PVEPeacefulMap {
			if t.ObjectType() != ObjectTypeMonster {
				continue
			}
		}

		// 安全区内不能攻击玩家
		if o.desk.roomCfg.Roomtype == constant.PVEWarMap {
			if (t.ObjectType() != ObjectTypeMonster || t.ObjectType() == ObjectTypeDecoy) && (o.desk.mapInfo.safeArea(o.location) || o.desk.mapInfo.safeArea(t.Location())) {
				continue
			}
		}

		targetsUids = append(targetsUids, t.Uid())

		packet := &pb.AttackPush{
			AttackerId: o.uid,
			TargetUids: targetsUids,
			SkillId:    o.linkedSkill,
			Location:   o.location,
			Direction:  o.direction}
		o.desk.Broadcast(protocol.RouteAttackPush, packet)

		//伤害延迟生效
		odesk := o.desk
		ot := t
		time.AfterFunc(time.Duration(500)*time.Millisecond, func() {
			if odesk != nil && odesk.status() == constant.DeskStatusPlaying {
				odesk.delayAttackLock.Lock()
				defer odesk.delayAttackLock.Unlock()
				if len(odesk.chDelayAttack) >= chLen {
					logger.Warningln("O chDelayAttack 通道已满")
					return
				}
				odesk.chDelayAttack <- &DelayAction{
					ObjType:   ObjectTypeDecoy,
					ObjUid:    o.uid,
					SkillId:   o.linkedSkill,
					Target:    ot,
					Direction: o.direction}
			}
		})
	}

	o.delTime = time.Now().Add(600 * time.Millisecond) // 替身延迟消失
}

// 斩杀
func (o *MapObject) ExecuteEnd(target IMapObject, skillID int32) {
	if target.Dead() {
		return
	}
	target.Attacked(o, skillID, 0, 0, false)
}
