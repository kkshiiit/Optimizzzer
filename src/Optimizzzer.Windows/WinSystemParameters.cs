using System;
using System.Runtime.InteropServices;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    /// <summary>SystemParametersInfo: the change applies at once and is saved to the user profile (SPIF_UPDATEINIFILE | SPIF_SENDCHANGE).</summary>
    public sealed class WinSystemParameters : ISystemParameters
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint uiParam, ref int pvParam, uint winIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo", SetLastError = true)]
        private static extern bool SystemParametersInfoPtr(uint action, uint uiParam, IntPtr pvParam, uint winIni);

        public int? Get(uint getAction)
        {
            int v = 0;
            return SystemParametersInfo(getAction, 0, ref v, 0) ? v : (int?)null;
        }

        public void Set(uint setAction, bool viaUiParam, int value)
        {
            const uint SPIF_UPDATEINIFILE = 0x01, SPIF_SENDCHANGE = 0x02;
            bool ok = viaUiParam
                ? SystemParametersInfoPtr(setAction, (uint)value, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE)
                : SystemParametersInfoPtr(setAction, 0, new IntPtr(value), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            if (!ok) throw new InvalidOperationException("SystemParametersInfo 0x" + setAction.ToString("X4") + " failed (error " + Marshal.GetLastWin32Error() + ")");
        }
    }
}
