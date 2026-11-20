using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    public enum StartupSource { UserRun, MachineRun, MachineRun32, UserFolder, CommonFolder }

    public sealed class StartupItem
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Description { get; set; }
        public string Publisher { get; set; }
        public StartupSource Source { get; set; }
        public bool Enabled { get; set; }
        public bool ForAllUsers => Source != StartupSource.UserRun && Source != StartupSource.UserFolder;
    }

    /// <summary>
    /// Enables and disables startup programs the same way Task Manager does (the StartupApproved flags),
    /// so the entry is never deleted and can always be turned back on, here or in Task Manager.
    /// </summary>
    public sealed class StartupManager
    {
        private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Run32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";
        private readonly IRegistry _reg;

        public StartupManager(IRegistry reg) { _reg = reg; }

        private static (Hive Hive, string ApprovedKey) ApprovedFor(StartupSource s)
        {
            switch (s)
            {
                case StartupSource.UserRun: return (Hive.CurrentUser, Approved + "Run");
                case StartupSource.MachineRun: return (Hive.LocalMachine, Approved + "Run");
                case StartupSource.MachineRun32: return (Hive.LocalMachine, Approved + "Run32");
                case StartupSource.UserFolder: return (Hive.CurrentUser, Approved + "StartupFolder");
                default: return (Hive.LocalMachine, Approved + "StartupFolder");
            }
        }

        public IList<StartupItem> List()
        {
            var items = new List<StartupItem>();
            AddRun(items, Hive.CurrentUser, Run, StartupSource.UserRun);
            AddRun(items, Hive.LocalMachine, Run, StartupSource.MachineRun);
            if (Environment.Is64BitOperatingSystem) AddRun(items, Hive.LocalMachine, Run32, StartupSource.MachineRun32);
            AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.UserFolder);
            AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.CommonFolder);
            foreach (var i in items)
            {
                i.Enabled = IsEnabled(i);
                Describe(i);
            }
            return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private void AddRun(List<StartupItem> items, Hive hive, string key, StartupSource src)
        {
            foreach (var name in _reg.GetValueNames(hive, key))
            {
                if (string.IsNullOrEmpty(name)) continue;
                var cmd = _reg.GetValue(hive, key, name)?.Data as string;
                if (string.IsNullOrWhiteSpace(cmd)) continue;
                items.Add(new StartupItem { Name = name, Command = cmd, Source = src });
            }
        }

        private static void AddFolder(List<StartupItem> items, string folder, StartupSource src)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            foreach (var f in Directory.GetFiles(folder))
            {
                if (Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                items.Add(new StartupItem { Name = Path.GetFileName(f), Command = f, Source = src });
            }
        }

        private bool IsEnabled(StartupItem i)
        {
            var (hive, key) = ApprovedFor(i.Source);
            var v = _reg.GetValue(hive, key, i.Name);
            if (v == null || !(v.Data is byte[] b) || b.Length == 0) return true;
            // Even first byte = enabled (02, 06), odd = disabled (03, 07).
            return (b[0] & 1) == 0;
        }

        public void SetEnabled(StartupItem i, bool enabled)
        {
            var (hive, key) = ApprovedFor(i.Source);
            var data = new byte[12];
            data[0] = enabled ? (byte)0x02 : (byte)0x03;
            if (!enabled) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            _reg.SetValue(hive, key, i.Name, RegValue.Bin(data));
            i.Enabled = enabled;
        }

        public int CountEnabled()
        {
            try { return List().Count(i => i.Enabled); } catch (Exception) { return -1; }
        }

        private static void Describe(StartupItem i)
        {
            try
            {
                var exe = ExtractExe(i.Command);
                if (exe == null || !File.Exists(exe)) return;
                var info = FileVersionInfo.GetVersionInfo(exe);
                i.Description = string.IsNullOrWhiteSpace(info.FileDescription) ? null : info.FileDescription.Trim();
                i.Publisher = string.IsNullOrWhiteSpace(info.CompanyName) ? null : info.CompanyName.Trim();
            }
            catch (Exception) { }
        }

        internal static string ExtractExe(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            var c = Environment.ExpandEnvironmentVariables(command.Trim());
            if (c.StartsWith("\""))
            {
                int end = c.IndexOf('"', 1);
                return end > 1 ? c.Substring(1, end - 1) : null;
            }
            int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe > 0 ? c.Substring(0, exe + 4) : c;
        }
    }
}
