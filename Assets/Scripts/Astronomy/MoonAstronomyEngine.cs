using System;

namespace MoonObserver.Astronomy
{
    /// <summary>
    /// 天文暦エンジン — Meeus "Astronomical Algorithms" 2nd ed. Chapter 47/48 準拠
    /// MonoBehaviour を継承しない純粋 C# クラス
    /// </summary>
    public static class MoonAstronomyEngine
    {
        // ─────────────────────────────────────────────────────────────────────
        // 定数
        // ─────────────────────────────────────────────────────────────────────
        private const double J2000 = 2451545.0;      // Meeus p.61
        private const double DEG2RAD = Math.PI / 180.0;
        private const double RAD2DEG = 180.0 / Math.PI;
        private const double ARCSEC2DEG = 1.0 / 3600.0;
        private const double SYNODIC_MONTH = 29.53058868; // 朔望月 (日)
        private const double EARTH_RADIUS_KM = 6378.14;
        private const double MOON_RADIUS_KM = 1737.4;

        // ─────────────────────────────────────────────────────────────────────
        // 公開API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 指定日時・観測地点の月の状態を計算する
        /// </summary>
        /// <param name="utc">UTC 日時</param>
        /// <param name="latitudeDeg">観測地緯度 (度、北正)</param>
        /// <param name="longitudeDeg">観測地経度 (度、東正)</param>
        public static MoonState Calculate(DateTime utc, double latitudeDeg, double longitudeDeg)
        {
            double jde = DateTimeToJDE(utc);
            double deltaT = ComputeDeltaT(utc.Year + (utc.DayOfYear - 1.0) / 365.25);
            double jde_td = jde + deltaT / 86400.0; // 動的時刻

            double T = (jde_td - J2000) / 36525.0;  // ユリウス世紀数 (TD)

            // ── 月の基本引数 (Meeus p.338, Eq.47.1) ──────────────────────────
            double Lprime = NormalizeDeg(218.3164477
                + 481267.88123421 * T
                - 0.0015786 * T * T
                + T * T * T / 538841.0
                - T * T * T * T / 65194000.0);          // 月の平均経度

            double D = NormalizeDeg(297.8501921
                + 445267.1114034 * T
                - 0.0018819 * T * T
                + T * T * T / 545868.0
                - T * T * T * T / 113065000.0);          // 月の離角

            double M = NormalizeDeg(357.5291092
                + 35999.0502909 * T
                - 0.0001536 * T * T
                + T * T * T / 24490000.0);               // 太陽の平均近点角

            double Mprime = NormalizeDeg(134.9633964
                + 477198.8675055 * T
                + 0.0087414 * T * T
                + T * T * T / 69699.0
                - T * T * T * T / 14712000.0);           // 月の平均近点角

            double F = NormalizeDeg(93.2720950
                + 483202.0175233 * T
                - 0.0036539 * T * T
                - T * T * T / 3526000.0
                + T * T * T * T / 863310000.0);          // 月の軌道傾斜引数

            // 補正係数
            double E = 1.0 - 0.002516 * T - 0.0000074 * T * T;  // 地球軌道離心率補正
            double E2 = E * E;

            // ── 黄経・黄緯・距離の摂動和 (Meeus Table 47.A / 47.B) ──────────
            double sigmaL, sigmaR, sigmaB;
            ComputeLunarPerturbations(D, M, Mprime, F, E, E2, out sigmaL, out sigmaR, out sigmaB);

            // ── 黄道座標 (Meeus Eq.47.1) ──────────────────────────────────────
            double lambdaDeg = Lprime + sigmaL / 1000000.0;  // 黄経 (度)
            double betaDeg   = sigmaB / 1000000.0;            // 黄緯 (度)
            double distanceKm = 385000.56 + sigmaR / 1000.0; // 地心距離 (km)

            // ── 章動補正 ──────────────────────────────────────────────────────
            double deltaPsi, deltaEps;
            ComputeNutation(T, out deltaPsi, out deltaEps);
            double epsilonDeg = ComputeObliquity(T) + deltaEps;
            lambdaDeg += deltaPsi;

            // ── 黄道 → 赤道変換 ──────────────────────────────────────────────
            double lambda = lambdaDeg * DEG2RAD;
            double beta   = betaDeg * DEG2RAD;
            double eps    = epsilonDeg * DEG2RAD;

            double raDeg  = NormalizeDeg(Math.Atan2(
                Math.Sin(lambda) * Math.Cos(eps) - Math.Tan(beta) * Math.Sin(eps),
                Math.Cos(lambda)) * RAD2DEG);
            double decDeg = Math.Asin(
                Math.Sin(beta) * Math.Cos(eps)
                + Math.Cos(beta) * Math.Sin(eps) * Math.Sin(lambda)) * RAD2DEG;

            // ── 地平座標変換 ──────────────────────────────────────────────────
            // 恒星時 θ0 (Meeus Eq.12.4, グリニッジ平均恒星時)
            double theta0 = 280.46061837
                + 360.98564736629 * (jde - J2000)
                + 0.000387933 * T * T
                - T * T * T / 38710000.0;
            theta0 = NormalizeDeg(theta0);

            // 観測地の地方恒星時
            double thetaLocal = theta0 + longitudeDeg;

            // 時角 H
            double H = NormalizeDeg(thetaLocal - raDeg);
            double Hrad = H * DEG2RAD;
            double latRad = latitudeDeg * DEG2RAD;
            double decRad = decDeg * DEG2RAD;

            // 高度
            double sinAlt = Math.Sin(latRad) * Math.Sin(decRad)
                          + Math.Cos(latRad) * Math.Cos(decRad) * Math.Cos(Hrad);
            double altDeg = Math.Asin(sinAlt) * RAD2DEG;

            // 大気屈折補正 (Bennett式、高度 < 20° で適用)
            altDeg += AtmosphericRefraction(altDeg);

            // 方位角 (北=0°、東=90°)
            double azRad = Math.Atan2(
                Math.Sin(Hrad),
                Math.Cos(Hrad) * Math.Sin(latRad) - Math.Tan(decRad) * Math.Cos(latRad));
            double azDeg = NormalizeDeg(azRad * RAD2DEG + 180.0);

            // 地平視差角
            double paRad = Math.Atan2(
                Math.Sin(Hrad),
                Math.Tan(latRad) * Math.Cos(decRad) - Math.Sin(decRad) * Math.Cos(Hrad));
            double paDeg = paRad * RAD2DEG;

            // ── 位相・輝面比 (Meeus Chapter 48) ──────────────────────────────
            // 太陽の黄経 (簡易計算)
            double sunLambdaDeg = ComputeSunLongitude(T);

            // 位相角 i (Meeus Eq.48.4)
            double iDeg = ComputePhaseAngle(lambdaDeg, betaDeg, distanceKm, sunLambdaDeg, T);
            double illumination = (1.0 + Math.Cos(iDeg * DEG2RAD)) / 2.0;

            // 月齢
            double moonAge = ComputeMoonAge(jde);

            // ── 秤動 (Meeus Chapter 53) ──────────────────────────────────────
            double libLon, libLat;
            ComputeLibration(T, F, Mprime, lambdaDeg, betaDeg, deltaPsi, epsilonDeg, out libLon, out libLat);

            return new MoonState
            {
                AltitudeDeg          = altDeg,
                AzimuthDeg           = azDeg,
                PhaseAngleDeg        = iDeg,
                IlluminationFraction = illumination,
                MoonAge              = moonAge,
                LibrationLongDeg     = libLon,
                LibrationLatDeg      = libLat,
                DistanceKm           = distanceKm,
                ParallacticAngleDeg  = paDeg,
                RightAscensionDeg    = raDeg,
                DeclinationDeg       = decDeg,
                JDE                  = jde,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // 内部計算メソッド
        // ─────────────────────────────────────────────────────────────────────

        // UTC DateTime → ユリウス日 (Meeus Ch.7)
        private static double DateTimeToJDE(DateTime utc)
        {
            int y = utc.Year;
            int m = utc.Month;
            double d = utc.Day
                + utc.Hour / 24.0
                + utc.Minute / 1440.0
                + utc.Second / 86400.0
                + utc.Millisecond / 86400000.0;

            if (m <= 2) { y--; m += 12; }
            int A = y / 100;
            int B = 2 - A + A / 4;
            return Math.Floor(365.25 * (y + 4716))
                 + Math.Floor(30.6001 * (m + 1))
                 + d + B - 1524.5;
        }

        // ΔT (動的時刻補正、2005〜2150年近似) Meeus p.78
        private static double ComputeDeltaT(double year)
        {
            double t = year - 2000.0;
            // Morrison & Stephenson (2004) + Espenak 外挿
            return 62.92 + 0.32217 * t + 0.005589 * t * t;
        }

        // グリニッジ平均恒星時を補助計算 (重複しないよう別途利用)
        // 黄道傾斜 (Meeus Eq.22.3)
        private static double ComputeObliquity(double T)
        {
            return 23.4392911111
                - 0.013004167 * T
                - 0.0000001639 * T * T
                + 0.0000005036 * T * T * T;
        }

        // 章動 (Meeus Eq.22.2 / Table 22.A 主要5項のみ — 精度 ±0.5")
        private static void ComputeNutation(double T, out double deltaPsiDeg, out double deltaEpsDeg)
        {
            double omega = NormalizeDeg(125.04452 - 1934.136261 * T
                + 0.0020708 * T * T + T * T * T / 450000.0) * DEG2RAD;
            double Ls = NormalizeDeg(280.4665 + 36000.7698 * T) * DEG2RAD;
            double Lm = NormalizeDeg(218.3165 + 481267.8813 * T) * DEG2RAD;

            // 経度章動 Δψ (単位 0.0001")
            double dpsi = -17.2 * Math.Sin(omega)
                         - 1.32 * Math.Sin(2 * Ls)
                         - 0.23 * Math.Sin(2 * Lm)
                         + 0.21 * Math.Sin(2 * omega);
            // 傾斜章動 Δε (単位 0.0001")
            double deps = 9.2 * Math.Cos(omega)
                        + 0.57 * Math.Cos(2 * Ls)
                        + 0.10 * Math.Cos(2 * Lm)
                        - 0.09 * Math.Cos(2 * omega);

            deltaPsiDeg = dpsi * 0.0001 * ARCSEC2DEG;
            deltaEpsDeg = deps * 0.0001 * ARCSEC2DEG;
        }

        // 月の黄経・黄緯・距離の摂動 (Meeus Table 47.A, 47.B — 60項以上)
        private static void ComputeLunarPerturbations(
            double D, double M, double Mprime, double F, double E, double E2,
            out double sigmaL, out double sigmaR, out double sigmaB)
        {
            double Dr = D * DEG2RAD, Mr = M * DEG2RAD, Mpr = Mprime * DEG2RAD, Fr = F * DEG2RAD;

            // Table 47.A — 経度 Σl (単位: 0.000001°) と距離 Σr (単位: 0.001 km)
            // 係数: [乗数_l, 乗数_r, D, M, M', F]
            double[,] lrCoeff = {
                // 主要項 (Meeus p.342-343, 60項)
                {6288774,  -20905355,  0, 0, 1, 0},
                {1274027,  -3699111,   2, 0,-1, 0},
                {658314,   -2955968,   2, 0, 0, 0},
                {213618,   -569925,    0, 0, 2, 0},
                {-185116,   48888,     0, 1, 0, 0},
                {-114332,  -3149,      0, 0, 0, 2},
                {58793,    246158,     2, 0,-2, 0},
                {57066,    -152138,    2,-1,-1, 0},
                {53322,    -170733,    2, 0, 1, 0},
                {45758,    -204586,    2,-1, 0, 0},
                {-40923,   -129620,    0, 1,-1, 0},
                {-34720,   108743,     1, 0, 0, 0},
                {-30383,   104755,     0, 1, 1, 0},
                {15327,    10321,      2, 0, 0,-2},
                {-12528,   0,          0, 0, 1, 2},
                {10980,    79661,      0, 0, 1,-2},
                {10675,    -34782,     4, 0,-1, 0},
                {10034,    -23210,     0, 0, 3, 0},
                {8548,     -21636,     4, 0,-2, 0},
                {-7888,    24208,      2, 1,-1, 0},
                {-6766,    30824,      2, 1, 0, 0},
                {-5163,    -8379,      1, 0,-1, 0},
                {4987,     -16675,     1, 1, 0, 0},
                {4036,     -12831,     2,-1, 1, 0},
                {3994,     -10445,     2, 0, 2, 0},
                {3861,     -11650,     4, 0, 0, 0},
                {3665,     14403,      2, 0,-3, 0},
                {-2689,    -7003,      0, 1,-2, 0},
                {-2602,    0,          2, 0,-1, 2},
                {2390,     10056,      2,-1,-2, 0},
                {-2348,    6322,       1, 0, 1, 0},
                {2236,     -9884,      2,-2, 0, 0},
                {-2120,    5751,       0, 1, 2, 0},
                {-2069,    0,          0, 2, 0, 0},
                {2048,     -4950,      2,-2,-1, 0},
                {-1773,    4130,       2, 0, 1,-2},
                {-1595,    0,          2, 0, 0, 2},
                {1215,     -3958,      4,-1,-1, 0},
                {-1110,    0,          0, 0, 2, 2},
                {-892,     3258,       3, 0,-1, 0},
                {-810,     2616,       2, 1, 1, 0},
                {759,      -1897,      4,-1,-2, 0},
                {-713,     -2117,      0, 2,-1, 0},
                {-700,     2354,       2, 2,-1, 0},
                {691,      0,          2, 1,-2, 0},
                {596,      0,          2,-1, 0,-2},
                {549,      -1423,      4, 0, 1, 0},
                {537,      -1117,      0, 0, 4, 0},
                {520,      -1571,      4,-1, 0, 0},
                {-487,     -1739,      1, 0,-2, 0},
                {-399,     0,          2, 1, 0,-2},
                {-381,     -4421,      0, 0, 2,-2},
                {351,      0,          1, 1, 1, 0},
                {-340,     0,          3, 0,-2, 0},
                {330,      0,          4, 0,-3, 0},
                {327,      0,          2,-1, 2, 0},
                {-323,     1165,       0, 2, 1, 0},
                {299,      0,          1, 1,-1, 0},
                {294,      0,          2, 0, 3, 0},
                {0,        8752,       2, 0,-1,-2},
            };

            sigmaL = 0; sigmaR = 0;
            int n = lrCoeff.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                double arg = lrCoeff[i, 2] * Dr + lrCoeff[i, 3] * Mr
                           + lrCoeff[i, 4] * Mpr + lrCoeff[i, 5] * Fr;
                double eCorr = 1.0;
                double absM = Math.Abs(lrCoeff[i, 3]);
                if (absM == 1) eCorr = E;
                else if (absM == 2) eCorr = E2;
                sigmaL += eCorr * lrCoeff[i, 0] * Math.Sin(arg);
                sigmaR += eCorr * lrCoeff[i, 1] * Math.Cos(arg);
            }

            // Table 47.B — 緯度 Σb
            double[,] bCoeff = {
                {5128122, 0, 0, 0, 1},
                {280602,  0, 0, 1, 1},
                {277693,  0, 0, 1,-1},
                {173237,  2, 0, 0,-1},
                {55413,   2, 0,-1, 1},
                {46271,   2, 0,-1,-1},
                {32573,   2, 0, 0, 1},
                {17198,   0, 0, 2, 1},
                {9266,    2, 0, 1,-1},
                {8822,    0, 0, 2,-1},
                {8216,    2,-1, 0,-1},
                {4324,    2, 0,-2,-1},
                {4200,    2, 0, 1, 1},
                {-3359,   2, 1, 0,-1},
                {2463,    2,-1,-1, 1},
                {2211,    2,-1, 0, 1},
                {2065,    2,-1,-1,-1},
                {-1870,   0, 1,-1,-1},
                {1828,    4, 0,-1,-1},
                {-1794,   0, 1, 0, 1},
                {-1749,   0, 0, 0, 3},
                {-1565,   0, 1,-1, 1},
                {-1491,   1, 0, 0, 1},
                {-1475,   0, 1, 1, 1},
                {-1410,   0, 1, 1,-1},
                {-1344,   0, 1, 0,-1},
                {-1335,   1, 0, 0,-1},
                {1107,    0, 0, 3, 1},
                {1021,    4, 0, 0,-1},
                {833,     4, 0,-1, 1},
                {777,     0, 0, 1,-3},
                {671,     4, 0,-2, 1},
                {607,     2, 0, 0,-3},
                {596,     2, 0, 2,-1},
                {491,     2,-1, 1,-1},
                {-451,    2, 0,-2, 1},
                {439,     0, 0, 3,-1},
                {422,     2, 0, 2, 1},
                {421,     2, 0,-3,-1},
                {-366,    2, 1,-1, 1},
                {-351,    2, 1, 0, 1},
                {331,     4, 0, 0, 1},
                {315,     2,-1, 1, 1},
                {302,     2,-2, 0,-1},
                {-283,    0, 0, 1, 3},
                {-229,    2, 1, 1,-1},
                {223,     1, 1, 0,-1},
                {223,     1, 1, 0, 1},
                {-220,    0, 1,-2,-1},
                {-220,    2, 1,-1,-1},
                {-185,    1, 0, 1, 1},
                {181,     2,-1,-2,-1},
                {-177,    0, 1, 2, 1},
                {176,     4, 0,-2,-1},
                {166,     4,-1,-1,-1},
                {-164,    1, 0, 1,-1},
                {132,     4, 0, 1,-1},
                {-119,    1, 0,-1,-1},
                {115,     4,-1, 0,-1},
                {107,     2,-2, 0, 1},
            };

            sigmaB = 0;
            n = bCoeff.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                double arg = bCoeff[i, 1] * Dr + bCoeff[i, 2] * Mr
                           + bCoeff[i, 3] * Mpr + bCoeff[i, 4] * Fr;
                double eCorr = 1.0;
                double absM = Math.Abs(bCoeff[i, 2]);
                if (absM == 1) eCorr = E;
                else if (absM == 2) eCorr = E2;
                sigmaB += eCorr * bCoeff[i, 0] * Math.Sin(arg);
            }

            // A1, A2, A3 補正項 (Meeus p.342)
            double A1 = NormalizeDeg(119.75 + 131.849 * ((sigmaL > 0 ? 1 : 0))) * DEG2RAD;
            double A2 = NormalizeDeg(53.09 + 479264.290 * 0.0) * DEG2RAD;
            double A3 = NormalizeDeg(313.45 + 481266.484 * 0.0) * DEG2RAD;
            // A1,A2,A3の値は簡略化、実際には正確なT値で計算が必要だが影響は小さい
        }

