using UnityEngine;
using UnityEngine.InputSystem;
using Project_II.InputSystem;

namespace ProjectII.Manager
{
    /// <summary>
    /// 管理场景的inputAction
    /// 和GameSceneManager一样，尽量在场景其他物体创建前创建
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class InputManager : MonoBehaviour
    {
        [Header("Input Action")]
        [SerializeField] private InputAction_0 inputAction;

        /// <summary>
        /// InputAction实例
        /// 初始化的时候创建一个inputAction，其他所有涉及玩家输入的脚本，都从这里拿引用，不要单独创建了
        /// </summary>
        public InputAction_0 InputAction
        {
            get => inputAction;
            private set => inputAction = value;
        }

        /// <summary>
        /// 当前输入设备类型
        /// 每帧调用，检测玩家的输入设备类型，其他所有涉及玩家输入的脚本，需要判断设备类型的时候，也从这里拿就行
        /// </summary>
        public InputDeviceType CurrentDeviceType { get; private set; }

        /// <summary>
        /// 输入设备类型枚举
        /// </summary>
        public enum InputDeviceType
        {
            KeyboardMouse,  // 键鼠
            Gamepad         // 手柄
        }

        private static InputManager instance;

        /// <summary>
        /// InputManager单例实例
        /// </summary>
        public static InputManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<InputManager>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            // 确保只有一个InputManager实例
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Debug.LogWarning("InputManager 单例已经存在，销毁新创建的实例。");
                Destroy(gameObject);
                return;
            }

            // 初始化InputAction
            InitializeInputAction();
        }

        private void Start()
        {
            // 初始化设备类型
            UpdateDeviceType();
        }

        private void Update()
        {
            // 每帧检测玩家的输入设备类型
            UpdateDeviceType();
        }

        private void OnEnable()
        {
            if (inputAction != null)
            {
                inputAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (inputAction != null)
            {
                inputAction.Disable();
            }
        }

        /// <summary>
        /// 初始化InputAction
        /// </summary>
        private void InitializeInputAction()
        {
            if (inputAction == null)
            {
                inputAction = new InputAction_0();
                //Debug.Log("InputAction 已创建");
            }
        }

        /// <summary>
        /// 每帧调用，检测玩家的输入设备类型
        /// </summary>
        private void UpdateDeviceType()
        {
            // 检测当前活动的输入设备
            // 优先检测手柄，因为手柄输入可能和键鼠同时存在
            if (Gamepad.current != null && IsGamepadActive())
            {
                CurrentDeviceType = InputDeviceType.Gamepad;
            }
            else if (Keyboard.current != null || Mouse.current != null)
            {
                CurrentDeviceType = InputDeviceType.KeyboardMouse;
            }
            else
            {
                // 如果没有检测到设备，保持当前类型（默认为键鼠）
                if (CurrentDeviceType == InputDeviceType.Gamepad && Gamepad.current == null)
                {
                    CurrentDeviceType = InputDeviceType.KeyboardMouse;
                }
            }
        }

        /// <summary>
        /// 检测手柄是否处于活动状态（有输入）
        /// </summary>
        private bool IsGamepadActive()
        {
            if (Gamepad.current == null)
            {
                return false;
            }

            // 检查手柄是否有任何输入
            var gamepad = Gamepad.current;
            return gamepad.leftStick.IsActuated() ||
                   gamepad.rightStick.IsActuated() ||
                   gamepad.dpad.IsActuated() ||
                   gamepad.buttonSouth.IsPressed() ||
                   gamepad.buttonNorth.IsPressed() ||
                   gamepad.buttonEast.IsPressed() ||
                   gamepad.buttonWest.IsPressed() ||
                   gamepad.leftShoulder.IsPressed() ||
                   gamepad.rightShoulder.IsPressed() ||
                   gamepad.leftTrigger.IsPressed() ||
                   gamepad.rightTrigger.IsPressed();
        }

        private void OnDestroy()
        {
            // 清理InputAction
            if (inputAction != null)
            {
                inputAction.Dispose();
                inputAction = null;
            }

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
