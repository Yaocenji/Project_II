using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Project_II.InputSystem;
using ProjectII.Manager;

namespace ProjectII.UI
{
    /// <summary>
    /// 手机操作系统核心（场景单例）。
    /// 负责：手机开关动画、桌面管理、App 注册与切换、交互屏蔽。
    /// </summary>
    [DefaultExecutionOrder(-97)]
    public class PhoneOS : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform phoneRect;

        [Header("桌面")]
        [SerializeField] private GameObject homeScreenPanel;

        [Header("已注册 App")]
        [SerializeField] private List<PhoneApp> apps = new List<PhoneApp>();

        [Header("收起状态")]
        [SerializeField] private Vector2 collapsedAnchoredPos = new Vector2(-300f, -200f);
        [SerializeField] private float collapsedScale = 0.3f;

        [Header("展开状态")]
        [SerializeField] private Vector2 expandedAnchoredPos = Vector2.zero;
        [SerializeField] private float expandedScale = 1f;

        [Header("动画设置")]
        [SerializeField] private float toggleDuration = 0.25f;
        [SerializeField] private Ease openEase = Ease.OutBack;
        [SerializeField] private Ease closeEase = Ease.InBack;

        [Header("背景遮罩")]
        [SerializeField] private GameObject darkBG;

        // 从 InputManager 获取的输入引用
        private InputAction_0 inputActions;

        // 手机总开关状态
        private bool isPhoneOpen = false;

        // 当前打开的 App 索引（-1 表示在桌面）
        private int currentAppIndex = -1;

        // 当前播放的动画
        private Tween currentTween;

        private static PhoneOS instance;

        /// <summary>
        /// PhoneOS 场景单例实例
        /// </summary>
        public static PhoneOS Instance
        {
            get
            {
                if (instance == null)
                    instance = FindObjectOfType<PhoneOS>();
                return instance;
            }
        }

        #region Unity 生命周期

        private void Awake()
        {
            if (instance == null)
                instance = this;
            else if (instance != this)
            {
                Debug.LogWarning("PhoneOS 单例已经存在，销毁新创建的实例。");
                Destroy(gameObject);
                return;
            }

            InputManager inputManager = InputManager.Instance;
            if (inputManager != null && inputManager.InputAction != null)
                inputActions = inputManager.InputAction;
            else
                Debug.LogError("PhoneOS: 无法从 InputManager 获取 InputAction 引用！");

            InitCollapsedState();
        }

        private void OnEnable()
        {
            if (inputActions != null)
                inputActions.Character.switchPhone.started += OnSwitchPhone;
        }

        private void OnDisable()
        {
            if (inputActions != null)
                inputActions.Character.switchPhone.started -= OnSwitchPhone;
        }

        private void OnDestroy()
        {
            currentTween.Kill();
            if (instance == this)
                instance = null;
        }

        #endregion

        #region 初始化和开关

        /// <summary>
        /// 初始化为收起状态，不播动画
        /// </summary>
        private void InitCollapsedState()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            phoneRect.anchoredPosition = collapsedAnchoredPos;
            phoneRect.localScale = Vector3.one * collapsedScale;

            if (darkBG != null)
                darkBG.SetActive(false);

            if (homeScreenPanel != null)
                homeScreenPanel.SetActive(false);

            foreach (var app in apps)
            {
                if (app != null)
                    app.Close();
            }
        }

        /// <summary>
        /// 响应 switchPhone 输入
        /// </summary>
        private void OnSwitchPhone(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (!isPhoneOpen)
                OpenPhone();
            else if (currentAppIndex >= 0)
                GoHome();    // 在 App 内时，先回到桌面
            else
                ClosePhone(); // 在桌面时，关闭手机
        }

        #endregion

        #region 手机开关

        /// <summary>
        /// 展开手机，回到桌面
        /// </summary>
        public void OpenPhone()
        {
            if (isPhoneOpen) return;
            isPhoneOpen = true;
            currentTween.Kill();

            if (darkBG != null)
                darkBG.SetActive(true);

            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            if (InteractManager.Instance != null)
                InteractManager.Instance.enabled = false;

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            var seq = DOTween.Sequence();
            seq.Join(canvasGroup.DOFade(1f, toggleDuration));
            seq.Join(phoneRect.DOAnchorPos(expandedAnchoredPos, toggleDuration).SetEase(openEase));
            seq.Join(phoneRect.DOScale(expandedScale, toggleDuration).SetEase(openEase));
            seq.OnComplete(() => ShowHomeScreen());
            currentTween = seq;
        }

        /// <summary>
        /// 收起手机
        /// </summary>
        public void ClosePhone()
        {
            if (!isPhoneOpen) return;
            isPhoneOpen = false;
            currentTween.Kill();

            // 关闭当前 App（如果有）
            CloseCurrentApp();

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (InteractManager.Instance != null)
                InteractManager.Instance.enabled = true;

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            var seq = DOTween.Sequence();
            seq.Join(canvasGroup.DOFade(0f, toggleDuration));
            seq.Join(phoneRect.DOAnchorPos(collapsedAnchoredPos, toggleDuration).SetEase(closeEase));
            seq.Join(phoneRect.DOScale(collapsedScale, toggleDuration).SetEase(closeEase));
            seq.OnComplete(() =>
            {
                if (darkBG != null)
                    darkBG.SetActive(false);
                if (homeScreenPanel != null)
                    homeScreenPanel.SetActive(false);
            });
            currentTween = seq;
        }

        #endregion

        #region App 管理

        /// <summary>
        /// 打开指定索引的 App（由桌面图标点击调用）
        /// </summary>
        /// <param name="appIndex">apps 列表中的索引</param>
        public void OpenApp(int appIndex)
        {
            if (appIndex < 0 || appIndex >= apps.Count) return;
            if (apps[appIndex] == null) return;

            CloseCurrentApp();

            if (homeScreenPanel != null)
                homeScreenPanel.SetActive(false);

            currentAppIndex = appIndex;
            apps[appIndex].Open();
        }

        /// <summary>
        /// 返回桌面
        /// </summary>
        public void GoHome()
        {
            CloseCurrentApp();
            ShowHomeScreen();
        }

        private void CloseCurrentApp()
        {
            if (currentAppIndex >= 0 && currentAppIndex < apps.Count && apps[currentAppIndex] != null)
            {
                apps[currentAppIndex].Close();
            }
            currentAppIndex = -1;
        }

        private void ShowHomeScreen()
        {
            if (homeScreenPanel != null)
                homeScreenPanel.SetActive(true);
        }

        #endregion
    }
}
