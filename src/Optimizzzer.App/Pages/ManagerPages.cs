using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;
using Optimizzzer.Windows;

namespace Optimizzzer.App.Pages
{
    public sealed class StartupPage : ListPage<StartupItem>
    {
        public StartupPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "startup";
        public override string Title => Loc.S("Programas de inicio", "Startup programs");
        public override string Icon => Icons.Startup;
        protected override string Intro => Loc.S("Programas que se abren solos al iniciar sesión. Desactivarlos no los desinstala; puedes volver a activarlos aquí o en el Administrador de tareas. Algunos programas arrancan con una tarea programada: esos están en Tareas programadas, con el filtro de otros programas.",
            "Programs that open on their own when you sign in. Turning them off does not uninstall them; you can turn them back on here or in Task Manager.");
        protected override string SearchHint => Loc.S("Buscar programa", "Search program");
        protected override string EmptyText => Loc.S("No hay programas de inicio.", "There are no startup programs.");
        protected override IList<StartupItem> Load() => S.Win.Startup.List();
        protected override bool Matches(StartupItem i, string q) => Has(i.Name, q) || Has(i.Description, q) || Has(i.Publisher, q) || Has(i.Command, q);

        protected override FrameworkElement RowFor(StartupItem i)
        {
            var sw = new CheckBox { IsChecked = i.Enabled, VerticalAlignment = VerticalAlignment.Top };
            sw.SetResourceReference(FrameworkElement.StyleProperty, "Switch");
            AutomationProperties.SetName(sw, i.Name);
            var state = Ui.Text(i.Enabled ? Loc.S("Activado", "On") : Loc.S("Desactivado", "Off"), 12, true, i.Enabled ? "Success" : "FgMuted", wrap: false);
            sw.Click += (_, __) =>
            {
                var on = sw.IsChecked == true;
                try
                {
                    S.Win.Startup.SetEnabled(i, on);
                    state.Text = on ? Loc.S("Activado", "On") : Loc.S("Desactivado", "Off");
                    state.SetResourceReference(TextBlock.ForegroundProperty, on ? "Success" : "FgMuted");
                }
                catch (Exception ex) { sw.IsChecked = !on; W.ShowError(ex.Message); }
            };
            var who = string.Join(" · ", new[] { i.Description, i.Publisher, i.ForAllUsers ? Loc.S("Todos los usuarios", "All users") : Loc.S("Solo tu usuario", "Your user only") }.Where(x => !string.IsNullOrEmpty(x)).Distinct());
            string origin;
            switch (i.Source)
            {
                case StartupSource.UserRun: origin = Loc.S("Registro, solo tu usuario", "Registry, your user only"); break;
                case StartupSource.MachineRun: origin = Loc.S("Registro, todos los usuarios", "Registry, all users"); break;
                case StartupSource.MachineRun32: origin = Loc.S("Registro de 32 bits, todos los usuarios", "32-bit registry, all users"); break;
                case StartupSource.UserFolder: origin = Loc.S("Carpeta Inicio, solo tu usuario", "Startup folder, your user only"); break;
                default: origin = Loc.S("Carpeta Inicio, todos los usuarios", "Startup folder, all users"); break;
            }
            var info = Ui.Stack(2, Ui.Row(8, Ui.Text(i.Name, 14, true), Ui.Chip(Icons.Startup, Loc.S("Arranca desde: ", "Starts from: ") + origin)), Ui.Muted(who), Ui.Muted(i.Command, 12));
            var right = Ui.Row(12, state, sw);
            state.VerticalAlignment = VerticalAlignment.Center;
            return CleanupPage.Row(Ui.Icon(Icons.Startup, 16, "FgMuted"), info, right);
        }
    }

