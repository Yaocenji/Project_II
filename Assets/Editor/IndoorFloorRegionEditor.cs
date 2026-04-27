using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RadianceCascadesWorldBVH;

namespace ProjectII.Render
{
    [CustomEditor(typeof(IndoorFloorRegion))]
    public class IndoorFloorRegionEditor : Editor
    {
        private IndoorFloorRegion m_Target;
        private int  m_SelectedVertex = -1;
        private bool m_AddMode = false;

        private static readonly Color k_PolygonColor  = new Color(0.2f, 0.8f, 1f, 0.20f);
        private static readonly Color k_OutlineColor  = new Color(0.2f, 0.8f, 1f, 0.9f);
        private static readonly Color k_VertexColor   = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color k_SelectedColor = new Color(1f, 0.6f, 0.1f, 1f);
        private static readonly Color k_AddModeColor  = new Color(0.2f, 1f, 0.4f, 0.9f);

        // 烘焙产生的子物体固定名称
        private const string k_BakedChildName = "_FloorBaked";

        private void OnEnable()
        {
            m_Target = (IndoorFloorRegion)target;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("tileSprites"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tileWorldSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("randomSeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rcwbMaterial"));

            // 校验
            if (m_Target.tileSprites != null && m_Target.tileSprites.Count > 0)
            {
                if (!m_Target.ValidateSpritesShareTexture(out _, out string warn))
                    EditorGUILayout.HelpBox(warn, MessageType.Warning);
            }

            EditorGUILayout.Space(6);

            // 顶点编辑工具栏
            EditorGUILayout.BeginHorizontal();
            GUI.color = m_AddMode ? k_AddModeColor : Color.white;
            bool newAddMode = GUILayout.Toggle(m_AddMode, "添加顶点模式", EditorStyles.miniButton);
            if (newAddMode != m_AddMode)
            {
                m_AddMode = newAddMode;
                if (m_AddMode) m_SelectedVertex = -1;
            }
            GUI.color = Color.white;
            if (GUILayout.Button("删除选中顶点", EditorStyles.miniButton))
                DeleteSelectedVertex();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 顶点列表
            EditorGUILayout.LabelField("顶点", EditorStyles.boldLabel);
            var vertsProp = serializedObject.FindProperty("localVertices");
            for (int i = 0; i < vertsProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUI.color = (i == m_SelectedVertex) ? k_SelectedColor : Color.white;
                if (GUILayout.Button($"#{i}", GUILayout.Width(30)))
                    m_SelectedVertex = (m_SelectedVertex == i) ? -1 : i;
                GUI.color = Color.white;
                EditorGUILayout.PropertyField(vertsProp.GetArrayElementAtIndex(i), GUIContent.none);
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("清空所有顶点"))
            {
                Undo.RecordObject(m_Target, "Clear Floor Vertices");
                m_Target.localVertices.Clear();
                m_SelectedVertex = -1;
                EditorUtility.SetDirty(m_Target);
            }

            EditorGUILayout.Space(8);

            // 烘焙按钮
            GUI.color = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("烘焙地板 Sprite", GUILayout.Height(32)))
                BakeFloor();
            GUI.color = Color.white;

