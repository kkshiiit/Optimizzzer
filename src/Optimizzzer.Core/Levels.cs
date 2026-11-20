using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimizzzer.Core
{
    /// <summary>The one-click levels on the home screen. Each level contains everything in the levels below it.</summary>
    public enum Level { Quick = 1, Balanced = 2, Gaming = 3, Maximum = 4 }

    public sealed class LevelInfo
    {
        public Level Level { get; set; }
        public Text Name { get; set; }
        public Text Tagline { get; set; }
        /// <summary>What the user gains, in plain words. Never a number: the gain depends on the PC and the game.</summary>
        public Text Gains { get; set; }
        public Text Duration { get; set; }
        /// <summary>Only the tweaks this level adds on top of the one below.</summary>
        public string[] Adds { get; set; }
        /// <summary>Levels that change more than settings ask for a second confirmation.</summary>
        public bool NeedsSecondConfirmation { get; set; }
    }

    public static class Levels
    {
        public static readonly IReadOnlyList<LevelInfo> All = new[]
        {
            new LevelInfo
            {
                Level = Level.Quick,
                Name = new Text("Rápido", "Quick"),
                Tagline = new Text("Lo básico y seguro, en menos de un minuto.", "The safe basics, in under a minute."),
                Gains = new Text("Menos telemetría y menos procesos en segundo plano. Sin anuncios en Inicio, la pantalla de bloqueo ni el Explorador.",
                                 "Less telemetry and fewer background processes. No ads in Start, the lock screen or File Explorer."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Adds = new[]
                {
                    "privacy.telemetry", "privacy.diagtrack", "privacy.telemetryTasks", "privacy.advertisingId", "privacy.tailored",
                    "privacy.activityHistory", "privacy.feedback", "privacy.settingsSuggestions", "privacy.webSearch", "privacy.cortana",
                    "ai.copilot", "ai.recall", "ui.widgets", "ui.silentApps", "ui.tips", "ui.lockScreenAds", "ui.startRecommendations",
                    "ui.syncProviderAds", "ui.fileExtensions", "svc.retailDemo", "update.deliveryOptimization", "perf.edgeBackground",
                },
            },
            new LevelInfo
            {
                Level = Level.Balanced,
                Name = new Text("Equilibrado", "Balanced"),
                Tagline = new Text("Un escritorio más ágil y limpio para el día a día.", "A snappier, cleaner desktop for every day."),
                Gains = new Text("Menús y ventanas que responden al instante, apps de inicio sin retraso y sin reinicios por sorpresa. Además quita apps preinstaladas que casi nadie usa.",
                                 "Menus and windows that respond instantly, startup apps with no delay, and no surprise restarts. Also removes preinstalled apps almost nobody uses."),
                Duration = new Text("1 a 3 minutos", "1 to 3 minutes"),
                Adds = new[]
                {
                    "perf.menuDelay", "perf.startupDelay", "perf.animations", "perf.storageSense", "gaming.gameDvr", "update.noAutoRestart",
                    "svc.maps", "svc.fax", "svc.wmpNetwork", "ui.endTask", "ui.chatIcon", "ui.meetNow",
                    "app.bingNews", "app.bingSearch", "app.tips", "app.feedbackHub", "app.mixedReality", "app.3dViewer",
                    "app.cortanaApp", "app.teamsPersonal", "app.devHome", "app.candyCrush", "app.candyCrushSoda",
                },
            },
            new LevelInfo
            {
                Level = Level.Gaming,
                Name = new Text("Juegos", "Gaming"),
                Tagline = new Text("Ajustes que influyen al jugar: latencia, grabación y ratón.", "Settings that matter while gaming: latency, recording and mouse."),
                Gains = new Text("Game Mode activo, sin grabación continua, puntería consistente y menos latencia en juegos en ventana. La ganancia de FPS depende del juego y del equipo, y se mide tras aplicar.",
                                 "Game Mode on, no continuous recording, consistent aim and lower latency in windowed games. The FPS gain depends on the game and the PC, and is measured after applying."),
                Duration = new Text("1 a 3 minutos y reinicio", "1 to 3 minutes plus a restart"),
                Adds = new[]
                {
                    "gaming.gameMode", "gaming.gameBarTips", "gaming.mouseAccel", "gaming.windowedOptimizations", "gaming.hags",
                    "ui.stickyKeys", "perf.transparency",
                },
            },
            new LevelInfo
            {
                Level = Level.Maximum,
                Name = new Text("Máximo rendimiento", "Maximum performance"),
                Tagline = new Text("Todo lo que tiene evidencia, aunque pierdas alguna función.", "Everything with evidence, even if you lose some features."),
                Gains = new Text("El procesador no entra en ahorro, no hay indexador ni apps de la Store en segundo plano, y se libera el espacio de la hibernación. Es lo máximo que se puede ajustar sin ajustes placebo.",
                                 "The processor never enters power saving, there is no indexer and no Store apps in the background, and the hibernation file is freed. This is as far as tuning goes without placebo tweaks."),
                Duration = new Text("2 a 5 minutos y reinicio", "2 to 5 minutes plus a restart"),
                NeedsSecondConfirmation = true,
                Adds = new[]
                {
                    "power.ultimate", "power.hibernate", "power.throttling", "perf.searchIndex", "perf.backgroundApps", "perf.lastAccess",
                    "app.copilotApp", "app.clipchamp", "app.powerAutomate", "app.people", "app.maps", "app.moviesTv", "app.getHelp", "app.bingWeather",
                },
            },
        };

        public static LevelInfo Get(Level l) => All.First(x => x.Level == l);

        /// <summary>Every tweak id up to and including this level.</summary>
        public static IEnumerable<string> IdsUpTo(Level level) => All.Where(l => l.Level <= level).SelectMany(l => l.Adds);

        /// <summary>The tweaks this level would change on this PC: applicable, not applied yet, and not avoided by the hardware.</summary>
        public static IList<Tweak> Select(IEnumerable<ScanResult> scan, Level level, ISystemInfo info)
        {
            var ids = new HashSet<string>(IdsUpTo(level), StringComparer.OrdinalIgnoreCase);
            return scan.Where(r => ids.Contains(r.Tweak.Id)
                                   && (r.Status == TweakStatus.NotApplied || r.Status == TweakStatus.Partial)
                                   && (r.Tweak.AvoidOn == null || info == null || !r.Tweak.AvoidOn(info)))
                       .Select(r => r.Tweak).ToList();
        }

        /// <summary>How many of this level's tweaks that apply to this PC are already in place.</summary>
        public static (int Applied, int Total) Progress(IEnumerable<ScanResult> scan, Level level)
        {
            var ids = new HashSet<string>(IdsUpTo(level), StringComparer.OrdinalIgnoreCase);
            var relevant = scan.Where(r => ids.Contains(r.Tweak.Id) && r.Status != TweakStatus.NotApplicable && r.Status != TweakStatus.Unknown).ToList();
            return (relevant.Count(r => r.Status == TweakStatus.Applied), relevant.Count);
        }

        public static Level? Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            switch (s.Trim().ToLowerInvariant())
            {
                case "quick": case "rapido": case "rápido": case "1": return Level.Quick;
                case "balanced": case "equilibrado": case "2": return Level.Balanced;
                case "gaming": case "juegos": case "3": return Level.Gaming;
                case "max": case "maximum": case "maximo": case "máximo": case "4": return Level.Maximum;
                default: return null;
            }
        }
    }
}
