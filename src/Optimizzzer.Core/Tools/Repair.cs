using System.Collections.Generic;

namespace Optimizzzer.Core.Tools
{
    public sealed class RepairStep
    {
        public RepairStep(string file, string args, bool tolerateExitCode = false) { File = file; Args = args; TolerateExitCode = tolerateExitCode; }
        public string File { get; }
        public string Args { get; }
        /// <summary>
        /// Some tools report a non-zero code even when the job is done (netsh int ip reset denies one protected key
        /// on most PCs; net stop returns 2 for a service that is already stopped). Those codes are shown, not treated as failure.
        /// </summary>
        public bool TolerateExitCode { get; }
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
        /// <summary>Steps that put the system back in working order. They run even after a failure or Cancel.</summary>
        public IList<RepairStep> CleanupSteps { get; set; } = new List<RepairStep>();
        public Text WhenToUse => RepairDetails.When(Id);
        public Text WhatItDoes => RepairDetails.What(Id);
    }

    public static class RepairDetails
    {
        private static readonly Dictionary<string, (Text When, Text What)> Map = new Dictionary<string, (Text, Text)>
        {
            ["dism"] = (new Text("Cuando SFC dice que encontró archivos dañados y no pudo repararlos, o Windows Update falla una y otra vez.", "When SFC says it found damaged files it could not repair, or Windows Update keeps failing."),
                new Text("Ejecuta DISM /Online /Cleanup-Image /RestoreHealth: compara la copia de reserva de Windows con la de Microsoft y descarga lo que esté dañado. Necesita internet. No borra programas ni archivos tuyos.", "Runs DISM /Online /Cleanup-Image /RestoreHealth: compares Windows' backup copy with Microsoft's and downloads what is damaged. Needs internet. Does not remove programs or your files.")),
            ["sfc"] = (new Text("Si Windows muestra errores raros, pantallas azules o programas del sistema que no abren.", "When Windows shows odd errors, blue screens, or system programs that will not open."),
                new Text("Ejecuta sfc /scannow: revisa cada archivo protegido de Windows y sustituye los modificados por la copia correcta. Si dice que no pudo reparar alguno, ejecuta DISM y luego SFC otra vez.", "Runs sfc /scannow: checks every protected Windows file and replaces modified ones with the correct copy. If it says it could not repair some, run DISM and then SFC again.")),
            ["network"] = (new Text("Si el icono dice que hay conexión pero las webs no cargan, o después de quitar una VPN o un antivirus.", "When the icon says you are connected but pages do not load, or after removing a VPN or an antivirus."),
                new Text("Vacía la caché DNS (ipconfig /flushdns) y restablece Winsock y TCP/IP (netsh). Se pierden las IP fijas que hubieras puesto a mano. Pide reiniciar.", "Flushes the DNS cache (ipconfig /flushdns) and resets Winsock and TCP/IP (netsh). Static IPs you set by hand are lost. Needs a restart.")),
            ["windowsUpdate"] = (new Text("Si una actualización falla siempre con el mismo error o se queda descargando al 0 %.", "When an update always fails with the same error or stays downloading at 0%."),
                new Text("Detiene Windows Update, BITS y Servicios criptográficos, renombra las carpetas SoftwareDistribution y catroot2 (no se borran: quedan como .old) y vuelve a iniciar los servicios. Windows descarga todo de nuevo.", "Stops Windows Update, BITS and Cryptographic Services, renames the SoftwareDistribution and catroot2 folders (not deleted: they stay as .old) and starts the services again. Windows downloads everything again.")),
            ["store"] = (new Text("Si la Microsoft Store no abre, o las descargas se quedan en pendiente.", "When the Microsoft Store will not open, or downloads stay pending."),
                new Text("Ejecuta wsreset: borra la caché de la Store sin tocar tus apps ni tu cuenta. Se abre la Store al terminar.", "Runs wsreset: clears the Store cache without touching your apps or account. The Store opens when done.")),
            ["iconCache"] = (new Text("Si ves iconos en blanco, genéricos o de otro programa.", "When you see blank, generic or wrong icons."),
                new Text("Cierra el Explorador, borra los archivos de la caché de iconos y lo vuelve a abrir. Las ventanas de carpetas abiertas se cierran.", "Closes File Explorer, deletes the icon cache files and opens it again. Open folder windows close.")),
            ["chkdsk"] = (new Text("Si el equipo se apagó de golpe, un archivo no se puede abrir o el disco hace ruidos.", "When the PC lost power, a file will not open, or the drive makes noises."),
                new Text("Ejecuta chkdsk C: /scan: revisa el sistema de archivos sin desmontar el disco y sin cambiar nada. Si encuentra errores, dice cómo repararlos (normalmente chkdsk /spotfix al reiniciar).", "Runs chkdsk C: /scan: checks the file system online without changing anything. If it finds errors, it tells you how to fix them (usually chkdsk /spotfix at restart).")),
            ["firewall"] = (new Text("Si un programa o juego no conecta y sospechas de una regla del Firewall que añadió otro programa.", "When a program or game cannot connect and you suspect a Firewall rule added by another program."),
                new Text("Ejecuta netsh advfirewall reset: vuelve el Firewall de Windows a su configuración de fábrica. Se borran las reglas que añadieron los programas; algunos te pedirán permiso otra vez la próxima vez que se conecten.", "Runs netsh advfirewall reset: returns Windows Firewall to its factory settings. Rules programs added are deleted; some will ask for permission again the next time they connect.")),
        };

