// 月面シェーダー (URP 17 / Unity 6 / Metal 対応・最小実装)
//
// 重要: CBUFFER_START(UnityPerMaterial) 内は必ず float / float4 を使うこと。
//       Metal + SRP Batcher は cbuffer 内の half 型に厳格でコンパイルエラーになる。
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _AtmosphericTint;
                float  _AmbientMin;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS  : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float3 normWS : TEXCOORD1;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS  = TransformObjectToHClip(IN.posOS.xyz);
                OUT.uv     = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normWS = TransformObjectToWorldNormal(IN.normOS);
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                Light  mainLight = GetMainLight();
                float3 n = normalize(IN.normWS);
                float3 l = normalize(mainLight.direction);
                float  NdotL = max(_AmbientMin, dot(n, l));

                float3 color = baseColor.rgb * NdotL * _AtmosphericTint.rgb;
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }

    // ShadowCaster は省略 (月は影を落とす必要がない)
    FallBack "Universal Render Pipeline/Unlit"
}
