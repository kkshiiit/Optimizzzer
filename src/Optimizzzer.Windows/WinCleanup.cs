using System;
using System.ServiceProcess;
using System.Threading;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.Windows
{
    public sealed class WinCleanup : ICleanupSpecial
    {
        private readonly IProcessRunner _proc;
        public WinCleanup(IProcessRunner proc) { _proc = proc; }

        public Action<string> OnOutput { get; set; }

        public long? RecycleBinSize()
        {
            var info = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.SHQUERYRBINFO)) };
            return Native.SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : (long?)null;
        }

        public void EmptyRecycleBin()
        {
            Native.SHEmptyRecycleBin(IntPtr.Zero, null, Native.SHERB_NOCONFIRMATION | Native.SHERB_NOPROGRESSUI | Native.SHERB_NOSOUND);
        }

        public void CleanComponentStore(CancellationToken ct)
        {
            var code = _proc.RunStreaming("DISM.exe", "/Online /Cleanup-Image /StartComponentCleanup", l => OnOutput?.Invoke(l), ct);
            if (code != 0) throw new InvalidOperationException("DISM exit code " + code);
        }

        public void StopUpdateServices()
        {
            foreach (var name in new[] { "wuauserv", "bits" }) StopService(name);
        }

        public void StartUpdateServices()
        {
            foreach (var name in new[] { "bits", "wuauserv" })
            {
                try
                {
                    using (var sc = new ServiceController(name))
                        if (sc.Status == ServiceControllerStatus.Stopped) sc.Start();
                }
                catch (Exception) { }
            }
        }

        private static void StopService(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped) return;
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception) { }
        }
    }
}
