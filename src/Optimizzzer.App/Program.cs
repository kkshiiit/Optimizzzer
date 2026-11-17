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
        [STAThread]
        public static int Main(string[] args)
        {
            // Must be set before any WPF type loads: lets WPF re-render crisply when moved between monitors with different scaling.
            AppContext.SetSwitch("Switch.System.Windows.DoNotScaleForDpiChanges", false);

            var cli = CliOptions.Parse(args);
            if (cli.Lang.HasValue) Loc.Lang = cli.Lang.Value;
            if (cli.SelfTest) return SelfTest();
            if (cli.HasCommand || cli.Errors.Count > 0) return Cli.Run(cli);

            if (!WindowsSystem.IsAdministrator())
            {
                MessageBox.Show(Loc.S("Optimizzzer necesita permisos de administrador para cambiar la configuración de Windows. Ábrelo con clic derecho > Ejecutar como administrador.",
                    "Optimizzzer needs administrator rights to change Windows settings. Open it with right-click > Run as administrator."), "Optimizzzer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return 1;
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (_, e) =>
            {
                var path = CrashLog(e.Exception);
                MessageBox.Show(Loc.S("Ocurrió un error inesperado. Se guardó un informe en:\n", "An unexpected error occurred. A report was saved to:\n") + path + "\n\n" + e.Exception.Message,
                    "Optimizzzer", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            Theme.LoadStyles(app.Resources);
            var state = new AppState();
            Theme.Apply(state.SavedTheme);
            Theme.WatchSystem();
            var window = new MainWindow(state);
            return app.Run(window);
        }

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
            var watchdog = new Thread(() =>
            {
                Thread.Sleep(TimeSpan.FromMinutes(5));
                lock (log) log.AppendLine("SELFTEST TIMEOUT");
                Flush();
                Environment.Exit(2);
            }) { IsBackground = true };
            watchdog.Start();

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Check("styles parse", () => Theme.LoadStyles(app.Resources));
            AppState state = null;
            Check("app state", () => state = new AppState());
            Check("catalog", () => { if (TweakCatalog.All.Count < 100) throw new Exception("catalog too small: " + TweakCatalog.All.Count); });
            Check("system context", () => state.EnsureReadyAsync().GetAwaiter().GetResult());
            Check("read-only scan", () =>
            {
                state.ScanResults = state.Engine.Scan(TweakCatalog.All);
                var unknown = state.ScanResults.Where(r => r.Status == TweakStatus.Unknown).ToList();
                log.AppendLine("  scan: " + string.Join(", ", state.ScanResults.GroupBy(r => r.Status).Select(g => g.Key + "=" + g.Count())));
                foreach (var u in unknown) log.AppendLine("  unknown " + u.Tweak.Id + ": " + u.Error);
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
                        void Verify(string what)
                        {
                            Pump();
                            w.UpdateLayout();
                            var v = w.CurrentView;
                            if (v == null || v.ActualWidth <= 0 || v.ActualHeight <= 0) throw new Exception(what + " has no size");
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
                        w.Close();
                    });
                }

            log.AppendLine(failures == 0 ? "SELFTEST OK" : "SELFTEST FAILED: " + failures);
            Flush();
            WindowsSystem.AttachParentConsole();
            Console.WriteLine(log.ToString());
            app.Shutdown();
            return failures == 0 ? 0 : 1;
        }

        /// <summary>Lets queued UI work (loads, layout) run, with a time limit so CI never hangs.</summary>
        private static void Pump()
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
                        r.Reverted.AddRange(part.Reverted); r.Failed.AddRange(part.Failed);
                    }
                }
                else if (win.Journal.LoadAll().Any(s => s.Id == o.Undo)) r = engine.UndoSession(o.Undo, catalog);
                else r = engine.Undo(o.Undo, catalog);
                foreach (var id in r.Reverted) Console.WriteLine("  undone  " + id);
                foreach (var id in r.Failed) Console.WriteLine("  FAILED  " + id);
                return r.Failed.Count == 0 ? 0 : 1;
            }

            var scan = engine.Scan(TweakCatalog.All);
            if (o.Scan && !o.Profile.HasValue && o.Apply.Count == 0)
            {
                foreach (var r in scan)
                    Console.WriteLine(r.Status.ToString().PadRight(14) + r.Tweak.Id.PadRight(34) + r.Tweak.Title[lang]);
                return 0;
            }

            var chosen = o.Profile.HasValue ? Profiles.Select(scan, o.Profile.Value, ctx.Info).ToList() : new System.Collections.Generic.List<Tweak>();
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
            var progress = new Progress<ApplyProgress>(p => { if (p.LastEntry != null) Console.WriteLine("  " + p.LastEntry.Result.PadRight(8) + p.Current.Id + (p.LastEntry.Error != null ? "  " + p.LastEntry.Error : "")); });
            var session = engine.Apply(chosen, new ApplyOptions { CreateRestorePoint = !o.NoRestorePoint }, progress, CancellationToken.None);
            Console.WriteLine(session.RestorePointCreated ? Loc.S("Punto de restauración creado.", "Restore point created.") : Loc.S("Sin punto de restauración: ", "No restore point: ") + session.RestorePointError);
            Console.WriteLine(Loc.S("Sesión: ", "Session: ") + session.Id + Loc.S("  (deshacer: -Undo ", "  (undo: -Undo ") + session.Id + ")");
            if (o.ReportPath != null)
                File.WriteAllText(o.ReportPath, HtmlReport.Build(lang, ctx.Info, session, TweakCatalog.All.ToList(), before, win.Measurer.Take("after"), DateTime.Now), new UTF8Encoding(false));
            return session.Entries.Any(e => e.ResultKind == EntryResult.Failed) ? 1 : 0;
        }
    }
}