            // 显示已烘焙子物体状态
            var child = m_Target.transform.Find(k_BakedChildName);
            if (child != null)
            {
                //EditorGUILayout.HelpBox( $"已烘焙：子物体 "{k_BakedChildName}" 存在。", MessageType.Info);
                if (GUILayout.Button("删除烘焙结果"))
                    DeleteBaked();
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── 烘焙入口 ──────────────────────────────────────────────────────────

        private void BakeFloor()
        {
            if (m_Target.localVertices == null || m_Target.localVertices.Count < 3)
            {
                EditorUtility.DisplayDialog("烘焙失败", "至少需要 3 个顶点。", "OK");
                return;
            }
            if (!m_Target.ValidateSpritesShareTexture(out _, out string warn))
            {
                EditorUtility.DisplayDialog("烘焙失败", warn, "OK");
                return;
            }

            // 强制要求父物体 Scale = (1,1,1)
            Vector3 ls = m_Target.transform.lossyScale;
            if (Mathf.Abs(ls.x - 1f) > 0.001f || Mathf.Abs(ls.y - 1f) > 0.001f || Mathf.Abs(ls.z - 1f) > 0.001f)
            {
                EditorUtility.DisplayDialog("烘焙失败",
                    $"父物体的世界 Scale 必须为 (1,1,1)，当前为 ({ls.x:F3}, {ls.y:F3}, {ls.z:F3})。\n请重置 Scale 后再烘焙。",
                    "OK");
                return;
            }

            // 局部空间烘焙
            Texture2D tex = FloorTextureBuilder.Build(
                m_Target.localVertices,
                m_Target.tileSprites,
                m_Target.tileWorldSize,
                m_Target.randomSeed,
                out Vector2 originLocal);

            if (tex == null)
            {
                EditorUtility.DisplayDialog("烘焙失败", "纹理生成失败，请检查参数。", "OK");
                return;
            }

            // 写 PNG 文件（放在场景文件旁边），让 Unity 重新导入生成带 Importer 的 Texture2D
            string pngPath = GetAssetSavePath(m_Target.gameObject.scene.path, m_Target.gameObject.name + "_FloorTex.png");
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            // 配置导入参数（Sprite 模式、PPU、读/写、纹理类型等）
            var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType  = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePackingTag = string.Empty;
                importer.filterMode  = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.isReadable    = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = m_Target.tileSprites[0].pixelsPerUnit;
                // Pivot 设为左下角 (0,0)，与 localPosition = originLocal 对齐
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0f, 0f);
                importer.SetTextureSettings(settings);
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }

            // 加载导入后的 Texture2D / Sprite
            var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            // PNG 设为 Single Sprite 模式时，Unity 自动生成一个 Sprite 子 asset
            var assets = AssetDatabase.LoadAllAssetsAtPath(pngPath);
            Sprite savedSprite = null;
            foreach (var a in assets)
                if (a is Sprite s) { savedSprite = s; break; }

            // 如果没拿到（极端情况），手动创建
            if (savedSprite == null && savedTex != null)
            {
                float ppu = m_Target.tileSprites[0].pixelsPerUnit;
                savedSprite = Sprite.Create(savedTex,
                    new Rect(0, 0, savedTex.width, savedTex.height),
                    Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
                savedSprite.name = m_Target.gameObject.name + "_FloorSprite";
                AssetDatabase.AddObjectToAsset(savedSprite, pngPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                // 重新加载
                assets = AssetDatabase.LoadAllAssetsAtPath(pngPath);
                foreach (var a in assets)
                    if (a is Sprite s) { savedSprite = s; break; }
            }

            if (savedSprite == null)
            {
                EditorUtility.DisplayDialog("烘焙失败", "Sprite 创建失败。", "OK");
                return;
            }

            // 创建或更新子物体
            var child = m_Target.transform.Find(k_BakedChildName);
            GameObject childGO;
            if (child != null)
            {
                childGO = child.gameObject;
                Undo.RegisterCompleteObjectUndo(childGO, "Rebake Floor");
            }
            else
            {
                childGO = new GameObject(k_BakedChildName);
                Undo.RegisterCreatedObjectUndo(childGO, "Bake Floor");
                childGO.transform.SetParent(m_Target.transform, false);
            }

            // 位置：局部空间左下角，旋转跟随父级（localRotation=identity），Scale 固定 (1,1,1)
            childGO.transform.localPosition = new Vector3(originLocal.x, originLocal.y, 0f);
            childGO.transform.localRotation = Quaternion.identity;
            childGO.transform.localScale    = Vector3.one;

            // SpriteRenderer
            var sr = childGO.GetComponent<SpriteRenderer>();
            if (sr == null) sr = childGO.AddComponent<SpriteRenderer>();
            sr.sprite = savedSprite;
            sr.sharedMaterial = m_Target.rcwbMaterial;
            sr.sortingLayerID  = GetDefaultSortingLayer();
            sr.sortingOrder    = 0;

            // RCWBObject
            var rcwb = childGO.GetComponent<RCWBObject>();
            if (rcwb == null) rcwb = childGO.AddComponent<RCWBObject>();
            rcwb.IsWall = false;

            EditorUtility.SetDirty(childGO);
            EditorUtility.SetDirty(m_Target);
            SceneView.RepaintAll();

            Debug.Log($"[IndoorFloorRegion] 烘焙完成 → {pngPath}");
        }

        private void DeleteBaked()
        {
            var child = m_Target.transform.Find(k_BakedChildName);
            if (child != null)
                Undo.DestroyObjectImmediate(child.gameObject);
            EditorUtility.SetDirty(m_Target);
        }

        // ── 资源路径 & 保存 ────────────────────────────────────────────────────

        private static string GetAssetSavePath(string scenePath, string fileName)
        {
            if (string.IsNullOrEmpty(scenePath))
                return "Assets/" + fileName;
            string dir = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            return dir + "/" + fileName;
        }

        private static int GetDefaultSortingLayer()
        {
            return SortingLayer.NameToID("Default");
        }

        // ── Scene View 多边形编辑（与原版完全一致） ──────────────────────────

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_Target == null) return;

