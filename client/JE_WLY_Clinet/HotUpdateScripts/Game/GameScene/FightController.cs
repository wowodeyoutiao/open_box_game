using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Event;
using JEngine.Helper;
using JEngine.MyNet;
using MetaFramework.UI;
using pb;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static JEngine.MyNet.WebClientGroup;

namespace HotUpdateScripts.Game
{
    public class FightController: MonoBehaviour
    {
        private static FightController _instance;
        public static FightController Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("FightCtr");
                    _instance = go.AddComponent<FightController>();
                }
                return _instance;
            }
        }

        private Camera _MainCamer;
        public Camera MainCamer { 
            get {
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

        //人物节点
        private Transform _root_Role;
        public Transform Root_Role
        {
            get
            {
                if (_root_Role == null)
                {
                    _root_Role = new GameObject("Root_Role").transform;
                    _root_Role.position = Vector3.zero;
                }
                return _root_Role;
            }
        }

        //特效节点
        private Transform _root_Eff3D;
        public Transform Root_Eff3D
        {
            get
            {
                if (_root_Eff3D == null)
                {
                    _root_Eff3D = new GameObject("Root_Eff3D").transform;
                    _root_Eff3D.position = Vector3.back;
                }
                return _root_Eff3D;
            }
        }

        //怪物
        private Transform _root_Monster;
        public Transform Root_Monster
        {
            get
            {
                if (_root_Monster == null)
                {
                    _root_Monster = new GameObject("Root_Monster").transform;
                    _root_Monster.position = Vector3.zero;
                }
                return _root_Monster;
            }
        }

        //角色管理列表
        public class RoleClass 
        {
            public GameObject obj;
            public Role role;
        }

        private Dictionary<long, RoleClass> RoleInfDic = new Dictionary<long, RoleClass>();

        private Dictionary<string, GameObject> ObjDic = new Dictionary<string, GameObject>();

        public void AddObj(string key, GameObject jac)
        {
            if (!ObjDic.ContainsKey(key))
            {
                ObjDic.Add(key, jac);
            }
            else
            {
                ClearOneObj(key);
                ObjDic[key] = jac;
            }
        }

        public void CleacObjDic()
        {
            foreach (var item in ObjDic.Values)
            {
                PoolHelper.Instance.RecycObj(item);
            }
            ObjDic.Clear();
        }

        public void ClearOneObj(string key)
        {
            if (ObjDic.ContainsKey(key))
            {
                PoolHelper.Instance.RecycObj(ObjDic[key]);
                ObjDic.Remove(key);
            }
        }

        private HashSet<long> CreatRoleHashSet = new HashSet<long>();
        public async void OnInit()
        {
            GameRoomM.Instance.TimeAutoBuy = false;
            CreatRoleHashSet.Clear();
            GameRoomM.Instance._GameState = GameRoomM.GameState.Ready;

            GameRoomM.Instance.CFGRoom = CFG_Room.GetById(GameRoomM.Instance.RoomTableInfo.RoomCfgId);

            CFGItem_Map MapCfg = CFG_Map.GetById(GameRoomM.Instance.CFGRoom.MapId);
            SceneManager.loadMapInfo(GameRoomM.Instance.CFGRoom.MapId);

            GameObject.Find("Map").transform.position = SceneManager.MapPos();
            int mapIdx = MapCfg.MapID == 1000 ? 1 : MapCfg.MapID;
            if (GameRoomM.Instance.RoomTableInfo.BattleId != 0 && GameRoomM.Instance.CFGRoom.Roomtype != CFG_Room.RoomType.Guild)
            {
                mapIdx = (GameRoomM.Instance.RoomTableInfo.BattleId % 7) + 1;
            }
            _ = ResouseHelper.Instance.LoadSprite("Terrain/Texture/Map_"+ mapIdx, GameObject.Find("Map").GetComponent<SpriteRenderer>());

            foreach (var monster in GameRoomM.Instance.RoomTableInfo.MonsterList)
                MonsterManager.Instance.addCreateMonsterList(monster);

            UITool.Open<UIGame3D>();
            UITool.Open<UIGameDungeons>();

            string poolKey = "MainRoleFight" + RoleM.Instance.Job + RoleM.Instance.Sex;
            if (!PoolHelper.Instance.CheckPool(poolKey))
            {
                var baseObj = new JPrefab(GameConst.GetModelByJobAndSex((JobType)RoleM.Instance.Job, (SexType)RoleM.Instance.Sex,true), true);
                await baseObj.WaitForAsyncLoading();
                PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
            }

            GameObject _mainRole = PoolHelper.Instance.GetPoolObj(poolKey);
            _mainRole.gameObject.name = poolKey + "/" + RoleM.Instance.Uid;
            _mainRole.transform.SetParent(Root_Role);
            _mainRole.transform.localScale = Vector3.one * 0.8f;
            _mainRole.SetActive(true);

            if (!_mainRole.GetComponent<MainRole>())
            {
                _mainRole.AddComponent<MainRole>();
            }
            MainRole _mRole = _mainRole.GetComponent<MainRole>();

            _mRole.OnInit();
            _mRole.uID = RoleM.Instance.Uid;

            AddOneRoleInfo(new RoleClass { obj = _mainRole, role = _mRole });

            _MainCamer = GameObject.Find("Main Camera").GetComponent<Camera>();
            if (_MainCamer != null)
            {
                _MainCamer.gameObject.AddComponent<FollowTarget>();
            }

            SetInitPlayer();
            MonsterManager.Instance.checkCreateMonsterList();

            if ((int)CFG_Room.GetById(GameRoomM.Instance.RoomTableInfo.RoomCfgId).Roomtype == (int)MatchType.FREE_FOR_ALL ||
                (int)CFG_Room.GetById(GameRoomM.Instance.RoomTableInfo.RoomCfgId).Roomtype == (int)MatchType.SINGLE_COMBAT)
            {
                TCPClientMgr.Instance.SendMessage((short)CmdId.C2S_MatchingReady, MType.Notify,
                           TCPClientMgr.ProtoSerialize(new MatchingReadyNotify()));
            }
            else
            {
                GameRoomM.Instance._GameState = GameRoomM.GameState.Play;
            }

        }

        private async void CreatRole(PlayerInfo pInfo)
        {
            if (CreatRoleHashSet.Contains(pInfo.Uid))
                return;
            CreatRoleHashSet.Add(pInfo.Uid);
            string path = GameConst.GetModelByJobAndSex((JobType)pInfo.Job, (SexType)pInfo.Gender,true);

            string poolKey = "Role" + pInfo.Job + pInfo.Gender;
            if (!PoolHelper.Instance.CheckPool(poolKey))
            {
                var baseObj = new JPrefab(GameConst.GetModelByJobAndSex((JobType)pInfo.Job, (SexType)pInfo.Gender,true), true);
                await baseObj.WaitForAsyncLoading();
                PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
            } 

            GameObject _Role = PoolHelper.Instance.GetPoolObj(poolKey);
            _Role.gameObject.name = poolKey + "/" + pInfo.Uid;
            _Role.transform.SetParent(Root_Role);
            _Role.transform.localScale = Vector3.one * 0.8f;
            _Role.SetActive(true);
            CreatRoleHashSet.Remove(pInfo.Uid);
            if (_Role.GetComponent<MainRole>())
            {
                Destroy(_Role.GetComponent<MainRole>());
            }

            if (!_Role.GetComponent<OtherRole>())
            {
                _Role.AddComponent<OtherRole>();
            }
          
            OtherRole _mRole = _Role.GetComponent<OtherRole>();
            _mRole.uID = pInfo.Uid;
            _mRole.OnInit();

            AddOneRoleInfo(new RoleClass { obj = _Role, role = _mRole });
            OnRefreshRole(pInfo);
        }

        private void OnRefreshRole(PlayerInfo pInfo)
        {
            if (RoleInfDic.ContainsKey(pInfo.Uid))
            {
                Role _mRole = RoleInfDic[pInfo.Uid].role;

                _mRole.job = (JobType)pInfo.Job;
                _mRole.sex = (SexType)pInfo.Gender;

                _mRole.maxHp = pInfo.MaxHp;
                _mRole.hp = pInfo.CurHp;
                _mRole.roleName = pInfo.Nickname;
                _mRole.level = pInfo.Level;
                _mRole.HeadUrl = pInfo.HeadUrl;
                _mRole.OnInitBuffer(pInfo.BuffList);

            }
        }

        //攻击
        public void SetRoleAttack(AttackPush data)
        {
            var player = GetOneRoleInfo(data.AttackerId);
            //角色攻击怪物
            if (player != null)
            {
                player.role.setRotation(data.Direction);
               // Debug.Log("data.TargetUids" + data.TargetUids[0]);
                player.role.SetAttack(data.SkillId, data.TargetUids, data.Location, data.Direction);
            }
            else
            {
                //怪物攻击人物
                MonsterManager.Instance.SetAttack(data.AttackerId, data.Location,data.Direction, data.SkillId);
            }
        }

        //移动
        public void SetRoleMove(MovePush data)
        {
            //角色攻击怪物
            var player = GetOneRoleInfo(data.Uid);
            if (player != null)
            {
                player.role.moveTo( data.Location,data.Direction);
            }
        }

        //传送
        public void SetTeleport(long uid, Point point, Direction dir, TeleportType tType)
        {
            var player = GetOneRoleInfo(uid);
            if (player != null)
            {
                player.role.SetTeleport(point, dir, tType);
            }
        }

        //角色加入创建
        public void SetInitPlayer()
        {
            Debug.Log("角色加入创建--------" + GameRoomM.Instance._GameState);
            if (GameRoomM.Instance._GameState == GameRoomM.GameState.No)
            {
                return;
            }
            var playerInfo = GameRoomM.Instance.getPlayerInfo();
            foreach (var item in playerInfo)
            {
                if (item.Key == RoleM.Instance.Uid)
                {
                    OnRefreshRole(item.Value);
                }
                else
                {
                    if (GetOneRoleInfo(item.Key) == null)
                        CreatRole(item.Value);
                }
            }

            var ObjectInfo = GameRoomM.Instance.getObjInfo();
            foreach (var item in ObjectInfo)
            {
                if (GetOneRoleInfo(item.Key) == null)
                    CreatGameObject(item.Value);
            }
        }

        //角色死亡
        public void SetPlayerDeath(long Uid)
        {
            var player = GetOneRoleInfo(Uid);
            if (player != null)
            {
                GameRoomM.Instance.SetPlayerHP(Uid, 0, 0, true);
                player.role.RoleDeal();
            }
        }

        //角色离开
        public void OnPlayerExit(long Uid)
        {
            var player = GetOneRoleInfo(Uid);
            if (player != null)
            {
                player.role.destoryRole();
                RemoveOneRoleInfo(Uid);
            }
        }

        public void OnPlayerUpLv(long Uid,int lv)
        {
            var player = GetOneRoleInfo(Uid);
            if (player != null)
            {
                player.role.level = lv;
                player.role.SetUpLv();
            }
            GameRoomM.Instance.SetPlayerLv(Uid, lv);
        }

        public Point getRolePos(long uId)
        {
            var player = GetOneRoleInfo(uId);
            if (player != null)
            {
                return player.role.pointNow;
            }
            return default;
        }

        public int getDistance(double x, double y,long uid)
        {
            var pointNow = uid == 0 ? MainRole._MainRole.pointNow : getRolePos(uid);
            int cutX = (int)Math.Abs(x - pointNow.X);
            int cutY = (int)Math.Abs(y - pointNow.Y);
            return Math.Max(cutX, cutY);
        }

        //复活
        public void SetPlayerRevive(RevivePush data)
        {
            var player = GetOneRoleInfo(data.Uid);
            if (player != null)
            {
                player.role.RoleRevive(data.CurHp, data.Location);
            }
        }

        public void RefrshBuffer()
        {
            var player = GetOneRoleInfo(RoleM.Instance.Uid);
            if (player != null)
            {
                player.role.RefrshBuffer();
            }
        }

        //buffer add or move
        public void AddOrMoveOneBuffer(long Uid, int bufferID,bool isAdd)
        {
            if (MonsterManager.Instance.getOneMonster(Uid) != null)
            {
                MonsterManager.Instance.getOneMonster(Uid)._monser.AddOrRemoveBuffer(bufferID, isAdd);
            }
            else
            {
                var player = GetOneRoleInfo(Uid);
                if (player != null)
                {
                    player.role.AddOrRemoveBuffer(bufferID, isAdd);
                }
            }
        }

        public void SetRoleHpChange(long id, int changeNum,int maxHp, long attackerId,int skillID)
        {
            var _Role = GetOneRoleInfo(id);
            if (_Role != null)
            {
                GameRoomM.Instance.SetPlayerHP(id, changeNum, maxHp);
                _Role.role.SetHurtChange(changeNum, maxHp,attackerId, skillID);
            }
        }

        //创建移对象
        public void SetGameObjectEff(ObjectInfo oinfo, bool isAdd)
        {
            Debug.Log("//创建移对象" + oinfo.Type.ToString() + GameRoomM.Instance._GameState.ToString());
            if (isAdd)
            {
                if (GameRoomM.Instance._GameState == GameRoomM.GameState.No)
                {
                    return;
                }
                if (GetOneRoleInfo(oinfo.Uid) == null)
                    CreatGameObject(oinfo);
            }
            else
            {
                //自己的伙伴不删除
                if (oinfo.OwnerUid != RoleM.Instance.Uid)
                    OnPlayerExit(oinfo.Uid);
            }
        }

        private async void CreatGameObject(ObjectInfo pInfo)
        {
            string poolKey = "";
            string poolPath = "";
            if (pInfo.Type == ObjectType.PARTNER)
            {
                poolKey = "Fight_Partner_" + CFG_Partner.GetById(pInfo.CfgID).ID;
                poolPath = GameConst.GetMonsterModelByID(poolKey);
            }
            else if (pInfo.Type == ObjectType.BUFF || pInfo.Type == ObjectType.DECOY)
            {
                poolKey = CFG_BuffEffect.GetById(pInfo.CfgID).BuffEffect;

                if (string.IsNullOrEmpty(poolKey))
                    return;
                poolPath = GameConst.GetEffPath(poolKey);
            }

            Debug.Log("//poolKey  - " + poolKey);
            if (string.IsNullOrEmpty(poolKey))
                return;
            if (GetOneRoleInfo(pInfo.Uid) == null)
            {
                if (CreatRoleHashSet.Contains(pInfo.Uid))
                    return;
                CreatRoleHashSet.Add(pInfo.Uid);
                if (!PoolHelper.Instance.CheckPool(poolKey))
                {
                    var baseObj = new JPrefab(poolPath, true);
                    await baseObj.WaitForAsyncLoading();
                    PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
                }

                GameObject _Role = PoolHelper.Instance.GetPoolObj(poolKey);
                _Role.gameObject.name = poolKey + "/" + pInfo.Uid;

                if (pInfo.Type == ObjectType.PARTNER)
                {
                    _Role.transform.SetParent(Root_Role);
                    _Role.transform.localScale = Vector3.one * GameConst.GetMonsterScalModelByID(poolKey);

                }
                else if (pInfo.Type == ObjectType.BUFF || pInfo.Type == ObjectType.DECOY)
                {
                    _Role.transform.SetParent(Root_Eff3D);
                    _Role.transform.localScale = Vector3.one;
                }

                CreatRoleHashSet.Remove(pInfo.Uid);
                _Role.SetActive(true);

                if (!_Role.GetComponent<OtherRole>())
                    _Role.AddComponent<OtherRole>();

                OtherRole _mRole = _Role.GetComponent<OtherRole>();
                _mRole.uID = pInfo.Uid;
                _mRole.OnInit();

                AddOneRoleInfo(new RoleClass { obj = _Role, role = _mRole });
            }
            OnRefreshRole(pInfo);
        }

        private void OnRefreshRole(ObjectInfo pInfo)
        {
            var player = GetOneRoleInfo(pInfo.Uid);
            if (player != null)
            {
                Role _Obj = player.role;
                _Obj.maxHp = pInfo.MaxHp;
                _Obj.level = pInfo.Level;
                _Obj.hp = pInfo.CurHp;
                _Obj._ObjectType = pInfo.Type;
                if (pInfo.Type == ObjectType.PARTNER)
                {
                    _Obj.roleName = GameUtil.Instance.GetStrByQuality(CFG_Partner.GetById(pInfo.CfgID).Quality, CFG_Partner.GetById(pInfo.CfgID).Name);
                    _Obj.job = (JobType)(CFG_Partner.GetById(pInfo.CfgID).job % 10);
                    _Obj.CfgID = pInfo.CfgID;
                }
                _Obj.OwnerUid = pInfo.OwnerUid;
                _Obj.OnInitBuffer(pInfo.BuffList);
            }
        }


        public List<RoleClass> GetRolesInfo()
        {
            return RoleInfDic.Values.ToList();
        }

        public RoleClass GetOneRoleInfo(long uid)
        {
            if (RoleInfDic.ContainsKey(uid))
                return RoleInfDic[uid];

            return null;
        }

        public void RemoveOneRoleInfo(long uid)
        {
            if (RoleInfDic.ContainsKey(uid))
                RoleInfDic.Remove(uid);
        }

        public void AddOneRoleInfo(RoleClass roleClass)
        {
            RoleInfDic.Add(roleClass.role.uID, roleClass);
        }


        private class DisRoleClass
        {
            public int Dis;
            public RoleClass RoleC;
        }

        private List<RoleClass> AttackRoles = new List<RoleClass>();
        //获取能攻击的对象
        public List<RoleClass> GetCanDisRoles(int attackDis ,int _Count)
        {
            AttackRoles.Clear();

            if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Monster)
                return AttackRoles;

            var RolesDisList = new List<DisRoleClass>();
            int skillId = GameConst.GetNormallSkillByJob(MainRole._MainRole.job);

            var _RoleList = GetRolesInfo();
            for (int i = 0; i < _RoleList.Count; i++)
            {
                int dis = _RoleList[i].role.getHurtDistance(skillId, attackDis);
                if (_RoleList[i].role != null && dis >= 0)
                {
                    RolesDisList.Add(new DisRoleClass { Dis = dis, RoleC  = _RoleList[i] });
                }
            }

            RolesDisList.Sort((a, b) => { return a.Dis.CompareTo(b.Dis); });
            for (int i = 0; i < RolesDisList.Count; i++)
            {
                if (AttackRoles.Count < _Count)
                    AttackRoles.Add(RolesDisList[i].RoleC);
                else
                    break;
            }
            return AttackRoles;
        }


        //检测攻击的人物
        public long getCanAttackMonster(int skillID, long uid)
        {
            if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Monster)
                return 0;

            int distace = 99999;

            if (GetOneRoleInfo(uid) != null && GetOneRoleInfo(uid).role.getHurtDistance(skillID) >= 0)
            {
                return uid;
            }
            uid = 0;

            var _RoleList = GetRolesInfo();
            for (int i = 0; i < _RoleList.Count; i++)
            {
                int dis = _RoleList[i].role.getHurtDistance(skillID);
                if ( dis >= 0 && distace > dis)
                {
                    distace = dis;
                    uid = _RoleList[i].role.uID;
                }
            }
            return uid;
        }

        private void OnDestroy()
        {
            _instance = null;
        }

        private void Clear()
        {
            foreach (var role in RoleInfDic)
            {
                role.Value.role.destoryRole();
            }
            RoleInfDic.Clear();
            MonsterManager.Instance.Clear();

            CleacObjDic();
        }

        public void OnExitGame(Action JumpUI = null)
        {
            Clear();
            GameRoomM.Instance._GameState = GameRoomM.GameState.No;

            SenceChange j = new SenceChange();
            j.scencePath = GameConst.HallPath;
            j.CallBack = () =>
            {
                UITool.Open<UIHall>(null);
                JumpUI?.Invoke();

                if (JumpUI == null)
                {
                    var selectType = UIFightChoice.SelectType.Challange;
                    var chanllengeType =  UIFightChoice.ChanllengeType.Fight;
                    switch (GameRoomM.Instance.CFGRoom.Roomtype)
                    {
                        case CFG_Room.RoomType.Boss:
                            chanllengeType =  UIFightChoice.ChanllengeType.Boss;
                            break;
                        case CFG_Room.RoomType.SLTower:
                            chanllengeType = UIFightChoice.ChanllengeType.SLTower;
                            break;
                        case CFG_Room.RoomType.Relic:
                            chanllengeType = UIFightChoice.ChanllengeType.Relic;
                            break;
                        case CFG_Room.RoomType.PVP_More:
                            selectType = UIFightChoice.SelectType.Limit;
                            chanllengeType = UIFightChoice.ChanllengeType.PvP;
                            break;
                        case CFG_Room.RoomType.PVP_One:
                            selectType = UIFightChoice.SelectType.Limit;
                            chanllengeType = UIFightChoice.ChanllengeType.PvP;
                            break;
                        case CFG_Room.RoomType.PVE_Peace:
                            chanllengeType = UIFightChoice.ChanllengeType.HaiDao;
                            break;
                        default:
                            break;
                    }
                    if (GameRoomM.Instance.IsDayDo())
                    {
                        selectType = UIFightChoice.SelectType.FuBen;
                        chanllengeType = UIFightChoice.ChanllengeType.DayDo;
                    }
                    else if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.Guild)
                    {
                        UITool.Open<UIGuild>();
                        return;
                    }
                    else if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.WorldBoss)
                    {
                        // 单机版不做世界Boss：进房已在 BattleService.Enter 被拦，
                        // 这里只是兜底，避免万一走到时打开一个没有数据源的死页面。
                        UITool.ShowTips("暂未开放");
                        return;
                    }
                    else if (GameRoomM.Instance.CFGRoom.Roomtype == CFG_Room.RoomType.YZDD)
                    {

                        UITool.Open<UIFightToTheEnd>();
                        return;
                    }
                    UITool.Open<UIFightChoice>(null, selectType, chanllengeType);
                }
            };
            JEvent.defaultEvent.Post(j);
        }
    }
}
