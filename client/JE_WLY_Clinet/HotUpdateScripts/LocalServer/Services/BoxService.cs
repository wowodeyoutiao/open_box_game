using System.Collections.Generic;
using JEngine.Core;
using JEngine.Helper;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 大厅宝箱本地服务。
    ///
    /// 规则：
    /// - 大厅击杀怪物累计开箱次数 TreasuerBox；
    /// - 开箱使用 boxlevel 的 Maxtime / qualityrandom；
    /// - 掉落使用 openbox.json，按角色等级和职业筛选；
    /// - 开箱结果先进入 BoxOpenItems，领取后才进入背包；
    /// - 宝箱升级使用 boxlevel.Needtime，升级完成后等级 +1。
    /// </summary>
    public class BoxService : SingletonTemplate<BoxService>
    {
        private static long NowUnix()
        {
            return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>处理已经到时间的宝箱升级。</summary>
        public bool Refresh(LocalRole role)
        {
            if (role == null || role.BoxUpgradeEndUnix <= 0)
                return false;
            if (role.BoxUpgradeEndUnix > NowUnix())
                return false;

            int maxLevel = CFG_BoxLevel.GetMaxLevel();
            if (role.BoxLevel < maxLevel)
                role.BoxLevel++;
            role.BoxUpgradeEndUnix = 0;

            SyncMirror(role);
            SaveCoordinator.Instance.MarkDirty();
            Log.Print("[BoxService] 宝箱升级完成 level=" + role.BoxLevel);
            return true;
        }

        /// <summary>击杀怪物累计开箱次数。</summary>
        public void AddTreasure(int amount)
        {
            if (amount <= 0)
                return;

            LocalData d = LocalData.Instance;
            LocalRole role = d.Current;
            if (role == null)
                return;

            role.TreasuerBox += amount;
            d.TreasuerBox = role.TreasuerBox;
            SaveCoordinator.Instance.MarkDirty();
            LocalPusher.Instance.PushUserBaseNow();
        }

        public HomeOpenBoxResponse OpenBox(byte[] req)
        {
            HomeOpenBoxRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<HomeOpenBoxRequest>(req);
            }
            catch (System.Exception e)
            {
                Log.PrintWarning("[BoxService] 开箱请求解析失败: " + e.Message);
                return new HomeOpenBoxResponse { Code = -1, Error = "请求解析失败" };
            }

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return new HomeOpenBoxResponse { Code = -1, Error = "没有角色" };

            if (role.BoxOpenItems == null)
                role.BoxOpenItems = new List<ItemInfo>();
            if (role.BoxOpenItems.Count > 0)
                return new HomeOpenBoxResponse { Code = -2, Error = "请先领取上一次开箱结果" };

            Refresh(role);
            CFGItem_BoxLevel boxCfg = GetBoxCfg(role.BoxLevel);
            if (boxCfg == null)
                return new HomeOpenBoxResponse { Code = -1, Error = "宝箱配置不存在" };

            int openNum = request.Num;
            if (openNum <= 0)
                return new HomeOpenBoxResponse { Code = -3, Error = "开箱数量无效" };
            if (openNum > boxCfg.Maxtime)
                return new HomeOpenBoxResponse { Code = -3, Error = "超过宝箱最大开启数量" };
            if (role.TreasuerBox < openNum)
                return new HomeOpenBoxResponse { Code = -3, Error = "开启次数不足" };

            int level = request.level > 0 ? request.level : role.Level;
            if (level > role.Level)
                return new HomeOpenBoxResponse { Code = -4, Error = "开箱等级不能超过角色等级" };
            int job = request.Job;

            Log.Print("[BoxService] OpenBox raw num=" + request.Num
                + " treasure=" + role.TreasuerBox + " boxLevel=" + role.BoxLevel);
            var generated = new ItemInfo[openNum];
            Log.Print("[BoxService] OpenBox array created=" + (generated != null));
            var rng = new System.Random(unchecked((int)NowUnix()) ^ role.Uid.GetHashCode() ^ openNum);
            for (int i = 0; i < openNum; i++)
            {
                ItemInfo item = CreateRandomItem(role, level, job, role.BoxLevel, rng);
                if (item == null)
                    return new HomeOpenBoxResponse { Code = -1, Error = "开箱掉落配置异常" };
                // 不走 ILRuntime 的数组下标赋值指令，避开 ArraySetValue 的空引用问题。
                generated.SetValue(item, i);
            }

            // ILRuntime 对热更泛型 List.Add 的绑定不稳定，用 Enumerable.ToList 生成正式 List。
            Log.Print("[BoxService] OpenBox before ToList count=" + generated.Length);
            var result = System.Linq.Enumerable.ToList(generated);
            Log.Print("[BoxService] OpenBox resultType=" + result.GetType().FullName);

            Log.Print("[BoxService] OpenBox generated=" + result.Count
                + " level=" + level + " job=" + job + " boxLevel=" + role.BoxLevel);

            if (!CanAddBatchToBag(role, result, out string bagError))
                return new HomeOpenBoxResponse { Code = -5, Error = bagError };

            // 前面已确认待领取列表为空，直接替换，避免 ILRuntime 泛型 List.AddRange 的绑定问题。
            role.BoxOpenItems = result;
            role.TreasuerBox -= openNum;
            LocalData.Instance.TreasuerBox = role.TreasuerBox;
            Log.Print("[BoxService] OpenBox save pending=" + role.BoxOpenItems.Count);
            SaveCoordinator.Instance.SaveNow();
            Log.Print("[BoxService] OpenBox push count");
            LocalPusher.Instance.PushUserBaseNow();

            return new HomeOpenBoxResponse
            {
                Code = 0,
                Error = "",
                // 响应必须与权威待领取列表分离，否则领取时客户端和本地会同时从同一个 List 删除。
                ItemInfo = CopyItems(result),
            };
        }

        public BoxResIntoBagResponse BoxIntoBag(byte[] req)
        {
            BoxResIntoBagRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<BoxResIntoBagRequest>(req);
            }
            catch (System.Exception e)
            {
                Log.PrintWarning("[BoxService] 领取开箱结果解析失败: " + e.Message);
                return new BoxResIntoBagResponse { Code = -1, Error = "请求解析失败" };
            }

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return new BoxResIntoBagResponse { Code = -1, Error = "没有角色" };

            if (role.BoxOpenItems == null)
                role.BoxOpenItems = new List<ItemInfo>();
            Refresh(role);

            ItemInfo item = null;
            for (int i = 0; i < role.BoxOpenItems.Count; i++)
            {
                if (role.BoxOpenItems[i] != null && role.BoxOpenItems[i].Uid == request.Uid)
                {
                    item = role.BoxOpenItems[i];
                    break;
                }
            }
            if (item == null)
                return new BoxResIntoBagResponse { Code = -2, Error = "开箱结果不存在", Uid = request.Uid };

            string error;
            if (!EquipRuleService.CanAddToBag(role, item, out error))
                return new BoxResIntoBagResponse { Code = -3, Error = error, Uid = request.Uid };

            role.BoxOpenItems.Remove(item);
            if (role.BagData == null)
                role.BagData = new Bag { ItemList = new List<ItemInfo>() };
            role.BagData.ItemList.Add(item);
            SaveCoordinator.Instance.SaveNow();

            return new BoxResIntoBagResponse
            {
                Code = 0,
                Error = "",
                Uid = request.Uid,
            };
        }

        public CommonResponse BoxSetting(byte[] req)
        {
            // 客户端设置主要保存在本地 PlayerPrefs，这里保持成功以兼容原协议。
            return Resp(CmdId.C2S_HomeBoxSetting, 0, "");
        }

        public CommonResponse UpgradeBox(byte[] req)
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return Resp(CmdId.C2S_HomeUpgradeBox, -1, "没有角色");

            Refresh(role);
            long now = NowUnix();
            if (role.BoxUpgradeEndUnix > now)
                return Resp(CmdId.C2S_HomeUpgradeBox, -2, "宝箱正在升级中");

            int maxLevel = CFG_BoxLevel.GetMaxLevel();
            if (role.BoxLevel >= maxLevel)
                return Resp(CmdId.C2S_HomeUpgradeBox, -3, "宝箱等级已满");

            CFGItem_BoxLevel cfg = GetBoxCfg(role.BoxLevel);
            if (cfg == null)
                return Resp(CmdId.C2S_HomeUpgradeBox, -1, "宝箱配置不存在");
            if (role.Level < cfg.Rolelevel)
                return Resp(CmdId.C2S_HomeUpgradeBox, -4, "角色等级不足");

            if (cfg.Consume != null && cfg.Consume.Length > 0)
            {
                CFGItem_BoxLevel.ConsumeItem consume = cfg.Consume[0];
                if (consume.id == 1)
                {
                    if (role.Coin < consume.num)
                        return Resp(CmdId.C2S_HomeUpgradeBox, -5, "金币不足");
                    role.Coin -= consume.num;
                    LocalData.Instance.Gold = role.Coin;
                }
                else
                {
                    return Resp(CmdId.C2S_HomeUpgradeBox, -5, "升级材料暂未接入");
                }
            }

            role.BoxUpgradeEndUnix = now + cfg.Needtime;
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushBoxNow();
            return Resp(CmdId.C2S_HomeUpgradeBox, 0, "");
        }

        public CommonResponse UpgradeBoxQuick(byte[] req)
        {
            HomeUpgradeBoxQuickRequest request;
            try
            {
                request = JEngine.MyNet.TCPClientMgr.ProtoDeSerialize<HomeUpgradeBoxQuickRequest>(req);
            }
            catch (System.Exception e)
            {
                Log.PrintWarning("[BoxService] 宝箱加速请求解析失败: " + e.Message);
                return Resp(CmdId.C2S_HomeUpgradeBoxQuick, -1, "请求解析失败");
            }

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return Resp(CmdId.C2S_HomeUpgradeBoxQuick, -1, "没有角色");

            Refresh(role);
            long now = NowUnix();
            if (role.BoxUpgradeEndUnix <= now)
                return Resp(CmdId.C2S_HomeUpgradeBoxQuick, -2, "当前没有进行中的升级");

            int num = request.Num;
            if (num <= 0)
                num = 1;
            if (role.BoxQuick < num)
                return Resp(CmdId.C2S_HomeUpgradeBoxQuick, -3, "加速券不足");

            role.BoxQuick -= num;
            int accelerate = 300;
            try
            {
                if (CFG_XMLConfig.xmlConfig != null
                    && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.BoxLevelUp_accelerate > 0)
                    accelerate = CFG_XMLConfig.xmlConfig.BaseValue.BoxLevelUp_accelerate;
            }
            catch { }

            role.BoxUpgradeEndUnix = System.Math.Max(now, role.BoxUpgradeEndUnix - (long)accelerate * num);
            Refresh(role);
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushBoxNow();
            return Resp(CmdId.C2S_HomeUpgradeBoxQuick, 0, "");
        }

        public static List<ItemInfo> CopyItems(List<ItemInfo> source)
        {
            if (source == null)
                return new List<ItemInfo>();
            return System.Linq.Enumerable.ToList(source);
        }

        /// <summary>
        /// 无 UI、无存档副作用的开箱生成自检，供 LocalSelfCheck 使用。
        /// </summary>
        public static bool SelfCheck(LocalRole role, out string error)
        {
            error = "";
            if (role == null)
            {
                error = "没有角色";
                return false;
            }

            List<ItemInfo> oldItems = role.BoxOpenItems;
            long oldTreasure = role.TreasuerBox;
            int oldLevel = role.BoxLevel;
            long oldNextItemUid = LocalData.Instance.NextItemUid;
            try
            {
                role.BoxOpenItems = new List<ItemInfo>();
                role.TreasuerBox = 1;
                role.BoxLevel = 1;

                var generated = new ItemInfo[1];
                var item = CreateRandomItem(
                    role,
                    role.Level > 0 ? role.Level : 1,
                    role.Job,
                    role.BoxLevel,
                    new System.Random(12345));
                if (item == null)
                {
                    error = "随机开箱结果为 null";
                    return false;
                }

                generated.SetValue(item, 0);
                var opened = System.Linq.Enumerable.ToList(generated);
                if (opened.Count != 1 || opened[0] == null)
                {
                    error = "开箱列表生成失败";
                    return false;
                }

                role.BoxOpenItems = opened;
                var copy = CopyItems(opened);
                if (copy.Count != 1)
                {
                    error = "开箱结果副本失败";
                    return false;
                }

                Log.Print("[BoxService][SelfCheck] box open generate ok cfg=" + item.CfgId
                    + " type=" + item.Type + " list=" + opened.GetType().FullName);
                return true;
            }
            catch (System.Exception e)
            {
                error = e.ToString();
                return false;
            }
            finally
            {
                role.BoxOpenItems = oldItems;
                role.TreasuerBox = oldTreasure;
                role.BoxLevel = oldLevel;
                LocalData.Instance.NextItemUid = oldNextItemUid;
            }
        }

        private static CommonResponse Resp(CmdId cmd, int code, string error)
        {
            return new CommonResponse
            {
                Code = code,
                Error = error ?? "",
                RequestCmdId = (uint)cmd,
            };
        }

        private static CFGItem_BoxLevel GetBoxCfg(int level)
        {
            try
            {
                return CFG_BoxLevel.GetById(level > 0 ? level : 1);
            }
            catch
            {
                return null;
            }
        }

        private static ItemInfo CreateRandomItem(LocalRole role, int level, int job, int boxLevel, System.Random rng)
        {
            CFGItem_OpenBox openCfg = CFG_OpenBox.GetByLevel(level);
            if (openCfg == null || openCfg.Equiprandom == null)
                return null;

            int totalRate = 0;
            for (int i = 0; i < openCfg.Equiprandom.Length; i++)
            {
                CFGItem_OpenBox.EquipRandom candidate = openCfg.Equiprandom[i];
                if (job == 0 || candidate.job == job)
                    totalRate += candidate.rate > 0 ? candidate.rate : 0;
            }
            if (totalRate <= 0)
                return null;

            int roll = rng.Next(totalRate);
            int cursor = 0;
            int itemId = 0;
            for (int i = 0; i < openCfg.Equiprandom.Length; i++)
            {
                CFGItem_OpenBox.EquipRandom candidate = openCfg.Equiprandom[i];
                if (job != 0 && candidate.job != job)
                    continue;
                if (candidate.rate <= 0)
                    continue;
                cursor += candidate.rate;
                if (roll < cursor)
                {
                    itemId = candidate.id;
                    break;
                }
            }
            if (itemId == 0)
                return null;

            CFGItem_Item itemCfg;
            try
            {
                itemCfg = CFG_Item.GetById(itemId);
            }
            catch (System.Exception e)
            {
                Log.PrintWarning("[BoxService] 开箱道具配置不存在 cfg=" + itemId + " " + e.Message);
                return null;
            }
            if (itemCfg == null)
                return null;

            ItemType type = (ItemType)EquipRuleService.ToInt(itemCfg.Type);
            var item = new ItemInfo
            {
                CfgId = itemId,
                Uid = LocalData.Instance.AllocateItemUid(),
                Num = 1,
                CreateTime = NowUnix(),
                Type = type,
            };

            if (type == ItemType.EQUIPMENT)
            {
                item.Equip = new EquipInfo
                {
                    Quality = RollQuality(boxLevel, itemCfg, rng),
                    Attributes = new List<Stats>(),
                };
            }
            return item;
        }

        private static ItemQuality RollQuality(int boxLevel, CFGItem_Item itemCfg, System.Random rng)
        {
            CFGItem_BoxLevel cfg = GetBoxCfg(boxLevel);
            if (cfg == null || cfg.qualityrandom == null || cfg.qualityrandom.Length == 0)
                return ToQuality(EquipRuleService.ToInt(itemCfg.Quality));

            int total = 0;
            for (int i = 0; i < cfg.qualityrandom.Length; i++)
                total += cfg.qualityrandom[i].rate;
            if (total <= 0)
                return ToQuality(EquipRuleService.ToInt(itemCfg.Quality));

            int roll = rng.Next(total);
            int cursor = 0;
            for (int i = 0; i < cfg.qualityrandom.Length; i++)
            {
                cursor += cfg.qualityrandom[i].rate;
                if (roll < cursor)
                    return ToQuality(cfg.qualityrandom[i].id);
            }
            return ToQuality(EquipRuleService.ToInt(itemCfg.Quality));
        }

        private static ItemQuality ToQuality(int value)
        {
            if (value < (int)ItemQuality.WHITE)
                return ItemQuality.WHITE;
            if (value > (int)ItemQuality.Rainbow)
                return ItemQuality.Rainbow;
            return (ItemQuality)value;
        }

        private static bool CanAddBatchToBag(LocalRole role, List<ItemInfo> items, out string error)
        {
            error = "";
            if (role == null || items == null)
            {
                error = "数据异常";
                return false;
            }

            var bag = role.BagData;
            int currentTotal = bag != null && bag.ItemList != null ? bag.ItemList.Count : 0;
            int currentEquip = 0;
            int currentItem = 0;
            if (bag != null && bag.ItemList != null)
            {
                for (int i = 0; i < bag.ItemList.Count; i++)
                {
                    if (bag.ItemList[i] == null)
                        continue;
                    if (EquipRuleService.IsEquipment(bag.ItemList[i]))
                        currentEquip++;
                    else
                        currentItem++;
                }
            }

            int addEquip = 0;
            int addItem = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null)
                    continue;
                if (EquipRuleService.IsEquipment(items[i]))
                    addEquip++;
                else
                    addItem++;
            }

            int totalLimit = 500;
            int equipLimit = 200;
            int itemLimit = 300;
            try
            {
                CFG_XMLConfig.XML_Config config = CFG_XMLConfig.xmlConfig;
                if (config != null && config.BaseValue != null && config.BaseValue.bagNum != null)
                {
                    totalLimit = config.BaseValue.bagNum.totlenum;
                    equipLimit = config.BaseValue.bagNum.equipnum;
                    itemLimit = config.BaseValue.bagNum.itemnum;
                }
            }
            catch { }

            if (totalLimit > 0 && currentTotal + items.Count > totalLimit)
            {
                error = "背包已满";
                return false;
            }
            if (equipLimit > 0 && currentEquip + addEquip > equipLimit)
            {
                error = "装备背包已满";
                return false;
            }
            if (itemLimit > 0 && currentItem + addItem > itemLimit)
            {
                error = "道具背包已满";
                return false;
            }
            return true;
        }

        private static void SyncMirror(LocalRole role)
        {
            if (LocalData.Instance.Current == role)
            {
                LocalData.Instance.BoxLevel = role.BoxLevel;
                LocalData.Instance.TreasuerBox = role.TreasuerBox;
                LocalData.Instance.BoxUpgradeEndUnix = role.BoxUpgradeEndUnix;
                LocalData.Instance.BoxQuick = role.BoxQuick;
                LocalData.Instance.Gold = role.Coin;
            }
        }
    }
}
