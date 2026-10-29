using System.Globalization;

namespace NumberRoad.Core
{
    /// <summary>Number formatting for big game numbers: 9,999 then 12.3K, 4.5M, 1.2B.</summary>
    public static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Num(long n)
        {
            if (n < 0) return "-" + Num(-n);
            if (n < 10000) return n.ToString(Inv);
            if (n < 1000000) return Short(n / 1000.0, "K");
            if (n < 1000000000) return Short(n / 1000000.0, "M");
            return Short(n / 1000000000.0, "B");
        }

        /// <summary>Like <see cref="Num"/> but keeps thousands separators below 1M (for coin totals).</summary>
        public static string Coins(long n) => n < 1000000 ? n.ToString("N0", Inv) : Num(n);

        public static string Signed(long n) => n >= 0 ? "+" + Num(n) : Num(n);

        static string Short(double v, string suffix)
        {
            // truncate rather than round so 99,999 never shows as "100K"
            double t = v < 100 ? System.Math.Floor(v * 10) / 10 : System.Math.Floor(v);
            return (v < 100 ? t.ToString("0.#", Inv) : t.ToString("0", Inv)) + suffix;
        }

        public static string Duration(double seconds)
        {
            long s = (long)System.Math.Max(0, seconds);
            if (s < 60) return s + "s";
            if (s < 3600) return (s / 60) + "m " + (s % 60).ToString("00", Inv) + "s";
            return (s / 3600) + "h " + (s / 60 % 60).ToString("00", Inv) + "m";
        }
    }
}
