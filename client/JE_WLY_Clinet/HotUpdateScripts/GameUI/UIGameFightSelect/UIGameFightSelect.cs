using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MetaFramework.UI;
using HotUpdateScripts.ProtoM;
using HotUpdateScripts.Game;
using JEngine.Helper;
using pb;
using HotUpdateScripts;
using JEngine.Core;
using static UIViewExpPool;

public class UIGameFightSelect : UIBase<UIModelGameFightSelect, UIViewGameFightSelect>
{
    public override UILayer GetLayer()
    {
        return UILayer.Normal;
    }
    public override void OnInit()
    {
        GetView().btn_Close.onClick.AddListener(() => {
            AudioManager.Instance.PlaySound(AudioKey.Sound.Button);
            UITool.Close<UIGameFightSelect>();
        });

        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            GetView().AttactOnes[i].obj.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnSelectAttactId(idx, 0);
            });

            GetView().AttactTwos[i].obj.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnSelectAttactId(idx, 5);
            });
        }

        GetView().tog_AttRole.onValueChanged.AddListener((isOn) => {
            GameRoomM.Instance.AttactToPlayer = isOn;
        });
    }

    private void OnSelectAttactId(int idx,int layIdx)
    {
        AudioManager.Instance.PlaySound(AudioKey.Sound.Button);

        if (!RoleM.Instance.CheakBuffId((int)CFG_BuffEffect.BufferID.XuanYun, (int)CFG_BuffEffect.BufferID.BingDong, (int)CFG_BuffEffect.BufferID.CaoFeng))
        {
            if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.All)
            {
                if (layIdx == 5)
                {
                    MainRole._MainRole.targetID = Roles[idx].role.uID;
                }
                else
                {
                    MainRole._MainRole.targetID = Monsters[idx]._monser.uID;
                }
            }
            else if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Monster)
            {
                MainRole._MainRole.targetID = Monsters[(idx + layIdx)]._monser.uID;
            }
            else if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Player)
            {
                MainRole._MainRole.targetID = Roles[(idx + layIdx)].role.uID;
            }
        }
        UITool.Close<UIGameFightSelect>();
    }

    private TimeItem RefreshJa;

    private void ClearTime()
    {
        if (RefreshJa != null && GameHelper.Instance != null)
        {
            GameHelper.Instance.AddInvoke(RefreshJa);
            RefreshJa = null;
        }
    }
    public override void OnEnter(params object[] args)
    {
        GetView().tog_AttRole.isOn = GameRoomM.Instance.AttactToPlayer;

        GetView().obj_Monster.SetActive(GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Monster);
        GetView().obj_Player.SetActive(GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Player);
        GetView().tog_AttRole.gameObject.SetActive(GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.All);

        OnSelectAttackShow();

        ClearTime();
        RefreshJa = GameHelper.Instance.AddInvoke(new TimeItem(-1, 3f, () => {
            if (isNullGo)
            {
                ClearTime();
                return;
            }
            OnSelectAttackShow();
        },true));
            
    }

    private List<FightController.RoleClass> Roles;
    private List<MonsterManager.monsterClass> Monsters;

    private int AttackDis = 9;
    private void OnSelectAttackShow()
    {
        if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.All)
        {
            Roles = FightController.Instance.GetCanDisRoles(AttackDis,5);
            for (int i = 0; i < 5; i++)
            {
                GetView().AttactTwos[i].obj.SetActive(i < Roles.Count);
                if (i < Roles.Count)
                {
                    GetView().AttactTwos[i].txt_Name.text = Roles[i].role.roleName;
                    GetView().AttactTwos[i].txt_Lv.text = Roles[i].role.level.ToString();

                    GetView().AttactTwos[i].txt_Hp.text = GameUtil.Instance.GetPercentStr(Roles[i].role.hp / (float)Roles[i].role.maxHp);
                    GetView().AttactTwos[i].img_Hp.value = Roles[i].role.hp / (float)Roles[i].role.maxHp;

                    if (Roles[i].role._ObjectType == ObjectType.PLAYER)
                        ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.RoleHeadAtlas, "img_" + Roles[i].role.job + Roles[i].role.sex, GetView().AttactTwos[i].img_Head);
                    else
                    {
                        GameUtil.Instance.SetItemIcon(CFG_Partner.GetById(Roles[i].role.CfgID).ItemID, GetView().AttactTwos[i].img_Head);
                    }
                }
            }

            Monsters = MonsterManager.Instance.getCanAttackMonsters(AttackDis,5);
            for (int i = 0; i < 5; i++)
            {
                GetView().AttactOnes[i].obj.SetActive(i < Monsters.Count);
                if (i < Monsters.Count)
                {
                    GetView().AttactOnes[i].txt_Name.text = CFG_Monster.GetMonsterNameById(Monsters[i]._monser.cfgId);
                    GetView().AttactOnes[i].txt_Lv.text = CFG_Monster.GetMonsterNameByLv(Monsters[i]._monser.cfgId);

                    GetView().AttactOnes[i].txt_Hp.text = GameUtil.Instance.GetPercentStr(Monsters[i]._monser.hp / (float)Monsters[i]._monser.maxHp);
                    GetView().AttactOnes[i].img_Hp.value = Monsters[i]._monser.hp / (float)Monsters[i]._monser.maxHp;
                    
                    GameUtil.Instance.SetMonsterIcon(Monsters[i]._monser.cfgId, GetView().AttactOnes[i].img_Head);
                }
            }
        }
        else if (GameRoomM.Instance.GameAttack == GameRoomM.AttackClass.Monster)
        {
            Monsters = MonsterManager.Instance.getCanAttackMonsters(AttackDis,10);

            for (int i = 0; i < 5; i++)
            {
                GetView().AttactOnes[i].obj.SetActive(i < Monsters.Count);
                if (i < Monsters.Count)
                {
                    GetView().AttactOnes[i].txt_Name.text = CFG_Monster.GetMonsterNameById(Monsters[i]._monser.cfgId);
                    GetView().AttactOnes[i].txt_Lv.text = CFG_Monster.GetMonsterNameByLv(Monsters[i]._monser.cfgId);

                    bool ShowHp = CFG_Monster.GetById(Monsters[i]._monser.cfgId).Isdisplaybar != 0;
                    GetView().AttactOnes[i].img_Hp.gameObject.SetActive(ShowHp);
                    GetView().AttactOnes[i].txt_Hp.text = ShowHp ? GameUtil.Instance.GetPercentStr(Monsters[i]._monser.hp / (float)Monsters[i]._monser.maxHp): "";
                    if (ShowHp)
                        GetView().AttactOnes[i].img_Hp.value = Monsters[i]._monser.hp / (float)Monsters[i]._monser.maxHp;

                    GameUtil.Instance.SetMonsterIcon(Monsters[i]._monser.cfgId, GetView().AttactOnes[i].img_Head);
                }
            }
            for (int j = 0; j < 5; j++)
            {
                int i = j + 5;
                GetView().AttactTwos[j].obj.SetActive(i < Monsters.Count);
                if (i < Monsters.Count)
                {
                    GetView().AttactTwos[j].txt_Name.text = CFG_Monster.GetMonsterNameById(Monsters[i]._monser.cfgId);
                    GetView().AttactTwos[j].txt_Lv.text = CFG_Monster.GetMonsterNameByLv(Monsters[i]._monser.cfgId);

                    bool ShowHp = CFG_Monster.GetById(Monsters[i]._monser.cfgId).Isdisplaybar != 0;
                    GetView().AttactTwos[j].img_Hp.gameObject.SetActive(ShowHp);
                    GetView().AttactTwos[j].txt_Hp.text = ShowHp ? GameUtil.Instance.GetPercentStr(Monsters[i]._monser.hp / (float)Monsters[i]._monser.maxHp) : "";
                    if (ShowHp)
                        GetView().AttactTwos[j].img_Hp.value = Monsters[i]._monser.hp / (float)Monsters[i]._monser.maxHp;

                    GameUtil.Instance.SetMonsterIcon(Monsters[i]._monser.cfgId, GetView().AttactTwos[j].img_Head);
                }
            }
        }

        else 
        {
            Roles = FightController.Instance.GetCanDisRoles(AttackDis,10);
            for (int i = 0; i < 5; i++)
            {
                GetView().AttactOnes[i].obj.SetActive(i < Roles.Count);
                if (i < Roles.Count)
                {
                    GetView().AttactOnes[i].txt_Lv.text = Roles[i].role.level.ToString();

                    GetView().AttactOnes[i].txt_Hp.text = GameUtil.Instance.GetPercentStr(Roles[i].role.hp / (float)Roles[i].role.maxHp);
                    GetView().AttactOnes[i].img_Hp.value = Roles[i].role.hp / (float)Roles[i].role.maxHp;

                    if (Roles[i].role._ObjectType == ObjectType.PLAYER)
                    {
                        GetView().AttactOnes[i].txt_Name.text = GameUtil.Instance.GetStrByQuality(7, Roles[i].role.roleName);
                        ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.RoleHeadAtlas, "img_" + (int)Roles[i].role.job + (int)Roles[i].role.sex, GetView().AttactOnes[i].img_Head);
                    }
                    else
                    {
                        GetView().AttactOnes[i].txt_Name.text = Roles[i].role.roleName + GameUtil.Instance.GetStrByQuality(7, GameRoomM.Instance.getPlayerInfoByID(Roles[i].role.OwnerUid).Nickname);
                        GameUtil.Instance.SetItemIcon(CFG_Partner.GetById(Roles[i].role.CfgID).ItemID, GetView().AttactOnes[i].img_Head);
                    }

                }
            }
            for (int j = 0; j < 5; j++)
            {
                int i = j + 5;
                GetView().AttactTwos[j].obj.SetActive(i < Roles.Count);
                if (i < Roles.Count)
                {
                    GetView().AttactTwos[j].txt_Name.text = Roles[i].role.roleName;
                    GetView().AttactTwos[j].txt_Lv.text = Roles[i].role.level.ToString();

                    GetView().AttactTwos[j].txt_Hp.text = GameUtil.Instance.GetPercentStr(Roles[i].role.hp / (float)Roles[i].role.maxHp);
                    GetView().AttactTwos[j].img_Hp.value = Roles[i].role.hp / (float)Roles[i].role.maxHp;

                    if (Roles[i].role._ObjectType == ObjectType.PLAYER)
                    {
                        GetView().AttactTwos[i].txt_Name.text = GameUtil.Instance.GetStrByQuality(7, Roles[i].role.roleName);
                        ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.RoleHeadAtlas, "img_" + Roles[i].role.job + Roles[i].role.sex, GetView().AttactTwos[i].img_Head);
                    }
                    else
                    {
                        GetView().AttactTwos[i].txt_Name.text = Roles[i].role.roleName + GameUtil.Instance.GetStrByQuality(7, GameRoomM.Instance.getPlayerInfoByID(Roles[i].role.OwnerUid).Nickname);
                        GameUtil.Instance.SetItemIcon(CFG_Partner.GetById(Roles[i].role.CfgID).ItemID, GetView().AttactTwos[i].img_Head);
                    }
                }
            }
        }
    }

    public override void OnExit()
    {
        ClearTime();
    }

    public override void OnDestroy()
    {
        ClearTime();
    }
}