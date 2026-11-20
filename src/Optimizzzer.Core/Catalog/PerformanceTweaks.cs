using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static Optimizzzer.Core.Catalog.TweakBuilder;
using P = Optimizzzer.Core.ProfileKind;

namespace Optimizzzer.Core.Catalog
{
    internal static class PerformanceTweaks
    {
        private const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        /// <summary>Fixed id for the Ultimate Performance copy, so detection and undo always find it.</summary>
        public const string UltimatePlanGuid = "b6a1d7e2-3c4f-4a5b-9e8d-7f6a5b4c3d2e";
        private const string UltimateTemplate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        // Fallback for PCs where the hidden Ultimate scheme is unavailable (for example Modern Standby).
        private const string HighPerformanceTemplate = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private static readonly Regex GuidRx = new Regex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");

        public static void Add(List<Tweak> l)
        {
            AddPerformance(l);
            AddGaming(l);
            AddPower(l);
        }

        private static void AddPerformance(List<Tweak> l)
        {
            new TweakBuilder(l, "perf.animations", Category.Performance, Risk.Safe)
                .Title("Desactivar animaciones de ventanas", "Turn off window animations")
                .Summary("Las ventanas se abren, minimizan y maximizan al instante.", "Windows open, minimise and maximise instantly.")
                .Why("Cada animación añade unos 200 ms de espera visual. Sin ellas el sistema se siente más rápido, sobre todo en equipos modestos.",
                     "Each animation adds about 200 ms of visual wait. Without them the system feels faster, especially on modest PCs.")
                .Downside("Las transiciones se ven más bruscas.", "Transitions look more abrupt.")
                .Source("Configuración > Accesibilidad > Efectos visuales")
                .Profiles(P.Gaming | P.Office)
                .Restart(RestartKind.SignOut)
                .Str(CU, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0")
                .Dword(CU, Adv, "TaskbarAnimations", 0)
                .Dword(CU, @"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0);

            new TweakBuilder(l, "perf.menuDelay", Category.Performance, Risk.Safe)
                .Title("Menús más rápidos", "Faster menus")
                .Summary("Los submenús se abren a los 100 ms en lugar de 400 ms.", "Submenus open after 100 ms instead of 400 ms.")
                .Why("Windows espera 400 ms antes de abrir un submenú al pasar el ratón. Reducirlo hace que la navegación se sienta inmediata.",
                     "Windows waits 400 ms before opening a submenu on hover. Lowering it makes navigation feel immediate.")
                .Source("Valor MenuShowDelay de Windows (predeterminado 400)")
                .Profiles(P.Minimal | P.Office | P.Gaming)
                .Restart(RestartKind.SignOut)
                .Str(CU, @"Control Panel\Desktop", "MenuShowDelay", "100");

            new TweakBuilder(l, "perf.transparency", Category.Performance, Risk.Safe)
                .Title("Desactivar transparencias", "Turn off transparency effects")
                .Summary("Inicio, la barra y las ventanas usan colores sólidos.", "Start, the taskbar and windows use solid colours.")
                .Why("El desenfoque de transparencia se calcula constantemente en la GPU. En gráficas integradas y portátiles se nota en fluidez y batería.",
                     "The transparency blur is constantly computed on the GPU. On integrated graphics and laptops it shows in smoothness and battery.")
                .Downside("Pierdes el efecto translúcido.", "You lose the translucent look.")
                .Source("Configuración > Personalización > Colores")
                .Profiles(P.Laptop)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);

            new TweakBuilder(l, "perf.startupDelay", Category.Performance, Risk.Safe)
                .Title("Quitar el retraso de las apps de inicio", "Remove the startup apps delay")
                .Summary("Las apps de inicio arrancan nada más entrar en vez de esperar unos segundos.", "Startup apps launch right after sign-in instead of waiting a few seconds.")
                .Why("Windows retrasa a propósito las apps de inicio. En un SSD ese retraso ya no es necesario.", "Windows deliberately delays startup apps. On an SSD that delay is no longer needed.")
                .Downside("En discos duros (HDD) el arranque puede sentirse más lento al competir todo a la vez.", "On hard drives (HDD) sign-in may feel slower as everything competes at once.")
                .Source("Valor StartupDelayInMSec del Explorador")
                .Hardware(i => i.SystemDriveIsSsd != false)
                .Profiles(P.Gaming | P.Office)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0);

            new TweakBuilder(l, "perf.backgroundApps", Category.Performance, Risk.Moderate)
                .Title("Impedir que las apps de la Store funcionen en segundo plano", "Stop Store apps running in the background")
                .Summary("Las apps de la Microsoft Store solo funcionan mientras las tienes abiertas.", "Microsoft Store apps only run while you have them open.")
                .Why("Muchas apps de la Store se quedan activas para actualizar mosaicos y notificaciones, usando memoria y batería.",
                     "Many Store apps stay active to update tiles and notifications, using memory and battery.")
                .Downside("Las apps de la Store (Correo, Teléfono, WhatsApp de la Store) no te avisarán si están cerradas.", "Store apps (Mail, Phone Link, Store WhatsApp) will not notify you while closed.")
                .Source("Microsoft Learn: Policy CSP Privacy/LetAppsRunInBackground")
                .Profiles(P.Laptop | P.Gaming)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2);

