using System;

namespace MoonObserver.Astronomy
{
    /// <summary>
    /// 太陽の地平座標を計算する (低精度法・誤差 0.01° 程度)。
    ///
    /// 空の色は太陽高度で決まる (市民薄明 / 航海薄明 / 天文薄明) ため、
    /// 現実的な空を描くには月だけでなく太陽の位置が必要になる。
    /// 色付けが目的なので Meeus の完全版ではなく低精度法で十分。
    /// </summary>
    public static class SunPosition
    {
        public struct Result
        {
            public double AltitudeDeg;
            public double AzimuthDeg;   // 北=0, 東=90
        }

        public static Result Calculate(DateTime utc, double latitudeDeg, double longitudeDeg)
        {
            double jd = ToJulianDate(utc);
            double n  = jd - 2451545.0;

            // 平均黄経と平均近点角
            double L = NormalizeDeg(280.460 + 0.9856474 * n);
            double g = NormalizeDeg(357.528 + 0.9856003 * n) * DEG2RAD;

            // 中心差を加えた真黄経
            double lambda = (L + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2.0 * g)) * DEG2RAD;

            // 黄道傾斜角
            double eps = (23.439 - 0.0000004 * n) * DEG2RAD;

            // 赤道座標へ
            double ra  = Math.Atan2(Math.Cos(eps) * Math.Sin(lambda), Math.Cos(lambda));
            double dec = Math.Asin(Math.Sin(eps) * Math.Sin(lambda));

            // 時角
            double lst = LocalSiderealTimeDeg(jd, longitudeDeg) * DEG2RAD;
            double ha  = lst - ra;

            double lat = latitudeDeg * DEG2RAD;

            double sinAlt = Math.Sin(dec) * Math.Sin(lat)
                          + Math.Cos(dec) * Math.Cos(lat) * Math.Cos(ha);
            double alt = Math.Asin(Math.Clamp(sinAlt, -1.0, 1.0));

            // 南を 0 とする方位角を求めてから北基準へ直す
            double azSouth = Math.Atan2(
                Math.Sin(ha),
                Math.Cos(ha) * Math.Sin(lat) - Math.Tan(dec) * Math.Cos(lat));

            return new Result
            {
                AltitudeDeg = alt * RAD2DEG,
                AzimuthDeg  = NormalizeDeg(azSouth * RAD2DEG + 180.0),
            };
        }

        // ─────────────────────────────────────────────────────────────────
        private const double DEG2RAD = Math.PI / 180.0;
        private const double RAD2DEG = 180.0 / Math.PI;

        private static double LocalSiderealTimeDeg(double jd, double longitudeDeg)
        {
            double d = jd - 2451545.0;
            return NormalizeDeg(280.46061837 + 360.98564736629 * d + longitudeDeg);
        }

        public static double ToJulianDate(DateTime utc)
        {
            int    y = utc.Year, m = utc.Month;
            double day = utc.Day
                       + utc.Hour   / 24.0
                       + utc.Minute / 1440.0
                       + utc.Second / 86400.0;

            if (m <= 2) { y -= 1; m += 12; }

            int a = y / 100;
            int b = 2 - a + a / 4;

            return Math.Floor(365.25 * (y + 4716))
                 + Math.Floor(30.6001 * (m + 1))
                 + day + b - 1524.5;
        }

        public static double NormalizeDeg(double deg)
        {
            deg %= 360.0;
            return deg < 0 ? deg + 360.0 : deg;
        }
    }
}
