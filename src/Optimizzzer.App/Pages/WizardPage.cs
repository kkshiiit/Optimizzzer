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
        private Action<double, string> _scanProgress;
        private ProfileKind? _profileChoice;
        private bool _manualChoice;

        public WizardPage(AppState s, MainWindow w) : base(s, w)
        {
            s.ScanProgress += (f, m) => _scanProgress?.Invoke(f, m);
            s.ScanFinished += () => { if (_step == Step.Scan) W.RefreshCurrentIf(this); };
        }

        public override string Key => "wizard";
        public override string Title => Loc.S("Personalizar", "Customize");
        public override string Icon => Icons.Wizard;

        private void GoTo(Step step)
        {
            // Leaving the one-click flow through the normal steps forgets the level.
            if (step != Step.Apply && step != Step.Done) _level = null;
            _step = step;
            Invalidate();
            W.ShowView(View);
            OnShown();
        }

        /// <summary>Self-test only: renders the profile, selection and review steps. Never applies anything.</summary>
        private LevelInfo _level;

        /// <summary>One-click level from Home: the user already read and confirmed the sheet, so go straight to Apply.</summary>
        public void StartLevel(LevelInfo level, IList<Tweak> tweaks, bool restorePoint)
        {
            if (BusyApplying()) return;
            _level = level;
            S.Selected.Clear();
            foreach (var t in tweaks) S.Selected.Add(t.Id);
            S.CreateRestorePoint = restorePoint;
            GoTo(Step.Apply);
        }

        /// <summary>Opens the tweak list, optionally filtered (used by Ctrl+K and by export/import).</summary>
        public void OpenSelection(string query = null)
        {
            if (BusyApplying()) return;
            W.Navigate(Key);
            if (S.ScanResults == null) return;
            _query = query ?? "";
            _category = null;
            GoTo(Step.Select);
        }

        /// <summary>While changes are being applied the selection must not change: show the running apply instead.</summary>
        private bool BusyApplying()
        {
            if (!_applyRunning) return false;
            W.Navigate(Key);
            W.Toast(ToastKind.Info, Loc.S("Espera a que terminen los cambios en curso.", "Wait for the current changes to finish."));
            return true;
        }

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
            if (_step == Step.Scan && S.ScanResults == null && !S.Scanning && S.ScanError == null) _ = ScanAsync();
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

        private Task ScanAsync(bool rescan = false)
        {
            var t = S.ScanAsync(rescan);
            if (_step == Step.Scan) { Invalidate(); W.ShowView(View); }
            return t;
        }

        private FrameworkElement BuildScan()
        {
            if (S.Scanning || (S.ScanResults == null && S.ScanError == null))
            {
                var (view, update) = Ui.Loading(Loc.S("Leyendo la información del equipo…", "Reading PC information…"));
                _scanProgress = update;
                return StepFrame(Loc.S("Analizando tu equipo", "Scanning your PC"),
                    Loc.S("Solo se lee. No se cambia nada hasta el paso Aplicar.", "Read only. Nothing changes until the Apply step."), view, null);
            }
            if (S.ScanError != null)
            {
                return StepFrame(Loc.S("Analizando tu equipo", "Scanning your PC"), null,
                    Ui.ErrorState(Loc.S("No se pudo analizar el equipo.", "The PC could not be scanned."), S.ScanError, () => _ = ScanAsync(true)), null);
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
                Ui.Secondary(Loc.S("Volver a analizar", "Scan again"), () => _ = ScanAsync(true), Icons.Restart),
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
            void SelectSafe()
            {
                foreach (var kv in rows.Where(r => r.Value.Visibility == Visibility.Visible && r.Key.Risk == Risk.Safe && r.Value.CanSelect))
                    kv.Value.SetChecked(true);
            }
            void ClearVisible()
            {
                foreach (var kv in rows.Where(r => r.Value.Visibility == Visibility.Visible)) kv.Value.SetChecked(false);
            }
            // One row: search, the filter that changes the list, and the less frequent actions behind "More actions".
            var more = Ui.MenuButton(Loc.S("Más acciones", "More actions"), Icons.More,
                (Loc.S("Marcar los seguros visibles", "Tick visible safe ones"), Icons.CheckCircle, (Action)SelectSafe),
                (Loc.S("Desmarcar visibles", "Untick visible"), Icons.Close, ClearVisible),
                (Loc.S("Exportar la selección…", "Export the selection…"), Icons.Export, ExportSelection),
                (Loc.S("Importar una selección…", "Import a selection…"), Icons.Import, ImportSelection));
            var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            // The search box takes whatever the filter and the menu leave free.
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = 180 });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.Children.Add(search);
            hideNa.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(hideNa, 1);
            toolbar.Children.Add(hideNa);
            more.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(more, 2);
            toolbar.Children.Add(more);

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

        private static string RiskCounts(int moderate, int advanced)
        {
            var parts = new List<string>();
            if (moderate > 0) parts.Add(moderate + (moderate == 1 ? Loc.S(" cambio de riesgo moderado", " moderate-risk change") : Loc.S(" cambios de riesgo moderado", " moderate-risk changes")));
            if (advanced > 0) parts.Add(advanced + (advanced == 1 ? Loc.S(" cambio avanzado", " advanced change") : Loc.S(" cambios avanzados", " advanced changes")));
            return string.Join(Loc.S(" y ", " and "), parts);
        }

        private void ExportSelection()
        {
            if (S.Selected.Count == 0) { W.Toast(ToastKind.Info, Loc.S("Marca al menos un ajuste para exportar.", "Tick at least one tweak to export.")); return; }
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "Optimizzzer-" + DateTime.Now.ToString("yyyy-MM-dd") + SelectionFile.Extension,
                Filter = Loc.S("Selección de Optimizzzer", "Optimizzzer selection") + " (*" + SelectionFile.Extension + ")|*" + SelectionFile.Extension,
            };
            if (dlg.ShowDialog(W) != true) return;
            try
            {
                System.IO.File.WriteAllText(dlg.FileName, SelectionFile.Write(S.Selected, DateTime.UtcNow), new System.Text.UTF8Encoding(false));
                W.Toast(ToastKind.Success, S.Selected.Count + Loc.S(" ajustes exportados. Ábrelo en otro PC con Importar.", " tweaks exported. Open it on another PC with Import."));
            }
            catch (Exception ex) { W.ShowError(ex.Message, Loc.S("No se pudo guardar el archivo. Prueba en otra carpeta.", "The file could not be saved. Try another folder.")); }
        }

        private void ImportSelection()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = Loc.S("Selección de Optimizzzer", "Optimizzzer selection") + " (*" + SelectionFile.Extension + ")|*" + SelectionFile.Extension,
            };
            if (dlg.ShowDialog(W) != true) return;
            try
            {
                var (known, unknown) = SelectionFile.Read(System.IO.File.ReadAllText(dlg.FileName), S.Catalog);
                int added = 0, skipped = 0;
                S.Selected.Clear();
                foreach (var t in known)
                {
                    var st = S.StatusOf(t);
                    if (st == TweakStatus.NotApplied || st == TweakStatus.Partial) { S.Selected.Add(t.Id); added++; } else skipped++;
                }
                _query = ""; _category = null;
                GoTo(Step.Select);
                W.Toast(added > 0 ? ToastKind.Success : ToastKind.Info,
                    added + Loc.S(" ajustes marcados.", " tweaks ticked.")
                    + (skipped > 0 ? " " + skipped + Loc.S(" ya estaban aplicados o no aplican a este equipo.", " were already applied or do not apply to this PC.") : "")
                    + (unknown.Count > 0 ? " " + unknown.Count + Loc.S(" no existen en esta versión.", " do not exist in this version.") : ""));
            }
            catch (System.IO.InvalidDataException) { W.ShowError(dlg.FileName, Loc.S("Ese archivo no es una selección de Optimizzzer.", "That file is not an Optimizzzer selection.")); }
            catch (Exception ex) { W.ShowError(ex.Message, Loc.S("No se pudo leer el archivo.", "The file could not be read.")); }
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
                moderate + advanced > 0 ? Ui.Notice(Icons.Warning, "Moderate", Loc.S("Incluye ", "Includes ") + RiskCounts(moderate, advanced) + Loc.S(". Lee su inconveniente antes de seguir.", ". Read their downside before continuing.")) : null,
                permanent > 0 ? Ui.Notice(Icons.Error, "Advanced", (permanent == 1
                    ? Loc.S("1 cambio es una desinstalación y no se puede deshacer desde Optimizzzer.", "1 change is an uninstall and cannot be undone from Optimizzzer.")
                    : permanent + Loc.S(" cambios son desinstalaciones y no se pueden deshacer desde Optimizzzer.", " changes are uninstalls and cannot be undone from Optimizzzer."))
                    + Loc.S(" Las apps se recuperan desde la Microsoft Store; OneDrive, desde la web de Microsoft.", " Apps come back from the Microsoft Store; OneDrive from Microsoft's website.")) : null,
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
        // Kept outside the view: the page can be rebuilt mid-apply (language, theme, undo elsewhere) and must show the same progress.
        private double _applyFraction;
        private string _applyStatus;
        private readonly List<(Tweak Tweak, JournalEntry Entry)> _applyLog = new List<(Tweak, JournalEntry)>();
        /// <summary>Tweaks that were selected but not attempted because the user pressed Stop.</summary>
        private int _notAttempted;
        private bool _applyRunning;

        private async Task RunApplyAsync()
        {
            if (_applyRunning) return;
            _applyRunning = true;
            var tweaks = SelectedTweaks();
            _cts = new CancellationTokenSource();
            _applyLog.Clear();
            _applyFraction = 0;
            _applyStatus = null;
            // Created on the UI thread, so its callbacks run on the UI thread.
            var progress = new Progress<ApplyProgress>(p =>
            {
                _applyFraction = p.Done / (double)Math.Max(1, p.Total);
                if (p.LastEntry == null) _applyStatus = Loc.S("Aplicando: ", "Applying: ") + Loc.S(p.Current.Title) + " (" + (p.Done + 1) + Loc.S(" de ", " of ") + p.Total + ")";
                _applyUi.Update?.Invoke(_applyFraction, p.LastEntry == null ? _applyStatus : null);
                if (p.LastEntry != null)
                {
                    _applyLog.Add((p.Current, p.LastEntry));
                    _applyUi.Log?.Children.Add(LogLine(p.Current, p.LastEntry));
                }
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
                // After Stop, keep the ones that never ran selected, so they are one click away.
                var attempted = new HashSet<string>(S.LastSession.Entries.Select(e => e.TweakId));
                _notAttempted = tweaks.Count(t => !attempted.Contains(t.Id));
                S.Selected.Clear();
                foreach (var t in tweaks.Where(t => !attempted.Contains(t.Id))) S.Selected.Add(t.Id);
                W.InvalidateScanViews(except: this);
                GoTo(Step.Done);
                ToastResult(S.LastSession);
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
            var stop = Ui.Secondary(Loc.S("Detener tras el cambio actual", "Stop after the current change"), () => _cts?.Cancel(), Icons.Stop);
            if (_applyRunning)
            {
                if (_applyStatus != null) update(_applyFraction, _applyStatus);
                foreach (var (t, e) in _applyLog) log.Children.Add(LogLine(t, e));
            }

            _applyUi = (update, log);
            return StepFrame(_level != null ? Loc.S("Aplicando el nivel " + _level.Name.Es, "Applying the " + _level.Name.En + " level") : Loc.S("Aplicando cambios", "Applying changes"),
                Loc.S("No apagues el equipo. Cada cambio que falle se revierte solo.", "Do not turn off the PC. Any change that fails is rolled back automatically."),
                Ui.Stack(0, loading, log), Footer(stop, null));
        }

        private static FrameworkElement LogLine(Tweak t, JournalEntry e)
        {
            switch (e.ResultKind)
            {
                case EntryResult.Applied: return Ui.Notice(Icons.CheckCircle, "Success", Loc.S(t.Title));
                case EntryResult.Skipped: return Ui.Notice(Icons.Info, "FgMuted", Loc.S(t.Title) + Loc.S(" (ya estaba aplicado)", " (already applied)"));
                case EntryResult.Blocked: return Ui.Notice(Icons.Lock, "Moderate", Loc.S(t.Title) + Loc.S(": Windows protege este ajuste en este equipo. No se cambió nada; puedes hacerlo desde Configuración de Windows.", ": Windows protects this setting on this PC. Nothing was changed; you can do it from Windows Settings."));
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
            int blocked = s.Entries.Count(e => e.ResultKind == EntryResult.Blocked);
            int skipped = s.Entries.Count(e => e.ResultKind == EntryResult.Skipped);
            int notAttempted = _notAttempted;
            var applied = s.Entries.Where(e => e.ResultKind == EntryResult.Applied).Select(e => S.Catalog.FirstOrDefault(t => t.Id == e.TweakId)).Where(t => t != null).ToList();
            var restart = applied.Select(t => t.Restart).DefaultIfEmpty(RestartKind.None).Max();

            var summary = Ui.Stack(8,
                Ui.Notice(Icons.CheckCircle, "Success", ok + Loc.S(" cambios aplicados.", " changes applied.")),
                skipped > 0 ? Ui.Notice(Icons.Info, "FgMuted", skipped + Loc.S(" ya estaban aplicados.", " were already applied.")) : null,
                blocked > 0 ? Ui.Notice(Icons.Lock, "Moderate", blocked + Loc.S(" no se aplicaron porque Windows protege ese ajuste en este equipo. No se volverán a ofrecer aquí; puedes cambiarlos desde Configuración de Windows.", " were not applied because Windows protects that setting on this PC. They will not be offered here again; you can change them from Windows Settings.")) : null,
                notAttempted > 0 ? Ui.Notice(Icons.Stop, "Moderate", notAttempted + Loc.S(" no se aplicaron porque detuviste el proceso. Siguen seleccionados en el paso Selección.", " were not applied because you stopped. They are still selected in the Selection step.")) : null,
                failed > 0 ? Ui.Notice(Icons.Error, "Advanced", failed + Loc.S(" fallaron y se revirtieron. Mira el detalle en Historial.", " failed and were rolled back. See details in History.")) : null,
                s.RestorePointCreated
                    ? Ui.Notice(Icons.Shield, "Success", Loc.S("Punto de restauración creado.", "Restore point created."))
                    : S.CreateRestorePoint ? Ui.Notice(Icons.Warning, "Moderate", Loc.S("No se pudo crear el punto de restauración: ", "The restore point could not be created: ") + s.RestorePointError + Loc.S(". Los cambios se pueden deshacer igualmente desde Historial.", ". Changes can still be undone from History.")) : null);

            var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            void Add(Button b) { b.Margin = new Thickness(0, 0, 8, 8); actions.Children.Add(b); }
            if (restart == RestartKind.Explorer || applied.Any(t => t.Restart == RestartKind.Explorer))
                Add(Ui.Secondary(Loc.S("Reiniciar el Explorador ahora", "Restart File Explorer now"), RestartExplorerAsync, Icons.Restart));
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

            var home = Ui.Secondary(Loc.S("Volver a Inicio", "Back to Home"), () => { _level = null; _step = Step.Scan; Invalidate(); W.Navigate("home"); }, Icons.Home);
            var footer = Footer(home, Ui.Primary(Loc.S("Empezar otra optimización", "Start another optimization"), () =>
            {
                _profileChoice = null; _manualChoice = false;
                GoTo(Step.Scan);
            }));

            // One animated mark at the end: the clearest possible "it worked" (or "look here").
            var mark = Ui.Icon(failed == 0 ? Icons.CheckCircle : Icons.Warning, 48, failed == 0 ? "Success" : "Moderate");
            mark.HorizontalAlignment = HorizontalAlignment.Left;
            mark.Margin = new Thickness(0, 0, 0, 12);
            mark.Loaded += (_, __) => Theme.Pop(mark);
            string heading = failed == 0
                ? (_level != null ? Loc.S("Nivel " + _level.Name.Es + " aplicado", _level.Name.En + " level applied") : Loc.S("Listo", "Done"))
                : Loc.S("Terminado con avisos", "Finished with warnings");
            return StepFrame(heading, null, Ui.Stack(0, mark, summary, actions, measures), footer);
        }

        private async void RestartExplorerAsync()
        {
            try
            {
                await Task.Run(() => S.Win.RestartExplorer());
                W.Toast(ToastKind.Success, Loc.S("Explorador reiniciado: la barra de tareas y el menú Inicio ya muestran los cambios.", "File Explorer restarted: the taskbar and Start now show the changes."));
            }
            catch (Exception ex)
            {
                W.ShowError(ex.Message, Loc.S("Cierra sesión y vuelve a entrar para ver los cambios.", "Sign out and back in to see the changes."));
            }
        }

        /// <summary>Shown once when an apply ends, never when the result screen is redrawn later.</summary>
        private void ToastResult(JournalSession s)
        {
            int ok = s.Entries.Count(e => e.ResultKind == EntryResult.Applied);
            int failed = s.Entries.Count(e => e.ResultKind == EntryResult.Failed);
            var restart = s.Entries.Where(e => e.ResultKind == EntryResult.Applied)
                .Select(e => S.Catalog.FirstOrDefault(t => t.Id == e.TweakId)).Where(t => t != null)
                .Select(t => t.Restart).DefaultIfEmpty(RestartKind.None).Max();
            if (failed == 0) W.Toast(ToastKind.Success, ok + Loc.S(" cambios aplicados.", " changes applied.") + (restart >= RestartKind.SignOut ? " " + Loc.S("Reinicia para verlos todos.", "Restart to see them all.") : ""));
            else W.Toast(ToastKind.Error, failed + Loc.S(" cambios fallaron y se revirtieron solos.", " changes failed and were rolled back."), Loc.S("Ver en Historial", "See in History"), () => W.Navigate("history"));
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
