using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.App.Shell
{
    /// <summary>
    /// Opens web pages, folders, settings pages and the browser WITHOUT administrator rights. Optimizzzer runs elevated;
    /// starting a browser from it directly would give that browser (and every site in it) administrator rights.
    /// Everything goes through the user's own desktop shell instead.
    /// </summary>
    public static class Launcher
    {
        /// <summary>A URL, a folder or an ms-settings: page, opened by the running Explorer (not elevated).</summary>
        public static void Open(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return;
            if (!ShellExecuteAsUser(target, null))
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + target + "\"") { UseShellExecute = false });
        }

        /// <summary>
        /// Searches the web with the person's default browser and that browser's own default search engine.
        /// Falls back to a search page opened through Explorer when the browser is not recognised.
        /// </summary>
        public static void SearchWeb(string what)
        {
            var query = WebSearch.Query(what, Loc.Lang == Optimizzzer.Core.Lang.Es);
            var browser = DefaultBrowserExe();
            var args = WebSearch.BrowserArgs(browser, query);
            if (browser != null && args != null && ShellExecuteAsUser(browser, args)) return;
            Open(WebSearch.FallbackUrl(query));
        }

        /// <summary>The program that opens https links for this user (from UserChoice and its ProgId's open command).</summary>
        public static string DefaultBrowserExe()
        {
            try
            {
                string progId;
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice"))
                    progId = k?.GetValue("ProgId") as string;
                if (string.IsNullOrEmpty(progId)) return null;
                using (var cmd = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command"))
                {
                    var exe = LeftoverScanner.ExecutableOf(cmd?.GetValue(null) as string);
                    return !string.IsNullOrEmpty(exe) && File.Exists(exe) ? exe : null;
                }
            }
            catch (Exception) { return null; }
        }

        // ---------- ShellExecute through the desktop (Explorer's own process, normal user rights) ----------

        [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider
        {
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object QueryService(ref Guid guidService, ref Guid riid);
        }

        [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellBrowser
        {
            void GetWindow(out IntPtr phwnd);
            void ContextSensitiveHelp(bool fEnterMode);
            void InsertMenusSB(IntPtr hmenuShared, IntPtr lpMenuWidths);
            void SetMenuSB(IntPtr hmenuShared, IntPtr holemenuRes, IntPtr hwndActiveObject);
            void RemoveMenusSB(IntPtr hmenuShared);
            void SetStatusTextSB(IntPtr pszStatusText);
            void EnableModelessSB(bool fEnable);
            void TranslateAcceleratorSB(IntPtr pmsg, ushort wID);
            void BrowseObject(IntPtr pidl, uint wFlags);
            void GetViewStateStream(uint grfMode, IntPtr ppStrm);
            void GetControlWindow(uint id, out IntPtr phwnd);
            void SendControlMsg(uint id, uint uMsg, IntPtr wParam, IntPtr lParam, IntPtr pret);
            IShellView QueryActiveShellView();
        }

        [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellView
        {
            void GetWindow(out IntPtr phwnd);
            void ContextSensitiveHelp(bool fEnterMode);
            void TranslateAccelerator(IntPtr pmsg);
            void EnableModeless(bool fEnable);
            void UIActivate(uint uState);
            void Refresh();
            void CreateViewWindow(IntPtr psvPrevious, IntPtr pfs, IntPtr psb, IntPtr prcView, out IntPtr phWnd);
            void DestroyViewWindow();
            void GetCurrentInfo(IntPtr pfs);
            void AddPropertySheetPages(uint dwReserved, IntPtr pfn, IntPtr lparam);
            void SaveViewState();
            void SelectItem(IntPtr pidlItem, uint uFlags);
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object GetItemObject(uint uItem, ref Guid riid);
        }

        /// <summary>Raymond Chen's technique: ask the desktop's shell view for its Shell.Application and run ShellExecute there.</summary>
        private static bool ShellExecuteAsUser(string file, string args)
        {
            try
            {
                dynamic shellWindows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
                object loc = 0, root = Type.Missing; // CSIDL_DESKTOP
                int hwnd;
                object desktop = shellWindows.FindWindowSW(ref loc, ref root, 8 /* SWC_DESKTOP */, out hwnd, 1 /* SWFO_NEEDDISPATCH */);
                if (desktop == null) return false;
                var sid = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"); // SID_STopLevelBrowser
                var iidBrowser = typeof(IShellBrowser).GUID;
                var browser = (IShellBrowser)((IServiceProvider)desktop).QueryService(ref sid, ref iidBrowser);
                var view = browser.QueryActiveShellView();
                var iidDispatch = new Guid("00020400-0000-0000-C000-000000000046");
                var folderView = view.GetItemObject(0 /* SVGIO_BACKGROUND */, ref iidDispatch);
                dynamic app = ((dynamic)folderView).Application;
                app.ShellExecute(file, args ?? "", "", "open", 1);
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
