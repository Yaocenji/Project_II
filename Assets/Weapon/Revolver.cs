using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using ProjectII.FX;
using UnityEngine.Pool;


namespace ProjectII.Weapon
{
    public class Revolver : WeaponBase
    {

        /// <summary>
        /// 子弹预制体
        /// </summary>
        public GameObject bulletPrefab;

        /// <summary>
        /// 曳光弹特效脚本
        /// </summary>
        public BulletRevolverFX_Tracer tracerVFX;
        
        /// <summary>
        /// 射击音效
        /// </summary>
        public FMODUnity.StudioEventEmitter shootSFX_Emitter;
        /// <summary>
        /// 换弹音效
        /// </summary>
        public FMODUnity.StudioEventEmitter reloadSFX_Emitter;


        [Header("Revolver Settings")]
        [SerializeField] private int maxAmmo = 6;
        [SerializeField] private float reloadTime = 2f;
        [SerializeField] private float fireInterval = 0.75f;

        [Header("Spread Settings")]
        [SerializeField] private float baseSpreadAngle = 2f; // 基础散布值
        [SerializeField] private float maxMovementSpread = 8f; // 最大移动散布值
        [SerializeField] private float maxSpreadMovementSpeed = 8f; // 最大散布的移动速度
        [SerializeField] private float maxRotationSpread = 5f; // 最大旋转散布值
        [SerializeField] private float maxRotationSpeed = 180f; // 最大旋转速度（度/秒）
        [SerializeField] private float spreadPerShot = 3f; // 每次射击增加的散布值
        [SerializeField] private float maxShootSpread = 12f; // 最大射击散布值
        [SerializeField] private float spreadRecoverySpeed = 10f; // 散布值恢复速度（度/秒）

        private int currentAmmo;
        private bool isReloading;
        private float lastFireTime;
        
        // 散布值组成部分
        private float currentShootSpread = 0f; // 当前射击散布值
        
