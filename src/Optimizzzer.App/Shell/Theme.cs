using System;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using Optimizzzer.Core;

namespace Optimizzzer.App.Shell
{
    public enum ThemeChoice { System, Light, Dark }

    /// <summary>Colour tokens from DESIGN.md. All brushes are frozen and swapped as resources.</summary>
    public static class Theme
    {
        public static ThemeChoice Choice { get; private set; } = ThemeChoice.System;
        public static bool IsDark { get; private set; }
        public static event Action Changed;

        private static readonly (string Key, string Light, string Dark)[] Tokens =
        {
            ("Bg", "#F7F7F8", "#1C1C1F"),
            ("Surface", "#FFFFFF", "#26262A"),
            ("SurfaceAlt", "#EFEFF1", "#303035"),
            ("Divider", "#E2E2E6", "#3A3A40"),
            ("ControlBorder", "#8A8A94", "#7C7C86"),
            ("Fg", "#1B1B1F", "#F2F2F4"),
            ("FgMuted", "#5A5A63", "#A8A8B0"),
            ("Moderate", "#8A5300", "#F2B24B"),
            ("Advanced", "#B3261E", "#FF8A80"),
            ("Success", "#1E7B34", "#6CCB7F"),
            ("Overlay", "#66000000", "#99000000"),
        };

        public static void LoadStyles(ResourceDictionary target)
        {
            using (var s = typeof(Theme).Assembly.GetManifestResourceStream("Optimizzzer.Styles.xaml"))
            {
                if (s == null) throw new InvalidOperationException("Styles.xaml resource missing");
                var dict = (ResourceDictionary)XamlReader.Load(s);
                target.MergedDictionaries.Add(dict);
            }
        }

        public static void Apply(ThemeChoice choice)
        {
            Choice = choice;
            IsDark = choice == ThemeChoice.Dark || (choice == ThemeChoice.System && SystemPrefersDark());
            var res = Application.Current.Resources;
            foreach (var (key, light, dark) in Tokens) res[key] = Brush(IsDark ? dark : light);

            // Accent: the user's Windows accent, nudged until it passes WCAG AA in this theme.
            var accent = SystemAccent() ?? (IsDark ? ((byte)0x4C, (byte)0xC2, (byte)0xFF) : ((byte)0x00, (byte)0x63, (byte)0xB1));
            var surface = IsDark ? ((byte)0x26, (byte)0x26, (byte)0x2A) : ((byte)0xFF, (byte)0xFF, (byte)0xFF);
            var fill = ColorMath.EnsureContrast(accent, surface, 3.0, darken: !IsDark);
            var onFill = ColorMath.Contrast((255, 255, 255), fill) >= ColorMath.Contrast((0, 0, 0), fill)
                ? ((byte)255, (byte)255, (byte)255) : ((byte)0, (byte)0, (byte)0);
            if (ColorMath.Contrast(onFill, fill) < 4.5) fill = ColorMath.EnsureContrast(fill, onFill, 4.5, darken: onFill.Item1 == 255);
            var text = ColorMath.EnsureContrast(accent, surface, 4.5, darken: !IsDark);
            res["Accent"] = Brush(fill);
            res["AccentFg"] = Brush(onFill);
            res["AccentText"] = Brush(text);
            Changed?.Invoke();
        }

        public static SolidColorBrush Brush(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        private static SolidColorBrush Brush((byte R, byte G, byte B) c)
        {
            var b = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        public static bool SystemPrefersDark()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch (Exception) { return false; }
        }

        private static (byte, byte, byte)? SystemAccent()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (k?.GetValue("AccentColor") is int abgr)
                        return ((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>Honours Windows "Show animations" (Settings > Accessibility > Visual effects).</summary>
        public static bool AnimationsEnabled => SystemParameters.ClientAreaAnimation;

        public static void WatchSystem()
        {
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color) return;
                Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(Choice)));
            };
        }
    }
}
