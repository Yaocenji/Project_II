using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// 前景纹理缓存的查询键。
    /// 由精灵 ID、法线图 ID、模糊半径、全分辨率缩放和 SDF 分辨率缩放五元组唯一标识一套纹理资产。
    /// </summary>
    internal readonly struct ForegroundTextureCacheKey
        : System.IEquatable<ForegroundTextureCacheKey>
    {
        public readonly int spriteID;
        public readonly int normalMapID;     // 无法线图时为 0
        public readonly int blurRadius;      // 整数模糊半径
        public readonly int fullResScale100; // fullResScale × 100，取整
        public readonly int sdfResScale100;  // sdfResolutionScale × 100，取整

        public ForegroundTextureCacheKey(
            int spriteID, int normalMapID, int blurRadius,
            float fullResScale, float sdfResolutionScale)
        {
            this.spriteID        = spriteID;
            this.normalMapID     = normalMapID;
            this.blurRadius      = blurRadius;
            this.fullResScale100 = Mathf.RoundToInt(fullResScale * 100f);
            this.sdfResScale100  = Mathf.RoundToInt(sdfResolutionScale * 100f);
        }

        public bool Equals(ForegroundTextureCacheKey o)
            => spriteID == o.spriteID
            && normalMapID == o.normalMapID
            && blurRadius == o.blurRadius
            && fullResScale100 == o.fullResScale100
            && sdfResScale100 == o.sdfResScale100;

        public override bool Equals(object obj)
            => obj is ForegroundTextureCacheKey k && Equals(k);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = spriteID;
                h = h * 397 ^ normalMapID;
                h = h * 397 ^ blurRadius;
                h = h * 397 ^ fullResScale100;
                h = h * 397 ^ sdfResScale100;
                return h;
            }
        }
    }

    /// <summary>
    /// 一套缓存的纹理资产（blur 颜色图、blur 法线图、SDF 图、blur 精灵）加引用计数。
    /// blurRadius = 0 时 blur 相关字段均为 null，SDF 仍有效。
    /// </summary>
    internal class ForegroundTextureCacheEntry
    {
        public Texture2D blurTexture;
        public Texture2D blurNormalTexture;
        public Texture2D sdfTexture;
        public Sprite    blurSprite;
        public int       refCount;
    }

    /// <summary>
    /// 前景纹理共享缓存（静态，全局生命周期）。
    /// 多个使用相同精灵和相同效果参数的 ForegroundObject 共享同一套运行时纹理，避免重复生成。
    /// 基于引用计数管理生命周期，引用归零时自动销毁 GPU 资产。
    /// </summary>
    internal static class ForegroundTextureCache
    {
        private static readonly Dictionary<ForegroundTextureCacheKey, ForegroundTextureCacheEntry>
            s_Cache = new Dictionary<ForegroundTextureCacheKey, ForegroundTextureCacheEntry>();

        /// <summary>
        /// 尝试从缓存获取纹理资产。命中时引用计数 +1 并返回 true。
        /// </summary>
        public static bool TryAcquire(ForegroundTextureCacheKey key,
                                       out ForegroundTextureCacheEntry entry)
        {
            if (s_Cache.TryGetValue(key, out entry))
            {
                entry.refCount++;
                return true;
            }
            entry = null;
            return false;
        }

        /// <summary>
        /// 将新生成的纹理资产注册进缓存（初始引用计数 = 1）。
        /// </summary>
        public static void Register(ForegroundTextureCacheKey key, ForegroundTextureCacheEntry entry)
        {
            entry.refCount = 1;
            s_Cache[key] = entry;
        }

        /// <summary>
        /// 释放一次引用。引用计数归零时销毁所有 GPU 资产并从缓存移除。
        /// </summary>
        public static void Release(ForegroundTextureCacheKey key)
        {
            if (!s_Cache.TryGetValue(key, out var entry)) return;
            entry.refCount--;
            if (entry.refCount > 0) return;

            DestroyEntry(entry);
            s_Cache.Remove(key);
        }

        /// <summary>
        /// 强制清空所有缓存并销毁全部资产（由 ForegroundManager.OnDestroy 调用）。
        /// </summary>
        public static void Clear()
        {
            foreach (var entry in s_Cache.Values)
                DestroyEntry(entry);
            s_Cache.Clear();
        }

        /// <summary>当前缓存条目数（用于调试）</summary>
        public static int Count => s_Cache.Count;

        private static void DestroyEntry(ForegroundTextureCacheEntry entry)
        {
            if (entry.blurSprite != null)        Object.Destroy(entry.blurSprite);
            if (entry.blurTexture != null)       Object.Destroy(entry.blurTexture);
            if (entry.blurNormalTexture != null) Object.Destroy(entry.blurNormalTexture);
            if (entry.sdfTexture != null)        Object.Destroy(entry.sdfTexture);
        }
    }
}
