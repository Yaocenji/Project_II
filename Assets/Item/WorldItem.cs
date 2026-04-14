using UnityEngine;

namespace ProjectII.Item
{
    /// <summary>
    /// 世界中的掉落物脚本。
    /// 持有对应手持物的 Prefab 引用，被拾取时实例化手持物并交给背包。
    /// 继承 InteractableBase，玩家进入范围后可按交互键拾取。
    /// </summary>
    public class WorldItem : ProjectII.SceneItems.InteractableBase
    {
        /// <summary>
        /// 对应的手持物 Prefab（挂有 Item.Base 派生类的预制体）
        /// 可由关卡设计者在 Inspector 中指定，或由背包丢弃逻辑在实例化时赋值
        /// </summary>
        [Header("物品设置")]
        public GameObject heldItemPrefab;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                EnterInteractableRange();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                ExitInteractableRange();
        }

        protected override void OnInteract()
        {
            if (heldItemPrefab == null)
            {
                Debug.LogWarning($"WorldItem: heldItemPrefab 未指定，无法拾取。", this);
                return;
            }

            // 实例化手持物
            GameObject go = Instantiate(heldItemPrefab);
            Base item = go.GetComponent<Base>();
            if (item == null)
            {
                Debug.LogWarning($"WorldItem: heldItemPrefab 上没有 Item.Base 组件，无法拾取。", this);
                Destroy(go);
                return;
            }

            // 尝试放入背包
            Backpack backpack = FindBackpack();
            if (backpack != null && backpack.PutItemAuto(item))
            {
                Destroy(gameObject);
                return;
            }

            // 背包已满，销毁刚创建的实例，保持自身不变
            Destroy(go);
        }

        private Backpack FindBackpack()
        {
            // 通过 Player 标签找到背包组件
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return null;
            return player.GetComponentInChildren<Backpack>();
        }
    }
}
