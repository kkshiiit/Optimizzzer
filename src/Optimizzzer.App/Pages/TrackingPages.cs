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
using Optimizzzer.Windows;

namespace Optimizzzer.App.Pages
{
    public sealed class HistoryPage : AppPage
    {
        private IList<JournalSession> _sessions;
        private string _error;
        private bool _busy;

        public HistoryPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "history";
        public override string Title => Loc.S("Historial y deshacer", "History and undo");
        public override string Icon => Icons.History;

        public override void OnShown() { _ = LoadAsync(); }

        private async Task LoadAsync()
        {
            try
            {
                await S.EnsureReadyAsync();
                _sessions = await Task.Run(() => S.Win.Journal.LoadAll().Reverse().ToList());
                _error = null;
            }
            catch (Exception ex) { _error = ex.Message; }
            Invalidate();
            W.RefreshCurrentIf(this);
        }

        protected override FrameworkElement Build()
        {
            const string intro = null;
            if (_error != null) return Frame(intro, Ui.ErrorState(Loc.S("No se pudo leer el historial.", "History could not be read."), _error, () => _ = LoadAsync()));
            if (_sessions == null || _busy)
            {
                var (v, _) = Ui.Loading(_busy ? Loc.S("Deshaciendo…", "Undoing…") : Loc.S("Leyendo el historial…", "Reading history…"));
                return Frame(intro, v);
            }
            var visible = _sessions.Where(s => s.Entries.Any(e => e.ResultKind != EntryResult.Skipped)).ToList();
            if (visible.Count == 0)
                return Frame(intro, Ui.Empty(Loc.S("Todavía no has aplicado ningún cambio con Optimizzzer.", "You have not applied any change with Optimizzzer yet.")));

            var body = new StackPanel();
            // Read the journal once for the whole page, not once per row.
            var undoCatalog = S.UndoCatalog();
            foreach (var s in visible)
            {
                var entries = s.Entries.Where(e => e.ResultKind != EntryResult.Skipped).ToList();
                bool anyOpen = entries.Any(e => e.ResultKind == EntryResult.Applied && !e.Reverted
                    && (undoCatalog.FirstOrDefault(x => x.Id == e.TweakId)?.Reversible ?? true));
                var header = new Grid { Margin = new Thickness(0, 16, 0, 8) };
                header.ColumnDefinitions.Add(new ColumnDefinition());
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                header.Children.Add(Ui.Stack(2,
                    Ui.H3(s.Started.ToLocalTime().ToString("f", Loc.Lang == Lang.En ? new System.Globalization.CultureInfo("en-US") : new System.Globalization.CultureInfo("es-ES"))),
                    Ui.Muted(s.RestorePointCreated ? Loc.S("Con punto de restauración", "With a restore point") : Loc.S("Sin punto de restauración", "Without a restore point"), 12)));
                if (anyOpen)
                {
                    var undoAll = Ui.Secondary(Loc.S("Deshacer toda la sesión", "Undo the whole session"), async () =>
                    {
                        if (!await W.Confirm(Loc.S("¿Deshacer la sesión?", "Undo the session?"), Loc.S("Se restauran los valores anteriores de todos los cambios de esta sesión.", "The previous values of every change in this session are restored."), Loc.S("Deshacer", "Undo"))) return;
                        await Undo(() => S.Engine.UndoSession(s.Id, S.UndoCatalog()));
                    }, Icons.Undo);
                    Grid.SetColumn(undoAll, 1);
                    header.Children.Add(undoAll);
                }
                body.Children.Add(header);

                foreach (var e in entries)
                {
                    var t = undoCatalog.FirstOrDefault(x => x.Id == e.TweakId);
                    var title = t != null ? Loc.S(t.Title) : e.TweakId;
                    string stateText; string brush; string icon;
                    if (e.ResultKind == EntryResult.Blocked) { stateText = Loc.S("Windows protege este ajuste; no se cambió nada.", "Windows protects this setting; nothing was changed."); brush = "Moderate"; icon = Icons.Lock; }
                    else if (e.ResultKind == EntryResult.Failed) { stateText = Loc.S("Falló y se revirtió: ", "Failed and rolled back: ") + e.Error; brush = "Advanced"; icon = Icons.Error; }
                    else if (e.Reverted) { stateText = Loc.S("Deshecho", "Undone"); brush = "FgMuted"; icon = Icons.Undo; }
                    else { stateText = Loc.S("Aplicado", "Applied"); brush = "Success"; icon = Icons.CheckCircle; }
                    var info = Ui.Stack(2, Ui.Text(title, 14, true), Ui.Text(stateText, 12, false, brush));
                    FrameworkElement right = null;
                    if (e.ResultKind == EntryResult.Applied && !e.Reverted)
                    {
                        if (t != null && t.Reversible)
                        {
                            var b = Ui.Secondary(Loc.S("Deshacer", "Undo"), async () => await Undo(() => S.Engine.Undo(e.TweakId, S.UndoCatalog())), Icons.Undo);
                            AutomationProperties.SetName(b, Loc.S("Deshacer ", "Undo ") + title);
                            right = b;
                        }
                        else right = Ui.Muted(Loc.S("No reversible aquí", "Not reversible here"), 12);
                    }
                    body.Children.Add(CleanupPage.Row(Ui.Icon(icon, 16, brush), info, right));
                }
            }

            var footer = Footer(Ui.Secondary(Loc.S("Abrir Restaurar sistema", "Open System Restore"), () =>
            {
                try { System.Diagnostics.Process.Start("rstrui.exe"); } catch (Exception ex) { W.ShowError(ex.Message); }
            }, Icons.Shield), null);
            return Frame(Loc.S("Cada cambio guarda el valor que había antes. Deshacer lo devuelve tal cual.", "Every change stores the value that was there before. Undo puts it back exactly."), body, footer);
        }

