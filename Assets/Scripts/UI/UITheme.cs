using System;
using UnityEngine;

namespace MoonObserver.UI
{
    /// <summary>天体観測アプリ向けのダークテーマ配色とサイズの定義。</summary>
    public static class UITheme
    {
        // ── 面 ───────────────────────────────────────────────────────────
        public static readonly Color Surface       = new Color(0.043f, 0.063f, 0.125f, 0.82f);
        public static readonly Color SurfaceRaised = new Color(0.078f, 0.102f, 0.180f, 0.92f);
        public static readonly Color Border        = new Color(1f, 1f, 1f, 0.10f);
        public static readonly Color TrackColor    = new Color(1f, 1f, 1f, 0.12f);

        // ── 文字 ─────────────────────────────────────────────────────────
        public static readonly Color TextPrimary   = new Color(0.91f, 0.94f, 0.98f);
        public static readonly Color TextSecondary = new Color(0.55f, 0.60f, 0.70f);
        public static readonly Color TextMuted     = new Color(0.40f, 0.45f, 0.55f);

        // ── アクセント ───────────────────────────────────────────────────
        /// <summary>月・時刻まわりの暖色アクセント</summary>
        public static readonly Color AccentMoon = new Color(0.96f, 0.79f, 0.48f);
        /// <summary>方位・座標まわりの寒色アクセント</summary>
        public static readonly Color AccentCool = new Color(0.42f, 0.66f, 0.91f);
        public static readonly Color Success    = new Color(0.49f, 0.83f, 0.63f);
        public static readonly Color Warning    = new Color(0.91f, 0.66f, 0.29f);

        // ── 寸法 ─────────────────────────────────────────────────────────
        public const float CardPadding = 22f;
        public const float RowHeight   = 34f;
        public const float ScreenMargin = 26f;

        // ── 文字サイズ ───────────────────────────────────────────────────
        public const float FontDisplay = 46f;
        public const float FontValue   = 25f;
        public const float FontBody    = 21f;
        public const float FontLabel   = 17f;
    }

    /// <summary>
    /// 測位・天気取得などの状況を画面へ知らせるための小さな通知ハブ。
    /// 各サービスが Debug.Log の代わりにここへ流し、HUD が購読して表示する。
    /// </summary>
    public static class ObservationStatus
    {
        public enum Level { Info, Success, Warning }

        public static event Action<string, Level> OnMessage;

        public static void Report(string message, Level level = Level.Info)
        {
            if (level == Level.Warning) Debug.LogWarning(message);
            else                        Debug.Log(message);

            OnMessage?.Invoke(message, level);
        }
    }
}
