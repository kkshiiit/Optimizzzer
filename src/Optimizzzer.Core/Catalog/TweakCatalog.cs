using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimizzzer.Core.Catalog
{
    public static class TweakCatalog
    {
        private static readonly Lazy<IReadOnlyList<Tweak>> _all = new Lazy<IReadOnlyList<Tweak>>(Build);

        public static IReadOnlyList<Tweak> All => _all.Value;

        public static Tweak Find(string id) => All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        private static IReadOnlyList<Tweak> Build()
        {
            var l = new List<Tweak>();
            PrivacyTweaks.Add(l);
            InterfaceTweaks.Add(l);
            PerformanceTweaks.Add(l);
            SystemTweaks.Add(l);
            return l.OrderBy(t => t.Category).ThenBy(t => t.Risk).ToList();
        }
    }
}
