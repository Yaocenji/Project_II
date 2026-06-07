using System.Collections.Generic;
using UnityEngine;
using Lumivara2D;

namespace ProjectII.Render
{
    /// <summary>
    /// 动态排除列表，适合运行时增删（如拾取/丢弃物品）。
    /// 每帧将当前列表提交给 PlayerVisionOccludeSystem。
    /// </summary>
    public class PlayerVisionExcludeList : MonoBehaviour
    {
        [Tooltip("需要排除遮挡判断的 LV2DObject（例如玩家自身的 Polygon）")]
        [SerializeField] private List<LV2DObject> excludedObjects = new List<LV2DObject>();

        private void LateUpdate()
        {
            PlayerVisionOccludeSystem.Instance?.SetDynamicExcludes(excludedObjects);
        }

        /// <summary>运行时动态增减排除列表</summary>
        public void AddExclude(LV2DObject obj)
        {
            if (obj != null && !excludedObjects.Contains(obj))
                excludedObjects.Add(obj);
        }

        public void RemoveExclude(LV2DObject obj)
        {
            excludedObjects.Remove(obj);
        }
    }
}
