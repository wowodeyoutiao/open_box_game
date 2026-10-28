using JEngine.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

public class CFGItem_Skill : CFGItemBase
{
    public int UUID;
    /// <summary>
    /// 技能ID
    /// </summary>
    public int SkillID;

    /// <summary>
    /// 技能等级
    /// </summary>
    public int SkillLevel;

    /// <summary>
    /// 职业
    /// </summary>
    public int SkillProfession;

    public override long GetIdx()
    {
        return UUID;
    }

    /// <summary>
    /// 技能名称
    /// </summary>
    public string Name; 
    public decimal RoleLevellimit; // 角色等级限制
    public Consume_Item[] PromoteConsume; // 升级消耗，JSON格式
    public decimal CanupperLevel; // 可进阶等级
    public Consume_Item[] UpperlevelConsume; // 进阶消耗，JSON格式
    public string SkillDes; // 升级技能描述
    public string UpperlevelDes; // 进阶效果描述
    public decimal CoolDown; // 技能冷却（毫秒）
    public decimal Delay; // 延迟（毫秒）
    public decimal MaxDistance; // 最大距离
    public decimal Radius; // 半径

    public decimal SkillIcon;
    public decimal ActiveorPassive; // 0主动 1被动

    //技能分类
    public int Skilltype;

    public int[] TriggerBuff;

    //技能目标释放
    public decimal Needaim;

    public decimal Needskill;

    public decimal Order;

    public struct Consume_Item
    {
        public int id;
        public int num;
    }

    public int PreMark;

    /// <summary>
    /// 技能伤害加成。格式 [{属性id: 数值}, ...]，对应原服 skillCfg.Effect。
    /// 属性 id 走 pb.Attribute，百分比类以 10000 为基数（见原服 BaseRate）。
    /// </summary>
    public Dictionary<string, int>[] SkillEffect;

    /// <summary>
    /// 特殊效果，对应原服 skillCfg.SpecialEffect。大部分技能为 null。
    /// </summary>
    public SpecialEffectItem SpecialEffect;

    /// <summary>
    /// 技能进阶效果，对应原服 global.SkillUpperlevel。
    /// 配置里同一 CanupperLevel 段内只有「该段第一级」有值（如 Lv5/Lv10/Lv15…），
    /// 其余行是 null，用 GetUpperlevelSetting 段内取第一个非空的。
    /// </summary>
    public UpperlevelSettingItem UpperlevelSetting;

    public class UpperlevelSettingItem
    {
        /// <summary>伤害百分比提升（万分比）。</summary>
        public int DamagePer;
        /// <summary>持续时间固定值增加（毫秒）。</summary>
        public int DurationCon;
        /// <summary>技能范围增加（格子）。</summary>
        public int Distance;
        /// <summary>减控百分比。</summary>
        public int Control;
        /// <summary>触发几率。</summary>
        public int Trigger;
    }

    public class SpecialEffectItem
    {
        /// <summary>斩杀比例（万分比）。目标生命百分比低于该值时直接斩杀。</summary>
        public int Slay;
        /// <summary>伤害转移/吸血比例（万分比）。</summary>
        public int DamTransfer;
        /// <summary>多段攻击次数。</summary>
        public int MulAttact;
        public int BackDistence;
        public int FlyDistence;
    }
}

/// <summary>
/// Skill 配置表
/// </summary>
public class CFG_Skill : CFGTableBase<CFGItem_Skill>
{

    private static CFG_Skill _AllCFG;
    private static CFG_Skill AllCFG
    {
        get
        {
            if (_AllCFG == null)
            {
                _AllCFG = ExcelDataManager.Instance.GetExcelTable<CFG_Skill, CFGItem_Skill>();
            }
            return _AllCFG;
        }
    }
    public static CFG_Skill Get()
    {
        return AllCFG;
    }

    public static CFGItem_Skill GetById(int id)
    {
        return AllCFG.DicDatas[id];
    }

    //职业 - 技能ID - 技能等级 (不包含普攻)
    private static Dictionary<int, Dictionary<int, Dictionary<int, CFGItem_Skill>>> _JobToSlillDic;
    private static Dictionary<int, Dictionary<int, Dictionary<int, CFGItem_Skill>>> JobToSlillDic
    {   
        get
        {
            if (_JobToSlillDic == null)
            {
                _JobToSlillDic = new Dictionary<int, Dictionary<int, Dictionary<int, CFGItem_Skill>>>();

                foreach (var item in Get().DicDatas.Values)
                {
                    if (item.SkillLevel != 0)
                    {
                        if (!_JobToSlillDic.ContainsKey(item.SkillProfession))
                        {
                            _JobToSlillDic.Add(item.SkillProfession, new Dictionary<int, Dictionary<int, CFGItem_Skill>>());
                        }
                        if (!_JobToSlillDic[item.SkillProfession].ContainsKey(item.SkillID))
                        {
                            _JobToSlillDic[item.SkillProfession].Add(item.SkillID, new Dictionary<int, CFGItem_Skill>());
                        }
                        _JobToSlillDic[item.SkillProfession][item.SkillID].Add(item.SkillLevel, item);
                    }
                }
            }
            return _JobToSlillDic;
        }
    }

