using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.Windows
{
    /// <summary>Builds the real implementations once and hands them out.</summary>
    public sealed class WindowsSystem
    {
        public WindowsSystem()
        {
            DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Optimizzzer");
            Registry = new WinRegistry();
            Process = new WinProcess();
            Services = new WinServices(Registry, Process);
            Tasks = new WinTasks();
            Appx = new WinAppx(Process);
            Startup = new StartupManager(Registry);
            Cleanup = new WinCleanup(Process);
            Dns = new WinDns(Process, Registry, DataDir);
            Measurer = new WinMeasurer(Startup);
            Journal = new FileJournalStore(Path.Combine(DataDir, "journal"));
            Measurements = new MeasurementStore(Path.Combine(DataDir, "measurements.json"));
        }

        public string DataDir { get; }
        public WinRegistry Registry { get; }
        public WinProcess Process { get; }
        public WinServices Services { get; }
        public WinTasks Tasks { get; }
        public WinAppx Appx { get; }
        public StartupManager Startup { get; }
        public WinCleanup Cleanup { get; }
        public WinDns Dns { get; }
        public WinMeasurer Measurer { get; }
        public FileJournalStore Journal { get; }
        public MeasurementStore Measurements { get; }
        public WinSystemInfo Info { get; private set; }

        /// <summary>Slow (WMI). Call from a background thread.</summary>
        public SystemContext CreateContext()
        {
            if (Info == null) Info = WinSystemInfo.Read(Registry);
            return new SystemContext
            {
                Registry = Registry, Services = Services, Tasks = Tasks, Process = Process, Appx = Appx,
                RestorePoint = new WinRestorePoint(Registry), Info = Info,
            };
        }

        public static bool IsAdministrator()
        {
            using (var id = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Restarts File Explorer as the normal (non elevated) user. Windows relaunches the shell by itself
        /// after it exits; the fallback starts it with a basic-user token so it never runs as administrator.
        /// </summary>
        public void RestartExplorer()
        {
            Process.Run("taskkill.exe", "/f /im explorer.exe", 15000);
            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(250);
                if (System.Diagnostics.Process.GetProcessesByName("explorer").Any()) return;
            }
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo("runas.exe", "/trustlevel:0x20000 explorer.exe") { UseShellExecute = false, CreateNoWindow = true });
            }
            catch (Exception) { }
        }

        public static void AttachParentConsole() => Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
    }
}
