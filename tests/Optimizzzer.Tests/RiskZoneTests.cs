using System;
using System.IO;
using System.Linq;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;
using Xunit;

namespace Optimizzzer.Tests
{
    public class RiskZoneTests
    {
        [Fact]
        public void EveryItemIsComplete()
        {
            Assert.Equal(5, RiskZone.All.Count);
            Assert.Equal(RiskZone.All.Count, RiskZone.All.Select(i => i.Id).Distinct().Count());
            foreach (var i in RiskZone.All)
            {
                foreach (var t in new[] { i.Title, i.What, i.Lose, i.Back })
                {
                    Assert.False(string.IsNullOrWhiteSpace(t.Es) || string.IsNullOrWhiteSpace(t.En), i.Id);
                    Assert.DoesNotContain("—", t.Es + t.En);
                }
                Assert.False(string.IsNullOrWhiteSpace(i.ApplyScript + i.RevertScript + i.DetectScript) || i.ApplyScript.Length == 0 || i.DetectScript.Length == 0, i.Id);
                var full = RiskZone.FullScript(i, true);
                Assert.StartsWith("#Requires -RunAsAdministrator", full);
                Assert.Contains("bajo tu propia responsabilidad", full);
            }
        }

        [Fact]
        public void EncodedCommandRoundTrips()
        {
            var args = RiskZone.PowerShellArgs("Write-Output 'ñ \"x\"'");
            var b64 = args.Split(' ').Last();
            Assert.Equal("Write-Output 'ñ \"x\"'", System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(b64)));
        }

        [Fact]
        public void PauseClampsReadsBackAndResumes()
        {
            var sys = new FakeSystem();
            var now = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
            var until = UpdatePause.Pause(sys.Context, now, new DateTime(2222, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(now.AddDays(UpdatePause.MaxDays), until);
            Assert.Equal(until, UpdatePause.PausedUntil(sys.Context, now));
            Assert.Equal((uint)UpdatePause.MaxDays, sys.Registry.GetValue(Hive.LocalMachine, UpdatePause.Key, UpdatePause.MaxDaysName).Data);
            UpdatePause.Resume(sys.Context);
            Assert.Null(UpdatePause.PausedUntil(sys.Context, now));
            Assert.Null(sys.Registry.GetValue(Hive.LocalMachine, UpdatePause.Key, UpdatePause.MaxDaysName));
        }

        /// <summary>The scripts/ folder in the repo is exactly what the app saves. OPTIMIZZZER_WRITE_SCRIPTS=1 regenerates it.</summary>
        [Fact]
        public void RepoScriptsAreCurrent()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !Directory.Exists(Path.Combine(dir, "scripts"))) dir = Path.GetDirectoryName(dir);
            Assert.NotNull(dir);
            var scripts = Path.Combine(dir, "scripts");
            var write = Environment.GetEnvironmentVariable("OPTIMIZZZER_WRITE_SCRIPTS") == "1";
            foreach (var i in RiskZone.All)
                foreach (var es in new[] { true, false })
                {
                    var path = Path.Combine(scripts, (es ? "es" : "en"), i.Id + ".ps1");
                    var text = RiskZone.FullScript(i, es);
                    if (write) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text, new System.Text.UTF8Encoding(true)); }
                    Assert.Equal(text, File.ReadAllText(path));
                }
        }
    }
}
