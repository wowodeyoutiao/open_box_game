using HotUpdateScripts.ProtoM;
using JEngine.Helper;
using MetaFramework.UI;

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using static CFG_XMLConfig;

public class CFG_XMLConfig
{
    // 定义与 XML 文件相对应的 C# 结构体
    public class XML_Config
    {
        public XML_Item_BaseValue BaseValue;
        public XML_Item_SiftQuality[] Sift_Quality;
        public XML_Item_SiftLevel[] Sift_Level;
        /// <summary>
        /// 道具表中属性所对应的中文显示内容
        /// </summary>
        public Dictionary<string, string> EquipDisplayDic;
        /// <summary>
        /// 道具表中属性所对应的服务器字段
        /// </summary>
        public Dictionary<string, int> EquipDisplayToSeverDic;
        public List<XML_Item_RolePanel> RolePanelList;

        /// <summary>
        /// 随机洗练消耗列表
        /// </summary>
        public Dictionary<int, XML_Item_Rifine> RifineRandomDic;

        /// <summary>
        /// 定向洗练消耗列表
        /// </summary>
        public Dictionary<int, XML_Item_Rifine> RifineDirectDic;

        /// <summary>
        /// 装备强化的数值品质系数
        /// </summary>
        public Dictionary<int, float> EquipEnhanceDic;

        /// <summary>
        /// 副本内死亡复活的每次道具消耗
        /// </summary>
        public Dictionary<int, XML_Item_Revive> ReviveDic;

        /// <summary>
        /// 用来配置技能槽位的解锁等级
        /// </summary>
        public Dictionary<int, int> SkillSlotDic;

        /// <summary>
        /// 星级直升宝石所对应的星
        /// </summary>
        public HashSet<int> StarsToneSet;

        /// <summary>
        /// PVP相关数据
        /// </summary>
        public XML_Item_PVP PVPInfo;

        /// <summary>
        /// GVG相关数据
        /// </summary>
        public XML_Item_PVP GVGInfo;

        /// <summary>
        /// 百宝箱中的物品抽取几率
        /// </summary>
        public Dictionary<int, string> TreasureboxDic;

        /// <summary>
        /// 功能的开启等级
        /// </summary>
        public Dictionary<SwitchOpne, int> SwitchOpenLvDic;

        /// <summary>
        /// 功能的开启时间。开服后第几天开启。
        /// </summary>
        public Dictionary<SwitchOpne, int> SwitchOpenDayDic;

        /// <summary>
        /// 功能开启相关配置
        /// </summary>
        public Dictionary<SwitchOpne, XML_OpenFunction> SwitchOpenFunction;

        /// <summary>
        /// 命格功能的道具使用
        /// </summary>
        public int[] FateUsedItemIDs;

        /// <summary>
        /// 用来配置宝石不同槽位的描述文字，id为装备槽位
        /// </summary>
        public Dictionary<int,string> Diamond_PositionDes;

        /// <summary>
        /// 用来配置魂石大师激活所需槽位
        /// </summary>
        public Dictionary<int, int> Diamond_MasterDic;

        /// <summary>
        /// 用来配置宝石不同槽位的描述文字，宝石槽位的开启等级
        /// </summary>
        public Dictionary<int, int> Diamond_OpenlvDic;

        /// <summary>
        /// 世界boss的相关数据
        /// </summary>
        public XML_WorldBoos WorldBoss;

        public XML_FightEnd FightEnd;

        /// <summary>
        /// 每日必做信息
        /// </summary>
        public Dictionary<CFG_Room.RoomType, XML_DailyMap> DayRoomDic = new Dictionary<CFG_Room.RoomType, XML_DailyMap>();


        /// <summary>
        /// 用来配置伙伴和宠物上阵时的品质筛选条件
        /// </summary>
        public Dictionary<int, string> SelectQualityDic;

        /// <summary>
        /// 用来配置伙伴槽位激活所需主角等级
        /// </summary>
        public Dictionary<int, int> OpenSlotDic;

        /// <summary>
        /// 用来配置伙伴槽位激活所需的开服时间（天）
        /// </summary>
        public Dictionary<int, int> OpenDaySlotDic;

        public XML_Item_Guild Guild;

        public XML_Item_Partener Partener;

        public Dictionary<int, int> PartenerEXPBeadDic;

        /// <summary>
        /// 用来配置用来配置经验珠的ID和使用可获得经验
        /// </summary>
        public Dictionary<int, (int id, int num)> PartenerPayRecastingDic;

        /// <summary>
        /// 用来配置不同品质宠物升级所需经验的品质参数
        /// </summary>
        public Dictionary<int,float> PetlevelDic;


        /// <summary>
        /// 基础配置
        /// </summary>
        public class XML_Item_BaseValue
        {
            public XML_Item_Global global;
            public XML_Item_Roleval roleval;
            public XML_Item_Crit crit;
            public XML_Item_BagNum bagNum;
            public XML_Item_Power Power;
            public XML_Item_Energy energy;

            /// <summary>
            /// 主界面在线挂机配置
            /// </summary>
            public XML_Item_OnlineBattle onlineBattle;

            /// <summary>
            /// 改名
            /// </summary>
            public XML_Item_Rifine ReName;

            /// <summary>
            /// 世界频道聊天
            /// </summary>
            public XML_Item_Rifine chatConsume;

            /// <summary>
            /// 装备强化最高等级与角色等级之间的倍率
            /// </summary>
            public int equipStrenghthen;

            /// <summary>
            /// 用来配置邮件的最大上限数量
            /// </summary>
            public int mailMaxNum;

            /// <summary>
            /// 自动战斗功能开启所需要的vip等级
            /// </summary>
            public int autoPlayVipLv;

            /// <summary>
            /// 自动分解功能开启所需要的vip等级
            /// </summary>
            public int autoDelVipLv;

            /// <summary>
            /// 自动分解功能开启所需要的等级
            /// </summary>
            public int autoDelLv;

            public int Petbuy_itemid;
            public int Petbuy_needmoney;

            /// <summary>
            /// 用来配置第一档宠物蛋免费恢复的时间间隔（分），最大可恢复数量
            /// </summary>
            public int Petfree_maxNum;

            /// <summary>
            /// 加速卷轴id,用于加速升级宝箱所用的的时间
            /// </summary>
            public int BoxLevelUp_itemId;

            /// <summary>
            /// 加速升级宝箱可以提升或加快多少时间（秒）
            /// </summary>
            public int BoxLevelUp_accelerate;
        }

        public class XML_Item_Revive
        {
            public int time;
            public int needMoneyID;
            public int num;
        }


        /// <summary>
        /// 局部变量
        /// </summary>
        public class XML_Item_Global
        {
            /// <summary>
            /// 角色数量
            /// </summary>
            public int rolenum;
        }

        /// <summary>
        /// 用来配置角色的
        /// </summary>
        public class XML_Item_Roleval
        {
            /// <summary>
            /// 攻击速度
            /// </summary>
            public int attackSpeed;
            /// <summary>
            /// 移动速度
            /// </summary>
            public int moveSpeed;
        }

