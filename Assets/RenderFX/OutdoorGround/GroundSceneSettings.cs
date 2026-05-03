using System;
using System.Collections.Generic;
using UnityEngine;
using RadianceCascadesWorldBVH;

namespace ProjectII.Render
{
    /// <summary>
    /// Splatmap 层定义：层名 + 地砖列表。最多4层，对应 Splatmap 的 RGBA 四通道。
    /// </summary>
    [Serializable]
    public class SplatLayer
    {
        [Tooltip("层名（如\"柏油\"、\"草地\"、\"泥地\"、\"碎石\"）")]
        public string layerName = "Layer";

        [Tooltip("该层的地砖 Sprite 列表（同层内必须共享同一 Texture，且 PPU 一致）")]
        public List<Sprite> tileSprites = new List<Sprite>();

        [Tooltip("使用 Hex-Tile 采样（六边形网格 + 三邻域混合 + 随机旋转），消除平铺重复感")]
        public bool useHexTile = false;

        [Tooltip("整体旋转角度（度），旋转该层的地砖排列方向")]
        [Range(0f, 360f)]
        public float tileRotation = 0f;

        [Tooltip("单块地砖的世界尺寸（米）")]
        public Vector2 tileWorldSize = new Vector2(1f, 1f);

        public void OnValidate()
        {
            if (tileWorldSize.x < 0.01f) tileWorldSize.x = 0.01f;
            if (tileWorldSize.y < 0.01f) tileWorldSize.y = 0.01f;
        }
    }

    /// <summary>
    /// 场景级地面烘焙设置（单例）。
    /// 持有 Splatmap（RGBA 四通道权重图）、4个 SplatLayer 定义。
    /// 场景 AABB 直接从 RCWB 系统读取（RCWBSceneSettings → PolygonManagerSettings），不单独维护。
    /// 整个场景只烘焙出一张覆盖 AABB 的 Albedo 纹理。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ProjectII/Ground Scene Settings")]
    [ExecuteInEditMode]
    public class GroundSceneSettings : MonoBehaviour
    {
        [Header("Splatmap 层（最多4层，对应 RGBA）")]
        [Tooltip("Splatmap 层定义。最多4层，分别对应 Splatmap 的 R/G/B/A 通道")]
        public List<SplatLayer> splatLayers = new List<SplatLayer>();

        [Header("Splatmap 权重图")]
        [Tooltip("场景级 Splatmap（ARGB32），R/G/B/A 分别为4层权重。由笔刷绘制")]
        public Texture2D splatmap;

        [Header("烘焙设置")]
        [Tooltip("输出纹理的像素密度（像素/世界单位）")]
        public float pixelsPerUnit = 32f;

        [Tooltip("烘焙后 SpriteRenderer 使用此材质（RCWB 材质）")]
        public Material rcwbMaterial;

        [Tooltip("Splatmap 预览使用的材质（为空则使用默认材质）")]
        public Material previewMaterial;

        [Tooltip("烘焙后 SpriteRenderer 的 sortingOrder")]
        public int sortingOrder = 0;

        [Tooltip("确定性随机种子（同种子→同一套地砖分布）")]
        public int randomSeed = 0;

        // ── 预览子物体（由 Editor 管理） ──
        public const string k_PreviewChildName = "_SplatmapPreview";
        [NonSerialized] public GameObject previewGO;
        [NonSerialized] public SpriteRenderer previewSR;
        [NonSerialized] public bool previewVisible;

        private static GroundSceneSettings s_Instance;

        /// <summary>场景单例</summary>
        public static GroundSceneSettings Instance
        {
            get
            {
                if (s_Instance == null)
                    s_Instance = FindObjectOfType<GroundSceneSettings>();
                return s_Instance;
            }
        }

        /// <summary>
        /// 从 RCWB 系统读取场景 AABB。
        /// 优先级：RCWBSceneSettings（场景级）→ PolygonManagerSettings（全局）→ 默认值。
        /// </summary>
        public static Vector4 GetSceneAABB()
        {
            var sceneSettings = FindObjectOfType<RCWBSceneSettings>();
            if (sceneSettings != null)
                return sceneSettings.sceneAABB;

            var globalSettings = PolygonManagerSettings.Instance;
            if (globalSettings != null)
                return globalSettings.sceneAABB;

            return new Vector4(-100f, -100f, 100f, 100f);
        }

        private void Awake()
        {
            if (s_Instance == null)
                s_Instance = this;
            else if (s_Instance != this)
            {
                Debug.LogWarning("[GroundSceneSettings] 场景中存在多个实例。");
            }
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }

        private void OnValidate()
        {
            if (pixelsPerUnit < 1f) pixelsPerUnit = 1f;
            if (splatLayers.Count > 4)
            {
                Debug.LogWarning("[GroundSceneSettings] SplatLayer 最多4层（对应 RGBA），多余层将被忽略。");
            }
            foreach (var layer in splatLayers)
                layer.OnValidate();
        }

        /// <summary>设置预览子物体可见性。</summary>
        public void SetPreviewVisible(bool visible)
        {
            previewVisible = visible;
            if (previewGO != null)
                previewGO.SetActive(visible);
        }
    }
}
