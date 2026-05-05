using System;

using System.Collections.Generic;
using UnityEngine;

namespace ProjectII.Manager
{
    /// <summary>
    /// 所有的可交互物品都会在这里注册
    /// </summary>
    [DefaultExecutionOrder(-98)]
    public class InteractManager : MonoBehaviour
    {
        private static InteractManager instance;
        /// <summary>
        /// GameSceneManager单例实例
        /// </summary>
        public static InteractManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<InteractManager>();
                }
                return instance;
            }
        }

        private List<Interact.InteractableBase> nearbyInteractables = new List<Interact.InteractableBase>();

        /// <summary>
        /// 当前玩家附近的可交互物品列表（只读）
        /// </summary>
        public IReadOnlyList<Interact.InteractableBase> NearbyInteractables => nearbyInteractables;
        
        /// <summary>
        /// 当前的多个可交互物品中选择的那个
        /// </summary>
        private Interact.InteractableBase chosenInteractable;

        private void Awake()
        {
            // 确保只有一个GameSceneManager实例
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Debug.LogWarning("GameSceneManager 单例已经存在，销毁新创建的实例。");
                Destroy(gameObject);
            }
            if (InputManager.Instance != null)
                InputManager.Instance.InputAction.Character.interaction.started += OnInteractionInput;
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.InputAction.Character.interaction.started -= OnInteractionInput;
        }


        /// <summary>
        /// 注册附近可交互物品
        /// 当可交互物品进入玩家交互范围时调用
        /// </summary>
        public void RegisterNearbyInteractable(Interact.InteractableBase item)
        {
            if (item != null && !nearbyInteractables.Contains(item))
            {
                nearbyInteractables.Add(item);
                Debug.Log($"可交互物品已加入附近列表: {item.name}，当前数量: {nearbyInteractables.Count}");
            }
        }

        /// <summary>
        /// 注销附近可交互物品
        /// 当可交互物品离开玩家交互范围或被销毁时调用
        /// </summary>
        public void UnregisterNearbyInteractable(Interact.InteractableBase item)
        {
            if (nearbyInteractables.Remove(item))
            {
                Debug.Log($"可交互物品已从附近列表移除: {item.name}，当前数量: {nearbyInteractables.Count}");
            }
        }

        private float chooseInterval = 0.2f;
        private float lastChooseTime;

        private void Update()
        {
            if (Time.time - lastChooseTime >= chooseInterval)
            {
                lastChooseTime = Time.time;
                ChooseInteractable();
            }
        }

        private void ChooseInteractable()
        {
            int chosenIndex = -1;
            float chosenDistance = float.MaxValue;
            for (int i = 0; i < nearbyInteractables.Count; i++)
            {
                float currentDistance = Vector2.Distance(nearbyInteractables[i].transform.position, GameSceneManager.Instance.CurrentMouse.VirtualMouseWorldPosition);
                if (currentDistance < chosenDistance)
                {
                    chosenIndex = i;
                    chosenDistance = currentDistance;
                }
            }
            if (chosenIndex != -1)
            {
                var oldChosenInteractable = chosenInteractable;
                
                chosenInteractable = nearbyInteractables[chosenIndex];
                
                chosenInteractable.SetChosen(true);
                if (oldChosenInteractable != null && oldChosenInteractable != chosenInteractable)
                {
                    oldChosenInteractable.SetChosen(false);
                }
            }

            if (chosenInteractable != null && !chosenInteractable.canInteract)
            {
                chosenInteractable.SetChosen(false);
                chosenInteractable = null;
            }
        }

        /// <summary>
        /// 触发交互按键的时候，调用该函数
        /// </summary>
        /// <param name="ctx"></param>
        private void OnInteractionInput(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (chosenInteractable != null)
            {
                chosenInteractable.OnInteractionInput();
            }
        }
    }
}
