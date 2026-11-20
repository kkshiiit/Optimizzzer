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
        /// <summary>One plain sentence: what this level is for.</summary>
        public Text What { get; set; }
        /// <summary>One concrete thing you will notice, different for each level.</summary>
        public Text Example { get; set; }
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
                What = new Text("Quita anuncios, sugerencias y el envío de datos de uso. No toca nada que uses.", "Removes ads, suggestions and usage data reporting. Touches nothing you use."),
                Example = new Text("Ejemplo: el menú Inicio deja de recomendarte apps para instalar.", "Example: Start stops recommending apps to install."),
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
                What = new Text("Rápido, más un escritorio que responde al instante: sin animaciones ni efectos de transparencia y sin apps preinstaladas que nadie usa.", "Quick, plus a desktop that responds instantly: no animations or transparency effects, and no preinstalled apps nobody uses."),
                Example = new Text("Ejemplo: al arrastrar una ventana ya no se dibuja su contenido, y los menús aparecen sin fundido.", "Example: dragging a window no longer redraws its contents, and menus appear without fading."),
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
                Tagline = new Text("Equilibrado, más todo lo que influye mientras juegas.", "Balanced, plus everything that matters while you play."),
                What = new Text("Equilibrado, más ajustes para jugar: el ratón responde igual siempre, nada graba en segundo plano y el juego usa la gráfica potente.", "Balanced, plus gaming settings: the mouse always responds the same, nothing records in the background and games use the powerful graphics card."),
                Example = new Text("Ejemplo: mover el ratón 5 cm gira la cámara lo mismo, lo muevas rápido o lento.", "Example: moving the mouse 5 cm turns the camera the same whether you move it fast or slow."),
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
                Tagline = new Text("Juegos, más lo que más rinde aunque quite funciones.", "Gaming, plus what performs best even if it removes features."),
                What = new Text("Incluye todo Juegos y suma lo que exprime el equipo a costa de funciones: el procesador nunca ahorra energía y se apagan el indexador y la hibernación.", "Includes all of Gaming and adds what squeezes the PC at the cost of features: the processor never saves power, and the indexer and hibernation are turned off."),
                Example = new Text("Ejemplo: la búsqueda de archivos de Windows tarda más, porque deja de mantener un índice en segundo plano.", "Example: Windows file search takes longer, because it no longer keeps an index in the background."),
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

        /// <summary>For each service a level changes: the lowest level that changes it, and the tweak that does.</summary>
        public static IDictionary<string, (Level Level, string TweakId)> ServiceOwners()
        {
            var map = new Dictionary<string, (Level, string)>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in All)
                foreach (var id in l.Adds)
                {
                    var t = Catalog.TweakCatalog.Find(id);
                    if (t == null) continue;
                    foreach (var a in t.Actions.Select(x => x is OsOnlyAction o ? o.Inner : x).OfType<ServiceStartAction>())
                        if (!map.ContainsKey(a.Service)) map[a.Service] = (l.Level, t.Id);
                }
            return map;
        }

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
