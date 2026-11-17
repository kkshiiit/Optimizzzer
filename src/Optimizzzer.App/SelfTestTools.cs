using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;
using Optimizzzer.Windows;

namespace Optimizzzer.App
{
    /// <summary>
    /// Self-test steps that change the machine. They only run when CI sets OPTIMIZZZER_SELFTEST_APPLY=1
    /// on a throwaway runner, and each one puts back what it changed.
    /// </summary>
    internal static class SelfTestTools
    {
        public static readonly string ShotsDir = Path.Combine(Path.GetTempPath(), "optimizzzer-shots");

        public static void Screenshot(Window w, string name)
        {
            Directory.CreateDirectory(ShotsDir);
            int width = (int)Math.Ceiling(w.ActualWidth), height = (int)Math.Ceiling(w.ActualHeight);
            if (width <= 0 || height <= 0) return;
            var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(w);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            var safe = string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
            using (var fs = File.Create(Path.Combine(ShotsDir, safe + ".png"))) enc.Save(fs);
        }

        public static void Run(AppState state, Action<string, Action> check, Action<string> log)
        {
            var win = state.Win;

            check("restore point", () =>
            {
                const string key = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
                var before = win.Registry.GetValue(Hive.LocalMachine, key, "SystemRestorePointCreationFrequency");
                var err = new WinRestorePoint(win.Registry).Create("Optimizzzer self-test");
                log(err == null ? "restore point created" : "restore point not available here (expected on Windows Server): " + err);
                var after = win.Registry.GetValue(Hive.LocalMachine, key, "SystemRestorePointCreationFrequency");
                if (!Equals(before, after)) throw new Exception("the 24 h frequency limit was not put back");
            });

            check("cleanup", () =>
            {
                var dir = Path.Combine(Path.GetTempPath(), "optz-selftest");
                Directory.CreateDirectory(dir);
                var oldFile = Path.Combine(dir, "old.tmp");
                var newFile = Path.Combine(dir, "new.tmp");
                File.WriteAllBytes(oldFile, new byte[4096]);
                File.WriteAllBytes(newFile, new byte[100]);
                File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-3));
                var engine = new CleanupEngine();
                foreach (var t in CleanupCatalog.All)
                {
                    var e = engine.Estimate(t, win.Cleanup);
                    log("estimate " + t.Id + ": " + (e.Measurable ? e.Bytes + " bytes in " + e.Files + " files" : "measured when run"));
                }
                foreach (var id in new[] { "userTemp", "thumbs", "crashDumps", "recycleBin", "wuCache" })
                {
                    var r = engine.Run(CleanupCatalog.All.First(t => t.Id == id), win.Cleanup);
                    log("clean " + id + ": freed " + r.FreedBytes + ", deleted " + r.Deleted + ", skipped " + r.Skipped + (r.Error != null ? ", error " + r.Error : ""));
                    if (r.Error != null) throw new Exception(id + ": " + r.Error);
                }
                if (File.Exists(oldFile)) throw new Exception("old temp file was not deleted");
                if (!File.Exists(newFile)) throw new Exception("recent temp file was deleted");
                using (var sc = new System.ServiceProcess.ServiceController("wuauserv"))
                    log("wuauserv after cleanup: " + sc.Status);
                Directory.Delete(dir, true);
            });

            check("dns change and restore", () =>
            {
                var adapters = win.Dns.Adapters();
                if (adapters.Count == 0) { log("no connected adapter, skipped"); return; }
                var a = adapters[0];
                var original = string.Join(",", a.Servers.OrderBy(x => x));
                log("adapter " + a.Name + " servers " + original + (a.Automatic ? " (automatic)" : " (static)"));
                var cloudflare = DnsCatalog.All.First(p => p.Id == "cloudflare");
                try
                {
                    win.Dns.Apply(cloudflare, new[] { a });
                    var now = win.Dns.Adapters().First(x => x.Id == a.Id);
                    log("after apply: " + string.Join(",", now.Servers));
                    if (!now.Servers.Contains("1.1.1.1")) throw new Exception("1.1.1.1 was not set");
                    var lat = DnsBenchmark.MeasureAsync(cloudflare).GetAwaiter().GetResult();
                    log("cloudflare median " + (lat.MedianMs?.ToString("0.0") ?? "no answer") + " ms, failures " + lat.Failures);
                }
                finally
                {
                    win.Dns.Restore();
                }
                var restored = string.Join(",", win.Dns.Adapters().First(x => x.Id == a.Id).Servers.OrderBy(x => x));
                log("after restore: " + restored);
                if (restored != original) throw new Exception("DNS not restored: " + restored + " vs " + original);
                if (win.Dns.HasBackup) throw new Exception("backup file left behind");
            });

