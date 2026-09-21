using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace MoonObserver.VR
{
    /// <summary>
    /// 観測地点の緯度・経度を決定して VRMoonViewer に反映する。
    ///
    /// 【GPS を使わない理由】
    /// Meta Quest 3 には GPS ハードウェアが搭載されていないため、
    /// UnityEngine.Input.location は実機で座標を返さない。
    /// 加えて本プロジェクトは New Input System 専用設定であり、
    /// legacy Input クラスへのアクセスは例外を投げる。
    /// そのため IP 測位 (都市レベル) を既定とする。
    /// 数 km の誤差は月の高度に 0.05° 未満しか影響しないため天体観測には十分。
    /// </summary>
    public class LocationService : MonoBehaviour
    {
        public enum LocationMode
        {
            /// <summary>Inspector で指定した座標をそのまま使う</summary>
            Manual,
            /// <summary>IP アドレスから現在地を推定する</summary>
            AutoByIP,
            /// <summary>都市名から座標を検索する</summary>
            ByCityName,
        }

        [Header("参照")]
        public VRMoonViewer moonViewer;

        [Header("測位方法")]
        public LocationMode mode = LocationMode.AutoByIP;
        [Tooltip("ByCityName のときに検索する都市名 (英語表記)")]
        public string cityName = "Tokyo";
        [Tooltip("通信タイムアウト (秒)")]
        public int timeoutSec = 8;

        [Header("取得結果 (読み取り専用)")]
        public string resolvedPlaceName = "(not resolved)";

        // ─────────────────────────────────────────────────────────────────
        public bool   HasResolved { get; private set; }
        public double LatitudeDeg  { get; private set; }
        public double LongitudeDeg { get; private set; }

        private const string IP_API   = "https://ipapi.co/json/";
        private const string GEOCODE  = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=en&format=json&name=";

        private void Start()
        {
            switch (mode)
            {
                case LocationMode.AutoByIP:    StartCoroutine(ResolveByIP());       break;
                case LocationMode.ByCityName:  StartCoroutine(ResolveByCity());     break;
                default:                       UseManualCoordinates();              break;
            }
        }

        private void UseManualCoordinates()
        {
            if (moonViewer == null) return;

            LatitudeDeg       = moonViewer.latitudeDeg;
            LongitudeDeg      = moonViewer.longitudeDeg;
            resolvedPlaceName = "Manual";
            HasResolved       = true;

            Debug.Log($"[LocationService] 手動座標を使用: {LatitudeDeg:F4}, {LongitudeDeg:F4}");
        }

        // ── IP 測位 ──────────────────────────────────────────────────────
        private IEnumerator ResolveByIP()
        {
            using var req = UnityWebRequest.Get(IP_API);
            req.timeout = timeoutSec;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LocationService] IP 測位に失敗しました ({req.error})。手動座標を使用します。");
                UseManualCoordinates();
                yield break;
            }

            IpApiResponse data = null;
            try { data = JsonUtility.FromJson<IpApiResponse>(req.downloadHandler.text); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[LocationService] IP 測位の応答を解析できませんでした: " + e.Message);
            }

            // 座標 0,0 は「取得失敗」を意味する (ギニア湾沖の海上)
            if (data == null || (data.latitude == 0.0 && data.longitude == 0.0))
            {
                Debug.LogWarning("[LocationService] IP 測位の結果が不正です。手動座標を使用します。");
                UseManualCoordinates();
                yield break;
            }

            string place = string.IsNullOrEmpty(data.city) ? "Unknown" : data.city;
            if (!string.IsNullOrEmpty(data.country_name)) place += ", " + data.country_name;

            Apply(data.latitude, data.longitude, place);
        }

        // ── 都市名から検索 ───────────────────────────────────────────────
        private IEnumerator ResolveByCity()
        {
            string url = GEOCODE + UnityWebRequest.EscapeURL(cityName);

            using var req = UnityWebRequest.Get(url);
            req.timeout = timeoutSec;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LocationService] 都市名検索に失敗しました ({req.error})。手動座標を使用します。");
                UseManualCoordinates();
                yield break;
            }

            GeocodeResponse data = null;
            try { data = JsonUtility.FromJson<GeocodeResponse>(req.downloadHandler.text); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[LocationService] 都市名検索の応答を解析できませんでした: " + e.Message);
            }

            if (data?.results == null || data.results.Length == 0)
            {
                Debug.LogWarning($"[LocationService] 都市 '{cityName}' が見つかりませんでした。手動座標を使用します。");
                UseManualCoordinates();
                yield break;
            }

            var hit   = data.results[0];
            string place = string.IsNullOrEmpty(hit.country) ? hit.name : $"{hit.name}, {hit.country}";
            Apply(hit.latitude, hit.longitude, place);
        }

        // ── 結果を反映 ───────────────────────────────────────────────────
        private void Apply(double lat, double lon, string placeName)
        {
            LatitudeDeg       = lat;
            LongitudeDeg      = lon;
            resolvedPlaceName = placeName;
            HasResolved       = true;

            if (moonViewer != null)
            {
                moonViewer.latitudeDeg  = lat;
                moonViewer.longitudeDeg = lon;
                moonViewer.OnLocationChanged();
            }

            Debug.Log($"[LocationService] 現在地: {placeName} ({lat:F4}, {lon:F4})");
        }

        // ── JSON DTO ─────────────────────────────────────────────────────
        [System.Serializable]
        private class IpApiResponse
        {
            public string city;
            public string country_name;
            public double latitude;
            public double longitude;
        }

        [System.Serializable]
        private class GeocodeResponse
        {
            public GeocodeHit[] results;
        }

        [System.Serializable]
        private class GeocodeHit
        {
            public string name;
            public string country;
            public double latitude;
            public double longitude;
        }
    }
}
