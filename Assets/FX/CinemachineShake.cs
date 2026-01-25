using UnityEngine;
using Cinemachine;

// 必须继承 CinemachineExtension 才能挂载在虚拟相机上
[ExecuteInEditMode] [SaveDuringPlay] [AddComponentMenu("")]
public class CinemachineShake : CinemachineExtension
{
    [Header("Settings")]
    public float decaySpeed = 5f; // 震动衰减速度 (数值越大，停得越快)
    public float maxOffset = 0.5f; // 限制最大震动距离，防止穿模

    // 内部状态变量
    private Vector3 shakeOffset = Vector3.zero; // 当前随机震动偏移
    private Vector3 recoilOffset = Vector3.zero; // 当前定向后坐力偏移
    
    private float shakeIntensity = 0f; // 当前震动强度

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        // 只在 Cinemachine 计算完 Aim (瞄准) 后应用震动，确保跟随逻辑不受影响
        if (stage == CinemachineCore.Stage.Aim)
        {
            // 1. 更新震动逻辑 (每一帧衰减)
            if (Application.isPlaying)
            {
                UpdateShake(deltaTime);
            }

            // 2. 将计算出的偏移量应用给摄像机
            state.PositionCorrection += shakeOffset + recoilOffset;
        }
    }

    private void UpdateShake(float deltaTime)
    {
        // --- A. 处理随机震动 (Perlin Noise) ---
        if (shakeIntensity > 0)
        {
            // 使用 Perlin Noise 生成平滑的随机移动
            float x = (Mathf.PerlinNoise(Time.time * 20, 0) - 0.5f) * 2 * shakeIntensity;
            float y = (Mathf.PerlinNoise(0, Time.time * 20) - 0.5f) * 2 * shakeIntensity;
            
            shakeOffset = new Vector3(x, y, 0);

            // 衰减强度
            shakeIntensity -= deltaTime * decaySpeed;
            if (shakeIntensity < 0) shakeIntensity = 0;
        }
        else
        {
            shakeOffset = Vector3.zero;
        }

        // --- B. 处理定向后坐力 (Recoil) ---
        // 使用 Lerp 让后坐力迅速归位 (弹簧效果)
        if (recoilOffset != Vector3.zero)
        {
            // 归位速度比震动衰减稍快一点，更有弹性
            recoilOffset = Vector3.Lerp(recoilOffset, Vector3.zero, deltaTime * decaySpeed * 2); 
            if (recoilOffset.magnitude < 0.01f) recoilOffset = Vector3.zero;
        }
    }

    // --- 公共接口：给你的枪调用 ---

    /// <summary>
    /// 触发震动
    /// </summary>
    /// <param name="shakeForce">随机震动强度 (营造混乱感)</param>
    /// <param name="recoilDir">后坐力方向 (通常是 -枪口方向)</param>
    /// <param name="recoilForce">后坐力推力 (营造打击感)</param>
    public void Shake(float shakeForce, Vector3 recoilDir, float recoilForce)
    {
        // 叠加随机震动
        shakeIntensity += shakeForce;

        // 叠加定向后坐力 (限制最大值，防止一枪飞出屏幕)
        Vector3 kick = recoilDir.normalized * recoilForce;
        recoilOffset += kick;
        
        // 简单的钳制，防止连续开火震飞
        recoilOffset = Vector3.ClampMagnitude(recoilOffset, maxOffset);
    }
}