    public sealed class ServicesPage : ListPage<ServiceInfo>
    {
        private bool _showProtected;
        private enum Show { All, ThirdParty, Mine }
        private Show _show = Show.All;
        // Service name -> the open journal id of the start type I set by hand (for "Changed by me" and "Back to original").
        private Dictionary<string, string> _mine = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public ServicesPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "services";
        public override string Title => Loc.S("Servicios", "Services");
        public override string Icon => Icons.Services;
        protected override string Intro => Loc.S("Cambia cómo arranca cada servicio. Los servicios esenciales están bloqueados. Cada cambio queda en Historial y se puede deshacer.",
            "Change how each service starts. Essential services are locked. Every change is saved in History and can be undone.");
        protected override string SearchHint => Loc.S("Buscar servicio", "Search service");
        protected override IList<ServiceInfo> Load()
        {
            S.EnsureReadyAsync().GetAwaiter().GetResult();
            _mine = S.Engine.History()
                .Where(h => h.Entry.ResultKind == EntryResult.Applied && !h.Entry.Reverted && h.Entry.TweakId.StartsWith(ManualTweaks.ServicePrefix, StringComparison.Ordinal))
                .GroupBy(h => h.Entry.TweakId.Split('|')[1], StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Entry.TweakId, StringComparer.OrdinalIgnoreCase);
            return ServiceList.Load(S.Win.Services);
        }
        protected override bool Matches(ServiceInfo i, string q) => Has(i.Name, q) || Has(i.DisplayName, q) || Has(i.Description, q) || Has(i.ImagePath, q);
        protected override IEnumerable<ServiceInfo> Filtered(IEnumerable<ServiceInfo> items)
        {
            var shown = _showProtected ? items : items.Where(i => !i.Protected);
            if (_show == Show.ThirdParty) shown = shown.Where(i => i.IsThirdParty);
            if (_show == Show.Mine) shown = items.Where(i => _mine.ContainsKey(i.Name));
            return shown;
        }

        protected override FrameworkElement Toolbar()
        {
            var seg = new StackPanel { Orientation = Orientation.Horizontal };
            AutomationProperties.SetName(seg, Loc.S("Mostrar", "Show"));
            foreach (var (v, label) in new[] { (Show.All, Loc.S("Todos", "All")), (Show.ThirdParty, Loc.S("De otros programas", "From other programs")), (Show.Mine, Loc.S("Cambiados por mí", "Changed by me") + " (" + _mine.Count + ")") })
            {
                var rb = new RadioButton { GroupName = "svcShow", Content = label, IsChecked = _show == v, Margin = new Thickness(0, 0, 4, 0) };
                rb.SetResourceReference(FrameworkElement.StyleProperty, "Segment");
                var val = v;
                rb.Checked += (_, __) => { _show = val; W.RefreshCurrentIf(this); };
                seg.Children.Add(rb);
            }
            var cb = new CheckBox { Content = Loc.S("Mostrar también los esenciales (bloqueados)", "Also show essential ones (locked)"), IsChecked = _showProtected, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            cb.Checked += (_, __) => { _showProtected = true; Refill(); };
            cb.Unchecked += (_, __) => { _showProtected = false; Refill(); };
            return _show == Show.Mine ? (FrameworkElement)seg : Ui.Row(0, seg, cb);
        }

        protected override Button PrimaryAction()
        {
            if (_show != Show.Mine || _mine.Count == 0) return null;
            return Ui.Primary(Loc.S("Volver al inicio original de los ", "Restore the original start of the ") + _mine.Count + Loc.S(" servicios", " services"), async () =>
            {
                if (!await W.Confirm(Loc.S("¿Volver al original?", "Restore the originals?"),
                    Loc.S("Cada servicio que cambiaste aquí vuelve al tipo de inicio que tenía antes, y se vuelve a iniciar si estaba en marcha.", "Every service you changed here goes back to the start type it had, and starts again if it was running."),
                    Loc.S("Volver al original", "Restore originals"))) return;
                var catalog = S.UndoCatalog();
                var ids = _mine.Values.ToList();
                var failed = await Task.Run(() => ids.Where(id => S.Engine.Undo(id, catalog).Failed.Count > 0).ToList());
                if (failed.Count == 0) W.Toast(ToastKind.Success, Loc.S("Servicios devueltos a su inicio original.", "Services restored to their original start."));
                else W.ShowError(string.Join(", ", failed), Loc.S("Algunos servicios no se pudieron devolver. Siguen en Historial para reintentarlo.", "Some services could not be restored. They stay in History to retry."));
                _ = ReloadAsync();
            }, Icons.Undo);
        }

        protected override FrameworkElement RowFor(ServiceInfo i)
        {
            var info = Ui.Stack(2,
                Ui.Row(10, Ui.Text(i.DisplayName, 14, true), Ui.Text(i.Running ? Loc.S("En ejecución", "Running") : Loc.S("Detenido", "Stopped"), 12, false, i.Running ? "Success" : "FgMuted", wrap: false),
                    _mine.ContainsKey(i.Name) ? Ui.Chip(Icons.History, Loc.S("Cambiado por ti", "Changed by you"), "AccentText") : null,
                    i.IsThirdParty ? Ui.Chip(Icons.Apps, Loc.S("De otro programa", "From another program")) : null),
                Ui.Clamped(i.Name + (string.IsNullOrEmpty(i.Description) ? "" : " · " + i.Description)));
            FrameworkElement right;
            if (i.Protected)
            {
                right = Ui.Row(6, Ui.Icon(Icons.Lock, 14, "FgMuted"), Ui.Muted(Loc.S("Esencial", "Essential"), 12));
                AutomationProperties.SetName(right, Loc.S("Bloqueado: servicio esencial", "Locked: essential service"));
            }
            else
            {
                var seg = new StackPanel { Orientation = Orientation.Horizontal };
                AutomationProperties.SetName(seg, Loc.S("Tipo de inicio de ", "Startup type of ") + i.DisplayName);
                foreach (var mode in new[] { StartMode.Automatic, StartMode.Manual, StartMode.Disabled })
                {
                    var current = i.StartMode == mode || (mode == StartMode.Automatic && i.StartMode == StartMode.AutomaticDelayed);
                    var rb = new RadioButton { Content = ServiceStartAction.ModeName(mode, Loc.Lang), GroupName = "svc-" + i.Name, IsChecked = current };
                    rb.SetResourceReference(FrameworkElement.StyleProperty, "Segment");
                    rb.Click += async (_, __) =>
                    {
                        if (current) return;
                        if (mode == StartMode.Disabled && !await W.Confirm(Loc.S("¿Deshabilitar ", "Disable ") + i.DisplayName + "?",
                            Loc.S("Si algo deja de funcionar, deshaz el cambio desde Historial o vuelve a ponerlo en Manual aquí.", "If something stops working, undo it from History or set it back to Manual here."),
                            Loc.S("Deshabilitar", "Disable"), danger: true))
                        { Refill(); return; }
                        // The row only changes when Windows accepted the new mode.
                        var change = ManualTweaks.Service(i.Name, i.DisplayName, mode);
                        if (await ApplyManual(change))
                        {
                            i.StartMode = mode;
                            _mine[i.Name] = change.Id;
                            W.Toast(ToastKind.Success, i.DisplayName + ": " + ServiceStartAction.ModeName(mode, Loc.Lang));
                        }
                        Refill();
                    };
                    seg.Children.Add(rb);
                }
                right = seg;
            }
            return CleanupPage.Row(Ui.Icon(Icons.Services, 16, "FgMuted"), info, right);
        }

        private async Task<bool> ApplyManual(Tweak t)
        {
            try
            {
                await S.EnsureReadyAsync();
                var session = await Task.Run(() => S.Engine.Apply(new[] { t }, new ApplyOptions { CreateRestorePoint = false }));
                var e = session.Entries.FirstOrDefault();
                if (e != null && e.ResultKind == EntryResult.Failed) { W.ShowError(e.Error); return false; }
                return true;
            }
            catch (Exception ex) { W.ShowError(ex.Message); return false; }
        }
    }

