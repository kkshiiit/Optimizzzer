using System;
using System.IO;
using System.Linq;
using Optimizzzer.Core;
using Optimizzzer.Core.Catalog;
using Optimizzzer.Core.Tools;
using Xunit;

namespace Optimizzzer.Tests
{
    public class LevelTests
    {
        [Fact]
        public void Every_level_id_exists_and_levels_only_grow()
        {
            foreach (var l in Levels.All)
                foreach (var id in l.Adds)
                    Assert.True(TweakCatalog.Find(id) != null, l.Level + " references unknown id " + id);
            var all = Levels.All.SelectMany(l => l.Adds).ToList();
            Assert.Equal(all.Count, all.Distinct().Count());
            for (int i = 1; i < Levels.All.Count; i++)
                Assert.True(Levels.IdsUpTo(Levels.All[i].Level).Count() > Levels.IdsUpTo(Levels.All[i - 1].Level).Count());
        }

        [Fact]
        public void Quick_is_safe_and_fully_reversible_and_only_the_top_level_needs_a_second_confirmation()
        {
            foreach (var id in Levels.Get(Level.Quick).Adds)
            {
                var t = TweakCatalog.Find(id);
                Assert.Equal(Risk.Safe, t.Risk);
                Assert.True(t.Reversible, id);
            }
            Assert.All(Levels.All.Where(l => l.Level != Level.Maximum), l => Assert.False(l.NeedsSecondConfirmation));
            Assert.True(Levels.Get(Level.Maximum).NeedsSecondConfirmation);
        }

        [Fact]
        public void No_level_contains_fixes_or_feature_trade_offs_unrelated_to_speed()
        {
            var excluded = new[] { "gaming.mpoFix", "svc.spooler", "svc.xbox", "app.xboxApp", "app.xboxOverlay", "app.spotify", "app.onedrive", "update.noDrivers", "privacy.location" };
            var ids = Levels.IdsUpTo(Level.Maximum).ToList();
            foreach (var e in excluded) Assert.DoesNotContain(e, ids);
        }

        [Fact]
        public void Level_selection_respects_hardware_and_applied_state()
        {
            var sys = new FakeSystem().WithDefaults(TweakCatalog.All);
            sys.Info.IsLaptop = true;
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var scan = engine.Scan(TweakCatalog.All);
            var max = Levels.Select(scan, Level.Maximum, sys.Info);
            Assert.DoesNotContain(max, t => t.Id == "power.ultimate" || t.Id == "power.hibernate");
            var quick = Levels.Select(scan, Level.Quick, sys.Info);
            engine.Apply(quick, new ApplyOptions());
            Assert.Empty(Levels.Select(engine.Scan(TweakCatalog.All), Level.Quick, sys.Info));
            var (applied, total) = Levels.Progress(engine.Scan(TweakCatalog.All), Level.Quick);
            Assert.Equal(total, applied);
        }

        [Fact]
        public void Level_names_parse_in_both_languages()
        {
            Assert.Equal(Level.Maximum, Levels.Parse("máximo"));
            Assert.Equal(Level.Balanced, Levels.Parse("Balanced"));
            Assert.Null(Levels.Parse("turbo"));
        }
    }

    public class SelectionFileTests
    {
        [Fact]
        public void Roundtrip_keeps_known_ids_and_reports_unknown_ones()
        {
            var json = SelectionFile.Write(new[] { "privacy.telemetry", "ui.fileExtensions", "made.up" }, new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc));
            var (known, unknown) = SelectionFile.Read(json, TweakCatalog.All);
            Assert.Equal(2, known.Count);
            Assert.Equal(new[] { "made.up" }, unknown);
        }

