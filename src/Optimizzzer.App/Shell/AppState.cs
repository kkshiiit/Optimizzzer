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
                    if (kv[0] == "lang" && Enum.TryParse<Lang>(kv[1], out var l)) Loc.Lang = l;
                }
            }
            catch (Exception) { }
        }

        public ThemeChoice SavedTheme { get; private set; } = ThemeChoice.System;

        public void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Win.DataDir);
                File.WriteAllLines(SettingsPath, new[] { "theme=" + Theme.Choice, "lang=" + Loc.Lang });
            }
            catch (Exception) { }
        }
    }
}
