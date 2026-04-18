using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectII.Render
{
    public class PlayerVisionFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            [Tooltip("用于渲染玩家视野效果的 Shader")]
            public Shader shader;

            [Header("暗区调色")]
            [Range(0f, 1f), Tooltip("暗区饱和度（0=全灰，1=保持原色）")]
            public float saturation = 0f;
            [Range(0f, 1f), Tooltip("暗区明度系数（0=全黑，1=原亮度）")]
            public float brightness = 0.1f;

            [Header("噪声扰动")]
            [Tooltip("噪声纹理（256x256 黑白 Perlin）")]
            public Texture2D noiseTex;
            [Tooltip("噪声世界空间缩放（值越大花纹越粗）")]
            public float noiseWorldScale = 2f;
            [Tooltip("噪声偏移强度（噪声=1 时的世界空间偏移距离）")]
            public float noiseStrength = 0.3f;

            [Header("S 曲线重映射")]
            [Range(0f, 0.5f), Tooltip("暗区截断点（低于此值的 visible 映射为 0）")]
            public float shadowEdge = 0.1f;
            [Range(0.5f, 1f), Tooltip("亮区截断点（高于此值的 visible 映射为 1）")]
            public float lightEdge = 0.9f;
        }

        public Settings settings = new Settings();

        private PlayerVisionPass m_Pass;
        private Material m_Material;

        public override void Create()
        {
            if (settings.shader == null) return;

            m_Material = CoreUtils.CreateEngineMaterial(settings.shader);
            m_Pass = new PlayerVisionPass(m_Material, settings);
            m_Pass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_Material == null || m_Pass == null) return;

            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
                return;

            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            CoreUtils.Destroy(m_Material);
        }

        // ─────────────────────────────────────────────────────────

        class PlayerVisionPass : ScriptableRenderPass, IDisposable
        {
            private readonly Material m_Material;
            private readonly Settings m_Settings;
            private RTHandle m_TempRT;

            public PlayerVisionPass(Material material, Settings settings)
            {
                m_Material = material;
                m_Settings = settings;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                RenderingUtils.ReAllocateIfNeeded(ref m_TempRT, desc, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_PlayerVision_Temp");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("Player Vision");

                // 写入调色参数
                cmd.SetGlobalFloat("_PlayerVision_Saturation", m_Settings.saturation);
                cmd.SetGlobalFloat("_PlayerVision_Brightness", m_Settings.brightness);

                // 写入噪声参数
                if (m_Settings.noiseTex != null)
                    cmd.SetGlobalTexture("_PlayerVision_NoiseTex", m_Settings.noiseTex);
                cmd.SetGlobalFloat("_PlayerVision_NoiseWorldScale", m_Settings.noiseWorldScale);
                cmd.SetGlobalFloat("_PlayerVision_NoiseStrength",   m_Settings.noiseStrength);

                // 写入 S 曲线参数
                cmd.SetGlobalFloat("_PlayerVision_ShadowEdge", m_Settings.shadowEdge);
                cmd.SetGlobalFloat("_PlayerVision_LightEdge",  m_Settings.lightEdge);

                RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

                // 将当前画面 blit 到临时 RT，再用 shader 合成写回
                Blitter.BlitCameraTexture(cmd, cameraColor, m_TempRT);
                Blitter.BlitCameraTexture(cmd, m_TempRT, cameraColor, m_Material, 0);

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
