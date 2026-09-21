using UnityEngine;
using MoonObserver.Astronomy;

namespace MoonObserver.Atmospheric
{
    /// <summary>
    /// 大気光学エフェクトのパラメータ管理と色計算
    /// Rayleigh + Mie 散乱モデルに基づく月の色フィルタリング
    /// </summary>
    public class AtmosphericEffects : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector プロパティ
        // ─────────────────────────────────────────────────────────────────────
        [Header("散乱係数")]
        [Tooltip("Rayleigh散乱係数 β_R [m^-1] (R,G,B)")]
        public Vector3 rayleighBeta = new Vector3(5.8e-6f, 1.35e-5f, 3.31e-5f);
        [Tooltip("Mie散乱係数 β_M [m^-1]")]
        public float   mieBeta = 2.1e-5f;
        [Tooltip("Mie非対称パラメータ g (0=等方、0.9=前方散乱強)")]
        [Range(0f, 0.99f)]
        public float   mieG = 0.76f;
        [Tooltip("Rayleigh散乱スケールハイト H_R [m]")]
        public float   rayleighScaleHeight = 8000f;
        [Tooltip("Mie散乱スケールハイト H_M [m]")]
        public float   mieScaleHeight = 1200f;

        [Header("月の錯覚")]
        [Tooltip("月の地平線錯覚を有効化する")]
        public bool    enableMoonIllusion = true;
        [Tooltip("錯覚の最大倍率 (地平線付近)")]
        [Range(1.0f, 1.5f)]
        public float   maxIllusionScale = 1.3f;
        [Tooltip("錯覚が消える高度 (度)")]
        [Range(5f, 20f)]
        public float   illusionFadeAltDeg = 10f;

        [Header("大気シェーダー参照")]
        public Material moonAtmosphereMaterial;
        public Renderer moonRenderer;

        // ─────────────────────────────────────────────────────────────────────
        // 公開メソッド
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 月の高度に基づく大気色フィルタ色を計算して返す
        /// </summary>
        public Color ComputeAtmosphericColor(float altitudeDeg)
        {
            float airMass = ComputeAirMass(altitudeDeg);

            // Rayleigh 透過率: exp(-β_R * airMass * H_R)
            float txR = Mathf.Exp(-rayleighBeta.x * airMass * rayleighScaleHeight);
            float txG = Mathf.Exp(-rayleighBeta.y * airMass * rayleighScaleHeight);
            float txB = Mathf.Exp(-rayleighBeta.z * airMass * rayleighScaleHeight);

            // Mie 透過率
            float mieTransmit = Mathf.Exp(-mieBeta * airMass * mieScaleHeight);
            txR *= mieTransmit;
            txG *= mieTransmit;
            txB *= mieTransmit;

            return new Color(txR, txG, txB);
        }

        /// <summary>
        /// 月の錯覚スケール係数を返す (enableMoonIllusion=false なら 1.0)
        /// </summary>
        public float ComputeIllusionScale(float altitudeDeg)
        {
            if (!enableMoonIllusion) return 1.0f;
            // 高度 0〜illusionFadeAltDeg で maxIllusionScale〜1.0 にリニア補間
            float t = Mathf.Clamp01(altitudeDeg / illusionFadeAltDeg);
            return Mathf.Lerp(maxIllusionScale, 1.0f, t);
        }

        /// <summary>
        /// シェーダーへ大気パラメータを転送する
        /// </summary>
        public void ApplyToShader(float altitudeDeg)
        {
            if (moonAtmosphereMaterial == null) return;

            float airMass = ComputeAirMass(altitudeDeg);
            Color atmColor = ComputeAtmosphericColor(altitudeDeg);

            moonAtmosphereMaterial.SetFloat("_AirMass", airMass);
            moonAtmosphereMaterial.SetVector("_RayleighBeta", rayleighBeta);
            moonAtmosphereMaterial.SetFloat("_MieBeta", mieBeta);
            moonAtmosphereMaterial.SetFloat("_MieG", mieG);
            moonAtmosphereMaterial.SetFloat("_MoonAltitude", altitudeDeg);
            moonAtmosphereMaterial.SetColor("_AtmosphericTint", atmColor);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 内部計算
        // ─────────────────────────────────────────────────────────────────────

        // Chapman の近似式による大気質量 (air mass) 計算
        private float ComputeAirMass(float altitudeDeg)
        {
            altitudeDeg = Mathf.Max(altitudeDeg, -2f);
            float zenithRad = (90f - altitudeDeg) * Mathf.Deg2Rad;
            float cosZ = Mathf.Cos(zenithRad);
            // X = 35 / sqrt(1224 * cos²Z + 1)
            float X = 35f / Mathf.Sqrt(1224f * cosZ * cosZ + 1f);
            return Mathf.Max(1f, X);
        }
    }
}
