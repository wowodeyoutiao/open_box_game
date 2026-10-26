using System;
using System.Collections.Generic;
using JEngine.Core;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 单机本地协议路由层（Adapter）。
    ///
    /// 上行请求在 TCPClientMgr.SendMessage 开头交给本地路由；
    /// 已处理时直接把响应对象投递到 JEvent，由现成的 ProtoC 更新 Manager。
    ///
    /// 运行模式统一由 LocalRuntime 决定：
    /// - LocalStrict：未实现协议必须失败，禁止回退真网；
    /// - LocalCompare：未实现协议才允许走真网并抓包；
    /// - Remote：完全走真实网络。
    /// </summary>
    public class LocalServer : SingletonTemplate<LocalServer>
    {
        /// <summary>
        /// 兼容旧调用点的只读开关。真实状态统一由 LocalRuntime.Mode 决定。
        /// </summary>
        public static bool Enabled
        {
            get { return LocalRuntime.IsLocalMode; }
        }

        public delegate object Handler(byte[] request);

        private readonly Dictionary<short, Handler> _handlers = new Dictionary<short, Handler>();

        private bool _inited;

        /// <summary>
        /// 未实现的协议清单，用于排查"哪个界面没数据"。
        /// </summary>
        public readonly HashSet<short> MissingCmds = new HashSet<short>();

        public void Init()
        {
            if (_inited)
                return;
            _inited = true;

            LocalData.Instance.EnsureInit();

            LoginLocalHandler.Register(_handlers);
            BasicLocalHandlers.Register(_handlers);
            GamePlayLocalHandlers.Register(_handlers);

            Log.Print($"[LocalServer] 初始化完成，已注册 {_handlers.Count} 条协议");
        }

        /// <summary>
        /// 尝试本地处理。
        ///
        /// 返回值明确区分：
        /// - Handled：Service 已执行，Response 可以投递；
        /// - NotImplemented：本地没有该协议，严格模式禁止回退真网；
        /// - Failed：Handler 抛异常或没有给出响应，必须走失败终态。
        ///
        /// 【为什么直接返回响应对象、不走 protobuf】
        /// protobuf-net 反序列化用 GetUninitializedObject 跳过构造函数，
        /// 空 List 字段不会被赋值，客户端拿到的 List 可能为 null。
        /// 本地直接投递对象可以避开这个坑，但语义仍按“已处理/未实现/失败”区分。
        /// </summary>
        public LocalHandleResult TryHandle(short cmdId, byte[] request)
        {
            if (!_inited)
                Init();

            Handler handler;
            if (_handlers.TryGetValue(cmdId, out handler))
            {
                try
                {
                    object response = handler(request);
                    if (response == null)
                    {
                        string error = "cmd=" + cmdId + " Handler 返回 null";
                        LocalDiagnostics.RecordError("LocalServer", error);
                        return LocalHandleResult.Failed(error);
                    }
                    return LocalHandleResult.Handled(response);
                }
                catch (Exception e)
                {
                    string error = "cmd=" + cmdId + " 处理异常: " + e;
                    LocalDiagnostics.RecordError("LocalServer", error);
                    return LocalHandleResult.Failed(error);
                }
            }

            MissingCmds.Add(cmdId);
            LocalDiagnostics.RecordMissingTcp(cmdId);
            return LocalHandleResult.NotImplemented();
        }

        /// <summary>注册一条协议处理器</summary>
        public static void Reg(Dictionary<short, Handler> dict, CmdId id, Handler handler)
        {
            dict[(short)id] = handler;
        }
    }
}
