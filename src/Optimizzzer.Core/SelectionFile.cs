using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Optimizzzer.Core
{
    /// <summary>A saved selection of tweak ids (.optz), to apply the same configuration on another PC.</summary>
    [DataContract]
    public sealed class SelectionFile
    {
        public const string Extension = ".optz";
        public const int CurrentVersion = 1;

        [DataMember(Order = 1)] public int Version { get; set; } = CurrentVersion;
        [DataMember(Order = 2)] public string CreatedUtc { get; set; }
        [DataMember(Order = 3)] public string App { get; set; } = "Optimizzzer";
        [DataMember(Order = 4)] public List<string> Tweaks { get; set; } = new List<string>();

        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(SelectionFile));

        public static string Write(IEnumerable<string> ids, DateTime nowUtc)
        {
            var f = new SelectionFile
            {
                CreatedUtc = nowUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                Tweaks = ids.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            };
            using (var ms = new MemoryStream())
            {
                Serializer.WriteObject(ms, f);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>Parses a file and splits its ids into known and unknown ones. Throws a readable error when it is not a selection file.</summary>
        public static (List<Tweak> Known, List<string> Unknown) Read(string json, IEnumerable<Tweak> catalog)
        {
            SelectionFile f;
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
                    f = (SelectionFile)Serializer.ReadObject(ms);
            }
            catch (Exception)
            {
                throw new InvalidDataException("This is not an Optimizzzer selection file.");
            }
            if (f == null || f.App != "Optimizzzer" || f.Tweaks == null) throw new InvalidDataException("This is not an Optimizzzer selection file.");
            if (f.Version > CurrentVersion) throw new InvalidDataException("This file was made by a newer version of Optimizzzer.");
            var byId = catalog.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
            var known = new List<Tweak>();
            var unknown = new List<string>();
            foreach (var id in f.Tweaks)
            {
                if (byId.TryGetValue(id, out var t)) known.Add(t); else unknown.Add(id);
            }
            return (known, unknown);
        }
    }
}
