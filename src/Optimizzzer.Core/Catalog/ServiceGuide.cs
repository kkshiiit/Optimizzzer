using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimizzzer.Core.Catalog
{
    public enum ServiceAdvice
    {
        /// <summary>Windows or common features need it: leave it alone.</summary>
        Keep,
        /// <summary>Starts only when something needs it; Manual is the safe, efficient setting.</summary>
        ManualIsFine,
        /// <summary>Safe to disable if you do not use the feature named in the note.</summary>
        DisableIfUnused,
    }

    public sealed class ServiceNote
    {
        public string Name { get; set; }
        public Text What { get; set; }
        public ServiceAdvice Advice { get; set; }
        /// <summary>The condition, e.g. "if you never print". Empty for Keep.</summary>
        public Text When { get; set; } = Text.Empty;
    }

    /// <summary>
    /// What common Windows services do and whether to touch them, in plain words. Sources: Microsoft's "System services
    /// on Windows" guidance (Windows Server/Windows 10 service configuration) and each service's own description.
    /// Services not listed here are shown as "no data: search before changing".
    /// </summary>
    public static class ServiceGuide
    {
        private static ServiceNote K(string n, string es, string en) => new ServiceNote { Name = n, What = new Text(es, en), Advice = ServiceAdvice.Keep };
        private static ServiceNote M(string n, string es, string en) => new ServiceNote { Name = n, What = new Text(es, en), Advice = ServiceAdvice.ManualIsFine };
        private static ServiceNote D(string n, string es, string en, string whenEs, string whenEn) =>
            new ServiceNote { Name = n, What = new Text(es, en), Advice = ServiceAdvice.DisableIfUnused, When = new Text(whenEs, whenEn) };

        public static readonly IReadOnlyList<ServiceNote> All = new[]
        {
            K("AudioSrv", "Reproduce el sonido de todo el equipo.", "Plays sound for the whole PC."),
            K("AudioEndpointBuilder", "Detecta altavoces, auriculares y micrófonos.", "Detects speakers, headphones and microphones."),
            K("BITS", "Descarga actualizaciones de Windows y de la Store en segundo plano.", "Downloads Windows and Store updates in the background."),
            K("CryptSvc", "Comprueba firmas de programas y actualizaciones.", "Checks the signatures of programs and updates."),
            K("Dhcp", "Obtiene la dirección de red de tu router.", "Gets your network address from your router."),
            K("Dnscache", "Resuelve nombres de webs; sin él no navegas.", "Resolves web names; without it you cannot browse."),
            K("EventLog", "Guarda el registro de eventos de Windows.", "Keeps the Windows event log."),
            K("mpssvc", "Firewall de Windows.", "Windows Firewall."),
            K("WinDefend", "Antivirus de Windows (Microsoft Defender).", "Windows antivirus (Microsoft Defender)."),
            K("wuauserv", "Windows Update. Para pausarlo usa la pausa de Windows Update, no este servicio.", "Windows Update. To pause it, use Windows Update's pause, not this service."),
            K("Winmgmt", "WMI: muchos programas y el propio Optimizzzer leen el equipo con él.", "WMI: many programs, Optimizzzer included, read the PC through it."),
            K("Themes", "Temas y aspecto visual de Windows.", "Windows themes and visual style."),
            K("ShellHWDetection", "Reproducción automática al conectar USB o discos.", "AutoPlay when you plug in USB drives or discs."),
            K("Spooler", "Imprimir. El nivel Juegos no lo toca; Personalizar lo ofrece si no imprimes.", "Printing. The Gaming level leaves it; Customize offers it if you never print."),
            K("NlaSvc", "Detecta si tienes internet y qué red es.", "Detects whether you are online and which network it is."),
            K("WlanSvc", "Wi-Fi. Imprescindible si te conectas por Wi-Fi.", "Wi-Fi. Required if you connect over Wi-Fi."),
            K("Schedule", "Programador de tareas: Windows lo necesita para mantenimiento y arranque.", "Task Scheduler: Windows needs it for maintenance and startup."),
            K("UsoSvc", "Orquesta Windows Update.", "Orchestrates Windows Update."),
            K("SecurityHealthService", "Seguridad de Windows (el panel).", "Windows Security (the dashboard)."),
            K("TrustedInstaller", "Instala actualizaciones y componentes de Windows.", "Installs updates and Windows components."),

            M("DoSvc", "Optimización de distribución: comparte actualizaciones entre equipos. El nivel Rápido lo limita a tu red local.", "Delivery Optimization: shares updates between PCs. The Quick level limits it to your local network."),
            M("WSearch", "Indexador de búsqueda: acelera la búsqueda de archivos a cambio de trabajo en segundo plano. El nivel Máximo lo desactiva.", "Search indexer: faster file search in exchange for background work. The Maximum level disables it."),
            M("SysMain", "Superfetch: precarga programas en memoria. En SSD su beneficio es pequeño; en HDD ayuda.", "Superfetch: preloads programs into memory. On an SSD the benefit is small; on an HDD it helps."),
            M("TabletInputService", "Teclado táctil y escritura a mano.", "Touch keyboard and handwriting."),
            M("WerSvc", "Informe de errores de Windows: envía datos de fallos.", "Windows Error Reporting: sends crash data."),
            M("PcaSvc", "Asistente de compatibilidad de programas.", "Program Compatibility Assistant."),
            M("TrkWks", "Mantiene accesos directos al mover archivos entre discos NTFS de la red.", "Keeps shortcuts working when files move between networked NTFS drives."),
            M("lmhosts", "Nombres NetBIOS en redes antiguas.", "NetBIOS names on older networks."),
            M("WbioSrvc", "Huella dactilar y reconocimiento facial (Windows Hello).", "Fingerprint and face sign-in (Windows Hello)."),
            M("bthserv", "Bluetooth: necesario para mandos, auriculares y ratones Bluetooth.", "Bluetooth: needed for Bluetooth controllers, headphones and mice."),
            M("Wcmsvc", "Gestor de conexiones de Windows.", "Windows connection manager."),

            D("DiagTrack", "Envía datos de uso y diagnóstico a Microsoft.", "Sends usage and diagnostic data to Microsoft.",
                "El nivel Rápido lo desactiva.", "The Quick level disables it."),
            D("dmwappushservice", "Mensajes de inscripción de dispositivos (MDM) y telemetría.", "Device enrollment (MDM) messages and telemetry.",
                "si tu equipo no lo gestiona una empresa", "if a company does not manage your PC"),
            D("RetailDemo", "Modo demostración de tienda.", "Retail store demo mode.", "siempre (es para equipos de exposición)", "always (it is for store display PCs)"),
            D("MapsBroker", "Descarga y actualiza mapas sin conexión.", "Downloads and updates offline maps.", "si no usas la app Mapas", "if you do not use the Maps app"),
            D("Fax", "Enviar y recibir faxes.", "Send and receive faxes.", "si no usas fax", "if you do not use fax"),
            D("WMPNetworkSvc", "Comparte la biblioteca del Reproductor de Windows Media en la red.", "Shares the Windows Media Player library on the network.", "si no compartes música o vídeo en red", "if you do not share music or video on the network"),
            D("WpcMonSvc", "Control parental.", "Parental controls.", "si nadie usa control parental en este equipo", "if nobody uses parental controls on this PC"),
            D("lfsvc", "Ubicación del equipo para apps y mapas.", "PC location for apps and maps.", "si no necesitas ubicación", "if you do not need location"),
            D("XblAuthManager", "Inicio de sesión de Xbox Live.", "Xbox Live sign-in.", "si no usas Xbox, Game Pass ni juegos de la Store", "if you do not use Xbox, Game Pass or Store games"),
            D("XblGameSave", "Guardado en la nube de juegos Xbox.", "Cloud saves for Xbox games.", "si no usas Xbox, Game Pass ni juegos de la Store", "if you do not use Xbox, Game Pass or Store games"),
            D("XboxNetApiSvc", "Red de Xbox Live (partidas en línea).", "Xbox Live networking (online play).", "si no usas Xbox, Game Pass ni juegos de la Store", "if you do not use Xbox, Game Pass or Store games"),
            D("XboxGipSvc", "Accesorios Xbox (mandos con cable o adaptador).", "Xbox accessories (wired controllers or adapter).", "si no usas mandos de Xbox", "if you do not use Xbox controllers"),
            D("RemoteRegistry", "Permite editar el registro desde otro equipo.", "Lets another PC edit this registry.", "casi siempre: es un riesgo si está activo", "almost always: it is a risk when on"),
            D("SharedAccess", "Compartir la conexión a internet con otros equipos.", "Shares the internet connection with other PCs.", "si no compartes tu conexión (zona Wi-Fi móvil)", "if you do not share your connection (mobile hotspot)"),
            D("PhoneSvc", "Telefonía (llamadas desde el PC con el móvil).", "Telephony (calls from the PC with your phone).", "si no haces llamadas desde el PC", "if you do not make calls from the PC"),
            D("SEMgrSvc", "Pagos y NFC.", "Payments and NFC.", "si no pagas con NFC desde el equipo", "if you do not pay with NFC from the PC"),
            D("wisvc", "Programa Windows Insider.", "Windows Insider program.", "si no estás en el programa Insider", "if you are not in the Insider program"),
            D("icssvc", "Zona con cobertura inalámbrica móvil (hotspot).", "Mobile hotspot.", "si no usas el PC como hotspot", "if you do not use the PC as a hotspot"),
            D("RmSvc", "Gestión de radios (modo avión, Bluetooth, Wi-Fi) en portátiles y tabletas.", "Radio management (airplane mode, Bluetooth, Wi-Fi) on laptops and tablets.", "en sobremesas sin Wi-Fi ni Bluetooth", "on desktops without Wi-Fi or Bluetooth"),
            D("TapiSrv", "Telefonía clásica y módems.", "Classic telephony and modems.", "si no usas módem ni fax", "if you do not use a modem or fax"),
        };

        private static readonly Dictionary<string, ServiceNote> ByName = All.ToDictionary(n => n.Name, StringComparer.OrdinalIgnoreCase);

        public static ServiceNote For(string name) => name != null && ByName.TryGetValue(name, out var n) ? n : null;
    }
}
