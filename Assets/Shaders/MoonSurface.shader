// シンプルなランバート拡散+法線マップ月面シェーダー
// 複雑な URP PBR を避けて確実にコンパイルされる実装
Shader "MoonObserver/MoonSurface"
{
    Properties
    {
        [MainTexture]
        _BaseMap        ("Color Map (NASA LROC)", 2D)         = "gray" {}
        [Normal]
        _BumpMap        ("Normal Map", 2D)                    = "bump" {}
        _BumpScale      ("Normal Scale", Float)               = 1.0
        _AtmosphericTint("Atmospheric Tint", Color)           = (1,1,1,1)
        _AmbientMin     ("Ambient (dark side)", Range(0,0.3)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderType"    = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"         = "Geometry"
        }

        // ── ForwardLit ──────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float  _BumpScale;
                float4 _AtmosphericTint;
                float  _AmbientMin;
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
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS    = normInputs.normalWS;
                OUT.tangentWS   = normInputs.tangentWS;
                OUT.bitangentWS = normInputs.bitangentWS;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // テクスチャ (NASA テクスチャがなければグレー)
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // 法線マップ
                float3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                float3x3 TBN    = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                // ランバート拡散 + 最低輝度 (暗い側も真っ黒にならない)
                Light mainLight = GetMainLight();
                float NdotL = max(_AmbientMin, dot(normalWS, normalize(mainLight.direction)));

                float3 color = baseColor.rgb * NdotL * mainLight.color.rgb * _AtmosphericTint.rgb;
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ── ShadowCaster (最小実装) ────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttr { float4 posOS : POSITION; float3 normOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 ShadowVert(ShadowAttr IN) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 posWS  = TransformObjectToWorld(IN.posOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 ld = normalize(_LightPosition - posWS);
                #else
                    float3 ld = _LightDirection;
                #endif
                // ApplyShadowBias の代わりに手動バイアス (URP 17 Metal 互換)
                posWS += normWS * 0.005 + ld * 0.005;
                return TransformWorldToHClip(posWS);
            }
            half4 ShadowFrag() : SV_Target { return half4(0,0,0,0); }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
