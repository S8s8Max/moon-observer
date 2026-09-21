using System;
using UnityEngine;
using MoonObserver.Astronomy;

#if UNITY_EDITOR
using NUnit.Framework;

namespace MoonObserver.Tests
{
    /// <summary>
    /// 天文暦エンジンの回帰テスト
    /// Unity Test Runner (Edit Mode) で実行
    /// </summary>
    [TestFixture]
    public class AstronomyEngineTests
    {
        // テスト観測地点: 東京
        private const double TOKYO_LAT = 35.6895;
        private const double TOKYO_LON = 139.6917;
        private const double TOLERANCE_DEG = 0.2; // ±0.2° 許容

        [Test]
        public void T01_MoonPosition_Tokyo_2024Jan01()
        {
            // 2024-01-01 00:00 UTC、東京
            var utc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);

            Debug.Log($"[T01] Moon state: {state}");

            // Stellarium 参照値 (2024-01-01 00:00 UTC 東京)
            // Alt ≈ 51.8°、Az ≈ 194.2° (Stellarium実測値に合わせて調整)
            double expectedAlt = 51.8;
            double expectedAz  = 194.2;

            Assert.That(state.AltitudeDeg, Is.InRange(expectedAlt - TOLERANCE_DEG, expectedAlt + TOLERANCE_DEG),
                $"高度誤差が許容範囲を超えています。期待:{expectedAlt}°、実際:{state.AltitudeDeg:F2}°");
            Assert.That(state.AzimuthDeg, Is.InRange(expectedAz - TOLERANCE_DEG, expectedAz + TOLERANCE_DEG),
                $"方位角誤差が許容範囲を超えています。期待:{expectedAz}°、実際:{state.AzimuthDeg:F2}°");
        }

        [Test]
        public void T02_FullMoon_Illumination()
        {
            // 2024-02-24 00:00 UTC (満月付近)
            var utc = new DateTime(2024, 2, 24, 0, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Debug.Log($"[T02-Full] Illumination={state.IlluminationFraction * 100:F1}%");
            Assert.That(state.IlluminationFraction, Is.GreaterThan(0.95), "満月の輝面比が95%未満です");
        }

        [Test]
        public void T02_NewMoon_Illumination()
        {
            // 2024-03-10 09:00 UTC (新月付近)
            var utc = new DateTime(2024, 3, 10, 9, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Debug.Log($"[T02-New] Illumination={state.IlluminationFraction * 100:F1}%");
            Assert.That(state.IlluminationFraction, Is.LessThan(0.05), "新月の輝面比が5%超です");
        }

        [Test]
        public void T02_FirstQuarter_Illumination()
        {
            // 2024-03-17 04:11 UTC (上弦)
            var utc = new DateTime(2024, 3, 17, 4, 11, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Debug.Log($"[T02-FQ] Illumination={state.IlluminationFraction * 100:F1}%");
            Assert.That(state.IlluminationFraction, Is.InRange(0.49, 0.51), "上弦の輝面比が50%±1%外です");
        }

        [Test]
        public void T02_LastQuarter_Illumination()
        {
            // 2024-03-25 07:00 UTC (下弦)
            var utc = new DateTime(2024, 3, 25, 7, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Debug.Log($"[T02-LQ] Illumination={state.IlluminationFraction * 100:F1}%");
            Assert.That(state.IlluminationFraction, Is.InRange(0.49, 0.51), "下弦の輝面比が50%±1%外です");
        }

        [Test]
        public void T01_DistanceRange_Valid()
        {
            var utc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Assert.That(state.DistanceKm, Is.InRange(356400, 406700),
                $"月の距離が物理的範囲外です: {state.DistanceKm:F0}km");
        }

        [Test]
        public void T01_AzimuthRange_Valid()
        {
            var utc = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Assert.That(state.AzimuthDeg, Is.InRange(0, 360),
                $"方位角が0〜360°外です: {state.AzimuthDeg:F2}°");
        }
    }
}
#endif
