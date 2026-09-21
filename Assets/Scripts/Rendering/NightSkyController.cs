using System;
using UnityEngine;
using UnityEngine.Rendering;
using MoonObserver.Astronomy;

namespace MoonObserver.Rendering
{
    /// <summary>
    /// 夜空・星空・月グローを管理する。ParticleSystem に依存しないメッシュ実装。
    /// 星は等級分布に従って生成し、恒星時に応じて天の北極まわりに日周回転させる。
    /// </summary>
    public class NightSkyController : MonoBehaviour
    {
        [Header("夜空")]
        [Tooltip("地面 (地平線より下) の色")]
        public Color groundColor = new Color(0.012f, 0.014f, 0.020f);
        [Tooltip("光害の強さ。0=完全な暗所、0.35=郊外、0.8=都市部")]
        [Range(0f, 1f)]
        public float lightPollution = 0.35f;
        [Tooltip("空の描画に失敗した場合のフォールバック色")]
        public Color fallbackSkyColor = new Color(0.01f, 0.03f, 0.10f);

        [Header("星空")]
        [Range(500, 6000)]
        public int   starCount        = 3000;
        [Range(200f, 900f)]
        public float starSphereRadius = 800f;
        [Tooltip("最も暗い星の見かけサイズ (m)")]
        public float minStarSize = 0.8f;
        [Tooltip("最も明るい星の見かけサイズ (m)")]
        public float maxStarSize = 4.5f;
        [Tooltip("大きいほど暗い星の割合が増える (実際の星空は 3〜5 程度)")]
        [Range(1f, 6f)]
        public float magnitudeSkew = 4f;

        [Header("日周回転")]
        [Tooltip("恒星時に応じて星空を回転させる")]
        public bool enableDiurnalRotation = true;

        [Header("月グロー")]
        public Light moonGlowLight;

        // ─────────────────────────────────────────────────────────────────
        private static readonly Color COLOR_HORIZON = new Color(1.0f, 0.3f, 0.05f);

        private static readonly int ID_SunDir     = Shader.PropertyToID("_SunDirection");
        private static readonly int ID_MoonDir    = Shader.PropertyToID("_MoonDirection");
        private static readonly int ID_SunAlt     = Shader.PropertyToID("_SunAltitude");
        private static readonly int ID_MoonIllum  = Shader.PropertyToID("_MoonIllumination");
        private static readonly int ID_Transmit   = Shader.PropertyToID("_Transmittance");
        private static readonly int ID_Pollution  = Shader.PropertyToID("_LightPollution");
        private static readonly int ID_Ground     = Shader.PropertyToID("_GroundColor");

        private Transform _starField;
        private Material  _starMaterial;
        private Material  _skyMaterial;
        private float     _baseStarBrightness = 1.5f;
        private float     _transmittance = 1f;

        /// <summary>
        /// 天候による大気透過率 (0-1) を反映する。
        /// 星を暗くすると同時に、雲が地上光を反射して空自体は明るくなる。
        /// </summary>
        public void SetTransmittance(float transmittance)
        {
            _transmittance = Mathf.Clamp01(transmittance);

            if (_starMaterial != null && _starMaterial.HasProperty("_Brightness"))
                _starMaterial.SetFloat("_Brightness", _baseStarBrightness * _transmittance);

            if (_skyMaterial != null)
                _skyMaterial.SetFloat(ID_Transmit, _transmittance);
        }

        /// <summary>
        /// 太陽・月の位置に応じて空のグラデーションを更新する。
        /// 空の色は太陽高度 (薄明の段階) が支配的なので毎回計算する。
        /// </summary>
        public void UpdateSky(DateTime utc, double latitudeDeg, double longitudeDeg,
                              MoonState moonState)
        {
            if (_skyMaterial == null) return;

            var sun = SunPosition.Calculate(utc, latitudeDeg, longitudeDeg);

            _skyMaterial.SetVector(ID_SunDir,  AltAzToDirection(sun.AltitudeDeg, sun.AzimuthDeg));
            _skyMaterial.SetVector(ID_MoonDir, AltAzToDirection(moonState.AltitudeDeg,
                                                               moonState.AzimuthDeg));
            _skyMaterial.SetFloat(ID_SunAlt,    (float)sun.AltitudeDeg);
            _skyMaterial.SetFloat(ID_MoonIllum, (float)moonState.IlluminationFraction);
            _skyMaterial.SetFloat(ID_Pollution, lightPollution);

            // 環境光も空に合わせる。月が明るいほど地上が持ち上がる
            float moonLift = (float)moonState.IlluminationFraction
                           * Mathf.Clamp01((float)moonState.AltitudeDeg / 30f);
            float dayLift  = Mathf.Clamp01(((float)sun.AltitudeDeg + 6f) / 12f);

            RenderSettings.ambientLight = Color.Lerp(
                new Color(0.030f, 0.038f, 0.070f) + Color.white * moonLift * 0.10f,
                new Color(0.42f, 0.47f, 0.58f),
                dayLift);
        }

