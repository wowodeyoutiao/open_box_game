using DG.Tweening;

using HotUpdateScripts;
using HotUpdateScripts.Game;
using HotUpdateScripts.ProtoM;

using JEngine.Core;
using JEngine.Event;
using JEngine.Helper;
using JEngine.UI;

using MetaFramework.UI;

using pb;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using static pb.TurnBasedBattle.Round;

public class UIGame3D : UIBase<UIModelGame3D, UIViewGame3D>
{
    public override UILayer GetLayer()
    {
        return UILayer.Base;
    }
    public class HUD
    {
        public GameObject obj;
        public Slider HBslider;
        public Text text;
        public Text text_HP;
        public Image Img_Fill;
        public Image Img_NextFile;
        public Image[] Img_buffers;
    }
    private Dictionary<long, HUD> HuDDic = new Dictionary<long, HUD>();

    private Color[] BossHubColor = {
        new Color(214 / 255f, 66  /255f,54 / 255f),
        new Color(255 / 255f, 154 / 255f, 64 / 255f),
        new Color(143 / 255f, 63 / 255f, 220 / 255f),
        new Color(219 / 255f, 86 / 255f, 212 / 255f),
        new Color(219 / 255f, 86 / 255f, 212 / 255f),
    };

    private Dictionary<HUB, GameObject> HubToGoDic = new Dictionary<HUB, GameObject>();


    private Dictionary<long,GameObject> TipEnemyDic = new Dictionary<long, GameObject>();
    private Queue<GameObject> TipEnemyQueue = new Queue<GameObject>();


    public override void OnInit()
    {
        HubToGoDic.Add(HUB.BossHUB, GetView().root_BossMonterHub.gameObject);
        HubToGoDic.Add(HUB.PlayerHUB, GetView().obj_PlayerHub);
        HubToGoDic.Add(HUB.MonsterHUB, GetView().obj_Monster);
        HubToGoDic.Add(HUB.CollectHUB, GetView().root_Collect);

        GetView().UpateCallBack = SetUUDShow;
    }

    public enum HUB 
    {
        PlayerHUB,
        MonsterHUB,
        BossHUB,
        CollectHUB,
    }

    private void GetEnemyTip(long id,Vector2 uiPos)
    {
        if (!TipEnemyDic.ContainsKey(id))
        {
            GameObject go;
            if (TipEnemyQueue.Count <= 0)
            {
                go = GameObject.Instantiate(GetView().tip_enemy, GetView().rect_Monster.transform);
                go.transform.SetParent(GetView().rect_Monster);
                go.transform.localScale = Vector3.one;
            }
            else
            {
                go = TipEnemyQueue.Dequeue();
            }
            go.SetActive(true);
            TipEnemyDic.Add(id, go);

        }

        if (Mathf.Abs(uiPos.x) > - GetView().rect_Monster.rect.x)
        {
            uiPos.x = uiPos.x > 0 ? GetView().rect_Monster.rect.xMax - 20 : GetView().rect_Monster.rect.xMin + 20;
        }

        if (Mathf.Abs(uiPos.y) > - GetView().rect_Monster.rect.y)
        {
            uiPos.y = uiPos.y > 0 ? GetView().rect_Monster.rect.yMax : GetView().rect_Monster.rect.yMin + 20;
        }
        float offy = (uiPos.y - GetView().rect_Monster.rect.y) / GetView().rect_Monster.rect.size.y;
        float offx = (uiPos.x - GetView().rect_Monster.rect.x) / GetView().rect_Monster.rect.size.x;

        Vector3 rot;
        if (offy < 0.3333)
        {
            if (offx < 0.3333)
                rot = new Vector3(0, 0, 135);
            else if (offx < 0.6666)
                rot = new Vector3(0, 0, 180);
            else
                rot = new Vector3(0, 0, 225);
        }
        else if (offy < 0.6666)
        {
            if (offx < 0.3333)
                rot = new Vector3(0, 0, 90);
            else
                rot = new Vector3(0, 0, 270);
        }
        else
        {
            if (offx < 0.3333)
                rot = new Vector3(0, 0, 45);
            else if (offx < 0.6666)
                rot = new Vector3(0, 0, 0);
            else
                rot = new Vector3(0, 0, 315);
        }
        TipEnemyDic[id].transform.localPosition = uiPos;
        TipEnemyDic[id].transform.localEulerAngles = rot;
    }


