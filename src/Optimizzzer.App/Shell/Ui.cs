using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Optimizzzer.Core;

namespace Optimizzzer.App.Shell
{
    /// <summary>Current UI language. Pages read it while building, and are rebuilt when it changes.</summary>
    public static class Loc
    {
        public static Lang Lang { get; set; } = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Lang.Es : Lang.En;
        public static string S(string es, string en) => Lang == Lang.En ? en : es;
        public static string S(Text t) => t?[Lang] ?? "";

        public static string Size(long bytes)
        {
            var c = Lang == Lang.En ? new System.Globalization.CultureInfo("en-US") : new System.Globalization.CultureInfo("es-ES");
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("N1", c) + " GB";
            if (bytes >= 1L << 20) return (bytes / (double)(1L << 20)).ToString("N0", c) + " MB";
            if (bytes >= 1L << 10) return (bytes / (double)(1L << 10)).ToString("N0", c) + " KB";
            return bytes + " B";
        }

        public static string N(double v, string fmt = "N0") =>
            v.ToString(fmt, Lang == Lang.En ? new System.Globalization.CultureInfo("en-US") : new System.Globalization.CultureInfo("es-ES"));
    }

    /// <summary>Segoe Fluent Icons / MDL2 code points used in the app. Each one maps to the concept it labels.</summary>
    public static class Icons
    {
        public const string Wizard = "";      // Diagnostic: scan and tune the PC
        public const string Privacy = "";     // Lock
        public const string WindowsAI = "";   // Chat bubbles: Copilot and assistants
        public const string Performance = ""; // Speed high
        public const string Gaming = "";      // Game controller
        public const string Power = "";       // Battery
        public const string Interface = "";   // Window
        public const string Update = "";      // Sync
        public const string Network = "";     // Globe
        public const string Services = "\uE912";    // Manage: services are managed system components
        public const string Settings = "\uE713";    // Gear: app settings
        public const string Home = "\uE80F";
        public const string Help = "\uE897";
        public const string Export = "\uEDE1";
        public const string More = "\uE712";
        public const string Processor = "\uE9F5";  // Processing
        public const string Memory = "\uEEA0";     // Memory chip
        public const string Disk = "\uEDA2";       // Hard drive
        public const string Display = "\uE7F4";    // Monitor
        public const string Board = "\uE950";      // Component
        public const string Windows = "\uE770";    // System
        public const string Web = "\uE774";        // Globe: search the internet
        public const string FullScreen = "\uE740";
        public const string BackToWindow = "\uE73F";
        public const string Menu = "\uE700";       // Collapse or expand the navigation menu
        public const string Import = "\uE8B5";
        public const string Glossary = "\uE82D";    // Dictionary
        public const string Keyboard = "\uE765";
        public const string Stopwatch = "\uE916";
        public const string SpeedMedium = "\uEC49";
        public const string Folder = "\uE8B7";
        public const string Apps = "";        // All apps
        public const string Cleanup = "";     // Delete
        public const string Repair = "";      // Wrench
        public const string Startup = "";     // Power button: what runs when the PC starts
        public const string Tasks = "";       // Calendar
        public const string History = "";     // History
        public const string Undo = "";        // Undo
        public const string Drift = "";       // Repeat: changes Windows reverted
        public const string Measure = "";     // Area chart
        public const string Search = "";
        public const string Check = "";
        public const string CheckCircle = "";
        public const string Warning = "";
        public const string Error = "";
        public const string Info = "";
        public const string ChevronDown = "";
        public const string ChevronUp = "";
        public const string ChevronRight = "";
        public const string Back = "";
        public const string Close = "";
        public const string Lock = "";
        public const string Theme = "";       // Contrast
        public const string Language = "";    // Keyboard language
        public const string Save = "";
        public const string Restart = "";     // Refresh
        public const string Shield = "";
        public const string Play = "";
        public const string Stop = "";

