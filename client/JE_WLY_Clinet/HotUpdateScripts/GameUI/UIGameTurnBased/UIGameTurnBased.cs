using HotUpdateScripts.Game;

using JEngine.Event;

using MetaFramework.UI;

using static GameEvent;

public class UIGameTurnBased : UIBase<UIModelGameTurnBased, UIViewGameTurnBased>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            ExitFighting();
        });

        GetView().btn_JumpGame.onClick.AddListener(() =>
        {
            TurnBasedFightCtrl.Instance.JumpGame();
            GetView().btn_JumpGame.gameObject.SetActive(false);
        });
    }

    private UICom_TopRes TopUI = null;
    private int timeSeconds = 10;

    public override void OnLayerChange()
    {
        if (TopUI == null)
        {
            UITool.LoadUII<UICom_TopRes>(GetView().Root, (ui) =>
            {
                TopUI = ui as UICom_TopRes;
                TopUI.OnSetLayer(_canvasOrder);
            });
        }
    }

    public override void OnEnter(params object[] args)
    {
        JEvent.defaultEvent.Register(this);
        GetView().txt_Rount.text = "";
        GetView().txt_Time.text = "";

        GetView().btn_JumpGame.gameObject.SetActive(false);
        GetView().btn_JumpGameDisabled.gameObject.SetActive(false);
        GetView().root_JumpTime.SetActive(false);
    }

    [Subscriber(ThreadMode.Main)]
    private void Event_ShowRound(PVP_TurnBasedRoundShow _e)
    {
        if (isNullGo)
            return;

        ShowRound(_e.currentRound, _e.totalRound);
    }

    private void ShowRound(int currentRound, int totalRound)
    {
        GetView().txt_Rount.text = $"回合数：{currentRound}/{CFG_XMLConfig.xmlConfig.GVGInfo.MaxRound}";
    }

    private void ExitFighting()
    {
        string showStr = "确认退出游戏？";

        UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
        {
            ShowStr = showStr,
            YesFun = () =>
            {
                TurnBasedFightCtrl.Instance.JumpGame();
                TurnBasedFightCtrl.Instance.ChangeState(TurnBasedFightCtrl.BattleState.Over);
            },
            BtnYes = "直接退出",
            BtnNo = "继续游戏",
        });
    }

    // 倒计时结束后可跳过战斗
    TimeItem jumpGameTimeItem;

    [Subscriber(ThreadMode.Main)]
    private void ShowJumpGameTime(GameEvent.PVP_JumpGameCountdown _e)
    {
        if (isNullGo) return;

        GetView().btn_JumpGameDisabled.gameObject.SetActive(true);
        GetView().root_JumpTime.SetActive(true);

        jumpGameTimeItem = GameHelper.Instance.AddInvoke(new TimeItem(timeSeconds + 1, 1, () =>
        {
            if (isNullGo) return;
            GetView().txt_JumpTime.text = $"{timeSeconds}秒后可跳过战斗";
            if (timeSeconds == 0)
            {
                GetView().txt_JumpTime.text = "";
                GetView().root_JumpTime.SetActive(false);
                GetView().btn_JumpGameDisabled.gameObject.SetActive(false);
                GetView().btn_JumpGame.gameObject.SetActive(true);
            }
            timeSeconds--;
        }));
    }

    public void ClearTime()
    {
        if(jumpGameTimeItem != null)
        {
            GameHelper.Instance.RemoveInvoke(jumpGameTimeItem);
            jumpGameTimeItem = null;
        }
    }

    public override void OnExit()
    {
        JEvent.defaultEvent.Unregister(this);
        ClearTime();
    }

    public override void OnDestroy()
    {
        ClearTime();
    }
}