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

        [Header("SDF 设置")]
        [Tooltip("SDF 纹理的分辨率相对于精灵原始像素尺寸的比例，降低可节省性能")]
        [Range(0.1f, 2f)]
        [SerializeField] private float sdfResolutionScale = 0.5f;
        
        [Header("是否随着人物接近而透明")]
        [Tooltip("是否随着人物接近而透明")]
        [SerializeField] private bool ifCloseTransparent = true;

        // 组件引用
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock mpb;

        // Shader 属性 ID 缓存
        // _ForegroundTransformData: xy = 世界空间位置偏移，z = scale 乘数
        // Shader 反推公式：P_pre = (P_post - objectWorldPos) / scale + (objectWorldPos - offset.xy)
        private static readonly int ForegroundTransformDataID =
            Shader.PropertyToID("_ForegroundTransformData");
        private static readonly int EmissionID              = Shader.PropertyToID("_Emission");
        private static readonly int RotationSinCosID        = Shader.PropertyToID("_RotationSinCos");
        private static readonly int GICoefficientID         = Shader.PropertyToID("_GICoefficient");
        private static readonly int BumpMapID               = Shader.PropertyToID("_BumpMap");
        private static readonly int VirtualHeightID         = Shader.PropertyToID("_VirtualHeight");
        private static readonly int MaxHeightID             = Shader.PropertyToID("_MaxHeight");
        private static readonly int SDFTexID                = Shader.PropertyToID("_SDFTex");
        // xy=UV offset，zw=UV scale，用于将当前 sprite 的 UV 重映射到精灵本地 UV 空间（供 SDF 采样）
        private static readonly int SDFLocalUVTransformID   = Shader.PropertyToID("_SDFLocalUVTransform");
        private static readonly int CloseTransparent   = Shader.PropertyToID("_CloseTransparent");

        // 原始状态缓存（OnEnable 时记录）
        private Sprite    originalSprite;
        private Texture2D originalBumpMap;
        private Vector3   baseLocalScale;

        // 运行时生成的模糊资产（颜色 + 法线），独立于 SDF
        private Texture2D blurTexture;
        private Texture2D blurNormalTexture;
        private Sprite    blurSprite;

        // SDF 资产（始终维护，与模糊状态无关）
        private Texture2D sdfTexture;
        // xy=offset，zw=scale：将 shader 中 IN.uv 映射到精灵本地 UV (0,0)-(1,1)
        // r=0 时需要将 atlas UV 映射回来；r>0 时需要将含 padding 的 UV 映射回精灵内容区域
        private Vector4 sdfLocalUVTransform = new Vector4(0f, 0f, 1f, 1f);

        // 脏标记与上次参数缓存
        private bool  blurDirty       = true;
        private float lastBlurRadius   = -1f;
        private float lastFullResScale  = -1f;

        // 上一帧应用的位置偏移（增量更新用，避免偏移累积）
        private Vector2 lastPositionOffset = Vector2.zero;

        /// <summary>
        /// 虚拟高度（距地面的距离）。值越大表示物体离地越高，三种前景效果越强。
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

        /// <summary>不含前景偏移效果的世界空间 XY 位置（用于方向计算，避免偏移反馈）</summary>
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
            if (spriteRenderer != null && originalSprite != null)
                spriteRenderer.sprite = originalSprite;

            transform.localScale = baseLocalScale;

            Vector3 pos = transform.position;
            pos.x -= lastPositionOffset.x;
            pos.y -= lastPositionOffset.y;
            transform.position = pos;
            lastPositionOffset = Vector2.zero;

            if (spriteRenderer != null)
            {
                spriteRenderer.GetPropertyBlock(mpb);
                mpb.SetVector(ForegroundTransformDataID, new Vector4(0f, 0f, 1f, 0f));
                mpb.SetVector(SDFLocalUVTransformID, new Vector4(0f, 0f, 1f, 1f));
                mpb.SetInt(CloseTransparent, ifCloseTransparent ? 1 : 0);
                if (originalBumpMap != null)
                    mpb.SetTexture(BumpMapID, originalBumpMap);
                spriteRenderer.SetPropertyBlock(mpb);
            }

            if (ForegroundManager.Instance != null)
                ForegroundManager.Instance.Unregister(this);
        }

        private void OnDestroy()
        {
            ReleaseBlurColorAssets();
            ReleaseSDFAsset();
        }

        // ────────── 由 ForegroundManager 驱动的公开接口 ──────────

        /// <summary>
        /// 由 ForegroundManager 每帧调用：应用 scale 放大和 XY 位置偏移，并同步 z 轴。
        /// </summary>
        public void UpdateScaleAndOffset(float scaleMultiplier, Vector2 positionOffset, float maxHeight)
        {
            transform.localScale = baseLocalScale * scaleMultiplier;

            Vector3 pos = transform.position;
            pos.x += positionOffset.x - lastPositionOffset.x;
            pos.y += positionOffset.y - lastPositionOffset.y;
            pos.z = virtualHeight;
            transform.position = pos;
            lastPositionOffset = positionOffset;

            UpdateMaterialPropertyBlock(positionOffset, scaleMultiplier, maxHeight);
        }

        /// <summary>
        /// 由 ForegroundManager 按需调用：生成或更新模糊纹理与 SDF 纹理（含脏检查）。
        /// </summary>
        public void UpdateBlur(float blurRadius, float fullResScale)
        {
            bool radiusChanged = Mathf.Abs(blurRadius - lastBlurRadius) >= 0.5f;
            bool scaleChanged  = !Mathf.Approximately(lastFullResScale, fullResScale);

            if (!blurDirty && !radiusChanged && !scaleChanged) return;

            GenerateAllTextures(blurRadius, fullResScale);

            blurDirty       = false;
            lastBlurRadius   = blurRadius;
            lastFullResScale  = fullResScale;
        }

        // ────────── MPB ──────────

        private void UpdateMaterialPropertyBlock(Vector2 positionOffset, float scaleMultiplier, float maxHeight)
        {
            spriteRenderer.GetPropertyBlock(mpb);

            mpb.SetVector(ForegroundTransformDataID,
                new Vector4(positionOffset.x, positionOffset.y, scaleMultiplier, 0f));

            mpb.SetColor(EmissionID, emission);

            float rotZ = -transform.eulerAngles.z * Mathf.Deg2Rad;
            mpb.SetVector(RotationSinCosID, new Vector4(Mathf.Cos(rotZ), Mathf.Sin(rotZ), 0f, 0f));

            mpb.SetFloat(GICoefficientID, giCoefficient);
            mpb.SetFloat(VirtualHeightID, virtualHeight);
            mpb.SetFloat(MaxHeightID, maxHeight);

            Texture2D bumpToUse = blurNormalTexture != null ? blurNormalTexture : originalBumpMap;
            if (bumpToUse != null)
                mpb.SetTexture(BumpMapID, bumpToUse);

            if (sdfTexture != null)
            {
                mpb.SetTexture(SDFTexID, sdfTexture);
                mpb.SetVector(SDFLocalUVTransformID, sdfLocalUVTransform);
            }
            
            mpb.SetInt(CloseTransparent, ifCloseTransparent ? 1 : 0);

            spriteRenderer.SetPropertyBlock(mpb);
        }

        // ────────── 纹理生成 ──────────

        private void GenerateAllTextures(float blurRadius, float fullResScale)
        {
            if (originalSprite == null) return;

            Texture2D sourceTex = originalSprite.texture;
            if (sourceTex == null)
            {
                Debug.LogWarning($"[ForegroundObject] {name}: 精灵纹理为空，跳过纹理生成。");
                return;
            }
            if (!sourceTex.isReadable)
            {
                Debug.LogWarning($"[ForegroundObject] {name}: 精灵纹理不可读，请在导入设置中启用 Read/Write Enabled。");
                return;
            }

            Rect texRect = originalSprite.textureRect;
            int srcW = Mathf.RoundToInt(texRect.width);
            int srcH = Mathf.RoundToInt(texRect.height);
            if (srcW <= 0 || srcH <= 0) return;

            int fullW = Mathf.Max(1, Mathf.RoundToInt(srcW * fullResScale));
            int fullH = Mathf.Max(1, Mathf.RoundToInt(srcH * fullResScale));
            int r     = Mathf.Max(0, Mathf.RoundToInt(blurRadius));

            // ── SDF（始终生成，不受模糊半径影响）──
            RegenerateSDFTexture(sourceTex, texRect, srcW, srcH);

            // ── SDF UV 变换 ──
            // SDF 纹理覆盖精灵本地 UV 空间 (0,0)-(1,1)
            // 当 r=0 时，spriteRenderer 使用 originalSprite（atlas UV），需要将其映射回本地 UV
            // 当 r>0 时，spriteRenderer 使用 blurSprite（全图 UV），需要将 padding 区域的 UV 映射回精灵内容区
            if (r == 0)
            {
                // atlas UV offset/scale（_MainTex_ST 等价值）
                float tw = sourceTex.width;
                float th = sourceTex.height;
                sdfLocalUVTransform = new Vector4(
                    texRect.x / tw, texRect.y / th,
                    texRect.width / tw, texRect.height / th
                );
            }
            else
            {
                int finalW = fullW + 2 * r;
                int finalH = fullH + 2 * r;
                sdfLocalUVTransform = new Vector4(
                    (float)r / finalW, (float)r / finalH,
                    (float)fullW / finalW, (float)fullH / finalH
                );
            }

            // ── 模糊（r=0 时直接恢复原始精灵）──
            if (r == 0)
            {
                ReleaseBlurColorAssets();
                spriteRenderer.sprite = originalSprite;
                return;
            }

            float sigma = Mathf.Max(0.1f, r / 3f);
            float[] kernel = BuildGaussianKernel(r, sigma);
            int outW = fullW + 2 * r;
            int outH = fullH + 2 * r;

            ReleaseBlurColorAssets();

            blurTexture = CreateBlurredTexture(sourceTex, texRect, fullW, fullH, r, kernel, TextureFormat.RGBA32);

            if (originalBumpMap != null)
            {
                if (!originalBumpMap.isReadable)
                    Debug.LogWarning($"[ForegroundObject] {name}: 法线纹理不可读，跳过法线模糊。");
                else
                    blurNormalTexture = CreateBlurredTexture(originalBumpMap, texRect,
                                                             fullW, fullH, r, kernel, TextureFormat.RGBA32);
            }

            Vector2 origPivot = originalSprite.pivot;
            float pivotX = (origPivot.x * fullResScale + r) / outW;
            float pivotY = (origPivot.y * fullResScale + r) / outH;
            float newPpu = originalSprite.pixelsPerUnit * fullResScale;

            blurSprite = Sprite.Create(
                blurTexture,
                new Rect(0, 0, outW, outH),
                new Vector2(pivotX, pivotY),
                newPpu
            );

            spriteRenderer.sprite = blurSprite;
        }

        /// <summary>
        /// 生成 SDF 纹理，使用可配置的缩减分辨率。
        /// 纹理格式 RFloat，R 通道存储带符号的世界空间距离：正=内部，负=外部，0=边界。
        /// 纹理覆盖精灵本地 UV 空间 (0,0)-(1,1)，与 atlas 布局无关。
        /// </summary>
        private void RegenerateSDFTexture(Texture2D sourceTex, Rect texRect, int srcW, int srcH)
        {
            int sdfW = Mathf.Max(1, Mathf.RoundToInt(srcW * sdfResolutionScale));
            int sdfH = Mathf.Max(1, Mathf.RoundToInt(srcH * sdfResolutionScale));

            int srcX = Mathf.RoundToInt(texRect.x);
            int srcY = Mathf.RoundToInt(texRect.y);

            Color[] srcPixels    = sourceTex.GetPixels(srcX, srcY, srcW, srcH);
            Color[] scaledColors = ScaleBilinear(srcPixels, srcW, srcH, sdfW, sdfH);

            bool[] binary = new bool[sdfW * sdfH];
            for (int i = 0; i < binary.Length; i++)
                binary[i] = scaledColors[i].a > 0.5f;

            float[] sdf = ComputeSignedDistanceField(binary, sdfW, sdfH);

            // 将 SDF 距离从 "sdf 像素" 转换到世界空间单位
            // sdf像素 / sdfResolutionScale = 原始精灵像素；原始精灵像素 / pixelsPerUnit = 世界单位
            float ppu = originalSprite.pixelsPerUnit;
            float toWorld = (sdfResolutionScale > 0f ? 1f / sdfResolutionScale : 1f) / ppu;

            Color[] pixels = new Color[sdfW * sdfH];
            for (int i = 0; i < sdf.Length; i++)
                pixels[i] = new Color(sdf[i] * toWorld, 0f, 0f, 1f);

            ReleaseSDFAsset();

            sdfTexture = new Texture2D(sdfW, sdfH, TextureFormat.RFloat, false);
            sdfTexture.filterMode = FilterMode.Bilinear;
            sdfTexture.wrapMode   = TextureWrapMode.Clamp;
            sdfTexture.SetPixels(pixels);
            sdfTexture.Apply();
        }

        // ────────── 资产释放 ──────────

        private void ReleaseBlurColorAssets()
        {
            if (blurSprite != null)        { Destroy(blurSprite);        blurSprite        = null; }
            if (blurTexture != null)       { Destroy(blurTexture);       blurTexture       = null; }
            if (blurNormalTexture != null) { Destroy(blurNormalTexture); blurNormalTexture = null; }
        }

        private void ReleaseSDFAsset()
        {
            if (sdfTexture != null) { Destroy(sdfTexture); sdfTexture = null; }
        }

        // ────────── 工具函数 ──────────

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

        /// <summary>
        /// 从源纹理的指定区域生成一张模糊后的 Texture2D。
        /// 输出尺寸为 (fullW + 2r) x (fullH + 2r)。
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

            Color[] srcPixels    = sourceTex.GetPixels(srcX, srcY, srcW, srcH);
            Color[] scaledPixels = ScaleBilinear(srcPixels, srcW, srcH, fullW, fullH);

            Color[] buffer = new Color[finalW * finalH];
            for (int y = 0; y < fullH; y++)
            for (int x = 0; x < fullW; x++)
                buffer[(y + r) * finalW + (x + r)] = scaledPixels[y * fullW + x];

            Color[] temp = GaussianBlurHorizontal(buffer, finalW, finalH, kernel, r);
            buffer = GaussianBlurVertical(temp, finalW, finalH, kernel, r);

            Texture2D tex = new Texture2D(finalW, finalH, format, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.SetPixels(buffer);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 计算有符号距离场：内部像素为正（到最近外部边界的距离），外部像素为负（到最近内部边界的距离）。
        /// 使用两遍 chamfer 距离变换，O(w*h)。
        /// </summary>
        private static float[] ComputeSignedDistanceField(bool[] inside, int w, int h)
        {
            bool[] outside = new bool[inside.Length];
            for (int i = 0; i < inside.Length; i++) outside[i] = !inside[i];

            float[] distToOutside = ComputeDistanceField(outside, w, h); // 内部像素→外部边界
            float[] distToInside  = ComputeDistanceField(inside,  w, h); // 外部像素→内部边界

            float[] sdf = new float[w * h];
            for (int i = 0; i < sdf.Length; i++)
                sdf[i] = inside[i] ? distToOutside[i] : -distToInside[i];
            return sdf;
        }

        /// <summary>
        /// 单次无符号距离变换：对每个像素计算到最近源像素（source=true）的 chamfer 近似距离。
        /// 两遍扫描（左上→右下，右下→左上），O(w*h)。
        /// </summary>
        private static float[] ComputeDistanceField(bool[] source, int w, int h)
        {
            const float D1 = 1f;
            const float D2 = 1.41421356f;
            float INF = (w + h) * 2f;

            float[] dist = new float[w * h];
            for (int i = 0; i < dist.Length; i++)
                dist[i] = source[i] ? 0f : INF;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = dist[y * w + x];
                if (x > 0)            d = Mathf.Min(d, dist[y * w + (x - 1)]         + D1);
                if (y > 0)            d = Mathf.Min(d, dist[(y-1) * w + x]            + D1);
                if (x > 0 && y > 0)   d = Mathf.Min(d, dist[(y-1) * w + (x-1)]       + D2);
                if (x < w-1 && y > 0) d = Mathf.Min(d, dist[(y-1) * w + (x+1)]       + D2);
                dist[y * w + x] = d;
            }

            for (int y = h-1; y >= 0; y--)
            for (int x = w-1; x >= 0; x--)
            {
                float d = dist[y * w + x];
                if (x < w-1)            d = Mathf.Min(d, dist[y * w + (x+1)]          + D1);
                if (y < h-1)            d = Mathf.Min(d, dist[(y+1) * w + x]           + D1);
                if (x < w-1 && y < h-1) d = Mathf.Min(d, dist[(y+1) * w + (x+1)]      + D2);
                if (x > 0 && y < h-1)   d = Mathf.Min(d, dist[(y+1) * w + (x-1)]      + D2);
                dist[y * w + x] = d;
            }

            return dist;
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
                float fx = u - x0, fy = v - y0;
                dst[y * dstW + x] = Color.Lerp(
                    Color.Lerp(src[y0 * srcW + x0], src[y0 * srcW + x1], fx),
                    Color.Lerp(src[y1 * srcW + x0], src[y1 * srcW + x1], fx), fy);
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
                int o = i - r;
                kernel[i] = Mathf.Exp(-o * o / (2f * sigma * sigma));
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
                    sum += pixels[y * w + Mathf.Clamp(x + k, 0, w - 1)] * kernel[k + r];
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
                    sum += pixels[Mathf.Clamp(y + k, 0, h - 1) * w + x] * kernel[k + r];
                result[y * w + x] = sum;
            }
            return result;
        }
    }
}
