using UnityEngine;
using UnityEngine.UI;
using Project_II.InputSystem;
using UnityEngine.InputSystem;

namespace ProjectII.Character
{
    /// <summary>
    /// 鼠标控制器，控制UI元素鼠标（不是真正的鼠标）
    /// </summary>
    public class Mouse : MonoBehaviour
    {
        [Header("Input System")]
        [SerializeField] private InputAction_0 inputActions; // 从InputManager获取，不应在Inspector中手动赋值

        [Header("Virtual Mouse UI")]
        [SerializeField] private RectTransform virtualMouseRectTransform; // 虚拟鼠标UI元素

        [Header("Settings")]
        [SerializeField] private float sensitivity = 1f; // 可以控制鼠标移动的系数

        [Header("Gamepad Settings")]
        [SerializeField] private float gamepadLookDistance = 100f; // 手柄模式下，虚拟鼠标距离角色的距离

        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Camera mainCamera;

        // 当前虚拟鼠标的世界位置（用于计算朝向）
        private Vector2 virtualMouseWorldPosition;
        
        public Vector2 VirtualMouseWorldPosition{get =>  virtualMouseWorldPosition;}

        // 是否使用手柄输入
        private bool isUsingGamepad = false;

        private void Awake()
        {
            // 从InputManager获取InputAction引用
            GetInputActionFromInputManager();

            // CharacterController将通过GameSceneManager获取
            // 注册到GameSceneManager
            RegisterToGameSceneManager();

            // 从GameSceneManager获取CharacterController
            GetCharacterControllerFromGameSceneManager();

            // 如果没有指定Camera，尝试获取主相机
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // 如果没有指定虚拟鼠标UI，尝试在当前GameObject上查找
            if (virtualMouseRectTransform == null)
            {
                virtualMouseRectTransform = GetComponent<RectTransform>();
            }
        }

        private void OnEnable()
        {
            if (inputActions != null)
            {
                inputActions.Enable();
            }

            // 锁定并隐藏真实鼠标
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Disable();
            }

            // 解锁并显示真实鼠标
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            // 如果CharacterController还未获取，尝试获取
            if (characterController == null)
            {
                GetCharacterControllerFromGameSceneManager();
            }

            // 每帧更新虚拟鼠标位置
            MouseMove();

            // 更新角色朝向
            UpdateCharacterLookDirection();
        }

        /// <summary>
        /// 每帧通过输入的inputAction，将虚拟鼠标放置到正确位置。
        /// 真正的鼠标被锁住位置、隐藏显示。
        /// 如果玩家用键鼠游玩，那么玩家滑动真实鼠标的时候就会操纵虚拟鼠标的位置。
        /// 如果玩家用手柄游玩，那么玩家的右摇杆指定了一个方向，此时将虚拟鼠标放到基于玩家角色，加上该方向上*某一单位指定长度的位置，同时"隐藏这个虚拟鼠标的显示"，达到"玩家通过右摇杆直接指定角色朝向"的效果（这样做能让玩家朝向的逻辑统一，不论输入设备）
        /// </summary>
        public void MouseMove()
        {
            if (inputActions == null)
            {
                return;
            }

            Vector2 mouseInput = inputActions.Character.mouse.ReadValue<Vector2>();

            // 检测是否使用手柄（通过检查是否有右摇杆输入）
            // 如果右摇杆有输入，则认为是手柄模式
            if (mouseInput.magnitude > 0.1f && IsGamepadInput())
            {
                isUsingGamepad = true;
                HandleGamepadInput(mouseInput);
            }
            else if (mouseInput.magnitude > 0.1f)
            {
                isUsingGamepad = false;
                HandleMouseInput(mouseInput);
            }
            else
            {
                // 没有输入时，保持当前状态
                isUsingGamepad = false;
                
                // 即使鼠标没有移动，也要更新虚拟鼠标的世界位置
                // 因为摄像机可能跟随角色移动，导致屏幕位置对应的世界位置发生变化
                UpdateVirtualMouseWorldPositionFromScreen();
            }
        }

