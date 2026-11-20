using System;
using System.Globalization;

namespace Optimizzzer.Core
{
    /// <summary>
    /// A SystemParametersInfo setting (keyboard repeat, pointer speed, menu fades, window shadows…). Windows applies it
    /// at once and saves it in the user profile; undo writes back the exact previous value.
    /// </summary>
    public sealed class SpiAction : TweakAction
    {
        public SpiAction(string name, uint getAction, uint setAction, bool viaUiParam, int value)
        {
            Name = name; GetAction = getAction; SetAction = setAction; ViaUiParam = viaUiParam; Value = value;
        }

        public string Name { get; }
        public uint GetAction { get; }
        public uint SetAction { get; }
        public bool ViaUiParam { get; }
        public int Value { get; }

        public override ActionState Detect(SystemContext ctx)
        {
            var v = ctx.Spi?.Get(GetAction);
            if (v == null) return ActionState.NotApplicable;
            return v.Value == Value ? ActionState.Applied : ActionState.NotApplied;
        }

        public override ActionSnapshot Snapshot(SystemContext ctx) =>
            new ActionSnapshot { Kind = "spi", Existed = true, Data = ctx.Spi?.Get(GetAction)?.ToString(CultureInfo.InvariantCulture) };

        public override void Apply(SystemContext ctx)
        {
            if (ctx.Spi == null) throw new InvalidOperationException("SystemParametersInfo is not available");
            ctx.Spi.Set(SetAction, ViaUiParam, Value);
        }

        public override void Revert(SystemContext ctx, ActionSnapshot snapshot)
        {
            if (ctx.Spi == null || snapshot?.Data == null) return;
            if (int.TryParse(snapshot.Data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var old)) ctx.Spi.Set(SetAction, ViaUiParam, old);
        }

        public override string Describe(Lang lang) =>
            (lang == Lang.Es ? "Parámetro del sistema " : "System parameter ") + Name + " = " + Value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The SystemParametersInfo codes used by the catalog (from WinUser.h).</summary>
    public static class Spi
    {
        public const uint GetKeyboardSpeed = 0x000A, SetKeyboardSpeed = 0x000B;
        public const uint GetKeyboardDelay = 0x0016, SetKeyboardDelay = 0x0017;
        public const uint GetDragFullWindows = 0x0026, SetDragFullWindows = 0x0025;
        public const uint GetMouseHoverTime = 0x0066, SetMouseHoverTime = 0x0067;
        public const uint GetMouseSpeed = 0x0070, SetMouseSpeed = 0x0071;
        public const uint GetMenuAnimation = 0x1002, SetMenuAnimation = 0x1003;
        public const uint GetComboBoxAnimation = 0x1004, SetComboBoxAnimation = 0x1005;
        public const uint GetListBoxSmoothScrolling = 0x1006, SetListBoxSmoothScrolling = 0x1007;
        public const uint GetSelectionFade = 0x1014, SetSelectionFade = 0x1015;
        public const uint GetTooltipAnimation = 0x1016, SetTooltipAnimation = 0x1017;
        public const uint GetDropShadow = 0x1024, SetDropShadow = 0x1025;
        public const uint GetClientAreaAnimation = 0x1042, SetClientAreaAnimation = 0x1043;
    }
}