        private async Task Undo(Func<UndoResult> work)
        {
            _busy = true; W.RefreshCurrentIf(this);
            try
            {
                var r = await Task.Run(work);
                if (r.Failed.Count > 0)
                    W.Toast(ToastKind.Error, Loc.S("No se pudieron deshacer ", "Could not undo ") + r.Failed.Count
                        + Loc.S(". Siguen en la lista para reintentarlo.", ". They stay in the list so you can retry."), Loc.S("Ver cuáles", "See which"),
                        () => W.ShowError(string.Join(", ", r.Failed)));
                else if (r.Reverted.Count > 0)
                    W.Toast(ToastKind.Success, r.Reverted.Count == 1 ? Loc.S("Cambio deshecho.", "Change undone.") : r.Reverted.Count + Loc.S(" cambios deshechos.", " changes undone."));
                if (r.NotReversible.Count > 0)
                    W.Toast(ToastKind.Info, r.NotReversible.Count + Loc.S(" desinstalaciones no se pueden deshacer desde aquí.", " uninstalls cannot be undone from here."));
                await S.RescanAsync();
                W.InvalidateScanViews();
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
            _busy = false;
            await LoadAsync();
        }
    }

    public sealed class DriftPage : AppPage
    {
        private IList<Tweak> _drifted;
        private string _error;
        private bool _busy;

        public DriftPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "drift";
        public override string Title => Loc.S("Cambios revertidos", "Reverted changes");
        public override string Icon => Icons.Drift;

        public override void OnShown() { _ = CheckAsync(); }

        private async Task CheckAsync()
        {
            try
            {
                await S.EnsureReadyAsync();
                _drifted = await Task.Run(() => S.Engine.FindDrift(S.UndoCatalog()));
                _error = null;
            }
            catch (Exception ex) { _error = ex.Message; }
            W.RefreshCurrentIf(this);
        }

