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

        public WinRestorePoint(IRegistry reg) { _reg = reg; }

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

                var begin = new RESTOREPOINTINFOW { dwEventType = BeginSystemChange, dwRestorePtType = ModifySettings, szDescription = description };
                if (!SRSetRestorePointW(ref begin, out var status))
                    return "SRSetRestorePoint failed with status " + status.nStatus + Hint(status.nStatus);
                var end = new RESTOREPOINTINFOW { dwEventType = EndSystemChange, dwRestorePtType = ModifySettings, llSequenceNumber = status.llSequenceNumber, szDescription = description };
                if (!SRSetRestorePointW(ref end, out var endStatus))
                    return "SRSetRestorePoint (end) failed with status " + endStatus.nStatus + Hint(endStatus.nStatus);

                if (LastSequence(out var listed) > lastBefore) return null;
                return "Windows accepted the request but did not create the restore point (sequence " + status.llSequenceNumber
                    + ", listed: " + listed + "). System Protection may be limited by policy or disk space.";
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
