using UnityEngine;

namespace ProjectII.Item
{
    /// <summary>
    /// 物品锚点，挂载在玩家的子 GameObject 上。
    /// 每个 ItemAnchor 通过 anchorName 标识，手持物的 anchorName 匹配时将自动挂接到此锚点。
    /// </summary>
    public class ItemAnchor : MonoBehaviour
    {
        [Tooltip("锚点名称，用于与手持物的 anchorName 匹配")]
        public string anchorName;
    }
}
