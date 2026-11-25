using System;
using System.Globalization;

namespace SignalLost
{
    /// <summary>Big-number display: whole numbers below 1,000, then 3 significant digits + suffix, then scientific.</summary>
    public static class NumberFormat
    {
        private static readonly string[] Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>When true every value >= 1,000 uses scientific notation (settings toggle).</summary>
        public static bool Scientific;

        public static string Format(double v)
        {
            if (double.IsNaN(v)) return "0";
            if (double.IsInfinity(v)) return "INF";
            bool negative = v < 0;
            v = Math.Abs(v);
            string s;
            if (v < 1000d)
            {
                s = Math.Floor(v).ToString("0", Inv);
            }
            else if (Scientific)
            {
                s = Sci(v);
            }
            else
            {
                int tier = (int)Math.Floor(Math.Log10(v) / 3d);
                if (tier >= Suffixes.Length)
                {
                    s = Sci(v);
                }
                else
                {
                    double scaled = v / Math.Pow(1000d, tier);
                    if (scaled >= 999.5d && tier + 1 < Suffixes.Length)
                    {
                        tier++;
                        scaled /= 1000d;
                    }
                    int decimals = scaled >= 100d ? 0 : scaled >= 10d ? 1 : 2;
                    double p = Math.Pow(10d, decimals);
                    double truncated = Math.Floor(scaled * p + 1e-9) / p;
                    string fmt = decimals == 0 ? "0" : "0." + new string('#', decimals);
                    s = truncated.ToString(fmt, Inv) + Suffixes[tier];
                }
            }
            return negative ? "-" + s : s;
        }

        /// <summary>Shows a fractional value for small numbers (Data/sec, multipliers).</summary>
        public static string FormatPrecise(double v)
        {
            if (Math.Abs(v) < 10d) return v.ToString("0.#", Inv);
            return Format(v);
        }

        public static string Sci(double v)
        {
            if (v <= 0) return "0";
            int exp = (int)Math.Floor(Math.Log10(v));
            double mant = v / Math.Pow(10d, exp);
            mant = Math.Floor(mant * 100d) / 100d;
            return mant.ToString("0.00", Inv) + "e" + exp.ToString(Inv);
        }

        public static string Mult(double m)
        {
            return "x" + (m < 10 ? m.ToString("0.##", Inv) : Format(m));
        }

        public static string Percent(double fraction, int decimals = 0)
        {
            return (fraction * 100d).ToString(decimals == 0 ? "0" : "0." + new string('#', decimals), Inv) + "%";
        }

        public static string Time(double seconds)
        {
            if (seconds < 0) seconds = 0;
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            if (t.TotalHours >= 1) return string.Format(Inv, "{0}h {1:00}m", (int)t.TotalHours, t.Minutes);
            if (t.TotalMinutes >= 1) return string.Format(Inv, "{0}m {1:00}s", t.Minutes, t.Seconds);
            return string.Format(Inv, "{0}s", Math.Ceiling(t.TotalSeconds));
        }
    }
}