        /// <summary>
        /// 配置暴击率
        /// </summary>
        public class XML_Item_Crit
        {
            /// <summary>
            /// 暴击率的系数K
            /// </summary>
            public float k;
            /// <summary>
            /// 固定常量
            /// </summary>
            public int prevalue;

            /// <summary>
            /// 暴击伤害的基础伤害比例
            /// </summary>
            public float critdamage;
        }

        /// <summary>
        /// 配置战斗力
        /// </summary>
        public class XML_Item_Power
        {
            /// <summary>
            /// 防御系数
            /// </summary>
            public int defvalue;
            /// <summary>
            /// 有效生命系数
            /// </summary>
            public int lifevalue;
            /// <summary>
            /// 有效输出系数
            /// </summary>
            public int attackvalue;

            /// <summary>
            /// 命中
            /// </summary>
            public int hitvalue;

            /// <summary>
            /// 闪避
            /// </summary>
            public int dodgevalue;

            /// <summary>
            /// 暴击
            /// </summary>
            public int critvalue;

            /// <summary>
            /// 韧性系数
            /// </summary>
            public int toughness;

        }

        /// <summary>
        /// 角色/伙伴各职业初始攻速移速
        /// </summary>
        public class XML_Item_Initial
        {
            public int job;
            public int attackSpeed;
            public int moveSpeed;
        }

        /// <summary>
        /// 主界面在线挂机配置
        /// </summary>
        public class XML_Item_OnlineBattle
        {
            /// <summary>基础在线挂机时长（秒）</summary>
            public int baseTime;
            /// <summary>事件触发间隔（秒）</summary>
            public int triggerTime;
            /// <summary>点击间隔（秒）</summary>
            public int limitTime;
        }

        public class XML_Item_Energy
        {
            /// <summary>
            /// 满进度值
            /// </summary>
            public int fullValue;
            /// <summary>
            /// 最大可累积值
            /// </summary>
            public int maxValue;
            /// <summary>
            /// 恢复一点需要的分钟
            /// </summary>
            public int timeInterval;

            /// <summary>
            /// Peace体力值兑换时间(ms)
            /// </summary>
            public int buyTime_Peace;
            /// <summary>
            /// Peace兑换一次时间需要消耗体力值
            /// </summary>
            public int consumeEnergy_Peace;

            /// <summary>
            /// War 体力值兑换时间(ms)
            /// </summary>
            public int buyTime_War;
            /// <summary>
            /// War 兑换一次时间需要消耗体力值
            /// </summary>
            public int consumeEnergy_War;
        }

        /// <summary>
        /// 配置总包裹
        /// </summary>
        public class XML_Item_BagNum
        {
            /// <summary>
            /// 总包裹的格子数量
            /// </summary>
            public int totlenum;
            /// <summary>
            /// 装备包裹的格子数量
            /// </summary>
            public int equipnum;

            /// <summary>
            /// 其他道具的格子数量
            /// </summary>
            public int itemnum;
        }

        public class XML_Item_SiftQuality
        {
            public int ID;
            public string Option;
        }

        public class XML_Item_SiftLevel
        {
            public int ID;
            public string Option;
        }

        //角色属性面板的显示
        public class XML_Item_RolePanel
        {
            public int SID;
            public string Name;
            /// <summary>
            /// 1代表固定值；2代表百分比
            /// </summary>
            public int Type;
            /// <summary>
            /// 1代表基础属性；2代表特殊属性
            /// </summary>
            public int Category;

            /// <summary>
            /// 面板上显示的顺序
            /// </summary>
            public int Priority;

            /// <summary>
            /// 用来控制是否在面板上显示
            /// </summary>
            public int Show;
        }

        //洗炼需要消耗的物品和数量
        public class XML_Item_Rifine 
        {
            public int itemid;
            public int num;
        }

        //PVP
        public class XML_Item_PVP
        {

            public int playerNum;

            /// <summary>
            /// 玩法中每天默认挑战次数
            /// </summary>
            public int dailyTime;

            /// <summary>
            /// 法中每天默认挑战次数，商城表中的购买ID
            /// </summary>
            public int storeId;

            // <summary>
            /// 玩法中匹配时间
            /// </summary>
            public int matchTime;

            // <summary>
            /// 开始挑战等待时间
            /// </summary>
            public int waitTime;

            // <summary>
            /// 挑战持续时间
            /// </summary>
            public int duration;

            // <summary>
            /// 每日活动开启时间
            /// </summary>
            public string dailyBegin;

            // <summary>
            /// 结束时间
            /// </summary>
            public string dailyEnd;

            // <summary>
            /// 每日榜单结算奖励
            /// </summary>
            public string time;

            public PVPreward[] winAwards;

            public PVPreward[] loseAwards;

            public class PVPreward 
            {
                public int id;
                public int num;
                public int mark;
            }

            public Dictionary<int, PVPseason> PVPSeasonDic;

            public class PVPseason
            {
                public int id;
                public string starttime;
                public string endtime;
            }

            // <summary>
            /// 死亡复活的时间
            /// </summary>
            public int alivetime;

            // <summary>
            /// 无敌时间
            /// </summary>
            public int protecttime;

            public Dictionary<int, PVPreward[]> GVGReward;

            public int WinScore;
            public int LoseScore;
            public int MaxRound;
        }

        public enum SwitchOpne 
        {
            No = 0,

            OpenBox = 1,
            Task =2,
            SignDay =3,
            ActiveCenter = 4,
            FirstPay = 5,
            PrivilegeCard = 6,
            VIP =7,
            Setting =8,
            Role =9,
            Bag =10,
            Shop = 11,
            Chat =12,

            Role_SkillAndCall = 13,
            ExpPool = 14,
            Partner_Star = 16,
            Shop_SL = 18,
            Pet = 20,
            Shop_ZH = 22,

            Play = 15,
            Play_Boss = 17,
            Play_SlTower = 19,
            Play_Relice = 21,
            Play_PVP = 23,
            Play_GVG = 24,
            Play_QHSAndGold = 25,
            Play_ExpAndSXS = 27,
            Play_SY = 29,
            Play_HS = 31,

            Build = 26,
            Build_EquipStar = 28,
            Build_SkillLv = 30,
            Build_EquipInster = 32,
            Build_SkillStar = 34,

            Guild = 33,

            DefendIsland = 35,
            WorldBoss = 36,
            FightEnd = 37,
        }

        public class XML_OpenFunction
        {
            public int id;
            public string name;
            public int openLevel;
            public int openDay;
            public bool isShow;
            public string describe;
        }

        //世界BOSS
        public class XML_WorldBoos 
        {
            public int weekSet;
            public string startTime;
            public string endTime;

            /// <summary>
            /// 世界boss的血条数量
            /// </summary>
            public int bloodCount;

            /// <summary>
            /// 世界boss每天的挑战次数
            /// </summary>
            public int enterTime;

            /// <summary>
            /// 世界boss每次的挑战时间
            /// </summary>
            public int challengeTime;

            public string bossDescribe;
            public int accommodationNum;
        }

        public class XML_FightEnd
        {
            public int weekSet;
            public string startTime;
            public string endTime;
        }