    public sealed class TasksPage : ListPage<ScheduledTaskInfo>
    {
        private bool _onlyMicrosoft = true;
        public TasksPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "tasks";
        public override string Title => Loc.S("Tareas programadas", "Scheduled tasks");
        public override string Icon => Icons.Tasks;
        protected override string Intro => Loc.S("Tareas que Windows y los programas ejecutan solos. Cada cambio queda en Historial y se puede deshacer.",
            "Tasks Windows and programs run on their own. Every change is saved in History and can be undone.");
        protected override string SearchHint => Loc.S("Buscar tarea", "Search task");
        protected override IList<ScheduledTaskInfo> Load() => S.Win.Tasks.List();
        protected override bool Matches(ScheduledTaskInfo i, string q) => Has(i.Path, q) || Has(i.Description, q) || Has(i.Author, q);
        protected override IEnumerable<ScheduledTaskInfo> Filtered(IEnumerable<ScheduledTaskInfo> items) => _onlyMicrosoft ? items.Where(i => !i.IsMicrosoft) : items;

        protected override FrameworkElement Toolbar()
        {
            var cb = new CheckBox { Content = Loc.S("Mostrar también las de Windows (si no, solo las de otros programas, como actualizadores)", "Also show Windows tasks (otherwise only other programs', such as updaters)"), IsChecked = !_onlyMicrosoft };
            cb.Checked += (_, __) => { _onlyMicrosoft = false; Refill(); };
            cb.Unchecked += (_, __) => { _onlyMicrosoft = true; Refill(); };
            return cb;
        }

        private readonly HashSet<string> _picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Button _disableMany;

