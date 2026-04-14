using UnityEngine;

namespace ProjectII.Item
{
    /// <summary>
    /// 所有背包物品的基类
    /// 物品本身不直接监听输入，所有交互均由快捷栏（Hotbar）统一监听后转发调用
    /// </summary>
    public class Base : MonoBehaviour
    {
        #region 物品属性

        /// <summary>
        /// 物品显示名称
        /// </summary>
        [Header("物品属性")]
        public string itemName = "未命名物品";

        /// <summary>
        /// 物品图标，用于背包/快捷栏 UI 显示
        /// </summary>
        public Sprite icon;

        /// <summary>
        /// 堆叠上限。为 1 表示不可堆叠；大于 1 表示可堆叠
        /// 背包格子中同类物品数量不会超过此值
        /// </summary>
        public int maxStackSize = 1;

        /// <summary>
        /// 对应的世界掉落物 Prefab（挂有 WorldItem 脚本）
        /// 玩家丢弃此物品时，由背包/快捷栏用此引用实例化掉落物
        /// </summary>
        public GameObject worldItemPrefab;

        #endregion

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

        #region 装备回调 - 由快捷栏在切换格子时调用

        /// <summary>
        /// 当此物品成为快捷栏当前装备时调用一次
        /// </summary>
        public virtual void OnEquip() { }

        /// <summary>
        /// 当此物品从快捷栏当前装备位置被切走时调用一次
        /// </summary>
        public virtual void OnUnequip() { }

        #endregion
    }
}
