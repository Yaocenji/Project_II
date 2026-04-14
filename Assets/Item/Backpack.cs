using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Item
{
    /// <summary>
    /// 背包脚本，与快捷栏并列独立，玩家持有一个。
    /// 每个格子是一个 List&lt;Base&gt;，支持同类物品堆叠。
    /// 是否可堆叠由该格子零号物品的 maxStackSize 决定。
    /// </summary>
    public class Backpack : MonoBehaviour
    {
        [Header("背包设置")]
        [SerializeField] private int slotCount = 20;

        /// <summary>
        /// 背包格子列表。每个格子是一个 List&lt;Base&gt;。
        /// 格子为空时，对应 List 的 Count 为 0。
        /// </summary>
        public List<Base>[] slots;

        private void Awake()
        {
            slots = new List<Base>[slotCount];
            for (int i = 0; i < slotCount; i++)
                slots[i] = new List<Base>();
        }

        /// <summary>
        /// 尝试将物品放入指定格子。
        /// 格子为空时直接放入；
        /// 格子有同类物品且未达堆叠上限时叠入；
        /// 否则拒绝放入。
        /// </summary>
        /// <param name="item">要放入的物品</param>
        /// <param name="slotIndex">目标格子序号</param>
        /// <returns>是否放入成功</returns>
        public bool PutItem(Base item, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;

            List<Base> slot = slots[slotIndex];

            // 格子为空，直接放入
            if (slot.Count == 0)
            {
                slot.Add(item);
                item.gameObject.SetActive(false);
                return true;
            }

            // 格子有物品：检查类型是否相同，且未达堆叠上限
            Base first = slot[0];
            if (first.GetType() != item.GetType()) return false;
            if (slot.Count >= first.maxStackSize) return false;

            slot.Add(item);
            item.gameObject.SetActive(false);
            return true;
        }

        /// <summary>
        /// 尝试自动寻找合适的格子放入物品（优先叠入同类格，其次找空格）。
        /// </summary>
        /// <param name="item">要放入的物品</param>
        /// <returns>是否放入成功</returns>
        public bool PutItemAuto(Base item)
        {
            // 优先叠入同类且未满的格子
            for (int i = 0; i < slots.Length; i++)
            {
                List<Base> slot = slots[i];
                if (slot.Count == 0) continue;
                if (slot[0].GetType() != item.GetType()) continue;
                if (slot.Count >= slot[0].maxStackSize) continue;
                return PutItem(item, i);
            }

            // 再找空格
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Count == 0)
                    return PutItem(item, i);
            }

            return false;
        }

        /// <summary>
        /// 从指定格子取出一个物品（取出堆叠顶部）。
        /// </summary>
        /// <param name="slotIndex">目标格子序号</param>
        /// <returns>取出的物品，格子为空则返回 null</returns>
        public Base TakeItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return null;

            List<Base> slot = slots[slotIndex];
            if (slot.Count == 0) return null;

            Base item = slot[slot.Count - 1];
            slot.RemoveAt(slot.Count - 1);
            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>
        /// 查询指定格子的物品数量
        /// </summary>
        public int GetStackCount(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return 0;
            return slots[slotIndex].Count;
        }

        /// <summary>
        /// 查询指定格子的零号物品（不取出）
        /// </summary>
        public Base PeekItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return null;
            List<Base> slot = slots[slotIndex];
            if (slot.Count == 0) return null;
            return slot[0];
        }
    }
}
