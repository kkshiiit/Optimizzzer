using System.Collections.Generic;
using static Optimizzzer.Core.Catalog.TweakBuilder;
using P = Optimizzzer.Core.ProfileKind;

namespace Optimizzzer.Core.Catalog
{
    internal static class InterfaceTweaks
    {
        private const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private const string WinAI = @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI";
        private const string WinAICU = @"Software\Policies\Microsoft\Windows\WindowsAI";

        public static void Add(List<Tweak> l)
        {
            AddAI(l);
            AddAds(l);
            AddExplorer(l);
            AddTaskbar(l);
        }

        private static void AddAI(List<Tweak> l)
        {
            new TweakBuilder(l, "ai.copilot", Category.WindowsAI, Risk.Safe)
                .Title("Desactivar Copilot en Windows", "Turn off Copilot in Windows")
                .Summary("Quita el botón de Copilot y bloquea su integración en el sistema.", "Removes the Copilot button and blocks its system integration.")
                .Why("Copilot se carga con Windows y envía lo que escribes a los servidores de Microsoft. Si no lo usas, solo ocupa espacio en la barra y memoria.",
                     "Copilot loads with Windows and sends what you type to Microsoft's servers. If you do not use it, it only takes taskbar space and memory.")
                .Downside("No podrás abrir Copilot desde la barra de tareas. La app de Copilot, si está instalada, se quita en Apps.", "You cannot open Copilot from the taskbar. The Copilot app, if installed, is removed under Apps.")
                .Source("Microsoft Learn: Policy CSP WindowsAI/TurnOffWindowsCopilot")
                .Win11(22621)
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Restart(RestartKind.Explorer)
                .Dword(CU, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)
                .Dword(CU, Adv, "ShowCopilotButton", 0);

            new TweakBuilder(l, "ai.recall", Category.WindowsAI, Risk.Safe)
                .Title("Desactivar Recall", "Turn off Recall")
                .Summary("Impide que Windows haga capturas periódicas de tu pantalla para buscarlas después.", "Stops Windows from taking periodic screenshots of your screen to search later.")
                .Why("Recall guarda capturas de todo lo que ves, incluidos documentos y webs privadas. Esta directiva lo desactiva y permite quitar la función.",
                     "Recall stores screenshots of everything you see, including private documents and sites. This policy turns it off and allows the feature to be removed.")
                .Source("Microsoft Learn: Policy CSP WindowsAI/DisableAIDataAnalysis, AllowRecallEnablement")
                .Win11(26100)
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Restart(RestartKind.Reboot)
                .Dword(LM, WinAI, "DisableAIDataAnalysis", 1)
                .Dword(CU, WinAICU, "DisableAIDataAnalysis", 1)
                .Dword(LM, WinAI, "AllowRecallEnablement", 0);

            new TweakBuilder(l, "ai.clickToDo", Category.WindowsAI, Risk.Safe)
                .Title("Desactivar Click to Do", "Turn off Click to Do")
                .Summary("Quita las acciones de IA sobre lo que hay en pantalla.", "Removes AI actions on what is on screen.")
                .Why("Click to Do analiza el contenido de tu pantalla para sugerir acciones. Si no lo usas, es otra función leyendo lo que ves.",
                     "Click to Do analyses your screen content to suggest actions. If you do not use it, it is one more feature reading what you see.")
                .Source("Microsoft Learn: Policy CSP WindowsAI/DisableClickToDo")
                .Win11(26100)
                .Profiles(P.Privacy)
                .Dword(LM, WinAI, "DisableClickToDo", 1)
                .Dword(CU, WinAICU, "DisableClickToDo", 1);

            new TweakBuilder(l, "ai.paintNotepad", Category.WindowsAI, Risk.Safe)
                .Title("Quitar las funciones de IA de Paint y Bloc de notas", "Remove AI features from Paint and Notepad")
                .Summary("Desactiva Cocreator, el relleno generativo, el creador de imágenes y la IA del Bloc de notas.", "Turns off Cocreator, generative fill, Image Creator and Notepad AI.")
                .Why("Estas funciones envían tu contenido a la nube y añaden botones que muchos no usan.", "These features send your content to the cloud and add buttons many people never use.")
                .Downside("No podrás generar imágenes ni reescribir texto con IA en esas apps.", "You cannot generate images or rewrite text with AI in those apps.")
                .Source("Microsoft Learn: Policy CSP WindowsAI (Paint, Notepad)")
                .Win11()
                .Profiles(P.Privacy)
                .Dword(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableCocreator", 1)
                .Dword(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeFill", 1)
                .Dword(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableImageCreator", 1)
                .Dword(LM, @"SOFTWARE\Policies\WindowsNotepad", "DisableAIFeatures", 1);

            new TweakBuilder(l, "ai.edgeSidebar", Category.WindowsAI, Risk.Safe)
                .Title("Quitar la barra lateral de Copilot en Edge", "Remove the Copilot sidebar in Edge")
                .Summary("Edge deja de mostrar el botón y el panel lateral de Copilot.", "Edge stops showing the Copilot button and side panel.")
                .Why("La barra lateral se carga en cada ventana de Edge aunque no la abras.", "The sidebar loads in every Edge window even if you never open it.")
                .Source("Microsoft Learn: Edge policy HubsSidebarEnabled")
                .Profiles(P.Office | P.Privacy)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0);

            new TweakBuilder(l, "ui.widgets", Category.WindowsAI, Risk.Safe)
                .Title("Quitar Widgets y Noticias", "Remove Widgets and News")
                .Summary("Quita el panel de noticias y el tiempo de la barra de tareas.", "Removes the news and weather panel from the taskbar.")
                .Why("El panel carga noticias y anuncios de MSN en segundo plano, usando memoria y red aunque no lo abras.",
                     "The panel loads MSN news and ads in the background, using memory and network even if you never open it.")
                .Downside("Pierdes el acceso rápido al tiempo desde la barra.", "You lose quick access to the weather from the taskbar.")
                .Source("Microsoft Learn: Policy CSP NewsAndInterests/AllowNewsAndInterests")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Restart(RestartKind.Explorer)
                .Action(new OsOnlyAction(OsFilter.Win11, new RegistryValueAction(LM, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", RegValue.DWord(0))))
                .Action(new OsOnlyAction(OsFilter.Win10, new RegistryValueAction(LM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", RegValue.DWord(0))));
        }

        private static void AddAds(List<Tweak> l)
        {
            new TweakBuilder(l, "ui.silentApps", Category.Interface, Risk.Safe)
                .Title("Impedir que Windows instale apps promocionadas", "Stop Windows installing promoted apps")
                .Summary("Windows deja de descargar juegos y apps de terceros por su cuenta.", "Windows stops downloading third-party games and apps on its own.")
                .Why("Windows instala apps patrocinadas (juegos, redes sociales) sin preguntar. Esto lo detiene para las que aún no se han instalado.",
                     "Windows installs sponsored apps (games, social media) without asking. This stops it for apps not yet installed.")
                .Source("Microsoft Learn: Manage Windows consumer features")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, Cdm, "SilentInstalledAppsEnabled", 0)
                .Dword(CU, Cdm, "PreInstalledAppsEnabled", 0)
                .Dword(CU, Cdm, "OemPreInstalledAppsEnabled", 0)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1);

            new TweakBuilder(l, "ui.tips", Category.Interface, Risk.Safe)
                .Title("Quitar consejos, trucos y sugerencias", "Remove tips, tricks and suggestions")
                .Summary("Windows deja de mostrar consejos en notificaciones y sugerencias en Inicio.", "Windows stops showing tips in notifications and suggestions in Start.")
                .Why("Estos avisos interrumpen y a menudo son anuncios de servicios de Microsoft.", "These prompts interrupt you and are often ads for Microsoft services.")
                .Source("Configuración > Sistema > Notificaciones")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, Cdm, "SubscribedContent-338389Enabled", 0)
                .Dword(CU, Cdm, "SubscribedContent-338388Enabled", 0)
                .Dword(CU, Cdm, "SystemPaneSuggestionsEnabled", 0)
                .Dword(CU, Cdm, "SoftLandingEnabled", 0)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0);

            new TweakBuilder(l, "ui.lockScreenAds", Category.Interface, Risk.Safe)
                .Title("Quitar datos curiosos y anuncios de la pantalla de bloqueo", "Remove fun facts and ads from the lock screen")
                .Summary("La pantalla de bloqueo solo muestra tu fondo y la hora.", "The lock screen only shows your background and the time.")
                .Why("Los textos de la pantalla de bloqueo incluyen promociones descargadas de Microsoft.", "Lock screen texts include promotions downloaded from Microsoft.")
                .Source("Configuración > Personalización > Pantalla de bloqueo")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, Cdm, "RotatingLockScreenOverlayEnabled", 0)
                .Dword(CU, Cdm, "SubscribedContent-338387Enabled", 0);

            new TweakBuilder(l, "ui.startRecommendations", Category.Interface, Risk.Safe)
                .Title("Quitar promociones del menú Inicio", "Remove promotions from Start")
                .Summary("Inicio deja de recomendar apps de la Store y avisos de tu cuenta.", "Start stops recommending Store apps and account notices.")
                .Why("La sección Recomendado incluye apps patrocinadas y avisos para usar OneDrive o Microsoft 365.",
                     "The Recommended section includes sponsored apps and nudges to use OneDrive or Microsoft 365.")
                .Source("Configuración > Personalización > Inicio")
                .Win11()
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, Adv, "Start_IrisRecommendations", 0)
                .Dword(CU, Adv, "Start_AccountNotifications", 0);

            new TweakBuilder(l, "ui.syncProviderAds", Category.Interface, Risk.Safe)
                .Title("Quitar anuncios del Explorador de archivos", "Remove ads in File Explorer")
                .Summary("El Explorador deja de mostrar avisos de OneDrive y Microsoft 365.", "File Explorer stops showing OneDrive and Microsoft 365 banners.")
                .Why("Son banners promocionales dentro de la ventana donde trabajas con tus archivos.", "They are promotional banners inside the window where you work with your files.")
                .Source("Explorador > Opciones de carpeta > Ver")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Dword(CU, Adv, "ShowSyncProviderNotifications", 0);
        }

        private static void AddExplorer(List<Tweak> l)
        {
            new TweakBuilder(l, "ui.fileExtensions", Category.Interface, Risk.Safe)
                .Title("Mostrar las extensiones de archivo", "Show file extensions")
                .Summary("Verás \"factura.pdf.exe\" en lugar de \"factura.pdf\".", "You will see \"invoice.pdf.exe\" instead of \"invoice.pdf\".")
                .Why("Ocultar extensiones es el truco más usado para disfrazar virus como documentos. Verlas te protege.",
                     "Hidden extensions are the most common trick to disguise malware as documents. Seeing them protects you.")
                .Source("Explorador > Ver > Extensiones de nombre de archivo")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop | P.Privacy)
                .Restart(RestartKind.Explorer)
                .Dword(CU, Adv, "HideFileExt", 0);

            new TweakBuilder(l, "ui.hiddenFiles", Category.Interface, Risk.Safe)
                .Title("Mostrar archivos ocultos", "Show hidden files")
                .Summary("El Explorador muestra carpetas como AppData.", "File Explorer shows folders like AppData.")
                .Why("Útil para encontrar configuraciones y archivos que ocupan espacio.", "Useful to find settings and files that take up space.")
                .Downside("Verás más carpetas del sistema; no las borres sin saber qué son.", "You will see more system folders; do not delete them unless you know what they are.")
                .Source("Explorador > Ver > Elementos ocultos")
                .Restart(RestartKind.Explorer)
                .Dword(CU, Adv, "Hidden", 1);

            new TweakBuilder(l, "ui.openThisPC", Category.Interface, Risk.Safe)
                .Title("Abrir el Explorador en \"Este equipo\"", "Open File Explorer to \"This PC\"")
                .Summary("El Explorador arranca mostrando tus unidades en lugar de archivos recientes.", "File Explorer starts on your drives instead of recent files.")
                .Why("Llegas antes a tus discos, y los archivos recientes no se muestran a quien mire tu pantalla.", "You reach your drives faster, and recent files are not shown to whoever looks at your screen.")
                .Source("Explorador > Opciones de carpeta > General")
                .Profiles(P.Office | P.Privacy)
                .Dword(CU, Adv, "LaunchTo", 1);

            new TweakBuilder(l, "ui.classicContextMenu", Category.Interface, Risk.Safe)
                .Title("Menú contextual clásico (clic derecho)", "Classic right-click menu")
                .Summary("El clic derecho muestra todas las opciones sin pulsar \"Mostrar más opciones\".", "Right-click shows every option without clicking \"Show more options\".")
                .Why("Ahorra un clic en cada acción y muestra las opciones de los programas que tienes instalados.", "Saves a click on every action and shows the options of the programs you installed.")
                .Downside("Pierdes el diseño nuevo del menú de Windows 11.", "You lose the new Windows 11 menu design.")
                .Source("Clave CLSID {86ca1aa0-34aa-4e8b-a509-50c905bae2a2} documentada por la comunidad (winutil, optimizer)")
                .Win11()
                .Profiles(P.Office)
                .Restart(RestartKind.Explorer)
                .Str(CU, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "");

            new TweakBuilder(l, "ui.homeGallery", Category.Interface, Risk.Safe)
                .Title("Quitar \"Inicio\" y \"Galería\" del Explorador", "Remove \"Home\" and \"Gallery\" from File Explorer")
                .Summary("El panel izquierdo del Explorador queda solo con tus carpetas y unidades.", "The File Explorer side pane keeps only your folders and drives.")
                .Why("Inicio y Galería cargan miniaturas y archivos recientes cada vez que abres el Explorador.", "Home and Gallery load thumbnails and recent files every time you open File Explorer.")
                .Source("Claves de espacio de nombres del Explorador documentadas por la comunidad (winutil)")
                .Win11(22621)
                .Restart(RestartKind.Explorer)
                .DeleteKey(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}")
                .Dword(CU, @"Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", "System.IsPinnedToNameSpaceTree", 0);

            new TweakBuilder(l, "ui.compactMode", Category.Interface, Risk.Safe)
                .Title("Explorador en modo compacto", "Compact File Explorer")
                .Summary("Reduce el espacio entre filas para ver más archivos a la vez.", "Reduces row spacing so you see more files at once.")
                .Why("Windows 11 añade espacio pensado para pantallas táctiles. Con ratón, el modo compacto muestra más información.",
                     "Windows 11 adds spacing meant for touch screens. With a mouse, compact mode shows more information.")
                .Source("Explorador > Ver > Vista compacta")
                .Win11()
                .Dword(CU, Adv, "UseCompactMode", 1);

            new TweakBuilder(l, "ui.autoplay", Category.Interface, Risk.Safe)
                .Title("Desactivar la reproducción automática", "Turn off AutoPlay")
                .Summary("Windows no abre nada por sí solo al conectar un USB o un disco.", "Windows opens nothing on its own when you plug in a USB drive or disc.")
                .Why("Evita que un USB infectado ejecute o sugiera abrir contenido nada más conectarlo.", "Prevents an infected USB drive from running or offering content as soon as you plug it in.")
                .Source("Configuración > Bluetooth y dispositivos > Reproducción automática")
                .Profiles(P.Minimal | P.Office | P.Privacy)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1);

            new TweakBuilder(l, "ui.darkMode", Category.Interface, Risk.Safe)
                .Title("Tema oscuro", "Dark theme")
                .Summary("Windows y las apps compatibles usan colores oscuros.", "Windows and supported apps use dark colours.")
                .Why("Cansa menos la vista con poca luz y ahorra batería en pantallas OLED.", "Easier on the eyes in low light and saves battery on OLED screens.")
                .Source("Configuración > Personalización > Colores")
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0);