        protected override FrameworkElement Build()
        {
            var intro = Loc.S("Las actualizaciones de Windows a veces vuelven a poner valores que habías cambiado. Aquí ves cuáles y puedes aplicarlos otra vez.",
                "Windows updates sometimes put back values you had changed. Here you see which ones and can apply them again.");
            if (_error != null) return Frame(intro, Ui.ErrorState(Loc.S("No se pudo comprobar.", "Could not check."), _error, () => _ = CheckAsync()));
            if (_drifted == null || _busy) { var (v, _) = Ui.Loading(Loc.S("Comprobando tus cambios…", "Checking your changes…")); return Frame(intro, v); }
            if (_drifted.Count == 0)
                return Frame(intro, Ui.Notice(Icons.CheckCircle, "Success", Loc.S("Todo lo que aplicaste sigue en su sitio.", "Everything you applied is still in place.")),
                    Footer(Ui.Secondary(Loc.S("Comprobar de nuevo", "Check again"), () => _ = CheckAsync(), Icons.Restart), null));

            var list = new StackPanel();
            foreach (var t in _drifted)
                list.Children.Add(CleanupPage.Row(Ui.Icon(Icons.Drift, 16, "Moderate"), Ui.Stack(2, Ui.Text(Loc.S(t.Title), 14, true), Ui.Muted(Loc.S(t.Summary))), Ui.RiskMarker(t.Risk)));
            var apply = Ui.Primary(Loc.S("Aplicar de nuevo ", "Apply again ") + _drifted.Count, async () =>
            {
                _busy = true; W.RefreshCurrentIf(this);
                try
                {
                    var again = _drifted.ToList();
                    var s = await Task.Run(() => S.Engine.Apply(again, new ApplyOptions { CreateRestorePoint = false }));
                    await S.RescanAsync(again);
                    W.InvalidateScanViews();
                    int failed = s.Entries.Count(e => e.ResultKind == EntryResult.Failed);
                    if (failed > 0) W.Toast(ToastKind.Error, failed + Loc.S(" no se pudieron aplicar de nuevo.", " could not be applied again."));
                    else W.Toast(ToastKind.Success, Loc.S("Ajustes aplicados de nuevo.", "Tweaks applied again."));
                }
                catch (Exception ex) { W.ShowError(ex.Message); }
                _busy = false;
                await CheckAsync();
            }, Icons.Restart);
            return Frame(intro, list, Footer(Ui.Secondary(Loc.S("Comprobar de nuevo", "Check again"), () => _ = CheckAsync(), Icons.Restart), apply));
        }
    }

    public sealed class MeasurePage : AppPage
    {
        private IList<Measurement> _all;
        private bool _taking;
        private bool? _afterRestartScheduled;

        public MeasurePage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "measure";
        public override string Title => Loc.S("Mediciones", "Measurements");
        public override string Icon => Icons.Measure;

        public override void OnShown()
        {
            if (_all != null) return;
            _ = Task.Run(() => (S.Win.Measurements.Load(), MeasureAfterRestart.IsScheduled(S.Win.Process)))
                .ContinueWith(t => { (_all, _afterRestartScheduled) = t.Result; W.Dispatcher.Invoke(() => W.RefreshCurrentIf(this)); });
        }

        public static FrameworkElement Value(double? v, string unit)
        {
            if (v == null) return Ui.Muted(Loc.S("Sin dato", "No data"));
            var t = Ui.Text(Format(v.Value, unit));
            t.Margin = new Thickness(0, 6, 0, 0);
            return t;
        }

        private static string Format(double v, string unit) =>
            Loc.N(v, unit == "s" || unit == "%" ? "N1" : "N0") + (unit.Length > 0 ? (unit == "%" ? "" : " ") + unit : "");

        private static string When(Measurement m) =>
            DateTime.Parse(m.TakenUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime().ToString("g");

        private static string LabelOf(Measurement m)
        {
            switch (m.Label)
            {
                case "before": return Loc.S("antes de aplicar", "before applying");
                case "after": return Loc.S("después de aplicar", "after applying");
                case MeasureAfterRestart.Label: return Loc.S("tras reiniciar", "after restart");
                case "manual": return Loc.S("manual", "manual");
                default: return m.Label;
            }
        }

        /// <summary>A small line chart of one metric across every saved measurement. No axes: the table has the numbers.</summary>
        private static FrameworkElement Spark(IList<double?> values, bool lowerIsBetter)
        {
            var pts = values.Select((v, i) => (v, i)).Where(x => x.v.HasValue).ToList();
            var canvas = new Canvas { Width = 140, Height = 28, Margin = new Thickness(0, 6, 0, 0), ClipToBounds = true };
            if (pts.Count < 2) return canvas;
            double min = pts.Min(p => p.v.Value), max = pts.Max(p => p.v.Value), span = Math.Max(1e-9, max - min);
            int n = values.Count - 1;
            var line = new System.Windows.Shapes.Polyline { StrokeThickness = 1.5 };
            line.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentText");
            foreach (var (v, i) in pts) line.Points.Add(new Point(2 + 136.0 * i / Math.Max(1, n), 25 - 22 * (v.Value - min) / span));
            canvas.Children.Add(line);
            var lastPt = line.Points.Last();
            var first = pts.First().v.Value; var last = pts.Last().v.Value;
            bool better = lowerIsBetter ? last < first : last > first;
            var dot = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6 };
            dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, Math.Abs(last - first) < 1e-9 ? "FgMuted" : better ? "Success" : "Moderate");
            Canvas.SetLeft(dot, lastPt.X - 3); Canvas.SetTop(dot, lastPt.Y - 3);
            canvas.Children.Add(dot);
            AutomationProperties.SetName(canvas, Loc.S("Evolución: de ", "Trend: from ") + Loc.N(first, "N1") + Loc.S(" a ", " to ") + Loc.N(last, "N1"));
            return canvas;
        }

