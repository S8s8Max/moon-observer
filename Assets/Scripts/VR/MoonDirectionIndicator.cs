using UnityEngine;
using TMPro;
using MoonObserver.Astronomy;

namespace MoonObserver.VR
{
    /// <summary>
    /// 月が視野外にある時、画面端に矢印を表示して月の方向を示す
    /// </summary>
    public class MoonDirectionIndicator : MonoBehaviour
    {
        [Header("参照")]
        public Transform moonTransform;
        public Camera    vrCamera;

        [Header("表示設定")]
        [Tooltip("カメラからインジケーターまでの距離 (m)")]
        [Range(1f, 5f)]
        public float displayDistance = 2.5f;

        [Tooltip("画面中心からの半径 (m) ― 小さいほど中央寄り")]
        [Range(0.2f, 1.2f)]
        public float edgeRadius = 0.65f;

        [Tooltip("月がこのマージン内に入ったらインジケーターを非表示")]
        [Range(0.05f, 0.25f)]
        public float visibilityMargin = 0.12f;

        // ─────────────────────────────────────────────────────────────────
        private GameObject  _root;
        private Transform   _arrowPivot;
        private TextMeshPro _arrowTMP;
        private TextMeshPro _infoTMP;

        private void Start()
        {
            if (vrCamera == null)
                vrCamera = Camera.main;

            BuildUI();
        }

        private void LateUpdate()
        {
            if (_root == null || moonTransform == null || vrCamera == null) return;
            UpdatePosition();
        }

        // ── 毎フレーム位置・向き更新 ────────────────────────────────────
        private void UpdatePosition()
        {
            Vector3 vp = vrCamera.WorldToViewportPoint(moonTransform.position);
            float   m  = visibilityMargin;

            bool moonInView = vp.z > 0
                && vp.x > m && vp.x < 1f - m
                && vp.y > m && vp.y < 1f - m;

            _root.SetActive(!moonInView);
            if (moonInView) return;

            // ビューポート上での月の方向ベクトル (中心 = 0,0)
            Vector2 dir2D = vp.z <= 0
                ? new Vector2(0.5f - vp.x, 0.5f - vp.y) // 後方 → 反転
                : new Vector2(vp.x - 0.5f, vp.y - 0.5f);

            if (dir2D.sqrMagnitude < 0.0001f) dir2D = Vector2.up;
            dir2D.Normalize();

            // ワールド空間に変換してインジケーターを配置
            Transform cam = vrCamera.transform;
            Vector3 worldOffset = (cam.right * dir2D.x + cam.up * dir2D.y) * edgeRadius;
            _root.transform.position = cam.position + cam.forward * displayDistance + worldOffset;
            _root.transform.rotation = cam.rotation; // 常にカメラと同じ向き

            // 矢印を月の方向に回転 (Z軸周り、↑が0°)
            float angleDeg = Mathf.Atan2(dir2D.y, dir2D.x) * Mathf.Rad2Deg - 90f;
            _arrowPivot.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        // ── 月の情報を更新 (VRMoonViewer から毎フレーム呼ぶ) ──────────
        public void SetMoonState(MoonState state)
        {
            if (_infoTMP == null) return;

            string aboveBelow = state.AltitudeDeg >= 0 ? "地平線上" : "地平線下";
            _infoTMP.text = $"月 {aboveBelow}\n高度 {state.AltitudeDeg:F0}°";
            _infoTMP.color = state.AltitudeDeg >= 0
                ? new Color(1f, 0.95f, 0.6f)
                : new Color(0.6f, 0.6f, 0.7f); // 地平線下はグレー
        }

        // ── UI 生成 ────────────────────────────────────────────────────
        private void BuildUI()
        {
            _root = new GameObject("Moon Direction Indicator");

            // ── 矢印 (▲ をピボット中心に回転) ─────────────────────────
            var pivotGO = new GameObject("Arrow Pivot");
            pivotGO.transform.SetParent(_root.transform, false);
            _arrowPivot = pivotGO.transform;

            var arrowGO  = new GameObject("Arrow");
            arrowGO.transform.SetParent(_arrowPivot, false);
            arrowGO.transform.localPosition = new Vector3(0f, 0.07f, 0f); // 先端を上へ
            _arrowTMP = arrowGO.AddComponent<TextMeshPro>();
            _arrowTMP.text      = "▲";
            _arrowTMP.fontSize  = 0.14f;
            _arrowTMP.color     = new Color(1f, 0.95f, 0.3f, 0.95f);
            _arrowTMP.alignment = TextAlignmentOptions.Center;
            _arrowTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(0.2f, 0.2f);

            // 円形の枠 (○)
            var circleGO = new GameObject("Circle");
            circleGO.transform.SetParent(_root.transform, false);
            var circleTMP = circleGO.AddComponent<TextMeshPro>();
            circleTMP.text      = "○";
            circleTMP.fontSize  = 0.22f;
            circleTMP.color     = new Color(1f, 0.95f, 0.3f, 0.5f);
            circleTMP.alignment = TextAlignmentOptions.Center;
            circleTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(0.3f, 0.3f);

            // ── テキストラベル (高度情報) ──────────────────────────────
            var labelGO = new GameObject("Info Label");
            labelGO.transform.SetParent(_root.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            _infoTMP = labelGO.AddComponent<TextMeshPro>();
            _infoTMP.text      = "月";
            _infoTMP.fontSize  = 0.055f;
            _infoTMP.color     = Color.white;
            _infoTMP.alignment = TextAlignmentOptions.Center;
            _infoTMP.GetComponent<RectTransform>().sizeDelta = new Vector2(0.4f, 0.2f);

            _root.SetActive(false);
        }
    }
}
