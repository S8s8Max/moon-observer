// 星空メッシュ用シェーダー — 頂点カラーで各星の輝度を表現
// URP 17 / Unity 6 / Metal 対応
//
// 重要: CBUFFER_START(UnityPerMaterial) 内は float / float4 のみ (Metal 互換)
Shader "MoonObserver/StarField"
{
    Properties
    {
        _Brightness ("Brightness", Range(0.5, 4)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "StarUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest  LEqual
            Blend  One One       // 加算合成 — 星らしい光
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS : POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : TEXCOORD1;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                OUT.uv    = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // UV(0-1) を中心基準に変換して円形グラデーションを作る
                float2 d  = IN.uv - 0.5;
                float  r2 = dot(d, d) * 4.0;      // 中心=0, 端=1
                float  a  = saturate(1.0 - r2);
                a = a * a;                         // 中心を引き締める

                float3 col = IN.color.rgb * a * _Brightness;
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
