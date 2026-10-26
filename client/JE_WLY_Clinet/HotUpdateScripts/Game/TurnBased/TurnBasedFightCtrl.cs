using DG.Tweening;

using HotUpdateScripts.ProtoM;

using JEngine.Core;
using JEngine.Event;
using JEngine.Helper;

using MetaFramework.UI;

using pb;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

using Object = UnityEngine.Object;

namespace HotUpdateScripts.Game
{
    public class TurnBasedFightCtrl : MonoBehaviour
    {
        public enum BattleState
        {
            None,
            Init,
            SelectAttacker,
            Attack,
            AttackCompleted,
            Over,
        }

        // 战斗状态
        public BattleState currentBattleState;

        // 当前攻击者角色
        public TurnBasedRole currentBattleRole;

        private readonly string _positionRootPath = "Common/Prefab/";

        private static TurnBasedFightCtrl _instance;
        public static TurnBasedFightCtrl Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("FightCtrl");
                    _instance = go.AddComponent<TurnBasedFightCtrl>();
                }
                return _instance;
            }
        }

        private Camera _MainCamer;
        public Camera MainCamer
        {
            get
            {
                if (_MainCamer == null)
                {
                    _MainCamer = GameObject.Find("Main Camera").GetComponent<Camera>();
                }
                return _MainCamer;
            }
        }

        private Camera _UICamera;
        public Camera UICamera
        {
            get
            {
                if (_UICamera == null)
                {
                    _UICamera = GameObject.Find("UICamera").GetComponent<Camera>();
                }
                return _UICamera;
            }
        }

        //特效节点
        private Transform _effectRoot;
        public Transform EffectRoot
        {
            get
            {
                if (_effectRoot == null)
                {
                    _effectRoot = new GameObject("EffectRoot").transform;
                    _effectRoot.position = new Vector3(0, 0, -3);
                }
                return _effectRoot;
            }
        }

        private Transform _roleRoot;
        public Transform RoleRoot
        {
            get
            {
                if (_roleRoot == null)
                {
                    GameObject roleRoot = GameObject.Find("RoleRoot");
                    if (!roleRoot)
                    {
                        roleRoot = new GameObject("RoleRoot");
                    }
                    _roleRoot = roleRoot.transform;
                }
                return _roleRoot;
            }
        }

        //蓝方人物
        private Transform _blueRoleRoot;
        public Transform BlueRoleRoot
        {
            get
            {
                if (_blueRoleRoot == null)
                {
                    _blueRoleRoot = RoleRoot.Find("BlueRoot(Clone)");
                }
                return _blueRoleRoot;
            }
        }

        //红方人物
        private Transform _redRoleRoot;
        public Transform RedRoleRoot
        {
            get
            {
                if (_redRoleRoot == null)
                {
                    _redRoleRoot = RoleRoot.Find("RedRoot(Clone)");
                }
                return _redRoleRoot;
            }
        }

        public float attackMoveSpeed = 0.2f;

        public TurnBasedBattle.TBPlayer bluePlayer;

        public TurnBasedBattle.TBPlayer redPlayer;

        // 攻击效果信息
        public List<TurnBasedBattle.Round.Effect> targetEffects = new List<TurnBasedBattle.Round.Effect>();

        TimeItem m_StartBattleTimeItem;

        public void ClearStartBattleTime()
        {
            if (m_StartBattleTimeItem != null)
            {
                GameHelper.Instance.RemoveInvoke(m_StartBattleTimeItem);
                m_StartBattleTimeItem = null;
            }
        }

        public void OnInit()
        {
            GameRoomM.Instance._GameState = GameRoomM.GameState.Ready;
            GameRoomM.Instance.CFGRoom = CFG_Room.GetById(TurnBasedM.Instance.roomId);
            bluePlayer = TurnBasedM.Instance.GetPlayerByCampType(TurnBasedM.CampType.BLUE_CAMP);
            redPlayer = TurnBasedM.Instance.GetPlayerByCampType(TurnBasedM.CampType.RED_CAMP);
            MainCamer.orthographicSize = 22;

            LoadMap();

            UITool.Open<UIPVPBeginTime>(null, TurnBasedM.Instance.GetFirstAttackCamp() == TurnBasedM.CampType.BLUE_CAMP ?
                UIPVPBeginTime.ShowType.MyTeamFirstAttack : UIPVPBeginTime.ShowType.OtherTeamFirstAttack);

            UITool.Open<UIGame3D>();
            UITool.Open<UIGameTurnBased>();
            LoadPositionRoot();
            UITool.Close<UILoading>();

            MainCamer.DOOrthoSize(20, 1f).SetEase(Ease.OutQuad);

            currentBattleState = BattleState.None;

            m_StartBattleTimeItem = GameHelper.Instance.AddInvoke(new TimeItem(3, () =>
            {
                if (Object.Equals(TurnBasedRoleManager.Instance, null))
                    return;

                ChangeState(BattleState.Init);
                JEvent.defaultEvent.Post(new GameEvent.PVP_JumpGameCountdown());
            }));
        }

        public async void LoadMap()
        {
            CFGItem_Map MapCfg = CFG_Map.GetById(GameRoomM.Instance.CFGRoom.MapId);
            await ResouseHelper.Instance.LoadSprite($"Terrain/Texture/Map_{MapCfg.MapID}", GameObject.Find("Map").GetComponent<SpriteRenderer>());
        }

        public async void LoadPositionRoot()
        {
            await ResouseHelper.Instance.LoadGameObj(_positionRootPath + "BlueRoot", o =>
            {
                _blueRoleRoot = Instantiate(o, RoleRoot).transform;
            });
            await ResouseHelper.Instance.LoadGameObj(_positionRootPath + "RedRoot", o =>
            {
                _redRoleRoot = Instantiate(o, RoleRoot).transform;
            });

            await LoadRole(TurnBasedM.CampType.BLUE_CAMP);

            await LoadRole(TurnBasedM.CampType.RED_CAMP);
        }

        public async Task LoadRole(TurnBasedM.CampType campType)
        {
            var players = campType switch
            {
                TurnBasedM.CampType.BLUE_CAMP => bluePlayer,
                TurnBasedM.CampType.RED_CAMP => redPlayer,
                _ => null,
            };

            for (int i = 0; i < players.Objs.Count; i++)
            {
                if (players.Objs[i].Uid == 0 || players.Objs[i].Type == ObjectType.INVALID_ObjectType)
                {
                    continue;
                }

                string poolKey = "";
                string poolPath = "";

                switch (players.Objs[i].Type)
                {
                    case ObjectType.PLAYER:
                        poolKey = "MainRoleFight_" + players.Objs[i].Job + players.Objs[i].Gender;
                        poolPath = GameConst.GetModelByJobAndSex((JobType)players.Objs[i].Job, (SexType)players.Objs[i].Gender, true);
                        break;
                    case ObjectType.PARTNER:
                        poolKey = "Fight_Partner_" + players.Objs[i].CfgID;
                        poolPath = GameConst.GetMonsterModelByID(poolKey);
                        break;
                    default:
                        break;
                }

                if (!PoolHelper.Instance.CheckPool(poolKey))
                {
                    var baseObj = new JPrefab(poolPath, true);
                    await baseObj.WaitForAsyncLoading();
                    PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
                }

                GameObject _mainRole = PoolHelper.Instance.GetPoolObj(poolKey);
                _mainRole.gameObject.name = poolKey + "_" + players.Objs[i].Uid;

                TurnBasedRole turnBasedRole = _mainRole.GetComponent<TurnBasedRole>();
                if (!turnBasedRole) turnBasedRole = _mainRole.AddComponent<TurnBasedRole>();

                int position = players.Objs[i].Position + 1;

                switch (campType)
                {
                    case TurnBasedM.CampType.BLUE_CAMP:
                        _mainRole.transform.SetParent(BlueRoleRoot.Find($"Pos_{position}"));
                        _mainRole.transform.localRotation = Quaternion.Euler(0, 180, 0);
                        turnBasedRole.campType = TurnBasedM.CampType.BLUE_CAMP;
                        TurnBasedRoleManager.Instance.AddBuleRole(turnBasedRole, position);

                        break;
                    case TurnBasedM.CampType.RED_CAMP:
                        _mainRole.transform.SetParent(RedRoleRoot.Find($"Pos_{position}"));
                        _mainRole.transform.localRotation = Quaternion.Euler(0, 0, 0);
                        turnBasedRole.campType = TurnBasedM.CampType.RED_CAMP;
                        TurnBasedRoleManager.Instance.AddRedRole(turnBasedRole, position);

                        break;
                    default:
                        break;
                }

                _mainRole.transform.localPosition = Vector3.zero;
                turnBasedRole.uid = players.Objs[i].Uid;
                turnBasedRole.cfgId = players.Objs[i].CfgID;
                turnBasedRole.roleName = players.Objs[i].Name;
                turnBasedRole.maxHp = players.Objs[i].MaxHp;
                turnBasedRole.currentHp = players.Objs[i].CurHp;
                turnBasedRole.job = players.Objs[i].Job;
                turnBasedRole.position = position;
                turnBasedRole.roleType = players.Objs[i].Type;

                switch (players.Objs[i].Type)
                {
                    case ObjectType.PLAYER:
                        _mainRole.transform.localScale = Vector3.one * 0.8f;
                        break;
                    case ObjectType.PARTNER:
                        _mainRole.transform.localScale = Vector3.one * GameConst.GetMonsterScalModelByID(poolKey);
                        break;
                    default:
                        break;
                }

                _mainRole.SetActive(true);
            }
        }

        /// <summary>
        /// 战斗状态
        /// </summary>
        /// <param name="battleState"></param>
        public void ChangeState(BattleState battleState)
        {
            currentBattleState = battleState;

            switch (battleState)
            {
                case BattleState.None:
                    break;
                case BattleState.Init:
                    StartGame();
                    break;
                case BattleState.SelectAttacker:
                    SelectAttacker();
                    break;
                case BattleState.Attack:
                    OnAttack();
                    break;
                case BattleState.AttackCompleted:
                    AttackCompleted();
                    break;
                case BattleState.Over:
                    OnPopupResult();
                    break;
                default:
                    break;
            }
        }

        // 总回合数
        int m_TotalRound;
        // 当前回合
        int m_CurrentRound;
        // 总的攻击战斗次数
        int m_AllAttackNum;
        // 当前第几次战斗
        int m_CurrentAttackNum;

        public int totalRound => m_TotalRound;
        public int currentRound => m_CurrentRound;
        public int attackNum => m_CurrentAttackNum;

        TurnBasedBattle.Round m_CurrendRoundData;
        TurnBasedBattle.Round m_LastRoundData;

        // 开始回合
        public void StartGame()
        {
            m_TotalRound = TurnBasedM.Instance.turnBasedBattleData.Rounds[TurnBasedM.Instance.turnBasedBattleData.Rounds.Count - 1].RoundId;
            m_CurrentRound = 1;
            m_AllAttackNum = TurnBasedM.Instance.turnBasedBattleData.Rounds.Count;
            m_CurrentAttackNum = 1;
            ChangeState(BattleState.SelectAttacker);
        }

        public bool IsBattleEnd()
        {
            return m_CurrentAttackNum >= m_AllAttackNum;
        }

        public void AttackCompleted()
        {
            if (IsBattleEnd())
            {
                ChangeState(BattleState.Over);
            }
            else
            {
                m_CurrentAttackNum++;
                ChangeState(BattleState.SelectAttacker);
            }
        }

        public void SelectAttacker()
        {
            targetEffects.Clear();
            m_CurrendRoundData = GetCurrentRoundData();
            m_CurrentRound = m_CurrendRoundData.RoundId;
            m_LastRoundData = GetLastRoundData();

            switch (m_CurrendRoundData.ActionCamp)
            {
                case TurnBasedBattle.ActionCamp.BLUE_CAMP:
                    if (TurnBasedRoleManager.Instance.GetBlueRoleByUid(m_CurrendRoundData.Uid, out TurnBasedRole blueRole))
                        currentBattleRole = blueRole;

                    break;
                case TurnBasedBattle.ActionCamp.RED_CAMP:
                    if (TurnBasedRoleManager.Instance.GetRedRoleByUid(m_CurrendRoundData.Uid, out TurnBasedRole redRole))
                        currentBattleRole = redRole;

                    break;
                default:
                    break;
            }

            if (currentBattleRole == null)
            {
                Debug.Log($"<color=red>当前角色未找到: uid = </color>{m_CurrendRoundData.Uid}");
                return;
            }

            currentBattleRole.isDead = false;
            currentBattleRole.skillId = m_CurrendRoundData.SkillId;

            // 加载被攻击者列表和信息
            for (int i = 0; i < m_CurrendRoundData.Effects.Count; i++)
            {
                targetEffects.Add(m_CurrendRoundData.Effects[i]);
            }

            ChangeState(BattleState.Attack);
        }

        private void OnAttack()
        {
            JEvent.defaultEvent.Post(new GameEvent.PVP_TurnBasedRoundShow
            {
                currentRound = m_CurrentRound,
                totalRound = m_TotalRound,
            });

            currentBattleRole.StartAttack();
        }

        public TurnBasedBattle.Round GetCurrentRoundData()
        {
            return TurnBasedM.Instance.GetAttackAction(m_CurrentAttackNum);
        }

        public TurnBasedBattle.Round GetLastRoundData()
        {
            if (m_CurrentAttackNum == 1)
                return null;

            return TurnBasedM.Instance.GetAttackAction(m_CurrentAttackNum - 1);
        }

        // 添加buff
        public void AddBuffers()
        {
            foreach (var effect in m_CurrendRoundData.Effects)
            {
                if (effect.TargetUid != 0 && effect.Addbuffs != null)
                {
                    switch (m_CurrendRoundData.ActionCamp)
                    {
                        case TurnBasedBattle.ActionCamp.BLUE_CAMP:
                            if (TurnBasedRoleManager.Instance.GetRedRoleByUid(effect.TargetUid, out var redRole))
                                redRole.AddBuffers(effect.Addbuffs);
                            break;

                        case TurnBasedBattle.ActionCamp.RED_CAMP:
                            if (TurnBasedRoleManager.Instance.GetBlueRoleByUid(effect.TargetUid, out var blueRole))
                                blueRole.AddBuffers(effect.Addbuffs);
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        public void UpdateRoleInfo()
        {
            switch (m_CurrendRoundData.ActionCamp)
            {
                case TurnBasedBattle.ActionCamp.BLUE_CAMP:
                    for (int i = 0; i < m_CurrendRoundData.RedStats.Count; i++)
                    {
                        var objData = m_CurrendRoundData.RedStats[i];
                        if (objData.Uid != 0 && TurnBasedRoleManager.Instance.GetRedRoleByUid(objData.Uid, out var role))
                        {
                            role.currentHp = objData.CurHp;
                            role.maxHp = objData.MaxHp;
                        }
                    }
                    break;

                case TurnBasedBattle.ActionCamp.RED_CAMP:
                    for (int i = 0; i < m_CurrendRoundData.BlueStats.Count; i++)
                    {
                        var objData = m_CurrendRoundData.BlueStats[i];
                        if (objData.Uid != 0 && TurnBasedRoleManager.Instance.GetBlueRoleByUid(objData.Uid, out var role))
                        {
                            role.currentHp = objData.CurHp;
                            role.maxHp = objData.MaxHp;
                        }
                    }
                    break;
                default:
                    break;
            }

            foreach (var effect in m_CurrendRoundData.Effects)
            {
                JEvent.defaultEvent.Post(new GameEvent.Fight_HPChange
                {
                    tid = effect.TargetUid,
                    changeNum = -effect.Damage,
                    Critical = effect.Crit,
                    Miss = effect.Miss,
                    isTurnBased = true,
                });
            }
        }

        public void CollectKilledRole()
        {
            List<long> killedUids = new List<long>();
            List<TurnBasedBattle.TBObject> objs = null;

            switch (m_CurrendRoundData.ActionCamp)
            {
                case TurnBasedBattle.ActionCamp.BLUE_CAMP:
                    if (m_CurrentAttackNum == 1 || m_CurrentAttackNum == 2)
                        objs = TurnBasedM.Instance.turnBasedBattleData.OriginRed.Objs;
                    else
                        objs = m_LastRoundData.RedStats;

                    for (int i = 0; i < objs.Count; i++)
                    {
                        if (objs[i].Uid != 0 && objs[i].State == TurnBasedBattle.TBObject.ObState.NORMAL
                            && m_CurrendRoundData.RedStats[i].State == TurnBasedBattle.TBObject.ObState.DEAD)
                        {
                            killedUids.Add(objs[i].Uid);
                        }
                    }
                    break;

                case TurnBasedBattle.ActionCamp.RED_CAMP:
                    if (m_CurrentAttackNum == 1 || m_CurrentAttackNum == 2)
                        objs = TurnBasedM.Instance.turnBasedBattleData.OriginBlue.Objs;
                    else
                        objs = m_LastRoundData.BlueStats;

                    for (int i = 0; i < objs.Count; i++)
                    {
                        if (objs[i].Uid != 0 && objs[i].State == TurnBasedBattle.TBObject.ObState.NORMAL
                            && m_CurrendRoundData.BlueStats[i].State == TurnBasedBattle.TBObject.ObState.DEAD)
                        {
                            killedUids.Add(objs[i].Uid);
                        }
                    }
                    break;
                default:
                    break;
            }

            if (killedUids.Count > 0)
            {
                KilledRole(killedUids, m_CurrendRoundData.ActionCamp == TurnBasedBattle.ActionCamp.BLUE_CAMP ? TurnBasedM.CampType.BLUE_CAMP : TurnBasedM.CampType.RED_CAMP);
            }
        }

        private void KilledRole(List<long> uids, TurnBasedM.CampType campType)
        {
            for (int i = 0; i < uids.Count; i++)
            {
                switch (campType)
                {
                    case TurnBasedM.CampType.BLUE_CAMP:
                        if (TurnBasedRoleManager.Instance.GetRedRoleByUid(uids[i], out TurnBasedRole redRole))
                        {
                            redRole.Dead();
                            JEvent.defaultEvent.Post(new GameEvent.Fight_Die
                            {
                                id = redRole.uid,
                            });
                        }
                        else
                        {
                            print("未找到死亡的角色 BLUE BLUE BLUE BLUE");
                        }
                        break;
                    case TurnBasedM.CampType.RED_CAMP:
                        if (TurnBasedRoleManager.Instance.GetBlueRoleByUid(uids[i], out TurnBasedRole blueRole))
                        {
                            blueRole.Dead();
                            JEvent.defaultEvent.Post(new GameEvent.Fight_Die
                            {
                                id = blueRole.uid,
                            });
                        }
                        else
                        {
                            print("未找到死亡的角色 RED RED RED RED");
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 战斗结果
        /// </summary>
        public void OnPopupResult()
        {
            var result = TurnBasedM.Instance.GetFightResult();
            var gameResult = new GameResultBattlePush
            {
                RoomId = TurnBasedM.Instance.roomId,
                BattleId = 0,
                OverType = result.Win ? GameOverType.GameOverTypeMDead : GameOverType.GameOverTypeDeadFaild,
                Items = result.Items
            };
            UITool.Open<UIGameFightOver>(null, gameResult);
        }

        /// <summary>
        /// 跳过战斗，直接显示结果。
        /// </summary>
        public void JumpGame()
        {
            TurnBasedRoleManager.Instance.StopAllRoleAttack();
            ChangeState(BattleState.Over);
        }

        // 重新挑战，打开布阵界面。
        public void GoToNextFight()
        {
            OnExitGame(() =>
            {
                UITool.Open<UIFightChoice>(null, UIFightChoice.SelectType.Limit, UIFightChoice.ChanllengeType.PvP);
                UITool.Open<UIFightFormation>(null, TurnBasedM.Instance.opponentUid);
            });
        }

        public void OnExitGame(Action JumpUI = null)
        {
            TurnBasedRoleManager.Instance.ClearAllRole();
            GameRoomM.Instance._GameState = GameRoomM.GameState.No;
            UITool.Open<UILoading>(null, new UILoading.Loading { _time = 0 });

            SenceChange handle = new SenceChange
            {
                scencePath = GameConst.HallPath,
                CallBack = () =>
                    {
                        UITool.Close<UILoading>();
                        UITool.Open<UIHall>(null);
                        JumpUI?.Invoke();
                        if (JumpUI == null)
                        {
                            UITool.Open<UIFightChoice>(null, UIFightChoice.SelectType.Limit, UIFightChoice.ChanllengeType.PvP);
                        }
                    }
            };
            JEvent.defaultEvent.Post(handle);
        }

        void OnDestyor()
        {
            ClearStartBattleTime();
        }
    }
}