        //每日必做副本
        public class XML_DailyMap
        {
            /// <summary>
            /// 挑战次数
            /// </summary>
            public int challengeTimeNum;

            /// <summary>
            /// 副本持续时间
            /// </summary>
            public int challengeTimeTime;

            /// <summary>
            /// 扫荡的vip等级
            /// </summary>
            public int viplimit;

            /// <summary>
            /// 双倍所需点券数量
            /// </summary>
            public int doubleaccessID;

            /// <summary>
            /// 双倍所需点券数量
            /// </summary>
            public int doubleaccessNum;

            /// <summary>
            /// 用来配置总刷怪波次
            /// </summary>
            public int totlecycleNum;

            /// <summary>
            /// 用来配置扫荡的结算波次
            /// </summary>
            public int quickaccessScore;

            /// <summary>
            /// 用来配置评分
            /// </summary>
            public ScoreItem[] ScoreDic;

            public class ScoreItem 
            {
                public string key;
                public int value;
            }

        }

        /// <summary>
        /// 配置不同品质装备的品质参数
        /// </summary>
        public Dictionary<int, Equipquality_Item> EquipQualityDic;
        public class Equipquality_Item 
        {
            public float _value;
            public string _prefix;
        }

        /// <summary>
        /// 各职业初始攻速移速
        /// </summary>
        public Dictionary<int, XML_Item_Initial> InitialDic;

        //用来配置主角等级解锁的宠物包裹数量
        public Dictionary<int, int> PetBagDic;

        //配置战宠进阶功能的道具使用
        public GoodItem[] PetAdvanceUseds;

        //配置战宠进阶功能的道具使用
        public Dictionary<int, float> PetQualityDic;

        //配置伙伴进阶功能的道具使用
        public Dictionary<int, float> PartnerQualityDic;
        public class GoodItem 
        {
            public int id;
            public int num;
        }

        public class XML_Item_Guild
        {
            public XML_Item_Match match;
            public XML_Item_Wishvalue wishvalue;
            public XML_Item_Creatcost creatcost;
            // 用来配置重新进入行会的冷却时间，单位小时
            public int interlimitTime;
            public XML_Item_Golddonate golddonate;
            public XML_Item_Moneydonate moneydonate;
            // 用来配置捐献的每日次数
            public int donatelimitDailytime;
            public XML_Item_Guildcopy guildcopy;
        }

        /// <summary>
        /// 用来配置申请加入行会的列表最多显示行会数量以及行会审核入会人员列表最多显示的角色数量
        /// </summary>
        public class XML_Item_Match
        {
            public int guildnum;
            public int applicantnum;
        }

        /// <summary>
        /// 用来配置祈愿点每隔X分钟恢复1点、每天最多累积X点、1点祈愿值对应多少行会贡献、1点祈愿值对应多少技能经验
        /// </summary>
        public class XML_Item_Wishvalue
        {
            public int refreshtime;
            public int maxvalue;
            public int getitemnum;
        }

        /// <summary>
        /// 用来配置创建行会消耗的道具id和数量
        /// </summary>
        public class XML_Item_Creatcost
        {
            public int itemid;
            public int neednum;
        }

        /// <summary>
        /// 用来配置金币捐献消耗的道具ID、数量、获得行会贡献的数量、提供行会经验的数量、行会贡献的数值道具ID
        /// </summary>
        public class XML_Item_Golddonate
        {
            public int needitemid;
            public int needitemnum;
            public int getitemnum;
            public int getguildexp;
            public int getguildexpid;
        }

        /// <summary>
        /// 用来配置点券捐献消耗的道具ID、数量、获得行会贡献的数量、提供行会经验的数量、行会贡献的数值道具ID
        /// </summary>
        public class XML_Item_Moneydonate
        {
            public int needitemid;
            public int needitemnum;
            public int getitemnum;
            public int getguildexp;
            public int getguildexpid;
        }

        /// <summary>
        /// 用来配置行会副本的起止时间以及活动持续时间（分钟）
        /// </summary>
        public class XML_Item_Guildcopy
        {
            public string starttime;
            public string endtime;
            public string lasttime;
        }

        public class XML_Item_Partener
        {
            public int forgeRate;
            //public int forgeChargeId;
            //public int forgeChargenum;
            public XML_Item_Decompose decompose;
        }

        public class XML_Item_Decompose
        {
            public int quality;
            public int starlevel;
            public int rate;
            public int id;
        }

        /// <summary>
        /// 守卫海岛（挂机副本）type 1免费；2广告；3物品；4充值; multiplier 为万分比；OrderNum为序号
        /// </summary>
        public Dictionary<int, GuardIslandItem> GuardIslandDic;
        public class GuardIslandItem
        {
            public int id;
            public int OrderNum;
            public int type;
            public float multiplier;
            public string describe;
        }
    }

    private static XML_Config _xmlConfig;
    public static XML_Config xmlConfig
    {
        get
        {
            return _xmlConfig;
        }
    }

    public static string GetAttrValueToStr(int SType, long value)
    {
        if (SType == 1)
            return GameUtil.Instance.FormatNumWithUnit(value);
        else
            return Mathf.Floor((value / 10f)) / 10f + "%";
    }

    //获取属性的显示字符串
    public static string GetAttributeStrByID(int id ,int value,bool isColor = false)
    {
        for (int i = 0; i < xmlConfig.RolePanelList.Count; i++)
        {
            if (xmlConfig.RolePanelList[i].SID == id)
            {
                return xmlConfig.RolePanelList[i].Name + ":" + ( isColor ? $"<color=#2F8C19>{GetAttrValueToStr(xmlConfig.RolePanelList[i].Type, value)}</color>" : GetAttrValueToStr(xmlConfig.RolePanelList[i].Type, value));
            }
        }
        return "";
    }

    public static XML_Config.XML_Item_RolePanel GetAttributeByID(int id)
    {
        for (int i = 0; i < xmlConfig.RolePanelList.Count; i++)
        {
            if (xmlConfig.RolePanelList[i].SID == id)
            {
                return xmlConfig.RolePanelList[i];
            }
        }
        return null;
    }