        /// <summary>
        /// 处理键鼠输入
        /// </summary>
        private void HandleMouseInput(Vector2 mouseDelta)
        {
            if (virtualMouseRectTransform == null || mainCamera == null)
            {
                return;
            }

            // 获取Canvas
            Canvas canvas = virtualMouseRectTransform.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            // 将鼠标delta转换为屏幕空间移动
            Vector2 screenDelta = mouseDelta * sensitivity;

            // 获取当前虚拟鼠标的屏幕位置
            Vector2 currentScreenPos = RectTransformUtility.WorldToScreenPoint(mainCamera, virtualMouseRectTransform.position);
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                currentScreenPos = virtualMouseRectTransform.anchoredPosition;
            }

            // 计算新位置
            Vector2 newScreenPos = currentScreenPos + screenDelta;

            // 限制在屏幕范围内
            newScreenPos.x = Mathf.Clamp(newScreenPos.x, 0, Screen.width);
            newScreenPos.y = Mathf.Clamp(newScreenPos.y, 0, Screen.height);

            // 更新虚拟鼠标位置
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                virtualMouseRectTransform.anchoredPosition = newScreenPos;
            }
            else
            {
                Vector3 worldPos;
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    virtualMouseRectTransform.parent as RectTransform,
                    newScreenPos,
                    canvas.worldCamera ?? mainCamera,
                    out worldPos);
                virtualMouseRectTransform.position = worldPos;
            }

            // 将屏幕坐标转换为世界坐标（用于计算朝向）
            virtualMouseWorldPosition = ScreenToWorldPosition(newScreenPos);

            // 显示虚拟鼠标UI（键鼠模式下）
            ShowVirtualMouse();
        }

        /// <summary>
        /// 处理手柄输入
        /// </summary>
        private void HandleGamepadInput(Vector2 rightStickInput)
        {
            if (characterController == null || mainCamera == null)
            {
                return;
            }

            // 归一化输入方向
            Vector2 direction = rightStickInput.normalized;

            // 如果输入太小，不更新位置
            if (rightStickInput.magnitude < 0.1f)
            {
                return;
            }

            // 计算虚拟鼠标的世界位置：角色位置 + 方向 * 距离
            Vector2 characterPosition = characterController.transform.position;
            virtualMouseWorldPosition = characterPosition + direction * gamepadLookDistance;

            // 隐藏虚拟鼠标UI（手柄模式下）
            HideVirtualMouse();
        }

        /// <summary>
        /// 显示虚拟鼠标UI
        /// </summary>
        private void ShowVirtualMouse()
        {
            if (virtualMouseRectTransform != null)
            {
                Image mouseImage = virtualMouseRectTransform.GetComponent<Image>();
                if (mouseImage != null)
                {
                    Color color = mouseImage.color;
                    color.a = 1f; // 显示
                    mouseImage.color = color;
                }
                else
                {
                    // 如果没有Image组件，尝试启用GameObject
                    virtualMouseRectTransform.gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 隐藏虚拟鼠标UI
        /// </summary>
        private void HideVirtualMouse()
        {
            if (virtualMouseRectTransform != null)
            {
                Image mouseImage = virtualMouseRectTransform.GetComponent<Image>();
                if (mouseImage != null)
                {
                    Color color = mouseImage.color;
                    color.a = 0f; // 隐藏
                    mouseImage.color = color;
                }
                else
                {
                    // 如果没有Image组件，尝试禁用GameObject
                    virtualMouseRectTransform.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 更新角色朝向
        /// 玩家角色的朝向，每一帧都：平滑的由当前朝向指向目标朝向（鼠标位置-玩家角色位置=玩家的目标朝向）
        /// </summary>
        private void UpdateCharacterLookDirection()
        {
            if (characterController == null)
            {
                return;
            }

            // 计算目标朝向：鼠标位置 - 玩家角色位置
            Vector2 characterPosition = characterController.transform.position;
            Vector2 targetDirection = (virtualMouseWorldPosition - characterPosition).normalized;

            // 如果方向有效，设置角色朝向
            if (targetDirection.magnitude > 0.01f)
            {
                SetDirection(targetDirection);
            }

            //Debug.Log("UpdateCharacterLookDirection: " + targetDirection);
        }

        /// <summary>
        /// 设置玩家角色 ProjectII.Character.CharacterController 的Direction
        /// </summary>
        public void SetDirection(Vector2 direction)
        {
            if (characterController == null)
            {
                return;
            }

            // 设置角色的目标朝向方向
            characterController.SetTargetLookDirection(direction);
        }

        /// <summary>
        /// 将屏幕坐标转换为世界坐标
        /// </summary>
        private Vector2 ScreenToWorldPosition(Vector2 screenPosition)
        {
            if (mainCamera == null)
            {
                return Vector2.zero;
            }

            // 将屏幕坐标转换为世界坐标
            // 对于正交相机，Z 参数表示从相机到目标平面的距离
            // 对于2D游戏，通常场景在 Z=0 平面，相机在负 Z 方向
            // 所以使用相机到原点的距离（即相机的 Z 位置的绝对值）
            float cameraZ = mainCamera.orthographic ? 
                Mathf.Abs(mainCamera.transform.position.z) : 
                mainCamera.nearClipPlane;
            
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, cameraZ));
            return new Vector2(worldPos.x, worldPos.y);
        }

        /// <summary>
        /// 从当前虚拟鼠标的屏幕位置更新世界位置
        /// 用于在鼠标不移动但摄像机移动时更新朝向
        /// </summary>
        private void UpdateVirtualMouseWorldPositionFromScreen()
        {
            if (virtualMouseRectTransform == null || mainCamera == null)
            {
                return;
            }

            // 获取Canvas
            Canvas canvas = virtualMouseRectTransform.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            // 获取当前虚拟鼠标的屏幕位置
            Vector2 currentScreenPos;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                currentScreenPos = virtualMouseRectTransform.anchoredPosition;
            }
            else
            {
                currentScreenPos = RectTransformUtility.WorldToScreenPoint(
                    canvas.worldCamera ?? mainCamera, 
                    virtualMouseRectTransform.position);
            }

            // 将屏幕坐标转换为世界坐标（用于计算朝向）
            virtualMouseWorldPosition = ScreenToWorldPosition(currentScreenPos);
        }

        /// <summary>
        /// 检测当前是否使用手柄输入
        /// 优先从InputManager获取设备类型，如果没有则使用本地检测
        /// </summary>
        private bool IsGamepadInput()
        {
            // 优先从InputManager获取设备类型
            ProjectII.Manager.InputManager inputManager = ProjectII.Manager.InputManager.Instance;
            if (inputManager != null)
            {
                return inputManager.CurrentDeviceType == ProjectII.Manager.InputManager.InputDeviceType.Gamepad;
            }

            // 如果没有InputManager，使用本地检测作为备选
            return UnityEngine.InputSystem.Gamepad.current != null && 
                   UnityEngine.InputSystem.Gamepad.current.rightStick.IsActuated();
        }

        /// <summary>
        /// 从InputManager获取InputAction引用
        /// </summary>
        private void GetInputActionFromInputManager()
        {
            if (inputActions == null)
            {
                ProjectII.Manager.InputManager inputManager = ProjectII.Manager.InputManager.Instance;
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
        /// 注册到GameSceneManager
        /// </summary>
        private void RegisterToGameSceneManager()
        {
            ProjectII.Manager.GameSceneManager gameSceneManager = ProjectII.Manager.GameSceneManager.Instance;
            if (gameSceneManager != null)
            {
                gameSceneManager.RegisterMouse(this);
            }
            else
            {
                Debug.LogWarning("GameSceneManager 未找到，Mouse 无法注册。");
            }
        }

        /// <summary>
        /// 从GameSceneManager获取CharacterController
        /// </summary>
        private void GetCharacterControllerFromGameSceneManager()
        {
            if (characterController == null)
            {
                ProjectII.Manager.GameSceneManager gameSceneManager = ProjectII.Manager.GameSceneManager.Instance;
                if (gameSceneManager != null && gameSceneManager.CurrentPlayerCharacter != null)
                {
                    characterController = gameSceneManager.CurrentPlayerCharacter;
                }
                else
                {
                    Debug.LogWarning("无法从GameSceneManager获取CharacterController，将尝试通过FindObjectOfType查找。");
                    characterController = FindObjectOfType<CharacterController>();
                }
            }
        }

        private void OnDestroy()
        {
            // 从GameSceneManager注销
            ProjectII.Manager.GameSceneManager gameSceneManager = ProjectII.Manager.GameSceneManager.Instance;
            if (gameSceneManager != null)
            {
                gameSceneManager.UnregisterMouse(this);
            }

            // 恢复鼠标状态
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
