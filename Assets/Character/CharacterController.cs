using UnityEngine;
using Project_II.InputSystem;

namespace ProjectII.Character
{
    /// <summary>
    /// 玩家控制器，控制玩家角色移动、跳跃、攻击等行为
    /// </summary>
    public class CharacterController : MonoBehaviour
    {
        /// <summary>
        /// 当前这一帧的运动速度状态
        /// </summary>
        public enum SpeedState
        {
            Idle,       // 静止
            Sneak,      // 静步慢走
            Walk,       // 行走
            Run         // 奔跑
        }

        [Header("Input System")]
        [SerializeField] private InputAction_0 inputActions; // 从InputManager获取，不应在Inspector中手动赋值

        [Header("Rigidbody")]
        [SerializeField] private Rigidbody2D rb;

        [Header("Speed Settings")]
        [SerializeField] private float sneakSpeed = 2f;
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float runSpeed = 8f;

        [Header("Smoothing Settings")]
        [SerializeField] private float rotationSmoothTime = 0.1f;
        [SerializeField] private float velocitySmoothTime = 0.1f;

        /// <summary>
        /// 当前这一帧的运动方向
        /// 应该保证被归一化，不过这个应该是inputAction取值的一个缓存而已
        /// </summary>
        public Vector2 Direction { get; private set; }

        /// <summary>
        /// 目标朝向方向（由Mouse脚本设置）
        /// 用于平滑旋转角色朝向
        /// </summary>
        public Vector2 TargetLookDirection { get; private set; }

        /// <summary>
        /// 当前这一帧的运动速度状态
        /// </summary>
        public SpeedState CurrentSpeedState { get; private set; }

        // 输入状态缓存
        private Vector2 moveInput;
        private bool isRunPressed;
        private bool isSneakActive; // sneak是Press交互，所以是切换状态

        // 平滑插值相关
        private float currentRotationVelocity;
        private Vector2 currentVelocity;

        private void Awake()
        {
            RegisterToGameSceneManager();
            
            // 如果没有指定Rigidbody2D，尝试获取
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            // 从InputManager获取InputAction引用
            GetInputActionFromInputManager();

            // 确保刚体是 Dynamic 类型，并设置基本属性
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f; // 2D 俯视角游戏不需要重力
                rb.angularDrag = 0f; // 移除角阻力，让角速度能被正确读取
                // 不锁定旋转，让 angularVelocity 能正确记录
            }

            // 注册sneak的切换回调
            if (inputActions != null)
            {
                inputActions.Character.sneak.performed += OnSneakToggle;
            }
        }

        private void OnSneakToggle(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            isSneakActive = !isSneakActive;
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
            // 从InputAction获取输入
            if (inputActions != null)
            {
                moveInput = inputActions.Character.move.ReadValue<Vector2>();
                isRunPressed = inputActions.Character.run.IsPressed();
                // sneak是Press交互，通过回调处理，不需要在这里读取
            }

            // 更新方向和速度状态
            UpdateDirection();
            UpdateSpeedState();

            // 虽然这样代码很不优雅，但是这是最简单的方法，因为之后会用SDF来计算玩家视野，所以需要一个玩家位置的变量
            Shader.SetGlobalVector("_Player_PosWS_Direction_Angle", new Vector4(transform.position.x, transform.position.y, transform.rotation.eulerAngles.z, 45f));
            Shader.SetGlobalVector("_Player_Radius_Eye_Inner_Outter_Blank", new Vector4(0.45f, .25f, 3.0f, 0));
        }

        private void FixedUpdate()
        {
            // 在FixedUpdate中调用Move方法
            if (Direction.magnitude > 0.01f)
            {
                Move(Direction);
            }
            else
            {
                // 如果没有输入，平滑停止
                Move(Vector2.zero);
            }

            // 在FixedUpdate中调用Rotate方法，平滑旋转角色朝向
            Rotate();
        }

        /// <summary>
        /// 根据InputAction的输入，更新运动方向，应该是可以直接从inputAction里面复制，不用太多操作
        /// </summary>
        public void UpdateDirection()
        {
            Direction = moveInput;

            // 归一化方向向量
            if (Direction.magnitude > 1f)
            {
                Direction.Normalize();
            }
        }

        /// <summary>
        /// 根据InputAction的输入，更新运动状态
        /// </summary>
        public void UpdateSpeedState()
        {
            // 如果没有移动输入，则为静止
            if (Direction.magnitude < 0.01f)
            {
                CurrentSpeedState = SpeedState.Idle;
                return;
            }

            // 根据按键状态决定速度状态
            // 优先级：静步 > 奔跑 > 行走
            if (isSneakActive)
            {
                CurrentSpeedState = SpeedState.Sneak;
            }
            else if (isRunPressed)
            {
                CurrentSpeedState = SpeedState.Run;
            }
            else
            {
                CurrentSpeedState = SpeedState.Walk;
            }
        }

