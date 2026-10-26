using DG.Tweening;

using HotUpdateScripts.ProtoM;

using JEngine.Core;
using JEngine.Helper;

using pb;

using Spine.Unity;

using System;
using System.Collections.Generic;

using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Events;

namespace HotUpdateScripts.Game
{
    public class TurnBasedRole : MonoBehaviour
    {
        private Transform m_Spine;

        private Ease m_MoveEase = Ease.OutSine;

        private CFGItem_Skill m_SkillCfg;

        private CFGItem_SkillEffect m_SkillEff;

        // 攻击时是否需要移动到对手面前
        private bool m_IsNeedMove = true;

        private float m_BuffTime = 0.7f;

        private float m_EndWaitTime = 0.3f;

        private Dictionary<string, GameObject> m_BuffObjList = new Dictionary<string, GameObject>();

        public SkeletonAnimation spineAnim;
        public GameConst.ModelAnim animState;

        public ObjectType roleType;
        public TurnBasedM.CampType campType;

        public UnityAction OnComplete;

        public Transform root_hub;

        Tweener m_TweenerMoveTo;

        public List<int> bufferList = new List<int>();

        public long uid;

        public int cfgId;
        /// <summary>
        /// 站位 （1 - 6）
        /// </summary>
        public int position;

        /// <summary>
        /// 角色名字
        /// </summary>
        public string roleName;

        /// <summary>
        /// 最大血量
        /// </summary>
        public int maxHp;

        /// <summary>
        /// 当前血量
        /// </summary>
        public int currentHp;

        /// <summary>
        /// 技能ID
        /// </summary>
        public int skillId;

        /// <summary>
        /// 已死亡
        /// </summary>
        public bool isDead;

        /// <summary>
        /// 职业
        /// </summary>
        public int job;

        /// <summary>
        /// 目标位子类型。
        /// Movement 近战攻击时需要移动到角色面前
        /// Effect 特效播放位子，一般在角色身上
        /// Buff 特效一般在角色身上
        /// </summary>
        public enum GetPositionType
        {
            Movement,
            Effect,
            Buff,
        }


        private void Awake()
        {
            m_Spine = transform.Find("spine");
            if (m_Spine)
                spineAnim = m_Spine.GetComponent<SkeletonAnimation>();

            root_hub = transform.Find("root_hub");
        }

        private void Start()
        {
            Init();
        }

        public void Init()
        {
            animState = GameConst.ModelAnim.No;
            ChangeAnimationState(GameConst.ModelAnim.stand);
        }

        public void ChangeAnimationState(GameConst.ModelAnim actName)
        {
            if (actName != GameConst.ModelAnim.dead && actName != GameConst.ModelAnim.attack && !animState.ToString().Contains("skill") && animState.CompareTo(actName) == 0)
                return;
            animState = actName;

            if (spineAnim == null || (roleType != ObjectType.PLAYER && roleType != ObjectType.PARTNER))
                return;

            if (animState == GameConst.ModelAnim.attack || animState.ToString().Contains("skill"))
                SpineUtil.Instance.PlayOnce(spineAnim, animState.ToString(), () =>
                {
                    animState = GameConst.ModelAnim.stand;
                    SpineUtil.Instance.PlayLoop(spineAnim, animState.ToString());
                });
            else if (actName == GameConst.ModelAnim.dead)
                SpineUtil.Instance.PlayOnce(spineAnim, animState.ToString(), null);
            else
                SpineUtil.Instance.PlayLoop(spineAnim, animState.ToString());
        }

        public void StartAttack()
        {
            m_SkillCfg = CFG_Skill.GetById(skillId);
            m_SkillEff = CFG_SkillEffect.GetById(m_SkillCfg.SkillID);
            m_IsNeedMove = m_SkillCfg.SkillProfession % 10 == 1 && m_SkillCfg.Needaim == 1;

            if (m_IsNeedMove)
            {
                m_TweenerMoveTo = MoveToTargetPosition();
                m_TweenerMoveTo.OnComplete(Attack);
            }
            else
            {
                Attack();
            }
        }

