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

            var current = Ui.Panel(Ui.Stack(8,
                Ui.H2(Loc.S("Ahora mismo", "Right now")),
                Ui.Stack(4, _adapters.Select(a => (UIElement)Ui.Text(a.Name + ": " + (a.Servers.Count == 0 ? Loc.S("sin DNS", "no DNS") : string.Join(", ", a.Servers))
                    + (a.Automatic ? Loc.S(" (automático, de tu router)", " (automatic, from your router)") : Loc.S(" (fijado a mano)", " (set manually)")))).ToArray()),
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
                var title = Ui.Row(10, Ui.Text(p.Name, 14, true, "Fg", wrap: false),
                    lat?.MedianMs != null && lat.MedianMs == best ? Ui.Text(Loc.S("El más rápido aquí", "Fastest here"), 12, true, "Success", wrap: false) : null);
                var rb = new RadioButton
                {
                    GroupName = "dns", IsChecked = _choice == p.Id,
                    Content = Ui.Stack(2, title, Ui.Muted(Loc.S(p.Description)), Ui.Muted(string.Join(", ", p.IPv4) + " · " + string.Join(", ", p.IPv6), 12)),
                };
                AutomationProperties.SetName(rb, p.Name + (speed.Length > 0 ? ", " + speed : ""));
                rb.Checked += (_, __) => { _choice = p.Id; apply.IsEnabled = true; apply.Content = Loc.S("Usar ", "Use ") + p.Name; AutomationProperties.SetName(apply, apply.Content.ToString()); };
                var speedText = Ui.Text(speed, 14, true, "Fg", wrap: false);
                body.Children.Add(CleanupPage.Row(null, rb, speedText));
            }
            body.Children.Add(Ui.Muted(Loc.S("La velocidad es la mediana de 10 consultas reales desde tu conexión. Sin medir, no se muestra ninguna cifra.",
                "Speed is the median of 10 real queries from your connection. Without measuring, no number is shown."), 12));

            apply = Ui.Primary(_choice == null ? Loc.S("Elige un DNS", "Choose a DNS") : Loc.S("Usar ", "Use ") + DnsCatalog.All.First(d => d.Id == _choice).Name, async () =>
            {
                var p = DnsCatalog.All.First(d => d.Id == _choice);
                _busy = true; W.RefreshCurrentIf(this);
                try { await Task.Run(() => S.Win.Dns.Apply(p, _adapters)); }
                catch (Exception ex) { W.ShowError(ex.Message); }
                _busy = false; _adapters = null; _ = LoadAsync();
            }, Icons.Check);
            apply.IsEnabled = _choice != null;

            Button restore = null;
            if (S.Win.Dns.HasBackup)
                restore = Ui.Secondary(Loc.S("Volver a mi DNS anterior", "Go back to my previous DNS"), async () =>
                {
                    _busy = true; W.RefreshCurrentIf(this);
                    try { await Task.Run(() => S.Win.Dns.Restore()); }
                    catch (Exception ex) { W.ShowError(ex.Message); }
                    _busy = false; _adapters = null; _ = LoadAsync();
                }, Icons.Undo);

            return Frame(Loc.S("El DNS traduce nombres como google.com en direcciones. Uno rápido acelera la carga de webs; algunos además bloquean malware o anuncios.",
                "DNS turns names like google.com into addresses. A fast one speeds up page loads; some also block malware or ads."), body, Footer(restore, apply));
        }
    }
}
