#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ProjectII.Render
{
    public class BSplineOffsetSweepWindow : EditorWindow
    {
        private BSplineComponent m_Spline;
        private float m_Offset = 1f;
        private int m_SampleSteps = 32;
        private float m_PPU = 100f;
        private FilterMode m_FilterMode = FilterMode.Point;
        private DefaultAsset m_SaveFolder;
        private string m_Status;

        [MenuItem("Tools/ProjectII/B-Spline Offset Sweep")]
        private static void Open()
        {
            GetWindow<BSplineOffsetSweepWindow>("B-Spline Offset Sweep");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("偏移扫掠生成器", EditorStyles.boldLabel);

            m_Spline = (BSplineComponent)EditorGUILayout.ObjectField(
                "B-Spline 曲线", m_Spline, typeof(BSplineComponent), true);

            m_Offset = EditorGUILayout.FloatField("偏移距离", m_Offset);
            m_SampleSteps = EditorGUILayout.IntField("采样步数", m_SampleSteps);
            m_SampleSteps = Mathf.Max(2, m_SampleSteps);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("纹理", EditorStyles.boldLabel);

            m_PPU = EditorGUILayout.FloatField("PPU", m_PPU);
            m_PPU = Mathf.Max(1f, m_PPU);
            m_FilterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", m_FilterMode);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);

            m_SaveFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "保存目录", m_SaveFolder, typeof(DefaultAsset), false);

            EditorGUILayout.Space(6);

            EditorGUI.BeginDisabledGroup(m_Spline == null || m_SaveFolder == null);
            if (GUILayout.Button("生成偏移扫掠"))
                Generate();
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(m_Status))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(m_Status, m_Status.StartsWith("Error") ? MessageType.Error : MessageType.Info);
            }
        }

        private void Generate()
        {
            if (m_Spline == null)
            {
                m_Status = "Error: 未指定 B-Spline 曲线";
                return;
            }

            if (m_SaveFolder == null)
            {
                m_Status = "Error: 未指定保存目录";
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(m_SaveFolder);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                m_Status = "Error: 保存目录无效";
                return;
            }

            int minPts = m_Spline.Degree + 1;
            if (m_Spline.ControlPoints.Count < minPts)
            {
                m_Status = $"Error: 至少需要 {minPts} 个控制点";
                return;
            }

            Vector2[] polyline = m_Spline.SampleCurve(m_SampleSteps);
            if (polyline.Length < 2)
            {
                m_Status = "Error: 采样点不足";
                return;
            }

            // 闭合曲线：SampleCurve 末尾重复首点，去掉以避免退化
            if (m_Spline.Closed && polyline.Length > 1 && Vector2.Distance(polyline[0], polyline[polyline.Length - 1]) < 1e-6f)
            {
                var trimmed = new Vector2[polyline.Length - 1];
                System.Array.Copy(polyline, trimmed, trimmed.Length);
                polyline = trimmed;
            }

            Vector2[] vertexNormals = ComputeVertexNormals(polyline, m_Spline.Closed);

            int N = polyline.Length;
            List<Vector2> polygon;

            if (m_Spline.Closed)
            {
                // 闭合曲线：Polygon = 偏移折线闭合环（逆时针）
                polygon = new List<Vector2>(N);
                for (int i = 0; i < N; i++)
                    polygon.Add(polyline[i] + vertexNormals[i] * m_Offset);
            }
            else
            {
                // 开放曲线：Polygon = 偏移折线正向 + 原折线反向（逆时针条带）
                polygon = new List<Vector2>(N * 2);
                for (int i = 0; i < N; i++)
                    polygon.Add(polyline[i] + vertexNormals[i] * m_Offset);
                for (int i = N - 1; i >= 0; i--)
                    polygon.Add(polyline[i]);
            }

            // 原折线世界坐标（用于距离渐变）
            var worldOrigPolyline = new Vector2[N];
            for (int i = 0; i < N; i++)
            {
                Vector3 wp = m_Spline.transform.TransformPoint(new Vector3(polyline[i].x, polyline[i].y, 0f));
                worldOrigPolyline[i] = new Vector2(wp.x, wp.y);
            }

            // 转换到世界空间
            var worldPoly = new Vector2[polygon.Count];
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 wp = m_Spline.transform.TransformPoint(new Vector3(polygon[i].x, polygon[i].y, 0f));
                worldPoly[i] = new Vector2(wp.x, wp.y);
            }

            // AABB
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < worldPoly.Length; i++)
            {
                if (worldPoly[i].x < minX) minX = worldPoly[i].x;
                if (worldPoly[i].y < minY) minY = worldPoly[i].y;
                if (worldPoly[i].x > maxX) maxX = worldPoly[i].x;
                if (worldPoly[i].y > maxY) maxY = worldPoly[i].y;
            }

            float rangeX = maxX - minX;
            float rangeY = maxY - minY;
            if (rangeX < 0.001f || rangeY < 0.001f)
            {
                m_Status = "Error: 多边形太小";
                return;
            }

            int texW = Mathf.Max(1, Mathf.CeilToInt(rangeX * m_PPU));
            int texH = Mathf.Max(1, Mathf.CeilToInt(rangeY * m_PPU));

            // 逐像素：Polygon 内填充黑色，alpha 按距原折线距离从 1 渐变到 0
            float invOffset = m_Offset > 1e-6f ? 1f / m_Offset : 0f;
            var pixels = new Color32[texW * texH];
            for (int py = 0; py < texH; py++)
            for (int px = 0; px < texW; px++)
            {
                float wx = minX + (px + 0.5f) / m_PPU;
                float wy = minY + (py + 0.5f) / m_PPU;
                if (!IsPointInPolygon(wx, wy, worldPoly))
                {
                    pixels[py * texW + px] = new Color32(0, 0, 0, 0);
                    continue;
                }
                float dist = DistanceToPolyline(wx, wy, worldOrigPolyline, m_Spline.Closed);
                float alpha = Mathf.Clamp01(1f - dist * invOffset);
                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[py * texW + px] = new Color32(0, 0, 0, a);
            }

            // 创建纹理并保存 PNG
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);

            string assetName = m_Spline.name + "_Sweep";
            string pngPath = Path.Combine(folderPath, assetName + ".png");
            pngPath = AssetDatabase.GenerateUniqueAssetPath(pngPath);
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            DestroyImmediate(tex);

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            // 设置纹理导入参数
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = m_PPU;
                importer.filterMode = m_FilterMode;
                importer.spritePivot = new Vector2(0f, 0f);
                importer.SaveAndReimport();
            }

            // 加载 Sprite
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(pngPath);
            Sprite sprite = sprites.Length > 0 ? sprites[0] as Sprite : null;
            if (sprite == null)
            {
                m_Status = "Error: 无法加载生成的 Sprite";
                return;
            }

            // 创建 GameObject：SpriteRenderer + 定位到 AABB 左下角
            GameObject go = new GameObject(assetName);
            go.transform.position = new Vector3(minX, minY, m_Spline.transform.position.z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            Undo.RegisterCreatedObjectUndo(go, "Create Offset Sweep");

            Selection.activeGameObject = go;
            SceneView.FrameLastActiveSceneView();

            m_Status = $"已生成 {texW}x{texH} 纹理，{polygon.Count} 个顶点的扫掠多边形";
        }

        private static bool IsPointInPolygon(float px, float py, Vector2[] poly)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float yi = poly[i].y, yj = poly[j].y;
                float xi = poly[i].x, xj = poly[j].x;
                if ((yi > py) != (yj > py) &&
                    px < (xj - xi) * (py - yi) / (yj - yi) + xi)
                    inside = !inside;
            }
            return inside;
        }

        private static float DistanceToPolyline(float px, float py, Vector2[] pts, bool closed)
        {
            float minDist = float.MaxValue;
            int segCount = closed ? pts.Length : pts.Length - 1;
            for (int i = 0; i < segCount; i++)
            {
                int j = closed ? (i + 1) % pts.Length : i + 1;
                float d = DistancePointToSegment(new Vector2(px, py), pts[i], pts[j]);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float sq = ab.sqrMagnitude;
            if (sq < 1e-8f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sq);
            return Vector2.Distance(p, a + t * ab);
        }

        private static Vector2[] ComputeVertexNormals(Vector2[] polyline, bool closed)
        {
            int N = polyline.Length;
            var normals = new Vector2[N];

            int segCount = closed ? N : N - 1;
            var segNormals = new Vector2[segCount];
            var segLengths = new float[segCount];

            for (int i = 0; i < segCount; i++)
            {
                int j = closed ? (i + 1) % N : i + 1;
                Vector2 dir = polyline[j] - polyline[i];
                segLengths[i] = dir.magnitude;
                if (segLengths[i] < 1e-8f)
                {
                    segNormals[i] = Vector2.right;
                }
                else
                {
                    dir /= segLengths[i];
                    segNormals[i] = new Vector2(dir.y, -dir.x);
                }
            }

            if (closed)
            {
                for (int i = 0; i < N; i++)
                {
                    int leftSeg = (i - 1 + N) % N;
                    int rightSeg = i;
                    float wl = segLengths[leftSeg];
                    float wr = segLengths[rightSeg];
                    float total = wl + wr;
                    if (total < 1e-8f)
                        normals[i] = segNormals[rightSeg];
                    else
                        normals[i] = ((segNormals[leftSeg] * wl + segNormals[rightSeg] * wr) / total).normalized;
                }
            }
            else
            {
                normals[0] = segNormals[0];

                for (int i = 1; i < N - 1; i++)
                {
                    int leftSeg = i - 1;
                    int rightSeg = i;
                    float wl = segLengths[leftSeg];
                    float wr = segLengths[rightSeg];
                    float total = wl + wr;
                    if (total < 1e-8f)
                        normals[i] = segNormals[rightSeg];
                    else
                        normals[i] = ((segNormals[leftSeg] * wl + segNormals[rightSeg] * wr) / total).normalized;
                }

                normals[N - 1] = segNormals[segCount - 1];
            }

            return normals;
        }
    }
}
#endif
