using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    /// <summary>Read once at startup. Every probe is independent so one failing WMI class never hides the rest.</summary>
    public sealed class WinSystemInfo : ISystemInfo
    {
        public int Build { get; private set; }
        public int Ubr { get; private set; }
        public bool IsWin11 => Build >= 22000;
        public string ProductName { get; private set; } = "Windows";
        public string DisplayVersion { get; private set; } = "";
        public bool IsLaptop { get; private set; }
        public bool? SystemDriveIsSsd { get; private set; }
        public string CpuName { get; private set; } = "";
        public int LogicalProcessors { get; private set; } = Environment.ProcessorCount;
        public long TotalMemoryMb { get; private set; }
        public IList<string> Gpus { get; private set; } = new List<string>();
        public bool HasNvidia => Gpus.Any(g => g.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0);
        public bool HasAmdGpu => Gpus.Any(g => g.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 || g.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0);
        public bool IsVirtualMachine { get; private set; }

        public static WinSystemInfo Read(IRegistry reg)
        {
            var i = new WinSystemInfo();
            const string cv = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            Try(() => i.Build = int.Parse((string)reg.GetValue(Hive.LocalMachine, cv, "CurrentBuildNumber").Data));
            Try(() => i.Ubr = Convert.ToInt32(reg.GetValue(Hive.LocalMachine, cv, "UBR").Data));
            Try(() => i.DisplayVersion = (string)(reg.GetValue(Hive.LocalMachine, cv, "DisplayVersion") ?? reg.GetValue(Hive.LocalMachine, cv, "ReleaseId")).Data);
            Try(() =>
            {
                var name = (string)reg.GetValue(Hive.LocalMachine, cv, "ProductName").Data;
                // Windows 11 still reports "Windows 10" in ProductName.
                i.ProductName = i.IsWin11 ? name.Replace("Windows 10", "Windows 11") : name;
            });

            Try(() =>
            {
                foreach (ManagementObject o in Query(@"root\cimv2", "SELECT Name, NumberOfLogicalProcessors FROM Win32_Processor"))
                {
                    i.CpuName = Convert.ToString(o["Name"]).Trim();
                    i.LogicalProcessors = Convert.ToInt32(o["NumberOfLogicalProcessors"]);
                    break;
                }
            });
            Try(() =>
            {
                foreach (ManagementObject o in Query(@"root\cimv2", "SELECT TotalPhysicalMemory, Model, Manufacturer, PCSystemType FROM Win32_ComputerSystem"))
                {
                    i.TotalMemoryMb = (long)(Convert.ToUInt64(o["TotalPhysicalMemory"]) / 1024 / 1024);
                    var model = (Convert.ToString(o["Model"]) + " " + Convert.ToString(o["Manufacturer"])).ToLowerInvariant();
                    i.IsVirtualMachine = new[] { "virtual", "vmware", "virtualbox", "qemu", "kvm", "hyper-v", "parallels" }.Any(model.Contains);
                    // PCSystemType 2 = Mobile
                    if (o["PCSystemType"] != null && Convert.ToInt32(o["PCSystemType"]) == 2) i.IsLaptop = true;
                    break;
                }
            });
            Try(() =>
            {
                foreach (ManagementObject o in Query(@"root\cimv2", "SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                    if (o["ChassisTypes"] is ushort[] types && types.Any(t => new ushort[] { 8, 9, 10, 11, 14, 30, 31, 32 }.Contains(t)))
                        i.IsLaptop = true;
            });
            Try(() =>
            {
                if (Query(@"root\cimv2", "SELECT Name FROM Win32_Battery").Count > 0) i.IsLaptop = true;
            });
            Try(() =>
            {
                i.Gpus = Query(@"root\cimv2", "SELECT Name FROM Win32_VideoController").Cast<ManagementObject>()
                    .Select(o => Convert.ToString(o["Name"]))
                    .Where(n => !string.IsNullOrWhiteSpace(n) && n.IndexOf("Basic Display", StringComparison.OrdinalIgnoreCase) < 0 && n.IndexOf("Remote", StringComparison.OrdinalIgnoreCase) < 0)
                    .Distinct().ToList();
            });
            Try(() =>
            {
                var letter = Path.GetPathRoot(Environment.SystemDirectory).Substring(0, 1);
                uint? disk = null;
                foreach (ManagementObject p in Query(@"root\Microsoft\Windows\Storage", "SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '" + letter + "'"))
                { disk = Convert.ToUInt32(p["DiskNumber"]); break; }
                if (disk == null) return;
                foreach (ManagementObject d in Query(@"root\Microsoft\Windows\Storage", "SELECT DeviceId, MediaType FROM MSFT_PhysicalDisk"))
                {
                    if (Convert.ToString(d["DeviceId"]) != disk.ToString()) continue;
                    var media = Convert.ToInt32(d["MediaType"]);
                    i.SystemDriveIsSsd = media == 4 ? true : media == 3 ? (bool?)false : null;
                }
            });
            return i;
        }

        private static ManagementObjectCollection Query(string ns, string wql)
        {
            using (var s = new ManagementObjectSearcher(new ManagementScope(ns), new ObjectQuery(wql)))
                return s.Get();
        }

        private static void Try(Action a)
        {
            try { a(); } catch (Exception) { }
        }
    }
}
