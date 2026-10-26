using JEngine.Singleton;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 战斗领域服务（阶段2 的主体）。
    ///
    /// 【移植来源】服务端约 7,400 行：
    ///   map_object.go(1710)  属性 / Buff / 扣血 / 攻击判定 / 射程
    ///   partner.go(1487)     伙伴属性与 AI
    ///   monster_mgr.go(1231) 怪物 AI（游荡 / 索敌 / A* 寻路 / 攻击 / 掉落）
    ///   player.go 战斗部分    玩家攻击、技能释放
    ///   skillmgr.go(484)     技能效果与冷却
    ///   logic_mow.go(325)    挂机刷怪
    ///   timer.go(125)        定时 tick（每日重置）
    ///
    /// 【架构约定】
    /// - 战斗是「定时 tick 驱动」而非每帧，用固定步长累加器模拟服务端 tick
    /// - 寻路必须移植服务端同一套 A*，不要换 Unity 原生寻路（否则走位差异影响命中判定）
    /// - 写完实测 WebGL 帧率，不达标再把本类迁到 Assets/Scripts（AOT）
    ///
    /// 【阶段1 状态】空骨架。
    /// </summary>
    public class BattleService : SingletonTemplate<BattleService>
    {
        // 待接入：
        // C2S_Attack          -> Attack(skillId, targets, location, direction)
        // C2S_Move            -> Move(...)
        // C2S_Revive          -> Revive()
        // C2S_AutoPlay        -> SetAutoPlay(bool)
    }
}