            Event e = Event.current;
            List<Vector2> verts = m_Target.localVertices;

            DrawPolygon(verts);
            HandleVertexDrag(verts, e);

            if (m_AddMode)
                HandleAddVertex(verts, e);
        }

        private void DrawPolygon(List<Vector2> localVerts)
        {
            if (localVerts.Count < 2) return;

            Vector2[] world = m_Target.GetWorldVertices();
            float z = m_Target.transform.position.z;
            Vector3[] world3 = new Vector3[world.Length];
            for (int i = 0; i < world.Length; i++)
                world3[i] = new Vector3(world[i].x, world[i].y, z);

            if (world3.Length >= 3)
            {
                Handles.color = k_PolygonColor;
                DrawFilledPolygon(world, z);
            }

            Handles.color = k_OutlineColor;
            for (int i = 0; i < world3.Length; i++)
                Handles.DrawLine(world3[i], world3[(i + 1) % world3.Length]);

            for (int i = 0; i < world.Length; i++)
            {
                Handles.color = (i == m_SelectedVertex) ? k_SelectedColor : k_VertexColor;
                float size = HandleUtility.GetHandleSize(world3[i]) * 0.08f;
                if (Handles.Button(world3[i], Quaternion.identity, size, size * 1.5f, Handles.DotHandleCap))
                {
                    m_SelectedVertex = (m_SelectedVertex == i) ? -1 : i;
                    m_AddMode = false;
                    Repaint();
                }
            }
        }

