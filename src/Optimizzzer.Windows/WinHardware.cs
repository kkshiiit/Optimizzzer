using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Optimizzzer.Windows
{
    /// <summary>What Home shows under "Your PC". Every field is read from Windows; null means it could not be read.</summary>
    public sealed class HardwareReport
    {
        public string Cpu;
        public int? Cores, Threads;
        public int? CpuMaxMhz;
        public long? RamBytes;
        public int? RamMhz;
        public int RamModules;
        public List<(string Name, long? MemoryBytes)> Gpus = new List<(string, long?)>();
        public List<(string Name, string Kind, long? Bytes)> Disks = new List<(string, string, long?)>();
        public int? DisplayWidth, DisplayHeight, DisplayHz, DisplayMaxHz;
        public string Board;
        public string Windows;
    }

    public static class WinHardware
    {
        /// <summary>Slow (WMI, a second or two): call from a background thread.</summary>
        public static HardwareReport Read()
        {
            var r = new HardwareReport();
            Try(() =>
            {
                foreach (ManagementObject o in Query(@"root\cimv2", "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor"))
                    using (o)
                    {
                        r.Cpu = Convert.ToString(o["Name"])?.Trim();
                        r.Cores = (r.Cores ?? 0) + ToInt(o["NumberOfCores"]);
                        r.Threads = (r.Threads ?? 0) + ToInt(o["NumberOfLogicalProcessors"]);
                        r.CpuMaxMhz = ToInt(o["MaxClockSpeed"]);
                    }
            });
            Try(() =>
            {
                long total = 0; int mhz = 0, n = 0;
                foreach (ManagementObject o in Query(@"root\cimv2", "SELECT Capacity, Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory"))
                    using (o)
                    {
                        total += ToLong(o["Capacity"]);
                        n++;
                        mhz = Math.Max(mhz, Math.Max(ToInt(o["ConfiguredClockSpeed"]), ToInt(o["Speed"])));
                    }
                if (total > 0) r.RamBytes = total;
                if (mhz > 0) r.RamMhz = mhz;
                r.RamModules = n;
            });
            Try(() =>
            {
                foreach (ManagementObject o in Query(@"root\cimv2", "SELECT Name, PNPDeviceID FROM Win32_VideoController"))
                    using (o)
                    {
                        var name = Convert.ToString(o["Name"])?.Trim();
                        if (string.IsNullOrEmpty(name)) continue;
                        r.Gpus.Add((name, VideoMemory(name)));
                    }
            });
            Try(() =>
            {
                foreach (ManagementObject o in Query(@"root\Microsoft\Windows\Storage", "SELECT FriendlyName, MediaType, BusType, Size FROM MSFT_PhysicalDisk"))
                    using (o)
                    {
                        int media = ToInt(o["MediaType"]), bus = ToInt(o["BusType"]);
                        string kind = bus == 17 ? "NVMe" : media == 4 ? "SSD" : media == 3 ? "HDD" : null;
                        r.Disks.Add((Convert.ToString(o["FriendlyName"])?.Trim(), kind, ToLong(o["Size"])));
                    }
            });
            Try(() =>
            {
                foreach (ManagementObject o in Query(@"root\cimv2", "SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                    using (o) r.Board = (Convert.ToString(o["Manufacturer"]) + " " + Convert.ToString(o["Product"])).Trim();
            });
            Try(() =>
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var build = ToInt(k?.GetValue("CurrentBuildNumber"));
                    var product = Convert.ToString(k?.GetValue("ProductName"));
                    if (build >= 22000) product = product?.Replace("Windows 10", "Windows 11");
                    r.Windows = (product + " " + Convert.ToString(k?.GetValue("DisplayVersion"))).Trim();
                }
            });
            Try(() => ReadDisplay(r));
            return r;
        }

        /// <summary>Dedicated video memory from the driver's registry entry (WMI's AdapterRAM stops at 4 GB).</summary>
        private static long? VideoMemory(string name)
        {
            using (var cls = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
            {
                if (cls == null) return null;
                foreach (var sub in cls.GetSubKeyNames().Where(n => n.Length == 4))
                    using (var k = cls.OpenSubKey(sub))
                    {
                        if (!string.Equals(Convert.ToString(k?.GetValue("DriverDesc")), name, StringComparison.OrdinalIgnoreCase)) continue;
                        var q = k.GetValue("HardwareInformation.qwMemorySize");
                        if (q != null) return ToLong(q);
                        var d = k.GetValue("HardwareInformation.MemorySize");
                        if (d is byte[] b && b.Length >= 4) return BitConverter.ToUInt32(b, 0);
                        if (d != null) return ToLong(d);
                    }
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        /// <summary>Current mode of the main screen, and the highest refresh rate it offers at that same resolution.</summary>
        private static void ReadDisplay(HardwareReport r)
        {
            const int ENUM_CURRENT_SETTINGS = -1;
            var cur = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref cur)) return;
            r.DisplayWidth = cur.dmPelsWidth; r.DisplayHeight = cur.dmPelsHeight; r.DisplayHz = cur.dmDisplayFrequency;
            int max = cur.dmDisplayFrequency;
            var m = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            for (int i = 0; EnumDisplaySettings(null, i, ref m) && i < 5000; i++)
                if (m.dmPelsWidth == cur.dmPelsWidth && m.dmPelsHeight == cur.dmPelsHeight && m.dmDisplayFrequency > max) max = m.dmDisplayFrequency;
            r.DisplayMaxHz = max;
        }

        private static IEnumerable<ManagementObject> Query(string scope, string wql)
        {
            using (var s = new ManagementObjectSearcher(scope, wql))
                foreach (ManagementObject o in s.Get()) yield return o;
        }

        private static int ToInt(object v) { try { return v == null ? 0 : Convert.ToInt32(v); } catch (Exception) { return 0; } }
        private static long ToLong(object v) { try { return v == null ? 0 : Convert.ToInt64(v); } catch (Exception) { return 0; } }
        private static void Try(Action a) { try { a(); } catch (Exception) { } }
    }
}