            new TweakBuilder(l, "perf.edgeBackground", Category.Performance, Risk.Safe)
                .Title("Impedir que Edge se quede abierto en segundo plano", "Stop Edge running in the background")
                .Summary("Edge deja de precargarse al iniciar Windows y se cierra del todo al cerrarlo.", "Edge stops preloading at startup and fully closes when you close it.")
                .Why("El arranque rápido de Edge mantiene procesos en memoria aunque uses otro navegador.", "Edge Startup Boost keeps processes in memory even if you use another browser.")
                .Downside("Edge tarda un poco más en abrirse la primera vez.", "Edge takes slightly longer to open the first time.")
                .Source("Microsoft Learn: Edge policies StartupBoostEnabled, BackgroundModeEnabled")
                .Profiles(P.Minimal | P.Office | P.Gaming | P.Laptop)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0);

            new TweakBuilder(l, "perf.storageSense", Category.Performance, Risk.Safe)
                .Title("Activar el Sensor de almacenamiento", "Turn on Storage Sense")
                .Summary("Windows borra automáticamente archivos temporales que ya no se usan.", "Windows automatically deletes temporary files it no longer needs.")
                .Why("Mantiene el disco con espacio libre sin que tengas que acordarte de limpiarlo.", "Keeps free space on your drive without you having to remember to clean it.")
                .Source("Configuración > Sistema > Almacenamiento")
                .Profiles(P.Minimal | P.Office | P.Laptop)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", 1);

            new TweakBuilder(l, "perf.lastAccess", Category.Performance, Risk.Safe)
                .Title("No actualizar la fecha de último acceso de los archivos", "Stop updating file last-access times")
                .Summary("NTFS deja de escribir en disco cada vez que solo lees un archivo.", "NTFS stops writing to disk every time you just read a file.")
                .Why("Cada lectura genera una escritura extra. Desactivarlo reduce escrituras, útil al compilar o manejar muchos archivos pequeños.",
                     "Every read causes an extra write. Turning it off reduces writes, useful when building code or handling many small files.")
                .Downside("Algunas herramientas de copia de seguridad que usan esa fecha pueden verse afectadas.", "Some backup tools that rely on that date may be affected.")
                .Source("Microsoft Learn: fsutil behavior disablelastaccess")
                .Restart(RestartKind.Reboot)
                .Dword(LM, @"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate", unchecked((int)0x80000001));

