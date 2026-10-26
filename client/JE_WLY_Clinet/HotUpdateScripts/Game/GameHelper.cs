using HotUpdateScripts;
using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Event;
using JEngine.Helper;
using JEngine.MyNet;
using MetaFramework.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class TimeItem
{
    // 原为 C# 12 主构造函数 class TimeItem()，降级为显式无参构造函数（语义等价）
    public TimeItem()
    {
    }

    public TimeItem(float _DelayTime, Action _action) :this()
    {
        DelayTime = _DelayTime;
        action = _action;
        IsUsed = false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="_DelayTime"> 总执行多少时间（_isLoop true 时无效）</param>
    /// <param name="repeat"> 间隔</param>
    /// <param name="_action"> 事件</param>
    /// <param name="_isLoop"> 循环执行</param>
    /// <param name="isDelay"> 循环第一次是否延迟</param>
    public TimeItem(float _DelayTime, float repeat, Action _action,bool _isLoop = false,bool isDelay = false) : this()
    {
        DelayTime = _DelayTime;
        action = _action;
        repeatTime = repeat;
        _repeatTime = isDelay ? repeat : 0;
        timeType = TimeType.Repeat;
        IsUsed = false;
        isLoop = _isLoop;
    }

    public enum TimeType
    {
        Repeat,
        Normal,
    }
    public float DelayTime = 0f;
    public bool isLoop = false;
    public TimeType timeType = TimeType.Normal;
    public Action action;
    public float repeatTime = 1f;
    public float _repeatTime = 0f;
    public bool IsUsed = false;
}

public class GameHelper : MonoBehaviour
{
    private static GameHelper _instance;
    public static GameHelper Instance
    {
        get
        {
           
            return _instance;
        }
    }
    public string ActiveSenceName = "";
    GameHelpMessage gameMessage = new GameHelpMessage();

    private TimeItem ZeroCheakTime;

    public void ClearZeroCheakTime()
    {
        if (ZeroCheakTime != null)
        {
            RemoveInvoke(ZeroCheakTime);
            ZeroCheakTime = null;
        }
    }
    public void SetZeroCheakTime()
    {
        ClearZeroCheakTime();
        int seconds = (int)GameUtil.Instance.GetNowToTimeSecoud(1, 0,00,00);

        ZeroCheakTime = AddInvoke(new TimeItem(seconds, () => {
            JEvent.defaultEvent.Post(new GameEvent.Refresh_ZeroTime());
            SetZeroCheakTime(); // 重新设置
        }));
    }
    private List<TimeItem> TimeList = new List<TimeItem>();

    public TimeItem AddInvoke(TimeItem _add)
    {
        TimeList.Add(_add);
        return _add;
    }

    public void RemoveInvoke(TimeItem _Item)
    {
        TimeList.Remove(_Item);
    }


    private void Update()
    {
        List<TimeItem> itemsToRemove = new List<TimeItem>();
        for (int i = 0; i < TimeList.Count; i++)
        {
            var _timeItem = TimeList[i];
            if (!_timeItem.IsUsed)
            {
                _timeItem.DelayTime -= Time.deltaTime;

                if (_timeItem.timeType == TimeItem.TimeType.Repeat)
                {
                    if (_timeItem._repeatTime <= 0)
                        _timeItem.action?.Invoke();

                    if (_timeItem._repeatTime <= 0)
                    {
                        _timeItem._repeatTime = _timeItem.repeatTime;
                    }

                    _timeItem._repeatTime -= Time.deltaTime;

                    
                }
                else
                {
                    if (_timeItem.DelayTime <= 0)
                    {
                        _timeItem.action?.Invoke();
                    }
                }

                if (_timeItem.DelayTime < 0 && !_timeItem.isLoop)
                {
                    _timeItem.IsUsed = true;
                    itemsToRemove.Add(_timeItem);
                }
            }
        }
        for (int i = 0; i < itemsToRemove.Count; i++)
        {
            RemoveInvoke(itemsToRemove[i]);
        }
    }

    public void Awake()
    {
        JEvent.defaultEvent.Register(gameMessage);
        TimeList.Clear();
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(_instance);
        }
    }

    private void OnDestroy()
    {
        JEvent.defaultEvent.Unregister(gameMessage);
        _instance = null;
    }

    public void BackToLogin(bool isChange = false)
    {
        GameM.Instance.SeverTimeOff = 0;
        TCPClientMgr.Instance.ClearIpPool();

        if (isChange)
        {
            PlayerPrefsExtend.DeleteKey(SDKCtr.SaveKey.WX_REFRESH_TOKEN_TIME.ToString());
            PlayerPrefsExtend.DeleteKey(SDKCtr.SaveKey.WX_OPEN_ID.ToString());
            PlayerPrefsExtend.DeleteKey(SDKCtr.SaveKey.WX_ACCESS_TOKEN.ToString());
            PlayerPrefsExtend.DeleteKey(SDKCtr.SaveKey.WX_REFRESH_TOKEN.ToString());
        }

        JEvent.defaultEvent.Post(new SenceChange()
        {
            scencePath = GameConst.LoginPath,
        });
    }

    public async Task SetRoleAsync(JobType jobType, SexType sexType,Action<GameObject> action)
    {
        string poolKey = "MainRole" + jobType + sexType;
        if (!PoolHelper.Instance.CheckPool(poolKey))
        {
            var baseObj = new JPrefab(GameConst.GetModelByJobAndSex(jobType, sexType), true);
            await baseObj.WaitForAsyncLoading();
            PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
        }
        GameObject _mainRole = PoolHelper.Instance.GetPoolObj(poolKey);
        _mainRole.gameObject.name = poolKey;
        action?.Invoke(_mainRole);
    }

    public async Task SetAsyncObj(string poolKey, string path, Action<GameObject> action,bool isUsedPath = false)
    {
        if (!PoolHelper.Instance.CheckPool(poolKey))
        {
            var baseObj = new JPrefab((isUsedPath ? path: GameConst.GetModelPath(path)), true);
            await baseObj.WaitForAsyncLoading();
            PoolHelper.Instance.AddPool(poolKey, baseObj.Instance);
        }
        GameObject _mainRole = PoolHelper.Instance.GetPoolObj(poolKey);
        _mainRole.gameObject.name = poolKey;
        action?.Invoke(_mainRole);
    }

    public void ClearObjToPool(Transform root)
    {
        if (root.childCount > 0)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                PoolHelper.Instance.RecycObj(root.GetChild(i).gameObject);
            }
        }
    }

    //延迟下一帧
    public void SetDeayFrame(Action acation)
    {
        CoroutineMgr.Instance.StartCoroutine(SetCoroutine(acation));
    }

    private System.Collections.IEnumerator SetCoroutine(Action action)
    {
        yield return new WaitForEndOfFrame();
        action?.Invoke();
    }

    //延迟几秒
    public Coroutine SetDeayTimeDo(Action acation,float time)
    {
        return CoroutineMgr.Instance.StartCoroutine(SetTimeCoroutine(acation, time));
    }

    private System.Collections.IEnumerator SetTimeCoroutine(Action action,float time)
    {
        yield return new WaitForSeconds(time);
        action?.Invoke();
    }

    public void StarGame(int buattleId)
    {
        CFGItem_Battle cfg = CFG_Battle.GetById(buattleId);
        if (cfg.Powerlimit > RoleM.Instance.TotalCombat)
        {
            UITool.ShowTips("当前战力不足");
            return;
        }

        if (cfg.Levellimit > RoleM.Instance.Level)
        {
            UITool.ShowTips("当前等级不足");
            return;
        }

        if (cfg.Copytype == (int)CFG_Room.RoomType.Boss && FightPlayM.Instance.GetInfo().boss_map.ChallengeTime <= 0 )
        {
            UITool.ShowTips("挑战次数不足");
            return;
        }
        TcpC.GetGameJoin(CFG_Room.GetUIDByRoomType((CFG_Room.RoomType)cfg.Copytype), buattleId);
    }

    void OnApplicationFocus(bool focus)
    {
        JEvent.defaultEvent.Post(new GameEvent.OnApplicationFocus { focus = focus });
    }

    void OnApplicationQuit()
    {
        HotUpdateScripts.LocalNet.LocalBootstrap.SaveOnPause();
    }

    void OnApplicationPause(bool pause)
    {
        JEvent.defaultEvent.Post(new GameEvent.OnApplicationPause { pause = pause });
        if (pause)
            HotUpdateScripts.LocalNet.LocalBootstrap.SaveOnPause();
    }

}

[Subscriber(ThreadMode.Main)]
public class GameHelpMessage 
{
    private async Task OnSenceChange(SenceChange data)
    {
        GameHelper.Instance.ActiveSenceName = data.scencePath;
        string sencePath = $"Assets/HotUpdateResources/{data.scencePath}.unity";
        if (data._senceType == 1)
        {
            sencePath = $"Assets/{data.scencePath}.unity";
        }

        await AssetMgr.LoadSceneAsync(sencePath);
        data.CallBack?.Invoke();
        if (data.scencePath == GameConst.NorFightPath || data.scencePath == GameConst.TurnBasedFightPath)
            AudioManager.Instance.PlayBgMusic(AudioKey.Sound.BGM_Fight);
        else if (data.scencePath == GameConst.HallPath)
            AudioManager.Instance.PlayBgMusic(AudioKey.Sound.BGM_Hall);
    }
}

/// <summary>
/// 游戏场景跳转
/// </summary>
public struct SenceChange
{
    public string scencePath;
    public Action CallBack;
    public int _senceType;
}
