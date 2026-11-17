using System;
using System.Collections.Generic;

namespace Optimizzzer.Core.Catalog
{
    /// <summary>Fluent helper so each tweak reads as one block in the catalog files.</summary>
    internal sealed class TweakBuilder
    {
        public const Hive LM = Hive.LocalMachine;
        public const Hive CU = Hive.CurrentUser;

        private readonly Tweak _t;

        public TweakBuilder(List<Tweak> sink, string id, Category cat, Risk risk)
        {
            _t = new Tweak { Id = id, Category = cat, Risk = risk };
            sink.Add(_t);
        }

        public TweakBuilder Title(string es, string en) { _t.Title = new Text(es, en); return this; }
        public TweakBuilder Summary(string es, string en) { _t.Summary = new Text(es, en); return this; }
        public TweakBuilder Why(string es, string en) { _t.Why = new Text(es, en); return this; }
        public TweakBuilder Downside(string es, string en) { _t.Downside = new Text(es, en); return this; }
        public TweakBuilder Source(string s) { _t.Source = s; return this; }
        public TweakBuilder Profiles(ProfileKind p) { _t.Profiles = p; return this; }
        public TweakBuilder Os(OsFilter os) { _t.Os = os; return this; }
        public TweakBuilder Win11(int minBuild = 22000) { _t.Os = OsFilter.Win11; _t.MinBuild = minBuild; return this; }
        public TweakBuilder Win10Only() { _t.Os = OsFilter.Win10; return this; }
        public TweakBuilder MinBuild(int b) { _t.MinBuild = b; return this; }
        public TweakBuilder Restart(RestartKind r) { _t.Restart = r; return this; }
        public TweakBuilder Hardware(Func<ISystemInfo, bool> f) { _t.HardwareFilter = f; return this; }
        public TweakBuilder AvoidOn(Func<ISystemInfo, bool> f) { _t.AvoidOn = f; return this; }

        public TweakBuilder Dword(Hive h, string path, string name, int value)
        {
            _t.Actions.Add(new RegistryValueAction(h, path, name, RegValue.DWord(value))); return this;
        }

        public TweakBuilder Str(Hive h, string path, string name, string value)
        {
            _t.Actions.Add(new RegistryValueAction(h, path, name, RegValue.Str(value))); return this;
        }

        public TweakBuilder DeleteValue(Hive h, string path, string name)
        {
            _t.Actions.Add(new RegistryValueAction(h, path, name, null)); return this;
        }

        public TweakBuilder DeleteKey(Hive h, string path)
        {
            _t.Actions.Add(new RegistryDeleteKeyAction(h, path)); return this;
        }

        public TweakBuilder CreateKey(Hive h, string path)
        {
            _t.Actions.Add(new RegistryCreateKeyAction(h, path)); return this;
        }

        public TweakBuilder Service(string name, StartMode mode)
        {
            _t.Actions.Add(new ServiceStartAction(name, mode)); return this;
        }

        public TweakBuilder Task(string path, bool enabled = false)
        {
            _t.Actions.Add(new ScheduledTaskAction(path, enabled)); return this;
        }

        public TweakBuilder Appx(string package)
        {
            _t.Actions.Add(new AppxRemoveAction(package)); return this;
        }

        public TweakBuilder Action(TweakAction a) { _t.Actions.Add(a); return this; }
    }
}
