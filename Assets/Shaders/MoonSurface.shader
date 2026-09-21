// URP 14.x 月面マテリアルシェーダー
// NASA CGI Moon Kit テクスチャを使用した物理ベースのレンダリング
Shader "MoonObserver/MoonSurface"
{
    Properties
    {
        [MainTexture]
        _BaseMap        ("Color Map (NASA LROC)", 2D)   = "white" {}
        [Normal]
        _BumpMap        ("Normal Map", 2D)              = "bump" {}
        _BumpScale      ("Normal Scale", Float)         = 1.0
        _Smoothness     ("Smoothness", Range(0,1))      = 0.05
        _Metallic       ("Metallic", Range(0,1))        = 0.0
        _OcclusionMap   ("Occlusion Map", 2D)           = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 0.5

        // 大気色ティント (AtmosphericEffects から更新)
        _AtmosphericTint("Atmospheric Tint", Color)     = (1,1,1,1)
        // 満ち欠けマスク: 位相角から CPU で更新
        _PhaseAngle     ("Phase Angle (deg)", Float)    = 0.0
        _EnablePhase    ("Enable Phase Mask", Float)    = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile _ REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);     SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap);SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float  _BumpScale;
                float  _Smoothness;
                float  _Metallic;
                float  _OcclusionStrength;
                float4 _AtmosphericTint;
                float  _PhaseAngle;
                float  _EnablePhase;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD5;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.tangentWS   = normInputs.tangentWS;
                OUT.bitangentWS = normInputs.bitangentWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    OUT.shadowCoord = GetShadowCoord(posInputs);
                #endif
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // テクスチャ
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 normalTS  = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                float occlusion  = lerp(1.0,
                    SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, IN.uv).r,
                    _OcclusionStrength);

                // TBN → ワールド法線
                float3x3 TBN = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                // PBR 入力構造体
                SurfaceData surfData;
                ZERO_INITIALIZE(SurfaceData, surfData);
                surfData.albedo     = baseColor.rgb;
                surfData.metallic   = _Metallic;
                surfData.smoothness = _Smoothness;
                surfData.normalTS   = normalTS;
                surfData.occlusion  = occlusion;
                surfData.alpha      = baseColor.a;

                InputData inputData;
                ZERO_INITIALIZE(InputData, inputData);
                inputData.positionWS     = IN.positionWS;
                inputData.normalWS       = normalWS;
                inputData.viewDirectionWS = normalize(GetCameraPositionWS() - IN.positionWS);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = IN.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord       = 0;
                inputData.vertexLighting = half3(0,0,0);
                inputData.bakedGI        = half3(0,0,0);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask     = half4(1, 1, 1, 1);

                // URP PBR ライティング
                half4 color = UniversalFragmentPBR(inputData, surfData);

                // 大気色ティント (AtmosphericEffects から注入)
                color.rgb *= _AtmosphericTint.rgb;

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
