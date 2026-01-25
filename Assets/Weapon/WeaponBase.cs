using UnityEngine;
using Project_II.InputSystem;
using ProjectII.Manager;
using UnityEngine.Pool;

namespace ProjectII.Weapon
{
    /// <summary>
    /// 所有武器的基类
    /// 从InputManager获取输入，并调用相应的虚方法
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Header("Input System")]
        [SerializeField] private InputAction_0 inputActions; // 从InputManager获取，不应在Inspector中手动赋值

        // 输入状态缓存
        private bool mainAttackWasPressed;
        private bool mainAttackIsHolding;
        private bool secondaryAttackWasPressed;
        private bool secondaryAttackIsHolding;

        // 将子弹池化，这里保留空实现，子类可以重写
        protected ObjectPool<GameObject> bulletPool;

        // 武器散布角度（单侧、角度）
        private float spreadAngle = 5.0f;

        protected virtual void Awake()
        {
            // 从InputManager获取InputAction引用
            GetInputActionFromInputManager();

            // 注册输入回调
            if (inputActions != null)
            {
                inputActions.Character.mainAttack.started += OnMainAttackStarted;
                inputActions.Character.mainAttack.canceled += OnMainAttackCanceled;
                inputActions.Character.secondaryAttack.started += OnSecondaryAttackStarted;
                inputActions.Character.secondaryAttack.canceled += OnSecondaryAttackCanceled;
                inputActions.Character.reload.started += OnReloadStarted;
            }
        }

        private void OnEnable()
        {
            if (inputActions != null)
            {
                inputActions.Enable();
            }
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Disable();
            }
        }

        private void Update()
        {
            if (inputActions == null)
            {
                return;
            }

            // 检测主要攻击的持续按下状态
            if (mainAttackIsHolding && inputActions.Character.mainAttack.IsPressed())
            {
                // 持续按下时，每帧调用MainAttackHold
                MainAttackHold();
            }

            // 检测次要攻击的持续按下状态
            if (secondaryAttackIsHolding && inputActions.Character.secondaryAttack.IsPressed())
            {
                // 持续按下时，每帧调用SecondaryAttackHold
                SecondaryAttackHold();
            }
        }

        /// <summary>
        /// 主要攻击按下时的回调
        /// </summary>
        private void OnMainAttackStarted(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!mainAttackWasPressed)
            {
                mainAttackWasPressed = true;
                mainAttackIsHolding = true;
                MainAttackPress();
            }
        }

        /// <summary>
        /// 主要攻击抬起时的回调
        /// </summary>
        private void OnMainAttackCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (mainAttackWasPressed)
            {
                mainAttackWasPressed = false;
                mainAttackIsHolding = false;
                MainAttackRelease();
            }
        }

        /// <summary>
        /// 次要攻击按下时的回调
        /// </summary>
        private void OnSecondaryAttackStarted(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!secondaryAttackWasPressed)
            {
                secondaryAttackWasPressed = true;
                secondaryAttackIsHolding = true;
                SecondaryAttackPress();
            }
        }

        /// <summary>
        /// 次要攻击抬起时的回调
        /// </summary>
        private void OnSecondaryAttackCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (secondaryAttackWasPressed)
            {
                secondaryAttackWasPressed = false;
                secondaryAttackIsHolding = false;
                SecondaryAttackRelease();
            }
        }

        /// <summary>
        /// 重新装填按下时的回调
        /// </summary>
        private void OnReloadStarted(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            ReloadPress();
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

        private void OnDestroy()
        {
            // 取消注册输入回调
            if (inputActions != null)
            {
                inputActions.Character.mainAttack.started -= OnMainAttackStarted;
                inputActions.Character.mainAttack.canceled -= OnMainAttackCanceled;
                inputActions.Character.secondaryAttack.started -= OnSecondaryAttackStarted;
                inputActions.Character.secondaryAttack.canceled -= OnSecondaryAttackCanceled;
                inputActions.Character.reload.started -= OnReloadStarted;
            }
        }

        #region 纯虚方法 - 子类必须实现

        /// <summary>
        /// 主要攻击被按下的瞬间调用一次
        /// </summary>
        protected abstract void MainAttackPress();

        /// <summary>
        /// 主要攻击按下的持续时间内，每个Update都被调用
        /// </summary>
        protected abstract void MainAttackHold();

        /// <summary>
        /// 主要攻击被抬起的瞬间调用一次
        /// </summary>
        protected abstract void MainAttackRelease();

        /// <summary>
        /// 次要攻击被按下的瞬间调用一次
        /// </summary>
        protected abstract void SecondaryAttackPress();

        /// <summary>
        /// 次要攻击按下的持续时间内，每个Update都被调用
        /// </summary>
        protected abstract void SecondaryAttackHold();

        /// <summary>
        /// 次要攻击被抬起的瞬间调用一次
        /// </summary>
        protected abstract void SecondaryAttackRelease();


        /// <summary>
        /// 重新装填按下时的回调
        /// </summary>
        protected abstract void ReloadPress();

        #endregion
    }
}
