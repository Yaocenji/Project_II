using UnityEngine;

namespace ProjectII.UI
{
    /// <summary>
    /// 手机 App 基类。
    /// 每个手机 App 面板挂载此脚本，由 PhoneOS 统一管理生命周期。
    /// </summary>
    public abstract class PhoneApp : MonoBehaviour
    {
        [Header("App 基础")]
        [Tooltip("此 App 的 UI 根 Panel，开/关时由 PhoneOS 控制显示")]
        [SerializeField] protected GameObject appPanel;

        [Tooltip("此 App 在桌面图标上显示的名称")]
        [SerializeField] protected string appName = "未命名";

        [Tooltip("桌面图标 Sprite，由 HomeScreen 读取")]
        [SerializeField] protected Sprite appIcon;

        /// <summary>此 App 当前是否处于打开状态</summary>
        public bool IsOpen { get; private set; }

        /// <summary>App 显示名称（只读）</summary>
        public string AppName => appName;

        /// <summary>App 桌面图标（只读）</summary>
        public Sprite AppIcon => appIcon;

        /// <summary>
        /// 由 PhoneOS 调用，打开此 App
        /// </summary>
        public void Open()
        {
            IsOpen = true;
            if (appPanel != null)
                appPanel.SetActive(true);
            OnOpen();
        }

        /// <summary>
        /// 由 PhoneOS 调用，关闭此 App
        /// </summary>
        public void Close()
        {
            IsOpen = false;
            OnClose();
            if (appPanel != null)
                appPanel.SetActive(false);
        }

        /// <summary>
        /// App 被打开时调用，子类在此初始化数据、订阅事件
        /// </summary>
        protected virtual void OnOpen() { }

        /// <summary>
        /// App 被关闭时调用，子类在此清理资源、取消订阅
        /// </summary>
        protected virtual void OnClose() { }
    }
}