        /// <summary>
        /// 被攻击目标的位置
        /// </summary>
        /// <param name="isOffset">角色移动时需要距离目标有段距离</param>
        /// <returns></returns>
        private Vector3 GetHitPositionOfsTanding(GetPositionType positionType)
        {
            long uid = GetFirstLivingUid();

            if (campType == TurnBasedM.CampType.BLUE_CAMP)
            {
                if(TurnBasedRoleManager.Instance.GetRedRoleByUid(uid, out TurnBasedRole role))
                {
                    return positionType switch
                    {
                        GetPositionType.Movement => role.GetWorldPosition() - new Vector3(2.3f, 0, 0),
                        GetPositionType.Effect => role.GetWorldPosition(),
                        GetPositionType.Buff => role.GetWorldPosition(),
                        _ => Vector3.zero,
                    };
                }
                else
                {
                    Debug.Log("未找到角色！ skill target role red uid " + uid);
                    return Vector3.zero;
                }
            }
            else if(campType == TurnBasedM.CampType.RED_CAMP)
            {
                if (TurnBasedRoleManager.Instance.GetBlueRoleByUid(uid, out TurnBasedRole role))
                {
                    return positionType switch
                    {
                        GetPositionType.Movement => role.GetWorldPosition() + new Vector3(2.3f, 0, 0),
                        GetPositionType.Effect => role.GetWorldPosition(),
                        GetPositionType.Buff => role.GetWorldPosition(),
                        _ => Vector3.zero,
                    };
                }
                else
                {
                    Debug.Log("未找到角色！ skill target role blueuid " + uid);
                    return Vector3.zero;
                }
            }
            else
                return Vector3.zero;

        }

        public Vector3 GetWorldPosition()
        {
            return transform.position;
        }

        private Tweener MoveToTargetPosition()
        {
            Vector3 targetPos = GetHitPositionOfsTanding(GetPositionType.Movement);
            return transform.DOMove(targetPos, TurnBasedFightCtrl.Instance.attackMoveSpeed).SetEase(m_MoveEase);
        }

        // 移动到原位
        private Tweener MoveBackToStandPosition()
        {
            return transform.DOLocalMove(Vector3.zero, TurnBasedFightCtrl.Instance.attackMoveSpeed).SetEase(m_MoveEase);
        }

        private async void Attack()
        {
            float waitTime = 0;

            if (!string.IsNullOrEmpty(m_SkillEff.StartEffect))
                waitTime = (float)m_SkillEff.StartRemainTime / 1000f;
            else if (!string.IsNullOrEmpty(m_SkillEff.BallisticEffect))
                waitTime = (float)m_SkillEff.BallisticRemainTime / 1000f;
            else
                waitTime = 0.5f;

            GameConst.ModelAnim animName = GameConst.GetAnim((int)m_SkillCfg.Needskill);
            ChangeAnimationState(animName);

            Debug.Log($"<color=yellow>Attack() | uid :</color> {uid}, roleType = {roleType}, cfgId = {cfgId}, name = {roleName}");
            Debug.Log($"<color=yellow>skill info | uid :skillID = {m_SkillCfg.UUID}, skillName:{m_SkillCfg.Name}, skillEffId:{m_SkillCfg.SkillID}, skill job = {m_SkillCfg.SkillProfession}, aniName = {animName}");
            
            await PlayEffect();

            TurnBasedFightCtrl.Instance.AddBuffers();

            float bloodTextWaitTime = waitTime * 0.7f;
            if (m_SkillEff.TargetID == 16)
                bloodTextWaitTime = 0.5f;
            else if (m_SkillEff.TargetID == 17)
                bloodTextWaitTime = 0.2f;

            CoroutineCall(bloodTextWaitTime, () =>
            {
                TurnBasedFightCtrl.Instance.UpdateRoleInfo();
                TurnBasedFightCtrl.Instance.CollectKilledRole();
            });

            CoroutineCall(waitTime, () =>
            {
                if (m_IsNeedMove)
                {
                    MoveBackToStandPosition();
                    CoroutineCall(TurnBasedFightCtrl.Instance.attackMoveSpeed + m_EndWaitTime, () =>
                    {
                        TurnBasedFightCtrl.Instance.ChangeState(TurnBasedFightCtrl.BattleState.AttackCompleted);
                    });
                }
                else
                {
                    CoroutineCall(m_EndWaitTime, () =>
                    {
                        TurnBasedFightCtrl.Instance.ChangeState(TurnBasedFightCtrl.BattleState.AttackCompleted);
                    });
                }
            });
        }

