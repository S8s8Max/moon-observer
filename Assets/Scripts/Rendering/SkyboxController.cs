using UnityEngine;
using MoonObserver.Astronomy;

namespace MoonObserver.Rendering
{
    /// <summary>
    /// 月の高度に応じてスカイボックスと大気グロー色を動的更新するコントローラー
    /// </summary>
    public class SkyboxController : MonoBehaviour
    {
        [Header("スカイボックスマテリアル")]
        public Material skyboxMaterial;

        [Header("大気グロー")]
        [Tooltip("月周囲のグロー (Post-Processing Bloom 用ライト)")]
        public Light moonGlowLight;

        [Header("色設定")]
        public Gradient altitudeColorGradient;

        // 高度ごとの色定義 (手動設定のフォールバック)
        private static readonly Color COLOR_HORIZON = new Color(1.0f, 0.3f, 0.05f); // 赤橙
        private static readonly Color COLOR_LOW     = new Color(1.0f, 0.8f, 0.2f);  // 黄
        private static readonly Color COLOR_HIGH    = new Color(1.0f, 1.0f, 0.95f); // 白

        private void Awake()
        {
            if (altitudeColorGradient == null)
                altitudeColorGradient = CreateDefaultGradient();
        }

        /// <summary>
        /// 月の高度に応じてスカイボックスと大気グローを更新する
        /// </summary>
        public void UpdateAtmosphere(MoonState state, Color atmosphericMoonColor)
        {
            float alt = (float)state.AltitudeDeg;
            UpdateSkybox(alt, atmosphericMoonColor);
            UpdateGlow(alt, atmosphericMoonColor);
        }

        private void UpdateSkybox(float altDeg, Color moonColor)
        {
            if (skyboxMaterial == null) return;

            // 地平線付近での大気散乱色
            float t = Mathf.Clamp01(altDeg / 20f);
            Color horizonColor = Color.Lerp(COLOR_HORIZON, COLOR_LOW, t);

            skyboxMaterial.SetColor("_HorizonColor", horizonColor);
            skyboxMaterial.SetFloat("_MoonAltitude", altDeg);

            // 夜空の基本色 (高度が高いほど純粋な夜空)
            float skyBlend = Mathf.Clamp01(altDeg / 60f);
            Color skyColor = Color.Lerp(new Color(0.05f, 0.03f, 0.12f), new Color(0f, 0f, 0.02f), skyBlend);
            skyboxMaterial.SetColor("_SkyColor", skyColor);
        }

        private void UpdateGlow(float altDeg, Color moonColor)
        {
            if (moonGlowLight == null) return;

            // 高度 < 5°: 赤橙グロー、5〜15°: 黄グロー、>15°: 白グロー
            float t = Mathf.Clamp01(altDeg / 15f);
            Color glowColor = Color.Lerp(COLOR_HORIZON, moonColor, t);

            moonGlowLight.color = glowColor;
            // 高度が低いほどグロー強度を上げる (HDR Bloom連携)
            moonGlowLight.intensity = Mathf.Lerp(3.0f, 0.5f, Mathf.Clamp01(altDeg / 20f));
        }

        private Gradient CreateDefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(COLOR_HORIZON, 0f),
                    new GradientColorKey(COLOR_LOW, 0.3f),
                    new GradientColorKey(COLOR_HIGH, 1f),
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            return g;
        }
    }
}
