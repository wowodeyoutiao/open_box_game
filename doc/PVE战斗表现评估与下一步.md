# PVE 战斗表现评估与下一步

> 触发问题：进入战役能看到战斗了，但「打得比较快」「看不到攻击特效」。
> 结论：**不是系统没接完导致功能缺失，而是 1 个真 bug（已修）+ 2 个配置事实 + 1 个数值节奏问题。**
> 评估时间：2026-10-27 11:56

---

## 一、先说结论

| 现象 | 是不是缺陷 | 结论 |
| --- | --- | --- |
| 打得比较快 | 否 | 数值问题：最低级战役 + 18 级角色 + 客户端 0.55s 攻速 |
| 看不到攻击特效 | 部分是 | 怪物追击位置不同步（真 bug，已修）；其余是特效配置本身为空 |
| 一进场就被贴身围打 | 是 | 同组怪物共用出生点且与玩家重叠（已修） |
| 杀了怪任务没反应 | 是 | 战斗内击杀没派发任务目标（已修） |

---

## 二、为什么「打得比较快」

这不是逻辑错误，是三层叠加：

### 2.1 关卡是最低门槛战役

`battle 1401001`（战役 1-1）：

```text
Copytype   = 14        （战役）
Levellimit = 1
Powerlimit = 0
Timelimit  = 90 秒
Monster    = [ 1   × 2 @ (24,37)
               2001 × 2 @ (15,35)
               1   × 2 @ (33,33) ]
Settlement = 14×12, 2×20000, 1×20000
```

一共 6 只怪，3 组。

### 2.2 怪物血量极低

`monster 1`（★魔法树妖）：

```text
Level        = 5
MAX_HEALTH   = 196
PHYSICAL_ATK = 82
PHYSICAL_DEF = 8
Delay        = 500ms        （攻击间隔）
AttackDistance = 2 格
AttackEffect = null
Skills       = [999900]     （SkillID 9999，Needskill 0 = 普攻动作）
```

玩家当前 18 级，`MAX_HEALTH = 1880`。

原服伤害公式（`map_object.go` `delayAttack`）：

```text
power = max(MC, DC) - target.防御
```

单机 `BattleService.AttackUnit` 用的是同一套减法：

```text
power = max(1, attacker.Attack - target.Defense)
```

所以伤害公式**没有跑偏**，是角色 18 级打 5 级怪，一刀 196 血，**一刀一只**。

### 2.3 客户端攻速 0.55 秒一刀

`MainRole.RefrshBuffer`：

```text
_attackSpeed = (ATTACK_SPEED + 50) * 0.001
```

`ATTACK_SPEED` 来自 `RoleStatService.AddInitialSpeed` 默认 500：

```text
(500 + 50) * 0.001 = 0.55 秒 / 刀
```

6 只怪 ≈ 3.3 秒清完，再叠加 `WinSettleDelayMs = 3000`，整局 6 秒左右。

### 2.4 客户端是自动战斗

`MainRole.AutoWalkOrAttack`：只要 `targetID == 0` 就自动寻路 + 自动普攻，不需要手动操作。
所以玩家看到的是「角色自己冲上去，几秒打完，弹结算」。

**这一层不是 bug，是原玩法设计。** 想看清过程，只能用后面的高等级战役。

---

## 三、为什么「看不到攻击特效」

这条要拆成四层，只有第一层是真 bug。

### 3.1 真 bug：怪物追击没有推 MovePush（已修）

原服怪物追击是**每走一格广播一次**：

```go
// monster_mgr.go monster_move
packet := &pb.MovePush{Uid: m.uid, Location: m.location, Direction: m.direction}
m.desk.Broadcast(protocol.RouteMovePush, packet)
```

我 P0 的 `Tick` 只改了服务端坐标，**没有推 MovePush**。

结果：

```text
服务端：怪物已经跑到玩家身边 → 进入 AttackRange → 开始攻击
客户端：怪物还停在出生点 → 攻击动画和特效在很远的地方播
玩家看到：远处的怪没动，自己的血在掉
```

这就是「看不到攻击特效」的主因 —— **特效在播，只是播在屏幕另一头**。

