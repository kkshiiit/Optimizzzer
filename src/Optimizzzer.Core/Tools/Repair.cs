using System.Collections.Generic;

namespace Optimizzzer.Core.Tools
{
    public sealed class RepairStep
    {
        public RepairStep(string file, string args) { File = file; Args = args; }
        public string File { get; }
        public string Args { get; }
        public override string ToString() => File + " " + Args;
    }

    public sealed class RepairTask
    {
        public string Id { get; set; }
        public Text Title { get; set; }
        public Text Description { get; set; }
        public Text Duration { get; set; }
        public bool NeedsReboot { get; set; }
        /// <summary>The runner starts File Explorer again (unelevated) after the steps.</summary>
        public bool RestartsExplorer { get; set; }
        public IList<RepairStep> Steps { get; set; } = new List<RepairStep>();
    }

    public static class RepairCatalog
    {
        public static readonly IReadOnlyList<RepairTask> All = new[]
        {
            new RepairTask
            {
                Id = "dism", Title = new Text("Reparar la imagen de Windows (DISM)", "Repair the Windows image (DISM)"),
                Description = new Text("Comprueba los archivos de origen de Windows y descarga de Windows Update los que estén dañados. Conviene hacerlo antes que SFC.", "Checks Windows source files and downloads damaged ones from Windows Update. Best run before SFC."),
                Duration = new Text("10 a 30 minutos", "10 to 30 minutes"),
                Steps = { new RepairStep("DISM.exe", "/Online /Cleanup-Image /RestoreHealth") },
            },
            new RepairTask
            {
                Id = "sfc", Title = new Text("Comprobar archivos del sistema (SFC)", "Check system files (SFC)"),
                Description = new Text("Busca archivos de Windows modificados o dañados y los sustituye por la copia correcta.", "Looks for modified or damaged Windows files and replaces them with the correct copy."),
                Duration = new Text("5 a 15 minutos", "5 to 15 minutes"),
                Steps = { new RepairStep("sfc.exe", "/scannow") },
            },
            new RepairTask
            {
                Id = "network", Title = new Text("Restablecer la red", "Reset networking"),
                Description = new Text("Vacía la caché DNS y restablece Winsock y TCP/IP. Útil si hay internet pero las webs no cargan.", "Flushes the DNS cache and resets Winsock and TCP/IP. Useful when you are connected but pages do not load."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"), NeedsReboot = true,
                Steps =
                {
                    new RepairStep("ipconfig.exe", "/flushdns"),
                    new RepairStep("netsh.exe", "winsock reset"),
                    new RepairStep("netsh.exe", "int ip reset"),
                },
            },
            new RepairTask
            {
                Id = "windowsUpdate", Title = new Text("Restablecer Windows Update", "Reset Windows Update"),
                Description = new Text("Detiene los servicios de actualización, renombra sus carpetas de caché y los vuelve a iniciar. Útil cuando una actualización falla una y otra vez.", "Stops the update services, renames their cache folders and starts them again. Useful when an update keeps failing."),
                Duration = new Text("1 a 2 minutos", "1 to 2 minutes"),
                Steps =
                {
                    new RepairStep("net.exe", "stop wuauserv"),
                    new RepairStep("net.exe", "stop bits"),
                    new RepairStep("net.exe", "stop cryptsvc"),
                    new RepairStep("cmd.exe", "/c if exist %SystemRoot%\\SoftwareDistribution.old rd /s /q %SystemRoot%\\SoftwareDistribution.old"),
                    new RepairStep("cmd.exe", "/c ren %SystemRoot%\\SoftwareDistribution SoftwareDistribution.old"),
                    new RepairStep("cmd.exe", "/c if exist %SystemRoot%\\System32\\catroot2.old rd /s /q %SystemRoot%\\System32\\catroot2.old"),
                    new RepairStep("cmd.exe", "/c ren %SystemRoot%\\System32\\catroot2 catroot2.old"),
                    new RepairStep("net.exe", "start cryptsvc"),
                    new RepairStep("net.exe", "start bits"),
                    new RepairStep("net.exe", "start wuauserv"),
                },
            },
            new RepairTask
            {
                Id = "store", Title = new Text("Reparar Microsoft Store", "Repair Microsoft Store"),
                Description = new Text("Borra la caché de la Store. Útil si las descargas se quedan atascadas.", "Clears the Store cache. Useful when downloads get stuck."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Steps = { new RepairStep("wsreset.exe", "-i") },
            },
            new RepairTask
            {
                Id = "iconCache", Title = new Text("Reconstruir la caché de iconos", "Rebuild the icon cache"),
                Description = new Text("Arregla iconos en blanco o incorrectos. El Explorador se reinicia durante unos segundos.", "Fixes blank or wrong icons. File Explorer restarts for a few seconds."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Steps =
                {
                    new RepairStep("ie4uinit.exe", "-show"),
                    new RepairStep("taskkill.exe", "/f /im explorer.exe"),
                    new RepairStep("cmd.exe", "/c del /a /q \"%LOCALAPPDATA%\\IconCache.db\" & del /a /q \"%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\iconcache_*.db\""),
                },
                RestartsExplorer = true,
            },
        };
    }
}
