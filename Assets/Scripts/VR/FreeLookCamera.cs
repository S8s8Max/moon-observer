using UnityEngine;

namespace MoonObserver.VR
{
    /// <summary>
    /// PC テスト用マウス視点コントローラー (XR ヘッドセット不要)
    /// 右クリック長押し + マウス移動で視点回転
    /// Quest 3 実機では XR トラッキングが自動的に優先される
    /// </summary>
    public class FreeLookCamera : MonoBehaviour
    {
        [Range(0.5f, 10f)]
        public float sensitivity = 2.5f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 e = transform.eulerAngles;
            _yaw   = e.y;
            _pitch = e.x;
        }

        private void Update()
        {
            // 右クリック押しっぱなし or タッチパッドで視点回転
            if (Input.GetMouseButton(1))
            {
                _yaw   += Input.GetAxis("Mouse X") * sensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * sensitivity;
                _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
                transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }
    }
}