        static RepairDetails()
        {
            Map["searchIndex"] = (new Text("Si la búsqueda de Windows no encuentra archivos que sí existen, o el indexador usa mucho disco sin parar.", "When Windows search cannot find files that exist, or the indexer keeps using the disk."),
                new Text("Detiene el servicio de búsqueda, borra su índice (Windows.edb) y lo vuelve a iniciar. Windows reconstruye el índice en las horas siguientes.", "Stops the search service, deletes its index (Windows.edb) and starts it again. Windows rebuilds the index over the next hours."));
            Map["timeSync"] = (new Text("Si el reloj va mal, las webs dan error de certificado o un juego con anticheat no conecta.", "When the clock is wrong, sites show certificate errors or a game with anti-cheat will not connect."),
                new Text("Inicia el servicio de hora de Windows y sincroniza con time.windows.com (w32tm /resync).", "Starts the Windows Time service and syncs with time.windows.com (w32tm /resync)."));
            Map["printQueue"] = (new Text("Si un documento se queda atascado en la cola y no se imprime nada más.", "When a document is stuck in the queue and nothing else prints."),
                new Text("Detiene la cola de impresión, borra los trabajos pendientes de spool\\PRINTERS y la vuelve a iniciar. Las impresoras no se tocan.", "Stops the print spooler, deletes pending jobs in spool\\PRINTERS and starts it again. Printers are not touched."));
            Map["proxyReset"] = (new Text("Si nada carga después de quitar una VPN, un antivirus o un programa sospechoso.", "When nothing loads after removing a VPN, an antivirus or a suspicious program."),
                new Text("Quita el proxy del sistema (netsh winhttp reset proxy) y el del usuario. Si usas un proxy a propósito, tendrás que volver a ponerlo.", "Removes the system proxy (netsh winhttp reset proxy) and the user's. If you use a proxy on purpose, you will need to set it again."));
            Map["audio"] = (new Text("Si no hay sonido o desaparecieron los altavoces o auriculares de la lista.", "When there is no sound or speakers or headphones vanished from the list."),
                new Text("Reinicia los servicios de audio de Windows (Audiosrv y AudioEndpointBuilder). El sonido se corta un par de segundos.", "Restarts the Windows audio services (Audiosrv and AudioEndpointBuilder). Sound stops for a couple of seconds."));
            Map["powerPlans"] = (new Text("Si otro optimizador dejó planes de energía raros o falta el plan Equilibrado.", "When another optimizer left strange power plans or Balanced is missing."),
                new Text("Restaura los planes de energía de fábrica (powercfg -restoredefaultschemes). Se borran los planes personalizados, incluido Máximo rendimiento.", "Restores factory power plans (powercfg -restoredefaultschemes). Custom plans are deleted, Ultimate Performance included."));
            Map["perfCounters"] = (new Text("Si el Administrador de tareas o el Monitor de rendimiento no muestran CPU, disco o red.", "When Task Manager or Performance Monitor do not show CPU, disk or network."),
                new Text("Reconstruye los contadores de rendimiento desde la copia de Windows (lodctr /R) y los vuelve a sincronizar con WMI.", "Rebuilds the performance counters from Windows' copy (lodctr /R) and resyncs them with WMI."));
            Map["wmi"] = (new Text("Si programas de hardware o Windows dan errores al leer información del equipo.", "When hardware programs or Windows fail to read PC information."),
                new Text("Comprueba el repositorio de WMI y, solo si está dañado, lo repara con la herramienta oficial (winmgmt /salvagerepository).", "Checks the WMI repository and, only if it is damaged, repairs it with the official tool (winmgmt /salvagerepository)."));
            Map["defender"] = (new Text("Para asegurarte de que el antivirus está al día y no hay nada raro.", "To make sure the antivirus is up to date and nothing is wrong."),
                new Text("Actualiza las firmas de Microsoft Defender y hace un análisis rápido. Puede tardar unos minutos.", "Updates Microsoft Defender signatures and runs a quick scan. It can take a few minutes."));
        }

