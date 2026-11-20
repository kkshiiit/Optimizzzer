using System;

namespace Optimizzzer.Core
{
    public sealed class ServiceStartAction : TweakAction
    {
        public ServiceStartAction(string service, StartMode mode) { Service = service; Mode = mode; }
        public string Service { get; }
        public StartMode Mode { get; }

        public override ActionState Detect(SystemContext ctx)
        {
            var current = ctx.Services.GetStartMode(Service);
            if (current == null) return ActionState.NotApplicable;
            return current.Value == Mode ? ActionState.Applied : ActionState.NotApplied;
        }

        public override ActionSnapshot Snapshot(SystemContext ctx)
        {
            var current = ctx.Services.GetStartMode(Service);
            return new ActionSnapshot
            {
                Kind = "svc", Existed = current != null, Data = current?.ToString(),
                WasRunning = current != null && ctx.Services.IsRunning(Service),
            };
        }

        public override void Apply(SystemContext ctx)
        {
            if (!ctx.Services.Exists(Service)) return;
            ctx.Services.SetStartMode(Service, Mode);
            if (Mode == StartMode.Disabled) ctx.Services.Stop(Service);
        }

        public override void Revert(SystemContext ctx, ActionSnapshot s)
        {
            if (!s.Existed || !ctx.Services.Exists(Service)) return;
            var mode = (StartMode)Enum.Parse(typeof(StartMode), s.Data);
            ctx.Services.SetStartMode(Service, mode);
            // Disabling stopped it; undo brings it back to work right away instead of after a reboot.
            if (s.WasRunning && mode != StartMode.Disabled) ctx.Services.Start(Service);
        }

        public override string Describe(Lang lang) =>
            (lang == Lang.En ? "Service " : "Servicio ") + Service + ": " + ModeName(Mode, lang);

        public static string ModeName(StartMode m, Lang lang)
        {
            switch (m)
            {
                case StartMode.Automatic: return lang == Lang.En ? "Automatic" : "Automático";
                case StartMode.AutomaticDelayed: return lang == Lang.En ? "Automatic (delayed)" : "Automático (retrasado)";
                case StartMode.Manual: return "Manual";
                case StartMode.Disabled: return lang == Lang.En ? "Disabled" : "Deshabilitado";
                default: return m.ToString();
            }
        }
    }

    public sealed class ScheduledTaskAction : TweakAction
    {
        public ScheduledTaskAction(string path, bool enabled) { TaskPath = path; Enabled = enabled; }
        public string TaskPath { get; }
        public bool Enabled { get; }

        public override ActionState Detect(SystemContext ctx)
        {
            var state = ctx.Tasks.IsEnabled(TaskPath);
            if (state == null) return ActionState.NotApplicable;
            return state.Value == Enabled ? ActionState.Applied : ActionState.NotApplied;
        }

        public override ActionSnapshot Snapshot(SystemContext ctx)
        {
            var state = ctx.Tasks.IsEnabled(TaskPath);
            return new ActionSnapshot { Kind = "task", Existed = state != null, Data = state?.ToString() };
        }

        public override void Apply(SystemContext ctx)
        {
            if (ctx.Tasks.IsEnabled(TaskPath) == null) return;
            ctx.Tasks.SetEnabled(TaskPath, Enabled);
        }

        public override void Revert(SystemContext ctx, ActionSnapshot s)
        {
            if (!s.Existed || ctx.Tasks.IsEnabled(TaskPath) == null) return;
            ctx.Tasks.SetEnabled(TaskPath, bool.Parse(s.Data));
        }

        public override string Describe(Lang lang) =>
            (lang == Lang.En ? (Enabled ? "Enable task " : "Disable task ") : (Enabled ? "Activar tarea " : "Desactivar tarea ")) + TaskPath;
    }

    /// <summary>Removes a preinstalled Store app. It can be reinstalled from the Microsoft Store, not by undo.</summary>
    public sealed class AppxRemoveAction : TweakAction
    {
        public AppxRemoveAction(string packageName) { PackageName = packageName; }
        public string PackageName { get; }
        public override bool Reversible => false;

        public override ActionState Detect(SystemContext ctx) =>
            ctx.Appx.IsInstalled(PackageName) ? ActionState.NotApplied : ActionState.Applied;

        public override ActionSnapshot Snapshot(SystemContext ctx) =>
            new ActionSnapshot { Kind = "appx", Existed = ctx.Appx.IsInstalled(PackageName), Data = PackageName };

        public override void Apply(SystemContext ctx) => ctx.Appx.Remove(PackageName);

        public override void Revert(SystemContext ctx, ActionSnapshot s) { }

        public override string Describe(Lang lang) =>
            (lang == Lang.En ? "Uninstall app " : "Desinstalar app ") + PackageName;
    }

    /// <summary>An action that only exists on Windows 10 or only on Windows 11; on the other one it does not apply.</summary>
    public sealed class OsOnlyAction : TweakAction
    {
        public OsOnlyAction(OsFilter os, TweakAction inner) { Os = os; Inner = inner; }
        public OsFilter Os { get; }
        public TweakAction Inner { get; }
        private bool Matches(SystemContext c) => c.Info == null || (Os & (c.Info.IsWin11 ? OsFilter.Win11 : OsFilter.Win10)) != 0;
        public override bool Reversible => Inner.Reversible;
        public override ActionState Detect(SystemContext ctx) => Matches(ctx) ? Inner.Detect(ctx) : ActionState.NotApplicable;
        public override ActionSnapshot Snapshot(SystemContext ctx) => Inner.Snapshot(ctx);
        public override void Apply(SystemContext ctx) => Inner.Apply(ctx);
        public override void Revert(SystemContext ctx, ActionSnapshot s) => Inner.Revert(ctx, s);
        // Same text as the inner action, so undo still matches journals written by v1.0.x.
        public override string Describe(Lang lang) => Inner.Describe(lang);
    }

    /// <summary>For changes that are not plain registry, service or task edits (powercfg, bcdedit, ...).</summary>
    public sealed class CustomAction : TweakAction
    {
        public Func<SystemContext, ActionState> DetectFn { get; set; }
        /// <summary>Returns whatever the revert step needs, as text.</summary>
        public Func<SystemContext, string> CaptureFn { get; set; }
        public Action<SystemContext> ApplyFn { get; set; }
        public Action<SystemContext, string> RevertFn { get; set; }
        public Text Description { get; set; } = Text.Empty;

        public override bool Reversible => RevertFn != null;
        public override ActionState Detect(SystemContext ctx) => DetectFn(ctx);
        public override ActionSnapshot Snapshot(SystemContext ctx) =>
            new ActionSnapshot { Kind = "custom", Existed = true, Data = CaptureFn?.Invoke(ctx) };
        public override void Apply(SystemContext ctx) => ApplyFn(ctx);
        public override void Revert(SystemContext ctx, ActionSnapshot s) => RevertFn?.Invoke(ctx, s.Data);
        public override string Describe(Lang lang) => Description[lang];
    }
}