        private static Vector4 AltAzToDirection(double altDeg, double azDeg)
        {
            float alt = (float)(altDeg * Mathf.Deg2Rad);
            float az  = (float)(azDeg  * Mathf.Deg2Rad);

            return new Vector4(
                Mathf.Sin(az) * Mathf.Cos(alt),
                Mathf.Sin(alt),
                Mathf.Cos(az) * Mathf.Cos(alt),
                0f);
        }

        private void Start()
        {
            ApplyNightEnvironment();
            GenerateStarField();
        }

        // ── 夜空環境設定 ────────────────────────────────────────────────
        private void ApplyNightEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog         = false;

            _skyMaterial = CreateSkyMaterial();

            var cam = Camera.main;
            if (cam != null)
            {
                if (_skyMaterial != null)
                {
                    RenderSettings.skybox = _skyMaterial;
                    cam.clearFlags        = CameraClearFlags.Skybox;
                }
                else
                {
                    // シェーダーが無い場合でも真っ暗にはしない
                    RenderSettings.skybox = null;
                    cam.clearFlags        = CameraClearFlags.SolidColor;
                    cam.backgroundColor   = fallbackSkyColor;
                }

                if (cam.farClipPlane < starSphereRadius * 1.5f)
                    cam.farClipPlane = starSphereRadius * 1.5f;
            }
        }

        private Material CreateSkyMaterial()
        {
            var shader = Shader.Find("MoonObserver/NightSkyGradient");
            if (shader == null)
            {
                Debug.LogWarning("[NightSkyController] MoonObserver/NightSkyGradient シェーダーが見つかりません。");
                return null;
            }

            var mat = new Material(shader);
            mat.SetColor(ID_Ground,    groundColor);
            mat.SetFloat(ID_Pollution, lightPollution);
            mat.SetFloat(ID_Transmit,  _transmittance);
            return mat;
        }

