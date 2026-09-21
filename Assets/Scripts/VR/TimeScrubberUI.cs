using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace MoonObserver.VR
{
    /// <summary>
    /// 画面下部に時刻スクラブ用のスライダーを生成する。
    /// ドラッグすると基準時刻からの前後の時間を変更でき、月と星の位置が追従する。
    ///
    /// PC テスト用に Screen Space - Overlay で構築する
    /// (VR 実機ではワールド空間 UI へ置き換える想定)。
    /// </summary>
    public class TimeScrubberUI : MonoBehaviour
    {
        [Header("参照")]
        public VRMoonViewer viewer;

        [Header("スクラブ範囲")]
        [Tooltip("基準時刻から前後に動かせる時間 (h)")]
        [Range(3f, 36f)]
        public float rangeHours = 12f;

        // ─────────────────────────────────────────────────────────────────
        private Slider          _slider;
        private TextMeshProUGUI _label;
        private bool            _suppressCallback;

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            UpdateLabel(0f);
        }

        private void Update()
        {
            // リアルタイム追従中はスライダーを中央へ戻しておく
            if (viewer != null && viewer.IsRealTime && _slider != null && _slider.value != 0f)
            {
                _suppressCallback = true;
                _slider.value     = 0f;
                _suppressCallback = false;
                UpdateLabel(0f);
            }
        }

        // ── uGUI の入力に必要な EventSystem を用意する ───────────────────
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // New Input System 専用プロジェクトなので InputSystemUIInputModule を使う
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ── UI 構築 ──────────────────────────────────────────────────────
        private void BuildUI()
        {
            var canvasGO = new GameObject("Time Scrubber Canvas");
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── 背景パネル ───────────────────────────────────────────
            var panel = CreateUIObject("Panel", canvasGO.transform);
            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin        = new Vector2(0.5f, 0f);
            panelRT.anchorMax        = new Vector2(0.5f, 0f);
            panelRT.pivot            = new Vector2(0.5f, 0f);
            panelRT.anchoredPosition = new Vector2(0f, 24f);
            panelRT.sizeDelta        = new Vector2(900f, 130f);

            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.55f);

            // ── 時刻ラベル ───────────────────────────────────────────
            var labelGO = CreateUIObject("Time Label", panel.transform);
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin        = new Vector2(0.5f, 1f);
            labelRT.anchorMax        = new Vector2(0.5f, 1f);
            labelRT.pivot            = new Vector2(0.5f, 1f);
            labelRT.anchoredPosition = new Vector2(0f, -10f);
            labelRT.sizeDelta        = new Vector2(860f, 46f);

            _label = labelGO.AddComponent<TextMeshProUGUI>();
            _label.fontSize  = 30f;
            _label.color     = Color.white;
            _label.alignment = TextAlignmentOptions.Center;

            // ── スライダー ───────────────────────────────────────────
            _slider = CreateSlider(panel.transform);
            _slider.minValue = -rangeHours;
            _slider.maxValue =  rangeHours;
            _slider.value    = 0f;
            _slider.onValueChanged.AddListener(OnSliderChanged);

            // ── Now ボタン ───────────────────────────────────────────
            CreateNowButton(panel.transform);
        }

        private void OnSliderChanged(float hours)
        {
            if (_suppressCallback) return;
            viewer?.SetTimeOffsetHours(hours);
            UpdateLabel(hours);
        }

        private void UpdateLabel(float hours)
        {
            if (_label == null) return;

            string jst = viewer != null
                ? viewer.CurrentUtc.AddHours(9).ToString("yyyy-MM-dd HH:mm")
                : "--";

            string sign = hours >= 0 ? "+" : "-";
            _label.text = $"JST {jst}    ({sign}{Mathf.Abs(hours):F1} h)   drag to scrub";
        }

        // ── UI ヘルパー ──────────────────────────────────────────────────
        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private Slider CreateSlider(Transform parent)
        {
            var sliderGO = CreateUIObject("Time Slider", parent);
            var rt = sliderGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(-50f, 22f);
            rt.sizeDelta        = new Vector2(700f, 34f);

            var slider = sliderGO.AddComponent<Slider>();

            // 溝 (background)
            var bg = CreateUIObject("Background", sliderGO.transform);
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.18f);

            // ハンドル
            var handleArea = CreateUIObject("Handle Slide Area", sliderGO.transform);
            var handleAreaRT = handleArea.GetComponent<RectTransform>();
            StretchFull(handleAreaRT);
            handleAreaRT.offsetMin = new Vector2(14f, 0f);
            handleAreaRT.offsetMax = new Vector2(-14f, 0f);

            var handle = CreateUIObject("Handle", handleArea.transform);
            var handleRT = handle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(28f, 44f);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(1f, 0.93f, 0.6f, 0.95f);

            slider.targetGraphic = handleImg;
            slider.handleRect    = handleRT;
            slider.direction     = Slider.Direction.LeftToRight;

            return slider;
        }

        private void CreateNowButton(Transform parent)
        {
            var btnGO = CreateUIObject("Now Button", parent);
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(370f, 20f);
            rt.sizeDelta        = new Vector2(110f, 38f);

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.25f, 0.45f, 0.70f, 0.9f);

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                viewer?.ResetToNow();
                _suppressCallback = true;
                _slider.value     = 0f;
                _suppressCallback = false;
                UpdateLabel(0f);
            });

            var textGO = CreateUIObject("Text", btnGO.transform);
            StretchFull(textGO.GetComponent<RectTransform>());
            var text = textGO.AddComponent<TextMeshProUGUI>();
            text.text      = "Now";
            text.fontSize  = 24f;
            text.color     = Color.white;
            text.alignment = TextAlignmentOptions.Center;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
