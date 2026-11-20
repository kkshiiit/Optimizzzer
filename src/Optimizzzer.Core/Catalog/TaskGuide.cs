using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Optimizzzer.Core.Catalog
{
    public sealed class TaskNote
    {
        public Text What { get; set; }
        /// <summary>What happens if it is turned off.</summary>
        public Text IfOff { get; set; }
        /// <summary>True when the note comes from the task's name only (a rule), not from a known task.</summary>
        public bool Estimated { get; set; }
    }

    /// <summary>Known scheduled tasks and what turning them off would do, plus name-based rules for the rest.</summary>
    public static class TaskGuide
    {
        private static readonly (Regex Match, TaskNote Note)[] Known =
        {
            (R(@"\\Google\\|GoogleUpdate|GoogleUpdater"), N("Actualizador de Google (Chrome y otros).", "Google updater (Chrome and others).",
                "Chrome deja de actualizarse solo; se actualiza al abrir Chrome > Ayuda > Información.", "Chrome stops updating by itself; it updates from Chrome > Help > About.")),
            (R(@"MicrosoftEdgeUpdate"), N("Actualizador de Microsoft Edge.", "Microsoft Edge updater.",
                "Edge deja de actualizarse solo; Edge desactualizado es un riesgo de seguridad.", "Edge stops updating by itself; an outdated Edge is a security risk.")),
            (R(@"Adobe.*(Update|Acrobat)|AdobeGCInvoker|Adobe Acrobat Update"), N("Actualizador o comprobación de licencia de Adobe.", "Adobe updater or licence check.",
                "Adobe deja de buscar actualizaciones solo; ábrelo de vez en cuando para actualizar.", "Adobe stops checking for updates; open it now and then to update.")),
            (R(@"\\Mozilla\\|Firefox Default Browser|Firefox Background Update"), N("Tareas de Firefox (actualización y navegador predeterminado).", "Firefox tasks (updates and default browser).",
                "Firefox se actualiza solo al abrirlo.", "Firefox updates itself when opened.")),
            (R(@"NvTm|NvDriverUpdateCheck|NVIDIA.*Telemetry|NvProfileUpdater"), N("Telemetría o comprobaciones de NVIDIA.", "NVIDIA telemetry or checks.",
                "Dejan de enviarse datos a NVIDIA; los controladores siguen funcionando igual.", "Data stops being sent to NVIDIA; drivers keep working the same.")),
            (R(@"AMD.*(Update|Installer|Link)|StartCN|StartDVR"), N("Tareas del software de AMD.", "AMD software tasks.",
                "El software de AMD puede no abrirse solo al iniciar o no avisar de actualizaciones.", "AMD software may not start by itself or warn about updates.")),
            (R(@"\\Intel\\|Intel.*Telemetry"), N("Tareas de Intel (actualizaciones o telemetría).", "Intel tasks (updates or telemetry).",
                "Intel deja de avisar de actualizaciones o de enviar datos.", "Intel stops notifying updates or sending data.")),
            (R(@"OneDrive"), N("Actualizaciones y mantenimiento de OneDrive.", "OneDrive updates and maintenance.",
                "OneDrive puede quedar sin actualizar; la sincronización sigue mientras la app esté abierta.", "OneDrive may stay outdated; syncing continues while the app is open.")),
            (R(@"\\Office\\|OfficeTelemetry|Office Automatic Updates"), N("Tareas de Microsoft Office.", "Microsoft Office tasks.",
                "Si es de actualizaciones, Office deja de actualizarse solo.", "If it is the updater, Office stops updating by itself.")),
            (R(@"Application Experience\\|Customer Experience Improvement|Feedback\\Siuf|DiskDiagnosticDataCollector|Autochk\\Proxy"), N("Telemetría de Windows.", "Windows telemetry.",
                "Microsoft recibe menos datos de uso. No afecta al funcionamiento. El nivel Rápido las desactiva.", "Microsoft receives less usage data. It does not affect how things work. The Quick level disables them.")),
            (R(@"\\Defrag\\ScheduledDefrag"), N("Optimización semanal de discos (TRIM en SSD, desfragmentación en HDD).", "Weekly drive optimisation (TRIM on SSD, defrag on HDD).",
                "No la desactives: el TRIM mantiene rápido el SSD.", "Do not turn it off: TRIM keeps the SSD fast.")),
            (R(@"\\WindowsUpdate\\|\\UpdateOrchestrator\\"), N("Parte de Windows Update.", "Part of Windows Update.",
                "No la desactives: para pausar actualizaciones usa la pausa de Windows Update.", "Do not turn it off: to pause updates use Windows Update's pause.")),
            (R(@"\\Windows Defender\\"), N("Análisis y mantenimiento de Microsoft Defender.", "Microsoft Defender scans and maintenance.",
                "No la desactives: Defender deja de analizar a tiempo.", "Do not turn it off: Defender stops scanning on time.")),
            (R(@"\\SystemRestore\\SR"), N("Crea puntos de restauración automáticos.", "Creates automatic restore points.",
                "No la desactives: es tu red de seguridad.", "Do not turn it off: it is your safety net.")),
        };

        private static Regex R(string p) => new Regex(p, RegexOptions.IgnoreCase);
        private static TaskNote N(string es, string en, string offEs, string offEn) => new TaskNote { What = new Text(es, en), IfOff = new Text(offEs, offEn) };

        public static TaskNote For(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var (m, n) in Known) if (m.IsMatch(path)) return n;
            // Name-based estimates for everything else.
            var name = path.Split('\\').Last();
            if (Regex.IsMatch(name, "update|updater|upgrade", RegexOptions.IgnoreCase))
                return Est("Parece un actualizador.", "Looks like an updater.", "Ese programa dejaría de buscar actualizaciones solo.", "That program would stop checking for updates by itself.");
            if (Regex.IsMatch(name, "telemetry|ceip|usage|report|census", RegexOptions.IgnoreCase))
                return Est("Parece telemetría (envío de datos de uso).", "Looks like telemetry (usage data).", "Se enviarían menos datos; normalmente no afecta al programa.", "Less data would be sent; usually it does not affect the program.");
            if (Regex.IsMatch(name, "crash|error|dump", RegexOptions.IgnoreCase))
                return Est("Parece un informe de fallos.", "Looks like crash reporting.", "No se enviarían informes de errores.", "Crash reports would not be sent.");
            if (Regex.IsMatch(name, "maintenance|cleanup|backup", RegexOptions.IgnoreCase))
                return Est("Parece mantenimiento o copia de seguridad.", "Looks like maintenance or backup.", "Revisa antes: podría dejar de hacer copias o limpieza.", "Check first: it could stop backups or cleanup.");
            return null;
        }

        private static TaskNote Est(string es, string en, string offEs, string offEn) =>
            new TaskNote { What = new Text(es, en), IfOff = new Text(offEs, offEn), Estimated = true };
    }
}