        public static Text When(string id) => id != null && Map.TryGetValue(id, out var t) ? t.When : null;
        public static Text What(string id) => id != null && Map.TryGetValue(id, out var t) ? t.What : null;
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
                    new RepairStep("netsh.exe", "int ip reset", tolerateExitCode: true),
                },
            },
            new RepairTask
            {
                Id = "windowsUpdate", Title = new Text("Restablecer Windows Update", "Reset Windows Update"),
                Description = new Text("Detiene los servicios de actualización, renombra sus carpetas de caché y los vuelve a iniciar. Útil cuando una actualización falla una y otra vez.", "Stops the update services, renames their cache folders and starts them again. Useful when an update keeps failing."),
                Duration = new Text("1 a 2 minutos", "1 to 2 minutes"),
                Steps =
                {
                    new RepairStep("net.exe", "stop wuauserv", tolerateExitCode: true),
                    new RepairStep("net.exe", "stop bits", tolerateExitCode: true),
                    new RepairStep("net.exe", "stop cryptsvc", tolerateExitCode: true),
                    new RepairStep("cmd.exe", "/c if exist %SystemRoot%\\SoftwareDistribution.old rd /s /q %SystemRoot%\\SoftwareDistribution.old", tolerateExitCode: true),
                    new RepairStep("cmd.exe", "/c ren %SystemRoot%\\SoftwareDistribution SoftwareDistribution.old"),
                    new RepairStep("cmd.exe", "/c if exist %SystemRoot%\\System32\\catroot2.old rd /s /q %SystemRoot%\\System32\\catroot2.old", tolerateExitCode: true),
                    new RepairStep("cmd.exe", "/c ren %SystemRoot%\\System32\\catroot2 catroot2.old"),
                },
                // Already running counts as success; any other failure to start is reported, so "Finished" is never shown with the service stopped.
                CleanupSteps =
                {
                    new RepairStep("cmd.exe", "/c sc query cryptsvc | find \"RUNNING\" >nul || net start cryptsvc"),
                    new RepairStep("cmd.exe", "/c sc query bits | find \"RUNNING\" >nul || net start bits"),
                    new RepairStep("cmd.exe", "/c sc query wuauserv | find \"RUNNING\" >nul || net start wuauserv"),
                },
            },
            new RepairTask
            {
                Id = "chkdsk", Title = new Text("Comprobar el disco (sin cambiar nada)", "Check the drive (read only)"),
                Description = new Text("Revisa el sistema de archivos del disco del sistema mientras lo usas. Solo informa; no repara ni reinicia.", "Checks the system drive's file system while you use it. It only reports; it does not repair or restart."),
                Duration = new Text("1 a 10 minutos", "1 to 10 minutes"),
                Steps = { new RepairStep("chkdsk.exe", "%SystemDrive% /scan") },
            },
            new RepairTask
            {
                Id = "firewall", Title = new Text("Restablecer el Firewall de Windows", "Reset Windows Firewall"),
                Description = new Text("Vuelve el Firewall a su configuración de fábrica y borra las reglas que añadieron los programas.", "Returns the Firewall to factory settings and deletes rules programs added."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Steps = { new RepairStep("netsh.exe", "advfirewall reset") },
            },
            new RepairTask
            {
                Id = "searchIndex", Title = new Text("Reconstruir el índice de búsqueda", "Rebuild the search index"),
                Description = new Text("Borra el índice de la búsqueda de Windows para que se cree de nuevo, limpio.", "Deletes the Windows search index so it is built again, clean."),
                Duration = new Text("1 minuto (el índice se completa en horas)", "1 minute (the index completes over hours)"),
                Steps =
                {
                    new RepairStep("net.exe", "stop wsearch", tolerateExitCode: true),
                    new RepairStep("cmd.exe", "/c del /f /q \"%ProgramData%\\Microsoft\\Search\\Data\\Applications\\Windows\\Windows.edb\"", tolerateExitCode: true),
                },
                CleanupSteps = { new RepairStep("cmd.exe", "/c sc query wsearch | find \"RUNNING\" >nul || net start wsearch") },
            },
            new RepairTask
            {
                Id = "timeSync", Title = new Text("Sincronizar la hora", "Sync the clock"),
                Description = new Text("Pone el reloj en hora con el servidor de Microsoft.", "Sets the clock from Microsoft's time server."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Steps =
                {
                    new RepairStep("cmd.exe", "/c sc query w32time | find \"RUNNING\" >nul || net start w32time"),
                    new RepairStep("w32tm.exe", "/resync /force"),
                },
            },
            new RepairTask
            {
                Id = "printQueue", Title = new Text("Desatascar la cola de impresión", "Clear a stuck print queue"),
                Description = new Text("Borra los trabajos de impresión atascados.", "Deletes stuck print jobs."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Steps =
                {
                    new RepairStep("net.exe", "stop spooler", tolerateExitCode: true),
                    new RepairStep("cmd.exe", "/c del /f /q \"%SystemRoot%\\System32\\spool\\PRINTERS\\*.*\"", tolerateExitCode: true),
                },
                CleanupSteps = { new RepairStep("cmd.exe", "/c sc query spooler | find \"RUNNING\" >nul || net start spooler") },
            },
            new RepairTask
            {
                Id = "proxyReset", Title = new Text("Quitar el proxy", "Remove the proxy"),
                Description = new Text("Vuelve la conexión a directa, sin proxy.", "Sets the connection back to direct, without a proxy."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Steps =
                {
                    new RepairStep("netsh.exe", "winhttp reset proxy"),
                    new RepairStep("reg.exe", "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /t REG_DWORD /d 0 /f"),
                },
            },
            new RepairTask
            {
                Id = "audio", Title = new Text("Reparar el sonido", "Repair sound"),
                Description = new Text("Reinicia los servicios de audio de Windows.", "Restarts the Windows audio services."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Steps = { new RepairStep("net.exe", "stop audiosrv", tolerateExitCode: true), new RepairStep("net.exe", "stop AudioEndpointBuilder", tolerateExitCode: true) },
                CleanupSteps =
                {
                    new RepairStep("cmd.exe", "/c sc query AudioEndpointBuilder | find \"RUNNING\" >nul || net start AudioEndpointBuilder"),
                    new RepairStep("cmd.exe", "/c sc query audiosrv | find \"RUNNING\" >nul || net start audiosrv"),
                },
            },
            new RepairTask
            {
                Id = "powerPlans", Title = new Text("Restaurar los planes de energía de fábrica", "Restore factory power plans"),
                Description = new Text("Deja solo los planes originales de Windows.", "Keeps only Windows' original plans."),
                Duration = new Text("Menos de 1 minuto", "Under 1 minute"),
                Steps = { new RepairStep("powercfg.exe", "-restoredefaultschemes") },
            },
            new RepairTask
            {
                Id = "perfCounters", Title = new Text("Reconstruir los contadores de rendimiento", "Rebuild performance counters"),
                Description = new Text("Arregla el Administrador de tareas cuando no muestra uso de CPU, disco o red.", "Fixes Task Manager when it shows no CPU, disk or network use."),
                Duration = new Text("1 a 2 minutos", "1 to 2 minutes"),
                Steps = { new RepairStep("lodctr.exe", "/R"), new RepairStep("winmgmt.exe", "/resyncperf", tolerateExitCode: true) },
            },
            new RepairTask
            {
                Id = "wmi", Title = new Text("Comprobar y reparar WMI", "Check and repair WMI"),
                Description = new Text("Revisa la base de datos de información del sistema y la repara solo si está dañada.", "Checks the system information database and repairs it only if damaged."),
                Duration = new Text("1 a 3 minutos", "1 to 3 minutes"),
                Steps = { new RepairStep("cmd.exe", "/c winmgmt /verifyrepository | find /i \"inconsistent\" >nul && winmgmt /salvagerepository || exit /b 0") },
            },
            new RepairTask
            {
                Id = "defender", Title = new Text("Actualizar Defender y análisis rápido", "Update Defender and quick scan"),
                Description = new Text("Descarga las últimas firmas del antivirus de Windows y analiza las zonas más comunes.", "Downloads the latest Windows antivirus signatures and scans the most common places."),
                Duration = new Text("2 a 10 minutos", "2 to 10 minutes"),
                Steps =
                {
                    new RepairStep("cmd.exe", "/c \"%ProgramFiles%\\Windows Defender\\MpCmdRun.exe\" -SignatureUpdate", tolerateExitCode: true),
                    new RepairStep("cmd.exe", "/c \"%ProgramFiles%\\Windows Defender\\MpCmdRun.exe\" -Scan -ScanType 1"),
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
                    new RepairStep("taskkill.exe", "/f /im explorer.exe", tolerateExitCode: true),
                    new RepairStep("cmd.exe", "/c del /a /q \"%LOCALAPPDATA%\\IconCache.db\" & del /a /q \"%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\iconcache_*.db\"", tolerateExitCode: true),
                },
                RestartsExplorer = true,
            },
        };
    }
}
