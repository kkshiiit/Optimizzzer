using System.Collections.Generic;
using System.IO;
using System.Linq;
using Optimizzzer.Core;
using Optimizzzer.Core.Catalog;
using Optimizzzer.Core.Tools;
using Xunit;

namespace Optimizzzer.Tests
{
    public class CatalogTests
    {
        private static IEnumerable<(string Where, Text Text)> AllTexts()
        {
            foreach (var t in TweakCatalog.All)
            {
                yield return (t.Id + ".Title", t.Title);
                yield return (t.Id + ".Summary", t.Summary);
                yield return (t.Id + ".Why", t.Why);
                yield return (t.Id + ".Downside", t.Downside);
            }
            foreach (var p in Profiles.All) { yield return (p.Kind + ".Name", p.Name); yield return (p.Kind + ".Desc", p.Description); }
            foreach (var c in CleanupCatalog.All) { yield return (c.Id + ".Title", c.Title); yield return (c.Id + ".Desc", c.Description); }
            foreach (var r in RepairCatalog.All) { yield return (r.Id + ".Title", r.Title); yield return (r.Id + ".Desc", r.Description); }
            foreach (var d in DnsCatalog.All) yield return (d.Id + ".Desc", d.Description);
        }

        [Fact]
        public void Ids_are_unique()
        {
            var dupes = TweakCatalog.All.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.Empty(dupes);
        }

        [Fact]
        public void Every_tweak_is_complete()
        {
            foreach (var t in TweakCatalog.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Title.Es), t.Id + " Title.Es");
                Assert.False(string.IsNullOrWhiteSpace(t.Title.En), t.Id + " Title.En");
                Assert.False(string.IsNullOrWhiteSpace(t.Summary.Es), t.Id + " Summary.Es");
                Assert.False(string.IsNullOrWhiteSpace(t.Summary.En), t.Id + " Summary.En");
                Assert.False(string.IsNullOrWhiteSpace(t.Why.Es), t.Id + " Why.Es");
                Assert.False(string.IsNullOrWhiteSpace(t.Why.En), t.Id + " Why.En");
                Assert.False(string.IsNullOrWhiteSpace(t.Source), t.Id + " Source");
                Assert.NotEmpty(t.Actions);
                if (t.Risk != Risk.Safe) Assert.False(string.IsNullOrWhiteSpace(t.Downside.Es), t.Id + " needs a downside");
                Assert.True(t.Downside.Es.Length == 0 == (t.Downside.En.Length == 0), t.Id + " downside in one language only");
            }
        }

        [Fact]
        public void No_em_dashes_in_user_facing_text()
        {
            foreach (var (where, text) in AllTexts())
            {
                Assert.DoesNotContain("—", text.Es + " " + where);
                Assert.DoesNotContain("—", text.En + " " + where);
            }
        }

        [Fact]
        public void Every_action_describes_itself_in_both_languages()
        {
            foreach (var t in TweakCatalog.All)
                foreach (var a in t.Actions)
                {
                    Assert.False(string.IsNullOrWhiteSpace(a.Describe(Lang.Es)), t.Id);
                    Assert.False(string.IsNullOrWhiteSpace(a.Describe(Lang.En)), t.Id);
                }
        }

        [Fact]
        public void Every_profile_has_tweaks_and_minimal_is_safe_only()
        {
            foreach (var p in Profiles.All)
                Assert.Contains(TweakCatalog.All, t => (t.Profiles & p.Kind) != 0);
            Assert.All(TweakCatalog.All.Where(t => (t.Profiles & ProfileKind.Minimal) != 0), t => Assert.Equal(Risk.Safe, t.Risk));
            Assert.All(TweakCatalog.All.Where(t => t.Risk == Risk.Advanced), t => Assert.Equal(ProfileKind.None, t.Profiles));
        }

        [Fact]
        public void Catalog_size_is_substantial()
        {
            Assert.True(TweakCatalog.All.Count >= 100, "count " + TweakCatalog.All.Count);
            foreach (Category c in System.Enum.GetValues(typeof(Category)))
                Assert.Contains(TweakCatalog.All, t => t.Category == c);
        }

        [Fact]
        public void Placebo_tweaks_are_not_in_the_catalog()
        {
            var banned = new[] { "SvcHostSplitThresholdInKB", "LargeSystemCache", "NetworkThrottlingIndex", "SystemResponsiveness", "TcpAckFrequency", "TCPNoDelay", "Win32PrioritySeparation", "DisablePagingExecutive", "NonBestEffortLimit" };
            var regNames = TweakCatalog.All.SelectMany(t => t.Actions).OfType<RegistryValueAction>().Select(a => a.Name).ToList();
            foreach (var b in banned) Assert.DoesNotContain(b, regNames);
        }

        [Fact]
        public void Docs_list_the_excluded_tweaks()
        {
            var root = Directory.GetCurrentDirectory();
            while (root != null && !File.Exists(Path.Combine(root, "Optimizzzer.sln"))) root = Path.GetDirectoryName(root);
            Assert.NotNull(root);
            var doc = File.ReadAllText(Path.Combine(root, "docs", "EXCLUDED.md"));
            Assert.Contains("SvcHostSplitThresholdInKB", doc);
        }
    }
}
