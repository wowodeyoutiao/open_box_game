using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Helper;
using JEngine.Singleton;
using pb;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace HotUpdateScripts.Game
{
    public class MonsterManager : SingletonTemplate<MonsterManager>
    {
        public class monsterClass
        {
            public GameObject _monsterObj;
            public Monster _monser;
        }
        private Dictionary<long, monsterClass> _monsterList = new Dictionary<long, monsterClass>();
        private Dictionary<long, MonsterInfo> _monsterCreateList = new Dictionary<long, MonsterInfo>();

        public int MonesterCount = 0;

        public void addCreateMonsterList(MonsterInfo monsterInfo)
        {
            //if (_monsterCreateList.Count >= 2)
            //    return;

            MonesterCount++;
            _monsterCreateList.Add(monsterInfo.Uid,monsterInfo);
        }

        public void checkCreateMonsterList()
        {
            int Count = 0;  
            foreach(var monsterInfo in _monsterCreateList)
            {
                MonsterInfo _monsterInfo = monsterInfo.Value;
                createMonster(_monsterInfo,true);
                Count++;
            }
            _monsterCreateList.Clear();
        }

        public async void createMonster(MonsterInfo monsterInfo,bool isInit = false)
        {
            if (_monsterList.ContainsKey(monsterInfo.Uid))
            {
               // Debug.Log("jerryc createMonster " + monsterInfo.Uid + " has excited");
                return;
            }
            if (!isInit)
            {
                MonesterCount++;
            }
             //Debug.Log("怪物加入-------------- " + monsterInfo.Uid + ""+ monsterInfo.Location.X + " -"+ monsterInfo.Location.Y);

            string poolKey = CFG_Monster.GetById(monsterInfo.CfgId).Model;
            if (!PoolHelper.Instance.CheckPool(poolKey))
            {
                var baseObj = new JPrefab(GameConst.GetMonsterModelByID(poolKey), true);
                await baseObj.WaitForAsyncLoading();
                PoolHelper.Instance.AddPool(poolKey, baseObj.Instance,true);
            }
            var _monsterObj = PoolHelper.Instance.GetPoolObj(poolKey);
            _monsterObj.transform.SetParent(FightController.Instance.Root_Monster);
            _monsterObj.transform.localScale = Vector3.one * GameConst.GetMonsterScalModelByID(poolKey);
            _monsterObj.gameObject.name = poolKey + "/" + monsterInfo.Uid;

            Monster monster = _monsterObj.GetComponent<Monster>();
            if (!monster)
            {
                monster = _monsterObj.AddComponent<Monster>();
            }

            monster.uID = monsterInfo.Uid;
            monster.maxHp = monsterInfo.MaxHp;
            monster.hp = monsterInfo.CurHp;
            monster._priority = monsterInfo.CfgId;
            monster.cfgId = monsterInfo.CfgId;

            _monsterObj.SetActive(true);
            monster.OnInit();
            monster.OnInitBuffer(monsterInfo.BuffList);

            monster.wrap(new Vector3((float)monsterInfo.Location.X, (float)monsterInfo.Location.Y, SceneManager.GetOffZ(monsterInfo.Location.X, monsterInfo.Location.Y)),true);

            monster.setRotation(monsterInfo.Direction);

            _monsterList.Add(monsterInfo.Uid, new monsterClass { _monser = monster, _monsterObj = _monsterObj });
            //    Debug.Log("jerryc 创建怪物----------- " + monsterInfo.Uid + "success");

            //当创建完毕后防止服务器已经将怪物死亡的消息发送过来，导致客户端创建的怪物被删除
            if (GameRoomM.Instance.DeathHashSet.Contains(monster.uID))
            {
                removeMonster(monster.uID);
            }
        }

        //血量刷新
        public void SetMonsterHPChange(long monsterId,int changeHP, int MaxHp, long AttackerId,int skillID)
        {
            var monster = getOneMonster(monsterId);
            if (monster != null)
            {
                monster._monser.hp += changeHP;
                monster._monser.maxHp = MaxHp;
                monster._monser.SetHurtChange(changeHP, AttackerId, skillID);

                //Debug.Log($"monsterId  {monsterId}  hp{monster._monser.hp} " );
                //if (monster._monser.hp <= 0)
                //{
                //    removeMonster(monsterId);
                //}
            }
        }

        public bool removeMonster(long monsterId)
        {
            var monster = getOneMonster(monsterId);
            if (monster != null)
            {
                Log.Print("jerryc attack removeMonster: "+ monsterId + "   "+ _monsterList[monsterId]._monsterObj.gameObject.name);
                monster._monser.destoryMonster();

                _monsterList.Remove(monsterId);
                MonesterCount--;
                return true;
            }
            return false;
        }

        public void clearMonsterList()
        {
            _monsterList.Clear();
        }

        public bool monsterMove(long id ,Point locaPos,Direction direction)
        {

            var monster = getOneMonster(id);
            if (monster != null)
            {
                monster._monser.moveTo(locaPos, direction);
                return true;
            }
            return false;
        }

        public bool SetAttack(long id, Point locaPos, Direction direction, int SkillId)
        {
            var monster = getOneMonster(id);
            if (monster != null)
            {
                monster._monser.SetAttack(locaPos, direction, SkillId);
                return true;
            }

            return false;
        }

        //传送
        public bool SetTeleport(long id, Point point, Direction dir, TeleportType tType)
        {

            var monster = getOneMonster(id);
            if (monster != null)
            {
                monster._monser.SetTeleport(point, dir, tType);
                return true;
            }
            return false;
        }

        public int GetOneAttackMonsterDis(long uid,int skillID,int _dis = -1)
        {
            if (getOneMonster(uid) != null && !GameRoomM.Instance.DeathHashSet.Contains(uid))
            {
                return getOneMonster(uid)._monser.getHurtDistance(skillID, _dis);
            }
            return -1;
        }

        public long getCanAttackMonster(int skillID,long uid,int offdis =-1)
        {
            int distace = 99999;
            //long uid = 0;

            if (GetOneAttackMonsterDis(uid, skillID, offdis) > 0)
            {
                return uid;
            }
            uid = 0;
            var _monsterList = getMonsterObjs();
            for (int i = 0; i < _monsterList.Count; i++)
            {
                int dis = _monsterList[i]._monser.getHurtDistance(skillID, offdis);
                if (!GameRoomM.Instance.DeathHashSet.Contains(uid) && dis >= 0 && distace > dis )
                {
                    distace = dis;
                    uid = _monsterList[i]._monser.uID;
                }
            }
            return uid;
        }

        private class DisMonster 
        {
            public int Dis;
            public monsterClass monster;
        }
        //获取普通能攻击的角色对象
        public List<monsterClass> getCanAttackMonsters(int dis ,int _Count)
        {
            var monsterList = new List<monsterClass>();

            var monsterDisList = new List<DisMonster>();
            int skillId = GameConst.GetNormallSkillByJob(MainRole._MainRole.job);
            var _monsterList = getMonsterObjs();
            for (int i = 0; i < _monsterList.Count; i++)
            {
                int _dis = _monsterList[i]._monser.getHurtDistance(skillId, dis);
                if (_dis >= 0 && !GameRoomM.Instance.DeathHashSet.Contains(_monsterList[i]._monser.uID))
                {
                    monsterDisList.Add(new DisMonster { Dis = _dis,monster = _monsterList[i] });
                }
            }

            monsterDisList.Sort((a, b) => { return a.Dis.CompareTo(b.Dis); });

            for (int i = 0; i < monsterDisList.Count; i++)
            {
                if (monsterList.Count < _Count)
                    monsterList.Add(monsterDisList[i].monster);
                else
                    break;
            }
            return monsterList;
        }

        public Point getMonsterPos(long monsterId)
        {
            if (_monsterList.ContainsKey(monsterId))
            {
                var _mon = _monsterList[monsterId];
                if (_mon._monser != null )
                { 
                    if(_mon._monser._gridNow!=null)
                        return _mon._monser._gridNow;
                    //else
                    //{
                    //    Log.Print("monster is null:" + monsterId);
                    //}
                }
            }

            Point point = new Point();
            point.X = 0; point.Y = 0;
            return point;
        }

        public List<monsterClass>  getMonsterObjs()
        {
            return _monsterList.Values.ToList();
        }

        public monsterClass getOneMonster(long uid)
        {
            if (_monsterList.ContainsKey(uid))
                return _monsterList[uid];

            return null;
        }

        //获取某一种怪物数量
        public int GetNumByMonsterId(int monsterID)
        {
            int Count = 0;
            var monters = _monsterList.Values.ToList();
            for (int i = 0; i < monters.Count; i++)
            {
                if (monters[i] != null && monters[i]._monser.cfgId == monsterID)
                    Count ++;
            }
            return Count;
        }

        public void Clear() 
        {
            MonesterCount = 0;
            foreach (var monsterInfo in _monsterList)
            {
                monsterInfo.Value._monser.Destory();
            }
            _monsterList.Clear();
            _monsterCreateList.Clear();
        }
    }
}