    private void RecycEnemyTip(long uid)
    {
        if (TipEnemyDic.ContainsKey(uid))
        {
            TipEnemyDic[uid].SetActive(false);
            TipEnemyQueue.Enqueue(TipEnemyDic[uid]);
            TipEnemyDic.Remove(uid);
        }
    }

    private HUD GetHUDById(long id, HUB hub = HUB.PlayerHUB)
    {
        if (!HuDDic.ContainsKey(id))
        {
            string Key = hub.ToString();
            GameObject obj = HubToGoDic[hub];
            if (!PoolHelper.Instance.CheckPool(Key))
            {
                PoolHelper.Instance.AddPool(Key, obj);
            }
            GameObject go = PoolHelper.Instance.GetPoolObj(Key);

            HuDDic.Add(id,new HUD { 
                obj = go, 
                HBslider = go.GetComponent<Slider>(), 
                text = JUIExtensions.txt(go.transform.Find("txt_name")),
                Img_Fill = JUIExtensions.img(go.transform.Find("Fill Area/Fill"))
            });

            if (go.transform.Find("txt_HP"))
            {
                HuDDic[id].text_HP = JUIExtensions.txt(go.transform.Find("txt_HP"));
            }

            if (go.transform.Find("NextFile"))
            {
                HuDDic[id].Img_NextFile = JUIExtensions.img(go.transform.Find("NextFile"));
            }

            if (go.transform.Find("bufferRoot"))
            {
                var root = go.transform.Find("bufferRoot");
                HuDDic[id].Img_buffers = new Image[root.childCount];
                for (int i = 0; i < root.childCount; i++)
                {
                    HuDDic[id].Img_buffers[i] = JUIExtensions.img(root.GetChild(i));
                }
            }

            go.transform.SetParent(hub == HUB.PlayerHUB ? GetView().root_PlayerHub : GetView().root_Monter);
            go.transform.localScale = Vector3.one;
            go.SetActive(true);
        }
        return HuDDic[id];
    }

    private void SetHUDById(long id)
    {
        if (HuDDic.ContainsKey(id))
        {
            PoolHelper.Instance.RecycObj(HuDDic[id].obj);
            HuDDic.Remove(id);
        }
    }

