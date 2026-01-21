using UnityEngine;
using ProjectII.Character;

namespace ProjectII.Manager
{
    /// <summary>
    /// 游戏场景管理器
    /// 使用DefaultExecutionOrder确保此脚本的Awake优先于场景中其他脚本执行
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameSceneManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Character.CharacterController currentPlayerCharacter;
        [SerializeField] private Mouse currentMouse;

        /// <summary>
        /// 当前玩家角色对象
        /// 玩家对象创建的时候注册，用于替代其他脚本访问玩家对象时的单例系统
        /// </summary>
        public Character.CharacterController CurrentPlayerCharacter 
        { 
            get => currentPlayerCharacter; 
            private set => currentPlayerCharacter = value; 
        }

        /// <summary>
        /// 当前的鼠标对象
        /// 鼠标对象创建时注册
        /// </summary>
        public Mouse CurrentMouse 
        { 
            get => currentMouse; 
            private set => currentMouse = value; 
        }

        private static GameSceneManager instance;

        /// <summary>
        /// GameSceneManager单例实例
        /// </summary>
        public static GameSceneManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<GameSceneManager>();
                }
                return instance;
            }
        }

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
        }

        /// <summary>
        /// 注册玩家角色对象
        /// 玩家对象创建的时候调用此方法注册
        /// </summary>
        /// <param name="characterController">要注册的CharacterController</param>
        public void RegisterPlayerCharacter(Character.CharacterController characterController)
        {
            if (characterController == null)
            {
                Debug.LogWarning("尝试注册空的CharacterController！");
                return;
            }

            if (currentPlayerCharacter != null && currentPlayerCharacter != characterController)
            {
                Debug.LogWarning($"已存在玩家角色对象 {currentPlayerCharacter.name}，将被 {characterController.name} 替换。");
            }

            currentPlayerCharacter = characterController;
            Debug.Log($"玩家角色对象已注册: {characterController.name}");
        }

        /// <summary>
        /// 注销玩家角色对象
        /// </summary>
        /// <param name="characterController">要注销的CharacterController</param>
        public void UnregisterPlayerCharacter(Character.CharacterController characterController)
        {
            if (currentPlayerCharacter == characterController)
            {
                currentPlayerCharacter = null;
                Debug.Log($"玩家角色对象已注销: {characterController.name}");
            }
        }

        /// <summary>
        /// 注册鼠标对象
        /// 鼠标对象创建时调用此方法注册
        /// </summary>
        /// <param name="mouse">要注册的Mouse</param>
        public void RegisterMouse(Mouse mouse)
        {
            if (mouse == null)
            {
                Debug.LogWarning("尝试注册空的Mouse！");
                return;
            }

            if (currentMouse != null && currentMouse != mouse)
            {
                Debug.LogWarning($"已存在鼠标对象 {currentMouse.name}，将被 {mouse.name} 替换。");
            }

            currentMouse = mouse;
            Debug.Log($"鼠标对象已注册: {mouse.name}");
        }

        /// <summary>
        /// 注销鼠标对象
        /// </summary>
        /// <param name="mouse">要注销的Mouse</param>
        public void UnregisterMouse(Mouse mouse)
        {
            if (currentMouse == mouse)
            {
                currentMouse = null;
                Debug.Log($"鼠标对象已注销: {mouse.name}");
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