        /// <summary>
        /// 这个方法在FixedUpdate中调用，每帧会有别的函数从InputAction里面获取输入，计算出当前的输入方向，归一化后传给这个方法。
        /// </summary>
        /// <param name="direction">归一化的方向向量</param>
        public void Move(Vector2 direction)
        {
            if (rb == null)
            {
                Debug.LogWarning("Rigidbody2D is not assigned!");
                return;
            }

            // 1、平滑地将角色方向改为当前方向
            // 只有奔跑的时候，强制角色转向朝移动方向
            // 慢走和行走的时候不管（之后会用准星位置来控制指向）
            if (direction.magnitude > 0.01f && CurrentSpeedState == SpeedState.Run)
            {
                float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                ApplyRotation(targetAngle);
            }
            // 慢走和行走状态：留空，之后会用准星位置来控制指向

            // 2、平滑地将角色速度改为当前速度状态，使用 Rigidbody2D.velocity
            float targetSpeed = GetSpeedForState(CurrentSpeedState);
            Vector2 targetVelocity = direction * targetSpeed;

            // 使用SmoothDamp平滑速度变化，直接设置刚体速度
            Vector2 smoothedVelocity = Vector2.SmoothDamp(rb.velocity, targetVelocity, 
                ref currentVelocity, velocitySmoothTime);
            
            // 直接设置刚体速度，让物理引擎处理位移
            rb.velocity = smoothedVelocity;
        }

        /// <summary>
        /// 设置目标朝向方向（由Mouse脚本调用）
        /// </summary>
        /// <param name="direction">归一化的目标朝向方向</param>
        public void SetTargetLookDirection(Vector2 direction)
        {
            // 归一化方向向量
            if (direction.magnitude > 0.01f)
            {
                TargetLookDirection = direction.normalized;
            }
        }

        /// <summary>
        /// 这个方法应该也在FixedUpdate中调用，因为它是修改朝向的
        /// 平滑地将角色朝向改为目标朝向（由Mouse脚本设置）
        /// 注意：奔跑状态下，朝向由Move方法控制；慢走和行走状态下，朝向由Mouse控制
        /// </summary>
        public void Rotate()
        {
            // 奔跑状态下，朝向由Move方法控制，这里不处理
            if (CurrentSpeedState == SpeedState.Run)
            {
                return;
            }

            // 慢走和行走状态下，使用Mouse设置的目标朝向
            if (TargetLookDirection.magnitude < 0.01f)
            {
                return;
            }

            // 计算目标角度
            float targetAngle = Mathf.Atan2(TargetLookDirection.y, TargetLookDirection.x) * Mathf.Rad2Deg;
            ApplyRotation(targetAngle);
        }

        /// <summary>
        /// 应用旋转，使用刚体的 MoveRotation 并设置角速度
        /// </summary>
        /// <param name="targetAngle">目标角度（度）</param>
        private void ApplyRotation(float targetAngle)
        {
            if (rb == null) return;

            float currentAngle = rb.rotation;

            // 处理角度环绕
            float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);
            float smoothedAngle = Mathf.SmoothDampAngle(currentAngle, currentAngle + angleDifference,
                ref currentRotationVelocity, rotationSmoothTime);

            // 使用刚体的 MoveRotation 来旋转，这样刚体会记录正确的角速度
            rb.MoveRotation(smoothedAngle);
            
            // 手动设置角速度，以便其他脚本可以读取
            rb.angularVelocity = currentRotationVelocity;
        }

        /// <summary>
        /// 根据速度状态获取对应的速度值
        /// </summary>
        private float GetSpeedForState(SpeedState state)
        {
            switch (state)
            {
                case SpeedState.Idle:
                    return 0f;
                case SpeedState.Sneak:
                    return sneakSpeed;
                case SpeedState.Walk:
                    return walkSpeed;
                case SpeedState.Run:
                    return runSpeed;
                default:
                    return 0f;
            }
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
                gameSceneManager.RegisterPlayerCharacter(this);
            }
            else
            {
                Debug.LogWarning("GameSceneManager 未找到，CharacterController 无法注册。");
            }
        }

        private void OnDestroy()
        {
            // 从GameSceneManager注销
            ProjectII.Manager.GameSceneManager gameSceneManager = ProjectII.Manager.GameSceneManager.Instance;
            if (gameSceneManager != null)
            {
                gameSceneManager.UnregisterPlayerCharacter(this);
            }

            // 取消注册sneak回调
            // 注意：不再需要Dispose，因为InputAction由InputManager管理
            if (inputActions != null)
            {
                inputActions.Character.sneak.performed -= OnSneakToggle;
            }
        }
    }
}
