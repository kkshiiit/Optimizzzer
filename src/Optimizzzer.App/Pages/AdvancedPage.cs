using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Optimizzzer.App.Shell;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.App.Pages
{
    /// <summary>
    /// "At your own risk": removing Copilot, Xbox, Edge, the Store, turning off Defender and pausing Windows Update for
    /// years. Kept apart from levels and Customize on purpose: none of this is an optimization with evidence, it is a
    /// personal choice. A one-time notice must be accepted first, and every action can be saved as a .ps1 instead.
    /// </summary>
    public sealed class AdvancedPage : AppPage
    {
        private Dictionary<string, bool> _present;   // id -> still present / active
        private (bool RealTime, bool Tamper)? _defender;
        private DateTime? _pausedUntil;
        private bool _loading;
        private string _busy;
        private int _pauseChoice = 365;

        public AdvancedPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "advanced";
        public override string Title => Loc.S("Bajo tu responsabilidad", "At your own risk");
        public override string Icon => Icons.Warning;

        public override void OnShown()
        {
            if (S.RiskAccepted && _present == null && !_loading) _ = LoadAsync();
        }

        private bool Es => Loc.Lang == Lang.Es;

        private async Task LoadAsync()
        {
            _loading = true;
            try
            {
                await S.EnsureReadyAsync();
                var script = new StringBuilder();
                foreach (var i in RiskZone.All) script.Append("try { $r = & { " + i.DetectScript + " } } catch { $r = 'on' }; \"" + i.Id + "=$r\"\n");
                script.Append("\"defender-status=$(& { " + RiskZone.DefenderStatusScript + " })\"\n");
                var r = await Task.Run(() => S.Win.Process.Run("powershell.exe", RiskZone.PowerShellArgs(script.ToString()), 120000));
                var map = new Dictionary<string, bool>();
                foreach (var line in r.Output.Split('\n').Select(l => l.Trim()))
                {
                    var kv = line.Split(new[] { '=' }, 2);
                    if (kv.Length != 2) continue;
                    if (kv[0] == "defender-status")
                    {
                        var p = kv[1].Split('|');
                        if (p.Length == 2) _defender = (p[0] == "True", p[1] == "True");
                    }
                    else map[kv[0]] = kv[1] != "off";
                }
                _present = map;
                _pausedUntil = UpdatePause.PausedUntil(S.Ctx, DateTime.UtcNow);
            }
            catch (Exception) { _present = new Dictionary<string, bool>(); }
            finally { _loading = false; W.RefreshCurrentIf(this); }
        }

        protected override FrameworkElement Build()
        {
            if (!S.RiskAccepted) return Frame(null, Disclaimer());
            if (_present == null)
            {
                var (view, _) = Ui.Loading(Loc.S("Comprobando qué está instalado…", "Checking what is installed…"));
                return Frame(null, view);
            }
            var body = Ui.Stack(28,
                Ui.Notice(Icons.Warning, "Advanced", Loc.S(
                    "Nada de esta página mejora el rendimiento de forma medible: son decisiones personales. Crea un punto de restauración y una copia del registro antes.",
                    "Nothing on this page improves performance measurably: these are personal choices. Create a restore point and a registry backup first.")),
                BackupSection(),
                UpdatesSection(),
                Ui.Stack(12, new UIElement[] { Ui.H3(Loc.S("Quitar o desactivar partes de Windows", "Remove or turn off parts of Windows")) }
                    .Concat(RiskZone.All.Select(ItemCard)).ToArray()));
            return Frame(Loc.S("Herramientas que quitan partes de Windows. Úsalas solo si sabes lo que pierdes.",
                "Tools that remove parts of Windows. Use them only if you know what you lose."), body);
        }

        private FrameworkElement Disclaimer()
        {
            var accept = new CheckBox { Content = Loc.S("Entiendo que esto puede dejar mi equipo sin seguridad, sin actualizaciones o sin apps, y lo uso bajo mi responsabilidad.", "I understand this can leave my PC without security, updates or apps, and I use it at my own risk.") };
            var go = Ui.Primary(Loc.S("Aceptar y continuar", "Accept and continue"), () =>
            {
                S.RiskAccepted = true; S.SaveSettings();
                _ = LoadAsync();
                W.RefreshCurrentIf(this);
            });
            go.IsEnabled = false;
            accept.Checked += (_, __) => go.IsEnabled = true;
            accept.Unchecked += (_, __) => go.IsEnabled = false;
            var text = Ui.Stack(12,
                Ui.Row(12, Ui.Icon(Icons.Warning, 28, "Advanced"), Ui.H2(Loc.S("Antes de entrar", "Before you go in"))),
                Ui.Text(Loc.S(
                    "Aquí puedes quitar Copilot, Xbox, Microsoft Edge y la Microsoft Store, desactivar Microsoft Defender y pausar Windows Update durante años.",
                    "Here you can remove Copilot, Xbox, Microsoft Edge and the Microsoft Store, turn off Microsoft Defender and pause Windows Update for years."), 14),
                Ui.Text(Loc.S(
                    "Estas acciones pueden comprometer la seguridad de tu equipo, romper juegos o apps y dejarte sin parches de seguridad. Algunas no se pueden deshacer desde aquí: hay que reinstalar.",
                    "These actions can compromise your PC's security, break games or apps and leave you without security patches. Some cannot be undone from here: you have to reinstall."), 14),
                Ui.Text(Loc.S(
                    "Optimizzzer y sus autores no se hacen cargo de ningún daño, pérdida de datos o problema causado por estas herramientas. Cada persona decide y responde por lo que ejecuta en su equipo.",
                    "Optimizzzer and its authors accept no liability for any damage, data loss or problem caused by these tools. Each person decides and is responsible for what they run on their PC."), 14, true),
                Ui.Muted(Loc.S("El texto completo está en docs/RESPONSABILIDAD.md del repositorio.", "The full text is in docs/RESPONSABILIDAD.md in the repository."), 13),
                accept,
                Ui.Row(8, go));
            return Ui.Panel(text, 28);
        }

        private FrameworkElement BackupSection()
        {
            var b = Ui.Secondary(Loc.S("Copiar el registro…", "Back up the registry…"), () => _ = BackupRegistryAsync(), Icons.Save);
            b.IsEnabled = _busy == null;
            return Ui.Stack(8, Ui.H3(Loc.S("Copia completa del registro", "Full registry backup")),
                Ui.Muted(Loc.S("Exporta tu usuario (HKCU) y HKLM\\SOFTWARE a dos archivos .reg en la carpeta que elijas. Para restaurar, abre el .reg con doble clic.",
                    "Exports your user (HKCU) and HKLM\\SOFTWARE to two .reg files in a folder you choose. To restore, double-click the .reg file.")),
                Ui.Row(8, b, _busy == "backup" ? Ui.Muted(Loc.S("Exportando, puede tardar un minuto…", "Exporting, it can take a minute…")) : null));
        }

        private async Task BackupRegistryAsync()
        {
            // A save dialog picks the folder: the two files are written next to the name chosen.
            var stamp = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
            var dlg = new Microsoft.Win32.SaveFileDialog { Title = Loc.S("Elige dónde guardar la copia del registro", "Choose where to save the registry backup"), FileName = "registro-usuario-" + stamp + ".reg", Filter = "Registro (*.reg)|*.reg" };
            if (dlg.ShowDialog(W) != true) return;
            var folder = System.IO.Path.GetDirectoryName(dlg.FileName);
            _busy = "backup"; W.RefreshCurrentIf(this);
            try
            {
                var files = new[] { ("HKCU", System.IO.Path.GetFileName(dlg.FileName)), ("HKLM\\SOFTWARE", "registro-software-" + stamp + ".reg") };
                foreach (var (key, name) in files)
                {
                    var path = System.IO.Path.Combine(folder, name);
                    var r = await Task.Run(() => S.Win.Process.Run("reg.exe", "export \"" + key + "\" \"" + path + "\" /y", 600000));
                    if (!r.Ok) throw new InvalidOperationException("reg export " + key + ": " + (r.Output + r.Error).Trim());
                }
                W.Toast(ToastKind.Success, Loc.S("Copia del registro guardada en ", "Registry backup saved to ") + folder + ".", Loc.S("Abrir carpeta", "Open folder"), () => Launcher.Open(folder));
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
            finally { _busy = null; W.RefreshCurrentIf(this); }
        }

        private FrameworkElement UpdatesSection()
        {
            var status = _pausedUntil.HasValue
                ? Ui.Chip(Icons.CheckCircle, Loc.S("En pausa hasta el ", "Paused until ") + _pausedUntil.Value.ToLocalTime().ToString("d"), "Success")
                : Ui.Chip(Icons.Update, Loc.S("Las actualizaciones no están en pausa", "Updates are not paused"));
            var pick = SettingsPage.Segmented(Loc.S("Pausar durante", "Pause for"), new[]
            {
                (30, Loc.S("1 mes", "1 month")), (365, Loc.S("1 año", "1 year")), (1825, Loc.S("5 años", "5 years")), (UpdatePause.MaxDays, Loc.S("Máximo (unos 20 años)", "Maximum (about 20 years)")),
            }, _pauseChoice, v => _pauseChoice = v);
            var pause = Ui.Primary(Loc.S("Pausar", "Pause"), () => _ = PauseAsync(), Icons.Pause);
            var resume = _pausedUntil.HasValue ? Ui.Secondary(Loc.S("Reanudar actualizaciones", "Resume updates"), () => _ = ResumeAsync(), Icons.Play) : null;
            var script = Ui.Subtle(Loc.S("Guardar como script .ps1", "Save as .ps1 script"), () =>
                SaveScript("pausar-windows-update.ps1", UpdatePause.Script(UpdatePause.ClampUntil(DateTime.UtcNow, DateTime.UtcNow.AddDays(_pauseChoice)), Es)), Icons.Save);
            foreach (var b in new[] { pause, resume }) if (b != null) b.IsEnabled = _busy == null;
            return Ui.Stack(10, Ui.H3(Loc.S("Pausar Windows Update", "Pause Windows Update")),
                Ui.Muted(Loc.S(
                    "Usa las mismas fechas de pausa que Configuración y sube el límite que Configuración permite. Pausar hasta 2222 no es posible: el máximo que se conoce que Windows acepta es de unos 20 años. Windows decide si lo respeta; abajo ves la fecha que de verdad quedó. Sin actualizaciones no recibes parches de seguridad.",
                    "It uses the same pause dates as Settings and raises the limit Settings allows. Pausing until 2222 is not possible: the highest limit known to work is about 20 years. Windows decides whether to honour it; below is the date that really stuck. Without updates you get no security patches.")),
                status, pick, Ui.Row(8, pause, resume, script));
        }

        private async Task PauseAsync()
        {
            if (!await W.Confirm(Loc.S("¿Pausar Windows Update?", "Pause Windows Update?"),
                Loc.S("No recibirás parches de seguridad hasta que reanudes. Puedes reanudar aquí o en Configuración > Windows Update.", "You will get no security patches until you resume. You can resume here or in Settings > Windows Update."),
                Loc.S("Pausar", "Pause"), danger: true)) return;
            _busy = "pause"; W.RefreshCurrentIf(this);
            try
            {
                var days = _pauseChoice;
                await Task.Run(() => UpdatePause.Pause(S.Ctx, DateTime.UtcNow, DateTime.UtcNow.AddDays(days)));
                _pausedUntil = UpdatePause.PausedUntil(S.Ctx, DateTime.UtcNow);
                if (_pausedUntil.HasValue) W.Toast(ToastKind.Success, Loc.S("Windows Update en pausa hasta el ", "Windows Update paused until ") + _pausedUntil.Value.ToLocalTime().ToString("d") + ".");
                else W.Toast(ToastKind.Warning, Loc.S("Windows no aceptó la pausa.", "Windows did not accept the pause."));
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
            finally { _busy = null; W.RefreshCurrentIf(this); }
        }

        private async Task ResumeAsync()
        {
            _busy = "pause"; W.RefreshCurrentIf(this);
            try
            {
                await Task.Run(() => UpdatePause.Resume(S.Ctx));
                _pausedUntil = UpdatePause.PausedUntil(S.Ctx, DateTime.UtcNow);
                W.Toast(ToastKind.Success, Loc.S("Actualizaciones reanudadas.", "Updates resumed."));
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
            finally { _busy = null; W.RefreshCurrentIf(this); }
        }

        private FrameworkElement ItemCard(RiskItem i)
        {
            bool present = !_present.TryGetValue(i.Id, out var p) || p;
            var chip = present
                ? Ui.Chip(Icons.Info, i.Id == "defender" ? Loc.S("Activo", "On") : Loc.S("Instalado", "Installed"))
                : Ui.Chip(Icons.CheckCircle, i.Id == "defender" ? Loc.S("Desactivado", "Off") : Loc.S("Quitado", "Removed"), "Success");
            var info = Ui.Stack(6,
                Ui.Row(10, Ui.Text(Loc.S(i.Title), 15, true), chip),
                Ui.Muted(Loc.S(i.What)),
                Ui.Row(8, Ui.Icon(Icons.Warning, 14, "Advanced"), Ui.Text(Loc.S("Pierdes: ", "You lose: ") + Loc.S(i.Lose), 13)),
                Ui.Row(8, Ui.Icon(Icons.Undo, 14, "FgMuted"), Ui.Muted(Loc.S(i.Back), 13)));

            var buttons = new WrapPanel();
            void Add(Button b) { if (b == null) return; b.Margin = new Thickness(0, 8, 8, 0); buttons.Children.Add(b); }
            bool tamperBlocks = i.Id == "defender" && _defender?.Tamper == true;
            if (tamperBlocks)
            {
                info.Children.Add(Ui.Notice(Icons.Lock, "Moderate", Loc.S(
                    "La Protección contra alteraciones está activa: Windows ignora estas directivas. Apágala primero en Seguridad de Windows > Protección antivirus y contra amenazas > Administrar la configuración, y vuelve aquí.",
                    "Tamper Protection is on: Windows ignores these policies. Turn it off first in Windows Security > Virus & threat protection > Manage settings, then come back.")));
                Add(Ui.Secondary(Loc.S("Abrir Seguridad de Windows", "Open Windows Security"), () => Launcher.Open("windowsdefender://threatsettings"), Icons.Lock));
            }
            if (present)
            {
                var act = Ui.Secondary(i.Id == "defender" ? Loc.S("Desactivar", "Turn off") : Loc.S("Quitar", "Remove"), () => _ = RunAsync(i, false), Icons.Cleanup);
                act.SetResourceReference(Control.ForegroundProperty, "Advanced");
                act.IsEnabled = _busy == null && !tamperBlocks;
                AutomationProperties.SetName(act, Loc.S(i.Title));
                Add(act);
            }
            else
            {
                if (!string.IsNullOrEmpty(i.RevertScript))
                {
                    var back = Ui.Secondary(Loc.S("Volver atrás", "Go back"), () => _ = RunAsync(i, true), Icons.Undo);
                    back.IsEnabled = _busy == null;
                    Add(back);
                }
                if (!string.IsNullOrEmpty(i.BackLink) && i.Id != "defender")
                    Add(Ui.Secondary(Loc.S("Reinstalar", "Reinstall"), () => Launcher.Open(i.BackLink), Icons.Download));
            }
            Add(Ui.Subtle(Loc.S("Guardar como script .ps1", "Save as .ps1 script"), () => SaveScript(i.Id + ".ps1", RiskZone.FullScript(i, Es)), Icons.Save));
            info.Children.Add(buttons);
            if (_busy == i.Id) info.Children.Add(Ui.Muted(Loc.S("En curso, puede tardar unos minutos…", "Running, it can take a few minutes…"), 13));
            return Ui.Panel(info, 20);
        }

        private async Task RunAsync(RiskItem i, bool revert)
        {
            if (!revert && !await W.Confirm(Loc.S("¿", "") + Loc.S(i.Title) + "?",
                Loc.S("Pierdes: ", "You lose: ") + Loc.S(i.Lose) + "\n\n" + Loc.S(i.Back) + "\n\n" +
                Loc.S("Lo haces bajo tu responsabilidad.", "You do this at your own risk."),
                i.Id == "defender" ? Loc.S("Desactivar", "Turn off") : Loc.S("Quitar", "Remove"), danger: true)) return;
            _busy = i.Id; W.RefreshCurrentIf(this);
            try
            {
                var script = (revert ? i.RevertScript : i.ApplyScript) + "\n" + "try { $r = & { " + i.DetectScript + " } } catch { $r = 'on' }; \"state=$r\"";
                var r = await Task.Run(() => S.Win.Process.Run("powershell.exe", RiskZone.PowerShellArgs(script), 1800000));
                var state = r.Output.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith("state="));
                bool nowPresent = state == null || state != "state=off";
                _present[i.Id] = nowPresent;
                if (revert) W.Toast(ToastKind.Success, Loc.S(i.Title) + Loc.S(": se deshizo lo que se podía. ", ": undone where possible. ") + Loc.S(i.Back));
                else if (!nowPresent) W.Toast(ToastKind.Success, Loc.S(i.Title) + Loc.S(": hecho.", ": done."));
                else W.Toast(ToastKind.Warning, Loc.S(i.Title) + Loc.S(": Windows no lo permitió del todo. Revisa el script guardándolo como .ps1.", ": Windows did not fully allow it. Check the script by saving it as .ps1."));
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
            finally { _busy = null; W.RefreshCurrentIf(this); }
        }

        private void SaveScript(string name, string text)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = name, Filter = "PowerShell (*.ps1)|*.ps1" };
            if (dlg.ShowDialog(W) != true) return;
            try
            {
                System.IO.File.WriteAllText(dlg.FileName, text, new UTF8Encoding(true));
                W.Toast(ToastKind.Success, Loc.S("Script guardado. Léelo antes de ejecutarlo: clic derecho > Ejecutar con PowerShell, como administrador.", "Script saved. Read it before running it: right click > Run with PowerShell, as administrator."));
            }
            catch (Exception ex) { W.ShowError(ex.Message); }
        }

        /// <summary>Self-test hooks.</summary>
        public void AcceptForTest() { S.RiskAccepted = true; }
        public bool LoadedForTest => _present != null;
    }
}
