using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimizzzer.Core.Catalog
{
    public static class TweakCatalog
    {
        private static readonly Lazy<IReadOnlyList<Tweak>> _all = new Lazy<IReadOnlyList<Tweak>>(Build);

        public static IReadOnlyList<Tweak> All => _all.Value;

        public static Tweak Find(string id) => All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Qualitative impact: how noticeable the change usually is on a typical PC. High means a difference most
        /// people notice (background CPU/disk/GPU work removed, visible delays gone); Low means tidiness or privacy.
        /// </summary>
        private static readonly Dictionary<string, Impact> ImpactOf = new Dictionary<string, Impact>(StringComparer.OrdinalIgnoreCase)
        {
            ["privacy.telemetryTasks"] = Impact.Medium,
            ["privacy.diagtrack"] = Impact.Medium,
            ["privacy.webSearch"] = Impact.Medium,
            ["ui.widgets"] = Impact.Medium,
            ["ai.copilot"] = Impact.Medium,
            ["ai.recall"] = Impact.High,
            ["perf.animations"] = Impact.High,
            ["perf.menuDelay"] = Impact.Medium,
            ["perf.startupDelay"] = Impact.Medium,
            ["perf.transparency"] = Impact.Medium,
            ["perf.backgroundApps"] = Impact.Medium,
            ["perf.edgeBackground"] = Impact.Medium,
            ["perf.searchIndex"] = Impact.High,
            ["gaming.gameDvr"] = Impact.High,
            ["gaming.gameMode"] = Impact.Medium,
            ["gaming.mouseAccel"] = Impact.High,
            ["gaming.windowedOptimizations"] = Impact.Medium,
            ["gaming.hags"] = Impact.Medium,
            ["power.ultimate"] = Impact.Medium,
            ["power.hibernate"] = Impact.Medium,
            ["power.throttling"] = Impact.Medium,
            ["power.fastStartup"] = Impact.Medium,
            ["update.noAutoRestart"] = Impact.Medium,
            ["update.deliveryOptimization"] = Impact.Medium,
            ["ui.classicContextMenu"] = Impact.Medium,
            ["app.onedrive"] = Impact.Medium,
        };

        private static IReadOnlyList<Tweak> Build()
        {
            var l = new List<Tweak>();
            PrivacyTweaks.Add(l);
            InterfaceTweaks.Add(l);
            PerformanceTweaks.Add(l);
            SystemTweaks.Add(l);
            foreach (var t in l)
                if (ImpactOf.TryGetValue(t.Id, out var impact)) t.Impact = impact;
                else if (t.Category == Category.Apps) t.Impact = Impact.Low;
            return l.OrderBy(t => t.Category).ThenBy(t => t.Risk).ToList();
        }
    }
}