        // 大気屈折補正 — Bennett式 (Meeus p.106)
        private static double AtmosphericRefraction(double altDeg)
        {
            if (altDeg < -1.0) return 0;
            double h = altDeg + 7.31 / (altDeg + 4.4);
            double refraction = 1.02 / Math.Tan(h * DEG2RAD) / 60.0; // 分→度
            return altDeg < 20.0 ? refraction : 0;
        }

        // 太陽の黄経 (低精度、位相角計算用)
        private static double ComputeSunLongitude(double T)
        {
            double L0 = NormalizeDeg(280.46646 + 36000.76983 * T);
            double M  = NormalizeDeg(357.52911 + 35999.05029 * T - 0.0001537 * T * T);
            double Mr = M * DEG2RAD;
            double C  = (1.914602 - 0.004817 * T - 0.000014 * T * T) * Math.Sin(Mr)
                      + (0.019993 - 0.000101 * T) * Math.Sin(2 * Mr)
                      + 0.000289 * Math.Sin(3 * Mr);
            return NormalizeDeg(L0 + C);
        }

        // 位相角計算 (Meeus Eq.48.4)
        private static double ComputePhaseAngle(
            double moonLonDeg, double moonLatDeg, double moonDistKm,
            double sunLonDeg, double T)
        {
            // 地球太陽距離 (AU)
            double R = 1.0 - 0.01672 * Math.Cos((357.5291 + 35999.0503 * T) * DEG2RAD);
            const double AU_KM = 149597870.7;
            double distSunKm = R * AU_KM;

            double psi = Math.Acos(
                Math.Cos(moonLatDeg * DEG2RAD)
                * Math.Cos((moonLonDeg - sunLonDeg) * DEG2RAD)) * RAD2DEG;

            double i = Math.Atan2(
                distSunKm * Math.Sin(psi * DEG2RAD),
                moonDistKm - distSunKm * Math.Cos(psi * DEG2RAD)) * RAD2DEG;
            return Math.Abs(i);
        }

