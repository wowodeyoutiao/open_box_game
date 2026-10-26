using DG.Tweening;

using HotUpdateScripts.Game;
using HotUpdateScripts.ProtoM;

using JEngine.Core;
using JEngine.Event;
using JEngine.Helper;

using MetaFramework.UI;

using pb;

using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

public class UIGuide : UIBase<UIModelGuide, UIViewGuide>
{
    public class GuideData
    {
        public int ID;
        public Vector3 ButtonPos;
        public Action callBack;
    }

    public override UILayer GetLayer()
    {
        return UILayer.Guide;
    }

    public override void OnInit()
    {
        GetView().btn_Jump.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            OnJumpGuide();
        });

        GetView().btn_Focus.onClick.AddListener(OnClickFocus);

        GetView()._ETCJoystick.onMove.AddListener(OnJoyMove);
        GetView()._ETCJoystick.onMoveEnd.AddListener(OnJoyMoveEnd);
    }

    private GuideData guideData;

    /// <summary>
    /// 对话框位置
    /// </summary>
    private Dictionary<int, float> tipPosDic = new Dictionary<int, float>()
    {
        // 开箱流程
        [1] = -220,
        [2] = -280,
        [3] = -200,
        // 伙伴上阵流程
        [4] = 320,
        [5] = -250,
        [6] = 300,
        [7] = 300,
        [8] = -200,
        [9] = -200,
        [10] = -250,
        [11] = 300,
        [12] = 300,
        [13] = 300,
        [14] = 300,
    };

    /// <summary>
    /// 摇杆位置
    /// </summary>
    private Dictionary<int, Vector3> joyPosDic = new Dictionary<int, Vector3>()
    {
        [0] = new Vector3(0, -200, 0),
    };

    private Dictionary<int, string> guideStepName = new Dictionary<int, string>()
    {
        [1] = "点击宝箱开箱",
        [2] = "点击开出的装备",
        [3] = "点击穿戴装备",
        [4] = "进入角色界面",
        [5] = "点击伙伴槽位",
        [6] = "进入伙伴召唤界面",
        [7] = "点击召唤按钮",
        [8] = "点击确定",
        [9] = "进入上阵界面",
        [10] = "选择上阵的伙伴",
        [11] = "点击上阵按钮",
        [12] = "返回大厅界面",
        [13] = "打开宝箱设置界面",
        [14] = "点击装备设置区域",
        
    };

    public override void OnEnter(params object[] args)
    {
        if (args == null || args.Length == 0)
            return;

        guideData = args[0] as GuideData;
        StartGuide();
    }

    public void StartGuide(GuideData guideData)
    {
        this.guideData = guideData;
        StartGuide();
    }

    public void StartGuide()
    {
        RoleM.Instance.GuideId = guideData.ID;
        RoleM.Instance.isGuiding = true;
        bool isShowJoy = CFG_Guidance.IsNeedShowJoy(guideData.ID);
        GetView().obj_Touch.SetActive(isShowJoy);
        GetView().root_Focus.gameObject.SetActive(!isShowJoy);
        SetTipPosition();
        GetView().txt_Des.text = "";
        ShowUI(true);
        string describe = CFG_Guidance.GetById(guideData.ID).describe;
        DOTween.To(() => "", (v) => GetView().txt_Des.text = v, describe, 40).SetSpeedBased().SetEase(Ease.Linear);
        //Debug.Log($"<color=yellow>StartGuide() id = </color> {guideData.ID}");

    }

    private void OnClickFocus()
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

        SetTipPosition();
        int guideType = CFG_Guidance.GetType(guideData.ID);
        //Debug.Log($"<color=cyan>OnClickGuide() id = </color> {guideData.ID}");
        TDSendGuideStep(RoleM.Instance.GuideId, guideStepName[RoleM.Instance.GuideId]);

        if (RoleM.Instance.GuideId < CFG_Guidance.GetLastSetpIdByType((CFG_Guidance.GuideType)guideType))
        {
            if (NeedContinue())
            {
                SaveProgress();
                RoleM.Instance.GuideId++;
                ShowUI(false);
                guideData.callBack?.Invoke();
            }
        }
        else
        {
            guideData.callBack?.Invoke();
            OnJumpGuide();
        }

    }

    private bool NeedContinue()
    {
        if (RoleM.Instance.GuideId == 1)
        {
            return RoleM.Instance.TreasuerBox > 0;
        }
        else
        {
            return true;
        }
    }

    private void ShowUI(bool isShow)
    {
        //GetView().obj_bgMask.SetActive(isShow);
        GetView().obj_Center.SetActive(isShow);
    }

    private void OnJumpGuide()
    {
        RoleM.Instance.isGuiding = false;
        SaveProgress();
        int guideType = CFG_Guidance.GetType(guideData.ID);

        // 恢复显示开箱结果
        if((CFG_Guidance.GuideType)guideType == CFG_Guidance.GuideType.Partner)
        {
            JEvent.defaultEvent.Post(new GameEvent.Refresh_ShowBoxUI());
        }

        TDAnalyticsUtil.Flush();
        UITool.Close<UIGuide>();
    }

    void SaveProgress()
    {
        int guideType = CFG_Guidance.GetType(RoleM.Instance.GuideId);
        if (RoleM.Instance.GuideTypes == null)
        {
            RoleM.Instance.GuideTypes = new List<int>
            {
                guideType
            };
        }
        else if (!RoleM.Instance.GuideTypes.Contains(guideType))
        {
            RoleM.Instance.GuideTypes.Add(guideType);
        }
        RoleM.Instance.SaveGuide();
    }

    public override void OnLayerChange()
    {
        GetView().canvas_Joy.sortingOrder = _canvasOrder + 1;
    }

    private void SetTipPosition()
    {
        var btnPos = guideData.ButtonPos;
        GetView().root_Focus.localPosition = guideData.ButtonPos;
        btnPos.x = 0;
        btnPos.y += tipPosDic[RoleM.Instance.GuideId];
        GetView().root_Des.localPosition = btnPos;
       
    }

    private void OnJoyMove(Vector2 moveVec)
    {
        Direction _dir = GetDirection(moveVec);
        MainRole._MainRole._joyDir = _dir;
    }

    private void OnJoyMoveEnd()
    {
        MainRole._MainRole._joyDir = Direction.NONE;
    }

    private Direction GetDirection(Vector2 position)
    {
        if (position.x > 0.1)
        {
            if (position.y > 0.1) return Direction.UP_RIGHT;
            if (position.y < -0.1) return Direction.DOWN_RIGHT;
            return Direction.RIGHT;
        }
        else if (position.x < -0.1)
        {
            if (position.y > 0.1) return Direction.UP_LEFT;
            if (position.y < -0.1) return Direction.DOWN_LEFT;
            return Direction.LEFT;
        }
        return position.y > 0 ? Direction.UP : Direction.DOWN;
    }

    private void TDSendGuideStep(int guideStep, string guideName)
    {
        //Dictionary<string, object> properties = new Dictionary<string, object>();
        //properties.Add("guide_step", guideStep);
        //properties.Add("guide_name", guideName);
        //TDAnalyticsUtil.Track("guide_completed", properties);

        HttpC.ShuShuSendGuide(new HTTP_WindowShushuCompletedRequest
        {
            name = "guide_completed",
            guide_name = guideName,
            guide_step = guideStep,
        });
    }

    public override void OnExit()
    {
        guideData = null;
    }

    public override void OnDestroy()
    {
        guideData = null;
    }
}