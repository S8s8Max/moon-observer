using UnityEngine;
using MoonObserver.Astronomy;

namespace MoonObserver.Rendering
{
    /// <summary>
    /// 月の3Dメッシュ・マテリアル・スケール・回転を制御するレンダラー
    /// </summary>
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class MoonRenderer : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector プロパティ
        // ─────────────────────────────────────────────────────────────────────
        [Header("テクスチャ")]
        [Tooltip("NASA CGI Moon Kit カラーマップ (Assets/Textures/Moon/lroc_color_8k.png)")]
        public Texture2D colorMap;
        [Tooltip("法線マップ (高度マップから生成)")]
        public Texture2D normalMap;

        [Header("マテリアル")]
        [Tooltip("URP Lit シェーダーを使うマテリアル")]
        public Material moonMaterial;

        [Header("スケール設定")]
        [Tooltip("VR空間内の月の配置距離 (m)")]
        public float placementDistanceM = 1000f;
        [Tooltip("視覚スケール倍率 (1=実サイズ約0.5°、5=見やすい大きさ)")]
        [Range(1f, 20f)]
        public float visualScaleMultiplier = 5f;

        [Header("太陽ライト")]
        [Tooltip("Directional Light (太陽)")]
        public Light sunLight;

        // ─────────────────────────────────────────────────────────────────────
        // 定数
        // ─────────────────────────────────────────────────────────────────────
        private const float MOON_RADIUS_KM = 1737.4f;
        private const float DEG2RAD = Mathf.PI / 180f;

        // ─────────────────────────────────────────────────────────────────────
        // 内部状態
        // ─────────────────────────────────────────────────────────────────────
        private MeshRenderer _meshRenderer;
        private float _baseRadiusM; // VR空間でのベース半径 (m)

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            if (moonMaterial != null)
            {
                _meshRenderer.material = moonMaterial;
                if (colorMap != null)
                    moonMaterial.SetTexture("_BaseMap", colorMap);
                if (normalMap != null)
                    moonMaterial.SetTexture("_BumpMap", normalMap);
                moonMaterial.SetFloat("_Smoothness", 0.05f); // 月面は非常に拡散反射
            }

            // メッシュがなければ球を生成
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter.sharedMesh == null)
                meshFilter.mesh = CreateSphereMesh(64, 32);
        }

        /// <summary>
        /// 毎フレーム月の状態を適用する
        /// </summary>
        public void UpdateMoon(MoonState state, float illusionScale = 1.0f)
        {
            UpdateScale(state, illusionScale);
            UpdateRotation(state);
            UpdateSunLight(state);
        }

        // ── スケール: 視直径が約0.5°になるよう調整 ─────────────────────────────
        private void UpdateScale(MoonState state, float illusionScale)
        {
            float moonAngularDiameterDeg = 2f * Mathf.Atan(
                MOON_RADIUS_KM / (float)state.DistanceKm) * Mathf.Rad2Deg;

            // VR空間での半径 = 距離 * tan(半視直径)
            _baseRadiusM = placementDistanceM * Mathf.Tan(moonAngularDiameterDeg * 0.5f * DEG2RAD);

            // 月の錯覚スケール + 視覚倍率を適用
            float totalScale = _baseRadiusM * 2f * illusionScale * visualScaleMultiplier;
            transform.localScale = Vector3.one * totalScale;
        }

        // ── 回転: 自転 + 秤動オフセット ──────────────────────────────────────
        private void UpdateRotation(MoonState state)
        {
            Quaternion baseRot = GetMoonBaseRotation(state.JDE);
            Quaternion librationOffset = Quaternion.Euler(
                (float)state.LibrationLatDeg,
                (float)state.LibrationLongDeg,
                0f);
            transform.rotation = baseRot * librationOffset;
        }

        // 月の基本自転姿勢 (地球に同じ面を向ける)
        private Quaternion GetMoonBaseRotation(double jde)
        {
            // 月の自転は公転と同期 (同期自転)
            // 地球方向に+Z軸を向けた上でY軸周りに自転角を適用
            float rotAngle = (float)((jde % 27.321661) / 27.321661 * 360.0);
            return Quaternion.Euler(0, rotAngle, 0);
        }

        // ── 太陽ライト方向を太陽の黄経から更新 ──────────────────────────────
        private void UpdateSunLight(MoonState state)
        {
            if (sunLight == null) return;

            // 月から見た太陽方向を位相角から逆算
            float phaseAngleRad = (float)(state.PhaseAngleDeg * DEG2RAD);
            float sunAlt = (float)(state.AltitudeDeg * DEG2RAD);
            float sunAz  = (float)((state.AzimuthDeg + 180.0) * DEG2RAD); // 反対方向

            Vector3 sunDir = new Vector3(
                Mathf.Sin(sunAz) * Mathf.Cos(sunAlt),
                Mathf.Sin(sunAlt),
                Mathf.Cos(sunAz) * Mathf.Cos(sunAlt));

            sunLight.transform.rotation = Quaternion.LookRotation(-sunDir);
        }

        // UV球メッシュ生成
        private Mesh CreateSphereMesh(int widthSegments, int heightSegments)
        {
            var mesh = new Mesh { name = "Moon Sphere" };
            int vCount = (widthSegments + 1) * (heightSegments + 1);
            var vertices = new Vector3[vCount];
            var normals  = new Vector3[vCount];
            var uvs      = new Vector2[vCount];

            int idx = 0;
            for (int y = 0; y <= heightSegments; y++)
            {
                float v = (float)y / heightSegments;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= widthSegments; x++)
                {
                    float u = (float)x / widthSegments;
                    float theta = u * 2f * Mathf.PI;
                    float sinPhi = Mathf.Sin(phi);
                    Vector3 n = new Vector3(
                        sinPhi * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        sinPhi * Mathf.Sin(theta));
                    vertices[idx] = n * 0.5f;
                    normals[idx]  = n;
                    uvs[idx]      = new Vector2(u, 1f - v);
                    idx++;
                }
            }

            var tris = new int[widthSegments * heightSegments * 6];
            int t = 0;
            for (int y = 0; y < heightSegments; y++)
            {
                for (int x = 0; x < widthSegments; x++)
                {
                    int i = y * (widthSegments + 1) + x;
                    tris[t++] = i;
                    tris[t++] = i + widthSegments + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + 1;
                    tris[t++] = i + widthSegments + 1;
                    tris[t++] = i + widthSegments + 2;
                }
            }

            mesh.vertices  = vertices;
            mesh.normals   = normals;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            return mesh;
        }
    }
}
