Shader "ProjectII/WorldSpaceFX_0"
{
    Properties
    {
        _BlitTexture ("Source Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "WorldSpaceFX_0"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/yaocenji.radiance-cascades-world-bvh/Shaders/IOField.hlsl"

            float4x4 MatrixInvVP;

            CBUFFER_START(UnityPerMaterial)
                // 全局变量：玩家属性
                float4 _Player_PosWS_Direction_Angle;

                float _WSFX0_RainWaveScale;
                float _WSFX0_PuddleScale;      // 水塘噪声世界空间缩放
                float4 _WSFX0_FogColor;
                float  _WSFX0_FogIntensity;
                float2 _WSFX0_FogNoiseScale;   // x=层1缩放, y=层2缩放
                float2 _WSFX0_FogSpeed1;       // 层1流动速度
                float2 _WSFX0_FogSpeed2;       // 层2流动速度
                int    _WSFX0_FogOctaves;

                // 距离衰减
                float  _WSFX0_FogDistFadeStart;
                float  _WSFX0_FogDistFadeEnd;

                // 时间系数
                float  _WSFX0_RainPhase;       // 周期性时间 [0, _WSFX0_RainPeriod)
                float  _WSFX0_RainPeriod;      // 周期长度
            CBUFFER_END

            TEXTURE2D(_WSFX0_RainWaveTex);
            SAMPLER(sampler_WSFX0_RainWaveTex);
            TEXTURE2D(_WSFX0_PuddleTex);
            SAMPLER(sampler_WSFX0_PuddleTex);
            //SAMPLER(sampler_PointClamp);
            TEXTURE2D(_WSFX0_FogNoiseTex);
            SAMPLER(sampler_WSFX0_FogNoiseTex);
            

            float2 UVToWorldPos(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                ndc.y = -ndc.y;
                #endif
                float4 posWSRaw = mul(MatrixInvVP, float4(ndc, 0.0, 1.0));
                return posWSRaw.xy / posWSRaw.w;
            }

            // FBM：用同一张噪声纹理多层叠加
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 worldPos = UVToWorldPos(uv);
                float io = SampleIOFieldClamped(worldPos, 1.0); // 0=室内, 1=室外

                // 涟漪来指导地面采样
                half3 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // 双层 FBM 雨雾
                float2 fogUV1 = worldPos / _WSFX0_FogNoiseScale.x + _Time.y * _WSFX0_FogSpeed1;
                float2 fogUV2 = worldPos / _WSFX0_FogNoiseScale.y + _Time.y * _WSFX0_FogSpeed2;

                int octaves = max(1, _WSFX0_FogOctaves);
                float fogNoise = FBM(fogUV1, octaves) * 0.6
                               + FBM(fogUV2, octaves) * 0.4;

                // 距离衰减：远处浓、近处淡
                float2 playerPos = _Player_PosWS_Direction_Angle.xy;
                float distToPlayer = length(worldPos - playerPos);
                float distAtten = smoothstep(_WSFX0_FogDistFadeStart, _WSFX0_FogDistFadeEnd, distToPlayer);

                // io 加权：室内无雾，室外全强度；距离衰减
                float fogAlpha = saturate(fogNoise * _WSFX0_FogIntensity * io * distAtten);
                float fogAlphaNormalized = saturate(fogNoise * io * distAtten);

                // 雾气本身的颜色
                half3 fogColor = fogAlpha * _WSFX0_FogColor.rgb;
                // 原本的颜色经过雾气的指数衰减
                float attenParam = exp(-fogAlphaNormalized * 5);
                half3 attenedSceneColor = sceneColor * attenParam;

                half3 finalColor = fogColor + attenedSceneColor;

                //return 1;
                //return half4(puddleEdge.rrr, 1);
                return half4(finalColor, 1);
            }
            ENDHLSL
        }
    }
}
