using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;

namespace Optimizzzer.App.Pages
{
    /// <summary>
    /// Levels: what this place is, how the four levels stack (each includes the one before), one row per level with an
    /// example, the cards, and a full preview of a level before anything is applied.
    /// </summary>
    public sealed class LevelsPage : AppPage
    {
        private Level? _detail;

        public LevelsPage(AppState s, MainWindow w) : base(s, w)
        {
            s.ScanFinished += () => W.RefreshCurrentIf(this);
        }

        public override string Key => "levels";
        public override string Title => Loc.S("Niveles", "Levels");
        public override string Icon => Icons.SpeedMedium;

        public override void OnShown()
        {
            if (S.ScanResults == null && !S.Scanning && S.ScanError == null) _ = S.ScanAsync();
        }

        /// <summary>Self-test only: the preview's Apply button, to check it stays on screen at any size.</summary>
        public Button ApplyForTest { get; private set; }
        public bool ShowingPreviewForTest => _detail != null && S.ScanResults != null;

        public void CloseForTest() { _detail = null; Invalidate(); }

        protected override FrameworkElement Build()
        {
            if (S.ScanError != null)
                return Frame(null, Ui.ErrorState(Loc.S("No se pudo analizar el equipo.", "The PC could not be scanned."), S.ScanError, () => _ = S.ScanAsync(true)));
            if (S.ScanResults == null)
            {
                var (view, _) = Ui.Loading(Loc.S("Leyendo el estado de cada ajuste…", "Reading the state of every tweak…"));
                return Frame(null, Ui.Stack(16, view, Ui.ScanDurationNote()));
            }
            return _detail.HasValue ? BuildPreview(_detail.Value) : BuildOverview();
        }

        // ---------- Overview ----------

        private FrameworkElement BuildOverview()
        {
            var body = new StackPanel();

            body.Children.Add(Ui.H2(Loc.S("Qué es esto", "What this is")));
            body.Children.Add(Ui.Muted(Loc.S(
                "Un nivel es un paquete de ajustes ya elegidos y probados. Eliges uno, ves la lista exacta de cambios con su explicación, y recién ahí decides aplicar. Todo se puede deshacer desde Historial, salvo las apps que se quitan, que se reinstalan desde la Store.",
                "A level is a bundle of tweaks already chosen and tested. You pick one, see the exact list of changes with their explanation, and only then decide to apply. Everything can be undone from History, except removed apps, which you reinstall from the Store.")));
            body.Children.Add(Ladder());

            // One row per level: what it does and a concrete, different example.
            var rows = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var l in Levels.All)
            {
                var g = new Grid { Margin = new Thickness(0, 10, 0, 10) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                var ic = Ui.Icon(Icons.For(l.Level), 22, "AccentText");
                ic.VerticalAlignment = VerticalAlignment.Top;
                ic.Margin = new Thickness(0, 2, 0, 0);
                g.Children.Add(ic);
                var text = Ui.Stack(2, Ui.Text(Loc.S(l.Name), 15, true), Ui.Text(Loc.S(l.What), 14), Ui.Muted(Loc.S(l.Example), 13));
                Grid.SetColumn(text, 1);
                g.Children.Add(text);
                rows.Children.Add(g);
            }
            body.Children.Add(rows);

            var heading = Ui.H2(Loc.S("Elige un nivel", "Pick a level"));
            heading.Margin = new Thickness(0, 20, 0, 4);
            body.Children.Add(heading);
            body.Children.Add(Ui.Muted(Loc.S("Cada nivel incluye todo lo del anterior. Al tocar uno ves qué cambia; nada se aplica hasta que confirmes.",
                "Each level includes everything in the one before. Tap one to see what changes; nothing is applied until you confirm.")));

            var cards = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            foreach (var l in Levels.All) cards.Children.Add(LevelCard(l));
            body.Children.Add(cards);

            var custom = Ui.Secondary(Loc.S("Prefiero elegir ajuste por ajuste", "I would rather pick tweak by tweak"), () => W.Navigate("wizard"), Icons.Wizard);
            custom.HorizontalAlignment = HorizontalAlignment.Left;
            custom.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(custom);
            return Frame(Loc.S("Optimiza en pocos pasos, de lo más seguro a lo más potente.", "Optimize in a few steps, from the safest to the most powerful."), body);
        }

