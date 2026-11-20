using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;

namespace Optimizzzer.App.Pages
{
    /// <summary>
    /// Home: what this PC is, how far each level already is, and the four one-click levels.
    /// Clicking a level opens a sheet that says in plain words what will happen; nothing changes until "Apply".
    /// </summary>
    public sealed class HomePage : AppPage
    {
        private Action<double, string> _progress;
        private IList<Tweak> _drift;
        private JournalSession _last;
        private Health _health;

        /// <summary>Facts read from this PC for the health card. Null fields could not be read and are not shown.</summary>
        private sealed class Health
        {
            public long? FreeBytes, TotalBytes;
            public double? UptimeDays;
            public DateTime? LastRestorePoint;
            public long? TempBytes;
        }

        public HomePage(AppState s, MainWindow w) : base(s, w)
        {
            s.ScanProgress += (f, m) => _progress?.Invoke(f, m);
            s.ScanFinished += () => { W.RefreshCurrentIf(this); _ = LoadExtrasAsync(); };
        }

        public override string Key => "home";
        public override string Title => Loc.S("Inicio", "Home");
        public override string Icon => Icons.Home;

        public override void OnShown()
        {
            if (S.ScanResults == null && !S.Scanning && S.ScanError == null) _ = S.ScanAsync();
            else if (S.ScanResults != null) _ = LoadExtrasAsync();
        }

