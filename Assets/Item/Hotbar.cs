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

        /// <summary>
        /// 背包引用，用于物品转移操作
        /// 在 Awake 中通过 GetComponent 获取同 GameObject 上的 Backpack
        /// </summary>
        private Backpack backpack;

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

            // 获取同 GameObject 上的 Backpack 引用
            backpack = GetComponentInParent<Backpack>();
            if (backpack == null)
                backpack = GetComponent<Backpack>();

            // 对已有物品初始化 active 状态，并对当前格触发 OnEquip
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                {
                    items[i].gameObject.SetActive(i == currentSlotIndex);
                    if (i == currentSlotIndex)
                        items[i].OnEquip();
                }
            }
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
                inputActions.Character.switch_0.started += OnSwitchSlot0;
                inputActions.Character.switch_1.started += OnSwitchSlot1;
                inputActions.Character.switch_2.started += OnSwitchSlot2;
                inputActions.Character.switch_3.started += OnSwitchSlot3;
                inputActions.Character.switch_4.started += OnSwitchSlot4;
                inputActions.Character.switch_5.started += OnSwitchSlot5;
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
                inputActions.Character.switch_0.started -= OnSwitchSlot0;
                inputActions.Character.switch_1.started -= OnSwitchSlot1;
                inputActions.Character.switch_2.started -= OnSwitchSlot2;
                inputActions.Character.switch_3.started -= OnSwitchSlot3;
                inputActions.Character.switch_4.started -= OnSwitchSlot4;
                inputActions.Character.switch_5.started -= OnSwitchSlot5;
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

        private void OnSwitchSlot0(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            SwitchSlot(0);
        }
        private void OnSwitchSlot1(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            SwitchSlot(1);
        }
        private void OnSwitchSlot2(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            SwitchSlot(2);
        }
        private void OnSwitchSlot3(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            SwitchSlot(3);
        }
        private void OnSwitchSlot4(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            SwitchSlot(4);
        }
        private void OnSwitchSlot5(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            SwitchSlot(5);
        }
        
        #endregion

        #region 快捷栏操作

        /// <summary>
        /// 放入物品到指定格子
        /// 如果该位置没有物品，则放入；如果有物品，则拒绝放入
        /// 放入后根据是否为当前装备格设置 SetActive
        /// </summary>
        /// <param name="item">要放入的物品</param>
        /// <param name="slotIndex">目标格子序号</param>
        /// <returns>是否放入成功</returns>
        public bool PutItem(Base item, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= items.Length) return false;
            if (items[slotIndex] != null) return false;

            items[slotIndex] = item;
            // 当前装备格显示，其余隐藏
            item.gameObject.SetActive(slotIndex == currentSlotIndex);
            return true;
        }

        /// <summary>
        /// 从指定格子拿出物品
        /// 如果该位置有物品就拿出，否则返回 null
        /// 拿出时统一 SetActive(true)，后续由调用方处理
        /// </summary>
        /// <param name="slotIndex">目标格子序号</param>
        /// <returns>拿出的物品，如果格子为空则返回 null</returns>
        public Base TakeItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= items.Length) return null;
            if (items[slotIndex] == null) return null;

            Base item = items[slotIndex];
            items[slotIndex] = null;
            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>
        /// 切换当前装备的物品序号
        /// 切走的物品 SetActive(false)，切入的物品 SetActive(true)
        /// </summary>
        /// <param name="slotIndex">目标格子序号</param>
        public void SwitchSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= items.Length) return;
            if (slotIndex == currentSlotIndex) return;

            // 切走旧物品
            if (CurrentItem != null)
            {
                CurrentItem.OnUnequip();
                CurrentItem.gameObject.SetActive(false);
            }

            currentSlotIndex = slotIndex;

            // 切入新物品
            if (CurrentItem != null)
            {
                CurrentItem.gameObject.SetActive(true);
                CurrentItem.OnEquip();
            }
        }

        #endregion

        #region 物品转移

        /// <summary>
        /// 从快捷栏转移到背包。
        /// 同步函数，调用即完成，不存在"物品悬空"的中间态。
        /// </summary>
        /// <param name="hotbarSlot">快捷栏格子序号</param>
        /// <param name="backpackSlot">背包格子序号</param>
        /// <returns>是否转移成功</returns>
        public bool TransferToBackpack(int hotbarSlot, int backpackSlot)
        {
            if (backpack == null) return false;
            if (hotbarSlot < 0 || hotbarSlot >= items.Length) return false;
            if (items[hotbarSlot] == null) return false;

            // 如果转移的是当前装备格，先触发 OnUnequip
            bool isCurrentSlot = (hotbarSlot == currentSlotIndex);
            if (isCurrentSlot)
                items[hotbarSlot].OnUnequip();

            // 从快捷栏取出
            Base item = TakeItem(hotbarSlot);

            // 尝试放入背包
            if (backpack.PutItem(item, backpackSlot))
            {
                return true;
            }

            // 放入失败，回滚：放回快捷栏原位
            items[hotbarSlot] = item;
            // 恢复正确的 SetActive 状态
            item.gameObject.SetActive(isCurrentSlot);
            if (isCurrentSlot)
                item.OnEquip();
            return false;
        }

        /// <summary>
        /// 从背包转移到快捷栏。
        /// 同步函数，调用即完成。
        /// </summary>
        /// <param name="backpackSlot">背包格子序号</param>
        /// <param name="hotbarSlot">快捷栏格子序号</param>
        /// <returns>是否转移成功</returns>
        public bool TransferFromBackpack(int backpackSlot, int hotbarSlot)
        {
            if (backpack == null) return false;
            if (hotbarSlot < 0 || hotbarSlot >= items.Length) return false;
            if (items[hotbarSlot] != null) return false;
            if (backpack.GetStackCount(backpackSlot) == 0) return false;

            // 从背包取出
            Base item = backpack.TakeItem(backpackSlot);
            if (item == null) return false;

            // 尝试放入快捷栏
            if (PutItem(item, hotbarSlot))
            {
                // 如果放入的是当前装备格，触发 OnEquip
                if (hotbarSlot == currentSlotIndex)
                    item.OnEquip();
                return true;
            }

            // 放入失败，回滚：放回背包原位
            backpack.PutItem(item, backpackSlot);
            return false;
        }

        /// <summary>
        /// 快捷栏内两个格子交换物品。
        /// 同步函数，调用即完成。
        /// </summary>
        /// <param name="slotA">格子序号 A</param>
        /// <param name="slotB">格子序号 B</param>
        /// <returns>是否交换成功</returns>
        public bool SwapHotbarSlots(int slotA, int slotB)
        {
            if (slotA < 0 || slotA >= items.Length) return false;
            if (slotB < 0 || slotB >= items.Length) return false;
            if (slotA == slotB) return false;

            // 如果其中一个是当前装备格，先 OnUnequip
            if (slotA == currentSlotIndex && items[slotA] != null)
                items[slotA].OnUnequip();
            else if (slotB == currentSlotIndex && items[slotB] != null)
                items[slotB].OnUnequip();

            // 交换
            Base temp = items[slotA];
            items[slotA] = items[slotB];
            items[slotB] = temp;

            // 交换后更新两个格子的 SetActive 状态
            if (items[slotA] != null)
                items[slotA].gameObject.SetActive(slotA == currentSlotIndex);
            if (items[slotB] != null)
                items[slotB].gameObject.SetActive(slotB == currentSlotIndex);

            // 交换后，如果当前装备格有物品，触发 OnEquip
            if (CurrentItem != null)
                CurrentItem.OnEquip();

            return true;
        }

        #endregion

        #region 物品丢弃

        /// <summary>
        /// 丢弃快捷栏中的物品。
        /// 在指定位置实例化掉落物，然后销毁手持物 GameObject。
        /// </summary>
        /// <param name="hotbarSlot">快捷栏格子序号</param>
        /// <param name="dropPosition">掉落物生成位置</param>
        /// <returns>是否丢弃成功</returns>
        public bool DropItemFromHotbar(int hotbarSlot, Vector2 dropPosition)
        {
            if (hotbarSlot < 0 || hotbarSlot >= items.Length) return false;
            if (items[hotbarSlot] == null) return false;
            if (items[hotbarSlot].worldItemPrefab == null) return false;

            // 如果丢弃的是当前装备格，先触发 OnUnequip
            bool isCurrentSlot = (hotbarSlot == currentSlotIndex);
            if (isCurrentSlot)
                items[hotbarSlot].OnUnequip();

            // 从快捷栏取出
            Base item = TakeItem(hotbarSlot);

            // 实例化掉落物
            Instantiate(item.worldItemPrefab, dropPosition, Quaternion.identity);

            // 销毁手持物 GameObject
            Destroy(item.gameObject);

            return true;
        }

        /// <summary>
        /// 丢弃背包中的物品。
        /// 在指定位置实例化掉落物，然后销毁手持物 GameObject。
        /// </summary>
        /// <param name="backpackSlot">背包格子序号</param>
        /// <param name="dropPosition">掉落物生成位置</param>
        /// <returns>是否丢弃成功</returns>
        public bool DropItemFromBackpack(int backpackSlot, Vector2 dropPosition)
        {
            if (backpack == null) return false;
            if (backpack.GetStackCount(backpackSlot) == 0) return false;

            Base peekItem = backpack.PeekItem(backpackSlot);
            if (peekItem == null || peekItem.worldItemPrefab == null) return false;

            // 从背包取出
            Base item = backpack.TakeItem(backpackSlot);
            if (item == null) return false;

            // 实例化掉落物
            Instantiate(item.worldItemPrefab, dropPosition, Quaternion.identity);

            // 销毁手持物 GameObject
            Destroy(item.gameObject);

            return true;
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
