using UnityEngine;
using Project_II.InputSystem;
using ProjectII.Manager;

namespace ProjectII.SceneItems
{
    /// <summary>
    /// 所有可交互物品的抽象基类
    /// 统一管理：输入订阅/取消、交互状态切换、向GameSceneManager注册近距离可交互列表
    /// 具体何时进入/退出可交互状态，由子类自行决定
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour
    {
        protected InputAction_0 inputActions;
        protected bool canInteract = false;

        protected virtual void Awake()
        {
            GetInputActionFromInputManager();
            if (inputActions != null)
                inputActions.Character.interaction.started += OnInteractionInput;
        }

        protected virtual void OnDestroy()
        {
            if (inputActions != null)
                inputActions.Character.interaction.started -= OnInteractionInput;
            ExitInteractableRange();
        }

        private void OnInteractionInput(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (canInteract)
            {
                OnInteract();
            }
        }

        /// <summary>
        /// 进入可交互状态，由子类在合适时机调用
        /// </summary>
        protected void EnterInteractableRange()
        {
            if (canInteract)
            {
                return;
            }

            canInteract = true;
            GameSceneManager.Instance?.RegisterNearbyInteractable(this);
            OnEnterInteractRange();
        }

        /// <summary>
        /// 离开可交互状态，由子类在合适时机调用
        /// </summary>
        protected void ExitInteractableRange()
        {
            if (!canInteract)
            {
                return;
            }

            canInteract = false;
            GameSceneManager.Instance?.UnregisterNearbyInteractable(this);
            OnExitInteractRange();
        }

        /// <summary>
        /// 从InputManager获取InputAction引用
        /// </summary>
        private void GetInputActionFromInputManager()
        {
            if (inputActions == null)
            {
                InputManager inputManager = InputManager.Instance;
                if (inputManager != null && inputManager.InputAction != null)
                {
                    inputActions = inputManager.InputAction;
                }
                else
                {
                    Debug.LogError("无法从InputManager获取InputAction引用！请确保场景中存在InputManager。");
                }
            }
        }

        /// <summary>
        /// 玩家按下交互键时调用（仅在 canInteract 为 true 时触发）
        /// </summary>
        protected abstract void OnInteract();

        /// <summary>
        /// 进入交互范围后的附加逻辑
        /// </summary>
        protected virtual void OnEnterInteractRange() { }

        /// <summary>
        /// 离开交互范围后的附加逻辑
        /// </summary>
        protected virtual void OnExitInteractRange() { }
    }
}
