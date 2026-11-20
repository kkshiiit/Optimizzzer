using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    /// <summary>
    /// Creates a restore point with SRSetRestorePoint (srclient.dll): a BEGIN_SYSTEM_CHANGE call followed by the
    /// matching END_SYSTEM_CHANGE, as Microsoft documents. A point left open is discarded by Windows, so after
    /// both calls the point is looked up and only reported as created if Windows actually lists it.
    /// </summary>
    public sealed class WinRestorePoint : IRestorePoint
    {
        private const string FrequencyKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        private const string FrequencyValue = "SystemRestorePointCreationFrequency";
        private const int BeginSystemChange = 100, EndSystemChange = 101, ModifySettings = 12;
        private readonly IRegistry _reg;
        private readonly IProcessRunner _proc;

        public WinRestorePoint(IRegistry reg, IProcessRunner proc) { _reg = reg; _proc = proc; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)] // srrestoreptapi.h uses #pragma pack(1)
        private struct RESTOREPOINTINFOW
        {
            public int dwEventType;
            public int dwRestorePtType;
            public long llSequenceNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szDescription;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct STATEMGRSTATUS
        {
            public int nStatus;
            public long llSequenceNumber;
        }

        [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SRSetRestorePointW(ref RESTOREPOINTINFOW pRestorePtSpec, out STATEMGRSTATUS pSMgrStatus);

        /// <summary>When the newest restore point was made, or null if there is none (or System Restore is off).</summary>
        public static DateTime? LastCreated()
        {
            try
            {
                DateTime? newest = null;
                using (var searcher = new ManagementObjectSearcher(@"root\default", "SELECT CreationTime FROM SystemRestore"))
                    foreach (ManagementObject o in searcher.Get())
                        using (o)
                        {
                            var t = ManagementDateTimeConverter.ToDateTime(Convert.ToString(o["CreationTime"]));
                            if (newest == null || t > newest) newest = t;
                        }
                return newest;
            }
            catch (Exception) { return null; }
        }

        public string Create(string description)
        {
            var drive = Path.GetPathRoot(Environment.SystemDirectory);
            // Windows silently skips a new point if another was made in the last 24 h; lift that limit for this call only.
            var previous = _reg.GetValue(Hive.LocalMachine, FrequencyKey, FrequencyValue);
            long lastBefore = LastSequence(out _);
            try
            {
                _reg.SetValue(Hive.LocalMachine, FrequencyKey, FrequencyValue, RegValue.DWord(0));
                if (!EnableProtection(drive, out var enableError)) return enableError;

                // 1) Checkpoint-Computer: Microsoft's own cmdlet, present in Windows PowerShell 5.1 on every Windows 10/11.
                var psError = CheckpointComputer(description);
                if (LastSequence(out _) > lastBefore) return null;

                // 2) Fallback: SRSetRestorePoint with a matching begin/end pair.
                var begin = new RESTOREPOINTINFOW { dwEventType = BeginSystemChange, dwRestorePtType = ModifySettings, szDescription = description };
                if (!SRSetRestorePointW(ref begin, out var status))
                    return "Checkpoint-Computer: " + psError + " / SRSetRestorePoint status " + status.nStatus + Hint(status.nStatus);
                var end = new RESTOREPOINTINFOW { dwEventType = EndSystemChange, dwRestorePtType = ModifySettings, llSequenceNumber = status.llSequenceNumber, szDescription = description };
                SRSetRestorePointW(ref end, out _);

                if (LastSequence(out var listed) > lastBefore) return null;
                return "Windows did not create the restore point (Checkpoint-Computer: " + (psError ?? "no error") + "; listed: " + listed
                    + "). System Protection may be turned off by policy or there may not be enough disk space.";
            }
            catch (DllNotFoundException)
            {
                return "System Restore is not available on this edition of Windows.";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                try
                {
                    if (previous == null) _reg.DeleteValue(Hive.LocalMachine, FrequencyKey, FrequencyValue);
                    else _reg.SetValue(Hive.LocalMachine, FrequencyKey, FrequencyValue, previous);
                }
                catch (Exception) { }
            }
        }

        private string CheckpointComputer(string description)
        {
            var script = "$ErrorActionPreference='Stop';$ProgressPreference='SilentlyContinue';" +
                         "Checkpoint-Computer -Description '" + description.Replace("'", "''") + "' -RestorePointType MODIFY_SETTINGS";
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            var path = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            var r = _proc.Run(path, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, 300000);
            return r.Ok ? null : (r.Error.Length > 0 ? r.Error : r.Output).Trim();
        }

        /// <summary>Turns System Protection on for the system drive when it is off (the Windows default on many PCs).</summary>
        private static bool EnableProtection(string drive, out string error)
        {
            error = null;
            try
            {
                using (var sr = new ManagementClass(new ManagementScope(@"\\.\root\default"), new ManagementPath("SystemRestore"), null))
                {
                    var args = sr.GetMethodParameters("Enable");
                    args["Drive"] = drive;
                    sr.InvokeMethod("Enable", args, null);
                }
                return true;
            }
            catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.InvalidClass || ex.ErrorCode == ManagementStatus.NotFound)
            {
                error = "System Restore is not available on this edition of Windows (for example Windows Server).";
                return false;
            }
            catch (Exception)
            {
                // Enabling can fail when protection is already on; creating the point still decides success.
                return true;
            }
        }

        /// <summary>Highest restore point sequence number Windows lists (0 when there are none), plus a short listing for diagnostics.</summary>
        public static long LastSequence(out string listing)
        {
            long max = 0;
            var parts = new System.Collections.Generic.List<string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\default", "SELECT SequenceNumber, Description, CreationTime FROM SystemRestore"))
                    foreach (ManagementObject o in searcher.Get())
                    {
                        var seq = Convert.ToInt64(o["SequenceNumber"]);
                        if (seq > max) max = seq;
                        parts.Add(seq + ":" + Convert.ToString(o["Description"]));
                    }
            }
            catch (Exception ex) { parts.Add("error " + ex.Message); }
            listing = parts.Count == 0 ? "none" : string.Join(", ", parts);
            return max;
        }

        private static string Hint(int status)
        {
            switch (status)
            {
                case 1058: return " (System Protection is turned off)";
                case 112: return " (not enough disk space)";
                default: return "";
            }
        }
    }
}
