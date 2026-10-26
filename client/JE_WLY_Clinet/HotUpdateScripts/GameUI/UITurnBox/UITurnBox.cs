using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using JEngine.UI;
using JEngine.Helper;
using JEngine.Event;
using DG.Tweening;
using JEngine.Core;

public class UITurnBox : UIBase<UIModelTurnBox, UIViewTurnBox>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        JEvent.defaultEvent.Register(this);
        GetView().btn_Close.onClick.AddListener(()=> {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UITurnBox>();
        });

        GetView().btn_Help.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UIComRule>(null, 14);
        });

        GetView().btn_Rule.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Open<UITurnBoxRule>();
        });

        GetView().tog_anim.onValueChanged.AddListener((isOn) =>
        {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            PlayerPrefsExtend.SetInt("TreasureBoxAnim", !isOn ? 0 : 1);
        });

        GetView().btn_One.onClick.AddListener(() => {
            if (isPlayAnim)
                return;

            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
           

            if (_CanCount <= 0)
            {
                UITool.ShowTips("今日次数已用完，明天再来吧");
                return;            
            }

            if (!GameUtil.Instance.ComCoinToCheck(SelectCFG.Consume[0].id, SelectCFG.Consume[0].num, ()=> { UITool.Close<UITurnBox>(); }))
            {
                return;
            }
            OnOneOrTenClick(1);
        });

        GetView().btn_Ten.onClick.AddListener(() => {
            if (isPlayAnim)
                return;
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            if (_CanCount < 10)
            {
                UITool.ShowTips("次数不足");
                return;
            }
            if (!GameUtil.Instance.ComCoinToCheck(SelectCFG.Consume[1].id, SelectCFG.Consume[1].num, () => { UITool.Close<UITurnBox>(); }))
            {
                return;
            }
            OnOneOrTenClick(2);
        });
    }

    private TimeItem Ja;
    private void OnOneOrTenClick(int _type)
    {
        if (isPlayAnim)
            return;

        HttpC.SetTreasureBox(_type, (r) =>
        {
            if (isNullGo)
                return;
            int Count = 0;
            float oneTime = GetView().tog_anim.isOn ? 1.2f : 3.5f;
            Ja = GameHelper.Instance.AddInvoke(new TimeItem(r.position.Length * oneTime, oneTime, () => {
                if (isNullGo) return;
                OnAnimShow(r.position[Count], !GetView().tog_anim.isOn);
                Count++;
            })); 
            GetInfo();
        });
    }

    private int _CanCount = 0;
    private void GetInfo()
    {
        HttpC.GetTreasureBox((_r) => {

            if (isNullGo)
                return;

            _CanCount = _r.can_play;
            GetView().txt_Count.text = $"今日剩余抽奖次数:<color=#{(_CanCount == 0 ? "DA4E38":"80CBCF")}>{_CanCount}</color>";
            if (_CanCount == 0)
            {
                GetView().txt_pro.text = "";
            }
            else
                GetView().txt_pro.text = _r.need_num == 1 ? $"本次必中粉色及以上大奖" 
                : $"<color=#4BEA52>{_r.need_num - 1}</color>次后必中粉色及以上大奖";

            OnResShow();
        });
    }

    private void ClearJa()
    {
        if (Ja != null)
        {
            GameHelper.Instance.RemoveInvoke(Ja);
            Ja = null;
        }
    }

    int allCount = 12;
    bool isPlayAnim = false;

    private void OnAnimShow(int endPos,bool isAnim)
    {
        isPlayAnim = true;
        System.Action ACEnd = () => {
            GetView().root_Result.gameObject.SetActive(true);
            GetView().root_Result.localScale = Vector3.one;

            GameUtil.Instance.SetItemIcon(SelectCFG.CommonPool[endPos-1].id, JUIExtensions.img(GetView().root_Result.Find("icon")), JUIExtensions.img(GetView().root_Result));
            JUIExtensions.txt(GetView().root_Result.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(SelectCFG.CommonPool[endPos - 1].num);

            GetView().root_Result.DOScale(Vector3.zero, 0.2f).SetDelay(1f).OnComplete(() => {
                GetView().root_Result.gameObject.SetActive(false);
                GetView().root_loopEff.gameObject.SetActive(false);
                isPlayAnim = false;
            });
        };

        if (!isAnim)
        {
            ACEnd();
            return;
        }
        int starIdx = Random.Range(0, allCount);
        var Poss = new Vector3[allCount - starIdx + allCount + endPos];
        int posIdx = 0;
        for (int i = starIdx; i < allCount; i++)
        {
            Poss[posIdx] = GetView().root_loop.Find("Loop_" + i).transform.position;
            posIdx++;
        }

        for (int i = 0; i < allCount; i++)
        {
            Poss[posIdx] = GetView().root_loop.Find("Loop_" + i).transform.position;
            posIdx++;
        }

        for (int i = 0; i < endPos; i++)
        {
            Poss[posIdx] = GetView().root_loop.Find("Loop_" + i).transform.position;
            posIdx++;
        }

        GetView().root_loopEff.transform.position = Poss[0];

        GetView().root_loopEff.gameObject.SetActive(true);



        GetView().root_loopEff.DOPath(Poss, 2).SetEase(Ease.InOutExpo).OnComplete(()=> { ACEnd(); });
    }

    private CFGItem_TreasureBox SelectCFG;
    public override void OnEnter(params object[] args)
    {
        SelectCFG = CFG_TreasureBox.GetBox(RoleM.Instance.Level);

        foreach (var item in SelectCFG.CommonPool)
        {
            var go = GetView().root_loop.Find("Loop_" + (item.position - 1) + "/item");
            GameUtil.Instance.SetItemIcon(item.id, JUIExtensions.img(go.transform.Find("icon")), JUIExtensions.img(go));
            JUIExtensions.txt(go.transform.Find("txt_num")).text = GameUtil.Instance.FormatNumWithUnit(item.num);

            JUIExtensions.btn(go).onClick.RemoveAllListeners();
            JUIExtensions.btn(go).onClick.AddListener(() => {
                AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
                UITool.Open<UIItemShow>(null, item.id);
            });
        }

        GameUtil.Instance.SetItemIcon(SelectCFG.Consume[0].id, JUIExtensions.img(GetView().btn_OneTip.Find("icon")));
        GameUtil.Instance.SetItemIcon(SelectCFG.Consume[1].id, JUIExtensions.img(GetView().btn_TenTip.Find("icon")));

        GetView().tog_anim.isOn = PlayerPrefsExtend.GetInt("TreasureBoxAnim") == 1;
        GetInfo();
    }

    private void OnResShow()
    {
        JUIExtensions.txt(GetView().btn_OneTip.Find("txt")).text =
            GameUtil.Instance.ComCoinToCheck(SelectCFG.Consume[0].id, SelectCFG.Consume[0].num, null, false) ? GameUtil.Instance.FormatNumWithUnit(SelectCFG.Consume[0].num) + "/" + GameUtil.Instance.FormatNumWithUnit(BagM.Instance.GetNumByCfgID(SelectCFG.Consume[0].id))
            : "<color=#DA4E38>" + GameUtil.Instance.FormatNumWithUnit(SelectCFG.Consume[0].num) + "/" + GameUtil.Instance.FormatNumWithUnit(BagM.Instance.GetNumByCfgID(SelectCFG.Consume[0].id)) + "</color>";

        GetView().btn_One.transform.Find("dot").gameObject.SetActive(_CanCount >= 1 && 
            GameUtil.Instance.ComCoinToCheck(SelectCFG.Consume[0].id, SelectCFG.Consume[0].num, null, false));

        JUIExtensions.txt(GetView().btn_TenTip.Find("txt")).text =
                GameUtil.Instance.ComCoinToCheck(SelectCFG.Consume[1].id, SelectCFG.Consume[1].num, null, false) ? GameUtil.Instance.FormatNumWithUnit(SelectCFG.Consume[1].num) + "/" + GameUtil.Instance.FormatNumWithUnit(BagM.Instance.GetNumByCfgID(SelectCFG.Consume[1].id))
                : "<color=#DA4E38>" + GameUtil.Instance.FormatNumWithUnit(SelectCFG.Consume[1].num) + "/" + GameUtil.Instance.FormatNumWithUnit(BagM.Instance.GetNumByCfgID(SelectCFG.Consume[1].id)) + "</color>";

        GetView().btn_Ten.transform.Find("dot").gameObject.SetActive(_CanCount >= 10 &&
          GameUtil.Instance.ComCoinToCheck(SelectCFG.Consume[1].id, SelectCFG.Consume[1].num, null, false));
    }

    [Subscriber(ThreadMode.Main)]
    private void OnSync_UserBase(GameEvent.Sync_UserStatsPush _e)
    {
        if (uiGo == null || uiGo.Equals(null))
            return;
        OnResShow();
    }

    public override void OnExit()
    {
        ClearJa();
        isPlayAnim = false;
    }

    public override void OnDestroy()
    {
        ClearJa();
        JEvent.defaultEvent.Unregister(this);
    }
}