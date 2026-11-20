using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Optimizzzer.Core.Tools
{
    public enum CleanupKind { Folders, RecycleBin, ComponentStore, WindowsUpdateCache }

    public sealed class CleanupTarget
    {
        public string Id { get; set; }
        public CleanupKind Kind { get; set; } = CleanupKind.Folders;
        public Text Title { get; set; }
        public Text Description { get; set; }
        public Risk Risk { get; set; } = Risk.Safe;
        /// <summary>Folders with environment variables, e.g. %TEMP%. Only their contents are deleted.</summary>
        public string[] Folders { get; set; } = new string[0];
        public string Pattern { get; set; } = "*";
        /// <summary>Files younger than this are left alone because a running program may still use them.</summary>
        public TimeSpan MinAge { get; set; } = TimeSpan.Zero;
        public bool Recursive { get; set; } = true;
        public bool SelectedByDefault { get; set; } = true;
    }

    public sealed class CleanupEstimate
    {
        public CleanupTarget Target { get; set; }
        public long Bytes { get; set; }
        public int Files { get; set; }
        /// <summary>False when the size cannot be known before running (for example DISM).</summary>
        public bool Measurable { get; set; } = true;
    }

    public sealed class CleanupResult
    {
        public CleanupTarget Target { get; set; }
        public long FreedBytes { get; set; }
        public int Deleted { get; set; }
        /// <summary>Files that were in use or protected. They are skipped, never forced.</summary>
        public int Skipped { get; set; }
        public string Error { get; set; }
    }

    public static class CleanupCatalog
    {
        public static readonly IReadOnlyList<CleanupTarget> All = new[]
        {
            new CleanupTarget
            {
                Id = "userTemp", Title = new Text("Archivos temporales de tu usuario", "Your user temporary files"),
                Description = new Text("Restos que dejan los programas e instaladores en %TEMP%. Se omiten los de las últimas 24 horas.", "Leftovers from programs and installers in %TEMP%. Files from the last 24 hours are kept."),
                Folders = new[] { "%TEMP%" }, MinAge = TimeSpan.FromHours(24),
            },
            new CleanupTarget
            {
                Id = "winTemp", Title = new Text("Archivos temporales de Windows", "Windows temporary files"),
                Description = new Text("Carpeta C:\\Windows\\Temp. Se omiten los de las últimas 24 horas.", "The C:\\Windows\\Temp folder. Files from the last 24 hours are kept."),
                Folders = new[] { @"%SystemRoot%\Temp" }, MinAge = TimeSpan.FromHours(24),
            },
            new CleanupTarget
            {
                Id = "thumbs", Title = new Text("Caché de miniaturas", "Thumbnail cache"),
                Description = new Text("Las vistas previas de imágenes se vuelven a generar al abrir cada carpeta.", "Image previews are rebuilt when you open each folder."),
                Folders = new[] { @"%LOCALAPPDATA%\Microsoft\Windows\Explorer" }, Pattern = "thumbcache_*.db", Recursive = false,
            },
            new CleanupTarget
            {
                Id = "crashDumps", Title = new Text("Volcados de memoria y errores", "Crash dumps and error reports"),
                Description = new Text("Informes de fallos ya enviados o que nadie va a revisar.", "Crash reports already sent or that nobody will review."),
                Folders = new[] { @"%LOCALAPPDATA%\CrashDumps", @"%ProgramData%\Microsoft\Windows\WER\ReportArchive", @"%ProgramData%\Microsoft\Windows\WER\ReportQueue", @"%SystemRoot%\Minidump" },
            },
            new CleanupTarget
            {
                Id = "deliveryOpt", Title = new Text("Caché de Optimización de distribución", "Delivery Optimization cache"),
                Description = new Text("Copias de actualizaciones guardadas para compartir con otros equipos.", "Copies of updates kept to share with other PCs."),
                Folders = new[] { @"%SystemRoot%\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache" },
            },
            new CleanupTarget
            {
                Id = "dxCache", Title = new Text("Caché de sombreadores de DirectX", "DirectX shader cache"),
                Description = new Text("Los juegos la regeneran; la primera partida puede tener algún tirón.", "Games rebuild it; the first session may stutter briefly."),
                Folders = new[] { @"%LOCALAPPDATA%\D3DSCache" }, SelectedByDefault = false,
            },
            new CleanupTarget
            {
                Id = "wuCache", Kind = CleanupKind.WindowsUpdateCache, Risk = Risk.Moderate,
                Title = new Text("Descargas de Windows Update", "Windows Update downloads"),
                Description = new Text("Paquetes ya instalados en SoftwareDistribution\\Download. Se detiene Windows Update mientras se borran.", "Already installed packages in SoftwareDistribution\\Download. Windows Update is stopped while they are deleted."),
                Folders = new[] { @"%SystemRoot%\SoftwareDistribution\Download" }, SelectedByDefault = false,
            },
            new CleanupTarget
            {
                Id = "recycleBin", Kind = CleanupKind.RecycleBin,
                Title = new Text("Papelera de reciclaje", "Recycle Bin"),
                Description = new Text("Vacía la papelera de todas las unidades. No se puede deshacer.", "Empties the Recycle Bin on every drive. This cannot be undone."),
                SelectedByDefault = false,
            },
            new CleanupTarget
            {
                Id = "componentStore", Kind = CleanupKind.ComponentStore, Risk = Risk.Moderate,
                Title = new Text("Componentes antiguos de Windows (DISM)", "Old Windows components (DISM)"),
                Description = new Text("Elimina versiones sustituidas por actualizaciones. Tarda varios minutos y luego no podrás desinstalar esas actualizaciones.", "Removes versions superseded by updates. Takes several minutes, and afterwards you cannot uninstall those updates."),
                SelectedByDefault = false,
            },
        };
    }

    /// <summary>Measures and deletes folder contents. Special kinds are delegated to <see cref="ICleanupSpecial"/>.</summary>
    public sealed class CleanupEngine
    {
        private readonly Func<DateTime> _now;
        public CleanupEngine(Func<DateTime> now = null) { _now = now ?? (() => DateTime.Now); }

        public static string Expand(string folder) => Environment.ExpandEnvironmentVariables(folder);

        private IEnumerable<FileInfo> Files(CleanupTarget t)
        {
            var cutoff = _now() - t.MinAge;
            foreach (var raw in t.Folders)
            {
                var dir = new DirectoryInfo(Expand(raw));
                if (!dir.Exists) continue;
                foreach (var f in SafeEnumerate(dir, t.Pattern, t.Recursive))
                {
                    DateTime written;
                    try { written = f.LastWriteTime; } catch (Exception) { continue; }
                    if (t.MinAge > TimeSpan.Zero && written > cutoff) continue;
                    yield return f;
                }
            }
        }

        /// <summary>Walks folders without following junctions and without failing on one unreadable folder.</summary>
        private static IEnumerable<FileInfo> SafeEnumerate(DirectoryInfo root, string pattern, bool recursive)
        {
            var stack = new Stack<DirectoryInfo>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var d = stack.Pop();
                FileInfo[] files;
                try { files = d.GetFiles(pattern); } catch (Exception) { continue; }
                foreach (var f in files) yield return f;
                if (!recursive) continue;
                DirectoryInfo[] subs;
                try { subs = d.GetDirectories(); } catch (Exception) { continue; }
                foreach (var s in subs)
                    if ((s.Attributes & FileAttributes.ReparsePoint) == 0) stack.Push(s);
            }
        }

        public CleanupEstimate Estimate(CleanupTarget t, ICleanupSpecial special, CancellationToken ct = default)
        {
            var e = new CleanupEstimate { Target = t };
            if (t.Kind == CleanupKind.RecycleBin)
            {
                var size = special?.RecycleBinSize();
                if (size == null) e.Measurable = false; else e.Bytes = size.Value;
                return e;
            }
            if (t.Kind == CleanupKind.ComponentStore) { e.Measurable = false; return e; }
            foreach (var f in Files(t))
            {
                ct.ThrowIfCancellationRequested();
                try { e.Bytes += f.Length; e.Files++; } catch (Exception) { }
            }
            return e;
        }

        public CleanupResult Run(CleanupTarget t, ICleanupSpecial special, CancellationToken ct = default)
        {
            var r = new CleanupResult { Target = t };
            try
            {
                switch (t.Kind)
                {
                    case CleanupKind.RecycleBin:
                        var before = special?.RecycleBinSize() ?? 0;
                        special?.EmptyRecycleBin();
                        // Measure again instead of trusting the call: a locked file or a policy can leave items behind.
                        var after = special?.RecycleBinSize() ?? 0;
                        r.FreedBytes = Math.Max(0, before - after);
                        if (after > 0) r.Error = "Recycle Bin could not be emptied completely";
                        return r;
                    case CleanupKind.ComponentStore:
                        special?.CleanComponentStore(ct);
                        return r;
                    case CleanupKind.WindowsUpdateCache:
                        special?.StopUpdateServices();
                        try { DeleteFiles(t, r, ct); } finally { special?.StartUpdateServices(); }
                        return r;
                    default:
                        DeleteFiles(t, r, ct);
                        return r;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { r.Error = ex.Message; return r; }
        }

        private void DeleteFiles(CleanupTarget t, CleanupResult r, CancellationToken ct)
        {
            foreach (var f in Files(t).ToList())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    long len = f.Length;
                    if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes &= ~FileAttributes.ReadOnly;
                    f.Delete();
                    r.FreedBytes += len; r.Deleted++;
                }
                catch (Exception) { r.Skipped++; }
            }
            foreach (var raw in t.Folders)
                if (t.Recursive) RemoveEmptyDirs(new DirectoryInfo(Expand(raw)), isRoot: true);
        }

        private static void RemoveEmptyDirs(DirectoryInfo d, bool isRoot)
        {
            if (!d.Exists || (d.Attributes & FileAttributes.ReparsePoint) != 0) return;
            DirectoryInfo[] subs;
            try { subs = d.GetDirectories(); } catch (Exception) { return; }
            foreach (var s in subs) RemoveEmptyDirs(s, false);
            if (isRoot) return;
            try { if (!d.EnumerateFileSystemInfos().Any()) d.Delete(); } catch (Exception) { }
        }
    }

    public interface ICleanupSpecial
    {
        long? RecycleBinSize();
        void EmptyRecycleBin();
        void CleanComponentStore(CancellationToken ct);
        void StopUpdateServices();
        void StartUpdateServices();
    }
}
