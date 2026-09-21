using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;
using MoonObserver.Astronomy;
using MoonObserver.Rendering;
using MoonObserver.Atmospheric;

namespace MoonObserver.VR
{
    /// <summary>
    /// VRリグ・入力・UI・天文計算を統合するメインコントローラー
    /// XR Interaction Toolkit 3.x 対応
    /// </summary>
    public class VRMoonViewer : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        // Inspector プロパティ
        // ─────────────────────────────────────────────────────────────────────
        [Header("月レンダラー")]
        public MoonRenderer moonRenderer;
        public Transform    moonTransform;

        [Header("大気エフェクト")]
        public AtmosphericEffects atmosphericEffects;
        public Rendering.SkyboxController skyboxController;

        [Header("観測地点")]
        [Tooltip("観測地緯度 (度、北正)")]
        public double latitudeDeg  = 35.6895;  // 東京
        [Tooltip("観測地経度 (度、東正)")]
        public double longitudeDeg = 139.6917;

        [Header("時刻設定")]
        public bool useRealTime = true;
        [Tooltip("useRealTime=false 時の参照 UTC 日時")]
        public string manualUtcTime = "2026-09-21T12:00:00";

        [Header("XR コントローラー")]
        public ActionBasedController leftController;
        public ActionBasedController rightController;

        [Header("UI パネル")]
        public GameObject infoPanel;
        public TextMeshProUGUI altitudeText;
        public TextMeshProUGUI azimuthText;
        public TextMeshProUGUI moonAgeText;
        public TextMeshProUGUI illuminationText;
        public TextMeshProUGUI distanceText;
        public TextMeshProUGUI currentTimeText;

        // ─────────────────────────────────────────────────────────────────────
        // 定数
        // ─────────────────────────────────────────────────────────────────────
        private const float STICK_THRESHOLD  = 0.3f;
        private const float UPDATE_INTERVAL  = 1f / 30f; // 30fps 更新

        // ─────────────────────────────────────────────────────────────────────
        // 内部状態
        // ─────────────────────────────────────────────────────────────────────
        private DateTime   _currentUtc;
        private MoonState  _moonState;
        private float      _updateTimer;
        private bool       _infoPanelVisible = false;
        private bool       _illusionEnabled  = true;

        // XR Input Action 参照 (Input System)
        private UnityEngine.InputSystem.InputAction _rightStickAction;
        private UnityEngine.InputSystem.InputAction _leftStickAction;
        private UnityEngine.InputSystem.InputAction _buttonAAction;
        private UnityEngine.InputSystem.InputAction _buttonBAction;
        private UnityEngine.InputSystem.InputAction _rightGripAction;

        private void Awake()
        {
            _currentUtc = useRealTime ? DateTime.UtcNow : ParseManualTime();
            InitializeInputActions();
        }

        private void Start()
        {
            UpdateMoonState();
            SetMoonTransformPosition();
        }

