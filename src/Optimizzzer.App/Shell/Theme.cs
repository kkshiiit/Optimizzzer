using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using Optimizzzer.Core;

namespace Optimizzzer.App.Shell
{
    public enum ThemeChoice { System, Light, Dark }
    public enum AccentChoice { System, Blue, Green, Violet, Orange }
    public enum LiteChoice { Auto, On, Off }

    /// <summary>
    /// Colour tokens, accent, elevation and motion (DESIGN.md). Brushes are frozen and swapped as resources.
    /// Lite mode removes every effect that costs GPU or CPU time, so modest PCs stay responsive.
    /// </summary>
    public static class Theme
    {
        public static ThemeChoice Choice { get; private set; } = ThemeChoice.Dark;
        public static AccentChoice Accent { get; private set; } = AccentChoice.System;
        public static LiteChoice LiteSetting { get; private set; } = LiteChoice.Auto;
        public static bool IsDark { get; private set; }
        /// <summary>True when effects are off: chosen by the user, or detected (software rendering, little RAM, remote session, animations off).</summary>
        public static bool Lite { get; private set; }
        public static string LiteReason { get; private set; } = "";
        public static event Action Changed;

        // Bg is a step darker than Surface so content panels read as layers floating above the window.
        private static readonly (string Key, string Light, string Dark)[] Tokens =
        {
            ("Bg", "#EDEEF1", "#161618"),
            ("Surface", "#FFFFFF", "#232327"),
            ("SurfaceRaised", "#FFFFFF", "#2B2B30"),
            ("SurfaceAlt", "#F0F1F3", "#2E2E33"),
            ("Divider", "#E2E3E7", "#38383E"),
            ("ControlBorder", "#80808A", "#7C7C86"),
            ("Fg", "#1B1B1F", "#F2F2F4"),
            ("FgMuted", "#5A5A63", "#A8A8B0"),
            ("Moderate", "#8A5300", "#F2B24B"),
            ("Advanced", "#B3261E", "#FF8A80"),
            ("Success", "#1E7B34", "#6CCB7F"),
            ("Overlay", "#59000000", "#99000000"),
        };

        private static readonly (AccentChoice Choice, string Light, string Dark)[] Accents =
        {
            (AccentChoice.Blue, "#0067C0", "#60CDFF"),
            (AccentChoice.Green, "#0F7B0F", "#6CCB5F"),
            (AccentChoice.Violet, "#6B46C1", "#B4A0FF"),
            (AccentChoice.Orange, "#B04A00", "#FFA05A"),
        };

        public static void LoadStyles(ResourceDictionary target)
        {
            using (var s = typeof(Theme).Assembly.GetManifestResourceStream("Optimizzzer.Styles.xaml"))
            {
                if (s == null) throw new InvalidOperationException("Styles.xaml resource missing");
                target.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(s));
            }
        }

        public static void Configure(ThemeChoice theme, AccentChoice accent, LiteChoice lite)
        {
            Accent = accent;
            LiteSetting = lite;
            DetectLite();
            Apply(theme);
        }

        public static void SetAccent(AccentChoice a) { Accent = a; Apply(Choice); }

        public static void SetLite(LiteChoice l) { LiteSetting = l; DetectLite(); Apply(Choice); }

        private static void DetectLite()
        {
            string reason = null;
            if ((RenderCapability.Tier >> 16) < 2) reason = Loc.S("la gráfica no acelera WPF", "the graphics card does not accelerate WPF");
            else if (TotalRamMb() is long mb && mb > 0 && mb < 4096) reason = Loc.S("hay menos de 4 GB de RAM", "there is less than 4 GB of RAM");
            else if (SystemParameters.IsRemoteSession) reason = Loc.S("es una sesión de Escritorio remoto", "this is a Remote Desktop session");
            else if (!SystemParameters.ClientAreaAnimation) reason = Loc.S("las animaciones de Windows están desactivadas", "Windows animations are turned off");
            Lite = LiteSetting == LiteChoice.On || (LiteSetting == LiteChoice.Auto && reason != null);
            LiteReason = LiteSetting == LiteChoice.On ? Loc.S("activado a mano", "turned on manually") : reason ?? "";
        }

        private static long TotalRamMb()
        {
            try
            {
                var s = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                return GlobalMemoryStatusEx(ref s) ? (long)(s.ullTotalPhys / 1024 / 1024) : 0;
            }
            catch (Exception) { return 0; }
        }