        public static string For(Category c)
        {
            switch (c)
            {
                case Category.Privacy: return Privacy;
                case Category.WindowsAI: return WindowsAI;
                case Category.Performance: return Performance;
                case Category.Gaming: return Gaming;
                case Category.Power: return Power;
                case Category.Interface: return Interface;
                case Category.Update: return Update;
                case Category.Network: return Network;
                case Category.Services: return Services;
                default: return Apps;
            }
        }

        public static string For(Risk r) => r == Risk.Safe ? CheckCircle : r == Risk.Moderate ? Warning : Error;

        /// <summary>Speed rises with the level; the controller marks the gaming level.</summary>
        public static string For(Level l)
        {
            switch (l)
            {
                case Level.Quick: return Stopwatch;
                case Level.Balanced: return SpeedMedium;
                case Level.Gaming: return Gaming;
                default: return Performance;
            }
        }
    }

    public static class Names
    {
        public static string Of(Category c)
        {
            switch (c)
            {
                case Category.Privacy: return Loc.S("Privacidad y telemetría", "Privacy and telemetry");
                case Category.WindowsAI: return Loc.S("IA y widgets", "AI and widgets");
                case Category.Performance: return Loc.S("Rendimiento", "Performance");
                case Category.Gaming: return Loc.S("Juegos", "Gaming");
                case Category.Power: return Loc.S("Energía", "Power");
                case Category.Interface: return Loc.S("Interfaz y Explorador", "Interface and Explorer");
                case Category.Update: return Loc.S("Windows Update", "Windows Update");
                case Category.Network: return Loc.S("Red", "Network");
                case Category.Services: return Loc.S("Servicios", "Services");
                default: return Loc.S("Apps preinstaladas", "Preinstalled apps");
            }
        }

        public static string Of(Risk r) => Optimizzzer.Core.Tools.HtmlReport.RiskLabel(r, Loc.Lang);

        public static string Of(TweakStatus s)
        {
            switch (s)
            {
                case TweakStatus.Applied: return Loc.S("Ya aplicado", "Already applied");
                case TweakStatus.Partial: return Loc.S("Aplicado en parte", "Partly applied");
                case TweakStatus.NotApplicable: return Loc.S("No aplica a este equipo", "Does not apply to this PC");
                case TweakStatus.Unknown: return Loc.S("No se pudo comprobar", "Could not be checked");
                default: return "";
            }
        }

        public static string Of(RestartKind r)
        {
            switch (r)
            {
                case RestartKind.Explorer: return Loc.S("Se ve tras reiniciar el Explorador", "Visible after restarting File Explorer");
                case RestartKind.SignOut: return Loc.S("Se ve tras cerrar sesión", "Visible after signing out");
                case RestartKind.Reboot: return Loc.S("Se ve tras reiniciar el equipo", "Visible after restarting the PC");
                default: return "";
            }
        }
    }

    /// <summary>Small factory so every page uses the same type scale, spacing and colours.</summary>
    public static class Ui
    {
        public static T Res<T>(this T e, DependencyProperty p, string key) where T : FrameworkElement
        {
            e.SetResourceReference(p, key);
            return e;
        }

        public static TextBlock Text(string text, double size = 14, bool semibold = false, string brush = "Fg", bool wrap = true)
        {
            // Small print reads better a half step up (owner request): 12 becomes 13 everywhere.
            if (size == 12) size = 13;
            var t = new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = semibold ? FontWeights.SemiBold : FontWeights.Normal,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            };
            t.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return t;
        }

