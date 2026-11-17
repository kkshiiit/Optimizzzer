using System;
using System.Collections.Generic;
using System.IO;
using static Optimizzzer.Core.Catalog.TweakBuilder;
using P = Optimizzzer.Core.ProfileKind;

namespace Optimizzzer.Core.Catalog
{
    internal static class SystemTweaks
    {
        private const string WU = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";

        public static void Add(List<Tweak> l)
        {
            AddUpdate(l);
            AddServices(l);
            AddApps(l);
        }

        private static void AddUpdate(List<Tweak> l)
        {
            new TweakBuilder(l, "update.noAutoRestart", Category.Update, Risk.Safe)
                .Title("No reiniciar solo mientras hay una sesión abierta", "No automatic restart while you are signed in")
                .Summary("Windows Update espera a que reinicies tú en lugar de hacerlo por su cuenta.", "Windows Update waits for you to restart instead of doing it on its own.")
                .Why("Evita perder trabajo o una partida por un reinicio automático. Las actualizaciones se siguen descargando e instalando.",
                     "Prevents losing work or a game to an automatic restart. Updates are still downloaded and installed.")
                .Downside("Tendrás que reiniciar tú para terminar de instalar las actualizaciones.", "You must restart yourself to finish installing updates.")
                .Source("Microsoft Learn: Policy NoAutoRebootWithLoggedOnUsers")
                .Profiles(P.Office | P.Gaming)
                .Dword(LM, WU + @"\AU", "NoAutoRebootWithLoggedOnUsers", 1);

            new TweakBuilder(l, "update.deliveryOptimization", Category.Update, Risk.Safe)
                .Title("No compartir actualizaciones con otros PC de internet", "Do not share updates with other PCs on the internet")
                .Summary("Windows descarga actualizaciones solo de Microsoft y no sube partes a desconocidos.", "Windows downloads updates only from Microsoft and does not upload parts to strangers.")
                .Why("La Optimización de distribución usa tu conexión para enviar actualizaciones a otros equipos, lo que puede saturar la subida mientras juegas o haces videollamadas.",
                     "Delivery Optimization uses your connection to send updates to other PCs, which can saturate your upload while gaming or on video calls.")
                .Downside("Las actualizaciones no se comparten entre los equipos de tu propia red.", "Updates are not shared between PCs on your own network.")
                .Source("Microsoft Learn: Delivery Optimization DODownloadMode")
                .Profiles(P.Minimal | P.Gaming | P.Privacy | P.Laptop)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0);

            new TweakBuilder(l, "update.noDrivers", Category.Update, Risk.Moderate)
                .Title("No instalar controladores desde Windows Update", "Do not install drivers through Windows Update")
                .Summary("Windows Update deja de sustituir los controladores que tú instalaste.", "Windows Update stops replacing drivers you installed yourself.")
                .Why("Windows Update a veces instala controladores de GPU o audio más antiguos o genéricos encima de los del fabricante.",
                     "Windows Update sometimes installs older or generic GPU or audio drivers over the manufacturer's ones.")
                .Downside("Tendrás que actualizar los controladores tú mismo desde la web del fabricante.", "You must update drivers yourself from the manufacturer's website.")
                .Source("Microsoft Learn: Policy CSP Update/ExcludeWUDriversInQualityUpdate")
                .Dword(LM, WU, "ExcludeWUDriversInQualityUpdate", 1);

            new TweakBuilder(l, "update.stayOnVersion", Category.Update, Risk.Moderate)
                .Title("Quedarse en la versión actual de Windows 11", "Stay on the current Windows 11 version")
                .Summary("Recibes parches de seguridad, pero no el salto a la siguiente versión anual.", "You get security patches but not the jump to the next yearly version.")
                .Why("Las versiones nuevas a veces traen errores o cambios que no quieres. Esta directiva oficial fija la versión que ya tienes.",
                     "New versions sometimes bring bugs or changes you do not want. This official policy pins the version you already have.")
                .Downside("Cuando tu versión deje de tener soporte tendrás que deshacer este cambio para actualizar.", "When your version reaches end of support you must undo this to upgrade.")
                .Source("Microsoft Learn: Policy CSP Update/TargetReleaseVersion")
                .Win11()
                .Dword(LM, WU, "TargetReleaseVersion", 1)
                .Str(LM, WU, "ProductVersion", "Windows 11")
                .Action(new CurrentVersionPinAction(WU));
        }