        /// <summary>
        /// 播放技能特效
        /// </summary>
        public async Task PlayEffect()
        {

            if (CFG_Actionset.ChealkId(m_SkillCfg.SkillID) && !string.IsNullOrEmpty(CFG_Actionset.GetById(m_SkillCfg.SkillID).Music))
            {
                AudioManager.Instance.PlaySound(CFG_Actionset.GetById(m_SkillCfg.SkillID).Music);
            }

            if (!CFG_SkillEffect.Get().DicDatas.ContainsKey(m_SkillCfg.SkillID))
                return;

            Vector3 tpos = GetHitPositionOfsTanding(GetPositionType.Effect);

            tpos.z = SceneManager.EffOffZ;

            if (!string.IsNullOrEmpty(m_SkillEff.BallisticEffect))
            {
                string poolKey = m_SkillEff.BallisticEffect;
                Debug.Log($"---- 1. SkillEff: {poolKey}");

                if (!PoolHelper.Instance.CheckPool(poolKey))
                {
                    var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                    await baseObj.WaitForAsyncLoading();
                    PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
                }

                var Eff = PoolHelper.Instance.GetPoolObj(poolKey);
                Eff.transform.SetParent(TurnBasedFightCtrl.Instance.EffectRoot);
                Eff.transform.position = new Vector3(transform.position.x, transform.position.y, SceneManager.EffOffZ);

                Vector2 direction = (transform.position - tpos);
                direction.Normalize();
                // 设置物体的朝向
                Eff.transform.rotation = Quaternion.AngleAxis(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, Vector3.forward);

                CoroutineCall((float)m_SkillEff.BallisticDelay / 1000f, () =>
                {
                    if (Eff == null)
                        return;
                    Eff.SetActive(true);
                    Eff.transform.DOMove(tpos, (float)m_SkillEff.BallisticRemainTime / 1000).OnComplete(() =>
                    {
                        PoolHelper.Instance.RecycObj(Eff);
                    });
                });
            }

            if (!string.IsNullOrEmpty(m_SkillEff.StartEffect))
            {
                string poolKey = m_SkillEff.StartEffect;
                Debug.Log($"++++ 2. SkillEff: {poolKey}");
                if (!PoolHelper.Instance.CheckPool(poolKey))
                {
                    var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                    await baseObj.WaitForAsyncLoading();
                    PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
                }
                var Eff = PoolHelper.Instance.GetPoolObj(poolKey);
                Eff.transform.SetParent(TurnBasedFightCtrl.Instance.EffectRoot);

                CoroutineCall((float)m_SkillEff.StartDelay / 1000f, () =>
                {
                    if (Eff == null)
                        return;
                    Eff.transform.position = new Vector3(transform.position.x, transform.position.y, SceneManager.EffOffZ);

                    if (campType == TurnBasedM.CampType.BLUE_CAMP)
                        Eff.transform.eulerAngles = new Vector3(0, 180, 0);
                    else if (campType == TurnBasedM.CampType.RED_CAMP)
                        Eff.transform.eulerAngles = Vector3.zero;

                    Eff.SetActive(true);
                });

                CoroutineCall((float)m_SkillEff.StartRemainTime / 1000f, () =>
                {
                    if (Eff == null)
                        return;
                    PoolHelper.Instance.RecycObj(Eff);
                });
            }

            if (!string.IsNullOrEmpty(m_SkillEff.BodyEffect))
            {
                string poolKey = m_SkillEff.BodyEffect;
                Debug.Log($"**** 3. SkillEff: {poolKey}");
                if (!PoolHelper.Instance.CheckPool(poolKey))
                {
                    var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                    await baseObj.WaitForAsyncLoading();
                    PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
                }

                var Eff = PoolHelper.Instance.GetPoolObj(poolKey);
                Eff.transform.SetParent(TurnBasedFightCtrl.Instance.EffectRoot);
                Eff.transform.position = tpos;

                CoroutineCall((float)m_SkillEff.BodyDelay / 1000f, () =>
                {
                    if (Eff == null)
                        return;
                    Eff.SetActive(true);
                    CoroutineCall((float)m_SkillEff.BodyRemainTime / 1000f, () =>
                    {
                        if (Eff == null)
                            return;
                        PoolHelper.Instance.RecycObj(Eff);
                    });
                });
            }
        }

        public void AddBuffers(int[] buffers)
        {
            for (int i = 0; i < buffers.Length; i++)
            {
                AddOrRemoveBuffer(buffers[i]);
            }

            CoroutineCall(m_BuffTime, () =>
            {
                ClearBuffObjDic();
            });
        }

