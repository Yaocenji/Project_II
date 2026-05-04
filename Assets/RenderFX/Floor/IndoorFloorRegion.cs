using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Render
{
    /// <summary>
    /// 室内地板区域描述器（纯数据，编辑器工具）。
    /// 运行时本身不做任何事；实际渲染由编辑器烘焙产生的子物体（SpriteRenderer + RCWBObject）承担。
    /// </summary>
    [DisallowMultipleComponent]
    public class IndoorFloorRegion : MonoBehaviour
    {
        [Tooltip("候选地砖列表（必须共享同一张 Texture，建议用 Sprite Atlas 打包，且 PPU 一致）")]
        public List<Sprite> tileSprites = new List<Sprite>();

        [Tooltip("单块地砖的世界尺寸（米）")]
        public Vector2 tileWorldSize = new Vector2(1f, 1f);

        [Tooltip("使用 Hex-Tile 采样（六边形网格 + 三邻域混合 + 随机旋转），消除平铺重复感")]
        public bool useHexTile = false;

        [Tooltip("整体旋转角度（度），旋转该区域的地砖排列方向")]
        [Range(0f, 360f)]
        public float tileRotation = 0f;

        [Tooltip("随机种子（同种子+同顶点 → 同一套地砖分布）")]
        public int randomSeed = 0;

        [Tooltip("多边形顶点（局部空间）。整体朝向/位置由 transform 控制")]
        public List<Vector2> localVertices = new List<Vector2>();

        [Tooltip("烘焙后的 SpriteRenderer 使用此材质（RCWB 材质）")]
        public Material rcwbMaterial;

        [Header("Color Grading")]
        [Tooltip("HSL 色相偏移（度）[0,360]")]
        [Range(0f, 360f)]
        public float hueShift = 0f;

        [Tooltip("HSL 饱和度缩放 [0,2]，0=灰度，1=原始，2=过饱和")]
        [Range(0f, 2f)]
        public float saturation = 1f;

        [Tooltip("HSL 亮度缩放 [0,2]，0=全黑，1=原始，2=过曝")]
        [Range(0f, 2f)]
        public float brightness = 1f;

        [Tooltip("RGB 着色叠加颜色")]
        public Color tintColor = Color.white;

        [Tooltip("着色混合权重 [0,1]，0=无着色，1=完全着色")]
        [Range(0f, 1f)]
        public float tintBlend = 0f;

        private void OnValidate()
        {
            if (tileWorldSize.x < 0.01f) tileWorldSize.x = 0.01f;
            if (tileWorldSize.y < 0.01f) tileWorldSize.y = 0.01f;
            saturation = Mathf.Clamp(saturation, 0f, 2f);
            brightness = Mathf.Clamp(brightness, 0f, 2f);
            tintBlend  = Mathf.Clamp(tintBlend, 0f, 1f);
        }

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

        public bool ValidateSpritesShareTexture(out Texture sharedTexture, out string warning)
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
    }
}
