using System.Collections.Generic;
using ProjectII.Character;
using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// 前景管理器（场景单例）
    /// 管理场景中所有 ForegroundObject，统一驱动前景效果的每帧更新。
    /// 持有全局参数（摄像机、像素密度、效果幅度），并将其传递给各 ForegroundObject。
    /// </summary>
    [DefaultExecutionOrder(-98)]
    public class ForegroundManager : MonoBehaviour
    {
        [Header("场景引用")]
        [SerializeField] private Camera mainCamera;
        
        [Header("世界空间的鼠标替身引用")]
        [SerializeField] private MouseWorldFollow mouseWorldFollow;

        [Header("像素设置")]
        [SerializeField] private float pixelsPerUnit = 32f;

        [Header("效果参数（线性映射，高度从 0 到 maxHeight）")]
        [SerializeField] private float maxHeight = 3f;
        [SerializeField] private float maxScaleAddition = 0.5f;
        [SerializeField] private float maxBlurRadius = 15f;
        [SerializeField] private float maxPositionOffset = 1f;

        // 已注册的前景物体列表（List 用于稳定顺序遍历，HashSet 用于 O(1) 查重）
        private readonly List<ForegroundObject> foregroundObjects = new List<ForegroundObject>();
        private readonly HashSet<ForegroundObject> foregroundObjectSet = new HashSet<ForegroundObject>();

        private static ForegroundManager instance;

        /// <summary>ForegroundManager 场景单例实例</summary>
        public static ForegroundManager Instance
        {
            get
            {
                if (instance == null)
                    instance = FindObjectOfType<ForegroundManager>();
                return instance;
            }
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Debug.LogWarning("[ForegroundManager] 场景中存在多个实例，销毁重复的实例。");
                Destroy(gameObject);
                return;
            }

            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            ScanAndRegisterExistingObjects();
        }

        private void Update()
        {
            if (mainCamera == null) return;

            Vector2 camPos = mainCamera.transform.position;
            float fullResScale = ComputeFullResScale();

            if (mouseWorldFollow != null)
            {
                Shader.SetGlobalVector("_FG_MousePosition", mouseWorldFollow.transform.position);
            }

            foreach (ForegroundObject obj in foregroundObjects)
            {
                if (obj == null) continue;

                float h = obj.VirtualHeight;
                float scale = ComputeScale(h);
                Vector2 offset = ComputePositionOffset(h, obj.BaseWorldPosition, camPos);
                float blurRadius = ComputeBlurRadius(h);

                obj.UpdateScaleAndOffset(scale, offset, maxHeight);
                obj.UpdateBlur(blurRadius, fullResScale);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            // 场景卸载时强制清空共享纹理缓存，防止跨场景泄漏
            ForegroundTextureCache.Clear();
        }

        // ────────── 注册/反注册 ──────────

        /// <summary>
        /// 注册一个 ForegroundObject，由 ForegroundObject.OnEnable 调用。
        /// </summary>
        /// <param name="obj">要注册的 ForegroundObject</param>
        public void Register(ForegroundObject obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("[ForegroundManager] Register: 传入的 ForegroundObject 为空，跳过。");
                return;
            }
            if (foregroundObjectSet.Add(obj))
                foregroundObjects.Add(obj);
        }

        /// <summary>
        /// 反注册一个 ForegroundObject，由 ForegroundObject.OnDisable 调用。
        /// </summary>
        /// <param name="obj">要反注册的 ForegroundObject</param>
        public void Unregister(ForegroundObject obj)
        {
            if (obj == null) return;
            if (foregroundObjectSet.Remove(obj))
                foregroundObjects.Remove(obj);
        }

        /// <summary>
        /// 扫描场景中已存在的 ForegroundObject 并注册，处理启动顺序导致的未注册情况。
        /// </summary>
        private void ScanAndRegisterExistingObjects()
        {
            ForegroundObject[] existing = FindObjectsByType<ForegroundObject>(FindObjectsSortMode.None);
            foreach (ForegroundObject obj in existing)
            {
                if (obj.isActiveAndEnabled)
                    Register(obj);
            }
        }

        // ────────── 线性映射函数（每个效果独立封装，方便后续调节） ──────────

        /// <summary>
        /// 根据虚拟高度计算 scale 乘数（线性映射）。
        /// 高度为 0 时返回 1，高度为 maxHeight 时返回 1 + maxScaleAddition。
        /// </summary>
        /// <param name="height">虚拟高度</param>
        /// <returns>scale 乘数</returns>
        private float ComputeScale(float height)
        {
            float t = maxHeight > 0f ? Mathf.Clamp01(height / maxHeight) : 0f;
            return 1f + t * maxScaleAddition;
        }

        /// <summary>
        /// 根据虚拟高度计算模糊半径（全分辨率像素，线性映射）。
        /// 高度为 0 时返回 0，高度为 maxHeight 时返回 maxBlurRadius。
        /// </summary>
        /// <param name="height">虚拟高度</param>
        /// <returns>模糊半径（全分辨率像素）</returns>
        private float ComputeBlurRadius(float height)
        {
            float t = maxHeight > 0f ? Mathf.Clamp01(height / maxHeight) : 0f;
            return t * maxBlurRadius;
        }

        /// <summary>
        /// 根据虚拟高度计算世界空间 XY 位置偏移（线性映射）。
        /// 偏移方向为从摄像机位置向物体方向发散，高度越高偏移越大。
        /// </summary>
        /// <param name="height">虚拟高度</param>
        /// <param name="objBasePos">物体的基础世界位置（不含偏移效果）</param>
        /// <param name="camWorldPos">摄像机世界位置</param>
        /// <returns>世界空间 XY 偏移向量</returns>
        private Vector2 ComputePositionOffset(float height, Vector2 objBasePos, Vector2 camWorldPos)
        {
            float t = maxHeight > 0f ? Mathf.Clamp01(height / maxHeight) : 0f;
            if (t < 1e-6f) return Vector2.zero;

            Vector2 direction = objBasePos - camWorldPos;
            if (direction.sqrMagnitude < 1e-6f) return Vector2.zero;

            // 不做归一化：偏移量同时正比于高度和物体到摄像机的距离，模拟透视视差
            return direction * (t * maxPositionOffset);
        }

        /// <summary>
        /// 计算全分辨率缩放倍数：屏幕像素高度 / 像素风空间对应的像素高度。
        /// </summary>
        /// <returns>全分辨率缩放倍数</returns>
        private float ComputeFullResScale()
        {
            if (mainCamera == null || mainCamera.orthographicSize <= 0f || pixelsPerUnit <= 0f)
                return 1f;
            float pixelSpaceHeight = mainCamera.orthographicSize * 2f * pixelsPerUnit;
            return Screen.height / pixelSpaceHeight;
        }
    }
}
