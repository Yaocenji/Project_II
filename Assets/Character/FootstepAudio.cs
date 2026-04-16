using UnityEngine;

namespace ProjectII.Character
{
    /// <summary>
    /// 脚步声音频控制器
    /// 负责根据角色移动状态触发脚步声播放，独立于 CharacterController
    /// 挂载在玩家角色 GameObject（或其子物体，与 StudioEventEmitter 同级）
    /// </summary>
    public class FootstepAudio : MonoBehaviour
    {
        [Header("FMOD")]
        [Tooltip("脚步声 FMOD 发射器引用")]
        public FMODUnity.StudioEventEmitter footstepEmitter;

        [Header("Settings")]
        [Tooltip("触发一次脚步声所需的移动距离阈值")]
        public float footstepDistanceInterval = 0.75f;

        /// <summary>
        /// 玩家刚体引用，用于读取当前移动速度
        /// </summary>
        private Rigidbody2D rb;

        /// <summary>
        /// 玩家角色控制器引用，用于读取当前速度状态
        /// </summary>
        private CharacterController characterController;

        /// <summary>
        /// 当前已累加的移动距离
        /// </summary>
        private float footstepDistance = 0f;

        private void Awake()
        {
            // 获取 Rigidbody2D（可能在父物体上）
            rb = GetComponentInParent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogWarning("FootstepAudio: 未找到 Rigidbody2D！");
            }
        }

        private void Start()
        {
            // 在 Start 中获取 CharacterController，确保 GameSceneManager 已完成注册
            var gameSceneManager = Manager.GameSceneManager.Instance;
            if (gameSceneManager != null)
            {
                characterController = gameSceneManager.CurrentPlayerCharacter;
            }

            if (characterController == null)
            {
                Debug.LogWarning("FootstepAudio: 未找到 CharacterController！");
            }
        }

        private void FixedUpdate()
        {
            if (rb == null || characterController == null || footstepEmitter == null) return;

            // 静止时不累加距离
            if (characterController.CurrentSpeedState == CharacterController.SpeedState.Idle)
            {
                return;
            }

            // 累加移动距离
            footstepDistance += Time.fixedDeltaTime * rb.velocity.magnitude;

            // 达到阈值时触发播放
            if (footstepDistance >= footstepDistanceInterval)
            {
                footstepDistance = 0f;
                footstepEmitter.Play();
            }
        }

        /// <summary>
        /// 设置 FMOD 参数，供 FMOD_FootStepPlayer_Parameter_Trigger 调用
        /// </summary>
        /// <param name="name">参数名</param>
        /// <param name="value">参数值</param>
        public void SetParameter(string name, float value)
        {
            if (footstepEmitter != null)
            {
                footstepEmitter.SetParameter(name, value);
            }
        }
    }
}
