using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Optimizzzer.Core.Tools
{
    /// <summary>A background app the temporary game mode can close.</summary>
    public sealed class BackgroundApp
    {
        public string Exe { get; set; }
        public string Name { get; set; }
    }

    /// <summary>What the temporary game mode changed, so it can put everything back. Saved as plain "key=value" text.</summary>
    public sealed class GameSessionState
    {
        public DateTime StartedUtc { get; set; }
        public string PreviousScheme { get; set; }
        /// <summary>Windows Update pause values before the session; null means the value did not exist.</summary>
        public Dictionary<string, string> PreviousPause { get; } = new Dictionary<string, string>();
        public List<string> Closed { get; } = new List<string>();
        public bool SchemeChanged { get; set; }
        public bool UpdatesPaused { get; set; }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("started=").Append(StartedUtc.ToString("o", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("scheme=").Append(PreviousScheme ?? "").Append('\n');
            sb.Append("schemeChanged=").Append(SchemeChanged ? "1" : "0").Append('\n');
            sb.Append("updatesPaused=").Append(UpdatesPaused ? "1" : "0").Append('\n');
            foreach (var kv in PreviousPause) sb.Append("pause.").Append(kv.Key).Append('=').Append(kv.Value ?? "\u0000").Append('\n');
            foreach (var c in Closed) sb.Append("closed=").Append(c).Append('\n');
            return sb.ToString();
        }

        public static GameSessionState Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var s = new GameSessionState();
            foreach (var line in text.Split('\n'))
            {
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var k = line.Substring(0, i); var v = line.Substring(i + 1).TrimEnd('\r');
                if (k == "started" && DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)) s.StartedUtc = d;
                else if (k == "scheme") s.PreviousScheme = v.Length == 0 ? null : v;
                else if (k == "schemeChanged") s.SchemeChanged = v == "1";
                else if (k == "updatesPaused") s.UpdatesPaused = v == "1";
                else if (k.StartsWith("pause.")) s.PreviousPause[k.Substring(6)] = v == "\u0000" ? null : v;
                else if (k == "closed") s.Closed.Add(v);
            }
            return s.StartedUtc == default(DateTime) ? null : s;
        }
    }

    /// <summary>
    /// Temporary game mode: while on, Windows Update pauses for a day, the High performance plan is active and the
    /// background apps the person ticked are closed. Turning it off restores the plan and the pause values exactly.
    /// Closed apps are not reopened: they come back when the person opens them or at the next sign-in.
    /// </summary>
    public static class GameSession
    {
        public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        public const string WuKey = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";
        public static readonly string[] PauseNames =
        {
            "PauseUpdatesStartTime", "PauseUpdatesExpiryTime",
            "PauseFeatureUpdatesStartTime", "PauseFeatureUpdatesEndTime",
            "PauseQualityUpdatesStartTime", "PauseQualityUpdatesEndTime",
        };

        /// <summary>Apps that commonly sit in the tray and use CPU, disk or network while you play.</summary>
        public static readonly IReadOnlyList<BackgroundApp> Known = new[]
        {
            new BackgroundApp { Exe = "OneDrive.exe", Name = "OneDrive" },
            new BackgroundApp { Exe = "Dropbox.exe", Name = "Dropbox" },
            new BackgroundApp { Exe = "GoogleDriveFS.exe", Name = "Google Drive" },
            new BackgroundApp { Exe = "ms-teams.exe", Name = "Microsoft Teams" },
            new BackgroundApp { Exe = "Teams.exe", Name = "Microsoft Teams (classic)" },
            new BackgroundApp { Exe = "Spotify.exe", Name = "Spotify" },
            new BackgroundApp { Exe = "Slack.exe", Name = "Slack" },
            new BackgroundApp { Exe = "Zoom.exe", Name = "Zoom" },
            new BackgroundApp { Exe = "Creative Cloud.exe", Name = "Adobe Creative Cloud" },
            new BackgroundApp { Exe = "EpicGamesLauncher.exe", Name = "Epic Games Launcher" },
            new BackgroundApp { Exe = "EADesktop.exe", Name = "EA app" },
            new BackgroundApp { Exe = "Battle.net.exe", Name = "Battle.net" },
            new BackgroundApp { Exe = "upc.exe", Name = "Ubisoft Connect" },
            new BackgroundApp { Exe = "GalaxyClient.exe", Name = "GOG Galaxy" },
            new BackgroundApp { Exe = "iCloudDrive.exe", Name = "iCloud" },
            new BackgroundApp { Exe = "PhoneExperienceHost.exe", Name = "Enlace Móvil / Phone Link" },
        };

        /// <summary>Known background apps running now, from "tasklist /fo csv /nh".</summary>
        public static IList<BackgroundApp> Running(SystemContext ctx)
        {
            var r = ctx.Process.Run("tasklist", "/fo csv /nh", 30000);
            if (!r.Ok) return new List<BackgroundApp>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(r.Output, "^\"([^\"]+)\"", RegexOptions.Multiline)) names.Add(m.Groups[1].Value);
            return Known.Where(a => names.Contains(a.Exe)).GroupBy(a => a.Name).Select(g => g.First()).ToList();
        }

        public static string ActiveScheme(SystemContext ctx)
        {
            var r = ctx.Process.Run("powercfg", "/getactivescheme", 30000);
            var m = Regex.Match(r.Output ?? "", "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return m.Success ? m.Value.ToLowerInvariant() : null;
        }

        private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        /// <summary>Turns it on. Each part is best effort; the state records only what really changed.</summary>
        public static GameSessionState Start(SystemContext ctx, IEnumerable<string> closeExes, DateTime nowUtc)
        {
            var s = new GameSessionState { StartedUtc = nowUtc };

            s.PreviousScheme = ActiveScheme(ctx);
            if (s.PreviousScheme != HighPerformance)
                s.SchemeChanged = ctx.Process.Run("powercfg", "/setactive " + HighPerformance, 30000).Ok;

            try
            {
                foreach (var n in PauseNames)
                    s.PreviousPause[n] = ctx.Registry.GetValue(Hive.LocalMachine, WuKey, n)?.Data as string;
                var start = Iso(nowUtc); var end = Iso(nowUtc.AddDays(1));
                foreach (var n in PauseNames)
                    ctx.Registry.SetValue(Hive.LocalMachine, WuKey, n, RegValue.Str(n.Contains("Start") ? start : end));
                s.UpdatesPaused = true;
            }
            catch (Exception) { s.PreviousPause.Clear(); }

            foreach (var exe in closeExes ?? Enumerable.Empty<string>())
            {
                if (!Known.Any(k => string.Equals(k.Exe, exe, StringComparison.OrdinalIgnoreCase))) continue;
                if (ctx.Process.Run("taskkill", "/im \"" + exe + "\" /t /f", 30000).Ok) s.Closed.Add(exe);
            }
            return s;
        }

        /// <summary>Turns it off: the previous plan and the previous pause values come back exactly.</summary>
        public static void Stop(SystemContext ctx, GameSessionState s)
        {
            if (s == null) return;
            if (s.SchemeChanged && !string.IsNullOrEmpty(s.PreviousScheme))
                ctx.Process.Run("powercfg", "/setactive " + s.PreviousScheme, 30000);
            if (s.UpdatesPaused)
                foreach (var n in PauseNames)
                {
                    s.PreviousPause.TryGetValue(n, out var old);
                    if (old == null) ctx.Registry.DeleteValue(Hive.LocalMachine, WuKey, n);
                    else ctx.Registry.SetValue(Hive.LocalMachine, WuKey, n, RegValue.Str(old));
                }
        }
    }
}
