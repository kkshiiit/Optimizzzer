using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Optimizzzer.App.Pages;
using Optimizzzer.Core;

namespace Optimizzzer.App.Shell
{
    public enum ToastKind { Success, Info, Warning, Error }

    /// <summary>
    /// Shell: navigation rail on the window background, content on a floating surface (Spatial layers),
    /// toasts in the top-right corner of the content, and one overlay layer for dialogs, sheets, Ctrl+K and the tour.
    /// </summary>
    public sealed class MainWindow : Window
    {
        private readonly AppState _state;
        private readonly List<AppPage> _pages = new List<AppPage>();
        private readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();
        private readonly ContentControl _host = new ContentControl { Focusable = false };
        private readonly Grid _overlay = new Grid { Visibility = Visibility.Collapsed };
        private readonly StackPanel _toasts = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 16, 16, 0), MaxWidth = 420 };
        private readonly Grid _root = new Grid();
        private Border _content;
        private StackPanel _nav, _navBottom;
        private AppPage _current;
        private TaskCompletionSource<bool> _dialog;
        private Action _overlayEscape;
        private ColumnDefinition _navColumn;
        private TextBlock _brand;

        /// <summary>Below this width the menu shows icons only and pages use tighter margins.</summary>
        public const double CompactWidth = 900;
        /// <summary>True while the window is narrow (or the user collapsed the menu): pages read it when they build.</summary>
        public static bool Compact { get; private set; }
        private IInputElement _focusBeforeOverlay;

        public MainWindow(AppState state)
        {
            _state = state;
            Title = "Optimizzzer";
            try
            {
                using (var ico = typeof(MainWindow).Assembly.GetManifestResourceStream("Optimizzzer.app.ico"))
                    if (ico != null) Icon = System.Windows.Media.Imaging.BitmapFrame.Create(ico, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            }
            catch (Exception) { } // the window works without its icon
            MinWidth = 640; MinHeight = 480;
            PlaceOnScreen();
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            SetResourceReference(BackgroundProperty, "Bg");
            SetResourceReference(FontFamilyProperty, "UiFont");
            SetResourceReference(ForegroundProperty, "Fg");
            FontSize = 14;

            _pages.Add(new HomePage(state, this));
            _pages.Add(new LevelsPage(state, this));
            _pages.Add(new WizardPage(state, this));
            _pages.Add(new CleanupPage(state, this));
            _pages.Add(new RepairPage(state, this));
            _pages.Add(new DnsPage(state, this));
            _pages.Add(new StartupPage(state, this));
            _pages.Add(new ServicesPage(state, this));
            _pages.Add(new TasksPage(state, this));
            _pages.Add(new AppsPage(state, this));
            _pages.Add(new HistoryPage(state, this));
            _pages.Add(new DriftPage(state, this));
            _pages.Add(new MeasurePage(state, this));
            _pages.Add(new SettingsPage(state, this));
            _pages.Add(new HelpPage(state, this));

            _navColumn = new ColumnDefinition { Width = GridLength.Auto };
            _root.ColumnDefinitions.Add(_navColumn);
            _root.ColumnDefinitions.Add(new ColumnDefinition());
            _root.Children.Add(BuildNav());

            // Right column: a slim header with the global actions, then the content surface below it.
            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition());
            right.Children.Add(BuildHeader());

            // The content surface floats above the window background: the first thing the eye lands on.
            var contentGrid = new Grid();
            contentGrid.Children.Add(_host);
            contentGrid.Children.Add(_toasts);
            _content = new Border { CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 12, 12), Child = contentGrid, BorderThickness = new Thickness(1) };
            _content.SetResourceReference(Border.BackgroundProperty, "Surface");
            _content.SetResourceReference(Border.BorderBrushProperty, "Divider");
            Theme.Elevate(_content);
            Grid.SetRow(_content, 1);
            right.Children.Add(_content);
            Grid.SetColumn(right, 1);
            _root.Children.Add(right);

            Grid.SetColumnSpan(_overlay, 2);
            _root.Children.Add(_overlay);
            Content = _root;
            ApplyTextScale();

            PreviewKeyDown += OnKey;
            SizeChanged += (_, __) => UpdateLayoutMode();
            Closing += (_, __) => RememberPlacement();
            SourceInitialized += (_, __) => Theme.MatchTitleBar(this);
            Theme.Changed += OnThemeChanged;
            Closed += (_, __) => Theme.Changed -= OnThemeChanged;
            ContentRendered += (_, __) =>
            {
                if (!_state.TourDone && !Program.InSelfTest) Dispatcher.BeginInvoke(new Action(ShowTour), DispatcherPriority.ApplicationIdle);
            };
            Navigate("home");
        }

        public IReadOnlyList<AppPage> Pages => _pages;
        public AppPage CurrentPage => _current;

        /// <summary>What is on screen right now (used by the self-test).</summary>
        public FrameworkElement CurrentView => _overlay.Visibility == Visibility.Visible && _overlay.Children.Count > 1
            ? _overlay.Children[1] as FrameworkElement
            : _host.Content as FrameworkElement;

        private void OnThemeChanged()
        {
            Theme.Elevate(_content);
            Theme.MatchTitleBar(this);
        }

        // ---------- Navigation ----------

        // ---------- Window size, full screen and compact layout ----------

        /// <summary>
        /// Starts at the last size and place, or at 1200x800 shrunk to fit the screen. A window taller than the screen
        /// hides its own title bar (minimize, maximize, close), which is what happened on 768 px screens.
        /// </summary>
        private void PlaceOnScreen()
        {
            var work = SystemParameters.WorkArea;
            var saved = _state.SavedPlacement;
            var virt = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (saved.HasValue && saved.Value.Width >= MinWidth && saved.Value.Height >= MinHeight && virt.Contains(new Point(saved.Value.Left + 40, saved.Value.Top + 20)))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = saved.Value.Left; Top = saved.Value.Top;
                Width = Math.Min(saved.Value.Width, work.Width); Height = Math.Min(saved.Value.Height, work.Height);
                if (_state.SavedMaximized) WindowState = WindowState.Maximized;
                return;
            }
            Width = Math.Max(MinWidth, Math.Min(1200, work.Width * 0.9));
            Height = Math.Max(MinHeight, Math.Min(800, work.Height * 0.9));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (work.Width < 1100 || work.Height < 720) WindowState = WindowState.Maximized;
        }

        private void RememberPlacement()
        {
            if (_fullScreen) ToggleFullScreen();
            var b = RestoreBounds;
            if (!b.IsEmpty) _state.SavePlacement(b, WindowState == WindowState.Maximized);
        }

        private bool _fullScreen;
        private WindowState _stateBeforeFullScreen;

        public bool IsFullScreen => _fullScreen;

        /// <summary>F11: the whole screen for the app, no title bar or taskbar. F11 or Esc goes back.</summary>
        /// <summary>Scales the whole window (text, icons and controls together, so nothing is cut).</summary>
        public void ApplyTextScale()
        {
            double f = _state.TextScale / 100.0;
            _root.LayoutTransform = f == 1 ? Transform.Identity : new ScaleTransform(f, f);
        }

        public void ToggleFullScreen()
        {
            if (!_fullScreen)
            {
                _stateBeforeFullScreen = WindowState;
                WindowStyle = WindowStyle.None;
                // Maximizing from Normal makes a borderless window cover the taskbar too.
                if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;
                _fullScreen = true;
                Toast(ToastKind.Info, Loc.S("Pantalla completa. Pulsa F11 o Esc para salir.", "Full screen. Press F11 or Esc to leave."));
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = _stateBeforeFullScreen;
                _fullScreen = false;
            }
        }

        // A narrow window closes the menu by itself; ≡ can still open it there until the window is resized again.
        private bool _userOpened;

        public void ToggleNavCollapsed()
        {
            if (Compact) { _state.NavCollapsed = false; _userOpened = true; }
            else { _state.NavCollapsed = true; _userOpened = false; }
            _state.SaveSettings();
            UpdateLayoutMode(force: true);
        }

        private bool? _appliedCompact;

        private void UpdateLayoutMode(bool force = false)
        {
            if (ActualWidth <= 0) return;
            bool compact = _state.NavCollapsed || (ActualWidth < CompactWidth && !_userOpened);
            // Compared with what this window last applied, not with the shared flag, so a new window always sizes its menu.
            if (compact == _appliedCompact && !force) return;
            bool first = _appliedCompact == null;
            _appliedCompact = compact;
            Compact = compact;
            BuildNavItems();
            AnimateNav(compact, animate: !first);
            // Pages read Compact when they build; rebuild them all (only the visible one is built right away).
            foreach (var p in _pages) p.Invalidate();
            if (_current != null && !OverlayOpen) { ShowView(_current.View); _current.OnShown(); }
        }

        public const double NavOpenWidth = 232, NavClosedWidth = 56;
        private Border _navHost;
        private ScrollViewer _navScroll;
        private Button _menuButton;

        /// <summary>
        /// The menu: [≡] Optimizzzer on top, then the pages. Closed, it keeps only the icons (same size, centred, never
        /// cut) and the title slides away; open, every icon gets its name.
        /// </summary>
        private FrameworkElement BuildNav()
        {
            var g = new Grid { Margin = new Thickness(8, 12, 8, 12) };
            AutomationProperties.SetName(g, Loc.S("Navegación", "Navigation"));
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());

            _menuButton = new Button { Content = Ui.Icon(Icons.Menu, 16), Width = 40, Height = 40, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(0) };
            _menuButton.SetResourceReference(StyleProperty, "NavButton");
            _menuButton.Click += (_, __) => ToggleNavCollapsed();
            _brand = Ui.Text("Optimizzzer", 18, true, "Fg", wrap: false);
            _brand.Margin = new Thickness(8, 0, 0, 0);
            _brand.VerticalAlignment = VerticalAlignment.Center;
            _brand.RenderTransform = new TranslateTransform();
            var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            top.Children.Add(_menuButton);
            top.Children.Add(_brand);
            g.Children.Add(top);

            _nav = new StackPanel();
            _navScroll = Ui.Scroll(_nav);
            Grid.SetRow(_navScroll, 1);
            g.Children.Add(_navScroll);

            _navBottom = new StackPanel(); // kept for older callers; the global actions now live in the header
            BuildNavItems();
            _navHost = new Border { Child = g, Width = NavOpenWidth, ClipToBounds = true };
            return _navHost;
        }

        /// <summary>Animates the menu width and the title. In lite mode it just jumps.</summary>
        private void AnimateNav(bool closed, bool animate)
        {
            if (_navHost == null) return;
            double to = closed ? NavClosedWidth : NavOpenWidth;
            // While closed the scroll bar would sit on top of the icons: hide it (the wheel still scrolls).
            _navScroll.VerticalScrollBarVisibility = closed ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
            var tt = (TranslateTransform)_brand.RenderTransform;
            if (!animate || Theme.Lite)
            {
                _navHost.BeginAnimation(WidthProperty, null);
                _navHost.Width = to;
                _brand.Visibility = closed ? Visibility.Collapsed : Visibility.Visible;
                _brand.Opacity = 1; tt.X = 0;
                return;
            }
            var d = TimeSpan.FromMilliseconds(160);
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            _navHost.BeginAnimation(WidthProperty, new System.Windows.Media.Animation.DoubleAnimation(to, d) { EasingFunction = ease });
            _brand.Visibility = Visibility.Visible;
            var fade = new System.Windows.Media.Animation.DoubleAnimation(closed ? 0 : 1, d) { EasingFunction = ease };
            if (closed) fade.Completed += (_, __) => { if (Compact) _brand.Visibility = Visibility.Collapsed; };
            _brand.BeginAnimation(OpacityProperty, fade);
            tt.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(closed ? -12 : 0, d) { EasingFunction = ease });
        }

        private Button _gameModeButton;

        /// <summary>Top right of the window: global actions as icons only (tooltips and screen-reader names).</summary>
        private FrameworkElement BuildHeader()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 12, 6) };
            Button IconButton(string icon, string name, Action onClick)
            {
                var b = Ui.Button("", "SubtleButton", onClick, icon, name);
                b.Width = 40; b.Height = 40; b.Padding = new Thickness(0);
                b.ToolTip = name;
                b.Margin = new Thickness(4, 0, 0, 0);
                return b;
            }
            _gameModeButton = IconButton(Icons.Gaming, Loc.S("Modo juego temporal", "Temporary game mode"), () => GameModeRequested?.Invoke());
            row.Children.Add(_gameModeButton);
            row.Children.Add(IconButton(Icons.Search, Loc.S("Buscar (Ctrl+K)", "Search (Ctrl+K)"), ShowPalette));
            row.Children.Add(IconButton(Icons.Settings, Loc.S("Ajustes", "Settings"), () => Navigate("settings")));
            row.Children.Add(IconButton(Icons.Help, Loc.S("Ayuda (F1)", "Help (F1)"), () => Navigate("help")));
            _header = row;
            return row;
        }

        private StackPanel _header;

        /// <summary>Raised by the header's game mode button; the game mode feature subscribes to it.</summary>
        public event Action GameModeRequested;

        /// <summary>Lets the game mode feature show that it is on (accent icon).</summary>
        public void SetGameModeIndicator(bool on)
        {
            if (_gameModeButton == null) return;
            _gameModeButton.SetResourceReference(ForegroundProperty, on ? "AccentText" : "Fg");
            var name = on ? Loc.S("Modo juego temporal: activo", "Temporary game mode: on") : Loc.S("Modo juego temporal", "Temporary game mode");
            _gameModeButton.ToolTip = name;
            AutomationProperties.SetName(_gameModeButton, name);
        }

        private void BuildNavItems()
        {
            _nav.Children.Clear();
            _navButtons.Clear();
            _menuButton.ToolTip = Compact ? Loc.S("Expandir menú", "Expand menu") : Loc.S("Contraer menú", "Collapse menu");
            AutomationProperties.SetName(_menuButton, _menuButton.ToolTip.ToString());
            AutomationProperties.SetName(_brand, "Optimizzzer");
            void Section(string text)
            {
                if (Compact)
                {
                    // Icons only: a thin line keeps the groups apart without words that would not fit.
                    var d = Ui.Divider(8, 8);
                    d.Width = 32; d.HorizontalAlignment = HorizontalAlignment.Left; d.Margin = new Thickness(4, 8, 0, 8);
                    _nav.Children.Add(d);
                    return;
                }
                var t = Ui.Muted(text, 12);
                t.Margin = new Thickness(12, 14, 0, 4);
                _nav.Children.Add(t);
            }
            // Grouped by task: start and results first, then cleaning, then system parts, then tracking, then the advanced zone.
            foreach (var k in new[] { "home", "levels", "wizard", "measure" }) AddNav(_nav, k);
            Section(Loc.S("Limpiar y reparar", "Clean and repair"));
            foreach (var k in new[] { "cleanup", "apps", "repair" }) AddNav(_nav, k);
            Section(Loc.S("Sistema", "System"));
            foreach (var k in new[] { "startup", "services", "tasks", "dns" }) AddNav(_nav, k);
            Section(Loc.S("Seguimiento", "Tracking"));
            foreach (var k in new[] { "history", "drift" }) AddNav(_nav, k);
            if (_pages.Any(p => p.Key == "advanced"))
            {
                Section(Loc.S("Avanzado", "Advanced"));
                AddNav(_nav, "advanced");
            }
        }

        private void AddNav(Panel panel, string key)
        {
            var page = _pages.FirstOrDefault(p => p.Key == key);
            if (page == null) return;
            var b = NavAction(page.Icon, page.Title, () => Navigate(key));
            _navButtons[key] = b;
            if (_current?.Key == key) MarkCurrent(b);
            panel.Children.Add(b);
        }

        private static Button NavAction(string icon, string text, Action onClick, string name = null, string hint = null)
        {
            Button b;
            if (Compact)
            {
                // Icon only, same 16 px as when open, centred in a 40 px square: never cut.
                b = new Button { Content = Ui.Icon(icon, 16), Width = 40, Height = 40, HorizontalAlignment = HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 1), ToolTip = name ?? text };
            }
            else
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var label = Ui.Text(text, 14, false, "Fg", wrap: false);
                label.Margin = new Thickness(12, 0, 8, 0);
                label.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(label, 1);
                g.Children.Add(Ui.Icon(icon, 16));
                g.Children.Add(label);
                if (hint != null)
                {
                    var h = Ui.Text(hint, 12, false, "FgMuted", wrap: false);
                    h.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(h, 2);
                    g.Children.Add(h);
                }
                b = new Button { Content = g, Margin = new Thickness(0, 1, 0, 1), MinHeight = 38 };
            }
            b.SetResourceReference(StyleProperty, "NavButton");
            AutomationProperties.SetName(b, name ?? text);
            b.Click += (_, __) => onClick();
            return b;
        }

        private static void MarkCurrent(Button b)
        {
            b.Tag = "current";
            AutomationProperties.SetItemStatus(b, "current");
        }

        public void Navigate(string key)
        {
            var page = _pages.FirstOrDefault(p => p.Key == key);
            if (page == null) return;
            foreach (var kv in _navButtons) { kv.Value.Tag = null; AutomationProperties.SetItemStatus(kv.Value, ""); }
            if (_navButtons.TryGetValue(key, out var nb)) MarkCurrent(nb);
            _current = page;
            ShowView(page.View);
            page.OnShown();
        }

        public T PageOf<T>() where T : AppPage => _pages.OfType<T>().First();

        public void ShowView(FrameworkElement view)
        {
            _host.Content = view;
            Theme.Enter(view);
        }

        /// <summary>Rebuilds a page; shows it again only if it is the one on screen.</summary>
        public void RefreshCurrentIf(AppPage page)
        {
            page.Invalidate();
            if (_current == page) ShowView(page.View);
        }

        public void RefreshCurrent()
        {
            if (_current == null) return;
            _current.Invalidate();
            ShowView(_current.View);
            _current.OnShown();
        }

        /// <summary>Pages that show tweak status are rebuilt after anything changed the system.</summary>
        public void InvalidateScanViews(AppPage except = null)
        {
            foreach (var p in _pages.Where(p => (p is HomePage || p is WizardPage || p is LevelsPage) && p != except))
            {
                p.Invalidate();
                if (p == _current) { ShowView(p.View); p.OnShown(); }
            }
        }

        /// <summary>Rebuilds every page and the navigation, for language and theme changes.</summary>
        public void RebuildAll()
        {
            foreach (var p in _pages) p.Invalidate();
            _commands = null; // titles are in the old language
            BuildNavItems();
            if (_current != null)
            {
                if (_navButtons.TryGetValue(_current.Key, out var b)) MarkCurrent(b);
                ShowView(_current.View);
                _current.OnShown();
            }
        }

        // ---------- Toasts ----------

        /// <summary>
        /// A short status message in the corner. Success and info close by themselves after 4 s;
        /// warnings and errors stay until closed, so nobody misses a problem.
        /// </summary>
        public void Toast(ToastKind kind, string message, string actionText = null, Action action = null)
        {
            string icon = kind == ToastKind.Success ? Icons.CheckCircle : kind == ToastKind.Error ? Icons.Error : kind == ToastKind.Warning ? Icons.Warning : Icons.Info;
            string brush = kind == ToastKind.Success ? "Success" : kind == ToastKind.Error ? "Advanced" : kind == ToastKind.Warning ? "Moderate" : "AccentText";
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ic = Ui.Icon(icon, 18, brush);
            ic.VerticalAlignment = VerticalAlignment.Top;
            ic.Margin = new Thickness(0, 2, 12, 0);
            var text = Ui.Text(message);
            var body = Ui.Stack(6, text);
            Border card = null;
            if (actionText != null && action != null)
            {
                var act = Ui.Subtle(actionText, () => { Remove(card); action(); });
                act.HorizontalAlignment = HorizontalAlignment.Left;
                act.Padding = new Thickness(0);
                act.MinHeight = 28;
                act.SetResourceReference(ForegroundProperty, "AccentText");
                body.Children.Add(act);
            }
            Grid.SetColumn(body, 1);
            var close = Ui.Subtle("", () => Remove(card), Icons.Close, Loc.S("Cerrar aviso", "Close notice"));
            close.Width = 32; close.MinHeight = 32; close.Height = 32; close.Padding = new Thickness(0);
            close.VerticalAlignment = VerticalAlignment.Top;
            close.Margin = new Thickness(8, -4, -8, 0);
            Grid.SetColumn(close, 2);
            g.Children.Add(ic); g.Children.Add(body); g.Children.Add(close);
            card = new Border { Child = g, Padding = new Thickness(16, 12, 16, 12), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 8) };
            card.SetResourceReference(Border.BackgroundProperty, "SurfaceRaised");
            card.SetResourceReference(Border.BorderBrushProperty, "Divider");
            Theme.Elevate(card, 2);
            AutomationProperties.SetName(card, message);
            AutomationProperties.SetLiveSetting(card, kind == ToastKind.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
            _toasts.Children.Insert(0, card);
            while (_toasts.Children.Count > 3) _toasts.Children.RemoveAt(_toasts.Children.Count - 1);
            Theme.Enter(card, -8);
            if (kind == ToastKind.Success || kind == ToastKind.Info)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                timer.Tick += (_, __) => { timer.Stop(); Remove(card); };
                timer.Start();
            }
        }

        private void Remove(UIElement card)
        {
            if (card != null) _toasts.Children.Remove(card);
        }

        public IEnumerable<string> ToastTexts => _toasts.Children.OfType<Border>().Select(b => AutomationProperties.GetName(b));

        // ---------- Overlays: dialogs, sheets, palette, tour ----------

        /// <summary>Shows a raised card above a dimmed window. Escape or a click on the shade calls <paramref name="onEscape"/>.</summary>
        public void ShowOverlay(FrameworkElement card, Action onEscape, double maxWidth = 560, bool top = false)
        {
            _focusBeforeOverlay = _focusBeforeOverlay ?? Keyboard.FocusedElement;
            _overlayEscape = onEscape;
            var shade = new Border();
            shade.SetResourceReference(Border.BackgroundProperty, "Overlay");
            shade.MouseLeftButtonDown += (_, __) => _overlayEscape?.Invoke();
            card.MaxWidth = Compact ? Math.Max(320, ActualWidth - 32) : maxWidth;
            double topGap = top && ActualHeight >= 640 ? 72 : 24;
            card.Margin = new Thickness(Compact ? 16 : 24, topGap, Compact ? 16 : 24, 24);
            // Never taller than the window: sheets scroll inside and keep their buttons visible.
            void Fit() { if (_overlay.ActualHeight > 0) card.MaxHeight = Math.Max(200, _overlay.ActualHeight - topGap - 24); }
            Fit();
            SizeChangedEventHandler onSize = (_, __) => Fit();
            _overlay.SizeChanged += onSize;
            _overlayCleanup = () => _overlay.SizeChanged -= onSize;
            card.HorizontalAlignment = HorizontalAlignment.Center;
            card.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Center;
            Theme.Elevate(card, 2);
            _overlay.Children.Clear();
            _overlay.Children.Add(shade);
            _overlay.Children.Add(card);
            _overlay.Visibility = Visibility.Visible;
            KeyboardNavigation.SetTabNavigation(_overlay, KeyboardNavigationMode.Cycle);
            _root.Children[0].IsEnabled = false;
            _content.IsEnabled = false;
            Theme.Enter(card, 12, 180);
        }

        private Action _overlayCleanup;

        /// <summary>
        /// A raised card whose header and buttons stay fixed while the middle scrolls, so nothing is cut off
        /// on short screens.
        /// </summary>
        public static Border Sheet(UIElement header, UIElement body, UIElement buttons, double padding = 28)
        {
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (header is FrameworkElement hf) hf.Margin = new Thickness(0, 0, 0, 16);
            if (header != null) g.Children.Add(header);
            var sv = Ui.Scroll(body);
            sv.Margin = new Thickness(0, 0, -8, 0);
            if (body is FrameworkElement bf) bf.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetRow(sv, 1);
            g.Children.Add(sv);
            if (buttons is FrameworkElement btf) btf.Margin = new Thickness(0, 16, 0, 0);
            if (buttons != null) { Grid.SetRow(buttons, 2); g.Children.Add(buttons); }
            return Card(g, padding);
        }

        public void CloseOverlay()
        {
            _overlayCleanup?.Invoke();
            _overlayCleanup = null;
            _overlay.Visibility = Visibility.Collapsed;
            _overlay.Children.Clear();
            _overlayEscape = null;
            _root.Children[0].IsEnabled = true;
            _content.IsEnabled = true;
            _focusBeforeOverlay?.Focus();
            _focusBeforeOverlay = null;
        }

        public bool OverlayOpen => _overlay.Visibility == Visibility.Visible;

        public static Border Card(UIElement child, double padding = 28)
        {
            var b = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(padding), Child = child, BorderThickness = new Thickness(1) };
            b.SetResourceReference(Border.BackgroundProperty, "SurfaceRaised");
            b.SetResourceReference(Border.BorderBrushProperty, "Divider");
            return b;
        }

        /// <summary>Modal confirmation inside the window. Escape or the secondary button cancels.</summary>
        public Task<bool> Confirm(string title, string message, string confirmText, bool danger = false, string cancelText = null)
        {
            _dialog?.TrySetResult(false);
            var tcs = new TaskCompletionSource<bool>();
            _dialog = tcs;
            void Done(bool r) { CloseOverlay(); tcs.TrySetResult(r); }

            var confirm = Ui.Primary(confirmText, () => Done(true));
            if (danger)
            {
                confirm.SetResourceReference(BackgroundProperty, "Advanced");
                confirm.SetResourceReference(BorderBrushProperty, "Advanced");
                confirm.SetResourceReference(ForegroundProperty, "Surface");
            }
            var cancel = Ui.Secondary(cancelText ?? Loc.S("Cancelar", "Cancel"), () => Done(false));
            var buttons = Ui.Row(8, cancel, confirm);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var card = Sheet(Ui.H2(title), Ui.Text(message), buttons);
            AutomationProperties.SetName(card, title);
            ShowOverlay(card, () => Done(false), 520);
            Dispatcher.BeginInvoke(new Action(() => (danger ? cancel : confirm).Focus()), DispatcherPriority.Input);
            return tcs.Task;
        }

        public Task Alert(string title, string message) => Confirm(title, message, Loc.S("Entendido", "OK"), cancelText: Loc.S("Cerrar", "Close"));

        /// <summary>Errors explain what happened and what to do; the message is the technical detail.</summary>
        public void ShowError(string message, string whatToDo = null) =>
            _ = Alert(Loc.S("Algo salió mal", "Something went wrong"),
                (whatToDo ?? Loc.S("No se ha cambiado nada más. Puedes reintentarlo; si vuelve a fallar, revisa Historial.", "Nothing else was changed. You can try again; if it fails again, check History."))
                + "\n\n" + Loc.S("Detalle: ", "Detail: ") + message);

        private void OnKey(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            if (e.Key == Key.Escape && OverlayOpen) { _overlayEscape?.Invoke(); e.Handled = true; return; }
            if (e.Key == Key.F11 || (e.Key == Key.Escape && _fullScreen)) { ToggleFullScreen(); e.Handled = true; return; }
            if (OverlayOpen) return;
            if (e.Key == Key.K && mods == ModifierKeys.Control) { ShowPalette(); e.Handled = true; }
            else if (e.Key == Key.F1) { Navigate("help"); e.Handled = true; }
            else if (e.Key == Key.OemComma && mods == ModifierKeys.Control) { Navigate("settings"); e.Handled = true; }
            else if (e.Key == Key.H && mods == (ModifierKeys.Control | ModifierKeys.Shift)) { Navigate("home"); e.Handled = true; }
        }

        // ---------- Command palette (Ctrl+K) ----------

        private sealed class Command
        {
            public string Icon, Title, Subtitle;
            public Action Run;
        }

        private List<Command> _commands;

        /// <summary>Built on first use, so it costs nothing at startup.</summary>
        private List<Command> Commands()
        {
            var list = new List<Command>();
            foreach (var p in _pages)
                list.Add(new Command { Icon = p.Icon, Title = p.Title, Subtitle = Loc.S("Ir a la página", "Go to page"), Run = () => Navigate(p.Key) });
            foreach (var l in Levels.All)
                list.Add(new Command { Icon = Icons.Play, Title = Loc.S("Aplicar el nivel " + l.Name.Es, "Apply the " + l.Name.En + " level"), Subtitle = Loc.S(l.Tagline), Run = () => PageOf<LevelsPage>().OpenLevel(l.Level) });
            list.Add(new Command { Icon = Icons.Undo, Title = Loc.S("Deshacer la última sesión", "Undo the last session"), Subtitle = Loc.S("Historial", "History"), Run = () => { Navigate("home"); PageOf<HomePage>().UndoLast(); } });
            list.Add(new Command { Icon = Icons.FullScreen, Title = Loc.S("Pantalla completa", "Full screen"), Subtitle = "F11", Run = ToggleFullScreen });
            list.Add(new Command { Icon = Icons.Menu, Title = Loc.S("Contraer o expandir el menú", "Collapse or expand the menu"), Subtitle = Loc.S("Más espacio para el contenido", "More room for content"), Run = ToggleNavCollapsed });
            list.Add(new Command { Icon = Icons.Theme, Title = Loc.S("Cambiar tema", "Change theme"), Subtitle = Loc.S("Ajustes", "Settings"), Run = () => Navigate("settings") });
            list.Add(new Command { Icon = Icons.Save, Title = Loc.S("Exportar o importar una selección", "Export or import a selection"), Subtitle = Loc.S("Personalizar", "Customize"), Run = () => PageOf<WizardPage>().OpenSelection() });
            foreach (var t in _state.Catalog)
                list.Add(new Command { Icon = Icons.For(t.Category), Title = Loc.S(t.Title), Subtitle = Names.Of(t.Category) + " · " + Names.Of(t.Risk), Run = () => PageOf<WizardPage>().OpenSelection(Loc.S(t.Title)) });
            return list;
        }

        public void ShowPalette()
        {
            if (OverlayOpen) return;
            _commands = _commands ?? Commands();
            var results = new StackPanel();
            var buttons = new List<Button>();
            var empty = Ui.Empty(Loc.S("Nada coincide. Prueba con otra palabra.", "Nothing matches. Try another word."));
            void Fill(string q)
            {
                results.Children.Clear();
                buttons.Clear();
                var matches = _commands.Where(c => q.Length == 0 || c.Title.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0
                                                   || c.Subtitle.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0).Take(8).ToList();
                foreach (var c in matches)
                {
                    var g = new Grid();
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g.ColumnDefinitions.Add(new ColumnDefinition());
                    var info = Ui.Stack(0, Ui.Text(c.Title, 14, false, "Fg", wrap: false), Ui.Text(c.Subtitle, 12, false, "FgMuted", wrap: false));
                    info.Margin = new Thickness(12, 0, 0, 0);
                    Grid.SetColumn(info, 1);
                    g.Children.Add(Ui.Icon(c.Icon, 16, "FgMuted"));
                    g.Children.Add(info);
                    var b = new Button { Content = g, Padding = new Thickness(8, 4, 8, 4) };
                    b.SetResourceReference(StyleProperty, "RowButton");
                    AutomationProperties.SetName(b, c.Title);
                    var cmd = c;
                    b.Click += (_, __) => { CloseOverlay(); cmd.Run(); };
                    buttons.Add(b);
                    results.Children.Add(b);
                }
                if (matches.Count == 0) results.Children.Add(empty);
            }
            var search = Ui.SearchBox(Loc.S("Busca una página, un nivel o un ajuste", "Search a page, a level or a tweak"), Fill);
            search.Box.PreviewKeyDown += (_, e) =>
            {
                if ((e.Key == Key.Down || e.Key == Key.Enter) && buttons.Count > 0)
                {
                    if (e.Key == Key.Enter) { buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                    else buttons[0].Focus();
                    e.Handled = true;
                }
            };
            Fill("");
            var hint = Ui.Muted(Loc.S("Enter abre el primero · Flechas para moverte · Esc para cerrar", "Enter opens the first · Arrows to move · Esc to close"), 12);
            var card = Sheet(search, results, hint, 16);
            AutomationProperties.SetName(card, Loc.S("Buscar", "Search"));
            ShowOverlay(card, CloseOverlay, 620, top: true);
            Dispatcher.BeginInvoke(new Action(() => search.Box.Focus()), DispatcherPriority.Input);
        }

        // ---------- First-run tour ----------

        public void ShowTour()
        {
            var steps = new[]
            {
                (Icons.Wizard, Loc.S("Primero se analiza tu equipo", "First, your PC is scanned"),
                 Loc.S("Optimizzzer solo lee al principio: qué Windows tienes, tu hardware y qué ajustes ya están aplicados. No cambia nada sin tu confirmación.",
                       "Optimizzzer only reads at first: your Windows, your hardware and which tweaks are already applied. Nothing changes without your confirmation.")),
                (Icons.Performance, Loc.S("Elige un nivel o ve ajuste por ajuste", "Pick a level or go tweak by tweak"),
                 Loc.S("En Inicio hay 4 niveles, de Rápido a Máximo rendimiento. Antes de aplicar ves exactamente qué va a cambiar y qué pierdes. En Personalizar eliges cada ajuste.",
                       "Home has 4 levels, from Quick to Maximum performance. Before applying you see exactly what will change and what you lose. In Customize you pick every tweak.")),
                (Icons.Undo, Loc.S("Los ajustes se pueden deshacer", "Settings can be undone"),
                 Loc.S("Se crea un punto de restauración y se guarda el valor anterior de cada ajuste. En Historial deshaces uno o una sesión entera. Las apps que quites se vuelven a instalar desde la Store. Ctrl+K busca cualquier cosa.",
                       "A restore point is created and the previous value of every setting is saved. In History you undo one or a whole session. Apps you remove are reinstalled from the Store. Ctrl+K searches anything.")),
            };
            int i = 0;
            void Close() { CloseOverlay(); _state.TourDone = true; _state.SaveSettings(); }
            void Render()
            {
                var (icon, title, text) = steps[i];
                var dots = Ui.Text((i + 1) + " / " + steps.Length, 12, false, "FgMuted", wrap: false);
                var next = Ui.Primary(i < steps.Length - 1 ? Loc.S("Siguiente", "Next") : Loc.S("Empezar", "Start"), () =>
                {
                    if (i < steps.Length - 1) { i++; Render(); } else Close();
                });
                var skip = Ui.Subtle(Loc.S("Saltar", "Skip"), Close);
                var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
                footer.ColumnDefinitions.Add(new ColumnDefinition());
                footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var right = Ui.Row(8, skip, next);
                Grid.SetColumn(right, 1);
                dots.VerticalAlignment = VerticalAlignment.Center;
                footer.Children.Add(dots);
                footer.Children.Add(right);
                var ic = Ui.Icon(icon, 32, "AccentText");
                ic.HorizontalAlignment = HorizontalAlignment.Left;
                var card = Sheet(Ui.Stack(12, ic, Ui.H2(title)), Ui.Text(text), footer);
                AutomationProperties.SetName(card, title);
                ShowOverlay(card, Close, 520);
                Dispatcher.BeginInvoke(new Action(() => next.Focus()), DispatcherPriority.Input);
            }
            Render();
        }
    }
}
