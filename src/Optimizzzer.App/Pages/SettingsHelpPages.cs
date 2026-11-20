using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;

namespace Optimizzzer.App.Pages
{
    public sealed class SettingsPage : AppPage
    {
        public SettingsPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "settings";
        public override string Title => Loc.S("Ajustes", "Settings");
        public override string Icon => Icons.Settings;

        private static FrameworkElement Segmented<T>(string name, (T Value, string Label)[] options, T current, Action<T> onPick)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            AutomationProperties.SetName(row, name);
            var group = name + Guid.NewGuid().ToString("N");
            foreach (var (value, label) in options)
            {
                var rb = new RadioButton { Content = label, GroupName = group, IsChecked = Equals(value, current), MinWidth = 96 };
                rb.SetResourceReference(FrameworkElement.StyleProperty, "Segment");
                var v = value;
                rb.Checked += (_, __) => onPick(v);
                row.Children.Add(rb);
            }
            return row;
        }

        private static FrameworkElement Setting(string title, string help, FrameworkElement control)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var info = Ui.Stack(2, Ui.Text(title, 14, true), string.IsNullOrEmpty(help) ? null : Ui.Muted(help, 12));
            info.Margin = new Thickness(0, 0, 24, 0);
            g.Children.Add(info);
            control.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(control, 1);
            g.Children.Add(control);
            var b = new Border { Child = g, Padding = new Thickness(0, 16, 0, 16), BorderThickness = new Thickness(0, 0, 0, 1) };
            b.SetResourceReference(Border.BorderBrushProperty, "Divider");
            return b;
        }

        protected override FrameworkElement Build()
        {
            void Changed() { S.SaveSettings(); W.RebuildAll(); }

            var theme = Segmented(Loc.S("Tema", "Theme"), new[]
            {
                (ThemeChoice.System, Loc.S("Sistema", "System")), (ThemeChoice.Light, Loc.S("Claro", "Light")), (ThemeChoice.Dark, Loc.S("Oscuro", "Dark")),
            }, Theme.Choice, v => { Theme.Apply(v); Changed(); });

            var swatches = new StackPanel { Orientation = Orientation.Horizontal };
            AutomationProperties.SetName(swatches, Loc.S("Color de acento", "Accent colour"));
            foreach (var (a, label) in new[]
            {
                (AccentChoice.System, Loc.S("El de Windows", "Windows accent")), (AccentChoice.Blue, Loc.S("Azul", "Blue")),
                (AccentChoice.Green, Loc.S("Verde", "Green")), (AccentChoice.Violet, Loc.S("Violeta", "Violet")), (AccentChoice.Orange, Loc.S("Naranja", "Orange")),
            })
            {
                var rb = new RadioButton { GroupName = "accent", IsChecked = Theme.Accent == a, Background = Theme.Brush(Theme.PreviewOf(a)), ToolTip = label, Margin = new Thickness(0, 0, 4, 0) };
                rb.SetResourceReference(FrameworkElement.StyleProperty, "Swatch");
                AutomationProperties.SetName(rb, label);
                var v = a;
                rb.Checked += (_, __) => { Theme.SetAccent(v); Changed(); };
                swatches.Children.Add(rb);
            }

            var lang = Segmented(Loc.S("Idioma", "Language"), new[] { (Lang.Es, "Español"), (Lang.En, "English") }, Loc.Lang, v => { Loc.Lang = v; Changed(); });

            var lite = Segmented(Loc.S("Modo ligero", "Lite mode"), new[]
            {
                (LiteChoice.Auto, Loc.S("Automático", "Automatic")), (LiteChoice.On, Loc.S("Siempre", "Always")), (LiteChoice.Off, Loc.S("Nunca", "Never")),
            }, Theme.LiteSetting, v => { Theme.SetLite(v); Changed(); });
            var liteState = Theme.Lite
                ? Loc.S("Activo ahora", "On now") + (Theme.LiteReason.Length > 0 ? Loc.S(" porque ", " because ") + Theme.LiteReason : "") + "."
                : Loc.S("Desactivado ahora: este equipo puede con las animaciones y las sombras.", "Off now: this PC handles animations and shadows fine.");

            var tour = Ui.Secondary(Loc.S("Ver el recorrido", "Show the tour"), W.ShowTour, Icons.Play);
            var data = Ui.Secondary(Loc.S("Abrir carpeta", "Open folder"), () =>
            {
                try { System.IO.Directory.CreateDirectory(S.Win.DataDir); System.Diagnostics.Process.Start("explorer.exe", "\"" + S.Win.DataDir + "\""); }
                catch (Exception ex) { W.ShowError(ex.Message); }
            }, Icons.Folder);

            var version = typeof(SettingsPage).Assembly.GetName().Version;
            var body = Ui.Stack(0,
                Setting(Loc.S("Tema", "Theme"), Loc.S("\"Sistema\" sigue el modo claro u oscuro de Windows.", "\"System\" follows Windows light or dark mode."), theme),
                Setting(Loc.S("Color de acento", "Accent colour"), Loc.S("Se ajusta solo para que el texto siempre se lea bien.", "Adjusted automatically so text always stays readable."), swatches),
                Setting(Loc.S("Idioma", "Language"), null, lang),
                Setting(Loc.S("Modo ligero", "Lite mode"), Loc.S("Quita animaciones y sombras para que vaya fluido en equipos modestos. ", "Removes animations and shadows so it stays smooth on modest PCs. ") + liteState, lite),
                Setting(Loc.S("Recorrido de bienvenida", "Welcome tour"), Loc.S("Tres pasos que explican cómo funciona.", "Three steps that explain how it works."), tour),
                Setting(Loc.S("Datos de Optimizzzer", "Optimizzzer data"), Loc.S("Historial para deshacer, mediciones y estos ajustes, en ", "Undo history, measurements and these settings, in ") + S.Win.DataDir, data),
                Ui.Muted("Optimizzzer " + version.Major + "." + version.Minor + "." + version.Build + " · MIT", 12));
            return Frame(null, body);
        }
    }

    public sealed class HelpPage : AppPage
    {
        public HelpPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "help";
        public override string Title => Loc.S("Ayuda", "Help");
        public override string Icon => Icons.Help;

        protected override FrameworkElement Build()
        {
            FrameworkElement Section(string icon, string title, params UIElement[] content)
            {
                var head = Ui.Row(10, Ui.Icon(icon, 18, "AccentText"), Ui.H2(title));
                var s = Ui.Stack(10, new UIElement[] { head }.Concat(content).ToArray());
                s.Margin = new Thickness(0, 0, 0, 28);
                return s;
            }

            FrameworkElement Term(string term, string meaning)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                var t = Ui.Text(term, 14, true);
                var m = Ui.Text(meaning);
                Grid.SetColumn(m, 1);
                g.Children.Add(t); g.Children.Add(m);
                g.Margin = new Thickness(0, 0, 0, 6);
                return g;
            }

            var how = Section(Icons.Wizard, Loc.S("Cómo funciona", "How it works"),
                Ui.Text(Loc.S("1. Optimizzzer analiza el equipo sin cambiar nada.", "1. Optimizzzer scans the PC without changing anything.")),
                Ui.Text(Loc.S("2. Eliges un nivel en Inicio, o cada ajuste en Personalizar. Siempre ves qué va a cambiar y qué pierdes antes de confirmar.", "2. You pick a level on Home, or each tweak in Customize. You always see what will change and what you lose before confirming.")),
                Ui.Text(Loc.S("3. Se crea un punto de restauración, se guarda el valor anterior de cada cambio y se aplica. Si algo falla, ese ajuste se revierte solo.", "3. A restore point is created, the previous value of every change is saved, and it is applied. If something fails, that tweak is rolled back.")),
                Ui.Text(Loc.S("4. Desde Historial deshaces un ajuste o una sesión entera cuando quieras.", "4. From History you undo one tweak or a whole session whenever you want.")));

            var glossary = Section(Icons.Glossary, Loc.S("Glosario", "Glossary"),
                Term(Loc.S("Seguro", "Safe"), Loc.S("Sin efectos secundarios conocidos.", "No known side effects.")),
                Term(Loc.S("Moderado", "Moderate"), Loc.S("Quita alguna función a cambio de rendimiento o privacidad. Lee \"Qué pierdes\".", "Removes some feature in exchange for performance or privacy. Read \"What you lose\".")),
                Term(Loc.S("Avanzado", "Advanced"), Loc.S("Solo para un problema concreto, como parpadeos de pantalla.", "Only for a specific problem, such as screen flicker.")),
                Term(Loc.S("Impacto", "Impact"), Loc.S("Cuánto se suele notar el cambio. Es una estimación, no una medición: la medición real está en Mediciones.", "How noticeable the change usually is. It is an estimate, not a measurement: the real measurement is in Measurements.")),
                Term(Loc.S("Punto de restauración", "Restore point"), Loc.S("Copia de la configuración de Windows para volver atrás desde \"Restaurar sistema\", incluso si Optimizzzer no abre.", "A copy of Windows settings you can return to from System Restore, even if Optimizzzer does not open.")),
                Term(Loc.S("Deshacer", "Undo"), Loc.S("Devuelve el valor exacto que había antes. Las desinstalaciones de apps no se deshacen: se reinstalan desde la Microsoft Store.", "Puts back the exact previous value. App uninstalls are not undone: you reinstall them from the Microsoft Store.")),
                Term(Loc.S("Cambio revertido", "Reverted change"), Loc.S("Un ajuste que aplicaste y que una actualización de Windows volvió a cambiar.", "A tweak you applied that a Windows update changed back.")),
                Term(Loc.S("Telemetría", "Telemetry"), Loc.S("Datos de uso y diagnóstico que Windows envía a Microsoft.", "Usage and diagnostic data Windows sends to Microsoft.")),
                Term(Loc.S("Servicio", "Service"), Loc.S("Programa de Windows que funciona en segundo plano, sin ventana.", "A Windows program that runs in the background, without a window.")),
                Term("DNS", Loc.S("El \"listín\" que traduce nombres como google.com en direcciones.", "The \"phone book\" that turns names like google.com into addresses.")));

            var keys = Section(Icons.Keyboard, Loc.S("Atajos de teclado", "Keyboard shortcuts"),
                Term("Ctrl + K", Loc.S("Buscar cualquier página, nivel o ajuste", "Search any page, level or tweak")),
                Term("Ctrl + F", Loc.S("Buscar en la lista de ajustes", "Search the tweak list")),
                Term("Ctrl + Shift + H", Loc.S("Ir a Inicio", "Go to Home")),
                Term("Ctrl + ,", Loc.S("Ajustes", "Settings")),
                Term("F1", Loc.S("Esta ayuda", "This help")),
                Term("Esc", Loc.S("Cerrar el diálogo abierto", "Close the open dialog")),
                Term("Tab / Shift + Tab", Loc.S("Moverse entre controles", "Move between controls")));

            var honest = Section(Icons.Shield, Loc.S("Lo que Optimizzzer no hace", "What Optimizzzer does not do"),
                Ui.Text(Loc.S("No promete cifras de FPS: la ganancia depende del juego y del equipo, y se mide tras aplicar. No aplica ajustes placebo, no desactiva Windows Defender ni Windows Update y no instala nada.",
                    "It does not promise FPS numbers: the gain depends on the game and the PC, and is measured after applying. It does not apply placebo tweaks, does not disable Windows Defender or Windows Update, and installs nothing.")));

            var tour = Ui.Secondary(Loc.S("Ver el recorrido de bienvenida", "Show the welcome tour"), W.ShowTour, Icons.Play);
            tour.HorizontalAlignment = HorizontalAlignment.Left;
            return Frame(null, Ui.Stack(0, how, glossary, keys, honest, tour));
        }
    }
}
