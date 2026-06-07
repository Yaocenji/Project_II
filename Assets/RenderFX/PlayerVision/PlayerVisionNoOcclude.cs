using UnityEngine;
using Lumivara2D;

namespace ProjectII.Render
{
    /// <summary>
    /// 挂载到含 LV2DObject 的 GameObject 上，使该物体不参与玩家视野遮挡计算。
    /// OnEnable/OnDisable 自动向 PlayerVisionOccludeSystem 注册/反注册。
    /// </summary>
    [RequireComponent(typeof(LV2DObject))]
    public class PlayerVisionNoOcclude : MonoBehaviour
    {
        private LV2DObject m_Lv2dObject;

        private void Awake()
        {
            m_Lv2dObject = GetComponent<LV2DObject>();
        }

        private void OnEnable()
        {
            PlayerVisionOccludeSystem.Instance?.RegisterStatic(m_Lv2dObject);
        }

        private void OnDisable()
        {
            PlayerVisionOccludeSystem.Instance?.UnregisterStatic(m_Lv2dObject);
        }
    }
}