        private static void AddServices(List<Tweak> l)
        {
            new TweakBuilder(l, "svc.dmwappush", Category.Services, Risk.Safe)
                .Title("Desactivar el servicio de enrutamiento de mensajes push (dmwappushservice)", "Disable the WAP push message routing service")
                .Summary("Apaga un servicio que acompaña a la telemetría.", "Turns off a service that works alongside telemetry.")
                .Why("En un PC personal solo se usa para telemetría y mensajes de administración.", "On a personal PC it is only used for telemetry and management messages.")
                .Downside("En equipos de empresa gestionados con Intune puede ser necesario.", "On company PCs managed with Intune it may be required.")
                .Source("Microsoft Learn: Windows services guidance")
                .Profiles(P.Privacy)
                .Service("dmwappushservice", StartMode.Disabled);

            new TweakBuilder(l, "svc.retailDemo", Category.Services, Risk.Safe)
                .Title("Desactivar el servicio de demostración para tiendas", "Disable the retail demo service")
                .Summary("Solo sirve para equipos expuestos en tiendas.", "Only used for PCs on display in shops.")
                .Why("No tiene ningún uso en un equipo personal.", "It has no use on a personal PC.")
                .Source("Microsoft Learn: Windows services guidance")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Service("RetailDemo", StartMode.Disabled);

            new TweakBuilder(l, "svc.maps", Category.Services, Risk.Safe)
                .Title("Desactivar el administrador de mapas descargados", "Disable the downloaded maps manager")
                .Summary("Apaga el servicio que actualiza mapas sin conexión.", "Turns off the service that updates offline maps.")
                .Why("Si no usas la app Mapas, este servicio solo consume red en segundo plano.", "If you do not use the Maps app, this service only uses network in the background.")
                .Downside("Los mapas sin conexión de la app Mapas dejan de actualizarse.", "Offline maps in the Maps app stop updating.")
                .Source("Microsoft Learn: Windows services guidance")
                .Profiles(P.Minimal | P.Gaming | P.Privacy)
                .Service("MapsBroker", StartMode.Disabled);

            new TweakBuilder(l, "svc.fax", Category.Services, Risk.Safe)
                .Title("Desactivar el servicio de Fax", "Disable the Fax service")
                .Summary("Apaga el servicio de envío y recepción de faxes.", "Turns off the fax send and receive service.")
                .Why("Casi nadie usa fax desde el PC.", "Almost nobody sends faxes from a PC.")
                .Source("Microsoft Learn: Windows services guidance")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop)
                .Service("Fax", StartMode.Disabled);

            new TweakBuilder(l, "svc.wmpNetwork", Category.Services, Risk.Safe)
                .Title("Desactivar el uso compartido de Windows Media Player", "Disable Windows Media Player network sharing")
                .Summary("Deja de compartir tu biblioteca multimedia en la red.", "Stops sharing your media library on the network.")
                .Why("Expone tu música y vídeos a otros dispositivos de la red sin que lo necesites.", "It exposes your music and videos to other devices on the network without need.")
                .Source("Microsoft Learn: Windows services guidance")
                .Profiles(P.Minimal | P.Gaming | P.Privacy)
                .Service("WMPNetworkSvc", StartMode.Disabled);

            new TweakBuilder(l, "svc.parental", Category.Services, Risk.Safe)
                .Title("Desactivar el control parental", "Disable parental controls")
                .Summary("Apaga el servicio de Microsoft Family Safety.", "Turns off the Microsoft Family Safety service.")
                .Why("Si en este equipo no hay cuentas de menores, el servicio no hace nada útil.", "If there are no child accounts on this PC, the service does nothing useful.")
                .Downside("No lo apliques si usas Microsoft Family para cuentas infantiles.", "Do not apply it if you use Microsoft Family for child accounts.")
                .Source("Microsoft Learn: Windows services guidance")
                .Profiles(P.Gaming)
                .Service("WpcMonSvc", StartMode.Disabled);

