using System;
using System.Collections.Generic;
using HotUpdateScripts.ProtoM;
using JEngine.Singleton;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 活动领域服务：活动总览、签到、特权卡、试用基金、成长基金。
    ///
    /// ⚠️ 这些响应全是 struct + 数组字段，必须直接构造对象（数组给空数组而非 null），
    /// 不能走 JsonMapper —— LitJson 解析热更 struct 会丢字段、数组会变 null 导致崩溃。
    ///
    /// 对应服务端：activity.go(308) + internal/web 的 activity 系列接口
    /// </summary>
    public class ActivityService : SingletonTemplate<ActivityService>
    {
        /// <summary>活动总览 /v1/activity/listall</summary>
        public HTTP_ActivityAllResponse GetActivityAll()
        {
            return new HTTP_ActivityAllResponse
            {
                code = 0,
                data = new HTTP_ItemActivityAll[0],
            };
        }

        /// <summary>签到活动列表 /v1/activity/listcontinuity</summary>
        public HTTP_ActivityListResponse GetActivityList()
        {
            return new HTTP_ActivityListResponse
            {
                code = 0,
                day = 0,
                buy = 0,
                data = new HTTP_ActivityListDay[0],
            };
        }

        // ==================== 签到 ====================

        /// <summary>
        /// 签到 /v1/sign/list
        ///
        /// 对齐原服 internal/web/api/sign.go 的 SignList：
        ///   · day_award = 当前进度（今天签过 → sign=true 且 day=已签天数；
        ///                 没签 → sign=false 且 day=下一次要签的天数）
        ///   · accumulate_award = 当前轮已签的天数列表（原服查 db.Sign 里同 UserRound+Round 的记录）
        /// </summary>
        public HTTP_SignListResponse GetSignList()
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
            {
                return new HTTP_SignListResponse
                {
                    code = 0,
                    data = new HTTP_SignAwsard
                    {
                        day_award = new HTTP_SignDayAward { round = 1, day = 1, sign = false },
                        accumulate_award = new HTTP_SignDayAward[0],
                    },
                };
            }

            bool signedToday = IsSignedToday(role);
            int nextDay = role.SignDay <= 0 ? 1 : role.SignDay + 1;

            // 当前轮已签的天（sign.json 的 Days 从 1 开始）
            var accumulate = new List<HTTP_SignDayAward>();
            for (int d = 1; d <= role.SignDay; d++)
            {
                accumulate.Add(new HTTP_SignDayAward
                {
                    round = role.SignRound,
                    day = d,
                    sign = true,
                });
            }

            return new HTTP_SignListResponse
            {
                code = 0,
                data = new HTTP_SignAwsard
                {
                    day_award = new HTTP_SignDayAward
                    {
                        round = role.SignRound <= 0 ? 1 : role.SignRound,
                        day = signedToday ? role.SignDay : nextDay,
                        sign = signedToday,
                    },
                    accumulate_award = accumulate.ToArray(),
                },
            };
        }

        /// <summary>
        /// 签到 /v1/sign/add（原服 signAdd → SignnAdd）
        ///
        /// 流程：今天已签 → 失败；否则推进 day/round，按 sign.json 发奖
        /// （VIP 达到 VIPLimit 时奖励翻倍）。
        /// </summary>
        public HTTP_SignOperateResponse SignOperate(string postStr)
        {
            LocalRole role = LocalData.Instance.Current;
            if (role == null)
                return new HTTP_SignOperateResponse { code = -1, data = false };

            // 原服：同一天重复签到返回 errutil.ErrGetAward
            if (IsSignedToday(role))
                return new HTTP_SignOperateResponse { code = -1, data = false };

            AdvanceSignDay(role);

            CFGItem_Sign cfg = FindSignCfg(role.SignRound, role.SignDay);
            if (cfg != null && cfg.Award != null && cfg.Award.Length > 0)
            {
                int multiplier = role.Vip >= cfg.VIPLimit ? 2 : 1;
                for (int i = 0; i < cfg.Award.Length; i++)
                {
                    CFGItem_Sign.Accumulate_Award award = cfg.Award[i];
                    if (award == null || award.id <= 0 || award.num <= 0)
                        continue;
                    ItemService.Instance.GrantReward(role, award.id, (long)award.num * multiplier);
                }
            }

            role.SignLastUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveCoordinator.Instance.MarkDirty();

            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();
            LocalPusher.Instance.PushStatsNow();
            return new HTTP_SignOperateResponse { code = 0, data = true };
        }

        /// <summary>今天是否已经签过（原服比较 db.Sign.CreatedAt 与 now 的日期）。</summary>
        private static bool IsSignedToday(LocalRole role)
        {
            if (role == null || role.SignLastUnix <= 0)
                return false;
            try
            {
                DateTime last = DateTimeOffset.FromUnixTimeSeconds(role.SignLastUnix).LocalDateTime;
                return last.Date == DateTime.Now.Date;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 推进签到天数。对齐原服 SignnAdd：
        ///   本轮签满 → Round++（所有轮走完则 UserRound++ 并回到 Round=1），Day 归 1；否则 Day++。
        /// </summary>
        private static void AdvanceSignDay(LocalRole role)
        {
            if (role.SignRound <= 0)
                role.SignRound = 1;
            if (role.SignUserRound <= 0)
                role.SignUserRound = 1;

            if (role.SignDay <= 0)
            {
                role.SignDay = 1;
                return;
            }

            int daysInRound = CountDaysInRound(role.SignRound);
            if (daysInRound <= 0)
            {
                role.SignDay++;
                return;
            }

            if (role.SignDay >= daysInRound)
            {
                int roundCount = CountRounds();
                if (roundCount > 0 && role.SignRound >= roundCount)
                {
                    role.SignUserRound++;
                    role.SignRound = 1;
                }
                else
                {
                    role.SignRound++;
                }
                role.SignDay = 1;
            }
            else
            {
                role.SignDay++;
            }
        }

        /// <summary>某一轮配置了多少天（Round 相同里的最大 Days）。</summary>
        private static int CountDaysInRound(int round)
        {
            CFG_Sign table = CFG_Sign.Get();
            if (table == null || table.DicDatas == null)
                return 0;
            int maxDay = 0;
            foreach (KeyValuePair<long, CFGItem_Sign> kv in table.DicDatas)
            {
                CFGItem_Sign item = kv.Value;
                if (item == null || item.Round != round)
                    continue;
                if (item.Days > maxDay)
                    maxDay = item.Days;
            }
            return maxDay;
        }

        /// <summary>配置里一共有几轮。</summary>
        private static int CountRounds()
        {
            CFG_Sign table = CFG_Sign.Get();
            if (table == null || table.DicDatas == null)
                return 0;
            int maxRound = 0;
            foreach (KeyValuePair<long, CFGItem_Sign> kv in table.DicDatas)
            {
                CFGItem_Sign item = kv.Value;
                if (item != null && item.Round > maxRound)
                    maxRound = item.Round;
            }
            return maxRound;
        }

        /// <summary>取某一轮某一天的配置。</summary>
        private static CFGItem_Sign FindSignCfg(int round, int day)
        {
            CFG_Sign table = CFG_Sign.Get();
            if (table == null || table.DicDatas == null)
                return null;
            foreach (KeyValuePair<long, CFGItem_Sign> kv in table.DicDatas)
            {
                CFGItem_Sign item = kv.Value;
                if (item != null && item.Round == round && item.Days == day)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// 特权卡 /v1/privilege/listcontinuity
        /// ⚠️ 正确类型是 HTTP_PrivilegeListResponse（不是 HTTP_PrizedrawListResponse，
        ///    那个是 GetPrivilegeDetail 用的另一个接口，搞混会导致工厂类型不匹配）
        /// </summary>
        public HTTP_PrivilegeListResponse GetPrivilegeList()
        {
            return new HTTP_PrivilegeListResponse
            {
                code = 0,
                data = new HTTP_PrivilegeDetailDay[0],
            };
        }

        /// <summary>试用基金 /v1/activity/listtrial</summary>
        public HTTP_ActivityTrialFundAwardResponse GetTrialFund()
        {
            return new HTTP_ActivityTrialFundAwardResponse
            {
                code = 0,
                day = 0,
                buy = 0,
                data = new HTTP_ActivityTrialFundDays[0],
            };
        }

        /// <summary>
        /// 特权卡抽奖详情 /v1/prize_draw/detail（URL 常量在 HttpM.cs:13 而非 GameM）
        /// </summary>
        public HTTP_PrizedrawListResponse GetPrivilegeDetail()
        {
            return new HTTP_PrizedrawListResponse
            {
                code = 0,
                data = "",
                canplay = new HTTP_PrizedrawListResponse.PrizedrawDetail[0],
            };
        }

        /// <summary>等级礼包 /v1/activity/listgrade</summary>
        public HTTP_ActivityGradeResponse GetLevelGiftList()
        {
            return new HTTP_ActivityGradeResponse
            {
                code = 0,
                data = "",
                activity_list_grade = new HTTP_ActivityListGrade[0],
            };
        }

        /// <summary>成长基金 /v1/activity/groupup</summary>
        public HTTP_GrowthFundResponse GetGrowthFund()
        {
            return new HTTP_GrowthFundResponse
            {
                code = 0,
                day = 0,
                buy = 0,
                data = new HTTP_GrowthFundInfo[0],
            };
        }
    }
}
