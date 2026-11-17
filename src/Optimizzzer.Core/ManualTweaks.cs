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

        public static Tweak FromId(string id)
        {
            if (id == null) return null;
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
