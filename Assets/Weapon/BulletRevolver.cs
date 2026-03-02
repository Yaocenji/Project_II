using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ProjectII.Weapon
{
    public class BulletRevolver : BulletBase
    {
        private bool hitSomething = false;

        private Vector2 hitPoint;
        private Vector2 hitNormal;

        private Revolver revolver;
        
        // 
        /// <summary>
        /// 重写一下：
        /// 1、先要重置hitSomething
        /// 2、根据hit的结果，播放手枪的VFX
        /// </summary>
        public override void BeginBullet()
        {
            hitSomething = false;
            base.BeginBullet();
            
            // 先获取手枪引用
            revolver = Shooter.GetComponent<Revolver>();
            if (hitSomething)
            {
                // 调用
                revolver.PlayHitVFX(transform.position, hitPoint, true, hitNormal);
            }
            else
            {
                // 调用
                revolver.PlayHitVFX(transform.position, transform.position + transform.right * raycastDistance, false, Vector2.one);
            }
        }
        
        protected override void Hit(GameObject hitTarget, Vector2 hitPoint, Vector2 hitNormal)
        {
            hitSomething = true;
            this.hitPoint = hitPoint;
            this.hitNormal = hitNormal;
            Debug.Log("左轮击中, hitTarget: " + hitTarget.name + ", hitPoint: " + hitPoint + ", hitNormal: " + hitNormal);
        }

    }
}
