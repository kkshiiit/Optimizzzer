using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.App.Pages
{
    /// <summary>The step-by-step flow: scan, profile, select, review (dry run), apply, results.</summary>
    public sealed class WizardPage : AppPage
    {
        private enum Step { Scan, Profile, Select, Review, Apply, Done }

        private Step _step = Step.Scan;
        private bool _scanning;
        private string _scanError;
        private Action<double, string> _scanProgress;
        private ProfileKind? _profileChoice;
        private bool _manualChoice;

        public WizardPage(AppState s, MainWindow w) : base(s, w) { }

        public override string Key => "wizard";
        public override string Title => Loc.S("Asistente de optimización", "Optimization wizard");
        public override string Icon => Icons.Wizard;

        private void GoTo(Step step)
        {
            _step = step;
            Invalidate();
            W.ShowView(View);
            OnShown();
        }

        /// <summary>Self-test only: renders the profile, selection and review steps. Never applies anything.</summary>
        public void ShowStepsForTest(Action<string> afterEach)
        {
            foreach (var step in new[] { Step.Profile, Step.Select, Step.Review })
            {
                _step = step;
                Invalidate();
                W.ShowView(View);
                afterEach(step.ToString());
            }
            _step = Step.Scan;
            Invalidate();
        }

        /// <summary>Self-test only (disposable CI machine): runs the real Apply step through the UI and reports the result.</summary>
        public bool ApplyFinishedForTest => _step == Step.Done && !_applyRunning;

        public void StartApplyForTest()
        {
            S.CreateRestorePoint = false;
            GoTo(Step.Apply);
        }

        public override void OnShown()
        {
            if (_step == Step.Scan && S.ScanResults == null && !_scanning && _scanError == null) _ = ScanAsync();
            if (_step == Step.Apply && !_applyRunning) _ = RunApplyAsync();
        }

        protected override FrameworkElement Build()
        {
            switch (_step)
            {
                case Step.Profile: return BuildProfile();
                case Step.Select: return BuildSelect();
                case Step.Review: return BuildReview();
                case Step.Apply: return BuildApply();
                case Step.Done: return BuildDone();
                default: return BuildScan();
            }
        }

        // ---------- Layout shared by every step ----------

        private static readonly (Step Step, string Es, string En)[] Steps =
        {
            (Step.Scan, "Análisis", "Scan"), (Step.Profile, "Perfil", "Profile"), (Step.Select, "Selección", "Selection"),
            (Step.Review, "Revisión", "Review"), (Step.Apply, "Aplicar", "Apply"), (Step.Done, "Resultado", "Result"),
        };

        private Grid StepFrame(string heading, string intro, UIElement body, UIElement footer, bool scroll = true)
        {
            var g = new Grid { Margin = new Thickness(32, 24, 32, 24) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var stepper = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            AutomationProperties.SetName(stepper, Loc.S("Pasos del asistente", "Wizard steps"));
            int current = (int)_step;
            for (int i = 0; i < Steps.Length; i++)
            {
                var done = i < current;
                var isCurrent = i == current;
                var mark = done ? Ui.Icon(Icons.Check, 12, "Success") : (UIElement)Ui.Text((i + 1).ToString(), 12, isCurrent, isCurrent ? "AccentText" : "FgMuted", wrap: false);
                var label = Ui.Text(Loc.S(Steps[i].Es, Steps[i].En), 12, isCurrent, isCurrent ? "Fg" : "FgMuted", wrap: false);
                var item = Ui.Row(6, mark, label);
                item.Margin = new Thickness(0, 0, 20, 4);
                AutomationProperties.SetName(item, Loc.S("Paso ", "Step ") + (i + 1) + ": " + label.Text + (isCurrent ? Loc.S(" (actual)", " (current)") : done ? Loc.S(" (hecho)", " (done)") : ""));
                stepper.Children.Add(item);
            }

            var header = Ui.Stack(0, stepper, Ui.H1(heading), string.IsNullOrEmpty(intro) ? null : Ui.Muted(intro));
            header.Margin = new Thickness(0, 0, 0, 20);
            header.MaxWidth = 1040;
            header.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(header);

            var content = scroll ? Ui.Scroll(body) : body;
            Grid.SetRow(content, 1);
            g.Children.Add(content);

            if (footer != null)
            {
                var bar = new Border { Child = footer, Padding = new Thickness(0, 16, 0, 0), BorderThickness = new Thickness(0, 1, 0, 0) };
                bar.SetResourceReference(Border.BorderBrushProperty, "Divider");
                Grid.SetRow(bar, 2);
                g.Children.Add(bar);
            }
            return g;
        }

        // ---------- Step 1: scan ----------

        private async Task ScanAsync()
        {
            _scanning = true;
            _scanError = null;
            if (_step == Step.Scan) { Invalidate(); W.ShowView(View); }
            try
            {
                _scanProgress?.Invoke(0, Loc.S("Leyendo la información del equipo…", "Reading PC information…"));
                await S.EnsureReadyAsync();
                var tweaks = S.Catalog.ToList();
                var progress = new Progress<int>(n => _scanProgress?.Invoke(n / (double)tweaks.Count,
                    Loc.S("Comprobando ajuste ", "Checking tweak ") + n + Loc.S(" de ", " of ") + tweaks.Count + "…"));
                S.ScanResults = await Task.Run(() => S.Engine.Scan(tweaks, progress));
            }
            catch (Exception ex)
            {
                _scanError = ex.Message;
                S.ResetInit();
            }
            finally
            {
                _scanning = false;
                if (_step == Step.Scan) { Invalidate(); W.ShowView(View); }
            }
        }

        private FrameworkElement BuildScan()
        {
            if (_scanning || (S.ScanResults == null && _scanError == null))
            {
                var (view, update) = Ui.Loading(Loc.S("Leyendo la información del equipo…", "Reading PC information…"));
                _scanProgress = update;
                return StepFrame(Loc.S("Analizando tu equipo", "Scanning your PC"),
                    Loc.S("Solo se lee. No se cambia nada hasta el paso Aplicar.", "Read only. Nothing changes until the Apply step."), view, null);
            }
            if (_scanError != null)
            {
                return StepFrame(Loc.S("Analizando tu equipo", "Scanning your PC"), null,
                    Ui.ErrorState(Loc.S("No se pudo analizar el equipo.", "The PC could not be scanned."), _scanError, () => _ = ScanAsync()), null);
            }

            var info = S.Ctx.Info;
            var results = S.ScanResults;
            int available = results.Count(r => r.Status == TweakStatus.NotApplied || r.Status == TweakStatus.Partial);
            int applied = results.Count(r => r.Status == TweakStatus.Applied);
            int na = results.Count(r => r.Status == TweakStatus.NotApplicable);
            int unknown = results.Count(r => r.Status == TweakStatus.Unknown);

            string disk = info.SystemDriveIsSsd == true ? "SSD" : info.SystemDriveIsSsd == false ? Loc.S("Disco duro (HDD)", "Hard drive (HDD)") : null;
            var pc = Ui.Panel(Ui.Stack(12,
                Ui.H2(Loc.S("Tu equipo", "Your PC")),
                Ui.KeyValues(
                    (Loc.S("Sistema", "System"), info.ProductName + " " + info.DisplayVersion + " · " + Loc.S("compilación ", "build ") + info.Build + "." + info.Ubr),
                    (Loc.S("Tipo", "Type"), (info.IsLaptop ? Loc.S("Portátil", "Laptop") : Loc.S("Sobremesa", "Desktop")) + (info.IsVirtualMachine ? Loc.S(" (máquina virtual)", " (virtual machine)") : "")),
                    ("CPU", info.CpuName + (info.LogicalProcessors > 0 ? " · " + info.LogicalProcessors + Loc.S(" hilos", " threads") : "")),
                    ("RAM", info.TotalMemoryMb > 0 ? Loc.N(info.TotalMemoryMb / 1024.0, "N1") + " GB" : null),
                    ("GPU", string.Join(", ", info.Gpus)),
                    (Loc.S("Disco del sistema", "System drive"), disk))));

            var status = Ui.Panel(Ui.Stack(12,
                Ui.H2(Loc.S("Estado de los ajustes", "Tweak status")),
                Ui.Notice(Icons.ChevronRight, "AccentText", available + Loc.S(" ajustes se pueden aplicar en este equipo.", " tweaks can be applied on this PC.")),
                Ui.Notice(Icons.CheckCircle, "Success", applied + Loc.S(" ya están aplicados.", " are already applied.")),
                Ui.Notice(Icons.Info, "FgMuted", na + Loc.S(" no aplican a tu versión de Windows o a tu hardware.", " do not apply to your Windows version or hardware.")),
                unknown > 0 ? Ui.Notice(Icons.Warning, "Moderate", unknown + Loc.S(" no se pudieron comprobar y no se tocarán.", " could not be checked and will not be touched.")) : null));

            var safety = Ui.Notice(Icons.Shield, "Success", Loc.S(
                "Antes de aplicar se crea un punto de restauración y se guarda el valor anterior de cada cambio, así que podrás deshacerlos uno a uno desde Historial.",
                "Before applying, a restore point is created and the previous value of every change is saved, so you can undo them one by one from History."));

            var body = Ui.Stack(16, pc, status, safety);
            var footer = Footer(
                Ui.Secondary(Loc.S("Volver a analizar", "Scan again"), () => { S.ScanResults = null; _ = ScanAsync(); }, Icons.Restart),
                Ui.Primary(Loc.S("Elegir un perfil", "Choose a profile"), () => GoTo(Step.Profile)));
            return StepFrame(Loc.S("Análisis terminado", "Scan complete"), Loc.S("Esto es lo que hemos encontrado.", "This is what we found."), body, footer);
        }

        // ---------- Step 2: profile ----------

        private FrameworkElement BuildProfile()
        {
            var info = S.Ctx.Info;
            var recommended = Profiles.Recommend(info);
            if (_profileChoice == null && !_manualChoice) _profileChoice = S.Profile ?? recommended;
            string reason = info.IsLaptop ? Loc.S("Hemos detectado un portátil.", "We detected a laptop.")
                : info.HasNvidia || info.HasAmdGpu ? Loc.S("Hemos detectado una tarjeta gráfica dedicada.", "We detected a dedicated graphics card.")
                : Loc.S("Hemos detectado un sobremesa sin gráfica dedicada.", "We detected a desktop without a dedicated graphics card.");

            var list = new StackPanel();
            Button next = null;
            void Update()
            {
                int n = _profileChoice.HasValue ? Profiles.Select(S.ScanResults, _profileChoice.Value, info).Count() : 0;
                next.Content = _profileChoice.HasValue
                    ? Loc.S("Ver y ajustar los ", "Review and adjust the ") + n + Loc.S(" cambios", " changes")
                    : Loc.S("Elegir los cambios uno a uno", "Pick changes one by one");
                AutomationProperties.SetName(next, next.Content.ToString());
            }

            foreach (var p in Profiles.All)
            {
                int count = Profiles.Select(S.ScanResults, p.Kind, info).Count();
                var title = Ui.Row(10, Ui.Text(Loc.S(p.Name), 16, true, "Fg", wrap: false),
                    p.Kind == recommended ? Ui.Text(Loc.S("Recomendado", "Recommended"), 12, true, "AccentText", wrap: false) : null);
                var content = Ui.Stack(4, title, Ui.Muted(Loc.S(p.Description)),
                    Ui.Muted(count + Loc.S(" cambios para este equipo", " changes for this PC") + (p.Kind == recommended ? " · " + reason : ""), 12));
                var rb = new RadioButton { Content = content, GroupName = "profile", IsChecked = _profileChoice == p.Kind, Margin = new Thickness(0, 0, 0, 16) };
                AutomationProperties.SetName(rb, Loc.S(p.Name) + ", " + count + Loc.S(" cambios", " changes"));
                rb.Checked += (_, __) => { _profileChoice = p.Kind; _manualChoice = false; Update(); };
                list.Children.Add(rb);
            }
            var manual = new RadioButton
            {
                Content = Ui.Stack(4, Ui.Text(Loc.S("Elegir yo mismo", "I will choose"), 16, true),
                    Ui.Muted(Loc.S("Empieza sin nada marcado y elige cada cambio en la lista.", "Start with nothing ticked and pick each change from the list."))),
                GroupName = "profile", IsChecked = _manualChoice,
            };
            AutomationProperties.SetName(manual, Loc.S("Elegir yo mismo", "I will choose"));
            manual.Checked += (_, __) => { _profileChoice = null; _manualChoice = true; Update(); };
            list.Children.Add(manual);

            next = Ui.Primary("", () =>
            {
                S.Profile = _profileChoice;
                S.Selected.Clear();
                if (_profileChoice.HasValue)
                    foreach (var t in Profiles.Select(S.ScanResults, _profileChoice.Value, info)) S.Selected.Add(t.Id);
                GoTo(Step.Select);
            });
            Update();

            var footer = Footer(Ui.Secondary(Loc.S("Atrás", "Back"), () => GoTo(Step.Scan), Icons.Back), next);
            return StepFrame(Loc.S("¿Para qué usas este equipo?", "What do you use this PC for?"),
                Loc.S("El perfil solo marca una selección inicial. En el siguiente paso puedes cambiar cualquier cosa.", "A profile only ticks a starting selection. You can change anything in the next step."),
                list, footer);
        }

        // ---------- Step 3: select ----------

        private Category? _category;
        private string _query = "";
        private bool _hideNotApplicable = true;

        private FrameworkElement BuildSelect()
        {
            var rows = new Dictionary<Tweak, TweakRow>();
            var listPanel = new StackPanel();
            var countText = Ui.Muted("");
            Button reviewButton = null;
            var categoryButtons = new Dictionary<string, Button>();
            var emptyHint = Ui.Empty(Loc.S("Ningún ajuste coincide con la búsqueda o el filtro.", "No tweak matches the search or filter."));
            emptyHint.Margin = new Thickness(0, 16, 0, 0);

            void UpdateCounts()
            {
                int n = S.Selected.Count;
                countText.Text = n == 1 ? Loc.S("1 cambio seleccionado", "1 change selected") : n + Loc.S(" cambios seleccionados", " changes selected");
                reviewButton.IsEnabled = n > 0;
                reviewButton.Content = Loc.S("Revisar ", "Review ") + n + (n == 1 ? Loc.S(" cambio", " change") : Loc.S(" cambios", " changes"));
                AutomationProperties.SetName(reviewButton, reviewButton.Content.ToString());
                foreach (var kv in categoryButtons)
                {
                    var tweaks = kv.Key == "all" ? S.Catalog : S.Catalog.Where(t => t.Category.ToString() == kv.Key);
                    var sel = tweaks.Count(t => S.Selected.Contains(t.Id));
                    var label = (TextBlock)((Grid)kv.Value.Content).Children[2];
                    label.Text = sel > 0 ? sel.ToString() : "";
                }
            }

            void ApplyFilter()
            {
                var q = _query.Trim();
                int visible = 0;
                foreach (var kv in rows)
                {
                    var t = kv.Key;
                    var st = S.StatusOf(t);
                    bool show = (_category == null || t.Category == _category)
                        && (!_hideNotApplicable || st != TweakStatus.NotApplicable)
                        && (q.Length == 0 || Matches(t, q));
                    kv.Value.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                    if (show) visible++;
                }
                emptyHint.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            foreach (var t in S.Catalog)
            {
                var row = new TweakRow(t, S.StatusOf(t), S.Selected.Contains(t.Id), on =>
                {
                    if (on) S.Selected.Add(t.Id); else S.Selected.Remove(t.Id);
                    UpdateCounts();
                });
                rows[t] = row;
                listPanel.Children.Add(row);
            }
            listPanel.Children.Add(emptyHint);

            // Category rail
            var rail = new StackPanel();
            AutomationProperties.SetName(rail, Loc.S("Categorías", "Categories"));
            Button CategoryButton(string key, string icon, string text, Category? cat)
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var ic = Ui.Icon(icon, 16);
                var tx = Ui.Text(text, 14, false, "Fg", wrap: false);
                tx.Margin = new Thickness(10, 0, 8, 0);
                tx.VerticalAlignment = VerticalAlignment.Center;
                var cnt = Ui.Text("", 12, true, "AccentText", wrap: false);
                cnt.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(tx, 1); Grid.SetColumn(cnt, 2);
                g.Children.Add(ic); g.Children.Add(tx); g.Children.Add(cnt);
                var b = new Button { Content = g, Tag = _category == cat ? "current" : null, Margin = new Thickness(0, 1, 0, 1) };
                b.SetResourceReference(FrameworkElement.StyleProperty, "NavButton");
                AutomationProperties.SetName(b, text);
                b.Click += (_, __) =>
                {
                    _category = cat;
                    foreach (var cb in categoryButtons.Values) { cb.Tag = null; AutomationProperties.SetItemStatus(cb, ""); }
                    b.Tag = "current";
                    AutomationProperties.SetItemStatus(b, "current");
                    ApplyFilter();
                };
                categoryButtons[key] = b;
                return b;
            }
            rail.Children.Add(CategoryButton("all", Icons.Apps, Loc.S("Todos", "All"), null));
            foreach (Category c in Enum.GetValues(typeof(Category)))
                rail.Children.Add(CategoryButton(c.ToString(), Icons.For(c), Names.Of(c), c));

            // Toolbar
            var search = Ui.SearchBox(Loc.S("Buscar ajustes (Ctrl+F)", "Search tweaks (Ctrl+F)"), q => { _query = q; ApplyFilter(); });
            search.Box.Text = _query;
            var hideNa = new CheckBox { Content = Loc.S("Ocultar los que no aplican", "Hide the ones that do not apply"), IsChecked = _hideNotApplicable, Margin = new Thickness(16, 0, 0, 0) };
            hideNa.Checked += (_, __) => { _hideNotApplicable = true; ApplyFilter(); };
            hideNa.Unchecked += (_, __) => { _hideNotApplicable = false; ApplyFilter(); };
            var selectSafe = Ui.Subtle(Loc.S("Marcar los seguros visibles", "Tick visible safe ones"), () =>
            {
                foreach (var kv in rows.Where(r => r.Value.Visibility == Visibility.Visible && r.Key.Risk == Risk.Safe && r.Value.CanSelect))
                    kv.Value.SetChecked(true);
            }, Icons.CheckCircle);
            var clear = Ui.Subtle(Loc.S("Desmarcar visibles", "Untick visible"), () =>
            {
                foreach (var kv in rows.Where(r => r.Value.Visibility == Visibility.Visible)) kv.Value.SetChecked(false);
            }, Icons.Close);
            var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            toolbar.Children.Add(search);
            hideNa.VerticalAlignment = VerticalAlignment.Center;
            toolbar.Children.Add(hideNa);
            selectSafe.Margin = new Thickness(8, 0, 0, 0);
            toolbar.Children.Add(selectSafe);
            toolbar.Children.Add(clear);

            var main = new Grid();
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            main.ColumnDefinitions.Add(new ColumnDefinition());
            var railScroll = Ui.Scroll(rail);
            railScroll.Margin = new Thickness(0, 0, 16, 0);
            main.Children.Add(railScroll);
            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition());
            right.Children.Add(toolbar);
            var listScroll = Ui.Scroll(listPanel);
            Grid.SetRow(listScroll, 1);
            right.Children.Add(listScroll);
            Grid.SetColumn(right, 1);
            main.Children.Add(right);

            reviewButton = Ui.Primary("", () => GoTo(Step.Review));
            var left = Ui.Row(16, Ui.Secondary(Loc.S("Atrás", "Back"), () => GoTo(Step.Profile), Icons.Back), countText);
            countText.VerticalAlignment = VerticalAlignment.Center;
            var footer = Footer(left, reviewButton);
            UpdateCounts();
            ApplyFilter();

            var frame = StepFrame(Loc.S("Elige qué cambiar", "Choose what to change"),
                Loc.S("Despliega cualquier ajuste para ver qué hace, qué pierdes y qué cambia exactamente.", "Expand any tweak to see what it does, what you lose and exactly what changes."),
                main, footer, scroll: false);
            frame.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.F && Keyboard.Modifiers == ModifierKeys.Control) { search.Box.Focus(); e.Handled = true; }
            };
            return frame;
        }

        private static bool Matches(Tweak t, string q)
        {
            bool In(string s) => s.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
            return In(Loc.S(t.Title)) || In(Loc.S(t.Summary)) || In(t.Id) || In(Names.Of(t.Category));
        }

        // ---------- Step 4: review (dry run) ----------

        private List<Tweak> SelectedTweaks() => S.Catalog.Where(t => S.Selected.Contains(t.Id)).ToList();

        private FrameworkElement BuildReview()
        {
            var tweaks = SelectedTweaks();
            int moderate = tweaks.Count(t => t.Risk == Risk.Moderate);
            int advanced = tweaks.Count(t => t.Risk == Risk.Advanced);
            int permanent = tweaks.Count(t => !t.Reversible);
            var restart = tweaks.Select(t => t.Restart).DefaultIfEmpty(RestartKind.None).Max();

            var notes = Ui.Stack(8,
                Ui.Notice(Icons.Info, "AccentText", tweaks.Count + Loc.S(" cambios. Esta es la lista exacta de lo que se va a modificar; todavía no se ha tocado nada.", " changes. This is the exact list of what will be modified; nothing has been touched yet.")),
                moderate + advanced > 0 ? Ui.Notice(Icons.Warning, "Moderate", Loc.S("Incluye ", "Includes ") + moderate + Loc.S(" de riesgo moderado y ", " moderate and ") + advanced + Loc.S(" avanzados. Lee su inconveniente antes de seguir.", " advanced. Read their downside before continuing.")) : null,
                permanent > 0 ? Ui.Notice(Icons.Error, "Advanced", permanent + Loc.S(" cambios no se pueden deshacer desde Optimizzzer (desinstalar apps). Se recuperan reinstalando desde la Microsoft Store.", " changes cannot be undone from Optimizzzer (uninstalling apps). You get them back by reinstalling from the Microsoft Store.")) : null,
                restart != RestartKind.None ? Ui.Notice(Icons.Restart, "FgMuted", Names.Of(restart) + ".") : null);

            var rp = new CheckBox { Content = Loc.S("Crear un punto de restauración antes de aplicar (recomendado)", "Create a restore point before applying (recommended)"), IsChecked = S.CreateRestorePoint, Margin = new Thickness(0, 8, 0, 0) };
            Button apply = null;
            void Label()
            {
                apply.Content = S.CreateRestorePoint
                    ? Loc.S("Crear punto de restauración y aplicar ", "Create restore point and apply ") + tweaks.Count
                    : Loc.S("Aplicar ", "Apply ") + tweaks.Count + Loc.S(" cambios", " changes");
                AutomationProperties.SetName(apply, apply.Content.ToString());
            }
            rp.Checked += (_, __) => { S.CreateRestorePoint = true; Label(); };
            rp.Unchecked += (_, __) => { S.CreateRestorePoint = false; Label(); };

            var list = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            foreach (var group in tweaks.GroupBy(t => t.Category))
            {
                var h = Ui.Row(10, Ui.Icon(Icons.For(group.Key), 16), Ui.H3(Names.Of(group.Key)));
                h.Margin = new Thickness(0, 16, 0, 8);
                list.Children.Add(h);
                foreach (var t in group)
                {
                    var actions = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
                    foreach (var a in t.Actions)
                    {
                        var line = Ui.Text(a.Describe(Loc.Lang), 12, false, "FgMuted");
                        line.FontFamily = new System.Windows.Media.FontFamily("Consolas");
                        actions.Children.Add(line);
                    }
                    var title = Ui.Row(12, Ui.Text(Loc.S(t.Title), 14, true), Ui.RiskMarker(t.Risk));
                    var item = Ui.Stack(2, title, actions);
                    item.Margin = new Thickness(0, 0, 0, 12);
                    list.Children.Add(item);
                }
            }

            apply = Ui.Primary("", () => GoTo(Step.Apply));
            Label();
            var footer = Footer(Ui.Secondary(Loc.S("Atrás", "Back"), () => GoTo(Step.Select), Icons.Back), apply);
            return StepFrame(Loc.S("Revisa los cambios", "Review the changes"), null, Ui.Stack(0, notes, rp, list), footer);
        }

        // ---------- Step 5: apply ----------

        private CancellationTokenSource _cts;
        private (Action<double, string> Update, StackPanel Log) _applyUi;
        private bool _applyRunning;

        private async Task RunApplyAsync()
        {
            if (_applyRunning) return;
            _applyRunning = true;
            var tweaks = SelectedTweaks();
            var (update, log) = _applyUi;
            // Created on the UI thread, so its callbacks run on the UI thread.
            var progress = new Progress<ApplyProgress>(p =>
            {
                update(p.Done / (double)Math.Max(1, p.Total),
                    p.LastEntry == null ? Loc.S("Aplicando: ", "Applying: ") + Loc.S(p.Current.Title) + " (" + (p.Done + 1) + Loc.S(" de ", " of ") + p.Total + ")" : null);
                if (p.LastEntry != null) log.Children.Add(LogLine(p.Current, p.LastEntry));
            });
            var options = new ApplyOptions { CreateRestorePoint = S.CreateRestorePoint, RestorePointName = "Optimizzzer " + DateTime.Now.ToString("g") };
            var token = _cts.Token;
            try
            {
                await Task.Run(() =>
                {
                    var before = S.Win.Measurer.Take("before");
                    S.Before = before;
                    S.Win.Measurements.Add(before);
                    var session = S.Engine.Apply(tweaks, options, progress, token);
                    S.LastSession = session;
                    if (tweaks.Any(t => t.Actions.OfType<AppxRemoveAction>().Any())) S.Win.Appx.Refresh();
                    for (int i = 0; i < S.ScanResults.Count; i++)
                        if (tweaks.Contains(S.ScanResults[i].Tweak))
                            S.ScanResults[i] = new ScanResult(S.ScanResults[i].Tweak, S.Engine.Detect(S.ScanResults[i].Tweak));
                    S.After = S.Win.Measurer.Take("after");
                    S.Win.Measurements.Add(S.After);
                });
                S.Selected.Clear();
                GoTo(Step.Done);
            }
            catch (Exception ex)
            {
                W.ShowError(ex.Message);
                GoTo(Step.Review);
            }
            finally { _applyRunning = false; }
        }

        private FrameworkElement BuildApply()
        {
            var (loading, update) = Ui.Loading(S.CreateRestorePoint
                ? Loc.S("Creando el punto de restauración…", "Creating the restore point…")
                : Loc.S("Preparando…", "Preparing…"));
            var log = new StackPanel { Margin = new Thickness(0, 20, 0, 0) };
            AutomationProperties.SetLiveSetting(log, AutomationLiveSetting.Polite);
            _cts = new CancellationTokenSource();
            var stop = Ui.Secondary(Loc.S("Detener tras el cambio actual", "Stop after the current change"), () => _cts.Cancel(), Icons.Stop);

            _applyUi = (update, log);
            return StepFrame(Loc.S("Aplicando cambios", "Applying changes"),
                Loc.S("No apagues el equipo. Cada cambio que falle se revierte solo.", "Do not turn off the PC. Any change that fails is rolled back automatically."),
                Ui.Stack(0, loading, log), Footer(stop, null));
        }

        private static FrameworkElement LogLine(Tweak t, JournalEntry e)
        {
            switch (e.ResultKind)
            {
                case EntryResult.Applied: return Ui.Notice(Icons.CheckCircle, "Success", Loc.S(t.Title));
                case EntryResult.Skipped: return Ui.Notice(Icons.Info, "FgMuted", Loc.S(t.Title) + Loc.S(" (ya estaba aplicado)", " (already applied)"));
                default: return Ui.Notice(Icons.Error, "Advanced", Loc.S(t.Title) + ": " + e.Error + Loc.S(" (revertido)", " (rolled back)"));
            }
        }

        // ---------- Step 6: results ----------

        private FrameworkElement BuildDone()
        {
            var s = S.LastSession;
            if (s == null) return BuildScan();
            int ok = s.Entries.Count(e => e.ResultKind == EntryResult.Applied);
            int failed = s.Entries.Count(e => e.ResultKind == EntryResult.Failed);
            int skipped = s.Entries.Count(e => e.ResultKind == EntryResult.Skipped);
            var applied = s.Entries.Where(e => e.ResultKind == EntryResult.Applied).Select(e => S.Catalog.FirstOrDefault(t => t.Id == e.TweakId)).Where(t => t != null).ToList();
            var restart = applied.Select(t => t.Restart).DefaultIfEmpty(RestartKind.None).Max();

            var summary = Ui.Stack(8,
                Ui.Notice(Icons.CheckCircle, "Success", ok + Loc.S(" cambios aplicados.", " changes applied.")),
                skipped > 0 ? Ui.Notice(Icons.Info, "FgMuted", skipped + Loc.S(" ya estaban aplicados.", " were already applied.")) : null,
                failed > 0 ? Ui.Notice(Icons.Error, "Advanced", failed + Loc.S(" fallaron y se revirtieron. Mira el detalle en Historial.", " failed and were rolled back. See details in History.")) : null,
                s.RestorePointCreated
                    ? Ui.Notice(Icons.Shield, "Success", Loc.S("Punto de restauración creado.", "Restore point created."))
                    : S.CreateRestorePoint ? Ui.Notice(Icons.Warning, "Moderate", Loc.S("No se pudo crear el punto de restauración: ", "The restore point could not be created: ") + s.RestorePointError + Loc.S(". Los cambios se pueden deshacer igualmente desde Historial.", ". Changes can still be undone from History.")) : null);

            var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            void Add(Button b) { b.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(b); }
            if (restart == RestartKind.Explorer || applied.Any(t => t.Restart == RestartKind.Explorer))
                Add(Ui.Secondary(Loc.S("Reiniciar el Explorador ahora", "Restart File Explorer now"), () => Task.Run(() => S.Win.RestartExplorer()), Icons.Restart));
            if (restart >= RestartKind.SignOut)
                Add(Ui.Secondary(Loc.S("Reiniciar el equipo", "Restart the PC"), async () =>
                {
                    if (await W.Confirm(Loc.S("¿Reiniciar ahora?", "Restart now?"), Loc.S("Guarda tu trabajo antes. El equipo se reiniciará de inmediato.", "Save your work first. The PC will restart immediately."), Loc.S("Reiniciar", "Restart")))
                        S.Win.Process.Run("shutdown.exe", "/r /t 0");
                }, Icons.Restart));
            Add(Ui.Secondary(Loc.S("Guardar informe", "Save report"), SaveReport, Icons.Save));
            Add(Ui.Secondary(Loc.S("Ver historial", "View history"), () => W.Navigate("history"), Icons.History));

            FrameworkElement measures = null;
            if (S.Before != null && S.After != null)
            {
                var rows = MeasurementCompare.Compare(S.Before, S.After).Where(d => d.Before.HasValue || d.After.HasValue).ToList();
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                void Cell(UIElement e, int r, int c) { Grid.SetRow(e, r); Grid.SetColumn(e, c); g.Children.Add(e); }
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Cell(Ui.Muted(Loc.S("Métrica", "Metric")), 0, 0); Cell(Ui.Muted(Loc.S("Antes", "Before")), 0, 1); Cell(Ui.Muted(Loc.S("Ahora", "Now")), 0, 2);
                for (int i = 0; i < rows.Count; i++)
                {
                    g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var name = Ui.Text(Loc.S(rows[i].Name)); name.Margin = new Thickness(0, 6, 0, 0);
                    Cell(name, i + 1, 0);
                    Cell(MeasurePage.Value(rows[i].Before, rows[i].Unit), i + 1, 1);
                    Cell(MeasurePage.Value(rows[i].After, rows[i].Unit), i + 1, 2);
                }
                measures = Ui.Panel(Ui.Stack(12, Ui.H2(Loc.S("Medido en este equipo", "Measured on this PC")),
                    Ui.Muted(Loc.S("Antes y justo después de aplicar. La memoria y los procesos se estabilizan tras reiniciar; la duración del arranque solo cambia después de reiniciar.",
                        "Before and right after applying. Memory and processes settle after a restart; boot duration only changes after restarting.")), g));
                measures.Margin = new Thickness(0, 8, 0, 0);
            }

            var footer = Footer(null, Ui.Primary(Loc.S("Empezar otra optimización", "Start another optimization"), () =>
            {
                _profileChoice = null; _manualChoice = false;
                GoTo(Step.Scan);
            }));
            return StepFrame(failed == 0 ? Loc.S("Listo", "Done") : Loc.S("Terminado con avisos", "Finished with warnings"), null,
                Ui.Stack(0, summary, actions, measures), footer);
        }

        private void SaveReport()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "Optimizzzer-" + DateTime.Now.ToString("yyyy-MM-dd-HHmm") + ".html",
                Filter = Loc.S("Página web", "Web page") + " (*.html)|*.html",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            };
            if (dlg.ShowDialog(W) != true) return;
            try
            {
                var html = HtmlReport.Build(Loc.Lang, S.Ctx.Info, S.LastSession, S.Catalog.ToList(), S.Before, S.After, DateTime.Now);
                File.WriteAllText(dlg.FileName, html, new System.Text.UTF8Encoding(false));
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
        }
    }
}
