using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace MoonObserver.Rendering
{
    /// <summary>
    /// 地上の平面グリッドと方位マーカー (N/E/S/W) を生成し、
    /// VR 空間内での「上下」と「方角」の手がかりを与える。
    ///
    /// 方位の定義は VRMoonViewer.SetMoonTransformPosition() と一致させる:
    ///   方位角 az=0 → +Z (北) / az=90 → +X (東)
    /// </summary>
    public class HorizonReference : MonoBehaviour
    {
        [Header("地上グリッド (平面)")]
        [Tooltip("グリッドの広がり (中心からの距離 m)")]
        public float extent = 400f;
        [Tooltip("マス目の一辺 (m)")]
        public float cellSize = 10f;
        [Tooltip("グリッドの色")]
        public Color gridColor = new Color(0.22f, 0.45f, 0.68f);
        [Tooltip("中心付近の不透明度")]
        [Range(0f, 1f)]
        public float gridAlpha = 0.55f;
        [Tooltip("10 マスごとの主線を強調する")]
        public bool emphasizeMajorLines = true;

        [Header("方位マーカー")]
        public bool  showCardinalLabels = true;
        [Tooltip("方位ラベルの配置距離 (m)")]
        public float labelDistance = 620f;
        [Tooltip("方位ラベルの高さ (m)")]
        public float labelHeight = 25f;
        [Tooltip("方位ラベルの文字サイズ (620m 先で読めるよう大きめに取る)")]
        public float labelFontSize = 260f;

        private void Start()
        {
            BuildGroundGrid();
            if (showCardinalLabels) BuildCardinalLabels();
        }

        // ── 平面グリッド (XZ 平面の格子) ─────────────────────────────────
        private void BuildGroundGrid()
        {
            var go = new GameObject("Ground Grid");
            go.transform.SetParent(transform, false);

            var verts   = new List<Vector3>();
            var colors  = new List<Color>();
            var indices = new List<int>();

            int lineCount = Mathf.RoundToInt(extent / cellSize);

            for (int i = -lineCount; i <= lineCount; i++)
            {
                float offset = i * cellSize;
                bool  major  = emphasizeMajorLines && (i % 10 == 0);

                // Z 方向に走る線 (東西方向に並ぶ)
                AddFadedLine(verts, colors, indices,
                             new Vector3(offset, 0f, -extent),
                             new Vector3(offset, 0f,  extent), major);

                // X 方向に走る線 (南北方向に並ぶ)
                AddFadedLine(verts, colors, indices,
                             new Vector3(-extent, 0f, offset),
                             new Vector3( extent, 0f, offset), major);
            }

            var mesh = new Mesh { name = "Ground Grid" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;

            var mat = CreateLineMaterial();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
        }

        /// <summary>
        /// 線分を細かく分割し、原点からの距離に応じて頂点カラーの alpha を落とす。
        /// 一様な明るさだと遠方の線が地平線付近で密集して帯状に潰れるため。
        /// </summary>
        private void AddFadedLine(List<Vector3> verts, List<Color> colors, List<int> indices,
                                  Vector3 from, Vector3 to, bool major)
        {
            const int SEGMENTS = 24;
            float baseAlpha = gridAlpha * (major ? 1.6f : 1f);

            for (int s = 0; s < SEGMENTS; s++)
            {
                Vector3 a = Vector3.Lerp(from, to, (float)s       / SEGMENTS);
                Vector3 b = Vector3.Lerp(from, to, (float)(s + 1) / SEGMENTS);

                int start = verts.Count;
                verts.Add(a);
                verts.Add(b);
                colors.Add(FadeColor(a, baseAlpha));
                colors.Add(FadeColor(b, baseAlpha));
                indices.Add(start);
                indices.Add(start + 1);
            }
        }

        private Color FadeColor(Vector3 pos, float baseAlpha)
        {
            float t = Mathf.Clamp01(pos.magnitude / extent);
            float a = baseAlpha * Mathf.Pow(1f - t, 1.8f);
            return new Color(gridColor.r, gridColor.g, gridColor.b, Mathf.Clamp01(a));
        }

        // ── 方位ラベル (N / E / S / W) ───────────────────────────────────
        private void BuildCardinalLabels()
        {
            CreateLabel("N", new Vector3(0f, labelHeight,  labelDistance), new Color(1f, 0.55f, 0.45f));
            CreateLabel("E", new Vector3( labelDistance, labelHeight, 0f), new Color(0.75f, 0.85f, 1f));
            CreateLabel("S", new Vector3(0f, labelHeight, -labelDistance), new Color(0.75f, 0.85f, 1f));
            CreateLabel("W", new Vector3(-labelDistance, labelHeight, 0f), new Color(0.75f, 0.85f, 1f));
        }

        private void CreateLabel(string text, Vector3 position, Color color)
        {
            var go = new GameObject($"Cardinal {text}");
            go.transform.SetParent(transform, false);
            go.transform.position = position;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text      = text;
            tmp.fontSize  = labelFontSize;
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 400f);

            // TMP は +Z 方向が表なので、forward は原点向き (= -position) でなければ裏返る
            go.transform.rotation = Quaternion.LookRotation(-position.normalized, Vector3.up);
        }

        private Material CreateLineMaterial()
        {
            var shader = Shader.Find("MoonObserver/VertexColorLine");
            if (shader == null)
            {
                Debug.LogWarning("[HorizonReference] MoonObserver/VertexColorLine シェーダーが見つかりません。");
                return null;
            }
            return new Material(shader);
        }
    }
}
