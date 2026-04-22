using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ProjectII.Item
{
    /// <summary>
    /// 挂载在每个 icon Image 上，处理拖动与格子间物品转移
    /// </summary>
    public class ItemIconDrag : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("所属信息")]
        [Tooltip("true = 快捷栏图标，false = 背包图标")]
        public bool isHotbarIcon = false;

        [Header("依赖")]
        [SerializeField] private HotbarUI hotbarUI;
        [SerializeField] private BackpackUI backpackUI;

        [Header("设置")]
        [SerializeField] private float snapThreshold = 60f;

        private int slotIndex = -1;
        private RectTransform rectTransform;
        private Image image;
        private Canvas rootCanvas;
        private Vector3 originalPosition;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            image = GetComponent<Image>();
            rootCanvas = GetComponentInParent<Canvas>();

            // 自动从对应 UI 的 iconImages 列表中定位自身序号
            var icons = isHotbarIcon ? hotbarUI.IconImages : backpackUI.IconImages;
            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] != null && icons[i].gameObject == gameObject)
                {
                    slotIndex = i;
                    break;
                }
            }

            if (slotIndex < 0)
                Debug.LogError($"ItemIconDrag: 在 {(isHotbarIcon ? "HotbarUI" : "BackpackUI")} 的 iconImages 中找不到自身，请确认已正确填入列表。", this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            originalPosition = rectTransform.position;
            // 拖动期间不参与 Raycast，避免挡住目标检测
            image.raycastTarget = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            rectTransform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            image.raycastTarget = true;

            // 用 EventSystem Raycast 找到鼠标下所有 UI 元素
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            ItemIconDrag targetIcon = null;
            foreach (RaycastResult result in results)
            {
                ItemIconDrag candidate = result.gameObject.GetComponent<ItemIconDrag>();
                if (candidate != null && candidate != this)
                {
                    targetIcon = candidate;
                    break;
                }
            }

            // 距离校验
            if (targetIcon != null)
            {
                float dist = Vector3.Distance(eventData.position, targetIcon.rectTransform.position);
                if (dist > snapThreshold)
                    targetIcon = null;
            }

            if (targetIcon != null)
                TryTransfer(targetIcon);

            // 无论结果如何，icon 回原位
            rectTransform.position = originalPosition;
        }

        private void TryTransfer(ItemIconDrag target)
        {
            Hotbar hotbar = hotbarUI.Hotbar;
            Backpack backpack = backpackUI.Backpack;

            int src = slotIndex;
            int dst = target.slotIndex;

            // 目标格有物品时，优先尝试作为附件放入
            Base targetItem = target.GetItem();
            Base selfItem = GetItem();
            if (targetItem != null && selfItem != null)
            {
                for (int i = 0; i < targetItem.attachmentSlots.Count; i++)
                {
                    if (targetItem.attachmentSlots[i].currentItem != null) continue;
                    if (!targetItem.CanAttach(i, selfItem)) continue;

                    Base removed = RemoveSelfFromContainer();
                    if (removed == null) return;
                    targetItem.Attach(i, removed);
                    return;
                }
            }

            // 常规转移
            if (isHotbarIcon && target.isHotbarIcon)
                hotbar.SwapHotbarSlots(src, dst);
            else if (isHotbarIcon && !target.isHotbarIcon)
                hotbar.TransferToBackpack(src, dst);
            else if (!isHotbarIcon && target.isHotbarIcon)
                hotbar.TransferFromBackpack(src, dst);
            else
                backpack.SwapBackpackSlots(src, dst);
        }

        /// <summary>
        /// 获取当前 icon 对应的物品实例
        /// </summary>
        public Base GetItem()
        {
            if (isHotbarIcon)
                return hotbarUI.Hotbar.items[slotIndex];
            else
                return backpackUI.Backpack.PeekItem(slotIndex);
        }

        /// <summary>
        /// 从原容器中取出自身物品
        /// </summary>
        private Base RemoveSelfFromContainer()
        {
            if (isHotbarIcon)
                return hotbarUI.Hotbar.TakeItem(slotIndex);
            else
                return backpackUI.Backpack.TakeItem(slotIndex);
        }
    }
}
