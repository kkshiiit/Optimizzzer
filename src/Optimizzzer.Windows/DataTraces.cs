using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    public sealed class TraceItem
    {
        public Text Name { get; set; }
        public Text Why { get; set; }
        public long Bytes { get; set; }
        /// <summary>Needed to undo changes: deleting it loses the way back.</summary>
        public bool NeededToUndo { get; set; }
    }

    /// <summary>
    /// Everything Optimizzzer leaves on the PC lives in ProgramData\Optimizzzer, plus the optional "measure after
    /// restart" task. This lists it and removes it on request. Nothing is written to %TEMP% outside the self-test.
    /// </summary>
    public static class DataTraces
    {
        private static long Size(string path)
        {
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (Directory.Exists(path)) return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => { try { return new FileInfo(f).Length; } catch (Exception) { return 0L; } });
            }
            catch (Exception) { }
            return 0;
        }

        /// <summary>True when the journal still holds changes that can be undone, or a DNS backup is waiting.</summary>
        public static bool HasUndo(WindowsSystem win)
        {
            try
            {
                if (File.Exists(Path.Combine(win.DataDir, "dns-backup.json"))) return true;
                if (File.Exists(Path.Combine(win.DataDir, "gamemode.txt"))) return true;
                return win.Journal.LoadAll().Any(s => s.Entries.Any(e => e.ResultKind == EntryResult.Applied && !e.Reverted));
            }
            catch (Exception) { return true; } // when unsure, keep the data
        }

        public static IList<TraceItem> Inventory(WindowsSystem win)
        {
            var d = win.DataDir;
            var list = new List<TraceItem>
            {
                new TraceItem { Name = new Text("Historial para deshacer", "Undo history"), Why = new Text("Lo que cambiaste y su valor anterior. Sin él no se puede deshacer.", "What you changed and its previous value. Without it nothing can be undone."), Bytes = Size(Path.Combine(d, "journal")), NeededToUndo = true },
                new TraceItem { Name = new Text("Copia del DNS anterior", "Previous DNS backup"), Why = new Text("Para volver a tu DNS de antes.", "To go back to your previous DNS."), Bytes = Size(Path.Combine(d, "dns-backup.json")), NeededToUndo = true },
                new TraceItem { Name = new Text("Modo juego activo", "Active game mode"), Why = new Text("Lo que el modo juego cambió, para devolverlo.", "What game mode changed, to put it back."), Bytes = Size(Path.Combine(d, "gamemode.txt")), NeededToUndo = true },
                new TraceItem { Name = new Text("Copias .reg de restos borrados", "Backups of removed leftovers (.reg)"), Why = new Text("Claves que se borraron al limpiar restos de programas.", "Keys deleted when cleaning program leftovers."), Bytes = Size(Path.Combine(d, "backups")) },
                new TraceItem { Name = new Text("Mediciones", "Measurements"), Why = new Text("Se borran solas a los 7 días, salvo las exportadas a CSV.", "Deleted automatically after 7 days, except those exported to CSV."), Bytes = Size(Path.Combine(d, "measurements.json")) },
                new TraceItem { Name = new Text("Ajustes de la app", "App settings"), Why = new Text("Tema, idioma, tamaño de la ventana.", "Theme, language, window size."), Bytes = Size(Path.Combine(d, "settings.txt")) },
                new TraceItem { Name = new Text("Copia para medir tras reiniciar", "Copy for measuring after restart"), Why = new Text("Solo mientras hay una medición programada; se borra sola después.", "Only while a measurement is scheduled; removed afterwards."), Bytes = Size(Path.Combine(d, "bin")) },
                new TraceItem { Name = new Text("Informes de errores", "Crash reports"), Why = new Text("Solo si la app falló alguna vez.", "Only if the app ever crashed."), Bytes = Directory.Exists(d) ? Directory.EnumerateFiles(d, "crash-*.txt").Sum(f => Size(f)) : 0 },
            };
            return list.Where(i => i.Bytes > 0).ToList();
        }

        /// <summary>On close, when nothing is left to undo: removes everything except the app settings.</summary>
        public static void CleanOnExit(WindowsSystem win)
        {
            if (HasUndo(win) || MeasureAfterRestart.IsScheduled(win.Process)) return;
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(win.DataDir))
                    if (!string.Equals(Path.GetFileName(dir), "bin", StringComparison.OrdinalIgnoreCase)) try { Directory.Delete(dir, true); } catch (Exception) { }
                foreach (var f in Directory.EnumerateFiles(win.DataDir))
                    if (!string.Equals(Path.GetFileName(f), "settings.txt", StringComparison.OrdinalIgnoreCase)) try { File.Delete(f); } catch (Exception) { }
            }
            catch (Exception) { }
        }

        /// <summary>Removes every trace: the scheduled task, its exe copy and the whole data folder.</summary>
        public static void DeleteAll(WindowsSystem win)
        {
            MeasureAfterRestart.Cancel(win.Process);
            try { if (Directory.Exists(win.DataDir)) Directory.Delete(win.DataDir, true); } catch (Exception) { }
        }
    }
}
