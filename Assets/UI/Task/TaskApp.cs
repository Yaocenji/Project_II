using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ProjectII.Manager;

namespace ProjectII.UI
{
    /// <summary>
    /// 手机任务面板 App。
    /// 以备忘录风格展示当前活跃任务，含目标勾选状态。
    /// 监听 TaskManager 事件自动刷新。
    /// </summary>
    public class TaskApp : PhoneApp
    {
        [Header("预制体")]
        [Tooltip("单个任务条目的预制体，需挂载 TaskEntry 脚本")]
        [SerializeField] private GameObject taskEntryPrefab;

        [Header("容器")]
        [Tooltip("任务条目实例化到的父 Transform（通常是一个 ScrollView 的 Content）")]
        [SerializeField] private Transform contentContainer;

        [Header("空状态")]
        [Tooltip("没有活跃任务时显示的文本")]
        [SerializeField] private TMP_Text noTaskText;

        // 已实例化的条目列表，刷新时回收
        private readonly List<TaskEntry> activeEntries = new List<TaskEntry>();

        protected override void OnOpen()
        {
            if (TaskManager.Instance != null)
                TaskManager.Instance.OnAnyStateChanged += RefreshTaskList;

            RefreshTaskList();
        }

        protected override void OnClose()
        {
            if (TaskManager.Instance != null)
                TaskManager.Instance.OnAnyStateChanged -= RefreshTaskList;

            ClearEntries();
        }

        /// <summary>
        /// 从 TaskManager 拉取活跃任务并重建列表
        /// </summary>
        public void RefreshTaskList()
        {
            ClearEntries();

            if (TaskManager.Instance == null)
            {
                Debug.LogError("TaskApp: TaskManager 实例不存在，无法刷新任务列表。");
                return;
            }

            var tasks = TaskManager.Instance.GetActiveTaskSnapshots();

            if (tasks.Count == 0)
            {
                if (noTaskText != null)
                    noTaskText.gameObject.SetActive(true);
                return;
            }

            if (noTaskText != null)
                noTaskText.gameObject.SetActive(false);

            foreach (var task in tasks)
            {
                var entryGO = Instantiate(taskEntryPrefab, contentContainer);
                var entry = entryGO.GetComponent<TaskEntry>();
                if (entry != null)
                {
                    entry.SetData(task);
                    activeEntries.Add(entry);
                }
                else
                {
                    Debug.LogWarning($"TaskApp: taskEntryPrefab 上未找到 TaskEntry 组件！挂载 TaskEntry 脚本到预制体上。");
                }
            }
        }

        private void ClearEntries()
        {
            foreach (var entry in activeEntries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }
            activeEntries.Clear();
        }
    }
}
