using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ProjectII.Weapon
{
    public class BulletRevolver : BulletBase
    {
        protected override void Hit(GameObject hitTarget, Vector2 hitPoint, Vector2 hitNormal)
        {
            Debug.Log("左轮击中, hitTarget: " + hitTarget.name + ", hitPoint: " + hitPoint + ", hitNormal: " + hitNormal);
        }
    }
}
