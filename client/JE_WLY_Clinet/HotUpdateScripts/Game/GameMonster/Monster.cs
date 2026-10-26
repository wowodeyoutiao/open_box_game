using DG.Tweening;
using HotUpdateScripts;
using HotUpdateScripts.Game;
using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Helper;
using pb;
using Spine.Unity;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Monster : MonoBehaviour
{
    public SkeletonAnimation anim;
    public GameConst.ModelAnim _animState;
    public bool isEndMove = false;

    //public NavMeshAgent agent;
    public int _priority;
    public Point _gridNow;//当前格子

    public double _gridX, _gridY;

    private int _maxLifeTime;//最大时间
    public int maxLifeTime
    {
        get => _maxLifeTime;

        set
        {
            if (_maxLifeTime == value)
                return;
            _maxLifeTime = value;
        }
    }

    private int _curLifeTime;//当前时间
    public int curLifeTime
    {
        get => _curLifeTime;

        set
        {
            if (_curLifeTime == value)
                return;
            _curLifeTime = value;
        }
    }

    private int _maxHp;//最大血量
    public int maxHp
    {
        get => _maxHp;

        set
        {
            if (_maxHp == value)
                return;
            _maxHp = value;
        }
    }

    private int _hpNow;//当前血量
    public int hp
    {
        get => _hpNow;

        set
        {
            if (_hpNow == value)
                return;
            _hpNow = value;

        }
    }

    private int _maxMp;//最大蓝
    public int maxMp
    {
        get => _maxMp;

        set
        {
            if (_maxMp == value)
                return;
            _maxMp = value;

        }
    }

    private int _mpNow;//当前蓝
    public int mpNow
    {
        get => _mpNow;

        set
        {
            if (_mpNow == value)
                return;
            _mpNow = value;

        }
    }

    private int _level;//等级
    public int level
    {
        get => _level;

        set
        {
            if (_level == value)
                return;
            _level = value;
        }
    }

    private string _monsterName;//怪物名字
    public string monsterName
    {
        get => _monsterName;

        set
        {
            if (_monsterName.CompareTo(value) == 0)
                return;
            _monsterName = value;

        }
    }

    private long _uID;//ID
    public long uID
    {
        get => _uID;

        set
        {
            if (_uID == value)
                return;
            _uID = value;

        }
    }

    private int _cfgId;//ID
    public int cfgId
    {
        get => _cfgId;

        set
        {
            if (_cfgId == value)
                return;
            _cfgId = value;

        }
    }

    private Direction _dir;
    public Direction _Dir
    {
        get => _dir;

        set
        {
            if (_dir == value)
                return;
            _dir = value;

        }
    }

    private Dictionary<string, GameObject> GameObjDic = new Dictionary<string, GameObject>();
    public List<int> _BufferList = new List<int>();

    private void AddGameObj(string key, GameObject jac)
    {
        if (!GameObjDic.ContainsKey(key))
        {
            GameObjDic.Add(key, jac);
        }
        else
        {
            ClearOneObj(key);
            GameObjDic[key] = null;
            GameObjDic[key] = jac;
        }
    }

    private void ClearOneObj(string key)
    {
        if (GameObjDic.ContainsKey(key))
        {
            PoolHelper.Instance.RecycObj(GameObjDic[key]);
            GameObjDic.Remove(key);
        }
    }

    private void CleacGameObjDic()
    {
        foreach (var item in GameObjDic.Values)
        {
            PoolHelper.Instance.RecycObj(item);
        }
        GameObjDic.Clear();
    }

    private void OnDisable()
    {
        CleacGameObjDic();
    }

    public async void AddOrRemoveBuffer(int bufferId, bool isAdd = true)
    {

        if (isAdd)
        {
            if (!_BufferList.Contains(bufferId))
                _BufferList.Add(bufferId);
        }
        else
        {
            if (_BufferList.Contains(bufferId))
                _BufferList.Remove(bufferId);
        }

        string effKey = CFG_BuffEffect.GetById(bufferId).BuffEffect;

        if (!string.IsNullOrEmpty(effKey) && GameConst.CheakEff(effKey))
        {
            if (isAdd)
            {
                GameObject effObj = null;
                string poolKey = effKey;

                if (!PoolHelper.Instance.CheckPool(poolKey))
                {
                    var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                    await baseObj.WaitForAsyncLoading();
                    PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
                }

                if (!this.gameObject.activeSelf || this.gameObject.Equals(null)) return;

                effObj = PoolHelper.Instance.GetPoolObj(poolKey);
                effObj.transform.SetParent(this.transform);
                effObj.transform.localPosition = Vector3.zero;
                effObj.SetActive(true);
                AddGameObj(effKey, effObj);
            }
            else
            {
                ClearOneObj(effKey);
            }
        }
    }

    public void OnInitBuffer(List<BuffInfo> _buffInfos)
    {
        _BufferList.Clear();
        if (_buffInfos != null)
        {
            int Idx = 1;
            foreach (BuffInfo item in _buffInfos)
            {
                string jkey = "InitBuffer_" + item.BuffId;
                AddOrRemoveBuffer(item.BuffId,true);
                _BufferList.Add(item.BuffId);
                Idx++;
            }
        }
       
    }

    public Transform root_hub;

    public void Awake()
    {
       
    }
    public void OnInit()
    {
        anim = this.transform.Find("spine").GetComponent<SkeletonAnimation>();
        root_hub = this.transform.Find("root_hub");
        if (_gridNow == null)
            _gridNow = new Point();
        //agent.avoidancePriority = _priority;
        //agent.speed = 2;
        //agent.radius = 0.1f;

        _animState = GameConst.ModelAnim.No;
        changeState(GameConst.ModelAnim.stand);

        _Dir = Direction.LEFT;

        if (CFG_Monster.GetById(cfgId).Isdisplaybar == 0)
            maxLifeTime = CFG_Monster.GetById(cfgId).Delay;

        curLifeTime = 0;

    }

    private void Update()
    {
        if (_animState.CompareTo(GameConst.ModelAnim.walk) == 0)
        {
            if (!IsMoving())
            {
                 changeState(GameConst.ModelAnim.stand);
            }

        }
    }

    public void changeState(GameConst.ModelAnim actName)
    {
        if (actName != GameConst.ModelAnim.dead && actName != GameConst.ModelAnim.attack && !_animState.ToString().Contains("skill") && _animState.CompareTo(actName) == 0)
            return;
        _animState = actName;

        if (_animState == GameConst.ModelAnim.attack || _animState.ToString().Contains("skill"))
        {
            SpineUtil.Instance.PlayOnce(anim, actName.ToString(), () => {
                _animState = GameConst.ModelAnim.stand;
                SpineUtil.Instance.PlayLoop(anim, _animState.ToString());
            });
        }
          
        else if (actName == GameConst.ModelAnim.dead)
        {
            SpineUtil.Instance.PlayOnce(anim, actName.ToString(), null);
        }
        else
            SpineUtil.Instance.PlayLoop(anim, actName.ToString());
    }

    public async void SetHurtChange(int changeNum,long AttackerId,int skillId)
    {
        if (changeNum < 0)
        {
            string poolKey = "Hurt_1";

            if (skillId > 0 && CFG_Skill.GetById(skillId).SkillProfession % 10 != 0)
            {
                poolKey = "Hurt_" + (CFG_Skill.GetById(skillId).SkillProfession % 10);

            }
            AudioManager.Instance.PlaySound((AudioKey.Sound)Enum.Parse(typeof(AudioKey.Sound), poolKey));

            if (!PoolHelper.Instance.CheckPool(poolKey))
            {
                var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                await baseObj.WaitForAsyncLoading();
                PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
            }

            var Eff = PoolHelper.Instance.GetPoolObj(poolKey);
            Eff.transform.SetParent(FightController.Instance.Root_Eff3D);
            Eff.transform.position = new Vector3(transform.position.x,transform.position.y, SceneManager.EffOffZ);
            Eff.SetActive(true);

            CoroutineCall(0.8f, () => {
                PoolHelper.Instance.RecycObj(Eff);
            });
        }
    }

    public async void SetAttack(Point Location, Direction direction,int SkillId)
    {
        setRotation(direction);
        //Log.Print( "SetAttack  SkillId+ " + SkillId);
        //Log.Print((int)CFG_Skill.GetById(SkillId).Needskill + "SetAttack + " + SkillId);
        changeState(GameConst.GetAnim((int)CFG_Skill.GetById(SkillId).Needskill));

        if (Location != null)
        {
            var cfgInfo = CFG_Monster.GetById(cfgId);
            string poolKey = cfgInfo.AttackEffect;

            var SoundKey = AudioKey.Sound.BossAttack;
            if (cfgInfo.Isboss == 0)
                SoundKey = string.IsNullOrEmpty(poolKey) ? AudioKey.Sound.JZAttack : AudioKey.Sound.YCAttack;
            AudioManager.Instance.PlaySound(SoundKey);

            if (string.IsNullOrEmpty(poolKey))
                return;
            var tpos = new Vector3((float)Location.X, (float)Location.Y, SceneManager.GetOffZ(Location.X, Location.Y));
            if (!PoolHelper.Instance.CheckPool(poolKey))
            {
                var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                await baseObj.WaitForAsyncLoading();
                PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
            }
            
            var Eff = PoolHelper.Instance.GetPoolObj(poolKey);
            Eff.transform.SetParent(FightController.Instance.Root_Eff3D);
            Eff.transform.position = new Vector3(transform.position.x, transform.position.y, SceneManager.EffOffZ);
            Eff.SetActive(true);

            Vector2 _direction = (transform.position - tpos);
            _direction.Normalize();
            Eff.transform.rotation = Quaternion.AngleAxis(Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg, Vector3.forward);

            Eff.transform.DOMove(tpos, 0.2f).OnComplete(() =>
            {
                Eff.transform.DOKill();
                PoolHelper.Instance.RecycObj(Eff);
            });
        }
    }

    private bool _isMove = false;

    public void moveTo(Point Location,Direction direction )
    {
        float offZ = SceneManager.GetOffZ(Math.Floor(Location.X), Math.Floor(Location.Y));
        if (offZ != SceneManager.Block)
        {
            changeState(GameConst.ModelAnim.walk);
            _isMove = true;
            Vector3 _runToPos = new Vector3((float)Location.X, (float)Location.Y, offZ);
            //agent.SetDestination(_runToPos);
            this.transform.DOKill();
            this.transform.DOMove(_runToPos, 0.8f).SetEase(Ease.Linear).OnComplete(() => {
                _isMove = false;
            }) ;
            setRotation(direction);

            _gridNow.X = Math.Floor(Location.X);
            _gridNow.Y = Math.Floor(Location.Y);

            _gridX = _gridNow.X;
            _gridY = _gridNow.Y;
        }

    }

    //传送
    public async void SetTeleport(Point point, Direction dir, TeleportType Type)
    {
        wrap(new Vector3((float)point.X, (float)point.Y, SceneManager.GetOffZ(Math.Floor(point.X), Math.Floor(point.Y))));
        setRotation(dir);

        if (Type == TeleportType.BLINK)
        {
            string poolKey = "eff_ChuanSong";
            if (!PoolHelper.Instance.CheckPool(poolKey))
            {
                var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                await baseObj.WaitForAsyncLoading();
                PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
            }
            var Eff = PoolHelper.Instance.GetPoolObj(poolKey);
            Eff.transform.SetParent(FightController.Instance.Root_Eff3D);
            Eff.transform.position = this.transform.position;
            Eff.SetActive(true);
            CoroutineCall(1, () => {
                if (Eff != null)
                    PoolHelper.Instance.RecycObj(Eff);
            });
        }
    }

    public async void wrap(Vector3 target,bool isInit = false)
    {
        //agent.Warp(target);
        this.transform.position = target;
        _gridNow.X = target.x;
        _gridNow.Y = target.y;

        //if (isInit)
        //{
            //string poolKey = "eff_MonsterBorn";
            //if (!PoolHelper.Instance.CheckPool(poolKey))
            //{
            //    var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
            //    await baseObj.WaitForAsyncLoading();
            //    PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
            //}

            //var Eff = PoolHelper.Instance.GetPoolObj(poolKey);
            //Eff.transform.SetParent(FightController.Instance.Root_Eff3D);
            //Eff.transform.position = this.transform.position;
            //Eff.SetActive(true);
            //CoroutineCall(1.5f, () =>
            //{
            //    PoolHelper.Instance.RecycObj(Eff);
            //});
        //}
    }

    public void setRotation(Direction dir)
    {
        if (dir != Direction.UP && dir != Direction.DOWN)
        {
            _Dir = dir;
        }

        Vector3 endPos = Vector3.one;
        if (_Dir == Direction.UP_LEFT || _Dir == Direction.DOWN_LEFT || _Dir == Direction.LEFT)
            endPos = Vector3.zero;
        else if (_Dir == Direction.UP_RIGHT || _Dir == Direction.DOWN_RIGHT || _Dir == Direction.RIGHT)
            endPos = new Vector3(0, 180, 0);

        this.transform.eulerAngles = endPos;
       // this.transform.DORotate(endPos, 0.18f);
    }


    public bool IsMoving()
    {
        return _isMove;
    }

   // private JAction DesJa;
    public void destoryMonster()
    {
        //Debug.Log("destoryMonster-");
        changeState(GameConst.ModelAnim.dead);
        transform.DOKill();

        CoroutineCall(1.2f, () => {
            if (this.gameObject != null && !this.gameObject.Equals(null))
            {
                string pooKey = this.gameObject.name.Split('/')[0];
                PoolHelper.Instance.RecycObj(pooKey, this.gameObject);
            }
        });
        CleacGameObjDic();

    }

    private void ClearCoroutine()
    {
        if (CoroutineMgr.Instance == null)
            return;

        foreach (var item in coroutines)
        {
            CoroutineMgr.Instance.StopCoroutine(item);
        }
        coroutines.Clear();
    }
    private HashSet<Coroutine> coroutines = new HashSet<Coroutine>();
    private Coroutine CoroutineCall(float time, Action callBack)
    {
        var coroutineItem = CoroutineMgr.Instance.StartCoroutine(IECoroutineCall(time, callBack));
        coroutines.Add(coroutineItem);
        return coroutineItem;
    }

    private System.Collections.IEnumerator IECoroutineCall(float time,Action callBack)
    {
        yield return new WaitForSeconds(time);
        callBack?.Invoke();
    }

    public void Destory()
    {
        ClearCoroutine();
        CleacGameObjDic();
        //if (DesJa != null)
        //{
        //    DesJa.Cancel();
        //}
        //PoolHelper.Instance.RecycObj("Monser" + cfgId, this.gameObject);
    }

    void OnDestroy() 
    {
        Destory();
    }


    public int getHurtDistance(int skillID, int dis = -1)
    {
        if (hp <= 0 && maxLifeTime ==0)
            return -1;

        if (_animState == GameConst.ModelAnim.dead)
            return -1;

        // int distance = MainRole._MainRole.getDistanceFromMainRole(_gridNow.X, _gridNow.Y);
        int distance = FightController.Instance.getDistance(_gridNow.X, _gridNow.Y,SkillM.Instance.GetUiDBySkillId(skillID));
        if (dis > 0)
        {
            if (dis >= distance)
            {
                return (int)distance;
            }
        }
        else
        {
            var cfgInfo = CFG_Skill.GetById(skillID);
            decimal needDistance = cfgInfo.MaxDistance;

            if (distance <= needDistance)
            {
                return (int)distance;
            }
        }

        return -1;
    }
}

