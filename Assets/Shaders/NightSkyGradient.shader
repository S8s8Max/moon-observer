// 夜空グラデーション スカイボックス (URP 17 / Unity 6 / Metal 対応)
//
// 実際の夜空が単色にならない理由をそのままモデル化している:
//   1. 天頂が最も暗い。地平線へ向かうほど大気を長く通すため明るくなる
//   2. 光害は地平線付近で最も強く、ナトリウム灯・LED による橙色を帯びる
//   3. 大気光 (酸素 557.7nm) がごく淡い緑として常に存在する
//   4. 薄明中は太陽側が暖色、反対側にはヴィーナスベルト (桃色の帯) が出る
//   5. 月が出ていると周囲にハローが生じ、空全体も明るくなる
//   6. 地平線より下は地面なので空の色にはならない
Shader "MoonObserver/NightSkyGradient"
{
    Properties
    {
        _SunDirection     ("Sun Direction",   Vector) = (0, -1, 0, 0)
        _MoonDirection    ("Moon Direction",  Vector) = (0, -1, 0, 0)
        _SunAltitude      ("Sun Altitude (deg)", Float) = -30
        _MoonIllumination ("Moon Illumination", Range(0,1)) = 0.5
        _Transmittance    ("Atmospheric Transmittance", Range(0,1)) = 1
        _LightPollution   ("Light Pollution", Range(0,1)) = 0.35
        _GroundColor      ("Ground Color", Color) = (0.012, 0.014, 0.020, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Background"
            "RenderType"     = "Background"
            "PreviewType"    = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SunDirection;
                float4 _MoonDirection;
                float  _SunAltitude;
                float  _MoonIllumination;
                float  _Transmittance;
                float  _LightPollution;
                float4 _GroundColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dirOS      : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // スカイボックスのメッシュ頂点はそのまま視線方向になる
                OUT.dirOS = IN.positionOS.xyz;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dirOS);

                // ── 昼夜の段階を太陽高度から求める ──────────────────────
                // 天文薄明 (-18°) から日の出まで、そして昼へ
                float twilight = saturate((_SunAltitude + 18.0) / 18.0);
                float daylight = saturate((_SunAltitude + 6.0)  / 12.0);

                // ── 天頂色と地平線色 ────────────────────────────────────
                float3 zenithNight    = float3(0.004, 0.009, 0.022);
                float3 zenithTwilight = float3(0.030, 0.070, 0.150);
                float3 zenithDay      = float3(0.150, 0.320, 0.620);

                float3 horizonNight    = float3(0.016, 0.026, 0.045);
                float3 horizonTwilight = float3(0.180, 0.180, 0.260);
                float3 horizonDay      = float3(0.500, 0.620, 0.800);

                float3 zenithCol  = lerp(zenithNight,  zenithTwilight,  twilight);
                zenithCol         = lerp(zenithCol,    zenithDay,       daylight);

                float3 horizonCol = lerp(horizonNight, horizonTwilight, twilight);
                horizonCol        = lerp(horizonCol,   horizonDay,      daylight);

                // 地平線へ向かうほど大気を長く通るので明るくなる
                float h            = saturate(d.y);
                float horizonBlend = pow(1.0 - h, 2.5);

                float3 col = lerp(zenithCol, horizonCol, horizonBlend);

                // ── 大気光 (常時存在するごく淡い緑) ─────────────────────
                col += float3(0.010, 0.019, 0.013) * pow(1.0 - h, 3.0) * (1.0 - daylight);

                // ── 光害 (地平線付近の橙色のドーム) ─────────────────────
                col += float3(0.230, 0.130, 0.045)
                     * _LightPollution * pow(1.0 - h, 4.0) * (1.0 - daylight);

                // ── 薄明: 太陽側の暖色 ──────────────────────────────────
                float3 sunHoriz = normalize(float3(_SunDirection.x, 0.0, _SunDirection.z) + 1e-5);
                float3 viewHoriz = normalize(float3(d.x, 0.0, d.z) + 1e-5);
                float  towardSun = saturate(dot(viewHoriz, sunHoriz));

                float warmth = pow(towardSun, 3.0) * horizonBlend * twilight;
                col += float3(0.480, 0.200, 0.060) * warmth;

                // ── ヴィーナスベルト ────────────────────────────────────
                // 太陽が地平線のすぐ下にあるとき、反対側の高度 10° 付近に
                // 地球の影の上端として桃色の帯が現れる
                float antiSun   = saturate(-dot(viewHoriz, sunHoriz));
                float beltBand  = exp(-pow((d.y - 0.18) / 0.10, 2.0));
                float beltPhase = smoothstep(-8.0, -2.0, _SunAltitude)
                                * (1.0 - smoothstep(0.0, 5.0, _SunAltitude));
                col += float3(0.300, 0.120, 0.165) * antiSun * beltBand * beltPhase;

                // ── 月明かりによるハローと空全体の持ち上がり ────────────
                float3 moonDir  = normalize(_MoonDirection.xyz + 1e-5);
                float  towardMoon = saturate(dot(d, moonDir));
                float  moonUp     = saturate(moonDir.y * 4.0);   // 月が沈むと効果も消える

                float halo = pow(towardMoon, 28.0) * 0.45 + pow(towardMoon, 5.0) * 0.055;
                col += float3(0.55, 0.60, 0.78) * halo * _MoonIllumination * moonUp;
                col += float3(0.018, 0.022, 0.035) * _MoonIllumination * moonUp * (1.0 - daylight);

                // ── 天候: 曇るとコントラストが失われ空は逆に明るくなる ──
                float3 overcast = float3(0.055, 0.062, 0.080)
                                + float3(0.110, 0.065, 0.030) * _LightPollution;
                overcast = lerp(overcast, float3(0.62, 0.64, 0.68), daylight);
                col = lerp(overcast, col, _Transmittance);

                // ── 地平線より下は地面 ──────────────────────────────────
                float belowness = saturate(-d.y * 7.0);
                float3 ground   = _GroundColor.rgb * lerp(1.0, 2.2, daylight);
                col = lerp(col, ground, belowness);

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
