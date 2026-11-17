using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;
using Optimizzzer.Windows;

namespace Optimizzzer.App.Pages
{
    public sealed class StartupPage : ListPage<StartupItem>
    {
        public StartupPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "startup";
        public override string Title => Loc.S("Programas de inicio", "Startup programs");
        public override string Icon => Icons.Startup;
        protected override string Intro => Loc.S("Programas que se abren solos al iniciar sesión. Desactivarlos no los desinstala; puedes volver a activarlos aquí o en el Administrador de tareas.",
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
            var info = Ui.Stack(2, Ui.Text(i.Name, 14, true), Ui.Muted(who), Ui.Muted(i.Command, 12));
            var right = Ui.Row(12, state, sw);
            state.VerticalAlignment = VerticalAlignment.Center;
            return CleanupPage.Row(Ui.Icon(Icons.Startup, 16, "FgMuted"), info, right);
        }
    }

    public sealed class ServicesPage : ListPage<ServiceInfo>
    {
        private bool _showProtected;
        public ServicesPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "services";
        public override string Title => Loc.S("Servicios", "Services");
        public override string Icon => Icons.Services;
        protected override string Intro => Loc.S("Cambia cómo arranca cada servicio. Los servicios esenciales están bloqueados. Cada cambio queda en Historial y se puede deshacer.",
            "Change how each service starts. Essential services are locked. Every change is saved in History and can be undone.");
        protected override string SearchHint => Loc.S("Buscar servicio", "Search service");
        protected override IList<ServiceInfo> Load() => ServiceList.Load(S.Win.Services);
        protected override bool Matches(ServiceInfo i, string q) => Has(i.Name, q) || Has(i.DisplayName, q) || Has(i.Description, q);
        protected override IEnumerable<ServiceInfo> Filtered(IEnumerable<ServiceInfo> items) => _showProtected ? items : items.Where(i => !i.Protected);

        protected override FrameworkElement Toolbar()
        {
            var cb = new CheckBox { Content = Loc.S("Mostrar también los esenciales (bloqueados)", "Also show essential ones (locked)"), IsChecked = _showProtected };
            cb.Checked += (_, __) => { _showProtected = true; Refill(); };
            cb.Unchecked += (_, __) => { _showProtected = false; Refill(); };
            return cb;
        }

        protected override FrameworkElement RowFor(ServiceInfo i)
        {
            var info = Ui.Stack(2,
                Ui.Row(10, Ui.Text(i.DisplayName, 14, true), Ui.Text(i.Running ? Loc.S("En ejecución", "Running") : Loc.S("Detenido", "Stopped"), 12, false, i.Running ? "Success" : "FgMuted", wrap: false)),
                Ui.Muted(i.Name + (string.IsNullOrEmpty(i.Description) ? "" : " · " + i.Description), 12));
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
                        await ApplyManual(ManualTweaks.Service(i.Name, i.DisplayName, mode));
                        i.StartMode = mode;
                        Refill();
                    };
                    seg.Children.Add(rb);
                }
                right = seg;
            }
            return CleanupPage.Row(Ui.Icon(Icons.Services, 16, "FgMuted"), info, right);
        }

        private async Task ApplyManual(Tweak t)
        {
            try
            {
                await S.EnsureReadyAsync();
                var session = await Task.Run(() => S.Engine.Apply(new[] { t }, new ApplyOptions { CreateRestorePoint = false }));
                var e = session.Entries.FirstOrDefault();
                if (e != null && e.ResultKind == EntryResult.Failed) W.ShowError(e.Error);
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
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
            var cb = new CheckBox { Content = Loc.S("Mostrar también las de Windows", "Also show Windows tasks"), IsChecked = !_onlyMicrosoft };
            cb.Checked += (_, __) => { _onlyMicrosoft = false; Refill(); };
            cb.Unchecked += (_, __) => { _onlyMicrosoft = true; Refill(); };
            return cb;
        }

        protected override FrameworkElement RowFor(ScheduledTaskInfo i)
        {
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
                string.IsNullOrWhiteSpace(i.Description) ? null : Ui.Muted(i.Description, 12));
            return CleanupPage.Row(Ui.Icon(Icons.Tasks, 16, "FgMuted"), info, sw);
        }
    }

    public sealed class AppsPage : ListPage<AppxInfo>
    {
        private bool _showSystem;
        public AppsPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "apps";
        public override string Title => Loc.S("Apps instaladas", "Installed apps");
        public override string Icon => Icons.Apps;
        protected override string Intro => Loc.S("Apps de la Microsoft Store y de Windows. Quitar una la desinstala para todos los usuarios; se recupera reinstalándola desde la Store.",
            "Microsoft Store and Windows apps. Removing one uninstalls it for all users; you get it back by reinstalling from the Store.");
        protected override string SearchHint => Loc.S("Buscar app", "Search app");
        protected override IList<AppxInfo> Load() => S.Win.Appx.List().Where(a => !a.IsFramework).ToList();
        protected override bool Matches(AppxInfo i, string q) => Has(i.Name, q) || Has(i.Publisher, q);
        protected override IEnumerable<AppxInfo> Filtered(IEnumerable<AppxInfo> items) => _showSystem ? items : items.Where(i => !i.IsSystem && !i.NonRemovable);

        protected override FrameworkElement Toolbar()
        {
            var cb = new CheckBox { Content = Loc.S("Mostrar también las del sistema (bloqueadas)", "Also show system apps (locked)"), IsChecked = _showSystem };
            cb.Checked += (_, __) => { _showSystem = true; Refill(); };
            cb.Unchecked += (_, __) => { _showSystem = false; Refill(); };
            return cb;
        }

        protected override FrameworkElement RowFor(AppxInfo i)
        {
            var publisher = i.Publisher?.Split(',').FirstOrDefault(p => p.Trim().StartsWith("CN=", StringComparison.OrdinalIgnoreCase))?.Trim().Substring(3) ?? "";
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
                        Items.Remove(i);
                        Refill();
                    }
                    catch (Exception ex) { b.IsEnabled = true; W.ShowError(ex.Message); }
                };
                right = b;
            }
            return CleanupPage.Row(Ui.Icon(Icons.Apps, 16, "FgMuted"), info, right);
        }
    }
}
