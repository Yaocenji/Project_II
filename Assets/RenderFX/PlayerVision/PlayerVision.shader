Shader "ProjectII/PlayerVision"
{
    Properties
    {
        // Blitter 框架要求：_BlitTexture 是当前帧画面输入
        _BlitTexture ("Source Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        // Pass 0: Dual Kawase Downsample
        Pass
        {
            Name "DualKawaseDown"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragKawaseDown

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float _KawaseOffset;
                float4 _BlitTexture_TexelSize;
            CBUFFER_END

            half4 FragKawaseDown(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float2 o = texelSize * (_KawaseOffset + 0.5);

                half4 sum = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv) * 4.0;
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x,  o.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( o.x,  o.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x, -o.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( o.x, -o.y));
                return sum / 8.0;
            }
            ENDHLSL
        }

        // Pass 1: Dual Kawase Upsample
        Pass
        {
            Name "DualKawaseUp"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment FragKawaseUp

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            CBUFFER_START(UnityPerMaterial)
                float _KawaseOffset;
                float4 _BlitTexture_TexelSize;
            CBUFFER_END

            half4 FragKawaseUp(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texelSize = _BlitTexture_TexelSize.xy;
                float2 o = texelSize * _KawaseOffset;

                half4 sum = half4(0, 0, 0, 0);
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x * 2.0, 0));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x,  o.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0,      o.y * 2.0));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( o.x,   o.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( o.x * 2.0, 0));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2( o.x,  -o.y));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0,      -o.y * 2.0));
                sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x,  -o.y));
                return sum / 8.0;
            }
            ENDHLSL
        }

        // Pass 2: PlayerVision 合成
        Pass
        {
            Name "PlayerVision"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

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

                // 调色参数（由 PlayerVisionFeature 写入）
                float _PlayerVision_Saturation;
                float _PlayerVision_Brightness;
                float _PlayerVision_Saturation_Far;
                float _PlayerVision_Brightness_Far;

                // 噪声参数（边界扰动）
                float _PlayerVision_NoiseWorldScale;
                float _PlayerVision_NoiseStrength;

                // S 曲线参数（分两套：inSight 和 notOccluded 各自独立）
                float _PlayerVision_ShadowEdge_Sight;
                float _PlayerVision_LightEdge_Sight;
                float _PlayerVision_ShadowEdge_Occlude;
                float _PlayerVision_LightEdge_Occlude;

                // 迷雾参数
                float4 _PlayerVision_FogColor;
                float  _PlayerVision_FogIntensity;
                float  _PlayerVision_FogDepthRange;
                float  _PlayerVision_FogBlendMode;
                float2 _PlayerVision_FogNoiseScale;   // x=层1缩放, y=层2缩放
                float2 _PlayerVision_FogNoiseSpeed1;  // 层1流动方向速度
                float2 _PlayerVision_FogNoiseSpeed2;  // 层2流动方向速度

                float _PlayerVision_BlurStartRadius;
                float _PlayerVision_BlurEndRadius;
                float _PlayerVision_GlobalStrength;
            CBUFFER_END

            TEXTURE2D(_PlayerVision_NoiseTex); SAMPLER(sampler_PlayerVision_NoiseTex);
            TEXTURE2D(_PlayerVision_BlurTex);  SAMPLER(sampler_PlayerVision_BlurTex);
            
            // ── BVH 数据（由 RC Feature 上传） ──
            // 压缩节点：内部节点存 AABB，叶子节点存边
            struct LBVHNodeGpu
            {
                float2 PosA;      // 内部节点: AABB Min；叶子节点: Edge Start
                float2 PosB;      // 内部节点: AABB Max；叶子节点: Edge End
                int    IndexData; // 内部节点: LeftChild；叶子节点: ~matIdx（< 0）
                int    RightChild;
            };

            StructuredBuffer<LBVHNodeGpu> _BVH_NodeEdge_Buffer;
            int _BVH_Root_Index;

            // ── 遮挡 flag 表（由 PlayerVisionOccludeSystem 每帧写入，索引即 matIdx） ──
            StructuredBuffer<int> _PlayerVision_OccludeFlags;
            int _PlayerVision_OccludeFlagsCount;

            bool IsExcludedMatIdx(int matIdx)
            {
                if (matIdx < 0 || matIdx >= _PlayerVision_OccludeFlagsCount) return false;
                return _PlayerVision_OccludeFlags[matIdx] == 0;
            }

            // ── 射线结构 ──
            struct RayWS
            {
                float2 Origin;
                float2 Direction;
            };

            struct HitResult
            {
                float2 hitPoint;
                float2 hitNormal;
            };

            // ── AABB 与射线求交 ──
            bool IntersectsRayAABB(RayWS ray, float2 invDir, LBVHNodeGpu node)
            {
                float2 t0 = (node.PosA - ray.Origin) * invDir;
                float2 t1 = (node.PosB - ray.Origin) * invDir;
                float2 tMinV = min(t0, t1);
                float2 tMaxV = max(t0, t1);
                float tEnter = max(tMinV.x, tMinV.y);
                float tExit  = min(tMaxV.x, tMaxV.y);
                return (tExit >= tEnter) && (tExit >= 0.0f);
            }

            // ── 射线与线段求交，返回射线参数 t（< 0 表示未命中） ──
            float IntersectsRaySegmentT(RayWS ray, LBVHNodeGpu leafNode)
            {
                float2 p = ray.Origin;
                float2 r = ray.Direction;
                float2 q = leafNode.PosA;
                float2 s = leafNode.PosB - leafNode.PosA;

                float rCrossS = r.x * s.y - r.y * s.x;
                if (abs(rCrossS) < 1e-7) return -1.0;

                float2 qp = q - p;
                float t = (qp.x * s.y - qp.y * s.x) / rCrossS;
                float u = (qp.x * r.y - qp.y * r.x) / rCrossS;

                if (t > 0.0f && u >= 0.0f && u <= 1.0f)
                    return t;
                return -1.0;
            }

            // ── BVH 遍历：在 [0, maxDist] 内是否有交点 ──
            //    有则返回 true（最近命中距离写入 hitDist）
            #define MAX_RECUR_DEEP 32
            bool IntersectRayBVH_Shadow(RayWS ray, float maxDist, out float hitDist)
            {
                hitDist = maxDist;
                bool found = false;

                if (_BVH_Root_Index == -1) return false;

                // 防零除
                float2 dir = ray.Direction;
                if (abs(dir.x) < 1e-9) dir.x = sign(dir.x + 1e-10) * 1e-9;
                if (abs(dir.y) < 1e-9) dir.y = sign(dir.y + 1e-10) * 1e-9;
                float2 invDir = 1.0 / dir;

                int nodeStack[MAX_RECUR_DEEP];
                int stackTop = 0;
                nodeStack[0] = _BVH_Root_Index;

                [loop]
                while (stackTop >= 0)
                {
                    int idx = nodeStack[stackTop--];
                    if (idx == -1) continue;

                    LBVHNodeGpu node = _BVH_NodeEdge_Buffer[idx];
                    bool isLeaf = (node.IndexData < 0);

                    if (isLeaf)
                    {
                        // 排除指定的 matIdx（如玩家自身 Polygon）
                        int matIdx = ~node.IndexData;
                        if (IsExcludedMatIdx(matIdx)) continue;

                        float t = IntersectsRaySegmentT(ray, node);
                        if (t > 0.01 && t < hitDist)
                        {
                            hitDist = t;
                            found = true;
                        }
                    }
                    else
                    {
                        if (!IntersectsRayAABB(ray, invDir, node)) continue;
                        if (stackTop < MAX_RECUR_DEEP - 2)
                        {
                            nodeStack[++stackTop] = node.RightChild;
                            nodeStack[++stackTop] = node.IndexData; // LeftChild
                        }
                    }
                }
                return found;
            }

            // ── UV → 世界空间（与 RC Feature 中 posPixel2World 完全一致） ──
            float2 UVToWorldPos(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                ndc.y = -ndc.y;
                #endif
                float4 posWSRaw = mul(MatrixInvVP, float4(ndc, 0.0, 1.0));
                return posWSRaw.xy / posWSRaw.w;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                // ── 采样原始画面 ──
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // 玩家世界坐标
                float2 playerPos = _Player_PosWS_Direction_Angle.xy;
                
                // ── 距离模糊：玩家到片元世界空间距离，混合降采样模糊纹理 ──
                float2 rawFragWorldPos = UVToWorldPos(uv);
                float distToPlayer = length(rawFragWorldPos - playerPos);
                float blurWeight = smoothstep(_PlayerVision_BlurStartRadius, _PlayerVision_BlurEndRadius, distToPlayer);
                half4 blurColor = SAMPLE_TEXTURE2D(_PlayerVision_BlurTex, sampler_PlayerVision_BlurTex, uv);
                half3 finalColorBlurred = lerp(sceneColor.xyz, blurColor.rgb, blurWeight);


                // 1. 将当前片元重建为世界空间坐标
                float2 fragWorldPos = UVToWorldPos(uv);

                // ── 噪声扰动：用世界坐标采样，偏移 fragWorldPos ──
                float2 noiseUV_X = fragWorldPos / _PlayerVision_NoiseWorldScale;
                float2 noiseUV_Y = noiseUV_X + float2(0.37, 0.63);
                float noiseX = SAMPLE_TEXTURE2D(_PlayerVision_NoiseTex, sampler_PlayerVision_NoiseTex, noiseUV_X).r * 2.0 - 1.0;
                float noiseY = SAMPLE_TEXTURE2D(_PlayerVision_NoiseTex, sampler_PlayerVision_NoiseTex, noiseUV_Y).r * 2.0 - 1.0;
                fragWorldPos += float2(noiseX, noiseY) * _PlayerVision_NoiseStrength;

                // 3. 从玩家出发射向片元的射线
                float2 toFrag = fragWorldPos - playerPos;
                float distToFrag = length(toFrag);

                // 公共参数
                float featherDist = _Player_Radius_Eye_Inner_Outter_Blank.y;

                // ── 判据1：BVH 遮挡，带空间羽化 ──
                float hitDist;
                float criterion1 = 1.0;
                float2 fragDir = float2(0, 0);
                float distHitToFrag = distToFrag; // 未命中时退化为玩家到片元距离
                if (distToFrag > 1e-4)
                {
                    fragDir = toFrag / distToFrag;
                    RayWS ray;
                    ray.Origin    = playerPos;
                    ray.Direction = fragDir;

                    bool hasHit = IntersectRayBVH_Shadow(ray, distToFrag, hitDist);
                    if (hasHit && hitDist < distToFrag - 0.02)
                    {
                        // 交点世界坐标
                        float2 hitWorldPos = playerPos + fragDir * hitDist;
                        // 交点到片元的距离
                        distHitToFrag = distance(hitWorldPos, fragWorldPos);
                        criterion1 = 1.0 - smoothstep(0.0, featherDist, distHitToFrag);
                    }
                }

                // ── 判据2：视野角（扇形），带空间羽化 ──
                float forwardAngleRad = _Player_PosWS_Direction_Angle.z * (3.14159265 / 180.0);
                float2 forwardDir = float2(cos(forwardAngleRad), sin(forwardAngleRad));
                float halfAngleRad = _Player_PosWS_Direction_Angle.w * (3.14159265 / 180.0);

                float criterion2 = 0.0;
                if (distToFrag > 1e-4)
                {
                    float cosHalf = cos(halfAngleRad);
                    float cosAngle = dot(fragDir, forwardDir);

                    if (cosAngle >= cosHalf)
                    {
                        // 扇形内
                        criterion2 = 1.0;
                    }
                    else
                    {
                        // 扇形外：计算片元到最近扇形射线（半直线）的世界空间距离
                        // 左右两条边射线
                        float2 rayL = float2(cos(forwardAngleRad + halfAngleRad), sin(forwardAngleRad + halfAngleRad));
                        float2 rayR = float2(cos(forwardAngleRad - halfAngleRad), sin(forwardAngleRad - halfAngleRad));

                        // 点到半直线距离：投影若 < 0 则最近点是 playerPos
                        float projL = dot(toFrag, rayL);
                        float distL = projL > 0.0
                            ? length(toFrag - rayL * projL)
                            : distToFrag;

                        float projR = dot(toFrag, rayR);
                        float distR = projR > 0.0
                            ? length(toFrag - rayR * projR)
                            : distToFrag;

                        float distToSector = min(distL, distR);
                        criterion2 = 1.0 - smoothstep(0.0, featherDist, distToSector);
                    }
                }

                // ── 判据3：近身距离，带空间羽化 ──
                float nearRadius = _Player_Radius_Eye_Inner_Outter_Blank.x;
                float criterion3 = 1.0 - smoothstep(nearRadius, nearRadius + featherDist, distToFrag);

                // ── 拆分两个独立通道 ──
                float inSight     = saturate(criterion2 + criterion3); // 视野范围内
                float notOccluded = criterion1;                         // 未被遮挡

                // S 曲线各自独立重映射
                inSight     = smoothstep(_PlayerVision_ShadowEdge_Sight,   _PlayerVision_LightEdge_Sight,   inSight);
                notOccluded = smoothstep(_PlayerVision_ShadowEdge_Occlude, _PlayerVision_LightEdge_Occlude, notOccluded);
                
                // ── 深度感：交点到片元的距离决定迷雾深浅，以及决定调暗的深潜 ──
                float occludeDepth = min(distToFrag, distHitToFrag);
                float depthFactor  = saturate(occludeDepth / max(_PlayerVision_FogDepthRange, 0.001));

                // ── 第一步：inSight 调色（视野外压暗，系数随遮挡深度lerp） ──
                float sat = lerp(_PlayerVision_Saturation, _PlayerVision_Saturation_Far, depthFactor);
                float bri = lerp(_PlayerVision_Brightness, _PlayerVision_Brightness_Far, depthFactor);
                float lum = dot(finalColorBlurred.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 desaturated = lerp((half3)lum, finalColorBlurred.rgb, sat);
                half3 colorOutOfSight = desaturated * bri;
                half3 afterSight = lerp(colorOutOfSight, finalColorBlurred.rgb, inSight * notOccluded);

                // ── 第二步：迷雾 ──
                float2 rawWorldPos = UVToWorldPos(uv);
                float2 fogUV1 = rawWorldPos / _PlayerVision_FogNoiseScale.x + _Time.y * _PlayerVision_FogNoiseSpeed1;
                float2 fogUV2 = rawWorldPos / _PlayerVision_FogNoiseScale.y + _Time.y * _PlayerVision_FogNoiseSpeed2;
                float fogNoise = SAMPLE_TEXTURE2D(_PlayerVision_NoiseTex, sampler_PlayerVision_NoiseTex, fogUV1).r * 0.6
                               + SAMPLE_TEXTURE2D(_PlayerVision_NoiseTex, sampler_PlayerVision_NoiseTex, fogUV2).r * 0.4;

                // 迷雾颜色固定，浓度随遮挡深度增加
                half3 fogColorFinal = _PlayerVision_FogColor.rgb;
                float fogAlpha = saturate(fogNoise * _PlayerVision_FogIntensity * depthFactor);

                // ── 混合模式：乘法(保留结构) 与 屏幕(暗区透光) 按权重混合 ──
                half3 fogMultiply = afterSight * lerp((half3)1.0, fogColorFinal, fogAlpha);
                half3 fogScreen   = 1.0 - (1.0 - afterSight) * (1.0 - fogColorFinal * fogAlpha);
                half3 colorOccluded = lerp(fogMultiply, fogScreen, _PlayerVision_FogBlendMode);
                half3 finalColor = lerp(colorOccluded, afterSight, notOccluded);

                //return depthFactor;

                //return blurWeight;
                return half4(lerp(sceneColor.rgb, finalColor.rgb, _PlayerVision_GlobalStrength), 1);
            }
            ENDHLSL
        }
    }
}