        protected override Button PrimaryAction()
        {
            _disableMany = Ui.Primary("", async () =>
            {
                var paths = _picked.ToList();
                if (!await W.Confirm(Loc.S("¿Desactivar ", "Disable ") + paths.Count + Loc.S(" tareas?", " tasks?"),
                    Loc.S("No se borran: dejan de ejecutarse solas. Puedes volver a activarlas aquí o deshacerlo desde Historial.", "They are not deleted: they stop running on their own. You can turn them back on here or undo from History."),
                    Loc.S("Desactivar", "Disable"))) return;
                try
                {
                    await S.EnsureReadyAsync();
                    var tweaks = paths.Select(p => ManualTweaks.Task(p, false)).ToList();
                    var s = await Task.Run(() => S.Engine.Apply(tweaks, new ApplyOptions { CreateRestorePoint = false }));
                    var bad = s.Entries.Where(e => e.ResultKind == EntryResult.Failed).ToList();
                    if (bad.Count > 0) W.ShowError(string.Join("\n", bad.Select(e => e.TweakId + ": " + e.Error)));
                    else W.Toast(ToastKind.Success, paths.Count + Loc.S(" tareas desactivadas.", " tasks disabled."));
                }
                catch (Exception ex) { W.ShowError(ex.Message); }
                _picked.Clear();
                _ = ReloadAsync();
            }, Icons.Stop);
            UpdateMany();
            return _disableMany;
        }

        private void UpdateMany()
        {
            if (_disableMany == null) return;
            _disableMany.Content = _picked.Count == 0 ? Loc.S("Marca tareas para desactivarlas juntas", "Tick tasks to disable them together") : Loc.S("Desactivar ", "Disable ") + _picked.Count + Loc.S(" tareas", " tasks");
            _disableMany.IsEnabled = _picked.Count > 0;
            AutomationProperties.SetName(_disableMany, _disableMany.Content.ToString());
        }

        protected override FrameworkElement RowFor(ScheduledTaskInfo i)
        {
            var pick = new CheckBox { IsChecked = _picked.Contains(i.Path), IsEnabled = i.Enabled, VerticalAlignment = VerticalAlignment.Top, ToolTip = Loc.S("Elegir para desactivar junto con otras", "Pick to disable together with others") };
            AutomationProperties.SetName(pick, Loc.S("Elegir ", "Pick ") + i.Path);
            pick.Checked += (_, __) => { _picked.Add(i.Path); UpdateMany(); };
            pick.Unchecked += (_, __) => { _picked.Remove(i.Path); UpdateMany(); };
            var sw = new CheckBox { IsChecked = i.Enabled };
            sw.SetResourceReference(FrameworkElement.StyleProperty, "Switch");
            AutomationProperties.SetName(sw, i.Path);
            sw.Click += async (_, __) =>
            {
                var on = sw.IsChecked == true;
                var t = ManualTweaks.Task(i.Path, on);
                try
                {
                    await S.EnsureReadyAsync();
                    var s = await Task.Run(() => S.Engine.Apply(new[] { t }, new ApplyOptions { CreateRestorePoint = false }));
                    if (s.Entries.Any(e => e.ResultKind == EntryResult.Failed)) { sw.IsChecked = !on; W.ShowError(s.Entries[0].Error); }
                    else i.Enabled = on;
                }
                catch (Exception ex) { sw.IsChecked = !on; W.ShowError(ex.Message); }
            };
            var last = i.LastRun.HasValue ? Loc.S("Última ejecución: ", "Last run: ") + i.LastRun.Value.ToString("g") : Loc.S("Nunca se ha ejecutado", "Never run");
            var info = Ui.Stack(2, Ui.Text(i.Name, 14, true), Ui.Muted(i.Folder + " · " + last, 12),
                string.IsNullOrWhiteSpace(i.Description) ? null : Ui.Clamped(i.Description));
            return CleanupPage.Row(pick, info, sw);
        }
    }

    /// <summary>One row of "Programs and apps": a classic program or game, or a Microsoft Store app.</summary>
    public sealed class AppEntry
    {
        public InstalledProgram Program { get; set; }
        public AppxInfo Store { get; set; }
        public string Name => Program?.Name ?? Store?.Name;
    }

    /// <summary>
    /// Every program, game and Store app on the PC. Programs and games can be uninstalled in batches, and what
    /// their uninstallers leave behind is found and removed (to the Recycle Bin, with registry backups).
    /// </summary>
    public sealed class AppsPage : ListPage<AppEntry>
    {
        private enum Tab { Programs, Games, Store }
        private Tab _tab = Tab.Programs;
        private bool _showSystem;
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Button _primary;

        public AppsPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "apps";
        public override string Title => Loc.S("Programas y apps", "Programs and apps");
        public override string Icon => Icons.Apps;
        protected override string Intro => Loc.S("Todo lo instalado: programas, juegos y apps de la Store. Marca varios y desinstálalos de una vez; después se buscan los restos que dejaron.",
            "Everything installed: programs, games and Store apps. Tick several and uninstall them at once; then what they left behind is found.");
        protected override string SearchHint => Loc.S("Buscar por nombre o editor", "Search by name or publisher");
        protected override string EmptyText => _tab == Tab.Games
            ? Loc.S("No se encontraron juegos de Steam, Epic, Riot, EA, Ubisoft, Battle.net, GOG o Xbox.", "No games from Steam, Epic, Riot, EA, Ubisoft, Battle.net, GOG or Xbox were found.")
            : Loc.S("No hay elementos.", "There are no items.");

