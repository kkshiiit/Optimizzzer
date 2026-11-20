using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;

namespace Optimizzzer.App.Pages
{
    /// <summary>
    /// One tweak in the selection list: checkbox, title, risk marker, summary and a details toggle.
    /// Details are built on first expand to keep the list fast.
    /// </summary>
    public sealed class TweakRow : Border
    {
        private readonly Tweak _t;
        private readonly CheckBox _check;
        private readonly Button _expand;
        private readonly ContentControl _details = new ContentControl { Visibility = Visibility.Collapsed, Focusable = false };
        private readonly Action<bool> _onChange;

        public bool CanSelect { get; }

        public TweakRow(Tweak t, TweakStatus status, bool selected, Action<bool> onChange)
        {
            _t = t;
            _onChange = onChange;
            CanSelect = status == TweakStatus.NotApplied || status == TweakStatus.Partial;
            BorderThickness = new Thickness(0, 0, 0, 1);
            SetResourceReference(BorderBrushProperty, "Divider");
            Padding = new Thickness(4, 12, 4, 12);

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _check = new CheckBox { IsChecked = selected && CanSelect, IsEnabled = CanSelect, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -6, 0, 0) };
            AutomationProperties.SetName(_check, Loc.S(t.Title));
            AutomationProperties.SetHelpText(_check, Loc.S(t.Summary));
            _check.Checked += (_, __) => _onChange(true);
            _check.Unchecked += (_, __) => _onChange(false);
            g.Children.Add(_check);

            var title = Ui.Text(Loc.S(t.Title), 14, true);
            var titleRow = new WrapPanel();
            title.Margin = new Thickness(0, 0, 12, 0);
            titleRow.Children.Add(title);
            titleRow.Children.Add(Ui.RiskMarker(t.Risk));
            var statusText = Names.Of(status);
            // Three plain facts so nobody has to open the details to know what kind of change this is.
            var chips = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            string impact = t.Impact == Impact.High ? Loc.S("Impacto alto", "High impact") : t.Impact == Impact.Medium ? Loc.S("Impacto medio", "Medium impact") : Loc.S("Impacto bajo", "Low impact");
            chips.Children.Add(Ui.Chip(Icons.Performance, impact, "FgMuted", Loc.S("Cuánto se suele notar. Es una estimación, no una medición.", "How noticeable it usually is. An estimate, not a measurement.")));
            chips.Children.Add(t.Reversible
                ? Ui.Chip(Icons.Undo, Loc.S("Se puede deshacer", "Can be undone"))
                : Ui.Chip(Icons.Error, Loc.S("No se deshace", "Cannot be undone"), "Advanced"));
            if (t.Restart != RestartKind.None)
                chips.Children.Add(Ui.Chip(Icons.Restart, t.Restart == RestartKind.Reboot ? Loc.S("Reiniciar equipo", "Restart PC") : t.Restart == RestartKind.SignOut ? Loc.S("Cerrar sesión", "Sign out") : Loc.S("Reinicia el Explorador", "Restarts Explorer")));
            var info = Ui.Stack(4, titleRow, Ui.Muted(Loc.S(t.Summary)), chips,
                statusText.Length > 0 ? Ui.Text(statusText, 12, true, status == TweakStatus.Applied ? "Success" : "FgMuted") : null);
            // Clicking the text toggles the checkbox, like a label.
            info.Background = Brushes.Transparent;
            info.Cursor = CanSelect ? System.Windows.Input.Cursors.Hand : null;
            info.MouseLeftButtonUp += (_, __) => { if (CanSelect) _check.IsChecked = _check.IsChecked != true; };
            Grid.SetColumn(info, 1);
            g.Children.Add(info);

            _expand = Ui.Subtle("", ToggleDetails, Icons.ChevronDown, Loc.S("Ver detalles de ", "Show details for ") + Loc.S(t.Title));
            _expand.Width = 40; _expand.Height = 40; _expand.Padding = new Thickness(0);
            _expand.VerticalAlignment = VerticalAlignment.Top;
            AutomationProperties.SetItemStatus(_expand, "collapsed");
            Grid.SetColumn(_expand, 2);
            g.Children.Add(_expand);

            Grid.SetColumn(_details, 1);
            Grid.SetRow(_details, 1);
            g.Children.Add(_details);
            Child = g;
        }

        public void SetChecked(bool on)
        {
            if (!CanSelect) return;
            _check.IsChecked = on;
        }

        private void ToggleDetails()
        {
            bool open = _details.Visibility != Visibility.Visible;
            if (open && _details.Content == null) _details.Content = BuildDetails();
            _details.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            ((TextBlock)((StackPanel)_expand.Content).Children[0]).Text = open ? Icons.ChevronUp : Icons.ChevronDown;
            AutomationProperties.SetName(_expand, (open ? Loc.S("Ocultar detalles de ", "Hide details for ") : Loc.S("Ver detalles de ", "Show details for ")) + Loc.S(_t.Title));
            AutomationProperties.SetItemStatus(_expand, open ? "expanded" : "collapsed");
        }

        private FrameworkElement BuildDetails()
        {
            var actions = new StackPanel();
            foreach (var a in _t.Actions)
            {
                var line = Ui.Text(a.Describe(Loc.Lang), 12, false, "FgMuted");
                line.FontFamily = new FontFamily("Consolas");
                actions.Children.Add(line);
            }

            var p = Ui.Stack(12,
                Section(Loc.S("Por qué", "Why"), Ui.Text(Loc.S(_t.Why))),
                _t.Downside.Es.Length > 0 ? Section(Loc.S("Qué pierdes", "What you lose"), Ui.Text(Loc.S(_t.Downside))) : null,
                Section(Loc.S("Qué cambia exactamente", "What exactly changes"), actions),
                _t.Restart != RestartKind.None ? Ui.Notice(Icons.Restart, "FgMuted", Names.Of(_t.Restart) + ".") : null,
                !_t.Reversible ? Ui.Notice(Icons.Error, "Advanced", Loc.S("No se puede deshacer desde Optimizzzer.", "Cannot be undone from Optimizzzer.")) : null,
                Ui.Muted(Loc.S("Fuente: ", "Source: ") + _t.Source, 12));
            p.Margin = new Thickness(0, 12, 0, 4);
            return p;
        }

        private static FrameworkElement Section(string heading, UIElement body) =>
            Ui.Stack(2, Ui.Text(heading, 12, true, "FgMuted"), body);
    }
}
