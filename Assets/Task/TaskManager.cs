using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Manager
{
    /// <summary>
    /// 运行时任务管理器（场景单例）
    /// 读取 TaskDatabase 的预设数据，管理任务的解锁/完成/目标状态。
    /// 通过事件通知外部（如 TaskApp UI）刷新显示。
    /// </summary>
    [DefaultExecutionOrder(-98)]
    public class TaskManager : MonoBehaviour
    {
        [Header("任务数据库")]
        [SerializeField] private Task.TaskDatabase taskDatabase;

        [Header("启用调试日志")]
        [SerializeField] private bool enableDebugLog = false;

        // 运行时状态字典：taskId → TaskRuntimeState
        private Dictionary<string, TaskRuntimeState> runtimeStates = new Dictionary<string, TaskRuntimeState>();

        #region 单例

        private static TaskManager instance;

        /// <summary>
        /// TaskManager 场景单例实例
        /// </summary>
        public static TaskManager Instance
        {
            get
            {
                if (instance == null)
                    instance = FindObjectOfType<TaskManager>();
                return instance;
            }
        }

        #endregion

        #region 事件

        /// <summary>任务解锁时触发，参数为新解锁的 taskId</summary>
        public event Action<string> OnTaskUnlocked;

        /// <summary>任务完成时触发，参数为已完成的 taskId</summary>
        public event Action<string> OnTaskCompleted;

        /// <summary>某个目标完成时触发，参数为 taskId + objectiveId</summary>
        public event Action<string, string> OnObjectiveCompleted;

        /// <summary>任务状态有任何更新时触发，TaskApp 可监听此事件刷新</summary>
        public event Action OnAnyStateChanged;

        #endregion

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Debug.LogWarning("TaskManager 单例已经存在，销毁新创建的实例。");
                Destroy(gameObject);
                return;
            }

            InitializeRuntimeStates();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// 从 TaskDatabase 初始化所有任务的运行时状态
        /// </summary>
        private void InitializeRuntimeStates()
        {
            if (taskDatabase == null)
            {
                Debug.LogError("TaskManager: 未设置 TaskDatabase 引用！请在 Inspector 中拖入 TaskDatabase.asset。");
                return;
            }

            foreach (var taskDef in taskDatabase.allTasks)
            {
                var state = new TaskRuntimeState(taskDef);
                runtimeStates.Add(taskDef.taskId, state);

                // 没有前置任务的，开局即解锁
                if (taskDef.prerequisiteTaskIds == null || taskDef.prerequisiteTaskIds.Count == 0)
                    state.isUnlocked = true;
            }

            if (enableDebugLog)
                Debug.Log($"TaskManager: 已从 TaskDatabase 初始化 {runtimeStates.Count} 个任务。");
        }

        #region 公共方法

        /// <summary>
        /// 获取某个任务的定义+运行时状态的快照，供外部只读查询
        /// </summary>
        /// <param name="taskId">任务 ID</param>
        /// <returns>该任务的信息，未找到返回 null</returns>
        public TaskInfoSnapshot GetTaskSnapshot(string taskId)
        {
            if (!runtimeStates.TryGetValue(taskId, out var state))
                return null;

            var def = FindTaskDef(taskId);
            if (def == null) return null;

            return TaskInfoSnapshot.From(def, state);
        }

        /// <summary>
        /// 获取所有已解锁且未完成的任务快照列表，用于手机面板渲染
        /// </summary>
        public List<TaskInfoSnapshot> GetActiveTaskSnapshots()
        {
            var list = new List<TaskInfoSnapshot>();
            foreach (var kvp in runtimeStates)
            {
                if (!kvp.Value.isUnlocked || kvp.Value.isCompleted) continue;

                var def = FindTaskDef(kvp.Key);
                if (def != null)
                    list.Add(TaskInfoSnapshot.From(def, kvp.Value));
            }
            return list;
        }

        /// <summary>
        /// 将指定目标标记为完成。
        /// 如果该目标完成后整个任务的所有目标都已完成，则自动完成该任务并解锁后续任务。
        /// </summary>
        /// <param name="objectiveId">目标 ID</param>
        public void CompleteObjective(string objectiveId)
        {
            foreach (var kvp in runtimeStates)
            {
                var state = kvp.Value;
                if (!state.isUnlocked || state.isCompleted) continue;

                for (int i = 0; i < state.objectiveStates.Count; i++)
                {
                    if (state.objectiveStates[i].objectiveId == objectiveId &&
                        !state.objectiveStates[i].completed)
                    {
                        state.objectiveStates[i].completed = true;
                        OnObjectiveCompleted?.Invoke(kvp.Key, objectiveId);
                        OnAnyStateChanged?.Invoke();

                        if (enableDebugLog)
                            Debug.Log($"TaskManager: 目标 {objectiveId} 完成。所属任务: {kvp.Key}");

                        // 检查是否整个任务的所有目标都完成了
                        if (AreAllObjectivesComplete(state))
                        {
                            CompleteTask(kvp.Key);
                        }
                        return;
                    }
                }
            }

            Debug.LogWarning($"TaskManager: 找不到可完成的目标 {objectiveId}（可能已经完成或任务未解锁）。");
        }

        /// <summary>
        /// 直接完成一个任务（所有目标立即标记完成）
        /// </summary>
        /// <param name="taskId">任务 ID</param>
        public void CompleteTask(string taskId)
        {
            if (!runtimeStates.TryGetValue(taskId, out var state)) return;
            if (state.isCompleted) return;

            state.isCompleted = true;
            foreach (var obj in state.objectiveStates)
                obj.completed = true;

            OnTaskCompleted?.Invoke(taskId);
            OnAnyStateChanged?.Invoke();

            if (enableDebugLog)
                Debug.Log($"TaskManager: 任务 {taskId} 已完成。");

            // 检查哪些任务的解锁条件被满足
            TryUnlockDependentTasks(taskId);
        }

        /// <summary>
        /// 判断一个任务是否已完成
        /// </summary>
        /// <param name="taskId">任务 ID</param>
        public bool IsTaskCompleted(string taskId)
        {
            return runtimeStates.TryGetValue(taskId, out var state) && state.isCompleted;
        }

        /// <summary>
        /// 判断一个任务是否已解锁
        /// </summary>
        /// <param name="taskId">任务 ID</param>
        public bool IsTaskUnlocked(string taskId)
        {
            return runtimeStates.TryGetValue(taskId, out var state) && state.isUnlocked;
        }

        #endregion

        #region 私有方法

        private Task.TaskInfo FindTaskDef(string taskId)
        {
            if (taskDatabase == null) return null;
            foreach (var def in taskDatabase.allTasks)
            {
                if (def.taskId == taskId)
                    return def;
            }
            return null;
        }

        private bool AreAllObjectivesComplete(TaskRuntimeState state)
        {
            foreach (var obj in state.objectiveStates)
            {
                if (!obj.completed) return false;
            }
            return true;
        }

        /// <summary>
        /// 当任务完成时，检查并解锁所有依赖该任务的后继任务
        /// </summary>
        private void TryUnlockDependentTasks(string completedTaskId)
        {
            foreach (var kvp in runtimeStates)
            {
                var state = kvp.Value;
                if (state.isUnlocked || state.isCompleted) continue;

                var def = FindTaskDef(kvp.Key);
                if (def?.prerequisiteTaskIds == null) continue;

                // 检查所有前置任务是否都已完成
                bool allPrereqsMet = true;
                foreach (var prereqId in def.prerequisiteTaskIds)
                {
                    if (!IsTaskCompleted(prereqId))
                    {
                        allPrereqsMet = false;
                        break;
                    }
                }

                if (allPrereqsMet)
                {
                    state.isUnlocked = true;
                    OnTaskUnlocked?.Invoke(kvp.Key);
                    OnAnyStateChanged?.Invoke();

                    if (enableDebugLog)
                        Debug.Log($"TaskManager: 任务 {kvp.Key} 已解锁。");
                }
            }
        }

        #endregion
    }

    #region 辅助类型

    /// <summary>
    /// 单个目标的运行时状态
    /// </summary>
    [Serializable]
    public class ObjectiveRuntimeState
    {
        /// <summary>匹配 TaskInfo 中对应的 objectiveId</summary>
        public string objectiveId;

        /// <summary>是否已完成</summary>
        public bool completed;
    }

    /// <summary>
    /// 单个任务的运行时状态
    /// </summary>
    [Serializable]
    public class TaskRuntimeState
    {
        public string taskId;

        /// <summary>是否已对玩家可见</summary>
        public bool isUnlocked;

        /// <summary>是否已完成</summary>
        public bool isCompleted;

        /// <summary>每个目标的运行时完成状态</summary>
        public List<ObjectiveRuntimeState> objectiveStates = new List<ObjectiveRuntimeState>();

        public TaskRuntimeState(Task.TaskInfo def)
        {
            taskId = def.taskId;
            isUnlocked = false;
            isCompleted = false;
            if (def.objectives != null)
            {
                foreach (var obj in def.objectives)
                {
                    objectiveStates.Add(new ObjectiveRuntimeState
                    {
                        objectiveId = obj.objectiveId,
                        completed = false
                    });
                }
            }
        }
    }

    /// <summary>
    /// 任务的只读快照，合并了静态数据 + 运行时状态。
    /// 供外部（如 TaskApp UI）查询和渲染时使用。
    /// </summary>
    [Serializable]
    public class TaskInfoSnapshot
    {
        public string taskId;
        public string taskTitle;
        public string taskDescription;
        public bool isCompleted;
        public List<ObjectiveSnapshot> objectives;

        [Serializable]
        public class ObjectiveSnapshot
        {
            public string objectiveId;
            public string description;
            public bool completed;
        }

        public static TaskInfoSnapshot From(Task.TaskInfo def, TaskRuntimeState state)
        {
            var snapshot = new TaskInfoSnapshot
            {
                taskId = def.taskId,
                taskTitle = def.taskTitle,
                taskDescription = def.taskDescription,
                isCompleted = state.isCompleted,
                objectives = new List<ObjectiveSnapshot>()
            };

            if (def.objectives != null)
            {
                foreach (var objDef in def.objectives)
                {
                    var objState = state.objectiveStates.Find(s => s.objectiveId == objDef.objectiveId);
                    snapshot.objectives.Add(new ObjectiveSnapshot
                    {
                        objectiveId = objDef.objectiveId,
                        description = objDef.description,
                        completed = objState?.completed ?? false
                    });
                }
            }

            return snapshot;
        }
    }

    #endregion
}
