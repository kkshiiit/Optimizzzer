using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimizzzer.Core
{
    public enum Lang { Es, En }

    /// <summary>A string in both supported languages.</summary>
    public sealed class Text
    {
        public Text(string es, string en) { Es = es ?? ""; En = en ?? ""; }
        public string Es { get; }
        public string En { get; }
        public string this[Lang lang] => lang == Lang.En ? En : Es;
        public static readonly Text Empty = new Text("", "");
        public override string ToString() => Es;
    }

    public enum Risk { Safe, Moderate, Advanced }

    public enum Category
    {
        Privacy,
        WindowsAI,
        Performance,
        Gaming,
        Input,
        Visual,
        Power,
        Interface,
        Update,
        Network,
        Services,
        Apps,
    }

    [Flags]
    public enum OsFilter { Win10 = 1, Win11 = 2, Both = Win10 | Win11 }

    /// <summary>What the user has to do before the change is fully visible.</summary>
    public enum RestartKind { None, Explorer, SignOut, Reboot }

    [Flags]
    public enum ProfileKind
    {
        None = 0,
        Minimal = 1,
        Office = 2,
        Gaming = 4,
        Laptop = 8,
        Privacy = 16,
    }

    public enum TweakStatus { NotApplied, Applied, Partial, NotApplicable, Unknown }

    /// <summary>How noticeable the change usually is. A qualitative estimate, never a measured number.</summary>
    public enum Impact { Low, Medium, High }

    public sealed class Tweak
    {
        public string Id { get; set; }
        public Category Category { get; set; }
        public Risk Risk { get; set; }
        public Text Title { get; set; } = Text.Empty;
        /// <summary>One sentence: what this does for the user.</summary>
        public Text Summary { get; set; } = Text.Empty;
        /// <summary>Why it helps, in plain words.</summary>
        public Text Why { get; set; } = Text.Empty;
        /// <summary>What you lose. Empty means no known downside.</summary>
        public Text Downside { get; set; } = Text.Empty;
        /// <summary>Where the behaviour is documented.</summary>
        public string Source { get; set; } = "";
        public OsFilter Os { get; set; } = OsFilter.Both;
        public int MinBuild { get; set; }
        public int MaxBuild { get; set; } = int.MaxValue;
        public RestartKind Restart { get; set; }
        public Impact Impact { get; set; } = Impact.Low;
        public ProfileKind Profiles { get; set; }
        /// <summary>Hardware conditions. Null means it applies to every PC.</summary>
        public Func<ISystemInfo, bool> HardwareFilter { get; set; }
        /// <summary>Profiles for which this tweak must not be suggested on this hardware (e.g. hibernation on laptops).</summary>
        public Func<ISystemInfo, bool> AvoidOn { get; set; }
        public List<TweakAction> Actions { get; } = new List<TweakAction>();

        public bool Reversible => Actions.All(a => a.Reversible);

        public bool IsApplicable(ISystemInfo info)
        {
            if (info == null) return true;
            var os = info.IsWin11 ? OsFilter.Win11 : OsFilter.Win10;
            if ((Os & os) == 0) return false;
            if (info.Build < MinBuild || info.Build > MaxBuild) return false;
            if (HardwareFilter != null && !HardwareFilter(info)) return false;
            return true;
        }

        public override string ToString() => Id;
    }
}
