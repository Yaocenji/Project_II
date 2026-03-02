using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectII.Weapon
{
    /// <summary>
    /// 武器射击提示线管理器
    /// 用于显示武器的射击轨迹或瞄准辅助线
    /// </summary>
    public class ShootInstructionLine : MonoBehaviour
    {

        public WeaponBase currentWeapon;

        [Header("UI Elements")]
        [SerializeField] private Image image0; // 第一条提示线
        [SerializeField] private Image image1; // 第二条提示线

        [Header("Settings")]
        [SerializeField] private float lineWidth = 2f; // 线宽
        [SerializeField] private float alphaFadeSpeed = 5f; // Alpha 渐变速度

        [Header("References")]
        [SerializeField] private Camera mainCamera; // 主相机，用于世界空间到屏幕空间转换

        private RectTransform rectTransform0;
        private RectTransform rectTransform1;
        private Canvas canvas;
        
        // 当前 alpha 值
        private float currentAlpha = 1f;

        private void Awake()
        {
            // 获取 RectTransform 组件
            if (image0 != null)
            {
                rectTransform0 = image0.GetComponent<RectTransform>();
            }
            if (image1 != null)
            {
                rectTransform1 = image1.GetComponent<RectTransform>();
            }

            // 获取 Canvas（用于确定 UI 空间）
            if (image0 != null)
            {
                canvas = image0.GetComponentInParent<Canvas>();
            }
            else if (image1 != null)
            {
                canvas = image1.GetComponentInParent<Canvas>();
            }

            // 如果没有指定 Camera，尝试获取主相机
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("未找到主相机，请确保场景中存在标记为 MainCamera 的相机。");
                }
            }
        }

        private void Update()
        {
            // 先获取玩家位置
            Transform playerTransform = ProjectII.Manager.GameSceneManager.Instance.CurrentPlayerCharacter.transform;
            Vector2 playerPos = playerTransform.position;
            float playerRad = Mathf.Deg2Rad * playerTransform.eulerAngles.z;
            float upAngle = playerRad + Mathf.Deg2Rad * currentWeapon.spreadAngle; 
            float downAngle = playerRad - Mathf.Deg2Rad * currentWeapon.spreadAngle;
            Vector2 upDir = new Vector2(Mathf.Cos(upAngle), Mathf.Sin(upAngle));
            Vector2 downDir = new Vector2(Mathf.Cos(downAngle), Mathf.Sin(downAngle));
            
            // 然后就可以设置了
            SetPosition0(playerPos + .75f * upDir, playerPos + currentWeapon.Range * .33f * upDir);
            SetPosition1(playerPos + .75f * downDir, playerPos + currentWeapon.Range * .33f * downDir);
            
            // 平滑调整 alpha
            UpdateAlpha();
        }

        /// <summary>
        /// 根据武器能否开火状态平滑调整提示线的透明度
        /// </summary>
        private void UpdateAlpha()
        {
            // 目标 alpha：能开火为 1，否则为 0
            float targetAlpha = currentWeapon.CanFire ? .5f : 0f;
            
            // 使用 MoveTowards 平滑过渡（性能好，避免创建新对象）
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, alphaFadeSpeed * Time.deltaTime);
            
            // 只有当 alpha 变化时才更新颜色（减少不必要的赋值）
            if (image0 != null)
            {
                Color c0 = image0.color;
                c0.a = currentAlpha;
                image0.color = c0;
            }
            if (image1 != null)
            {
                Color c1 = image1.color;
                c1.a = currentAlpha;
                image1.color = c1;
            }
        }

        /// <summary>
        /// 设置第一条提示线的位置
        /// </summary>
        /// <param name="startPos">起点世界空间位置</param>
        /// <param name="endPos">终点世界空间位置</param>
        public void SetPosition0(Vector2 startPos, Vector2 endPos)
        {
            SetLinePosition(rectTransform0, startPos, endPos);
        }

        /// <summary>
        /// 设置第二条提示线的位置
        /// </summary>
        /// <param name="startPos">起点世界空间位置</param>
        /// <param name="endPos">终点世界空间位置</param>
        public void SetPosition1(Vector2 startPos, Vector2 endPos)
        {
            SetLinePosition(rectTransform1, startPos, endPos);
        }

        /// <summary>
        /// 设置线段的位置、旋转和缩放
        /// </summary>
        /// <param name="rectTransform">目标 RectTransform</param>
        /// <param name="startPos">起点世界空间位置</param>
        /// <param name="endPos">终点世界空间位置</param>
        private void SetLinePosition(RectTransform rectTransform, Vector2 startPos, Vector2 endPos)
        {
            if (rectTransform == null || mainCamera == null || canvas == null)
            {
                return;
            }

            // 将世界空间位置转换为屏幕空间位置
            Vector2 startScreenPos = mainCamera.WorldToScreenPoint(startPos);
            Vector2 endScreenPos = mainCamera.WorldToScreenPoint(endPos);

            // 计算中点（屏幕空间）
            Vector2 midScreenPos = (startScreenPos + endScreenPos) * 0.5f;

            // 计算线段方向和长度（屏幕空间）
            Vector2 direction = endScreenPos - startScreenPos;
            float length = direction.magnitude;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // 根据 Canvas 的渲染模式设置位置
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Screen Space - Overlay 模式：直接使用屏幕坐标
                rectTransform.anchoredPosition = midScreenPos;
            }
            else
            {
                // Screen Space - Camera 或 World Space 模式：需要转换
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvas.GetComponent<RectTransform>(),
                    midScreenPos,
                    canvas.worldCamera ?? mainCamera,
                    out localPoint);
                rectTransform.anchoredPosition = localPoint;
            }

            // 设置旋转（Z 轴旋转）
            rectTransform.localRotation = Quaternion.Euler(0, 0, angle);

            // 设置大小（长度和宽度）
            rectTransform.sizeDelta = new Vector2(length, lineWidth);
        }
    }
}
