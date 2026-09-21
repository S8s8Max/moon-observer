using UnityEngine;
using UnityEngine.Rendering;
using MoonObserver.Astronomy;

namespace MoonObserver.Rendering
{
    /// <summary>
    /// 夜空・星空・月グローを管理する。ParticleSystem に依存しないメッシュ実装。
    /// </summary>
    public class NightSkyController : MonoBehaviour
    {
        [Header("夜空の色")]
        public Color skyColor     = new Color(0.02f, 0.02f, 0.06f); // 薄暗い夜空
        public Color ambientColor = new Color(0.06f, 0.06f, 0.12f);

        [Header("星空")]
        [Range(500, 5000)]
        public int   starCount        = 2500;
        [Range(200f, 900f)]
        public float starSphereRadius = 800f; // カメラ far clip (1000m) 以内

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
            RenderSettings.skybox       = null;

            SetCameraBackground();
        }

        private void SetCameraBackground()
        {
            foreach (var cam in Camera.allCameras)
            {
                cam.clearFlags      = CameraClearFlags.SolidColor;
                cam.backgroundColor = skyColor;
                // 星球が見えるよう far clip を拡張
                if (cam.farClipPlane < starSphereRadius * 1.5f)
                    cam.farClipPlane = starSphereRadius * 1.5f;
            }
        }

        // ── メッシュベース星フィールド (ParticleSystem 不要) ────────────
        private void GenerateStarField()
        {
            var starGO = new GameObject("Star Field");
            starGO.transform.SetParent(transform, false);

            int   count = starCount;
            var   verts   = new Vector3[count * 4];
            var   cols    = new Color[count * 4];
            var   uvs     = new Vector2[count * 4];
            var   indices = new int[count * 6];

            var rng = new System.Random(42);
            float r = starSphereRadius;

            for (int i = 0; i < count; i++)
            {
                // 球面上のランダム点
                float theta = (float)(rng.NextDouble() * 2.0 * System.Math.PI);
                float phi   = Mathf.Acos((float)(2.0 * rng.NextDouble() - 1.0));
                Vector3 center = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Sin(phi) * Mathf.Sin(theta),
                    Mathf.Cos(phi)) * r;

                // 星の向き (内側を向く TBN)
                Vector3 radial = center.normalized;
                Vector3 right  = Vector3.Cross(radial, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f)
                    right = Vector3.Cross(radial, Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(right, radial).normalized;

                // 星のサイズ (半径 800m に合わせて 1.5〜5m)
                float s = (float)(rng.NextDouble() * 3.5 + 1.5);

                int vi = i * 4;
                verts[vi + 0] = center + (-right - up) * s;
                verts[vi + 1] = center + ( right - up) * s;
                verts[vi + 2] = center + ( right + up) * s;
                verts[vi + 3] = center + (-right + up) * s;

                uvs[vi + 0] = new Vector2(0f, 0f);
                uvs[vi + 1] = new Vector2(1f, 0f);
                uvs[vi + 2] = new Vector2(1f, 1f);
                uvs[vi + 3] = new Vector2(0f, 1f);

                // 白〜青白のランダム色
                float bri = (float)(rng.NextDouble() * 0.35 + 0.65);
                float blueShift = (float)(rng.NextDouble() * 0.15);
                var c = new Color(bri, bri, Mathf.Min(1f, bri + blueShift), 1f);
                cols[vi] = cols[vi+1] = cols[vi+2] = cols[vi+3] = c;

                // 内側向き三角形 (カメラは球内部にいる)
                int ti = i * 6;
                indices[ti + 0] = vi;
                indices[ti + 1] = vi + 2;
                indices[ti + 2] = vi + 1;
                indices[ti + 3] = vi;
                indices[ti + 4] = vi + 3;
                indices[ti + 5] = vi + 2;
            }

            var mesh = new Mesh { name = "Stars" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices    = verts;
            mesh.colors      = cols;
            mesh.uv          = uvs;
            mesh.triangles   = indices;
            mesh.RecalculateBounds();

            var mf = starGO.AddComponent<MeshFilter>();
            var mr = starGO.AddComponent<MeshRenderer>();
            mf.sharedMesh     = mesh;
            mr.sharedMaterial = CreateStarMaterial();
            mr.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows       = false;
        }

        private Material CreateStarMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.SetColor("_BaseColor", Color.white);
            mat.enableInstancing = false;
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
