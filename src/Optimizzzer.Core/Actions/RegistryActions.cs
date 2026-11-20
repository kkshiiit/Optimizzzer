using System;
using System.Collections.Generic;

namespace Optimizzzer.Core
{
    internal static class RegPath
    {
        public static string HiveName(Hive h)
        {
            switch (h)
            {
                case Hive.LocalMachine: return "HKLM";
                case Hive.CurrentUser: return "HKCU";
                case Hive.ClassesRoot: return "HKCR";
                default: return "HKU";
            }
        }

        public static string Full(Hive h, string path) => HiveName(h) + "\\" + path;

        /// <summary>The shallowest key along <paramref name="path"/> that does not exist, or null when the full path exists.</summary>
        public static string FirstMissing(IRegistry reg, Hive hive, string path)
        {
            if (reg.KeyExists(hive, path)) return null;
            var parts = path.Split('\\');
            var current = "";
            foreach (var p in parts)
            {
                current = current.Length == 0 ? p : current + "\\" + p;
                if (!reg.KeyExists(hive, current)) return current;
            }
            return path;
        }

        /// <summary>
        /// Removes <paramref name="path"/> and its parents up to and including <paramref name="createdKey"/>, but only
        /// while each key is empty. Keys Optimizzzer created may since hold values from other tweaks or from the user.
        /// </summary>
        public static void RemoveEmptyKeys(IRegistry reg, Hive hive, string path, string createdKey)
        {
            if (string.IsNullOrEmpty(createdKey)) return;
            var current = path;
            while (current != null && current.Length >= createdKey.Length
                   && current.StartsWith(createdKey, StringComparison.OrdinalIgnoreCase))
            {
                if (!reg.KeyExists(hive, current)) { current = Parent(current); continue; }
                if (reg.GetValueNames(hive, current).Count > 0 || reg.GetSubKeyNames(hive, current).Count > 0) return;
                reg.DeleteKeyTree(hive, current);
                current = Parent(current);
            }
        }

        private static string Parent(string p)
        {
            int i = p.LastIndexOf('\\');
            return i < 0 ? null : p.Substring(0, i);
        }

        public static RegTreeNode Export(IRegistry reg, Hive hive, string path, string name)
        {
            var node = new RegTreeNode { Name = name };
            foreach (var v in reg.GetValueNames(hive, path))
            {
                var val = reg.GetValue(hive, path, v);
                if (val == null) continue;
                node.Values.Add(new RegTreeValue { Name = v, Kind = val.Kind.ToString(), Data = val.Serialize() });
            }
            foreach (var sub in reg.GetSubKeyNames(hive, path))
                node.Children.Add(Export(reg, hive, path + "\\" + sub, sub));
            return node;
        }

        public static void Import(IRegistry reg, Hive hive, string path, RegTreeNode node)
        {
            reg.CreateKey(hive, path);
            foreach (var v in node.Values)
            {
                var kind = (RegKind)Enum.Parse(typeof(RegKind), v.Kind);
                reg.SetValue(hive, path, v.Name, RegValue.Deserialize(kind, v.Data));
            }
            foreach (var c in node.Children)
                Import(reg, hive, path + "\\" + c.Name, c);
        }
    }

    /// <summary>Sets one value, or deletes it when <see cref="Value"/> is null.</summary>
    public sealed class RegistryValueAction : TweakAction
    {
        public RegistryValueAction(Hive hive, string path, string name, RegValue value)
        {
            Hive = hive; Path = path; Name = name ?? ""; Value = value;
        }

        public Hive Hive { get; }
        public string Path { get; }
        public string Name { get; }
        public RegValue Value { get; }

        public override ActionState Detect(SystemContext ctx)
        {
            var current = ctx.Registry.GetValue(Hive, Path, Name);
            bool applied = Value == null ? current == null : Value.Equals(current);
            if (applied) return ActionState.Applied;
            // Windows protects some policy keys even from administrators: never offer a change that cannot be made.
            return ctx.Registry.CanWrite(Hive, Path) ? ActionState.NotApplied : ActionState.NotApplicable;
        }

