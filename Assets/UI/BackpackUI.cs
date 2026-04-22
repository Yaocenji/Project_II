using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Project_II.InputSystem;
using ProjectII.Manager;

namespace ProjectII.Item
{
    /// <summary>
    /// 背包 UI 管理器，挂载在背包 UI 的根 GameObject 上
    /// 订阅 Backpack 事件驱动图标/数量刷新，自行监听输入控制开关
    /// </summary>
    public class BackpackUI : MonoBehaviour
    {
        [Header("背包 UI 元素")]
        [SerializeField] private List<Image> borderImages;
        [SerializeField] private List<Image> iconImages;
        [SerializeField] private List<TextMeshProUGUI> stackCountTexts;

        [Header("依赖")]
        [SerializeField] private Backpack backpack;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup panelCanvasGroup;

        public IReadOnlyList<Image> IconImages => iconImages;
        public Backpack Backpack => backpack;

        [Header("设置")]
        [SerializeField] private bool defaultOpen = false;
        [SerializeField] private float fadeDuration = 0.2f;

        private InputAction_0 inputActions;
        private bool isPanelOpen = false;
        private bool isTransitioning = false;

        private void Awake()
        {
            InputManager inputManager = InputManager.Instance;
            if (inputManager != null && inputManager.InputAction != null)
                inputActions = inputManager.InputAction;
            else
                Debug.LogError("BackpackUI: 无法从 InputManager 获取 InputAction 引用！");

            // 根据初始开关状态直接设置，不播动画
            isPanelOpen = defaultOpen;
            if (panelCanvasGroup != null)
                panelCanvasGroup.alpha = defaultOpen ? 1f : 0f;
            if (panelRoot != null)
                panelRoot.SetActive(defaultOpen);
        }

        private void Start()
        {
            UpdateAllSlots();
        }

        private void OnEnable()
        {
            if (backpack != null)
                backpack.OnSlotChanged += OnSlotChanged;

            if (inputActions != null)
                inputActions.Character.switchBackPack.started += OnSwitchBackpack;
        }

        private void OnDisable()
        {
            if (backpack != null)
                backpack.OnSlotChanged -= OnSlotChanged;

            if (inputActions != null)
                inputActions.Character.switchBackPack.started -= OnSwitchBackpack;
        }

        private void OnSlotChanged(int slot, Base item, int stackCount)
        {
            UpdateAllSlots();
        }

        private void OnSwitchBackpack(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (isTransitioning) return;
            StartCoroutine(TogglePanel(!isPanelOpen));
        }

        private IEnumerator TogglePanel(bool open)
        {
            isTransitioning = true;
            isPanelOpen = open;

            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                panelRoot.SetActive(true);
                yield return StartCoroutine(FadeCanvasGroup(0f, 1f));
            }
            else
            {
                yield return StartCoroutine(FadeCanvasGroup(1f, 0f));
                panelRoot.SetActive(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            isTransitioning = false;
        }

        private IEnumerator FadeCanvasGroup(float from, float to)
        {
            if (panelCanvasGroup == null) yield break;

            float elapsed = 0f;
            panelCanvasGroup.alpha = from;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                panelCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
                yield return null;
            }

            panelCanvasGroup.alpha = to;
        }

        /// <summary>
        /// 刷新所有格子的图标和堆叠数量显示
        /// </summary>
        private void UpdateAllSlots()
        {
            for (int i = 0; i < iconImages.Count; i++)
            {
                if (iconImages[i] == null) continue;

                Base item = (backpack != null && i < backpack.slots.Length) ? backpack.PeekItem(i) : null;
                Sprite icon = item?.icon;
                int count = backpack != null ? backpack.GetStackCount(i) : 0;

                // 图标
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

                // 堆叠数量
                if (i < stackCountTexts.Count && stackCountTexts[i] != null)
                {
                    if (count > 1)
                    {
                        stackCountTexts[i].text = count.ToString();
                        Color c = stackCountTexts[i].color;
                        c.a = 1f;
                        stackCountTexts[i].color = c;
                    }
                    else
                    {
                        Color c = stackCountTexts[i].color;
                        c.a = 0f;
                        stackCountTexts[i].color = c;
                    }
                }
            }
        }
    }
}
