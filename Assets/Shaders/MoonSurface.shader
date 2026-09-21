// 月面シェーダー (URP 17 / Unity 6 対応・最小実装)
// ShadowCaster パスを除去して確実にコンパイルされる構成
Shader "MoonObserver/MoonSurface"
{
    Properties
    {
        [MainTexture]
        _BaseMap        ("Color Map (NASA LROC)", 2D) = "gray" {}
        _AtmosphericTint("Atmospheric Tint", Color)   = (1,1,1,1)
        _AmbientMin     ("Ambient (dark side)", Range(0,0.5)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _AtmosphericTint;
                half   _AmbientMin;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS  : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float3 normWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.posCS  = TransformObjectToHClip(IN.posOS.xyz);
                OUT.uv     = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normWS = TransformObjectToWorldNormal(IN.normOS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                half4 base  = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                Light light = GetMainLight();
                half  NdotL = max(_AmbientMin,
                                  dot(normalize(IN.normWS), normalize(light.direction)));
                half3 color = base.rgb * NdotL * _AtmosphericTint.rgb;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    // ShadowCaster は省略 (月は影を落とさなくてよい)
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
