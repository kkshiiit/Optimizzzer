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
        /// <summary>The longer "More information" text: what it is, why it piles up, whether it is safe, what happens after.</summary>
        public Text Details => CleanupDetails.For(Id);
    }

    public static class CleanupDetails
    {
        private static readonly Dictionary<string, Text> Map = new Dictionary<string, Text>
        {
            ["userTemp"] = new Text(
                "Qué es: archivos que los programas e instaladores crean para trabajar y deberían borrar al terminar. Por qué se acumula: muchos no lo hacen, sobre todo si se cierran mal. Es seguro: sí, se dejan los de las últimas 24 horas y nunca se fuerza un archivo en uso. Después: nada cambia; si un programa necesita uno, lo vuelve a crear.",
                "What it is: files programs and installers create while working and should delete afterwards. Why it piles up: many do not, especially after a crash. Safe: yes, files from the last 24 hours are kept and a file in use is never forced. Afterwards: nothing changes; a program that needs one creates it again."),
            ["winTemp"] = new Text(
                "Qué es: la carpeta temporal de Windows y de los servicios del sistema. Por qué se acumula: actualizaciones e instaladores dejan registros y descomprimidos. Es seguro: sí, con el mismo margen de 24 horas. Después: nada cambia.",
                "What it is: the temporary folder of Windows and system services. Why it piles up: updates and installers leave logs and unpacked files. Safe: yes, with the same 24-hour margin. Afterwards: nothing changes."),
            ["thumbs"] = new Text(
                "Qué es: las miniaturas que ves en las carpetas con fotos y vídeos. Por qué se acumula: guarda las de todo lo que abriste alguna vez, aunque ya no exista. Es seguro: sí. Después: la primera vez que abras una carpeta con muchas fotos tardará un poco en mostrar las miniaturas.",
                "What it is: the previews you see in folders with photos and videos. Why it piles up: it keeps one for everything you ever opened, even deleted files. Safe: yes. Afterwards: the first time you open a folder with many photos, previews take a moment to appear."),
            ["crashDumps"] = new Text(
                "Qué es: copias de la memoria guardadas cuando un programa o Windows fallan, y los informes de errores. Por qué se acumula: cada fallo deja uno y casi nunca se borran. Es seguro: sí, salvo que un técnico te haya pedido un volcado concreto. Después: nada cambia.",
                "What it is: memory copies saved when a program or Windows crashes, plus error reports. Why it piles up: each crash leaves one and they are rarely removed. Safe: yes, unless a technician asked you for a specific dump. Afterwards: nothing changes."),
            ["deliveryOpt"] = new Text(
                "Qué es: trozos de actualizaciones que Windows guarda para compartir con otros equipos de tu red. Es seguro: sí. Después: si otro PC de tu casa necesita la misma actualización, la descargará de internet en lugar de este equipo.",
                "What it is: pieces of updates Windows keeps to share with other PCs on your network. Safe: yes. Afterwards: if another PC at home needs the same update, it downloads it from the internet instead of from this PC."),
            ["dxCache"] = new Text(
                "Qué es: sombreadores ya compilados por la tarjeta gráfica para que los juegos carguen más rápido. Por qué está desmarcado: borrarla no mejora el rendimiento; solo sirve si un juego muestra fallos gráficos después de actualizar los controladores. Después: la primera partida de cada juego puede tener tirones mientras se regenera.",
                "What it is: shaders your graphics card already compiled so games load faster. Why it is unticked: deleting it does not improve performance; it only helps when a game shows graphics glitches after a driver update. Afterwards: the first session of each game may stutter while it is rebuilt."),
            ["wuCache"] = new Text(
                "Qué es: las descargas de actualizaciones que ya se instalaron. Por qué se acumula: Windows las conserva por si tiene que reintentar. Es seguro: sí; se detiene Windows Update mientras se borran y se vuelve a iniciar al terminar. Después: si una actualización falló, se descarga de nuevo.",
                "What it is: update downloads that are already installed. Why it piles up: Windows keeps them in case it has to retry. Safe: yes; Windows Update is stopped while they are deleted and started again afterwards. Afterwards: if an update had failed, it is downloaded again."),
            ["recycleBin"] = new Text(
                "Qué es: los archivos que borraste. Es seguro: solo si no necesitas recuperar nada; esto no se puede deshacer. Después: los archivos desaparecen del todo.",
                "What it is: files you deleted. Safe: only if you do not need anything back; this cannot be undone. Afterwards: the files are gone for good."),
            ["componentStore"] = new Text(
                "Qué es: versiones antiguas de partes de Windows que las actualizaciones sustituyeron. Usa la herramienta oficial DISM con StartComponentCleanup. Tarda: de 5 a 20 minutos. Después: no podrás desinstalar las actualizaciones ya instaladas, pero todo sigue funcionando igual.",
                "What it is: old versions of Windows parts replaced by updates. It uses the official DISM tool with StartComponentCleanup. Takes: 5 to 20 minutes. Afterwards: you cannot uninstall already installed updates, but everything works the same."),
            ["edgeCache"] = new Text(
                "Qué es: copias de webs, imágenes y scripts que Edge guarda para cargar más rápido. Por qué se acumula: crece hasta varios GB. Es seguro: sí; no se tocan contraseñas, historial, cookies ni sesiones. Cierra Edge antes: si está abierto, sus archivos en uso se saltan. Después: las webs que visitas a menudo tardan un poco más la primera vez.",
                "What it is: copies of pages, images and scripts Edge keeps to load faster. Why it piles up: it grows to several GB. Safe: yes; passwords, history, cookies and sessions are not touched. Close Edge first: if it is open, its files in use are skipped. Afterwards: sites you visit often load a little slower the first time."),
            ["chromeCache"] = new Text(
                "Qué es: la caché de Chrome (copias de webs). Es seguro: sí; no se tocan contraseñas, historial, cookies ni sesiones. Cierra Chrome antes. Después: las webs tardan un poco más la primera vez.",
                "What it is: Chrome's cache (copies of pages). Safe: yes; passwords, history, cookies and sessions are not touched. Close Chrome first. Afterwards: sites load a little slower the first time."),
            ["firefoxCache"] = new Text(
                "Qué es: la caché de Firefox (copias de webs). Es seguro: sí; no se tocan contraseñas, historial, cookies ni sesiones. Cierra Firefox antes. Después: las webs tardan un poco más la primera vez.",
                "What it is: Firefox's cache (copies of pages). Safe: yes; passwords, history, cookies and sessions are not touched. Close Firefox first. Afterwards: sites load a little slower the first time."),
        };

        public static Text For(string id) => id != null && Map.TryGetValue(id, out var t) ? t : null;
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
                Id = "edgeCache", Title = new Text("Caché de Microsoft Edge", "Microsoft Edge cache"),
                Description = new Text("Copias de webs que Edge guarda. No toca contraseñas, historial ni cookies. Cierra Edge antes.", "Page copies Edge keeps. Passwords, history and cookies are not touched. Close Edge first."),
                Folders = new[] { @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Cache\Cache_Data", @"%LOCALAPPDATA%\Microsoft\Edge\User Data\*\Code Cache" },
            },
            new CleanupTarget
            {
                Id = "chromeCache", Title = new Text("Caché de Google Chrome", "Google Chrome cache"),
                Description = new Text("Copias de webs que Chrome guarda. No toca contraseñas, historial ni cookies. Cierra Chrome antes.", "Page copies Chrome keeps. Passwords, history and cookies are not touched. Close Chrome first."),
                Folders = new[] { @"%LOCALAPPDATA%\Google\Chrome\User Data\*\Cache\Cache_Data", @"%LOCALAPPDATA%\Google\Chrome\User Data\*\Code Cache" },
            },
            new CleanupTarget
            {
                Id = "firefoxCache", Title = new Text("Caché de Mozilla Firefox", "Mozilla Firefox cache"),
                Description = new Text("Copias de webs que Firefox guarda. No toca contraseñas, historial ni cookies. Cierra Firefox antes.", "Page copies Firefox keeps. Passwords, history and cookies are not touched. Close Firefox first."),
                Folders = new[] { @"%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2" },
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

        /// <summary>
        /// Expands environment variables and "*" path segments (one per browser profile, for example) into the
        /// folders that exist. Only whole segments can be "*", so nothing outside the intended place matches.
        /// </summary>
        public static IEnumerable<string> ExpandFolders(string raw)
        {
            var path = Expand(raw);
            if (path.IndexOf('*') < 0) { yield return path; yield break; }
            var parts = path.Split('\\', '/');
            var current = new List<string> { parts[0].Length == 0 ? "/" : parts[0] + Path.DirectorySeparatorChar };
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                var next = new List<string>();
                foreach (var dir in current)
                {
                    if (parts[i] == "*")
                    {
                        string[] subs;
                        try { subs = Directory.GetDirectories(dir); } catch (Exception) { continue; }
                        next.AddRange(subs);
                    }
                    else next.Add(Path.Combine(dir, parts[i]));
                }
                current = next;
            }
            foreach (var d in current) if (Directory.Exists(d)) yield return d;
        }

        private IEnumerable<FileInfo> Files(CleanupTarget t)
        {
            var cutoff = _now() - t.MinAge;
            foreach (var folder in t.Folders.SelectMany(ExpandFolders))
            {
                var dir = new DirectoryInfo(folder);
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
            foreach (var folder in t.Folders.SelectMany(ExpandFolders))
                if (t.Recursive) RemoveEmptyDirs(new DirectoryInfo(folder), isRoot: true);
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