        private static void DrawFilledPolygon(Vector2[] world, float z)
        {
            var triangles = TriangulateEarClipping(world);
            if (triangles == null) return;

            var tri3 = new Vector3[3];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                tri3[0] = new Vector3(world[triangles[i]].x,     world[triangles[i]].y,     z);
                tri3[1] = new Vector3(world[triangles[i + 1]].x, world[triangles[i + 1]].y, z);
                tri3[2] = new Vector3(world[triangles[i + 2]].x, world[triangles[i + 2]].y, z);
                Handles.DrawAAConvexPolygon(tri3);
            }
        }

        private void HandleVertexDrag(List<Vector2> localVerts, Event e)
        {
            if (localVerts.Count == 0) return;

            for (int i = 0; i < localVerts.Count; i++)
            {
                Vector3 wp = m_Target.transform.TransformPoint(new Vector3(localVerts[i].x, localVerts[i].y, 0f));

                EditorGUI.BeginChangeCheck();
                Vector3 newWp = Handles.PositionHandle(wp, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_Target, "Move Floor Vertex");
                    Vector3 lp = m_Target.transform.InverseTransformPoint(newWp);
                    localVerts[i] = new Vector2(lp.x, lp.y);
                    EditorUtility.SetDirty(m_Target);
                }
            }
        }

        private void HandleAddVertex(List<Vector2> localVerts, Event e)
        {
            Handles.color = k_AddModeColor;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 mouseWorld = ray.origin;
            float planeZ = m_Target.transform.position.z;
            if (Mathf.Abs(ray.direction.z) > 1e-6f)
            {
                float t = (planeZ - ray.origin.z) / ray.direction.z;
                mouseWorld = ray.origin + ray.direction * t;
            }
            float size = HandleUtility.GetHandleSize(mouseWorld) * 0.08f;
            Handles.DotHandleCap(0, mouseWorld, Quaternion.identity, size, EventType.Repaint);
            SceneView.RepaintAll();

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                Vector3 lp = m_Target.transform.InverseTransformPoint(mouseWorld);
                Vector2 newLocal = new Vector2(lp.x, lp.y);
                Undo.RecordObject(m_Target, "Add Floor Vertex");

                int insertIdx = FindBestInsertIndex(localVerts, newLocal);
                localVerts.Insert(insertIdx, newLocal);
                m_SelectedVertex = insertIdx;
                EditorUtility.SetDirty(m_Target);
                e.Use();
            }
        }

        private int FindBestInsertIndex(List<Vector2> verts, Vector2 newLocalPt)
        {
            if (verts.Count < 2) return verts.Count;

            float minDist = float.MaxValue;
            int bestIdx = verts.Count;

            for (int i = 0; i < verts.Count; i++)
            {
                int j = (i + 1) % verts.Count;
                float d = DistancePointToSegment(newLocalPt, verts[i], verts[j]);
                if (d < minDist)
                {
                    minDist = d;
                    bestIdx = j == 0 ? verts.Count : j;
                }
            }
            return bestIdx;
        }

        private void DeleteSelectedVertex()
        {
            if (m_SelectedVertex < 0 || m_SelectedVertex >= m_Target.localVertices.Count) return;
            Undo.RecordObject(m_Target, "Delete Floor Vertex");
            m_Target.localVertices.RemoveAt(m_SelectedVertex);
            m_SelectedVertex = Mathf.Clamp(m_SelectedVertex - 1, -1, m_Target.localVertices.Count - 1);
            EditorUtility.SetDirty(m_Target);
        }

        private static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float sq = ab.sqrMagnitude;
            if (sq < 1e-8f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / sq);
            return Vector2.Distance(p, a + t * ab);
        }

        // ── 耳切法（Editor 内独立，避免依赖 Runtime 代码） ──────────────────

        private static List<int> TriangulateEarClipping(Vector2[] verts)
        {
            int n = verts.Length;
            if (n < 3) return null;

            float area = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = verts[i], b = verts[(i + 1) % n];
                area += (b.x - a.x) * (b.y + a.y);
            }

            var indices = new List<int>(n);
            if (area > 0f)
                for (int i = n - 1; i >= 0; i--) indices.Add(i);
            else
                for (int i = 0; i < n; i++) indices.Add(i);

            var triangles = new List<int>((n - 2) * 3);
            int guard = n * n;
            while (indices.Count > 3 && guard-- > 0)
            {
                bool earFound = false;
                int count = indices.Count;
                for (int i = 0; i < count; i++)
                {
                    int i0 = indices[(i - 1 + count) % count];
                    int i1 = indices[i];
                    int i2 = indices[(i + 1) % count];
                    Vector2 a = verts[i0], b = verts[i1], c = verts[i2];
                    float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (cross <= 0f) continue;
                    bool containsOther = false;
                    for (int k = 0; k < count; k++)
                    {
                        int idx = indices[k];
                        if (idx == i0 || idx == i1 || idx == i2) continue;
                        if (PointInTriangle(verts[idx], a, b, c)) { containsOther = true; break; }
                    }
                    if (containsOther) continue;
                    triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }
                if (!earFound) break;
            }
            if (indices.Count == 3)
            {
                triangles.Add(indices[0]); triangles.Add(indices[1]); triangles.Add(indices[2]);
            }
            return triangles;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }
    }
}
