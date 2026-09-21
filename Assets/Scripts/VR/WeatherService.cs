using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MoonObserver.VR
{
    /// <summary>
    /// Open-Meteo から現在の気象データを取得し、観測条件として提供する。
    /// Open-Meteo は API キー不要・非商用無料で利用できる。
    /// </summary>
    public class WeatherService : MonoBehaviour
    {
        [Header("参照")]
        public VRMoonViewer moonViewer;

        [Header("取得設定")]
        [Tooltip("天気を再取得する間隔 (分)")]
        [Range(5f, 120f)]
        public float refreshIntervalMinutes = 20f;
        [Tooltip("通信タイムアウト (秒)")]
        public int timeoutSec = 8;

        [Header("取得結果 (読み取り専用)")]
        [Tooltip("雲量 0-1")]
        public float cloudCover01;
        [Tooltip("相対湿度 0-1")]
        public float humidity01;
        public float temperatureC;
        public string conditionText = "(no data)";

        // ─────────────────────────────────────────────────────────────────
        public bool HasData { get; private set; }

        /// <summary>
        /// 大気の透過率 (0-1)。雲が主要因、湿度によるもやが副次的に効く。
        /// 月と星の明るさに掛ける。
        /// </summary>
        public float Transmittance
        {
            get
            {
                if (!HasData) return 1f;
                float cloudLoss = cloudCover01 * 0.95f;          // 全天曇りでほぼ見えない
                float hazeLoss  = Mathf.Max(0f, humidity01 - 0.6f) * 0.25f; // 高湿度のもや
                return Mathf.Clamp01(1f - cloudLoss - hazeLoss);
            }
        }

        private const string API =
            "https://api.open-meteo.com/v1/forecast" +
            "?current=temperature_2m,relative_humidity_2m,cloud_cover,weather_code" +
            "&latitude={0}&longitude={1}";

        private void Start()
        {
            StartCoroutine(RefreshLoop());
        }

        private IEnumerator RefreshLoop()
        {
            while (true)
            {
                yield return StartCoroutine(Fetch());
                yield return new WaitForSeconds(refreshIntervalMinutes * 60f);
            }
        }

        /// <summary>現在の観測地点の天気を取得する。</summary>
        public IEnumerator Fetch()
        {
            if (moonViewer == null) yield break;

            string url = string.Format(API,
                moonViewer.latitudeDeg.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                moonViewer.longitudeDeg.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));

            using var req = UnityWebRequest.Get(url);
            req.timeout = timeoutSec;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[WeatherService] 天気の取得に失敗しました ({req.error})。");
                yield break;
            }

            OpenMeteoResponse data = null;
            try { data = JsonUtility.FromJson<OpenMeteoResponse>(req.downloadHandler.text); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WeatherService] 天気の応答を解析できませんでした: " + e.Message);
                yield break;
            }

            if (data?.current == null)
            {
                Debug.LogWarning("[WeatherService] 天気の応答に current が含まれていません。");
                yield break;
            }

            cloudCover01  = Mathf.Clamp01(data.current.cloud_cover / 100f);
            humidity01    = Mathf.Clamp01(data.current.relative_humidity_2m / 100f);
            temperatureC  = data.current.temperature_2m;
            conditionText = DescribeWeatherCode(data.current.weather_code);
            HasData       = true;

            Debug.Log($"[WeatherService] {conditionText} / 雲量 {cloudCover01 * 100f:F0}% / " +
                      $"湿度 {humidity01 * 100f:F0}% / {temperatureC:F1}℃ " +
                      $"(透過率 {Transmittance * 100f:F0}%)");
        }

        /// <summary>WMO 気象コードを短い英語表記に変換する。</summary>
        private static string DescribeWeatherCode(int code)
        {
            if (code == 0)                  return "Clear";
            if (code <= 2)                  return "Partly cloudy";
            if (code == 3)                  return "Overcast";
            if (code >= 45 && code <= 48)   return "Fog";
            if (code >= 51 && code <= 57)   return "Drizzle";
            if (code >= 61 && code <= 67)   return "Rain";
            if (code >= 71 && code <= 77)   return "Snow";
            if (code >= 80 && code <= 82)   return "Showers";
            if (code >= 85 && code <= 86)   return "Snow showers";
            if (code >= 95)                 return "Thunderstorm";
            return "Unknown";
        }

        // ── JSON DTO ─────────────────────────────────────────────────────
        [System.Serializable]
        private class OpenMeteoResponse
        {
            public CurrentBlock current;
        }

        [System.Serializable]
        private class CurrentBlock
        {
            public float temperature_2m;
            public float relative_humidity_2m;
            public float cloud_cover;
            public int   weather_code;
        }
    }
}
