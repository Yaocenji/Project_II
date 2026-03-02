using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ProjectII.FX
{
    public class FMOD_Snapshot_Trigger : MonoBehaviour
    {
        /// <summary>
        /// 场景效果快照名称
        /// </summary>
        public string snapshotName;
        
        [Header("Gizmo Settings")]
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        [SerializeField] private Color gizmoWireColor = new Color(0.2f, 0.8f, 0.2f, 1f);
        
        /// <summary>
        /// 场景效果快照实例
        /// </summary>
        private FMOD.Studio.EventInstance snapshot;

        private void Start()
        {
            snapshot = FMODUnity.RuntimeManager.CreateInstance(snapshotName);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            { 
                snapshot.start();
                //Debug.Log("Snapshot triggered: " + snapshotName);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            { 
                snapshot.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                //Debug.Log("Snapshot stopped: " + snapshotName);
            }
        }

        /// <summary>
        /// 在 Scene 视图中绘制触发器范围（始终绘制，无论是否选中）
        /// </summary>
        private void OnDrawGizmos()
        {
            DrawTriggerGizmo();
        }

        /// <summary>
        /// 绘制触发器的 Gizmo
        /// </summary>
        private void DrawTriggerGizmo()
        {
            // 尝试获取 BoxCollider2D
            BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider != null && boxCollider.isTrigger)
            {
                // 计算实际的世界空间位置和大小
                Vector3 center = transform.TransformPoint(boxCollider.offset);
                Vector3 size = new Vector3(
                    boxCollider.size.x * transform.lossyScale.x,
                    boxCollider.size.y * transform.lossyScale.y,
                    0.1f
                );

                // 绘制半透明填充
                Gizmos.color = gizmoColor;
                Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
                Gizmos.DrawCube(Vector3.zero, size);

                // 绘制边框线
                Gizmos.color = gizmoWireColor;
                Gizmos.DrawWireCube(Vector3.zero, size);
                
                Gizmos.matrix = Matrix4x4.identity;
                return;
            }

            // 尝试获取 CircleCollider2D
            CircleCollider2D circleCollider = GetComponent<CircleCollider2D>();
            if (circleCollider != null && circleCollider.isTrigger)
            {
                Vector3 center = transform.TransformPoint(circleCollider.offset);
                float radius = circleCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

                // 绘制半透明填充圆
                Gizmos.color = gizmoColor;
                DrawFilledCircle(center, radius);

                // 绘制边框圆
                Gizmos.color = gizmoWireColor;
                DrawWireCircle(center, radius);
                return;
            }

            // 尝试获取 PolygonCollider2D
            PolygonCollider2D polygonCollider = GetComponent<PolygonCollider2D>();
            if (polygonCollider != null && polygonCollider.isTrigger)
            {
                Gizmos.color = gizmoWireColor;
                Vector2[] points = polygonCollider.points;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 p1 = transform.TransformPoint(points[i] + polygonCollider.offset);
                    Vector3 p2 = transform.TransformPoint(points[(i + 1) % points.Length] + polygonCollider.offset);
                    Gizmos.DrawLine(p1, p2);
                }
            }
        }

        /// <summary>
        /// 绘制填充圆（近似）
        /// </summary>
        private void DrawFilledCircle(Vector3 center, float radius)
        {
            // 使用 Gizmos.DrawSphere 绘制一个扁平的球体来模拟圆
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(1, 1, 0.01f));
            Gizmos.DrawSphere(Vector3.zero, radius);
            Gizmos.matrix = Matrix4x4.identity;
        }

        /// <summary>
        /// 绘制线框圆
        /// </summary>
        private void DrawWireCircle(Vector3 center, float radius, int segments = 32)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
                Gizmos.DrawLine(prevPoint, nextPoint);
                prevPoint = nextPoint;
            }
        }
    }
}
