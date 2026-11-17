using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Optimizzzer.App.Shell;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.App.Pages
{
    public sealed class RepairPage : AppPage
    {
        private RepairTask _running;
        private CancellationTokenSource _cts;
        private TextBox _output;
        private readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();
        private string _lastResult;

        public RepairPage(AppState s, MainWindow w) : base(s, w) { }
        public override string Key => "repair";
        public override string Title => Loc.S("Reparación", "Repair");
        public override string Icon => Icons.Repair;

        protected override FrameworkElement Build()
        {
            var list = new StackPanel();
            foreach (var t in RepairCatalog.All)
            {
                var info = Ui.Stack(2, Ui.Text(Loc.S(t.Title), 14, true), Ui.Muted(Loc.S(t.Description)),
                    Ui.Muted(Loc.S("Duración aproximada: ", "Approximate duration: ") + Loc.S(t.Duration) + (t.NeedsReboot ? Loc.S(" · requiere reiniciar", " · needs a restart") : ""), 12));
                var run = Ui.Secondary(_running == t ? Loc.S("En curso…", "Running…") : Loc.S("Ejecutar", "Run"), () => _ = RunAsync(t), Icons.Play);
                run.IsEnabled = _running == null;
                AutomationProperties.SetName(run, Loc.S("Ejecutar ", "Run ") + Loc.S(t.Title));
                list.Children.Add(CleanupPage.Row(Ui.Icon(Icons.Repair, 16, "FgMuted"), info, run));
            }

            _output = new TextBox
            {
                IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 180, MaxHeight = 280, VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(10, 8, 10, 8),
                FontFamily = new FontFamily("Consolas"), FontSize = 12, Text = _log.ToString(),
            };
            AutomationProperties.SetName(_output, Loc.S("Salida del comando", "Command output"));
            var outputHeader = Ui.Row(12, Ui.H3(Loc.S("Salida", "Output")),
                _lastResult != null ? Ui.Text(_lastResult, 14, true, _lastResult.StartsWith("✓") ? "Success" : "Advanced") : null);
            var outputSection = Ui.Stack(8, outputHeader, _log.Length == 0 ? (UIElement)Ui.Empty(Loc.S("Ejecuta una reparación para ver aquí lo que hace.", "Run a repair to see what it does here.")) : _output);
            outputSection.Margin = new Thickness(0, 24, 0, 0);

            UIElement footer = _running != null ? Footer(Ui.Secondary(Loc.S("Cancelar", "Cancel"), () => _cts?.Cancel(), Icons.Stop), null) : null;
            return Frame(Loc.S("Herramientas oficiales de Windows para arreglar problemas comunes. Se ejecutan con su salida visible.",
                "Official Windows tools to fix common problems. They run with their output shown."), Ui.Stack(0, list, outputSection), footer);
        }

        private async Task RunAsync(RepairTask t)
        {
            _running = t;
            _lastResult = null;
            _log.Clear();
            _cts = new CancellationTokenSource();
            W.RefreshCurrentIf(this);
            bool ok = true;
            try
            {
                foreach (var step in t.Steps)
                {
                    Append("> " + step);
                    var code = await Task.Run(() => S.Win.Process.RunStreaming(Environment.ExpandEnvironmentVariables(step.File), step.Args, Append, _cts.Token));
                    Append(Loc.S("Código de salida: ", "Exit code: ") + code);
                    // net stop on a stopped service returns 2; that is fine for the reset sequences.
                    if (code != 0 && !(step.File == "net.exe" && code == 2)) ok = false;
                }
                if (t.RestartsExplorer) await Task.Run(() => S.Win.RestartExplorer());
            }
            catch (OperationCanceledException) { Append(Loc.S("Cancelado.", "Cancelled.")); ok = false; }
            catch (Exception ex) { Append(ex.Message); ok = false; }
            _lastResult = ok ? "✓ " + Loc.S("Terminado", "Finished") : Loc.S("Terminado con errores, revisa la salida", "Finished with errors, check the output");
            _running = null;
            W.RefreshCurrentIf(this);
        }

        private void Append(string line)
        {
            W.Dispatcher.BeginInvoke(new Action(() =>
            {
                _log.AppendLine(line);
                if (_output != null)
                {
                    _output.AppendText(line + Environment.NewLine);
                    _output.ScrollToEnd();
                }
            }));
        }
    }
}
