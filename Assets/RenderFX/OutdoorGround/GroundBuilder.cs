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
        /// <summary>
        /// 生成场景地面纹理。
        /// </summary>
        /// <param name="sceneAABB">场景包围盒 (minX, minY, maxX, maxY)</param>
        /// <param name="splatLayers">Splatmap 层定义（最多4层，对应 RGBA）</param>
        /// <param name="splatmap">场景级 Splatmap（ARGB32），RGBA = 各层权重</param>
        /// <param name="regions">场景中所有 GroundRegion</param>
        /// <param name="ppu">像素密度</param>
        /// <param name="seed">确定性随机种子</param>
        /// <returns>生成的 Texture2D（ARGB32）</returns>
        public static Texture2D Build(
            Vector4 sceneAABB,
            IList<SplatLayer> splatLayers,
            Texture2D splatmap,
            IList<GroundRegion> regions,
            float ppu,
            int seed)
        {
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

            // 预建 SplatLayer 的 SpriteCache
            int layerCount = Mathf.Min(splatLayers.Count, 4);
            var splatCaches = new SplatLayerCache[layerCount];
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

                splatCaches[li] = new SplatLayerCache
                {
                    spriteCaches = BuildSpriteCache(sl.tileSprites, srcTex),
                    tileWorldSize = sl.tileWorldSize,
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

            // 预建 Region 的 SpriteCache
            var regionCaches = new RegionCache[regions.Count];
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

                regionCaches[ri] = new RegionCache
                {
                    region = reg,
                    spriteCaches = BuildSpriteCache(reg.tileSprites, srcTex),
                    tileWorldSize = reg.tileWorldSize,
                };
            }

            // 逐像素烘焙
            var pixels = new Color32[texW * texH];

            for (int py = 0; py < texH; py++)
            for (int px = 0; px < texW; px++)
            {
                // 像素中心 → 世界坐标
                float wx = minX + (px + 0.5f) / ppu;
                float wy = minY + (py + 0.5f) / ppu;

                // ── Step 1: Splatmap 基础纹理混合 ──
                Color32 baseColor = new Color32(0, 0, 0, 0);

                // UV 映射到 splatmap
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

                        Color32 c = SampleTileLayer(sc, wx, wy, seed + li);
                        r += c.r / 255f * w;
                        g += c.g / 255f * w;
                        b += c.b / 255f * w;
                        a += c.a / 255f * w;
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
                for (int ri = 0; ri < regionCaches.Length; ri++)
                {
                    var rc = regionCaches[ri];
                    if (rc == null) continue;

                    float alpha = rc.region.SampleAlpha(new Vector2(wx, wy));
                    if (alpha < 0.001f) continue;

                    Color32 regionColor = SampleTileLayer(rc, wx, wy, seed + 100 + ri);

                    // Alpha 混合：region over base
                    float ra = regionColor.a / 255f * alpha;
                    float invRa = 1f - ra;
                    float fr = regionColor.r / 255f * ra + result.r / 255f * invRa;
                    float fg = regionColor.g / 255f * ra + result.g / 255f * invRa;
                    float fb = regionColor.b / 255f * ra + result.b / 255f * invRa;
                    float fa = ra + result.a / 255f * invRa;

                    result = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fr * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fg * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fb * 255f), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(fa * 255f), 0, 255)
                    );
                }

                pixels[py * texW + px] = result;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
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

        // ── 地砖层采样 ────────────────────────────────────────────────────

        private static Color32 SampleTileLayer(SplatLayerCache slc, float wx, float wy, int seed)
        {
            float tsX = slc.tileWorldSize.x;
            float tsY = slc.tileWorldSize.y;
            int gi = Mathf.FloorToInt(wx / tsX);
            int gj = Mathf.FloorToInt(wy / tsY);

            int sprIdx = (int)((uint)Hash2D(gi, gj, seed) % (uint)slc.spriteCaches.Length);
            SpriteCache sc = slc.spriteCaches[sprIdx];

            float tu = Mathf.Repeat(wx / tsX, 1f);
            float tv = Mathf.Repeat(wy / tsY, 1f);

            int sx = Mathf.Clamp(Mathf.FloorToInt(tu * sc.width), 0, sc.width - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(tv * sc.height), 0, sc.height - 1);

            return sc.pixels[sy * sc.width + sx];
        }

        private static Color32 SampleTileLayer(RegionCache rc, float wx, float wy, int seed)
        {
            float tsX = rc.tileWorldSize.x;
            float tsY = rc.tileWorldSize.y;
            int gi = Mathf.FloorToInt(wx / tsX);
            int gj = Mathf.FloorToInt(wy / tsY);

            int sprIdx = (int)((uint)Hash2D(gi, gj, seed) % (uint)rc.spriteCaches.Length);
            SpriteCache sc = rc.spriteCaches[sprIdx];

            float tu = Mathf.Repeat(wx / tsX, 1f);
            float tv = Mathf.Repeat(wy / tsY, 1f);

            int sx = Mathf.Clamp(Mathf.FloorToInt(tu * sc.width), 0, sc.width - 1);
            int sy = Mathf.Clamp(Mathf.FloorToInt(tv * sc.height), 0, sc.height - 1);

            return sc.pixels[sy * sc.width + sx];
        }

        // ── Sprite 像素缓存 ──────────────────────────────────────────────

        private struct SpriteCache
        {
            public Color32[] pixels;
            public int       width;
            public int       height;
        }

        private class SplatLayerCache
        {
            public SpriteCache[] spriteCaches;
            public Vector2       tileWorldSize;
        }

        private class RegionCache
        {
            public GroundRegion  region;
            public SpriteCache[] spriteCaches;
            public Vector2       tileWorldSize;
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

                caches[i] = new SpriteCache { pixels = pix, width = sw, height = sh };
            }
            return caches;
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
