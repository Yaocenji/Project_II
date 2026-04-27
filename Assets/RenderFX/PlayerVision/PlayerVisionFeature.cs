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
            [Range(0f, 1f), Tooltip("近处/完全可见时的饱和度（0=全灰，1=保持原色）")]
            public float saturation_Near = 1f;
            [Range(0f, 1f), Tooltip("近处/完全可见时的亮度系数")]
            public float brightness_Near = 1f;
            [Range(0f, 1f), Tooltip("远处/完全遮挡时的饱和度")]
            public float saturation_Far = 0f;
            [Range(0f, 1f), Tooltip("远处/完全遮挡时的亮度系数")]
            public float brightness_Far = 0.05f;
            [Tooltip("调色渐变开始的世界空间距离（近端，此距离内保持 Near 调色）")]
            public float distFadeStart = 3f;
            [Tooltip("调色渐变结束的世界空间距离（远端，此距离外取 Far 调色）")]
            public float distFadeEnd = 10f;

            [Header("噪声扰动（边界）")]
            [Tooltip("X 方向蓝噪声纹理")]
            public Texture2D noiseTexX;
            [Tooltip("Y 方向蓝噪声纹理")]
            public Texture2D noiseTexY;
            [Tooltip("噪声世界空间缩放（值越大花纹越粗）")]
            public float noiseWorldScale = 2f;
            [Tooltip("噪声偏移强度（噪声=1 时的世界空间偏移距离）")]
            public float noiseStrength = 0.3f;

            [Header("S 曲线 — 视野范围（inSight）")]
            [Range(0f, 0.5f)] public float shadowEdge_Sight = 0.1f;
            [Range(0.5f, 1f)] public float lightEdge_Sight  = 0.9f;

            [Header("S 曲线 — 遮挡（notOccluded）")]
            [Range(0f, 0.5f)] public float shadowEdge_Occlude = 0.1f;
            [Range(0.5f, 1f)] public float lightEdge_Occlude  = 0.9f;

            [Header("迷雾（视野内遮挡区）")]
            [ColorUsage(false, true), Tooltip("迷雾颜色")]
            public Color fogColor = new Color(0.05f, 0.05f, 0.2f);
            [Range(0f, 1f)] public float fogIntensity = 0.6f;
            [Tooltip("遮挡深度映射范围（世界空间单位），超过此深度视为最深层")]
            public float fogDepthRange = 3f;
            [Range(0f, 1f), Tooltip("混合模式权重：0=纯乘法（保留结构/压暗），1=纯屏幕（暗区透光）")]
            public float fogBlendMode = 0.4f;
            [Tooltip("迷雾噪声纹理")]
            public Texture2D fogNoiseTex;
            [Tooltip("x=层1世界空间缩放, y=层2世界空间缩放")]
            public Vector2 fogNoiseScale = new Vector2(3f, 7f);
            [Tooltip("层1流动速度（世界空间方向）")]
            public Vector2 fogNoiseSpeed1 = new Vector2(0.05f, 0.02f);
            [Tooltip("层2流动速度（世界空间方向）")]
            public Vector2 fogNoiseSpeed2 = new Vector2(-0.03f, 0.04f);

            [Header("全局强度")]
            [Range(0f, 1f), Tooltip("所有视野效果的整体强度（0=完全关闭，1=完整效果）")]
            public float globalStrength = 1f;

            [Header("距离模糊（Dual Kawase）")]
            [Tooltip("模糊开始生效的世界空间半径")]
            public float blurStartRadius = 3f;
            [Tooltip("模糊达到最强的世界空间半径")]
            public float blurEndRadius = 10f;
            [Range(1, 6), Tooltip("Dual Kawase 迭代次数（越大越模糊，每次+2个Pass开销）")]
            public int blurIterations = 2;
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

            // Dual Kawase 迭代 RT 链：最多 6 次迭代需 7 个 RT（索引 0 = 全分辨率，1..N = 逐级降采样）
            private const int k_MaxIterations = 6;
            private RTHandle[] m_BlurChain = new RTHandle[k_MaxIterations + 1];

            // Pass 索引（对应 Shader 中的 Pass 顺序）
            private const int k_PassDown  = 0;
            private const int k_PassUp    = 1;
            private const int k_PassCompo = 2;

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

                int iter = Mathf.Clamp(m_Settings.blurIterations, 1, k_MaxIterations);
                int w = desc.width;
                int h = desc.height;
                for (int i = 0; i <= iter; i++)
                {
                    var d = desc;
                    d.width  = Mathf.Max(1, w);
                    d.height = Mathf.Max(1, h);
                    RenderingUtils.ReAllocateIfNeeded(ref m_BlurChain[i], d, FilterMode.Bilinear,
                        TextureWrapMode.Clamp, name: $"_PlayerVision_Blur{i}");
                    w = Mathf.Max(1, w / 2);
                    h = Mathf.Max(1, h / 2);
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material == null) return;

                CommandBuffer cmd = CommandBufferPool.Get("Player Vision");

                // 读取 Volume 参数，有 override 时覆盖 Settings 默认值
                var vol = VolumeManager.instance.stack.GetComponent<PlayerVisionVolume>();

                float saturation_Near   = vol.saturation_Near.overrideState  ? vol.saturation_Near.value  : m_Settings.saturation_Near;
                float brightness_Near   = vol.brightness_Near.overrideState  ? vol.brightness_Near.value  : m_Settings.brightness_Near;
                float saturation_Far    = vol.saturation_Far.overrideState   ? vol.saturation_Far.value   : m_Settings.saturation_Far;
                float brightness_Far    = vol.brightness_Far.overrideState   ? vol.brightness_Far.value   : m_Settings.brightness_Far;
                float distFadeStart     = vol.distFadeStart.overrideState    ? vol.distFadeStart.value    : m_Settings.distFadeStart;
                float distFadeEnd       = vol.distFadeEnd.overrideState      ? vol.distFadeEnd.value      : m_Settings.distFadeEnd;

                Texture noiseTexX      = (vol.noiseTexX.overrideState && vol.noiseTexX.value != null)
                                         ? vol.noiseTexX.value : m_Settings.noiseTexX;
                Texture noiseTexY      = (vol.noiseTexY.overrideState && vol.noiseTexY.value != null)
                                         ? vol.noiseTexY.value : m_Settings.noiseTexY;
                float noiseWorldScale  = vol.noiseWorldScale.overrideState ? vol.noiseWorldScale.value : m_Settings.noiseWorldScale;
                float noiseStrength    = vol.noiseStrength.overrideState   ? vol.noiseStrength.value   : m_Settings.noiseStrength;

                float shadowEdge_Sight   = vol.shadowEdge_Sight.overrideState   ? vol.shadowEdge_Sight.value   : m_Settings.shadowEdge_Sight;
                float lightEdge_Sight    = vol.lightEdge_Sight.overrideState    ? vol.lightEdge_Sight.value    : m_Settings.lightEdge_Sight;
                float shadowEdge_Occlude = vol.shadowEdge_Occlude.overrideState ? vol.shadowEdge_Occlude.value : m_Settings.shadowEdge_Occlude;
                float lightEdge_Occlude  = vol.lightEdge_Occlude.overrideState  ? vol.lightEdge_Occlude.value  : m_Settings.lightEdge_Occlude;

                Color   fogColor      = vol.fogColor.overrideState      ? vol.fogColor.value      : m_Settings.fogColor;
                float   fogIntensity  = vol.fogIntensity.overrideState  ? vol.fogIntensity.value  : m_Settings.fogIntensity;
                float   fogDepthRange = vol.fogDepthRange.overrideState ? vol.fogDepthRange.value : m_Settings.fogDepthRange;
                float   fogBlendMode  = vol.fogBlendMode.overrideState  ? vol.fogBlendMode.value  : m_Settings.fogBlendMode;
                Texture fogNoiseTex   = (vol.fogNoiseTex.overrideState && vol.fogNoiseTex.value != null)
                                         ? vol.fogNoiseTex.value : m_Settings.fogNoiseTex;
                Vector2 fogNoiseScale  = vol.fogNoiseScale.overrideState  ? vol.fogNoiseScale.value  : m_Settings.fogNoiseScale;
                Vector2 fogNoiseSpeed1 = vol.fogNoiseSpeed1.overrideState ? vol.fogNoiseSpeed1.value : m_Settings.fogNoiseSpeed1;
                Vector2 fogNoiseSpeed2 = vol.fogNoiseSpeed2.overrideState ? vol.fogNoiseSpeed2.value : m_Settings.fogNoiseSpeed2;

                float blurStartRadius = vol.blurStartRadius.overrideState ? vol.blurStartRadius.value : m_Settings.blurStartRadius;
                float blurEndRadius   = vol.blurEndRadius.overrideState   ? vol.blurEndRadius.value   : m_Settings.blurEndRadius;
                int   blurIterations  = vol.blurIterations.overrideState  ? vol.blurIterations.value  : m_Settings.blurIterations;
                float globalStrength  = vol.globalStrength.overrideState  ? vol.globalStrength.value  : m_Settings.globalStrength;

                // 写入调色参数
                cmd.SetGlobalFloat("_PlayerVision_Saturation_Near", saturation_Near);
                cmd.SetGlobalFloat("_PlayerVision_Brightness_Near", brightness_Near);
                cmd.SetGlobalFloat("_PlayerVision_Saturation_Far",  saturation_Far);
                cmd.SetGlobalFloat("_PlayerVision_Brightness_Far",  brightness_Far);
                cmd.SetGlobalFloat("_PlayerVision_DistFadeStart",   distFadeStart);
                cmd.SetGlobalFloat("_PlayerVision_DistFadeEnd",     distFadeEnd);

                // 写入噪声参数
                if (noiseTexX != null)
                    cmd.SetGlobalTexture("_PlayerVision_NoiseTexX", noiseTexX);
                if (noiseTexY != null)
                    cmd.SetGlobalTexture("_PlayerVision_NoiseTexY", noiseTexY);
                cmd.SetGlobalFloat("_PlayerVision_NoiseWorldScale", noiseWorldScale);
                cmd.SetGlobalFloat("_PlayerVision_NoiseStrength",   noiseStrength);

                // 写入 S 曲线参数
                cmd.SetGlobalFloat("_PlayerVision_ShadowEdge_Sight",   shadowEdge_Sight);
                cmd.SetGlobalFloat("_PlayerVision_LightEdge_Sight",    lightEdge_Sight);
                cmd.SetGlobalFloat("_PlayerVision_ShadowEdge_Occlude", shadowEdge_Occlude);
                cmd.SetGlobalFloat("_PlayerVision_LightEdge_Occlude",  lightEdge_Occlude);

                // 写入迷雾参数
                cmd.SetGlobalColor("_PlayerVision_FogColor",      fogColor);
                cmd.SetGlobalFloat("_PlayerVision_FogIntensity",  fogIntensity);
                cmd.SetGlobalFloat("_PlayerVision_FogDepthRange", fogDepthRange);
                cmd.SetGlobalFloat("_PlayerVision_FogBlendMode",  fogBlendMode);
                if (fogNoiseTex != null)
                    cmd.SetGlobalTexture("_PlayerVision_FogNoiseTex", fogNoiseTex);
                cmd.SetGlobalVector("_PlayerVision_FogNoiseScale",  new Vector4(fogNoiseScale.x,  fogNoiseScale.y,  0, 0));
                cmd.SetGlobalVector("_PlayerVision_FogNoiseSpeed1", new Vector4(fogNoiseSpeed1.x, fogNoiseSpeed1.y, 0, 0));
                cmd.SetGlobalVector("_PlayerVision_FogNoiseSpeed2", new Vector4(fogNoiseSpeed2.x, fogNoiseSpeed2.y, 0, 0));

                // 写入模糊参数
                cmd.SetGlobalFloat("_PlayerVision_BlurStartRadius", blurStartRadius);
                cmd.SetGlobalFloat("_PlayerVision_BlurEndRadius",   blurEndRadius);
                cmd.SetGlobalFloat("_PlayerVision_GlobalStrength",  globalStrength);

                RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
                int iter = Mathf.Clamp(blurIterations, 1, k_MaxIterations);

                // Step 1：保留原始帧到 m_TempRT
                Blitter.BlitCameraTexture(cmd, cameraColor, m_TempRT);

                // Step 2：Dual Kawase 下采样链
                // chain[0] = 全分辨率原始色副本，chain[1..iter] = 逐级降采样
                Blitter.BlitCameraTexture(cmd, m_TempRT, m_BlurChain[0]);
                for (int i = 0; i < iter; i++)
                {
                    cmd.SetGlobalFloat("_KawaseOffset", i);
                    Blitter.BlitCameraTexture(cmd, m_BlurChain[i], m_BlurChain[i + 1], m_Material, k_PassDown);
                }

                // Step 3：Dual Kawase 上采样链（从最深层恢复回 chain[0]）
                for (int i = iter; i > 0; i--)
                {
                    cmd.SetGlobalFloat("_KawaseOffset", i - 1);
                    Blitter.BlitCameraTexture(cmd, m_BlurChain[i], m_BlurChain[i - 1], m_Material, k_PassUp);
                }
                cmd.SetGlobalTexture("_PlayerVision_BlurTex", m_BlurChain[0]);

                // Step 4：视野合成写回画面
                Blitter.BlitCameraTexture(cmd, m_TempRT, cameraColor, m_Material, k_PassCompo);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd) { }

            public void Dispose()
            {
                m_TempRT?.Release();
                m_TempRT = null;
                for (int i = 0; i < m_BlurChain.Length; i++)
                {
                    m_BlurChain[i]?.Release();
                    m_BlurChain[i] = null;
                }
            }
        }
    }
}
