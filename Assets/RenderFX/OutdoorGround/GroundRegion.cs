using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// 地面区域（per-polygon）：一个多边形区域绑定一种材质 + 随机地砖列表。
    /// 类似 IndoorFloorRegion 的角色，但属于场景级烘焙系统的一部分。
    /// 区域纹理以 Alpha 混合方式叠加在 Splatmap 基础纹理之上。
    /// </summary>
    [DisallowMultipleComponent]
    public class GroundRegion : MonoBehaviour
    {
        [Tooltip("该区域的地砖 Sprite 列表（必须共享同一 Texture，且 PPU 一致）")]
        public List<Sprite> tileSprites = new List<Sprite>();

        [Tooltip("使用 Hex-Tile 采样（六边形网格 + 三邻域混合 + 随机旋转），消除平铺重复感")]
        public bool useHexTile = false;

        [Tooltip("整体旋转角度（度），旋转该区域的地砖排列方向")]
        [Range(0f, 360f)]
        public float tileRotation = 0f;

        [Tooltip("单块地砖的世界尺寸（米）")]
        public Vector2 tileWorldSize = new Vector2(1f, 1f);

        [Tooltip("多边形顶点（局部空间）。整体朝向/位置由 transform 控制")]
        public List<Vector2> localVertices = new List<Vector2>();

        [Tooltip("边缘羽化宽度（世界空间单位），0 = 硬边")]
        [Min(0f)]
        public float featherWidth = 0.5f;

        /// <summary>
        /// 将本地顶点转换为世界空间顶点。
        /// </summary>
        public Vector2[] GetWorldVertices()
        {
            var result = new Vector2[localVertices.Count];
            for (int i = 0; i < localVertices.Count; i++)
            {
                Vector3 wp = transform.TransformPoint(new Vector3(localVertices[i].x, localVertices[i].y, 0f));
                result[i] = new Vector2(wp.x, wp.y);
            }
            return result;
        }

        /// <summary>
        /// 对世界空间中的点采样：返回该多边形对此点的 Alpha 贡献。
        /// 双向羽化：边界两侧各 featherWidth/2 范围内做线性过渡。
        /// </summary>
        public float SampleAlpha(Vector2 worldPoint)
        {
            Vector2[] verts = GetWorldVertices();
            float signedDist = SignedDistanceToPolygon(worldPoint, verts);

            if (featherWidth <= 0f)
                return signedDist <= 0f ? 1f : 0f;

            float halfFeather = featherWidth * 0.5f;
            if (signedDist <= -halfFeather) return 1f;
            if (signedDist >= halfFeather)  return 0f;

            return Mathf.Clamp01(0.5f - signedDist / featherWidth);
        }

        /// <summary>
        /// 校验所有 Sprite 是否共享同一 Texture 且 PPU 一致。
        /// </summary>
        public bool ValidateSprites(out Texture sharedTexture, out string warning)
        {
            sharedTexture = null;
            warning = null;
            if (tileSprites == null || tileSprites.Count == 0)
            {
                warning = "tileSprites 为空";
                return false;
            }

            float ppu = -1f;
            for (int i = 0; i < tileSprites.Count; i++)
            {
                if (tileSprites[i] == null) { warning = $"tileSprites[{i}] 为 null"; return false; }
                Texture tex = tileSprites[i].texture;
                if (sharedTexture == null)
                {
                    sharedTexture = tex;
                    ppu = tileSprites[i].pixelsPerUnit;
                }
                else
                {
                    if (sharedTexture != tex)
                    {
                        warning = $"tileSprites[{i}] 与其他 Sprite 不在同一 Texture（请用 Sprite Atlas 打包）";
                        return false;
                    }
                    if (!Mathf.Approximately(tileSprites[i].pixelsPerUnit, ppu))
                    {
                        warning = $"tileSprites[{i}] 的 PPU ({tileSprites[i].pixelsPerUnit}) 与其他 Sprite ({ppu}) 不一致";
                        return false;
                    }
                }
            }
            return true;
        }

        private static float SignedDistanceToPolygon(Vector2 p, Vector2[] verts)
        {
            int n = verts.Length;
            if (n < 3) return float.MaxValue;

            float minDist = float.MaxValue;
            bool inside = false;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = verts[j];
                Vector2 b = verts[i];

                if ((verts[i].y > p.y) != (verts[j].y > p.y) &&
                    p.x < (verts[j].x - verts[i].x) * (p.y - verts[i].y) / (verts[j].y - verts[i].y) + verts[i].x)
                    inside = !inside;

                float d = DistancePointToSegment(p, a, b);
                if (d < minDist) minDist = d;
            }

            return inside ? -minDist : minDist;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + t * ab);
        }

        private void OnValidate()
        {
            if (tileWorldSize.x < 0.01f) tileWorldSize.x = 0.01f;
            if (tileWorldSize.y < 0.01f) tileWorldSize.y = 0.01f;
        }
    }
}
