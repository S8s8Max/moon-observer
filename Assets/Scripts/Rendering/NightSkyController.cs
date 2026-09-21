using UnityEngine;
using UnityEngine.Rendering;
using MoonObserver.Astronomy;

namespace MoonObserver.Rendering
{
    /// <summary>
    /// 夜空・星空・月グローを管理する。SkyboxController の代替として使用。
    /// </summary>
    public class NightSkyController : MonoBehaviour
    {
        [Header("夜空の色")]
        public Color skyColor     = new Color(0.00f, 0.00f, 0.02f);
        public Color ambientColor = new Color(0.02f, 0.02f, 0.05f);

        [Header("星空")]
        [Range(500, 5000)]
        public int   starCount        = 2500;
        [Range(2000f, 10000f)]
        public float starSphereRadius = 8000f;

        [Header("月グロー")]
        public Light moonGlowLight;

        // ─────────────────────────────────────────────────────────────────
        private static readonly Color COLOR_HORIZON = new Color(1.0f, 0.3f, 0.05f);

        private void Start()
        {
            ApplyNightEnvironment();
            GenerateStarField();
        }

        // ── 夜空環境設定 ────────────────────────────────────────────────
        private void ApplyNightEnvironment()
        {
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.fog          = false;

            // 既存スカイボックスを無効化
            RenderSettings.skybox = null;

            // カメラ背景を暗い夜空色に
            SetCameraBackground();
        }

        private void SetCameraBackground()
        {
            // XR Origin 内の Camera を探す
            Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cams)
            {
                cam.clearFlags      = CameraClearFlags.SolidColor;
                cam.backgroundColor = skyColor;
            }
        }

        // ── 星フィールド生成 ────────────────────────────────────────────
        private void GenerateStarField()
        {
            var starGO = new GameObject("Star Field");
            starGO.transform.SetParent(transform);

            var ps   = starGO.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles    = starCount;
            main.startLifetime   = float.MaxValue;
            main.startSpeed      = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop            = false;
            main.playOnAwake     = true;

            // ランダムなサイズ (明るい星と暗い星)
            main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.014f);

            // 一度に全星を放出
            var em = ps.emission;
            em.enabled       = true;
            em.rateOverTime  = 0;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, starCount) });

            // 球面上にランダム配置
            var shape = ps.shape;
            shape.enabled        = true;
            shape.shapeType      = ParticleSystemShapeType.Sphere;
            shape.radius         = starSphereRadius;
            shape.radiusThickness = 0f; // 表面のみ

            // ランダム色 (白・青白・薄黄)
            var col = ps.colorOverLifetime;
            col.enabled = false;

            var startCol = ps.main;
            startCol.startColor = new ParticleSystem.MinMaxGradient(
                Color.white,
                new Color(0.85f, 0.92f, 1.0f)); // ほぼ白〜わずかに青白

            // URP 用マテリアル
            var renderer      = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = CreateStarMaterial();
            if (mat != null) renderer.material = mat;

            ps.Play();
        }

        private Material CreateStarMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", Color.white);
            return mat;
        }

        // ── 月グロー更新 (VRMoonViewer から呼ぶ) ────────────────────────
        public void UpdateAtmosphere(MoonState state, Color moonColor)
        {
            if (moonGlowLight == null) return;

            float alt = (float)state.AltitudeDeg;
            float t   = Mathf.Clamp01(alt / 15f);

            moonGlowLight.color     = Color.Lerp(COLOR_HORIZON, moonColor, t);
            moonGlowLight.intensity = Mathf.Lerp(1.5f, 0.2f, Mathf.Clamp01(alt / 20f));
        }
    }
}
