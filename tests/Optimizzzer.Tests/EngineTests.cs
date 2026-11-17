using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Optimizzzer.Core;
using Optimizzzer.Core.Catalog;
using Xunit;

namespace Optimizzzer.Tests
{
    public class EngineTests
    {
        private static IList<Tweak> Catalog => TweakCatalog.All.ToList();

        private static IList<Tweak> ReversibleWithoutFiles => Catalog
            .Where(t => t.Reversible && t.Id != "app.onedrive")
            .ToList();

        [Fact]
        public void Applying_then_undoing_every_reversible_tweak_restores_the_exact_state()
        {
            var sys = new FakeSystem().WithDefaults(Catalog);
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var before = sys.Dump();

            var tweaks = ReversibleWithoutFiles.Where(t => t.IsApplicable(sys.Info)).ToList();
            var session = engine.Apply(tweaks, new ApplyOptions());

            Assert.All(session.Entries, e => Assert.NotEqual(EntryResult.Failed.ToString(), e.Result));
            Assert.Contains(session.Entries, e => e.ResultKind == EntryResult.Applied);
            Assert.NotEqual(before, sys.Dump());

            var undo = engine.UndoSession(session.Id, Catalog);
            Assert.Empty(undo.Failed);
            Assert.Equal(before, sys.Dump());
        }

        [Fact]
        public void Every_applied_tweak_reports_applied_afterwards()
        {
            var sys = new FakeSystem().WithDefaults(Catalog);
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var tweaks = Catalog.Where(t => t.IsApplicable(sys.Info) && t.Id != "app.onedrive").ToList();
            engine.Apply(tweaks, new ApplyOptions());
            foreach (var t in tweaks)
                Assert.True(engine.Detect(t) == TweakStatus.Applied, t.Id + " is " + engine.Detect(t));
        }

        [Fact]
        public void Applying_twice_skips_everything_the_second_time()
        {
            var sys = new FakeSystem().WithDefaults(Catalog);
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var tweaks = ReversibleWithoutFiles.Where(t => t.IsApplicable(sys.Info)).ToList();
            engine.Apply(tweaks, new ApplyOptions());
            var second = engine.Apply(tweaks, new ApplyOptions());
            Assert.All(second.Entries, e => Assert.Equal(EntryResult.Skipped, e.ResultKind));
        }

        [Fact]
        public void A_failing_action_rolls_back_the_rest_of_that_tweak()
        {
            var sys = new FakeSystem();
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var t = TweakCatalog.Find("privacy.activityHistory");
            sys.Registry.FailOnSet = (h, p, n) => n == "UploadUserActivities";
            var before = sys.Dump();

            var session = engine.Apply(new[] { t }, new ApplyOptions());

            Assert.Equal(EntryResult.Failed, session.Entries[0].ResultKind);
            Assert.Contains("denied", session.Entries[0].Error);
            Assert.Equal(before, sys.Dump());
        }

        [Fact]
        public void Restore_point_is_created_once_per_session_and_failure_is_recorded()
        {
            var sys = new FakeSystem();
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var s1 = engine.Apply(new[] { TweakCatalog.Find("ui.fileExtensions") }, new ApplyOptions());
            Assert.True(s1.RestorePointCreated);
            Assert.Equal(1, sys.RestorePoint.Created);

            sys.RestorePoint.Fail = "System Protection is off";
            var s2 = engine.Apply(new[] { TweakCatalog.Find("ui.hiddenFiles") }, new ApplyOptions());
            Assert.False(s2.RestorePointCreated);
            Assert.Equal("System Protection is off", s2.RestorePointError);
        }

