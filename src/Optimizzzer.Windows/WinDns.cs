using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Optimizzzer.Core;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.Windows
{
    public sealed class AdapterDns
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public IList<string> Servers { get; set; } = new List<string>();
        /// <summary>True when the servers come from the router (DHCP) rather than being set by hand.</summary>
        public bool Automatic { get; set; }
    }

    [DataContract]
    public sealed class DnsBackup
    {
        [DataMember] public string AdapterId { get; set; }
        [DataMember] public string AdapterName { get; set; }
        [DataMember] public bool Automatic { get; set; }
        [DataMember] public List<string> V4 { get; set; } = new List<string>();
        [DataMember] public List<string> V6 { get; set; } = new List<string>();
    }

    public sealed class WinDns
    {
        private readonly IProcessRunner _proc;
        private readonly IRegistry _reg;
        private readonly string _backupPath;
        private static readonly DataContractJsonSerializer Ser = new DataContractJsonSerializer(typeof(List<DnsBackup>));

        public WinDns(IProcessRunner proc, IRegistry reg, string dataDir)
        {
            _proc = proc; _reg = reg; _backupPath = Path.Combine(dataDir, "dns-backup.json");
        }

        public IList<AdapterDns> Adapters()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                            && n.GetIPProperties().GatewayAddresses.Count > 0)
                .Select(n => new AdapterDns
                {
                    Id = n.Id,
                    Name = n.Name,
                    Description = n.Description,
                    Servers = n.GetIPProperties().DnsAddresses.Where(a => !a.IsIPv6SiteLocal).Select(a => a.ToString()).ToList(),
                    Automatic = IsAutomatic(n.Id, "Tcpip"),
                }).ToList();
        }

        private bool IsAutomatic(string id, string stack)
        {
            var v = _reg.GetValue(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\" + stack + @"\Parameters\Interfaces\" + id, "NameServer");
            return v == null || string.IsNullOrWhiteSpace(v.Data as string);
        }

        private List<string> Static(string id, string stack)
        {
            var v = _reg.GetValue(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\" + stack + @"\Parameters\Interfaces\" + id, "NameServer")?.Data as string;
            return (v ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public bool HasBackup => LoadBackup().Count > 0;

        public void Apply(DnsProvider p, IEnumerable<AdapterDns> adapters)
        {
            var backups = LoadBackup();
            foreach (var a in adapters)
            {
                // Keep only the very first backup per adapter: that is the user's own setting.
                if (!backups.Any(b => b.AdapterId == a.Id))
                    backups.Add(new DnsBackup { AdapterId = a.Id, AdapterName = a.Name, Automatic = IsAutomatic(a.Id, "Tcpip"), V4 = Static(a.Id, "Tcpip"), V6 = Static(a.Id, "Tcpip6") });
                SaveBackup(backups);
                SetServers(a.Name, "ipv4", p.IPv4);
                if (p.IPv6.Length > 0 && Socket.OSSupportsIPv6) SetServers(a.Name, "ipv6", p.IPv6, throwOnError: false);
            }
            _proc.Run("ipconfig.exe", "/flushdns");
        }

        /// <summary>
        /// Puts back the DNS each adapter had before Optimizzzer changed it. Adapters are found by their Id, because
        /// the name can change. A backup entry is deleted only once its adapter was restored, so nothing is lost.
        /// </summary>
        public void Restore()
        {
            var backups = LoadBackup();
            var remaining = new List<DnsBackup>();
            var errors = new List<string>();
            foreach (var b in backups)
            {
                var name = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == b.AdapterId)?.Name ?? b.AdapterName;
                try
                {
                    if (b.Automatic) Run("interface ipv4 set dnsservers name=\"" + name + "\" source=dhcp", true);
                    else SetServers(name, "ipv4", b.V4.ToArray(), throwOnError: true);
                    if (b.V6.Count == 0) Run("interface ipv6 set dnsservers name=\"" + name + "\" source=dhcp", false);
                    else SetServers(name, "ipv6", b.V6.ToArray(), throwOnError: false);
                }
                catch (Exception ex)
                {
                    remaining.Add(b);
                    errors.Add(name + ": " + ex.Message);
                }
            }
            _proc.Run("ipconfig.exe", "/flushdns");
            if (remaining.Count == 0) { if (File.Exists(_backupPath)) File.Delete(_backupPath); }
            else
            {
                SaveBackup(remaining);
                throw new InvalidOperationException("Could not restore the previous DNS for: " + string.Join("; ", errors));
            }
        }

        private void SetServers(string adapter, string family, string[] servers, bool throwOnError = true)
        {
            if (servers.Length == 0) return;
            Run("interface " + family + " set dnsservers name=\"" + adapter + "\" static " + servers[0] + " primary validate=no", throwOnError);
            for (int i = 1; i < servers.Length; i++)
                Run("interface " + family + " add dnsservers name=\"" + adapter + "\" " + servers[i] + " index=" + (i + 1) + " validate=no", throwOnError);
        }

        private void Run(string args, bool throwOnError)
        {
            var r = _proc.Run("netsh.exe", args, 30000);
            if (!r.Ok && throwOnError) throw new InvalidOperationException("netsh " + args + ": " + (r.Output + r.Error).Trim());
        }

        private List<DnsBackup> LoadBackup()
        {
            try
            {
                if (!File.Exists(_backupPath)) return new List<DnsBackup>();
                using (var fs = File.OpenRead(_backupPath)) return (List<DnsBackup>)Ser.ReadObject(fs);
            }
            catch (Exception) { return new List<DnsBackup>(); }
        }

        private void SaveBackup(List<DnsBackup> b)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_backupPath));
            using (var fs = File.Create(_backupPath)) Ser.WriteObject(fs, b);
        }
    }
}