        private void Update()
        {
            HandleInput();

            _updateTimer += Time.deltaTime;
            if (_updateTimer >= UPDATE_INTERVAL)
            {
                _updateTimer = 0f;
                if (useRealTime) _currentUtc = DateTime.UtcNow;
                UpdateMoonState();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 入力処理
        // ─────────────────────────────────────────────────────────────────────
        private void HandleInput()
        {
            // 右スティック — 時刻操作
            var rightStick = _rightStickAction?.ReadValue<Vector2>() ?? Vector2.zero;
            if (Mathf.Abs(rightStick.x) > STICK_THRESHOLD)
            {
                // 左右: ±1時間
                float hours = rightStick.x > 0 ? 1f : -1f;
                _currentUtc = _currentUtc.AddHours(hours * Time.deltaTime * 3f);
                useRealTime = false;
            }
            if (Mathf.Abs(rightStick.y) > STICK_THRESHOLD)
            {
                // 前後: ±10分
                float mins = rightStick.y > 0 ? 10f : -10f;
                _currentUtc = _currentUtc.AddMinutes(mins * Time.deltaTime * 3f);
                useRealTime = false;
            }

            // 左スティック — 視点回転はXR Rigで自動処理 (Continuous Turn Provider)

            // Aボタン — 情報パネルトグル
            if (_buttonAAction?.WasPressedThisFrame() == true)
                ToggleInfoPanel();

            // Bボタン — 月の錯覚トグル
            if (_buttonBAction?.WasPressedThisFrame() == true)
                ToggleMoonIllusion();

            // グリップ長押し — 現在時刻にリセット
            if ((_rightGripAction?.ReadValue<float>() ?? 0f) > 0.9f)
            {
                _currentUtc = DateTime.UtcNow;
                useRealTime = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 月の状態更新
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateMoonState()
        {
            _moonState = MoonAstronomyEngine.Calculate(_currentUtc, latitudeDeg, longitudeDeg);

            // 大気色フィルタ
            float alt = (float)_moonState.AltitudeDeg;
            Color atmColor = atmosphericEffects != null
                ? atmosphericEffects.ComputeAtmosphericColor(alt)
                : Color.white;

            // 月の錯覚スケール
            float illusionScale = atmosphericEffects != null
                ? atmosphericEffects.ComputeIllusionScale(alt)
                : 1.0f;

            // レンダラー更新
            if (moonRenderer != null)
                moonRenderer.UpdateMoon(_moonState, illusionScale);

            // 大気シェーダーパラメータ
            if (atmosphericEffects != null)
                atmosphericEffects.ApplyToShader(alt);

            // スカイボックス更新
            if (skyboxController != null)
                skyboxController.UpdateAtmosphere(_moonState, atmColor);

            // 月の3D位置を天球上の正しい位置に配置
            SetMoonTransformPosition();

            // UI更新
            UpdateInfoUI();
        }

        // 月の天球座標 → VR空間位置変換
        private void SetMoonTransformPosition()
        {
            if (moonTransform == null) return;

            float altRad = (float)(_moonState.AltitudeDeg * Mathf.Deg2Rad);
            float azRad  = (float)(_moonState.AzimuthDeg  * Mathf.Deg2Rad);
            float dist   = moonRenderer != null ? moonRenderer.placementDistanceM : 1000f;

            // VR空間: Y=上、Z=北(Unity座標系では-Z=前)
            // 方位角: 北=0°, 東=90° → Unity XZ平面
            Vector3 moonDir = new Vector3(
                Mathf.Sin(azRad) * Mathf.Cos(altRad),
                Mathf.Sin(altRad),
                Mathf.Cos(azRad) * Mathf.Cos(altRad));

            moonTransform.position = moonDir * dist;
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI
        // ─────────────────────────────────────────────────────────────────────
        private void UpdateInfoUI()
        {
            if (!_infoPanelVisible) return;

            // JST = UTC + 9h
            DateTime jst = _currentUtc.AddHours(9);
            string dirLabel = AzimuthToDirectionLabel((float)_moonState.AzimuthDeg);

            if (altitudeText)    altitudeText.text    = $"高度:     {_moonState.AltitudeDeg:F1}°";
            if (azimuthText)     azimuthText.text     = $"方位角:   {dirLabel} {_moonState.AzimuthDeg:F1}°";
            if (moonAgeText)     moonAgeText.text     = $"月齢:     {_moonState.MoonAge:F1}日";
            if (illuminationText) illuminationText.text = $"輝面比:   {_moonState.IlluminationFraction * 100:F1}%";
            if (distanceText)    distanceText.text    = $"月の距離: {_moonState.DistanceKm:F0} km";
            if (currentTimeText) currentTimeText.text = $"現在時刻: {jst:yyyy-MM-dd HH:mm} JST";
        }

        private void ToggleInfoPanel()
        {
            _infoPanelVisible = !_infoPanelVisible;
            if (infoPanel != null)
                infoPanel.SetActive(_infoPanelVisible);
            if (_infoPanelVisible) UpdateInfoUI();
        }

        private void ToggleMoonIllusion()
        {
            _illusionEnabled = !_illusionEnabled;
            if (atmosphericEffects != null)
                atmosphericEffects.enableMoonIllusion = _illusionEnabled;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ユーティリティ
        // ─────────────────────────────────────────────────────────────────────
        private string AzimuthToDirectionLabel(float az)
        {
            string[] dirs = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE",
                              "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            int idx = Mathf.RoundToInt(az / 22.5f) % 16;
            return dirs[idx];
        }

        private DateTime ParseManualTime()
        {
            if (DateTime.TryParse(manualUtcTime, out var dt))
                return dt.ToUniversalTime();
            return DateTime.UtcNow;
        }

        private void InitializeInputActions()
        {
            // XR Input System 経由でアクションを取得
            var actionMap = new UnityEngine.InputSystem.InputActionMap("VRMoonViewer");
            _rightStickAction = actionMap.AddAction("RightStick",
                binding: "<XRController>{RightHand}/thumbstick");
            _leftStickAction  = actionMap.AddAction("LeftStick",
                binding: "<XRController>{LeftHand}/thumbstick");
            _buttonAAction    = actionMap.AddAction("ButtonA",
                binding: "<XRController>{RightHand}/primaryButton");
            _buttonBAction    = actionMap.AddAction("ButtonB",
                binding: "<XRController>{RightHand}/secondaryButton");
            _rightGripAction  = actionMap.AddAction("RightGrip",
                binding: "<XRController>{RightHand}/grip");
            actionMap.Enable();
        }

        private void OnDestroy()
        {
            _rightStickAction?.Disable();
            _leftStickAction?.Disable();
            _buttonAAction?.Disable();
            _buttonBAction?.Disable();
            _rightGripAction?.Disable();
        }
    }
}