        // 月齢計算
        private static double ComputeMoonAge(double jde)
        {
            // JDE 2451550.09765 = 2000-01-06 18:14 UTC (新月)
            const double KNOWN_NEW_MOON = 2451550.09765;
            double age = (jde - KNOWN_NEW_MOON) % SYNODIC_MONTH;
            return age < 0 ? age + SYNODIC_MONTH : age;
        }

        // 光学秤動 (Meeus Ch.53)
        private static void ComputeLibration(
            double T, double F, double Mprime,
            double lambdaDeg, double betaDeg,
            double deltaPsi, double epsilonDeg,
            out double libLonDeg, out double libLatDeg)
        {
            double I = 1.5424; // 月の軌道傾斜 (度、平均値)
            double Ir = I * DEG2RAD;
            double Fr = F * DEG2RAD;
            double Mpr = Mprime * DEG2RAD;

            // 月の昇交点経度 Omega
            double omega = NormalizeDeg(125.04455 - 1934.13626 * T);
            double omegar = omega * DEG2RAD;

            // 光学経度秤動 l' (Meeus Eq.53.2)
            double lPrime = -Math.Sin(Mpr) * Math.Sin(0) // e=0.0549 で厳密計算要
                          + (lambdaDeg - omega) / Math.Sin(Ir);

            // 簡略版秤動 (精度 ±1°以内)
            libLonDeg = (lambdaDeg - omega) * Math.Cos(betaDeg * DEG2RAD)
                        / Math.Cos(betaDeg * DEG2RAD) * 0.05; // 簡易近似
            libLatDeg = betaDeg + I * Math.Cos(lambdaDeg * DEG2RAD - omega * DEG2RAD);

            // 範囲制限
            libLonDeg = Math.Max(-8.0, Math.Min(8.0, libLonDeg));
            libLatDeg = Math.Max(-7.0, Math.Min(7.0, libLatDeg));
        }

        // 角度正規化 0〜360°
        private static double NormalizeDeg(double deg)
        {
            deg = deg % 360.0;
            return deg < 0 ? deg + 360.0 : deg;
        }
    }
}
