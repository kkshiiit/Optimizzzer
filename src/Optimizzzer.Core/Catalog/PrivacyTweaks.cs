using System.Collections.Generic;
using static Optimizzzer.Core.Catalog.TweakBuilder;
using P = Optimizzzer.Core.ProfileKind;

namespace Optimizzzer.Core.Catalog
{
    internal static class PrivacyTweaks
    {
        private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private const string DataCollection = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection";

        public static void Add(List<Tweak> l)
        {
            new TweakBuilder(l, "privacy.telemetry", Category.Privacy, Risk.Safe)
                .Title("Reducir la telemetría de Windows al mínimo", "Reduce Windows telemetry to the minimum")
                .Summary("Limita los datos de diagnóstico que Windows envía a Microsoft.", "Limits the diagnostic data Windows sends to Microsoft.")
                .Why("Windows envía datos de uso y diagnóstico de forma continua. Esta directiva los reduce al nivel más bajo que permite tu edición (en Home y Pro equivale a \"Datos obligatorios\").",
                     "Windows continuously sends usage and diagnostic data. This policy lowers it to the minimum your edition allows (on Home and Pro that means \"Required data\").")
                .Downside("Microsoft recibe menos información para diagnosticar fallos de tu equipo.", "Microsoft gets less information to diagnose crashes on your PC.")
                .Source("Microsoft Learn: Policy CSP System/AllowTelemetry")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(LM, DataCollection, "AllowTelemetry", 0)
                .Dword(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0)
                .Dword(LM, DataCollection, "DisableOneSettingsDownloads", 1);

            new TweakBuilder(l, "privacy.diagtrack", Category.Privacy, Risk.Safe)
                .Title("Desactivar el servicio de telemetría (DiagTrack)", "Disable the telemetry service (DiagTrack)")
                .Summary("Detiene el servicio \"Experiencias del usuario y telemetría asociadas\".", "Stops the \"Connected User Experiences and Telemetry\" service.")
                .Why("Es el servicio que recoge y sube los datos de diagnóstico. Sin él, la telemetría deja de enviarse y se libera algo de disco y CPU en segundo plano.",
                     "This service collects and uploads diagnostic data. Without it, telemetry stops being sent and some background disk and CPU use goes away.")
                .Downside("El Visor de datos de diagnóstico deja de mostrar información nueva.", "Diagnostic Data Viewer stops showing new information.")
                .Source("Microsoft Learn: Configure Windows diagnostic data")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Service("DiagTrack", StartMode.Disabled);

            new TweakBuilder(l, "privacy.telemetryTasks", Category.Privacy, Risk.Safe)
                .Title("Desactivar las tareas programadas de telemetría", "Disable telemetry scheduled tasks")
                .Summary("Apaga las tareas que recopilan datos de compatibilidad y de uso.", "Turns off the tasks that collect compatibility and usage data.")
                .Why("Estas tareas se ejecutan en segundo plano y pueden usar mucho disco y CPU durante minutos (sobre todo \"Microsoft Compatibility Appraiser\").",
                     "These tasks run in the background and can use heavy disk and CPU for minutes (especially \"Microsoft Compatibility Appraiser\").")
                .Downside("Windows Update tarda más en comprobar si tu PC es compatible con una nueva versión.", "Windows Update takes longer to check whether your PC can run a new version.")
                .Source("Microsoft Learn: Manage connections from Windows components")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Task(@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser")
                .Task(@"\Microsoft\Windows\Application Experience\ProgramDataUpdater")
                .Task(@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator")
                .Task(@"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip")
                .Task(@"\Microsoft\Windows\Autochk\Proxy")
                .Task(@"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector")
                .Task(@"\Microsoft\Windows\Feedback\Siuf\DmClient")
                .Task(@"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload");

            new TweakBuilder(l, "privacy.advertisingId", Category.Privacy, Risk.Safe)
                .Title("Desactivar el identificador de publicidad", "Turn off the advertising ID")
                .Summary("Las apps dejan de poder usar un ID único para mostrarte anuncios personalizados.", "Apps can no longer use a unique ID to show you personalised ads.")
                .Why("El identificador permite a las apps seguir tus intereses entre aplicaciones. Sin él seguirás viendo anuncios, pero no basados en tu perfil.",
                     "The ID lets apps track your interests across apps. Without it you still see ads, but not based on your profile.")
                .Source("Microsoft Learn: Policy CSP Privacy/DisableAdvertisingId")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1);

            new TweakBuilder(l, "privacy.tailored", Category.Privacy, Risk.Safe)
                .Title("Desactivar las experiencias personalizadas", "Turn off tailored experiences")
                .Summary("Microsoft deja de usar tus datos de diagnóstico para sugerencias y anuncios.", "Microsoft stops using your diagnostic data for tips and ads.")
                .Why("Con esta opción activada, los consejos y ofertas que ves en Windows se eligen a partir de cómo usas el equipo.",
                     "When on, the tips and offers you see in Windows are picked based on how you use your PC.")
                .Source("Configuración > Privacidad > General")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0);

            new TweakBuilder(l, "privacy.activityHistory", Category.Privacy, Risk.Safe)
                .Title("Desactivar el historial de actividad", "Turn off activity history")
                .Summary("Windows deja de guardar qué archivos, apps y webs abres.", "Windows stops recording which files, apps and sites you open.")
                .Why("El historial de actividad registra lo que haces para la línea de tiempo y para sincronizarlo con tu cuenta.",
                     "Activity history records what you do for Timeline and to sync it with your account.")
                .Downside("No podrás retomar actividades desde otros dispositivos.", "You cannot pick up activities from other devices.")
                .Source("Microsoft Learn: Policy CSP Privacy/EnableActivityFeed")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0);

