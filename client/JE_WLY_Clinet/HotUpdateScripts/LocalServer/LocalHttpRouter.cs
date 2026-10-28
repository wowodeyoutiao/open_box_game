using System;
using System.Collections.Generic;
using JEngine.Core;
using UnityEngine;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 单机模式的本地 HTTP 路由。
    ///
    /// 【工作方式】
    /// - Factory 命中 → 直接构造响应对象，适合 struct + 嵌套 List；
    /// - JSON 路由命中 → 返回本地 JSON，复用原有 JsonMapper 链路；
    /// - 未命中时由 HttpRequestHelper 按运行模式决定：
    ///   LocalStrict 直接失败，LocalCompare 才允许走真网并抓包。
    ///
    /// ⚠️ JSON 里所有数组字段必须显式写 [] 或 [元素]，
    ///    否则 LitJson 反序列化后 List 为 null，上游 .Count 会空引用。
    /// </summary>
    public static class LocalHttpRouter
    {
        public delegate string JsonProvider(string postStr);

        /// <summary>返回构造好的响应对象（用于 struct + 嵌套 List 的响应，绕开 LitJson）</summary>
        public delegate object Factory(string postStr);

        /// <summary>HTTP 本地路由结果，明确区分未命中、成功和失败。</summary>
        public sealed class RouteResult
        {
            public bool Matched;
            public bool Success;
            public string Json;
            public string Error;

            public static RouteResult NotMatched()
            {
                return new RouteResult { Matched = false, Success = false, Json = null, Error = "" };
            }

            public static RouteResult Ok(string json)
            {
                return new RouteResult { Matched = true, Success = true, Json = json, Error = "" };
            }

            public static RouteResult Failed(string error)
            {
                return new RouteResult
                {
                    Matched = true,
                    Success = false,
                    Json = null,
                    Error = string.IsNullOrEmpty(error) ? "本地 HTTP 路由处理失败" : error
                };
            }
        }

        private static readonly Dictionary<string, JsonProvider> _routes = new Dictionary<string, JsonProvider>();
        private static readonly Dictionary<string, Factory> _factories = new Dictionary<string, Factory>();
        private static string _capturePath;
        private static bool _inited;

        public static void Init()
        {
            if (_inited)
                return;
            _inited = true;

            _capturePath = System.IO.Path.Combine(Application.persistentDataPath, "http_capture.log");

            // 登录：也走 Factory，彻底不依赖 LitJson 解析热更 struct
            RegisterFactory("/v1/user/login/guest", (body) => PlayerService.Instance.BuildLoginResponse());
            RegisterFactory("/v1/user/login/3rd", (body) => PlayerService.Instance.BuildLoginResponse());

            // 选角进游戏：本地放行（返回 code=0 才会继续走 TCP 登录）
            // 选角：必须返回 code=0，客户端才会继续走 TCP 登录。
            // 走 Factory 是为了拿到请求体里的 uid，按 uid 选中 LocalData.Roles 中的角色
            RegisterFactory("/v1/user/login/select_char", PlayerService.Instance.BuildSelectCharResponse);
            RegisterFactory("/v1/user/login/del_char", PlayerService.Instance.BuildDeleteCharResponse);
            Register("/v1/user/login/realname", OkJson);

            // 建角：必须返回完整角色信息，否则客户端 Insert 默认角色 → job=0 → 后续 JobDesDic[0] 崩溃
            RegisterFactory("/v1/user/login/create_char", PlayerService.Instance.BuildCreateCharResponse);

            // ---- 主城 8 个接口 ----
            // 这些响应都是 struct + 数组字段，走 Factory 直接构造对象，
            // 保证数组是空数组而非 null（LitJson 解析热更 struct 会丢字段）
            RegisterFactory("/v1/mail/list", (body) => MailService.Instance.GetList());
            RegisterFactory("/v1/mail/detail", (body) => MailService.Instance.GetDetail(body));
            RegisterFactory("/v1/mail/extract", (body) => MailService.Instance.Extract(body));
            RegisterFactory("/v1/mail/extractall", (body) => MailService.Instance.ExtractAll());
            RegisterFactory("/v1/mail/del", (body) => MailService.Instance.Delete(body));
            RegisterFactory("/v1/mail/delall", (body) => MailService.Instance.DeleteAll());
            // 商城：列表返回各商品已购数量，购买走同一套限购/货币/发货逻辑
            RegisterFactory("/v1/store/list", StoreService.Instance.GetList);
            RegisterFactory("/v1/store/buy", StoreService.Instance.Buy);
            RegisterFactory("/v1/sign/list", (body) => ActivityService.Instance.GetSignList());
            RegisterFactory("/v1/activity/listall", (body) => ActivityService.Instance.GetActivityAll());
            RegisterFactory("/v1/activity/listcontinuity", (body) => ActivityService.Instance.GetActivityList());
            RegisterFactory("/v1/privilege/listcontinuity", (body) => ActivityService.Instance.GetPrivilegeList());
            RegisterFactory("/v1/activity/listtrial", (body) => ActivityService.Instance.GetTrialFund());
            RegisterFactory("/v1/activity/groupup", (body) => ActivityService.Instance.GetGrowthFund());
            RegisterFactory("/v1/prize_draw/detail", (body) => ActivityService.Instance.GetPrivilegeDetail());
            RegisterFactory("/v1/activity/listgrade", (body) => ActivityService.Instance.GetLevelGiftList());
        }

        public static void Register(string urlKey, JsonProvider provider)
        {
            _routes[urlKey] = provider;
        }

        /// <summary>注册「对象工厂」：适合 struct + 嵌套 List 的响应</summary>
        public static void RegisterFactory(string urlKey, Factory factory)
        {
            _factories[urlKey] = factory;
        }

        /// <summary>查找对象工厂，未命中返回 null</summary>
        public static Factory TryGetFactory(string fullUrl)
        {
            if (!_inited)
                Init();

            if (string.IsNullOrEmpty(fullUrl))
                return null;

            foreach (KeyValuePair<string, Factory> kv in _factories)
            {
                if (fullUrl.EndsWith(kv.Key))
                    return kv.Value;
            }
            return null;
        }

        /// <summary>
        /// 命中 JSON 路由时返回明确结果。未命中时调用方按运行模式决定是否走真网。
        ///
        /// 重要：路由命中后即使内部异常，也返回 Matched + Failed，
        /// 调用方不得再降级真实网络。
        /// </summary>
        public static RouteResult HandleJson(string fullUrl, string postStr)
        {
            if (!_inited)
                Init();

            if (string.IsNullOrEmpty(fullUrl))
                return RouteResult.NotMatched();

            foreach (KeyValuePair<string, JsonProvider> kv in _routes)
            {
                if (!fullUrl.EndsWith(kv.Key))
                    continue;

                try
                {
                    string json = kv.Value(postStr);
                    if (json == null)
                        return RouteResult.Failed("HTTP 路由返回 null: " + kv.Key);
                    return RouteResult.Ok(json);
                }
                catch (Exception e)
                {
                    LocalDiagnostics.RecordError("LocalHttp", kv.Key + " 处理异常: " + e);
                    return RouteResult.Failed("HTTP 路由异常: " + kv.Key + " " + e.Message);
                }
            }
            return RouteResult.NotMatched();
        }

        /// <summary>
        /// 把真服响应落盘，用于生成离线模板。
        /// 位置：Application.persistentDataPath/http_capture.log
        /// </summary>
        public static void Capture(string url, string respJson)
        {
            if (!LocalRuntime.CanFallbackToNetwork)
                return;
            if (string.IsNullOrEmpty(respJson))
                return;
            try
            {
                System.IO.File.AppendAllText(_capturePath, url + "\t" + respJson + "\n");
            }
            catch
            {
                // 抓包失败不影响主流程
            }
        }

        public static string CapturePath()
        {
            if (!_inited)
                Init();
            return _capturePath;
        }

        // ---------------- 登录 ----------------

        /// <summary>通用成功响应（删角 / 实名等）</summary>
        private static string OkJson(string postStr)
        {
            return "{\"code\":0}";
        }

        /// <summary>
        /// 转调 PlayerService —— Adapter 层不写业务。
        /// HttpC.OnLoginSever 在单机模式下走这里。
        /// </summary>
        public static HotUpdateScripts.ProtoM.HTTP_LoginResponse BuildLoginResponse()
        {
            return PlayerService.Instance.BuildLoginResponse();
        }

        // 注：原 LoginJson（返回登录 JSON 字符串）已废弃并删除。
        // 登录改为走 Factory + PlayerService.BuildLoginResponse()，
        // 避免 LitJson 解析热更 struct 时丢字段（曾导致 job=0 → JobDesDic[0] 崩溃）。
    }
}