        protected override IList<AppEntry> Load()
        {
            var list = S.Win.Programs.List().Select(p => new AppEntry { Program = p }).ToList();
            try { list.AddRange(S.Win.Appx.List().Where(a => !a.IsFramework).Select(a => new AppEntry { Store = a })); }
            catch (Exception) { } // Store apps need PowerShell; programs still show without them.
            _selected.RemoveWhere(k => !list.Any(e => e.Program?.Key == k));
            return list;
        }

        protected override bool Matches(AppEntry i, string q) =>
            Has(i.Name, q) || Has(i.Program?.Publisher, q) || Has(i.Store?.Publisher, q) || Has(i.Program?.Platform, q);

        protected override IEnumerable<AppEntry> Filtered(IEnumerable<AppEntry> items)
        {
            switch (_tab)
            {
                case Tab.Games: return items.Where(e => e.Program != null && e.Program.IsGame);
                case Tab.Store: return items.Where(e => e.Store != null && (_showSystem || (!e.Store.IsSystem && !e.Store.NonRemovable)));
                default: return items.Where(e => e.Program != null && !e.Program.IsGame && (_showSystem || !ProgramClassifier.IsSystemLike(e.Program)));
            }
        }

        protected override FrameworkElement Toolbar()
        {
            int Count(Tab t) => Items == null ? 0 : Filtered2(t).Count();
            var tabs = new StackPanel { Orientation = Orientation.Horizontal };
            AutomationProperties.SetName(tabs, Loc.S("Tipo", "Type"));
            foreach (var (t, label) in new[] { (Tab.Programs, Loc.S("Programas", "Programs")), (Tab.Games, Loc.S("Juegos", "Games")), (Tab.Store, Loc.S("Apps de la Store", "Store apps")) })
            {
                var rb = new RadioButton { GroupName = "appsTab", Content = label + " (" + Count(t) + ")", IsChecked = _tab == t, Margin = new Thickness(0, 0, 4, 0) };
                rb.SetResourceReference(FrameworkElement.StyleProperty, "Segment");
                var tab = t;
                rb.Checked += (_, __) => { _tab = tab; W.RefreshCurrentIf(this); };
                tabs.Children.Add(rb);
            }
            var cb = new CheckBox
            {
                Content = _tab == Tab.Store ? Loc.S("Mostrar también las del sistema (bloqueadas)", "Also show system apps (locked)") : Loc.S("Mostrar también componentes y actualizaciones", "Also show components and updates"),
                IsChecked = _showSystem, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            };
            cb.Checked += (_, __) => { _showSystem = true; Refill(); };
            cb.Unchecked += (_, __) => { _showSystem = false; Refill(); };
            if (_tab == Tab.Games) return tabs;
            return Ui.Row(0, tabs, cb);
        }

        private IEnumerable<AppEntry> Filtered2(Tab t)
        {
            var keep = _tab; _tab = t;
            var r = Filtered(Items).ToList();
            _tab = keep;
            return r;
        }

        protected override Button PrimaryAction()
        {
            if (_tab == Tab.Store) return null;
            _primary = Ui.Primary("", () => _ = UninstallSelectedAsync(), Icons.Cleanup);
            UpdatePrimary();
            return _primary;
        }

        private void UpdatePrimary()
        {
            if (_primary == null) return;
            int n = _selected.Count;
            var text = n == 0 ? Loc.S("Marca programas para desinstalar", "Tick programs to uninstall")
                : n == 1 ? Loc.S("Desinstalar 1 programa", "Uninstall 1 program") : Loc.S("Desinstalar ", "Uninstall ") + n + Loc.S(" programas", " programs");
            _primary.Content = text;
            _primary.IsEnabled = n > 0;
            AutomationProperties.SetName(_primary, text);
        }

        protected override FrameworkElement RowFor(AppEntry e) => e.Program != null ? ProgramRow(e.Program) : StoreRow(e.Store);

