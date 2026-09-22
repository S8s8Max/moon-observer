using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using MoonObserver.Astronomy;
using MoonObserver.VR;

namespace MoonObserver.UI
{
    /// <summary>
    /// 観測情報と時刻スクラバーをまとめたモダンな HUD。
    ///
    /// 左上  : 観測地点・天候・ステータスログ
    /// 右上  : 月のデータ
    /// 下中央: 時刻スクラバー
    ///
    /// PC テスト用に Screen Space - Overlay で構築する。
    /// VR 実機では canvasMode を WorldSpace に変えるだけで移行できる。
    /// </summary>
    public class ObservationHUD : MonoBehaviour
    {
        [Header("参照")]
        public VRMoonViewer viewer;

        [Header("表示設定")]
        public RenderMode canvasMode = RenderMode.ScreenSpaceOverlay;
        [Tooltip("基準時刻から前後に動かせる時間 (h)")]
        [Range(3f, 36f)]
        public float scrubRangeHours = 12f;
        [Tooltip("ステータスログの保持件数")]
        [Range(1, 6)]
        public int statusLineCount = 3;

        // ── 月データ行 ───────────────────────────────────────────────────
        private TextMeshProUGUI _altValue, _azValue, _ageValue, _illumValue, _distValue;

        // ── 観測地・天候 ─────────────────────────────────────────────────
        private TextMeshProUGUI _siteValue, _coordValue, _weatherValue, _seeingValue;

        // ── 時刻 ─────────────────────────────────────────────────────────
        private TextMeshProUGUI _clockValue, _dateValue, _offsetValue;
        private Slider          _slider;
        private bool            _suppressCallback;

        // ── ステータスログ ───────────────────────────────────────────────
        private readonly List<string>    _statusLines = new();
        private TextMeshProUGUI          _statusText;

        private GameObject _root;

        // ─────────────────────────────────────────────────────────────────
        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
        }

        private void OnEnable()  => ObservationStatus.OnMessage += HandleStatus;
        private void OnDisable() => ObservationStatus.OnMessage -= HandleStatus;

        private void Update()
        {
            // リアルタイム追従中はスライダーを中央へ戻す
            if (viewer != null && viewer.IsRealTime && _slider != null && _slider.value != 0f)
            {
                SetSliderSilently(0f);
                UpdateOffsetChip(0f);
            }
        }

        public void ToggleVisibility()
        {
            if (_root != null) _root.SetActive(!_root.activeSelf);
        }

        // ── 毎フレーム呼ばれる表示更新 ───────────────────────────────────
        public void Refresh(MoonState state, DateTime utc,
                            double latitudeDeg, double longitudeDeg,
                            ObserverLocationProvider location, WeatherService weather)
        {
            if (_altValue == null) return;

            _altValue.text   = $"{state.AltitudeDeg:F1}°";
            _altValue.color  = state.AltitudeDeg >= 0 ? UITheme.AccentMoon : UITheme.TextMuted;

            _azValue.text    = $"{AzimuthLabel((float)state.AzimuthDeg)} {state.AzimuthDeg:F0}°";
            _ageValue.text   = $"{state.MoonAge:F1} d";
            _illumValue.text = $"{state.IlluminationFraction * 100f:F0} %";
            _distValue.text  = $"{state.DistanceKm:N0} km";

            DateTime jst = utc.AddHours(9);
            _clockValue.text = jst.ToString("HH:mm");
            _dateValue.text  = jst.ToString("ddd, MMM d yyyy");

            // 観測地点
            bool resolved = location != null && location.HasResolved;
            _siteValue.text  = resolved ? location.resolvedPlaceName : "Default site";
            _siteValue.color = resolved ? UITheme.TextPrimary : UITheme.TextMuted;
            _coordValue.text = $"{latitudeDeg:F3}, {longitudeDeg:F3}";

            // 天候
            if (weather != null && weather.HasData)
            {
                _weatherValue.text  = $"{weather.conditionText}  ·  {weather.temperatureC:F0}°C";
                _weatherValue.color = UITheme.TextPrimary;

                float seeing = weather.Transmittance;
                _seeingValue.text  = $"cloud {weather.cloudCover01 * 100f:F0}%  ·  visibility {seeing * 100f:F0}%";
                _seeingValue.color = seeing > 0.7f ? UITheme.Success
                                   : seeing > 0.35f ? UITheme.Warning
                                   : UITheme.TextMuted;
            }
            else
            {
                _weatherValue.text  = "No weather data";
                _weatherValue.color = UITheme.TextMuted;
                _seeingValue.text   = "";
            }
        }