        /// <summary>Quick ⊂ Balanced ⊂ Gaming ⊂ Maximum, drawn as a staircase so "Maximum includes Gaming" is obvious.</summary>
        private static FrameworkElement Ladder()
        {
            var row = new WrapPanel { Margin = new Thickness(0, 14, 0, 4) };
            int i = 0;
            foreach (var l in Levels.All)
            {
                if (i > 0)
                {
                    var plus = Ui.Text("+", 16, true, "FgMuted", wrap: false);
                    plus.Margin = new Thickness(8, 0, 8, 0);
                    plus.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(plus);
                }
                var chip = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 12, 6), Margin = new Thickness(0, 4, 0, 4) };
                chip.SetResourceReference(Border.BackgroundProperty, "SurfaceAlt");
                chip.Child = Ui.Row(6, Ui.Icon(Icons.For(l.Level), 14, "AccentText"), Ui.Text(Loc.S(l.Name), 13, true, "Fg", wrap: false));
                row.Children.Add(chip);
                i++;
            }
            var note = Ui.Muted(Loc.S("Máximo rendimiento ya incluye todo Juegos; Juegos incluye Equilibrado, y así.",
                "Maximum performance already includes all of Gaming; Gaming includes Balanced, and so on."), 13);
            return Ui.Stack(4, row, note);
        }

        /// <summary>A level card: how much of it this PC already has, and how many changes are left.</summary>
        private FrameworkElement LevelCard(LevelInfo l)
        {
            var pending = Levels.Select(S.ScanResults, l.Level, S.Ctx.Info);
            var (applied, total) = Levels.Progress(S.ScanResults, l.Level);

            var steps = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            for (int i = 1; i <= 4; i++)
            {
                var bar = new Border { Width = 18, Height = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 4, 0) };
                bar.SetResourceReference(Border.BackgroundProperty, i <= (int)l.Level ? "AccentText" : "Divider");
                steps.Children.Add(bar);
            }
            AutomationProperties.SetName(steps, Loc.S("Nivel ", "Level ") + (int)l.Level + Loc.S(" de 4", " of 4"));

            var progress = new ProgressBar { Minimum = 0, Maximum = Math.Max(1, total), Value = 0, Margin = new Thickness(0, 8, 0, 0) };
            progress.SetResourceReference(Control.BackgroundProperty, "Divider");
            Theme.AnimateValue(progress, applied);
            var status = pending.Count == 0
                ? Ui.Text(Loc.S("Todo aplicado", "All applied"), 13, true, "Success", wrap: false)
                : Ui.Text(pending.Count + (pending.Count == 1 ? Loc.S(" cambio pendiente", " change left") : Loc.S(" cambios pendientes", " changes left")), 13, true, "AccentText", wrap: false);
            var done = Ui.Muted(applied + Loc.S(" de ", " of ") + total + Loc.S(" ya aplicados en este equipo", " already applied on this PC"), 13);

            var title = Ui.Row(10, Ui.Icon(Icons.For(l.Level), 20, "AccentText"), Ui.Text(Loc.S(l.Name), 16, true, "Fg"));
            var tagline = Ui.Muted(Loc.S(l.Tagline));
            tagline.Margin = new Thickness(0, 4, 0, 12);
            tagline.MinHeight = 40; // same height for every card, so the progress bars line up
            var content = Ui.Stack(0, steps, title, tagline, status, progress, done,
                l.NeedsSecondConfirmation ? Ui.Text(Loc.S("Incluye cambios moderados: lee qué pierdes.", "Includes moderate changes: read what you lose."), 13, false, "Moderate") : null);

            var b = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
            b.SetResourceReference(FrameworkElement.StyleProperty, "CardButton");
            b.Width = MainWindow.Compact ? 280 : 300;
            b.Margin = new Thickness(0, 0, 12, 12);
            AutomationProperties.SetName(b, Loc.S(l.Name) + ". " + Loc.S(l.Tagline) + " " + status.Text);
            b.Click += (_, __) => OpenLevel(l.Level);
            return b;
        }

        // ---------- Preview of one level ----------

        /// <summary>Shows everything a level would change. Nothing is applied until the user confirms.</summary>
        public void OpenLevel(Level level)
        {
            if (S.ScanResults == null) { W.Toast(ToastKind.Info, Loc.S("Espera a que termine el análisis.", "Wait for the scan to finish.")); return; }
            var l = Levels.Get(level);
            if (Levels.Select(S.ScanResults, level, S.Ctx.Info).Count == 0)
            {
                W.Toast(ToastKind.Success, Loc.S("Todo lo del nivel " + l.Name.Es + " ya está aplicado en este equipo.", "Everything in the " + l.Name.En + " level is already applied on this PC."));
                return;
            }
            _detail = level;
            Invalidate();
            W.Navigate(Key);
        }

        private FrameworkElement BuildPreview(Level level)
        {
            var l = Levels.Get(level);
            var tweaks = Levels.Select(S.ScanResults, level, S.Ctx.Info);
            bool restorePoint = true;
            var restart = tweaks.Select(t => t.Restart).DefaultIfEmpty(RestartKind.None).Max();
            int uninstalls = tweaks.Count(t => !t.Reversible);
            int moderate = tweaks.Count(t => t.Risk != Risk.Safe);

            var body = new StackPanel();
            var head = Ui.Row(12, Ui.Icon(Icons.For(level), 28, "AccentText"), Ui.Stack(0, Ui.H2(Loc.S("Nivel " + l.Name.Es, l.Name.En + " level")), Ui.Muted(Loc.S(l.What))));
            body.Children.Add(head);

            var what = Ui.Stack(10,
                Ui.Notice(Icons.Info, "AccentText", tweaks.Count + Loc.S(" cambios en este equipo. Abajo tienes cada uno explicado.", " changes on this PC. Each one is explained below.")),
                Ui.Notice(Icons.CheckCircle, "Success", Loc.S("Qué ganas: ", "What you gain: ") + Loc.S(l.Gains)),
                uninstalls > 0 ? Ui.Notice(Icons.Error, "Advanced", uninstalls == 1
                    ? Loc.S("1 es una desinstalación de una app preinstalada: no se deshace desde aquí, se reinstala desde la Microsoft Store.", "1 is an uninstall of a preinstalled app: it is not undone from here, you reinstall it from the Microsoft Store.")
                    : uninstalls + Loc.S(" son desinstalaciones de apps preinstaladas: no se deshacen desde aquí, se reinstalan desde la Microsoft Store.", " are uninstalls of preinstalled apps: they are not undone from here, you reinstall them from the Microsoft Store.")) : null,
                Ui.Notice(restart != RestartKind.None ? Icons.Restart : Icons.Stopwatch, "FgMuted",
                    Loc.S("Tarda ", "Takes ") + Loc.S(l.Duration).ToLowerInvariant() + ". "
                    + (restart == RestartKind.Reboot ? Loc.S("Reinicia el equipo al terminar para que todo tome efecto; espera a que termine antes de abrir juegos.", "Restart the PC when it finishes so everything takes effect; wait until it is done before opening games.")
                       : restart != RestartKind.None ? Loc.S("Algunos cambios se ven al cerrar sesión o reiniciar el Explorador.", "Some changes show after signing out or restarting File Explorer.")
                       : Loc.S("Los cambios se ven al momento.", "Changes show right away."))));
            what.Margin = new Thickness(0, 16, 0, 0);
            body.Children.Add(what);

            var rp = new CheckBox { Content = Loc.S("Crear un punto de restauración antes (recomendado)", "Create a restore point first (recommended)"), IsChecked = true, Margin = new Thickness(0, 14, 0, 0) };
            rp.Checked += (_, __) => restorePoint = true;
            rp.Unchecked += (_, __) => restorePoint = false;
            body.Children.Add(rp);

            // Every change, grouped by category, with what it does and what you lose.
            foreach (var group in tweaks.GroupBy(t => t.Category))
            {
                var h = Ui.Row(8, Ui.Icon(Icons.For(group.Key), 16, "FgMuted"), Ui.Text(Names.Of(group.Key) + " · " + group.Count(), 15, true));
                h.Margin = new Thickness(0, 22, 0, 6);
                body.Children.Add(h);
                foreach (var t in group)
                {
                    var row = Ui.Stack(2,
                        Ui.Row(10, Ui.Text(Loc.S(t.Title), 14, true, "Fg"), Ui.RiskMarker(t.Risk)),
                        Ui.Muted(Loc.S(t.Summary), 13),
                        t.Downside.Es.Length > 0 ? Ui.Text(Loc.S("Qué pierdes: ", "What you lose: ") + Loc.S(t.Downside), 13, false, "Moderate") : null,
                        !t.Reversible ? Ui.Text(Loc.S("No se deshace desde aquí.", "Not undone from here."), 13, false, "Advanced") : null);
                    row.Margin = new Thickness(24, 6, 0, 6);
                    body.Children.Add(row);
                }
            }

            var back = Ui.Secondary(Loc.S("Volver a los niveles", "Back to levels"), () => { _detail = null; W.RefreshCurrentIf(this); }, Icons.Back);
            var apply = Ui.Primary(Loc.S("Aplicar ", "Apply ") + tweaks.Count + Loc.S(" cambios", " changes"), async () =>
            {
                string question = Loc.S("¿Aplicar el nivel " + l.Name.Es + "?", "Apply the " + l.Name.En + " level?");
                string message = l.NeedsSecondConfirmation
                    ? moderate + Loc.S(" de estos cambios son de riesgo moderado: quitan funciones como la búsqueda rápida de archivos o la hibernación. Podrás deshacerlos desde Historial; las apps quitadas se reinstalan desde la Store.",
                        " of these changes are moderate risk: they remove features such as fast file search or hibernation. You can undo them from History; removed apps are reinstalled from the Store.")
                    : Loc.S("Se aplican los ", "The ") + tweaks.Count + Loc.S(" cambios de la lista. Podrás deshacerlos desde Historial.", " changes in the list are applied. You can undo them from History.");
                if (!await W.Confirm(question, message, Loc.S("Sí, aplicar", "Yes, apply"), danger: l.NeedsSecondConfirmation)) return;
                _detail = null;
                Invalidate();
                W.Navigate("wizard");
                W.PageOf<WizardPage>().StartLevel(l, tweaks, restorePoint);
            }, Icons.Check);
            ApplyForTest = apply;
            return Frame(null, body, Footer(back, apply));
        }
    }
}