        private FrameworkElement ProgramRow(InstalledProgram p)
        {
            var facts = new[]
            {
                p.Publisher, p.Version,
                p.SizeBytes > 0 ? Loc.Size(p.SizeBytes) : null,
                p.InstallDate.HasValue ? Loc.S("instalado el ", "installed ") + p.InstallDate.Value.ToString("d") : null,
            }.Where(x => !string.IsNullOrWhiteSpace(x));
            var title = Ui.Row(8, Ui.Text(p.Name, 14, true, "Fg", wrap: false), p.Platform != null ? Ui.Chip(Icons.Gaming, p.Platform) : null);
            var info = Ui.Stack(2, title, Ui.Muted(string.Join(" · ", facts), 12));
            var cb = new CheckBox { IsChecked = _selected.Contains(p.Key), VerticalAlignment = VerticalAlignment.Center, Content = info };
            AutomationProperties.SetName(cb, p.Name);
            cb.Checked += (_, __) => { _selected.Add(p.Key); UpdatePrimary(); };
            cb.Unchecked += (_, __) => { _selected.Remove(p.Key); UpdatePrimary(); };
            if (string.IsNullOrWhiteSpace(p.UninstallString) && string.IsNullOrWhiteSpace(p.QuietUninstallString)) cb.IsEnabled = false;
            return CleanupPage.Row(Ui.Icon(p.IsGame ? Icons.Gaming : Icons.Apps, 16, "FgMuted"), cb, null);
        }

        private FrameworkElement StoreRow(AppxInfo i)
        {
            var publisher = i.Publisher?.Split(',').FirstOrDefault(x => x.Trim().StartsWith("CN=", StringComparison.OrdinalIgnoreCase))?.Trim().Substring(3) ?? "";
            var info = Ui.Stack(2, Ui.Text(i.Name, 14, true), Ui.Muted(publisher + " · " + i.Version, 12));
            FrameworkElement right;
            if (i.IsSystem || i.NonRemovable)
            {
                right = Ui.Row(6, Ui.Icon(Icons.Lock, 14, "FgMuted"), Ui.Muted(Loc.S("Parte de Windows", "Part of Windows"), 12));
            }
            else
            {
                var b = Ui.Secondary(Loc.S("Quitar", "Remove"), null, Icons.Cleanup);
                AutomationProperties.SetName(b, Loc.S("Quitar ", "Remove ") + i.Name);
                b.Click += async (_, __) =>
                {
                    if (!await W.Confirm(Loc.S("¿Quitar ", "Remove ") + i.Name + "?",
                        Loc.S("Se desinstala para todos los usuarios. Para recuperarla tendrás que instalarla desde la Microsoft Store.", "It is uninstalled for all users. To get it back you will need to install it from the Microsoft Store."),
                        Loc.S("Quitar", "Remove"), danger: true)) return;
                    b.IsEnabled = false;
                    try
                    {
                        await Task.Run(() => S.Win.Appx.Remove(i.Name));
                        Items.Remove(Items.First(x => x.Store == i));
                        Refill();
                        W.Toast(ToastKind.Success, i.Name + Loc.S(" quitada.", " removed."));
                    }
                    catch (Exception ex) { b.IsEnabled = true; W.ShowError(ex.Message); }
                };
                right = b;
            }
            return CleanupPage.Row(Ui.Icon(Icons.Apps, 16, "FgMuted"), info, right);
        }

        // ---------- Batch uninstall ----------

