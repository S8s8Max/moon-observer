// 星空メッシュ用シェーダー — 頂点カラーを使って各星の輝度を表現
// URP 17 / Unity 6 対応
Shader "MoonObserver/StarField"
{
    Properties {}

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off
        ZTest LEqual
        Blend One One   // 加算合成 — 星らしい光の重なりを表現
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4  color : COLOR;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                OUT.uv    = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // UV (0-1) を中心基準 (-0.5 〜 0.5) に変換して距離を計算
                float2 uvc = IN.uv - 0.5;
                float  r2  = dot(uvc, uvc) * 4.0; // 中心=0, 端=1
                half   a   = saturate(1.0 - r2);  // 円形グラデーション
                return half4(IN.color.rgb * a, 1.0);
            }
            ENDHLSL
        }
    }
}