    private void CleatHUB()
    {
        foreach (var item in HuDDic)
        {
            PoolHelper.Instance.RecycObj(item.Value.obj);
        }
        HuDDic.Clear();
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);

    }

    public void SetUUDShow()
    {
        if (isNullGo || GameRoomM.Instance._GameState == GameRoomM.GameState.Over)
            return;
        var monsets = MonsterManager.Instance.getMonsterObjs();
        for (int i = 0; i < monsets.Count; i++)
        {
            if (monsets[i]._monser != null )
            {
                var _monser = monsets[i]._monser;
                var uiPos = GetCanvarPos(_monser.root_hub.transform.position);
                if (Mathf.Abs(uiPos.x) > - GetView().rect_Monster.rect.x || Mathf.Abs(uiPos.y) > - GetView().rect_Monster.rect.y)
                {
                    SetHUDById(_monser.uID);

                    GetEnemyTip(_monser.uID, uiPos);
                }
                else
                {
                    RecycEnemyTip(_monser.uID);

                    var CFGMonster = CFG_Monster.GetById(_monser.cfgId);
                    HUB _hub = CFGMonster.Isboss == 0 ? HUB.MonsterHUB : HUB.BossHUB ;
                    if (CFGMonster.Isdisplaybar == 0)
                    {
                        _hub = HUB.CollectHUB;

                        if (_monser.curLifeTime == 0)
                        {
                            SetHUDById(_monser.uID);
                        }
                    }

                    if (_hub != HUB.CollectHUB || (_hub == HUB.CollectHUB && _monser.curLifeTime > 0))
                    {
                        var hud = GetHUDById(_monser.uID, _hub);
                        hud.obj.transform.localPosition = uiPos;

                        //hud.text.text = "(" + _monser._gridNow.X + "," + _monser._gridNow.Y + ")" + CFG_Monster.GetHUDNameById(_monser.cfgId);
                        hud.text.text = CFG_Monster.GetHUDNameById(_monser.cfgId);
                        if (_hub == HUB.MonsterHUB)
                        {
                            hud.HBslider.value = (float)_monser.hp / _monser.maxHp;
                        }
                        else if (_hub == HUB.CollectHUB)
                        {
                            int usedTime = (int)Mathf.Ceil(_monser.curLifeTime / 1000);
                            int allTime = (int)(_monser.maxLifeTime / 1000);
                            hud.HBslider.value = (float)usedTime / allTime;
                            hud.text_HP.text = (allTime - usedTime).ToString();
                        }
                        else
                        {
                            int oneHP = _monser.maxHp / (int)CFGMonster.Barnum;//CFG_XMLConfig.xmlConfig.WorldBoss.bloodCount;
                            int curHp = _monser.hp == 0 ? 0 : (_monser.hp % oneHP == 0 ? oneHP : _monser.hp % oneHP);
                            int curCount = Mathf.CeilToInt((float)_monser.hp / oneHP);
                            hud.HBslider.value = (float)curHp / oneHP;
                            hud.text_HP.text = "X" + curCount;
                            hud.Img_Fill.color = BossHubColor[curCount % 5];

                            hud.Img_NextFile.enabled = curCount > 1;
                            if (curCount > 1)
                            {
                                hud.Img_NextFile.color = BossHubColor[(curCount - 1) % 5];
                            }
                        }

                        if (_hub != HUB.CollectHUB)
                        {
                            for (int j = 0; j < hud.Img_buffers.Length; j++)
                            {
                                bool isShow = j < _monser._BufferList.Count && !string.IsNullOrEmpty(CFG_BuffEffect.GetById(_monser._BufferList[j]).Icon);
                                hud.Img_buffers[j].gameObject.SetActive(isShow);
                                if (isShow)
                                {
                                    var Buffer = CFG_BuffEffect.GetById(_monser._BufferList[j]);
                                    ResouseHelper.Instance.LoadSpriteByAtlas(Buffer.Gallery, Buffer.Icon, hud.Img_buffers[j]);
                                }
                            }
                        }
                    }
                }
            }
        }

        var Players = FightController.Instance.GetRolesInfo();
        
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].role != null)
            {
                var role = Players[i].role;

                if (role._ObjectType != pb.ObjectType.PLAYER && role._ObjectType != pb.ObjectType.PARTNER)
                    continue;

                var uiPos = GetCanvarPos(role.root_hub.position);
                bool isOverSize = Mathf.Abs(uiPos.x) > -GetView().rect_Monster.rect.x || Mathf.Abs(uiPos.y) > -GetView().rect_Monster.rect.y;
                if (role.hp <= 0 || isOverSize)
                {
                    SetHUDById(role.uID);

                    if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Player)
                    { 
                        if (isOverSize && role.hp > 0 )
                            GetEnemyTip(role.uID, uiPos);
                        else
                            RecycEnemyTip(role.uID);
                    }
                }
                else
                {
                    RecycEnemyTip(role.uID);

                    var hud = GetHUDById(role.uID, HUB.PlayerHUB);
                    hud.obj.transform.localPosition = uiPos;
                    hud.HBslider.value = (float)role.hp / role.maxHp;

                    string Str = "";
                    if (role._ObjectType == pb.ObjectType.PARTNER && role.OwnerUid != 0)
                        Str = GetRoleStr(GameRoomM.Instance.getPlayerInfoByID(role.OwnerUid).Nickname, role.OwnerUid == RoleM.Instance.Uid) + "\n" + role.roleName;
                    else
                        Str = GetRoleStr(role.roleName, role.uID == RoleM.Instance.Uid);

                    //hud.text.text = "(" + role._gridNow.X + "," + role._gridNow.Y + ")" + Str;
                    hud.text.text = Str;
                    for (int j = 0; j < hud.Img_buffers.Length; j++)
                    {
                        bool isShow = j < role._BufferList.Count && !string.IsNullOrEmpty(CFG_BuffEffect.GetById(role._BufferList[j]).Icon);

                        hud.Img_buffers[j].gameObject.SetActive(isShow);
                        if (isShow)
                        {
                            var Buffer = CFG_BuffEffect.GetById(role._BufferList[j]);
                            ResouseHelper.Instance.LoadSpriteByAtlas(Buffer.Gallery, Buffer.Icon, hud.Img_buffers[j]);
                        }
                    }
                }
            }
        }

        var TurnBasedPlayer = TurnBasedRoleManager.Instance.allRoles;

        for (int i = 0; i < TurnBasedPlayer.Count; i++)
        {
            if (TurnBasedPlayer[i] != null)
            {
                var role = TurnBasedPlayer[i];
                var uiPos = GetCanvarPos(role.root_hub.position);
                bool isOverSize = Mathf.Abs(uiPos.x) > -GetView().rect_Monster.rect.x || Mathf.Abs(uiPos.y) > -GetView().rect_Monster.rect.y;
                if (role.currentHp <= 0 || isOverSize)
                {
                    SetHUDById(role.uid);

                    if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Player)
                    {
                        if (isOverSize && role.currentHp > 0)
                        {
                            GetEnemyTip(role.uid, uiPos);
                        }
                        else
                        {
                            RecycEnemyTip(role.uid);
                        }
                    }
                }
                else
                {
                    RecycEnemyTip(role.uid);

                    var hud = GetHUDById(role.uid, HUB.PlayerHUB);
                    hud.obj.transform.localPosition = uiPos;
                    hud.HBslider.value = (float)role.currentHp / role.maxHp;

                    string name = null;
                    if (role.roleType == pb.ObjectType.PLAYER)
                        name = GetRoleStr(TurnBasedM.Instance.GetRoleInfo(role.uid).Name, role.uid == RoleM.Instance.Uid);
                    else if (role.roleType == pb.ObjectType.PARTNER)
                        name = GameUtil.Instance.GetStrByQuality(CFG_Partner.GetById(role.cfgId).Quality, role.roleName);

                    hud.text.text = name;

                    for (int j = 0; j < hud.Img_buffers.Length; j++)
                    {
                        bool isShow = j < role.bufferList.Count && !string.IsNullOrEmpty(CFG_BuffEffect.GetById(role.bufferList[j]).Icon);

                        hud.Img_buffers[j].gameObject.SetActive(isShow);
                        if (isShow)
                        {
                            var Buffer = CFG_BuffEffect.GetById(role.bufferList[j]);
                            ResouseHelper.Instance.LoadSpriteByAtlas(Buffer.Gallery, Buffer.Icon, hud.Img_buffers[j]);
                        }
                    }
                }
            }
        }
    }

    private string GetRoleStr(string str,bool isMy)
    {
        int QuaIdx = 8;
        if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Player)
        {
            if (isMy)
                QuaIdx = 2;
            else
                QuaIdx = 7;
        }

        return GameUtil.Instance.GetStrByQuality(QuaIdx, str);
    }

    private Vector2 GetCanvarPos(Vector3 worldPoint)
    {
        Vector2 anchoredPosition;
        Vector2 screenPosition = FightController.Instance.MainCamer.WorldToScreenPoint(worldPoint);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(GetView().rect_Monster, screenPosition, FightController.Instance.UICamera, out anchoredPosition);
        //anchoredPosition.y += 20;
        return anchoredPosition ;
    }

    /// <summary>
    /// 血量变化
    /// </summary>
    /// <param name="id">uid</param>
    /// <param name="changeNum">变化值</param>
    /// <param name="isTurnBased">是否为回合游戏</param>
    private void OnHPChangeEff(long id,int changeNum, bool isTurnBased = false)
    {
        if (HuDDic.ContainsKey(id))
        {
            GameObject go = PoolHelper.Instance.GetPoolObj("HPChangeEff", GetView().root_FlyHP.gameObject);
            go.transform.SetParent(GetView().rect_HPChnage);
            Vector3 basePos = HuDDic[id].obj.transform.position;
            go.transform.position = basePos;
            go.transform.localScale = Vector3.one;
            bool isEnemy;
            if (isTurnBased)
                isEnemy = TurnBasedRoleManager.Instance.GetRoleCampType(id) == TurnBasedM.CampType.RED_CAMP;
            else
                isEnemy = MonsterManager.Instance.getOneMonster(id) != null;

            if (!isEnemy && GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Player)
            {
                var player = FightController.Instance.GetOneRoleInfo(id);
                if (player != null)
                {
                    isEnemy = (player.role._ObjectType == pb.ObjectType.PLAYER && player.role.uID != RoleM.Instance.Uid) ||
                        (player.role._ObjectType == pb.ObjectType.PARTNER && player.role.OwnerUid != RoleM.Instance.Uid);
                }
            }

            JUIExtensions.txt(go.transform).text = !isEnemy && changeNum < 0 ? changeNum.ToString() :"";
            JUIExtensions.txt(go.transform.Find("add")).text = changeNum > 0 ? "+" + changeNum : "";
            JUIExtensions.txt(go.transform.Find("jian_monster")).text = isEnemy && changeNum < 0 ? changeNum.ToString() : "";

            go.SetActive(true);
            int off = changeNum > 0 ? -30 : 50;
            if (changeNum < 0)
            {
                int offx = off;
                if (Random.Range(0, 2) == 0)
                    offx = - offx;
                DOTween.Sequence().Append(go.transform.DOScale(Vector3.one * 1.2f, 0.2f)).
                        Append(go.transform.DOLocalMoveY(go.transform.localPosition.y + off, 0.3f)).
                        Append(go.transform.DOScale(Vector3.one * 1.5f, 0.2f)).
                        Append(go.transform.DOLocalMove(new Vector3(go.transform.localPosition.x + offx, go.transform.localPosition.y + (off * 2), go.transform.localPosition.z), 0.2f)).
                        OnComplete(() => {
                            go.transform.DOKill();
                            PoolHelper.Instance.RecycObj(go);
                        });
            }
            else
                go.transform.DOLocalMoveY(go.transform.localPosition.y + off, 0.3f).OnComplete(() =>
                {
                    go.transform.DOKill();
                    PoolHelper.Instance.RecycObj(go);
                });
        }
    }

    private async void OnMissOrCriticalShow(long id, bool miss, bool critical)
    {
        if (HuDDic.ContainsKey(id) && (miss || critical))
        {
            Vector3 endPos = HuDDic[id].obj.transform.position;
            string poolKey = miss ? "Attack_Miss" : "Attack_BJ";
            if (!PoolHelper.Instance.CheckPool(poolKey))
            {
                var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                await baseObj.WaitForAsyncLoading();
                PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
            }

            GameObject go = PoolHelper.Instance.GetPoolObj(poolKey);
           
            go.transform.SetParent(GetView().rect_HPChnage);
            go.transform.localScale = Vector3.one;
            go.transform.position = endPos;
            go.gameObject.SetActive(true);

            if (miss)
                go.transform.DOLocalMoveY(go.transform.localPosition.y + ( - 50), 0.2f).OnComplete(() =>
                {
                    go.transform.DOKill();
                    PoolHelper.Instance.RecycObj(go);
                });
            else
                go.transform.DOScale(Vector3.one * 1.5f, 0.2f).OnComplete(() =>
                {
                    go.transform.DOLocalMoveY(go.transform.localPosition.y + 90, 0.3f).OnComplete(() =>
                    {
                        go.transform.DOKill();
                        PoolHelper.Instance.RecycObj(go);
                    });
                });
        }
    }

    public override void OnExit()
    {
        GetView().UpateCallBack = null;
        JEvent.defaultEvent.Unregister(this);
        //CleatHUB();
    }

    [Subscriber(ThreadMode.Main)]
    private void OnHUDReMove(GameEvent.Fight_Die _e)
    {
        SetHUDById(_e.id);
        RecycEnemyTip(_e.id);
    }

    [Subscriber(ThreadMode.Main)]
    private void OnHPChangeEff(GameEvent.Fight_HPChange _e)
    {
        OnHPChangeEff(_e.tid, _e.changeNum, _e.isTurnBased);
        OnMissOrCriticalShow(_e.tid, _e.Miss, _e.Critical);
    }

    public override void OnDestroy()
    {
        OnExit();
    }
}