        [Fact]
        public void Undo_by_tweak_uses_the_original_state_even_after_reapplying()
        {
            var sys = new FakeSystem();
            var store = new MemoryJournalStore();
            var engine = new TweakEngine(sys.Context, store);
            var t = TweakCatalog.Find("perf.menuDelay");
            sys.Registry.SetValue(Hive.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", RegValue.Str("400"));

            engine.Apply(new[] { t }, new ApplyOptions());
            // Windows update puts a different value back; the user re-applies.
            sys.Registry.SetValue(Hive.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", RegValue.Str("250"));
            Assert.Contains(t, engine.FindDrift(Catalog));
            engine.Apply(new[] { t }, new ApplyOptions());
            Assert.Empty(engine.FindDrift(Catalog));

            var r = engine.Undo(t.Id, Catalog);
            Assert.Single(r.Reverted);
            Assert.Equal("400", sys.Registry.GetValue(Hive.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay").Data);
            Assert.Empty(engine.FindDrift(Catalog));
        }

        [Fact]
        public void Creating_a_value_under_missing_keys_and_undoing_removes_the_keys_we_created()
        {
            var sys = new FakeSystem();
            sys.Registry.CreateKey(Hive.CurrentUser, @"Software\Classes\CLSID");
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            var t = TweakCatalog.Find("ui.classicContextMenu");
            var s = engine.Apply(new[] { t }, new ApplyOptions());
            Assert.True(sys.Registry.KeyExists(Hive.CurrentUser, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"));
            engine.UndoSession(s.Id, Catalog);
            Assert.False(sys.Registry.KeyExists(Hive.CurrentUser, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}"));
            Assert.True(sys.Registry.KeyExists(Hive.CurrentUser, @"Software\Classes\CLSID"));
        }

        [Fact]
        public void DirectX_setting_keeps_other_entries()
        {
            var sys = new FakeSystem();
            sys.Registry.SetValue(Hive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", RegValue.Str("AutoHDREnable=1;VRROptimizeEnable=0;"));
            var a = new DirectXSettingAction("SwapEffectUpgradeEnable", "1");
            var snap = a.Snapshot(sys.Context);
            a.Apply(sys.Context);
            var text = (string)sys.Registry.GetValue(Hive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings").Data;
            Assert.Contains("AutoHDREnable=1;", text);
            Assert.Contains("VRROptimizeEnable=0;", text);
            Assert.Contains("SwapEffectUpgradeEnable=1;", text);
            a.Revert(sys.Context, snap);
            text = (string)sys.Registry.GetValue(Hive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings").Data;
            Assert.DoesNotContain("SwapEffectUpgradeEnable", text);
        }

        [Fact]
        public void Not_applicable_tweaks_are_skipped_by_os()
        {
            var sys = new FakeSystem();
            sys.Info.Build = 19045;
            var engine = new TweakEngine(sys.Context, new MemoryJournalStore());
            Assert.Equal(TweakStatus.NotApplicable, engine.Detect(TweakCatalog.Find("ai.recall")));
            Assert.Equal(TweakStatus.NotApplicable, engine.Detect(TweakCatalog.Find("ui.classicContextMenu")));
            Assert.NotEqual(TweakStatus.NotApplicable, engine.Detect(TweakCatalog.Find("privacy.cortana")));
        }

        [Fact]
        public void Laptops_never_get_desktop_power_tweaks()
        {
            var info = new FakeInfo { IsLaptop = true };
            Assert.False(TweakCatalog.Find("power.ultimate").IsApplicable(info));
            Assert.False(TweakCatalog.Find("power.hibernate").IsApplicable(info));
            Assert.Equal(ProfileKind.Laptop, Profiles.Recommend(info));
        }

        [Fact]
        public void Journal_survives_a_file_roundtrip()
        {
            var dir = Path.Combine(Path.GetTempPath(), "optz-" + Guid.NewGuid().ToString("N"));
            try
            {
                var sys = new FakeSystem().WithDefaults(Catalog);
                var store = new FileJournalStore(dir);
                var engine = new TweakEngine(sys.Context, store);
                var before = sys.Dump();
                var s = engine.Apply(new[] { TweakCatalog.Find("ui.homeGallery"), TweakCatalog.Find("power.ultimate") }, new ApplyOptions());

                var reloaded = new TweakEngine(sys.Context, new FileJournalStore(dir));
                Assert.Equal(2, reloaded.History().Count);
                reloaded.UndoSession(s.Id, Catalog);
                Assert.Equal(before, sys.Dump());
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