            new TweakBuilder(l, "svc.xbox", Category.Services, Risk.Moderate)
                .Title("Desactivar los servicios de Xbox", "Disable Xbox services")
                .Summary("Apaga los servicios de inicio de sesión, partidas guardadas y red de Xbox.", "Turns off the Xbox sign-in, cloud save and networking services.")
                .Why("Si no juegas con Game Pass ni con la app Xbox, estos servicios no se usan.", "If you do not play Game Pass or use the Xbox app, these services are unused.")
                .Downside("Los juegos de Game Pass y de la Microsoft Store pueden no arrancar o perder partidas en la nube.", "Game Pass and Microsoft Store games may fail to start or lose cloud saves.")
                .Source("Microsoft Learn: Windows services guidance")
                .Profiles(P.Office)
                .Service("XblAuthManager", StartMode.Disabled)
                .Service("XblGameSave", StartMode.Disabled)
                .Service("XboxNetApiSvc", StartMode.Disabled)
                .Service("XboxGipSvc", StartMode.Disabled);

            new TweakBuilder(l, "svc.spooler", Category.Services, Risk.Moderate)
                .Title("Desactivar la cola de impresión", "Disable the print spooler")
                .Summary("Apaga el servicio de impresión.", "Turns off the printing service.")
                .Why("Si no tienes impresora, el servicio está activo sin uso y ha tenido fallos de seguridad graves (PrintNightmare).",
                     "If you have no printer, the service runs for nothing and has had serious security flaws (PrintNightmare).")
                .Downside("No podrás imprimir ni guardar como PDF con \"Microsoft Print to PDF\".", "You cannot print or save with \"Microsoft Print to PDF\".")
                .Source("Microsoft Security Response Center: CVE-2021-34527")
                .Service("Spooler", StartMode.Disabled);

