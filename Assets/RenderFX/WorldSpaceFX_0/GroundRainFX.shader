Shader "ProjectII/GroundRainFX"
{
    Properties
    {
        [Header(Textures)]
        [PerRendererData] _MainTex ("Albedo (RGB) Alpha (A)", 2D) = "white" {}
        [PerRendererData] _BumpMap ("Normal Map", 2D) = "bump" {}

        [Header(Emission Data)]
        [HDR] _Emission ("Emission Color", Color) = (0,0,0,0)

        [Header(Radiance Cascades Data)]
        _IsWall ("Is Wall (1=Block Light)", Float) = 1.0
        _Occlusion ("Occlusion (0=Transparent, 1=Opaque)", Range(0.0, 1.0)) = 1.0

    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "Universal2D"
            Tags { "Queue" = "Transparent" "LightMode"="Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ RCWB_EDITOR_SCENE_PREVIEW
            #pragma multi_compile_fragment _ ENABLE_TRANSLUCENT_OBJECTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/yaocenji.radiance-cascades-world-bvh/Shaders/RCW_BVH_Inc.hlsl"
            #include "Packages/yaocenji.radiance-cascades-world-bvh/Shaders/IOField.hlsl"

            // ---------------------------------------------------------
            // 1. CBUFFER 定义 (严格匹配 SRP Batcher)
            // ---------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                half4 _Emission;
                float2 _RotationSinCos;
                float _GICoefficient;

                float4x4 MatrixInvVP;
                float4x4 MatrixVP;
                float4x4 MatrixVP_Prev;
                float4x4 MatrixInvVP_Prev;
                float4 _RCWB_HistoryColor_TexelSize;

                float _RCWB_HistoryWeight;

                float _IsWall;
            CBUFFER_END

            float _RCWB_GI_Height;

            // 全局变量：玩家属性
            float4 _Player_PosWS_Direction_Angle;

            // ---------------------------------------------------------
            // 2. 雨效果参数 (全局变量，由 Feature/脚本设置)
            // ---------------------------------------------------------
            float  _WSFX0_RainWaveScale;
            float  _WSFX0_PuddleScale;
            float2 _WSFX0_FogNoiseScale;
            float  _WSFX0_RainPhase;
            float  _WSFX0_RainPeriod;

            TEXTURE2D(_WSFX0_RainWaveTex);
            SAMPLER(sampler_WSFX0_RainWaveTex);
            TEXTURE2D(_WSFX0_PuddleTex);
            SAMPLER(sampler_WSFX0_PuddleTex);
            TEXTURE2D(_WSFX0_FogNoiseTex);
            SAMPLER(sampler_WSFX0_FogNoiseTex);

            // ---------------------------------------------------------
            // 3. 纹理定义
            // ---------------------------------------------------------
            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);

            // ---------------------------------------------------------
            // 4. FBM
            // ---------------------------------------------------------
            float FBM(float2 uv, int octaves)
            {
                float value = 0.0;
                float amplitude = 1.0;
                float frequency = 1.0;
                float maxValue = 0.0;

                [loop]
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * SAMPLE_TEXTURE2D_LOD(_WSFX0_FogNoiseTex, sampler_WSFX0_FogNoiseTex, uv * frequency, 0).r;
                    maxValue += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                return value / maxValue;
            }

            // ---------------------------------------------------------
            // 5. 输入/输出 结构体
            // ---------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color  : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color : TEXCOORD1;
                half3 normalWS    : TEXCOORD2;
                half3 tangentWS   : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
            };

            // ---------------------------------------------------------
            // 6. 顶点着色器
            // ---------------------------------------------------------
            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vertexInput.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                float cosA = _RotationSinCos.x;
                float sinA = _RotationSinCos.y;
                half3 worldTangent = half3(cosA, sinA, 0);
                half3 worldBitangent = half3(-sinA, cosA, 0);
                half3 worldNormal = half3(0, 0, 1);

                OUT.tangentWS = worldTangent;
                OUT.bitangentWS = worldBitangent;
                OUT.normalWS = worldNormal;

                return OUT;
            }

            // ---------------------------------------------------------
            // 7. 片元着色器
            // ---------------------------------------------------------
            half4 Frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                #if defined(RCWB_EDITOR_SCENE_PREVIEW)
                return half4(albedo.rgb * IN.color.rgb, albedo.a * IN.color.a);
                #endif

                // 世界空间坐标
                float2 worldPos = posPixel2World(IN.positionCS.xy, _ScreenParams.xy, MatrixInvVP);
                // 室内外信息
                float io = SampleIOFieldClamped(worldPos, 1.0);
                float ioPow2 = io * io;

                // ====== 涟漪白水 ======
                half3 rainTex = SAMPLE_TEXTURE2D(_WSFX0_RainWaveTex, sampler_WSFX0_RainWaveTex, frac(worldPos.xy / _WSFX0_RainWaveScale));

                half3 valid = rainTex > half3(0, 0, 0) ? 1 : 0;
                half3 trueVal = clamp(rainTex - 1.0/256.0, 0, 1) * 256.0/255.0;
                float t = frac(_WSFX0_RainPhase / _WSFX0_RainPeriod);
                half3 dist = half3(
                    min(abs(trueVal.r - t), 1.0 - abs(trueVal.r - t)),
                    min(abs(trueVal.g - t), 1.0 - abs(trueVal.g - t)),
                    min(abs(trueVal.b - t), 1.0 - abs(trueVal.b - t))
                );
                float rainTimeThreshold = 0.03 / _WSFX0_RainPeriod;
                half rainWhiteWave = dot(valid * (dist < rainTimeThreshold), half3(1,1,1)) > 0 ? 1 : 0;

                // ====== 水塘遮罩 ======
                float puddleRaw =
                    SAMPLE_TEXTURE2D(_WSFX0_PuddleTex, sampler_WSFX0_PuddleTex, worldPos / _WSFX0_PuddleScale).r +
                    SAMPLE_TEXTURE2D(_WSFX0_PuddleTex, sampler_WSFX0_PuddleTex, worldPos / _WSFX0_PuddleScale * 2).r +
                    SAMPLE_TEXTURE2D(_WSFX0_PuddleTex, sampler_WSFX0_PuddleTex, worldPos / _WSFX0_PuddleScale * 4).r +
                    SAMPLE_TEXTURE2D(_WSFX0_PuddleTex, sampler_WSFX0_PuddleTex, worldPos / _WSFX0_PuddleScale * 8).r;
                puddleRaw /= 4.0f;

                float puddleMask = puddleRaw > 0.5f ? 1 : 0;
                float puddleDepth = puddleMask > .5f ? (puddleRaw - .5f) * 2.0f : 0;
                float puddleEdge0 = (puddleMask == 1) && (puddleDepth <= .005f);
                float puddleEdge1 = (puddleMask == 1) && (puddleDepth <= .02f) && (puddleDepth > .005f);
                float puddleFBM = FBM(worldPos + _Time.y * .05, 4) > .51f;
                puddleEdge1 *= puddleFBM;
                float puddleEdge = max(puddleEdge0, puddleEdge1);

                // 室内外过滤
                puddleMask *= ioPow2;
                puddleDepth *= ioPow2;
                puddleEdge *= ioPow2;
                rainWhiteWave *= puddleMask;

                // ====== 输出：alpha blend 模式 ======
                // 非水塘区域 alpha=0，完全透明
                clip(puddleMask - 0.001);

                // 水塘暗色：黑色 + alpha 模拟 exp(-depth*15) 衰减
                float puddleAlpha = 1.0 - exp(-puddleDepth * 15);

                // 白水（边缘 + 涟漪）：略亮颜色 + 小 alpha
                float whiteWave = saturate(max(puddleEdge, rainWhiteWave)) * 0.2;
                half3 outColor = whiteWave.xxx;
                float outAlpha = saturate(max(puddleAlpha, whiteWave));

                return half4(outColor, outAlpha);
            }
            ENDHLSL
        }

    }
}
