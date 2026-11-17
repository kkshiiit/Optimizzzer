using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    /// <summary>Start mode is read and written in the registry, which is what services.msc does under the hood.</summary>
    public sealed class WinServices : IServiceManager
    {
        private const string Base = @"SYSTEM\CurrentControlSet\Services\";
        private readonly IRegistry _reg;
        private readonly IProcessRunner _proc;

        public WinServices(IRegistry reg, IProcessRunner proc) { _reg = reg; _proc = proc; }

        public bool Exists(string name) => _reg.GetValue(Hive.LocalMachine, Base + name, "Start") != null;

        public StartMode? GetStartMode(string name)
        {
            var start = _reg.GetValue(Hive.LocalMachine, Base + name, "Start");
            if (start == null) return null;
            switch (Convert.ToInt32(start.Data))
            {
                case 0: return StartMode.Boot;
                case 1: return StartMode.System;
                case 2:
                    var delayed = _reg.GetValue(Hive.LocalMachine, Base + name, "DelayedAutostart");
                    return delayed != null && Convert.ToInt32(delayed.Data) == 1 ? StartMode.AutomaticDelayed : StartMode.Automatic;
                case 3: return StartMode.Manual;
                default: return StartMode.Disabled;
            }
        }

        public void SetStartMode(string name, StartMode mode)
        {
            // sc.exe goes through the Service Control Manager, so the change is live without a reboot.
            string sc;
            switch (mode)
            {
                case StartMode.Automatic: sc = "auto"; break;
                case StartMode.AutomaticDelayed: sc = "delayed-auto"; break;
                case StartMode.Manual: sc = "demand"; break;
                case StartMode.Disabled: sc = "disabled"; break;
                case StartMode.System: sc = "system"; break;
                default: sc = "boot"; break;
            }
            var r = _proc.Run("sc.exe", "config \"" + name + "\" start= " + sc, 30000);
            if (r.Ok && GetStartMode(name) == mode) return;

            // Some per-user and protected services refuse sc.exe but accept the registry write.
            int value = mode == StartMode.Boot ? 0 : mode == StartMode.System ? 1 : mode == StartMode.Manual ? 3 : mode == StartMode.Disabled ? 4 : 2;
            _reg.SetValue(Hive.LocalMachine, Base + name, "Start", RegValue.DWord(value));
            if (mode == StartMode.AutomaticDelayed) _reg.SetValue(Hive.LocalMachine, Base + name, "DelayedAutostart", RegValue.DWord(1));
            else if (mode == StartMode.Automatic) _reg.DeleteValue(Hive.LocalMachine, Base + name, "DelayedAutostart");
            if (GetStartMode(name) != mode)
                throw new InvalidOperationException("Could not change service " + name + ": " + (r.Output + " " + r.Error).Trim());
        }

        public void Stop(string name)
        {
            try
            {
                using (var sc = new ServiceController(name))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped || !sc.CanStop) return;
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(15));
                }
            }
            catch (Exception)
            {
                // Stopping is a convenience; the start mode change already took effect for the next boot.
            }
        }
    }

    public sealed class ServiceInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool Running { get; set; }
        public StartMode? StartMode { get; set; }
        /// <summary>Services Windows cannot run without. The UI never lets these be changed.</summary>
        public bool Protected { get; set; }
    }

    public static class ServiceList
    {
        private static readonly HashSet<string> Critical = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "SamSs", "Winmgmt", "EventLog", "Power", "PlugPlay", "ProfSvc",
            "BFE", "MpsSvc", "WinDefend", "SecurityHealthService", "wscsvc", "Dnscache", "Dhcp", "nsi", "CryptSvc", "gpsvc",
            "Schedule", "SystemEventsBroker", "BrokerInfrastructure", "CoreMessagingRegistrar", "StateRepository", "UserManager",
            "AudioSrv", "AudioEndpointBuilder", "Themes", "TrustedInstaller", "wuauserv", "WaaSMedicSvc", "UsoSvc", "BITS",
            "LanmanWorkstation", "NlaSvc", "netprofm", "Wcmsvc", "WlanSvc", "EventSystem", "FontCache", "ShellHWDetection",
            "TimeBrokerSvc", "CDPSvc", "TokenBroker", "AppXSvc", "ClipSVC", "msiserver", "KeyIso", "VaultSvc", "camsvc", "TabletInputService",
        };

        public static IList<ServiceInfo> Load(WinServices svc)
        {
            var list = new List<ServiceInfo>();
            foreach (var sc in ServiceController.GetServices())
            {
                using (sc)
                {
                    try
                    {
                        var mode = svc.GetStartMode(sc.ServiceName);
                        var desc = new WinRegistry().GetValue(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\" + sc.ServiceName, "Description")?.Data as string;
                        list.Add(new ServiceInfo
                        {
                            Name = sc.ServiceName,
                            DisplayName = Native.ResolveIndirect(sc.DisplayName),
                            Description = Native.ResolveIndirect(desc),
                            Running = sc.Status == ServiceControllerStatus.Running,
                            StartMode = mode,
                            Protected = Critical.Contains(sc.ServiceName) || mode == StartMode.Boot || mode == StartMode.System,
                        });
                    }
                    catch (Exception) { }
                }
            }
            return list.OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
