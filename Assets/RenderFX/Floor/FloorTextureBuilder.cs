using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// 把多边形区域光栅化为一张 Texture2D，按地砖格子随机采样 Sprite。
    /// 所有计算在局部空间进行，与父物体旋转/缩放无关。
    /// 所有传入 Sprite 必须共享同一张 Texture（Sprite Atlas），且 PPU 一致。
    /// </summary>
    public static class FloorTextureBuilder
    {
        private const float k_Sqrt3Over2 = 0.8660254f;

        /// <summary>
        /// 生成地板纹理（局部空间）。
        /// </summary>
        public static Texture2D Build(
            IList<Vector2> localPolygon,
            IList<Sprite>  sprites,
            Vector2        tileSize,
            int            seed,
            bool           useHexTile,
            float          tileRotation,
            out Vector2    outOriginLocal,
            out Texture2D  bumpTex)
        {
            outOriginLocal = Vector2.zero;
            bumpTex = null;
            if (localPolygon == null || localPolygon.Count < 3 || sprites == null || sprites.Count == 0)
                return null;

            float      ppu    = sprites[0].pixelsPerUnit;
            Texture2D  srcTex = sprites[0].texture;

            // 局部空间 AABB
            Vector2 polyMin = localPolygon[0];
            Vector2 polyMax = localPolygon[0];
            for (int i = 1; i < localPolygon.Count; i++)
            {
                polyMin = Vector2.Min(polyMin, localPolygon[i]);
                polyMax = Vector2.Max(polyMax, localPolygon[i]);
            }

            int texW = Mathf.Max(1, Mathf.CeilToInt((polyMax.x - polyMin.x) * ppu));
            int texH = Mathf.Max(1, Mathf.CeilToInt((polyMax.y - polyMin.y) * ppu));

            outOriginLocal = polyMin;

            var tex = new Texture2D(texW, texH, TextureFormat.ARGB32, false)
            {
                filterMode = srcTex.filterMode,
                wrapMode   = TextureWrapMode.Clamp,
                name       = "FloorTex"
            };

            var spriteCaches = BuildSpriteCache(sprites, srcTex);
            var pixels       = new Color32[texW * texH];
            var bumpPixels   = new Color32[texW * texH];
            bool hasAnyBump  = false;

            Color32 flatNormal = new Color32(128, 128, 255, 255);

            for (int py = 0; py < texH; py++)
            for (int px = 0; px < texW; px++)
            {
                // 像素中心的局部坐标
                float lx = polyMin.x + (px + 0.5f) / ppu;
                float ly = polyMin.y + (py + 0.5f) / ppu;
                var   lp = new Vector2(lx, ly);

                if (!PointInPolygon(lp, localPolygon))
                {
                    pixels[py * texW + px]     = new Color32(0, 0, 0, 0);
                    bumpPixels[py * texW + px]  = flatNormal;
                    continue;
                }

                TileSample ts = useHexTile
                    ? SampleHexTile(lx, ly, tileSize, seed, spriteCaches, tileRotation)
                    : SampleSimpleTile(lx, ly, tileSize, seed, spriteCaches);

                pixels[py * texW + px] = ts.color;
                if (ts.hasBump)
                {
                    hasAnyBump = true;
                    bumpPixels[py * texW + px] = ts.bump;
                }
                else
                {
                    bumpPixels[py * texW + px] = flatNormal;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);

            if (hasAnyBump)
            {
                bumpTex = new Texture2D(texW, texH, TextureFormat.ARGB32, false, true)
                {
                    filterMode = FilterMode.Point,
                    wrapMode   = TextureWrapMode.Clamp,
                    name       = "FloorBumpTex"
                };
                bumpTex.SetPixels32(bumpPixels);
                bumpTex.Apply(false);
            }

            return tex;
        }

        // ── 采样结果 ──────────────────────────────────────────────────────────

        private struct TileSample
        {
            public Color32 color;
            public Color32 bump;
            public bool    hasBump;
        }

        // ── 简单平铺采样 ────────────────────────────────────────────────────────

        private static TileSample SampleSimpleTile(float lx, float ly, Vector2 tileSize, int seed, SpriteCache[] caches)
        {
            int gi = Mathf.FloorToInt(lx / tileSize.x);
            int gj = Mathf.FloorToInt(ly / tileSize.y);
            int sprIdx = (int)((uint)Hash2D(gi, gj, seed) % (uint)caches.Length);
            SpriteCache sc = caches[sprIdx];
            float tu = Mathf.Repeat(lx / tileSize.x, 1f);
            float tv = Mathf.Repeat(ly / tileSize.y, 1f);
            int sx = Mathf.Clamp(Mathf.FloorToInt(tu * sc.width), 0, sc.width - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(tv * sc.height), 0, sc.height - 1);

            var result = new TileSample { hasBump = sc.hasBump };
            result.color = sc.pixels[sy * sc.width + sx];
            if (sc.hasBump)
                result.bump = sc.bumpPixels[sy * sc.width + sx];
            return result;
        }

        // ── Hex-Tile 采样 ──────────────────────────────────────────────────────

        private static TileSample SampleHexTile(float lx, float ly, Vector2 tileSize, int seed, SpriteCache[] caches, float rotationDeg)
        {
            ApplyLayerRotation(rotationDeg * Mathf.Deg2Rad, ref lx, ref ly);

            float hexW = tileSize.x;
            float hexH = tileSize.y * k_Sqrt3Over2;
            float hexRadius = hexW * 0.5f;

            if (hexRadius < 0.001f) return default;

            int approxRow = Mathf.FloorToInt(ly / hexH);
            float rowOffset = ((approxRow & 1) == 1) ? hexW * 0.5f : 0f;
            int approxCol = Mathf.FloorToInt((lx - rowOffset) / hexW);

            int bestR0 = 0, bestR1 = 0, bestR2 = 0;
            int bestC0 = 0, bestC1 = 0, bestC2 = 0;
            bool has0 = false, has1 = false, has2 = false;
            float dist0 = float.MaxValue, dist1 = float.MaxValue, dist2 = float.MaxValue;

            for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 2; dc++)
            {
                int row = approxRow + dr;
                int col = approxCol + dc;
                Vector2 center = HexCenter(row, col, hexW, hexH);
                float dx = lx - center.x, dy = ly - center.y;
                float distSq = dx * dx + dy * dy;

                if (distSq < dist0)
                {
                    dist2 = dist1; bestR2 = bestR1; bestC2 = bestC1; has2 = has1;
                    dist1 = dist0; bestR1 = bestR0; bestC1 = bestC0; has1 = has0;
                    dist0 = distSq; bestR0 = row; bestC0 = col; has0 = true;
                }
                else if (distSq < dist1)
                {
                    dist2 = dist1; bestR2 = bestR1; bestC2 = bestC1; has2 = has1;
                    dist1 = distSq; bestR1 = row; bestC1 = col; has1 = true;
                }
                else if (distSq < dist2)
                {
                    dist2 = distSq; bestR2 = row; bestC2 = col; has2 = true;
                }
            }

            float r = 0f, g = 0f, b = 0f, a = 0f;
            float br = 0f, bg = 0f, bb = 0f, ba = 0f;
            bool anyBump = false;
            float wSum = 0f;

            bool[]  hasArr = { has0, has1, has2 };
            int[]   rowArr = { bestR0, bestR1, bestR2 };
            int[]   colArr = { bestC0, bestC1, bestC2 };
            float[] dists  = { dist0, dist1, dist2 };

            for (int i = 0; i < 3; i++)
            {
                if (!hasArr[i]) continue;
                int row = rowArr[i];
                int col = colArr[i];

                float dist = Mathf.Sqrt(dists[i]);
                float w = 1f - Mathf.SmoothStep(hexRadius * 0.5f, hexRadius, dist);
                if (w < 0.001f) continue;

                Vector2 center = HexCenter(row, col, hexW, hexH);
                float localX = lx - center.x;
                float localY = ly - center.y;

                // 连续随机旋转 [0°, 360°)
                float angle = (float)(uint)Hash2D(row, col, seed + 9999) / (float)0x7fffffff * 360f * Mathf.Deg2Rad;
                float cosA = Mathf.Cos(-angle), sinA = Mathf.Sin(-angle);
                float rlx = localX * cosA - localY * sinA;
                float rly = localX * sinA + localY * cosA;

                float tu = Mathf.Clamp01(rlx / tileSize.x + 0.5f);
                float tv = Mathf.Clamp01(rly / tileSize.y + 0.5f);

                int sprIdx = (int)((uint)Hash2D(row, col, seed) % (uint)caches.Length);
                SpriteCache sc = caches[sprIdx];

                int sx = Mathf.Clamp(Mathf.FloorToInt(tu * sc.width), 0, sc.width - 1);
                int sy = Mathf.Clamp(Mathf.FloorToInt(tv * sc.height), 0, sc.height - 1);
                Color32 c = sc.pixels[sy * sc.width + sx];

                r += c.r / 255f * w;
                g += c.g / 255f * w;
                b += c.b / 255f * w;
                a += c.a / 255f * w;

                if (sc.hasBump)
                {
                    anyBump = true;
                    Color32 bc = sc.bumpPixels[sy * sc.width + sx];
                    br += bc.r / 255f * w;
                    bg += bc.g / 255f * w;
                    bb += bc.b / 255f * w;
                    ba += bc.a / 255f * w;
                }

                wSum += w;
            }

            if (wSum < 0.001f) return default;
            float invW = 1f / wSum;
            var result = new TileSample { hasBump = anyBump };
            result.color = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(r * invW * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(g * invW * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b * invW * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(a * invW * 255f), 0, 255)
            );
            if (anyBump)
            {
                result.bump = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(br * invW * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(bg * invW * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(bb * invW * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(ba * invW * 255f), 0, 255)
                );
            }
            return result;
        }

        // ── 六边形工具 ──────────────────────────────────────────────────────────

        private static Vector2 HexCenter(int row, int col, float hexW, float hexH)
        {
            float x = col * hexW + ((row & 1) == 1 ? hexW * 0.5f : 0f);
            float y = row * hexH;
            return new Vector2(x, y);
        }

        private static void ApplyLayerRotation(float angleRad, ref float wx, ref float wy)
        {
            if (angleRad < 0.0001f) return;
            float cosA = Mathf.Cos(-angleRad);
            float sinA = Mathf.Sin(-angleRad);
            float rx = wx * cosA - wy * sinA;
            float ry = wx * sinA + wy * cosA;
            wx = rx;
            wy = ry;
        }

        // ── 调色后处理 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 对已烘焙的地板纹理进行调色（原地修改）。
        /// 在 EncodeToPNG 之前调用。仅修改 RGB 通道，保留 Alpha 以保护多边形遮罩。
        /// </summary>
        public static void ApplyColorGrading(
            Texture2D tex,
            float     hueShift,
            float     saturation,
            float     brightness,
            Color     tintColor,
            float     tintBlend)
        {
            if (tex == null) return;

            bool needsHsl  = Mathf.Abs(hueShift) > 0.01f
                          || Mathf.Abs(saturation - 1f) > 0.001f
                          || Mathf.Abs(brightness - 1f) > 0.001f;
            bool needsTint = tintBlend > 0.001f;
            if (!needsHsl && !needsTint) return;

            Color32[] pixels    = tex.GetPixels32();
            float     hueOffset = hueShift / 360f;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                if (c.a == 0) continue;

                float r = c.r / 255f;
                float g = c.g / 255f;
                float b = c.b / 255f;
                float a = c.a / 255f;

                if (needsHsl)
                {
                    var hsl = HslColor.FromRgb(new Color(r, g, b, a));
                    hsl.h = Mathf.Repeat(hsl.h + hueOffset, 1f);
                    hsl.s = Mathf.Clamp01(hsl.s * saturation);
                    hsl.l = Mathf.Clamp01(hsl.l * brightness);
                    Color rgb = hsl.ToRgb();
                    r = rgb.r; g = rgb.g; b = rgb.b;
                }

                if (needsTint)
                {
                    float inv = 1f - tintBlend;
                    r = r * inv + tintColor.r * tintBlend;
                    g = g * inv + tintColor.g * tintBlend;
                    b = b * inv + tintColor.b * tintBlend;
                }

                pixels[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                    c.a
                );
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
        }

        // ── 点在多边形内测试（Ray-casting） ───────────────────────────────────

        private static bool PointInPolygon(Vector2 p, IList<Vector2> poly)
        {
            int  n      = poly.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 vi = poly[i], vj = poly[j];
                if (((vi.y > p.y) != (vj.y > p.y)) &&
                    (p.x < (vj.x - vi.x) * (p.y - vi.y) / (vj.y - vi.y) + vi.x))
                    inside = !inside;
            }
            return inside;
        }

        // ── Sprite 像素缓存 ────────────────────────────────────────────────────

        private struct SpriteCache
        {
            public Color32[] pixels;
            public Color32[] bumpPixels;
            public int       width;
            public int       height;
            public bool      hasBump;
        }

        private static SpriteCache[] BuildSpriteCache(IList<Sprite> sprites, Texture2D srcTex)
        {
            Color32[] atlas  = srcTex.GetPixels32();
            int       atlasW = srcTex.width;

            var caches = new SpriteCache[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite spr = sprites[i];
                Rect   r   = spr.textureRect;
                int    sx  = Mathf.RoundToInt(r.x);
                int    sy  = Mathf.RoundToInt(r.y);
                int    sw  = Mathf.RoundToInt(r.width);
                int    sh  = Mathf.RoundToInt(r.height);

                var pix = new Color32[sw * sh];
                for (int row = 0; row < sh; row++)
                    System.Array.Copy(atlas, (sy + row) * atlasW + sx, pix, row * sw, sw);

                // 提取 _BumpMap secondary texture
                Color32[] bumpPix = null;
                bool hasBump = false;
                Texture2D bumpTex = GetSecondaryBumpMap(spr);
                if (bumpTex != null && bumpTex.isReadable)
                {
                    Color32[] bumpAtlas = bumpTex.GetPixels32();
                    int bumpAtlasW = bumpTex.width;
                    bool isNormalMap = false;
#if UNITY_EDITOR
                    isNormalMap = IsNormalMapTexture(bumpTex);
#endif
                    bumpPix = new Color32[sw * sh];
                    for (int row = 0; row < sh; row++)
                    {
                        for (int col = 0; col < sw; col++)
                        {
                            Color32 src = bumpAtlas[(sy + row) * bumpAtlasW + (sx + col)];
                            if (isNormalMap)
                                src = UnpackNormalDXT5nm(src);
                            bumpPix[row * sw + col] = src;
                        }
                    }
                    hasBump = true;
                }

                caches[i] = new SpriteCache { pixels = pix, bumpPixels = bumpPix, width = sw, height = sh, hasBump = hasBump };
            }
            return caches;
        }

        private static Texture2D GetSecondaryBumpMap(Sprite sprite)
        {
            if (sprite == null) return null;
            int count = sprite.GetSecondaryTextureCount();
            if (count <= 0) return null;
            var secondaries = new SecondarySpriteTexture[count];
            sprite.GetSecondaryTextures(secondaries);
            foreach (var st in secondaries)
                if (st.name == "_BumpMap") return st.texture as Texture2D;
            return null;
        }

#if UNITY_EDITOR
        private static bool IsNormalMapTexture(Texture2D tex)
        {
            if (tex == null) return false;
            string path = UnityEditor.AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return false;
            var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
            if (importer == null) return false;
            return importer.textureType == UnityEditor.TextureImporterType.NormalMap;
        }
#endif

        /// <summary>
        /// 将 DXT5nm 编码的法线贴图像素还原为标准 RGB 法线。
        /// </summary>
        private static Color32 UnpackNormalDXT5nm(Color32 c)
        {
            float nx = c.r / 255f * 2f - 1f;
            float ny = c.a / 255f * 2f - 1f;
            float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
            byte rx = (byte)Mathf.Clamp(Mathf.RoundToInt((nx * 0.5f + 0.5f) * 255f), 0, 255);
            byte ry = (byte)Mathf.Clamp(Mathf.RoundToInt((ny * 0.5f + 0.5f) * 255f), 0, 255);
            byte rz = (byte)Mathf.Clamp(Mathf.RoundToInt((nz * 0.5f + 0.5f) * 255f), 0, 255);
            return new Color32(rx, ry, rz, 255);
        }

        // ── 确定性哈希 ────────────────────────────────────────────────────────

        private static int Hash2D(int x, int y, int seed)
        {
            unchecked
            {
                int h = seed;
                h = h * 374761393 + x;
                h = (h << 13) | (int)((uint)h >> 19);
                h = h * 668265263 + y;
                h = (h << 13) | (int)((uint)h >> 19);
                h ^= h >> 16;
                h *= (int)0x85ebca6b;
                h ^= h >> 13;
                h *= (int)0xc2b2ae35;
                h ^= h >> 16;
                return h & 0x7fffffff;
            }
        }
    }
}
