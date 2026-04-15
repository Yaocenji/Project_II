Shader "RadianceCascadesWB/Foreground_Object"
{
    Properties
    {
        [Header(Textures)]
        [PerRendererData] _MainTex ("Albedo (RGB) Alpha (A)", 2D) = "white" {}
        [PerRendererData] _BumpMap ("Normal Map", 2D) = "bump" {}
        [PerRendererData] _SDFTex  ("SDF (R: signed distance, 0.5=boundary)", 2D) = "gray" {}

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

            // RCWB库
            #include "Packages/yaocenji.radiance-cascades-world-bvh/Shaders/RCW_BVH_Inc.hlsl"
            #include "Packages/yaocenji.radiance-cascades-world-bvh/Shaders/SpotLight2D_Inc.hlsl"

            // ---------------------------------------------------------
            // 1. CBUFFER 定义 (严格匹配 SRP Batcher)
            // ---------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                half4 _Emission;

                // 全局变量：玩家属性
                float4 _Player_PosWS_Direction_Angle;
                float4 _Player_Radius_Eye_Inner_Outter_Blank;

                // 鼠标位置
                float4 _FG_MousePosition;

                float2 _RotationSinCos; // x=cos, y=sin
                float _GICoefficient;

                // 摄像机矩阵
                float4x4 MatrixInvVP;
                float4x4 MatrixVP;
                // 上一帧的摄像机矩阵
                float4x4 MatrixVP_Prev;
                float4x4 MatrixInvVP_Prev;
            
                float4 _ForegroundTransformData;

                float _VirtualHeight;
                float _MaxHeight;

                // xy=UV offset，zw=UV scale
                // 将 IN.uv（可能是 atlas UV 或含 padding 的 blurSprite UV）映射到精灵本地 UV (0,0)-(1,1)
                float4 _SDFLocalUVTransform;

                // =1，则人物靠近变得透明；=0，则不这样
                int _CloseTransparent;
                float _SDFWorldScale;
            CBUFFER_END

            float _RCWB_GI_Height;

            // ---------------------------------------------------------
            // 2. 纹理定义 (分离采样器以提高性能)
            // ---------------------------------------------------------
            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SDFTex);         SAMPLER(sampler_SDFTex);
            TEXTURE2D(_RCWB_HistoryColor);SAMPLER(sampler_RCWB_HistoryColor);

            // ---------------------------------------------------------
            // 3. 输入/输出 结构体
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
            // 4. 顶点着色器
            // ---------------------------------------------------------
            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                // 顶点变换
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vertexInput.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                // 手动构建 TBN
                float cosA = _RotationSinCos.x;
                float sinA = _RotationSinCos.y;
                // [ cos  -sin ]
                // [ sin   cos ]
                half3 worldTangent = half3(cosA, sinA, 0);
                half3 worldBitangent = half3(-sinA, cosA, 0);
                half3 worldNormal = half3(0, 0, 1); 

                // 赋值
                OUT.tangentWS = worldTangent;
                OUT.bitangentWS = worldBitangent;
                OUT.normalWS = worldNormal;

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                #if defined(RCWB_EDITOR_SCENE_PREVIEW)
                return half4(albedo.rgb * IN.color.rgb, albedo.a * IN.color.a);
                #endif

                // 世界空间
                float2 posWS = posPixel2World(IN.positionCS.xy, _ScreenParams.xy, MatrixInvVP);
                // 屏幕空间uv
                float2 screenUV = IN.positionCS.xy / _ScreenParams.xy;

                // 法线
                half4 packednorm = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv);
                half3 unpackednorm = UnpackNormal(packednorm);
                unpackednorm = normalize(unpackednorm);
                half3 normalWS = mul(half3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS), unpackednorm);

                // RCWB GI
                float2 sdfUV   = (IN.uv - _SDFLocalUVTransform.xy) / _SDFLocalUVTransform.zw;
                float sdfValue = SAMPLE_TEXTURE2D(_SDFTex, sampler_SDFTex, sdfUV).r * _SDFWorldScale;
                // SDF低于这个的，才会被照亮；
                float lumMaxSDF = _RCWB_GI_Height * pow(_VirtualHeight, -1);
                // 通过SDF计算照亮系数
                float lumParam = sdfValue <= lumMaxSDF ? 1 - max(0,sdfValue) / lumMaxSDF : 0;
                
                RcwbLightData lightRCWBGI = GetBlurRcwbLightData(screenUV,  _ScreenParams.xy, MatrixInvVP);

                if (length(_Emission.rgb) > 0.0001f)
                {
                    lightRCWBGI.color = _Emission.rgb;
                }
                
                // 计算安全统一规范化向量
                float directionLength = length(lightRCWBGI.direction.xy);

                // 使用统一的兰伯特函数计算 RCWB GI 光照
                half3 realDirectionRCWBGI = normalize(half3(normalize(lightRCWBGI.direction.xy), _RCWB_GI_Height - _RCWB_GI_Height * _VirtualHeight / _MaxHeight));
                half lambertRCWBGI = CalculateLighting(normalWS, realDirectionRCWBGI);

                // 物体内部的光，为了过渡平滑，需要乘上这个
                lambertRCWBGI *= clamp(directionLength, 0, 1);

                // SpotLight2D（带阴影和兰伯特）
                // fragmentZ = 0 表示片元在 Z=0 平面上

                // 全局光
                float3 globalLight = .0;
                
                half3 ansColor = IN.color.xyz * albedo.xyz * (_GICoefficient * lumParam * lightRCWBGI.color * lambertRCWBGI + globalLight);

                half alpha = albedo.a * IN.color.a;

                // 特效
                // 1 离人物越近越透明
                float distancePlayer = length(posWS - _Player_PosWS_Direction_Angle.xy);
                alpha *= _CloseTransparent == 1 ? smoothstep(.3, 1, distancePlayer) : 1;
                // 2 离鼠标越近越透明
                float distanceMouse = length(posWS - _FG_MousePosition.xy);
                alpha *= _CloseTransparent == 1 ? smoothstep(.3, 1, distanceMouse) : 1;

                //return lumParam;
                
                return half4(ansColor, alpha);
            }
            ENDHLSL
        }

    }
}
