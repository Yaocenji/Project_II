using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.SceneItems
{
    // 控制自发光物体的两种状态：开和关
    [RequireComponent(typeof(Collider2D))]
    public class EmitLightSwitcher : MonoBehaviour
    {
        public bool isOn = false;
        
        /// <summary>
        /// 自发光物体
        /// </summary>
        public List<RCObject> lightObjects;

        private Project_II.InputSystem.InputAction_0 inputActions;
        
        private bool canInteract = false;

        [Header("开灯状态参数")]
        [ColorUsage(false, true)] 
        public Color lightColorUp = Color.white;
        [Header("关灯状态参数")]
        [ColorUsage(false, true)] 
        public Color lightColorDown = Color.white;
        
        [Header("灯开关颜色")]
        [ColorUsage(false, true)] 
        public Color switchColor = Color.white;

        private void Start()
        {
            foreach (RCObject obj in lightObjects)
            {
                if (isOn){
                    obj.emissionColor = lightColorUp;
                }else{
                    obj.emissionColor = lightColorDown;
                }
            }
            
            // 绑定
            GetInputActionFromInputManager();
            inputActions.Character.interaction.started += TrySwitchLight;
        }

        /// <summary>
        /// 从InputManager获取InputAction引用
        /// </summary>
        private void GetInputActionFromInputManager()
        {
            if (inputActions == null)
            {
                ProjectII.Manager.InputManager inputManager = ProjectII.Manager.InputManager.Instance;
                if (inputManager != null && inputManager.InputAction != null)
                {
                    inputActions = inputManager.InputAction;
                }
                else
                {
                    Debug.LogError("无法从InputManager获取InputAction引用！请确保场景中存在InputManager。");
                }
            }
        }

        /// <summary>
        /// 尝试切换灯光状态（如果是可交互状态就能切换）
        /// </summary>
        public void TrySwitchLight(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            Debug.Log("尝试切换灯光状态");
            
            if (!canInteract) return;
            
            isOn = !isOn;
            foreach (RCObject obj in lightObjects)
            {
                if (isOn){
                    obj.emissionColor = lightColorUp;
                }else{
                    obj.emissionColor = lightColorDown;
                }
            }
        }

        /// <summary>
        /// 玩家进入触发器时，进入可交互状态
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                canInteract = true;
                Debug.Log("玩家进入触发器，进入可交互状态");

                // 临时代码：自己变成蓝色
                GetComponent<RCObject>().emissionColor = switchColor;
            }  
        }
        
        /// <summary>
        /// 玩家离开触发器时，进入不可交互状态
        /// </summary>
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                canInteract = false;
                Debug.Log("玩家离开触发器，进入不可交互状态");
                // 临时代码：自己变成黑色
                GetComponent<RCObject>().emissionColor = Color.black;
            }
        }
    }
}
