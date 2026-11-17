using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Optimizzzer.App.Pages;
using Optimizzzer.Core;

namespace Optimizzzer.App.Shell
{
    public sealed class MainWindow : Window
    {
        private readonly AppState _state;
        private readonly List<AppPage> _pages = new List<AppPage>();
        private readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();
        private readonly ContentControl _host = new ContentControl { Focusable = false };
        private readonly Grid _overlay = new Grid { Visibility = Visibility.Collapsed };
        private readonly Grid _root = new Grid();
        private StackPanel _nav;
        private AppPage _current;
        private TaskCompletionSource<bool> _dialog;
        private IInputElement _focusBeforeDialog;

        public MainWindow(AppState state)
        {
            _state = state;
            Title = "Optimizzzer";
            Width = 1180; Height = 780; MinWidth = 900; MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);
            SetResourceReference(BackgroundProperty, "Bg");
            SetResourceReference(FontFamilyProperty, "UiFont");
            SetResourceReference(ForegroundProperty, "Fg");
            FontSize = 14;

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

            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(256) });
            _root.ColumnDefinitions.Add(new ColumnDefinition());
            var navHost = BuildNav();
            _root.Children.Add(navHost);
            Grid.SetColumn(_host, 1);
            _root.Children.Add(_host);
            Grid.SetColumnSpan(_overlay, 2);
            _root.Children.Add(_overlay);
            Content = _root;

            PreviewKeyDown += OnKey;
            Navigate("wizard");
        }

        public IReadOnlyList<AppPage> Pages => _pages;

        /// <summary>What is on screen right now (used by the self-test).</summary>
        public FrameworkElement CurrentView => _host.Content as FrameworkElement;

        private FrameworkElement BuildNav()
        {
            var border = new Border { BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(12, 20, 12, 12) };
            border.SetResourceReference(Border.BackgroundProperty, "Bg");
            border.SetResourceReference(Border.BorderBrushProperty, "Divider");
            AutomationProperties.SetName(border, Loc.S("Navegación", "Navigation"));

            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var brand = Ui.Text("Optimizzzer", 20, true);
            brand.Margin = new Thickness(12, 0, 0, 20);
            g.Children.Add(brand);

            _nav = new StackPanel();
            var scroll = Ui.Scroll(_nav);
            Grid.SetRow(scroll, 1);
            g.Children.Add(scroll);

            var bottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            Grid.SetRow(bottom, 2);
            g.Children.Add(bottom);
            BuildNavItems(bottom);

            border.Child = g;
            return border;
        }

        private void BuildNavItems(StackPanel bottom)
        {
            _nav.Children.Clear();
            _navButtons.Clear();
            void Section(string text)
            {
                var t = Ui.Muted(text, 12);
                t.Margin = new Thickness(12, _nav.Children.Count == 0 ? 0 : 16, 0, 6);
                _nav.Children.Add(t);
            }
            Section(Loc.S("Optimizar", "Optimize"));
            AddNav("wizard");
            Section(Loc.S("Herramientas", "Tools"));
            foreach (var k in new[] { "cleanup", "repair", "dns", "startup", "services", "tasks", "apps" }) AddNav(k);
            Section(Loc.S("Seguimiento", "Tracking"));
            foreach (var k in new[] { "history", "drift", "measure" }) AddNav(k);

            bottom.Children.Clear();
            bottom.Children.Add(NavAction(Icons.Theme, ThemeLabel(), CycleTheme));
            bottom.Children.Add(NavAction(Icons.Language, Loc.Lang == Lang.Es ? "English" : "Español", ToggleLanguage,
                Loc.S("Cambiar idioma a inglés", "Switch language to Spanish")));
        }

        private string ThemeLabel()
        {
            switch (Theme.Choice)
            {
                case ThemeChoice.Light: return Loc.S("Tema: claro", "Theme: light");
                case ThemeChoice.Dark: return Loc.S("Tema: oscuro", "Theme: dark");
                default: return Loc.S("Tema: el del sistema", "Theme: system");
            }
        }

        private void AddNav(string key)
        {
            var page = _pages.First(p => p.Key == key);
            var b = NavAction(page.Icon, page.Title, () => Navigate(key));
            _navButtons[key] = b;
            if (_current?.Key == key) MarkCurrent(b);
            _nav.Children.Add(b);
        }

        private static Button NavAction(string icon, string text, Action onClick, string name = null)
        {
            var row = Ui.Row(12, Ui.Icon(icon, 16), Ui.Text(text, 14, false, "Fg", wrap: false));
            var b = new Button { Content = row, Margin = new Thickness(0, 1, 0, 1) };
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
            foreach (var kv in _navButtons)
            {
                kv.Value.Tag = null;
                AutomationProperties.SetItemStatus(kv.Value, "");
            }
            if (_navButtons.TryGetValue(key, out var nb)) MarkCurrent(nb);
            _current = page;
            ShowView(page.View);
            page.OnShown();
        }

        /// <summary>Replaces the content area with a short fade (skipped when Windows animations are off).</summary>
        public void ShowView(FrameworkElement view)
        {
            _host.Content = view;
            if (!Theme.AnimationsEnabled) return;
            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(120));
            view.BeginAnimation(OpacityProperty, fade);
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

        private void CycleTheme()
        {
            var next = Theme.Choice == ThemeChoice.System ? ThemeChoice.Light : Theme.Choice == ThemeChoice.Light ? ThemeChoice.Dark : ThemeChoice.System;
            Theme.Apply(next);
            _state.SaveSettings();
            RebuildChrome();
        }

        private void ToggleLanguage()
        {
            Loc.Lang = Loc.Lang == Lang.Es ? Lang.En : Lang.Es;
            _state.SaveSettings();
            foreach (var p in _pages) p.Invalidate();
            RebuildChrome();
            if (_current != null) { ShowView(_current.View); _current.OnShown(); }
        }

        private void RebuildChrome()
        {
            var navBorder = (Border)_root.Children[0];
            var grid = (Grid)navBorder.Child;
            BuildNavItems((StackPanel)grid.Children[2]);
            if (_current != null && _navButtons.TryGetValue(_current.Key, out var b)) MarkCurrent(b);
        }

        // ---------- Dialogs ----------

        /// <summary>Modal confirmation inside the window. Escape or the secondary button cancels.</summary>
        public Task<bool> Confirm(string title, string message, string confirmText, bool danger = false, string cancelText = null)
        {
            _dialog?.TrySetResult(false);
            _dialog = new TaskCompletionSource<bool>();
            _focusBeforeDialog = Keyboard.FocusedElement;

            var confirm = Ui.Primary(confirmText, () => CloseDialog(true));
            if (danger) confirm.SetResourceReference(BackgroundProperty, "Advanced");
            if (danger) confirm.SetResourceReference(BorderBrushProperty, "Advanced");
            if (danger) confirm.SetResourceReference(ForegroundProperty, "Surface");
            var cancel = Ui.Secondary(cancelText ?? Loc.S("Cancelar", "Cancel"), () => CloseDialog(false));
            var buttons = Ui.Row(8, cancel, confirm);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;

            var body = Ui.Stack(12, Ui.H2(title), Ui.Text(message), buttons);
            buttons.Margin = new Thickness(0, 12, 0, 0);
            var card = Ui.Panel(body);
            card.MaxWidth = 520;
            card.Margin = new Thickness(24);
            card.HorizontalAlignment = HorizontalAlignment.Center;
            card.VerticalAlignment = VerticalAlignment.Center;
            AutomationProperties.SetName(card, title);

            var shade = new Border();
            shade.SetResourceReference(Border.BackgroundProperty, "Overlay");
            shade.MouseLeftButtonDown += (_, __) => CloseDialog(false);
            _overlay.Children.Clear();
            _overlay.Children.Add(shade);
            _overlay.Children.Add(card);
            _overlay.Visibility = Visibility.Visible;
            KeyboardNavigation.SetTabNavigation(_overlay, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetTabNavigation(_host, KeyboardNavigationMode.None);
            _host.IsEnabled = false;
            Dispatcher.BeginInvoke(new Action(() => (danger ? cancel : confirm).Focus()), System.Windows.Threading.DispatcherPriority.Input);
            return _dialog.Task;
        }

        public Task Alert(string title, string message) => Confirm(title, message, Loc.S("Entendido", "OK"), cancelText: Loc.S("Cerrar", "Close"));

        public void ShowError(string message) => _ = Alert(Loc.S("Algo salió mal", "Something went wrong"), message);

        private void CloseDialog(bool result)
        {
            _overlay.Visibility = Visibility.Collapsed;
            _overlay.Children.Clear();
            _host.IsEnabled = true;
            KeyboardNavigation.SetTabNavigation(_host, KeyboardNavigationMode.Continue);
            (_focusBeforeDialog as IInputElement)?.Focus();
            _dialog?.TrySetResult(result);
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _overlay.Visibility == Visibility.Visible)
            {
                CloseDialog(false);
                e.Handled = true;
            }
        }
    }
}
