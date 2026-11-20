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
        // "What takes the most space": read only, started on demand because it walks the whole drive.
        private IList<(string Path, long Bytes)> _largest;
        private bool _sizing;
        private string _sizingNow;
        private Action<double, string> _sizingUpdate;
        private System.Threading.CancellationTokenSource _sizingCts;

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
                var content = Ui.Stack(2, title, Ui.Muted(Loc.S(t.Description)), t.Details != null ? Ui.MoreInfo((null, Loc.S(t.Details))) : null);
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
                "Only temporary files and caches that Windows and programs recreate are deleted. Your documents are not touched."), Ui.Stack(0, list, LargestSection()), footer);
        }

        private FrameworkElement LargestSection()
        {
            var head = Ui.H2(Loc.S("Qué ocupa más", "What takes the most space"));
            var intro = Ui.Muted(Loc.S("Las 20 carpetas más grandes del disco del sistema. Solo se mira: no se borra nada. Útil para encontrar juegos, descargas o copias que ya no usas. Revisarlo todo puede tardar varios minutos.",
                "The 20 largest folders on the system drive. Look only: nothing is deleted. Useful to find games, downloads or copies you no longer use. Going through everything can take a few minutes."), 12);
            var box = Ui.Stack(8, head, intro);
            box.Margin = new Thickness(0, 28, 0, 0);
            if (_sizing)
            {
                var (v, upd) = Ui.Loading(Loc.S("Revisando ", "Checking ") + (_sizingNow ?? "…"));
                _sizingUpdate = upd;
                box.Children.Add(v);
                box.Children.Add(Ui.Secondary(Loc.S("Detener", "Stop"), () => _sizingCts?.Cancel(), Icons.Stop));
                return box;
            }
            if (_largest != null)
            {
                foreach (var (path, bytes) in _largest)
                {
                    var open = Ui.Subtle(Loc.S("Abrir", "Open"), () =>
                    {
                        try { System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\""); } catch (Exception ex) { W.ShowError(ex.Message); }
                    }, Icons.Folder, Loc.S("Abrir ", "Open ") + path);
                    var size = Ui.Text(Loc.Size(bytes), 14, true, "Fg", wrap: false);
                    size.VerticalAlignment = VerticalAlignment.Center;
                    var right = Ui.Row(8, size, open);
                    box.Children.Add(Row(Ui.Icon(Icons.Folder, 16, "FgMuted"), Ui.Text(path, 14), right));
                }
            }
            var go = Ui.Secondary(_largest == null ? Loc.S("Ver qué ocupa más", "See what takes the most space") : Loc.S("Volver a revisar", "Check again"), async () =>
            {
                _sizing = true; _sizingCts = new System.Threading.CancellationTokenSource();
                W.RefreshCurrentIf(this);
                // Only the status line changes while walking the drive; the page is not rebuilt (it would jump to the top).
                var progress = new Progress<string>(p => { _sizingNow = p; _sizingUpdate?.Invoke(0, Loc.S("Revisando ", "Checking ") + p); });
                var drive = System.IO.Path.GetPathRoot(Environment.SystemDirectory);
                try { _largest = await Task.Run(() => FolderSizes.Largest(drive, 3, 20, _sizingCts.Token, progress)); }
                catch (OperationCanceledException) { }
                catch (Exception ex) { W.ShowError(ex.Message); }
                _sizing = false; _sizingNow = null;
                W.RefreshCurrentIf(this);
            }, Icons.Search);
            go.HorizontalAlignment = HorizontalAlignment.Left;
            box.Children.Add(go);
            return box;
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
