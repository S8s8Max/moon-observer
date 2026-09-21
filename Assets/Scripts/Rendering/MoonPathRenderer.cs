using System;
using System.Collections.Generic;
using UnityEngine;
using MoonObserver.Astronomy;

namespace MoonObserver.Rendering
{
    /// <summary>
    /// 月の日周軌道を薄い線で描画する。
    /// 指定時刻の前後を一定間隔でサンプリングし、地平座標へ変換して線で結ぶ。
    /// 地平線より下の区間は暗く描いて「見えない時間帯」がわかるようにする。
    /// </summary>
    public class MoonPathRenderer : MonoBehaviour
    {
        [Header("サンプリング範囲")]
        [Tooltip("中心時刻からの前後の時間 (h)。24 なら前後 24 時間 = 計 48 時間")]
        [Range(3f, 36f)]
        public float hoursSpan = 14f;
        [Tooltip("サンプリング間隔 (分)。小さいほど滑らかだが天文計算の回数が増える")]
        [Range(2f, 30f)]
        public float stepMinutes = 10f;

        [Header("見た目")]
        [Tooltip("軌道を描く距離 (m)。月の配置距離と揃える")]
        public float pathDistance = 600f;
        [Tooltip("地平線より上の軌道色")]
        public Color aboveColor = new Color(0.85f, 0.85f, 0.55f, 0.45f);
        [Tooltip("地平線より下の軌道色")]
        public Color belowColor = new Color(0.35f, 0.40f, 0.55f, 0.16f);

        // ─────────────────────────────────────────────────────────────────
        private MeshFilter _meshFilter;
        private Mesh       _mesh;

        private void Awake()
        {
            var go = new GameObject("Moon Path");
            go.transform.SetParent(transform, false);

            _meshFilter = go.AddComponent<MeshFilter>();
            var mr      = go.AddComponent<MeshRenderer>();

            _mesh = new Mesh { name = "Moon Path" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _meshFilter.sharedMesh = _mesh;

            var shader = Shader.Find("MoonObserver/VertexColorLine");
            if (shader != null)
                mr.sharedMaterial = new Material(shader);
            else
                Debug.LogWarning("[MoonPathRenderer] MoonObserver/VertexColorLine シェーダーが見つかりません。");

            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
        }

        /// <summary>指定時刻を中心に軌道を再構築する。</summary>
        public void Rebuild(DateTime centerUtc, double latitudeDeg, double longitudeDeg)
        {
            if (_mesh == null) return;

            int steps = Mathf.Max(2, Mathf.RoundToInt(hoursSpan * 2f * 60f / stepMinutes));

            var verts   = new List<Vector3>(steps + 1);
            var colors  = new List<Color>(steps + 1);
            var indices = new List<int>(steps * 2);

            DateTime start = centerUtc.AddHours(-hoursSpan);

            for (int i = 0; i <= steps; i++)
            {
                DateTime t     = start.AddMinutes(i * stepMinutes);
                MoonState state = MoonAstronomyEngine.Calculate(t, latitudeDeg, longitudeDeg);

                verts.Add(AltAzToPosition(state.AltitudeDeg, state.AzimuthDeg, pathDistance));
                colors.Add(state.AltitudeDeg >= 0 ? aboveColor : belowColor);

                if (i > 0)
                {
                    indices.Add(i - 1);
                    indices.Add(i);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetColors(colors);
            _mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
        }

        /// <summary>地平座標 (高度・方位角) をワールド座標へ変換する。</summary>
        private static Vector3 AltAzToPosition(double altDeg, double azDeg, float distance)
        {
            float alt = (float)(altDeg * Mathf.Deg2Rad);
            float az  = (float)(azDeg  * Mathf.Deg2Rad);

            return new Vector3(
                Mathf.Sin(az) * Mathf.Cos(alt),
                Mathf.Sin(alt),
                Mathf.Cos(az) * Mathf.Cos(alt)) * distance;
        }
    }
}
