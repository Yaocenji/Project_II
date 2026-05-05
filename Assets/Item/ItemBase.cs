using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Item
{
    [Serializable]
    public struct AttachmentSlot
    {
        public string slotName;
        public Base currentItem;
    }

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

        /// <summary>
        /// 锚点名称，用于匹配玩家身上的 ItemAnchor。
        /// 为空则不挂接锚点。
        /// </summary>
        [Header("锚点")]
        public string anchorName;

        /// <summary>
        /// 附加槽列表，在 Inspector 里配置槽名和初始物品
        /// </summary>
        [Header("附加槽")]
        public List<AttachmentSlot> attachmentSlots = new List<AttachmentSlot>();

        /// <summary>
        /// 有附件槽的物品不可堆叠
        /// </summary>
        public bool IsStackable => maxStackSize > 1 && attachmentSlots.Count == 0;

        #endregion

        /// <summary>
        /// 尝试将物品放入指定附加槽。槽为空且 CanAttach 通过时放入。
        /// </summary>
        public bool Attach(int slotIndex, Base item)
        {
            if (slotIndex < 0 || slotIndex >= attachmentSlots.Count) return false;
            if (attachmentSlots[slotIndex].currentItem != null) return false;
            if (!CanAttach(slotIndex, item)) return false;

            AttachmentSlot slot = attachmentSlots[slotIndex];
            slot.currentItem = item;
            attachmentSlots[slotIndex] = slot;
            item.gameObject.SetActive(false);
            OnAttached(slotIndex, item);
            return true;
        }

        /// <summary>
        /// 从指定附加槽取出物品。
        /// </summary>
        public Base Detach(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= attachmentSlots.Count) return null;
            if (attachmentSlots[slotIndex].currentItem == null) return null;

            AttachmentSlot slot = attachmentSlots[slotIndex];
            Base item = slot.currentItem;
            slot.currentItem = null;
            attachmentSlots[slotIndex] = slot;
            item.gameObject.SetActive(true);
            OnDetached(slotIndex, item);
            return item;
        }

        /// <summary>
        /// 子类重写以限制某个槽能接受的物品类型。默认全部允许。
        /// </summary>
        public virtual bool CanAttach(int slotIndex, Base item) => true;

        /// <summary>物品被放入附加槽后调用</summary>
        protected virtual void OnAttached(int slotIndex, Base item) { }

        /// <summary>物品被从附加槽取出后调用</summary>
        protected virtual void OnDetached(int slotIndex, Base item) { }

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

        /// <summary>
        /// 查找玩家身上匹配的 ItemAnchor 并将自身挂接上去，Transform 归零。
        /// anchorName 为空时不做任何操作。
        /// </summary>
        public void BindToAnchor()
        {
            if (string.IsNullOrEmpty(anchorName)) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning($"Item.Base: 找不到 Player，无法绑定锚点。", this);
                return;
            }

            ItemAnchor[] anchors = player.GetComponentsInChildren<ItemAnchor>();
            foreach (ItemAnchor anchor in anchors)
            {
                if (anchor.anchorName == anchorName)
                {
                    transform.SetParent(anchor.transform, false);
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    return;
                }
            }

            Debug.LogWarning($"Item.Base: 在玩家身上找不到 anchorName 为 '{anchorName}' 的 ItemAnchor。", this);
        }

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
