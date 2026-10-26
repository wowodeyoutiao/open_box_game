using JEngine.Singleton;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 任务领域服务：主线/日常/周常、成就、引导触发。
    ///
    /// 【阶段1 状态】骨架就位，逻辑待补。
    /// 对应服务端：task_mgr.go(768) + player.go 里的 tasks 部分
    /// </summary>
    public class TaskService : SingletonTemplate<TaskService>
    {
        // 待接入：
        // C2S_TaskComplate  -> CompleteTask(int taskId)
        // SyncTaskPush      -> 任务状态推送
    }
}
