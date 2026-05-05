using System;
using UnityEngine;
using Project_II.InputSystem;
using ProjectII.Manager;

namespace ProjectII.Interact
{
    /// <summary>
    /// 所有可交互物品的抽象基类
    /// 统一管理：输入订阅/取消、交互状态切换、向GameSceneManager注册近距离可交互列表
    /// 具体何时进入/退出可交互状态，由子类自行决定
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour
    {
        protected InputAction_0 inputActions;
        public bool canInteract = false;
        
        [Header("仿UI高亮物体")]
        public SpriteRenderer highLightSR;

        private bool _chosen = false;
        /// <summary>
        /// 被选中
        /// </summary>
        public void SetChosen(bool chosenNew)
        {
            _chosen = chosenNew;
        }

        private void Update()
        {
            UpdateHighLight();
            InteractableUpdate();
        }

        protected void UpdateHighLight()
        {
            if (highLightSR != null)
            {
                if (_chosen && Mathf.Abs(1 - highLightSR.color.a) >= .001f)
                {
                    highLightSR.color = new Color(highLightSR.color.r, highLightSR.color.g, highLightSR.color.b,
                        (highLightSR.color.a + 1) / 2.0f);
                }
                else if (!_chosen && Mathf.Abs(highLightSR.color.a) >= .001f)
                {
                    highLightSR.color = new Color(highLightSR.color.r, highLightSR.color.g, highLightSR.color.b,
                        highLightSR.color.a / 2.0f);
                }
            }
        }

        protected virtual void InteractableUpdate()
        {
        }

        protected virtual void Awake()
        {
            GetInputActionFromInputManager();
        }

        protected virtual void OnDestroy()
        {
            ExitInteractableRange();
        }

        public void OnInteractionInput()
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
            InteractManager.Instance?.RegisterNearbyInteractable(this);
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
            InteractManager.Instance?.UnregisterNearbyInteractable(this);
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