            new TweakBuilder(l, "ui.stickyKeys", Category.Gaming, Risk.Safe)
                .Title("No preguntar por las teclas especiales al pulsar Mayús 5 veces", "Do not prompt for Sticky Keys after pressing Shift 5 times")
                .Summary("Evita que salga el aviso de Teclas especiales en mitad de un juego.", "Stops the Sticky Keys prompt appearing in the middle of a game.")
                .Why("El aviso saca al juego a pantalla de escritorio. Las teclas especiales siguen disponibles en Accesibilidad.",
                     "The prompt throws the game back to the desktop. Sticky Keys remain available in Accessibility settings.")
                .Source("Configuración > Accesibilidad > Teclado")
                .Profiles(P.Gaming)
                .Str(CU, @"Control Panel\Accessibility\StickyKeys", "Flags", "506");
        }

        private static void AddTaskbar(List<Tweak> l)
        {
            new TweakBuilder(l, "ui.taskbarLeft", Category.Interface, Risk.Safe)
                .Title("Barra de tareas alineada a la izquierda", "Left-aligned taskbar")
                .Summary("El botón Inicio vuelve a la esquina, como en Windows 10.", "The Start button goes back to the corner, as in Windows 10.")
                .Why("El botón siempre está en el mismo sitio, sin moverse según cuántas apps abras.", "The button is always in the same place, regardless of how many apps are open.")
                .Source("Configuración > Personalización > Barra de tareas")
                .Win11()
                .Dword(CU, Adv, "TaskbarAl", 0);

            new TweakBuilder(l, "ui.taskView", Category.Interface, Risk.Safe)
                .Title("Quitar el botón Vista de tareas", "Remove the Task View button")
                .Summary("Libera espacio en la barra. Win + Tab sigue funcionando.", "Frees taskbar space. Win + Tab still works.")
                .Why("Es un acceso duplicado de un atajo de teclado.", "It duplicates a keyboard shortcut.")
                .Source("Configuración > Personalización > Barra de tareas")
                .Profiles(P.Minimal | P.Gaming)
                .Dword(CU, Adv, "ShowTaskViewButton", 0);

            new TweakBuilder(l, "ui.searchIcon", Category.Interface, Risk.Safe)
                .Title("Búsqueda como icono en la barra", "Search as an icon on the taskbar")
                .Summary("Sustituye el cuadro de búsqueda por un icono pequeño.", "Replaces the search box with a small icon.")
                .Why("El cuadro ocupa mucho espacio y puedes buscar igualmente pulsando Win y escribiendo.", "The box takes a lot of space and you can still search by pressing Win and typing.")
                .Source("Configuración > Personalización > Barra de tareas")
                .Profiles(P.Minimal | P.Gaming)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 1);

