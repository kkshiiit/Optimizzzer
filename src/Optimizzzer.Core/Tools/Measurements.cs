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
    }

    public interface IMeasurer
    {
        Measurement Take(string label);
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