        [Fact]
        public void Garbage_is_rejected_with_a_readable_error()
        {
            Assert.Throws<InvalidDataException>(() => SelectionFile.Read("{\"hello\":1}", TweakCatalog.All));
            Assert.Throws<InvalidDataException>(() => SelectionFile.Read("not json", TweakCatalog.All));
        }
    }

    public class AuditFixTests
    {
        private const string Adv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

        [Fact]
        public void Undoing_one_tweak_keeps_values_other_tweaks_wrote_in_a_key_it_created()
        {
            var sys = new FakeSystem();
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var a = TweakCatalog.Find("update.noAutoRestart");
            var b = TweakCatalog.Find("update.noDrivers");
            engine.Apply(new[] { a, b }, new ApplyOptions());
            engine.Undo(a.Id, TweakCatalog.All.ToList());
            Assert.Equal(TweakStatus.Applied, engine.Detect(b));
            Assert.Equal(TweakStatus.NotApplied, engine.Detect(a));
            engine.Undo(b.Id, TweakCatalog.All.ToList());
            Assert.Equal(TweakStatus.NotApplied, engine.Detect(b));
            Assert.False(sys.Registry.KeyExists(Hive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU"));
        }

        [Fact]
        public void Session_undo_reports_uninstalls_separately_instead_of_failing()
        {
            var sys = new FakeSystem().WithDefaults(TweakCatalog.All);
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var s = engine.Apply(new[] { TweakCatalog.Find("ui.fileExtensions"), TweakCatalog.Find("app.bingNews") }, new ApplyOptions());
            var r = engine.UndoSession(s.Id, TweakCatalog.All.ToList());
            Assert.Empty(r.Failed);
            Assert.Equal(new[] { "ui.fileExtensions" }, r.Reverted);
            Assert.Equal(new[] { "app.bingNews" }, r.NotReversible);
        }

        [Fact]
        public void Undo_reverts_actions_recorded_only_by_a_later_reapply()
        {
            var sys = new FakeSystem();
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var t = TweakCatalog.Find("perf.animations");
            sys.Registry.SetValue(Hive.CurrentUser, Adv, "TaskbarAnimations", RegValue.DWord(0));
            engine.Apply(new[] { t }, new ApplyOptions());
            sys.Registry.SetValue(Hive.CurrentUser, Adv, "TaskbarAnimations", RegValue.DWord(1)); // Windows flips it back
            engine.Apply(new[] { t }, new ApplyOptions());
            engine.Undo(t.Id, TweakCatalog.All.ToList());
            Assert.Equal(1u, (uint)sys.Registry.GetValue(Hive.CurrentUser, Adv, "TaskbarAnimations").Data);
            Assert.Null(sys.Registry.GetValue(Hive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate"));
        }

        [Fact]
        public void A_later_manual_change_replaces_the_earlier_one_and_undo_returns_to_the_original()
        {
            var sys = new FakeSystem();
            sys.Services.Modes["Fax"] = StartMode.Manual;
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            engine.Apply(new[] { ManualTweaks.Service("Fax", "Fax", StartMode.Disabled) }, new ApplyOptions());
            engine.Apply(new[] { ManualTweaks.Service("Fax", "Fax", StartMode.Automatic) }, new ApplyOptions());
            var catalog = ManualTweaks.WithJournal(TweakCatalog.All, engine.History().Select(h => h.Entry.TweakId));
            Assert.Empty(engine.FindDrift(catalog));
            engine.Undo("manual.service|Fax|Automatic", catalog);
            Assert.Equal(StartMode.Manual, sys.Services.Modes["Fax"]);
        }

        [Fact]
        public void Undoing_a_service_tweak_starts_the_service_again_if_it_was_running()
        {
            var sys = new FakeSystem();
            sys.Services.Modes["Spooler"] = StartMode.Automatic;
            sys.Services.Running.Add("Spooler");
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var s = engine.Apply(new[] { TweakCatalog.Find("svc.spooler") }, new ApplyOptions());
            Assert.False(sys.Services.IsRunning("Spooler"));
            engine.UndoSession(s.Id, TweakCatalog.All.ToList());
            Assert.True(sys.Services.IsRunning("Spooler"));
        }

        [Fact]
        public void Undo_ids_are_case_insensitive()
        {
            var sys = new FakeSystem();
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            engine.Apply(new[] { TweakCatalog.Find("ui.fileExtensions") }, new ApplyOptions());
            var r = engine.Undo("UI.FileExtensions", TweakCatalog.All.ToList());
            Assert.Single(r.Reverted);
            Assert.True(engine.Undo("does.not.exist", TweakCatalog.All.ToList()).NothingFound);
        }

        [Fact]
        public void Journal_recovers_a_session_left_as_tmp_by_a_crash()
        {
            var dir = Path.Combine(Path.GetTempPath(), "optz-j-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new FileJournalStore(dir);
                store.Save(new JournalSession { Id = "a", StartedUtc = "2026-01-01T00:00:00Z" });
                store.Save(new JournalSession { Id = "a", StartedUtc = "2026-01-01T00:00:00Z" });
                File.Move(Path.Combine(dir, "session-a.json"), Path.Combine(dir, "session-a.json.tmp"));
                Assert.Single(new FileJournalStore(dir).LoadAll());
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Fact]
        public void Integrated_amd_graphics_are_not_a_dedicated_card()
        {
            Assert.False(GpuNames.IsDedicatedAmd("AMD Radeon(TM) Graphics"));
            Assert.False(GpuNames.IsDedicatedAmd("AMD Radeon Graphics"));
            Assert.False(GpuNames.IsDedicatedAmd("Radeon(TM) Vega 8 Graphics"));
            Assert.True(GpuNames.IsDedicatedAmd("AMD Radeon RX 6700 XT"));
            Assert.True(GpuNames.IsDedicatedAmd("Radeon RX 580 Series"));
        }

        [Fact]
        public void Recycle_bin_reports_only_what_was_really_freed()
        {
            var special = new StuckBin();
            var r = new CleanupEngine().Run(CleanupCatalog.All.First(t => t.Kind == CleanupKind.RecycleBin), special);
            Assert.Equal(600, r.FreedBytes);
            Assert.NotNull(r.Error);
        }

        private sealed class StuckBin : ICleanupSpecial
        {
            private long _size = 1000;
            public long? RecycleBinSize() => _size;
            public void EmptyRecycleBin() => _size = 400; // a locked file stays behind
            public void CleanComponentStore(System.Threading.CancellationToken ct) { }
            public void StopUpdateServices() { }
            public void StartUpdateServices() { }
        }

        [Fact]
        public void Powercfg_undo_failure_keeps_the_tweak_open()
        {
            var sys = new FakeSystem();
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var t = TweakCatalog.Find("power.hibernate");
            sys.Registry.SetValue(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", RegValue.DWord(1));
            engine.Apply(new[] { t }, new ApplyOptions());
            sys.Process.FailNext = "/hibernate";
            var r = engine.Undo(t.Id, TweakCatalog.All.ToList());
            Assert.Single(r.Failed);
            Assert.Contains(engine.History(), h => h.Entry.TweakId == t.Id && !h.Entry.Reverted);
        }
    }
}

namespace Optimizzzer.Tests
{
    public class CliV11Tests
    {
        [Xunit.Fact]
        public void Level_and_apply_file_options_parse()
        {
            var o = Optimizzzer.Core.CliOptions.Parse(new[] { "-Level", "gaming", "-Silent" });
            Xunit.Assert.Equal(Optimizzzer.Core.Level.Gaming, o.Level);
            Xunit.Assert.Empty(o.Errors);
            var f = Optimizzzer.Core.CliOptions.Parse(new[] { "-ApplyFile", "a.optz", "-DryRun" });
            Xunit.Assert.Equal("a.optz", f.ApplyFile);
            Xunit.Assert.Single(Optimizzzer.Core.CliOptions.Parse(new[] { "-Level", "turbo" }).Errors);
        }
    }
}

namespace Optimizzzer.Tests
{
    public class ProtectedKeyTests
    {
        [Xunit.Fact]
        public void A_tweak_on_a_protected_key_is_not_offered()
        {
            var sys = new FakeSystem();
            sys.Registry.Protected.Add(@"SOFTWARE\Policies\Microsoft\Dsh");
            var engine = new Optimizzzer.Core.TweakEngine(sys.Context, new Optimizzzer.Core.MemoryJournalStore());
            var widgets = Optimizzzer.Core.Catalog.TweakCatalog.Find("ui.widgets");
            Xunit.Assert.Equal(Optimizzzer.Core.TweakStatus.NotApplicable, engine.Detect(widgets));
            sys.Info.Build = 19045;
            Xunit.Assert.Equal(Optimizzzer.Core.TweakStatus.NotApplied, engine.Detect(widgets));
        }
    }
}
