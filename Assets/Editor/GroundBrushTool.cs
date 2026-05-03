using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace ProjectII.Render
{
    /// <summary>
    /// 场景视图笔刷工具：在 GroundSceneSettings 的 Splatmap 上绘制/擦除/平滑 RGBA 通道权重。
    /// 权重归一化：始终维持 R+G+B+A = 1。
    /// 预览对象贯穿场景生命周期（[InitializeOnLoad] + 场景回调），不绑定 Inspector 选中。
    /// </summary>
    [InitializeOnLoad]
    public static class GroundBrushTool
    {
        public enum BrushMode
        {
            Paint,
            Erase,
            Smooth,
        }

        // ── 笔刷参数 ──
        public static BrushMode mode = BrushMode.Paint;
        public static float brushRadius = 2f;
        public static float brushStrength = 0.5f;
        public static int selectedChannel = 0; // 0=R, 1=G, 2=B, 3=A

        // ── 激活状态 ──
        public static bool IsActive { get; private set; }

        private static GroundSceneSettings s_Target;

        // ══════════════════════════════════════════════════════════════════
        // 场景级生命周期（[InitializeOnLoad] + 场景回调）
        // ══════════════════════════════════════════════════════════════════

        static GroundBrushTool()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.sceneOpened += (scene, mode) => RefreshAllPreviews();
            //EditorSceneManager.sceneReloaded += scene => RefreshAllPreviews();
            // 延迟首次刷新，确保场景已加载
            EditorApplication.delayCall += RefreshAllPreviews;
        }

        private static void OnHierarchyChanged()
        {
            // 节流：避免拖拽等高频操作时反复刷新
            if (EditorApplication.timeSinceStartup - s_LastRefreshTime < 0.5) return;
            s_LastRefreshTime = EditorApplication.timeSinceStartup;
            RefreshAllPreviews();
        }

        private static double s_LastRefreshTime;

        /// <summary>遍历所有已加载场景中的 GroundSceneSettings，确保预览对象存在。</summary>
        private static void RefreshAllPreviews()
        {
            // 防止在播放模式或编译中执行
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            for (int si = 0; si < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; si++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(si);
                if (!scene.isLoaded) continue;
                foreach (var go in scene.GetRootGameObjects())
                {
                    var settings = go.GetComponentInChildren<GroundSceneSettings>();
                    if (settings != null)
                        EnsurePreview(settings);
                }
            }
        }

        // ── 笔刷绘制缓存 ──
        private static Color32[] s_Pixels;
        private static int s_TexW, s_TexH;
        private static bool s_Dirty;
        private static bool s_StrokeActive;
        private static Vector4 s_CachedAABB;
        private static int s_StrokeFrameCount;
        private const int k_GpuSyncInterval = 4;

        // ── 预览 ──
        private static Sprite s_PreviewSprite;

        // ══════════════════════════════════════════════════════════════════
        // 预览生命周期（贯穿场景，由 [InitializeOnLoad] + 场景回调驱动）
        // ══════════════════════════════════════════════════════════════════

        /// <summary>确保预览子物体存在。由场景级回调自动调用，也可手动调用。</summary>
        public static void EnsurePreview(GroundSceneSettings target)
        {
            EnsurePreviewInternal(target, false);
        }

        /// <summary>强制重建预览（splatmap 等属性变更时调用）。</summary>
        public static void ForceRefreshPreview(GroundSceneSettings target)
        {
            // 先销毁旧的，再重建
            if (target != null && target.previewGO != null)
            {
                Object.DestroyImmediate(target.previewGO);
                target.previewGO = null;
                target.previewSR = null;
            }
            EnsurePreviewInternal(target, false);
        }

        private static void EnsurePreviewInternal(GroundSceneSettings target, bool force)
        {
            if (target == null || target.splatmap == null) return;

            // 如果预览已存在且有效，跳过（除非强制刷新）
            if (!force && target.previewGO != null && target.previewSR != null && target.previewSR.sprite != null)
                return;

            Vector4 aabb = GroundSceneSettings.GetSceneAABB();
            float rangeX = aabb.z - aabb.x;
            float rangeY = aabb.w - aabb.y;
            if (rangeX < 0.001f || rangeY < 0.001f) return;

            // 从 PNG 加载自动生成的 Sprite
            string pngPath = GetSplatmapPath(target);
            Sprite autoSprite = null;
            var assets = AssetDatabase.LoadAllAssetsAtPath(pngPath);
            foreach (var a in assets)
                if (a is Sprite s) { autoSprite = s; break; }

            if (autoSprite == null)
            {
                float ppu = target.splatmap.width / rangeX;
                if (s_PreviewSprite != null) Object.DestroyImmediate(s_PreviewSprite);
                s_PreviewSprite = Sprite.Create(target.splatmap,
                    new Rect(0, 0, target.splatmap.width, target.splatmap.height),
                    new Vector2(0f, 0f), ppu);
                s_PreviewSprite.name = "_SplatmapPreviewSprite";
                autoSprite = s_PreviewSprite;
            }

            // 创建或复用 GameObject
            var existing = target.transform.Find(GroundSceneSettings.k_PreviewChildName);
            if (existing != null)
                target.previewGO = existing.gameObject;
            else
            {
                target.previewGO = new GameObject(GroundSceneSettings.k_PreviewChildName);
                target.previewGO.transform.SetParent(target.transform, false);
            }

            target.previewGO.transform.position = new Vector3(aabb.x, aabb.y, 0f);
            target.previewGO.transform.localRotation = Quaternion.identity;
            target.previewGO.transform.localScale = Vector3.one;

            target.previewSR = target.previewGO.GetComponent<SpriteRenderer>();
            if (target.previewSR == null)
                target.previewSR = target.previewGO.AddComponent<SpriteRenderer>();

            target.previewSR.sprite = autoSprite;
            target.previewSR.color = new Color(1f, 1f, 1f, 0.5f);
            target.previewSR.sharedMaterial = target.previewMaterial;
            target.previewSR.sortingLayerID = SortingLayer.NameToID("Default");
            target.previewSR.sortingOrder = 9999;

            target.previewGO.SetActive(target.previewVisible);
        }

        /// <summary>销毁预览子物体和临时 Sprite。</summary>
        public static void DestroyPreview(GroundSceneSettings target)
        {
            if (target == null) return;
            if (s_PreviewSprite != null)
            {
                Object.DestroyImmediate(s_PreviewSprite);
                s_PreviewSprite = null;
            }
            if (target.previewGO != null)
            {
                Object.DestroyImmediate(target.previewGO);
                target.previewGO = null;
                target.previewSR = null;
            }
        }

        public static void Activate(GroundSceneSettings target)
        {
            if (IsActive) Deactivate();
            s_Target = target;
            IsActive = true;
            CacheSplatmapData();
            s_Target.SetPreviewVisible(true);
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.delayCall += () => SceneView.RepaintAll();
        }

        public static void Deactivate()
        {
            FlushStroke();
            if (s_Target != null)
                s_Target.SetPreviewVisible(false);
            s_Target = null;
            s_Pixels = null;
            IsActive = false;
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }

        private static void CacheSplatmapData()
        {
            if (s_Target != null && s_Target.splatmap != null && s_Target.splatmap.isReadable)
            {
                var tex = s_Target.splatmap;
                s_Pixels = tex.GetPixels32();
                s_TexW = tex.width;
                s_TexH = tex.height;
            }
            else
            {
                s_Pixels = null;
                s_TexW = s_TexH = 0;
            }
            s_CachedAABB = GroundSceneSettings.GetSceneAABB();
            s_Dirty = false;
            s_StrokeActive = false;
            s_StrokeFrameCount = 0;
        }

        /// <summary>同步像素到 GPU 内存纹理（笔画过程中节流调用，不写盘）。</summary>
        private static void SyncPreviewTexture()
        {
            if (s_Target == null || s_Target.splatmap == null || s_Pixels == null) return;
            s_Target.splatmap.SetPixels32(s_Pixels);
            s_Target.splatmap.Apply(false, false);
        }

        /// <summary>将缓存的像素写回 PNG 并重导入。</summary>
        private static void FlushStroke()
        {
            if (s_Dirty && s_Target != null && s_Target.splatmap != null && s_Pixels != null)
                SaveSplatmapToDisk(s_Target);
            s_Dirty = false;
            s_StrokeActive = false;
            s_StrokeFrameCount = 0;
        }

        // ── Scene GUI ──────────────────────────────────────────────────────

        private static void OnSceneGUI(SceneView sv)
        {
            if (s_Target == null) { Deactivate(); return; }

            Event e = Event.current;

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            sv.Repaint();

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 mouseWorld = GetWorldPosOnZPlane(ray, 0f);

            DrawBrushCursor(mouseWorld);
            DrawSceneAABB(s_CachedAABB);

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                s_StrokeActive = true;
                s_StrokeFrameCount = 0;
                Undo.RecordObject(s_Target.splatmap, "Paint Splatmap");
            }

            bool isPaint = (e.type == EventType.MouseDrag || e.type == EventType.MouseDown)
                           && e.button == 0 && !e.alt;
            if (isPaint)
            {
                bool eraseMode = e.control;
                var prevMode = mode;
                if (eraseMode) mode = BrushMode.Erase;

                if (PaintStroke(new Vector2(mouseWorld.x, mouseWorld.y)))
                {
                    s_StrokeFrameCount++;
                    if (s_StrokeFrameCount >= k_GpuSyncInterval)
                    {
                        SyncPreviewTexture();
                        s_StrokeFrameCount = 0;
                    }
                    e.Use();
                }

                if (eraseMode) mode = prevMode;
            }

            if (e.type == EventType.MouseUp && e.button == 0 && s_StrokeActive)
                FlushStroke();
        }

        private static Vector3 GetWorldPosOnZPlane(Ray ray, float z)
        {
            if (Mathf.Abs(ray.direction.z) > 1e-6f)
            {
                float t = (z - ray.origin.z) / ray.direction.z;
                return ray.origin + ray.direction * t;
            }
            return ray.origin;
        }

        private static void DrawBrushCursor(Vector3 center)
        {
            Handles.color = mode == BrushMode.Paint
                ? new Color(0.3f, 0.9f, 0.3f, 0.6f)
                : mode == BrushMode.Erase
                    ? new Color(0.9f, 0.3f, 0.3f, 0.6f)
                    : new Color(0.3f, 0.6f, 0.9f, 0.6f);

            Handles.DrawWireDisc(center, Vector3.forward, brushRadius);

            string chLabel = selectedChannel switch
            {
                0 => "R", 1 => "G", 2 => "B", 3 => "A", _ => "?"
            };
            Handles.Label(center + Vector3.up * brushRadius * 1.1f,
                $"{chLabel} | {mode}", EditorStyles.miniLabel);
        }

        private static void DrawSceneAABB(Vector4 aabb)
        {
            Handles.color = new Color(1f, 1f, 0.3f, 0.5f);
            Vector3[] corners = {
                new Vector3(aabb.x, aabb.y, 0), new Vector3(aabb.z, aabb.y, 0),
                new Vector3(aabb.z, aabb.w, 0), new Vector3(aabb.x, aabb.w, 0)
            };
            for (int i = 0; i < 4; i++)
                Handles.DrawLine(corners[i], corners[(i + 1) % 4]);
        }

        // ── 笔刷绘制核心（归一化权重版） ──────────────────────────────────

        private static bool PaintStroke(Vector2 worldPos)
        {
            if (s_Pixels == null) return false;

            Vector4 aabb = s_CachedAABB;
            float rangeX = aabb.z - aabb.x;
            float rangeY = aabb.w - aabb.y;
            if (rangeX < 0.001f || rangeY < 0.001f) return false;

            int w = s_TexW, h = s_TexH;
            float pu = (worldPos.x - aabb.x) / rangeX * w;
            float pv = (worldPos.y - aabb.y) / rangeY * h;
            float pixelRadius = brushRadius / rangeX * w;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(pu - pixelRadius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(pv - pixelRadius));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(pu + pixelRadius));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(pv + pixelRadius));
            if (x0 > x1 || y0 > y1) return false;

            bool changed = false;
            float radiusSq = pixelRadius * pixelRadius;

            for (int py = y0; py <= y1; py++)
            for (int px = x0; px <= x1; px++)
            {
                float dx = px - pu, dy = py - pv;
                float distSq = dx * dx + dy * dy;
                if (distSq > radiusSq) continue;

                float falloff = 1f - Mathf.Sqrt(distSq) / pixelRadius;
                falloff = falloff * falloff;

                int idx = py * w + px;
                Color32 c = s_Pixels[idx];
                float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f, a = c.a / 255f;

                switch (mode)
                {
                    case BrushMode.Paint:
                    {
                        float current = GetChannelF(r, g, b, a, selectedChannel);
                        float delta = falloff * brushStrength;
                        float target = Mathf.Clamp01(current + delta);
                        float actualDelta = target - current;
                        if (actualDelta > 0f)
                        {
                            // 其他通道按原始比例缩减 actualDelta 的量
                            float otherSum = r + g + b + a - current;
                            if (otherSum > 0.001f)
                            {
                                float scale = (otherSum - actualDelta) / otherSum;
                                r = (selectedChannel == 0) ? target : r * scale;
                                g = (selectedChannel == 1) ? target : g * scale;
                                b = (selectedChannel == 2) ? target : b * scale;
                                a = (selectedChannel == 3) ? target : a * scale;
                            }
                            else
                            {
                                SetChannelF(ref r, ref g, ref b, ref a, selectedChannel, target);
                            }
                        }
                        NormalizeWeights(ref r, ref g, ref b, ref a);
                        break;
                    }
                    case BrushMode.Erase:
                    {
                        float current = GetChannelF(r, g, b, a, selectedChannel);
                        float target = Mathf.Max(0f, current - falloff * brushStrength);
                        float remaining = 1f - target;
                        float otherSum = r + g + b + a - current;
                        SetChannelF(ref r, ref g, ref b, ref a, selectedChannel, target);
                        if (otherSum > 0.001f && remaining > 0.001f)
                        {
                            float scale = remaining / otherSum;
                            if (selectedChannel != 0) r *= scale;
                            if (selectedChannel != 1) g *= scale;
                            if (selectedChannel != 2) b *= scale;
                            if (selectedChannel != 3) a *= scale;
                        }
                        NormalizeWeights(ref r, ref g, ref b, ref a);
                        break;
                    }
                    case BrushMode.Smooth:
                    {
                        float sumW = 0f;
                        int count = 0;
                        for (int ky = -1; ky <= 1; ky++)
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int nx = px + kx, ny = py + ky;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            Color32 nc = s_Pixels[ny * w + nx];
                            sumW += GetChannelF(nc.r / 255f, nc.g / 255f, nc.b / 255f, nc.a / 255f, selectedChannel);
                            count++;
                        }
                        float avg = sumW / count;
                        float current = GetChannelF(r, g, b, a, selectedChannel);
                        float smoothed = Mathf.Lerp(current, avg, falloff * brushStrength);
                        float scale = (1f - smoothed) / Mathf.Max(1f - current, 0.001f);
                        r = (selectedChannel == 0) ? smoothed : r * scale;
                        g = (selectedChannel == 1) ? smoothed : g * scale;
                        b = (selectedChannel == 2) ? smoothed : b * scale;
                        a = (selectedChannel == 3) ? smoothed : a * scale;
                        NormalizeWeights(ref r, ref g, ref b, ref a);
                        break;
                    }
                }

                // 量化时保证 R+G+B+A = 255，消除取整累积误差
                int ir = Mathf.RoundToInt(r * 255f);
                int ig = Mathf.RoundToInt(g * 255f);
                int ib = Mathf.RoundToInt(b * 255f);
                int ia = Mathf.RoundToInt(a * 255f);
                int diff = 255 - (ir + ig + ib + ia);
                if (diff != 0)
                {
                    // 将余量加到值最大的通道，减少视觉偏差
                    int maxCh = Mathf.Max(ir, ig, ib, ia);
                    if (ir == maxCh) ir += diff;
                    else if (ig == maxCh) ig += diff;
                    else if (ib == maxCh) ib += diff;
                    else ia += diff;
                }
                c.r = (byte)Mathf.Clamp(ir, 0, 255);
                c.g = (byte)Mathf.Clamp(ig, 0, 255);
                c.b = (byte)Mathf.Clamp(ib, 0, 255);
                c.a = (byte)Mathf.Clamp(ia, 0, 255);
                s_Pixels[idx] = c;
                changed = true;
            }

            if (changed)
                s_Dirty = true;

            return changed;
        }

        private static void NormalizeWeights(ref float r, ref float g, ref float b, ref float a)
        {
            float sum = r + g + b + a;
            if (sum > 0.001f) { r /= sum; g /= sum; b /= sum; a /= sum; }
            else { r = 1f; g = b = a = 0f; }
        }

        private static float GetChannelF(float r, float g, float b, float a, int channel)
            => channel switch { 0 => r, 1 => g, 2 => b, 3 => a, _ => r };

        private static void SetChannelF(ref float r, ref float g, ref float b, ref float a, int channel, float value)
        { switch (channel) { case 0: r = value; break; case 1: g = value; break; case 2: b = value; break; case 3: a = value; break; } }

        // ── Splatmap 初始化 ────────────────────────────────────────────────

        private static string GetSplatmapPath(GroundSceneSettings settings)
        {
            string scenePath = settings.gameObject.scene.path;
            string dir = string.IsNullOrEmpty(scenePath)
                ? "Assets"
                : Path.GetDirectoryName(scenePath).Replace('\\', '/');
            return dir + "/" + settings.gameObject.name + "_Splatmap.png";
        }

        public static Texture2D CreateSplatmap(GroundSceneSettings settings)
        {
            Vector4 aabb = GroundSceneSettings.GetSceneAABB();
            float rangeX = aabb.z - aabb.x;
            float rangeY = aabb.w - aabb.y;
            if (rangeX < 0.001f || rangeY < 0.001f) return null;

            int w = Mathf.Max(1, Mathf.CeilToInt(rangeX * settings.pixelsPerUnit));
            int h = Mathf.Max(1, Mathf.CeilToInt(rangeY * settings.pixelsPerUnit));

            var tex = new Texture2D(w, h, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = settings.gameObject.name + "_Splatmap"
            };

            var pixels = tex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 0, 0, 0);
            tex.SetPixels32(pixels);
            tex.Apply(false);

            string pngPath = GetSplatmapPath(settings);
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            // 配置导入：Sprite 模式 + 可读
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.isReadable = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                // Pivot 左下角，和 AABB 左下角对齐；PPU 确保世界尺寸覆盖 AABB
                var texSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(texSettings);
                texSettings.spriteAlignment = (int)SpriteAlignment.Custom;
                texSettings.spritePivot = new Vector2(0f, 0f);
                texSettings.spritePixelsPerUnit = w / rangeX;
                //texSettings.alphaSource = TextureImporterAlphaSource.None;
                importer.SetTextureSettings(texSettings);
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            settings.splatmap = loaded;
            EditorUtility.SetDirty(settings);

            return loaded;
        }

        /// <summary>将像素写回 PNG 并重导入（鼠标抬起时调用）。</summary>
        public static void SaveSplatmapToDisk(GroundSceneSettings settings)
        {
            if (settings == null || settings.splatmap == null || s_Pixels == null) return;

            string pngPath = GetSplatmapPath(settings);
            var tex = settings.splatmap;
            tex.SetPixels32(s_Pixels);
            tex.Apply(false);

            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            EditorUtility.SetDirty(tex);
        }
    }
}
