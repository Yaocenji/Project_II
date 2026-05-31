using System;
using System.Collections.Generic;

namespace ProjectII.Task
{
    /// <summary>
    /// 任务的单个目标描述（纯数据，不包含运行时状态）
    /// </summary>
    [Serializable]
    public class ObjectiveInfo
    {
        /// <summary>目标唯一标识，代码中通过此 ID 更新完成状态</summary>
        public string objectiveId;

        /// <summary>目标描述文本，显示在手机备忘录中</summary>
        public string description;
    }

    /// <summary>
    /// 任务定义（纯数据，不包含运行时状态）。
    /// 由开发者在 TaskDatabase 中预设。
    /// </summary>
    [Serializable]
    public class TaskInfo
    {
        /// <summary>任务唯一标识，代码中通过此 ID 解锁 / 查询任务</summary>
        public string taskId;

        /// <summary>任务标题，作为备忘录条目标题</summary>
        public string taskTitle;

        /// <summary>任务详细描述（手机备忘录正文），支持多行文本</summary>
        public string taskDescription;

        /// <summary>任务包含的目标列表</summary>
        public List<ObjectiveInfo> objectives;

        /// <summary>前置任务 ID 列表，全部完成后本任务才会解锁</summary>
        public List<string> prerequisiteTaskIds;
    }
}