    public static Dictionary<int, Dictionary<int,CFGItem_Skill>> GetSkillsByJob(int job)
    {
        return JobToSlillDic[job];
    }

    private static Dictionary<int, Dictionary<int,CFGItem_Skill>> SkillAllListDic = new Dictionary<int, Dictionary<int, CFGItem_Skill>>();

    private static Dictionary<int, CFGItem_Skill> GetOneSkillListBySkillID(int SkillId)
    {
        if (SkillAllListDic.Count == 0)
        {
            var _List = Get().DicList;
            for (int i = 0; i < _List.Count; i++)
            {
                if (_List[i].Skilltype != (int)SkillType.Normall)
                {
                    if (!SkillAllListDic.ContainsKey(_List[i].SkillID))
                    {
                        SkillAllListDic.Add(_List[i].SkillID, new Dictionary<int, CFGItem_Skill>());
                    }

                    SkillAllListDic[_List[i].SkillID].Add(_List[i].SkillLevel, _List[i]);
                }
            }
        }
        return SkillAllListDic[SkillId];
    }

    public static CFGItem_Skill GetNextLvByUid(int cfgID)
    {
        CFGItem_Skill baseSkill = GetById(cfgID);
        int nextLv = baseSkill.SkillLevel + 1;
        CFGItem_Skill nextSkill = baseSkill;

        var _Dic = GetOneSkillListBySkillID(nextSkill.SkillID);
        if (_Dic.ContainsKey(nextLv))
        {
            return _Dic[nextLv];
        }
        return nextSkill;
    }

    //升阶信息
    private static Dictionary<int, Dictionary<int, CFGItem_Skill>> _UpperAdvanceList;
    private static Dictionary<int, Dictionary<int, CFGItem_Skill>> UpperAdvanceList { get 
        {
            if (_UpperAdvanceList == null)
            {
                _UpperAdvanceList = new Dictionary<int, Dictionary<int, CFGItem_Skill>>();
                var _List = Get().DicList;
                for (int i = 0; i < _List.Count; i++)
                {
                    if (_List[i].UpperlevelConsume != null)
                    {
                        if (!_UpperAdvanceList.ContainsKey(_List[i].SkillID))
                        {
                            _UpperAdvanceList.Add(_List[i].SkillID, new Dictionary<int, CFGItem_Skill>());
                        }
                        _UpperAdvanceList[_List[i].SkillID].Add((int)_List[i].CanupperLevel, _List[i]);
                    }
                }
            }
            return _UpperAdvanceList;
        }
    }

    /// <summary>
    /// 取指定进阶等级的效果，对齐原服 global.GetUpperlevelSettingCfg：
    ///   id := uid / 100（= SkillID）
    ///   在同 SkillID 的配置里找 CanupperLevel == advanced 且 UpperlevelSetting 非空的那条
    /// 原服加载配置时做了「段内向上填充」（lastUpperlevelSetting），
    /// 所以直接取段内第一条非空的即可。
    /// </summary>
    public static CFGItem_Skill.UpperlevelSettingItem GetUpperlevelSetting(int skillUuid, int advancedLevel)
    {
        CFGItem_Skill baseSkill;
        try { baseSkill = GetById(skillUuid); }
        catch { return null; }
        if (baseSkill == null)
            return null;

        var list = Get().DicList;
        if (list == null)
            return null;
        for (int i = 0; i < list.Count; i++)
        {
            CFGItem_Skill cfg = list[i];
            if (cfg == null)
                continue;
            if (cfg.SkillID != baseSkill.SkillID)
                continue;
            int canUpper;
            try { canUpper = (int)cfg.CanupperLevel; }
            catch { continue; }
            if (canUpper != advancedLevel)
                continue;
            if (cfg.UpperlevelSetting != null)
                return cfg.UpperlevelSetting;
        }
        return null;
    }

    //根据当前技能ID 
    public static CFGItem_Skill GetAdvanceByUidAndAdvan(int cfgID,int nowAdvance)
    {
        CFGItem_Skill baseSkill = GetById(cfgID);
        CFGItem_Skill advanceSkill = null;

        if (UpperAdvanceList.ContainsKey(baseSkill.SkillID) && UpperAdvanceList[baseSkill.SkillID].ContainsKey(nowAdvance))
        {
            return UpperAdvanceList[baseSkill.SkillID][nowAdvance];
        }
        return advanceSkill;
    }

    public enum SkillType 
    {
        //普通
        Normall =1,
        //职业，
        Job = 2,
        //特别
        Super =3,

        /// <summary>
        /// 职业 控制
        /// </summary>
        JobCtr = 4,

        /// <summary>
        /// 特殊 控制
        /// </summary>
        SuperCtr = 5,
    }


}
