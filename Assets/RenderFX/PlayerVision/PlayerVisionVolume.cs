using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectII.Render
{
    [VolumeComponentMenu("Project II/Player Vision")]
    public class PlayerVisionVolume : VolumeComponent, IPostProcessComponent
    {
        [Header("全局强度")]
        public ClampedFloatParameter globalStrength = new ClampedFloatParameter(1f, 0f, 1f);

        [Header("暗区调色")]
        public ClampedFloatParameter saturation     = new ClampedFloatParameter(0f,    0f, 1f);
        public ClampedFloatParameter brightness     = new ClampedFloatParameter(0.1f,  0f, 1f);
        public ClampedFloatParameter saturation_Far = new ClampedFloatParameter(0f,    0f, 1f);
        public ClampedFloatParameter brightness_Far = new ClampedFloatParameter(0.05f, 0f, 1f);

        [Header("噪声扰动（边界）")]
        public Texture2DParameter   noiseTex        = new Texture2DParameter(null);
        public FloatParameter       noiseWorldScale = new FloatParameter(2f);
        public FloatParameter       noiseStrength   = new FloatParameter(0.3f);

        [Header("S 曲线 — 视野范围（inSight）")]
        public ClampedFloatParameter shadowEdge_Sight = new ClampedFloatParameter(0.1f, 0f,  0.5f);
        public ClampedFloatParameter lightEdge_Sight  = new ClampedFloatParameter(0.9f, 0.5f, 1f);

        [Header("S 曲线 — 遮挡（notOccluded）")]
        public ClampedFloatParameter shadowEdge_Occlude = new ClampedFloatParameter(0.1f, 0f,  0.5f);
        public ClampedFloatParameter lightEdge_Occlude  = new ClampedFloatParameter(0.9f, 0.5f, 1f);

        [Header("迷雾（视野内遮挡区）")]
        public ColorParameter        fogColor      = new ColorParameter(new Color(0.05f, 0.05f, 0.2f), false, false, true);
        public ClampedFloatParameter fogIntensity  = new ClampedFloatParameter(0.6f, 0f, 1f);
        public FloatParameter        fogDepthRange = new FloatParameter(3f);
        public ClampedFloatParameter fogBlendMode  = new ClampedFloatParameter(0.4f, 0f, 1f);
        public Vector2Parameter      fogNoiseScale = new Vector2Parameter(new Vector2(3f, 7f));
        public Vector2Parameter      fogNoiseSpeed1 = new Vector2Parameter(new Vector2(0.05f, 0.02f));
        public Vector2Parameter      fogNoiseSpeed2 = new Vector2Parameter(new Vector2(-0.03f, 0.04f));

        [Header("距离模糊（Dual Kawase）")]
        public FloatParameter blurStartRadius = new FloatParameter(3f);
        public FloatParameter blurEndRadius   = new FloatParameter(10f);
        public ClampedIntParameter blurIterations = new ClampedIntParameter(2, 1, 6);

        public bool IsActive() => true;
        public bool IsTileCompatible() => false;
    }
}
