using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    public sealed class AppxInfo
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public bool NonRemovable { get; set; }
        public bool IsFramework { get; set; }
        /// <summary>System apps are part of Windows; removing them can break the shell.</summary>
        public bool IsSystem { get; set; }
    }

    /// <summary>Uses Windows PowerShell 5.1 (built in) for the Appx cmdlets, which have no stable .NET Framework API.</summary>
    public sealed class WinAppx : IAppxManager
    {
        private readonly IProcessRunner _proc;
        private HashSet<string> _installed;
        private readonly object _lock = new object();

        public WinAppx(IProcessRunner proc) { _proc = proc; }

        private static string Escape(string s) => s.Replace("'", "''");

        private ProcessResult PowerShell(string script, int timeoutMs = 180000)
        {
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(
                "$ProgressPreference='SilentlyContinue';[Console]::OutputEncoding=[Text.Encoding]::UTF8;" + script));
            return _proc.Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, timeoutMs);
        }

        public void Refresh()
        {
            lock (_lock) _installed = null;
        }

        public bool IsInstalled(string packageName)
        {
            lock (_lock)
            {
                if (_installed == null)
                    _installed = new HashSet<string>(List().Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
                return _installed.Contains(packageName);
            }
        }

        public void Remove(string packageName)
        {
            var n = Escape(packageName);
            var r = PowerShell(
                "$ErrorActionPreference='Continue';" +
                "Get-AppxPackage -AllUsers -Name '" + n + "' | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue };" +
                "Get-AppxPackage -Name '" + n + "' | Remove-AppxPackage -ErrorAction SilentlyContinue;" +
                "Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq '" + n + "' | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null;" +
                "if (Get-AppxPackage -Name '" + n + "') { Write-Error 'still installed'; exit 1 }");
            Refresh();
            if (!r.Ok) throw new InvalidOperationException("Could not remove " + packageName + ": " + (r.Error.Length > 0 ? r.Error : r.Output).Trim());
        }

        public IList<AppxInfo> List()
        {
            var r = PowerShell(
                "Get-AppxPackage -AllUsers | Select-Object Name,PackageFullName,Publisher,@{n='Version';e={$_.Version.ToString()}},NonRemovable,IsFramework,@{n='Sig';e={$_.SignatureKind.ToString()}} | ConvertTo-Json -Compress");
            var list = new List<AppxInfo>();
            if (!r.Ok || string.IsNullOrWhiteSpace(r.Output)) return list;
            var json = r.Output.Trim();
            if (json.StartsWith("{")) json = "[" + json + "]";
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            foreach (var o in ser.Deserialize<List<Dictionary<string, object>>>(json))
            {
                string S(string k) => o.TryGetValue(k, out var v) && v != null ? v.ToString() : "";
                bool B(string k) => o.TryGetValue(k, out var v) && v is bool b && b;
                list.Add(new AppxInfo
                {
                    Name = S("Name"),
                    FullName = S("PackageFullName"),
                    Publisher = S("Publisher"),
                    Version = S("Version"),
                    NonRemovable = B("NonRemovable"),
                    IsFramework = B("IsFramework"),
                    IsSystem = S("Sig") == "System",
                });
            }
            return list.GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