        public static void Apply(ThemeChoice choice)
        {
            Choice = choice;
            IsDark = choice == ThemeChoice.Dark || (choice == ThemeChoice.System && SystemPrefersDark());
            var res = Application.Current.Resources;
            foreach (var (key, light, dark) in Tokens) res[key] = Brush(IsDark ? dark : light);

            // Accent: the chosen colour (or the Windows accent), nudged until it passes WCAG AA in this theme.
            (byte, byte, byte) accent;
            var preset = Array.Find(Accents, a => a.Choice == Accent);
            if (Accent != AccentChoice.System && preset.Light != null) accent = Rgb(IsDark ? preset.Dark : preset.Light);
            else accent = SystemAccent() ?? (IsDark ? ((byte)0x4C, (byte)0xC2, (byte)0xFF) : ((byte)0x00, (byte)0x63, (byte)0xB1));
            var surface = IsDark ? ((byte)0x23, (byte)0x23, (byte)0x27) : ((byte)0xFF, (byte)0xFF, (byte)0xFF);
            var fill = ColorMath.EnsureContrast(accent, surface, 3.0, darken: !IsDark);
            var onFill = ColorMath.Contrast((255, 255, 255), fill) >= ColorMath.Contrast((0, 0, 0), fill)
                ? ((byte)255, (byte)255, (byte)255) : ((byte)0, (byte)0, (byte)0);
            if (ColorMath.Contrast(onFill, fill) < 4.5) fill = ColorMath.EnsureContrast(fill, onFill, 4.5, darken: onFill.Item1 == 255);
            var text = ColorMath.EnsureContrast(accent, surface, 4.5, darken: !IsDark);
            res["Accent"] = Brush(fill);
            res["AccentFg"] = Brush(onFill);
            res["AccentText"] = Brush(text);
            // A faint accent wash for selected cards and the current step.
            res["AccentSoft"] = Brush(Color.FromArgb(IsDark ? (byte)0x33 : (byte)0x1A, fill.Item1, fill.Item2, fill.Item3));
            Changed?.Invoke();
        }

        public static (byte, byte, byte) PreviewOf(AccentChoice a)
        {
            var preset = Array.Find(Accents, x => x.Choice == a);
            if (a == AccentChoice.System || preset.Light == null) return SystemAccent() ?? ((byte)0x00, (byte)0x67, (byte)0xC0);
            return Rgb(IsDark ? preset.Dark : preset.Light);
        }

        private static (byte, byte, byte) Rgb(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return (c.R, c.G, c.B);
        }

        public static SolidColorBrush Brush(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        public static SolidColorBrush Brush((byte R, byte G, byte B) c)
        {
            var b = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        private static SolidColorBrush Brush(Color c)
        {
            var b = new SolidColorBrush(c);
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
                    if (k?.GetValue("AccentColor") is int abgr)
                        return ((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
            }
            catch (Exception) { }
            return null;
        }

        public static bool AnimationsEnabled => !Lite && SystemParameters.ClientAreaAnimation;

        public static void WatchSystem()
        {
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color) return;
                Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply(Choice)));
            };
        }

        // ---------- Elevation (Spatial) ----------

        /// <summary>
        /// One soft shadow for a floating surface: it separates layers so the eye finds the content first.
        /// Only on a handful of large surfaces, never on list rows; in lite mode the surface gets a border instead.
        /// </summary>
        public static void Elevate(FrameworkElement e, int level = 1)
        {
            if (Lite) { e.Effect = null; return; }
            e.Effect = new DropShadowEffect
            {
                BlurRadius = level == 1 ? 18 : 32,
                ShadowDepth = level == 1 ? 1 : 6,
                Direction = 270,
                Opacity = IsDark ? 0.45 : (level == 1 ? 0.07 : 0.14),
                Color = Colors.Black,
                RenderingBias = RenderingBias.Performance,
            };
        }

        // ---------- Motion ----------

        private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        /// <summary>Fade plus an 8 px rise, 160 ms, on the GPU (opacity and a render transform only, never layout).</summary>
        public static void Enter(UIElement e, double distance = 8, int ms = 160)
        {
            if (!AnimationsEnabled) return;
            var tt = new TranslateTransform(0, distance);
            e.RenderTransform = tt;
            var d = new Duration(TimeSpan.FromMilliseconds(ms));
            tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(distance, 0, d) { EasingFunction = EaseOut });
            e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, d) { EasingFunction = EaseOut });
        }

        /// <summary>A small scale-in for a confirmation mark, used once at the end of an action.</summary>
        public static void Pop(UIElement e)
        {
            if (!AnimationsEnabled) return;
            var st = new ScaleTransform(0.6, 0.6);
            e.RenderTransformOrigin = new Point(0.5, 0.5);
            e.RenderTransform = st;
            var d = new Duration(TimeSpan.FromMilliseconds(220));
            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.6, 1, d) { EasingFunction = ease });
            st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.6, 1, d) { EasingFunction = ease });
            e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, d));
        }

        /// <summary>Moves a progress bar smoothly to its new value instead of jumping.</summary>
        public static void AnimateValue(System.Windows.Controls.ProgressBar bar, double to)
        {
            if (!AnimationsEnabled) { bar.Value = to; return; }
            bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,
                new DoubleAnimation(bar.Value, to, TimeSpan.FromMilliseconds(180)) { EasingFunction = EaseOut });
        }

        // ---------- Window chrome ----------

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>Dark or light title bar to match the theme (Windows 10 2004+ and 11). Ignored where unsupported.</summary>
        public static void MatchTitleBar(Window w)
        {
            try
            {
                var hwnd = new WindowInteropHelper(w).Handle;
                if (hwnd == IntPtr.Zero) return;
                int dark = IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)) != 0) DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int));
            }
            catch (Exception) { }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
    }
}