            check("remove a Store app", () =>
            {
                var apps = win.Appx.List();
                log(apps.Count + " Appx packages, " + apps.Count(x => !x.IsSystem && !x.IsFramework && !x.NonRemovable) + " removable");
                var target = new[] { "Microsoft.WindowsFeedbackHub", "Microsoft.GetHelp", "Microsoft.Getstarted", "Microsoft.BingWeather", "Microsoft.MicrosoftSolitaireCollection" }
                    .FirstOrDefault(n => apps.Any(x => x.Name == n));
                if (target == null) { log("none of the test apps is installed here, removal skipped"); return; }
                win.Appx.Remove(target);
                if (win.Appx.List().Any(x => x.Name == target)) throw new Exception(target + " still installed");
                log("removed " + target);
            });

            check("repair (network reset and DISM health check)", () =>
            {
                var task = RepairCatalog.All.First(t => t.Id == "network");
                foreach (var step in task.Steps)
                {
                    int lines = 0;
                    var code = win.Process.RunStreaming(step.File, step.Args, _ => lines++, System.Threading.CancellationToken.None);
                    log(step + " exit " + code + ", " + lines + " lines");
                    if (code != 0 && !step.TolerateExitCode) throw new Exception(step + " failed");
                }
                string last = null;
                var dism = win.Process.RunStreaming("DISM.exe", "/Online /Cleanup-Image /CheckHealth", l => last = l, System.Threading.CancellationToken.None);
                log("DISM CheckHealth exit " + dism + ": " + last);
                if (dism != 0) throw new Exception("DISM failed");
                if (last != null && last.IndexOf('\0') >= 0) throw new Exception("UTF-16 output not cleaned");
            });

            check("startup programs toggle", () =>
            {
                const string run = @"Software\Microsoft\Windows\CurrentVersion\Run";
                const string name = "OptimizzzerSelfTest";
                win.Registry.SetValue(Hive.CurrentUser, run, name, RegValue.Str("\"" + Path.Combine(Environment.SystemDirectory, "notepad.exe") + "\""));
                try
                {
                    var item = win.Startup.List().First(i => i.Name == name);
                    if (!item.Enabled) throw new Exception("new entry should start enabled");
                    win.Startup.SetEnabled(item, false);
                    if (win.Startup.List().First(i => i.Name == name).Enabled) throw new Exception("disable did not stick");
                    win.Startup.SetEnabled(item, true);
                    if (!win.Startup.List().First(i => i.Name == name).Enabled) throw new Exception("enable did not stick");
                    log("startup list " + win.Startup.List().Count + " entries; toggle works");
                }
                finally
                {
                    win.Registry.DeleteValue(Hive.CurrentUser, run, name);
                    win.Registry.DeleteValue(Hive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", name);
                }
            });

            check("services and scheduled tasks lists", () =>
            {
                var services = ServiceList.Load(win.Services);
                var tasks = win.Tasks.List();
                log(services.Count + " services (" + services.Count(x => x.Protected) + " locked), " + tasks.Count + " scheduled tasks");
                if (services.Count < 50 || !services.Any(x => x.Name == "RpcSs" && x.Protected)) throw new Exception("service list looks wrong");
                if (tasks.Count < 20) throw new Exception("task list looks wrong");
            });

            check("html report file", () =>
            {
                var path = Path.Combine(ShotsDir, "report.html");
                Directory.CreateDirectory(ShotsDir);
                File.WriteAllText(path, HtmlReport.Build(Lang.Es, state.Ctx.Info, state.LastSession, state.Catalog.ToList(), state.Before, state.After, DateTime.Now));
                log("report " + new FileInfo(path).Length + " bytes");
            });
        }
    }
}
