using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Task
{
    /// <summary>
    /// 全局任务数据库，存放开发者预设的所有任务定义。
    /// 通过 Project 窗口右键 Create → ProjectII → Task Database 创建。
    /// 由 TaskManager 在运行时读取，不直接持有运行时状态。
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectII/Task Database", fileName = "TaskDatabase")]
    public class TaskDatabase : ScriptableObject
    {
        /// <summary>全游戏所有任务的数据定义列表</summary>
        public List<TaskInfo> allTasks = new List<TaskInfo>();
    }
}
