using System;
using System.Globalization;

namespace DeepDrill.Core
{
    public static class NumberFormat
    {
        private static readonly string[] Suffixes = { "K", "M", "B", "T" };

        /// <summary>Three significant figures, for tight spaces: 6.27B, 36.0M, 105B, 1.2e16.</summary>
        public static string Compact(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            if (value < 0) return "-" + Compact(-value);
            if (value < 1000) return (Math.Floor(value * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture);
            if (value >= 1e15) return value.ToString("0.0e0", CultureInfo.InvariantCulture);
            int tier = (int)Math.Floor(Math.Log10(value) / 3);
            double scaled = value / Math.Pow(1000, tier);
            string digits = scaled >= 100 ? "0" : scaled >= 10 ? "0.0" : "0.00";
            string text = scaled.ToString(digits, CultureInfo.InvariantCulture);
            if (text == "1000" && tier < Suffixes.Length) return "1.00" + Suffixes[tier];
            return text + Suffixes[tier - 1];
        }

        /// <summary>Below 1000 as is, K/M/B/T up to 1e15, scientific notation after that.</summary>
        public static string Short(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            if (value < 0) return "-" + Short(-value);
            // keeps the early game's fractions visible: a dirt block pays 0.5, not 0
            if (value < 1000) return (Math.Floor(value * 10) / 10).ToString("0.#", CultureInfo.InvariantCulture);
            if (value >= 1e15)
                return value.ToString("0.00e0", CultureInfo.InvariantCulture);

            int tier = (int)Math.Floor(Math.Log10(value) / 3);
            double scaled = value / Math.Pow(1000, tier);
            // 999.999K must not print as 1000.00K
            if (Math.Round(scaled, 2) >= 1000 && tier < Suffixes.Length)
            {
                tier++;
                scaled /= 1000;
            }
            if (tier > Suffixes.Length) return value.ToString("0.00e0", CultureInfo.InvariantCulture);
            return scaled.ToString("0.00", CultureInfo.InvariantCulture) + Suffixes[tier - 1];
        }
    }
}
