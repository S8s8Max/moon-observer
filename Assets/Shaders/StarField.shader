// 星空メッシュ用シェーダー — 頂点カラーで輝度・色温度、頂点カラー alpha で瞬きの位相を渡す
// URP 17 / Unity 6 / Metal 対応
//
// 実際の星は「鋭い芯 + 淡く広がるハロー」に見える。単純な円形グラデーションでは
// ぼやけた塊になってしまうため、芯とハローを別々の指数カーブで合成する。
Shader "MoonObserver/StarField"
{
    Properties
    {
        _Brightness  ("Brightness", Range(0.5, 4))       = 1.5
        _CoreSharp   ("Core Sharpness", Range(2, 16))    = 7.0
        _HaloStrength("Halo Strength", Range(0, 0.5))    = 0.10
        _Twinkle     ("Twinkle Amount", Range(0, 0.5))   = 0.12
        _TwinkleSpeed("Twinkle Speed", Range(0, 8))      = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "StarUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest  LEqual
            Blend  One One       // 加算合成
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Brightness;
                float _CoreSharp;
                float _HaloStrength;
                float _Twinkle;
                float _TwinkleSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS : POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;   // rgb = 輝度込みの色 / a = 瞬きの位相 (0-1)
            };

            struct Varyings
            {
                float4 posCS       : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : TEXCOORD1;
                float  horizonFade : TEXCOORD2;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.posOS.xyz);

                OUT.posCS = TransformWorldToHClip(posWS);
                OUT.uv    = IN.uv;
                OUT.color = IN.color;

                // 星は原点中心の球上にあるので、正規化した y がそのまま sin(高度)。
                // 地平線より下の星を消し、地平線付近は大気減光で暗くする
                // (地面が描かれるようになったため、消さないと地中に星が浮いて見える)。
                float sinAlt = normalize(posWS).y;
                OUT.horizonFade = smoothstep(-0.01, 0.13, sinAlt);

                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // 中心からの距離 (0 = 中心, 1 = クワッドの辺)
                float2 d = IN.uv - 0.5;
                float  r = saturate(length(d) * 2.0);
                float  falloff = saturate(1.0 - r);

                // 鋭い芯 + 淡いハロー
                float core = pow(falloff, _CoreSharp);
                float halo = pow(falloff, 1.5) * _HaloStrength;
                float i    = core + halo;

                // 大気によるシンチレーション (星ごとに位相をずらす)
                float phase   = IN.color.a * 6.2831853;
                float twinkle = 1.0 - _Twinkle
                              + _Twinkle * sin(_Time.y * _TwinkleSpeed + phase);

                float3 col = IN.color.rgb * i * twinkle * _Brightness * IN.horizonFade;
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
