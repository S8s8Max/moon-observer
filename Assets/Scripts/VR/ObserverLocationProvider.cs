using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
using MoonObserver.UI;

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
    ///
    /// 無料の IP 測位 API は予告なくレート制限や 403 を返すため、
    /// 複数のプロバイダを順に試すフォールバック方式にしている。
    /// </summary>
    public class ObserverLocationProvider : MonoBehaviour
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
        [Tooltip("1 プロバイダあたりの通信タイムアウト (秒)")]
        public int timeoutSec = 6;

        [Header("取得結果 (読み取り専用)")]
        public string resolvedPlaceName = "(not resolved)";

        // ─────────────────────────────────────────────────────────────────
        public bool   HasResolved { get; private set; }
        public double LatitudeDeg  { get; private set; }
        public double LongitudeDeg { get; private set; }

        private const string GEOCODE =
            "https://geocoding-api.open-meteo.com/v1/search?count=1&language=en&format=json&name=";

        /// <summary>IP 測位プロバイダ。上から順に試し、最初に成功したものを使う。</summary>
        private static readonly (string url, Func<string, GeoResult> parse)[] IpProviders =
        {
            ("https://ipwho.is/",                   ParseIpWhoIs),
            ("https://freeipapi.com/api/json",      ParseFreeIpApi),
            ("https://get.geojs.io/v1/ip/geo.json", ParseGeoJs),
        };

        private void Start()
        {
            switch (mode)
            {
                case LocationMode.AutoByIP:   StartCoroutine(ResolveByIP());   break;
                case LocationMode.ByCityName: StartCoroutine(ResolveByCity()); break;
                default:                      UseManualCoordinates();          break;
            }
        }

        private void UseManualCoordinates()
        {
            if (moonViewer == null) return;

            LatitudeDeg       = moonViewer.latitudeDeg;
            LongitudeDeg      = moonViewer.longitudeDeg;
            resolvedPlaceName = "Manual site";
            HasResolved       = true;

            ObservationStatus.Report(
                $"Using manual coordinates ({LatitudeDeg:F3}, {LongitudeDeg:F3})");
        }

        // ── IP 測位 (複数プロバイダを順に試す) ───────────────────────────
        private IEnumerator ResolveByIP()
        {
            ObservationStatus.Report("Locating observer by IP...");

            for (int i = 0; i < IpProviders.Length; i++)
            {
                var (url, parse) = IpProviders[i];
                string host = HostOf(url);

                using var req = UnityWebRequest.Get(url);
                req.timeout = timeoutSec;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    ObservationStatus.Report($"{host} unavailable ({req.error})",
                                             ObservationStatus.Level.Warning);
                    continue;
                }

                GeoResult result = null;
                try { result = parse(req.downloadHandler.text); }
                catch (Exception e)
                {
                    ObservationStatus.Report($"{host} returned unreadable data ({e.Message})",
                                             ObservationStatus.Level.Warning);
                }

                if (result == null || !result.IsValid)
                {
                    ObservationStatus.Report($"{host} returned no usable position",
                                             ObservationStatus.Level.Warning);
                    continue;
                }

                Apply(result.Latitude, result.Longitude, result.PlaceName);
                yield break;
            }

            ObservationStatus.Report("All IP lookups failed - falling back to manual coordinates",
                                     ObservationStatus.Level.Warning);
            UseManualCoordinates();
        }

        // ── 都市名から検索 ───────────────────────────────────────────────
        private IEnumerator ResolveByCity()
        {
            ObservationStatus.Report($"Looking up \"{cityName}\"...");

            using var req = UnityWebRequest.Get(GEOCODE + UnityWebRequest.EscapeURL(cityName));
            req.timeout = timeoutSec;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                ObservationStatus.Report($"City lookup failed ({req.error})",
                                         ObservationStatus.Level.Warning);
                UseManualCoordinates();
                yield break;
            }

            GeocodeResponse data = null;
            try { data = JsonUtility.FromJson<GeocodeResponse>(req.downloadHandler.text); }
            catch (Exception e)
            {
                ObservationStatus.Report($"City lookup returned unreadable data ({e.Message})",
                                         ObservationStatus.Level.Warning);
            }

            if (data?.results == null || data.results.Length == 0)
            {
                ObservationStatus.Report($"City \"{cityName}\" not found",
                                         ObservationStatus.Level.Warning);
                UseManualCoordinates();
                yield break;
            }

            var hit = data.results[0];
            Apply(hit.latitude, hit.longitude,
                  string.IsNullOrEmpty(hit.country) ? hit.name : $"{hit.name}, {hit.country}");
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

            ObservationStatus.Report($"Site set to {placeName} ({lat:F3}, {lon:F3})",
                                     ObservationStatus.Level.Success);
        }

        private static string HostOf(string url)
        {
            try { return new Uri(url).Host; }
            catch { return url; }
        }

        // ─────────────────────────────────────────────────────────────────
        // プロバイダごとの応答解析
        // ─────────────────────────────────────────────────────────────────
        private class GeoResult
        {
            public double Latitude;
            public double Longitude;
            public string PlaceName;

            // 0,0 は取得失敗を示す値として扱う (ギニア湾沖の海上で観測地にはなり得ない)
            public bool IsValid => Latitude != 0.0 || Longitude != 0.0;
        }

        private static GeoResult ParseIpWhoIs(string json)
        {
            var d = JsonUtility.FromJson<IpWhoIsResponse>(json);
            if (d == null || !d.success) return null;
            return new GeoResult
            {
                Latitude  = d.latitude,
                Longitude = d.longitude,
                PlaceName = Join(d.city, d.country),
            };
        }

        private static GeoResult ParseFreeIpApi(string json)
        {
            var d = JsonUtility.FromJson<FreeIpApiResponse>(json);
            if (d == null) return null;
            return new GeoResult
            {
                Latitude  = d.latitude,
                Longitude = d.longitude,
                PlaceName = Join(d.cityName, d.countryName),
            };
        }

        private static GeoResult ParseGeoJs(string json)
        {
            // geojs は緯度経度を文字列で返すため個別に変換する
            var d = JsonUtility.FromJson<GeoJsResponse>(json);
            if (d == null) return null;

            if (!double.TryParse(d.latitude,  NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(d.longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                return null;

            return new GeoResult
            {
                Latitude  = lat,
                Longitude = lon,
                PlaceName = Join(d.city, d.country),
            };
        }

        private static string Join(string city, string country)
        {
            if (string.IsNullOrEmpty(city))    return string.IsNullOrEmpty(country) ? "Unknown" : country;
            if (string.IsNullOrEmpty(country)) return city;
            return $"{city}, {country}";
        }

        // ── JSON DTO ─────────────────────────────────────────────────────
        [Serializable] private class IpWhoIsResponse
        {
            public bool   success;
            public string city;
            public string country;
            public double latitude;
            public double longitude;
        }

        [Serializable] private class FreeIpApiResponse
        {
            public string cityName;
            public string countryName;
            public double latitude;
            public double longitude;
        }

        [Serializable] private class GeoJsResponse
        {
            public string city;
            public string country;
            public string latitude;
            public string longitude;
        }

        [Serializable] private class GeocodeResponse { public GeocodeHit[] results; }

        [Serializable] private class GeocodeHit
        {
            public string name;
            public string country;
            public double latitude;
            public double longitude;
        }
    }
}