            new TweakBuilder(l, "privacy.feedback", Category.Privacy, Risk.Safe)
                .Title("No pedir comentarios", "Stop feedback requests")
                .Summary("Windows deja de mostrar encuestas y peticiones de opinión.", "Windows stops showing surveys and feedback prompts.")
                .Why("Las notificaciones de comentarios interrumpen y envían tus respuestas junto con datos del sistema.",
                     "Feedback notifications interrupt you and send your answers along with system data.")
                .Source("Microsoft Learn: Policy CSP Experience/DoNotShowFeedbackNotifications")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0)
                .Dword(LM, DataCollection, "DoNotShowFeedbackNotifications", 1);

            new TweakBuilder(l, "privacy.inking", Category.Privacy, Risk.Safe)
                .Title("No recopilar lo que escribes a mano o con el teclado", "Stop inking and typing data collection")
                .Summary("Desactiva la personalización de escritura que envía muestras a Microsoft.", "Turns off typing personalisation that sends samples to Microsoft.")
                .Why("Windows guarda palabras que escribes y trazos de lápiz para mejorar el reconocimiento, y puede enviarlos a Microsoft.",
                     "Windows stores words you type and pen strokes to improve recognition, and can send them to Microsoft.")
                .Downside("La predicción de texto y el reconocimiento de escritura aprenden menos de ti.", "Text prediction and handwriting recognition learn less from you.")
                .Source("Configuración > Privacidad > Personalización de entrada manuscrita y escritura")
                .Profiles(P.Privacy)
                .Dword(CU, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1)
                .Dword(CU, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1)
                .Dword(CU, @"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0)
                .Dword(CU, @"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0);

            new TweakBuilder(l, "privacy.appLaunchTracking", Category.Privacy, Risk.Safe)
                .Title("No registrar qué apps abres", "Do not track app launches")
                .Summary("Windows deja de contar las apps que abres para ordenar Inicio y la búsqueda.", "Windows stops counting app launches to rank Start and search.")
                .Why("Es un registro local de uso. Quitarlo evita que otros usuarios del equipo vean tus apps más usadas.",
                     "It is a local usage log. Turning it off stops others on this PC from seeing your most used apps.")
                .Downside("Inicio deja de mostrar \"Más usadas\".", "Start no longer shows \"Most used\".")
                .Source("Configuración > Privacidad > General")
                .Profiles(P.Privacy)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0);

            new TweakBuilder(l, "privacy.languageList", Category.Privacy, Risk.Safe)
                .Title("No compartir tu lista de idiomas con las webs", "Do not share your language list with websites")
                .Summary("Las webs dejan de recibir la lista de idiomas instalados.", "Websites no longer receive your installed language list.")
                .Why("La lista de idiomas es un dato más que se usa para identificarte entre webs (huella digital).",
                     "The language list is one more signal used to fingerprint you across sites.")
                .Source("Configuración > Privacidad > General")
                .Profiles(P.Minimal | P.Privacy)
                .Dword(CU, @"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1);

            new TweakBuilder(l, "privacy.settingsSuggestions", Category.Privacy, Risk.Safe)
                .Title("Quitar el contenido sugerido en Configuración", "Remove suggested content in Settings")
                .Summary("Configuración deja de mostrar promociones y apps recomendadas.", "Settings stops showing promotions and recommended apps.")
                .Why("Ese contenido se descarga de Microsoft y se elige según tu uso.", "That content is downloaded from Microsoft and picked from your usage.")
                .Source("Configuración > Privacidad > General")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, Cdm, "SubscribedContent-338393Enabled", 0)
                .Dword(CU, Cdm, "SubscribedContent-353694Enabled", 0)
                .Dword(CU, Cdm, "SubscribedContent-353696Enabled", 0);

            new TweakBuilder(l, "privacy.webSearch", Category.Privacy, Risk.Safe)
                .Title("Quitar los resultados web de Bing en la búsqueda", "Remove Bing web results from search")
                .Summary("La búsqueda de Inicio solo busca en tu PC.", "Start search only looks on your PC.")
                .Why("Todo lo que escribes en Inicio se envía a Bing. Sin resultados web, la búsqueda es más rápida y privada.",
                     "Everything you type in Start is sent to Bing. Without web results, search is faster and private.")
                .Downside("Para buscar en internet tendrás que abrir el navegador.", "To search the web you will need to open the browser.")
                .Source("Microsoft Learn: Policy CSP Search/DisableSearchBoxSuggestions")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Restart(RestartKind.Explorer)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0)
                .Dword(CU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1);

