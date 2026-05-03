Shader "ProjectII/GroundSplatMaps"
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
        // 渲染队列根据需要调整，通常墙壁是不透明的 (Geometry)
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }

        // =================================================================================
        // Pass 1: Universal2D
        // 作用：主相机渲染，玩家看到的最终画面 (Albedo + Normal Lighting)
        // =================================================================================
        Pass
        {
            Name "Universal2D"
            Tags { "Queue" = "Overlay" "LightMode"="Universal2D" }

            // 混合模式根据需求，墙壁通常是不透明 (One Zero)
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ RCWB_EDITOR_SCENE_PREVIEW
            #pragma multi_compile_fragment _ ENABLE_TRANSLUCENT_OBJECTS
            
            // 引入 URP 核心库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---------------------------------------------------------
            // 1. CBUFFER 定义 (严格匹配 SRP Batcher)
            // ---------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
            CBUFFER_END

            float _RCWB_GI_Height;

            // ---------------------------------------------------------
            // 2. 纹理定义 (分离采样器以提高性能)
            // ---------------------------------------------------------
            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);

            // ---------------------------------------------------------
            // 3. 输入/输出 结构体
            // ---------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // ---------------------------------------------------------
            // 4. 顶点着色器
            // ---------------------------------------------------------
            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                // 顶点变换
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vertexInput.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                
                return half4(albedo.rgb, .5f);
            }
            ENDHLSL
        }

    }
}
