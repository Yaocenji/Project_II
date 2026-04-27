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
            #include "Packages/yaocenji.radiance-cascades-world-bvh/Shaders/RCW_BVH_Inc.hlsl"
            #include "Packages/yaocenji.radiance-cascades-world-bvh/Shaders/IOField.hlsl"

            CBUFFER_START(UnityPerMaterial)
                // 全局变量：玩家属性
                float4 _Player_PosWS_Direction_Angle;
                float4 _Player_Radius_Eye_Inner_Outter_Blank;

                // 摄像机矩阵
                float4x4 MatrixInvVP;

                // 调色参数（由 PlayerVisionFeature 写入）
                // Near = 玩家近处/完全可见；Far = 距离远或完全遮挡（两者复用同一套）
                float _PlayerVision_Saturation_Near;
                float _PlayerVision_Brightness_Near;
                float _PlayerVision_Saturation_Far;
                float _PlayerVision_Brightness_Far;
                // 调色距离渐变范围（世界空间）
                float _PlayerVision_DistFadeStart;
                float _PlayerVision_DistFadeEnd;

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

            TEXTURE2D(_PlayerVision_NoiseTexX);   SAMPLER(sampler_PlayerVision_NoiseTexX);
            TEXTURE2D(_PlayerVision_NoiseTexY);   SAMPLER(sampler_PlayerVision_NoiseTexY);
            TEXTURE2D(_PlayerVision_FogNoiseTex); SAMPLER(sampler_PlayerVision_FogNoiseTex);
            TEXTURE2D(_PlayerVision_BlurTex);     SAMPLER(sampler_PlayerVision_BlurTex);

            // ── 遮挡 flag 表（由 PlayerVisionOccludeSystem 每帧写入，索引即 matIdx） ──
            StructuredBuffer<int> _PlayerVision_OccludeFlags;
            int _PlayerVision_OccludeFlagsCount;

            bool IsExcludedMatIdx(int matIdx)
            {
                if (matIdx < 0 || matIdx >= _PlayerVision_OccludeFlagsCount) return false;
                return _PlayerVision_OccludeFlags[matIdx] == 0;
            }

            // ── BVH 多交点收集（带排除逻辑），最多收集 MAX_INTERSECTS 个 ──
            // 若收集满后仍有未排除的交点（即遮挡物超出容量），直接返回 false 表示完全遮挡
            bool IntersectRayBVHArray_Vision(RayWS ray, float maxDistance, out IntersectsRaySegmentResultArray result)
            {
                result.intersectsCount = 0;
                [unroll]
                for (int i = 0; i < MAX_INTERSECTS; i++)
                {
                    result.results[i].hitPoint  = float2(0, 0);
                    result.results[i].hitNormal = float2(0, 0);
                    result.results[i].nodeIndex = -1;
                    result.results[i].matIdx    = -1;
                }

                float hitDistances[MAX_INTERSECTS];
                [unroll]
                for (int j = 0; j < MAX_INTERSECTS; j++) hitDistances[j] = 1e30;

                if (_BVH_Root_Index == -1) return true; // 无 BVH，完全透明

                float2 dir = ray.Direction;
                if (abs(dir.x) < 1e-9) dir.x = 1e-9 * (dir.x >= 0 ? 1.0 : -1.0);
                if (abs(dir.y) < 1e-9) dir.y = 1e-9 * (dir.y >= 0 ? 1.0 : -1.0);
                float2 invDir = 1.0 / dir;

                int nodeStack[MAX_RECUR_DEEP];
                int stackTop = 0;
                nodeStack[0] = _BVH_Root_Index;

                [loop]
                while (stackTop >= 0)
                {
                    int nodeIdx = nodeStack[stackTop--];
                    if (nodeIdx == -1) continue;

                    LBVHNodeGpu node = _BVH_NodeEdge_Buffer[nodeIdx];
                    bool isLeaf = (node.IndexData < 0);

                    if (isLeaf)
                    {
                        int matIdx = ~node.IndexData;
                        if (IsExcludedMatIdx(matIdx)) continue;

                        IntersectsRaySegmentResult tempResult;
                        if (!IntersectsRaySegment(ray, node, matIdx, tempResult)) continue;

                        float dist = distance(ray.Origin, tempResult.hitPoint);
                        if (dist <= 0.01 || dist > maxDistance) continue;

                        // 容量已满：存在更多有效遮挡，直接完全遮挡
                        if (result.intersectsCount == MAX_INTERSECTS)
                            return false;

                        // 插入排序
                        int insertPos = result.intersectsCount;
                        [unroll]
                        for (int k = 0; k < MAX_INTERSECTS; k++)
                        {
                            if (dist < hitDistances[k])
                                insertPos = min(insertPos, k);
                        }

                        if (insertPos < MAX_INTERSECTS)
                        {
                            [unroll]
                            for (int m = MAX_INTERSECTS - 1; m > 0; m--)
                            {
                                if (m > insertPos)
                                {
                                    result.results[m]  = result.results[m - 1];
                                    hitDistances[m]    = hitDistances[m - 1];
                                }
                            }
                            [unroll]
                            for (int n = 0; n < MAX_INTERSECTS; n++)
                            {
                                if (n == insertPos)
                                {
                                    result.results[n]            = tempResult;
                                    result.results[n].nodeIndex  = nodeIdx;
                                    hitDistances[n]              = dist;
                                }
                            }
                            if (result.intersectsCount < MAX_INTERSECTS)
                                result.intersectsCount++;
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
                return true;
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
                float2 noiseUV = fragWorldPos / _PlayerVision_NoiseWorldScale;
                noiseUV = frac(noiseUV);
                float noiseX = SAMPLE_TEXTURE2D(_PlayerVision_NoiseTexX, sampler_PlayerVision_NoiseTexX, noiseUV).r * 2.0f - 1.0;
                float noiseY = SAMPLE_TEXTURE2D(_PlayerVision_NoiseTexY, sampler_PlayerVision_NoiseTexY, noiseUV).r * 2.0f - 1.0;
                fragWorldPos += float2(noiseX, noiseY) * _PlayerVision_NoiseStrength;

                // 3. 从玩家出发射向片元的射线
                float2 toFrag = fragWorldPos - playerPos;
                float distToFrag = length(toFrag);

                // 公共参数
                float featherDist = _Player_Radius_Eye_Inner_Outter_Blank.y;

                // ── 判据1：BVH 遮挡，Beer-Lambert 指数衰减（density 决定半透明程度） ──
                float criterion1 = 1.0;
                float2 fragDir = float2(0, 0);
                float distHitToFrag = distToFrag; // 未命中时退化为玩家到片元距离
                if (distToFrag > 1e-4)
                {
                    fragDir = toFrag / distToFrag;
                    RayWS ray;
                    ray.Origin    = playerPos;
                    ray.Direction = fragDir;

                    IntersectsRaySegmentResultArray hitArray;
                    // 返回 false 表示超出容量仍有遮挡，直接完全遮挡
                    bool withinCapacity = IntersectRayBVHArray_Vision(ray, distToFrag, hitArray);
                    if (!withinCapacity)
                    {
                        criterion1 = 0.0;
                        distHitToFrag = 0.0;
                    }
                    else if (hitArray.intersectsCount > 0)
                    {
                        RayMarchingInterval intervals[MAX_RAYMARCHING_INTERVALS];
                        int intervalCount = 0;
                        GetIntervals(ray, hitArray, distToFrag, intervals, intervalCount);

                        float transmittance = 1.0;
                        for (int ii = 0; ii < intervalCount; ii++)
                        {
                            MaterialData mat = _BVH_Material_Buffer[intervals[ii].matIdx];
                            float segDist = length(intervals[ii].end - intervals[ii].start);
                            transmittance *= exp(-segDist * mat.Density);
                            if (transmittance < 0.001) { transmittance = 0.0; break; }
                        }
                        criterion1 = transmittance;

                        // 用最近交点估算 distHitToFrag（供 depthFactor 使用）
                        if (intervalCount > 0)
                            distHitToFrag = distance(intervals[0].start, fragWorldPos);
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

                // ── 调色：distFactor 基于玩家距离，notOccluded 作为加速器将 t 压向 Far 端 ──
                // notOccluded=1：t = distFactor（正常距离驱动）
                // notOccluded=0：t = 0（直接取最暗/最灰的 Far 值）
                float distFactor = 1.0 - smoothstep(_PlayerVision_DistFadeStart, _PlayerVision_DistFadeEnd, distToPlayer);
                float colorT = lerp(0.0, distFactor, notOccluded); // notOccluded 加速 distFactor 向 0 压
                float sat = lerp(_PlayerVision_Saturation_Far, _PlayerVision_Saturation_Near, colorT);
                float bri = lerp(_PlayerVision_Brightness_Far, _PlayerVision_Brightness_Near, colorT);
                float lum = dot(finalColorBlurred.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 desaturated = lerp((half3)lum, finalColorBlurred.rgb, sat);
                half3 colorToned = desaturated * bri;
                // inSight=1 且 notOccluded=1（完全可见）时还原原色；其余情况显示调色结果
                half3 afterSight = lerp(colorToned, finalColorBlurred.rgb, inSight * notOccluded);

                // ── 深度感：遮挡交点到片元的距离驱动迷雾（与调色距离独立） ──
                float depthFactor = saturate(distHitToFrag / max(_PlayerVision_FogDepthRange, 0.001));

                // ── 迷雾：双层噪声流动，浓度随遮挡深度增加 ──
                float2 rawWorldPos = UVToWorldPos(uv);
                float2 fogUV1 = rawWorldPos / _PlayerVision_FogNoiseScale.x + _Time.y * _PlayerVision_FogNoiseSpeed1;
                float2 fogUV2 = rawWorldPos / _PlayerVision_FogNoiseScale.y + _Time.y * _PlayerVision_FogNoiseSpeed2;
                float fogNoise = SAMPLE_TEXTURE2D(_PlayerVision_FogNoiseTex, sampler_PlayerVision_FogNoiseTex, fogUV1).r * 0.6
                               + SAMPLE_TEXTURE2D(_PlayerVision_FogNoiseTex, sampler_PlayerVision_FogNoiseTex, fogUV2).r * 0.4;
                half3 fogColorFinal = _PlayerVision_FogColor.rgb;
                float fogAlpha = saturate(fogNoise * _PlayerVision_FogIntensity * depthFactor);

                // 混合模式：乘法（保留结构）与屏幕（暗区透光）按权重混合
                half3 fogMultiply = afterSight * lerp((half3)1.0, fogColorFinal, fogAlpha);
                half3 fogScreen   = 1.0 - (1.0 - afterSight) * (1.0 - fogColorFinal * fogAlpha);
                half3 colorOccluded = lerp(fogMultiply, fogScreen, _PlayerVision_FogBlendMode);
                // notOccluded=1 时绕过迷雾直接用 afterSight，遮挡区才叠迷雾
                half3 finalColor = lerp(colorOccluded, afterSight, notOccluded);

                //return half4(noiseX, noiseY, 0, 1) * 5;
                
                return half4(lerp(sceneColor.rgb, finalColor.rgb, _PlayerVision_GlobalStrength), 1);
            }
            ENDHLSL
        }
    }
}
