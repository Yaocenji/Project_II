using UnityEngine;

namespace ProjectII.Item
{
    /// <summary>
    /// 所有背包物品的基类
    /// 物品本身不直接监听输入，所有交互均由快捷栏（Hotbar）统一监听后转发调用
    /// </summary>
    public class Base : MonoBehaviour
    {
        #region 交互虚方法 - 由快捷栏转发调用，子类按需重写

        /// <summary>
        /// 主要交互被按下的瞬间调用一次
        /// 由快捷栏在 InputAction 的 mainAttack.started 时调用
        /// </summary>
        public virtual void MainInteractPress() { }

        /// <summary>
        /// 主要交互按下的持续时间内，每个 Update 都被调用
        /// 由快捷栏在 Update 中检测 mainAttack 持续按下时调用
        /// </summary>
        public virtual void MainInteractHold() { }

        /// <summary>
        /// 主要交互被抬起的瞬间调用一次
        /// 由快捷栏在 InputAction 的 mainAttack.canceled 时调用
        /// </summary>
        public virtual void MainInteractRelease() { }

        /// <summary>
        /// 次要交互被按下的瞬间调用一次
        /// 由快捷栏在 InputAction 的 secondaryAttack.started 时调用
        /// </summary>
        public virtual void SecondaryInteractPress() { }

        /// <summary>
        /// 次要交互按下的持续时间内，每个 Update 都被调用
        /// 由快捷栏在 Update 中检测 secondaryAttack 持续按下时调用
        /// </summary>
        public virtual void SecondaryInteractHold() { }

        /// <summary>
        /// 次要交互被抬起的瞬间调用一次
        /// 由快捷栏在 InputAction 的 secondaryAttack.canceled 时调用
        /// </summary>
        public virtual void SecondaryInteractRelease() { }

        /// <summary>
        /// 装填操作被按下时调用一次
        /// 由快捷栏在 InputAction 的 reload.started 时调用
        /// 非武器类物品通常不需要重写此方法
        /// </summary>
        public virtual void ReloadPress() { }

        #endregion
    }
}
