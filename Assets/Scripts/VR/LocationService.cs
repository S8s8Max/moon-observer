using System.Collections;
using UnityEngine;

namespace MoonObserver.VR
{
    /// <summary>
    /// デバイスの GPS から緯度・経度を取得し VRMoonViewer に反映する。
    /// Quest 3 (Android) では自動取得、PC では手動座標を維持する。
    /// </summary>
    public class LocationService : MonoBehaviour
    {
        [Header("参照")]
        public VRMoonViewer moonViewer;

        [Header("GPS設定")]
        [Tooltip("GPS取得タイムアウト秒数")]
        public float timeoutSec = 10f;
        [Tooltip("精度 (メートル)")]
        public float desiredAccuracyMeters = 500f;
        [Tooltip("更新距離 (メートル)")]
        public float updateDistanceMeters  = 1000f;

        private void Start()
        {
            StartCoroutine(RequestLocation());
        }

        private IEnumerator RequestLocation()
        {
#if UNITY_ANDROID || UNITY_IOS
            // モバイル: デバイスの位置情報を使用
            if (!Input.location.isEnabledByUser)
            {
                Debug.Log("[LocationService] 位置情報が無効です。手動座標を使用します。");
                yield break;
            }

            Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);
            Input.compass.enabled = true;

            float elapsed = 0f;
            while (Input.location.status == LocationServiceStatus.Initializing && elapsed < timeoutSec)
            {
                yield return new WaitForSeconds(1f);
                elapsed += 1f;
            }

            if (Input.location.status == LocationServiceStatus.Running)
            {
                var data = Input.location.lastData;
                if (moonViewer != null)
                {
                    moonViewer.latitudeDeg  = data.latitude;
                    moonViewer.longitudeDeg = data.longitude;
                    moonViewer.useRealTime  = true;
                    Debug.Log($"[LocationService] GPS取得: 緯度={data.latitude:F4}°, 経度={data.longitude:F4}°");
                }
            }
            else
            {
                Debug.Log($"[LocationService] GPS失敗 (status={Input.location.status})。手動座標を使用。");
            }
#else
            // PC/Editor: 手動座標をそのまま使用
            Debug.Log("[LocationService] PC環境: Inspector の緯度・経度を使用します。");
            yield break;
#endif
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID || UNITY_IOS
            Input.location.Stop();
#endif
        }
    }
}
