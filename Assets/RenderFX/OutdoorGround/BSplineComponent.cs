#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Render
{
    [DisallowMultipleComponent]
    [AddComponentMenu("ProjectII/B-Spline Curve")]
    public class BSplineComponent : MonoBehaviour
    {
        [SerializeField] private List<Vector2> controlPoints = new List<Vector2>();
        [SerializeField] private List<bool> cornerFlags = new List<bool>();
        [SerializeField] private bool closed;
        [SerializeField] private int degree = 3;
        [SerializeField] private int sampleDensity = 20;

        public List<Vector2> ControlPoints => controlPoints;
        public List<bool> CornerFlags => cornerFlags;
        public bool Closed { get => closed; set => closed = value; }
        public int Degree { get => degree; set => degree = Mathf.Max(1, value); }
        public int SampleDensity { get => sampleDensity; set => sampleDensity = Mathf.Max(1, value); }

        private void Reset()
        {
            controlPoints = new List<Vector2>
            {
                new Vector2(-2f, 0f),
                new Vector2(-1f, 2f),
                new Vector2(1f, 2f),
                new Vector2(2f, 0f)
            };
            cornerFlags = new List<bool> { false, false, false, false };
            closed = false;
            degree = 3;
            sampleDensity = 20;
        }

        public Vector2[] GetWorldControlPoints()
        {
            var result = new Vector2[controlPoints.Count];
            for (int i = 0; i < controlPoints.Count; i++)
            {
                Vector3 wp = transform.TransformPoint(new Vector3(controlPoints[i].x, controlPoints[i].y, 0f));
                result[i] = new Vector2(wp.x, wp.y);
            }
            return result;
        }

        public void SyncCornerFlagsCount()
        {
            while (cornerFlags.Count < controlPoints.Count)
                cornerFlags.Add(false);
            while (cornerFlags.Count > controlPoints.Count)
                cornerFlags.RemoveAt(cornerFlags.Count - 1);
        }

        #region Knot Vector

        /// <summary>
        /// 获取实际用于求值的控制点数组（开放曲线：corner 点重复 p-1 次）。
        /// </summary>
        public Vector2[] GetEffectiveControlPoints()
        {
            if (closed)
                return BuildEffectiveControlPointsClosed();

            int p = degree;
            var result = new List<Vector2>(controlPoints.Count);

            for (int i = 0; i < controlPoints.Count; i++)
            {
                result.Add(controlPoints[i]);
                // 硬角点：在控制点数组中重复 p-1 次，使节点向量中对应节点的重复度达到 p
                if (i > 0 && i < controlPoints.Count - 1 &&
                    i < cornerFlags.Count && cornerFlags[i])
                {
                    for (int r = 0; r < p - 1; r++)
                        result.Add(controlPoints[i]);
                }
            }

            return result.ToArray();
        }

        private Vector2[] BuildEffectiveControlPointsClosed()
        {
            int p = degree;
            var result = new List<Vector2>(controlPoints.Count + p);

            for (int i = 0; i < controlPoints.Count; i++)
            {
                result.Add(controlPoints[i]);
                // 硬角点重复 p-1 次
                if (i < cornerFlags.Count && cornerFlags[i])
                {
                    for (int r = 0; r < p - 1; r++)
                        result.Add(controlPoints[i]);
                }
            }

            // 闭合 wrap：追加首 p 个点
            int wrapCount = p;
            for (int i = 0; i < wrapCount && i < result.Count; i++)
                result.Add(result[i]);

            return result.ToArray();
        }

        /// <summary>
        /// 根据 effective 控制点数量构建对应的 clamped 节点向量。
        /// </summary>
        public float[] BuildKnotVectorForEffective(Vector2[] effectivePts)
        {
            int p = degree;
            int n = effectivePts.Length - 1;

            if (closed)
                return BuildKnotVectorForEffectiveClosed(effectivePts, p);

            int knotCount = n + p + 2;
            var knots = new float[knotCount];

            for (int i = 0; i <= p; i++)
                knots[i] = 0f;

            for (int i = n + 1; i < knotCount; i++)
                knots[i] = 1f;

            int internalCount = n - p;
            for (int i = 1; i <= internalCount; i++)
                knots[p + i] = (float)i / (internalCount + 1);

            return knots;
        }

        private float[] BuildKnotVectorForEffectiveClosed(Vector2[] effectivePts, int p)
        {
            int n = effectivePts.Length - 1;
            int knotCount = n + p + 2;
            var knots = new float[knotCount];

            for (int i = 0; i < knotCount; i++)
                knots[i] = (float)i / (knotCount - 1);

            return knots;
        }

        #endregion

        #region De Boor Evaluation

        public Vector2 EvaluatePoint(float t)
        {
            var pts = GetEffectiveControlPoints();
            float[] knots = BuildKnotVectorForEffective(pts);
            int p = degree;

            if (pts.Length < p + 1) return Vector2.zero;

            t = Mathf.Clamp01(t);

            if (!closed && t <= 0f) return pts[0];
            if (!closed && t >= 1f) return pts[pts.Length - 1];

            return DeBoor(t, knots, pts, p);
        }

        public static Vector2 DeBoorStatic(float t, float[] knots, Vector2[] pts, int p)
        {
            int k = FindSpan(t, knots, pts.Length, p);

            var d = new Vector2[p + 1];
            for (int i = 0; i <= p; i++)
                d[i] = pts[k - p + i];

            for (int r = 1; r <= p; r++)
            {
                for (int j = p; j >= r; j--)
                {
                    int idx = k - p + j;
                    float denom = knots[idx + p + 1 - r] - knots[idx];
                    float alpha;
                    if (Mathf.Abs(denom) < 1e-10f)
                        alpha = 0f;
                    else
                        alpha = (t - knots[idx]) / denom;

                    d[j] = (1f - alpha) * d[j - 1] + alpha * d[j];
                }
            }

            return d[p];
        }

        private static Vector2 DeBoor(float t, float[] knots, Vector2[] pts, int p)
        {
            return DeBoorStatic(t, knots, pts, p);
        }

        private static int FindSpan(float t, float[] knots, int n, int p)
        {
            // 找到 k 使 knots[k] <= t < knots[k+1]，跳过退化区间
            for (int k = p; k < n; k++)
            {
                if (knots[k + 1] - knots[k] < 1e-10f) continue;
                if (t >= knots[k] && t < knots[k + 1])
                    return k;
            }
            // t 在末尾，回退到最后一个非退化区间
            for (int k = n - 1; k >= p; k--)
            {
                if (knots[k + 1] - knots[k] > 1e-10f)
                    return k;
            }
            return p;
        }

        public static Vector2 EvaluateTangentStatic(float t, float[] knots, Vector2[] pts, int p)
        {
            int k = FindSpan(t, knots, pts.Length, p);

            var d = new Vector2[p + 1];
            for (int i = 0; i <= p; i++)
                d[i] = pts[k - p + i];

            for (int j = p; j >= 1; j--)
            {
                int idx = k - p + j;
                float denom = knots[idx + p] - knots[idx];
                float alpha;
                if (Mathf.Abs(denom) < 1e-10f)
                    alpha = 0f;
                else
                    alpha = (t - knots[idx]) / denom;

                d[j] = (1f - alpha) * d[j - 1] + alpha * d[j];
            }

            Vector2 tangent = p * (d[p] - d[p - 1]);

            if (tangent.sqrMagnitude < 1e-12f)
            {
                float dt = 1e-4f;
                Vector2 pPlus = DeBoorStatic(Mathf.Min(t + dt, 1f), knots, pts, p);
                Vector2 pMinus = DeBoorStatic(Mathf.Max(t - dt, 0f), knots, pts, p);
                tangent = pPlus - pMinus;
            }

            return tangent;
        }

        private static Vector2 EvaluateTangent(float t, float[] knots, Vector2[] pts, int p)
        {
            return EvaluateTangentStatic(t, knots, pts, p);
        }

        /// <summary>
        /// 在参数 t 处求法线（沿曲线行进方向向右，即切线逆时针旋转 90°）。
        /// </summary>
        public Vector2 EvaluateNormal(float t)
        {
            var pts = GetEffectiveControlPoints();
            float[] knots = BuildKnotVectorForEffective(pts);
            int p = degree;

            if (pts.Length < p + 1) return Vector2.right;

            Vector2 tangent = EvaluateTangent(Mathf.Clamp01(t), knots, pts, p);
            if (tangent.sqrMagnitude < 1e-12f) return Vector2.right;

            // 切线 (dx, dy) → 法线 (dy, -dx)（向右）
            return new Vector2(tangent.y, -tangent.x).normalized;
        }

        #endregion

        #region Sampling

        public Vector2[] SampleCurve()
        {
            return SampleCurve(sampleDensity);
        }

        public Vector2[] SampleCurve(int samplesPerSpan)
        {
            var pts = GetEffectiveControlPoints();
            float[] knots = BuildKnotVectorForEffective(pts);
            int p = degree;

            if (pts.Length < p + 1) return new Vector2[0];

            // 确定有效跨度
            var spans = new List<(float t0, float t1)>();
            int knotStart = p;
            int knotEnd = pts.Length;

            for (int i = knotStart; i < knotEnd; i++)
            {
                if (knots[i] < knots[i + 1] - 1e-10f)
                    spans.Add((knots[i], knots[i + 1]));
            }

            if (spans.Count == 0) return new Vector2[] { pts[0] };

            var points = new List<Vector2>(spans.Count * samplesPerSpan);

            for (int si = 0; si < spans.Count; si++)
            {
                float t0 = spans[si].t0;
                float t1 = spans[si].t1;
                for (int j = 0; j < samplesPerSpan; j++)
                {
                    float t = t0 + (t1 - t0) * (j + 0.5f) / samplesPerSpan;
                    points.Add(DeBoor(t, knots, pts, p));
                }
            }

            if (closed && points.Count > 0)
                points.Add(points[0]);

            return points.ToArray();
        }

        /// <summary>
        /// 采样曲线并转换为世界坐标。
        /// </summary>
        public Vector3[] SampleCurveWorld()
        {
            Vector2[] localPts = SampleCurve();
            var world = new Vector3[localPts.Length];
            float z = transform.position.z;
            for (int i = 0; i < localPts.Length; i++)
            {
                Vector3 wp = transform.TransformPoint(new Vector3(localPts[i].x, localPts[i].y, 0f));
                world[i] = new Vector3(wp.x, wp.y, z);
            }
            return world;
        }

        /// <summary>
        /// 采样曲线法线（局部空间，向右为正方向）。
        /// 返回与 SampleCurve 采样点一一对应的法线数组。
        /// </summary>
        public Vector2[] SampleNormals()
        {
            return SampleNormals(sampleDensity);
        }

        public Vector2[] SampleNormals(int samplesPerSpan)
        {
            var pts = GetEffectiveControlPoints();
            float[] knots = BuildKnotVectorForEffective(pts);
            int p = degree;

            if (pts.Length < p + 1) return new Vector2[0];

            var spans = new List<(float t0, float t1)>();
            int knotStart = p;
            int knotEnd = pts.Length;

            for (int i = knotStart; i < knotEnd; i++)
            {
                if (knots[i] < knots[i + 1] - 1e-10f)
                    spans.Add((knots[i], knots[i + 1]));
            }

            if (spans.Count == 0) return new Vector2[] { Vector2.right };

            var normals = new List<Vector2>(spans.Count * samplesPerSpan);

            for (int si = 0; si < spans.Count; si++)
            {
                float t0 = spans[si].t0;
                float t1 = spans[si].t1;
                for (int j = 0; j < samplesPerSpan; j++)
                {
                    float t = t0 + (t1 - t0) * (j + 0.5f) / samplesPerSpan;
                    Vector2 tangent = EvaluateTangent(t, knots, pts, p);
                    if (tangent.sqrMagnitude < 1e-12f)
                        normals.Add(Vector2.right);
                    else
                        normals.Add(new Vector2(tangent.y, -tangent.x).normalized);
                }
            }

            if (closed && normals.Count > 0)
                normals.Add(normals[0]);

            return normals.ToArray();
        }

        /// <summary>
        /// 计算每个用户控制点对应的曲线点位置和法线（向右为正方向）。
        /// 法线取控制点左右近处曲线法线的平均。
        /// 对硬角点，左右法线不同，平均得到折中法线。
        /// 对普通点，左右法线一致，平均后不变。
        /// </summary>
        /// <param name="curvePoints">输出：每个控制点对应的曲线上的点（局部空间）</param>
        /// <param name="normals">输出：每个控制点对应的法线（局部空间，向右为正）</param>
        /// <param name="epsilon">控制点对应参数 t 附近的偏移量，默认 0.001</param>
        public void SampleControlPointNormals(out Vector2[] curvePoints, out Vector2[] normals, float epsilon = 0.001f)
        {
            var pts = GetEffectiveControlPoints();
            float[] knots = BuildKnotVectorForEffective(pts);
            int p = degree;

            int count = controlPoints.Count;
            curvePoints = new Vector2[count];
            normals = new Vector2[count];

            if (pts.Length < p + 1)
            {
                for (int i = 0; i < count; i++)
                {
                    curvePoints[i] = controlPoints[i];
                    normals[i] = Vector2.right;
                }
                return;
            }

            float tMin = knots[p];
            float tMax = knots[pts.Length];
            float tRange = tMax - tMin;

            for (int i = 0; i < count; i++)
            {
                float tCenter = FindClosestT(controlPoints[i], pts, knots, p, tMin, tMax);

                // 曲线点位置
                curvePoints[i] = DeBoor(tCenter, knots, pts, p);

                // 左右偏移处的法线
                float tLeft = Mathf.Max(tMin, tCenter - epsilon * tRange);
                float tRight = Mathf.Min(tMax, tCenter + epsilon * tRange);

                Vector2 nLeft, nRight;

                Vector2 tanLeft = EvaluateTangent(tLeft, knots, pts, p);
                if (tanLeft.sqrMagnitude < 1e-12f)
                    nLeft = Vector2.right;
                else
                    nLeft = new Vector2(tanLeft.y, -tanLeft.x).normalized;

                Vector2 tanRight = EvaluateTangent(tRight, knots, pts, p);
                if (tanRight.sqrMagnitude < 1e-12f)
                    nRight = Vector2.right;
                else
                    nRight = new Vector2(tanRight.y, -tanRight.x).normalized;

                Vector2 avg = nLeft + nRight;
                normals[i] = avg.sqrMagnitude > 1e-12f ? avg.normalized : Vector2.right;
            }
        }

        /// <summary>
        /// 通过粗采样找到曲线上离指定局部空间点最近的参数 t。
        /// </summary>
        public static float FindClosestTStatic(Vector2 localPoint, Vector2[] pts, float[] knots, int p, float tMin, float tMax)
        {
            // 粗采样 64 个点
            const int coarseSamples = 64;
            float bestT = tMin;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < coarseSamples; i++)
            {
                float t = tMin + (tMax - tMin) * (i + 0.5f) / coarseSamples;
                Vector2 pt = DeBoorStatic(t, knots, pts, p);
                float distSq = (pt - localPoint).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestT = t;
                }
            }

            // 精细搜索：在 bestT 附近缩小范围再采样
            float fineRange = (tMax - tMin) / coarseSamples;
            float fineMin = Mathf.Max(tMin, bestT - fineRange);
            float fineMax = Mathf.Min(tMax, bestT + fineRange);

            for (int i = 0; i < 16; i++)
            {
                float t = fineMin + (fineMax - fineMin) * (i + 0.5f) / 16;
                Vector2 pt = DeBoorStatic(t, knots, pts, p);
                float distSq = (pt - localPoint).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestT = t;
                }
            }

            return bestT;
        }

        private static float FindClosestT(Vector2 localPoint, Vector2[] pts, float[] knots, int p, float tMin, float tMax)
        {
            return FindClosestTStatic(localPoint, pts, knots, p, tMin, tMax);
        }

        #endregion
    }
}
#endif
