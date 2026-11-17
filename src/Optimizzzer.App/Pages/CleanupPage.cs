using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.App.Pages
{
    public sealed class CleanupPage : AppPage
    {
        private readonly CleanupEngine _engine = new CleanupEngine();
        private readonly Dictionary<string, CleanupEstimate> _estimates = new Dictionary<string, CleanupEstimate>();
        private readonly HashSet<string> _selected = new HashSet<string>(CleanupCatalog.All.Where(c => c.SelectedByDefault).Select(c => c.Id));
        private bool _measuring, _cleaning;
        private List<CleanupResult> _results;

        public CleanupPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "cleanup";
        public override string Title => Loc.S("Limpieza", "Cleanup");
        public override string Icon => Icons.Cleanup;

        public override void OnShown()
        {
            if (_estimates.Count == 0 && !_measuring) _ = MeasureAsync();
        }

        private async Task MeasureAsync()
        {
            _measuring = true;
            _results = null;
            W.RefreshCurrentIf(this);
            try
            {
                foreach (var t in CleanupCatalog.All)
                {
                    var est = await Task.Run(() => _engine.Estimate(t, S.Win.Cleanup));
                    _estimates[t.Id] = est;
                }
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
            finally { _measuring = false; W.RefreshCurrentIf(this); }
        }

        protected override FrameworkElement Build()
        {
            if (_measuring)
            {
                var (view, _) = Ui.Loading(Loc.S("Calculando cuánto espacio se puede liberar…", "Working out how much space can be freed…"));
                return Frame(null, view);
            }
            if (_cleaning)
            {
                var (view, _) = Ui.Loading(Loc.S("Limpiando… Los archivos en uso se saltan, nunca se fuerzan.", "Cleaning… Files in use are skipped, never forced."));
                return Frame(null, view);
            }

            var list = new StackPanel();
            if (_results != null)
            {
                long freed = _results.Sum(r => r.FreedBytes);
                int skipped = _results.Sum(r => r.Skipped);
                var done = Ui.Panel(Ui.Stack(8,
                    Ui.Notice(Icons.CheckCircle, "Success", Loc.S("Liberado: ", "Freed: ") + Loc.Size(freed)),
                    skipped > 0 ? Ui.Notice(Icons.Info, "FgMuted", skipped + Loc.S(" archivos estaban en uso y se dejaron.", " files were in use and were left alone.")) : null,
                    _results.Any(r => r.Error != null) ? Ui.Notice(Icons.Error, "Advanced", string.Join(" · ", _results.Where(r => r.Error != null).Select(r => Loc.S(r.Target.Title) + ": " + r.Error))) : null));
                done.Margin = new Thickness(0, 0, 0, 16);
                list.Children.Add(done);
            }

            Button go = null;
            void Update()
            {
                long total = CleanupCatalog.All.Where(t => _selected.Contains(t.Id) && _estimates.TryGetValue(t.Id, out var e) && e.Measurable).Sum(t => _estimates[t.Id].Bytes);
                bool unmeasured = CleanupCatalog.All.Any(t => _selected.Contains(t.Id) && (!_estimates.TryGetValue(t.Id, out var e) || !e.Measurable));
                go.IsEnabled = _selected.Count > 0;
                go.Content = Loc.S("Liberar ", "Free ") + Loc.Size(total) + (unmeasured ? Loc.S(" o más", " or more") : "");
                AutomationProperties.SetName(go, go.Content.ToString());
            }

            foreach (var t in CleanupCatalog.All)
            {
                _estimates.TryGetValue(t.Id, out var est);
                string size = est == null ? "" : est.Measurable ? Loc.Size(est.Bytes) : Loc.S("Se sabe al terminar", "Known when finished");
                var title = Ui.Row(12, Ui.Text(Loc.S(t.Title), 14, true), t.Risk != Risk.Safe ? Ui.RiskMarker(t.Risk) : null);
                var content = Ui.Stack(2, title, Ui.Muted(Loc.S(t.Description)));
                var cb = new CheckBox { IsChecked = _selected.Contains(t.Id), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -6, 0, 0) };
                AutomationProperties.SetName(cb, Loc.S(t.Title) + ", " + size);
                cb.Checked += (_, __) => { _selected.Add(t.Id); Update(); };
                cb.Unchecked += (_, __) => { _selected.Remove(t.Id); Update(); };
                var sizeText = Ui.Text(size, 14, true, "Fg", wrap: false);
                sizeText.VerticalAlignment = VerticalAlignment.Top;
                list.Children.Add(Row(cb, content, sizeText));
            }

            go = Ui.Primary("", async () =>
            {
                var chosen = CleanupCatalog.All.Where(t => _selected.Contains(t.Id)).ToList();
                if (chosen.Any(t => t.Kind == CleanupKind.RecycleBin || t.Kind == CleanupKind.ComponentStore) &&
                    !await W.Confirm(Loc.S("¿Seguir?", "Continue?"),
                        Loc.S("Vaciar la papelera y limpiar componentes de Windows no se puede deshacer.", "Emptying the Recycle Bin and cleaning Windows components cannot be undone."),
                        Loc.S("Limpiar", "Clean"))) return;
                _cleaning = true;
                W.RefreshCurrentIf(this);
                var results = new List<CleanupResult>();
                try
                {
                    foreach (var t in chosen) results.Add(await Task.Run(() => _engine.Run(t, S.Win.Cleanup)));
                }
                catch (Exception ex) { W.ShowError(ex.Message); }
                _cleaning = false;
                _results = results;
                _estimates.Clear();
                await MeasureAsync();
            }, Icons.Cleanup);
            Update();
            var footer = Footer(Ui.Secondary(Loc.S("Volver a calcular", "Recalculate"), () => _ = MeasureAsync(), Icons.Restart), go);
            return Frame(Loc.S("Solo se borran archivos temporales y cachés que Windows y los programas vuelven a crear. Tus documentos no se tocan.",
                "Only temporary files and caches that Windows and programs recreate are deleted. Your documents are not touched."), list, footer);
        }

        internal static Border Row(UIElement left, UIElement middle, UIElement right)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (left != null) g.Children.Add(left);
            Grid.SetColumn(middle, 1);
            g.Children.Add(middle);
            if (right != null)
            {
                if (right is FrameworkElement fe)
                {
                    fe.Margin = new Thickness(16, 0, 0, 0);
                    // Controls keep their natural height instead of stretching to a tall row.
                    fe.VerticalAlignment = VerticalAlignment.Top;
                }
                Grid.SetColumn(right, 2);
                g.Children.Add(right);
            }
            var b = new Border { Child = g, Padding = new Thickness(4, 12, 4, 12), BorderThickness = new Thickness(0, 0, 0, 1) };
            b.SetResourceReference(Border.BorderBrushProperty, "Divider");
            return b;
        }
    }
}
