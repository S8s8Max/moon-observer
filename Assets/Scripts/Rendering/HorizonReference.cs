using UnityEngine;
using TMPro;

namespace MoonObserver.Rendering
{
    /// <summary>
    /// 地平線グリッドと方位マーカー (N/E/S/W) を生成し、
    /// VR 空間内での「上下」と「方角」の手がかりを与える。
    ///
    /// 方位の定義は VRMoonViewer.SetMoonTransformPosition() と一致させる:
    ///   方位角 az=0 → +Z (北) / az=90 → +X (東)
    /// </summary>
    public class HorizonReference : MonoBehaviour
    {
        [Header("地平線グリッド")]
        [Tooltip("グリッドの半径 (m)")]
        public float radius = 700f;
        [Tooltip("同心円の本数")]
        [Range(2, 12)]
        public int ringCount = 6;
        [Tooltip("同心円の間隔カーブ。大きいほど手前が密になり地面を認識しやすい")]
        [Range(1f, 4f)]
        public float ringFalloff = 2.5f;
        [Tooltip("放射線の本数 (方位の分割数)")]
        [Range(4, 36)]
        public int spokeCount = 16;
        [Tooltip("グリッドの色")]
        public Color gridColor = new Color(0.20f, 0.42f, 0.62f, 1f);

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
            BuildHorizonGrid();
            if (showCardinalLabels) BuildCardinalLabels();
        }

        // ── 地平面グリッド (同心円 + 放射線) ─────────────────────────────
        private void BuildHorizonGrid()
        {
            var go = new GameObject("Horizon Grid");
            go.transform.SetParent(transform, false);

            var verts   = new System.Collections.Generic.List<Vector3>();
            var indices = new System.Collections.Generic.List<int>();

            const int SEGMENTS = 96; // 円一周の分割数

            // 同心円 (手前を密にする指数配置。目線 1.6m から地面として見えるようにするため
            //           最内円は数 m 付近に置く必要がある)
            for (int ring = 1; ring <= ringCount; ring++)
            {
                float r = radius * Mathf.Pow((float)ring / ringCount, ringFalloff);
                int   start = verts.Count;

                for (int s = 0; s < SEGMENTS; s++)
                {
                    float a = (float)s / SEGMENTS * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r));
                }
                for (int s = 0; s < SEGMENTS; s++)
                {
                    indices.Add(start + s);
                    indices.Add(start + (s + 1) % SEGMENTS);
                }
            }

            // 放射線 (最内円から外周へ)
            float innerRadius = radius * Mathf.Pow(1f / ringCount, ringFalloff);
            for (int sp = 0; sp < spokeCount; sp++)
            {
                float a = (float)sp / spokeCount * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));

                int start = verts.Count;
                verts.Add(dir * innerRadius);
                verts.Add(dir * radius);
                indices.Add(start);
                indices.Add(start + 1);
            }

            var mesh = new Mesh { name = "Horizon Grid" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;

            var mat = CreateUnlitMaterial(gridColor);
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
        }

        // ── 方位ラベル (N / E / S / W) ───────────────────────────────────
        private void BuildCardinalLabels()
        {
            // az=0 → +Z(北), az=90 → +X(東) ... VRMoonViewer と同じ定義
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

            // 原点 (観測者) の方へ「文字の表面」を向ける。
            // TMP は +Z 方向が表なので、forward は原点向き (= -position) でなければ裏返る。
            go.transform.rotation = Quaternion.LookRotation(-position.normalized, Vector3.up);
        }

        // ── 組み込み URP Unlit シェーダーでマテリアルを作る ──────────────
        // 自前シェーダーを使わないことでコンパイル失敗のリスクを排除する
        private Material CreateUnlitMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color")
                      ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[HorizonReference] Unlit シェーダーが見つかりませんでした。");
                return null;
            }

            var mat = new Material(shader);
            // URP Unlit は _BaseColor、組み込み Unlit は _Color を使う
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);
            return mat;
        }
    }
}