        public override ActionSnapshot Snapshot(SystemContext ctx)
        {
            var current = ctx.Registry.GetValue(Hive, Path, Name);
            return new ActionSnapshot
            {
                Kind = "reg",
                Existed = current != null,
                ValueKind = current?.Kind.ToString(),
                Data = current?.Serialize(),
                CreatedKey = Value == null ? null : RegPath.FirstMissing(ctx.Registry, Hive, Path),
            };
        }

        public override void Apply(SystemContext ctx)
        {
            if (Value == null) ctx.Registry.DeleteValue(Hive, Path, Name);
            else ctx.Registry.SetValue(Hive, Path, Name, Value);
        }

        public override void Revert(SystemContext ctx, ActionSnapshot s)
        {
            if (s.Existed)
            {
                var kind = (RegKind)Enum.Parse(typeof(RegKind), s.ValueKind);
                ctx.Registry.SetValue(Hive, Path, Name, RegValue.Deserialize(kind, s.Data));
            }
            else
            {
                // Remove only the value this action wrote; other tweaks may share the key.
                ctx.Registry.DeleteValue(Hive, Path, Name);
                RegPath.RemoveEmptyKeys(ctx.Registry, Hive, Path, s.CreatedKey);
            }
        }

        public override string Describe(Lang lang)
        {
            var target = RegPath.Full(Hive, Path) + "\\" + (Name.Length == 0 ? "(" + (lang == Lang.En ? "Default" : "Predeterminado") + ")" : Name);
            if (Value == null) return (lang == Lang.En ? "Delete " : "Borrar ") + target;
            return target + " = " + Value.Display();
        }
    }

    /// <summary>Deletes a whole key. The subtree is exported first so undo restores it exactly.</summary>
    public sealed class RegistryDeleteKeyAction : TweakAction
    {
        public RegistryDeleteKeyAction(Hive hive, string path) { Hive = hive; Path = path; }
        public Hive Hive { get; }
        public string Path { get; }

        public override ActionState Detect(SystemContext ctx) =>
            ctx.Registry.KeyExists(Hive, Path) ? ActionState.NotApplied : ActionState.Applied;

        public override ActionSnapshot Snapshot(SystemContext ctx)
        {
            bool exists = ctx.Registry.KeyExists(Hive, Path);
            return new ActionSnapshot
            {
                Kind = "regkey",
                Existed = exists,
                Tree = exists ? RegPath.Export(ctx.Registry, Hive, Path, LastSegment(Path)) : null,
            };
        }

        public override void Apply(SystemContext ctx)
        {
            if (ctx.Registry.KeyExists(Hive, Path)) ctx.Registry.DeleteKeyTree(Hive, Path);
        }

        public override void Revert(SystemContext ctx, ActionSnapshot s)
        {
            if (s.Existed && s.Tree != null) RegPath.Import(ctx.Registry, Hive, Path, s.Tree);
        }

        public override string Describe(Lang lang) =>
            (lang == Lang.En ? "Delete key " : "Borrar clave ") + RegPath.Full(Hive, Path);

        private static string LastSegment(string p)
        {
            int i = p.LastIndexOf('\\');
            return i < 0 ? p : p.Substring(i + 1);
        }
    }

    /// <summary>Creates an empty key (some shell switches work by key presence alone).</summary>
    public sealed class RegistryCreateKeyAction : TweakAction
    {
        public RegistryCreateKeyAction(Hive hive, string path) { Hive = hive; Path = path; }
        public Hive Hive { get; }
        public string Path { get; }

        public override ActionState Detect(SystemContext ctx) =>
            ctx.Registry.KeyExists(Hive, Path) ? ActionState.Applied : ActionState.NotApplied;

        public override ActionSnapshot Snapshot(SystemContext ctx) => new ActionSnapshot
        {
            Kind = "regnewkey",
            Existed = ctx.Registry.KeyExists(Hive, Path),
            CreatedKey = RegPath.FirstMissing(ctx.Registry, Hive, Path),
        };

        public override void Apply(SystemContext ctx) => ctx.Registry.CreateKey(Hive, Path);

        public override void Revert(SystemContext ctx, ActionSnapshot s)
        {
            if (!s.Existed) RegPath.RemoveEmptyKeys(ctx.Registry, Hive, Path, s.CreatedKey);
        }

        public override string Describe(Lang lang) =>
            (lang == Lang.En ? "Create key " : "Crear clave ") + RegPath.Full(Hive, Path);
    }
}