已修：`BattleService.PushMonsterMoveIfNeeded`，按格补推 `MovePush`，并做 700ms 节流（客户端 `moveTo` 是 0.8s 补间，推太密会互相打断）。

### 3.2 怪物攻击特效配置本身就是空

`monster.json` 里 `AttackEffect` 全是 `null`：

```text
monster 1   AttackEffect = null
monster 2   AttackEffect = null
monster 3   AttackEffect = null
```

客户端 `Monster.SetAttack`：

```csharp
string poolKey = cfgInfo.AttackEffect;
...
if (string.IsNullOrEmpty(poolKey))
    return;          // ← 没有飞行特效，只有动画 + 音效
```

**原服也是这样。** 怪物攻击 = 普攻动作 + 攻击音效，没有弹道特效。
所以这不是单机没接，这是配置事实。

### 3.3 怪物技能 999900 在 skilleffect 表里没有记录

```text
skill 999900 → SkillID = 9999, Needskill = 0, Name = 普通攻击
skilleffect 里没有 TargetID = 999900
```

`skilleffect` 表只有 74 条，TargetID 1~ 系列，全部是**玩家技能**。
怪物不吃这张表，符合原服设计。

### 3.4 主角普攻特效只有「起手特效」

主角普攻链路：

```text
AttackPush.SkillId = 100
→ CFG_Skill.GetById(100) → SkillID = 1（普通攻击）
→ CFG_SkillEffect.GetById(1) → TargetID = 1
→ StartEffect = eff_ZSAttack
→ EffDic["eff_ZSAttack"] = Other/Effects/spine_tx/Prefabs/Zs_Attack.prefab
```

资源存在，能播。
但 `skilleffect` TargetID=1 只有 `StartEffect`，这三项都是空：

```text
HittedEffect    = null   （目标受击特效）
BallisticEffect = null   （弹道特效）
DeadEffect      = null   （死亡特效）
```

所以主角普攻看得见的只有：**角色身上闪一下起手特效 + 怪物受击飘字**。
没有弹道、没有命中爆点。这是配置表达的原服表现，不是缺失。

### 3.5 结算太快

`WinSettleDelayMs = 3000`，怪物全灭 3 秒后直接 `GameResultBattlePush`。
原服同样是「怪物清空 3 秒后结算」：

```go
if len(d.monsters) == 0 && now.After(d.beginTime.Add(3*time.Second)) {
    d.batlleGameOver(constant.GameOverTypeMDead)
}
```

保持一致，不调。

---

## 四、顺带查出来的真问题（已修）

### 4.1 同组怪物出生点完全重叠

`battle 1401001` 第一组：

```text
{ id: 1, num: 2, point: {x: 24, y: 37} }
```

两只怪共用同一个点，而我 P0 直接用了原点，导致：

```text
两只怪叠在一格
而且这一格正好是玩家出生点
→ 一进场就被贴身围打
```

原服用 `mapInfo.getAvailableCell(&m.bornLocation, ViewRange)` 找可用格，第一只占了之后第二只会挪开。

已修：同组怪绕圈散开（半径 1.2 格）。

### 4.2 战斗内击杀没有派发任务目标

原服 `MonsterInfo.die` 里做：

```go
player.tasks.dispatch(pb.TargetType_KILL_MONSTER_REQUIREMENT, 0, 1)
if m.cfg.MType == MTypeCommonBoss {
    player.tasks.dispatch(pb.TargetType_TOTAL_KILLS_OF_BOSS_MONSTERS, 0, 1)
} else {
    player.tasks.dispatch(pb.TargetType_TOTAL_KILLS_OF_NORMAL_MONSTERS, 0, 1)
}
```

我之前只在大厅遇怪回收（`UIHall.RecycleEncounterMonster`）里派发过，**战斗内没有**。

已修：`AttackUnit` 击杀分支派发 `KILL_MONSTER_REQUIREMENT(101)` + `TOTAL_KILLS_OF_NORMAL/BOSS_MONSTERS(115/116)`。

> 说明：当前 `task.json` 里没有 101 / 115 / 116 配置，所以这次不会多出可见任务，
> 但接线是对的，后续配置一加就生效。

### 4.3 攻击方向用了目标朝向

