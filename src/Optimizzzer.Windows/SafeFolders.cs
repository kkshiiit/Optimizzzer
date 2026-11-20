using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Optimizzzer.Windows
{
    /// <summary>Small hardening steps for an elevated tool that people run from their Downloads folder.</summary>
    public static class SafeFolders
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint flags);

        /// <summary>DLLs load only from System32, so a malicious DLL dropped next to the exe is ignored.</summary>
        public static void RestrictDllSearch()
        {
            const uint LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
            try { SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32); } catch (Exception) { }
        }

        /// <summary>
        /// Creates the folder if needed and gives full control to Administrators and SYSTEM only (no inheritance),
        /// so a normal program cannot plant files that Optimizzzer later uses with admin rights.
        /// Only applied when running as administrator; otherwise the folder is left as it is.
        /// </summary>
        public static void LockToAdmins(string path)
        {
            try
            {
                if (!WindowsSystem.IsAdministrator()) return;
                Directory.CreateDirectory(path);
                var sec = new DirectorySecurity();
                sec.SetAccessRuleProtection(true, false);
                var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
                sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
                Directory.SetAccessControl(path, sec);
            }
            catch (Exception) { } // never block the app over this
        }
    }
}
