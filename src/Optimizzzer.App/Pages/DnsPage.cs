using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Optimizzzer.App.Shell;
using Optimizzzer.Core.Tools;
using Optimizzzer.Windows;

namespace Optimizzzer.App.Pages
{
    public sealed class DnsPage : AppPage
    {
        private IList<AdapterDns> _adapters;
        private string _loadError;
        private readonly Dictionary<string, DnsLatency> _latency = new Dictionary<string, DnsLatency>();
        private DnsLatency _currentLatency;
        private bool _measuring, _busy;
        private string _choice;

        public DnsPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "dns";
        public override string Title => Loc.S("Red y DNS", "Network and DNS");
        public override string Icon => Icons.Network;

        public override void OnShown()
        {
            if (_adapters == null && _loadError == null) _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try { _adapters = await Task.Run(() => S.Win.Dns.Adapters()); _loadError = null; }
            catch (Exception ex) { _loadError = ex.Message; }
            W.RefreshCurrentIf(this);
        }

        private async Task MeasureAsync()
        {
            _measuring = true;
            _latency.Clear();
            W.RefreshCurrentIf(this);
            var tasks = DnsCatalog.All.Select(p => DnsBenchmark.MeasureAsync(p)).ToList();
            var current = _adapters?.SelectMany(a => a.Servers).FirstOrDefault(ip => !ip.Contains(":"));
            Task<DnsLatency> currentTask = current == null ? null
                : DnsBenchmark.MeasureAsync(new DnsProvider { Id = "current", Name = current, IPv4 = new[] { current } });
            foreach (var r in await Task.WhenAll(tasks)) _latency[r.Provider.Id] = r;
            _currentLatency = currentTask == null ? null : await currentTask;
            _measuring = false;
            W.RefreshCurrentIf(this);
        }

