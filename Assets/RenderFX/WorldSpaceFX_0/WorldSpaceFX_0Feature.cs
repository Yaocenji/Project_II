using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectII.Render
{
    public class WorldSpaceFX_0Feature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            [Tooltip("用于渲染 WorldSpaceFX_0 效果的 Shader")]
            public Shader shader;
            
            public Texture2D rainWaveTex;
            public float rainWaveScale = 1.0f;
            public Texture2D puddleTex;
            public float puddleScale = 10f;

            [Header("雨雾默认值")]
            [ColorUsage(false, true)]
            public Color fogColor = new Color(0.5f, 0.55f, 0.65f);
            [Range(0f, 1f)]
            public float fogIntensity = 0.5f;
            public Texture2D fogNoiseTex;
            public Vector2 fogNoiseScale = new Vector2(3f, 7f);
            public Vector2 fogSpeed1 = new Vector2(0.05f, 0.02f);
            public Vector2 fogSpeed2 = new Vector2(-0.03f, 0.04f);
            [Range(1, 8)]
            public int fogOctaves = 4;

            [Header("距离衰减默认值")]
            public float fogDistFadeStart = 3f;
            public float fogDistFadeEnd = 15f;

            [Header("涟漪时间")]
            [Tooltip("涟漪动画速度倍率")]
            public float rainSpeed = 1f;
            [Tooltip("涟漪动画周期（秒）")]
            public float rainPeriod = 3f;
        }

        public Settings settings = new Settings();

        private WorldSpaceFX_0Pass m_Pass;
        private Material m_Material;

        public override void Create()
        {
            if (settings.shader == null) return;

            m_Material = CoreUtils.CreateEngineMaterial(settings.shader);
            m_Pass = new WorldSpaceFX_0Pass(m_Material, settings);
            m_Pass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Material == null || m_Pass == null) return;

            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            var vol = VolumeManager.instance.stack.GetComponent<WorldSpaceFX_0Volume>();
            if (!vol.IsActive())
                return;

            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            CoreUtils.Destroy(m_Material);
        }

        // ─────────────────────────────────────────────────────────

        class WorldSpaceFX_0Pass : ScriptableRenderPass, IDisposable
        {
            private readonly Material m_Material;
            private readonly Settings m_Settings;
            private RTHandle m_TempRT;

            private const int k_PassCompo = 0;

            public WorldSpaceFX_0Pass(Material material, Settings settings)
            {
                m_Material = material;
                m_Settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref m_TempRT, desc, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_WorldSpaceFX_0_Temp");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("World Space FX 0");

                var vol = VolumeManager.instance.stack.GetComponent<WorldSpaceFX_0Volume>();
                var s = m_Settings;

                // 读取 Volume 参数，有 override 时覆盖 Settings 默认值
                Texture rainWaveTex = (vol.rainWaveTex.overrideState && vol.rainWaveTex.value != null)
                                         ? vol.rainWaveTex.value : s.rainWaveTex;
                float rainWaveScale = (vol.rainWaveScale.overrideState && vol.rainWaveScale.value != null)
                                         ? vol.rainWaveScale.value : s.rainWaveScale;
                Texture puddleTex = (vol.puddleTex.overrideState && vol.puddleTex.value != null)
                                         ? vol.puddleTex.value : s.puddleTex;
                float puddleScale = vol.puddleScale.overrideState ? vol.puddleScale.value : s.puddleScale;

                Color   fogColor      = vol.fogColor.overrideState      ? vol.fogColor.value      : s.fogColor;
                float   fogIntensity  = vol.fogIntensity.overrideState  ? vol.fogIntensity.value  : s.fogIntensity;
                Texture fogNoiseTex   = (vol.fogNoiseTex.overrideState && vol.fogNoiseTex.value != null)
                                         ? vol.fogNoiseTex.value : s.fogNoiseTex;
                Vector2 fogNoiseScale = vol.fogNoiseScale.overrideState  ? vol.fogNoiseScale.value  : s.fogNoiseScale;
                Vector2 fogSpeed1     = vol.fogSpeed1.overrideState      ? vol.fogSpeed1.value      : s.fogSpeed1;
                Vector2 fogSpeed2     = vol.fogSpeed2.overrideState      ? vol.fogSpeed2.value      : s.fogSpeed2;
                int     fogOctaves    = vol.fogOctaves.overrideState     ? vol.fogOctaves.value     : s.fogOctaves;
                float   fogDistFadeStart = vol.fogDistFadeStart.overrideState ? vol.fogDistFadeStart.value : s.fogDistFadeStart;
                float   fogDistFadeEnd   = vol.fogDistFadeEnd.overrideState   ? vol.fogDistFadeEnd.value   : s.fogDistFadeEnd;

                if (rainWaveTex != null)
                    cmd.SetGlobalTexture("_WSFX0_RainWaveTex", rainWaveTex);
                
                cmd.SetGlobalFloat("_WSFX0_RainWaveScale", rainWaveScale);

                if (puddleTex != null)
                    cmd.SetGlobalTexture("_WSFX0_PuddleTex", puddleTex);
                cmd.SetGlobalFloat("_WSFX0_PuddleScale", puddleScale);
                

                cmd.SetGlobalColor("_WSFX0_FogColor", fogColor);
                cmd.SetGlobalFloat("_WSFX0_FogIntensity", fogIntensity);
                if (fogNoiseTex != null)
                    cmd.SetGlobalTexture("_WSFX0_FogNoiseTex", fogNoiseTex);
                cmd.SetGlobalVector("_WSFX0_FogNoiseScale", new Vector4(fogNoiseScale.x, fogNoiseScale.y, 0, 0));
                cmd.SetGlobalVector("_WSFX0_FogSpeed1", new Vector4(fogSpeed1.x, fogSpeed1.y, 0, 0));
                cmd.SetGlobalVector("_WSFX0_FogSpeed2", new Vector4(fogSpeed2.x, fogSpeed2.y, 0, 0));
                cmd.SetGlobalInt("_WSFX0_FogOctaves", fogOctaves);
                cmd.SetGlobalFloat("_WSFX0_FogDistFadeStart", fogDistFadeStart);
                cmd.SetGlobalFloat("_WSFX0_FogDistFadeEnd", fogDistFadeEnd);

                // 周期性时间系数：在 [0, period) 之间循环
                float rainPhase = (Time.time * s.rainSpeed) % s.rainPeriod;
                cmd.SetGlobalFloat("_WSFX0_RainPhase", rainPhase);
                cmd.SetGlobalFloat("_WSFX0_RainPeriod", s.rainPeriod);

                RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

                Blitter.BlitCameraTexture(cmd, cameraColor, m_TempRT);
                Blitter.BlitCameraTexture(cmd, m_TempRT, cameraColor, m_Material, k_PassCompo);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd) { }

            public void Dispose()
            {
                m_TempRT?.Release();
                m_TempRT = null;
            }
        }
    }
}
