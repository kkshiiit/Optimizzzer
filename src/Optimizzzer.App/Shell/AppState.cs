using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Optimizzzer.Core;
using Optimizzzer.Core.Catalog;
using Optimizzzer.Core.Tools;
using Optimizzzer.Windows;

namespace Optimizzzer.App.Shell
{
    /// <summary>Everything the pages share. Lives for the whole session.</summary>
    public sealed class AppState
    {
        public AppState()
        {
            Win = new WindowsSystem();
            LoadSettings();
        }

        /// <summary>Set when -Lang was given: it wins over the saved preference.</summary>
        public static bool LanguageFromCommandLine { get; set; }

        public WindowsSystem Win { get; }
        public SystemContext Ctx { get; private set; }
        public TweakEngine Engine { get; private set; }
        public IReadOnlyList<Tweak> Catalog => TweakCatalog.All;

        public IList<ScanResult> ScanResults { get; set; }
        public HashSet<string> Selected { get; } = new HashSet<string>();
        public ProfileKind? Profile { get; set; }
        public JournalSession LastSession { get; set; }
        public Measurement Before { get; set; }
        public Measurement After { get; set; }
        public bool CreateRestorePoint { get; set; } = true;

        private Task _init;

        /// <summary>WMI and registry reads happen once, off the UI thread.</summary>
        public Task EnsureReadyAsync()
        {
            return _init ?? (_init = Task.Run(() =>
            {
                Ctx = Win.CreateContext();
                Engine = new TweakEngine(Ctx, Win.Journal);
            }));
        }

        public void ResetInit() => _init = null;

        // ---------- Shared scan (Home and the wizard both use it) ----------

        public event Action<double, string> ScanProgress;
        public event Action ScanFinished;
        private Task _scan;
        public bool Scanning => _scan != null && !_scan.IsCompleted;
        public string ScanError { get; private set; }

        /// <summary>Starts the read-only scan once; callers that arrive while it runs share the same task.</summary>
        public Task ScanAsync(bool rescan = false)
        {
            if (Scanning) return _scan;
            if (!rescan && ScanResults != null) return Task.CompletedTask;
            _scan = DoScanAsync();
            return _scan;
        }

        private async Task DoScanAsync()
        {
            ScanError = null;
            try
            {
                ScanProgress?.Invoke(0, Loc.S("Leyendo la información del equipo…", "Reading PC information…"));
                await EnsureReadyAsync();
                var tweaks = Catalog.ToList();
                var progress = new Progress<int>(n => ScanProgress?.Invoke(n / (double)tweaks.Count,
                    Loc.S("Comprobando ajuste ", "Checking tweak ") + n + Loc.S(" de ", " of ") + tweaks.Count + "…"));
                ScanResults = await Task.Run(() => Engine.Scan(tweaks, progress));
            }
            catch (Exception ex)
            {
                ScanError = ex.Message;
                ResetInit();
            }
            ScanFinished?.Invoke();
        }

        /// <summary>Re-detects the given tweaks (all when null) off the UI thread, after anything changed them.</summary>
        public Task RescanAsync(IEnumerable<Tweak> only = null)
        {
            if (ScanResults == null || Engine == null) return Task.CompletedTask;
            var set = only == null ? null : new HashSet<Tweak>(only);
            return Task.Run(() =>
            {
                for (int i = 0; i < ScanResults.Count; i++)
                    if (set == null || set.Contains(ScanResults[i].Tweak))
                        ScanResults[i] = new ScanResult(ScanResults[i].Tweak, Engine.Detect(ScanResults[i].Tweak));
            });
        }

        public ScanResult Result(Tweak t) => ScanResults?.FirstOrDefault(r => r.Tweak == t);

        public TweakStatus StatusOf(Tweak t) => Result(t)?.Status ?? TweakStatus.Unknown;

        /// <summary>Catalog plus manual service/task changes found in the journal (for undo).</summary>
        public IList<Tweak> UndoCatalog() =>
            ManualTweaks.WithJournal(Catalog, Engine.History().Select(h => h.Entry.TweakId));

        // Settings are a tiny text file next to the journal, so nothing is written to the user's profile.
        private string SettingsPath => Path.Combine(Win.DataDir, "settings.txt");

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    var kv = line.Split(new[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    if (kv[0] == "theme" && Enum.TryParse<ThemeChoice>(kv[1], out var t)) SavedTheme = t;
                    if (kv[0] == "accent" && Enum.TryParse<AccentChoice>(kv[1], out var a)) SavedAccent = a;
                    if (kv[0] == "lite" && Enum.TryParse<LiteChoice>(kv[1], out var li)) SavedLite = li;
                    if (kv[0] == "tour" && kv[1] == "done") TourDone = true;
                    if (kv[0] == "lang" && !LanguageFromCommandLine && Enum.TryParse<Lang>(kv[1], out var l)) Loc.Lang = l;
                }
            }
            catch (Exception) { }
        }

        // Dark by default (owner's choice); Light and System stay one click away in Settings.
        public ThemeChoice SavedTheme { get; private set; } = ThemeChoice.Dark;
        public AccentChoice SavedAccent { get; private set; } = AccentChoice.System;
        public LiteChoice SavedLite { get; private set; } = LiteChoice.Auto;
        public bool TourDone { get; set; }

        public void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Win.DataDir);
                File.WriteAllLines(SettingsPath, new[]
                {
                    "theme=" + Theme.Choice, "accent=" + Theme.Accent, "lite=" + Theme.LiteSetting,
                    "lang=" + Loc.Lang, "tour=" + (TourDone ? "done" : "pending"),
                });
            }
            catch (Exception) { }
        }
    }
}