            new TweakBuilder(l, "svc.geolocation", Category.Services, Risk.Moderate)
                .Title("Desactivar el servicio de geolocalización", "Disable the geolocation service")
                .Summary("Apaga el servicio que calcula tu ubicación.", "Turns off the service that works out your location.")
                .Why("Complementa la opción de privacidad de ubicación: sin servicio, ninguna app puede pedirla.", "Complements the location privacy option: without the service, no app can request it.")
                .Downside("Mapas, el tiempo y la zona horaria automática dejan de funcionar.", "Maps, weather and automatic time zone stop working.")
                .Source("Microsoft Learn: Windows services guidance")
                .Profiles(P.Privacy)
                .Service("lfsvc", StartMode.Disabled);
        }

        private static void App(List<Tweak> l, string id, string package, string esName, string enName, Risk risk, ProfileKind profiles, string esDown = null, string enDown = null)
        {
            new TweakBuilder(l, "app." + id, Category.Apps, risk)
                .Title("Quitar " + esName, "Remove " + enName)
                .Summary("Desinstala la app para todos los usuarios y evita que se reinstale en cuentas nuevas.", "Uninstalls the app for all users and stops it being reinstalled for new accounts.")
                .Why("Es una app preinstalada que muchos no usan. Ocupa espacio y algunas se actualizan o se ejecutan en segundo plano.",
                     "It is a preinstalled app many people never use. It takes space and some update or run in the background.")
                .Downside(esDown ?? "Para recuperarla tendrás que instalarla de nuevo desde la Microsoft Store.", enDown ?? "To get it back you must install it again from the Microsoft Store.")
                .Source("Paquete " + package)
                .Profiles(profiles)
                .Appx(package);
        }

        private static void AddApps(List<Tweak> l)
        {
            const P All = P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy;
            const P Lean = P.Gaming | P.Privacy;
            App(l, "bingNews", "Microsoft.BingNews", "Noticias (Microsoft News)", "News (Microsoft News)", Risk.Safe, All);
            App(l, "bingWeather", "Microsoft.BingWeather", "El Tiempo (MSN)", "Weather (MSN)", Risk.Safe, Lean);
            App(l, "bingSearch", "Microsoft.BingSearch", "Bing Search", "Bing Search", Risk.Safe, All);
            App(l, "getHelp", "Microsoft.GetHelp", "Obtener ayuda", "Get Help", Risk.Safe, Lean);
            App(l, "tips", "Microsoft.Getstarted", "Consejos", "Tips", Risk.Safe, All);
            App(l, "solitaire", "Microsoft.MicrosoftSolitaireCollection", "Microsoft Solitaire Collection", "Microsoft Solitaire Collection", Risk.Safe, P.Office | P.Privacy);
            App(l, "people", "Microsoft.People", "Contactos", "People", Risk.Safe, Lean);
            App(l, "feedbackHub", "Microsoft.WindowsFeedbackHub", "Centro de opiniones", "Feedback Hub", Risk.Safe, All);
            App(l, "moviesTv", "Microsoft.ZuneVideo", "Películas y TV", "Movies & TV", Risk.Safe, Lean);
            App(l, "officeHub", "Microsoft.MicrosoftOfficeHub", "Microsoft 365 (Office) app", "Microsoft 365 (Office) app", Risk.Safe, P.Gaming | P.Privacy,
                "Solo es un acceso a la web de Office; tus programas de Office instalados no se tocan.", "It is only a shortcut to Office online; your installed Office programs are not touched.");
            App(l, "todo", "Microsoft.Todos", "Microsoft To Do", "Microsoft To Do", Risk.Safe, P.None);
            App(l, "maps", "Microsoft.WindowsMaps", "Mapas", "Maps", Risk.Safe, Lean);
            App(l, "mixedReality", "Microsoft.MixedReality.Portal", "Portal de realidad mixta", "Mixed Reality Portal", Risk.Safe, All);
            App(l, "3dViewer", "Microsoft.Microsoft3DViewer", "Visor 3D", "3D Viewer", Risk.Safe, All);
            App(l, "cortanaApp", "Microsoft.549981C3F5F10", "Cortana (app)", "Cortana (app)", Risk.Safe, All);
            App(l, "teamsPersonal", "MicrosoftTeams", "Teams personal (Chat)", "Teams personal (Chat)", Risk.Safe, All,
                "No afecta a Teams de trabajo o escuela.", "Does not affect Teams for work or school.");
            App(l, "clipchamp", "Clipchamp.Clipchamp", "Clipchamp", "Clipchamp", Risk.Safe, Lean);
            App(l, "powerAutomate", "Microsoft.PowerAutomateDesktop", "Power Automate", "Power Automate", Risk.Safe, Lean);
            App(l, "copilotApp", "Microsoft.Copilot", "Copilot (app)", "Copilot (app)", Risk.Safe, P.Privacy | P.Gaming);
            App(l, "devHome", "Microsoft.Windows.DevHome", "Dev Home", "Dev Home", Risk.Safe, All);
            App(l, "newOutlook", "Microsoft.OutlookForWindows", "Outlook (nuevo)", "Outlook (new)", Risk.Moderate, P.None,
                "Si usas el nuevo Outlook para tu correo, no la quites. El Outlook de Office no se toca.", "If you use new Outlook for email, keep it. Outlook from Office is not touched.");
            App(l, "phoneLink", "Microsoft.YourPhone", "Vínculo móvil", "Phone Link", Risk.Moderate, P.None,
                "Dejarás de ver notificaciones y fotos del móvil en el PC.", "You will stop seeing phone notifications and photos on the PC.");
            App(l, "quickAssist", "MicrosoftCorporationII.QuickAssist", "Asistencia rápida", "Quick Assist", Risk.Moderate, P.None,
                "Nadie podrá ayudarte en remoto con esta herramienta. Es también la favorita de los estafadores de \"soporte técnico\".", "Nobody can help you remotely with this tool. It is also a favourite of tech-support scammers.");
            App(l, "xboxApp", "Microsoft.GamingApp", "Xbox (app)", "Xbox (app)", Risk.Moderate, P.Office,
                "Los juegos de Game Pass dejarán de poder instalarse o abrirse.", "Game Pass games can no longer be installed or launched.");
            App(l, "xboxOverlay", "Microsoft.XboxGamingOverlay", "Xbox Game Bar", "Xbox Game Bar", Risk.Moderate, P.Office,
                "Perderás Win + G: capturas, grabación y widgets de juego.", "You lose Win + G: screenshots, recording and game widgets.");
            App(l, "spotify", "SpotifyAB.SpotifyMusic", "Spotify (preinstalada)", "Spotify (preinstalled)", Risk.Safe, All,
                "Si usas Spotify, no la quites.", "If you use Spotify, keep it.");
            App(l, "candyCrush", "king.com.CandyCrushSaga", "Candy Crush Saga", "Candy Crush Saga", Risk.Safe, All);
            App(l, "candyCrushSoda", "king.com.CandyCrushSodaSaga", "Candy Crush Soda Saga", "Candy Crush Soda Saga", Risk.Safe, All);
            App(l, "disneyPlus", "Disney.37853FC22B2CE", "Disney+ (preinstalada)", "Disney+ (preinstalled)", Risk.Safe, All,
                "Si usas Disney+, no la quites.", "If you use Disney+, keep it.");
            App(l, "primeVideo", "AmazonVideo.PrimeVideo", "Prime Video (preinstalada)", "Prime Video (preinstalled)", Risk.Safe, All,
                "Si usas Prime Video, no la quites.", "If you use Prime Video, keep it.");

            new TweakBuilder(l, "app.onedrive", Category.Apps, Risk.Moderate)
                .Title("Desinstalar OneDrive", "Uninstall OneDrive")
                .Summary("Quita OneDrive y su icono del Explorador. Tus archivos locales se quedan en el equipo.", "Removes OneDrive and its File Explorer icon. Your local files stay on the PC.")
                .Why("OneDrive arranca con Windows, sincroniza en segundo plano y a veces mueve tus carpetas Documentos y Escritorio a la nube sin que lo notes.",
                     "OneDrive starts with Windows, syncs in the background and sometimes moves your Documents and Desktop folders to the cloud without you noticing.")
                .Downside("Los archivos que solo estén en la nube (icono de nube) no se descargan. Revisa que todo esté en local antes. Para volver, descarga OneDrive de Microsoft.",
                          "Cloud-only files (cloud icon) are not downloaded. Make sure everything is local first. To go back, download OneDrive from Microsoft.")
                .Source("Microsoft Support: Uninstall OneDrive")
                .Action(new CustomAction
                {
                    Description = new Text("OneDriveSetup.exe /uninstall", "OneDriveSetup.exe /uninstall"),
                    DetectFn = c => OneDriveInstalled() ? ActionState.NotApplied : ActionState.Applied,
                    ApplyFn = c =>
                    {
                        c.Process.Run("taskkill", "/f /im OneDrive.exe", 15000);
                        var setup = OneDriveSetupPath();
                        if (setup == null) throw new InvalidOperationException("OneDriveSetup.exe not found");
                        c.Process.Run(setup, "/uninstall", 300000);
                    },
                });
        }

        private static bool OneDriveInstalled()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            return File.Exists(Path.Combine(local, @"Microsoft\OneDrive\OneDrive.exe"))
                || File.Exists(Path.Combine(pf, @"Microsoft OneDrive\OneDrive.exe"))
                || (pf86.Length > 0 && File.Exists(Path.Combine(pf86, @"Microsoft OneDrive\OneDrive.exe")));
        }

        private static string OneDriveSetupPath()
        {
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            foreach (var p in new[] { @"System32\OneDriveSetup.exe", @"SysWOW64\OneDriveSetup.exe" })
            {
                var full = Path.Combine(win, p);
                if (File.Exists(full)) return full;
            }
            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive");
            if (Directory.Exists(local))
                foreach (var f in Directory.GetFiles(local, "OneDriveSetup.exe", SearchOption.AllDirectories)) return f;
            return null;
        }
    }

    /// <summary>Writes TargetReleaseVersionInfo with the version this PC runs right now (for example 24H2).</summary>
    public sealed class CurrentVersionPinAction : TweakAction
    {
        private readonly string _path;
        public CurrentVersionPinAction(string path) { _path = path; }

        private RegistryValueAction Inner(SystemContext c) =>
            new RegistryValueAction(Hive.LocalMachine, _path, "TargetReleaseVersionInfo", RegValue.Str(c.Info?.DisplayVersion ?? ""));

        public override ActionState Detect(SystemContext ctx) =>
            string.IsNullOrEmpty(ctx.Info?.DisplayVersion) ? ActionState.NotApplicable : Inner(ctx).Detect(ctx);
        public override ActionSnapshot Snapshot(SystemContext ctx) => Inner(ctx).Snapshot(ctx);
        public override void Apply(SystemContext ctx) => Inner(ctx).Apply(ctx);
        public override void Revert(SystemContext ctx, ActionSnapshot s) => Inner(ctx).Revert(ctx, s);
        public override string Describe(Lang lang) =>
            @"HKLM\" + _path + @"\TargetReleaseVersionInfo = " + (lang == Lang.En ? "(your current version)" : "(tu versión actual)");
    }
}
