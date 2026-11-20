using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Optimizzzer.Core.Tools;

namespace Optimizzzer.App.Shell
{
    /// <summary>
    /// The real icon of each installed program, from its DisplayIcon ("file,index") or its main exe. Read in the
    /// background, cached for the session, frozen so any thread can use it. A generic icon shows meanwhile.
    /// </summary>
    public static class ProgramIcons
    {
        private static readonly ConcurrentDictionary<string, ImageSource> Cache = new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Task<ImageSource> GetAsync(InstalledProgram p) => Task.Run(() => Get(p));

        public static ImageSource Get(InstalledProgram p)
        {
            var (file, index) = Source(p);
            if (file == null) return null;
            var key = file + "," + index;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            ImageSource img = null;
            try
            {
                var large = new IntPtr[1];
                if (ExtractIconEx(file, index, large, null, 1) > 0 && large[0] != IntPtr.Zero)
                {
                    try
                    {
                        var bmp = Imaging.CreateBitmapSourceFromHIcon(large[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        bmp.Freeze();
                        img = bmp;
                    }
                    finally { DestroyIcon(large[0]); }
                }
            }
            catch (Exception) { }
            Cache[key] = img;
            return img;
        }

        /// <summary>DisplayIcon may be quoted and carry ",index"; .ico files and exes both work.</summary>
        private static (string File, int Index) Source(InstalledProgram p)
        {
            var raw = (p.DisplayIcon ?? "").Trim();
            int index = 0;
            if (raw.Length > 0)
            {
                var comma = raw.LastIndexOf(',');
                if (comma > 0 && int.TryParse(raw.Substring(comma + 1).Trim(), out var i)) { index = i; raw = raw.Substring(0, comma); }
                raw = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));
                if (File.Exists(raw)) return (raw, index);
            }
            // No usable DisplayIcon: the uninstaller usually sits in the install folder; the first exe there is a good guess.
            try
            {
                var dir = (p.InstallLocation ?? "").Trim().Trim('"');
                if (dir.Length > 0 && Directory.Exists(dir))
                    foreach (var exe in Directory.EnumerateFiles(dir, "*.exe"))
                    {
                        var n = Path.GetFileName(exe).ToLowerInvariant();
                        if (n.StartsWith("unins") || n.StartsWith("uninst") || n.Contains("update") || n.Contains("crash")) continue;
                        return (exe, 0);
                    }
            }
            catch (Exception) { }
            return (null, 0);
        }
    }
}
