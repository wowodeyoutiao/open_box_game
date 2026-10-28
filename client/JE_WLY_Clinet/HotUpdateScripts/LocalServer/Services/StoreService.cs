using System.Collections.Generic;
using HotUpdateScripts.ProtoM;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 商城服务（HTTP 通道 /v1/store/list + /v1/store/buy）。
    ///
    /// 对齐原服 internal/web/api/store.go 的 StoreList / StoreBuy：
    ///
    /// 【列表】
    /// 客户端只拿「已买多少」，商品详情全在本地 CFG_Store。
    ///   - buy        当前限购窗口内的购买量（limittype 决定窗口长度）
    ///   - buy_all_num 终生购买量
    /// 窗口长度（原服 StoreList 里的 day）：
    ///   limittype 0 → 终生
    ///             1 → 1 天（每日）
    ///             2 → 7 天（每周）
    ///             3 → 30 天（每月）
    ///             4 → 90 天（每季）
    ///             999 → 无限制，不统计
    ///
    /// 【购买】
    ///   1) storeType = goodsId / 1000（原服也是这么反推的）
    ///   2) 限购：limitnum != 0 且 buy + num > limitnum → 失败
    ///   3) 试炼商城（StoreType 5）：试炼塔层数 &lt; Prelimit → 失败
    ///   4) 扣货币：needmoney × Num × num（times &gt; 0 时前 times 次收费、之后免费）
    ///   5) 校验背包并发放 Solditems × num
    ///   6) 写购买记录
    ///
    /// 单机版不做：人民币支付、广告、特权卡/基金/守卫海岛等 StoreType 特例。
    /// </summary>
    public class StoreService : SingletonTemplate<StoreService>
    {
        /// <summary>试炼商城的 StoreType。</summary>
        private const int StoreTypeTrialReward = 5;

        /// <summary>无购买限制的 limittype（人民币/广告商品）。</summary>
        private const int LimitTypeUnlimited = 999;

        // ==================== 列表 ====================

        /// <summary>GET /v1/store/list 的本地实现，body 是 HTTP_StoreListRequset 的 JSON。</summary>
        public object GetList(string body)
        {
            var response = new HTTP_StoreListResponse
            {
                code = 0,
                data = new HTTP_StoreList[0],
            };

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.code = -1;
                return response;
            }

            int storeType = ParseIntField(body, "store_type", 0);
            long goodsUuid = ParseLongField(body, "goods_uuid", 0);

            var result = new List<HTTP_StoreList>();
            CFG_Store table = CFG_Store.Get();
            if (table != null && table.DicDatas != null)
            {
                foreach (KeyValuePair<long, CFGItem_Store> kv in table.DicDatas)
                {
                    CFGItem_Store cfg = kv.Value;
                    if (cfg == null)
                        continue;
                    // store_type == 0 表示要全部
                    if (storeType != 0 && cfg.StoreType != storeType)
                        continue;
                    if (goodsUuid > 0 && cfg.ID != goodsUuid)
                        continue;

                    int limitType = cfg.Limite == null ? 0 : cfg.Limite.limittype;
                    // 单条构造抽成独立方法：GetList 里同时持有多个 long 局部变量时，
                    // ILRuntime 的 RegisterVM 会走到未实现的路径（NotImplementedException）。
                    result.Add(BuildListItem(role, cfg, limitType));
                }
            }

            response.data = result.ToArray();
            return response;
        }

        /// <summary>构造列表里的一条记录（已购数量按 limittype 的时间窗口统计）。</summary>
        private static HTTP_StoreList BuildListItem(LocalRole role, CFGItem_Store cfg, int limitType)
        {
            var item = new HTTP_StoreList();
            item.goods_uuid = cfg.ID;
            item.buy = CountBought(role, cfg.ID, limitType);
            item.buy_all_num = (int)CountBought(role, cfg.ID, 0);
            item.date = 0;
            item.other_num = 0;
            return item;
        }

        // ==================== 购买 ====================

        /// <summary>PUT /v1/store/buy 的本地实现，body 是 HTTP_StoreOperateRequset 的 JSON。</summary>
        public object Buy(string body)
        {
            var response = new HTTP_StoreOperateResponse
            {
                code = -1,
                data = false,
                error = "",
            };

            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                response.error = "没有角色";
                return response;
            }

            long goodsId = ParseLongField(body, "goods_uuid", 0);
            int num = ParseIntField(body, "num", 1);
            if (num <= 0)
                num = 1;
            if (goodsId <= 0)
            {
                response.error = "商品不存在";
                return response;
            }

            CFGItem_Store cfg;
            try { cfg = CFG_Store.GetById((int)goodsId); }
            catch { cfg = null; }
            if (cfg == null)
            {
                response.error = "商品不存在";
                return response;
            }
            if (cfg.Limite == null || cfg.Solditems == null || cfg.Solditems.Length == 0)
            {
                response.error = "商品配置不完整";
                return response;
            }

            int limitType = cfg.Limite.limittype;

            // 1) 限购校验（原服 StoreBuy：LimitNum != 0 才校验）
            if (limitType != LimitTypeUnlimited && cfg.Limite.limitnum > 0)
            {
                long bought = CountBought(role, cfg.ID, limitType);
                if (bought + num > cfg.Limite.limitnum)
                {
                    response.error = "已达购买上限";
                    return response;
                }
            }

            // 2) 试炼商城额外校验试炼塔层数（原服 TrialTowerLevel = BattleId % 1000）
            if (cfg.StoreType == StoreTypeTrialReward)
            {
                int needLevel = 0;
                try { needLevel = (int)cfg.Prelimit; }
                catch { needLevel = 0; }
                int towerLevel = role.TrialTowerId % 1000;
                if (towerLevel < needLevel)
                {
                    response.error = "试炼塔层数不足（需要第 " + needLevel + " 层）";
                    return response;
                }
            }

            // 3) 计算本次实际要扣的货币数量（times 逻辑：前 times 次收费，之后免费）
            long totalCost = 0;
            int payTimes = CountPayTimes(role, cfg, num);
            if (payTimes > 0)
            {
                totalCost = (long)cfg.Limite.Num * payTimes;
            }

            int needMoney = cfg.Limite.needmoney;
            if (totalCost > 0)
            {
                if (ItemService.CountItem(role, needMoney) < totalCost)
                {
                    response.error = "货币不足";
                    return response;
                }
            }

            // 4) 扣货币（先校验再扣，保证失败不改数据）
            if (totalCost > 0 && !ItemService.ConsumeItem(role, needMoney, totalCost))
            {
                response.error = "货币不足";
                return response;
            }

            // 5) 发货
            var granted = new List<string>();
            for (int i = 0; i < cfg.Solditems.Length; i++)
            {
                CFGItem_Store.CFGItem_Solditem sold = cfg.Solditems[i];
                if (sold == null || sold.itemID <= 0 || sold.Num <= 0)
                    continue;
                long give = (long)sold.Num * num;
                if (!ItemService.Instance.GrantReward(role, sold.itemID, give))
                {
                    // 发不出去时把货币退回去，避免玩家白花
                    if (totalCost > 0)
                        ItemService.Instance.GrantReward(role, needMoney, totalCost);
                    response.error = "背包空间不足";
                    return response;
                }
                granted.Add("" + sold.itemID + "x" + give);
            }

            // 6) 写购买记录
            List<long> log = EnsureBuyLog(role);
            log.Add((long)cfg.ID);
            log.Add((long)num);
            log.Add(NowUnix());

            SaveCoordinator.Instance.MarkDirty();
            SaveCoordinator.Instance.SaveNow();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();

            response.code = 0;
            response.data = true;
            Log.Print("[StoreService] buy goods=" + cfg.ID + " num=" + num
                + " cost=" + totalCost + "(item " + needMoney + ") got=" + string.Join(",", granted.ToArray()));
            return response;
        }

        // ==================== 限购统计 ====================

        /// <summary>
        /// limittype → 统计窗口秒数。返回 0 表示终生。
        /// 对齐原服 StoreList 里 day 的取值：1/7/30/90，其余按终生处理。
        /// </summary>
        private static long LimitWindowSeconds(int limitType)
        {
            switch (limitType)
            {
                case 1: return 86400L;
                case 2: return 86400L * 7;
                case 3: return 86400L * 30;
                case 4: return 86400L * 90;
                case LimitTypeUnlimited: return 0;
                default: return 0;
            }
        }

        /// <summary>统计某商品在限购窗口内的购买量。</summary>
        private static long CountBought(LocalRole role, int goodsId, int limitType)
        {
            if (role == null || role.StoreBuyLog == null)
                return 0;
            // 无限制商品不需要统计（原服直接跳过）
            if (limitType == LimitTypeUnlimited)
                return 0;

            long window = LimitWindowSeconds(limitType);
            long now = NowUnix();
            long total = 0;
            List<long> log = role.StoreBuyLog;
            int count = log.Count;
            // StoreBuyLog 是 [商品ID, 数量, 时间] 三个 long 一条记录。
            // 刻意不用 continue：ILRuntime 的 RegisterVM 对多分支 continue 会抛
            // NotImplementedException，写成正向 if 最稳。
            for (int i = 0; i + 2 < count; i = i + 3)
            {
                long logGoods = log[i];
                if (logGoods == (long)goodsId && InLimitWindow(now, log[i + 2], window))
                {
                    total = total + log[i + 1];
                }
            }
            return total;
        }

        /// <summary>
        /// 购买时间是否落在限购窗口内。window == 0 表示终生有效。
        ///
        /// 这里刻意只用 &lt; 和 ==，不写 &gt; / &lt;=：
        /// ILRuntime 的 RegisterVM 在处理 cgt 指令时，如果某个 register 的
        /// ObjectType 不是 Integer/Long/Float/Double（比如拿到的值是 Null），
        /// 会直接 throw NotImplementedException（见 ILIntepreter.Register.cs 的 cgt 分支）。
        /// 改成 &lt; + == 组合可以完全避开这个坑。
        /// </summary>
        private static bool InLimitWindow(long now, long boughtAt, long window)
        {
            if (window == 0L)
                return true;
            long elapsed = now - boughtAt;
            if (elapsed < window)
                return true;
            if (elapsed == window)
                return true;
            return false;
        }

        /// <summary>
        /// 本次购买里有几次需要付费。
        /// times == 0：全部付费；times &gt; 0：累计购买次数在 times 以内付费，超出部分免费。
        /// 对齐原服 StoreBuy 里 list[0].Times 那段分支。
        /// </summary>
        private static int CountPayTimes(LocalRole role, CFGItem_Store cfg, int num)
        {
            int times = cfg.Limite == null ? 0 : cfg.Limite.times;
            if (times <= 0)
                return num;

            long boughtAll = CountBought(role, cfg.ID, 0);
            if (boughtAll >= times)
                return 0;
            long leftFree = times - boughtAll;
            return leftFree >= num ? num : (int)leftFree;
        }

        private static List<long> EnsureBuyLog(LocalRole role)
        {
            if (role.StoreBuyLog == null)
                role.StoreBuyLog = new List<long>();
            return role.StoreBuyLog;
        }

        // ==================== 极简 JSON 取值 ====================

        /// <summary>
        /// 从 JSON 里取整数字段。只用于 /v1/store/* 这几个固定的扁平请求，
        /// 不引入通用 JSON 解析，避免 LitJson 对热更 struct 的坑。
        /// </summary>
        private static int ParseIntField(string body, string field, int fallback)
        {
            long value = ParseLongField(body, field, fallback);
            return (int)value;
        }

        private static long ParseLongField(string body, string field, long fallback)
        {
            if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(field))
                return fallback;

            string key = "\"" + field + "\"";
            int keyIndex = body.IndexOf(key);
            if (keyIndex < 0)
                return fallback;

            int colon = body.IndexOf(':', keyIndex + key.Length);
            if (colon < 0)
                return fallback;

            int i = colon + 1;
            while (i < body.Length && (body[i] == ' ' || body[i] == '\t'))
                i++;

            bool negative = false;
            if (i < body.Length && body[i] == '-')
            {
                negative = true;
                i++;
            }

            long value = 0;
            bool any = false;
            while (i < body.Length && body[i] >= '0' && body[i] <= '9')
            {
                value = value * 10 + (body[i] - '0');
                any = true;
                i++;
            }
            if (!any)
                return fallback;
            return negative ? -value : value;
        }

        private static long NowUnix()
        {
            return System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