        /// <summary>
        /// Secondary text cut to a few lines so long lists stay scannable. Long texts get a "Show more" toggle;
        /// the full text is always in the tooltip and for screen readers.
        /// </summary>
        public static FrameworkElement Clamped(string text, double size = 12, int lines = 2, int longAfter = 140)
        {
            var t = Muted(text, size);
            t.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            t.LineHeight = Math.Round(size * 1.4);
            AutomationProperties.SetName(t, text);
            if (string.IsNullOrEmpty(text) || text.Length <= longAfter) return t;
            double clamp = t.LineHeight * lines;
            t.MaxHeight = clamp;
            t.TextTrimming = TextTrimming.WordEllipsis;
            t.ToolTip = text;
            var more = Button(Loc.S("Ver más", "Show more"), "SubtleButton", null);
            more.MinHeight = 28;
            more.Padding = new Thickness(0);
            more.FontSize = size;
            more.HorizontalAlignment = HorizontalAlignment.Left;
            more.SetResourceReference(Control.ForegroundProperty, "AccentText");
            more.Click += (_, __) =>
            {
                bool open = double.IsInfinity(t.MaxHeight);
                t.MaxHeight = open ? clamp : double.PositiveInfinity;
                more.Content = open ? Loc.S("Ver más", "Show more") : Loc.S("Ver menos", "Show less");
                AutomationProperties.SetName(more, (string)more.Content);
            };
            return Stack(0, t, more);
        }

        public static TextBlock H1(string text) { var t = Text(text, 26, true); t.Margin = new Thickness(0, 0, 0, 4); return t; }
        public static TextBlock H2(string text) { var t = Text(text, 20, true); t.Margin = new Thickness(0, 0, 0, 4); return t; }
        public static TextBlock H3(string text) { var t = Text(text, 16, true); return t; }
        public static TextBlock Muted(string text, double size = 14) => Text(text, size, false, "FgMuted");

