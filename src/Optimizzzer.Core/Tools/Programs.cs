using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Optimizzzer.Core.Tools
{
    /// <summary>A classic (Win32) program or game, as registered under the Windows "Uninstall" keys.</summary>
    public sealed class InstalledProgram
    {
        /// <summary>Registry path of its Uninstall entry, e.g. HKLM\SOFTWARE\...\Uninstall\{GUID}. Unique.</summary>
        public string Key { get; set; }
        public string Name { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public long SizeBytes { get; set; }
        public DateTime? InstallDate { get; set; }
        public string InstallLocation { get; set; }
        public string UninstallString { get; set; }
        public string QuietUninstallString { get; set; }
        public string DisplayIcon { get; set; }
        public bool SystemComponent { get; set; }
        /// <summary>Windows updates, patches and components that belong to another program.</summary>
        public bool IsUpdate { get; set; }
        public bool WindowsInstaller { get; set; }

        public bool IsGame => ProgramClassifier.Platform(this) != null;
        public string Platform => ProgramClassifier.Platform(this);
    }

    public static class ProgramClassifier
    {
        private static readonly (string Name, Regex Match)[] Platforms =
        {
            ("Steam", new Regex(@"^Steam App \d+$|\\steamapps\\common\\", RegexOptions.IgnoreCase)),
            ("Epic Games", new Regex(@"com\.epicgames\.launcher|\\Epic Games\\", RegexOptions.IgnoreCase)),
            ("Riot Games", new Regex(@"^Riot Game |\\Riot Games\\", RegexOptions.IgnoreCase)),
            ("EA", new Regex(@"\\EA Games\\|\\Electronic Arts\\", RegexOptions.IgnoreCase)),
            ("Ubisoft", new Regex(@"^Uplay Install \d+$|\\Ubisoft Game Launcher\\games\\|\\Ubisoft\\", RegexOptions.IgnoreCase)),
            ("Battle.net", new Regex(@"\\Battle\.net\\|\\Blizzard Entertainment\\", RegexOptions.IgnoreCase)),
            ("GOG", new Regex(@"\\GOG Galaxy\\Games\\|\\GOG Games\\", RegexOptions.IgnoreCase)),
            ("Xbox", new Regex(@"\\XboxGames\\", RegexOptions.IgnoreCase)),
        };

        // Launchers themselves are programs, not games.
        private static readonly Regex Launcher = new Regex(@"^(Steam|Epic Games Launcher|Riot Client|EA app|Ubisoft Connect|Battle\.net|GOG GALAXY)$", RegexOptions.IgnoreCase);

        /// <summary>The game store a program came from, or null when it is not a game.</summary>
        public static string Platform(InstalledProgram p)
        {
            if (p == null || Launcher.IsMatch(p.Name ?? "")) return null;
            var keyName = (p.Key ?? "").Split('\\').LastOrDefault() ?? "";
            var haystack = keyName + "|" + (p.InstallLocation ?? "") + "|" + (p.UninstallString ?? "") + "|" + (p.DisplayIcon ?? "");
            foreach (var (name, match) in Platforms)
                if (match.IsMatch(keyName) || match.IsMatch(haystack)) return name;
            return null;
        }

        private static readonly Regex Runtime = new Regex(
            @"^Microsoft Visual C\+\+ .*Redistributable|^Microsoft \.NET|^Microsoft Windows Desktop Runtime|^Microsoft ASP\.NET|^Windows SDK|^Windows Software Development Kit|^Microsoft Update Health Tools|^Update for |^Security Update|^Hotfix|\(KB\d+\)",
            RegexOptions.IgnoreCase);

        /// <summary>Runtimes, updates and hidden components: shown only with "Also show system components".</summary>
        public static bool IsSystemLike(InstalledProgram p) =>
            p.SystemComponent || p.IsUpdate || string.IsNullOrWhiteSpace(p.UninstallString) || Runtime.IsMatch(p.Name ?? "");
    }

    public enum LeftoverKind { Folder, RegistryKey, Shortcut, ScheduledTask, Service }
    public enum Confidence { High, Medium }

    public sealed class Leftover
    {
        public LeftoverKind Kind { get; set; }
        public string Path { get; set; }
        public long SizeBytes { get; set; }
        public Confidence Confidence { get; set; }
        public Text Reason { get; set; }
    }

    /// <summary>What the leftover scanner needs from the machine (real on Windows, fake in tests).</summary>
    public interface ILeftoverProbe
    {
        bool DirectoryExists(string path);
        long DirectorySize(string path);
        IEnumerable<string> Directories(string root);
        bool RegistryKeyExists(string path);
        IEnumerable<string> RegistrySubKeys(string path);
        /// <summary>Shortcut file paths with the full path of their target.</summary>
        IEnumerable<(string Shortcut, string Target)> Shortcuts();
        /// <summary>Scheduled tasks (path) and the program they run.</summary>
        IEnumerable<(string Task, string Command)> ScheduledTasks();
        /// <summary>Services (name) and the program they run.</summary>
        IEnumerable<(string Service, string Command)> Services();
        bool FileExists(string path);
        /// <summary>Roots for per-program data: %AppData%, %LocalAppData%, %ProgramData%, Program Files folders.</summary>
        IEnumerable<string> DataRoots();
        IEnumerable<string> ProtectedRoots();
    }

    /// <summary>
    /// Finds what an uninstaller left behind. It is conservative on purpose: anything in Windows or shared with another
    /// installed program is never proposed, and anything matched only by name is "Medium" and left unticked.
    /// </summary>
    public static class LeftoverScanner
    {
        public static IList<Leftover> Find(InstalledProgram p, IEnumerable<InstalledProgram> stillInstalled, ILeftoverProbe probe)
        {
            var found = new List<Leftover>();
            var others = stillInstalled.Where(o => !string.Equals(o.Key, p.Key, StringComparison.OrdinalIgnoreCase)).ToList();
            var protectedRoots = probe.ProtectedRoots().Select(Norm).ToList();

            bool UsedByOther(string dir)
            {
                var d = Norm(dir);
                return others.Any(o =>
                {
                    var loc = Norm(o.InstallLocation);
                    var exe = Norm(ExecutableOf(o.UninstallString));
                    return (loc.Length > 0 && (loc.StartsWith(d + "\\") || loc == d || d.StartsWith(loc + "\\")))
                        || (exe.Length > 0 && exe.StartsWith(d + "\\"));
                });
            }

            bool Allowed(string dir)
            {
                var d = Norm(dir);
                if (d.Length < 4) return false;                                   // a drive root
                if (protectedRoots.Any(r => d == r || r.StartsWith(d + "\\"))) return false; // a protected folder or one of its parents
                if (protectedRoots.Any(r => d.StartsWith(r + "\\") && IsWindowsRoot(r))) return false; // anything inside Windows
                return !UsedByOther(d);
            }

            void Add(LeftoverKind kind, string path, Confidence c, string es, string en, long size = 0)
            {
                if (found.Any(f => f.Kind == kind && string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) return;
                found.Add(new Leftover { Kind = kind, Path = path, Confidence = c, SizeBytes = size, Reason = new Text(es, en) });
            }

            // 1. The install folder, if the uninstaller left it.
            var install = (p.InstallLocation ?? "").Trim().Trim('"').TrimEnd('\\');
            if (install.Length > 0 && probe.DirectoryExists(install) && Allowed(install))
                Add(LeftoverKind.Folder, install, Confidence.High, "Carpeta de instalación del programa.", "The program's install folder.", probe.DirectorySize(install));

            // 2. Its own Uninstall entry, if it is still there (the uninstaller failed to remove it).
            if (!string.IsNullOrEmpty(p.Key) && probe.RegistryKeyExists(p.Key))
                Add(LeftoverKind.RegistryKey, p.Key, Confidence.High, "Entrada de desinstalación huérfana.", "Orphaned uninstall entry.");

            // 3. Shortcuts, tasks and services that point into the install folder and whose program no longer exists.
            if (install.Length > 0 && Allowed(install))
            {
                var root = Norm(install) + "\\";
                foreach (var (lnk, target) in probe.Shortcuts())
                    if (Norm(target).StartsWith(root) && !probe.FileExists(target))
                        Add(LeftoverKind.Shortcut, lnk, Confidence.High, "Acceso directo a un programa que ya no existe.", "Shortcut to a program that no longer exists.");
                foreach (var (task, cmd) in probe.ScheduledTasks())
                {
                    var exe = ExecutableOf(cmd);
                    if (Norm(exe).StartsWith(root) && !probe.FileExists(exe))
                        Add(LeftoverKind.ScheduledTask, task, Confidence.High, "Tarea programada que ejecuta un programa que ya no existe.", "Scheduled task that runs a program that no longer exists.");
                }
                foreach (var (svc, cmd) in probe.Services())
                {
                    var exe = ExecutableOf(cmd);
                    if (Norm(exe).StartsWith(root) && !probe.FileExists(exe))
                        Add(LeftoverKind.Service, svc, Confidence.High, "Servicio cuyo programa ya no existe.", "Service whose program no longer exists.");
                }
            }

            // 4. Data folders and settings keys named after the program (and its publisher). Name matches only: Medium.
            var names = NameVariants(p.Name).ToList();
            var publisher = CleanPublisher(p.Publisher);
            foreach (var root in probe.DataRoots())
            {
                foreach (var dir in SafeDirs(probe, root))
                {
                    var leaf = Leaf(dir);
                    if (names.Any(n => Same(leaf, n)) && Allowed(dir))
                        Add(LeftoverKind.Folder, dir, Confidence.Medium, "Carpeta de datos con el mismo nombre que el programa.", "Data folder with the program's name.", probe.DirectorySize(dir));
                    else if (publisher.Length > 0 && Same(leaf, publisher))
                        foreach (var sub in SafeDirs(probe, dir))
                            if (names.Any(n => Same(Leaf(sub), n)) && Allowed(sub))
                                Add(LeftoverKind.Folder, sub, Confidence.Medium, "Carpeta de datos del programa dentro de la de su editor.", "The program's data folder inside its publisher's folder.", probe.DirectorySize(sub));
                }
            }
            foreach (var hive in new[] { @"HKCU\Software", @"HKLM\SOFTWARE", @"HKLM\SOFTWARE\WOW6432Node" })
            {
                foreach (var key in probe.RegistrySubKeys(hive))
                {
                    var leaf = key.Split('\\').Last();
                    if (ReservedKeys.Contains(leaf)) continue;
                    if (names.Any(n => Same(leaf, n)) && !others.Any(o => names.Any(n => Same(o.Name, n))))
                        Add(LeftoverKind.RegistryKey, key, Confidence.Medium, "Ajustes del programa en el registro.", "The program's settings in the registry.");
                    else if (publisher.Length > 0 && Same(leaf, publisher))
                        foreach (var sub in probe.RegistrySubKeys(key))
                            if (names.Any(n => Same(sub.Split('\\').Last(), n)))
                                Add(LeftoverKind.RegistryKey, sub, Confidence.Medium, "Ajustes del programa dentro de los de su editor.", "The program's settings inside its publisher's.");
                }
            }
            return found;
        }

        /// <summary>Last part of a Windows path; Path.GetFileName would not split on '\' outside Windows (tests).</summary>
        private static string Leaf(string path) => (path ?? "").TrimEnd('\\').Split('\\').Last();

        // Keys that belong to Windows itself, whatever a program is called.
        private static readonly HashSet<string> ReservedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Microsoft", "Classes", "Policies", "WOW6432Node", "Windows", "Clients", "RegisteredApplications", "Intel", "AMD", "NVIDIA Corporation" };

        private static IEnumerable<string> SafeDirs(ILeftoverProbe probe, string root)
        {
            try { return probe.Directories(root).ToList(); } catch (Exception) { return Enumerable.Empty<string>(); }
        }

        private static bool IsWindowsRoot(string r) => r.EndsWith("\\windows") || r.Contains("\\windows\\");

        /// <summary>"Contoso Editor 2.1 (x64)" also matches "Contoso Editor".</summary>
        public static IEnumerable<string> NameVariants(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) yield break;
            var n = name.Trim();
            yield return n;
            var noParens = Regex.Replace(n, @"\s*\([^)]*\)", "").Trim();
            if (noParens != n && noParens.Length >= 3) yield return noParens;
            var noVersion = Regex.Replace(noParens, @"\s+v?\d+(\.\d+)*\s*$", "").Trim();
            if (noVersion != noParens && noVersion.Length >= 3) yield return noVersion;
        }

        public static string CleanPublisher(string publisher)
        {
            if (string.IsNullOrWhiteSpace(publisher)) return "";
            var p = Regex.Replace(publisher.Trim(), @"[,.]?\s+(Inc|LLC|Ltd|GmbH|Corporation|Corp|Co|S\.A|SA|B\.V|AB|Limited)\.?$", "", RegexOptions.IgnoreCase).Trim();
            // Too generic to match folders safely.
            return p.Length < 3 || p.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) ? "" : p;
        }

        private static bool Same(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        public static string Norm(string path) =>
            string.IsNullOrWhiteSpace(path) ? "" : path.Trim().Trim('"').TrimEnd('\\').ToLowerInvariant();

        /// <summary>The executable in a command line: quoted, or everything up to ".exe".</summary>
        public static string ExecutableOf(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "";
            var c = Environment.ExpandEnvironmentVariables(command.Trim());
            if (c.StartsWith("\""))
            {
                int end = c.IndexOf('"', 1);
                return end > 1 ? c.Substring(1, end - 1) : c.Trim('"');
            }
            int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe > 0 ? c.Substring(0, exe + 4) : c.Split(' ')[0];
        }
    }

    /// <summary>Turns an Uninstall entry into the command to run.</summary>
    public static class UninstallCommand
    {
        private static readonly Regex MsiGuid = new Regex(@"\{[0-9A-F\-]{36}\}", RegexOptions.IgnoreCase);

        /// <summary>
        /// Silent when the program offers it (QuietUninstallString, or Windows Installer with /qn); otherwise the
        /// program's own uninstaller, which shows its window. Returns the file and arguments, and whether it is silent.
        /// </summary>
        public static (string File, string Args, bool Silent) For(InstalledProgram p, bool preferSilent)
        {
            if (preferSilent && !string.IsNullOrWhiteSpace(p.QuietUninstallString))
            {
                var (f, a) = Split(p.QuietUninstallString);
                return (f, a, true);
            }
            var u = p.UninstallString ?? "";
            var guid = MsiGuid.Match(u);
            if ((p.WindowsInstaller || u.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0) && guid.Success)
                return ("msiexec.exe", "/x " + guid.Value + (preferSilent ? " /qn /norestart" : ""), preferSilent);
            var (file, args) = Split(u);
            return (file, args, false);
        }

        public static (string File, string Args) Split(string command)
        {
            var c = (command ?? "").Trim();
            if (c.StartsWith("\""))
            {
                int end = c.IndexOf('"', 1);
                if (end > 1) return (c.Substring(1, end - 1), c.Substring(end + 1).Trim());
            }
            int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0) return (c.Substring(0, exe + 4), c.Substring(exe + 4).Trim());
            int sp = c.IndexOf(' ');
            return sp > 0 ? (c.Substring(0, sp), c.Substring(sp + 1)) : (c, "");
        }
    }
}
