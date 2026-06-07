using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectII.UI
{
    /// <summary>
    /// 单个任务条目 UI，挂载在任务条目预制体上。
    /// 由 TaskApp 实例化后调用 SetData 填充。
    /// 显示任务标题、描述正文、目标清单（带勾选框）。
    /// </summary>
    public class TaskEntry : MonoBehaviour
    {
        [Header("标题")]
        [SerializeField] private TMP_Text titleText;

        [Header("描述正文")]
        [SerializeField] private TMP_Text descriptionText;

        [Header("描述展开/折叠按钮")]
        [SerializeField] private Button expandButton;
        [SerializeField] private GameObject descriptionPanel;

        [Header("目标列表容器")]
        [SerializeField] private Transform objectivesContainer;

        [Header("单个目标行的预制体")]
        [SerializeField] private GameObject objectiveRowPrefab;

        [Tooltip("每个目标行之间的距离（垂直间隔）")]
        [SerializeField] private float objectiveRowSpacing = 40f;

        private Manager.TaskInfoSnapshot data;
        private bool isExpanded = false;
        private readonly List<GameObject> spawnedRows = new List<GameObject>();

        private void Awake()
        {
            if (expandButton != null)
                expandButton.onClick.AddListener(ToggleExpand);

            if (descriptionPanel != null)
                descriptionPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (expandButton != null)
                expandButton.onClick.RemoveListener(ToggleExpand);
        }

        /// <summary>
        /// 由 TaskApp 调用，填充此条目的数据
        /// </summary>
        /// <param name="snapshot">任务快照数据</param>
        public void SetData(Manager.TaskInfoSnapshot snapshot)
        {
            data = snapshot;

            if (titleText != null)
                titleText.text = snapshot.taskTitle;

            if (descriptionText != null)
                descriptionText.text = snapshot.taskDescription;

            // 构建目标清单
            ClearObjectiveRows();
            BuildObjectiveRows();
        }

        private void ToggleExpand()
        {
            isExpanded = !isExpanded;
            if (descriptionPanel != null)
                descriptionPanel.SetActive(isExpanded);
        }

        private void BuildObjectiveRows()
        {
            if (objectiveRowPrefab == null || objectivesContainer == null || data?.objectives == null)
                return;

            for (int i = 0; i < data.objectives.Count; i++)
            {
                var row = Instantiate(objectiveRowPrefab, objectivesContainer);
                var rowRect = row.GetComponent<RectTransform>();
                if (rowRect != null)
                {
                    rowRect.anchoredPosition = new Vector2(0, -i * objectiveRowSpacing);
                }

                // 尝试设置行内的文本和勾选框
                var rowText = row.GetComponentInChildren<TMP_Text>();
                if (rowText != null)
                {
                    // 已完成的目标加上删除线
                    var obj = data.objectives[i];
                    rowText.text = obj.completed
                        ? $"<s>{obj.description}</s>"
                        : obj.description;
                }

                var toggle = row.GetComponentInChildren<Toggle>();
                if (toggle != null)
                {
                    toggle.isOn = data.objectives[i].completed;
                    toggle.interactable = false; // 只读，完成状态由代码驱动
                }

                spawnedRows.Add(row);
            }
        }

        private void ClearObjectiveRows()
        {
            foreach (var row in spawnedRows)
            {
                if (row != null)
                    Destroy(row);
            }
            spawnedRows.Clear();
        }
    }
}
