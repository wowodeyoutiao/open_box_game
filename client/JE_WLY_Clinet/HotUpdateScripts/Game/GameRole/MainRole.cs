using DG.Tweening;
using HotUpdateScripts;
using HotUpdateScripts.Game;
using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Helper;
using pb;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MainRole : Role
{
    public static MainRole _MainRole;

    public Direction _joyDir;

    /// <summary>
    /// 上次移动发送时间
    /// </summary>
    private float _sendMoveLast;
    /// <summary>
    /// 上次攻击发送时间
    /// </summary>
    private float _sendAttackLast;

    /// <summary>
    /// 攻击时间间隔
    /// </summary>
    private float _attackSpeed;

    private Point _lastSendLocation;
    private Point _lastMoveLocation;

    private long _targetID;//目标攻击玩家ID
    public long targetID
    {
        get => _targetID;

        set
        {
            if (_targetID == value)
                return;
            _targetID = value;

        }
    }

    public override void OnInit()
    {
        base.OnInit();
        _MainRole = this;
        _joyDir = Direction.NONE;
        isSendMove = true;

        _targetID = 0;
        _sendMoveLast = 0;
        _sendAttackLast = 0;

        _lastSendLocation = new Point();
        _lastSendLocation.X = 0; _lastSendLocation.Y = 0;
        _lastMoveLocation = new Point { X = 0, Y = 0 };

        uID = RoleM.Instance.Uid;

        _ObjectType = ObjectType.PLAYER;

        PlayerInfo _playerInfo = GameRoomM.Instance.getPlayerInfoByID(RoleM.Instance.Uid);
        if (_playerInfo != null)
        {
            pointNow = _playerInfo.Location;

            wrap(new Vector3((float)pointNow.X, (float)pointNow.Y, SceneManager.GetOffZ(pointNow.X, pointNow.Y)));

        }

        RefrshBuffer();
    }

    public override void RefrshBuffer()
    {

        _attackSpeed = ((int)RoleM.Instance.GetRoleAttrValueByAttr(pb.Attribute.ATTACK_SPEED) + 50) * 0.001f;
        moveSpeed = ((int)RoleM.Instance.GetRoleAttrValueByAttr(pb.Attribute.MOVE_SPEED) + 50) * 0.001f;
    }

    public override void AddOrRemoveBuffer(int bufferId, bool isAdd)
    {
        base.AddOrRemoveBuffer(bufferId, isAdd);

        if (bufferId == (int)CFG_BuffEffect.BufferID.CaoFeng)
        {
            if (isAdd)
            {
                if (RoleM.Instance.GetBuffOwnerUid(bufferId) != 0)
                {
                    MainRole._MainRole.targetID = RoleM.Instance.GetBuffOwnerUid(bufferId);
                }
            }
            else
            {
                MainRole._MainRole.targetID = 0;
            }
        }
    }

    private int AutoDis = 50;
    //自动攻击检测
    private bool AutoWalkOrAttack()
    {
        if (!GameRoomM.Instance.AutoPlay && !GameRoomM.Instance.ShortAttactToPlayer)
            return false;

        var Roles = FightController.Instance.GetCanDisRoles(AutoDis, 1);
        long TargetRole = Roles.Count > 0 ? Roles[0].role.uID : 0;

        var Monsters = MonsterManager.Instance.getCanAttackMonsters(AutoDis, 1);
        long TargetMonster = Monsters.Count > 0 ? Monsters[0]._monser.uID : 0;

        if (TargetRole == 0 && TargetMonster == 0)
            return false;

        if (GameRoomM.Instance.AttactToPlayer && TargetRole > 0)
        {
            MainRole._MainRole.targetID = TargetRole;
        }
        else
        {
            MainRole._MainRole.targetID = Math.Max(TargetRole, TargetMonster);
        }
        return true;

    }

    private void Update()
    {
        //if (_Actionset > 0)
        //{
        //    _Actionset -= Time.deltaTime;
        //}
        if (GameRoomM.Instance._GameState != GameRoomM.GameState.Play  )
            return;

        _sendAttackLast += Time.deltaTime;

        _sendMoveLast += Time.deltaTime;
        if (MainRole._MainRole._joyDir != Direction.NONE)
        {
            if (_sendMoveLast > moveSpeed)
            {
                _sendMoveLast = 0;
                sendPath(_joyDir);
            }
        }
        else if (MainRole._MainRole.targetID != 0)
        {
            if (_sendMoveLast > moveSpeed)
            {
                _sendMoveLast = 0;
                sendAutoPath();
            }
        }
        else
        {
            AutoWalkOrAttack();
        }


        var _SkillList = SkillM.Instance.GetGameFightSKill();
        //OnSkillCDRefrsh(_SkillList);

        //移动状态不攻击
        if (_animState == GameConst.ModelAnim.walk || _animState.CompareTo(GameConst.ModelAnim.run) == 0)
        {
            if (!IsMoving())
            {
                changeState(GameConst.ModelAnim.stand);
            }
        }

        bool isNorAttack = true;
        for (int i = 0; i < _SkillList.Count; i++)
        {
            if (checkSkillAttack(_SkillList[i]))
            {
                isNorAttack = false;
                break;
            }
        }
        if (hp > 0)
        {
            if (isNorAttack)
                checkNormalAttack();

            checkCollection();
        }


       
    }

    public override void moveTo(Point Location, Direction _Direction)
    {

        base.moveTo(Location, _Direction);
        _lastMoveLocation = Location;

        //if (RoleM.Instance.Guidance && RoleM.Instance.GuideIdx == 3)
        //{
        //    Point _Pos = default;
        //    long targetID = GetAttackUid(GameConst.GetNormallSkillByJob(job), ref _Pos);
        //    if (targetID == 0)
        //        return;
        //    MainRole._MainRole._joyDir = Direction.NONE;
        //    MainRole._MainRole.targetID = targetID;
        //    MetaFramework.UI.UITool.Close<UIGuilde>();
        //    RoleM.Instance.GuideIdx = 4;
        //    //_lastSendLocation = pointNow;
        //    //TcpC.C2S_Move(new MoveRequest
        //    //{
        //    //    Location = pointNow,
        //    //});
        //}
    }

    private void OnDisable() { }

    //根据攻击目标寻路移动
    public bool sendAutoPath()
    {
        //if (_Actionset > 0)
        //{
        //    return false;
        //}
        //自动行走时检测下攻击，防止攻击动画播放未完成
        if (_sendAttackLast < _attackSpeed)
            return false;
        if (MainRole._MainRole.targetID == 0)
        {
            return false;
        }

        if (hp <= 0)
            return false;

        Point toPoint = pointNow;
        var target = FightController.Instance.GetOneRoleInfo(MainRole._MainRole.targetID);
        if (target != null)
        {
            toPoint = target.role.pointNow;
        }
        else
        {
            var monsterTarget = MonsterManager.Instance.getOneMonster(MainRole._MainRole.targetID);
            if (monsterTarget != null)
            {
                toPoint = monsterTarget._monser._gridNow;
            }
        }
        if (toPoint == pointNow)
        {
            return false;
        }

        //检测是否在攻击距离内（在攻击范围内不移动）
        float attackDis = GameRoomM.Instance.GetCanNoAttackGame() ? 1 : (float)CFG_Skill.GetById(GameConst.GetNormallSkillByJob(job)).MaxDistance;
        int Dis = getDistanceFromMainRole(toPoint.X, toPoint.Y);

        //非嘲讽状态的寻路 超过指定距离就不再检测
        if (!RoleM.Instance.CheakBuffId((int)CFG_BuffEffect.BufferID.CaoFeng) && Dis > AutoDis)
            return false;

        if (attackDis >= Dis)
        {
            return false;
        }

        Point posNow = pointNow;
        double x = posNow.X;
        double y = posNow.Y;

        var offx = toPoint.X - pointNow.X;
        var offy = toPoint.Y - pointNow.Y;


        if (Math.Abs(offx) > 0)
        {
            if (offx > 0)
                x += MoveOff;
            else
                x -= MoveOff;
        }
        if (Math.Abs(offy) > 0)
        {
            if (offy > 0)
                y += MoveOff;
            else
                y -= MoveOff;
        }

        if (x == _lastMoveLocation.X && y == _lastMoveLocation.Y)
            return false;


        if (SceneManager.GetOffZ(x, y) != SceneManager.Block)
        {
            _lastSendLocation.X = x;
            _lastSendLocation.Y = y;
            TcpC.C2S_Move(new MoveRequest
            {
                Location = _lastSendLocation,
            });

        }
        return true;
    }

    private float MoveOff = 1f;
    public void sendPath(Direction dir)
    {
        //if (_Actionset > 0)
        //{
        //    return;
        //}

        Point posNow = pointNow;

        double x = posNow.X;
        double y = posNow.Y;
        switch (dir)
        {
            case Direction.NONE:

                break;
            case Direction.UP:
                y += MoveOff;
                break;
            case Direction.DOWN:
                y -= MoveOff;
                break;
            case Direction.LEFT:
                x -= MoveOff;
                break;
            case Direction.RIGHT:
                x += MoveOff;
                break;
            case Direction.UP_LEFT:
                x -= MoveOff;
                y += MoveOff;
                break;
            case Direction.UP_RIGHT:
                x += MoveOff;
                y += MoveOff;
                break;
            case Direction.DOWN_LEFT:
                x -= MoveOff;
                y -= MoveOff;
                break;
            case Direction.DOWN_RIGHT:
                x += MoveOff;
                y -= MoveOff;
                break;
        }

        if (x == _lastMoveLocation.X && y == _lastMoveLocation.Y)
            return;

        if (hp <= 0)
            return;

        if (SceneManager.GetOffZ(x, y) != SceneManager.Block)
        {
            //_sendAttackLast = 0;
            //_Actionset = 0;

            _lastSendLocation.X = x;
            _lastSendLocation.Y = y;

            TcpC.C2S_Move(new MoveRequest
            {
                Location = _lastSendLocation,
            });
        }

    }

    public int getDistanceFromMainRole(double x, double y)
    {
        //Vector2.Distance(new Vector2((float)pointNow.X, (float)pointNow.Y), new Vector2(x, y));
        int cutX = (int)Math.Abs(x - pointNow.X);
        int cutY = (int)Math.Abs(y - pointNow.Y);
        return Math.Max(cutX, cutY);
    }

    //技能CD刷新
    //private void OnSkillCDRefrsh(List<int> Skills)
    //{
    //    for (int i = 0; i < Skills.Count; i++)
    //    {
    //        if (SkillM.Instance.GetGameSkillDealy(Skills[i]) > 0)
    //        {
    //            float cdTime = SkillM.Instance.GetGameSkillDealy(Skills[i]) - Time.deltaTime;
    //            SkillM.Instance.SetGameSkillDealy(Skills[i], cdTime);
    //        }
    //    }
    //}

    //技能攻击检测条件判定
    private bool AttackCheak(int skillId = 0)
    {
        long Pid = skillId == 0 ? 0 : SkillM.Instance.GetUiDBySkillId(skillId);

        //if (_Actionset > 0 && Pid == 0)
        //{
        //    return false;
        //}
        //普攻检测自己
        if (Pid == 0 && (hp <= 0 || IsMoving()))
            return false;
        else if (Pid != 0)
        {
            //自己伙伴的技能 检测伙伴的血量
            if (FightController.Instance.GetOneRoleInfo(Pid) == null || FightController.Instance.GetOneRoleInfo(Pid).role.hp <= 0)
                return false;
        }

        if (GameRoomM.Instance.GetCanNoAttackGame())
            return false;

        if (GameRoomM.Instance._GameState != GameRoomM.GameState.Play)
            return false;

        if (RoleM.Instance.CheakBuffId((int)CFG_BuffEffect.BufferID.XuanYun, (int)CFG_BuffEffect.BufferID.BingDong))
            return false;

        if (Pid == 0 && _sendAttackLast < _attackSpeed)
            return false;

        return true;
    }

    //技能攻击检测
    private bool checkSkillAttack(int SkillID)
    {
        if (!SkillM.Instance.GetGameSkillOpen(SkillID))
            return false;

        if (SkillM.Instance.GetGameSkillDealy(SkillID) > GameUtil.Instance.GetCurrentTimeSeconds())
            return false;

        if (!AttackCheak(SkillID))
            return false;

        var CFGSKill = CFG_Skill.GetById(SkillID);
        if (CFGSKill.PreMark == 1)
            return false;

        if (CFGSKill.Skilltype == (int)CFG_Skill.SkillType.Super || CFGSKill.Skilltype == (int)CFG_Skill.SkillType.SuperCtr)
        {
            SkillM.Instance.SetGameSkillOpen(SkillID, false, 0);
        }

        bool IsNoTarget = CFGSKill.Needaim == 0;
        Direction dir = _Dir;

        long targetID;
        Point targetPos = null;

        if (!IsNoTarget)
        {
            targetID = GetAttackUid(SkillID, ref targetPos);
            if (targetID == 0)
                return false;

            MainRole._MainRole.targetID = targetID;
            dir = getDir(targetPos);
        }
        else
        {
            targetID = 0;
        }

        //Log.PrintYellow("SkillAttack targetID:" + targetID + " dir:" + dir + "Time" + GameUtil.Instance.GetCurrentMSTimeSeconds()
        //    + "role pos is:" + pointNow.X + " " + pointNow.Y + "SkillID  " + SkillID);

        TcpC.C2S_Attack(new AttackRequest
        {
            SkillId = SkillID,
            TargetUids = new long[] { targetID },
            Direction = dir,
            Location = IsNoTarget ? pointNow : targetPos,
        });

        if (SkillM.Instance.GetUiDBySkillId(SkillID) != 0)
        {
            return false;
        }

        _sendAttackLast = 0;
        //_sendMoveLast = 0;

        return true;
    }

    private long GetAttackUid(int SkillId, ref Point point)
    {
        long mId = MonsterManager.Instance.getCanAttackMonster(SkillId, MainRole._MainRole.targetID);
        if (mId != 0 && mId == MainRole._MainRole.targetID)
        {
            point = MonsterManager.Instance.getMonsterPos(mId);
            return mId;
        }

        long uId = FightController.Instance.getCanAttackMonster(SkillId, MainRole._MainRole.targetID);
        if (uId != 0 && (uId == MainRole._MainRole.targetID || GameRoomM.Instance.AttactToPlayer))
        {
            point = FightController.Instance.getRolePos(uId);
            return uId;

        }
        if (mId > 0)
        {
            point = MonsterManager.Instance.getMonsterPos(mId);
            return mId;
        }
        else if (uId > 0)
        {
            point = FightController.Instance.getRolePos(uId);
            return uId;
        }
        return 0;
    }

    //普攻检测
    private void checkNormalAttack()
    {
        if (!AttackCheak())
            return;

        Point monsterPos = default;

        long targetID = GetAttackUid(GameConst.GetNormallSkillByJob(job), ref monsterPos);
        if (targetID == 0)
            return;
        MainRole._MainRole.targetID = targetID;
        _sendAttackLast = 0;
        //_sendMoveLast = 0;


        //Log.Print("j普攻检测:------------+" + _Actionset + " nowTime" + GameUtil.Instance.GetCurrentMSTimeSeconds());

        Direction dir = getDir(monsterPos);

        TcpC.C2S_Attack(new AttackRequest
        {
            SkillId = GameConst.GetNormallSkillByJob(job),
            TargetUids = new long[] { targetID },
            Direction = dir
        });
    }

    //可收集 怪物检测
    private long GetCollectionUid(int SkillId)
    {
        long mId = MonsterManager.Instance.getCanAttackMonster(SkillId, MainRole._MainRole.targetID, 1);
        if (mId != 0)
        {
            MainRole._MainRole.targetID = mId;
            return mId;
        }

        return 0;
    }

    //采集检测
    private void checkCollection()
    {
        if (!GameRoomM.Instance.GetCanNoAttackGame())
            return;
        if (_sendAttackLast < _attackSpeed)
            return;
        _sendAttackLast = 0;
        long targetID = GetCollectionUid(GameConst.GetNormallSkillByJob(job));
        if (targetID == 0)
            return;

        var Monster = MonsterManager.Instance.getOneMonster(targetID);
        if (Monster == null)
            return;


        Monster._monser.curLifeTime += (int)(_attackSpeed * 1000);

        if (Monster._monser.curLifeTime >= Monster._monser.maxLifeTime)
        {
            Monster._monser.curLifeTime = Monster._monser.maxLifeTime;

            TcpC.C2S_Mining(targetID);
        }

    }


    public override int getHurtDistance(int skillID, int dis = -1)
    {
        return -1;
    }
    private int EffIdx = 0;

    public async void SetDropEff(Point effPos, List<ItemInfo> ItemInfos)
    {
        Vector3 toPos = new Vector3((float)effPos.X, (float)effPos.Y, SceneManager.EffOffZ);

        //foreach (var item in ItemInfos)
        //{
        //    if (CFG_Item.GetById(item.CfgId).Quality > 2)
        //    {

        //        string poolKey = "drop_" + CFG_Item.GetById(item.CfgId).Quality;
        //        if (!PoolHelper.Instance.CheckPool(poolKey))
        //        {
        //            var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
        //            await baseObj.WaitForAsyncLoading();
        //            PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
        //        }

        //        var Eff = PoolHelper.Instance.GetPoolObj(poolKey);
        //        Eff.transform.SetParent(FightController.Instance.Root_Eff3D);
        //        Eff.transform.position = toPos;
        //        Eff.SetActive(true);
        //        string effJacKey = "DropEff" + EffIdx;

        //        Eff.transform.DOMove(this.transform.position, 0.2f).SetDelay(1f).OnComplete(() =>
        //        {
        //            PoolHelper.Instance.RecycObj(Eff);
        //        });

        //        EffIdx++;
        //    }

        //}
    }

    public void OnDestroy()
    {
        _MainRole = null;
    }
}

