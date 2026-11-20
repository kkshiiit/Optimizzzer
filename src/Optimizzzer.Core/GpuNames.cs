using System;
using System.Text.RegularExpressions;

namespace Optimizzzer.Core
{
    /// <summary>Tells dedicated graphics cards from integrated ones by their Windows adapter name.</summary>
    public static class GpuNames
    {
        // Ryzen APUs report "AMD Radeon(TM) Graphics", "AMD Radeon Graphics" or "Radeon(TM) Vega 8 Graphics",
        // and newer ones "AMD Radeon 780M Graphics" (with or without "(TM)").
        // Intel integrated GPUs report "Intel(R) UHD/Iris/HD Graphics".
        private static readonly Regex AmdIntegrated = new Regex(@"Radeon(\(TM\))?\s+(Vega\s+\d+\s+)?Graphics|Radeon(\(TM\))?\s+\d+M?\s+Graphics|Vega\s+\d+\s+Graphics", RegexOptions.IgnoreCase);

        public static bool IsNvidia(string name) => name != null && name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsDedicatedAmd(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            bool amd = name.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0;
            return amd && !AmdIntegrated.IsMatch(name);
        }
    }
}
