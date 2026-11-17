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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RESTOREPOINTINFOW
        {
            public int dwEventType;
            public int dwRestorePtType;
            public long llSequenceNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szDescription;
        }

        [StructLayout(LayoutKind.Sequential)]
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
            var started = DateTime.UtcNow.AddMinutes(-1);
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

                return Exists(description, started) ? null
                    : "Windows accepted the request but did not create the restore point (System Protection may be limited by policy or disk space).";
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

        /// <summary>True when Windows lists a restore point with this description created after <paramref name="sinceUtc"/>.</summary>
        public static bool Exists(string description, DateTime sinceUtc)
        {
            using (var searcher = new ManagementObjectSearcher(@"root\default", "SELECT Description, CreationTime FROM SystemRestore"))
                foreach (ManagementObject o in searcher.Get())
                {
                    if (Convert.ToString(o["Description"]) != description) continue;
                    var created = ManagementDateTimeConverter.ToDateTime(Convert.ToString(o["CreationTime"])).ToUniversalTime();
                    if (created >= sinceUtc) return true;
                }
            return false;
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
