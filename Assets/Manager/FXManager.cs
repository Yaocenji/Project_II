using UnityEngine;
using ProjectII.Character;

namespace ProjectII.Manager
{
    /// <summary>
    /// 特效管理器
    /// 使用DefaultExecutionOrder确保此脚本的Awake优先于场景中其他脚本执行
    /// </summary>
    [DefaultExecutionOrder(-98)]
    public class FXManager : MonoBehaviour
    {        
        private static FXManager instance;

        /// <summary>
        /// FXManager单例实例
        /// </summary>
        public static FXManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<FXManager>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            // 确保只有一个FXManager实例
            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Debug.LogWarning("FXManager 单例已经存在，销毁新创建的实例。");
                Destroy(gameObject);
            }
        }
    }
}
