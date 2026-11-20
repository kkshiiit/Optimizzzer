using System.Collections.Generic;
using System.Linq;

namespace Optimizzzer.Core
{
    public sealed class ProfileInfo
    {
        public ProfileKind Kind { get; set; }
        public Text Name { get; set; }
        public Text Description { get; set; }
    }

    public static class Profiles
    {
        public static readonly IReadOnlyList<ProfileInfo> All = new[]
        {
            new ProfileInfo { Kind = ProfileKind.Minimal, Name = new Text("Mínimo seguro", "Safe minimum"),
                Description = new Text("Solo cambios sin efectos secundarios: menos telemetría, sin anuncios ni apps promocionadas.", "Only changes with no side effects: less telemetry, no ads or promoted apps.") },
            new ProfileInfo { Kind = ProfileKind.Office, Name = new Text("Trabajo y estudio", "Work and study"),
                Description = new Text("Un escritorio limpio y rápido, sin reinicios inesperados ni distracciones.", "A clean, fast desktop with no surprise restarts or distractions.") },
            new ProfileInfo { Kind = ProfileKind.Gaming, Name = new Text("Juegos", "Gaming"),
                Description = new Text("Menos procesos en segundo plano, sin grabación continua y ajustes de ratón y GPU para jugar.", "Fewer background processes, no continuous recording, and mouse and GPU settings for games.") },
            new ProfileInfo { Kind = ProfileKind.Laptop, Name = new Text("Portátil y batería", "Laptop and battery"),
                Description = new Text("Menos trabajo en segundo plano para que la batería dure más, sin tocar la hibernación.", "Less background work so the battery lasts longer, without touching hibernation.") },
            new ProfileInfo { Kind = ProfileKind.Privacy, Name = new Text("Privacidad máxima", "Maximum privacy"),
                Description = new Text("Corta todo lo que envía datos a Microsoft o a terceros, aunque pierdas alguna función.", "Cuts everything that sends data to Microsoft or third parties, even if you lose some features.") },
        };

        public static ProfileInfo Get(ProfileKind k) => All.First(p => p.Kind == k);

        /// <summary>Tweaks for a profile that make sense on this PC and are not applied yet.</summary>
        public static IEnumerable<Tweak> Select(IEnumerable<ScanResult> scan, ProfileKind profile, ISystemInfo info) =>
            scan.Where(r => (r.Tweak.Profiles & profile) != 0
                            && (r.Status == TweakStatus.NotApplied || r.Status == TweakStatus.Partial)
                            && (r.Tweak.AvoidOn == null || info == null || !r.Tweak.AvoidOn(info)))
                .Select(r => r.Tweak);

        /// <summary>Suggests a starting profile from the hardware. The user always confirms.</summary>
        public static ProfileKind Recommend(ISystemInfo info)
        {
            if (info == null) return ProfileKind.Minimal;
            if (info.IsLaptop) return ProfileKind.Laptop;
            if (info.HasNvidia || info.HasAmdGpu) return ProfileKind.Gaming;
            return ProfileKind.Office;
        }
    }
}
