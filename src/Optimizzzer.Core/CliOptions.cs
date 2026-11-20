using System;
using System.Collections.Generic;
using System.Linq;

namespace Optimizzzer.Core
{
    /// <summary>
    /// Command line for unattended use. Examples:
    ///   Optimizzzer.exe -Scan
    ///   Optimizzzer.exe -Profile Gaming -DryRun
    ///   Optimizzzer.exe -Profile Privacy -Silent
    ///   Optimizzzer.exe -Apply privacy.telemetry,ui.fileExtensions -Silent
    ///   Optimizzzer.exe -Undo all | -Undo &lt;session id&gt; | -Undo &lt;tweak id&gt;
    /// </summary>
    public sealed class CliOptions
    {
        public bool HasCommand => Scan || DryRun || Silent || Profile.HasValue || Level.HasValue || ApplyFile != null || Apply.Count > 0 || Undo != null || List || SelfTest || Help;
        public Level? Level { get; private set; }
        public string ApplyFile { get; private set; }
        public bool Scan { get; private set; }
        public bool DryRun { get; private set; }
        public bool Silent { get; private set; }
        public bool List { get; private set; }
        public bool SelfTest { get; private set; }
        public bool Help { get; private set; }
        public bool NoRestorePoint { get; private set; }
        public ProfileKind? Profile { get; private set; }
        public List<string> Apply { get; } = new List<string>();
        public string Undo { get; private set; }
        public string ReportPath { get; private set; }
        public Lang? Lang { get; private set; }
        public List<string> Errors { get; } = new List<string>();

        public static CliOptions Parse(IList<string> args)
        {
            var o = new CliOptions();
            for (int i = 0; i < args.Count; i++)
            {
                var a = args[i].TrimStart('-', '/').ToLowerInvariant();
                string Next()
                {
                    if (i + 1 < args.Count) return args[++i];
                    o.Errors.Add("Missing value after -" + a);
                    return null;
                }
                switch (a)
                {
                    case "scan": o.Scan = true; break;
                    case "dryrun": o.DryRun = true; break;
                    case "silent": o.Silent = true; break;
                    case "list": o.List = true; break;
                    case "selftest": o.SelfTest = true; break;
                    case "norestorepoint": o.NoRestorePoint = true; break;
                    case "help": case "?": case "h": o.Help = true; break;
                    case "profile":
                        var p = Next();
                        if (p == null) break;
                        if (Enum.TryParse<ProfileKind>(p, true, out var kind) && kind != ProfileKind.None) o.Profile = kind;
                        else o.Errors.Add("Unknown profile: " + p + " (Minimal, Office, Gaming, Laptop, Privacy)");
                        break;
                    case "level":
                        var lv = Next();
                        if (lv == null) break;
                        o.Level = Levels.Parse(lv);
                        if (o.Level == null) o.Errors.Add("Unknown level: " + lv + " (Quick, Balanced, Gaming, Max)");
                        break;
                    case "applyfile":
                        o.ApplyFile = Next();
                        break;
                    case "apply":
                        var ids = Next();
                        if (ids != null) o.Apply.AddRange(ids.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                        break;
                    case "undo": o.Undo = Next(); break;
                    case "report": o.ReportPath = Next(); break;
                    case "lang":
                        var l = Next();
                        if (l == null) break;
                        if (l.StartsWith("en", StringComparison.OrdinalIgnoreCase)) o.Lang = Core.Lang.En;
                        else if (l.StartsWith("es", StringComparison.OrdinalIgnoreCase)) o.Lang = Core.Lang.Es;
                        else o.Errors.Add("Unknown language: " + l + " (es, en)");
                        break;
                    default: o.Errors.Add("Unknown option: " + args[i]); break;
                }
            }
            if (o.Silent && !o.Profile.HasValue && !o.Level.HasValue && o.ApplyFile == null && o.Apply.Count == 0)
                o.Errors.Add("-Silent needs -Level, -Profile, -Apply or -ApplyFile");
            return o;
        }

        public const string Usage =
@"Optimizzzer command line

  -Scan                   Show the status of every tweak on this PC
  -List                   List every tweak id with its category and risk
  -Level <name>           Quick | Balanced | Gaming | Max (each includes the ones before)
  -Profile <name>         Minimal | Office | Gaming | Laptop | Privacy
  -ApplyFile <file.optz>  Tweaks from a selection exported in the app
  -Apply <id,id,...>      Specific tweak ids (see -List)
  -DryRun                 Show exactly what would change, change nothing
  -Silent                 Apply without asking (needs -Level, -Profile, -Apply or -ApplyFile)
  -NoRestorePoint         Skip the restore point (not recommended)
  -Undo <all|session|id>  Undo everything, one session, or one tweak
  -Report <file.html>     Write an HTML report of the run
  -Lang <es|en>           Output language
  -SelfTest               Load every screen and the catalog, then exit (CI)";
    }
}
