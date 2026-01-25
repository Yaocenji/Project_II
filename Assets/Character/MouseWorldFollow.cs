using System;
using UnityEngine;

namespace ProjectII.Character
{
    public class MouseWorldFollow : MonoBehaviour
    {
        private void Update()
        {
            Vector2 worldPos = ProjectII.Manager.GameSceneManager.Instance.CurrentMouse.VirtualMouseWorldPosition;
            transform.position = worldPos;
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
        #endif
    }
}