        private async Task UninstallSelectedAsync()
        {
            var chosen = Items.Where(e => e.Program != null && _selected.Contains(e.Program.Key)).Select(e => e.Program).ToList();
            if (chosen.Count == 0) return;

            // 1. Confirm, saying which ones ask questions and which ones go silently.
            bool restorePoint = true;
            var list = new StackPanel();
            foreach (var p in chosen)
            {
                var (_, _, silent) = UninstallCommand.For(p, true);
                var how = silent ? Loc.S("se quita sin preguntar", "removed without questions") : Loc.S("abrirá su propio desinstalador: respóndelo", "opens its own uninstaller: answer it");
                var launcher = p.IsGame ? Loc.S(" · consejo: también puedes quitarlo desde ", " · tip: you can also remove it from ") + p.Platform : "";
                list.Children.Add(Ui.Notice(p.IsGame ? Icons.Gaming : Icons.Apps, "Fg", p.Name + "  ·  " + how + launcher));
            }
            var rp = new CheckBox { Content = Loc.S("Crear un punto de restauración antes (recomendado)", "Create a restore point first (recommended)"), IsChecked = true };
            rp.Checked += (_, __) => restorePoint = true;
            rp.Unchecked += (_, __) => restorePoint = false;
            var warn = Ui.Notice(Icons.Warning, "Moderate", Loc.S(
                "Desinstalar no se puede deshacer desde aquí: para recuperarlo hay que volver a instalarlo. Los restos que borres después van a la Papelera y las claves del registro se guardan en un archivo .reg.",
                "Uninstalling cannot be undone from here: to get it back you install it again. Leftovers you delete afterwards go to the Recycle Bin, and registry keys are saved to a .reg file."));
            var tcs = new TaskCompletionSource<bool>();
            var go = Ui.Primary(chosen.Count == 1 ? Loc.S("Desinstalar 1 programa", "Uninstall 1 program") : Loc.S("Desinstalar ", "Uninstall ") + chosen.Count + Loc.S(" programas", " programs"), () => { W.CloseOverlay(); tcs.TrySetResult(true); }, Icons.Cleanup);
            go.SetResourceReference(Control.BackgroundProperty, "Advanced");
            go.SetResourceReference(Control.BorderBrushProperty, "Advanced");
            go.SetResourceReference(Control.ForegroundProperty, "Surface");
            var cancel = Ui.Secondary(Loc.S("Cancelar", "Cancel"), () => { W.CloseOverlay(); tcs.TrySetResult(false); });
            var buttons = Ui.Row(8, cancel, go);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            W.ShowOverlay(MainWindow.Sheet(Ui.H2(Loc.S("Desinstalar programas", "Uninstall programs")), Ui.Stack(12, list, warn, rp), buttons), () => { W.CloseOverlay(); tcs.TrySetResult(false); }, 640);
            if (!await tcs.Task) return;

            // 2. Uninstall one by one, showing which one is running.
            var status = Ui.Text("", 14);
            var (loading, update) = Ui.Loading(Loc.S("Preparando…", "Preparing…"));
            var stopRequested = new System.Threading.CancellationTokenSource();
            var stop = Ui.Secondary(Loc.S("Detener tras el actual", "Stop after the current one"), () => stopRequested.Cancel(), Icons.Stop);
            stop.HorizontalAlignment = HorizontalAlignment.Right;
            W.ShowOverlay(MainWindow.Sheet(Ui.H2(Loc.S("Desinstalando", "Uninstalling")), Ui.Stack(12, loading, status,
                Ui.Muted(Loc.S("Si se abre la ventana de un desinstalador, respóndela. Puede estar detrás de esta.", "If an uninstaller window opens, answer it. It may be behind this one."), 12)), stop), () => { }, 560);

            if (restorePoint)
            {
                update(0, Loc.S("Creando el punto de restauración…", "Creating the restore point…"));
                var err = await Task.Run(() => new WinRestorePoint(S.Win.Registry, S.Win.Process).Create("Optimizzzer: " + Loc.S("desinstalar programas", "uninstall programs")));
                if (err != null) status.Text = Loc.S("No se pudo crear el punto de restauración: ", "The restore point could not be created: ") + err;
            }

            var removed = new List<InstalledProgram>();
            var failed = new List<(InstalledProgram P, string Why)>();
            for (int i = 0; i < chosen.Count; i++)
            {
                if (stopRequested.IsCancellationRequested) break;
                var p = chosen[i];
                update(i / (double)chosen.Count, Loc.S("Desinstalando ", "Uninstalling ") + p.Name + " (" + (i + 1) + Loc.S(" de ", " of ") + chosen.Count + ")");
                try
                {
                    var r = await Task.Run(() => S.Win.Programs.Uninstall(p, true, System.Threading.CancellationToken.None));
                    if (r.Removed) removed.Add(p);
                    else failed.Add((p, r.Error ?? Loc.S("el desinstalador terminó pero el programa sigue instalado (¿lo cancelaste?)", "the uninstaller finished but the program is still installed (did you cancel it?)")));
                }
                catch (Exception ex) { failed.Add((p, ex.Message)); }
            }

            // 3. Look for what they left behind.
            update(1, Loc.S("Buscando restos…", "Looking for leftovers…"));
            var leftovers = new List<(InstalledProgram P, Leftover L)>();
            foreach (var p in removed)
            {
                try { foreach (var l in await Task.Run(() => S.Win.Programs.FindLeftovers(p))) leftovers.Add((p, l)); }
                catch (Exception) { }
            }
            W.CloseOverlay();
            foreach (var p in removed) _selected.Remove(p.Key);

            long freed = 0;
            if (leftovers.Count > 0) freed = await CleanLeftoversAsync(leftovers);

            if (removed.Count > 0)
                W.Toast(ToastKind.Success, (removed.Count == 1 ? Loc.S("1 programa desinstalado", "1 program uninstalled") : removed.Count + Loc.S(" programas desinstalados", " programs uninstalled"))
                    + (freed > 0 ? Loc.S(". Restos borrados: ", ". Leftovers removed: ") + Loc.Size(freed) : "") + ".");
            if (failed.Count > 0)
                W.ShowError(string.Join("\n", failed.Select(f => f.P.Name + ": " + f.Why)),
                    Loc.S("Estos programas siguen instalados. Prueba de nuevo, o desinstálalos desde Configuración > Aplicaciones o desde su lanzador.",
                        "These programs are still installed. Try again, or uninstall them from Settings > Apps or from their launcher."));
            _ = ReloadAsync();
        }

