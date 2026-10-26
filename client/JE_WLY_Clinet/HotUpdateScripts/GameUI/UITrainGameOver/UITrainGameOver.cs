using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.Game;
using JEngine.Helper;
using JEngine.UI;
using JEngine.Core;
using HotUpdateScripts.ProtoM;

public class UITrainGameOver : UIBase<UIModelTrainGameOver, UIViewTrainGameOver>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }

    public override void OnInit()
    {
        GetView().btn_back.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            ClearJa();
            OnBack();
        });
    }

    private void OnBack()
    {
        FightController.Instance.OnExitGame();
    }
    public override void OnEnter(params object[] args)
    {
        bool isWin = (bool)args[0];
        GetView().obj_Lose.SetActive(!isWin);
        GetView().obj_Win.SetActive(isWin);
        GetView().root_Awards.gameObject.SetActive(isWin);

        if (isWin) 
        {
            var Awrads = CFG_EndlessTrials.GetByLayer((int)args[1]).Settlement;
            for (int i = 0; i < Awrads.Length; i++)
            {
                var go = GetView().root_Awards.GetChild(i);
                var cfg_item = Awrads[i];
                GameUtil.Instance.SetItemIcon(cfg_item.id, JUIExtensions.img(go.transform.Find("icon")), JUIExtensions.img(go.transform));
                JUIExtensions.txt(go.transform.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(cfg_item.num);

                //JUIExtensions.btn(go).onClick.AddListener(() => {
                //    AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                //    UITool.Open<UIItemShow>(null, cfg_item.id);
                //});
            }
        }
        

        int Count = 5;
        Ja = GameHelper.Instance.AddInvoke(new TimeItem(Count,1, () => {
            GetView().txt_Time.text = Count + "秒后将自动退出";
            Count--;
            if (Count <= 0)
                OnBack();
        }));
    }

    private TimeItem Ja;
    private void ClearJa()
    {
        if (Ja != null)
        {
            GameHelper.Instance.RemoveInvoke(Ja);
            Ja = null;
        }
    }

    public override void OnExit()
    {

    }

    public override void OnDestroy()
    {
        ClearJa();
    }
}