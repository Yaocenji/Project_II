using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.SceneItems
{
    // 控制自发光物体的两种状态：开和关
    [RequireComponent(typeof(Collider2D))]
    public class EmitLightSwitcher : InteractableBase
    {
        public bool isOn = false;

        /// <summary>
        /// 自发光物体
        /// </summary>
        public List<RCObject> lightObjects;

        [Header("开灯状态参数")]
        [ColorUsage(false, true)]
        public Color lightColorUp = Color.white;
        [Header("关灯状态参数")]
        [ColorUsage(false, true)]
        public Color lightColorDown = Color.white;

        [Header("灯开关颜色")]
        [ColorUsage(false, true)]
        public Color switchColor = Color.white;

        protected override void Awake()
        {
            base.Awake();

            foreach (RCObject obj in lightObjects)
            {
                obj.emissionColor = isOn ? lightColorUp : lightColorDown;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("玩家进入触发器，进入可交互状态");
                EnterInteractableRange();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("玩家离开触发器，进入不可交互状态");
                ExitInteractableRange();
            }
        }

        protected override void OnInteract()
        {
            Debug.Log("尝试切换灯光状态");

            isOn = !isOn;
            foreach (RCObject obj in lightObjects)
            {
                obj.emissionColor = isOn ? lightColorUp : lightColorDown;
            }
        }

        protected override void OnEnterInteractRange()
        {
            // 临时代码：自己变成蓝色
            GetComponent<RCObject>().emissionColor = switchColor;
        }

        protected override void OnExitInteractRange()
        {
            // 临时代码：自己变成黑色
            GetComponent<RCObject>().emissionColor = Color.black;
        }
    }
}
