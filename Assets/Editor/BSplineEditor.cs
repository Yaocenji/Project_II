#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectII.Render
{
    [CustomEditor(typeof(BSplineComponent))]
    public class BSplineEditor : Editor
    {
        private BSplineComponent m_Target;
        private int m_SelectedPoint = -1;
        private bool m_AddMode;
        private bool m_ShowNormals;
        private bool m_ShowSampledNormals;
        private int m_SampledNormalCount = 16;

        private static readonly Color k_CurveColor      = new Color(0.8f, 0.6f, 0.2f, 0.9f);
        private static readonly Color k_PolygonColor    = new Color(0.8f, 0.6f, 0.2f, 0.25f);
        private static readonly Color k_PointColor      = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color k_SelectedColor   = new Color(1f, 0.6f, 0.1f, 1f);
        private static readonly Color k_CornerColor     = new Color(1f, 0.3f, 0.3f, 0.9f);
        private static readonly Color k_AddModeColor    = new Color(0.2f, 1f, 0.4f, 0.9f);
        private static readonly Color k_NormalColor     = new Color(0.3f, 0.7f, 1f, 0.8f);
        private static readonly Color k_SampledNormalColor = new Color(0.4f, 1f, 0.5f, 0.6f);

        private void OnEnable()
        {
            m_Target = (BSplineComponent)target;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        #region Inspector

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            m_Target.SyncCornerFlagsCount();

            // ── 全局参数 ──
            EditorGUILayout.LabelField("曲线参数", EditorStyles.boldLabel);

            var closedProp = serializedObject.FindProperty("closed");
            EditorGUILayout.PropertyField(closedProp);

            var degreeProp = serializedObject.FindProperty("degree");
            EditorGUILayout.PropertyField(degreeProp);

            var densityProp = serializedObject.FindProperty("sampleDensity");
            EditorGUILayout.PropertyField(densityProp);

            EditorGUILayout.Space(6);

            // ── 显示选项 ──
            EditorGUILayout.LabelField("显示", EditorStyles.boldLabel);
            m_ShowNormals = EditorGUILayout.Toggle("显示控制点法线", m_ShowNormals);
            m_ShowSampledNormals = EditorGUILayout.Toggle("显示采样法线", m_ShowSampledNormals);
            if (m_ShowSampledNormals)
            {
                EditorGUI.indentLevel++;
                m_SampledNormalCount = EditorGUILayout.IntSlider("采样数", m_SampledNormalCount, 4, 64);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6);

            // ── 工具栏 ──
            EditorGUILayout.LabelField("控制点", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.color = m_AddMode ? k_AddModeColor : Color.white;
            bool newAddMode = GUILayout.Toggle(m_AddMode, "添加控制点", EditorStyles.miniButton);
            if (newAddMode != m_AddMode)
            {
                m_AddMode = newAddMode;
                if (m_AddMode) m_SelectedPoint = -1;
            }
            GUI.color = Color.white;
            if (GUILayout.Button("删除选中", EditorStyles.miniButton))
                DeleteSelectedPoint();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── 控制点列表 ──
            var ptsProp = serializedObject.FindProperty("controlPoints");
            var cornerProp = serializedObject.FindProperty("cornerFlags");

            for (int i = 0; i < ptsProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // 选中按钮
                bool isSelected = (i == m_SelectedPoint);
                bool isCorner = i < cornerProp.arraySize && cornerProp.GetArrayElementAtIndex(i).boolValue;

                GUI.color = isSelected ? k_SelectedColor : (isCorner ? k_CornerColor : Color.white);
                if (GUILayout.Button($"#{i}", GUILayout.Width(30)))
                {
                    m_SelectedPoint = (m_SelectedPoint == i) ? -1 : i;
                    m_AddMode = false;
                    Repaint();
                }
                GUI.color = Color.white;

                // 坐标
                EditorGUILayout.PropertyField(ptsProp.GetArrayElementAtIndex(i), GUIContent.none);

                // 硬角 Toggle
                if (i < cornerProp.arraySize)
                {
                    var cElem = cornerProp.GetArrayElementAtIndex(i);
                    bool newCorner = GUILayout.Toggle(cElem.boolValue, "硬角", GUILayout.Width(48));
                    if (newCorner != cElem.boolValue)
                        cElem.boolValue = newCorner;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("清空所有控制点"))
            {
                Undo.RecordObject(m_Target, "Clear B-Spline Points");
                m_Target.ControlPoints.Clear();
                m_Target.CornerFlags.Clear();
                m_SelectedPoint = -1;
                EditorUtility.SetDirty(m_Target);
            }

            // ── 信息 ──
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("信息", EditorStyles.boldLabel);
            int minPts = m_Target.Degree + 1;
            if (m_Target.ControlPoints.Count < minPts)
                EditorGUILayout.HelpBox($"至少需要 {minPts} 个控制点（阶数+1）。", MessageType.Warning);

            EditorGUILayout.LabelField($"控制点: {m_Target.ControlPoints.Count}  阶数: {m_Target.Degree}  采样密度: {m_Target.SampleDensity}");

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Scene View

        private void OnSceneGUI(SceneView sceneView)
        {
            if (m_Target == null) return;

            Event e = Event.current;
            List<Vector2> pts = m_Target.ControlPoints;

            DrawCurve();
            DrawControlPolygon(pts);
            if (m_ShowNormals)
                DrawControlPointNormals();
            if (m_ShowSampledNormals)
                DrawSampledNormals();
            HandlePointDrag(pts, e);

            if (m_AddMode)
                HandleAddPoint(pts, e);
        }

        private void DrawCurve()
        {
            if (m_Target.ControlPoints.Count < m_Target.Degree + 1) return;

            Vector3[] worldPts = m_Target.SampleCurveWorld();
            if (worldPts.Length < 2) return;

            Handles.color = k_CurveColor;
            Handles.DrawAAPolyLine(3f, worldPts);
        }

        private void DrawControlPolygon(List<Vector2> pts)
        {
            if (pts.Count < 2) return;

            Vector2[] world = m_Target.GetWorldControlPoints();
            float z = m_Target.transform.position.z;

            Handles.color = k_PolygonColor;
            for (int i = 0; i < world.Length - 1; i++)
                Handles.DrawLine(
                    new Vector3(world[i].x, world[i].y, z),
                    new Vector3(world[i + 1].x, world[i + 1].y, z));

            if (m_Target.Closed && world.Length >= 2)
                Handles.DrawLine(
                    new Vector3(world[world.Length - 1].x, world[world.Length - 1].y, z),
                    new Vector3(world[0].x, world[0].y, z));

            // 控制点手柄
            m_Target.SyncCornerFlagsCount();
            for (int i = 0; i < world.Length; i++)
            {
                Vector3 wp3 = new Vector3(world[i].x, world[i].y, z);
                bool isCorner = i < m_Target.CornerFlags.Count && m_Target.CornerFlags[i];
                bool isSelected = (i == m_SelectedPoint);

                Handles.color = isSelected ? k_SelectedColor : (isCorner ? k_CornerColor : k_PointColor);

                float size = HandleUtility.GetHandleSize(wp3) * 0.08f;

                if (Handles.Button(wp3, Quaternion.identity, size, size * 1.5f,
                        isCorner ? Handles.RectangleHandleCap : Handles.DotHandleCap))
                {
                    m_SelectedPoint = (m_SelectedPoint == i) ? -1 : i;
                    m_AddMode = false;
                    Repaint();
                }
            }
        }

        private void DrawControlPointNormals()
        {
            if (m_Target.ControlPoints.Count < m_Target.Degree + 1) return;

            m_Target.SampleControlPointNormals(out Vector2[] curvePoints, out Vector2[] normals);
            float z = m_Target.transform.position.z;

            Handles.color = k_NormalColor;
            for (int i = 0; i < normals.Length && i < m_Target.ControlPoints.Count; i++)
            {
                Vector3 wp = m_Target.transform.TransformPoint(new Vector3(curvePoints[i].x, curvePoints[i].y, 0f));
                wp.z = z;

                Vector3 localNormal = new Vector3(normals[i].x, normals[i].y, 0f);
                Vector3 worldNormal = m_Target.transform.TransformDirection(localNormal);
                worldNormal.z = 0f;
                worldNormal = worldNormal.normalized;

                DrawNormalArrow(wp, worldNormal, k_NormalColor);
            }
        }

        private void DrawSampledNormals()
        {
            if (m_Target.ControlPoints.Count < m_Target.Degree + 1) return;

            var pts = m_Target.GetEffectiveControlPoints();
            float[] knots = m_Target.BuildKnotVectorForEffective(pts);
            int p = m_Target.Degree;
            float z = m_Target.transform.position.z;

            float tMin = knots[p];
            float tMax = knots[pts.Length];

            for (int i = 0; i < m_SampledNormalCount; i++)
            {
                float t = tMin + (tMax - tMin) * (i + 0.5f) / m_SampledNormalCount;
                Vector2 localPt = BSplineComponent.DeBoorStatic(t, knots, pts, p);
                Vector3 wp = m_Target.transform.TransformPoint(new Vector3(localPt.x, localPt.y, 0f));
                wp.z = z;

                Vector2 tangent = BSplineComponent.EvaluateTangentStatic(t, knots, pts, p);
                Vector3 worldNormal;
                if (tangent.sqrMagnitude < 1e-12f)
                    worldNormal = Vector3.right;
                else
                {
                    Vector2 n = new Vector2(tangent.y, -tangent.x).normalized;
                    Vector3 wn = m_Target.transform.TransformDirection(new Vector3(n.x, n.y, 0f));
                    wn.z = 0f;
                    worldNormal = wn.normalized;
                }

                DrawNormalArrow(wp, worldNormal, k_SampledNormalColor);
            }
        }

        private static void DrawNormalArrow(Vector3 origin, Vector3 direction, Color color)
        {
            Handles.color = color;
            float len = HandleUtility.GetHandleSize(origin) * 0.25f;
            Handles.DrawLine(origin, origin + direction * len);

            Vector3 tip = origin + direction * len;
            Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
            float arrowSize = len * 0.2f;
            Handles.DrawLine(tip, tip - direction * arrowSize + right * arrowSize * 0.5f);
            Handles.DrawLine(tip, tip - direction * arrowSize - right * arrowSize * 0.5f);
        }

        private void HandlePointDrag(List<Vector2> pts, Event e)
        {
            if (pts.Count == 0 || m_SelectedPoint < 0 || m_SelectedPoint >= pts.Count) return;

            Vector3 wp = m_Target.transform.TransformPoint(new Vector3(pts[m_SelectedPoint].x, pts[m_SelectedPoint].y, 0f));

            EditorGUI.BeginChangeCheck();
            Vector3 newWp = Handles.PositionHandle(wp, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_Target, "Move B-Spline Point");
                Vector3 lp = m_Target.transform.InverseTransformPoint(newWp);
                pts[m_SelectedPoint] = new Vector2(lp.x, lp.y);
                EditorUtility.SetDirty(m_Target);
            }
        }

        private void HandleAddPoint(List<Vector2> pts, Event e)
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
                Undo.RecordObject(m_Target, "Add B-Spline Point");

                int insertIdx = FindBestInsertIndex(pts, newLocal);
                pts.Insert(insertIdx, newLocal);
                m_Target.CornerFlags.Insert(insertIdx, false);
                m_SelectedPoint = insertIdx;
                EditorUtility.SetDirty(m_Target);
                e.Use();
            }
        }

        private int FindBestInsertIndex(List<Vector2> pts, Vector2 newPt)
        {
            if (pts.Count < 2) return pts.Count;

            float minDist = float.MaxValue;
            int bestIdx = pts.Count;

            int end = m_Target.Closed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < end; i++)
            {
                int j = (i + 1) % pts.Count;
                float d = DistancePointToSegment(newPt, pts[i], pts[j]);
                if (d < minDist)
                {
                    minDist = d;
                    bestIdx = j == 0 ? pts.Count : j;
                }
            }
            return bestIdx;
        }

        private void DeleteSelectedPoint()
        {
            if (m_SelectedPoint < 0 || m_SelectedPoint >= m_Target.ControlPoints.Count) return;
            Undo.RecordObject(m_Target, "Delete B-Spline Point");
            m_Target.ControlPoints.RemoveAt(m_SelectedPoint);
            if (m_SelectedPoint < m_Target.CornerFlags.Count)
                m_Target.CornerFlags.RemoveAt(m_SelectedPoint);
            m_SelectedPoint = Mathf.Clamp(m_SelectedPoint - 1, -1, m_Target.ControlPoints.Count - 1);
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

        #endregion
    }
}
#endif
