using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Optimizzzer.Core.Catalog
{
    /// <summary>
    /// Keyboard and mouse, visual effects, and the Explorer and system preferences added in v1.3.
    /// Each one changes something you can see or feel, says what it does not do, and undoes to the exact previous value.
    /// </summary>
    internal static class InputVisualTweaks
    {
        private const Hive CU = Hive.CurrentUser, LM = Hive.LocalMachine;
        private const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        public static void Add(List<Tweak> l)
        {
            // ---------- Keyboard and mouse ----------

            new TweakBuilder(l, "input.keyRepeat", Category.Input, Risk.Safe)
                .Title("Repetición de teclas más rápida", "Faster key repeat")
                .Summary("Al mantener una tecla, empieza a repetirse antes y más rápido.", "Holding a key starts repeating sooner and faster.")
                .Why("Útil para moverte con las flechas o borrar texto. Cambia la repetición, no el tiempo que tarda en llegar cada pulsación: eso depende del teclado.",
                    "Useful to move with the arrows or delete text. It changes repetition, not how long each keypress takes to arrive: that depends on the keyboard.")
                .Downside("Si escribes despacio, puede salir una letra repetida al dejar una tecla pulsada.", "If you type slowly, a letter may repeat when you hold a key.")
                .Source("Microsoft Learn: SystemParametersInfo SPI_SETKEYBOARDDELAY / SPI_SETKEYBOARDSPEED")
                .Action(new SpiAction("KeyboardDelay", Spi.GetKeyboardDelay, Spi.SetKeyboardDelay, true, 0))
                .Action(new SpiAction("KeyboardSpeed", Spi.GetKeyboardSpeed, Spi.SetKeyboardSpeed, true, 31));

            new TweakBuilder(l, "input.filterToggleKeys", Category.Input, Risk.Safe)
                .Title("Sin avisos de Teclas filtro y Teclas de alternancia", "No Filter Keys or Toggle Keys prompts")
                .Summary("Mantener Mayús derecha 8 s o BloqNum 5 s ya no abre una ventana en mitad de una partida.", "Holding right Shift for 8 s or Num Lock for 5 s no longer opens a window mid-game.")
                .Why("Esas ventanas sacan el foco del juego. Las funciones siguen disponibles en Configuración > Accesibilidad.",
                    "Those windows steal focus from the game. The features stay available in Settings > Accessibility.")
                .Source("Microsoft Learn: FILTERKEYS / TOGGLEKEYS flags (FKF_HOTKEYACTIVE, TKF_HOTKEYACTIVE)")
                .Str(CU, @"Control Panel\Accessibility\Keyboard Response", "Flags", "122")
                .Str(CU, @"Control Panel\Accessibility\ToggleKeys", "Flags", "58");

            new TweakBuilder(l, "input.accessShortcuts", Category.Input, Risk.Safe)
                .Title("Sin atajos accidentales de Narrador y Teclas de mouse", "No accidental Narrator or Mouse Keys shortcuts")
                .Summary("Win + Ctrl + Enter ya no inicia Narrador y Alt + Mayús + BloqNum ya no activa Teclas de mouse.", "Win + Ctrl + Enter no longer starts Narrator and Alt + Shift + Num Lock no longer turns on Mouse Keys.")
                .Why("Se pulsan sin querer jugando o con atajos de otros programas. Se pueden activar desde Configuración > Accesibilidad.",
                    "They get pressed by accident while gaming or with other apps' shortcuts. They can be turned on from Settings > Accessibility.")
                .Downside("Si usas Narrador con ese atajo, tendrás que iniciarlo desde Configuración.", "If you start Narrator with that shortcut, you will need to start it from Settings.")
                .Source("Microsoft Learn: MOUSEKEYS flags (MKF_HOTKEYACTIVE); Narrator keyboard shortcut setting")
                .Str(CU, @"Control Panel\Accessibility\MouseKeys", "Flags", "58")
                .Dword(CU, @"Software\Microsoft\Narrator\NoRoam", "WinEnterLaunchEnabled", 0);

            new TweakBuilder(l, "input.pointerSpeed", Category.Input, Risk.Safe)
                .Title("Velocidad del puntero 1:1", "1:1 pointer speed")
                .Summary("Pone la velocidad del puntero en la marca central (6 de 11), la única que no multiplica ni descarta movimiento.", "Sets pointer speed to the middle mark (6 of 11), the only one that neither multiplies nor drops movement.")
                .Why("Junto con \"Precisión del puntero\" apagada, el ratón mueve exactamente lo que mide su sensor. Ajusta la sensibilidad con los DPI del ratón o dentro del juego.",
                    "With \"Enhance pointer precision\" off, the mouse moves exactly what its sensor measures. Set sensitivity with the mouse's DPI or in the game.")
                .Downside("Si tenías el puntero más rápido o más lento, notarás el cambio en el escritorio.", "If your pointer was faster or slower, you will notice the change on the desktop.")
                .Source("Microsoft Learn: SPI_SETMOUSESPEED (10 = 1:1)")
                .Action(new SpiAction("MouseSpeed", Spi.GetMouseSpeed, Spi.SetMouseSpeed, false, 10));

            new TweakBuilder(l, "input.hoverTime", Category.Input, Risk.Safe)
                .Title("Información al pasar el ratón, sin esperar", "Hover information without waiting")
                .Summary("Las vistas previas e información al pasar el ratón aparecen a los 100 ms en vez de 400 ms.", "Hover previews and information appear after 100 ms instead of 400 ms.")
                .Why("Es una espera fija de Windows; bajarla hace que el escritorio se sienta más rápido.", "It is a fixed Windows delay; lowering it makes the desktop feel faster.")
                .Source("Microsoft Learn: SPI_SETMOUSEHOVERTIME")
                .Action(new SpiAction("MouseHoverTime", Spi.GetMouseHoverTime, Spi.SetMouseHoverTime, true, 100));

            new TweakBuilder(l, "gaming.controllerGameBar", Category.Gaming, Risk.Safe)
                .Title("El botón Xbox del mando no abre la Game Bar", "The controller's Xbox button does not open Game Bar")
                .Summary("Pulsar el botón central del mando ya no abre la Game Bar encima del juego.", "Pressing the controller's centre button no longer opens Game Bar over the game.")
                .Why("Muchos juegos y Steam usan ese botón; la Game Bar se superpone y roba el foco.", "Many games and Steam use that button; Game Bar overlays and steals focus.")
                .Source("Configuración > Juegos > Game Bar: \"Abrir Xbox Game Bar con este botón del mando\"")
                .Dword(CU, @"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0);

            // ---------- Visual effects ----------

            new TweakBuilder(l, "visual.dragContents", Category.Visual, Risk.Safe)
                .Title("No dibujar el contenido al arrastrar ventanas", "Do not draw contents while dragging windows")
                .Summary("Al mover o cambiar el tamaño de una ventana se ve solo su contorno hasta soltarla.", "Moving or resizing a window shows only its outline until you let go.")
                .Why("Redibujar la ventana entera en cada píxel cuesta CPU y GPU; en equipos modestos el arrastre se vuelve fluido.",
                    "Redrawing the whole window at every pixel costs CPU and GPU; on modest PCs dragging becomes smooth.")
                .Downside("No ves el contenido mientras arrastras.", "You do not see the contents while dragging.")
                .Source("Opciones de rendimiento > Efectos visuales: \"Mostrar el contenido de la ventana mientras se arrastra\"")
                .Action(new SpiAction("DragFullWindows", Spi.GetDragFullWindows, Spi.SetDragFullWindows, true, 0))
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3);

            new TweakBuilder(l, "visual.selectionRect", Category.Visual, Risk.Safe)
                .Title("Rectángulo de selección sin transparencia", "Selection rectangle without transparency")
                .Summary("Al seleccionar iconos arrastrando el ratón en el escritorio o una carpeta, el recuadro es un contorno simple.", "Selecting icons by dragging on the desktop or a folder draws a simple outline.")
                .Why("Es un efecto translúcido que se recalcula mientras arrastras.", "It is a translucent effect recalculated while you drag.")
                .Source("Opciones de rendimiento > Efectos visuales: \"Mostrar rectángulo de selección translúcido\"")
                .Restart(RestartKind.Explorer)
                .Dword(CU, Adv, "ListviewAlphaSelect", 0);

            new TweakBuilder(l, "visual.menuFades", Category.Visual, Risk.Safe)
                .Title("Menús e información sin fundido", "Menus and tooltips without fading")
                .Summary("Menús, información sobre herramientas y elementos seleccionados aparecen y desaparecen al instante.", "Menus, tooltips and selected items appear and disappear instantly.")
                .Why("Cada fundido añade una espera visual en cada clic.", "Each fade adds a visual wait to every click.")
                .Source("Opciones de rendimiento > Efectos visuales: fundidos de menús, información y selección")
                .Action(new SpiAction("MenuAnimation", Spi.GetMenuAnimation, Spi.SetMenuAnimation, false, 0))
                .Action(new SpiAction("TooltipAnimation", Spi.GetTooltipAnimation, Spi.SetTooltipAnimation, false, 0))
                .Action(new SpiAction("SelectionFade", Spi.GetSelectionFade, Spi.SetSelectionFade, false, 0));

            new TweakBuilder(l, "visual.shadows", Category.Visual, Risk.Safe)
                .Title("Sin sombras bajo ventanas e iconos", "No shadows under windows and icons")
                .Summary("Quita la sombra de ventanas, menús y del texto de los iconos del escritorio.", "Removes the shadow under windows, menus and desktop icon labels.")
                .Why("Las sombras se dibujan en cada movimiento; quitarlas aligera el escritorio en equipos modestos.", "Shadows are drawn on every move; removing them lightens the desktop on modest PCs.")
                .Downside("Las ventanas se ven más planas y algo menos separadas del fondo.", "Windows look flatter and slightly less separated from the background.")
                .Source("Opciones de rendimiento > Efectos visuales: sombras de ventanas y de etiquetas de iconos")
                .Restart(RestartKind.Explorer)
                .Action(new SpiAction("DropShadow", Spi.GetDropShadow, Spi.SetDropShadow, false, 0))
                .Dword(CU, Adv, "ListviewShadow", 0);

            new TweakBuilder(l, "visual.smoothScroll", Category.Visual, Risk.Safe)
                .Title("Listas sin desplazamiento suave", "Lists without smooth scrolling")
                .Summary("Las listas se desplazan de golpe, sin animación.", "Lists scroll at once, without animation.")
                .Why("Es una animación más en cada desplazamiento; sin ella las listas largas responden antes.", "It is one more animation on every scroll; without it long lists respond sooner.")
                .Source("Opciones de rendimiento > Efectos visuales: \"Suavizar desplazamiento de cuadros de lista\"")
                .Action(new SpiAction("ListBoxSmoothScrolling", Spi.GetListBoxSmoothScrolling, Spi.SetListBoxSmoothScrolling, false, 0));

            new TweakBuilder(l, "visual.controlAnimations", Category.Visual, Risk.Safe)
                .Title("Controles sin animación", "Controls without animation")
                .Summary("Botones, cuadros desplegables y controles de las ventanas responden sin animarse.", "Buttons, drop-down boxes and window controls respond without animating.")
                .Why("Menos animaciones pequeñas que dibujar en cada clic.", "Fewer small animations to draw on every click.")
                .Source("Opciones de rendimiento > Efectos visuales: \"Animar controles y elementos dentro de las ventanas\"")
                .Action(new SpiAction("ClientAreaAnimation", Spi.GetClientAreaAnimation, Spi.SetClientAreaAnimation, false, 0))
                .Action(new SpiAction("ComboBoxAnimation", Spi.GetComboBoxAnimation, Spi.SetComboBoxAnimation, false, 0));

            new TweakBuilder(l, "visual.taskbarThumbs", Category.Visual, Risk.Safe)
                .Title("Miniaturas de la barra de tareas al instante", "Taskbar thumbnails instantly")
                .Summary("Al pasar el ratón por un programa de la barra de tareas, su miniatura aparece sin esperar.", "Hovering a program on the taskbar shows its thumbnail without waiting.")
                .Why("Quita una espera fija de unos 400 ms.", "Removes a fixed wait of about 400 ms.")
                .Source("Registro de Explorer: ExtendedUIHoverTime")
                .Restart(RestartKind.Explorer)
                .Dword(CU, Adv, "ExtendedUIHoverTime", 100);

            // ---------- Explorer and system ----------

            new TweakBuilder(l, "explorer.classicMenu", Category.Interface, Risk.Safe)
                .Title("Menú contextual completo (clásico) en Windows 11", "Full (classic) context menu on Windows 11")
                .Summary("El clic derecho muestra todas las opciones sin pasar por \"Mostrar más opciones\".", "Right-click shows every option without going through \"Show more options\".")
                .Why("Ahorra un clic cada vez y muestra las opciones de 7-Zip, WinRAR y otros programas al instante.", "Saves a click every time and shows 7-Zip, WinRAR and other programs' options right away.")
                .Downside("Se pierde el menú compacto con iconos de Windows 11.", "You lose Windows 11's compact menu with icons.")
                .Os(OsFilter.Win11)
                .Restart(RestartKind.Explorer)
                .Source("Clave CLSID {86ca1aa0-34aa-4e8b-a509-50c905bae2a2} documentada por la comunidad desde Windows 11 21H2")
                .Str(CU, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "");

            new TweakBuilder(l, "explorer.openThisPC", Category.Interface, Risk.Safe)
                .Title("El Explorador abre en Este equipo", "File Explorer opens on This PC")
                .Summary("Al abrir el Explorador ves tus discos en lugar de Inicio o Acceso rápido.", "Opening File Explorer shows your drives instead of Home or Quick access.")
                .Why("Abre más rápido porque no tiene que reunir archivos recientes ni de OneDrive.", "It opens faster because it does not gather recent or OneDrive files.")
                .Source("Opciones de carpeta: \"Abrir el Explorador de archivos en: Este equipo\"")
                .Dword(CU, Adv, "LaunchTo", 1);

            new TweakBuilder(l, "privacy.recentItems", Category.Privacy, Risk.Safe)
                .Title("Sin elementos recientes", "No recent items")
                .Summary("Inicio, las listas de salto y el Explorador dejan de mostrar los archivos que abriste.", "Start, jump lists and File Explorer stop showing the files you opened.")
                .Why("Más privacidad si alguien más usa el equipo, y menos registros en disco.", "More privacy if someone else uses the PC, and less logging to disk.")
                .Downside("Pierdes el acceso rápido a archivos recientes.", "You lose quick access to recent files.")
                .Source("Configuración > Personalización > Inicio: \"Mostrar elementos abiertos recientemente\"")
                .Restart(RestartKind.Explorer)
                .Dword(CU, Adv, "Start_TrackDocs", 0)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowRecent", 0)
                .Dword(CU, @"Software\Microsoft\Windows\CurrentVersion\Explorer", "ShowFrequent", 0);

            new TweakBuilder(l, "ui.clockSeconds", Category.Interface, Risk.Safe)
                .Title("Segundos en el reloj de la barra de tareas", "Seconds on the taskbar clock")
                .Summary("El reloj muestra también los segundos.", "The clock also shows seconds.")
                .Why("Útil para medir tiempos sin abrir nada. Es una preferencia, no mejora el rendimiento.", "Handy to time things without opening anything. It is a preference, not a performance gain.")
                .Os(OsFilter.Win11).MinBuild(22621)
                .Restart(RestartKind.Explorer)
                .Source("Configuración > Personalización > Barra de tareas: \"Mostrar segundos en el reloj\"")
                .Dword(CU, Adv, "ShowSecondsInSystemClock", 1);

            new TweakBuilder(l, "system.verboseStatus", Category.Interface, Risk.Safe)
                .Title("Mensajes detallados al iniciar y apagar", "Detailed messages at startup and shutdown")
                .Summary("En lugar de \"Apagando\", Windows dice qué está esperando (por ejemplo, un servicio concreto).", "Instead of \"Shutting down\", Windows says what it is waiting for (for example, a specific service).")
                .Why("Si el equipo tarda en apagar, ves el culpable.", "If the PC is slow to shut down, you see the culprit.")
                .Source("Microsoft Learn: VerboseStatus")
                .Dword(LM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", 1);

            // ---------- Power and games (power plan settings) ----------

            l.Add(PowerSetting("power.usbSuspend", Category.Power, Risk.Moderate,
                "Sin suspensión selectiva USB", "No USB selective suspend",
                "Windows deja de dormir los puertos USB inactivos con el plan actual.", "Windows stops putting idle USB ports to sleep on the current plan.",
                "Arregla ratones, teclados y auriculares USB que se desconectan o tardan en responder al volver a usarlos.", "Fixes USB mice, keyboards and headsets that disconnect or are slow to respond after a pause.",
                "Algo más de consumo con el equipo en reposo.", "Slightly higher power use while idle.",
                "2a737441-1930-4402-8d77-b2bebba308a3", "48e6b7a6-50f5-4782-a5d4-53bb8f07e226", 0, desktopOnly: true));

            l.Add(PowerSetting("power.pcieAspm", Category.Power, Risk.Moderate,
                "Sin ahorro de energía del enlace PCIe", "No PCIe link power saving",
                "La gráfica y el SSD NVMe no bajan su enlace a modo ahorro con el plan actual.", "The graphics card and NVMe SSD do not drop their link to power-saving with the current plan.",
                "Evita la pequeña espera al despertar el enlace, que algunos notan como microtirones. Solo en sobremesas.", "Avoids the small wake-up delay of the link, which some notice as micro-stutter. Desktops only.",
                "Algo más de consumo en reposo.", "Slightly higher idle power use.",
                "501a4d13-42af-4429-9fd1-a8218c268e20", "ee12f906-d277-404b-b6da-e5fa1a576df5", 0, desktopOnly: true));

            // ---------- Microsoft's own gaming guidance (Advanced, never in a level) ----------

            new TweakBuilder(l, "security.memoryIntegrity", Category.Gaming, Risk.Advanced)
                .Title("Desactivar Integridad de memoria (HVCI)", "Turn off Memory Integrity (HVCI)")
                .Summary("Apaga la virtualización que protege el núcleo de Windows contra controladores maliciosos.", "Turns off the virtualisation that protects the Windows core against malicious drivers.")
                .Why("Microsoft indica que, en Windows 11, desactivarla puede mejorar el rendimiento en juegos. La mejora depende del procesador.",
                    "Microsoft states that on Windows 11 turning it off can improve gaming performance. The gain depends on the processor.")
                .Downside("El equipo queda menos protegido frente a controladores dañinos. Algunos juegos con anticheat (por ejemplo Valorant) piden tenerla activada.",
                    "The PC is less protected against harmful drivers. Some games with anti-cheat (for example Valorant) require it on.")
                .Os(OsFilter.Win11)
                .Restart(RestartKind.Reboot)
                .Source("Microsoft Support: Options to optimize gaming performance in Windows 11")
                .Dword(LM, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0);

            l.Add(new Tweak
            {
                Id = "security.vmPlatform", Category = Category.Gaming, Risk = Risk.Advanced, Os = OsFilter.Win11, Restart = RestartKind.Reboot,
                Title = new Text("Desactivar la Plataforma de máquina virtual", "Turn off Virtual Machine Platform"),
                Summary = new Text("Apaga la capa de virtualización que usan WSL, Android y las máquinas virtuales.", "Turns off the virtualisation layer used by WSL, Android and virtual machines."),
                Why = new Text("Es la otra recomendación de Microsoft para juegos en Windows 11. No reduce la seguridad.", "It is Microsoft's other gaming recommendation on Windows 11. It does not reduce security."),
                Downside = new Text("Dejan de funcionar WSL 2, las apps de Android y las máquinas virtuales de Hyper-V. No se ofrece si tienes distribuciones de WSL.", "WSL 2, Android apps and Hyper-V virtual machines stop working. Not offered if you have WSL distributions."),
                Source = "Microsoft Support: Options to optimize gaming performance in Windows 11",
                Actions =
                {
                    new CustomAction
                    {
                        Description = new Text("dism /online /disable-feature /featurename:VirtualMachinePlatform /norestart", "dism /online /disable-feature /featurename:VirtualMachinePlatform /norestart"),
                        DetectFn = c =>
                        {
                            if (c.Registry.GetSubKeyNames(Hive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Lxss").Count > 0) return ActionState.NotApplicable;
                            var r = c.Process.Run("dism.exe", "/online /get-featureinfo /featurename:VirtualMachinePlatform /english", 120000);
                            if (!r.Ok) return ActionState.NotApplicable;
                            return Regex.IsMatch(r.Output, @"State\s*:\s*Disabled", RegexOptions.IgnoreCase) ? ActionState.Applied : ActionState.NotApplied;
                        },
                        CaptureFn = c => "Enabled",
                        ApplyFn = c => RunOk(c, "dism.exe", "/online /disable-feature /featurename:VirtualMachinePlatform /norestart"),
                        RevertFn = (c, _) => RunOk(c, "dism.exe", "/online /enable-feature /featurename:VirtualMachinePlatform /all /norestart"),
                    },
                },
            });
        }

        /// <summary>DISM returns 3010 when it worked and needs a restart.</summary>
        private static void RunOk(SystemContext c, string file, string args)
        {
            var r = c.Process.Run(file, args, 600000);
            if (r.ExitCode != 0 && r.ExitCode != 3010) throw new InvalidOperationException(file + " " + args + ": " + (r.Output + r.Error).Trim());
        }

        private static readonly Regex Hex = new Regex(@"0x([0-9a-fA-F]{8})");

        /// <summary>A setting of the active power plan (AC). The previous value is read back with powercfg /query.</summary>
        private static Tweak PowerSetting(string id, Category cat, Risk risk, string titleEs, string titleEn, string sumEs, string sumEn,
            string whyEs, string whyEn, string downEs, string downEn, string subgroup, string setting, int value, bool desktopOnly)
        {
            int? Current(SystemContext c)
            {
                var r = c.Process.Run("powercfg", "/query SCHEME_CURRENT " + subgroup + " " + setting);
                if (!r.Ok) return null;
                // The last two hex numbers are the current AC and DC values, whatever the language of Windows.
                var m = Hex.Matches(r.Output).Cast<Match>().ToList();
                return m.Count >= 2 ? Convert.ToInt32(m[m.Count - 2].Groups[1].Value, 16) : (int?)null;
            }
            void Set(SystemContext c, int v)
            {
                var r = c.Process.Run("powercfg", "/setacvalueindex SCHEME_CURRENT " + subgroup + " " + setting + " " + v);
                if (!r.Ok) throw new InvalidOperationException("powercfg: " + (r.Output + r.Error).Trim());
                c.Process.Run("powercfg", "/setactive SCHEME_CURRENT");
            }
            return new Tweak
            {
                Id = id, Category = cat, Risk = risk,
                Title = new Text(titleEs, titleEn), Summary = new Text(sumEs, sumEn), Why = new Text(whyEs, whyEn), Downside = new Text(downEs, downEn),
                Source = "Microsoft Learn: powercfg /setacvalueindex (" + setting + ")",
                HardwareFilter = desktopOnly ? (Func<ISystemInfo, bool>)(i => !i.IsLaptop) : null,
                Actions =
                {
                    new CustomAction
                    {
                        Description = new Text("powercfg /setacvalueindex SCHEME_CURRENT " + subgroup + " " + setting + " " + value, "powercfg /setacvalueindex SCHEME_CURRENT " + subgroup + " " + setting + " " + value),
                        DetectFn = c => { var v = Current(c); return v == null ? ActionState.NotApplicable : v == value ? ActionState.Applied : ActionState.NotApplied; },
                        CaptureFn = c => Current(c)?.ToString(),
                        ApplyFn = c => Set(c, value),
                        RevertFn = (c, old) => { if (int.TryParse(old, out var v)) Set(c, v); },
                    },
                },
            };
        }
    }
}
