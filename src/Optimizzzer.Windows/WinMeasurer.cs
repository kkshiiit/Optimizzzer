using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Xml.Linq;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.Windows
{
    public sealed class WinMeasurer : IMeasurer
    {
        private readonly StartupManager _startup;
        public WinMeasurer(StartupManager startup) { _startup = startup; }

        public Measurement Take(string label)
        {
            var m = new Measurement { TakenUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), Label = label };
            Try(() =>
            {
                var s = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX)) };
                if (Native.GlobalMemoryStatusEx(ref s))
                {
                    m.MemoryTotalMb = (long)(s.ullTotalPhys / 1024 / 1024);
                    m.MemoryUsedMb = (long)((s.ullTotalPhys - s.ullAvailPhys) / 1024 / 1024);
                }
            });
            Try(() => m.Processes = Process.GetProcesses().Length);
            Try(() => m.RunningServices = ServiceController.GetServices().Count(s => { using (s) return s.Status == ServiceControllerStatus.Running; }));
            Try(() => { var c = _startup.CountEnabled(); if (c >= 0) m.StartupItems = c; });
            Try(() => m.SystemDriveFreeMb = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)).AvailableFreeSpace / 1024 / 1024);
            Try(() => m.LastBootUtc = DateTime.UtcNow.AddMilliseconds(-(double)Native.GetTickCount64()).ToString("o", CultureInfo.InvariantCulture));
            Try(() => m.BootMs = LastBootDuration());
            return m;
        }

        /// <summary>Event 100 of Diagnostics-Performance records each boot's duration in milliseconds.</summary>
        private static long? LastBootDuration()
        {
            var q = new EventLogQuery("Microsoft-Windows-Diagnostics-Performance/Operational", PathType.LogName, "*[System[EventID=100]]")
            { ReverseDirection = true };
            using (var reader = new EventLogReader(q))
            using (var e = reader.ReadEvent())
            {
                if (e == null) return null;
                var x = XDocument.Parse(e.ToXml());
                XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
                var boot = x.Descendants(ns + "Data").FirstOrDefault(d => (string)d.Attribute("Name") == "BootTime");
                return boot != null && long.TryParse(boot.Value, out var ms) ? ms : (long?)null;
            }
        }

        private static void Try(Action a)
        {
            try { a(); } catch (Exception) { }
        }
    }
}