        public async void AddOrRemoveBuffer(int bufferId, bool isAdd = true)
        {
            if (isAdd)
            {
                if (!bufferList.Contains(bufferId))
                    bufferList.Add(bufferId);
            }
            else
            {
                if (bufferList.Contains(bufferId))
                    bufferList.Remove(bufferId);
            }

            string effKey = CFG_BuffEffect.GetById(bufferId).BuffEffect;
            //Debug.Log($"---- BuffEffect: {effKey}");
            if (!string.IsNullOrEmpty(effKey))
            {
                if (isAdd)
                {
                    string poolKey = effKey;
                    if (!PoolHelper.Instance.CheckPool(poolKey))
                    {
                        var baseObj = new JPrefab(GameConst.GetEffPath(poolKey), true);
                        await baseObj.WaitForAsyncLoading();
                        PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
                    }

                    GameObject effObj = PoolHelper.Instance.GetPoolObj(effKey);
                    effObj.transform.SetParent(this.transform);
                    effObj.transform.localPosition = Vector3.zero;
                    effObj.gameObject.name = effKey;
                    effObj.SetActive(true);
                    AddJaction(effKey, effObj);
                }
                else
                {
                    ClearOneBuffObj(effKey);
                }
            }

        }

        public void AddJaction(string key, GameObject jac)
        {
            if (!m_BuffObjList.ContainsKey(key))
            {
                m_BuffObjList.Add(key, jac);
            }
            else
            {
                ClearOneBuffObj(key);
                m_BuffObjList[key] = jac;
            }
        }

        public void ClearBuffObjDic()
        {
            foreach (var item in m_BuffObjList.Values)
            {
                PoolHelper.Instance.RecycObj(item);
            }
            m_BuffObjList.Clear();
        }

        public void ClearOneBuffObj(string key)
        {
            if (m_BuffObjList.ContainsKey(key))
            {
                PoolHelper.Instance.RecycObj(m_BuffObjList[key]);
                m_BuffObjList.Remove(key);
            }
        }

        public void StopAttack()
        {
            ClearCoroutine();
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

        private System.Collections.IEnumerator IECoroutineCall(float time, Action callBack)
        {
            yield return new WaitForSeconds(time);
            callBack?.Invoke();
        }

        /// <summary>
        /// 受击对手中，第一个活着的对手的uid
        /// </summary>
        /// <returns></returns>
        private long GetFirstLivingUid()
        {
            if (campType == TurnBasedM.CampType.BLUE_CAMP)
            {
                List<TurnBasedBattle.TBObject> roleData = new List<TurnBasedBattle.TBObject>();
                if (TurnBasedFightCtrl.Instance.attackNum > 2)
                {
                    var data = TurnBasedFightCtrl.Instance.GetLastRoundData();
                    roleData = data.RedStats;
                }
                else
                    roleData = TurnBasedM.Instance.turnBasedBattleData.OriginRed.Objs;

                foreach (var item in roleData)
                {
                    if (item.Uid != 0 && item.State == TurnBasedBattle.TBObject.ObState.NORMAL)
                        return item.Uid;
                }
                return 0;
            }

            else if (campType == TurnBasedM.CampType.RED_CAMP)
            {
                List<TurnBasedBattle.TBObject> roleObjs = new List<TurnBasedBattle.TBObject>();
                if (TurnBasedFightCtrl.Instance.attackNum > 2)
                {
                    var data = TurnBasedFightCtrl.Instance.GetLastRoundData();
                    roleObjs = data.BlueStats;
                }
                else
                    roleObjs = TurnBasedM.Instance.turnBasedBattleData.OriginBlue.Objs;

                foreach (var item in roleObjs)
                {
                    if (item.Uid != 0 && item.State == TurnBasedBattle.TBObject.ObState.NORMAL)
                        return item.Uid;
                }
                return 0;
            }

            return 0;
        }

        public void StopAllTeween()
        {
            m_TweenerMoveTo?.Kill();
        }

        public void Dead()
        {
            isDead = true;
            bufferList.Clear();
            ChangeAnimationState(GameConst.ModelAnim.dead);
            ClearBuffObjDic();
        }

        void OnDestroy()
        {
            bufferList.Clear();
            StopAllTeween();
            ClearCoroutine();
            ClearBuffObjDic();
        }

    }
}
