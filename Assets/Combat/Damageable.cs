using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ProjectII.Combat
{
    /// <summary>
    /// 可受伤组件（生命值管理）
    /// 采用组合模式，任何需要被伤害的 GameObject（玩家、敌人、可破坏物）
    /// 只需挂载此组件即可获得生命值管理能力。
    /// 伤害逻辑与反馈逻辑通过事件完全解耦。
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        #region 序列化字段

        [Header("生命值")]
        [SerializeField] private int maxHP = 100;

        [Header("无敌帧")]
        [SerializeField] private float invincibleDuration = 0.2f;

        #endregion

        #region 运行时状态

        /// <summary>
        /// 当前生命值
        /// </summary>
        private int currentHP;

        /// <summary>
        /// 当前是否处于无敌状态
        /// </summary>
        private bool isInvincible = false;

        /// <summary>
        /// 无敌帧协程引用，用于防止重复启动
        /// </summary>
        private Coroutine invincibleCoroutine;

        #endregion

        #region 公有属性（只读）

        /// <summary>
        /// 当前生命值
        /// </summary>
        public int CurrentHP => currentHP;

        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHP => maxHP;

        /// <summary>
        /// 是否存活（currentHP > 0）
        /// </summary>
        public bool IsAlive => currentHP > 0;

        /// <summary>
        /// 当前是否处于无敌状态
        /// </summary>
        public bool IsInvincible => isInvincible;

        /// <summary>
        /// 生命值比例（0~1），用于血条 UI 填充
        /// </summary>
        public float HPRatio => maxHP > 0 ? (float)currentHP / maxHP : 0f;

        #endregion

        #region C# 事件（供代码逻辑订阅，性能好，编译期检查）

        /// <summary>
        /// 受到伤害时触发
        /// 参数：(伤害值, 伤害来源 GameObject)
        /// </summary>
        public event Action<int, GameObject> OnDamaged_CSharp;

        /// <summary>
        /// 死亡时触发（HP 归零）
        /// </summary>
        public event Action OnDeath_CSharp;

        /// <summary>
        /// 生命值变化时触发
        /// 参数：(当前HP, 最大HP)
        /// </summary>
        public event Action<int, int> OnHPChanged_CSharp;

        #endregion

        #region UnityEvent（Inspector 可拖拽，策划友好）

        [Header("事件（Inspector 可拖拽）")]

        /// <summary>
        /// 受到伤害时触发（UnityEvent 版本）
        /// </summary>
        [SerializeField] private UnityEvent<int, GameObject> onDamaged_Unity;

        /// <summary>
        /// 死亡时触发（UnityEvent 版本）
        /// </summary>
        [SerializeField] private UnityEvent onDeath_Unity;

        /// <summary>
        /// 生命值变化时触发（UnityEvent 版本）
        /// </summary>
        [SerializeField] private UnityEvent<int, int> onHPChanged_Unity;

        /// <summary>
        /// 受到伤害时触发的 UnityEvent（只读访问，供外部通过代码添加 Inspector 风格的监听）
        /// </summary>
        public UnityEvent<int, GameObject> OnDamaged_Unity => onDamaged_Unity;

        /// <summary>
        /// 死亡时触发的 UnityEvent（只读访问）
        /// </summary>
        public UnityEvent OnDeath_Unity => onDeath_Unity;

        /// <summary>
        /// 生命值变化时触发的 UnityEvent（只读访问）
        /// </summary>
        public UnityEvent<int, int> OnHPChanged_Unity => onHPChanged_Unity;

        #endregion

        #region 生命周期

        private void Awake()
        {
            currentHP = maxHP;

            // 确保 UnityEvent 不为 null
            if (onDamaged_Unity == null) onDamaged_Unity = new UnityEvent<int, GameObject>();
            if (onDeath_Unity == null) onDeath_Unity = new UnityEvent();
            if (onHPChanged_Unity == null) onHPChanged_Unity = new UnityEvent<int, int>();
        }

        private void OnDestroy()
        {
            // 清空所有 C# 事件订阅，防止内存泄漏
            OnDamaged_CSharp = null;
            OnDeath_CSharp = null;
            OnHPChanged_CSharp = null;

            // 清空 UnityEvent 监听
            onDamaged_Unity?.RemoveAllListeners();
            onDeath_Unity?.RemoveAllListeners();
            onHPChanged_Unity?.RemoveAllListeners();
        }

        #endregion

        #region 公有方法

        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害值（正整数）</param>
        /// <param name="source">伤害来源 GameObject（可为 null）</param>
        public void TakeDamage(int damage, GameObject source)
        {
            // 无敌状态或已死亡，忽略伤害
            if (isInvincible || !IsAlive)
            {
                return;
            }

            // 防御性检查：伤害值不能为负
            if (damage <= 0)
            {
                Debug.LogWarning($"Damageable.TakeDamage: 伤害值 {damage} 无效（必须为正整数），已忽略。");
                return;
            }

            // 扣减 HP
            currentHP = Mathf.Max(0, currentHP - damage);

            // 触发受伤事件（C# + Unity 双轨）
            OnDamaged_CSharp?.Invoke(damage, source);
            onDamaged_Unity?.Invoke(damage, source);

            // 触发 HP 变化事件
            OnHPChanged_CSharp?.Invoke(currentHP, maxHP);
            onHPChanged_Unity?.Invoke(currentHP, maxHP);

            Debug.Log($"[Damageable] {gameObject.name} 受到 {damage} 点伤害（来源: {(source != null ? source.name : "null")}），剩余 HP: {currentHP}/{maxHP}");

            // 检查死亡
            if (currentHP <= 0)
            {
                Debug.Log($"[Damageable] {gameObject.name} 已死亡！");
                OnDeath_CSharp?.Invoke();
                onDeath_Unity?.Invoke();
                return; // 死亡后不启动无敌帧
            }

            // 启动无敌帧
            if (invincibleDuration > 0f)
            {
                // 如果已有无敌帧协程在运行，不重复启动
                if (invincibleCoroutine != null)
                {
                    StopCoroutine(invincibleCoroutine);
                }
                invincibleCoroutine = StartCoroutine(InvincibleCoroutine());
            }
        }

        /// <summary>
        /// 恢复生命值
        /// </summary>
        /// <param name="amount">恢复量（正整数）</param>
        public void Heal(int amount)
        {
            // 已死亡不能回血
            if (!IsAlive)
            {
                Debug.LogWarning($"Damageable.Heal: {gameObject.name} 已死亡，无法恢复生命值。");
                return;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"Damageable.Heal: 恢复量 {amount} 无效（必须为正整数），已忽略。");
                return;
            }

            int previousHP = currentHP;
            currentHP = Mathf.Min(maxHP, currentHP + amount);

            // 只有实际恢复了才触发事件
            if (currentHP != previousHP)
            {
                OnHPChanged_CSharp?.Invoke(currentHP, maxHP);
                onHPChanged_Unity?.Invoke(currentHP, maxHP);

                Debug.Log($"[Damageable] {gameObject.name} 恢复了 {currentHP - previousHP} 点生命值，当前 HP: {currentHP}/{maxHP}");
            }
        }

        /// <summary>
        /// 重置生命值到最大值（用于重生/场景重载）
        /// </summary>
        public void ResetHP()
        {
            currentHP = maxHP;
            isInvincible = false;

            // 停止无敌帧协程
            if (invincibleCoroutine != null)
            {
                StopCoroutine(invincibleCoroutine);
                invincibleCoroutine = null;
            }

            // 触发 HP 变化事件
            OnHPChanged_CSharp?.Invoke(currentHP, maxHP);
            onHPChanged_Unity?.Invoke(currentHP, maxHP);
        }

        #endregion

        #region 无敌帧协程

        /// <summary>
        /// 无敌帧协程：受击后短暂无敌，防止连续受击
        /// </summary>
        private IEnumerator InvincibleCoroutine()
        {
            isInvincible = true;
            yield return new WaitForSeconds(invincibleDuration);
            isInvincible = false;
            invincibleCoroutine = null;
        }

        #endregion
    }
}
