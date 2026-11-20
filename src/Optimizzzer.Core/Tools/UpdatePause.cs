using System;
using System.Globalization;

namespace Optimizzzer.Core.Tools
{
    /// <summary>
    /// Pauses Windows Update until a date through the same values the Settings app writes (UX\Settings), and raises the
    /// limit Settings allows (FlightSettingsMaxPauseDays). Windows decides whether to honour it: the date that really
    /// stuck is read back and shown, never assumed.
    /// </summary>
    public static class UpdatePause
    {
        public const string Key = GameSession.WuKey;
        public const string MaxDaysName = "FlightSettingsMaxPauseDays";
        /// <summary>The highest limit reported to work with the Settings app (about 20 years). 2222 is not reachable.</summary>
        public const int MaxDays = 7300;

        private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        public static DateTime ClampUntil(DateTime nowUtc, DateTime untilUtc)
        {
            var max = nowUtc.AddDays(MaxDays);
            return untilUtc > max ? max : untilUtc < nowUtc.AddDays(1) ? nowUtc.AddDays(1) : untilUtc;
        }

        public static DateTime Pause(SystemContext ctx, DateTime nowUtc, DateTime untilUtc)
        {
            untilUtc = ClampUntil(nowUtc, untilUtc);
            var days = (int)Math.Ceiling((untilUtc - nowUtc).TotalDays);
            var r = ctx.Registry;
            r.SetValue(Hive.LocalMachine, Key, MaxDaysName, RegValue.DWord(Math.Max(days, 35)));
            var start = Iso(nowUtc); var end = Iso(untilUtc);
            foreach (var n in GameSession.PauseNames)
                r.SetValue(Hive.LocalMachine, Key, n, RegValue.Str(n.Contains("Start") ? start : end));
            return untilUtc;
        }

        /// <summary>The pause end Windows has now, or null when updates are not paused.</summary>
        public static DateTime? PausedUntil(SystemContext ctx, DateTime nowUtc)
        {
            var v = ctx.Registry.GetValue(Hive.LocalMachine, Key, "PauseUpdatesExpiryTime")?.Data as string;
            if (v == null || !DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)) return null;
            return d > nowUtc ? d : (DateTime?)null;
        }

        public static void Resume(SystemContext ctx)
        {
            foreach (var n in GameSession.PauseNames) ctx.Registry.DeleteValue(Hive.LocalMachine, Key, n);
            ctx.Registry.DeleteValue(Hive.LocalMachine, Key, MaxDaysName);
        }

        public static string Script(DateTime untilUtc, bool spanish) =>
            RiskZone.Header(spanish ? "Pausar Windows Update hasta " + untilUtc.ToString("yyyy-MM-dd") : "Pause Windows Update until " + untilUtc.ToString("yyyy-MM-dd"), spanish) +
            "$k = 'HKLM:\\" + Key + "'\r\n" +
            "$now = (Get-Date).ToUniversalTime()\r\n" +
            "$until = [datetime]::Parse('" + Iso(untilUtc) + "').ToUniversalTime()\r\n" +
            "$days = [math]::Ceiling(($until - $now).TotalDays)\r\n" +
            "New-Item -Path $k -Force | Out-Null\r\n" +
            "Set-ItemProperty -Path $k -Name " + MaxDaysName + " -Type DWord -Value ([math]::Max($days, 35))\r\n" +
            "$s = $now.ToString(\"yyyy-MM-dd'T'HH:mm:ss'Z'\"); $e = $until.ToString(\"yyyy-MM-dd'T'HH:mm:ss'Z'\")\r\n" +
            string.Concat(Array.ConvertAll(GameSession.PauseNames, n => "Set-ItemProperty -Path $k -Name " + n + " -Type String -Value " + (n.Contains("Start") ? "$s" : "$e") + "\r\n")) +
            "\r\n# " + (spanish ? "Para volver atrás (reanudar actualizaciones):" : "To go back (resume updates):") + "\r\n" +
            "# Remove-ItemProperty -Path $k -Name " + string.Join(",", GameSession.PauseNames) + "," + MaxDaysName + " -ErrorAction SilentlyContinue\r\n";
    }
}