`AttackPush.Direction` 之前取 `target.Direction`，应该是**攻击者面向目标**的朝向。
已修：`DirectionTowards(target - attacker)`，同时回写 `attacker.Direction`。

### 4.4 进场多派发了一个战役不存在的任务目标

原服 `PVE_INSTANCE_ENTRY_REQUIREMENT(103)` 只在 `PVEPeacefulMap` / `PVEWarMap` 里派发：

```go
case constant.PVEPeacefulMap, constant.PVEWarMap:
    if !reJoin {
        p.tasks.dispatch(pb.TargetType_PVE_INSTANCE_ENTRY_REQUIREMENT, 0, 1)
```

`BattleMap`（战役 14）分支只设 `BattleId / BattleCfg / beginTime`，**不派发 103**。

已修：删掉 `BattleService.Enter` 里的 103 派发，跟原服一致。

---

## 五、原服「战役」到底该有什么（这次核对清楚的）

这次把 Go 常量枚举和客户端 RoomType 对齐了：

```text
server  constant.BattleMap   = 14    ← 当前游戏的战役
client  CFG_Room.RoomType.Fight = 14
room.json 600 战役副本  Roomtype = 14
battle.json Copytype 14 = 战役
```

**注意：`PVEWarMap = 2` 不是当前游戏的战役**，那是老玩法的「战争模式地图」，
掉怪、体力、103 任务那些分支都挂在它身上，不能拿来套。

按 `BattleMap = 14` 逐个对照，战役真实行为是：

| 行为 | 原服 | 单机 P0 | 状态 |
| --- | --- | --- | --- |
| 进入扣体力 | `room 600 Needenerge = 0`，不扣 | 不扣 | 一致 |
| 进场任务目标 | 不派发 103 | 已改为不派发 | 一致 |
| 玩家死亡 | 1.5 秒后 `GameOverTypeDeadFaild` | 同 | 一致 |
| 超时 | `BattleCfg.TimeLimit`（90s） | 同 | 一致 |
| 怪物清空 | 3 秒后 `GameOverTypeMDead` | 同 | 一致 |
| 怪物掉落 | `shouldProcessDrop` **不含 BattleMap** → 不掉 | 不掉 | 一致 |
| 通关任务 | `BATTLE_CHAPTERS_CLEARED(151)`，传 `BattleId` | 同 | 一致 |
| 章节进度 | `p.BattleMap.BattleId = d.BattleId`（直接覆盖） | `if (role.BattleId < BattleId)` 才推进 | **有意偏差** |
| 章节奖励 | 单独 `C2S_BattleReward` 领取 | 同 | 一致 |

> 章节进度这里我保留了 `<` 保护：原服是直接覆盖，重打低级关会把进度退回去。
> 单机没有跑马灯也没有运营约束，保留 `<` 更安全。

### 5.1 一个必须知道的事实

当前 `task.json` 里，**真正能被战斗驱动的任务只有一条**：

```text
BATTLE_CHAPTERS_CLEARED = 151
```

其余战斗相关目标（101 击杀 / 115 普通怪 / 116 BOSS / 152 首领层数 / 153 塔层数 / 154 遗迹章节）
在 `task.json` 里**都没有配置**。

所以「打完战役任务系统没变化」不是没接，是**配置里只有通关任务**。

---

## 六、这次实际改了什么

文件：`client/JE_WLY_Clinet/HotUpdateScripts/LocalServer/Services/BattleService.cs`

1. `BattleUnit` 增加 `IsBoss / PushedX / PushedY / LastMovePushMs`；
2. 新增 `PushMonsterMoveIfNeeded`，怪物按格补推 `MovePush`（700ms 节流 + 进入攻击距离时强制推）；
3. 新增 `DirectionTowards`，攻击朝向改为攻击者面向目标；
4. 同组怪物出生点绕圈散开，不再和玩家/彼此重叠；
5. 追击不再「贴到玩家身上」，停在 `AttackRange × 0.8` 处；
6. 击杀派发 `KILL_MONSTER_REQUIREMENT` + `TOTAL_KILLS_OF_NORMAL/BOSS_MONSTERS`；
7. 删除 `PVE_INSTANCE_ENTRY_REQUIREMENT(103)` 派发，对齐原服 `BattleMap` 分支。

