using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    public sealed class WinRegistry : IRegistry
    {
        private static RegistryKey Root(Hive h)
        {
            var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
            switch (h)
            {
                case Hive.LocalMachine: return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                case Hive.CurrentUser: return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
                case Hive.ClassesRoot: return RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view);
                default: return RegistryKey.OpenBaseKey(RegistryHive.Users, view);
            }
        }

        private static RegistryKey Open(Hive h, string path, bool write)
        {
            using (var root = Root(h)) return root.OpenSubKey(path, write);
        }

        public RegValue GetValue(Hive hive, string path, string name)
        {
            using (var k = Open(hive, path, false))
            {
                if (k == null) return null;
                var data = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (data == null) return null;
                switch (k.GetValueKind(name))
                {
                    case RegistryValueKind.DWord: return RegValue.DWord(unchecked((uint)(int)data));
                    case RegistryValueKind.QWord: return RegValue.QWord(unchecked((ulong)(long)data));
                    case RegistryValueKind.ExpandString: return RegValue.Expand((string)data);
                    case RegistryValueKind.MultiString: return RegValue.Multi((string[])data);
                    case RegistryValueKind.Binary: return RegValue.Bin((byte[])data);
                    case RegistryValueKind.String: return RegValue.Str((string)data);
                    default: return data is byte[] b ? RegValue.Bin(b) : RegValue.Str(Convert.ToString(data));
                }
            }
        }

        /// <summary>Turns "access denied" into ProtectedSettingException: as administrator, only Windows' own protection refuses.</summary>
        private static void Guard(Hive hive, string path, Action a)
        {
            try { a(); }
            catch (UnauthorizedAccessException ex) { throw new ProtectedSettingException(RegPathName(hive, path), ex); }
            catch (System.Security.SecurityException ex) { throw new ProtectedSettingException(RegPathName(hive, path), ex); }
        }

        private static string RegPathName(Hive h, string p) => (h == Hive.LocalMachine ? "HKLM" : h == Hive.CurrentUser ? "HKCU" : h.ToString()) + "\\" + p;

        public void SetValue(Hive hive, string path, string name, RegValue value) => Guard(hive, path, () => SetValueCore(hive, path, name, value));

        private void SetValueCore(Hive hive, string path, string name, RegValue value)
        {
            using (var root = Root(hive))
            using (var k = root.CreateSubKey(path, true))
            {
                switch (value.Kind)
                {
                    case RegKind.DWord: k.SetValue(name, unchecked((int)(uint)value.Data), RegistryValueKind.DWord); break;
                    case RegKind.QWord: k.SetValue(name, unchecked((long)(ulong)value.Data), RegistryValueKind.QWord); break;
                    case RegKind.String: k.SetValue(name, (string)value.Data, RegistryValueKind.String); break;
                    case RegKind.ExpandString: k.SetValue(name, (string)value.Data, RegistryValueKind.ExpandString); break;
                    case RegKind.MultiString: k.SetValue(name, (string[])value.Data, RegistryValueKind.MultiString); break;
                    default: k.SetValue(name, (byte[])value.Data, RegistryValueKind.Binary); break;
                }
            }
        }

        public void DeleteValue(Hive hive, string path, string name)
        {
            using (var k = Open(hive, path, true)) k?.DeleteValue(name, false);
        }

        public bool KeyExists(Hive hive, string path)
        {
            using (var k = Open(hive, path, false)) return k != null;
        }

        public void CreateKey(Hive hive, string path)
        {
            using (var root = Root(hive)) root.CreateSubKey(path, true)?.Dispose();
        }

        public void DeleteKeyTree(Hive hive, string path)
        {
            using (var root = Root(hive)) root.DeleteSubKeyTree(path, false);
        }

        private static readonly Lazy<bool> Elevated = new Lazy<bool>(() =>
        {
            using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        });

        public bool CanWrite(Hive hive, string path)
        {
            // Without elevation every HKLM key refuses writes; that is not Windows protecting the setting.
            if (!Elevated.Value) return true;
            try
            {
                using (var root = Root(hive))
                {
                    var p = path;
                    while (true)
                    {
                        using (var k = root.OpenSubKey(p, false))
                        {
                            if (k != null)
                            {
                                // Opening for write checks the ACL without changing anything.
                                using (root.OpenSubKey(p, true)) { }
                                return true;
                            }
                        }
                        int i = p.LastIndexOf('\\');
                        if (i < 0) return true;
                        p = p.Substring(0, i);
                    }
                }
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
        }

        public IList<string> GetValueNames(Hive hive, string path)
        {
            using (var k = Open(hive, path, false)) return k?.GetValueNames().ToList() ?? new List<string>();
        }

        public IList<string> GetSubKeyNames(Hive hive, string path)
        {
            using (var k = Open(hive, path, false)) return k?.GetSubKeyNames().ToList() ?? new List<string>();
        }
    }
}
