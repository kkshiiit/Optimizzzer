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
        private IInputElement _focusBeforeOverlay;

        public MainWindow(AppState state)
        {
            _state = state;
            Title = "Optimizzzer";
            Width = 1200; Height = 800; MinWidth = 920; MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            SetResourceReference(BackgroundProperty, "Bg");
            SetResourceReference(FontFamilyProperty, "UiFont");
            SetResourceReference(ForegroundProperty, "Fg");
            FontSize = 14;

            _pages.Add(new HomePage(state, this));
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

            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(244) });
            _root.ColumnDefinitions.Add(new ColumnDefinition());
            _root.Children.Add(BuildNav());

            // The content surface floats above the window background: the first thing the eye lands on.
            var contentGrid = new Grid();
            contentGrid.Children.Add(_host);
            contentGrid.Children.Add(_toasts);
            _content = new Border { CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 12, 12, 12), Child = contentGrid, BorderThickness = new Thickness(1) };
            _content.SetResourceReference(Border.BackgroundProperty, "Surface");
            _content.SetResourceReference(Border.BorderBrushProperty, "Divider");
            Theme.Elevate(_content);
            Grid.SetColumn(_content, 1);
            _root.Children.Add(_content);

            Grid.SetColumnSpan(_overlay, 2);
            _root.Children.Add(_overlay);
            Content = _root;

            PreviewKeyDown += OnKey;
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

        private FrameworkElement BuildNav()
        {
            var g = new Grid { Margin = new Thickness(12, 20, 12, 12) };
            AutomationProperties.SetName(g, Loc.S("Navegación", "Navigation"));
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var brand = Ui.Text("Optimizzzer", 20, true);
            brand.Margin = new Thickness(12, 0, 0, 16);
            g.Children.Add(brand);

            _nav = new StackPanel();
            var scroll = Ui.Scroll(_nav);
            Grid.SetRow(scroll, 1);
            g.Children.Add(scroll);

            _navBottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(_navBottom, 2);
            g.Children.Add(_navBottom);
            BuildNavItems();
            return g;
        }

        private void BuildNavItems()
        {
            _nav.Children.Clear();
            _navBottom.Children.Clear();
            _navButtons.Clear();
            void Section(string text)
            {
                var t = Ui.Muted(text, 12);
                t.Margin = new Thickness(12, _nav.Children.Count == 0 ? 0 : 16, 0, 6);
                _nav.Children.Add(t);
            }
            AddNav(_nav, "home");
            AddNav(_nav, "wizard");
            Section(Loc.S("Herramientas", "Tools"));
            foreach (var k in new[] { "cleanup", "repair", "dns", "startup", "services", "tasks", "apps" }) AddNav(_nav, k);
            Section(Loc.S("Seguimiento", "Tracking"));
            foreach (var k in new[] { "history", "drift", "measure" }) AddNav(_nav, k);

            var search = NavAction(Icons.Search, Loc.S("Buscar", "Search"), ShowPalette, Loc.S("Buscar (Ctrl+K)", "Search (Ctrl+K)"), "Ctrl+K");
            _navBottom.Children.Add(search);
            AddNav(_navBottom, "settings");
            AddNav(_navBottom, "help");
        }

        private void AddNav(Panel panel, string key)
        {
            var page = _pages.First(p => p.Key == key);
            var b = NavAction(page.Icon, page.Title, () => Navigate(key));
            _navButtons[key] = b;
            if (_current?.Key == key) MarkCurrent(b);
            panel.Children.Add(b);
        }

        private static Button NavAction(string icon, string text, Action onClick, string name = null, string hint = null)
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
            var b = new Button { Content = g, Margin = new Thickness(0, 1, 0, 1) };
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
            foreach (var p in _pages.Where(p => (p is HomePage || p is WizardPage) && p != except))
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
            card.MaxWidth = maxWidth;
            card.Margin = new Thickness(24, top ? 72 : 24, 24, 24);
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

        public void CloseOverlay()
        {
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
            buttons.Margin = new Thickness(0, 12, 0, 0);
            var card = Card(Ui.Stack(12, Ui.H2(title), Ui.Text(message), buttons));
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
                list.Add(new Command { Icon = Icons.Play, Title = Loc.S("Aplicar nivel ", "Apply level ") + Loc.S(l.Name), Subtitle = Loc.S(l.Tagline), Run = () => { Navigate("home"); PageOf<HomePage>().OpenLevel(l.Level); } });
            list.Add(new Command { Icon = Icons.Undo, Title = Loc.S("Deshacer la última sesión", "Undo the last session"), Subtitle = Loc.S("Historial", "History"), Run = () => { Navigate("home"); PageOf<HomePage>().UndoLast(); } });
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
            var card = Card(Ui.Stack(12, search, results, hint), 16);
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
                var card = Card(Ui.Stack(12, ic, Ui.H2(title), Ui.Text(text), footer));
                AutomationProperties.SetName(card, title);
                ShowOverlay(card, Close, 520);
                Dispatcher.BeginInvoke(new Action(() => next.Focus()), DispatcherPriority.Input);
            }
            Render();
        }
    }
}