            new TweakBuilder(l, "privacy.cortana", Category.Privacy, Risk.Safe)
                .Title("Desactivar Cortana", "Turn off Cortana")
                .Summary("Cortana deja de funcionar y de escuchar.", "Cortana stops working and listening.")
                .Why("Cortana ya no recibe funciones nuevas y sigue cargándose en segundo plano en Windows 10.",
                     "Cortana no longer gets new features and still loads in the background on Windows 10.")
                .Source("Microsoft Learn: Policy CSP Experience/AllowCortana")
                .Win10Only()
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0);

            new TweakBuilder(l, "privacy.errorReporting", Category.Privacy, Risk.Safe)
                .Title("Desactivar el informe de errores de Windows", "Turn off Windows Error Reporting")
                .Summary("Los fallos de las apps dejan de enviarse a Microsoft.", "App crashes are no longer sent to Microsoft.")
                .Why("Cada fallo genera un informe que se procesa y sube en segundo plano, a veces con partes de la memoria del programa.",
                     "Each crash creates a report that is processed and uploaded in the background, sometimes with parts of the program's memory.")
                .Downside("Microsoft y los desarrolladores no recibirán tus informes de fallos.", "Microsoft and developers will not get your crash reports.")
                .Source("Microsoft Learn: Windows Error Reporting settings")
                .Profiles(P.Privacy)
                .Dword(LM, @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1);

            new TweakBuilder(l, "privacy.location", Category.Privacy, Risk.Moderate)
                .Title("Desactivar la ubicación", "Turn off location")
                .Summary("Ninguna app puede saber dónde estás.", "No app can know where you are.")
                .Why("Si no usas mapas ni el tiempo local, la ubicación solo es un dato más que las apps pueden leer.",
                     "If you do not use maps or local weather, location is just one more thing apps can read.")
                .Downside("Mapas, el tiempo, \"Buscar mi dispositivo\" y el cambio automático de zona horaria dejan de funcionar.", "Maps, weather, Find my device and automatic time zone stop working.")
                .Source("Configuración > Privacidad > Ubicación")
                .Profiles(P.Privacy)
                .Str(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny");

            new TweakBuilder(l, "privacy.powershellTelemetry", Category.Privacy, Risk.Safe)
                .Title("Desactivar la telemetría de PowerShell 7", "Turn off PowerShell 7 telemetry")
                .Summary("PowerShell 7 deja de enviar datos de uso al iniciarse.", "PowerShell 7 stops sending usage data at startup.")
                .Why("PowerShell 7 envía datos de uso anónimos cada vez que se abre, salvo que esta variable esté definida.",
                     "PowerShell 7 sends anonymous usage data every time it starts unless this variable is set.")
                .Source("Microsoft Learn: about_Telemetry")
                .Profiles(P.Privacy)
                .Restart(RestartKind.SignOut)
                .Str(LM, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", "POWERSHELL_TELEMETRY_OPTOUT", "1");

            new TweakBuilder(l, "network.llmnr", Category.Network, Risk.Safe)
                .Title("Desactivar LLMNR", "Turn off LLMNR")
                .Summary("Elimina un protocolo antiguo de resolución de nombres en la red local.", "Removes an old local name resolution protocol.")
                .Why("LLMNR permite que otro equipo de la red se haga pasar por un servidor y capture credenciales. Las redes domésticas modernas no lo necesitan.",
                     "LLMNR lets another device on the network pretend to be a server and capture credentials. Modern home networks do not need it.")
                .Downside("En redes muy antiguas sin DNS, algunos equipos podrían dejar de encontrarse por nombre.", "On very old networks without DNS, some devices may stop finding each other by name.")
                .Source("Microsoft Learn: Policy Turn off multicast name resolution")
                .Profiles(P.Privacy)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", "EnableMulticast", 0);
        }
    }
}
