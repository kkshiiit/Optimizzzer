using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    public sealed class WinProcess : IProcessRunner
    {
        private static Encoding ConsoleEncoding()
        {
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch (Exception) { return Encoding.UTF8; }
        }

        private static ProcessStartInfo Info(string file, string args) => new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = ConsoleEncoding(),
            StandardErrorEncoding = ConsoleEncoding(),
            WorkingDirectory = Environment.SystemDirectory,
        };

        // sfc and DISM write UTF-16 to a redirected pipe, which shows up as text with NULs between letters.
        private static string Clean(string s) => s?.Replace("\0", "").TrimEnd('\r') ?? "";

        public ProcessResult Run(string file, string arguments, int timeoutMs = 120000)
        {
            using (var p = new Process { StartInfo = Info(file, arguments) })
            {
                var output = new StringBuilder();
                var error = new StringBuilder();
                p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(Clean(e.Data)); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (error) error.AppendLine(Clean(e.Data)); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch (Exception) { }
                    return new ProcessResult(-1, output.ToString(), "Timed out after " + timeoutMs / 1000 + " s");
                }
                p.WaitForExit();
                return new ProcessResult(p.ExitCode, output.ToString(), error.ToString());
            }
        }

        public int RunStreaming(string file, string arguments, Action<string> onLine, CancellationToken ct)
        {
            using (var p = new Process { StartInfo = Info(file, arguments) })
            {
                DataReceivedEventHandler handler = (_, e) =>
                {
                    var line = Clean(e.Data);
                    if (!string.IsNullOrWhiteSpace(line)) onLine(line);
                };
                p.OutputDataReceived += handler;
                p.ErrorDataReceived += handler;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                using (ct.Register(() => { try { if (!p.HasExited) p.Kill(); } catch (Exception) { } }))
                {
                    p.WaitForExit();
                }
                ct.ThrowIfCancellationRequested();
                return p.ExitCode;
            }
        }
    }
}