            new TweakBuilder(l, "perf.searchIndex", Category.Performance, Risk.Moderate)
                .Title("Desactivar el indexador de búsqueda", "Turn off the search indexer")
                .Summary("Windows deja de indexar tus archivos en segundo plano.", "Windows stops indexing your files in the background.")
                .Why("En discos duros (HDD) y equipos con poca RAM, el indexador causa picos de disco al arrancar y tras copiar muchos archivos.",
                     "On hard drives (HDD) and low-RAM PCs, the indexer causes disk spikes at startup and after copying many files.")
                .Downside("La búsqueda de archivos en Inicio y en Outlook será mucho más lenta.", "File search in Start and in Outlook becomes much slower.")
                .Source("Microsoft Learn: Windows Search service")
                .Service("WSearch", StartMode.Disabled);
        }

        private static void AddGaming(List<Tweak> l)
        {
            new TweakBuilder(l, "gaming.gameMode", Category.Gaming, Risk.Safe)
                .Title("Activar el Modo juego", "Turn on Game Mode")
                .Summary("Windows da prioridad al juego y evita instalar actualizaciones mientras juegas.", "Windows prioritises the game and avoids installing updates while you play.")
                .Why("Está activado de fábrica, pero algunos optimizadores lo desactivan. Microsoft recomienda dejarlo activo.",
                     "It is on by default, but some optimizers turn it off. Microsoft recommends keeping it on.")
                .Source("Microsoft Support: Use Game Mode while gaming on your Windows device")
                .Profiles(P.Gaming)
                .Dword(CU, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
                .Dword(CU, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);

            new TweakBuilder(l, "gaming.gameDvr", Category.Gaming, Risk.Safe)
                .Title("Desactivar la grabación en segundo plano (Game DVR)", "Turn off background recording (Game DVR)")
                .Summary("Windows deja de grabar continuamente los últimos minutos de juego.", "Windows stops continuously recording the last minutes of gameplay.")
                .Why("Cuando está activa, la grabación codifica vídeo todo el tiempo y resta FPS, sobre todo en GPUs de gama media y baja.",
                     "When on, recording encodes video all the time and costs FPS, especially on mid and low-end GPUs.")
                .Downside("No podrás usar Win + Alt + G para grabar lo que acaba de pasar.", "You cannot use Win + Alt + G to record what just happened.")
                .Source("Microsoft Learn: Policy CSP ApplicationManagement/AllowGameDVR")
                .Profiles(P.Gaming | P.Laptop)
                .Dword(CU, @"System\GameConfigStore", "GameDVR_Enabled", 0)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0)
                .Dword(LM, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);

            new TweakBuilder(l, "gaming.gameBarTips", Category.Gaming, Risk.Safe)
                .Title("Quitar el aviso de la Game Bar al abrir juegos", "Remove the Game Bar prompt when launching games")
                .Summary("Deja de salir el mensaje \"Pulsa Win + G\" al iniciar un juego.", "Stops the \"Press Win + G\" message when a game starts.")
                .Why("Es un aviso que aparece encima del juego y no aporta nada si ya conoces el atajo.", "It is a prompt that appears over the game and adds nothing if you know the shortcut.")
                .Source("Configuración > Juegos > Xbox Game Bar")
                .Profiles(P.Gaming)
                .Dword(CU, @"Software\Microsoft\GameBar", "ShowStartupPanel", 0);

            new TweakBuilder(l, "gaming.mouseAccel", Category.Gaming, Risk.Safe)
                .Title("Desactivar la aceleración del ratón", "Turn off mouse acceleration")
                .Summary("El puntero se mueve siempre la misma distancia por cada centímetro de ratón.", "The pointer always moves the same distance per centimetre of mouse movement.")
                .Why("Con \"Mejorar precisión del puntero\" activo, la distancia depende de la velocidad del gesto, lo que hace la puntería inconsistente.",
                     "With \"Enhance pointer precision\" on, distance depends on how fast you move, which makes aiming inconsistent.")
                .Downside("En escritorio, quizá necesites subir la sensibilidad para cruzar la pantalla.", "On the desktop you may need a higher sensitivity to cross the screen.")
                .Source("Panel de control > Mouse > Opciones de puntero")
                .Profiles(P.Gaming)
                .Restart(RestartKind.SignOut)
                .Str(CU, @"Control Panel\Mouse", "MouseSpeed", "0")
                .Str(CU, @"Control Panel\Mouse", "MouseThreshold1", "0")
                .Str(CU, @"Control Panel\Mouse", "MouseThreshold2", "0");

            new TweakBuilder(l, "gaming.windowedOptimizations", Category.Gaming, Risk.Safe)
                .Title("Optimizaciones para juegos en ventana", "Optimizations for windowed games")
                .Summary("Los juegos DirectX 10 y 11 en ventana o sin bordes tienen menos latencia.", "DirectX 10 and 11 games in windowed or borderless mode get lower latency.")
                .Why("Activa el modelo de presentación moderno (flip) en juegos antiguos, que reduce la latencia y permite VRR y Auto HDR en ventana.",
                     "Enables the modern flip presentation model in older games, lowering latency and allowing VRR and Auto HDR in windowed mode.")
                .Source("Configuración > Pantalla > Gráficos > Optimizaciones para juegos en ventana")
                .Win11()
                .Profiles(P.Gaming)
                .Action(new DirectXSettingAction("SwapEffectUpgradeEnable", "1"));

            new TweakBuilder(l, "gaming.hags", Category.Gaming, Risk.Moderate)
                .Title("Programación de GPU acelerada por hardware", "Hardware-accelerated GPU scheduling")
                .Summary("La tarjeta gráfica gestiona su propia memoria de vídeo.", "The graphics card manages its own video memory.")
                .Why("Reduce algo la latencia y es necesario para la generación de fotogramas DLSS 3 en NVIDIA. El efecto en FPS varía según el juego.",
                     "Slightly lowers latency and is required for DLSS 3 frame generation on NVIDIA. The FPS effect varies by game.")
                .Downside("En algunos equipos provoca tirones o problemas en grabación. Si pasa, deshazlo.", "On some PCs it causes stutter or recording issues. If so, undo it.")
                .Source("Configuración > Pantalla > Gráficos > Cambiar configuración de gráficos predeterminada")
                .MinBuild(19041)
                .Hardware(i => i.HasNvidia || i.HasAmdGpu)
                .Restart(RestartKind.Reboot)
                .Dword(LM, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);

            new TweakBuilder(l, "gaming.mpoFix", Category.Gaming, Risk.Advanced)
                .Title("Desactivar la superposición multiplano (MPO)", "Turn off multiplane overlay (MPO)")
                .Summary("Solución para parpadeos y pantallas negras con algunas GPU NVIDIA.", "Fix for flicker and black screens with some NVIDIA GPUs.")
                .Why("NVIDIA recomienda desactivar MPO si ves parpadeos en el navegador o en juegos con varias pantallas. Solo aplícalo si tienes ese problema.",
                     "NVIDIA recommends turning off MPO if you see flicker in the browser or in games with multiple monitors. Only apply it if you have that problem.")
                .Downside("Si no tienes el problema, puede aumentar un poco el consumo de la GPU.", "If you do not have the problem, it can slightly raise GPU power use.")
                .Source("NVIDIA Support: After updating to NVIDIA Game Ready Driver 461.09, some desktop apps may flicker")
                .Restart(RestartKind.Reboot)
                .Dword(LM, @"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5);
        }

        private static void AddPower(List<Tweak> l)
        {
            new TweakBuilder(l, "power.ultimate", Category.Power, Risk.Moderate)
                .Title("Plan de energía Máximo rendimiento", "Ultimate Performance power plan")
                .Summary("Crea y activa el plan de energía oculto de Microsoft para equipos de alto rendimiento (o Alto rendimiento si tu equipo no lo admite).", "Creates and activates Microsoft's hidden power plan for high-performance PCs (or High performance if your PC does not support it).")
                .Why("Evita que el procesador y los dispositivos entren en estados de ahorro, reduciendo pequeñas latencias.", "Keeps the processor and devices out of power-saving states, removing small latencies.")
                .Downside("Más consumo eléctrico y temperatura en reposo. No recomendado en portátiles.", "Higher power use and idle temperature. Not recommended on laptops.")
                .Source("Microsoft Learn: powercfg, Ultimate Performance scheme")
                .Hardware(i => !i.IsLaptop)
                .Profiles(P.Gaming)
                .Action(new CustomAction
                {
                    Description = new Text("powercfg: duplicar el plan " + UltimateTemplate + " como " + UltimatePlanGuid + " y activarlo",
                                           "powercfg: duplicate scheme " + UltimateTemplate + " as " + UltimatePlanGuid + " and activate it"),
                    DetectFn = c => ActiveScheme(c).Equals(UltimatePlanGuid, StringComparison.OrdinalIgnoreCase) ? ActionState.Applied : ActionState.NotApplied,
                    CaptureFn = ActiveScheme,
                    ApplyFn = c =>
                    {
                        var list = c.Process.Run("powercfg", "/list").Output;
                        if (list.IndexOf(UltimatePlanGuid, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            var r = c.Process.Run("powercfg", "-duplicatescheme " + UltimateTemplate + " " + UltimatePlanGuid);
                            if (!r.Ok) r = c.Process.Run("powercfg", "-duplicatescheme " + HighPerformanceTemplate + " " + UltimatePlanGuid);
                            if (!r.Ok) throw new InvalidOperationException("powercfg -duplicatescheme: " + r.Output + r.Error);
                        }
                        var s = c.Process.Run("powercfg", "/setactive " + UltimatePlanGuid);
                        if (!s.Ok) throw new InvalidOperationException("powercfg /setactive: " + s.Output + s.Error);
                    },
                    RevertFn = (c, previous) =>
                    {
                        if (!string.IsNullOrEmpty(previous)) c.Process.Run("powercfg", "/setactive " + previous);
                        c.Process.Run("powercfg", "/delete " + UltimatePlanGuid);
                    },
                });

            new TweakBuilder(l, "power.hibernate", Category.Power, Risk.Moderate)
                .Title("Desactivar la hibernación", "Turn off hibernation")
                .Summary("Borra el archivo hiberfil.sys y libera tanto espacio como RAM tengas.", "Deletes hiberfil.sys and frees as much disk space as you have RAM.")
                .Why("En un sobremesa que no hibernas, hiberfil.sys ocupa entre 6 y 32 GB sin ningún uso.", "On a desktop you never hibernate, hiberfil.sys takes 6 to 32 GB for nothing.")
                .Downside("Se pierde la hibernación y el inicio rápido. En portátiles no se recomienda.", "You lose hibernation and Fast Startup. Not recommended on laptops.")
                .Source("Microsoft Learn: powercfg /hibernate")
                .Hardware(i => !i.IsLaptop)
                .Profiles(P.Gaming)
                .Action(new CustomAction
                {
                    Description = new Text("powercfg /hibernate off", "powercfg /hibernate off"),
                    DetectFn = c =>
                    {
                        var v = c.Registry.GetValue(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled");
                        return v != null && Convert.ToUInt64(v.Data) == 0 ? ActionState.Applied : ActionState.NotApplied;
                    },
                    CaptureFn = c => "on",
                    ApplyFn = c => RunOrThrow(c, "powercfg", "/hibernate off"),
                    RevertFn = (c, _) => c.Process.Run("powercfg", "/hibernate on"),
                });

            new TweakBuilder(l, "power.fastStartup", Category.Power, Risk.Safe)
                .Title("Desactivar el inicio rápido", "Turn off Fast Startup")
                .Summary("\"Apagar\" apaga el equipo de verdad en lugar de hibernar el sistema.", "\"Shut down\" really shuts down instead of hibernating the system.")
                .Why("El inicio rápido conserva el estado del sistema entre apagados, lo que arrastra errores de controladores y bloquea discos para el arranque dual.",
                     "Fast Startup keeps system state between shutdowns, which carries over driver errors and locks drives for dual boot.")
                .Downside("El arranque desde apagado tarda unos segundos más.", "Booting from shutdown takes a few seconds longer.")
                .Source("Panel de control > Opciones de energía > Elegir el comportamiento de los botones")
                .Restart(RestartKind.Reboot)
                .Dword(LM, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0);

            new TweakBuilder(l, "power.throttling", Category.Power, Risk.Moderate)
                .Title("Desactivar la limitación de energía de procesos", "Turn off power throttling")
                .Summary("Windows deja de ralentizar los programas en segundo plano para ahorrar energía.", "Windows stops slowing background programs to save power.")
                .Why("En sobremesa, la limitación puede ralentizar tareas largas (renderizado, compresión) cuando la ventana no está en primer plano.",
                     "On desktops, throttling can slow long tasks (rendering, compression) when their window is not in front.")
                .Downside("Más consumo en reposo. No recomendado en portátiles.", "Higher idle power use. Not recommended on laptops.")
                .Source("Microsoft Learn: Power throttling")
                .Hardware(i => !i.IsLaptop)
                .Restart(RestartKind.Reboot)
                .Dword(LM, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1);
        }

        private static void RunOrThrow(SystemContext c, string file, string args)
        {
            var r = c.Process.Run(file, args);
            if (!r.Ok) throw new InvalidOperationException(file + " " + args + ": " + (r.Error.Length > 0 ? r.Error : r.Output));
        }

        internal static string ActiveScheme(SystemContext c)
        {
            var m = GuidRx.Match(c.Process.Run("powercfg", "/getactivescheme").Output);
            return m.Success ? m.Value : "";
        }
    }

    /// <summary>Edits one key in the DirectXUserGlobalSettings string without touching the others (Auto HDR, VRR...).</summary>
    public sealed class DirectXSettingAction : TweakAction
    {
        private const string Path = @"Software\Microsoft\DirectX\UserGpuPreferences";
        private const string Name = "DirectXUserGlobalSettings";

        public DirectXSettingAction(string key, string value) { Key = key; Value = value; }
        public string Key { get; }
        public string Value { get; }

        private static Dictionary<string, string> Parse(string s) =>
            (s ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split(new[] { '=' }, 2))
                .Where(p => p.Length == 2)
                .GroupBy(p => p[0].Trim())
                .ToDictionary(g => g.Key, g => g.Last()[1].Trim());

        private string Current(SystemContext c) => c.Registry.GetValue(Hive.CurrentUser, Path, Name)?.Data as string;

        public override ActionState Detect(SystemContext ctx) =>
            Parse(Current(ctx)).TryGetValue(Key, out var v) && v == Value ? ActionState.Applied : ActionState.NotApplied;

        public override ActionSnapshot Snapshot(SystemContext ctx)
        {
            var cur = Current(ctx);
            Parse(cur).TryGetValue(Key, out var old);
            return new ActionSnapshot { Kind = "dx", Existed = old != null, Data = old };
        }

        public override void Apply(SystemContext ctx) => Write(ctx, Value);

        public override void Revert(SystemContext ctx, ActionSnapshot s) => Write(ctx, s.Existed ? s.Data : null);

        private void Write(SystemContext ctx, string value)
        {
            var d = Parse(Current(ctx));
            if (value == null) d.Remove(Key); else d[Key] = value;
            var text = string.Concat(d.Select(kv => kv.Key + "=" + kv.Value + ";"));
            if (text.Length == 0) ctx.Registry.DeleteValue(Hive.CurrentUser, Path, Name);
            else ctx.Registry.SetValue(Hive.CurrentUser, Path, Name, RegValue.Str(text));
        }

        public override string Describe(Lang lang) => @"HKCU\" + Path + @"\" + Name + ": " + Key + "=" + Value;
    }
}
