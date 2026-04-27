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
        /// <summary>
        /// 生成地板纹理（局部空间）。
        /// </summary>
        /// <param name="localPolygon">多边形顶点（局部空间）</param>
        /// <param name="sprites">地砖 Sprite 列表，必须共享 Texture 且 PPU 一致</param>
        /// <param name="tileSize">单块地砖的局部尺寸</param>
        /// <param name="seed">随机种子</param>
        /// <param name="outOriginLocal">输出：纹理左下角的局部空间坐标（用于定位子物体 localPosition）</param>
        /// <returns>生成的 Texture2D（ARGB32，未压缩）</returns>
        public static Texture2D Build(
            IList<Vector2> localPolygon,
            IList<Sprite>  sprites,
            Vector2        tileSize,
            int            seed,
            out Vector2    outOriginLocal)
        {
            outOriginLocal = Vector2.zero;
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

            for (int py = 0; py < texH; py++)
            for (int px = 0; px < texW; px++)
            {
                // 像素中心的局部坐标
                float lx = polyMin.x + (px + 0.5f) / ppu;
                float ly = polyMin.y + (py + 0.5f) / ppu;
                var   lp = new Vector2(lx, ly);

                if (!PointInPolygon(lp, localPolygon))
                {
                    pixels[py * texW + px] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // 格子索引（局部空间，随父物体旋转走）
                int gi = Mathf.FloorToInt(lx / tileSize.x);
                int gj = Mathf.FloorToInt(ly / tileSize.y);

                int        sprIdx = (int)((uint)Hash2D(gi, gj, seed) % (uint)sprites.Count);
                SpriteCache sc    = spriteCaches[sprIdx];

                // 格内归一化坐标 [0,1)
                float tu = Mathf.Repeat(lx / tileSize.x, 1f);
                float tv = Mathf.Repeat(ly / tileSize.y, 1f);

                int sx = Mathf.Clamp(Mathf.FloorToInt(tu * sc.width),  0, sc.width  - 1);
                int sy = Mathf.Clamp(Mathf.FloorToInt(tv * sc.height), 0, sc.height - 1);

                pixels[py * texW + px] = sc.pixels[sy * sc.width + sx];
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
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
            public int       width;
            public int       height;
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

                caches[i] = new SpriteCache { pixels = pix, width = sw, height = sh };
            }
            return caches;
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
