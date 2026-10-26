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

        /// <summary>签到 /v1/sign/list</summary>
        public HTTP_SignListResponse GetSignList()
        {
            return new HTTP_SignListResponse
            {
                code = 0,
                data = new HTTP_SignAwsard
                {
                    accumulate_award = new HTTP_SignDayAward[0],
                },
            };
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
