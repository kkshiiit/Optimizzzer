using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        public Measurement Take(string label) => Take(label, 3, null);

        /// <summary>
        /// Samples CPU and memory once per second for <paramref name="seconds"/> and keeps the average and the peak;
        /// a few seconds are not enough to see the real load. Progress reports the seconds done.
        /// </summary>
        public Measurement Take(string label, int seconds, IProgress<int> progress)
        {
            var m = TakeOnce(label);
            var cpu = new System.Collections.Generic.List<double>();
            var mem = new System.Collections.Generic.List<long>();
            var commit = new System.Collections.Generic.List<long>();
            GetSystemTimes(out var idle0, out var kernel0, out var user0);
            for (int i = 1; i <= seconds; i++)
            {
                System.Threading.Thread.Sleep(1000);
                if (GetSystemTimes(out var idle1, out var kernel1, out var user1))
                {
                    double idle = idle1 - idle0, total = (kernel1 - kernel0) + (user1 - user0);
                    if (total > 0) cpu.Add(Math.Max(0, Math.Min(100, (total - idle) * 100.0 / total)));
                    idle0 = idle1; kernel0 = kernel1; user0 = user1;
                }
                var s = new Native.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX)) };
                if (Native.GlobalMemoryStatusEx(ref s)) mem.Add((long)((s.ullTotalPhys - s.ullAvailPhys) / 1024 / 1024));
                var pi = new PERFORMANCE_INFORMATION { cb = (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION)) };
                if (GetPerformanceInfo(out pi, pi.cb)) commit.Add((long)((ulong)pi.CommitTotal * (ulong)pi.PageSize / 1024 / 1024));
                progress?.Report(i);
            }
            m.Seconds = seconds;
            if (cpu.Count > 0) { m.CpuPercent = Math.Round(cpu.Average(), 1); m.CpuPeakPercent = Math.Round(cpu.Max(), 1); }
            if (mem.Count > 0) { m.MemoryUsedMb = (long)mem.Average(); m.MemoryPeakMb = mem.Max(); }
            if (commit.Count > 0) m.CommittedMb = (long)commit.Average();
            return m;
        }

        private Measurement TakeOnce(string label)
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
            Try(() => m.BootMs = LastDuration(100, "BootTime"));
            Try(() => m.ShutdownMs = LastDuration(200, "ShutdownTime"));
            Try(() =>
            {
                var pi = new PERFORMANCE_INFORMATION { cb = (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION)) };
                if (GetPerformanceInfo(out pi, pi.cb))
                {
                    m.CommittedMb = (long)((ulong)pi.CommitTotal * (ulong)pi.PageSize / 1024 / 1024);
                    m.Handles = (int)pi.HandleCount;
                    m.Threads = (int)pi.ThreadCount;
                }
            });
            Try(() => m.TempFolderMb = FolderMb(Path.GetTempPath()));
            Try(() => m.Drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => new DriveSpace { Name = d.Name.TrimEnd('\\'), FreeMb = d.AvailableFreeSpace / 1024 / 1024, TotalMb = d.TotalSize / 1024 / 1024 }).ToList());
            return m;
        }

        private static long FolderMb(string path)
        {
            long total = 0;
            foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.TopDirectoryOnly)) { try { total += f.Length; } catch (Exception) { } }
            foreach (var d in new DirectoryInfo(path).EnumerateDirectories())
            {
                try { foreach (var f in d.EnumerateFiles("*", SearchOption.AllDirectories)) { try { total += f.Length; } catch (Exception) { } } }
                catch (Exception) { }
            }
            return total / 1024 / 1024;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            public UIntPtr CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable, SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
            public uint HandleCount, ProcessCount, ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION info, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        /// <summary>Diagnostics-Performance logs each boot (event 100, BootTime) and shutdown (event 200, ShutdownTime) in milliseconds.</summary>
        private static long? LastDuration(int eventId, string field)
        {
            var q = new EventLogQuery("Microsoft-Windows-Diagnostics-Performance/Operational", PathType.LogName, "*[System[EventID=" + eventId + "]]")
            { ReverseDirection = true };
            using (var reader = new EventLogReader(q))
            using (var e = reader.ReadEvent())
            {
                if (e == null) return null;
                var x = XDocument.Parse(e.ToXml());
                XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
                var boot = x.Descendants(ns + "Data").FirstOrDefault(d => (string)d.Attribute("Name") == field);
                return boot != null && long.TryParse(boot.Value, out var ms) ? ms : (long?)null;
            }
        }

        private static void Try(Action a)
        {
            try { a(); } catch (Exception) { }
        }
    }
}
