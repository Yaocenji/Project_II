using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectII.Render
{
    [CustomEditor(typeof(GroundRegion))]
    public class GroundRegionEditor : Editor
    {
        private GroundRegion m_Target;
        private int  m_SelectedVertex = -1;
        private bool m_AddMode = false;

        private static readonly Color k_PolygonColor  = new Color(0.8f, 0.6f, 0.2f, 0.20f);
        private static readonly Color k_OutlineColor  = new Color(0.8f, 0.6f, 0.2f, 0.9f);
        private static readonly Color k_VertexColor   = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color k_SelectedColor = new Color(1f, 0.6f, 0.1f, 1f);
        private static readonly Color k_AddModeColor  = new Color(0.2f, 1f, 0.4f, 0.9f);

        private void OnEnable()
        {
            m_Target = (GroundRegion)target;
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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("featherWidth"));

            // 校验
            if (m_Target.tileSprites != null && m_Target.tileSprites.Count > 0)
            {
                if (!m_Target.ValidateSprites(out _, out string warn))
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
                Undo.RecordObject(m_Target, "Clear GroundRegion Vertices");
                m_Target.localVertices.Clear();
                m_SelectedVertex = -1;
                EditorUtility.SetDirty(m_Target);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Scene View 多边形编辑 ──────────────────────────────────────────

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

            // 羽化范围可视化
            if (m_Target.featherWidth > 0f && world3.Length >= 3)
            {
                Handles.color = new Color(0.8f, 0.6f, 0.2f, 0.15f);
                // 简化：仅画轮廓线
                for (int i = 0; i < world3.Length; i++)
                {
                    Vector3 a = world3[i];
                    Vector3 b = world3[(i + 1) % world3.Length];
                    Handles.DrawLine(a, b);
                }
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
                    Undo.RecordObject(m_Target, "Move GroundRegion Vertex");
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
                Undo.RecordObject(m_Target, "Add GroundRegion Vertex");

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
            Undo.RecordObject(m_Target, "Delete GroundRegion Vertex");
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

        // ── 耳切法 ─────────────────────────────────────────────────────────

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
