using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Win32;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.Windows
{
    public sealed class UninstallResult
    {
        public bool Removed { get; set; }
        public int ExitCode { get; set; }
        public bool Silent { get; set; }
        public string Error { get; set; }
    }

    public sealed class LeftoverResult
    {
        public Leftover Item { get; set; }
        public string Error { get; set; }
        public bool Ok => Error == null;
    }

    /// <summary>
    /// Classic programs and games from the Windows "Uninstall" keys: list them, run their uninstaller and clean
    /// what it left. Files go to the Recycle Bin and registry keys are exported to a .reg before deletion.
    /// </summary>
    public sealed class WinPrograms
    {
        private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private readonly IProcessRunner _proc;
        private readonly string _backupDir;

        public WinPrograms(IProcessRunner proc, string dataDir)
        {
            _proc = proc;
            _backupDir = Path.Combine(dataDir, "backups");
        }

        public IList<InstalledProgram> List()
        {
            var list = new List<InstalledProgram>();
            Read(RegistryHive.LocalMachine, RegistryView.Registry64, "HKLM", list);
            if (Environment.Is64BitOperatingSystem) Read(RegistryHive.LocalMachine, RegistryView.Registry32, @"HKLM\SOFTWARE\WOW6432Node", list);
            Read(RegistryHive.CurrentUser, RegistryView.Default, "HKCU", list);
            // The same program can be listed in two places (per-user and per-machine): keep one.
            return list.GroupBy(p => (p.Name + "|" + p.Version).ToLowerInvariant()).Select(g => g.First())
                .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static void Read(RegistryHive hive, RegistryView view, string prefix, List<InstalledProgram> list)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var un = root.OpenSubKey(UninstallPath))
                {
                    if (un == null) return;
                    foreach (var name in un.GetSubKeyNames())
                    {
                        try
                        {
                            using (var k = un.OpenSubKey(name))
                            {
                                var display = k?.GetValue("DisplayName") as string;
                                if (string.IsNullOrWhiteSpace(display)) continue;
                                var keyPath = (prefix.Contains("WOW6432Node") ? prefix + @"\Microsoft\Windows\CurrentVersion\Uninstall" : prefix + "\\" + UninstallPath) + "\\" + name;
                                list.Add(new InstalledProgram
                                {
                                    Key = keyPath,
                                    Name = display.Trim(),
                                    Publisher = (k.GetValue("Publisher") as string)?.Trim(),
                                    Version = (k.GetValue("DisplayVersion") as string)?.Trim(),
                                    SizeBytes = ToLong(k.GetValue("EstimatedSize")) * 1024,
                                    InstallDate = ParseDate(k.GetValue("InstallDate") as string),
                                    InstallLocation = (k.GetValue("InstallLocation") as string)?.Trim().Trim('"'),
                                    UninstallString = k.GetValue("UninstallString") as string,
                                    QuietUninstallString = k.GetValue("QuietUninstallString") as string,
                                    DisplayIcon = k.GetValue("DisplayIcon") as string,
                                    SystemComponent = ToLong(k.GetValue("SystemComponent")) == 1,
                                    IsUpdate = k.GetValue("ParentKeyName") != null || (k.GetValue("ReleaseType") as string)?.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0,
                                    WindowsInstaller = ToLong(k.GetValue("WindowsInstaller")) == 1,
                                });
                            }
                        }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception) { }
        }

        private static long ToLong(object v) { try { return v == null ? 0 : Convert.ToInt64(v); } catch (Exception) { return 0; } }

        private static DateTime? ParseDate(string s) =>
            s != null && DateTime.TryParseExact(s.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateTime?)null;

        public bool StillInstalled(InstalledProgram p) => KeyExists(p.Key);

        // Uninstallers that copy themselves to %TEMP% and run from there after the first process exits.
        private static readonly Regex HelperProcess = new Regex(@"^(Au_|Un_A|Un_|unins\d+|_iu14D2N|uninst|uninstall|Uninstall|setup|msiexec)", RegexOptions.IgnoreCase);

        /// <summary>
        /// Runs the program's uninstaller and waits until it really finished (helpers included), up to 30 minutes.
        /// Silent when the program supports it; otherwise its own window appears and the user answers it.
        /// </summary>
        public UninstallResult Uninstall(InstalledProgram p, bool preferSilent, CancellationToken ct)
        {
            var (file, args, silent) = UninstallCommand.For(p, preferSilent);
            if (string.IsNullOrWhiteSpace(file)) return new UninstallResult { Error = "No uninstaller registered" };
            var before = new HashSet<int>(Process.GetProcesses().Select(x => x.Id));
            int code;
            try
            {
                using (var proc = Process.Start(new ProcessStartInfo(Environment.ExpandEnvironmentVariables(file), args) { UseShellExecute = false }))
                {
                    while (!proc.WaitForExit(500)) ct.ThrowIfCancellationRequested();
                    code = proc.ExitCode;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) { return new UninstallResult { Error = ex.Message, Silent = silent }; }

            var until = DateTime.UtcNow.AddMinutes(30);
            while (StillInstalled(p) && DateTime.UtcNow < until)
            {
                ct.ThrowIfCancellationRequested();
                bool helperRunning = Process.GetProcesses().Any(x => !before.Contains(x.Id) && HelperProcess.IsMatch(SafeName(x)));
                if (!helperRunning) break;
                Thread.Sleep(1000);
            }
            Thread.Sleep(500);
            return new UninstallResult { ExitCode = code, Silent = silent, Removed = !StillInstalled(p) };
        }

        private static string SafeName(Process p) { try { return p.ProcessName; } catch (Exception) { return ""; } }

        // ---------- Leftovers ----------

        public IList<Leftover> FindLeftovers(InstalledProgram p) =>
            LeftoverScanner.Find(p, List(), new Probe());

        /// <summary>Deletes what the user ticked. Files and folders go to the Recycle Bin; keys are backed up first.</summary>
        public IList<LeftoverResult> Delete(IEnumerable<Leftover> items)
        {
            var results = new List<LeftoverResult>();
            Directory.CreateDirectory(_backupDir);
            foreach (var item in items)
            {
                var r = new LeftoverResult { Item = item };
                try
                {
                    switch (item.Kind)
                    {
                        case LeftoverKind.Folder:
                        case LeftoverKind.Shortcut:
                            if (Directory.Exists(item.Path) || File.Exists(item.Path)) ToRecycleBin(item.Path);
                            break;
                        case LeftoverKind.RegistryKey:
                            var file = Path.Combine(_backupDir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Safe(item.Path.Split('\\').Last()) + ".reg");
                            var export = _proc.Run("reg.exe", "export \"" + item.Path + "\" \"" + file + "\" /y", 60000);
                            if (!export.Ok) throw new InvalidOperationException("reg export: " + (export.Output + export.Error).Trim());
                            var del = _proc.Run("reg.exe", "delete \"" + item.Path + "\" /f", 60000);
                            if (!del.Ok) throw new InvalidOperationException("reg delete: " + (del.Output + del.Error).Trim());
                            break;
                        case LeftoverKind.ScheduledTask:
                            var t = _proc.Run("schtasks.exe", "/delete /tn \"" + item.Path + "\" /f", 60000);
                            if (!t.Ok) throw new InvalidOperationException((t.Output + t.Error).Trim());
                            break;
                        case LeftoverKind.Service:
                            _proc.Run("sc.exe", "stop \"" + item.Path + "\"", 30000);
                            var s = _proc.Run("sc.exe", "delete \"" + item.Path + "\"", 60000);
                            if (!s.Ok) throw new InvalidOperationException((s.Output + s.Error).Trim());
                            break;
                    }
                }
                catch (Exception ex) { r.Error = ex.Message; }
                results.Add(r);
            }
            return results;
        }

        public string BackupFolder => _backupDir;

        private static string Safe(string s) => string.Concat((s ?? "key").Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));

        private static bool KeyExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var (hive, view, sub) = Split(path);
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var k = root.OpenSubKey(sub)) return k != null;
            }
            catch (Exception) { return false; }
        }

        private static (RegistryHive, RegistryView, string) Split(string path)
        {
            var i = path.IndexOf('\\');
            var hive = i < 0 ? path : path.Substring(0, i);
            var sub = i < 0 ? "" : path.Substring(i + 1);
            var h = hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase) ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;
            return (h, RegistryView.Registry64, sub);
        }

        // ---------- Recycle Bin (SHFileOperation keeps it recoverable) ----------

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

        private static void ToRecycleBin(string path)
        {
            const uint FO_DELETE = 3;
            const ushort FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;
            var op = new SHFILEOPSTRUCT { wFunc = FO_DELETE, pFrom = path + "\0\0", fFlags = FOF_SILENT | FOF_NOCONFIRMATION | FOF_ALLOWUNDO | FOF_NOERRORUI };
            int rc = SHFileOperation(ref op);
            if (rc != 0 || op.fAnyOperationsAborted) throw new IOException("Could not move to the Recycle Bin (code " + rc + "). A file may be in use: close the program and try again.");
            if (Directory.Exists(path) || File.Exists(path)) throw new IOException("Still there after moving to the Recycle Bin: a file may be in use.");
        }

        /// <summary>The real machine, for the leftover scanner.</summary>
        private sealed class Probe : ILeftoverProbe
        {
            public bool DirectoryExists(string path) { try { return Directory.Exists(path); } catch (Exception) { return false; } }

            public long DirectorySize(string path)
            {
                long total = 0;
                try
                {
                    foreach (var f in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        try { total += f.Length; } catch (Exception) { }
                    }
                }
                catch (Exception) { }
                return total;
            }

            public IEnumerable<string> Directories(string root)
            {
                try { return Directory.Exists(root) ? Directory.GetDirectories(root) : new string[0]; }
                catch (Exception) { return new string[0]; }
            }

            public bool RegistryKeyExists(string path) => KeyExists(path);

            public IEnumerable<string> RegistrySubKeys(string path)
            {
                var (hive, view, sub) = Split(path);
                try
                {
                    using (var root = RegistryKey.OpenBaseKey(hive, view))
                    using (var k = root.OpenSubKey(sub))
                        return k == null ? new string[0] : k.GetSubKeyNames().Select(n => path + "\\" + n).ToArray();
                }
                catch (Exception) { return new string[0]; }
            }

            public IEnumerable<(string Shortcut, string Target)> Shortcuts()
            {
                var dirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                };
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) yield break;
                var shell = Activator.CreateInstance(shellType);
                foreach (var dir in dirs.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)))
                {
                    string[] links;
                    try { links = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories); } catch (Exception) { continue; }
                    foreach (var lnk in links)
                    {
                        string target = null;
                        try
                        {
                            var sc = shellType.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                            target = sc.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, sc, null) as string;
                            Marshal.ReleaseComObject(sc);
                        }
                        catch (Exception) { }
                        if (!string.IsNullOrEmpty(target)) yield return (lnk, target);
                    }
                }
                Marshal.ReleaseComObject(shell);
            }

            public IEnumerable<(string Task, string Command)> ScheduledTasks()
            {
                // Task definitions are XML files under System32\Tasks; reading them is much faster than schtasks /query.
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Tasks");
                if (!Directory.Exists(root)) yield break;
                string[] files;
                try { files = Directory.GetFiles(root, "*", SearchOption.AllDirectories); } catch (Exception) { yield break; }
                foreach (var f in files)
                {
                    string cmd = null;
                    try
                    {
                        var x = XDocument.Load(f);
                        cmd = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "Command")?.Value;
                    }
                    catch (Exception) { }
                    if (!string.IsNullOrEmpty(cmd)) yield return ("\\" + f.Substring(root.Length).TrimStart('\\'), cmd);
                }
            }

            public IEnumerable<(string Service, string Command)> Services()
            {
                var list = new List<(string, string)>();
                try
                {
                    using (var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var svcs = root.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
                        foreach (var name in svcs.GetSubKeyNames())
                            using (var k = svcs.OpenSubKey(name))
                                if (k?.GetValue("ImagePath", null, RegistryValueOptions.None) is string img) list.Add((name, img));
                }
                catch (Exception) { }
                return list;
            }

            public bool FileExists(string path) { try { return File.Exists(Environment.ExpandEnvironmentVariables(path)); } catch (Exception) { return false; } }

            public IEnumerable<string> DataRoots() => new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow"),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            }.Where(d => !string.IsNullOrEmpty(d)).Distinct(StringComparer.OrdinalIgnoreCase);

            public IEnumerable<string> ProtectedRoots()
            {
                var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft"),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft"),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                    Path.Combine(profile, "AppData", "LocalLow"),
                    profile,
                    Path.GetDirectoryName(profile),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                }.Where(d => !string.IsNullOrEmpty(d));
            }
        }
    }
}
