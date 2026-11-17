using System;

namespace Optimizzzer.Core
{
    /// <summary>WCAG 2.x contrast math, used to keep the Windows accent color readable in both themes.</summary>
    public static class ColorMath
    {
        public static double Luminance(byte r, byte g, byte b)
        {
            double C(byte c)
            {
                var s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * C(r) + 0.7152 * C(g) + 0.0722 * C(b);
        }

        public static double Contrast((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
        {
            var l1 = Luminance(a.R, a.G, a.B);
            var l2 = Luminance(b.R, b.G, b.B);
            return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
        }

        /// <summary>
        /// Darkens (light theme) or lightens (dark theme) the accent in small steps until it reaches
        /// <paramref name="target"/> against the given background. Keeps the hue the user picked.
        /// </summary>
        public static (byte R, byte G, byte B) EnsureContrast((byte R, byte G, byte B) accent, (byte R, byte G, byte B) against, double target, bool darken)
        {
            var c = accent;
            for (int i = 0; i < 40 && Contrast(c, against) < target; i++)
            {
                c = darken
                    ? ((byte)(c.R * 0.92), (byte)(c.G * 0.92), (byte)(c.B * 0.92))
                    : ((byte)(c.R + (255 - c.R) * 0.12), (byte)(c.G + (255 - c.G) * 0.12), (byte)(c.B + (255 - c.B) * 0.12));
            }
            return c;
        }
    }
}