        protected override FrameworkElement Build()
        {
            if (_loadError != null) return Frame(null, Ui.ErrorState(Loc.S("No se pudieron leer los adaptadores de red.", "Network adapters could not be read."), _loadError, () => { _loadError = null; _ = LoadAsync(); }));
            if (_adapters == null || _busy)
            {
                var (v, _) = Ui.Loading(_busy ? Loc.S("Cambiando la configuración de red…", "Changing network settings…") : Loc.S("Leyendo la configuración de red…", "Reading network settings…"));
                return Frame(null, v);
            }

            var body = new StackPanel();
            if (_adapters.Count == 0)
            {
                body.Children.Add(Ui.Empty(Loc.S("No hay ningún adaptador de red conectado a internet.", "No network adapter is connected to the internet.")));
                return Frame(null, body);
            }

            // Which provider is in use, by name, so nobody has to read IP addresses to know.
            var inUse = _adapters.Select(a => DnsCatalog.Match(a.Servers)).FirstOrDefault(m => m != null);
            if (_choice == null && inUse != null) _choice = inUse.Id;
            UIElement AdapterLine(AdapterDns a)
            {
                var m = DnsCatalog.Match(a.Servers);
                var what = m != null
                    ? Ui.Notice(Icons.CheckCircle, "Success", Loc.S("Usando ", "Using ") + m.Name + Loc.S(" en ", " on ") + a.Name)
                    : a.Automatic
                        ? Ui.Notice(Icons.Network, "FgMuted", a.Name + Loc.S(": automático, el DNS de tu router o proveedor de internet", ": automatic, your router's or internet provider's DNS"))
                        : Ui.Notice(Icons.Network, "FgMuted", a.Name + Loc.S(": DNS personalizado", ": custom DNS"));
                var ips = Ui.Muted(Loc.S("DNS: ", "DNS: ") + (a.Servers.Count == 0 ? Loc.S("ninguno", "none") : string.Join(", ", a.Servers))
                    + (a.LocalIPv4 != null ? Loc.S(" · IP de este equipo: ", " · This PC's IP: ") + a.LocalIPv4 : "")
                    + (a.Gateway != null ? Loc.S(" · Router: ", " · Router: ") + a.Gateway : ""), 12);
                ips.Margin = new Thickness(26, 2, 0, 0);
                var reset = Ui.Subtle(Loc.S("Restablecer adaptador", "Reset adapter"), async () =>
                {
                    if (!await W.Confirm(Loc.S("¿Restablecer ", "Reset ") + a.Name + "?",
                        Loc.S("Se apaga y se vuelve a encender, como desconectar el cable. Te quedas sin internet unos segundos. Útil si la conexión se quedó colgada.", "It is turned off and on again, like unplugging the cable. You lose internet for a few seconds. Useful when the connection hangs."),
                        Loc.S("Restablecer", "Reset"))) return;
                    _busy = true; W.RefreshCurrentIf(this);
                    try { await Task.Run(() => S.Win.Dns.ResetAdapter(a)); W.Toast(ToastKind.Success, a.Name + Loc.S(" restablecido.", " reset.")); }
                    catch (Exception ex) { W.ShowError(ex.Message, Loc.S("Si el adaptador quedó apagado, actívalo en Configuración > Red e Internet.", "If the adapter stayed off, turn it on in Settings > Network & internet.")); }
                    _busy = false; _adapters = null; _ = LoadAsync();
                }, Icons.Restart, Loc.S("Restablecer ", "Reset ") + a.Name);
                reset.Margin = new Thickness(20, 0, 0, 0);
                reset.HorizontalAlignment = HorizontalAlignment.Left;
                return Ui.Stack(0, what, ips, reset);
            }
            var current = Ui.Panel(Ui.Stack(8,
                Ui.H2(Loc.S("Ahora mismo", "Right now")),
                Ui.Stack(8, _adapters.Select(AdapterLine).ToArray()),
                _currentLatency?.MedianMs != null ? Ui.Muted(Loc.S("Tu DNS actual responde en ", "Your current DNS answers in ") + Loc.N(_currentLatency.MedianMs.Value) + " ms") : null));
            body.Children.Add(current);

            var listHeader = Ui.Row(12, Ui.H2(Loc.S("Elige un DNS", "Choose a DNS")),
                Ui.Secondary(_measuring ? Loc.S("Midiendo…", "Measuring…") : Loc.S("Medir velocidad desde este equipo", "Measure speed from this PC"), () => _ = MeasureAsync(), Icons.Measure));
            ((Button)listHeader.Children[1]).IsEnabled = !_measuring;
            listHeader.Margin = new Thickness(0, 24, 0, 8);
            body.Children.Add(listHeader);

            double? best = _latency.Values.Where(l => l.MedianMs.HasValue).Select(l => l.MedianMs).DefaultIfEmpty(null).Min();
            Button apply = null;
            foreach (var p in DnsCatalog.All)
            {
                _latency.TryGetValue(p.Id, out var lat);
                string speed = lat == null ? "" : lat.MedianMs.HasValue ? Loc.N(lat.MedianMs.Value) + " ms" : Loc.S("Sin respuesta", "No answer");
                bool used = inUse?.Id == p.Id;
                var title = Ui.Row(10, Ui.Text(p.Name, 14, true, "Fg", wrap: false),
                    used ? Ui.Chip(Icons.CheckCircle, Loc.S("En uso", "In use"), "Success") : null,
                    lat?.MedianMs != null && lat.MedianMs == best ? Ui.Text(Loc.S("El más rápido aquí", "Fastest here"), 12, true, "Success", wrap: false) : null);
                FrameworkElement last = null;
                if (S.DnsLast?.Id == p.Id)
                {
                    var when = S.DnsLast.Value.WhenUtc.ToLocalTime();
                    var text = Loc.S("Lo aplicaste el ", "You applied it on ") + when.ToString("d") + Loc.S(" a las ", " at ") + when.ToString("t")
                        + (used ? "" : Loc.S(" · ya no está en uso (otro programa o el router lo cambió)", " · no longer in use (another program or the router changed it)"));
                    last = Ui.Notice(Icons.History, used ? "AccentText" : "Moderate", text);
                }
                var rb = new RadioButton
                {
                    GroupName = "dns", IsChecked = _choice == p.Id,
                    Content = Ui.Stack(2, title, Ui.Muted(Loc.S(p.Description)), Ui.Muted(string.Join(", ", p.IPv4) + " · " + string.Join(", ", p.IPv6), 12), last),
                };
                AutomationProperties.SetName(rb, p.Name + (used ? ", " + Loc.S("en uso", "in use") : "") + (speed.Length > 0 ? ", " + speed : ""));
                rb.Checked += (_, __) => { _choice = p.Id; UpdateApply(); };
                var speedText = Ui.Text(speed, 14, true, "Fg", wrap: false);
                body.Children.Add(CleanupPage.Row(null, rb, speedText));
            }
            body.Children.Add(Ui.Muted(Loc.S("La velocidad es la mediana de 10 consultas reales desde tu conexión. Sin medir, no se muestra ninguna cifra.",
                "Speed is the median of 10 real queries from your connection. Without measuring, no number is shown."), 12));

            // Encrypted DNS: Windows 11 does it itself for these providers; the switch lives in Settings, per connection.
            if (S.Ctx?.Info.Build >= 22000)
            {
                var doh = Ui.Panel(Ui.Stack(8,
                    Ui.H3(Loc.S("DNS cifrado (Windows 11)", "Encrypted DNS (Windows 11)")),
                    Ui.Muted(Loc.S("Con Cloudflare, Google, Quad9 o AdGuard, Windows 11 puede cifrar las consultas para que tu proveedor de internet no las vea. Se activa en Configuración > Red e Internet > tu conexión > Asignación de servidor DNS > Editar > DNS sobre HTTPS: Activado (plantilla automática).",
                        "With Cloudflare, Google, Quad9 or AdGuard, Windows 11 can encrypt lookups so your internet provider cannot see them. Turn it on in Settings > Network & internet > your connection > DNS server assignment > Edit > DNS over HTTPS: On (automatic template)."), 12),
                    Ui.Secondary(Loc.S("Abrir Configuración de red", "Open network settings"), () =>
                    {
                        try { System.Diagnostics.Process.Start("ms-settings:network-status"); } catch (Exception ex) { W.ShowError(ex.Message); }
                    }, Icons.Network)), 20);
                doh.Margin = new Thickness(0, 16, 0, 0);
                body.Children.Add(doh);
            }

            apply = Ui.Primary("", async () =>
            {
                var p = DnsCatalog.All.First(d => d.Id == _choice);
                var names = string.Join(", ", _adapters.Select(a => a.Name));
                _busy = true; W.RefreshCurrentIf(this);
                try
                {
                    await Task.Run(() => S.Win.Dns.Apply(p, _adapters));
                    S.DnsLast = (p.Id, DateTime.UtcNow);
                    S.SaveSettings();
                    W.Toast(ToastKind.Success, p.Name + Loc.S(" activo en ", " active on ") + names + ".");
                }
                catch (Exception ex) { W.ShowError(ex.Message); }
                _busy = false; _adapters = null; _ = LoadAsync();
            }, Icons.Check);
            void UpdateApply()
            {
                var chosen = DnsCatalog.All.FirstOrDefault(d => d.Id == _choice);
                string label = chosen == null ? Loc.S("Elige un DNS", "Choose a DNS")
                    : chosen.Id == inUse?.Id ? Loc.S("Ya estás usando ", "You are already using ") + chosen.Name
                    : Loc.S("Usar ", "Use ") + chosen.Name;
                apply.Content = label;
                apply.IsEnabled = chosen != null && chosen.Id != inUse?.Id;
                AutomationProperties.SetName(apply, label);
            }
            UpdateApply();

            Button restore = null;
            if (S.Win.Dns.HasBackup)
                restore = Ui.Secondary(Loc.S("Volver a mi DNS anterior", "Go back to my previous DNS"), async () =>
                {
                    _busy = true; W.RefreshCurrentIf(this);
                    try
                    {
                        await Task.Run(() => S.Win.Dns.Restore());
                        S.DnsLast = null;
                        S.SaveSettings();
                        _choice = null;
                        W.Toast(ToastKind.Success, Loc.S("Volviste a tu DNS anterior.", "You are back on your previous DNS."));
                    }
                    catch (Exception ex) { W.ShowError(ex.Message); }
                    _busy = false; _adapters = null; _ = LoadAsync();
                }, Icons.Undo);

            return Frame(Loc.S("El DNS traduce nombres como google.com en direcciones. Uno rápido acelera la carga de webs; algunos además bloquean malware o anuncios.",
                "DNS turns names like google.com into addresses. A fast one speeds up page loads; some also block malware or ads."), body, Footer(restore, apply));
        }
    }
}
