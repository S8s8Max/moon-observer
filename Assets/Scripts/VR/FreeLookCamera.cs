using UnityEngine;
using UnityEngine.InputSystem;

namespace MoonObserver.VR
{
    /// <summary>
    /// PC テスト用マウス視点コントローラー (New Input System 対応)
    /// 右クリック長押し + マウス移動で視点回転
    /// </summary>
    public class FreeLookCamera : MonoBehaviour
    {
        [Range(0.01f, 0.5f)]
        public float sensitivity = 0.1f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            Vector3 e = transform.eulerAngles;
            _yaw   = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw   += delta.x * sensitivity;
                _pitch -= delta.y * sensitivity;
                _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
                transform.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
        }
    }
}