### 测试结果

batchmode `all` 15 项全 PASS：

```text
box / box_guard / equip / save_box / idle / level / mail / bag_full
skill / skill_action / recycle / task / task_reset / battle / home_afk
```

```text
battle.enter=0
battle.monsters=6
battle.move=0
battle.clear=1401001
battle.coin=1159778
```

```text
正式构建：待跑
测试构建：1,366,016 bytes
```

---

## 七、下一步该做什么（按优先级）

### P0.5　战斗可读性补齐（小改动，先做）

这几项都是「让玩家能看懂战斗」，不改数值、不改配置：

| 项 | 现状 | 建议 |
| --- | --- | --- |
| 玩家出生点 | 硬编码 `(24,37)`（借用第一组怪的点） | 改用 `map.json` 的 `Born[0]`，需要给 `CFG_Map` 补 `Born` 字段 |
| 怪物行走格子校验 | 直接按直线走 | 校验客户端 `SceneManager.GetOffZ` 是否为 Block，避免怪走进墙里 |
| 普攻命中表现 | 只有 StartEffect + 飘字 | 如果要命中爆点，需在 `skilleffect` 给 TargetID=1 补 `HittedEffect`（改配置，不改代码） |
| 战斗内怪物剩余数 | 没有 UI 反馈 | 复用 `MonsterOutPush` 推送剩余数（协议已存在） |

> 其中「玩家出生点」最值得做：现在第一组怪就刷在出生点旁边 1.2 格，
> 一进场立刻被打，看不出「走路→索敌→交战」的过程。

### P1　战斗内核补齐（主要工作量）

按原服 `desk.go` / `monster_mgr.go` / `map_object.go` 逐个补：

```text
1. 技能释放
   C2S_Attack 已通，但只算普攻。需要：
   - SkillM 冷却校验（客户端已有 GetGameSkillDealy）
   - Powerfactor 技能系数
   - MaxDistance / Radius 范围判定
   - 多目标 TargetUids

2. Buff
   TriggerBuff / Bufftime / buffeffect.json
   至少要支持：加攻、加防、减伤、护盾（战斗结算会用到）

3. 怪物 AI
   ViewRange 索敌 / Chase 追击 / 脱战回位
   现在是「全图无条件追玩家」

4. A* 寻路
   服务端 mapInfo 有格子，单机需要一份只读地图数据
   没有的话怪物会穿墙

5. 多波怪 / 分批刷怪
   battle.MonsterShow 字段还没用

6. 伙伴
   PartnerList 已推空数组，伙伴 AI 没做

7. 复活
   战役 BattleMap 是死亡直接失败，不做复活
```

### P2　其他 PVE 模式复用同一内核

`BattleMap` 内核跑稳之后，下面三个几乎是复制粘贴 + 换 Copytype：

```text
首领 BossMap   = 15   （多 ChallengeTime 扣减 + BOSS 任务）
试炼塔 TowerMap = 16  （层数 Layers + 层奖励）
遗迹 RelicMap  = 17   （评价 + 挑战次数）
```

### P3　独立状态机（不能塞进 BattleService）

```text
每日副本  DDailyMap      轮次 / 评分 / 每日次数
守卫海岛  PVEPeacefulMap 挂机刷怪，不是房间通关
```

### 明确不做

```text
世界BOSS    全服数据依赖
一战到底    排行语义，单机需要重设计
行会副本    家族依赖
PVP / 乱斗  真人匹配，服务端 Matching 本身就是旧多人框架
排行榜 / 跑马灯 / 跨服 / 运营统计
```

---

## 八、建议的下一步

先做 **P0.5 的「玩家出生点改用 map.Born」** 这一项，理由：

1. 它是现在「一进场就贴身挨打、看不到交战过程」的直接原因；
2. 改动很小：`CFG_Map` 加一个 `Born` 字段 + `BattleService` 读一次；
3. 风险可控：读不到就回退到当前的 `(24,37)`，不会黑屏。

然后再进 **P1 的技能释放 + Buff**，这两项做完，战斗才算「像原服」。
A* 和伙伴放到技能之后，因为它们的依赖更多。