    public static async Task InitAsync()
    {
        await ResouseHelper.Instance.LoadTextAssetAsync("Other/TextAssets/config", (_xmlStr) => {
            SetXML(_xmlStr);
        });
       
    }
    private static void SetXML(string xmlStr)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlStr);
        _xmlConfig = new XML_Config();
        // 解析 BaseValue
        XmlElement baseValueElement = (XmlElement)xmlDoc.SelectSingleNode("/Config/BaseValue");
        //Debug.Log("Xml 7");
        _xmlConfig.BaseValue = new XML_Config.XML_Item_BaseValue
        {
            global = new XML_Config.XML_Item_Global
            {
                rolenum = GetIntXML(baseValueElement, "global", "rolenum"),
            },
            //roleval = new XML_Config.XML_Item_Roleval
            //{
            //    attackSpeed = GetIntXML(baseValueElement, "roleval", "attackSpeed") ,
            //    moveSpeed = GetIntXML(baseValueElement, "roleval", "moveSpeed") ,
            //},
            crit = new XML_Config.XML_Item_Crit
            {
                k = GetFloatXML(baseValueElement, "crit", "k"),
                prevalue = GetIntXML(baseValueElement, "crit", "prevalue"),
                critdamage = GetFloatXML(baseValueElement, "crit", "critdamage"),
            },

            Power = new XML_Config.XML_Item_Power
            {
                defvalue = GetIntXML(baseValueElement, "power", "defvalue"),
                lifevalue = GetIntXML(baseValueElement, "power", "lifevalue"),
                attackvalue = GetIntXML(baseValueElement, "power", "attackvalue"),
                hitvalue = GetIntXML(baseValueElement, "power", "hitvalue"),
                dodgevalue = GetIntXML(baseValueElement, "power", "dodgevalue"),
                critvalue = GetIntXML(baseValueElement, "power", "critvalue"),
                toughness = GetIntXML(baseValueElement, "power", "toughness"),
            },

            bagNum = new XML_Config.XML_Item_BagNum
            {
                totlenum = GetIntXML(baseValueElement, "bagnum", "totlenum"),
                equipnum = GetIntXML(baseValueElement, "bagnum", "equipnum"),
                itemnum = GetIntXML(baseValueElement, "bagnum", "itemnum"),
            },
            energy = new XML_Config.XML_Item_Energy
            {
                buyTime_Peace = GetIntXML(baseValueElement, "energy", "buytime_peace"),
                consumeEnergy_Peace = GetIntXML(baseValueElement, "energy", "consumeenergy_peace"),
                buyTime_War = GetIntXML(baseValueElement, "energy", "buytime_war"),
                consumeEnergy_War = GetIntXML(baseValueElement, "energy", "consumeenergy_war"),
                fullValue = GetIntXML(baseValueElement, "energy", "fullvalue"),
                maxValue = GetIntXML(baseValueElement, "energy", "maxvalue"),
                timeInterval = GetIntXML(baseValueElement, "energy", "timeinterval"),
            },
            onlineBattle = new XML_Config.XML_Item_OnlineBattle
            {
                baseTime = GetIntXML(baseValueElement, "onlinebattle", "basetime"),
                triggerTime = GetIntXML(baseValueElement, "onlinebattle", "triggertime"),
                limitTime = GetIntXML(baseValueElement, "onlinebattle", "limittime"),
            },

            ReName = new XML_Config.XML_Item_Rifine
            {
                itemid = GetIntXML(baseValueElement, "rename", "needitem"),
                num = GetIntXML(baseValueElement, "rename", "neednum"),
            },
            equipStrenghthen = GetIntXML(baseValueElement, "equipstrenghthen", "rate"),

            chatConsume = new XML_Config.XML_Item_Rifine
            {
                itemid = GetIntXML(baseValueElement, "chatconsume", "id"),
                num = GetIntXML(baseValueElement, "chatconsume", "num"),
            },

            mailMaxNum = GetIntXML(baseValueElement, "mailmax", "num"),
            autoPlayVipLv = GetIntXML(baseValueElement, "autobattle", "viplevel"),

            autoDelLv = GetIntXML(baseValueElement, "autodecompose", "rolelevel"),
            autoDelVipLv = GetIntXML(baseValueElement, "autodecompose", "viplevel"),

            Petbuy_itemid = GetIntXML(baseValueElement, "petbuy", "itemid"),
            Petbuy_needmoney = GetIntXML(baseValueElement, "petbuy", "needmoney"),
            Petfree_maxNum = GetIntXML(baseValueElement, "petfree", "maxnum"),

            BoxLevelUp_itemId = GetIntXML(baseValueElement, "boxlevelup", "itemid"),
            BoxLevelUp_accelerate = GetIntXML(baseValueElement, "boxlevelup", "accelerate"),
        };

        //批量分解中的筛选条件
        XmlNodeList KeyNodes = xmlDoc.SelectNodes("/Config/sifting/quality/key");

        _xmlConfig.Sift_Quality = new XML_Config.XML_Item_SiftQuality[KeyNodes.Count];

        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];

            _xmlConfig.Sift_Quality[i] = new XML_Config.XML_Item_SiftQuality
            {
                ID = int.Parse(_element.GetAttribute("ID")),
                Option = _element.GetAttribute("option"),
            };
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/Equipquality/key");
        _xmlConfig.EquipQualityDic = new Dictionary<int, XML_Config.Equipquality_Item>();

        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.EquipQualityDic.Add(int.Parse(_element.GetAttribute("ID")), new XML_Config.Equipquality_Item
            {
                _value = float.Parse(_element.GetAttribute("value")),
                _prefix = _element.GetAttribute("prefix"),
            });
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/initial/key");
        _xmlConfig.InitialDic = new Dictionary<int, XML_Config.XML_Item_Initial>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            int job = int.Parse(_element.GetAttribute("job"));
            _xmlConfig.InitialDic[job] = new XML_Config.XML_Item_Initial
            {
                job = job,
                attackSpeed = int.Parse(_element.GetAttribute("attackspeed")),
                moveSpeed = int.Parse(_element.GetAttribute("movespeed")),
            };
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/sifting/level/key");
        _xmlConfig.Sift_Level = new XML_Config.XML_Item_SiftLevel[KeyNodes.Count];

        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.Sift_Level[i] = new XML_Config.XML_Item_SiftLevel
            {
                ID = int.Parse(_element.GetAttribute("ID")),
                Option = (_element.GetAttribute("option")),
            };
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/EquipDisplay/key");
        _xmlConfig.EquipDisplayDic = new Dictionary<string, string>();

        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.EquipDisplayDic.Add(_element.GetAttribute("word"), _element.GetAttribute("name"));
        }

        _xmlConfig.EquipDisplayToSeverDic = new Dictionary<string, int>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.EquipDisplayToSeverDic.Add(_element.GetAttribute("word"), int.Parse(_element.GetAttribute("id")));
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/RolePanel/key");
        _xmlConfig.RolePanelList = new List<XML_Config.XML_Item_RolePanel>();

        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.RolePanelList.Add(new XML_Config.XML_Item_RolePanel
            {
                SID = int.Parse(_element.GetAttribute("id")),
                Name = _element.GetAttribute("name"),
                Type = int.Parse(_element.GetAttribute("type")),
                Category = int.Parse(_element.GetAttribute("category")),
                Priority = int.Parse(_element.GetAttribute("priority")),
                Show = int.Parse(_element.GetAttribute("show")),
            });
        }
        _xmlConfig.RolePanelList.Sort((a, b) => { return a.Priority.CompareTo(b.Priority); });

        //装备洗练信息

        KeyNodes = xmlDoc.SelectNodes("/Config/Rifinerandom/quality");
        _xmlConfig.RifineRandomDic = new Dictionary<int, XML_Config.XML_Item_Rifine>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.RifineRandomDic.Add(
                int.Parse(_element.GetAttribute("ID")),
                new XML_Config.XML_Item_Rifine
                {
                    itemid = int.Parse(_element.GetAttribute("itemid")),
                    num = int.Parse(_element.GetAttribute("itemnum")),
                });
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/Rifinedirect/quality");
        _xmlConfig.RifineDirectDic = new Dictionary<int, XML_Config.XML_Item_Rifine>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.RifineDirectDic.Add(
                int.Parse(_element.GetAttribute("ID")),
                new XML_Config.XML_Item_Rifine
                {
                    itemid = int.Parse(_element.GetAttribute("itemid")),
                    num = int.Parse(_element.GetAttribute("itemnum")),
                });
        }


        //装备强化的数值品质系数

        KeyNodes = xmlDoc.SelectNodes("/Config/Equipenhance/quality");
        _xmlConfig.EquipEnhanceDic = new Dictionary<int, float>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.EquipEnhanceDic.Add(
                int.Parse(_element.GetAttribute("ID")),
                float.Parse(_element.GetAttribute("value"))
                );
        }

        //用来配置星级直升宝石所对应的星
        KeyNodes = xmlDoc.SelectNodes("/Config/starstone/starlevel");
        _xmlConfig.StarsToneSet = new HashSet<int>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.StarsToneSet.Add(
                int.Parse(_element.GetAttribute("ID"))
                );
        }

        //副本内死亡复活的每次道具消耗
        KeyNodes = xmlDoc.SelectNodes("/Config/Revive/key");
        _xmlConfig.ReviveDic = new Dictionary<int, XML_Config.XML_Item_Revive>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.ReviveDic.Add(
                int.Parse(_element.GetAttribute("time")),
                new XML_Config.XML_Item_Revive
                {
                    time = int.Parse(_element.GetAttribute("time")),
                    needMoneyID = int.Parse(_element.GetAttribute("needMoneyID")),
                    num = int.Parse(_element.GetAttribute("num")),
                });
        }

        //用来配置技能槽位的解锁等级
        KeyNodes = xmlDoc.SelectNodes("/Config/skillslot/slot");
        _xmlConfig.SkillSlotDic = new Dictionary<int, int>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.SkillSlotDic.Add(
                int.Parse(_element.GetAttribute("ID")),
                int.Parse(_element.GetAttribute("openlevel"))
               );
        }

        //用来配置伙伴和宠物上阵时的品质筛选条件
        KeyNodes = xmlDoc.SelectNodes("/Config/selectquality/key");
        _xmlConfig.SelectQualityDic = new Dictionary<int, string>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.SelectQualityDic.Add(
                int.Parse(_element.GetAttribute("ID")),
                _element.GetAttribute("option")
               );
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/openslot/stage");
        _xmlConfig.OpenSlotDic = new Dictionary<int, int>();
        _xmlConfig.OpenDaySlotDic = new Dictionary<int, int>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.OpenSlotDic.Add(
                int.Parse(_element.GetAttribute("id")),
                int.Parse(_element.GetAttribute("needlevel"))
               );
            _xmlConfig.OpenDaySlotDic.Add(
                int.Parse(_element.GetAttribute("id")),
                int.Parse(_element.GetAttribute("openday"))
               );
        }

        //pvp 信息
        baseValueElement = (XmlElement)xmlDoc.SelectSingleNode("/Config/PVP");
        _xmlConfig.PVPInfo = new XML_Config.XML_Item_PVP
        {
            playerNum = GetIntXML(baseValueElement, "match", "singlePVP"),
            dailyTime = GetIntXML(baseValueElement, "dailyset", "dailytime"),
            storeId = GetIntXML(baseValueElement, "dailyset", "storeid"),

            matchTime = GetIntXML(baseValueElement, "timeset", "matchtime"),
            waitTime = GetIntXML(baseValueElement, "timeset", "waittime"),
            duration = GetIntXML(baseValueElement, "timeset", "duration"),

            dailyBegin = GetStrXML(baseValueElement, "settlement", "dailybegin"),
            dailyEnd = GetStrXML(baseValueElement, "settlement", "dailyend"),
            time = GetStrXML(baseValueElement, "settlement", "time"),

            WinScore = GetIntXML(baseValueElement, "score", "victory"),
            LoseScore = GetIntXML(baseValueElement, "score", "lose"),
        };

        KeyNodes = xmlDoc.SelectNodes("/Config/PVPreward/win/key");
        _xmlConfig.PVPInfo.winAwards = new XML_Config.XML_Item_PVP.PVPreward[KeyNodes.Count];
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PVPInfo.winAwards[i] = new XML_Config.XML_Item_PVP.PVPreward
            {
                id = int.Parse(_element.GetAttribute("id")),
                num = int.Parse(_element.GetAttribute("num")),
                mark = int.Parse(_element.GetAttribute("mark")),
            };
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/PVPreward/lose/key");
        _xmlConfig.PVPInfo.loseAwards = new XML_Config.XML_Item_PVP.PVPreward[KeyNodes.Count];
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PVPInfo.loseAwards[i] = new XML_Config.XML_Item_PVP.PVPreward
            {
                id = int.Parse(_element.GetAttribute("id")),
                num = int.Parse(_element.GetAttribute("num")),
                mark = int.Parse(_element.GetAttribute("mark")),
            };
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/PVPseason/season");
        _xmlConfig.PVPInfo.PVPSeasonDic = new Dictionary<int, XML_Config.XML_Item_PVP.PVPseason>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PVPInfo.PVPSeasonDic.Add(
                int.Parse(_element.GetAttribute("id")),
                new XML_Config.XML_Item_PVP.PVPseason
                {
                    id = int.Parse(_element.GetAttribute("id")),
                    starttime = _element.GetAttribute("starttime"),
                    endtime = _element.GetAttribute("endtime"),
                });
        }

        //GVG 信息
        baseValueElement = (XmlElement)xmlDoc.SelectSingleNode("/Config/GVG");
        _xmlConfig.GVGInfo = new XML_Config.XML_Item_PVP
        {
            playerNum = GetIntXML(baseValueElement, "match", "multiplePVP"),

            dailyTime = GetIntXML(baseValueElement, "dailyset", "dailytime"),
            storeId = GetIntXML(baseValueElement, "dailyset", "storeid"),

            matchTime = GetIntXML(baseValueElement, "timeset", "matchtime"),
            waitTime = GetIntXML(baseValueElement, "timeset", "waittime"),
            duration = GetIntXML(baseValueElement, "timeset", "duration"),

            dailyBegin = GetStrXML(baseValueElement, "settlement", "dailybegin"),
            dailyEnd = GetStrXML(baseValueElement, "settlement", "dailyend"),
            time = GetStrXML(baseValueElement, "settlement", "time"),

            alivetime = GetIntXML(baseValueElement, "relive", "alivetime"),
            protecttime = GetIntXML(baseValueElement, "relive", "protecttime"),
            MaxRound = GetIntXML(baseValueElement, "Maxround", "num"),
        };

        KeyNodes = xmlDoc.SelectNodes("/Config/GVG/reward/key");
        _xmlConfig.GVGInfo.GVGReward = new Dictionary<int, XML_Config.XML_Item_PVP.PVPreward[]>();

        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            var nodes = _element.SelectNodes("item");

            var rewards = new XML_Config.XML_Item_PVP.PVPreward[nodes.Count];
            for (int j = 0; j < nodes.Count; j++)
            {
                XmlElement element = (XmlElement)nodes[j];
                rewards[j] = new XML_Config.XML_Item_PVP.PVPreward
                {
                    id = int.Parse(element.GetAttribute("id")),
                    num = int.Parse(element.GetAttribute("num"))
                };
            }
            _xmlConfig.GVGInfo.GVGReward.Add(int.Parse(_element.GetAttribute("round")), rewards);
        }

        //百宝箱中的物品抽取几率
        KeyNodes = xmlDoc.SelectNodes("/Config/Treasurebox/key");
        _xmlConfig.TreasureboxDic = new Dictionary<int, string>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.TreasureboxDic.Add(
                int.Parse(_element.GetAttribute("id")),
                _element.GetAttribute("rate"));
        }

        //功能的开启等级
        KeyNodes = xmlDoc.SelectNodes("/Config/switch/module");
        _xmlConfig.SwitchOpenLvDic = new Dictionary<XML_Config.SwitchOpne, int>();
        _xmlConfig.SwitchOpenDayDic = new Dictionary<XML_Config.SwitchOpne, int>();
        _xmlConfig.SwitchOpenFunction = new Dictionary<XML_Config.SwitchOpne, XML_Config.XML_OpenFunction>();

        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            int openLv = int.Parse(_element.GetAttribute("openlevel"));
            XML_Config.SwitchOpne id = (XML_Config.SwitchOpne)int.Parse(_element.GetAttribute("ID"));
            _xmlConfig.SwitchOpenLvDic.Add(id, openLv);

            if(int.TryParse(_element.GetAttribute("openday"), out int value))
            {
                _xmlConfig.SwitchOpenDayDic.Add(id, value);
            }

            // **   功能开启配置信息   ** //
            bool isShow = _element.GetAttribute("isshow") == "1";
            var openFunc = new XML_Config.XML_OpenFunction
            {
                id = int.Parse(_element.GetAttribute("ID")),
                name = _element.GetAttribute("name"),
                openLevel = int.Parse(_element.GetAttribute("openlevel")),
                openDay = int.TryParse(_element.GetAttribute("openday"), out int r) ? r : 1,
                isShow = isShow,
                describe = isShow ? _element.GetAttribute("describe") : ""
            };

            _xmlConfig.SwitchOpenFunction.Add(id, openFunc);
        }

        //FateUsedItemIDs

        KeyNodes = xmlDoc.SelectNodes("/Config/fate/basevalue/item");
        _xmlConfig.FateUsedItemIDs = new int[KeyNodes.Count];
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.FateUsedItemIDs[i] = int.Parse(_element.GetAttribute("id"));
        }

        //用来配置宝石的相关内容
        KeyNodes = xmlDoc.SelectNodes("/Config/Diamond/position/pos");
        _xmlConfig.Diamond_PositionDes = new Dictionary<int, string>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.Diamond_PositionDes.Add(int.Parse(_element.GetAttribute("id")), _element.GetAttribute("des"));
        }

        //用来配置魂石大师激活所需槽位
        KeyNodes = xmlDoc.SelectNodes("/Config/Diamond/master/stage");
        _xmlConfig.Diamond_MasterDic = new Dictionary<int, int>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.Diamond_MasterDic.Add(int.Parse(_element.GetAttribute("id")), int.Parse(_element.GetAttribute("num")));
        }


        //用来配置宝石槽位的开启等级
        KeyNodes = xmlDoc.SelectNodes("/Config/Diamond/openlevel/stage");
        _xmlConfig.Diamond_OpenlvDic = new Dictionary<int, int>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.Diamond_OpenlvDic.Add(int.Parse(_element.GetAttribute("num")), int.Parse(_element.GetAttribute("level")));
        }

        //用来配置不同品质宠物升级所需经验的品质参数
        KeyNodes = xmlDoc.SelectNodes("/Config/Petlevel/key");
        _xmlConfig.PetlevelDic = new Dictionary<int, float>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PetlevelDic.Add(int.Parse(_element.GetAttribute("ID")), float.Parse(_element.GetAttribute("value")));
        }

        #region 海岛挂机
        KeyNodes = xmlDoc.SelectNodes("/Config/GuardIsland/record/rate");
        _xmlConfig.GuardIslandDic = new Dictionary<int, XML_Config.GuardIslandItem>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.GuardIslandDic.Add(int.Parse(_element.GetAttribute("OrderNum")), new XML_Config.GuardIslandItem { 
                
                OrderNum = int.Parse(_element.GetAttribute("OrderNum")),
                id = int.Parse(_element.GetAttribute("id")),
                type = int.Parse(_element.GetAttribute("type")),
                multiplier = float.Parse(_element.GetAttribute("multiplier")) / 10000f + 1,
                describe = _element.GetAttribute("describe"),
            });
        }
        #endregion

        #region 宠物
        //解锁的宠物包裹
        KeyNodes = xmlDoc.SelectNodes("/Config/Petbag/key");
        _xmlConfig.PetBagDic = new Dictionary<int, int>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PetBagDic.Add(int.Parse(_element.GetAttribute("value")), int.Parse(_element.GetAttribute("Level")));
        }

        //战宠进阶功能的道具
        KeyNodes = xmlDoc.SelectNodes("/Config/PetAdvance/basevalue/item");
        _xmlConfig.PetAdvanceUseds = new XML_Config.GoodItem[KeyNodes.Count];
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PetAdvanceUseds[i] = new XML_Config.GoodItem
            {
                id = int.Parse(_element.GetAttribute("id")),
                num = int.Parse(_element.GetAttribute("num")),
            };
        }

        //用来配置不同品质宠物升级的品质参数
        KeyNodes = xmlDoc.SelectNodes("/Config/Petquality/key");
        _xmlConfig.PetQualityDic = new Dictionary<int, float>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PetQualityDic.Add(int.Parse(_element.GetAttribute("ID")), float.Parse(_element.GetAttribute("value")));
        }

        //用来配置不同品质伙伴升级的品质参数
        KeyNodes = xmlDoc.SelectNodes("/Config/Partnerquality/key");
        _xmlConfig.PartnerQualityDic = new Dictionary<int, float>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PartnerQualityDic.Add(int.Parse(_element.GetAttribute("ID")), float.Parse(_element.GetAttribute("value")));
        }


        #endregion

        #region 世界BOSS

        baseValueElement = (XmlElement)xmlDoc.SelectSingleNode("/Config/worldBoss");
        _xmlConfig.WorldBoss = new XML_Config.XML_WorldBoos
        {
            weekSet = GetIntXML(baseValueElement, "set", "weekset"),
            startTime = GetStrXML(baseValueElement, "set", "starttime"),
            endTime = GetStrXML(baseValueElement, "set", "endtime"),

            bloodCount = GetIntXML(baseValueElement, "blood", "num"),
            enterTime = GetIntXML(baseValueElement, "entertime", "num"),

            challengeTime = GetIntXML(baseValueElement, "challengetime", "value"),

            bossDescribe = GetStrXML(baseValueElement, "bossdescribe", "des"),
            accommodationNum = GetIntXML(baseValueElement, "accommodation", "num"),
        };

        #endregion

        #region 一战到底
        baseValueElement = (XmlElement)xmlDoc.SelectSingleNode("/Config/fightend");
        _xmlConfig.FightEnd = new XML_Config.XML_FightEnd
        {
            weekSet = GetIntXML(baseValueElement, "set", "weekset"),
            startTime = GetStrXML(baseValueElement, "set", "starttime"),
            endTime = GetStrXML(baseValueElement, "set", "endtime"),
        };
        #endregion

        #region 每日必做

        baseValueElement = (XmlElement)xmlDoc.SelectSingleNode("/Config/dailymap");

        _xmlConfig.DayRoomDic.Add(CFG_Room.RoomType.Gold, new XML_Config.XML_DailyMap
        {
            challengeTimeNum = GetIntXML(baseValueElement, "goldmap/challengetime", "num"),
            challengeTimeTime = GetIntXML(baseValueElement, "goldmap/challengetime", "time"),
            viplimit = GetIntXML(baseValueElement, "goldmap/challengetime", "viplimit"),
            doubleaccessID = GetIntXML(baseValueElement, "goldmap/doubleaccess", "id"),
            doubleaccessNum = GetIntXML(baseValueElement, "goldmap/doubleaccess", "num"),
        });

        KeyNodes = xmlDoc.SelectNodes("/Config/dailymap/goldmap/score/stage");
        _xmlConfig.DayRoomDic[CFG_Room.RoomType.Gold].ScoreDic = new XML_Config.XML_DailyMap.ScoreItem[KeyNodes.Count];
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.DayRoomDic[CFG_Room.RoomType.Gold].ScoreDic[i] = new XML_Config.XML_DailyMap.ScoreItem
            {
                key = _element.GetAttribute("key"),
                value = int.Parse(_element.GetAttribute("value")),
            };
        }

        _xmlConfig.DayRoomDic.Add(CFG_Room.RoomType.EXP, new XML_Config.XML_DailyMap
        {
            challengeTimeNum = GetIntXML(baseValueElement, "expmap/challengetime", "num"),
            challengeTimeTime = GetIntXML(baseValueElement, "expmap/challengetime", "time"),
            viplimit = GetIntXML(baseValueElement, "expmap/challengetime", "viplimit"),
            doubleaccessID = GetIntXML(baseValueElement, "expmap/doubleaccess", "id"),
            doubleaccessNum = GetIntXML(baseValueElement, "expmap/doubleaccess", "num"),
        });

        KeyNodes = xmlDoc.SelectNodes("/Config/dailymap/expmap/score/stage");
        _xmlConfig.DayRoomDic[CFG_Room.RoomType.EXP].ScoreDic = new XML_Config.XML_DailyMap.ScoreItem[KeyNodes.Count];
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.DayRoomDic[CFG_Room.RoomType.EXP].ScoreDic[i] = new XML_Config.XML_DailyMap.ScoreItem
            {
                key = _element.GetAttribute("key"),
                value = int.Parse(_element.GetAttribute("value")),
            };
        }

        _xmlConfig.DayRoomDic.Add(CFG_Room.RoomType.QHS, new XML_Config.XML_DailyMap
        {
            challengeTimeNum = GetIntXML(baseValueElement, "strengthenstonemap/challengetime", "num"),
            challengeTimeTime = GetIntXML(baseValueElement, "strengthenstonemap/challengetime", "time"),
            viplimit = GetIntXML(baseValueElement, "strengthenstonemap/challengetime", "viplimit"),
            doubleaccessID = GetIntXML(baseValueElement, "strengthenstonemap/doubleaccess", "id"),
            doubleaccessNum = GetIntXML(baseValueElement, "strengthenstonemap/doubleaccess", "num"),
            quickaccessScore = GetIntXML(baseValueElement, "strengthenstonemap/quickaccess", "score"),
            totlecycleNum = GetIntXML(baseValueElement, "strengthenstonemap/totlecycle", "num"),
        });

        _xmlConfig.DayRoomDic.Add(CFG_Room.RoomType.SY, new XML_Config.XML_DailyMap
        {
            challengeTimeNum = GetIntXML(baseValueElement, "skillpagemap/challengetime", "num"),
            challengeTimeTime = GetIntXML(baseValueElement, "skillpagemap/challengetime", "time"),
            viplimit = GetIntXML(baseValueElement, "skillpagemap/challengetime", "viplimit"),
            doubleaccessID = GetIntXML(baseValueElement, "skillpagemap/doubleaccess", "id"),
            doubleaccessNum = GetIntXML(baseValueElement, "skillpagemap/doubleaccess", "num"),
            quickaccessScore = GetIntXML(baseValueElement, "skillpagemap/quickaccess", "score"),
            totlecycleNum = GetIntXML(baseValueElement, "skillpagemap/totlecycle", "num"),
        });

        _xmlConfig.DayRoomDic.Add(CFG_Room.RoomType.SXS, new XML_Config.XML_DailyMap
        {
            challengeTimeNum = GetIntXML(baseValueElement, "starstonemap/challengetime", "num"),
            challengeTimeTime = GetIntXML(baseValueElement, "starstonemap/challengetime", "time"),
            viplimit = GetIntXML(baseValueElement, "starstonemap/challengetime", "viplimit"),
            doubleaccessID = GetIntXML(baseValueElement, "starstonemap/doubleaccess", "id"),
            doubleaccessNum = GetIntXML(baseValueElement, "starstonemap/doubleaccess", "num"),
        });

        _xmlConfig.DayRoomDic.Add(CFG_Room.RoomType.HS, new XML_Config.XML_DailyMap
        {
            challengeTimeNum = GetIntXML(baseValueElement, "soulstonemap/challengetime", "num"),
            challengeTimeTime = GetIntXML(baseValueElement, "soulstonemap/challengetime", "time"),
            viplimit = GetIntXML(baseValueElement, "soulstonemap/challengetime", "viplimit"),
            doubleaccessID = GetIntXML(baseValueElement, "soulstonemap/doubleaccess", "id"),
            doubleaccessNum = GetIntXML(baseValueElement, "soulstonemap/doubleaccess", "num"),
        });

        KeyNodes = xmlDoc.SelectNodes("/Config/partener/forge/charge");
        _xmlConfig.PartenerPayRecastingDic = new Dictionary<int, (int, int)>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PartenerPayRecastingDic.Add(int.Parse(_element.GetAttribute("quality")), (int.Parse(_element.GetAttribute("id")), int.Parse(_element.GetAttribute("num"))));
        }

        KeyNodes = xmlDoc.SelectNodes("/Config/partener/exp/range");

        _xmlConfig.PartenerEXPBeadDic = new Dictionary<int, int>();
        for (int i = 0; i < KeyNodes.Count; i++)
        {
            XmlElement _element = (XmlElement)KeyNodes[i];
            _xmlConfig.PartenerEXPBeadDic.Add(int.Parse(_element.GetAttribute("id")), int.Parse(_element.GetAttribute("num")));
        }

        #endregion

        #region 行会

        baseValueElement = (XmlElement)xmlDoc.SelectSingleNode("/Config/Guild");

        _xmlConfig.Guild = new XML_Config.XML_Item_Guild
        {
            match = new XML_Config.XML_Item_Match
            {
                guildnum = GetIntXML(baseValueElement, "match", "guildnum"),
                applicantnum = GetIntXML(baseValueElement, "match", "applicantnum"),
            },

            wishvalue = new XML_Config.XML_Item_Wishvalue
            {
                refreshtime = GetIntXML(baseValueElement, "wishvalue", "refreshtime"),
                maxvalue = GetIntXML(baseValueElement, "wishvalue", "maxvalue"),
                getitemnum = GetIntXML(baseValueElement, "wishvalue", "getitemnum"),
            },

            creatcost = new XML_Config.XML_Item_Creatcost
            {
                itemid = GetIntXML(baseValueElement, "creatcost", "itemid"),
                neednum = GetIntXML(baseValueElement, "creatcost", "neednum"),
            },

            interlimitTime = GetIntXML(baseValueElement, "interlimit", "time"),

            golddonate = new XML_Config.XML_Item_Golddonate
            {
                needitemid = GetIntXML(baseValueElement, "golddonate", "needitemid"),
                needitemnum = GetIntXML(baseValueElement, "golddonate", "needitemnum"),
                getitemnum = GetIntXML(baseValueElement, "golddonate", "getitemnum"),
                getguildexp = GetIntXML(baseValueElement, "golddonate", "getguildexp"),
                getguildexpid = GetIntXML(baseValueElement, "golddonate", "getguildexpid"),
            },

            moneydonate = new XML_Config.XML_Item_Moneydonate
            {
                needitemid = GetIntXML(baseValueElement, "moneydonate", "needitemid"),
                needitemnum = GetIntXML(baseValueElement, "moneydonate", "needitemnum"),
                getitemnum = GetIntXML(baseValueElement, "moneydonate", "getitemnum"),
                getguildexp = GetIntXML(baseValueElement, "moneydonate", "getguildexp"),
                getguildexpid = GetIntXML(baseValueElement, "moneydonate", "getguildexpid"),
            },

            donatelimitDailytime = GetIntXML(baseValueElement, "donatelimit", "dailytime"),

            guildcopy = new XML_Config.XML_Item_Guildcopy
            {
                starttime = GetStrXML(baseValueElement, "guildcopy", "starttime"),
                endtime = GetStrXML(baseValueElement, "guildcopy", "endtime"),
                lasttime = GetStrXML(baseValueElement, "guildcopy", "lasttime"),
            },
        };
        
        baseValueElement = (XmlElement)xmlDoc.SelectSingleNode("/Config/partener");

        _xmlConfig.Partener = new XML_Config.XML_Item_Partener
        {
            forgeRate = GetIntXML(baseValueElement, "forge/free", "rate"),
            //forgeChargeId = GetIntXML(baseValueElement, "forge/charge", "id"),
            //forgeChargenum = GetIntXML(baseValueElement, "forge/charge", "num"),

            decompose = new XML_Config.XML_Item_Decompose
            {
                quality = GetIntXML(baseValueElement, "decompose/stage", "quality"),
                starlevel = GetIntXML(baseValueElement, "decompose/stage", "starlevel"),
                rate = GetIntXML(baseValueElement, "decompose/stage", "rate"),
                id = GetIntXML(baseValueElement, "decompose/stage", "id"),
            }
        };
        #endregion
    }

    /// <summary>
    /// 获取伙伴槽位的解锁等级
    /// </summary>
    /// <param name="Lv"></param>
    /// <returns></returns>
    public static int GetOpenSolt(int Lv)
    {
        int openCount = 0;
        foreach (var item in xmlConfig.OpenSlotDic)
        {
            if (item.Value <= Lv)
            {
                openCount = item.Key;
            }
        }
        return openCount;
    }

    /// <summary>
    /// 检查开服后伙伴槽位是否开启。
    /// </summary>
    /// <returns></returns>
    public static bool CheckPartnerSoltIsOpenServer(int slot, bool showTip = true)
    {
        //int openDay = GetPartnerSoltOpenDay(slot);
        //long serverOpeningTome = GameUtil.Instance.GetServerOpeningTime();
        //bool islock = true;
        //if (serverOpeningTome >= 0)
        //{
        //    int day = GameUtil.Instance.GetTimeToD(serverOpeningTome);
        //    return day < openDay - 1;
        //}
        //return islock;

        // 今天零点距离开服时间的秒数
        long sec =GameUtil.Instance.GetNowToTimeSecoud(1, 0) + GameUtil.Instance.GetCurrentTimeSeconds() - GameUtil.Instance.GetServerOpenTime();
        // 没到开服时间
        if (GameUtil.Instance.GetNowToTimeSecoud(1, 0) > sec)
        {
            if (showTip)
                UITool.ShowTips($"未到开服时间");

            return false;
        }

        // 开服第一天0点到开服时间的秒数
        int sec_firstDay0 = (int)(sec % 86400);
        // 开服的第几天
        int dayOpenServer = (int)(sec - sec_firstDay0) / 86400 + 1;
        // 功能在开服后第几天开启。
        int funcOpenDay = GetPartnerSoltOpenDay(slot);

        bool isLock = dayOpenServer < funcOpenDay;
        if (isLock && showTip)
            UITool.ShowTips($"{funcOpenDay - dayOpenServer}天后开启");

        return isLock;
    }

    public static int RemainingDaysPartnerSlotUnlockOpen(int slot)
    {
        // 今天零点距离开服时间的秒数
        long sec = GameUtil.Instance.GetNowToTimeSecoud(1, 0) + GameUtil.Instance.GetCurrentTimeSeconds() - GameUtil.Instance.GetServerOpenTime();
        // 没到开服时间
        if (GameUtil.Instance.GetNowToTimeSecoud(1, 0) > sec)
        {
            return -1;
        }

        // 开服第一天0点到开服时间的秒数
        int sec_firstDay0 = (int)(sec % 86400);
        // 开服的第几天
        int dayOpenServer = (int)(sec - sec_firstDay0) / 86400 + 1;
        // 功能在开服后第几天开启。
        int funcOpenDay = GetPartnerSoltOpenDay(slot);
        return funcOpenDay - dayOpenServer;
    }

    public static int GetOpenSoltLv(int idx)
    {
        return xmlConfig.OpenSlotDic[idx];
    }

    public static int GetPartnerSoltOpenDay(int idx)
    {
        return xmlConfig.OpenDaySlotDic[idx];
    }

    private static float GetFloatXML(XmlElement root, string keyFirst, string keySecound)
    {
        return float.Parse(root.SelectSingleNode($"{keyFirst}[@{keySecound}]").Attributes[$"{keySecound}"].Value);
    }

    private static int GetIntXML(XmlElement root, string keyFirst, string keySecound)
    {
        return int.Parse(root.SelectSingleNode($"{keyFirst}[@{keySecound}]").Attributes[$"{keySecound}"].Value);
    }

    private static string GetStrXML(XmlElement root, string keyFirst, string keySecound)
    {
        return root.SelectSingleNode($"{keyFirst}[@{keySecound}]").Attributes[$"{keySecound}"].Value;
    }
}

