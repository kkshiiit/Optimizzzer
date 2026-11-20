using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Optimizzzer.Core.Tools
{
    /// <summary>Numbers read from this PC. A null field means it could not be measured, never a guess.</summary>
    [DataContract]
    public sealed class Measurement
    {
        [DataMember(Order = 1)] public string TakenUtc { get; set; }
        [DataMember(Order = 2)] public string Label { get; set; }
        /// <summary>Last boot duration from the Diagnostics-Performance event log (event 100).</summary>
        [DataMember(Order = 3)] public long? BootMs { get; set; }
        [DataMember(Order = 4)] public long? MemoryUsedMb { get; set; }
        [DataMember(Order = 5)] public long? MemoryTotalMb { get; set; }
        [DataMember(Order = 6)] public int? Processes { get; set; }
        [DataMember(Order = 7)] public int? RunningServices { get; set; }
        [DataMember(Order = 8)] public int? StartupItems { get; set; }
        [DataMember(Order = 9)] public long? SystemDriveFreeMb { get; set; }
        [DataMember(Order = 10)] public string LastBootUtc { get; set; }
        /// <summary>Average total CPU use over a short sample (a few seconds), in percent.</summary>
        [DataMember(Order = 11, EmitDefaultValue = false)] public double? CpuPercent { get; set; }
        /// <summary>Committed memory (RAM plus page file in use), the number that grows with background programs.</summary>
        [DataMember(Order = 12, EmitDefaultValue = false)] public long? CommittedMb { get; set; }
        [DataMember(Order = 13, EmitDefaultValue = false)] public int? Threads { get; set; }
        [DataMember(Order = 14, EmitDefaultValue = false)] public int? Handles { get; set; }
        /// <summary>Last shutdown duration from the Diagnostics-Performance event log (event 200).</summary>
        [DataMember(Order = 15, EmitDefaultValue = false)] public long? ShutdownMs { get; set; }
        [DataMember(Order = 16, EmitDefaultValue = false)] public long? TempFolderMb { get; set; }
        [DataMember(Order = 17, EmitDefaultValue = false)] public List<DriveSpace> Drives { get; set; }
        /// <summary>How long the sample lasted; CPU and memory are averages over it.</summary>
        [DataMember(Order = 18, EmitDefaultValue = false)] public int? Seconds { get; set; }
        [DataMember(Order = 19, EmitDefaultValue = false)] public double? CpuPeakPercent { get; set; }
        [DataMember(Order = 20, EmitDefaultValue = false)] public long? MemoryPeakMb { get; set; }
        /// <summary>Exported to CSV: kept forever instead of being cleaned after 7 days.</summary>
        [DataMember(Order = 21, EmitDefaultValue = false)] public bool Kept { get; set; }
    }

    [DataContract]
    public sealed class DriveSpace
    {
        [DataMember(Order = 1)] public string Name { get; set; }
        [DataMember(Order = 2)] public long FreeMb { get; set; }
        [DataMember(Order = 3)] public long TotalMb { get; set; }
    }

    public interface IMeasurer
    {
        Measurement Take(string label);
    }

    public static class MeasurementRetention
    {
        public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

        /// <summary>Measurements older than 7 days go, unless they were exported (Kept). Returns what stays.</summary>
        public static List<Measurement> Prune(IEnumerable<Measurement> all, DateTime nowUtc) =>
            all.Where(m => m.Kept || !DateTime.TryParse(m.TakenUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var t)
                || nowUtc - t.ToUniversalTime() <= MaxAge).ToList();
    }

    public sealed class MeasurementStore
    {
        private readonly string _path;
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(List<Measurement>));

        public MeasurementStore(string path) { _path = path; }

        public IList<Measurement> Load()
        {
            try
            {
                if (!File.Exists(_path)) return new List<Measurement>();
                using (var fs = File.OpenRead(_path)) return (List<Measurement>)Serializer.ReadObject(fs);
            }
            catch (System.Exception) { return new List<Measurement>(); }
        }

        /// <summary>Silently removes measurements older than 7 days that were never exported.</summary>
        public int PruneOld(DateTime nowUtc)
        {
            var all = Load().ToList();
            var keep = MeasurementRetention.Prune(all, nowUtc);
            if (keep.Count == all.Count) return 0;
            Save(keep);
            return all.Count - keep.Count;
        }

        /// <summary>Marks every measurement as exported, so the 7-day cleanup never removes them.</summary>
        public void MarkAllKept()
        {
            var all = Load().ToList();
            foreach (var m in all) m.Kept = true;
            Save(all);
        }

        private void Save(List<Measurement> all)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            using (var fs = File.Create(_path)) Serializer.WriteObject(fs, all);
        }

        public void Clear()
        {
            if (File.Exists(_path)) File.Delete(_path);
        }

        /// <summary>Keeps only the newest <paramref name="keep"/> measurements.</summary>
        public void KeepLatest(int keep)
        {
            var all = Load().ToList();
            if (all.Count <= keep) return;
            all = all.Skip(all.Count - keep).ToList();
            using (var fs = File.Create(_path)) Serializer.WriteObject(fs, all);
        }

        public void Add(Measurement m)
        {
            var all = Load().ToList();
            all.Add(m);
            // Keep the file small: the first baseline plus the latest 49.
            if (all.Count > 50) all = new[] { all[0] }.Concat(all.Skip(all.Count - 49)).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            using (var fs = File.Create(_path)) Serializer.WriteObject(fs, all);
        }
    }

    public sealed class MetricDelta
    {
        public Text Name { get; set; }
        public string Unit { get; set; }
        public double? Before { get; set; }
        public double? After { get; set; }
        /// <summary>True when a lower number is the better outcome.</summary>
        public bool LowerIsBetter { get; set; } = true;
        public double? Change => Before.HasValue && After.HasValue ? After - Before : null;
    }

    public static class MeasurementCompare
    {
        /// <summary>Every metric, in display order, with how to read it from a measurement.</summary>
        public static readonly IReadOnlyList<(Text Name, string Unit, bool LowerIsBetter, Func<Measurement, double?> Get)> Metrics = new (Text, string, bool, Func<Measurement, double?>)[]
        {
            (new Text("Duración del último arranque", "Last boot duration"), "s", true, m => m.BootMs / 1000.0),
            (new Text("Duración del último apagado", "Last shutdown duration"), "s", true, m => m.ShutdownMs / 1000.0),
            (new Text("Uso de CPU (promedio)", "CPU use (average)"), "%", true, m => m.CpuPercent),
            (new Text("Uso de CPU (pico)", "CPU use (peak)"), "%", true, m => m.CpuPeakPercent),
            (new Text("Memoria en uso (promedio)", "Memory in use (average)"), "MB", true, m => m.MemoryUsedMb),
            (new Text("Memoria en uso (pico)", "Memory in use (peak)"), "MB", true, m => m.MemoryPeakMb),
            (new Text("Memoria comprometida (RAM y archivo de paginación)", "Committed memory (RAM and page file)"), "MB", true, m => m.CommittedMb),
            (new Text("Procesos en ejecución", "Running processes"), "", true, m => m.Processes),
            (new Text("Hilos", "Threads"), "", true, m => m.Threads),
            (new Text("Identificadores abiertos (handles)", "Open handles"), "", true, m => m.Handles),
            (new Text("Servicios en ejecución", "Running services"), "", true, m => m.RunningServices),
            (new Text("Programas de inicio activos", "Enabled startup programs"), "", true, m => m.StartupItems),
            (new Text("Espacio libre en el disco del sistema", "Free space on the system drive"), "MB", false, m => m.SystemDriveFreeMb),
            (new Text("Tamaño de la carpeta temporal", "Temporary folder size"), "MB", true, m => m.TempFolderMb),
        };

        public static IList<MetricDelta> CompareAll(Measurement before, Measurement after) =>
            Metrics.Select(x => new MetricDelta
            {
                Name = x.Name, Unit = x.Unit, LowerIsBetter = x.LowerIsBetter,
                Before = before == null ? null : x.Get(before), After = after == null ? null : x.Get(after),
            }).ToList();

        /// <summary>One row per measurement, one column per metric, for spreadsheets. Invariant numbers, local time.</summary>
        public static string ToCsv(IEnumerable<Measurement> all, Lang lang)
        {
            var sb = new System.Text.StringBuilder();
            string Esc(string v) => v == null ? "" : v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
            sb.Append(lang == Lang.Es ? "Fecha,Etiqueta" : "Date,Label");
            foreach (var x in Metrics) sb.Append(',').Append(Esc(x.Name[lang] + (x.Unit.Length > 0 ? " (" + x.Unit + ")" : "")));
            sb.Append("\r\n");
            foreach (var m in all)
            {
                var when = DateTime.TryParse(m.TakenUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var d)
                    ? d.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) : m.TakenUtc;
                sb.Append(Esc(when)).Append(',').Append(Esc(m.Label));
                foreach (var x in Metrics)
                {
                    var v = x.Get(m);
                    sb.Append(',').Append(v.HasValue ? v.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "");
                }
                sb.Append("\r\n");
            }
            return sb.ToString();
        }

        public static IList<MetricDelta> Compare(Measurement before, Measurement after) => new List<MetricDelta>
        {
            new MetricDelta { Name = new Text("Duración del último arranque", "Last boot duration"), Unit = "s", Before = before?.BootMs / 1000.0, After = after?.BootMs / 1000.0 },
            new MetricDelta { Name = new Text("Memoria en uso", "Memory in use"), Unit = "MB", Before = before?.MemoryUsedMb, After = after?.MemoryUsedMb },
            new MetricDelta { Name = new Text("Procesos en ejecución", "Running processes"), Unit = "", Before = before?.Processes, After = after?.Processes },
            new MetricDelta { Name = new Text("Servicios en ejecución", "Running services"), Unit = "", Before = before?.RunningServices, After = after?.RunningServices },
            new MetricDelta { Name = new Text("Programas de inicio activos", "Enabled startup programs"), Unit = "", Before = before?.StartupItems, After = after?.StartupItems },
            new MetricDelta { Name = new Text("Espacio libre en el disco del sistema", "Free space on the system drive"), Unit = "MB", Before = before?.SystemDriveFreeMb, After = after?.SystemDriveFreeMb, LowerIsBetter = false },
        };
    }
}
