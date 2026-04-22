using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectII.Item
{
    /// <summary>
    /// 快捷栏 UI 管理器，挂载在快捷栏 UI 的根 GameObject 上
    /// 订阅 Hotbar 事件，驱动图标和高亮块的刷新
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        [Header("快捷栏 UI 元素")]
        [SerializeField] private List<Image> borderImages;
        [SerializeField] private List<Image> iconImages;
        [SerializeField] private Image highlightImage;

        [Header("依赖")]
        [SerializeField] private Hotbar hotbar;

        public IReadOnlyList<Image> IconImages => iconImages;
        public Hotbar Hotbar => hotbar;

        private void Start()
        {
            UpdateAllIcons();
            UpdateHighlight();
        }

        private void OnEnable()
        {
            if (hotbar == null) return;
            hotbar.OnSlotChanged += OnSlotChanged;
            hotbar.OnActiveSlotChanged += OnActiveSlotChanged;
        }

        private void OnDisable()
        {
            if (hotbar == null) return;
            hotbar.OnSlotChanged -= OnSlotChanged;
            hotbar.OnActiveSlotChanged -= OnActiveSlotChanged;
        }

        private void OnSlotChanged(int slot, Base item)
        {
            UpdateAllIcons();
        }

        private void OnActiveSlotChanged(int oldSlot, int newSlot)
        {
            UpdateHighlight();
        }

        /// <summary>
        /// 刷新所有格子的图标显示
        /// </summary>
        private void UpdateAllIcons()
        {
            for (int i = 0; i < iconImages.Count; i++)
            {
                if (iconImages[i] == null) continue;

                Base item = (hotbar != null && i < hotbar.items.Length) ? hotbar.items[i] : null;
                Sprite icon = item?.icon;

                if (icon != null)
                {
                    iconImages[i].sprite = icon;
                    Color c = iconImages[i].color;
                    c.a = 1f;
                    iconImages[i].color = c;
                }
                else
                {
                    Color c = iconImages[i].color;
                    c.a = 0f;
                    iconImages[i].color = c;
                }
            }
        }

        /// <summary>
        /// 将高亮块的位置对齐到当前选中格的边框位置
        /// </summary>
        private void UpdateHighlight()
        {
            if (highlightImage == null || hotbar == null) return;

            int index = hotbar.CurrentSlotIndex;
            if (index < 0 || index >= borderImages.Count || borderImages[index] == null) return;

            highlightImage.transform.position = borderImages[index].transform.position;
        }
    }
}
