using UnityEngine;

namespace MoonObserver.UI
{
    /// <summary>
    /// 角丸・円形スプライトを手続き的に生成する。
    /// 外部アセットを持ち込まずにモダンな UI を組むために用意している。
    /// 生成したスプライトはキャッシュして使い回す。
    /// </summary>
    public static class UISpriteFactory
    {
        private static Sprite _roundedSmall;
        private static Sprite _roundedLarge;
        private static Sprite _circle;
        private static Sprite _pill;
        private static Sprite _triangle;

        /// <summary>上向きの三角形。方向インジケーターの矢印に使う。</summary>
        public static Sprite Triangle =>
            _triangle ??= CreateTriangle(64);

        /// <summary>カード用の角丸スプライト (9 スライス)。</summary>
        public static Sprite RoundedCard =>
            _roundedLarge ??= CreateRoundedRect(16);

        /// <summary>小さめの角丸スプライト (チップ・スライダー溝用)。</summary>
        public static Sprite RoundedSmall =>
            _roundedSmall ??= CreateRoundedRect(6);

        /// <summary>ボタン用のピル型スプライト。</summary>
        public static Sprite Pill =>
            _pill ??= CreateRoundedRect(19);

        /// <summary>スライダーのつまみ用の円。</summary>
        public static Sprite Circle =>
            _circle ??= CreateCircle(32);

        // ── 角丸矩形 ─────────────────────────────────────────────────────
        private static Sprite CreateRoundedRect(int radius)
        {
            int size = radius * 2 + 4;
            var tex  = NewTexture(size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 角の円の中心へクランプした点との距離で角丸を作る
                    float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                    float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                    float d  = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                    // +0.5 のマージンで 1px 分のアンチエイリアスを効かせる
                    float a = Mathf.Clamp01(radius + 0.5f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            // 9 スライスにして任意サイズへ引き伸ばせるようにする
            var border = new Vector4(radius + 1, radius + 1, radius + 1, radius + 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, border);
        }

        // ── 円 ───────────────────────────────────────────────────────────
        private static Sprite CreateCircle(int size)
        {
            var tex = NewTexture(size);
            float r = size * 0.5f;
            var center = new Vector2(r - 0.5f, r - 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float a = Mathf.Clamp01(r - 0.5f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ── 三角形 (上向き) ──────────────────────────────────────────────
        private static Sprite CreateTriangle(int size)
        {
            var tex = NewTexture(size);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;          // 0..1 (左→右)
                    float v = (y + 0.5f) / size;          // 0..1 (下→上)

                    // 高さ v における三角形の半幅。頂点 (v=1) で 0、底辺 (v=0) で 0.5
                    float halfWidth = (1f - v) * 0.5f;
                    float dist      = Mathf.Abs(u - 0.5f);

                    // size を掛けてピクセル単位の距離にし、1px のアンチエイリアスにする
                    float a = Mathf.Clamp01((halfWidth - dist) * size);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
                name       = $"UIGenerated_{size}",
            };
        }
    }
}
