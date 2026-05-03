using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// 场景级地面烘焙工具。
    /// 将 Splatmap 基础纹理 + GroundRegion 多边形区域叠加 → 输出单张覆盖场景 AABB 的 Texture2D。
    /// </summary>
    public static class GroundBuilder
    {
        private const float k_Sqrt3Over2 = 0.8660254f;

        /// <summary>
        /// 生成场景地面纹理（Albedo + BumpMap）。
        /// </summary>
        public static Texture2D Build(
            Vector4 sceneAABB,
            IList<SplatLayer> splatLayers,
            Texture2D splatmap,
            IList<GroundRegion> regions,
            float ppu,
            int seed,
            out Texture2D bumpTex)
        {
            bumpTex = null;
            float minX = sceneAABB.x, minY = sceneAABB.y;
            float maxX = sceneAABB.z, maxY = sceneAABB.w;
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;

            if (rangeX < 0.001f || rangeY < 0.001f) return null;

            int texW = Mathf.Max(1, Mathf.CeilToInt(rangeX * ppu));
            int texH = Mathf.Max(1, Mathf.CeilToInt(rangeY * ppu));

            var tex = new Texture2D(texW, texH, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
                name       = "GroundTex"
            };

            // 预建 SplatLayer 的 TileCache
            int layerCount = Mathf.Min(splatLayers.Count, 4);
            var splatCaches = new TileCache[layerCount];
            for (int li = 0; li < layerCount; li++)
            {
                var sl = splatLayers[li];
                if (sl.tileSprites == null || sl.tileSprites.Count == 0)
                {
                    splatCaches[li] = null;
                    continue;
                }

                Texture2D srcTex = sl.tileSprites[0].texture;
                if (srcTex == null)
                {
                    splatCaches[li] = null;
                    continue;
                }

                splatCaches[li] = new TileCache
                {
                    spriteCaches = BuildSpriteCache(sl.tileSprites, srcTex),
                    tileWorldSize = sl.tileWorldSize,
                    useHexTile = sl.useHexTile,
                    tileRotationRad = sl.tileRotation * Mathf.Deg2Rad,
                };
            }

            // 预读 Splatmap 像素
            SplatmapData smd = default;
            if (splatmap != null && splatmap.isReadable)
            {
                smd = new SplatmapData
                {
                    pixels = splatmap.GetPixels32(),
                    width = splatmap.width,
                    height = splatmap.height,
                };
            }

            // 预建 Region 的 TileCache
            var regionCaches = new TileCache[regions.Count];
            for (int ri = 0; ri < regions.Count; ri++)
            {
                var reg = regions[ri];
                if (reg.tileSprites == null || reg.tileSprites.Count == 0)
                {
                    regionCaches[ri] = null;
                    continue;
                }

                Texture2D srcTex = reg.tileSprites[0].texture;
                if (srcTex == null)
                {
                    regionCaches[ri] = null;
                    continue;
                }

                regionCaches[ri] = new TileCache
                {
                    spriteCaches = BuildSpriteCache(reg.tileSprites, srcTex),
                    tileWorldSize = reg.tileWorldSize,
                    useHexTile = reg.useHexTile,
                    tileRotationRad = reg.tileRotation * Mathf.Deg2Rad,
                };
            }

            // 逐像素烘焙
            var pixels = new Color32[texW * texH];
            var bumpPixels = new Color32[texW * texH];
            bool hasAnyBump = false;

            // 默认法线 (0.5, 0.5, 1.0, 1) — Unity 标准平坦法线
            Color32 flatNormal = new Color32(128, 128, 255, 255);

            for (int py = 0; py < texH; py++)
            for (int px = 0; px < texW; px++)
            {
                float wx = minX + (px + 0.5f) / ppu;
                float wy = minY + (py + 0.5f) / ppu;

                // ── Step 1: Splatmap 基础纹理混合 ──
                Color32 baseColor = new Color32(0, 0, 0, 0);
                float baseBr = 0f, baseBg = 0f, baseBb = 0f, baseBa = 0f;
                bool baseHasBump = false;

                float u = (wx - minX) / rangeX;
                float v = (wy - minY) / rangeY;

                float wR = 0f, wG = 0f, wB = 0f, wA = 0f;
                SampleSplatmap(ref smd, u, v, out wR, out wG, out wB, out wA);

                float weightSum = wR + wG + wB + wA;
                if (weightSum > 0.001f)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    float[] weights = { wR, wG, wB, wA };
                    for (int li = 0; li < layerCount; li++)
                    {
                        var sc = splatCaches[li];
                        if (sc == null) continue;
                        float w = weights[li] / weightSum;
                        if (w < 0.001f) continue;

                        TileSample ts = SampleTileLayer(sc, wx, wy, seed + li);
                        r += ts.color.r / 255f * w;
                        g += ts.color.g / 255f * w;
                        b += ts.color.b / 255f * w;
                        a += ts.color.a / 255f * w;

                        if (ts.hasBump)
                        {
                            baseHasBump = true;
                            baseBr += ts.bump.r / 255f * w;
                            baseBg += ts.bump.g / 255f * w;
                            baseBb += ts.bump.b / 255f * w;
                            baseBa += ts.bump.a / 255f * w;
                        }
                    }
                    baseColor = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255)
                    );
                }

                // ── Step 2: GroundRegion 多边形区域叠加（Alpha 混合） ──
                Color32 result = baseColor;
                float resultBr = baseBr, resultBg = baseBg, resultBb = baseBb, resultBa = baseBa;
                bool resultHasBump = baseHasBump;

                for (int ri = 0; ri < regionCaches.Length; ri++)
                {
                    var rc = regionCaches[ri];
                    if (rc == null) continue;

                    float alpha = regions[ri].SampleAlpha(new Vector2(wx, wy));
                    if (alpha < 0.001f) continue;

                    TileSample ts = SampleTileLayer(rc, wx, wy, seed + 100 + ri);

                    float ra = ts.color.a / 255f * alpha;
                    float invRa = 1f - ra;
                    float fr = ts.color.r / 255f * ra + result.r / 255f * invRa;
                    float fg = ts.color.g / 255f * ra + result.g / 255f * invRa;
                    float fb = ts.color.b / 255f * ra + result.b / 255f * invRa;
                    float fa = ra + result.a / 255f * invRa;

                    result = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fr * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fg * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fb * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fa * 255f), 0, 255)
                    );

                    if (ts.hasBump)
                    {
                        resultHasBump = true;
                        float bra = ra, binvRa = invRa;
                        resultBr = ts.bump.r / 255f * bra + resultBr * binvRa;
                        resultBg = ts.bump.g / 255f * bra + resultBg * binvRa;
                        resultBb = ts.bump.b / 255f * bra + resultBb * binvRa;
                        resultBa = ts.bump.a / 255f * bra + resultBa * binvRa;
                    }
                }

                pixels[py * texW + px] = result;

                if (resultHasBump)
                {
                    hasAnyBump = true;
                    bumpPixels[py * texW + px] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(resultBr * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(resultBg * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(resultBb * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(resultBa * 255f), 0, 255)
                    );
                }
                else
                {
                    bumpPixels[py * texW + px] = flatNormal;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);

            // 生成法线纹理
            if (hasAnyBump)
            {
                bumpTex = new Texture2D(texW, texH, TextureFormat.ARGB32, false, true)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "GroundBumpTex"
                };
                bumpTex.SetPixels32(bumpPixels);
                bumpTex.Apply(false);
            }

            return tex;
        }

        // ── Splatmap 采样 ──────────────────────────────────────────────────

        private struct SplatmapData
        {
            public Color32[] pixels;
            public int width;
            public int height;
        }

        private static void SampleSplatmap(ref SplatmapData smd, float u, float v,
            out float r, out float g, out float b, out float a)
        {
            r = g = b = a = 0f;
            if (smd.pixels == null || smd.width <= 0 || smd.height <= 0) return;

            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);

            int px = Mathf.Clamp(Mathf.RoundToInt(u * (smd.width - 1)), 0, smd.width - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(v * (smd.height - 1)), 0, smd.height - 1);

            Color32 c = smd.pixels[py * smd.width + px];
            r = c.r / 255f;
            g = c.g / 255f;
            b = c.b / 255f;
            a = c.a / 255f;
        }

        // ── 地砖层采样（统一入口） ────────────────────────────────────────

        private struct TileSample
        {
            public Color32 color;
            public Color32 bump;
            public bool hasBump;
        }

        private static TileSample SampleTileLayer(TileCache tc, float wx, float wy, int seed)
        {
            return tc.useHexTile
                ? SampleTileLayerHex(tc, wx, wy, seed)
                : SampleTileLayerSimple(tc, wx, wy, seed);
        }

        // ── 简单平铺采样（原始逻辑） ──────────────────────────────────────

        private static TileSample SampleTileLayerSimple(TileCache tc, float wx, float wy, int seed)
        {
            ApplyLayerRotation(tc.tileRotationRad, ref wx, ref wy);

            float tsX = tc.tileWorldSize.x;
            float tsY = tc.tileWorldSize.y;
            int gi = Mathf.FloorToInt(wx / tsX);
            int gj = Mathf.FloorToInt(wy / tsY);

            int sprIdx = (int)((uint)Hash2D(gi, gj, seed) % (uint)tc.spriteCaches.Length);
            SpriteCache sc = tc.spriteCaches[sprIdx];

            float tu = Mathf.Repeat(wx / tsX, 1f);
            float tv = Mathf.Repeat(wy / tsY, 1f);

            int sx = Mathf.Clamp(Mathf.FloorToInt(tu * sc.width), 0, sc.width - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(tv * sc.height), 0, sc.height - 1);

            var result = new TileSample { hasBump = sc.hasBump };
            result.color = sc.pixels[sy * sc.width + sx];
            if (sc.hasBump)
                result.bump = sc.bumpPixels[sy * sc.width + sx];
            return result;
        }

        // ── Hex-Tile 采样 ─────────────────────────────────────────────────

        private static TileSample SampleTileLayerHex(TileCache tc, float wx, float wy, int seed)
        {
            ApplyLayerRotation(tc.tileRotationRad, ref wx, ref wy);

            float hexW = tc.tileWorldSize.x;
            float hexH = tc.tileWorldSize.y * k_Sqrt3Over2;
            float hexRadius = hexW * 0.5f;

            if (hexRadius < 0.001f) return default;

            int approxRow = Mathf.FloorToInt(wy / hexH);
            float rowOffset = ((approxRow & 1) == 1) ? hexW * 0.5f : 0f;
            int approxCol = Mathf.FloorToInt((wx - rowOffset) / hexW);

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
                float dx = wx - center.x, dy = wy - center.y;
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

            bool[] hasArr = { has0, has1, has2 };
            int[] rowArr = { bestR0, bestR1, bestR2 };
            int[] colArr = { bestC0, bestC1, bestC2 };
            float[] dists = { dist0, dist1, dist2 };

            for (int i = 0; i < 3; i++)
            {
                if (!hasArr[i]) continue;
                int row = rowArr[i];
                int col = colArr[i];

                float dist = Mathf.Sqrt(dists[i]);
                float w = 1f - Mathf.SmoothStep(hexRadius * 0.5f, hexRadius, dist);
                if (w < 0.001f) continue;

                Vector2 center = HexCenter(row, col, hexW, hexH);
                float lx = wx - center.x;
                float ly = wy - center.y;

                int rotIdx = (int)((uint)Hash2D(row, col, seed + 9999) % 12u);
                float angle = rotIdx * 30f * Mathf.Deg2Rad;
                float cosA = Mathf.Cos(-angle), sinA = Mathf.Sin(-angle);
                float rlx = lx * cosA - ly * sinA;
                float rly = lx * sinA + ly * cosA;

                float tu = Mathf.Clamp01(rlx / tc.tileWorldSize.x + 0.5f);
                float tv = Mathf.Clamp01(rly / tc.tileWorldSize.y + 0.5f);

                int sprIdx = (int)((uint)Hash2D(row, col, seed) % (uint)tc.spriteCaches.Length);
                SpriteCache sc = tc.spriteCaches[sprIdx];

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

        // ── 六边形工具 ────────────────────────────────────────────────────

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

        // ── 缓存结构 ──────────────────────────────────────────────────────

        private struct SpriteCache
        {
            public Color32[] pixels;
            public Color32[] bumpPixels;
            public int       width;
            public int       height;
            public bool      hasBump;
        }

        private class TileCache
        {
            public SpriteCache[] spriteCaches;
            public Vector2       tileWorldSize;
            public bool          useHexTile;
            public float         tileRotationRad;
        }

        private static SpriteCache[] BuildSpriteCache(IList<Sprite> sprites, Texture2D srcTex)
        {
            Color32[] atlas = srcTex.GetPixels32();
            int atlasW = srcTex.width;

            var caches = new SpriteCache[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                Sprite spr = sprites[i];
                Rect r = spr.textureRect;
                int sx = Mathf.RoundToInt(r.x);
                int sy = Mathf.RoundToInt(r.y);
                int sw = Mathf.RoundToInt(r.width);
                int sh = Mathf.RoundToInt(r.height);

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
        /// DXT5nm: R=X, A=Y, Z 需从 X/Y 重建。
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

        // ── 确定性哈希 ────────────────────────────────────────────────────

        public static int Hash2D(int x, int y, int seed)
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
