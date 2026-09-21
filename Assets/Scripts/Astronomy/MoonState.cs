using System;

namespace MoonObserver.Astronomy
{
    /// <summary>
    /// 月の天文状態を保持するデータ構造体
    /// </summary>
    [Serializable]
    public struct MoonState
    {
        // 地平座標
        public double AltitudeDeg;       // 高度 (度)
        public double AzimuthDeg;        // 方位角 (度、北=0、東=90)

        // 位相
        public double PhaseAngleDeg;     // 位相角 (度)
        public double IlluminationFraction; // 輝面比 (0〜1)
        public double MoonAge;           // 月齢 (0〜29.53日)

        // 秤動
        public double LibrationLongDeg;  // 経度秤動 (度)
        public double LibrationLatDeg;   // 緯度秤動 (度)

        // 距離・その他
        public double DistanceKm;        // 地心距離 (km)
        public double ParallacticAngleDeg; // 地平視差角 (度)

        // 赤道座標 (内部計算用)
        public double RightAscensionDeg; // 赤経 (度)
        public double DeclinationDeg;    // 赤緯 (度)

        // ユリウス日
        public double JDE;

        public override string ToString()
        {
            return $"Alt={AltitudeDeg:F2}° Az={AzimuthDeg:F2}° Phase={PhaseAngleDeg:F2}° " +
                   $"Illum={IlluminationFraction * 100:F1}% Age={MoonAge:F1}d Dist={DistanceKm:F0}km";
        }
    }
}
