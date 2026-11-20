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
        private Optimizzzer.Windows.HardwareReport _hw;

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

        /// <summary>
        /// "Your PC": one tile per component with a large icon, read from Windows (never estimated). The screen tile says
        /// when the monitor runs below the refresh rate it supports, which is a real, free improvement.
        /// </summary>
        private FrameworkElement HardwareCards()
        {
            var wrap = new WrapPanel();
            var hw = _hw;
            var info = S.Ctx.Info;
            void Tile(string icon, string title, string main, string detail, string note = null, string noteBrush = "FgMuted", Action noteAction = null, string actionText = null)
            {
                if (string.IsNullOrWhiteSpace(main)) return;
                var ic = Ui.Icon(icon, 26, "AccentText");
                ic.HorizontalAlignment = HorizontalAlignment.Left;
                ic.Margin = new Thickness(0, 0, 0, 10);
                var stack = Ui.Stack(2, ic, Ui.Muted(title, 13), Ui.Text(main, 15, true), string.IsNullOrWhiteSpace(detail) ? null : Ui.Muted(detail, 13));
                if (note != null)
                {
                    var n = Ui.Text(note, 13, false, noteBrush);
                    n.Margin = new Thickness(0, 8, 0, 0);
                    stack.Children.Add(n);
                    if (noteAction != null)
                    {
                        var b = Ui.Subtle(actionText, noteAction);
                        b.SetResourceReference(Control.ForegroundProperty, "AccentText");
                        b.HorizontalAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(0); b.MinHeight = 28;
                        stack.Children.Add(b);
                    }
                }
                var card = Ui.Panel(stack, 18);
                card.Width = MainWindow.Compact ? 230 : 250;
                card.Margin = new Thickness(0, 0, 12, 12);
                AutomationProperties.SetName(card, title + ": " + main);
                wrap.Children.Add(card);
            }
            string Gb(long? b) => b.HasValue && b > 0 ? Loc.N(b.Value / 1073741824.0, b.Value >= 10L * 1073741824 ? "N0" : "N1") + " GB" : null;

            Tile(Icons.Processor, Loc.S("Procesador", "Processor"), Short(hw?.Cpu ?? info.CpuName),
                hw?.Cores > 0 ? hw.Cores + (hw.Cores == 1 ? Loc.S(" núcleo · ", " core · ") : Loc.S(" núcleos · ", " cores · ")) + hw.Threads + (hw.Threads == 1 ? Loc.S(" hilo", " thread") : Loc.S(" hilos", " threads")) + (hw.CpuMaxMhz > 0 ? " · " + Loc.N(hw.CpuMaxMhz.Value / 1000.0, "N1") + " GHz" : "") : null);
            if (hw != null && hw.Gpus.Count > 0)
                foreach (var g in hw.Gpus)
                    Tile(Icons.Gaming, Loc.S("Gráfica", "Graphics"), g.Name, g.MemoryBytes > 0 ? Gb(g.MemoryBytes) + Loc.S(" de memoria propia", " of its own memory") : null);
            else Tile(Icons.Gaming, Loc.S("Gráfica", "Graphics"), string.Join(", ", info.Gpus), null);
            Tile(Icons.Memory, Loc.S("Memoria RAM", "Memory (RAM)"), Gb(hw?.RamBytes) ?? (info.TotalMemoryMb > 0 ? Loc.N(info.TotalMemoryMb / 1024.0, "N0") + " GB" : null),
                hw == null ? null : string.Join(" · ", new[] { hw.RamMhz > 0 ? hw.RamMhz + " MHz" : null, hw.RamModules > 0 ? hw.RamModules + Loc.S(" módulos", " modules") : null }.Where(x => x != null)),
                hw?.RamModules == 1 ? Loc.S("Un solo módulo: con dos iguales la memoria trabaja en doble canal y rinde más en juegos.", "A single module: with two matching ones memory runs in dual channel and games perform better.") : null);
            if (hw != null)
                foreach (var d in hw.Disks)
                    Tile(Icons.Disk, Loc.S("Disco", "Drive"), d.Name, string.Join(" · ", new[] { d.Kind, Gb(d.Bytes) }.Where(x => x != null)),
                        d.Kind == "HDD" ? Loc.S("Disco mecánico: un SSD hace que Windows y los juegos carguen varias veces más rápido.", "Hard drive: an SSD makes Windows and games load several times faster.") : null, "Moderate");
            if (hw?.DisplayWidth > 0)
            {
                bool below = hw.DisplayMaxHz > hw.DisplayHz;
                Tile(Icons.Display, Loc.S("Pantalla", "Display"), hw.DisplayWidth + " × " + hw.DisplayHeight, hw.DisplayHz + " Hz",
                    below ? Loc.S("Tu pantalla admite ", "Your display supports ") + hw.DisplayMaxHz + Loc.S(" Hz y va a ", " Hz and runs at ") + hw.DisplayHz + Loc.S(" Hz. Subirla es gratis y se nota al jugar.", " Hz. Raising it is free and noticeable in games.") : null,
                    "Moderate", below ? (Action)(() => Launcher.Open("ms-settings:display-advanced")) : null, Loc.S("Cambiar la frecuencia", "Change the refresh rate"));
            }
            Tile(Icons.Board, Loc.S("Placa base", "Motherboard"), hw?.Board, null);
            Tile(Icons.Windows, "Windows", hw?.Windows ?? (info.ProductName + " " + info.DisplayVersion), Loc.S("Compilación ", "Build ") + info.Build + "." + info.Ubr);
            if (hw == null) wrap.Children.Add(Ui.Muted(Loc.S("Leyendo los componentes…", "Reading the components…"), 13));
            return wrap;
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
            var panel = Ui.Panel(rows, 20);
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
                if (_hw == null) _hw = await Task.Run(() => Optimizzzer.Windows.WinHardware.Read());
                bool changed = (last?.Id != _last?.Id) || (drift.Count != (_drift?.Count ?? 0)) || _health == null || _hw != null;
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

            body.Children.Add(Ui.H2(Loc.S("Tu equipo", "Your PC")));
            body.Children.Add(HardwareCards());
            var healthHead = Ui.H2(Loc.S("Salud del equipo", "PC health"));
            healthHead.Margin = new Thickness(0, 20, 0, 8);
            body.Children.Add(healthHead);
            body.Children.Add(HealthCard());

            var go = Ui.Primary(Loc.S("Optimizar con un nivel", "Optimize with a level"), () => W.Navigate("levels"), Icons.SpeedMedium);
            var custom = Ui.Secondary(Loc.S("Elegir ajuste por ajuste", "Pick tweak by tweak"), () => W.Navigate("wizard"), Icons.Wizard);
            var actions = Ui.Row(8, go, custom);
            actions.Margin = new Thickness(0, 16, 0, 0);
            body.Children.Add(actions);
            return Frame(null, body);
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
