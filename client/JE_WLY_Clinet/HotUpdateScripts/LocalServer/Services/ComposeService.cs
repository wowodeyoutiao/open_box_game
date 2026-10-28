using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 道具合成，对齐原服 Player.compose。
    ///
    /// 原服有两条分支，本游戏**两条都有配置在用**（所以都要实现）：
    ///   ① 可堆叠分支（item.Maxnum &gt; 1）：魂石 10001..10300、材料 25..124
    ///   ② 按 Uid 分支（Maxnum == 1）：装备合成，compose.json 里有 242 个装备原料
    ///
    /// 两条分支判定流程几乎一样，但有四处**故意不同**（照抄原服，不是笔误）：
    ///   · 金币消耗：可堆叠 = NeedGold.Num × 材料数 / NeedNum；
    ///               按 Uid = NeedGold.Num（只扣一份，不乘材料数）
    ///   · 成功率：可堆叠是「每次合成各自 roll 一次」；
    ///             按 Uid 是「整批只 roll 一次」，成功后再按次数抽产出
    ///   · 扣减时机：**先扣金币、再扣材料，然后才 roll**
    ///   · 全失败：返回 ComposeError，但金币和材料不退
    ///
    /// 魂石合成额外有一条：产出 Quality &gt; 4 且带自然系时发全服跑马灯
    /// （LampGemSynthesis）。单机没有全服频道，跳过，见 Compose 里的注释。
    /// </summary>
    public class ComposeService : SingletonTemplate<ComposeService>
    {
        private const int BaseRate = 10000;

        /// <summary>测试钩子：&gt;= 0 时用固定权重代替随机数（0 = 必成，9999 = 必败）。</summary>
        public int ForcedComposeWeight = -1;

        private readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 原料 → ComposeID 的反查表（对齐原服 compose() 开头建的 whileAll）。
        /// 同一个原料 id 只登记第一条，和原服 `whileAll[v2] = v.ComposeID` 的覆盖顺序一致。
        /// </summary>
        private static Dictionary<int, int> _rawToCompose;

        private static int GetComposeIdByRaw(int itemCfgId)
        {
            if (_rawToCompose == null)
            {
                var map = new Dictionary<int, int>();
                try
                {
                    List<CFGItem_Compose> list = CFG_Compose.Get().DicList;
                    for (int i = 0; i < list.Count; i++)
                    {
                        CFGItem_Compose cfg = list[i];
                        if (cfg == null || cfg.Raw == null)
                            continue;
                        for (int j = 0; j < cfg.Raw.Length; j++)
                        {
                            int raw = cfg.Raw[j];
                            if (raw > 0 && !map.ContainsKey(raw))
                                map[raw] = cfg.ComposeID;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Log.PrintWarning("[ComposeService] 读取 compose 配置失败: " + e.Message);
                }
                _rawToCompose = map;
            }
            int composeId;
            return _rawToCompose.TryGetValue(itemCfgId, out composeId) ? composeId : 0;
        }

        /// <summary>
        /// 某个道具能参与的所有 ComposeID（宠物合成用）：
        /// 原服 petMove 里先建 whileAll[raw] = []ComposeID，再看两只宠物的道具 id 有没有共同档位，
        /// 有多档时取 Raw 最短的一档。注意和 GetComposeIdByRaw（只留第一条）不同，这里要全量。
        /// </summary>
        public static List<int> GetComposeIdsByRaw(int itemCfgId)
        {
            var result = new List<int>();
            if (itemCfgId <= 0)
                return result;
            try
            {
                List<CFGItem_Compose> list = CFG_Compose.Get().DicList;
                for (int i = 0; i < list.Count; i++)
                {
                    CFGItem_Compose cfg = list[i];
                    if (cfg == null || cfg.Raw == null)
                        continue;
                    for (int j = 0; j < cfg.Raw.Length; j++)
                    {
                        if (cfg.Raw[j] == itemCfgId)
                        {
                            if (!result.Contains(cfg.ComposeID))
                                result.Add(cfg.ComposeID);
                            break;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.PrintWarning("[ComposeService] 读取 compose 配置失败: " + e.Message);
            }
            return result;
        }

        /// <summary>合成入口，对齐原服 Manager.Compose → Player.compose。</summary>
        public ComposeResponse Compose(ComposeRequest req)
        {
            var response = new ComposeResponse { Code = -1, Error = "" };

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.Error = "没有角色";
                return response;
            }
            if (req == null || req.Info == null || req.Info.Count == 0 || req.Info[0] == null)
            {
                response.Error = "合成材料为空";
                return response;
            }

            // 原服先看第一个材料的 Uid 决定走哪条分支
            ItemInfo firstItem = FindByUid(role, req.Info[0].Uid);
            if (firstItem == null)
            {
                response.Error = "道具不存在";
                return response;
            }

            CFGItem_Item firstCfg;
            try { firstCfg = CFG_Item.GetById(firstItem.CfgId); }
            catch { firstCfg = null; }
            if (firstCfg == null)
            {
                response.Error = "道具不存在";
                return response;
            }

            int composeId = GetComposeIdByRaw(firstItem.CfgId);
            if (composeId <= 0)
            {
                response.Error = "该道具不能合成";
                return response;
            }

            CFGItem_Compose composeCfg;
            try { composeCfg = CFG_Compose.GetById(composeId); }
            catch { composeCfg = null; }
            if (composeCfg == null)
            {
                response.Error = "合成配置不存在";
                return response;
            }
            if (composeCfg.NeedGold == null || composeCfg.Produced == null || composeCfg.Produced.Length == 0)
            {
                response.Error = "合成配置不完整";
                return response;
            }

            int needNum = (int)composeCfg.NeedNum;
            if (needNum <= 0)
            {
                response.Error = "合成配置不完整";
                return response;
            }

            // 原服：ItemCfg.Maxnum > 1 → 按配置 id 处理（可堆叠）；否则按道具 Uid 处理
            bool stackable = firstCfg.Maxnum.HasValue && (int)firstCfg.Maxnum.Value > 1;
            if (stackable)
                return ComposeStackable(response, role, req, composeCfg, needNum);
            return ComposeByUid(response, role, req, composeCfg, needNum);
        }

        /// <summary>可堆叠分支：魂石 / 材料。</summary>
        private ComposeResponse ComposeStackable(
            ComposeResponse response, LocalRole role, ComposeRequest req,
            CFGItem_Compose composeCfg, int needNum)
        {
            int sourceLen = 0;
            bool sameKind = true;
            var del = new List<KeyValuePair<int, long>>();   // cfgId → 数量

            for (int i = 0; i < req.Info.Count; i++)
            {
                ComposeInfo info = req.Info[i];
                if (info == null || info.Num <= 0)
                {
                    response.Error = "道具数量不足";
                    return response;
                }

                ItemInfo item = FindByUid(role, info.Uid);
                if (item == null)
                {
                    response.Error = "道具不存在";
                    return response;
                }
                if (item.Num < info.Num)
                {
                    response.Error = "道具数量不足";
                    return response;
                }
                // 必须全是同一个 ComposeID 的原料（魂石 5 种自然系都映射到同一档）
                if (GetComposeIdByRaw(item.CfgId) != composeCfg.ComposeID)
                    sameKind = false;

                sourceLen += info.Num;
                del.Add(new KeyValuePair<int, long>(item.CfgId, info.Num));
            }

            if (needNum <= 0 || sourceLen % needNum != 0 || !sameKind)
            {
                response.Error = "道具数量不足";
                return response;
            }

            // 原服：金币按「合成次数」分摊 —— NeedGold.Num × 材料数 / NeedNum
            long needGold = (long)composeCfg.NeedGold.num * sourceLen / needNum;
            int goldId = composeCfg.NeedGold.id;

            if (ItemService.CountItem(role, goldId) < needGold)
            {
                response.Error = "道具数量不足";
                return response;
            }
            ItemService.ConsumeItem(role, goldId, needGold);
            for (int i = 0; i < del.Count; i++)
                ItemService.ConsumeItem(role, del[i].Key, del[i].Value);

            // 逐次判定：每合成一次独立 roll
            int times = sourceLen / needNum;
            var send = new Dictionary<int, int>();
            int successRate = (int)composeCfg.SuccessRate;
            for (int i = 0; i < times; i++)
            {
                int weight = ForcedComposeWeight >= 0 ? ForcedComposeWeight : _rng.Next(BaseRate);
                if (weight >= successRate)
                    continue;
                int productId = RollProduced(composeCfg);
                if (productId <= 0)
                    continue;
                int have;
                send[productId] = send.TryGetValue(productId, out have) ? have + 1 : 1;
                // 原服：Quality>4 且带自然系的魂石合成成功 → 跑马灯（单机无全服频道，跳过）
            }

            return Finish(response, role, send, "合成失败（材料已消耗）");
        }

        /// <summary>按 Uid 分支：装备合成。</summary>
        private ComposeResponse ComposeByUid(
            ComposeResponse response, LocalRole role, ComposeRequest req,
            CFGItem_Compose composeCfg, int needNum)
        {
            int sourceLen = 0;
            bool sameKind = true;
            var del = new List<KeyValuePair<long, int>>();   // uid → 数量

            for (int i = 0; i < req.Info.Count; i++)
            {
                ComposeInfo info = req.Info[i];
                if (info == null || info.Num <= 0)
                {
                    response.Error = "道具数量不足";
                    return response;
                }

                ItemInfo item = FindByUid(role, info.Uid);
                if (item == null)
                {
                    response.Error = "道具不存在";
                    return response;
                }
                if (item.Num < info.Num)
                {
                    response.Error = "道具数量不足";
                    return response;
                }
                if (GetComposeIdByRaw(item.CfgId) != composeCfg.ComposeID)
                    sameKind = false;

                sourceLen += info.Num;
                del.Add(new KeyValuePair<long, int>(info.Uid, info.Num));
            }

            if (needNum <= 0 || sourceLen % needNum != 0 || !sameKind)
            {
                response.Error = "道具数量不足";
                return response;
            }

            // 原服这条分支**只扣一份金币**，不乘材料数（和可堆叠分支不一样）
            long needGold = composeCfg.NeedGold.num;
            int goldId = composeCfg.NeedGold.id;
            if (ItemService.CountItem(role, goldId) < needGold)
            {
                response.Error = "道具数量不足";
                return response;
            }
            ItemService.ConsumeItem(role, goldId, needGold);

            for (int i = 0; i < del.Count; i++)
                ConsumeByUid(role, del[i].Key, del[i].Value);

            // 整批只 roll 一次
            int successRate = (int)composeCfg.SuccessRate;
            int weight = ForcedComposeWeight >= 0 ? ForcedComposeWeight : _rng.Next(BaseRate);
            if (weight >= successRate)
                return Finish(response, role, null, "合成失败（材料已消耗）");

            int times = sourceLen / needNum;
            var send = new Dictionary<int, int>();
            for (int i = 0; i < times; i++)
            {
                int productId = RollProduced(composeCfg);
                if (productId <= 0)
                    continue;
                int have;
                send[productId] = send.TryGetValue(productId, out have) ? have + 1 : 1;
            }
            return Finish(response, role, send, "合成失败（材料已消耗）");
        }

        /// <summary>
        /// 抽产出：Produced 多段按 ratio 累加分段，段内再随机一个 id。
        /// 对齐原服 composeRand：rand = RandIn(total) + 1，落在 [1, total]。
        /// </summary>
        public int RollProduced(CFGItem_Compose composeCfg)
        {
            CFGItem_Compose.ProducedItem[] produced = composeCfg.Produced;
            if (produced == null || produced.Length == 0)
                return 0;

            int total = 0;
            for (int i = 0; i < produced.Length; i++)
            {
                if (produced[i] != null && produced[i].ratio > 0)
                    total += produced[i].ratio;
            }
            if (total <= 0)
                return 0;

            int rand = _rng.Next(total) + 1;
            int begin = 0;
            for (int i = 0; i < produced.Length; i++)
            {
                CFGItem_Compose.ProducedItem p = produced[i];
                if (p == null || p.ratio <= 0)
                    continue;
                if (begin + p.ratio >= rand)
                {
                    if (p.id == null || p.id.Length == 0)
                        return 0;
                    return p.id[_rng.Next(p.id.Length)];
                }
                begin += p.ratio;
            }
            return 0;
        }

        /// <summary>发产出 + 收尾（存档 / 推送）。产出为空按失败处理（原服返回 ComposeError）。</summary>
        private static ComposeResponse Finish(
            ComposeResponse response, LocalRole role, Dictionary<int, int> send, string failMessage)
        {
            bool any = false;
            if (send != null)
            {
                foreach (KeyValuePair<int, int> kv in send)
                {
                    if (kv.Key <= 0 || kv.Value <= 0)
                        continue;
                    ItemService.Instance.GrantReward(role, kv.Key, kv.Value);
                    response.NewConpose.Add(new ComposeInfo { Uid = kv.Key, Num = kv.Value });
                    any = true;
                }
            }

            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushUserBaseNow();

            if (!any)
            {
                response.Code = -2;
                response.Error = failMessage;
                return response;
            }
            response.Code = 0;
            return response;
        }

        // ── 背包按 Uid 读写（装备类 Maxnum == 1，必须按实例操作）─────────────

        private static ItemInfo FindByUid(LocalRole role, long uid)
        {
            if (role == null || role.BagData == null || role.BagData.ItemList == null)
                return null;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item != null && item.Uid == uid)
                    return item;
            }
            return null;
        }

        private static bool ConsumeByUid(LocalRole role, long uid, int num)
        {
            if (role == null || role.BagData == null || role.BagData.ItemList == null || num <= 0)
                return false;
            for (int i = 0; i < role.BagData.ItemList.Count; i++)
            {
                ItemInfo item = role.BagData.ItemList[i];
                if (item == null || item.Uid != uid)
                    continue;
                if (item.Num < num)
                    return false;
                item.Num -= num;
                if (item.Num <= 0)
                    role.BagData.ItemList.RemoveAt(i);
                return true;
            }
            return false;
        }
    }
}
