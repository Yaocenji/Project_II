using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// 前景物品管理器（挂载在单个前景物体上）
    /// 为俯视角游戏中的前景物体提供 scale 放大、高斯模糊和 XY 位置偏移效果，
    /// 模拟前景物体距摄像机更近的视觉感受。
    /// 效果参数由 ForegroundManager 统一驱动，本脚本负责封装具体的应用逻辑。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ForegroundObject : MonoBehaviour
    {
        [Header("前景设置")]
        [SerializeField] private float virtualHeight = 0f;

        [Header("渲染属性")]
        [ColorUsage(false, true)]
        [SerializeField] private Color emission = Color.black;
        [Range(0f, 10f)]
        [SerializeField] private float giCoefficient = 1f;

        // 组件引用
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock mpb;

        // Shader 属性 ID 缓存
        // xy = 世界空间位置偏移，z = scale 乘数
        // shader 反推公式：P_pre = (P_post - objectWorldPos) / scale + (objectWorldPos - offset.xy)
        private static readonly int ForegroundTransformDataID =
            Shader.PropertyToID("_ForegroundTransformData");
        private static readonly int EmissionID       = Shader.PropertyToID("_Emission");
        private static readonly int RotationSinCosID = Shader.PropertyToID("_RotationSinCos");
        private static readonly int GICoefficientID  = Shader.PropertyToID("_GICoefficient");
        private static readonly int BumpMapID        = Shader.PropertyToID("_BumpMap");
        private static readonly int VirtualHeightID  = Shader.PropertyToID("_VirtualHeight");

        // 原始状态缓存（OnEnable 时记录）
        private Sprite    originalSprite;
        private Texture2D originalBumpMap;   // 从 sprite 的 Secondary Textures 中读取
        private Vector3   baseLocalScale;

        // 运行时生成的模糊资产（颜色纹理 + 法线纹理）
        private Texture2D blurTexture;
        private Texture2D blurNormalTexture;
        private Sprite    blurSprite;

        // 脏标记与上次参数缓存，用于跳过不必要的重新生成
        private bool  blurDirty      = true;
        private float lastBlurRadius  = -1f;
        private float lastFullResScale = -1f;

        // 上一帧应用的位置偏移（增量更新用，避免偏移累积）
        private Vector2 lastPositionOffset = Vector2.zero;

        /// <summary>
        /// 虚拟高度（距地面的距离）。
        /// 值越大表示物体离地越高，三种前景效果越强。
        /// </summary>
        public float VirtualHeight
        {
            get => virtualHeight;
            set
            {
                if (Mathf.Approximately(virtualHeight, value)) return;
                virtualHeight = value;
                blurDirty = true;
            }
        }

        /// <summary>
        /// 不含前景偏移效果的世界空间 XY 位置（用于方向计算，避免偏移反馈）
        /// </summary>
        public Vector2 BaseWorldPosition => (Vector2)transform.position - lastPositionOffset;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            baseLocalScale = transform.localScale;
            mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            originalSprite  = spriteRenderer.sprite;
            originalBumpMap = GetSecondaryBumpMap(originalSprite);
            blurDirty       = true;

            if (ForegroundManager.Instance != null)
                ForegroundManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            // 恢复原始精灵
            if (spriteRenderer != null && originalSprite != null)
                spriteRenderer.sprite = originalSprite;

            // 恢复原始 scale
            transform.localScale = baseLocalScale;

            // 撤销上次位置偏移，恢复逻辑位置
            Vector3 pos = transform.position;
            pos.x -= lastPositionOffset.x;
            pos.y -= lastPositionOffset.y;
            transform.position = pos;
            lastPositionOffset = Vector2.zero;

            // 清除 MPB，恢复 identity 变换数据
            if (spriteRenderer != null)
            {
                spriteRenderer.GetPropertyBlock(mpb);
                mpb.SetVector(ForegroundTransformDataID, new Vector4(0f, 0f, 1f, 0f));
                if (originalBumpMap != null)
                    mpb.SetTexture(BumpMapID, originalBumpMap);
                spriteRenderer.SetPropertyBlock(mpb);
            }

            if (ForegroundManager.Instance != null)
                ForegroundManager.Instance.Unregister(this);
        }

        private void OnDestroy()
        {
            ReleaseBlurAssets();
        }

        /// <summary>
        /// 由 ForegroundManager 每帧调用：应用 scale 放大和 XY 位置偏移，并同步 z 轴。
        /// </summary>
        /// <param name="scaleMultiplier">scale 乘数（相对于原始 localScale）</param>
        /// <param name="positionOffset">世界空间 XY 偏移量</param>
        public void UpdateScaleAndOffset(float scaleMultiplier, Vector2 positionOffset)
        {
            // 应用 scale（基于 OnEnable 时记录的原始 scale）
            transform.localScale = baseLocalScale * scaleMultiplier;

            // 增量方式更新 XY 偏移：撤销上帧偏移，叠加新偏移
            Vector3 pos = transform.position;
            pos.x += positionOffset.x - lastPositionOffset.x;
            pos.y += positionOffset.y - lastPositionOffset.y;

            // 将 z 轴同步为虚拟高度，实现遮挡关系自动正确
            pos.z = virtualHeight;

            transform.position = pos;
            lastPositionOffset = positionOffset;

            UpdateMaterialPropertyBlock(positionOffset, scaleMultiplier);
        }

        /// <summary>
        /// 更新 MaterialPropertyBlock，将变换映射数据和渲染属性传入 Shader。
        /// _ForegroundTransformData: (offsetX, offsetY, scaleMultiplier, 0)
        /// Shader 反推偏移前世界坐标：P_pre = (P_post - objectWorldPos) / scale + (objectWorldPos - offset.xy)
        /// </summary>
        private void UpdateMaterialPropertyBlock(Vector2 positionOffset, float scaleMultiplier)
        {
            spriteRenderer.GetPropertyBlock(mpb);

            mpb.SetVector(ForegroundTransformDataID,
                new Vector4(positionOffset.x, positionOffset.y, scaleMultiplier, 0f));

            mpb.SetColor(EmissionID, emission);

            float rotZ = -transform.eulerAngles.z * Mathf.Deg2Rad;
            mpb.SetVector(RotationSinCosID, new Vector4(Mathf.Cos(rotZ), Mathf.Sin(rotZ), 0f, 0f));

            mpb.SetFloat(GICoefficientID, giCoefficient);

            mpb.SetFloat(VirtualHeightID, virtualHeight);

            // 优先使用模糊法线纹理；若无模糊则回退到原始法线纹理（维持 MPB 对 _BumpMap 的统一管理）
            Texture2D bumpToUse = blurNormalTexture != null ? blurNormalTexture : originalBumpMap;
            if (bumpToUse != null)
                mpb.SetTexture(BumpMapID, bumpToUse);

            spriteRenderer.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// 由 ForegroundManager 按需调用：生成或更新模糊纹理（含脏检查）。
        /// </summary>
        /// <param name="blurRadius">模糊半径（全分辨率像素单位）</param>
        /// <param name="fullResScale">全分辨率缩放倍数（屏幕像素/像素风像素）</param>
        public void UpdateBlur(float blurRadius, float fullResScale)
        {
            bool radiusChanged = Mathf.Abs(blurRadius - lastBlurRadius) >= 0.5f;
            bool scaleChanged  = !Mathf.Approximately(lastFullResScale, fullResScale);

            if (!blurDirty && !radiusChanged && !scaleChanged) return;

            GenerateBlurTextures(blurRadius, fullResScale);

            blurDirty      = false;
            lastBlurRadius  = blurRadius;
            lastFullResScale = fullResScale;
        }

        // ────────── 模糊纹理生成 ──────────

        private void GenerateBlurTextures(float blurRadius, float fullResScale)
        {
            if (originalSprite == null) return;

            Texture2D sourceTex = originalSprite.texture;
            if (sourceTex == null)
            {
                Debug.LogWarning($"[ForegroundObject] {name}: 精灵纹理为空，跳过模糊生成。");
                return;
            }
            if (!sourceTex.isReadable)
            {
                Debug.LogWarning($"[ForegroundObject] {name}: 精灵纹理不可读，无法生成模糊纹理。" +
                                 "请在纹理导入设置中启用 Read/Write Enabled。");
                return;
            }

            // 精灵在图集中的像素区域
            Rect texRect = originalSprite.textureRect;
            int srcW = Mathf.RoundToInt(texRect.width);
            int srcH = Mathf.RoundToInt(texRect.height);
            if (srcW <= 0 || srcH <= 0) return;

            // 全分辨率尺寸
            int fullW = Mathf.Max(1, Mathf.RoundToInt(srcW * fullResScale));
            int fullH = Mathf.Max(1, Mathf.RoundToInt(srcH * fullResScale));

            // 模糊半径（整数，全分辨率像素）
            int r = Mathf.Max(0, Mathf.RoundToInt(blurRadius));

            // 模糊半径为 0 时恢复原始资产
            if (r == 0)
            {
                ReleaseBlurAssets();
                spriteRenderer.sprite = originalSprite;
                return;
            }

            // 预计算共用的高斯 kernel
            float sigma = Mathf.Max(0.1f, r / 3f);
            float[] kernel = BuildGaussianKernel(r, sigma);

            int finalW = fullW + 2 * r;
            int finalH = fullH + 2 * r;

            ReleaseBlurAssets();

            // ── 颜色纹理 ──
            blurTexture = CreateBlurredTexture(sourceTex, texRect, fullW, fullH, r, kernel,
                                               TextureFormat.RGBA32);

            // ── 法线纹理（若存在且可读）──
            if (originalBumpMap != null)
            {
                if (!originalBumpMap.isReadable)
                {
                    Debug.LogWarning($"[ForegroundObject] {name}: 法线纹理不可读，跳过法线模糊。" +
                                     "请在纹理导入设置中启用 Read/Write Enabled。");
                }
                else
                {
                    // 使用与颜色图相同的 texRect：
                    // Sprite Atlas 会将 secondary textures 打包进独立的 secondary atlas，
                    // 但保持与主 atlas 完全相同的 layout，因此法线图的子区域坐标与颜色图一致。
                    // 对于未打包进 atlas 的独立纹理，texRect = (0, 0, w, h)，同样正确。
                    blurNormalTexture = CreateBlurredTexture(originalBumpMap, texRect,
                                                             fullW, fullH, r, kernel,
                                                             TextureFormat.RGBA32);
                }
            }

            // ── 创建 blurSprite（颜色纹理对应的新精灵）──
            Vector2 origPivot = originalSprite.pivot;
            float pivotX = (origPivot.x * fullResScale + r) / finalW;
            float pivotY = (origPivot.y * fullResScale + r) / finalH;
            float newPpu = originalSprite.pixelsPerUnit * fullResScale;

            blurSprite = Sprite.Create(
                blurTexture,
                new Rect(0, 0, finalW, finalH),
                new Vector2(pivotX, pivotY),
                newPpu
            );

            spriteRenderer.sprite = blurSprite;
        }

        /// <summary>
        /// 从源纹理的指定区域生成一张模糊后的 Texture2D。
        /// 输出尺寸为 (fullW + 2r) x (fullH + 2r)，四周为模糊扩展的透明区域。
        /// </summary>
        private static Texture2D CreateBlurredTexture(
            Texture2D sourceTex, Rect srcRect,
            int fullW, int fullH, int r, float[] kernel,
            TextureFormat format)
        {
            int srcX = Mathf.RoundToInt(srcRect.x);
            int srcY = Mathf.RoundToInt(srcRect.y);
            int srcW = Mathf.RoundToInt(srcRect.width);
            int srcH = Mathf.RoundToInt(srcRect.height);

            int finalW = fullW + 2 * r;
            int finalH = fullH + 2 * r;

            // 读取、缩放、pad
            Color[] srcPixels    = sourceTex.GetPixels(srcX, srcY, srcW, srcH);
            Color[] scaledPixels = ScaleBilinear(srcPixels, srcW, srcH, fullW, fullH);

            Color[] buffer = new Color[finalW * finalH]; // 默认 Color.clear
            for (int y = 0; y < fullH; y++)
            for (int x = 0; x < fullW; x++)
                buffer[(y + r) * finalW + (x + r)] = scaledPixels[y * fullW + x];

            // 可分离高斯模糊
            Color[] temp = GaussianBlurHorizontal(buffer, finalW, finalH, kernel, r);
            buffer = GaussianBlurVertical(temp, finalW, finalH, kernel, r);

            Texture2D tex = new Texture2D(finalW, finalH, format, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.SetPixels(buffer);
            tex.Apply();
            return tex;
        }

        private void ReleaseBlurAssets()
        {
            if (blurSprite != null)        { Destroy(blurSprite);        blurSprite        = null; }
            if (blurTexture != null)       { Destroy(blurTexture);       blurTexture       = null; }
            if (blurNormalTexture != null) { Destroy(blurNormalTexture); blurNormalTexture = null; }
        }

        // ────────── 工具函数 ──────────

        /// <summary>
        /// 从 Sprite 的 Secondary Textures 中查找名为 "_BumpMap" 的法线纹理。
        /// </summary>
        private static Texture2D GetSecondaryBumpMap(Sprite sprite)
        {
            if (sprite == null) return null;
            SecondarySpriteTexture[] secondaries = new SecondarySpriteTexture[2];
            sprite.GetSecondaryTextures(secondaries);
            foreach (SecondarySpriteTexture st in secondaries)
                if (st.name == "_BumpMap")
                    return st.texture as Texture2D;
            return null;
        }

        private static Color[] ScaleBilinear(Color[] src, int srcW, int srcH, int dstW, int dstH)
        {
            Color[] dst = new Color[dstW * dstH];
            for (int y = 0; y < dstH; y++)
            for (int x = 0; x < dstW; x++)
            {
                float u = (x + 0.5f) / dstW * srcW - 0.5f;
                float v = (y + 0.5f) / dstH * srcH - 0.5f;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, srcW - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, srcH - 1);
                int x1 = Mathf.Clamp(x0 + 1, 0, srcW - 1);
                int y1 = Mathf.Clamp(y0 + 1, 0, srcH - 1);
                float fx = u - x0;
                float fy = v - y0;
                dst[y * dstW + x] = Color.Lerp(
                    Color.Lerp(src[y0 * srcW + x0], src[y0 * srcW + x1], fx),
                    Color.Lerp(src[y1 * srcW + x0], src[y1 * srcW + x1], fx),
                    fy);
            }
            return dst;
        }

        private static float[] BuildGaussianKernel(int r, float sigma)
        {
            int size = 2 * r + 1;
            float[] kernel = new float[size];
            float sum = 0f;
            for (int i = 0; i < size; i++)
            {
                int offset = i - r;
                kernel[i] = Mathf.Exp(-offset * offset / (2f * sigma * sigma));
                sum += kernel[i];
            }
            for (int i = 0; i < size; i++) kernel[i] /= sum;
            return kernel;
        }

        private static Color[] GaussianBlurHorizontal(Color[] pixels, int w, int h, float[] kernel, int r)
        {
            Color[] result = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear;
                for (int k = -r; k <= r; k++)
                {
                    int sx = Mathf.Clamp(x + k, 0, w - 1);
                    sum += pixels[y * w + sx] * kernel[k + r];
                }
                result[y * w + x] = sum;
            }
            return result;
        }

        private static Color[] GaussianBlurVertical(Color[] pixels, int w, int h, float[] kernel, int r)
        {
            Color[] result = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color sum = Color.clear;
                for (int k = -r; k <= r; k++)
                {
                    int sy = Mathf.Clamp(y + k, 0, h - 1);
                    sum += pixels[sy * w + x] * kernel[k + r];
                }
                result[y * w + x] = sum;
            }
            return result;
        }
    }
}
