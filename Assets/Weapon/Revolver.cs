using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.Pool;


namespace ProjectII.Weapon
{
    public class Revolver : WeaponBase
    {

        public GameObject bulletPrefab;
        
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

        private int currentAmmo;
        private bool isReloading;
        private float lastFireTime;
        
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

        private void GetShootBulletFromPool(GameObject bullet)
        {
            // 子弹的发射位置是枪口，以后用一个函数表示
            bullet.transform.position = transform.position;
            // 这里设置子弹的transform.right为枪口方向
            bullet.transform.right = transform.right;
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
                yield return new WaitForSeconds(0.1f);
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
                Debug.Log("添加 Wall 参数: " + occlusionDistance / (occlusionDistance + 1));
            }
            else
            {
                emitter.SetParameter("Wall", 0);
            }
        }

        
        protected override void MainAttackPress()
        {
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

            // Debug.Log("Revolver MainAttackPress");
            // 从池子里激活一枚子弹
            GameObject bullet = bulletPool.Get();
            
            // 添加镜头抖动
            var cameraShake = FindObjectOfType<CinemachineShake>();
            cameraShake.Shake(.15f, -transform.right, .1f);
            // 添加音效
            shootSFX_Emitter.Play();
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
