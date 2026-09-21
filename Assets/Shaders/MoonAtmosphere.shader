// URP 14.x (Unity 6) カスタム大気散乱シェーダー
// Rayleigh + Mie 散乱による月の色変化を実装
Shader "MoonObserver/MoonAtmosphere"
{
    Properties
    {
        _BaseMap        ("Base Map (Moon Color)", 2D)    = "white" {}
        _BumpMap        ("Normal Map", 2D)               = "bump" {}
        _Smoothness     ("Smoothness", Range(0,1))       = 0.05
        _Metallic       ("Metallic", Range(0,1))         = 0.0

        // 大気散乱パラメータ (CPU から SetFloat/SetVector で更新)
        _AirMass        ("Air Mass", Float)              = 1.0
        _RayleighBeta   ("Rayleigh Beta (RGB)", Vector)  = (0.0000058, 0.0000135, 0.0000331, 0)
        _MieBeta        ("Mie Beta", Float)              = 0.000021
        _MieG           ("Mie g", Range(0,0.99))         = 0.76
        _MoonAltitude   ("Moon Altitude (deg)", Float)   = 45.0
        _AtmosphericTint("Atmospheric Tint", Color)      = (1,1,1,1)

        // 月の錯覚スケール (CPU から更新)
        _IllusionScale  ("Illusion Scale", Float)        = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── テクスチャ ────────────────────────────────────────────────
            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);

            // ── CBUFFER ────────────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float  _Smoothness;
                float  _Metallic;
                float  _AirMass;
                float4 _RayleighBeta;
                float  _MieBeta;
                float  _MieG;
                float  _MoonAltitude;
                float4 _AtmosphericTint;
                float  _IllusionScale;
            CBUFFER_END

            // ── 頂点入力・出力構造体 ───────────────────────────────────────
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Rayleigh 散乱位相関数 ─────────────────────────────────────
            float3 RayleighScattering(float cosTheta)
            {
                float3 beta_R = _RayleighBeta.rgb;
                float phase = (3.0 / (16.0 * PI)) * (1.0 + cosTheta * cosTheta);
                return beta_R * phase;
            }

            // ── Mie 散乱位相関数 (Henyey-Greenstein) ────────────────────
            float MiePhase(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 / (4.0 * PI))
                     * (1.0 - g2)
                     / pow(max(1.0 + g2 - 2.0 * g * cosTheta, 0.0001), 1.5);
            }

            // ── 大気透過率計算 ─────────────────────────────────────────────
            float3 ComputeTransmittance(float airMass)
            {
                float3 tau_R = _RayleighBeta.rgb * airMass * 8000.0; // H_R = 8000m
                float  tau_M = _MieBeta         * airMass * 1200.0;  // H_M = 1200m
                return exp(-tau_R - tau_M);
            }

            // ── 頂点シェーダー ─────────────────────────────────────────────
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
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            // ── フラグメントシェーダー ─────────────────────────────────────
            half4 Frag(Varyings IN) : SV_Target
            {
                // テクスチャサンプリング
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float3 normalTS  = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv));

                // 法線をワールド空間へ変換
                float3x3 TBN = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                // ライティング取得
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float NdotL = max(0, dot(normalWS, lightDir));

                // 大気透過率
                float3 transmittance = ComputeTransmittance(_AirMass);

                // Rayleigh + Mie 散乱
                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                float cosTheta = dot(viewDir, lightDir);
                float3 rayleigh = RayleighScattering(cosTheta);
                float  mie      = MiePhase(cosTheta, _MieG);

                // 月面色 × 大気透過率 × Lambertian
                float3 moonColor = baseColor.rgb * transmittance * NdotL * mainLight.color;

                // 大気散乱によるグロー加算 (地平線近くで増大)
                float altFactor = saturate(1.0 - _MoonAltitude / 20.0);
                float3 scatter  = (rayleigh + float3(mie, mie, mie) * 0.5) * altFactor * 0.3;
                moonColor += scatter;

                // 大気色ティント適用
                moonColor *= _AtmosphericTint.rgb;

                return half4(moonColor, baseColor.a);
            }
            ENDHLSL
        }

        // シャドウキャストパス
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