        private static Health ReadHealth()
        {
            var h = new Health();
            try
            {
                var d = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory));
                h.FreeBytes = d.AvailableFreeSpace; h.TotalBytes = d.TotalSize;
            }
            catch (Exception) { }
            try { h.UptimeDays = Optimizzzer.Windows.Native.GetTickCount64() / 86400000.0; } catch (Exception) { }
            h.LastRestorePoint = Optimizzzer.Windows.WinRestorePoint.LastCreated();
            try
            {
                long t = 0;
                foreach (var f in new System.IO.DirectoryInfo(System.IO.Path.GetTempPath()).EnumerateFiles("*", System.IO.SearchOption.AllDirectories)) { try { t += f.Length; } catch (Exception) { } }
                h.TempBytes = t;
            }
            catch (Exception) { }
            return h;
        }

        /// <summary>The health card: measured facts, each with what to do when it needs attention.</summary>
        private FrameworkElement HealthCard()
        {
            var h = _health;
            var rows = new StackPanel();
            void Line(string icon, string brush, string text, string action = null, Action onAction = null)
            {
                var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var n = Ui.Notice(icon, brush, text);
                n.VerticalAlignment = VerticalAlignment.Center;
                g.Children.Add(n);
                if (action != null)
                {
                    var b = Ui.Subtle(action, onAction);
                    b.SetResourceReference(Control.ForegroundProperty, "AccentText");
                    Grid.SetColumn(b, 1);
                    g.Children.Add(b);
                }
                rows.Children.Add(g);
            }
            if (h == null)
            {
                rows.Children.Add(Ui.Muted(Loc.S("Leyendo…", "Reading…"), 12));
            }
            else
            {
                if (h.FreeBytes.HasValue && h.TotalBytes > 0)
                {
                    double pct = h.FreeBytes.Value * 100.0 / h.TotalBytes.Value;
                    bool low = pct < 10 || h.FreeBytes < 15L * 1024 * 1024 * 1024;
                    Line(low ? Icons.Warning : Icons.CheckCircle, low ? "Moderate" : "Success",
                        Loc.S("Espacio libre en el disco del sistema: ", "Free space on the system drive: ") + Loc.Size(h.FreeBytes.Value) + " (" + Loc.N(pct, "N0") + " %)"
                        + (low ? Loc.S(". Poco espacio vuelve lento a Windows.", ". Low space slows Windows down.") : "."),
                        low ? Loc.S("Liberar espacio", "Free up space") : null, () => W.Navigate("cleanup"));
                }
                if (h.UptimeDays.HasValue)
                {
                    bool longUp = h.UptimeDays > 7;
                    Line(longUp ? Icons.Warning : Icons.CheckCircle, longUp ? "Moderate" : "Success",
                        Loc.S("Sin reiniciar desde hace ", "Not restarted for ") + (h.UptimeDays < 1 ? Loc.S("menos de un día", "less than a day") : Loc.N(Math.Floor(h.UptimeDays.Value), "N0") + Loc.S(" días", " days"))
                        + (longUp ? Loc.S(". Reiniciar aplica actualizaciones y libera memoria. El apagado normal con Inicio rápido no cuenta como reinicio.", ". Restarting applies updates and frees memory. A normal shutdown with Fast Startup does not count as a restart.") : "."));
                }
                if (h.LastRestorePoint.HasValue)
                {
                    var days = (DateTime.Now - h.LastRestorePoint.Value).TotalDays;
                    Line(days > 30 ? Icons.Warning : Icons.Shield, days > 30 ? "Moderate" : "Success",
                        Loc.S("Último punto de restauración: ", "Last restore point: ") + h.LastRestorePoint.Value.ToString("d")
                        + (days > 30 ? Loc.S(". Tiene más de un mes; se crea uno nuevo al aplicar cualquier nivel.", ". It is over a month old; a new one is created when you apply any level.") : "."));
                }
                else
                    Line(Icons.Warning, "Moderate", Loc.S("No hay puntos de restauración, o Restaurar sistema está desactivado. Optimizzzer intenta crear uno antes de cada cambio.",
                        "There are no restore points, or System Restore is off. Optimizzzer tries to create one before every change."));
                if (h.TempBytes.HasValue && h.TempBytes > 1024L * 1024 * 1024)
                    Line(Icons.Info, "FgMuted", Loc.S("Archivos temporales: ", "Temporary files: ") + Loc.Size(h.TempBytes.Value) + ".", Loc.S("Limpiar", "Clean up"), () => W.Navigate("cleanup"));
                if (_drift != null && _drift.Count > 0)
                    Line(Icons.Drift, "Moderate", _drift.Count + Loc.S(" ajustes revertidos por Windows.", " tweaks reverted by Windows."), Loc.S("Revisar", "Review"), () => W.Navigate("drift"));
                else
                    Line(Icons.CheckCircle, "Success", Loc.S("Ningún ajuste fue revertido por Windows.", "No tweak was reverted by Windows."));
            }
            var panel = Ui.Panel(Ui.Stack(8, Ui.H2(Loc.S("Salud del equipo", "PC health")), rows), 20);
            panel.Margin = new Thickness(0, 0, 0, 8);
            return panel;
        }

        /// <summary>Last session and drift, read in the background after the scan.</summary>
        private async Task LoadExtrasAsync()
        {
            if (S.Engine == null) return;
            try
            {
                var catalog = S.UndoCatalog();
                var (last, drift, health) = await Task.Run(() =>
                {
                    var l = S.Win.Journal.LoadAll().Where(x => x.Entries.Any(e => e.ResultKind == EntryResult.Applied && !e.Reverted
                        && (catalog.FirstOrDefault(t => t.Id == e.TweakId)?.Reversible ?? true))).LastOrDefault();
                    return (l, S.Engine.FindDrift(catalog), ReadHealth());
                });
                bool changed = (last?.Id != _last?.Id) || (drift.Count != (_drift?.Count ?? 0)) || _health == null;
                _last = last; _drift = drift; _health = health;
                if (changed) W.RefreshCurrentIf(this);
            }
            catch (Exception) { }
        }

        protected override FrameworkElement Build()
        {
            if (S.ScanError != null)
                return Frame(null, Ui.ErrorState(Loc.S("No se pudo analizar el equipo.", "The PC could not be scanned."), S.ScanError, () => _ = S.ScanAsync(true)));
            if (S.ScanResults == null)
            {
                var (view, update) = Ui.Loading(Loc.S("Leyendo la información del equipo…", "Reading PC information…"));
                _progress = update;
                return Frame(Loc.S("Solo se lee: no se cambia nada sin tu confirmación.", "Read only: nothing changes without your confirmation."), Ui.Stack(16, view, Ui.ScanDurationNote()));
            }

            var info = S.Ctx.Info;
            var gpu = info.Gpus.FirstOrDefault();
            var pc = string.Join(" · ", new[] { info.ProductName + " " + info.DisplayVersion, Short(info.CpuName),
                info.TotalMemoryMb > 0 ? Loc.N(info.TotalMemoryMb / 1024.0, "N0") + " GB RAM" : null, gpu }.Where(x => !string.IsNullOrWhiteSpace(x)));

            var body = new StackPanel();

            // Notices first: they need attention before anything else.
            // Reverted tweaks are shown in the health card below, with a Review button.
            if (_last != null)
            {
                int n = _last.Entries.Count(e => e.ResultKind == EntryResult.Applied && !e.Reverted);
                body.Children.Add(NoticeBar(Icons.History, "FgMuted",
                    Loc.S("Última optimización: ", "Last optimization: ") + _last.Started.ToLocalTime().ToString("g") + " · " + n + Loc.S(" cambios activos", " active changes"),
                    Loc.S("Deshacer", "Undo"), UndoLast));
            }

            body.Children.Add(HealthCard());

            var heading = Ui.H2(Loc.S("Elige un nivel", "Pick a level"));
            heading.Margin = new Thickness(0, body.Children.Count > 0 ? 12 : 0, 0, 4);
            body.Children.Add(heading);
            body.Children.Add(Ui.Muted(Loc.S("Cada nivel incluye los anteriores. Antes de aplicar verás exactamente qué cambia. Los ajustes se pueden deshacer; las apps quitadas se reinstalan desde la Store.",
                "Each level includes the ones before. Before applying you see exactly what changes. Settings can be undone; removed apps are reinstalled from the Store.")));

            var cards = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            foreach (var l in Levels.All) cards.Children.Add(LevelCard(l));
            body.Children.Add(cards);

            var custom = Ui.Secondary(Loc.S("Personalizar ajuste por ajuste", "Customize tweak by tweak"), () => W.Navigate("wizard"), Icons.Wizard);
            custom.HorizontalAlignment = HorizontalAlignment.Left;
            custom.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(custom);

            var frame = Frame(pc, body);
            return frame;
        }

        private static string Short(string cpu) =>
            string.IsNullOrEmpty(cpu) ? null : cpu.Replace("(R)", "").Replace("(TM)", "").Replace(" CPU", "").Replace("  ", " ").Trim();

        private FrameworkElement NoticeBar(string icon, string brush, string text, string action, Action onAction)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var n = Ui.Notice(icon, brush, text);
            n.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(n);
            var b = Ui.Secondary(action, onAction);
            Grid.SetColumn(b, 1);
            b.Margin = new Thickness(16, 0, 0, 0);
            g.Children.Add(b);
            var bar = new Border { Child = g, Padding = new Thickness(16, 10, 12, 10), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 8) };
            bar.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
            return bar;
        }

        /// <summary>A level card: what it is, how much of it this PC already has, and how many changes are left.</summary>
        private FrameworkElement LevelCard(LevelInfo l)
        {
            var pending = Levels.Select(S.ScanResults, l.Level, S.Ctx.Info);
            var (applied, total) = Levels.Progress(S.ScanResults, l.Level);

            var steps = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            for (int i = 1; i <= 4; i++)
            {
                var bar = new Border { Width = 18, Height = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 4, 0) };
                bar.SetResourceReference(Border.BackgroundProperty, i <= (int)l.Level ? "AccentText" : "Divider");
                steps.Children.Add(bar);
            }
            AutomationProperties.SetName(steps, Loc.S("Nivel ", "Level ") + (int)l.Level + Loc.S(" de 4", " of 4"));

            var progress = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, total), Value = 0, Margin = new Thickness(0, 6, 0, 0) };
            progress.SetResourceReference(Control.BackgroundProperty, "Divider"); // the card is SurfaceAlt, so the track needs its own tone
            Theme.AnimateValue(progress, applied);
            var status = pending.Count == 0
                ? Ui.Text(Loc.S("Todo aplicado", "All applied"), 12, true, "Success", wrap: false)
                : Ui.Text(pending.Count + (pending.Count == 1 ? Loc.S(" cambio pendiente", " change left") : Loc.S(" cambios pendientes", " changes left")), 12, true, "AccentText", wrap: false);
            var done = Ui.Muted(applied + Loc.S(" de ", " of ") + total + Loc.S(" ya aplicados en este equipo", " already applied on this PC"), 12);

            var title = Ui.Row(10, Ui.Icon(Icons.For(l.Level), 20, "AccentText"), Ui.Text(Loc.S(l.Name), 16, true, "Fg"));
            var content = Ui.Stack(0, steps, title, Ui.Muted(Loc.S(l.Tagline)), status, progress, done,
                l.NeedsSecondConfirmation ? Ui.Text(Loc.S("Incluye cambios moderados: lee qué pierdes.", "Includes moderate changes: read what you lose."), 12, false, "Moderate") : null);
            ((FrameworkElement)content.Children[2]).Margin = new Thickness(0, 4, 0, 12);

            var b = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
            b.SetResourceReference(FrameworkElement.StyleProperty, "CardButton");
            b.Width = 300; // two per row on a 1024 px window, so titles such as "Maximum performance" fit
            b.Margin = new Thickness(0, 0, 12, 12);
            AutomationProperties.SetName(b, Loc.S(l.Name) + ". " + Loc.S(l.Tagline) + " " + status.Text);
            b.Click += (_, __) => OpenLevel(l.Level);
            return b;
        }

        // ---------- The confirmation sheet ----------

        /// <summary>Self-test only: the last level sheet's buttons, to check they stay on screen at any size.</summary>
        public Button SheetApplyForTest { get; private set; }
        public Button SheetDetailsForTest { get; private set; }

        public void OpenLevel(Level level)
        {
            if (S.ScanResults == null) { W.Toast(ToastKind.Info, Loc.S("Espera a que termine el análisis.", "Wait for the scan to finish.")); return; }
            var l = Levels.Get(level);
            var tweaks = Levels.Select(S.ScanResults, level, S.Ctx.Info);
            if (tweaks.Count == 0)
            {
                W.Toast(ToastKind.Success, Loc.S("Todo lo del nivel ", "Everything in ") + Loc.S(l.Name) + Loc.S(" ya está aplicado en este equipo.", " is already applied on this PC."));
                return;
            }

            bool restorePoint = true;
            var losses = tweaks.Where(t => t.Downside.Es.Length > 0).ToList();
            var restart = tweaks.Select(t => t.Restart).DefaultIfEmpty(RestartKind.None).Max();
            int uninstalls = tweaks.Count(t => !t.Reversible);
            int moderate = tweaks.Count(t => t.Risk != Risk.Safe);

            var byCategory = string.Join(" · ", tweaks.GroupBy(t => t.Category).Select(g => Names.Of(g.Key) + " " + g.Count()));

            var what = Ui.Stack(10,
                Ui.Notice(Icons.Info, "AccentText", tweaks.Count + Loc.S(" cambios en este equipo: ", " changes on this PC: ") + byCategory + "."),
                Ui.Notice(Icons.CheckCircle, "Success", Loc.S("Qué ganas: ", "What you gain: ") + Loc.S(l.Gains)),
                losses.Count > 0 ? Ui.Notice(Icons.Warning, "Moderate", Loc.S("Qué pierdes: ", "What you lose: ")
                    + string.Join(" ", losses.Take(3).Select(t => Loc.S(t.Downside)))
                    + (losses.Count > 3 ? Loc.S(" Y ", " Plus ") + (losses.Count - 3) + Loc.S(" más en el detalle.", " more in the details.") : "")) : null,
                uninstalls > 0 ? Ui.Notice(Icons.Error, "Advanced", uninstalls == 1
                    ? Loc.S("1 es una desinstalación de una app preinstalada: no se deshace desde aquí, se reinstala desde la Microsoft Store.",
                        "1 is an uninstall of a preinstalled app: it is not undone from here, you reinstall it from the Microsoft Store.")
                    : uninstalls + Loc.S(" son desinstalaciones de apps preinstaladas: no se deshacen desde aquí, se reinstalan desde la Microsoft Store.",
                        " are uninstalls of preinstalled apps: they are not undone from here, you reinstall them from the Microsoft Store.")) : null,
                restart != RestartKind.None ? Ui.Notice(Icons.Restart, "FgMuted", Names.Of(restart) + ". " + Loc.S("Duración: ", "Duration: ") + Loc.S(l.Duration) + ".")
                    : Ui.Notice(Icons.Stopwatch, "FgMuted", Loc.S("Duración: ", "Duration: ") + Loc.S(l.Duration) + "."));

            var rp = new CheckBox { Content = Loc.S("Crear un punto de restauración antes (recomendado)", "Create a restore point first (recommended)"), IsChecked = true };
            rp.Checked += (_, __) => restorePoint = true;
            rp.Unchecked += (_, __) => restorePoint = false;

            // The exact list, built only if the user opens it.
            var detailsHost = new ContentControl { Focusable = false, Visibility = Visibility.Collapsed };
            var toggle = Ui.Subtle(Loc.S("Ver los ", "See the ") + tweaks.Count + Loc.S(" cambios exactos", " exact changes"), null, Icons.ChevronDown);
            toggle.HorizontalAlignment = HorizontalAlignment.Left;
            toggle.Click += (_, __) =>
            {
                if (detailsHost.Content == null)
                {
                    var list = new StackPanel();
                    foreach (var t in tweaks)
                    {
                        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
                        var name = Ui.Text(Loc.S(t.Title), 14, false, "Fg", wrap: false);
                        name.Margin = new Thickness(0, 0, 10, 0);
                        row.Children.Add(name);
                        row.Children.Add(Ui.RiskMarker(t.Risk));
                        list.Children.Add(row);
                    }
                    // No inner scroll: the sheet body scrolls as a whole, so the buttons below never move off screen.
                    detailsHost.Content = list;
                }
                detailsHost.Visibility = detailsHost.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            };

            var apply = Ui.Primary(Loc.S("Aplicar ", "Apply ") + tweaks.Count + Loc.S(" cambios", " changes"), null, Icons.Check);
            var cancel = Ui.Secondary(Loc.S("Cancelar", "Cancel"), W.CloseOverlay);
            apply.Click += async (_, __) =>
            {
                W.CloseOverlay();
                if (l.NeedsSecondConfirmation && !await W.Confirm(
                        Loc.S("¿Aplicar Máximo rendimiento?", "Apply Maximum performance?"),
                        moderate + Loc.S(" de estos cambios son de riesgo moderado: quitan funciones como la búsqueda rápida de archivos o la hibernación a cambio de menos trabajo en segundo plano. Podrás deshacerlos desde Historial; las apps quitadas se reinstalan desde la Store.",
                            " of these changes are moderate risk: they remove features such as fast file search or hibernation in exchange for less background work. You can undo them from History; removed apps are reinstalled from the Store."),
                        Loc.S("Sí, aplicar", "Yes, apply"), danger: true)) return;
                W.Navigate("wizard");
                W.PageOf<WizardPage>().StartLevel(l, tweaks, restorePoint);
            };
            var buttons = Ui.Row(8, cancel, apply);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            SheetApplyForTest = apply;
            SheetDetailsForTest = toggle;

            var head = Ui.Row(12, Ui.Icon(Icons.For(level), 28, "AccentText"), Ui.Stack(0, Ui.H2(Loc.S("Nivel " + l.Name.Es, l.Name.En + " level")), Ui.Muted(Loc.S(l.Tagline))));
            var sheet = MainWindow.Sheet(head, Ui.Stack(16, Ui.Text(Loc.S("Qué va a pasar", "What will happen"), 14, true), what, rp, Ui.Stack(4, toggle, detailsHost)), buttons);
            AutomationProperties.SetName(sheet, Loc.S("Nivel " + l.Name.Es, l.Name.En + " level"));
            W.ShowOverlay(sheet, W.CloseOverlay, 640);
            Dispatcher(() => apply.Focus());
        }

        private void Dispatcher(Action a) => W.Dispatcher.BeginInvoke(a, System.Windows.Threading.DispatcherPriority.Input);

        public async void UndoLast()
        {
            var last = _last;
            if (last == null) { W.Toast(ToastKind.Info, Loc.S("No hay ninguna sesión que deshacer.", "There is no session to undo.")); return; }
            if (!await W.Confirm(Loc.S("¿Deshacer la última optimización?", "Undo the last optimization?"),
                Loc.S("Se restauran los valores anteriores de todos sus cambios. Las desinstalaciones de apps no se deshacen.", "The previous values of all its changes are restored. App uninstalls are not undone."),
                Loc.S("Deshacer", "Undo"))) return;
            try
            {
                var r = await Task.Run(() => S.Engine.UndoSession(last.Id, S.UndoCatalog()));
                await S.RescanAsync();
                if (r.Failed.Count > 0)
                    W.Toast(ToastKind.Error, r.Failed.Count + Loc.S(" cambios no se pudieron deshacer. Siguen en Historial para reintentarlo.", " changes could not be undone. They stay in History to retry."), Loc.S("Abrir Historial", "Open History"), () => W.Navigate("history"));
                else W.Toast(ToastKind.Success, r.Reverted.Count + Loc.S(" cambios deshechos.", " changes undone."));
                _last = null;
                W.InvalidateScanViews();
                _ = LoadExtrasAsync();
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
        }
    }
}
