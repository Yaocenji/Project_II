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

        [Tooltip("每秒消耗的电量")]
        public float drainRate = 5f;

        private bool isOn = false;

        private Battery CurrentBattery =>
            attachmentSlots.Count > 0 ? attachmentSlots[0].currentItem as Battery : null;

        public override bool CanAttach(int slotIndex, Base item)
        {
            return slotIndex == 0 && item is Battery;
        }

        protected override void OnDetached(int slotIndex, Base item)
        {
            if (slotIndex == 0)
                SetLight(false);
        }

        public override void MainInteractPress()
        {
            if (CurrentBattery != null)
                SetLight(!isOn);
        }

        private void Update()
        {
            if (!isOn) return;

            Battery battery = CurrentBattery;
            if (battery == null)
            {
                SetLight(false);
                return;
            }

            battery.charge -= drainRate * Time.deltaTime;
            if (battery.charge <= 0f)
            {
                battery.charge = 0f;
                SetLight(false);
            }
        }

        private void SetLight(bool on)
        {
            isOn = on;
            if (rcwbObject != null)
                rcwbObject.Emission = isOn ? onEmission : Color.black;
        }
    }
}