        public static TextBlock Icon(string glyph, double size = 16, string brush = "Fg")
        {
            var t = new IconBlock { Text = glyph, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
            t.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
            t.SetResourceReference(TextBlock.ForegroundProperty, brush);
            return t;
        }

        public static StackPanel Row(double gap, params UIElement[] children)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null) continue;
                if (i > 0 && children[i] is FrameworkElement fe) fe.Margin = new Thickness(gap, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
                p.Children.Add(children[i]);
            }
            return p;
        }

        public static StackPanel Stack(double gap, params UIElement[] children)
        {
            var p = new StackPanel();
            bool first = true;
            foreach (var c in children)
            {
                if (c == null) continue;
                if (!first && c is FrameworkElement fe) fe.Margin = new Thickness(fe.Margin.Left, gap, fe.Margin.Right, fe.Margin.Bottom);
                p.Children.Add(c);
                first = false;
            }
            return p;
        }

        public static Button Button(string text, string style, Action onClick, string icon = null, string automationName = null)
        {
            object content = text;
            if (icon != null)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(Icon(icon, 16, style == "PrimaryButton" ? "AccentFg" : "Fg"));
                if (!string.IsNullOrEmpty(text))
                {
                    var label = new TextBlock { Text = text, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    row.Children.Add(label);
                }
                content = row;
            }
            var b = new Button { Content = content };
            b.SetResourceReference(FrameworkElement.StyleProperty, style);
            AutomationProperties.SetName(b, automationName ?? text);
            if (onClick != null) b.Click += (_, __) => onClick();
            return b;
        }

        public static Button Primary(string text, Action onClick, string icon = null) => Button(text, "PrimaryButton", onClick, icon);
        public static Button Secondary(string text, Action onClick, string icon = null) => Button(text, "SecondaryButton", onClick, icon);
        public static Button Subtle(string text, Action onClick, string icon = null, string automationName = null) => Button(text, "SubtleButton", onClick, icon, automationName);

        /// <summary>
        /// An icon-only button that opens a small menu of related actions (keyboard: Enter or Space, then arrows).
        /// The name is the tooltip and what screen readers announce.
        /// </summary>
        public static Button MenuButton(string text, string icon, params (string Text, string Icon, Action Run)[] items)
        {
            var b = Button("", "SubtleButton", null, icon, text);
            b.MinWidth = 40;
            b.ToolTip = text;
            var menu = new ContextMenu { PlacementTarget = b, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
            foreach (var (label, glyph, run) in items)
            {
                var mi = new MenuItem { Header = label, Icon = Icon(glyph, 16) };
                AutomationProperties.SetName(mi, label);
                mi.Click += (_, __) => run();
                menu.Items.Add(mi);
            }
            b.Click += (_, __) => { menu.IsOpen = true; };
            AutomationProperties.SetName(b, text);
            b.SetValue(AutomationProperties.HelpTextProperty, string.Join(", ", System.Linq.Enumerable.Select(items, i => i.Text)));
            return b;
        }

        public static Border Divider(double top = 0, double bottom = 0)
        {
            var d = new Border { Height = 1, Margin = new Thickness(0, top, 0, bottom) };
            d.SetResourceReference(Border.BackgroundProperty, "Divider");
            return d;
        }

        public static Border Panel(UIElement child, double padding = 24)
        {
            // A quiet filled area, no outline: sections read as groups without boxes inside boxes.
            var b = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(padding), Child = child };
            b.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            return b;
        }

        /// <summary>The identity motif: an icon plus a word, never colour alone.</summary>
        public static FrameworkElement RiskMarker(Risk r)
        {
            string brush = r == Risk.Safe ? "Success" : r == Risk.Moderate ? "Moderate" : "Advanced";
            var row = Row(6, Icon(Icons.For(r), 14, brush), Text(Names.Of(r), 12, true, brush, wrap: false));
            row.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(row, Loc.S("Riesgo: ", "Risk: ") + Names.Of(r));
            return row;
        }

        /// <summary>A small labelled fact (impact, reversible, restart): icon plus words, quiet colour.</summary>
        public static FrameworkElement Chip(string icon, string text, string brush = "FgMuted", string tooltip = null)
        {
            var row = Row(4, Icon(icon, 12, brush), Text(text, 12, false, brush, wrap: false));
            var b = new Border { Child = row, Padding = new Thickness(8, 2, 8, 2), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 6, 0), HorizontalAlignment = HorizontalAlignment.Left };
            b.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            if (tooltip != null) b.ToolTip = tooltip;
            AutomationProperties.SetName(b, text);
            return b;
        }

        public static FrameworkElement Notice(string icon, string brush, string text)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var i = Icon(icon, 16, brush);
            i.VerticalAlignment = VerticalAlignment.Top;
            i.Margin = new Thickness(0, 2, 10, 0);
            var t = Text(text);
            Grid.SetColumn(t, 1);
            g.Children.Add(i);
            g.Children.Add(t);
            return g;
        }

        /// <summary>Loading state with a determinate bar when progress is known.</summary>
        public static (FrameworkElement View, Action<double, string> Update) Loading(string text)
        {
            var label = Muted(text);
            var bar = new ProgressBar { Minimum = 0, Maximum = 1, Value = 0, Margin = new Thickness(0, 12, 0, 0), MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left, Width = 420 };
            AutomationProperties.SetName(bar, text);
            var view = Stack(0, label, bar);
            return (view, (v, msg) => { Theme.AnimateValue(bar, v); if (msg != null) label.Text = msg; });
        }

        public static FrameworkElement ErrorState(string what, string detail, Action retry)
        {
            return Stack(12,
                Notice(Icons.Error, "Advanced", what),
                string.IsNullOrEmpty(detail) ? null : Muted(detail, 12),
                retry == null ? null : Secondary(Loc.S("Intentar de nuevo", "Try again"), retry, Icons.Restart));
        }

        /// <summary>A "More information" link that unfolds the longer explanation in place.</summary>
        public static FrameworkElement MoreInfo(params (string Heading, string Body)[] parts)
        {
            var body = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 4), MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var (h, t) in parts)
            {
                if (string.IsNullOrEmpty(t)) continue;
                if (!string.IsNullOrEmpty(h)) body.Children.Add(Text(h, 12, true, "Fg"));
                var tb = Muted(t, 12);
                tb.Margin = new Thickness(0, 0, 0, 6);
                body.Children.Add(tb);
            }
            var more = Button(Loc.S("Más información", "More information"), "SubtleButton", null, Icons.ChevronDown);
            more.MinHeight = 28;
            more.Padding = new Thickness(0);
            more.FontSize = 12;
            more.HorizontalAlignment = HorizontalAlignment.Left;
            more.SetResourceReference(Control.ForegroundProperty, "AccentText");
            more.Click += (_, __) =>
            {
                bool open = body.Visibility == Visibility.Visible;
                body.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
                AutomationProperties.SetName(more, open ? Loc.S("Más información", "More information") : Loc.S("Menos información", "Less information"));
            };
            return Stack(0, more, body);
        }

        /// <summary>The scan reads every tweak's real state, so on slow PCs it takes minutes: say so up front.</summary>
        public static FrameworkElement ScanDurationNote() => Notice(Icons.Stopwatch, "Moderate", Loc.S(
            "El análisis puede tardar varios minutos según tu equipo. Mientras tanto puedes usar las herramientas del menú.",
            "The scan can take a few minutes depending on your PC. You can use the tools in the menu meanwhile."));

        public static FrameworkElement Empty(string text) => Notice(Icons.Info, "FgMuted", text);

        /// <summary>A text box with a visible placeholder that disappears while typing. Escape clears it.</summary>
        public static SearchField SearchBox(string placeholder, Action<string> onChange) => new SearchField(placeholder, onChange);

        public static ScrollViewer Scroll(UIElement content) => new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
            PanningMode = PanningMode.VerticalOnly,
        };

        /// <summary>Two-column key/value table for system information.</summary>
        public static Grid KeyValues(params (string Key, string Value)[] rows)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < rows.Length; i++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var k = Muted(rows[i].Key);
                var v = Text(string.IsNullOrWhiteSpace(rows[i].Value) ? Loc.S("Sin dato", "No data") : rows[i].Value);
                k.Margin = v.Margin = new Thickness(0, 4, 12, 4);
                Grid.SetRow(k, i); Grid.SetRow(v, i); Grid.SetColumn(v, 1);
                g.Children.Add(k); g.Children.Add(v);
            }
            return g;
        }
    }

    public sealed class SearchField : Grid
    {
        public TextBox Box { get; }

        public SearchField(string placeholder, Action<string> onChange)
        {
            MinWidth = 260;
            Box = new TextBox { Padding = new Thickness(36, 0, 10, 0) };
            AutomationProperties.SetName(Box, placeholder);
            var icon = Ui.Icon(Icons.Search, 14, "FgMuted");
            icon.Margin = new Thickness(12, 0, 0, 0);
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            icon.IsHitTestVisible = false;
            var hint = new IconBlock { Text = placeholder, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
            hint.SetResourceReference(TextBlock.FontFamilyProperty, "UiFont");
            hint.SetResourceReference(TextBlock.ForegroundProperty, "FgMuted");
            hint.Margin = new Thickness(37, 0, 10, 0);
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.IsHitTestVisible = false;
            Children.Add(Box);
            Children.Add(icon);
            Children.Add(hint);
            Box.TextChanged += (_, __) =>
            {
                hint.Visibility = Box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                onChange(Box.Text);
            };
            Box.KeyDown += (_, e) => { if (e.Key == Key.Escape && Box.Text.Length > 0) { Box.Text = ""; e.Handled = true; } };
        }
    }

    /// <summary>
    /// Decorative text (icon glyphs, placeholders) that screen readers must skip: the control next to it
    /// already carries the accessible name. Returning no automation peer removes it from the UIA tree.
    /// </summary>
    public sealed class IconBlock : TextBlock
    {
        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => null;
    }
}
