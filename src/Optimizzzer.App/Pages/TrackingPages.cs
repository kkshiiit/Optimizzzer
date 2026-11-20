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
                    if (e.ResultKind == EntryResult.Failed) { stateText = Loc.S("Falló y se revirtió: ", "Failed and rolled back: ") + e.Error; brush = "Advanced"; icon = Icons.Error; }
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

        public MeasurePage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "measure";
        public override string Title => Loc.S("Mediciones", "Measurements");
        public override string Icon => Icons.Measure;

        public override void OnShown()
        {
            if (_all != null) return;
            _ = Task.Run(() => S.Win.Measurements.Load()).ContinueWith(t => { _all = t.Result; W.Dispatcher.Invoke(() => W.RefreshCurrentIf(this)); });
        }

        public static FrameworkElement Value(double? v, string unit)
        {
            if (v == null) return Ui.Muted(Loc.S("Sin dato", "No data"));
            var t = Ui.Text(Loc.N(v.Value, unit == "s" ? "N1" : "N0") + (unit.Length > 0 ? " " + unit : ""));
            t.Margin = new Thickness(0, 6, 0, 0);
            return t;
        }

        protected override FrameworkElement Build()
        {
            var intro = Loc.S("Números leídos de este equipo, nunca estimados. Toma una medición ahora y otra después de reiniciar para comparar.",
                "Numbers read from this PC, never estimated. Take a measurement now and another after restarting to compare.");
            var take = Ui.Primary(_taking ? Loc.S("Midiendo…", "Measuring…") : Loc.S("Medir ahora", "Measure now"), async () =>
            {
                _taking = true; W.RefreshCurrentIf(this);
                try
                {
                    await Task.Run(() => S.Win.Measurements.Add(S.Win.Measurer.Take("manual")));
                    _all = await Task.Run(() => S.Win.Measurements.Load());
                }
                catch (Exception ex) { W.ShowError(ex.Message); }
                _taking = false; W.RefreshCurrentIf(this);
            }, Icons.Measure);
            take.IsEnabled = !_taking;

            if (_all == null) { var (v, _) = Ui.Loading(Loc.S("Cargando…", "Loading…")); return Frame(intro, v); }
            if (_all.Count == 0) return Frame(intro, Ui.Empty(Loc.S("Aún no hay mediciones. Pulsa Medir ahora para crear la primera.", "There are no measurements yet. Press Measure now to create the first one.")), Footer(null, take));

            var first = _all.First();
            var last = _all.Last();
            var rows = MeasurementCompare.Compare(first, last);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            void Cell(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); g.Children.Add(e); }
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string When(Measurement m) => DateTime.Parse(m.TakenUtc, null, System.Globalization.DateTimeStyles.RoundtripKind).ToLocalTime().ToString("g");
            Cell(Ui.Muted(Loc.S("Métrica", "Metric")), 0, 0);
            Cell(Ui.Muted(Loc.S("Primera", "First") + "\n" + When(first), 12), 0, 1);
            Cell(Ui.Muted(Loc.S("Última", "Latest") + "\n" + When(last), 12), 0, 2);
            for (int i = 0; i < rows.Count; i++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var n = Ui.Text(Loc.S(rows[i].Name)); n.Margin = new Thickness(0, 6, 0, 0);
                Cell(n, i + 1, 0);
                Cell(Value(rows[i].Before, rows[i].Unit), i + 1, 1);
                Cell(Value(rows[i].After, rows[i].Unit), i + 1, 2);
            }
            var body = Ui.Stack(16, Ui.Panel(g),
                Ui.Muted(_all.Count + Loc.S(" mediciones guardadas. La duración del arranque la registra Windows en cada inicio; si no aparece, el registro de rendimiento está desactivado.",
                    " measurements saved. Boot duration is logged by Windows at each start; if it is missing, the performance log is turned off."), 12));
            return Frame(intro, body, Footer(null, take));
        }
    }
}
