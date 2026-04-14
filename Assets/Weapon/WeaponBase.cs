using UnityEngine;
using UnityEngine.Pool;

namespace ProjectII.Weapon
{
    /// <summary>
    /// 所有武器的基类
    /// 继承自 Item.Base，武器是物品的一种
    /// 武器不直接监听输入，所有输入由快捷栏（Hotbar）统一监听后转发
    /// </summary>
    public abstract class WeaponBase : Item.Base
    {
        // 将子弹池化，这里保留空实现，子类可以重写
        protected ObjectPool<GameObject> bulletPool;

        // 武器散布角度（单侧、角度）
        public float spreadAngle = 5.0f;
        // 武器射程（世界空间）
        public float range = 10.0f;
        public float SpreadAngle{get => spreadAngle;}
        public float Range{get => range;}

        // 能否开火标志位（由派生类根据具体规则设置）
        protected bool canFire = true;
        
        public bool CanFire {get => canFire;}

        #region 纯虚方法 - 子类必须实现

        /// <summary>
        /// 主要攻击被按下的瞬间调用一次
        /// </summary>
        public abstract override void MainInteractPress();

        /// <summary>
        /// 主要攻击按下的持续时间内，每个Update都被调用
        /// </summary>
        public abstract override void MainInteractHold();

        /// <summary>
        /// 主要攻击被抬起的瞬间调用一次
        /// </summary>
        public abstract override void MainInteractRelease();

        /// <summary>
        /// 次要攻击被按下的瞬间调用一次
        /// </summary>
        public abstract override void SecondaryInteractPress();

        /// <summary>
        /// 次要攻击按下的持续时间内，每个Update都被调用
        /// </summary>
        public abstract override void SecondaryInteractHold();

        /// <summary>
        /// 次要攻击被抬起的瞬间调用一次
        /// </summary>
        public abstract override void SecondaryInteractRelease();

        /// <summary>
        /// 重新装填按下时的回调
        /// </summary>
        public abstract override void ReloadPress();

        #endregion
    }
}
