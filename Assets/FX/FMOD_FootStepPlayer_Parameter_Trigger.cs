using UnityEngine;

namespace ProjectII.FX
{
    /// <summary>
    /// 脚步声触发器盒
    /// 玩家进入触发器时，设置脚步声的参数
    /// </summary>
    public class FMOD_FootStepPlayer_Parameter_Trigger : MonoBehaviour
    {
        /// <summary>
        /// 地面材质类型
        /// </summary>
        public enum GroundMaterial
        {
            Wood = 0,
            Tile = 1,
            // 可根据需要扩展...
        }

        [Header("FMOD Parameters")]
        [SerializeField] private GroundMaterial groundMaterial = GroundMaterial.Wood;

        [Header("Gizmo Settings")]
        [SerializeField] private Color gizmoColor = new Color(0.8f, 0.5f, 0.2f, 0.3f);
        [SerializeField] private Color gizmoWireColor = new Color(0.8f, 0.5f, 0.2f, 1f);

        /// <summary>
        /// 玩家进入触发器时，设置脚步声参数
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            // 获取玩家 GameObject 下的脚步声 Emitter
            // 假设脚步声 Emitter 挂载在玩家的一个子物体上，名称为 "FootstepEmitter" 或有特定组件
            FMODUnity.StudioEventEmitter footstepEmitter = FindFootstepEmitter(other.gameObject);
            
            if (footstepEmitter != null)
            {
                // 设置地面材质参数
                footstepEmitter.SetParameter("GroundMaterial", (float)groundMaterial);
                Debug.Log("设置地面材质参数: " + (int)groundMaterial);
            }
        }

        /// <summary>
        /// 查找玩家的脚步声 Emitter
        /// </summary>
        private FMODUnity.StudioEventEmitter FindFootstepEmitter(GameObject player)
        {
            return Manager.GameSceneManager.Instance.CurrentPlayerCharacter.footstepSFX_Emitter;
            // 先尝试在子物体中查找带有 "Footstep" 名称的物体
            /*Transform footstepTransform = player.transform.Find("FootstepEmitter");
            if (footstepTransform != null)
            {
                FMODUnity.StudioEventEmitter emitter = footstepTransform.GetComponent<FMODUnity.StudioEventEmitter>();
                if (emitter != null) return emitter;
            }

            // 如果没找到，尝试在所有子物体中查找 StudioEventEmitter
            FMODUnity.StudioEventEmitter[] emitters = player.GetComponentsInChildren<FMODUnity.StudioEventEmitter>();
            foreach (var emitter in emitters)
            {
                // 检查是否是脚步声相关的 Emitter（通过名称或事件路径判断）
                if (emitter.EventReference.Path.ToLower().Contains("footstep") ||
                    emitter.gameObject.name.ToLower().Contains("footstep"))
                {
                    return emitter;
                }
            }*/

            return null;
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
