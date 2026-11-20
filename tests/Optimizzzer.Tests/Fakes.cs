using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Optimizzzer.Core;

namespace Optimizzzer.Tests
{
    /// <summary>In-memory registry. Keys are case-insensitive like the real one.</summary>
    public sealed class FakeRegistry : IRegistry
    {
        private readonly Dictionary<string, Dictionary<string, RegValue>> _keys =
            new Dictionary<string, Dictionary<string, RegValue>>(StringComparer.OrdinalIgnoreCase);

        public Func<Hive, string, string, bool> FailOnSet { get; set; }
        /// <summary>Simulates Windows refusing a protected setting (WinRegistry turns access denied into this).</summary>
        public Func<Hive, string, string, bool> ProtectOnSet { get; set; }
        /// <summary>Keys (prefix) that behave like Windows' protected policy keys.</summary>
        public HashSet<string> Protected { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool CanWrite(Hive hive, string path) => !Protected.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        private static string K(Hive h, string p) => h + "\\" + p.TrimEnd('\\');

        public RegValue GetValue(Hive hive, string path, string name) =>
            _keys.TryGetValue(K(hive, path), out var vals) && vals.TryGetValue(name, out var v) ? v : null;

        public void SetValue(Hive hive, string path, string name, RegValue value)
        {
            if (FailOnSet != null && FailOnSet(hive, path, name)) throw new UnauthorizedAccessException("denied: " + path);
            if (ProtectOnSet != null && ProtectOnSet(hive, path, name)) throw new ProtectedSettingException(path, null);
            CreateKey(hive, path);
            _keys[K(hive, path)][name] = value;
        }

        public void DeleteValue(Hive hive, string path, string name)
        {
            if (_keys.TryGetValue(K(hive, path), out var vals)) vals.Remove(name);
        }

        public bool KeyExists(Hive hive, string path) => _keys.ContainsKey(K(hive, path));

        public void CreateKey(Hive hive, string path)
        {
            var parts = path.Split('\\');
            var cur = "";
            foreach (var p in parts)
            {
                cur = cur.Length == 0 ? p : cur + "\\" + p;
                if (!_keys.ContainsKey(K(hive, cur)))
                    _keys[K(hive, cur)] = new Dictionary<string, RegValue>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public void DeleteKeyTree(Hive hive, string path)
        {
            var root = K(hive, path);
            foreach (var k in _keys.Keys.Where(k => k.Equals(root, StringComparison.OrdinalIgnoreCase) || k.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)).ToList())
                _keys.Remove(k);
        }

        public IList<string> GetValueNames(Hive hive, string path) =>
            _keys.TryGetValue(K(hive, path), out var vals) ? vals.Keys.ToList() : new List<string>();

        public IList<string> GetSubKeyNames(Hive hive, string path)
        {
            var prefix = K(hive, path) + "\\";
            return _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(k => k.Substring(prefix.Length))
                .Where(rest => !rest.Contains('\\'))
                .ToList();
        }

        /// <summary>Stable text dump of every key and value, for before/after comparisons.</summary>
        public string Dump() => string.Join("\n", _keys.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(k => k.Key.ToLowerInvariant() + " {" + string.Join(";", k.Value.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase).Select(v => v.Key.ToLowerInvariant() + "=" + v.Value.Kind + ":" + v.Value.Serialize())) + "}"));
    }

    public sealed class FakeServices : IServiceManager
    {
        public Dictionary<string, StartMode> Modes { get; } = new Dictionary<string, StartMode>(StringComparer.OrdinalIgnoreCase);
        public bool Exists(string name) => Modes.ContainsKey(name);
        public StartMode? GetStartMode(string name) => Modes.TryGetValue(name, out var m) ? m : (StartMode?)null;
        public void SetStartMode(string name, StartMode mode) => Modes[name] = mode;
        public HashSet<string> Running { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public void Stop(string name) => Running.Remove(name);
        public bool IsRunning(string name) => Running.Contains(name);
        public void Start(string name) => Running.Add(name);
        public string Dump() => string.Join(";", Modes.OrderBy(m => m.Key).Select(m => m.Key + "=" + m.Value + (Running.Contains(m.Key) ? "+run" : "")));
    }

    public sealed class FakeTasks : ITaskScheduler
    {
        public Dictionary<string, bool> Tasks { get; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public bool? IsEnabled(string path) => Tasks.TryGetValue(path, out var e) ? e : (bool?)null;
        public void SetEnabled(string path, bool enabled) => Tasks[path] = enabled;
        public string Dump() => string.Join(";", Tasks.OrderBy(m => m.Key).Select(m => m.Key + "=" + m.Value));
    }

    /// <summary>Simulates powercfg well enough for the power plan tweaks.</summary>
    public sealed class FakeProcess : IProcessRunner
    {
        public List<string> Calls { get; } = new List<string>();
        public string ActiveScheme { get; set; } = "381b4222-f694-41f0-9685-ff5bb260df2e";
        public HashSet<string> Schemes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "381b4222-f694-41f0-9685-ff5bb260df2e" };
        public FakeRegistry Registry { get; set; }

