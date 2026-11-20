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
            // The task runs with the highest rights at sign-in. Pointing it at the exe in Downloads would let any program
            // that replaces that file get admin rights; run a copy from a folder only admins can write instead.
            var source = Process.GetCurrentProcess().MainModule.FileName;
            var binDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Optimizzzer", "bin");
            SafeFolders.LockToAdmins(System.IO.Path.GetDirectoryName(binDir));
            System.IO.Directory.CreateDirectory(binDir);
            var exe = System.IO.Path.Combine(binDir, "Optimizzzer.exe");
            if (!string.Equals(source, exe, StringComparison.OrdinalIgnoreCase)) System.IO.File.Copy(source, exe, true);
            var args = "/create /tn \"" + TaskName + "\" /tr \"\\\"" + exe + "\\\" -Measure " + Label + "\" /sc onlogon /delay 0002:00 /rl highest /f";
            var r = proc.Run("schtasks.exe", args, 60000);
            if (!r.Ok) throw new InvalidOperationException("schtasks: " + (r.Output + r.Error).Trim());
        }

        /// <summary>At startup, off the UI thread: deletes the exe copy when no measurement is pending (it already ran).</summary>
        public static void CleanupLeftovers(IProcessRunner proc)
        {
            try
            {
                var exe = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Optimizzzer", "bin", "Optimizzzer.exe");
                if (System.IO.File.Exists(exe) && !IsScheduled(proc)) Cancel(proc);
            }
            catch (Exception) { }
        }

        /// <summary>Removes the task and the copy of the exe it used, so nothing stays behind.</summary>
        public static void Cancel(IProcessRunner proc)
        {
            proc.Run("schtasks.exe", "/delete /tn \"" + TaskName + "\" /f", 30000);
            try
            {
                var binDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Optimizzzer", "bin");
                var exe = System.IO.Path.Combine(binDir, "Optimizzzer.exe");
                var running = Process.GetCurrentProcess().MainModule.FileName;
                // The copy cannot delete itself while it runs: it is removed the next time the app opens.
                if (System.IO.File.Exists(exe) && !string.Equals(running, exe, StringComparison.OrdinalIgnoreCase)) System.IO.File.Delete(exe);
            }
            catch (Exception) { }
        }
    }
}
