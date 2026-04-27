using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectII.Render
{
    [VolumeComponentMenu("Project II/World Space FX 0")]
    public class WorldSpaceFX_0Volume : VolumeComponent, IPostProcessComponent
    {
        //public BoolParameter active = new BoolParameter(false);
        [Tooltip("雨水波纹噪声纹理")]
        public Texture2DParameter rainWaveTex = new Texture2DParameter(null);
        [Tooltip("雨水波纹世界空间缩放")]
        public FloatParameter rainWaveScale = new FloatParameter(1);
        [Tooltip("水塘遮罩噪声纹理")]
        public Texture2DParameter puddleTex = new Texture2DParameter(null);
        [Tooltip("水塘噪声世界空间缩放")]
        public FloatParameter puddleScale = new FloatParameter(10f);

        [Header("雨雾")]
        [ColorUsage(false, true), Tooltip("雨雾颜色")]
        public ColorParameter fogColor = new ColorParameter(new Color(0.5f, 0.55f, 0.65f), false, false, true);
        [Range(0f, 1f), Tooltip("雨雾浓度（0=无雾，1=最浓）")]
        public ClampedFloatParameter fogIntensity = new ClampedFloatParameter(0.5f, 0f, 1f);
        [Tooltip("雨雾噪声纹理")]
        public Texture2DParameter fogNoiseTex = new Texture2DParameter(null);
        [Tooltip("x=层1世界空间缩放, y=层2世界空间缩放")]
        public Vector2Parameter fogNoiseScale = new Vector2Parameter(new Vector2(3f, 7f));
        [Tooltip("层1流动速度（世界空间方向）")]
        public Vector2Parameter fogSpeed1 = new Vector2Parameter(new Vector2(0.05f, 0.02f));
        [Tooltip("层2流动速度（世界空间方向）")]
        public Vector2Parameter fogSpeed2 = new Vector2Parameter(new Vector2(-0.03f, 0.04f));
        [Tooltip("FBM 层数")]
        public ClampedIntParameter fogOctaves = new ClampedIntParameter(4, 1, 8);

        [Header("距离衰减")]
        [Tooltip("雾气开始衰减的世界空间距离")]
        public FloatParameter fogDistFadeStart = new FloatParameter(3f);
        [Tooltip("雾气完全消失的世界空间距离")]
        public FloatParameter fogDistFadeEnd = new FloatParameter(15f);
        public bool IsActive() => active;//.value;
        public bool IsTileCompatible() => false;
    }
}
