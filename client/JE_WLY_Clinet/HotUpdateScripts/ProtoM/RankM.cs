using JEngine.Helper;
using JEngine.Singleton;
using pb;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdateScripts.ProtoM
{
    /// <summary>
    /// 每日签到
    /// </summary>
    public class RankM : SingletonTemplate<RankM>
    {
       
    }

    public struct HTTP_RankListRequest 
    {
        public int type { get; set; }
        public int all { get; set; }
    }

    /// <summary>
    /// 排行榜列表
    /// </summary>
    public struct HTTP_RankListResponse
    {
        public int code {get; set; }
        public HTTP_RankDetail data { get; set; }
    }

    public struct HTTP_RankDetail
    {
        //总排行榜
        public HTTP_RankUserInfo[] rank_all { get; set; }
        //个人榜单信息
        public HTTP_RankUserInfo rank_own { get; set; }
    }

    public struct HTTP_RankUserInfo
    {
        public long uid { get; set; }
        //排行榜类型
        public int type { get; set; }
        public int job { get; set; } //职业
        public int sex { get; set; } //等级
        //名次
        public int num { get; set; }

        //头像
        public string avatar { get; set; }
        //昵称
        public string name { get; set; }

        //数值
        public int value { get; set; }
    }


}