        // ── ステータス受信 ───────────────────────────────────────────────
        private void HandleStatus(string message, ObservationStatus.Level level)
        {
            string color = level switch
            {
                ObservationStatus.Level.Success => ColorUtility.ToHtmlStringRGB(UITheme.Success),
                ObservationStatus.Level.Warning => ColorUtility.ToHtmlStringRGB(UITheme.Warning),
                _                               => ColorUtility.ToHtmlStringRGB(UITheme.TextSecondary),
            };

            _statusLines.Add($"<color=#{color}>{message}</color>");
            while (_statusLines.Count > statusLineCount)
                _statusLines.RemoveAt(0);

            if (_statusText != null)
                _statusText.text = string.Join("\n", _statusLines);
        }

        // ─────────────────────────────────────────────────────────────────
        // UI 構築
        // ─────────────────────────────────────────────────────────────────
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // New Input System 専用プロジェクトなので InputSystemUIInputModule が必須
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void BuildUI()
        {
            _root = new GameObject("Observation HUD");
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode   = canvasMode;
            canvas.sortingOrder = 100;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            _root.AddComponent<GraphicRaycaster>();

            BuildSiteCard(_root.transform);
            BuildMoonCard(_root.transform);
            BuildTimeBar(_root.transform);
        }

        // ── 左上: 観測地点・天候・ステータス ─────────────────────────────
        private void BuildSiteCard(Transform parent)
        {
            var card = Card(parent, "Site Card", new Vector2(0f, 1f), new Vector2(440f, 250f),
                            new Vector2(UITheme.ScreenMargin, -UITheme.ScreenMargin));

            float y = -UITheme.CardPadding;

            SectionTitle(card.transform, "OBSERVATION SITE", ref y);
            _siteValue  = ValueLine(card.transform, UITheme.FontValue, UITheme.TextPrimary, ref y);
            _coordValue = ValueLine(card.transform, UITheme.FontLabel, UITheme.AccentCool, ref y, 26f);

            y -= 10f;
            Divider(card.transform, ref y);

            SectionTitle(card.transform, "CONDITIONS", ref y);
            _weatherValue = ValueLine(card.transform, UITheme.FontBody, UITheme.TextPrimary, ref y, 30f);
            _seeingValue  = ValueLine(card.transform, UITheme.FontLabel, UITheme.TextSecondary, ref y, 26f);

            y -= 8f;
            Divider(card.transform, ref y);

            _statusText = ValueLine(card.transform, 15f, UITheme.TextMuted, ref y,
                                    18f * statusLineCount);
            _statusText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _statusText.text = "";

            // 中身に合わせてカードの高さを決める
            var rt = card.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, -y + UITheme.CardPadding);
        }

        // ── 右上: 月のデータ ─────────────────────────────────────────────
        private void BuildMoonCard(Transform parent)
        {
            var card = Card(parent, "Moon Card", new Vector2(1f, 1f), new Vector2(370f, 300f),
                            new Vector2(-UITheme.ScreenMargin, -UITheme.ScreenMargin));

            float y = -UITheme.CardPadding;

            SectionTitle(card.transform, "MOON", ref y);

            _altValue   = DataRow(card.transform, "Altitude",     ref y);
            _azValue    = DataRow(card.transform, "Azimuth",      ref y);
            _ageValue   = DataRow(card.transform, "Age",          ref y);
            _illumValue = DataRow(card.transform, "Illumination", ref y);
            _distValue  = DataRow(card.transform, "Distance",     ref y);

            var rt = card.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, -y + UITheme.CardPadding);
        }

        // ── 下中央: 時刻スクラバー ───────────────────────────────────────
        private void BuildTimeBar(Transform parent)
        {
            var card = Card(parent, "Time Bar", new Vector2(0.5f, 0f), new Vector2(720f, 148f),
                            new Vector2(0f, UITheme.ScreenMargin));

            // 大きな時刻表示
            _clockValue = Text(card.transform, "Clock", UITheme.FontDisplay, UITheme.TextPrimary,
                               TextAlignmentOptions.Left);
            Place(_clockValue, new Vector2(0f, 1f), new Vector2(200f, 54f),
                  new Vector2(UITheme.CardPadding, -16f));

            _dateValue = Text(card.transform, "Date", UITheme.FontLabel, UITheme.TextSecondary,
                              TextAlignmentOptions.Left);
            Place(_dateValue, new Vector2(0f, 1f), new Vector2(260f, 24f),
                  new Vector2(UITheme.CardPadding + 3f, -70f));

            // オフセット表示チップ
            var chip = new GameObject("Offset Chip");
            chip.transform.SetParent(card.transform, false);
            var chipRT  = chip.AddComponent<RectTransform>();
            chipRT.anchorMin = chipRT.anchorMax = new Vector2(0f, 1f);
            chipRT.pivot     = new Vector2(0f, 1f);
            chipRT.anchoredPosition = new Vector2(218f, -22f);
            chipRT.sizeDelta = new Vector2(104f, 38f);

            var chipImg = chip.AddComponent<Image>();
            chipImg.sprite = UISpriteFactory.Pill;
            chipImg.type   = Image.Type.Sliced;
            chipImg.color  = new Color(UITheme.AccentMoon.r, UITheme.AccentMoon.g,
                                       UITheme.AccentMoon.b, 0.16f);

            _offsetValue = Text(chip.transform, "Offset", UITheme.FontLabel, UITheme.AccentMoon,
                                TextAlignmentOptions.Center);
            StretchFull(_offsetValue.rectTransform);

            // スライダー
            _slider = BuildSlider(card.transform);

            // NOW ボタン
            BuildNowButton(card.transform);
        }

        private Slider BuildSlider(Transform parent)
        {
            var go = new GameObject("Time Slider");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(UITheme.CardPadding, 26f);
            rt.offsetMax = new Vector2(-136f, 26f + 26f);

            var slider = go.AddComponent<Slider>();
            slider.minValue  = -scrubRangeHours;
            slider.maxValue  =  scrubRangeHours;
            slider.value     = 0f;
            slider.direction = Slider.Direction.LeftToRight;

            // 溝 (細いライン)
            var track = new GameObject("Track");
            track.transform.SetParent(go.transform, false);
            var trackRT = track.AddComponent<RectTransform>();
            trackRT.anchorMin = new Vector2(0f, 0.5f);
            trackRT.anchorMax = new Vector2(1f, 0.5f);
            trackRT.pivot     = new Vector2(0.5f, 0.5f);
            trackRT.sizeDelta = new Vector2(0f, 5f);
            trackRT.anchoredPosition = Vector2.zero;
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite = UISpriteFactory.RoundedSmall;
            trackImg.type   = Image.Type.Sliced;
            trackImg.color  = UITheme.TrackColor;

            // つまみ
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);
            var areaRT = handleArea.AddComponent<RectTransform>();
            StretchFull(areaRT);
            areaRT.offsetMin = new Vector2(11f, 0f);
            areaRT.offsetMax = new Vector2(-11f, 0f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(22f, 22f);
            var handleImg = handle.AddComponent<Image>();
            handleImg.sprite = UISpriteFactory.Circle;
            handleImg.color  = UITheme.AccentMoon;

            slider.targetGraphic = handleImg;
            slider.handleRect    = handleRT;
            slider.onValueChanged.AddListener(OnSliderChanged);

            return slider;
        }

        private void BuildNowButton(Transform parent)
        {
            var go = new GameObject("Now Button");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-UITheme.CardPadding, 22f);
            rt.sizeDelta = new Vector2(96f, 38f);

            var img = go.AddComponent<Image>();
            img.sprite = UISpriteFactory.Pill;
            img.type   = Image.Type.Sliced;
            img.color  = new Color(UITheme.AccentCool.r, UITheme.AccentCool.g,
                                   UITheme.AccentCool.b, 0.22f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            btn.onClick.AddListener(() =>
            {
                viewer?.ResetToNow();
                SetSliderSilently(0f);
                UpdateOffsetChip(0f);
            });

            var label = Text(go.transform, "Label", UITheme.FontLabel, UITheme.AccentCool,
                             TextAlignmentOptions.Center);
            label.text = "NOW";
            label.characterSpacing = 6f;
            StretchFull(label.rectTransform);
        }

        // ── スライダー操作 ───────────────────────────────────────────────
        private void OnSliderChanged(float hours)
        {
            if (_suppressCallback) return;
            viewer?.SetTimeOffsetHours(hours);
            UpdateOffsetChip(hours);
        }

        private void SetSliderSilently(float value)
        {
            _suppressCallback = true;
            _slider.value     = value;
            _suppressCallback = false;
        }

        private void UpdateOffsetChip(float hours)
        {
            if (_offsetValue == null) return;

            if (Mathf.Abs(hours) < 0.05f)
            {
                _offsetValue.text  = "LIVE";
                _offsetValue.color = UITheme.Success;
            }
            else
            {
                // LiberationSans SDF に無い文字は豆腐になるため ASCII の - を使う
                _offsetValue.text  = $"{(hours >= 0 ? "+" : "-")}{Mathf.Abs(hours):F1} h";
                _offsetValue.color = UITheme.AccentMoon;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // 構築ヘルパー
        // ─────────────────────────────────────────────────────────────────
        private static GameObject Card(Transform parent, string name, Vector2 anchor,
                                       Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;

            // 外側を枠色、内側 1px 内寄せを面色にして 1px の縁取りを作る。
            // 枠を子にすると親の Image より後に描画されカード全体が明るくなるため、
            // 枠を親側、面を子側にする (文字などの内容は親に足すので面より手前に来る)。
            var borderImg = go.AddComponent<Image>();
            borderImg.sprite = UISpriteFactory.RoundedCard;
            borderImg.type   = Image.Type.Sliced;
            borderImg.color  = UITheme.Border;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            var frt = fill.AddComponent<RectTransform>();
            StretchFull(frt);
            frt.offsetMin = new Vector2(1f, 1f);
            frt.offsetMax = new Vector2(-1f, -1f);

            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = UISpriteFactory.RoundedCard;
            fillImg.type   = Image.Type.Sliced;
            fillImg.color  = UITheme.Surface;
            fillImg.raycastTarget = false;

            return go;
        }

        /// <summary>ラベル (小・薄) と値 (大・明) を左右に並べた行。</summary>
        private static TextMeshProUGUI DataRow(Transform parent, string labelText, ref float y)
        {
            var label = Text(parent, $"{labelText} Label", UITheme.FontLabel,
                             UITheme.TextSecondary, TextAlignmentOptions.Left);
            label.text = labelText;
            Place(label, new Vector2(0f, 1f), new Vector2(180f, UITheme.RowHeight),
                  new Vector2(UITheme.CardPadding, y));

            var value = Text(parent, $"{labelText} Value", UITheme.FontValue,
                             UITheme.TextPrimary, TextAlignmentOptions.Right);
            Place(value, new Vector2(1f, 1f), new Vector2(210f, UITheme.RowHeight),
                  new Vector2(-UITheme.CardPadding, y));

            y -= UITheme.RowHeight + 6f;
            return value;
        }

        private static void SectionTitle(Transform parent, string title, ref float y)
        {
            var t = Text(parent, $"{title} Title", UITheme.FontLabel - 2f,
                         UITheme.TextMuted, TextAlignmentOptions.Left);
            t.text = title;
            t.characterSpacing = 8f;
            Place(t, new Vector2(0f, 1f), new Vector2(340f, 22f),
                  new Vector2(UITheme.CardPadding, y));
            y -= 30f;
        }

        private static TextMeshProUGUI ValueLine(Transform parent, float fontSize, Color color,
                                                 ref float y, float height = 32f)
        {
            var t = Text(parent, "Line", fontSize, color, TextAlignmentOptions.TopLeft);
            Place(t, new Vector2(0f, 1f), new Vector2(396f, height),
                  new Vector2(UITheme.CardPadding, y));
            y -= height;
            return t;
        }

        private static void Divider(Transform parent, ref float y)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            // 横方向はストレッチなので sizeDelta.x が「アンカー幅からの差分」になる。
            // 左右に CardPadding 分の余白を作るため負の値を入れる。
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(-UITheme.CardPadding * 2f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);

            var img = go.AddComponent<Image>();
            img.color = UITheme.Border;
            img.raycastTarget = false;

            y -= 16f;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, float size,
                                            Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize      = size;
            t.color         = color;
            t.alignment     = align;
            t.raycastTarget = false;
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            return t;
        }

        private static void Place(TextMeshProUGUI t, Vector2 anchor, Vector2 size, Vector2 pos)
        {
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = new Vector2(anchor.x, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static string AzimuthLabel(float az)
        {
            string[] dirs = { "N","NNE","NE","ENE","E","ESE","SE","SSE",
                              "S","SSW","SW","WSW","W","WNW","NW","NNW" };
            return dirs[Mathf.RoundToInt(az / 22.5f) % 16];
        }
    }
}
