using UnityEngine;
using RadianceCascadesWorldBVH;

namespace ProjectII.Render
{
    /// <summary>
    /// 挂载到含 RCWBObject 的 GameObject 上，使该物体不参与玩家视野遮挡计算。
    /// OnEnable/OnDisable 自动向 PlayerVisionOccludeSystem 注册/反注册。
    /// </summary>
    [RequireComponent(typeof(RCWBObject))]
    public class PlayerVisionNoOcclude : MonoBehaviour
    {
        private RCWBObject m_RcwbObject;

        private void Awake()
        {
            m_RcwbObject = GetComponent<RCWBObject>();
        }

        private void OnEnable()
        {
            PlayerVisionOccludeSystem.Instance?.RegisterStatic(m_RcwbObject);
        }

        private void OnDisable()
        {
            PlayerVisionOccludeSystem.Instance?.UnregisterStatic(m_RcwbObject);
        }
    }
}