            new TweakBuilder(l, "ui.chatIcon", Category.Interface, Risk.Safe)
                .Title("Quitar el icono de Chat (Teams personal)", "Remove the Chat (personal Teams) icon")
                .Summary("Quita el acceso de Teams personal de la barra de tareas.", "Removes the personal Teams shortcut from the taskbar.")
                .Why("Ocupa espacio y preinstala Teams personal aunque no lo uses.", "It takes space and preinstalls personal Teams even if you do not use it.")
                .Source("Configuración > Personalización > Barra de tareas")
                .Win11()
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Privacy)
                .Dword(CU, Adv, "TaskbarMn", 0);

            new TweakBuilder(l, "ui.meetNow", Category.Interface, Risk.Safe)
                .Title("Quitar el icono Reunirse ahora", "Remove the Meet Now icon")
                .Summary("Quita el acceso a Skype de la bandeja del sistema.", "Removes the Skype shortcut from the system tray.")
                .Why("Skype ya no existe y el icono no tiene uso.", "Skype has been retired and the icon has no use.")
                .Source("Microsoft Learn: HideSCAMeetNow policy")
                .Win10Only()
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Privacy)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1);

            new TweakBuilder(l, "ui.endTask", Category.Interface, Risk.Safe)
                .Title("Añadir \"Finalizar tarea\" al clic derecho de la barra", "Add \"End task\" to the taskbar right-click menu")
                .Summary("Cierra una app colgada desde la barra de tareas sin abrir el Administrador de tareas.", "Close a frozen app from the taskbar without opening Task Manager.")
                .Why("Es una opción oficial de Windows 11 oculta en las opciones para desarrolladores.", "It is an official Windows 11 option hidden in developer settings.")
                .Source("Configuración > Sistema > Para programadores > Finalizar tarea")
                .Win11(22631)
                .Profiles(P.Minimal | P.Office | P.Gaming)
                .Dword(CU, Adv + @"\TaskbarDeveloperSettings", "TaskbarEndTask", 1);
        }
    }
}
