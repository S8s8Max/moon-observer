using UnityEngine;
using UnityEngine.InputSystem;

namespace MoonObserver.VR
{
    /// <summary>
    /// PC テスト用マウス視点コントローラー (New Input System 対応)
    /// ゲームビュー内をクリックするとカーソルがロックされ、マウス移動で視点回転。
    /// Escape キーでロック解除。
    /// </summary>
    public class FreeLookCamera : MonoBehaviour
    {
        [Range(0.05f, 1.0f)]
        public float sensitivity = 0.15f;

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
            var kb    = Keyboard.current;

            if (mouse == null) return;

            // クリックでカーソルロック → 視点操作モードへ
            if (mouse.leftButton.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.Locked;

            // Escape でロック解除
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.None;

            if (Cursor.lockState == CursorLockMode.Locked)
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
