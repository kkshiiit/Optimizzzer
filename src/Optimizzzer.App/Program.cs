using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;
using Optimizzzer.Core.Catalog;
using Optimizzzer.Core.Tools;
using Optimizzzer.Windows;

namespace Optimizzzer.App
{
    public static class Program
    {
        /// <summary>True while -SelfTest runs: the welcome tour and other first-run prompts stay out of the way.</summary>
        public static bool InSelfTest { get; private set; }

        [STAThread]
        public static int Main(string[] args)
        {
            // Must be set before any WPF type loads: lets WPF re-render crisply when moved between monitors with different scaling.
            AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);

            var cli = CliOptions.Parse(args);
            if (cli.Lang.HasValue) { Loc.Lang = cli.Lang.Value; AppState.LanguageFromCommandLine = true; }
            if (cli.SelfTest) { InSelfTest = true; return SelfTest(); }
            if (cli.HasCommand || cli.Errors.Count > 0) return Cli.Run(cli);

            if (!WindowsSystem.IsAdministrator())
            {
                MessageBox.Show(Loc.S("Optimizzzer necesita permisos de administrador para cambiar la configuración de Windows. Ábrelo con clic derecho > Ejecutar como administrador.",
                    "Optimizzzer needs administrator rights to change Windows settings. Open it with right-click > Run as administrator."), "Optimizzzer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return 1;
            }

            AppDomain.CurrentDomain.UnhandledException += (_, e) => CrashLog(e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)));
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) => { CrashLog(e.Exception); e.SetObserved(); };
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            InstallUiContext();
            app.DispatcherUnhandledException += (_, e) =>
            {
                var path = CrashLog(e.Exception);
                MessageBox.Show(Loc.S("Ocurrió un error inesperado. Se guardó un informe en:\n", "An unexpected error occurred. A report was saved to:\n") + path + "\n\n" + e.Exception.Message,
                    "Optimizzzer", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            Theme.LoadStyles(app.Resources);
            var state = new AppState();
            Theme.Configure(state.SavedTheme, state.SavedAccent, state.SavedLite);
            Theme.WatchSystem();
            var window = new MainWindow(state);
            return app.Run(window);
        }

        /// <summary>
        /// WPF only installs its synchronization context once the dispatcher runs. The main window starts loading
        /// in its constructor, before that, so install it now: every await and Progress then returns to the UI thread.
        /// </summary>
        private static void InstallUiContext() =>
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

        private static string CrashLog(Exception ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Optimizzzer");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
                File.WriteAllText(path, DateTime.Now + Environment.NewLine + Environment.OSVersion + Environment.NewLine + ex);
                return path;
            }
            catch (Exception) { return "(not saved)"; }
        }

        /// <summary>
        /// Used by CI on a Windows runner: parses the styles, builds every page in both themes and languages,
        /// lays them out, and runs a read-only scan of the whole catalog. Changes nothing.
        /// </summary>
        private static int SelfTest()
        {
            var log = new StringBuilder();
            int failures = 0;
            var outPath = Path.Combine(Path.GetTempPath(), "optimizzzer-selftest.txt");
            void Flush() { try { File.WriteAllText(outPath, log.ToString()); } catch (Exception) { } }
            void Check(string name, Action a)
            {
                log.AppendLine("RUN  " + name + " " + DateTime.Now.ToString("HH:mm:ss"));
                Flush();
                try { a(); log.AppendLine("PASS " + name); }
                catch (Exception ex) { failures++; log.AppendLine("FAIL " + name + ": " + ex); }
                Flush();
            }
            // CI must never hang: if something blocks, record it and exit with a distinct code.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                lock (log) log.AppendLine("UNHANDLED " + e.ExceptionObject);
                Flush();
            };
            var watchdog = new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromMinutes(9));
                lock (log) log.AppendLine("SELFTEST TIMEOUT");
                Flush();
                Environment.Exit(2);
            }) { IsBackground = true };
            watchdog.Start();

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            InstallUiContext();
            Check("styles parse", () => Theme.LoadStyles(app.Resources));
            AppState state = null;
            Check("app state", () => state = new AppState());
            Check("catalog", () => { if (TweakCatalog.All.Count < 100) throw new Exception("catalog too small: " + TweakCatalog.All.Count); });
            Check("system context", () => state.EnsureReadyAsync().GetAwaiter().GetResult());
            Check("first launch scan through the UI", () =>
            {
                var fresh = new AppState();
                Theme.Configure(ThemeChoice.System, AccentChoice.System, LiteChoice.Auto);
                log.AppendLine("  lite mode: " + Theme.Lite + (Theme.LiteReason.Length > 0 ? " (" + Theme.LiteReason + ")" : "") + ", render tier " + (System.Windows.Media.RenderCapability.Tier >> 16));
                // Time to a visible window: what a user on a slow PC waits before seeing anything.
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var w = new MainWindow(fresh) { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
                long shownMs = -1;
                w.ContentRendered += (_, __) => { if (shownMs < 0) shownMs = clock.ElapsedMilliseconds; };
                w.Show();
                var until = DateTime.Now.AddSeconds(120);
                while ((fresh.ScanResults == null || shownMs < 0) && DateTime.Now < until) Pump();
                if (fresh.ScanResults == null) throw new Exception("the home scan did not finish");
                var workingSetMb = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
                log.AppendLine("  window visible after " + shownMs + " ms; working set after scan " + workingSetMb + " MB");
                if (shownMs > 2000) throw new Exception("window took " + shownMs + " ms to appear (limit 2000)");
                if (workingSetMb > 200) throw new Exception("working set " + workingSetMb + " MB (limit 200)");
                Pump();
                w.UpdateLayout();
                if (w.CurrentView == null || w.CurrentView.ActualHeight <= 0) throw new Exception("scan result view has no size");
                log.AppendLine("  wizard scanned " + fresh.ScanResults.Count + " tweaks");
                w.Close();
            });
            Check("read-only scan", () =>
            {
                state.ScanResults = state.Engine.Scan(TweakCatalog.All);
                var unknown = state.ScanResults.Where(r => r.Status == TweakStatus.Unknown).ToList();
                log.AppendLine("  scan: " + string.Join(", ", state.ScanResults.GroupBy(r => r.Status).Select(g => g.Key + "=" + g.Count())));
                foreach (var u in unknown) log.AppendLine("  unknown " + u.Tweak.Id + ": " + u.Error);
            });
            Check("accent contrast (all accents, both themes)", () =>
            {
                foreach (var dark in new[] { ThemeChoice.Light, ThemeChoice.Dark })
                    foreach (AccentChoice a in Enum.GetValues(typeof(AccentChoice)))
                    {
                        Theme.Configure(dark, a, LiteChoice.Auto);
                        var r = app.Resources;
                        (byte, byte, byte) C(string k) { var c = ((System.Windows.Media.SolidColorBrush)r[k]).Color; return (c.R, c.G, c.B); }
                        var onFill = ColorMath.Contrast(C("AccentFg"), C("Accent"));
                        var text = ColorMath.Contrast(C("AccentText"), C("Surface"));
                        if (onFill < 4.5 || text < 4.5) throw new Exception(dark + "/" + a + ": text on accent " + onFill.ToString("0.0") + ", accent text " + text.ToString("0.0"));
                    }
                Theme.Configure(ThemeChoice.Light, AccentChoice.System, LiteChoice.Auto);
            });
            Check("measurement", () => { var m = state.Win.Measurer.Take("selftest"); log.AppendLine("  memory used MB: " + m.MemoryUsedMb + ", processes: " + m.Processes); });
            Check("report", () => HtmlReport.Build(Lang.Es, state.Ctx.Info, null, TweakCatalog.All.ToList(), null, null, DateTime.Now));

            foreach (var theme in new[] { ThemeChoice.Light, ThemeChoice.Dark })
                foreach (var lang in new[] { Lang.Es, Lang.En })
                {
                    Check("pages " + theme + "/" + lang, () =>
                    {
                        Theme.Apply(theme);
                        Loc.Lang = lang;
                        var w = new MainWindow(state) { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
                        w.Show();
                        bool shots = (theme == ThemeChoice.Light && lang == Lang.Es) || (theme == ThemeChoice.Dark && lang == Lang.En);
                        void Verify(string what)
                        {
                            Pump();
                            if (shots) { Pump(); Pump(); Pump(); }
                            w.UpdateLayout();
                            var v = w.CurrentView;
                            if (v == null || v.ActualWidth <= 0 || v.ActualHeight <= 0) throw new Exception(what + " has no size");
                            if (shots) SelfTestTools.Screenshot(w, theme + "-" + lang + "-" + what.Replace(' ', '-'));
                        }
                        foreach (var p in w.Pages)
                        {
                            w.Navigate(p.Key);
                            Verify("page " + p.Key);
                        }
                        // Pick the recommended profile so the selection and review steps have content.
                        foreach (var t in Profiles.Select(state.ScanResults, Profiles.Recommend(state.Ctx.Info), state.Ctx.Info)) state.Selected.Add(t.Id);
                        w.Navigate("wizard");
                        w.Pages.OfType<Pages.WizardPage>().First().ShowStepsForTest(step => Verify("wizard step " + step));
                        state.Selected.Clear();

                        // Overlays: level sheet, Ctrl+K, tour, a toast and a confirmation.
                        w.Navigate("home");
                        Pump();
                        foreach (var level in new[] { Level.Quick, Level.Maximum })
                        {
                            w.PageOf<Pages.HomePage>().OpenLevel(level);
                            if (w.OverlayOpen) { Verify("level sheet " + level); w.CloseOverlay(); }
                            else log.AppendLine("  level " + level + ": nothing to apply here");
                        }
                        w.ShowPalette();
                        if (!w.OverlayOpen) throw new Exception("Ctrl+K palette did not open");
                        Verify("command palette");
                        w.CloseOverlay();
                        w.ShowTour();
                        Verify("tour");
                        w.CloseOverlay();
                        w.Toast(ToastKind.Success, "toast-check");
                        if (!w.ToastTexts.Contains("toast-check")) throw new Exception("toast not shown");
                        w.Close();
                    });
                }

            // Only on a throwaway machine (CI sets this variable): apply two tweaks through the wizard UI, then undo them.
            if (Environment.GetEnvironmentVariable("OPTIMIZZZER_SELFTEST_APPLY") == "1")
                Check("wizard apply and undo", () =>
                {
                    Theme.Apply(ThemeChoice.Light);
                    var w = new MainWindow(state) { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
                    w.Show();
                    var ids = new[] { "perf.menuDelay", "ui.fileExtensions", "gaming.gameDvr" };
                    foreach (var id in ids)
                    {
                        var t = TweakCatalog.Find(id);
                        state.Engine.Undo(id, TweakCatalog.All.ToList());
                        if (state.Engine.Detect(t) == TweakStatus.Applied) throw new Exception(id + " already applied on this machine; pick another");
                    }
                    state.Selected.Clear();
                    foreach (var id in ids) state.Selected.Add(id);
                    var wizard = w.Pages.OfType<Pages.WizardPage>().First();
                    w.Navigate("wizard");
                    wizard.StartApplyForTest();
                    var until = DateTime.Now.AddSeconds(90);
                    while (!wizard.ApplyFinishedForTest && DateTime.Now < until) Pump();
                    if (!wizard.ApplyFinishedForTest) throw new Exception("apply did not reach the result step");
                    var s = state.LastSession;
                    log.AppendLine("  " + string.Join(", ", s.Entries.Select(e => e.TweakId + "=" + e.Result)));
                    if (s.Entries.Count != ids.Length || s.Entries.Any(e => e.ResultKind != EntryResult.Applied)) throw new Exception("not every tweak was applied");
                    foreach (var id in ids)
                        if (state.Engine.Detect(TweakCatalog.Find(id)) != TweakStatus.Applied) throw new Exception(id + " not applied");
                    var undo = state.Engine.UndoSession(s.Id, TweakCatalog.All.ToList());
                    if (undo.Failed.Count > 0 || undo.Reverted.Count != ids.Length) throw new Exception("undo failed");
                    foreach (var id in ids)
                        if (state.Engine.Detect(TweakCatalog.Find(id)) == TweakStatus.Applied) throw new Exception(id + " still applied after undo");
                    w.Navigate("history");
                    Pump();
                    w.Close();
                });

            if (Environment.GetEnvironmentVariable("OPTIMIZZZER_SELFTEST_APPLY") == "1")
                SelfTestTools.Run(state, Check, s => log.AppendLine("  " + s));

            if (Environment.GetEnvironmentVariable("OPTIMIZZZER_SELFTEST_APPLY") == "1")
                Check("one-click Quick level, then undo", () =>
                {
                    state.ScanResults = state.Engine.Scan(TweakCatalog.All);
                    var tweaks = Levels.Select(state.ScanResults, Level.Quick, state.Ctx.Info).Where(t => t.Reversible).ToList();
                    log.AppendLine("  Quick level would change " + tweaks.Count + " tweaks here");
                    if (tweaks.Count == 0) return;
                    var w = new MainWindow(state) { ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000 };
                    w.Show();
                    var wizard = w.PageOf<Pages.WizardPage>();
                    w.Navigate("wizard");
                    wizard.StartLevel(Levels.Get(Level.Quick), tweaks, restorePoint: false);
                    var until = DateTime.Now.AddSeconds(120);
                    while (!wizard.ApplyFinishedForTest && DateTime.Now < until) Pump();
                    if (!wizard.ApplyFinishedForTest) throw new Exception("level apply did not finish");
                    Pump(); w.UpdateLayout();
                    SelfTestTools.Screenshot(w, "Light-Es-level-result");
                    var s = state.LastSession;
                    var bad = s.Entries.Where(e => e.ResultKind == EntryResult.Failed).Select(e => e.TweakId + ": " + e.Error).ToList();
                    if (bad.Count > 0) throw new Exception("failed: " + string.Join("; ", bad));
                    var undo = state.Engine.UndoSession(s.Id, TweakCatalog.All.ToList());
                    if (undo.Failed.Count > 0) throw new Exception("undo failed: " + string.Join(", ", undo.Failed));
                    foreach (var t in tweaks)
                        if (state.Engine.Detect(t) == TweakStatus.Applied) throw new Exception(t.Id + " still applied after undo");
                    log.AppendLine("  applied " + s.Entries.Count(e => e.ResultKind == EntryResult.Applied) + ", undone " + undo.Reverted.Count);
                    w.Close();
                });

            log.AppendLine(failures == 0 ? "SELFTEST OK" : "SELFTEST FAILED: " + failures);
            Flush();
            WindowsSystem.AttachParentConsole();
            Console.WriteLine(log.ToString());
            app.Shutdown();
            return failures == 0 ? 0 : 1;
        }

        /// <summary>Lets queued UI work (loads, layout) run, with a time limit so CI never hangs.</summary>
        internal static void Pump()
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (_, __) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }

    /// <summary>Unattended mode. Writes to the console the exe was started from.</summary>
    internal static class Cli
    {
        public static int Run(CliOptions o)
        {
            WindowsSystem.AttachParentConsole();
            Console.WriteLine();
            if (o.Help || o.Errors.Count > 0)
            {
                foreach (var e in o.Errors) Console.WriteLine("Error: " + e);
                Console.WriteLine(CliOptions.Usage);
                return o.Errors.Count > 0 ? 2 : 0;
            }
            var lang = Loc.Lang;
            if (o.List)
            {
                foreach (var t in TweakCatalog.All)
                    Console.WriteLine(t.Id.PadRight(34) + t.Category.ToString().PadRight(13) + HtmlReport.RiskLabel(t.Risk, lang).PadRight(10) + t.Title[lang]);
                return 0;
            }

            var win = new WindowsSystem();
            if (!WindowsSystem.IsAdministrator() && (o.Silent || o.Undo != null))
            {
                Console.WriteLine(Loc.S("Se necesitan permisos de administrador.", "Administrator rights are required."));
                return 3;
            }
            var ctx = win.CreateContext();
            var engine = new TweakEngine(ctx, win.Journal);

            if (o.Undo != null)
            {
                var catalog = ManualTweaks.WithJournal(TweakCatalog.All, engine.History().Select(h => h.Entry.TweakId));
                UndoResult r;
                if (o.Undo.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    r = new UndoResult();
                    foreach (var s in win.Journal.LoadAll().Reverse())
                    {
                        var part = engine.UndoSession(s.Id, catalog);
                        r.Reverted.AddRange(part.Reverted); r.Failed.AddRange(part.Failed); r.NotReversible.AddRange(part.NotReversible);
                    }
                }
                else if (win.Journal.LoadAll().Any(s => s.Id.Equals(o.Undo, StringComparison.OrdinalIgnoreCase))) r = engine.UndoSession(o.Undo, catalog);
                else r = engine.Undo(o.Undo, catalog);
                foreach (var id in r.Reverted) Console.WriteLine("  undone          " + id);
                foreach (var id in r.NotReversible) Console.WriteLine("  not reversible  " + id);
                foreach (var id in r.Failed) Console.WriteLine("  FAILED          " + id);
                if (r.NothingFound && !o.Undo.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Loc.S("No hay nada que deshacer para: ", "Nothing to undo for: ") + o.Undo
                        + Loc.S(" (usa -List para ver los ids o el id de sesión que mostró -Silent)", " (see -List for ids, or use the session id printed by -Silent)"));
                    return 2;
                }
                return r.Failed.Count == 0 ? 0 : 1;
            }

            var scan = engine.Scan(TweakCatalog.All);
            if (o.Scan && !o.Profile.HasValue && !o.Level.HasValue && o.ApplyFile == null && o.Apply.Count == 0)
            {
                foreach (var r in scan)
                    Console.WriteLine(r.Status.ToString().PadRight(14) + r.Tweak.Id.PadRight(34) + r.Tweak.Title[lang]);
                return 0;
            }

            var chosen = o.Profile.HasValue ? Profiles.Select(scan, o.Profile.Value, ctx.Info).ToList() : new System.Collections.Generic.List<Tweak>();
            if (o.Level.HasValue)
                foreach (var t in Levels.Select(scan, o.Level.Value, ctx.Info)) if (!chosen.Contains(t)) chosen.Add(t);
            if (o.ApplyFile != null)
            {
                System.Collections.Generic.List<Tweak> known; System.Collections.Generic.List<string> unknown;
                try { (known, unknown) = SelectionFile.Read(File.ReadAllText(o.ApplyFile), TweakCatalog.All); }
                catch (Exception ex) { Console.WriteLine(Loc.S("No se pudo leer ", "Could not read ") + o.ApplyFile + ": " + ex.Message); return 2; }
                foreach (var id in unknown) Console.WriteLine(Loc.S("  ignorado (no existe en esta versión): ", "  ignored (not in this version): ") + id);
                foreach (var t in known)
                {
                    var st = scan.First(r => r.Tweak == t).Status;
                    if ((st == TweakStatus.NotApplied || st == TweakStatus.Partial) && !chosen.Contains(t)) chosen.Add(t);
                }
            }
            foreach (var id in o.Apply)
            {
                var t = TweakCatalog.Find(id);
                if (t == null) { Console.WriteLine("Unknown tweak id: " + id); return 2; }
                if (!chosen.Contains(t)) chosen.Add(t);
            }
            if (chosen.Count == 0) { Console.WriteLine(Loc.S("No hay nada que aplicar.", "Nothing to apply.")); return 0; }

            Console.WriteLine(chosen.Count + Loc.S(" cambios:", " changes:"));
            foreach (var t in chosen)
            {
                Console.WriteLine("  [" + HtmlReport.RiskLabel(t.Risk, lang) + "] " + t.Id + "  " + t.Title[lang]);
                foreach (var a in t.Actions) Console.WriteLine("      " + a.Describe(lang));
            }
            if (o.DryRun || !o.Silent)
            {
                if (!o.DryRun) Console.WriteLine(Loc.S("Añade -Silent para aplicar.", "Add -Silent to apply."));
                return 0;
            }

            var before = win.Measurer.Take("before");
            var progress = new SyncProgress<ApplyProgress>(p => { if (p.LastEntry != null) Console.WriteLine("  " + p.LastEntry.Result.PadRight(8) + p.Current.Id + (p.LastEntry.Error != null ? "  " + p.LastEntry.Error : "")); });
            var session = engine.Apply(chosen, new ApplyOptions { CreateRestorePoint = !o.NoRestorePoint }, progress, CancellationToken.None);
            Console.WriteLine(session.RestorePointCreated ? Loc.S("Punto de restauración creado.", "Restore point created.")
                : o.NoRestorePoint ? Loc.S("Sin punto de restauración (-NoRestorePoint).", "No restore point (-NoRestorePoint).")
                : Loc.S("No se pudo crear el punto de restauración: ", "Could not create a restore point: ") + session.RestorePointError);
            Console.WriteLine(Loc.S("Sesión: ", "Session: ") + session.Id + Loc.S("  (deshacer: -Undo ", "  (undo: -Undo ") + session.Id + ")");
            if (o.ReportPath != null)
                File.WriteAllText(o.ReportPath, HtmlReport.Build(lang, ctx.Info, session, TweakCatalog.All.ToList(), before, win.Measurer.Take("after"), DateTime.Now), new UTF8Encoding(false));
            return session.Entries.Any(e => e.ResultKind == EntryResult.Failed) ? 1 : 0;
        }
    }

    /// <summary>Reports on the calling thread, so console lines come out in order.</summary>
    internal sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _a;
        public SyncProgress(Action<T> a) { _a = a; }
        public void Report(T value) => _a(value);
    }
}