        /// <summary>The next powercfg call whose arguments contain this text fails.</summary>
        public string FailNext { get; set; }

        public ProcessResult Run(string file, string arguments, int timeoutMs = 120000)
        {
            Calls.Add(file + " " + arguments);
            if (FailNext != null && arguments.Contains(FailNext)) { FailNext = null; return new ProcessResult(1, "", "denied"); }
            if (file != "powercfg") return new ProcessResult(0, "", "");
            var a = arguments.Split(' ');
            switch (a[0].ToLowerInvariant())
            {
                case "/getactivescheme": return new ProcessResult(0, "Power Scheme GUID: " + ActiveScheme + "  (Balanced)", "");
                case "/list": return new ProcessResult(0, string.Join("\n", Schemes), "");
                case "-duplicatescheme": Schemes.Add(a[2]); return new ProcessResult(0, "", "");
                case "/setactive": ActiveScheme = a[1]; return new ProcessResult(0, "", "");
                case "/delete":
                    // Like Windows: the active plan cannot be deleted.
                    if (string.Equals(a[1], ActiveScheme, StringComparison.OrdinalIgnoreCase)) return new ProcessResult(1, "", "active");
                    Schemes.Remove(a[1]); return new ProcessResult(0, "", "");
                case "/hibernate":
                    Registry?.SetValue(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", RegValue.DWord(a[1] == "on" ? 1 : 0));
                    return new ProcessResult(0, "", "");
            }
            return new ProcessResult(0, "", "");
        }

        public int RunStreaming(string file, string arguments, Action<string> onLine, CancellationToken ct)
        {
            Calls.Add(file + " " + arguments);
            onLine("ok");
            return 0;
        }
    }

    public sealed class FakeAppx : IAppxManager
    {
        public HashSet<string> Installed { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public bool IsInstalled(string packageName) => Installed.Contains(packageName);
        public void Remove(string packageName) => Installed.Remove(packageName);
    }

    public sealed class FakeRestorePoint : IRestorePoint
    {
        public int Created { get; private set; }
        public string Fail { get; set; }
        public string Create(string description) { if (Fail == null) Created++; return Fail; }
    }

    public sealed class FakeInfo : ISystemInfo
    {
        public int Build { get; set; } = 26100;
        public int Ubr { get; set; } = 2000;
        public bool IsWin11 => Build >= 22000;
        public string ProductName { get; set; } = "Windows 11 Pro";
        public string DisplayVersion { get; set; } = "24H2";
        public bool IsLaptop { get; set; }
        public bool? SystemDriveIsSsd { get; set; } = true;
        public string CpuName { get; set; } = "Test CPU";
        public int LogicalProcessors { get; set; } = 8;
        public long TotalMemoryMb { get; set; } = 16384;
        public IList<string> Gpus { get; set; } = new List<string> { "NVIDIA GeForce RTX 3060" };
        public bool HasNvidia { get; set; } = true;
        public bool HasAmdGpu { get; set; }
        public bool IsVirtualMachine { get; set; }
    }

    public sealed class FakeSystem
    {
        public FakeRegistry Registry { get; } = new FakeRegistry();
        public FakeServices Services { get; } = new FakeServices();
        public FakeTasks Tasks { get; } = new FakeTasks();
        public FakeProcess Process { get; } = new FakeProcess();
        public FakeAppx Appx { get; } = new FakeAppx();
        public FakeRestorePoint RestorePoint { get; } = new FakeRestorePoint();
        public FakeInfo Info { get; } = new FakeInfo();
        public SystemContext Context { get; }

        public FakeSystem()
        {
            Process.Registry = Registry;
            Context = new SystemContext
            {
                Registry = Registry, Services = Services, Tasks = Tasks, Process = Process,
                Appx = Appx, RestorePoint = RestorePoint, Info = Info,
            };
        }

        /// <summary>Makes every service and task referenced by the catalog exist in a typical default state.</summary>
        public FakeSystem WithDefaults(IEnumerable<Tweak> catalog)
        {
            foreach (var a in catalog.SelectMany(t => t.Actions))
            {
                if (a is ServiceStartAction s) Services.Modes[s.Service] = StartMode.Manual;
                if (a is ScheduledTaskAction t) Tasks.Tasks[t.TaskPath] = true;
                if (a is AppxRemoveAction x) Appx.Installed.Add(x.PackageName);
            }
            Registry.SetValue(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", RegValue.DWord(1));
            Registry.SetValue(Hive.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", RegValue.Str("400"));
            Registry.SetValue(Hive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", RegValue.Str("AutoHDREnable=1;"));
            Registry.CreateKey(Hive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}");
            Registry.SetValue(Hive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}", "", RegValue.Str("CLSID_MSGraphHomeFolder"));
            return this;
        }

        public string Dump() => Registry.Dump() + "\n#svc " + Services.Dump() + "\n#task " + Tasks.Dump() + "\n#power " + Process.ActiveScheme + " " + string.Join(",", Process.Schemes.OrderBy(s => s));
    }
}
