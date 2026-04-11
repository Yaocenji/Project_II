using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// 前景物品管理器（挂载在单个前景物体上）
    /// 为俯视角游戏中的前景物体提供 scale 放大、高斯模糊和 XY 位置偏移效果，
    /// 模拟前景物体距摄像机更近的视觉感受。
    /// 效果参数由 ForegroundManager 统一驱动，本脚本负责封装具体的应用逻辑。
    /// 相同精灵与相同效果参数的多个实例通过 ForegroundTextureCache 共享纹理资产。
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

        [Header("伪透视强度控制")]
        [Tooltip("全局倍率：=0 时完全禁用模糊/位移/缩放，虚拟高度仍正常传入 shader")]
        [Range(0f, 1f)]
        [SerializeField] private float pseudoPerspectiveStrength = 1f;
        [Tooltip("模糊效果强度倍率")]
        [Range(0f, 1f)]
        [SerializeField] private float blurStrength = 1f;
        [Tooltip("XY 视差位移强度倍率")]
        [Range(0f, 1f)]
        [SerializeField] private float offsetStrength = 1f;
        [Tooltip("scale 放大强度倍率")]
        [Range(0f, 1f)]
        [SerializeField] private float scaleStrength = 1f;

        // 组件引用
        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock mpb;

        // Shader 属性 ID 缓存
        // _ForegroundTransformData: xy = 世界空间位置偏移，z = scale 乘数
        // Shader 反推公式：P_pre = (P_post - objectWorldPos) / scale + (objectWorldPos - offset.xy)
        private static readonly int ForegroundTransformDataID =
            Shader.PropertyToID("_ForegroundTransformData");
        private static readonly int EmissionID            = Shader.PropertyToID("_Emission");
        private static readonly int RotationSinCosID      = Shader.PropertyToID("_RotationSinCos");
        private static readonly int GICoefficientID       = Shader.PropertyToID("_GICoefficient");
        private static readonly int BumpMapID             = Shader.PropertyToID("_BumpMap");
        private static readonly int VirtualHeightID       = Shader.PropertyToID("_VirtualHeight");
        private static readonly int MaxHeightID           = Shader.PropertyToID("_MaxHeight");
        private static readonly int SDFTexID              = Shader.PropertyToID("_SDFTex");
        // xy=UV offset，zw=UV scale，用于将 IN.uv 重映射到精灵本地 UV 空间（供 SDF 采样）
        private static readonly int SDFLocalUVTransformID = Shader.PropertyToID("_SDFLocalUVTransform");
        private static readonly int SDFWorldScaleID       = Shader.PropertyToID("_SDFWorldScale");
        private static readonly int CloseTransparentID    = Shader.PropertyToID("_CloseTransparent");

        // 原始状态缓存（OnEnable 时记录）
        private Sprite    originalSprite;
        private Texture2D originalBumpMap;
        private Vector3   baseLocalScale;

        // 当前持有的缓存条目（引用计数已 +1）
        // 各纹理字段均为缓存条目中对应字段的镜像，不直接拥有生命周期
        private ForegroundTextureCacheKey   currentCacheKey;
        private ForegroundTextureCacheEntry currentCacheEntry;
        private bool hasCachedEntry = false;

        // 便捷访问，来自缓存条目
        private Texture2D blurTexture       => currentCacheEntry?.blurTexture;
        private Texture2D blurNormalTexture => currentCacheEntry?.blurNormalTexture;
        private Texture2D sdfTexture        => currentCacheEntry?.sdfTexture;
        private Sprite    blurSprite        => currentCacheEntry?.blurSprite;

        // xy=offset，zw=scale：将 shader 中 IN.uv 映射到精灵本地 UV (0,0)-(1,1)
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

        /// <summary>
        /// 精灵视觉中心的世界坐标（不含前景偏移）。
        /// 用 bounds.center 而非 transform.position，避免非中心 pivot + 非单位 scale 时偏移方向算错。
        /// </summary>
        public Vector2 BaseWorldPosition =>
            (Vector2)spriteRenderer.bounds.center - lastPositionOffset;

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
            // 先恢复视觉状态，再释放缓存（保证 Destroy 时 SpriteRenderer 已不再指向共享 Sprite）
            if (spriteRenderer != null && originalSprite != null)
                spriteRenderer.sprite = originalSprite;

            transform.localScale = baseLocalScale;

            Vector3 pos = transform.position;
            pos.x -= lastPositionOffset.x;
            pos.y -= lastPositionOffset.y;
            transform.position = pos;
            lastPositionOffset = Vector2.zero;

            ReleaseCacheEntry();

            if (spriteRenderer != null)
            {
                spriteRenderer.GetPropertyBlock(mpb);
                mpb.SetVector(ForegroundTransformDataID, new Vector4(0f, 0f, 1f, 0f));
                mpb.SetVector(SDFLocalUVTransformID, new Vector4(0f, 0f, 1f, 1f));
                mpb.SetInt(CloseTransparentID, ifCloseTransparent ? 1 : 0);
                if (originalBumpMap != null)
                    mpb.SetTexture(BumpMapID, originalBumpMap);
                spriteRenderer.SetPropertyBlock(mpb);
            }

            if (ForegroundManager.Instance != null)
                ForegroundManager.Instance.Unregister(this);
        }

        private void OnDestroy()
        {
            // OnDisable 已调用 ReleaseCacheEntry，此处幂等保底
            ReleaseCacheEntry();
        }

        // ────────── 由 ForegroundManager 驱动的公开接口 ──────────

        /// <summary>
        /// 由 ForegroundManager 每帧调用：应用 scale 放大和 XY 位置偏移，并同步 z 轴。
        /// </summary>
        public void UpdateScaleAndOffset(float scaleMultiplier, Vector2 positionOffset, float maxHeight)
        {
            float combinedScale  = pseudoPerspectiveStrength * scaleStrength;
            float combinedOffset = pseudoPerspectiveStrength * offsetStrength;

            float   effectiveScale  = 1f + (scaleMultiplier - 1f) * combinedScale;
            Vector2 effectiveOffset = positionOffset * combinedOffset;

            transform.localScale = baseLocalScale * effectiveScale;

            Vector3 pos = transform.position;
            pos.x += effectiveOffset.x - lastPositionOffset.x;
            pos.y += effectiveOffset.y - lastPositionOffset.y;
            pos.z = virtualHeight;
            transform.position = pos;
            lastPositionOffset = effectiveOffset;

            UpdateMaterialPropertyBlock(effectiveOffset, effectiveScale, maxHeight);
        }

        /// <summary>
        /// 由 ForegroundManager 按需调用：生成或更新模糊纹理与 SDF 纹理（含脏检查）。
        /// </summary>
        public void UpdateBlur(float blurRadius, float fullResScale)
        {
            float effectiveBlurRadius = blurRadius * blurStrength * pseudoPerspectiveStrength;

            bool radiusChanged = Mathf.Abs(effectiveBlurRadius - lastBlurRadius) >= 0.5f;
            bool scaleChanged  = !Mathf.Approximately(lastFullResScale, fullResScale);

            if (!blurDirty && !radiusChanged && !scaleChanged) return;

            GenerateAllTextures(effectiveBlurRadius, fullResScale);

            blurDirty        = false;
            lastBlurRadius    = effectiveBlurRadius;
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
            mpb.SetInt(CloseTransparentID, ifCloseTransparent ? 1 : 0);

            Texture2D bumpToUse = blurNormalTexture != null ? blurNormalTexture : originalBumpMap;
            if (bumpToUse != null)
                mpb.SetTexture(BumpMapID, bumpToUse);

            if (sdfTexture != null)
            {
                mpb.SetTexture(SDFTexID, sdfTexture);
                mpb.SetVector(SDFLocalUVTransformID, sdfLocalUVTransform);
                float worldScale = (transform.lossyScale.x + transform.lossyScale.y) * 0.5f;
                mpb.SetFloat(SDFWorldScaleID, worldScale);
            }

            spriteRenderer.SetPropertyBlock(mpb);
        }

        // ────────── 纹理生成（接入缓存） ──────────

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

            // 构建缓存 key
            int normalMapID = originalBumpMap != null ? originalBumpMap.GetInstanceID() : 0;
            var key = new ForegroundTextureCacheKey(
                originalSprite.GetInstanceID(), normalMapID, r, fullResScale, sdfResolutionScale);

            // 计算 SDF UV 变换（命中缓存时仍需更新，因为 UV 变换是本地状态）
            UpdateSDFUVTransform(r, fullW, fullH, sourceTex, texRect);

            // 释放当前条目，尝试从缓存获取
            ReleaseCacheEntry();

            if (ForegroundTextureCache.TryAcquire(key, out var entry))
            {
                ApplyCacheEntry(key, entry, r);
                return;
            }

            // 缓存未命中：生成所有纹理，注册后应用
            entry = BuildNewEntry(sourceTex, texRect, srcW, srcH, fullW, fullH, r, fullResScale);
            ForegroundTextureCache.Register(key, entry);
            ApplyCacheEntry(key, entry, r);
        }

        /// <summary>
        /// 将缓存条目应用到本实例，设置 SpriteRenderer。
        /// </summary>
        private void ApplyCacheEntry(ForegroundTextureCacheKey key, ForegroundTextureCacheEntry entry, int r)
        {
            currentCacheKey   = key;
            currentCacheEntry = entry;
            hasCachedEntry    = true;

            spriteRenderer.sprite = entry.blurSprite != null ? entry.blurSprite : originalSprite;
        }

        /// <summary>
        /// 释放当前缓存条目（引用计数 -1），清空本地引用。
        /// </summary>
        private void ReleaseCacheEntry()
        {
            if (!hasCachedEntry) return;
            ForegroundTextureCache.Release(currentCacheKey);
            currentCacheEntry = null;
            hasCachedEntry    = false;
        }

        /// <summary>
        /// 更新 SDF UV 变换参数（本地状态，不存入缓存）。
        /// </summary>
        private void UpdateSDFUVTransform(int r, int fullW, int fullH, Texture2D sourceTex, Rect texRect)
        {
            if (r == 0)
            {
                // originalSprite 使用 atlas UV，需要将其映射回精灵本地 UV (0,0)-(1,1)
                float tw = sourceTex.width, th = sourceTex.height;
                sdfLocalUVTransform = new Vector4(
                    texRect.x / tw, texRect.y / th,
                    texRect.width / tw, texRect.height / th);
            }
            else
            {
                // blurSprite 的 UV 包含 padding，需要将其映射回精灵内容区
                int finalW = fullW + 2 * r, finalH = fullH + 2 * r;
                sdfLocalUVTransform = new Vector4(
                    (float)r / finalW, (float)r / finalH,
                    (float)fullW / finalW, (float)fullH / finalH);
            }
        }

        /// <summary>
        /// 生成一套全新的纹理资产并打包为缓存条目（不注册，由调用方注册）。
        /// </summary>
        private ForegroundTextureCacheEntry BuildNewEntry(
            Texture2D sourceTex, Rect texRect, int srcW, int srcH,
            int fullW, int fullH, int r, float fullResScale)
        {
            var entry = new ForegroundTextureCacheEntry
            {
                sdfTexture = CreateSDFTexture(sourceTex, texRect, srcW, srcH)
            };

            if (r == 0) return entry; // 无模糊，仅含 SDF

            float sigma   = Mathf.Max(0.1f, r / 3f);
            float[] kernel = BuildGaussianKernel(r, sigma);
            int outW = fullW + 2 * r, outH = fullH + 2 * r;

            entry.blurTexture = CreateBlurredTexture(
                sourceTex, texRect, fullW, fullH, r, kernel, TextureFormat.RGBA32);

            if (originalBumpMap != null)
            {
                if (!originalBumpMap.isReadable)
                    Debug.LogWarning($"[ForegroundObject] {name}: 法线纹理不可读，跳过法线模糊。");
                else
                    entry.blurNormalTexture = CreateBlurredTexture(
                        originalBumpMap, texRect, fullW, fullH, r, kernel, TextureFormat.RGBA32);
            }

            Vector2 origPivot = originalSprite.pivot;
            // 先归一化再乘以实际取整后的 fullW/fullH，避免 fullResScale 浮点误差被 scale 放大
            float pivotX = (origPivot.x / srcW * fullW + r) / outW;
            float pivotY = (origPivot.y / srcH * fullH + r) / outH;
            float newPpu  = originalSprite.pixelsPerUnit * fullResScale;

            entry.blurSprite = Sprite.Create(
                entry.blurTexture,
                new Rect(0, 0, outW, outH),
                new Vector2(pivotX, pivotY),
                newPpu);

            return entry;
        }

        // ────────── 纹理生成工具函数 ──────────

        /// <summary>
        /// 生成 SDF 纹理（RFloat，R 通道为带符号世界空间距离：正=内部，负=外部）。
        /// 覆盖精灵本地 UV (0,0)-(1,1)，与 atlas 布局无关。
        /// </summary>
        private Texture2D CreateSDFTexture(Texture2D sourceTex, Rect texRect, int srcW, int srcH)
        {
            int sdfW = Mathf.Max(1, Mathf.RoundToInt(srcW * sdfResolutionScale));
            int sdfH = Mathf.Max(1, Mathf.RoundToInt(srcH * sdfResolutionScale));
            int srcX = Mathf.RoundToInt(texRect.x);
            int srcY = Mathf.RoundToInt(texRect.y);

            Color[] srcPixels    = sourceTex.GetPixels(srcX, srcY, srcW, srcH);
            Color[] scaledColors = ScaleBilinear(srcPixels, srcW, srcH, sdfW, sdfH);

            // 在外围加一圈 false（外部像素），确保精灵充满纹理时 SDF 仍有外部种子点
            int padW = sdfW + 2, padH = sdfH + 2;
            bool[] binary = new bool[padW * padH]; // 默认 false，即外部
            for (int y = 0; y < sdfH; y++)
            for (int x = 0; x < sdfW; x++)
                binary[(y + 1) * padW + (x + 1)] = scaledColors[y * sdfW + x].a > 0.5f;

            float[] sdfPadded = ComputeSignedDistanceField(binary, padW, padH);

            // 裁剪掉 padding，恢复原始尺寸
            float[] sdf = new float[sdfW * sdfH];
            for (int y = 0; y < sdfH; y++)
            for (int x = 0; x < sdfW; x++)
                sdf[y * sdfW + x] = sdfPadded[(y + 1) * padW + (x + 1)];

            // sdf 像素 / sdfResolutionScale = 原始精灵像素；/ pixelsPerUnit = 世界单位
            float toWorld = (sdfResolutionScale > 0f ? 1f / sdfResolutionScale : 1f)
                            / originalSprite.pixelsPerUnit;

            Color[] pixels = new Color[sdfW * sdfH];
            for (int i = 0; i < sdf.Length; i++)
                pixels[i] = new Color(sdf[i] * toWorld, 0f, 0f, 1f);

            var tex = new Texture2D(sdfW, sdfH, TextureFormat.RFloat, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateBlurredTexture(
            Texture2D sourceTex, Rect srcRect,
            int fullW, int fullH, int r, float[] kernel, TextureFormat format)
        {
            int srcX = Mathf.RoundToInt(srcRect.x), srcY = Mathf.RoundToInt(srcRect.y);
            int srcW = Mathf.RoundToInt(srcRect.width), srcH = Mathf.RoundToInt(srcRect.height);
            int finalW = fullW + 2 * r, finalH = fullH + 2 * r;

            Color[] src    = sourceTex.GetPixels(srcX, srcY, srcW, srcH);
            Color[] scaled = ScaleBilinear(src, srcW, srcH, fullW, fullH);

            Color[] buffer = new Color[finalW * finalH];
            for (int y = 0; y < fullH; y++)
            for (int x = 0; x < fullW; x++)
                buffer[(y + r) * finalW + (x + r)] = scaled[y * fullW + x];

            Color[] temp = GaussianBlurHorizontal(buffer, finalW, finalH, kernel, r);
            buffer = GaussianBlurVertical(temp, finalW, finalH, kernel, r);

            var tex = new Texture2D(finalW, finalH, format, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.SetPixels(buffer);
            tex.Apply();
            return tex;
        }

        // ────────── 图像算法工具函数 ──────────

        private static Texture2D GetSecondaryBumpMap(Sprite sprite)
        {
            if (sprite == null) return null;
            var secondaries = new SecondarySpriteTexture[2];
            sprite.GetSecondaryTextures(secondaries);
            foreach (var st in secondaries)
                if (st.name == "_BumpMap") return st.texture as Texture2D;
            return null;
        }

        private static float[] ComputeSignedDistanceField(bool[] inside, int w, int h)
        {
            bool[] outside = new bool[inside.Length];
            for (int i = 0; i < inside.Length; i++) outside[i] = !inside[i];

            float[] dOut = ComputeDistanceField(outside, w, h);
            float[] dIn  = ComputeDistanceField(inside,  w, h);

            float[] sdf = new float[w * h];
            for (int i = 0; i < sdf.Length; i++)
                sdf[i] = inside[i] ? dOut[i] : -dIn[i];
            return sdf;
        }

        private static float[] ComputeDistanceField(bool[] source, int w, int h)
        {
            const float D1 = 1f, D2 = 1.41421356f;
            float INF = (w + h) * 2f;

            float[] dist = new float[w * h];
            for (int i = 0; i < dist.Length; i++) dist[i] = source[i] ? 0f : INF;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = dist[y * w + x];
                if (x > 0)             d = Mathf.Min(d, dist[y * w + (x-1)]        + D1);
                if (y > 0)             d = Mathf.Min(d, dist[(y-1) * w + x]         + D1);
                if (x > 0 && y > 0)    d = Mathf.Min(d, dist[(y-1) * w + (x-1)]    + D2);
                if (x < w-1 && y > 0)  d = Mathf.Min(d, dist[(y-1) * w + (x+1)]    + D2);
                dist[y * w + x] = d;
            }
            for (int y = h-1; y >= 0; y--)
            for (int x = w-1; x >= 0; x--)
            {
                float d = dist[y * w + x];
                if (x < w-1)            d = Mathf.Min(d, dist[y * w + (x+1)]        + D1);
                if (y < h-1)            d = Mathf.Min(d, dist[(y+1) * w + x]         + D1);
                if (x < w-1 && y < h-1) d = Mathf.Min(d, dist[(y+1) * w + (x+1)]   + D2);
                if (x > 0 && y < h-1)   d = Mathf.Min(d, dist[(y+1) * w + (x-1)]   + D2);
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
                int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, srcW-1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, srcH-1);
                int x1 = Mathf.Clamp(x0+1, 0, srcW-1), y1 = Mathf.Clamp(y0+1, 0, srcH-1);
                float fx = u - x0, fy = v - y0;
                dst[y * dstW + x] = Color.Lerp(
                    Color.Lerp(src[y0*srcW+x0], src[y0*srcW+x1], fx),
                    Color.Lerp(src[y1*srcW+x0], src[y1*srcW+x1], fx), fy);
            }
            return dst;
        }

        private static float[] BuildGaussianKernel(int r, float sigma)
        {
            int size = 2 * r + 1;
            float[] k = new float[size];
            float sum = 0f;
            for (int i = 0; i < size; i++) { int o = i-r; k[i] = Mathf.Exp(-o*o/(2f*sigma*sigma)); sum += k[i]; }
            for (int i = 0; i < size; i++) k[i] /= sum;
            return k;
        }

        private static Color[] GaussianBlurHorizontal(Color[] p, int w, int h, float[] k, int r)
        {
            Color[] res = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color s = Color.clear;
                for (int i = -r; i <= r; i++) s += p[y*w + Mathf.Clamp(x+i, 0, w-1)] * k[i+r];
                res[y*w+x] = s;
            }
            return res;
        }

        private static Color[] GaussianBlurVertical(Color[] p, int w, int h, float[] k, int r)
        {
            Color[] res = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color s = Color.clear;
                for (int i = -r; i <= r; i++) s += p[Mathf.Clamp(y+i, 0, h-1)*w + x] * k[i+r];
                res[y*w+x] = s;
            }
            return res;
        }
    }
}
