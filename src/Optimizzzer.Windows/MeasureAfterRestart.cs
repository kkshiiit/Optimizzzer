using System;
using System.Diagnostics;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    /// <summary>
    /// A one-time scheduled task that takes a measurement two minutes after the next sign-in (so the boot has
    /// settled and Windows has logged its duration), then deletes itself.
    /// </summary>
    public static class MeasureAfterRestart
    {
        public const string Label = "after-restart";
        private const string TaskName = @"Optimizzzer\MeasureAfterRestart";

        public static bool IsScheduled(IProcessRunner proc) => proc.Run("schtasks.exe", "/query /tn \"" + TaskName + "\"", 30000).Ok;

        public static void Schedule(IProcessRunner proc)
        {
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            var args = "/create /tn \"" + TaskName + "\" /tr \"\\\"" + exe + "\\\" -Measure " + Label + "\" /sc onlogon /delay 0002:00 /rl highest /f";
            var r = proc.Run("schtasks.exe", args, 60000);
            if (!r.Ok) throw new InvalidOperationException("schtasks: " + (r.Output + r.Error).Trim());
        }

        public static void Cancel(IProcessRunner proc) => proc.Run("schtasks.exe", "/delete /tn \"" + TaskName + "\" /f", 30000);
    }
}
