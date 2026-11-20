using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimizzzer.Core
{
    /// <summary>
    /// One-off changes made from the Services and Scheduled tasks screens. They go through the engine
    /// like catalog tweaks so they appear in History and can be undone. The id carries everything needed
    /// to rebuild the tweak later: "manual.service|Name|Mode" or "manual.task|Path|True".
    /// </summary>
    public static class ManualTweaks
    {
        public const string ServicePrefix = "manual.service|";
        public const string TaskPrefix = "manual.task|";
        public const string GpuPrefix = "manual.gpu|";

        /// <summary>
        /// Runs one game on the high-performance graphics card (Settings > Display > Graphics). Only useful on PCs
        /// with two GPUs (for example integrated plus dedicated). Undo restores the previous choice.
        /// </summary>
        public static Tweak GpuPreference(string exePath, string gameName)
        {
            var t = new Tweak
            {
                Id = GpuPrefix + exePath,
                Category = Category.Gaming,
                Risk = Risk.Safe,
                Title = new Text((gameName ?? exePath) + ": gráfica de alto rendimiento", (gameName ?? exePath) + ": high-performance graphics"),
                Summary = new Text("El juego usa la gráfica potente en lugar de la integrada.", "The game uses the powerful graphics card instead of the integrated one."),
                Why = new Text("En equipos con dos gráficas, Windows a veces elige la integrada y el juego rinde mucho menos.", "On PCs with two graphics cards, Windows sometimes picks the integrated one and the game runs much slower."),
                Source = "Microsoft: Configuración > Pantalla > Gráficos (UserGpuPreferences)",
            };
            t.Actions.Add(new RegistryValueAction(Hive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", exePath, RegValue.Str("GpuPreference=2;")));
            return t;
        }

        public static Tweak Service(string name, string displayName, StartMode mode)
        {
            var t = new Tweak
            {
                Id = ServicePrefix + name + "|" + mode,
                Category = Category.Services,
                Risk = Risk.Advanced,
                Title = new Text("Servicio " + (displayName ?? name) + ": " + ServiceStartAction.ModeName(mode, Lang.Es),
                                 "Service " + (displayName ?? name) + ": " + ServiceStartAction.ModeName(mode, Lang.En)),
                Summary = new Text("Cambio manual desde Servicios.", "Manual change from Services."),
                Why = new Text("Elegido por ti.", "Chosen by you."),
                Source = "services",
            };
            t.Actions.Add(new ServiceStartAction(name, mode));
            return t;
        }

        public static Tweak Task(string path, bool enabled)
        {
            var t = new Tweak
            {
                Id = TaskPrefix + path + "|" + enabled,
                Category = Category.Services,
                Risk = Risk.Advanced,
                Title = new Text((enabled ? "Activar tarea " : "Desactivar tarea ") + path, (enabled ? "Enable task " : "Disable task ") + path),
                Summary = new Text("Cambio manual desde Tareas programadas.", "Manual change from Scheduled tasks."),
                Why = new Text("Elegido por ti.", "Chosen by you."),
                Source = "tasks",
            };
            t.Actions.Add(new ScheduledTaskAction(path, enabled));
            return t;
        }

        /// <summary>"manual.service|Name" for "manual.service|Name|Mode": the thing being changed, without the new value.</summary>
        public static string TargetOf(string id)
        {
            if (id == null || !(id.StartsWith(ServicePrefix, StringComparison.Ordinal) || id.StartsWith(TaskPrefix, StringComparison.Ordinal))) return null;
            int i = id.LastIndexOf('|');
            return i > 0 ? id.Substring(0, i).ToLowerInvariant() : null;
        }

        public static Tweak FromId(string id)
        {
            if (id == null) return null;
            if (id.StartsWith(GpuPrefix, StringComparison.Ordinal)) return GpuPreference(id.Substring(GpuPrefix.Length), null);
            var parts = id.Split('|');
            if (id.StartsWith(ServicePrefix, StringComparison.Ordinal) && parts.Length == 3
                && Enum.TryParse<StartMode>(parts[2], out var mode))
                return Service(parts[1], null, mode);
            if (id.StartsWith(TaskPrefix, StringComparison.Ordinal) && parts.Length == 3 && bool.TryParse(parts[2], out var en))
                return Task(parts[1], en);
            return null;
        }

        /// <summary>The catalog plus any manual tweaks referenced by the journal, so undo can find them.</summary>
        public static IList<Tweak> WithJournal(IEnumerable<Tweak> catalog, IEnumerable<string> journalIds)
        {
            var list = catalog.ToList();
            foreach (var id in journalIds.Distinct())
            {
                if (list.Any(t => t.Id == id)) continue;
                var m = FromId(id);
                if (m != null) list.Add(m);
            }
            return list;
        }
    }
}