        protected override FrameworkElement Build()
        {
            var intro = Loc.S("Números leídos de este equipo, nunca estimados. Mide antes y después de cambiar algo, y otra vez tras reiniciar, para comparar.",
                "Numbers read from this PC, never estimated. Measure before and after changing something, and again after restarting, to compare.");
            var take = Ui.Primary(_taking ? Loc.S("Midiendo…", "Measuring…") : Loc.S("Medir ahora", "Measure now"), async () =>
            {
                _taking = true; W.RefreshCurrentIf(this);
                try
                {
                    await Task.Run(() => S.Win.Measurements.Add(S.Win.Measurer.Take("manual")));
                    _all = await Task.Run(() => S.Win.Measurements.Load());
                    W.Toast(ToastKind.Success, Loc.S("Medición guardada.", "Measurement saved."));
                }
                catch (Exception ex) { W.ShowError(ex.Message); }
                _taking = false; W.RefreshCurrentIf(this);
            }, Icons.Measure);
            take.IsEnabled = !_taking;

            if (_all == null) { var (v, _) = Ui.Loading(Loc.S("Cargando…", "Loading…")); return Frame(intro, v); }

            var tools = new WrapPanel();
            void Tool(Button b) { b.Margin = new Thickness(0, 0, 8, 8); tools.Children.Add(b); }
            Tool(Ui.Secondary(_afterRestartScheduled == true ? Loc.S("Cancelar la medición tras reiniciar", "Cancel the after-restart measurement") : Loc.S("Medir después de reiniciar", "Measure after restarting"), async () =>
            {
                try
                {
                    if (_afterRestartScheduled == true)
                    {
                        await Task.Run(() => MeasureAfterRestart.Cancel(S.Win.Process));
                        _afterRestartScheduled = false;
                        W.Toast(ToastKind.Info, Loc.S("Medición tras reiniciar cancelada.", "After-restart measurement cancelled."));
                    }
                    else
                    {
                        await Task.Run(() => MeasureAfterRestart.Schedule(S.Win.Process));
                        _afterRestartScheduled = true;
                        W.Toast(ToastKind.Success, Loc.S("Listo: 2 minutos después de iniciar sesión se guarda una medición, sin abrir nada. No muevas Optimizzzer de carpeta hasta entonces.",
                            "Done: 2 minutes after you sign in, a measurement is saved without opening anything. Do not move Optimizzzer to another folder until then."));
                    }
                }
                catch (Exception ex) { W.ShowError(ex.Message); }
                W.RefreshCurrentIf(this);
            }, Icons.Restart));
            if (_all.Count > 0)
            {
                Tool(Ui.Secondary(Loc.S("Exportar a CSV", "Export to CSV"), ExportCsv, Icons.Export));
                Tool(Ui.Secondary(Loc.S("Borrar mediciones", "Delete measurements"), async () =>
                {
                    if (!await W.Confirm(Loc.S("¿Borrar las mediciones?", "Delete the measurements?"),
                        Loc.S("Se borran las ", "All ") + _all.Count + Loc.S(" mediciones guardadas. No cambia nada del equipo.", " saved measurements are deleted. Nothing on the PC changes."),
                        Loc.S("Borrar", "Delete"), danger: true)) return;
                    try { await Task.Run(() => S.Win.Measurements.Clear()); _all = new List<Measurement>(); W.Toast(ToastKind.Success, Loc.S("Mediciones borradas.", "Measurements deleted.")); }
                    catch (Exception ex) { W.ShowError(ex.Message); }
                    W.RefreshCurrentIf(this);
                }, Icons.Cleanup));
            }
            var scheduledNote = _afterRestartScheduled == true
                ? Ui.Notice(Icons.Restart, "AccentText", Loc.S("Hay una medición programada para después del próximo reinicio.", "A measurement is scheduled for after the next restart."))
                : null;

            if (_all.Count == 0)
                return Frame(intro, Ui.Stack(16, tools, scheduledNote, Ui.Empty(Loc.S("Aún no hay mediciones. Pulsa Medir ahora para crear la primera.", "There are no measurements yet. Press Measure now to create the first one."))), Footer(null, take));

            var first = _all.First();
            var prev = _all.Count > 1 ? _all[_all.Count - 2] : null;
            var last = _all.Last();
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (prev != null && prev != first) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            int cols = g.ColumnDefinitions.Count;
            void Cell(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); g.Children.Add(e); }
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Cell(Ui.Muted(Loc.S("Métrica", "Metric")), 0, 0);
            Cell(Ui.Muted(Loc.S("Primera", "First") + "\n" + When(first) + "\n" + LabelOf(first), 12), 0, 1);
            int c = 2;
            if (cols == 5) Cell(Ui.Muted(Loc.S("Anterior", "Previous") + "\n" + When(prev) + "\n" + LabelOf(prev), 12), 0, c++);
            Cell(Ui.Muted(Loc.S("Última", "Latest") + "\n" + When(last) + "\n" + LabelOf(last), 12), 0, c);
            Cell(Ui.Muted(Loc.S("Evolución", "Trend")), 0, cols - 1);
            int row = 1;
            foreach (var m in MeasurementCompare.Metrics)
            {
                var values = _all.Select(x => m.Get(x)).ToList();
                if (values.All(v => !v.HasValue)) continue; // never measured on this PC (older versions, or not available)
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var n = Ui.Text(Loc.S(m.Name)); n.Margin = new Thickness(0, 6, 12, 0);
                Cell(n, row, 0);
                Cell(Value(m.Get(first), m.Unit), row, 1);
                int cc = 2;
                if (cols == 5) Cell(Value(m.Get(prev), m.Unit), row, cc++);
                Cell(Value(m.Get(last), m.Unit), row, cc);
                Cell(Spark(values, m.LowerIsBetter), row, cols - 1);
                row++;
            }
            if (last.Drives != null && last.Drives.Count > 0)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var n = Ui.Text(Loc.S("Espacio libre por disco (última)", "Free space per drive (latest)")); n.Margin = new Thickness(0, 6, 12, 0);
                Cell(n, row, 0);
                var drives = Ui.Text(string.Join("   ", last.Drives.Select(d => d.Name + " " + Loc.Size(d.FreeMb * 1024 * 1024) + Loc.S(" de ", " of ") + Loc.Size(d.TotalMb * 1024 * 1024))));
                drives.Margin = new Thickness(0, 6, 0, 0);
                Grid.SetColumnSpan(drives, cols - 1);
                Cell(drives, row, 1);
            }

            var count = _all.Count == 1 ? Loc.S("1 medición guardada.", "1 measurement saved.") : _all.Count + Loc.S(" mediciones guardadas.", " measurements saved.");
            var body = Ui.Stack(16, tools, scheduledNote, Ui.Panel(g),
                Ui.Muted(count + Loc.S(" La duración del arranque y del apagado la registra Windows; si no aparece, su registro de rendimiento está desactivado. El punto verde indica mejora entre la primera y la última; el ámbar, que empeoró.",
                    " Boot and shutdown durations are logged by Windows; if they are missing, its performance log is turned off. The green dot means better from first to latest; amber means worse."), 12));
            return Frame(intro, body, Footer(null, take));
        }

        private void ExportCsv()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = "Optimizzzer-mediciones-" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv", Filter = "CSV (*.csv)|*.csv" };
            if (dlg.ShowDialog(W) != true) return;
            try
            {
                System.IO.File.WriteAllText(dlg.FileName, MeasurementCompare.ToCsv(_all, Loc.Lang), new System.Text.UTF8Encoding(true));
                W.Toast(ToastKind.Success, Loc.S("Exportado a ", "Exported to ") + System.IO.Path.GetFileName(dlg.FileName) + ".");
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
        }
    }
}
