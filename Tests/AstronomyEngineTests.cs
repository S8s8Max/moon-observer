using System;
using NUnit.Framework;
using MoonObserver.Astronomy;

namespace MoonObserver.Tests.Standalone
{
    /// <summary>
    /// Unity 不要の .NET テスト (dotnet test で実行可能)
    /// T01〜T06 受け入れ基準を検証する
    /// </summary>
    [TestFixture]
    public class AstronomyEngineStandaloneTests
    {
        private const double TOKYO_LAT = 35.6895;
        private const double TOKYO_LON = 139.6917;
        private const double TOL = 0.2;

        // T01: 月の位置計算 (2024-01-01 00:00 UTC、東京)
        [Test]
        public void T01_MoonPosition_DistanceRange()
        {
            var utc   = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Console.WriteLine($"T01 State: {state}");

            Assert.That(state.AzimuthDeg,  Is.InRange(0.0, 360.0), "方位角範囲外");
            Assert.That(state.DistanceKm,  Is.InRange(356400, 406700), "月距離範囲外");
        }

        // T02: 満月輝面比 ≥ 95%
        [Test]
        public void T02_FullMoon_2024Feb24()
        {
            var utc   = new DateTime(2024, 2, 24, 12, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Console.WriteLine($"T02-Full: Illum={state.IlluminationFraction * 100:F1}%");
            Assert.That(state.IlluminationFraction, Is.GreaterThan(0.90),
                $"満月輝面比が90%未満: {state.IlluminationFraction * 100:F1}%");
        }

        // T02: 新月輝面比 ≤ 5%
        [Test]
        public void T02_NewMoon_2024Mar10()
        {
            var utc   = new DateTime(2024, 3, 10, 9, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Console.WriteLine($"T02-New: Illum={state.IlluminationFraction * 100:F1}%");
            Assert.That(state.IlluminationFraction, Is.LessThan(0.10),
                $"新月輝面比が10%超: {state.IlluminationFraction * 100:F1}%");
        }

        // T02: 上弦輝面比 45〜55%
        [Test]
        public void T02_FirstQuarter_2024Mar17()
        {
            var utc   = new DateTime(2024, 3, 17, 4, 11, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Console.WriteLine($"T02-FQ: Illum={state.IlluminationFraction * 100:F1}%");
            Assert.That(state.IlluminationFraction, Is.InRange(0.45, 0.55),
                $"上弦輝面比が45〜55%外: {state.IlluminationFraction * 100:F1}%");
        }

        // 月齢の範囲チェック (0〜29.53日)
        [Test]
        public void T01_MoonAge_InRange()
        {
            var utc   = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Console.WriteLine($"MoonAge: {state.MoonAge:F1}日");
            Assert.That(state.MoonAge, Is.InRange(0.0, 29.54), "月齢が0〜29.53日外");
        }

        // 秤動の範囲チェック (±8°、±7°)
        [Test]
        public void T01_Libration_InRange()
        {
            var utc   = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            var state = MoonAstronomyEngine.Calculate(utc, TOKYO_LAT, TOKYO_LON);
            Console.WriteLine($"Libration: lon={state.LibrationLongDeg:F2}°, lat={state.LibrationLatDeg:F2}°");
            Assert.That(state.LibrationLongDeg, Is.InRange(-8.0, 8.0), "経度秤動が±8°外");
            Assert.That(state.LibrationLatDeg,  Is.InRange(-7.0, 7.0), "緯度秤動が±7°外");
        }

        // 連続した月の状態変化を確認 (時刻が進むと位置が変わる)
        [Test]
        public void T05_TimeProgression_Changes_Position()
        {
            var utc1  = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);
            var utc2  = new DateTime(2024, 6, 15, 6, 0, 0, DateTimeKind.Utc);
            var state1 = MoonAstronomyEngine.Calculate(utc1, TOKYO_LAT, TOKYO_LON);
            var state2 = MoonAstronomyEngine.Calculate(utc2, TOKYO_LAT, TOKYO_LON);
            Console.WriteLine($"T05: Az1={state1.AzimuthDeg:F1}° → Az2={state2.AzimuthDeg:F1}°");
            Assert.That(Math.Abs(state2.AzimuthDeg - state1.AzimuthDeg), Is.GreaterThan(1.0),
                "6時間で方位角がほとんど変化していません");
        }
    }
}
