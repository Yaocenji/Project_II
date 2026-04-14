using UnityEngine;
using RadianceCascadesWorldBVH;

namespace ProjectII.Item
{
    /// <summary>
    /// 手电筒物品
    /// 按下 MainAttack 切换开/关，通过修改挂接的 RCWBObject.Emission 实现发光效果
    /// </summary>
    public class Flashlight : Base
    {
        [Header("手电筒设置")]
        [Tooltip("挂接的 RCWBObject，控制其 Emission 实现发光")]
        public RCWBObject rcwbObject;

        [ColorUsage(false, true)]
        [Tooltip("开灯时的 HDR 发光颜色")]
        public Color onEmission = Color.white;

        private bool isOn = false;

        public override void MainInteractPress()
        {
            isOn = !isOn;
            if (rcwbObject != null)
                rcwbObject.Emission = isOn ? onEmission : Color.black;
        }
    }
}