        // ── メッシュベース星フィールド ──────────────────────────────────
        private void GenerateStarField()
        {
            var starGO = new GameObject("Star Field");
            starGO.transform.SetParent(transform, false);
            _starField = starGO.transform;

            int count   = starCount;
            var verts   = new Vector3[count * 4];
            var cols    = new Color[count * 4];
            var uvs     = new Vector2[count * 4];
            var indices = new int[count * 6];

            var rng = new System.Random(42);
            float r = starSphereRadius;

            for (int i = 0; i < count; i++)
            {
                // 球面上の一様ランダム点
                float theta = (float)(rng.NextDouble() * 2.0 * Math.PI);
                float phi   = Mathf.Acos((float)(2.0 * rng.NextDouble() - 1.0));
                Vector3 center = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Sin(phi) * Mathf.Sin(theta),
                    Mathf.Cos(phi)) * r;

                // 内向きクワッドの TBN
                Vector3 radial = center.normalized;
                Vector3 right  = Vector3.Cross(radial, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f)
                    right = Vector3.Cross(radial, Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(right, radial).normalized;

                // ── 等級分布 ──────────────────────────────────────────
                // 実際の星空は等級が 1 下がるごとに星数が約 3 倍になる。
                // u^skew で暗い星を圧倒的多数にし、明るい星を少数に絞る。
                float u          = (float)rng.NextDouble();
                float brightness = Mathf.Lerp(0.07f, 1.0f, Mathf.Pow(u, magnitudeSkew));

                // 明るい星ほど大きく見える (グレア効果)
                float size = Mathf.Lerp(minStarSize, maxStarSize, brightness * brightness);

                int vi = i * 4;
                verts[vi + 0] = center + (-right - up) * size;
                verts[vi + 1] = center + ( right - up) * size;
                verts[vi + 2] = center + ( right + up) * size;
                verts[vi + 3] = center + (-right + up) * size;

                uvs[vi + 0] = new Vector2(0f, 0f);
                uvs[vi + 1] = new Vector2(1f, 0f);
                uvs[vi + 2] = new Vector2(1f, 1f);
                uvs[vi + 3] = new Vector2(0f, 1f);

                Color c = ComputeStarColor(rng, brightness);
                // alpha は瞬きの位相としてシェーダーへ渡す (加算合成なので混色には影響しない)
                c.a = (float)rng.NextDouble();
                cols[vi] = cols[vi+1] = cols[vi+2] = cols[vi+3] = c;

                // 内側向き三角形
                int ti = i * 6;
                indices[ti + 0] = vi;
                indices[ti + 1] = vi + 2;
                indices[ti + 2] = vi + 1;
                indices[ti + 3] = vi;
                indices[ti + 4] = vi + 3;
                indices[ti + 5] = vi + 2;
            }

            var mesh = new Mesh { name = "Stars" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices    = verts;
            mesh.colors      = cols;
            mesh.uv          = uvs;
            mesh.triangles   = indices;
            mesh.RecalculateBounds();

            var mf = starGO.AddComponent<MeshFilter>();
            var mr = starGO.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;

            var mat = CreateStarMaterial();
            if (mat != null)
            {
                mr.sharedMaterial = mat;
                _starMaterial = mat;
                if (mat.HasProperty("_Brightness"))
                    _baseStarBrightness = mat.GetFloat("_Brightness");
            }
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = false;
        }

        /// <summary>スペクトル型の分布に基づく星の色を返す。</summary>
        private Color ComputeStarColor(System.Random rng, float brightness)
        {
            float t = (float)rng.NextDouble();
            Color c;
            if      (t < 0.10f) c = new Color(0.70f, 0.80f, 1.00f); // O/B 青白
            else if (t < 0.38f) c = new Color(0.94f, 0.97f, 1.00f); // A   白
            else if (t < 0.70f) c = new Color(1.00f, 0.98f, 0.91f); // F/G 黄白
            else if (t < 0.90f) c = new Color(1.00f, 0.88f, 0.74f); // K   橙
            else                c = new Color(1.00f, 0.76f, 0.60f); // M   赤橙

            // 暗い星は暗所視で色が判別できないため白へ寄せる
            float sat = Mathf.Clamp01(brightness * 1.4f);
            c = Color.Lerp(Color.white, c, sat);

            return new Color(c.r * brightness, c.g * brightness, c.b * brightness, 1f);
        }

        private Material CreateStarMaterial()
        {
            var shader = Shader.Find("MoonObserver/StarField");
            if (shader == null)
            {
                Debug.LogWarning("[NightSkyController] MoonObserver/StarField シェーダーが見つかりません。");
                return null;
            }
            return new Material(shader);
        }

        // ── 日周回転 (VRMoonViewer から時刻更新時に呼ぶ) ─────────────────
        /// <summary>
        /// 恒星時に応じて星空を天の北極まわりに回転させる。
        /// 天の北極は「方位角 0 (北)・高度 = 観測地緯度」の方向にある。
        /// </summary>
        public void UpdateStarRotation(DateTime utc, double latitudeDeg, double longitudeDeg)
        {
            if (!enableDiurnalRotation || _starField == null) return;

            double lst    = LocalSiderealTimeDeg(utc, longitudeDeg);
            float  latRad = (float)(latitudeDeg * Mathf.Deg2Rad);

            Vector3 poleAxis = new Vector3(0f, Mathf.Sin(latRad), Mathf.Cos(latRad));

            // 地球は東向きに自転するため、星は西向き (極を見て反時計回り) に動く
            _starField.rotation = Quaternion.AngleAxis(-(float)lst, poleAxis);
        }

        /// <summary>地方恒星時 (度) を返す。</summary>
        private static double LocalSiderealTimeDeg(DateTime utc, double longitudeDeg)
        {
            double jd  = ToJulianDate(utc);
            double d   = jd - 2451545.0;
            double gmst = 280.46061837 + 360.98564736629 * d;
            return NormalizeDeg(gmst + longitudeDeg);
        }

        private static double ToJulianDate(DateTime utc)
        {
            int    y = utc.Year, m = utc.Month;
            double day = utc.Day
                       + utc.Hour   / 24.0
                       + utc.Minute / 1440.0
                       + utc.Second / 86400.0;

            if (m <= 2) { y -= 1; m += 12; }

            int a = y / 100;
            int b = 2 - a + a / 4;

            return Math.Floor(365.25 * (y + 4716))
                 + Math.Floor(30.6001 * (m + 1))
                 + day + b - 1524.5;
        }

        private static double NormalizeDeg(double deg)
        {
            deg %= 360.0;
            return deg < 0 ? deg + 360.0 : deg;
        }

        // ── 月グロー更新 ────────────────────────────────────────────────
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
