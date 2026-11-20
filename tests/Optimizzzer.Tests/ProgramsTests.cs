using System;
using System.Collections.Generic;
using System.Linq;
using Optimizzzer.Core.Tools;
using Xunit;

namespace Optimizzzer.Tests
{
    public class ProgramsTests
    {
        private sealed class Probe : ILeftoverProbe
        {
            public HashSet<string> Dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<(string, string)> Links = new List<(string, string)>();
            public List<(string, string)> TaskList = new List<(string, string)>();
            public List<(string, string)> ServiceList = new List<(string, string)>();

            public bool DirectoryExists(string p) => Dirs.Contains(p);
            public long DirectorySize(string p) => 1000;
            public IEnumerable<string> Directories(string root) =>
                Dirs.Where(d => d.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) && d.Substring(root.Length + 1).IndexOf('\\') < 0);
            public bool RegistryKeyExists(string p) => Keys.Contains(p);
            public IEnumerable<string> RegistrySubKeys(string p) =>
                Keys.Where(k => k.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase) && k.Substring(p.Length + 1).IndexOf('\\') < 0);
            public IEnumerable<(string Shortcut, string Target)> Shortcuts() => Links;
            public IEnumerable<(string Task, string Command)> ScheduledTasks() => TaskList;
            public IEnumerable<(string Service, string Command)> Services() => ServiceList;
            public bool FileExists(string p) => Files.Contains(p);
            public IEnumerable<string> DataRoots() => new[] { @"C:\Users\u\AppData\Roaming", @"C:\ProgramData", @"C:\Program Files" };
            public IEnumerable<string> ProtectedRoots() => new[] { @"C:\Windows", @"C:\Program Files", @"C:\Program Files\Common Files", @"C:\ProgramData", @"C:\Users\u\AppData\Roaming", @"C:\Users" };
        }

        private static InstalledProgram Prog(string name, string publisher, string loc, string key = null) => new InstalledProgram
        {
            Name = name, Publisher = publisher, InstallLocation = loc, Key = key ?? @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + name,
            UninstallString = "\"" + loc + "\\uninstall.exe\"",
        };

        [Fact]
        public void Games_are_told_apart_by_their_store()
        {
            Assert.Equal("Steam", Prog("Counter-Strike 2", "Valve", @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive", @"HKLM\SOFTWARE\...\Uninstall\Steam App 730").Platform);
            Assert.Equal("Riot Games", Prog("VALORANT", "Riot Games, Inc", @"C:\Riot Games\VALORANT\live", @"HKCU\...\Uninstall\Riot Game valorant.live").Platform);
            Assert.Equal("Epic Games", Prog("Fortnite", "Epic Games", @"C:\Program Files\Epic Games\Fortnite").Platform);
            Assert.Null(Prog("Steam", "Valve", @"C:\Program Files (x86)\Steam").Platform);
            Assert.Null(Prog("7-Zip", "Igor Pavlov", @"C:\Program Files\7-Zip").Platform);
        }

        [Fact]
        public void Runtimes_and_updates_are_hidden_by_default()
        {
            Assert.True(ProgramClassifier.IsSystemLike(new InstalledProgram { Name = "Microsoft Visual C++ 2015-2022 Redistributable (x64)", UninstallString = "x" }));
            Assert.True(ProgramClassifier.IsSystemLike(new InstalledProgram { Name = "Something", UninstallString = "x", SystemComponent = true }));
            Assert.True(ProgramClassifier.IsSystemLike(new InstalledProgram { Name = "No uninstaller" }));
            Assert.False(ProgramClassifier.IsSystemLike(new InstalledProgram { Name = "VALORANT", UninstallString = "x" }));
        }

        [Fact]
        public void Leftovers_include_the_install_folder_orphans_and_named_data()
        {
            var p = Prog("Contoso Editor 2.1 (x64)", "Contoso Ltd.", @"C:\Program Files\Contoso Editor");
            var probe = new Probe();
            probe.Dirs.Add(@"C:\Program Files\Contoso Editor");
            probe.Dirs.Add(@"C:\Users\u\AppData\Roaming\Contoso Editor");
            probe.Dirs.Add(@"C:\ProgramData\Contoso");
            probe.Dirs.Add(@"C:\ProgramData\Contoso\Contoso Editor");
            probe.Keys.Add(p.Key);
            probe.Keys.Add(@"HKCU\Software\Contoso");
            probe.Keys.Add(@"HKCU\Software\Contoso\Contoso Editor");
            probe.Links.Add((@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Contoso Editor.lnk", @"C:\Program Files\Contoso Editor\editor.exe"));
            probe.TaskList.Add((@"\Contoso Updater", "\"C:\\Program Files\\Contoso Editor\\update.exe\" /check"));
            probe.ServiceList.Add(("ContosoSvc", @"C:\Program Files\Contoso Editor\svc.exe"));

            var found = LeftoverScanner.Find(p, new InstalledProgram[0], probe);
            Assert.Contains(found, l => l.Kind == LeftoverKind.Folder && l.Path == @"C:\Program Files\Contoso Editor" && l.Confidence == Confidence.High);
            Assert.Contains(found, l => l.Kind == LeftoverKind.RegistryKey && l.Path == p.Key && l.Confidence == Confidence.High);
            Assert.Contains(found, l => l.Kind == LeftoverKind.Shortcut);
            Assert.Contains(found, l => l.Kind == LeftoverKind.ScheduledTask);
            Assert.Contains(found, l => l.Kind == LeftoverKind.Service);
            Assert.Contains(found, l => l.Path == @"C:\Users\u\AppData\Roaming\Contoso Editor" && l.Confidence == Confidence.Medium);
            Assert.Contains(found, l => l.Path == @"C:\ProgramData\Contoso\Contoso Editor" && l.Confidence == Confidence.Medium);
            Assert.Contains(found, l => l.Path == @"HKCU\Software\Contoso\Contoso Editor" && l.Confidence == Confidence.Medium);
            // The publisher's own folder and key are never proposed: other programs may use them.
            Assert.DoesNotContain(found, l => l.Path == @"C:\ProgramData\Contoso" || l.Path == @"HKCU\Software\Contoso");
        }

        [Fact]
        public void Protected_and_shared_folders_are_never_proposed()
        {
            var probe = new Probe();
            probe.Dirs.Add(@"C:\Program Files");
            probe.Dirs.Add(@"C:\Windows\System32");
            probe.Dirs.Add(@"C:\Program Files\Shared Suite");
            // Install location pointing at a protected root, at Windows, and at a folder another program still uses.
            Assert.DoesNotContain(LeftoverScanner.Find(Prog("Bad1", "X", @"C:\Program Files"), new InstalledProgram[0], probe), l => l.Kind == LeftoverKind.Folder);
            Assert.DoesNotContain(LeftoverScanner.Find(Prog("Bad2", "X", @"C:\Windows\System32"), new InstalledProgram[0], probe), l => l.Kind == LeftoverKind.Folder);
            var other = Prog("Suite Tool", "S", @"C:\Program Files\Shared Suite\Tool");
            Assert.DoesNotContain(LeftoverScanner.Find(Prog("Suite Main", "S", @"C:\Program Files\Shared Suite"), new[] { other }, probe), l => l.Kind == LeftoverKind.Folder);
        }

        [Fact]
        public void Uninstall_command_prefers_silent_and_turns_msi_install_into_remove()
        {
            var msi = new InstalledProgram { UninstallString = "MsiExec.exe /I{12345678-1234-1234-1234-123456789012}", WindowsInstaller = true };
            Assert.Equal(("msiexec.exe", "/x {12345678-1234-1234-1234-123456789012} /qn /norestart", true), UninstallCommand.For(msi, true));
            var quiet = new InstalledProgram { UninstallString = "\"C:\\A\\unins000.exe\"", QuietUninstallString = "\"C:\\A\\unins000.exe\" /SILENT" };
            Assert.Equal(("C:\\A\\unins000.exe", "/SILENT", true), UninstallCommand.For(quiet, true));
            Assert.Equal(("C:\\A\\unins000.exe", "", false), UninstallCommand.For(quiet, false));
            var plain = new InstalledProgram { UninstallString = "C:\\Program Files\\B\\uninst.exe /x" };
            Assert.Equal(("C:\\Program Files\\B\\uninst.exe", "/x", false), UninstallCommand.For(plain, true));
        }
    }
}
