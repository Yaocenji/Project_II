using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace ProjectII.Weapon
{
    /// <summary>
    /// 子弹类型枚举
    /// </summary>
    public enum BulletType
    {
        InstantHit,     // 即时命中：使用射线检测
        Physical       // 物理实体：使用碰撞检测
    }

    /// <summary>
    /// 所有子弹的基类
    /// 支持即时命中和物理实体两种类型
    /// </summary>
    public abstract class BulletBase : MonoBehaviour
    {
        // Layer名称常量
        private const string LAYER_PLAYER = "Player";
        private const string LAYER_ALLY = "Ally";
        private const string LAYER_ENEMY = "Enemy";
        private const string LAYER_FRIENDLY_FIRE = "FriendlyFire";
        private const string LAYER_ENEMY_FIRE = "EnemyFire";

        [Header("Bullet Settings")]
        [SerializeField] private BulletType bulletType = BulletType.InstantHit;
        [SerializeField] private LayerMask hitLayerMask = -1; // 可碰撞的层

        [Header("Raycast Settings (仅即时命中类型)")]
        [SerializeField] protected float raycastDistance = 100f; // 射线检测距离

        /// <summary>
        /// 这里保留子弹池的引用。
        /// </summary>
        protected ObjectPool<GameObject> bulletPool;

        /// <summary>
        /// 发射者GameObject
        /// </summary>
        public GameObject Shooter { get; private set; }

        /// <summary>
        /// 子弹类型
        /// </summary>
        public BulletType Type => bulletType;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool isInitialized = false;

        /// <summary>
        /// 初始化子弹
        /// 在实际使用中，在实例化后立即调用以保证在OnEnable前调用该函数
        /// </summary>
        /// <param name="shooter">发射者GameObject</param>
        /// <param name="type">子弹类型（即时命中/物理实体）</param>
        /// <param name="bulletPool">传递进来的，用于释放的子弹对象池</param>
        public void Init(GameObject shooter, BulletType type, ObjectPool<GameObject> bulletPool)
        {
            if (isInitialized)
            {
                Debug.LogWarning("BulletBase已经初始化，重复初始化将被忽略。");
                return;
            }

            Shooter = shooter;
            bulletType = type;
            this.bulletPool = bulletPool;
            isInitialized = true;

            // 根据发射者的Layer设置子弹的Layer
            SetBulletLayer(shooter);

            // 根据子弹的Layer设置hitLayerMask
            SetHitMaskByBulletLayer();

            // 如果是物理实体类型，确保有必要的组件
            if (bulletType == BulletType.Physical)
            {
                SetupPhysicalBullet();
            }
        }

        /// <summary>
        /// 根据发射者的Layer设置子弹的Layer
        /// Player/Ally -> FriendlyFire
        /// Enemy -> EnemyFire
        /// </summary>
        private void SetBulletLayer(GameObject shooter)
        {
            if (shooter == null)
            {
                Debug.LogWarning("发射者为空，无法设置子弹Layer。");
                return;
            }

            string shooterLayerName = LayerMask.LayerToName(shooter.layer);
            string targetLayerName = null;

            // 根据发射者Layer确定目标Layer
            if (shooterLayerName == LAYER_PLAYER || shooterLayerName == LAYER_ALLY)
            {
                targetLayerName = LAYER_FRIENDLY_FIRE;
            }
            else if (shooterLayerName == LAYER_ENEMY)
            {
                targetLayerName = LAYER_ENEMY_FIRE;
            }
            else
            {
                Debug.LogWarning($"发射者Layer '{shooterLayerName}' 不在预期范围内（Player/Ally/Enemy），子弹Layer未设置。");
                return;
            }

            // 设置子弹Layer
            int targetLayer = LayerMask.NameToLayer(targetLayerName);
            if (targetLayer != -1)
            {
                gameObject.layer = targetLayer;
            }
            else
            {
                Debug.LogError($"找不到'{targetLayerName}'层，请检查Layer设置。");
            }
        }

        /// <summary>
        /// 根据子弹的Layer，设置hitLayerMask
        /// </summary>
        private void SetHitMaskByBulletLayer()
        {
            if (this.gameObject.layer == LayerMask.NameToLayer(LAYER_FRIENDLY_FIRE))
            {
                // 如果是友军火力，则不击中自己或友方
                hitLayerMask &= ~LayerMask.GetMask(LAYER_PLAYER, LAYER_ALLY);
            }
            else if (this.gameObject.layer == LayerMask.NameToLayer(LAYER_ENEMY_FIRE))
            {
                // 如果是敌方火力，则不击中敌方
                hitLayerMask &= ~LayerMask.GetMask(LAYER_ENEMY);
            }
        }

        /// <summary>
        /// 设置物理实体类型子弹的必要组件
        /// </summary>
        private void SetupPhysicalBullet()
        {
            // // 确保有Collider2D组件（触发器）
            // Collider2D collider = GetComponent<Collider2D>();
            // if (collider == null)
            // {
            //     // 如果没有Collider，添加一个CircleCollider2D作为默认
            //     collider = gameObject.AddComponent<CircleCollider2D>();
            //     Debug.LogWarning("物理实体类型子弹缺少Collider2D组件，已自动添加CircleCollider2D。");
            // }

            // // 确保Collider是触发器
            // collider.isTrigger = true;

            // // 确保有Rigidbody2D组件（用于物理检测）
            // Rigidbody2D rb = GetComponent<Rigidbody2D>();
            // if (rb == null)
            // {
            //     rb = gameObject.AddComponent<Rigidbody2D>();
            //     rb.gravityScale = 0f; // 通常子弹不受重力影响
            //     rb.isKinematic = false; // 需要物理检测
            // }
        }

        public virtual void BeginBullet()
        {
            if (!isInitialized)
            {
                Debug.LogError("BulletBase未初始化！请在实例化后立即调用Init方法。");
                return;
            }

            // 如果是即时命中类型，在OnEnable中进行射线检测
            if (bulletType == BulletType.InstantHit)
            {
                PerformRaycast();
                StartCoroutine(DelayedDeleteInstantHit());
            }
        }

        /// <summary>
        /// 执行射线检测（仅即时命中类型）
        /// 射线方向为右（transform.right）
        /// </summary>
        protected virtual void PerformRaycast()
        {
            Vector2 rayOrigin = transform.position;
            Vector2 rayDirection = transform.right;

            // 执行2D射线检测
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDirection, raycastDistance, hitLayerMask);

            // 如果击中目标，调用Hit方法
            if (hit.collider != null)
            {
                Hit(hit.collider.gameObject, hit.point, hit.normal);
            }
        }

        /// <summary>
        /// 触发器触发（仅物理实体类型）
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 只处理物理实体类型的子弹
            if (bulletType != BulletType.Physical)
            {
                return;
            }

            // 检查是否在可碰撞的层中
            if ((hitLayerMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            // 调用Hit方法
            Vector2 hitPoint = other.ClosestPoint(transform.position);
            Vector2 hitNormal = (transform.position - (Vector3)hitPoint).normalized;
            Hit(other.gameObject, hitPoint, hitNormal);
        }
        
        IEnumerator DelayedDeleteInstantHit()
        {
            // 延迟0.5秒，删除即时命中类型的子弹
            // 等待0.5秒
            yield return new WaitForSeconds(0.1f); 
    
            // 如果子弹类型是即时命中，并且子弹池不为空，则释放子弹
            if (bulletType == BulletType.InstantHit && bulletPool != null) 
            {
                bulletPool.Release(this.gameObject);
            }
        }

        #region 纯虚方法 - 子类必须实现

        /// <summary>
        /// 击中目标时调用
        /// </summary>
        /// <param name="hitTarget">被击中的目标GameObject</param>
        /// <param name="hitPoint">击中点位置</param>
        /// <param name="hitNormal">击中点法线</param>
        protected abstract void Hit(GameObject hitTarget, Vector2 hitPoint, Vector2 hitNormal);

        #endregion

        #region Debug辅助

        /// <summary>
        /// 在Scene视图中绘制射线（仅即时命中类型）
        /// </summary>
        private void OnDrawGizmos()
        {
            if (bulletType == BulletType.InstantHit && isInitialized)
            {
                Gizmos.color = Color.red;
                Vector2 rayOrigin = transform.position;
                Vector2 rayDirection = transform.right;
                Gizmos.DrawRay(rayOrigin, rayDirection * raycastDistance);
            }
        }

        #endregion
    }
}