        /// <summary>Shows what was found, High confidence ticked and Medium unticked with the reason, then deletes the ticked ones.</summary>
        private async Task<long> CleanLeftoversAsync(List<(InstalledProgram P, Leftover L)> items)
        {
            var ticked = new HashSet<Leftover>(items.Where(x => x.L.Confidence == Confidence.High).Select(x => x.L));
            var body = new StackPanel();
            Button go = null;
            void Update()
            {
                long size = ticked.Sum(l => l.SizeBytes);
                go.Content = Loc.S("Borrar ", "Delete ") + ticked.Count + Loc.S(" restos", " leftovers") + (size > 0 ? " (" + Loc.Size(size) + ")" : "") + Loc.S(" a la Papelera", " to the Recycle Bin");
                go.IsEnabled = ticked.Count > 0;
            }
            foreach (var group in items.GroupBy(x => x.P))
            {
                var head = Ui.Text(group.Key.Name, 14, true);
                head.Margin = new Thickness(0, 8, 0, 4);
                body.Children.Add(head);
                foreach (var (_, l) in group)
                {
                    string kind;
                    switch (l.Kind)
                    {
                        case LeftoverKind.Folder: kind = Loc.S("Carpeta", "Folder"); break;
                        case LeftoverKind.RegistryKey: kind = Loc.S("Registro", "Registry"); break;
                        case LeftoverKind.Shortcut: kind = Loc.S("Acceso directo", "Shortcut"); break;
                        case LeftoverKind.ScheduledTask: kind = Loc.S("Tarea", "Task"); break;
                        default: kind = Loc.S("Servicio", "Service"); break;
                    }
                    var conf = l.Confidence == Confidence.High
                        ? Ui.Chip(Icons.CheckCircle, Loc.S("Seguro que es suyo", "Certainly its own"), "Success")
                        : Ui.Chip(Icons.Warning, Loc.S("Coincide por nombre: revisa antes", "Matched by name: check first"), "Moderate");
                    var info = Ui.Stack(2,
                        Ui.Row(8, Ui.Text(kind + (l.SizeBytes > 0 ? " · " + Loc.Size(l.SizeBytes) : ""), 13, true, "Fg", wrap: false), conf),
                        Ui.Muted(l.Path, 12),
                        Ui.Muted(Loc.S(l.Reason), 12));
                    var cb = new CheckBox { IsChecked = ticked.Contains(l), Content = info };
                    AutomationProperties.SetName(cb, kind + " " + l.Path);
                    var item = l;
                    cb.Checked += (_, __) => { ticked.Add(item); Update(); };
                    cb.Unchecked += (_, __) => { ticked.Remove(item); Update(); };
                    cb.Margin = new Thickness(0, 4, 0, 4);
                    body.Children.Add(cb);
                }
            }
            var tcs = new TaskCompletionSource<bool>();
            go = Ui.Primary("", () => { W.CloseOverlay(); tcs.TrySetResult(true); }, Icons.Cleanup);
            var skip = Ui.Secondary(Loc.S("Dejarlos", "Keep them"), () => { W.CloseOverlay(); tcs.TrySetResult(false); });
            Update();
            var buttons = Ui.Row(8, skip, go);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var intro = Ui.Muted(Loc.S("Esto quedó después de desinstalar. Las carpetas van a la Papelera; las claves del registro se guardan antes en ", "This was left after uninstalling. Folders go to the Recycle Bin; registry keys are saved first to ") + S.Win.Programs.BackupFolder + ".", 12);
            W.ShowOverlay(MainWindow.Sheet(Ui.Stack(4, Ui.H2(Loc.S("Restos encontrados", "Leftovers found")), intro), body, buttons), () => { W.CloseOverlay(); tcs.TrySetResult(false); }, 720);
            if (!await tcs.Task) return 0;

            var results = await Task.Run(() => S.Win.Programs.Delete(ticked.ToList()));
            var bad = results.Where(r => !r.Ok).ToList();
            if (bad.Count > 0)
                W.ShowError(string.Join("\n", bad.Select(r => r.Item.Path + ": " + r.Error)),
                    Loc.S("Algunos restos no se pudieron borrar, normalmente porque un archivo está en uso. Reinicia y vuelve a intentarlo.", "Some leftovers could not be deleted, usually because a file is in use. Restart and try again."));
            return results.Where(r => r.Ok).Sum(r => r.Item.SizeBytes);
        }
    }
}
