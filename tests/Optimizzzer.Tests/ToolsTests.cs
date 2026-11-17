using System;
using System.IO;
using System.Linq;
using Optimizzzer.Core;
using Optimizzzer.Core.Catalog;
using Optimizzzer.Core.Tools;
using Xunit;

namespace Optimizzzer.Tests
{
    public class ToolsTests
    {
        [Fact]
        public void Cleanup_measures_and_deletes_only_old_files()
        {
            var dir = Path.Combine(Path.GetTempPath(), "optz-clean-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "sub"));
            try
            {
                var oldFile = Path.Combine(dir, "sub", "old.tmp");
                var newFile = Path.Combine(dir, "new.tmp");
                File.WriteAllBytes(oldFile, new byte[1000]);
                File.WriteAllBytes(newFile, new byte[500]);
                File.SetLastWriteTime(oldFile, DateTime.Now.AddDays(-3));

                var target = new CleanupTarget { Id = "t", Title = Text.Empty, Description = Text.Empty, Folders = new[] { dir }, MinAge = TimeSpan.FromHours(24) };
                var engine = new CleanupEngine();
                var est = engine.Estimate(target, null);
                Assert.Equal(1000, est.Bytes);
                Assert.Equal(1, est.Files);

                var r = engine.Run(target, null);
                Assert.Equal(1000, r.FreedBytes);
                Assert.False(File.Exists(oldFile));
                Assert.True(File.Exists(newFile));
                Assert.False(Directory.Exists(Path.Combine(dir, "sub")));
                Assert.True(Directory.Exists(dir));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void Dns_query_is_a_valid_packet()
        {
            var q = DnsBenchmark.BuildQuery("github.com", 0x1234);
            Assert.Equal(0x12, q[0]);
            Assert.Equal(0x34, q[1]);
            Assert.Equal(1, q[5]);
            Assert.Equal(6, q[12]);
            Assert.Equal((byte)'g', q[13]);
            Assert.Equal(new byte[] { 0, 0, 1, 0, 1 }, q.Skip(q.Length - 5).ToArray());
            Assert.Equal(12 + 1 + 6 + 1 + 3 + 5, q.Length);
        }

        [Fact]
        public void Median_handles_even_odd_and_empty()
        {
            Assert.Null(DnsBenchmark.Median(new double[0]));
            Assert.Equal(2, DnsBenchmark.Median(new double[] { 3, 1, 2 }));
            Assert.Equal(2.5, DnsBenchmark.Median(new double[] { 4, 1, 2, 3 }));
        }

        [Fact]
        public void Cli_parses_profiles_and_rejects_unknown_options()
        {
            var o = CliOptions.Parse(new[] { "-Profile", "gaming", "-DryRun", "-Lang", "en" });
            Assert.Equal(ProfileKind.Gaming, o.Profile);
            Assert.True(o.DryRun);
            Assert.Equal(Lang.En, o.Lang);
            Assert.Empty(o.Errors);

            var bad = CliOptions.Parse(new[] { "-Silent", "-Nope" });
            Assert.Equal(2, bad.Errors.Count);

            var apply = CliOptions.Parse(new[] { "/apply", "a.b, c.d" });
            Assert.Equal(new[] { "a.b", "c.d" }, apply.Apply);
        }

        [Fact]
        public void Report_is_self_contained_and_has_no_em_dash()
        {
            var sys = new FakeSystem().WithDefaults(TweakCatalog.All);
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var session = engine.Apply(TweakCatalog.All.Take(5).ToList(), new ApplyOptions());
            foreach (var lang in new[] { Lang.Es, Lang.En })
            {
                var html = HtmlReport.Build(lang, sys.Info, session, TweakCatalog.All.ToList(), new Measurement { MemoryUsedMb = 4000 }, new Measurement { MemoryUsedMb = 3500 }, DateTime.Now);
                Assert.StartsWith("<!doctype html>", html);
                Assert.DoesNotContain("<script", html);
                Assert.DoesNotContain("—", html);
                Assert.Contains("<title>", html);
            }
        }

        [Fact]
        public void Profile_selection_excludes_applied_and_avoided_tweaks()
        {
            var sys = new FakeSystem().WithDefaults(TweakCatalog.All);
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var scan = engine.Scan(TweakCatalog.All);
            var gaming = Profiles.Select(scan, ProfileKind.Gaming, sys.Info).ToList();
            Assert.Contains(gaming, t => t.Id == "gaming.gameDvr");
            Assert.DoesNotContain(gaming, t => t.Id == "gaming.mpoFix");
            engine.Apply(gaming, new ApplyOptions());
            Assert.Empty(Profiles.Select(engine.Scan(TweakCatalog.All), ProfileKind.Gaming, sys.Info));
        }
    }
}

namespace Optimizzzer.Tests
{
    public class ManualAndColorTests
    {
        [Xunit.Fact]
        public void Manual_service_change_is_journaled_and_undoable()
        {
            var sys = new FakeSystem();
            sys.Services.Modes["Spooler"] = Optimizzzer.Core.StartMode.Automatic;
            var engine = new Optimizzzer.Core.TweakEngine(sys.Context, new Optimizzzer.Core.MemoryJournalStore());
            var t = Optimizzzer.Core.ManualTweaks.Service("Spooler", "Print Spooler", Optimizzzer.Core.StartMode.Disabled);
            engine.Apply(new[] { t }, new Optimizzzer.Core.ApplyOptions());
            Xunit.Assert.Equal(Optimizzzer.Core.StartMode.Disabled, sys.Services.Modes["Spooler"]);

            var ids = engine.History().Select(h => h.Entry.TweakId);
            var catalog = Optimizzzer.Core.ManualTweaks.WithJournal(Optimizzzer.Core.Catalog.TweakCatalog.All, ids);
            var r = engine.Undo(t.Id, catalog);
            Xunit.Assert.Single(r.Reverted);
            Xunit.Assert.Equal(Optimizzzer.Core.StartMode.Automatic, sys.Services.Modes["Spooler"]);
            Xunit.Assert.NotNull(Optimizzzer.Core.ManualTweaks.FromId(@"manual.task|\Microsoft\Windows\X|False"));
        }

        [Xunit.Fact]
        public void Accent_is_adjusted_until_it_passes_AA()
        {
            var yellow = ((byte)255, (byte)200, (byte)0);
            var white = ((byte)255, (byte)255, (byte)255);
            var fixedLight = Optimizzzer.Core.ColorMath.EnsureContrast(yellow, white, 4.5, darken: true);
            Xunit.Assert.True(Optimizzzer.Core.ColorMath.Contrast(fixedLight, white) >= 4.5);
            var navy = ((byte)0, (byte)30, (byte)90);
            var dark = ((byte)38, (byte)38, (byte)42);
            var fixedDark = Optimizzzer.Core.ColorMath.EnsureContrast(navy, dark, 4.5, darken: false);
            Xunit.Assert.True(Optimizzzer.Core.ColorMath.Contrast(fixedDark, dark) >= 4.5);
        }
    }
}
