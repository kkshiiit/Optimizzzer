using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Optimizzzer.Core.Tools
{
    public sealed class DnsProvider
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Text Description { get; set; }
        public string[] IPv4 { get; set; } = new string[0];
        public string[] IPv6 { get; set; } = new string[0];
    }

    public sealed class DnsLatency
    {
        public DnsProvider Provider { get; set; }
        /// <summary>Median of the successful queries, or null if every query failed.</summary>
        public double? MedianMs { get; set; }
        public int Failures { get; set; }
    }

    public static class DnsCatalog
    {
        public static readonly IReadOnlyList<DnsProvider> All = new[]
        {
            new DnsProvider { Id = "cloudflare", Name = "Cloudflare", IPv4 = new[] { "1.1.1.1", "1.0.0.1" }, IPv6 = new[] { "2606:4700:4700::1111", "2606:4700:4700::1001" },
                Description = new Text("Rápido y con política de no guardar tu IP más de 25 horas.", "Fast, with a policy of not keeping your IP for more than 25 hours.") },
            new DnsProvider { Id = "cloudflareFamily", Name = "Cloudflare for Families", IPv4 = new[] { "1.1.1.3", "1.0.0.3" }, IPv6 = new[] { "2606:4700:4700::1113", "2606:4700:4700::1003" },
                Description = new Text("Igual que Cloudflare, pero bloquea malware y contenido para adultos.", "Like Cloudflare, but blocks malware and adult content.") },
            new DnsProvider { Id = "google", Name = "Google Public DNS", IPv4 = new[] { "8.8.8.8", "8.8.4.4" }, IPv6 = new[] { "2001:4860:4860::8888", "2001:4860:4860::8844" },
                Description = new Text("Muy fiable y con servidores en todo el mundo.", "Very reliable, with servers worldwide.") },
            new DnsProvider { Id = "quad9", Name = "Quad9", IPv4 = new[] { "9.9.9.9", "149.112.112.112" }, IPv6 = new[] { "2620:fe::fe", "2620:fe::9" },
                Description = new Text("Bloquea dominios maliciosos conocidos. Fundación sin ánimo de lucro con sede en Suiza.", "Blocks known malicious domains. Non-profit foundation based in Switzerland.") },
            new DnsProvider { Id = "adguard", Name = "AdGuard DNS", IPv4 = new[] { "94.140.14.14", "94.140.15.15" }, IPv6 = new[] { "2a10:50c0::ad1:ff", "2a10:50c0::ad2:ff" },
                Description = new Text("Bloquea anuncios y rastreadores en todo el equipo.", "Blocks ads and trackers across the whole PC.") },
        };
    }

    public static class DnsBenchmark
    {
        private static readonly string[] Domains = { "www.microsoft.com", "www.wikipedia.org", "www.youtube.com", "github.com", "www.amazon.com" };

        /// <summary>Builds a standard A-record query packet (RFC 1035).</summary>
        public static byte[] BuildQuery(string domain, ushort id)
        {
            var bytes = new List<byte> { (byte)(id >> 8), (byte)id, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0 };
            foreach (var label in domain.Split('.'))
            {
                bytes.Add((byte)label.Length);
                bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(label));
            }
            bytes.AddRange(new byte[] { 0, 0, 1, 0, 1 });
            return bytes.ToArray();
        }

        public static async Task<double?> QueryOnceAsync(string server, string domain, int timeoutMs = 1500)
        {
            var ip = IPAddress.Parse(server);
            using (var udp = new UdpClient(ip.AddressFamily))
            {
                var id = (ushort)new Random().Next(1, 65535);
                var packet = BuildQuery(domain, id);
                var sw = Stopwatch.StartNew();
                await udp.SendAsync(packet, packet.Length, new IPEndPoint(ip, 53)).ConfigureAwait(false);
                var receive = udp.ReceiveAsync();
                var done = await Task.WhenAny(receive, Task.Delay(timeoutMs)).ConfigureAwait(false);
                if (done != receive) return null;
                var resp = (await receive.ConfigureAwait(false)).Buffer;
                sw.Stop();
                if (resp.Length < 4 || resp[0] != (byte)(id >> 8) || resp[1] != (byte)id) return null;
                return sw.Elapsed.TotalMilliseconds;
            }
        }

        /// <summary>Queries each domain against the primary IPv4 server and reports the median.</summary>
        public static async Task<DnsLatency> MeasureAsync(DnsProvider p, int rounds = 2)
        {
            var samples = new List<double>();
            int failures = 0;
            for (int r = 0; r < rounds; r++)
                foreach (var d in Domains)
                {
                    double? ms;
                    try { ms = await QueryOnceAsync(p.IPv4[0], d).ConfigureAwait(false); }
                    catch (Exception) { ms = null; }
                    if (ms == null) failures++; else samples.Add(ms.Value);
                }
            return new DnsLatency { Provider = p, MedianMs = Median(samples), Failures = failures };
        }

        public static double? Median(IList<double> values)
        {
            if (values.Count == 0) return null;
            var s = values.OrderBy(v => v).ToList();
            int n = s.Count;
            return n % 2 == 1 ? s[n / 2] : (s[n / 2 - 1] + s[n / 2]) / 2;
        }
    }
}