        protected override void Awake()
        {
            base.Awake();

            bulletPool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    // 创建实例
                    GameObject bullet = Instantiate(bulletPrefab);
                    // 这是手枪，所以是即时命中类型
                    bullet.GetComponent<BulletRevolver>().Init(gameObject, BulletType.InstantHit, bulletPool);
                    // 预制体已经设为了不激活，所以这里不需要设置
                    // 因为Hit在OnEnable中进行
                    // 所以一旦被激活，子弹就会进行射线检测
                    // 所以千万不能在这里设置Active
                    return bullet;
                },
                actionOnGet: (bullet) =>
                {
                    GetShootBulletFromPool(bullet);
                },
                actionOnRelease: (bullet) =>
                {
                    bullet.SetActive(false);
                },
                actionOnDestroy: (bullet) =>
                {
                    Destroy(bullet);
                },
                collectionCheck: false,
                defaultCapacity: 3,
                maxSize: 5
            );
        }

        protected void Start()
        {
            currentAmmo = maxAmmo;
            // 初始化lastFireTime允许立即射击
            lastFireTime = -fireInterval; 
            
            // 初始化枪声参数更新协程
            StartCoroutine(LowUpdateSFXParameters());
        }

        protected override void Update()
        {
            base.Update();
            
            // 1. 更新能否开火标志位（根据玩家是否奔跑）
            UpdateCanFire();
            
            // 2. 更新散布值
            UpdateSpreadAngle();
        }

        /// <summary>
        /// 更新能否开火标志位
        /// 如果玩家正在奔跑，则不能开火
        /// </summary>
        private void UpdateCanFire()
        {
            var playerCharacter = ProjectII.Manager.GameSceneManager.Instance.CurrentPlayerCharacter;
            if (playerCharacter != null)
            {
                canFire = playerCharacter.CurrentSpeedState != ProjectII.Character.CharacterController.SpeedState.Run;
            }
            else
            {
                canFire = true; // 如果找不到玩家，默认可以开火
            }
        }

        /// <summary>
        /// 更新散布值
        /// 散布值 = 基础散布值 + 移动散布值 + 旋转散布值 + 射击散布值
        /// </summary>
        private void UpdateSpreadAngle()
        {
            var playerCharacter = ProjectII.Manager.GameSceneManager.Instance.CurrentPlayerCharacter;
            if (playerCharacter == null)
            {
                // 如果没有玩家引用，保持当前散布值
                return;
            }

            // 获取玩家的 Rigidbody2D 组件
            Rigidbody2D playerRb = playerCharacter.GetComponent<Rigidbody2D>();
            if (playerRb == null)
            {
                // 如果没有刚体组件，保持当前散布值
                return;
            }

            // 1. 基础散布值
            float baseSpread = baseSpreadAngle;

            // 2. 移动散布值（从 Rigidbody2D 获取速度）
            float playerSpeed = playerRb.velocity.magnitude;
            float movementSpread = Mathf.Lerp(0f, maxMovementSpread, Mathf.Clamp01(playerSpeed / maxSpreadMovementSpeed));

            // 3. 旋转散布值（从 Rigidbody2D 获取角速度，单位是度/秒）
            float currentRotationSpeed = Mathf.Abs(playerRb.angularVelocity);
            float rotationSpread = Mathf.Lerp(0f, maxRotationSpread, Mathf.Clamp01(currentRotationSpeed / maxRotationSpeed));

            // 4. 射击散布值（在射击时增加，非射击时恢复）
            // 这部分在MainAttackPress中增加，这里只负责恢复
            if (currentShootSpread > 0f)
            {
                currentShootSpread -= spreadRecoverySpeed * Time.deltaTime;
                currentShootSpread = Mathf.Max(0f, currentShootSpread);
            }

            // 合计散布值
            spreadAngle = baseSpread + movementSpread + rotationSpread + currentShootSpread;
        }

        private void GetShootBulletFromPool(GameObject bullet)
        {
            // 子弹的发射位置是枪口，以后用一个函数表示
            bullet.transform.position = transform.position;
            
            // 应用散布值：在 ±spreadAngle 范围内随机偏移
            float randomSpreadAngle = Random.Range(-spreadAngle, spreadAngle);
            float baseAngle = Mathf.Atan2(transform.right.y, transform.right.x) * Mathf.Rad2Deg;
            float finalAngle = baseAngle + randomSpreadAngle;
            
            // 设置子弹的射击方向（应用散布后的方向）
            Vector2 shootDirection = new Vector2(Mathf.Cos(finalAngle * Mathf.Deg2Rad), Mathf.Sin(finalAngle * Mathf.Deg2Rad));
            bullet.transform.right = shootDirection;
            
            // 这里直接设置Active，子弹会立即进行射线检测
            bullet.SetActive(true);
            // 开始子弹
            bullet.GetComponent<BulletBase>().BeginBullet();
        }

        IEnumerator LowUpdateSFXParameters()
        {
            while (true)
            {
                // 执行你的逻辑
                if (shootSFX_Emitter.IsPlaying())
                {
                    UpdateSFXParameters(ref shootSFX_Emitter);
                }
                if (reloadSFX_Emitter.IsPlaying())
                {
                    UpdateSFXParameters(ref reloadSFX_Emitter);
                }

                // 等待指定时间后再继续
                yield return new WaitForSeconds(0.05f);
            }
        }

        private void UpdateSFXParameters(ref FMODUnity.StudioEventEmitter emitter)
        {
            // 更新音效参数
            Vector2 position = transform.position;
            Vector2 playerPosition = ProjectII.Manager.GameSceneManager.Instance.CurrentPlayerCharacter.transform.position;
            
            Vector2 direction = (playerPosition - position);
            float distance = direction.magnitude;
            direction = direction.normalized;
            // 射线检测
            var hit0 = Physics2D.Raycast(position, direction, distance, LayerMask.GetMask("Wall"));
            var hit1 = Physics2D.Raycast(playerPosition, -direction, distance, LayerMask.GetMask("Wall"));
            if (hit0.collider != null && hit1.collider != null)
            {
                float occlusionDistance = distance - (hit0.distance + hit1.distance);
                emitter.SetParameter("Wall", occlusionDistance / (occlusionDistance + 1));
                // Debug.Log("添加 Wall 参数: " + occlusionDistance / (occlusionDistance + 1));
            }
            else
            {
                emitter.SetParameter("Wall", 0);
            }
        }

        
        protected override void MainAttackPress()
        {
            // 检查能否开火标志位
            if (!canFire) return;

            // 如果正在换弹，不能射击
            if (isReloading) return;

            // 检查射击冷却
            if (Time.time < lastFireTime + fireInterval) return;

            // 检查弹药
            if (currentAmmo <= 0)
            {
                // 弹药不足，自动换弹
                StartCoroutine(ReloadCoroutine());
                return;
            }

            // 更新射击时间和弹药
            lastFireTime = Time.time;
            currentAmmo--;

            // 增加射击散布值
            currentShootSpread += spreadPerShot;
            currentShootSpread = Mathf.Min(currentShootSpread, maxShootSpread);

            // Debug.Log("Revolver MainAttackPress");
            // 从池子里激活一枚子弹
            GameObject bullet = bulletPool.Get();
            
            // 添加镜头抖动
            var cameraShake = FindObjectOfType<FX.CinemachineShake>();
            cameraShake.Shake(.15f, -transform.right, .1f);
            // 添加音效
            shootSFX_Emitter.Play();
            UpdateSFXParameters(ref shootSFX_Emitter);
        }

        /// <summary>
        /// 回调函数，子弹进行射线检测后，就调用这个
        /// </summary>
        public void PlayHitVFX(Vector3 startPos, Vector3 endPos, bool hited, Vector2 hitNormal)
        {
            tracerVFX.SetTracePosition(startPos, endPos);
            tracerVFX.StartTracer();
        }
        
        protected override void MainAttackHold()
        {
            // Debug.Log("Revolver MainAttackHold");
        }

        protected override void MainAttackRelease()
        {
            // Debug.Log("Revolver MainAttackRelease");
        }

        protected override void SecondaryAttackPress()
        {
            // Debug.Log("Revolver SecondaryAttackPress");
        }

        protected override void SecondaryAttackHold()
        {
            // Debug.Log("Revolver SecondaryAttackHold");
        }

        protected override void SecondaryAttackRelease()
        {
            // Debug.Log("Revolver SecondaryAttackRelease");
        }

        protected override void ReloadPress()
        {
            // 如果正在换弹或弹药已满，不进行换弹
            if (isReloading || currentAmmo >= maxAmmo) return;

            StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;
            
            // Debug.Log("Revolver Reloading...");
            if (reloadSFX_Emitter != null)
            {
                reloadSFX_Emitter.Play();
                UpdateSFXParameters(ref reloadSFX_Emitter);
            }

            // 等待换弹时间
            yield return new WaitForSeconds(reloadTime);

            // 补满弹药
            currentAmmo = maxAmmo;
            isReloading = false;
            // Debug.Log("Revolver Reloaded");
        }
    }
}
