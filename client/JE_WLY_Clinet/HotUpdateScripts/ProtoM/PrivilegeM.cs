using JEngine.Helper;
using JEngine.Singleton;
using pb;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdateScripts.ProtoM
{
    /// <summary>
    /// 特权卡
    /// </summary>
    public class PrivilegeM : SingletonTemplate<PrivilegeM>
    {
       
    }

    /// <summary>
    /// 特权卡列表
    /// </summary>
    public struct HTTP_PrivilegeListResponse
    {
        public int code {get; set; }
        public HTTP_PrivilegeDetailDay[] data { get; set; }
    }

    public struct HTTP_PrivilegeDetailDay
    {
        //内容编号
        public int pid { get; set; }
        // 1-未购买，2-已购买
        public int buy { get; set; }
        //1-待领取，2-已领取
        public int get { get; set; }

        public long end_time { get; set; }
    }

    //特权卡领取
    public struct HTTP_PrivilegeReceiveDayResponse
    {
        public int code { get; set; }
        public string data { get; set; }
    }

    //特权卡领取
    public struct HTTP_PrivilegeReceiveDayRequset
    {
        public int pid { get; set; }
        //提取的第几天
    }

}

