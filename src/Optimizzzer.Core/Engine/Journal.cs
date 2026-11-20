using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Optimizzzer.Core
{
    [DataContract]
    public sealed class JournalSession
    {
        [DataMember(Order = 1)] public string Id { get; set; }
        [DataMember(Order = 2)] public string StartedUtc { get; set; }
        [DataMember(Order = 3)] public bool RestorePointCreated { get; set; }
        [DataMember(Order = 4, EmitDefaultValue = false)] public string RestorePointError { get; set; }
        [DataMember(Order = 5)] public List<JournalEntry> Entries { get; set; } = new List<JournalEntry>();

        public DateTime Started => DateTime.Parse(StartedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public enum EntryResult { Applied, Failed, Skipped }

    [DataContract]
    public sealed class JournalEntry
    {
        [DataMember(Order = 1)] public string TweakId { get; set; }
        [DataMember(Order = 2)] public string AppliedUtc { get; set; }
        [DataMember(Order = 3)] public string Result { get; set; }
        [DataMember(Order = 4, EmitDefaultValue = false)] public string Error { get; set; }
        [DataMember(Order = 5)] public List<ActionRecord> Actions { get; set; } = new List<ActionRecord>();
        [DataMember(Order = 6)] public bool Reverted { get; set; }
        [DataMember(Order = 7, EmitDefaultValue = false)] public string RevertedUtc { get; set; }

        public EntryResult ResultKind => (EntryResult)Enum.Parse(typeof(EntryResult), Result);
    }

    [DataContract]
    public sealed class ActionRecord
    {
        [DataMember(Order = 1)] public int Index { get; set; }
        /// <summary>English description at apply time. Undo checks it so a changed catalog never reverts the wrong thing.</summary>
        [DataMember(Order = 2)] public string Description { get; set; }
        [DataMember(Order = 3)] public ActionSnapshot Snapshot { get; set; }
    }

    public interface IJournalStore
    {
        void Save(JournalSession session);
        IList<JournalSession> LoadAll();
    }

    public sealed class FileJournalStore : IJournalStore
    {
        private readonly string _dir;
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(JournalSession));

        public FileJournalStore(string directory) { _dir = directory; }

        public void Save(JournalSession session)
        {
            Directory.CreateDirectory(_dir);
            var path = Path.Combine(_dir, "session-" + session.Id + ".json");
            var tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                Serializer.WriteObject(fs, session);
            // Replace swaps the files in one step, so a crash never leaves the session without a file on disk.
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        public IList<JournalSession> LoadAll()
        {
            var list = new List<JournalSession>();
            if (!Directory.Exists(_dir)) return list;
            // A leftover .tmp whose .json is missing means the process stopped between writing and moving: keep it.
            foreach (var tmp in Directory.GetFiles(_dir, "session-*.json.tmp"))
            {
                var target = tmp.Substring(0, tmp.Length - 4);
                try { if (!File.Exists(target)) File.Move(tmp, target); else File.Delete(tmp); } catch (Exception) { }
            }
            foreach (var f in Directory.GetFiles(_dir, "session-*.json"))
            {
                try
                {
                    using (var fs = File.OpenRead(f))
                        list.Add((JournalSession)Serializer.ReadObject(fs));
                }
                catch (Exception)
                {
                    // A damaged file must not hide the other sessions; it stays on disk for inspection.
                }
            }
            return list.OrderBy(s => s.StartedUtc, StringComparer.Ordinal).ToList();
        }

        public static string ToJson(JournalSession s)
        {
            using (var ms = new MemoryStream())
            {
                Serializer.WriteObject(ms, s);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }

    public sealed class MemoryJournalStore : IJournalStore
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
        private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(JournalSession));

        public void Save(JournalSession session) => _files[session.Id] = FileJournalStore.ToJson(session);

        public IList<JournalSession> LoadAll() => _files.Values
            .Select(j => { using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(j))) return (JournalSession)Serializer.ReadObject(ms); })
            .OrderBy(s => s.StartedUtc, StringComparer.Ordinal).ToList();
    }
}
