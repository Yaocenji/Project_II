using UnityEngine;
using DG.Tweening;
using Project_II.InputSystem;
using ProjectII.Manager;

namespace ProjectII.UI
{
    /// <summary>
    /// 手机 UI 管理器，挂载在手机 Canvas 的根 Panel 上
    /// 负责：开关手机、播放展开/收起动画、控制交互屏蔽
    /// </summary>
    public class PhoneUI : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform phoneRect;

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

        [Header("背景")]
        [SerializeField] private GameObject darkBG;

        private InputAction_0 inputActions;
        private bool isOpen = false;
        private Tween currentTween;

        private void Awake()
        {
            InputManager inputManager = InputManager.Instance;
            if (inputManager != null && inputManager.InputAction != null)
                inputActions = inputManager.InputAction;
            else
                Debug.LogError("PhoneUI: 无法从 InputManager 获取 InputAction 引用！");

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
        }

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
        }

        /// <summary>
        /// 切换手机开关
        /// </summary>
        private void OnSwitchPhone(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (isOpen)
                Close();
            else
                Open();
        }

        /// <summary>
        /// 展开手机
        /// </summary>
        private void Open()
        {
            isOpen = true;
            currentTween.Kill();

            if (darkBG != null)
                darkBG.SetActive(true);

            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            if (InteractManager.Instance != null)
                InteractManager.Instance.enabled = false;

            var seq = DOTween.Sequence();
            seq.Join(canvasGroup.DOFade(1f, toggleDuration));
            seq.Join(phoneRect.DOAnchorPos(expandedAnchoredPos, toggleDuration).SetEase(openEase));
            seq.Join(phoneRect.DOScale(expandedScale, toggleDuration).SetEase(openEase));
            currentTween = seq;
        }

        /// <summary>
        /// 收起手机
        /// </summary>
        private void Close()
        {
            isOpen = false;
            currentTween.Kill();

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (InteractManager.Instance != null)
                InteractManager.Instance.enabled = true;

            var seq = DOTween.Sequence();
            seq.Join(canvasGroup.DOFade(0f, toggleDuration));
            seq.Join(phoneRect.DOAnchorPos(collapsedAnchoredPos, toggleDuration).SetEase(closeEase));
            seq.Join(phoneRect.DOScale(collapsedScale, toggleDuration).SetEase(closeEase));
            seq.OnComplete(() =>
            {
                if (darkBG != null)
                    darkBG.SetActive(false);
            });
            currentTween = seq;
        }
    }
}
