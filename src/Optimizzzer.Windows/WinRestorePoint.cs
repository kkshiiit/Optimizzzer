using System;
using System.IO;
using System.Management;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    /// <summary>Creates a restore point through WMI. Turns on System Protection for the system drive if it is off.</summary>
    public sealed class WinRestorePoint : IRestorePoint
    {
        private const string FrequencyKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        private const string FrequencyValue = "SystemRestorePointCreationFrequency";
        private readonly IRegistry _reg;

        public WinRestorePoint(IRegistry reg) { _reg = reg; }

        public string Create(string description)
        {
            var drive = Path.GetPathRoot(Environment.SystemDirectory);
            // Windows silently skips a new point if another was made in the last 24 h; lift that limit for this call only.
            var previous = _reg.GetValue(Hive.LocalMachine, FrequencyKey, FrequencyValue);
            try
            {
                _reg.SetValue(Hive.LocalMachine, FrequencyKey, FrequencyValue, RegValue.DWord(0));
                using (var sr = new ManagementClass(new ManagementScope(@"\\.\root\default"), new ManagementPath("SystemRestore"), null))
                {
                    try
                    {
                        var enableArgs = sr.GetMethodParameters("Enable");
                        enableArgs["Drive"] = drive;
                        sr.InvokeMethod("Enable", enableArgs, null);
                    }
                    catch (Exception) { }

                    var args = sr.GetMethodParameters("CreateRestorePoint");
                    args["Description"] = description;
                    args["RestorePointType"] = 12; // MODIFY_SETTINGS
                    args["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
                    var result = sr.InvokeMethod("CreateRestorePoint", args, null);
                    var code = Convert.ToUInt32(result["ReturnValue"]);
                    return code == 0 ? null : "SystemRestore.CreateRestorePoint returned " + code;
                }
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
    }
}
