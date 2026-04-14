using UnityEngine;
using Project_II.InputSystem;
using ProjectII.Manager;

namespace ProjectII.Item
{
    /// <summary>
    /// 快捷栏脚本，与背包并列独立
    /// 负责统一监听输入并将交互转发给当前装备的物品
    /// 快捷栏是整个物品使用系统的核心枢纽
    /// </summary>
    public class Hotbar : MonoBehaviour
    {
        [Header("快捷栏设置")]
        [SerializeField] private int slotCount = 5;

        /// <summary>
        /// 物品列表，存储快捷栏中的物品
        /// 如果该位置为空值，那么相当于这个格子没东西
        /// </summary>
        public Base[] items;

        /// <summary>
        /// 当前装备的物品序号（0 ~ slotCount-1）
        /// 如果该格子为空，就相当于是空手
        /// </summary>
        [SerializeField] private int currentSlotIndex = 0;

        /// <summary>
        /// 从 InputManager 处获取的 InputAction 引用
        /// </summary>
        private InputAction_0 inputActions;

        // 输入状态缓存
        private bool mainAttackIsHolding;
        private bool secondaryAttackIsHolding;

        /// <summary>
        /// 当前装备的物品（只读）
        /// </summary>
        public Base CurrentItem
        {
            get
            {
                if (items == null || currentSlotIndex < 0 || currentSlotIndex >= items.Length)
                    return null;
                return items[currentSlotIndex];
            }
        }

        private void Awake()
        {
            // 仅在 Inspector 未赋值时才初始化，避免覆盖 Inspector 中的赋值
            if (items == null || items.Length == 0)
                items = new Base[slotCount];

            // 从 InputManager 获取 InputAction 引用
            GetInputActionFromInputManager();
        }

        private void OnEnable()
        {
            if (inputActions != null)
            {
                inputActions.Enable();
                inputActions.Character.mainAttack.started += OnMainAttackStarted;
                inputActions.Character.mainAttack.canceled += OnMainAttackCanceled;
                inputActions.Character.secondaryAttack.started += OnSecondaryAttackStarted;
                inputActions.Character.secondaryAttack.canceled += OnSecondaryAttackCanceled;
                inputActions.Character.reload.started += OnReloadStarted;
            }
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                inputActions.Character.mainAttack.started -= OnMainAttackStarted;
                inputActions.Character.mainAttack.canceled -= OnMainAttackCanceled;
                inputActions.Character.secondaryAttack.started -= OnSecondaryAttackStarted;
                inputActions.Character.secondaryAttack.canceled -= OnSecondaryAttackCanceled;
                inputActions.Character.reload.started -= OnReloadStarted;
                inputActions.Disable();
            }
        }

        private void Update()
        {
            if (inputActions == null) return;

            Base current = CurrentItem;
            if (current == null) return;

            // 检测主要攻击的持续按下状态
            if (mainAttackIsHolding && inputActions.Character.mainAttack.IsPressed())
            {
                current.MainInteractHold();
            }

            // 检测次要攻击的持续按下状态
            if (secondaryAttackIsHolding && inputActions.Character.secondaryAttack.IsPressed())
            {
                current.SecondaryInteractHold();
            }
        }

        #region 输入回调

        private void OnMainAttackStarted(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!mainAttackIsHolding)
            {
                mainAttackIsHolding = true;
                CurrentItem?.MainInteractPress();

                Debug.Log("按下11");
            }
        }

        private void OnMainAttackCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (mainAttackIsHolding)
            {
                mainAttackIsHolding = false;
                CurrentItem?.MainInteractRelease();
            }
        }

        private void OnSecondaryAttackStarted(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!secondaryAttackIsHolding)
            {
                secondaryAttackIsHolding = true;
                CurrentItem?.SecondaryInteractPress();
            }
        }

        private void OnSecondaryAttackCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (secondaryAttackIsHolding)
            {
                secondaryAttackIsHolding = false;
                CurrentItem?.SecondaryInteractRelease();
            }
        }

        private void OnReloadStarted(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            CurrentItem?.ReloadPress();
        }

        #endregion

        #region 快捷栏操作

        /// <summary>
        /// 放入物品到指定格子
        /// 如果该位置没有物品，则放入；如果有物品，则拒绝放入
        /// </summary>
        /// <param name="item">要放入的物品</param>
        /// <param name="slotIndex">目标格子序号</param>
        /// <returns>是否放入成功</returns>
        public bool PutItem(Base item, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= items.Length) return false;
            if (items[slotIndex] != null) return false;

            items[slotIndex] = item;
            return true;
        }

        /// <summary>
        /// 从指定格子拿出物品
        /// 如果该位置有物品就拿出，否则返回 null
        /// </summary>
        /// <param name="slotIndex">目标格子序号</param>
        /// <returns>拿出的物品，如果格子为空则返回 null</returns>
        public Base TakeItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= items.Length) return null;
            if (items[slotIndex] == null) return null;

            Base item = items[slotIndex];
            items[slotIndex] = null;
            return item;
        }

        /// <summary>
        /// 切换当前装备的物品序号
        /// </summary>
        /// <param name="slotIndex">目标格子序号</param>
        public void SwitchSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= items.Length) return;
            if (slotIndex == currentSlotIndex) return;

            CurrentItem?.OnUnequip();
            currentSlotIndex = slotIndex;
            CurrentItem?.OnEquip();
        }

        #endregion

        /// <summary>
        /// 从 InputManager 获取 InputAction 引用
        /// </summary>
        private void GetInputActionFromInputManager()
        {
            if (inputActions == null)
            {
                InputManager inputManager = InputManager.Instance;
                if (inputManager != null && inputManager.InputAction != null)
                {
                    inputActions = inputManager.InputAction;
                }
                else
                {
                    Debug.LogError("Hotbar: 无法从InputManager获取InputAction引用！请确保场景中存在InputManager。");
                }
            }
        }
    }
}
