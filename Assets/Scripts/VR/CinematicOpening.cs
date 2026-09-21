using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MoonObserver.VR
{
    /// <summary>
    /// 起動時の神秘的な演出を管理する。
    ///
    /// フェーズ:
    ///   0–2s  完全な暗闇から徐々に星空が浮かび上がる (Bloom 高め)
    ///   2–5s  月がフェードインしながらズームアウト
    ///   5–8s  Bloom が落ち着き、通常の視野へ
    ///
    /// Post Processing Volume の Bloom・Vignette・Color Adjustments を
    /// ランタイムでアニメーションさせる。
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class CinematicOpening : MonoBehaviour
    {
        [Header("演出時間")]
        public float blackoutDuration    = 1.5f;  // 真っ暗な時間
        public float starRevealDuration  = 3.0f;  // 星が現れる時間
        public float moonRevealDuration  = 3.0f;  // 月フェードイン時間
        public float settleDuration      = 2.0f;  // Bloom が落ち着く時間

        [Header("Bloom 設定")]
        public float bloomPeakIntensity  = 3.5f;
        public float bloomIdleIntensity  = 0.7f;
        public float bloomThreshold      = 0.6f;

        [Header("Vignette 設定")]
        public float vignetteOpen        = 0.85f;
        public float vignetteClosed      = 0.0f;

        [Header("UI を演出中は非表示に")]
        public GameObject hudRoot;

        // ── 内部 ──────────────────────────────────────────────────────────
        private Volume       _volume;
        private Bloom        _bloom;
        private Vignette     _vignette;
        private ColorAdjustments _colorAdj;

        private bool _done;

        private void Awake()
        {
            _volume = GetComponent<Volume>();
            _volume.TryGet(out _bloom);
            _volume.TryGet(out _vignette);
            _volume.TryGet(out _colorAdj);

            // 開始時は真っ暗
            if (_vignette  != null) _vignette.intensity.Override(vignetteOpen);
            if (_colorAdj  != null) _colorAdj.postExposure.Override(-5f);
            if (_bloom     != null)
            {
                _bloom.intensity.Override(bloomPeakIntensity);
                _bloom.threshold.Override(bloomThreshold);
            }

            if (hudRoot != null) hudRoot.SetActive(false);
        }

        private void Start()
        {
            StartCoroutine(PlayOpening());
        }

        private IEnumerator PlayOpening()
        {
            // ── Phase 0: 暗闇 ─────────────────────────────────────────────
            yield return new WaitForSeconds(blackoutDuration);

            // ── Phase 1: 星空が浮かび上がる ──────────────────────────────
            float t = 0f;
            while (t < starRevealDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / starRevealDuration);

                if (_colorAdj != null)
                    _colorAdj.postExposure.Override(Mathf.Lerp(-5f, 0f, p));

                if (_vignette != null)
                    _vignette.intensity.Override(Mathf.Lerp(vignetteOpen, 0.45f, p));

                yield return null;
            }

            // ── Phase 2: 月フェードイン (Bloom ピーク → 落ち着く) ─────────
            t = 0f;
            while (t < moonRevealDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / moonRevealDuration);

                if (_bloom != null)
                    _bloom.intensity.Override(Mathf.Lerp(bloomPeakIntensity, bloomIdleIntensity * 1.8f, p));

                yield return null;
            }

            // ── Phase 3: Bloom が通常値へ ─────────────────────────────────
            t = 0f;
            while (t < settleDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.SmoothStep(0f, 1f, t / settleDuration);

                if (_bloom != null)
                    _bloom.intensity.Override(Mathf.Lerp(bloomIdleIntensity * 1.8f, bloomIdleIntensity, p));

                if (_vignette != null)
                    _vignette.intensity.Override(Mathf.Lerp(0.45f, vignetteClosed, p));

                yield return null;
            }

            // ── 演出完了 ──────────────────────────────────────────────────
            if (_bloom    != null) _bloom.intensity.Override(bloomIdleIntensity);
            if (_vignette != null) _vignette.intensity.Override(vignetteClosed);

            if (hudRoot != null) hudRoot.SetActive(true);
            _done = true;
        }
    }
}
