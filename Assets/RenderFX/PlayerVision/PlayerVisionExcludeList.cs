using System.Collections.Generic;
using UnityEngine;
using RadianceCascadesWorldBVH;

namespace ProjectII.Render
{
    /// <summary>
    /// 动态排除列表，适合运行时增删（如拾取/丢弃物品）。
    /// 每帧将当前列表提交给 PlayerVisionOccludeSystem。
    /// </summary>
    public class PlayerVisionExcludeList : MonoBehaviour
    {
        [Tooltip("需要排除遮挡判断的 RCWBObject（例如玩家自身的 Polygon）")]
        [SerializeField] private List<RCWBObject> excludedObjects = new List<RCWBObject>();

        private void LateUpdate()
        {
            PlayerVisionOccludeSystem.Instance?.SetDynamicExcludes(excludedObjects);
        }

        /// <summary>运行时动态增减排除列表</summary>
        public void AddExclude(RCWBObject obj)
        {
            if (obj != null && !excludedObjects.Contains(obj))
                excludedObjects.Add(obj);
        }

        public void RemoveExclude(RCWBObject obj)
        {
            excludedObjects.Remove(obj);
        }
    }
}
