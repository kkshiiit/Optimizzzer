using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Optimizzzer.Core
{
    public sealed class ScanResult
    {
        public ScanResult(Tweak tweak, TweakStatus status, string error = null)
        {
            Tweak = tweak; Status = status; Error = error;
        }
        public Tweak Tweak { get; }
        public TweakStatus Status { get; }
        public string Error { get; }
    }

    public sealed class ApplyProgress
    {
        public int Done { get; set; }
        public int Total { get; set; }
        public Tweak Current { get; set; }
        public JournalEntry LastEntry { get; set; }
    }

    public sealed class ApplyOptions
    {
        public bool CreateRestorePoint { get; set; } = true;
        public string RestorePointName { get; set; } = "Optimizzzer";
    }

    public sealed class UndoResult
    {
        public List<string> Reverted { get; } = new List<string>();
        public List<string> Failed { get; } = new List<string>();
        /// <summary>Tweaks that cannot be undone by design (app removals). Not a failure.</summary>
        public List<string> NotReversible { get; } = new List<string>();
        public bool NothingFound => Reverted.Count == 0 && Failed.Count == 0 && NotReversible.Count == 0;
    }

    public sealed class TweakEngine
    {
        private readonly SystemContext _ctx;
        private readonly IJournalStore _store;
        private readonly Func<DateTime> _clock;

        public TweakEngine(SystemContext ctx, IJournalStore store, Func<DateTime> clock = null)
        {
            _ctx = ctx; _store = store; _clock = clock ?? (() => DateTime.UtcNow);
        }

        public TweakStatus Detect(Tweak t)
        {
            if (!t.IsApplicable(_ctx.Info)) return TweakStatus.NotApplicable;
            int applied = 0, notApplied = 0;
            foreach (var a in t.Actions)
            {
                var s = a.Detect(_ctx);
                if (s == ActionState.Applied) applied++;
                else if (s == ActionState.NotApplied) notApplied++;
            }
            if (applied == 0 && notApplied == 0) return TweakStatus.NotApplicable;
            if (notApplied == 0) return TweakStatus.Applied;
            return applied == 0 ? TweakStatus.NotApplied : TweakStatus.Partial;
        }

        public IList<ScanResult> Scan(IEnumerable<Tweak> tweaks, IProgress<int> progress = null, CancellationToken ct = default)
        {
            var results = new List<ScanResult>();
            int i = 0;
            foreach (var t in tweaks)
            {
                ct.ThrowIfCancellationRequested();
                try { results.Add(new ScanResult(t, Detect(t))); }
                catch (Exception ex) { results.Add(new ScanResult(t, TweakStatus.Unknown, ex.Message)); }
                progress?.Report(++i);
            }
            return results;
        }

        public JournalSession Apply(IList<Tweak> tweaks, ApplyOptions options, IProgress<ApplyProgress> progress = null, CancellationToken ct = default)
        {
            var session = new JournalSession
            {
                Id = _clock().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 4),
                StartedUtc = _clock().ToString("o", CultureInfo.InvariantCulture),
            };

            if (options.CreateRestorePoint && _ctx.RestorePoint != null)
            {
                string err;
                try { err = _ctx.RestorePoint.Create(options.RestorePointName); }
                catch (Exception ex) { err = ex.Message; }
                session.RestorePointCreated = err == null;
                session.RestorePointError = err;
            }
            _store.Save(session);

            for (int i = 0; i < tweaks.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var t = tweaks[i];
                progress?.Report(new ApplyProgress { Done = i, Total = tweaks.Count, Current = t });
                var entry = ApplyOne(t);
                if (entry.ResultKind == EntryResult.Applied) SupersedeManual(session, entry);
                session.Entries.Add(entry);
                _store.Save(session);
                progress?.Report(new ApplyProgress { Done = i + 1, Total = tweaks.Count, Current = t, LastEntry = entry });
            }
            return session;
        }

        private JournalEntry ApplyOne(Tweak t)
        {
            var entry = new JournalEntry { TweakId = t.Id, AppliedUtc = _clock().ToString("o", CultureInfo.InvariantCulture) };
            TweakStatus status;
            try { status = Detect(t); }
            catch (Exception ex)
            {
                entry.Result = EntryResult.Failed.ToString();
                entry.Error = ex.Message;
                return entry;
            }
            if (status == TweakStatus.Applied || status == TweakStatus.NotApplicable)
            {
                entry.Result = EntryResult.Skipped.ToString();
                return entry;
            }

            var done = new List<int>();
            try
            {
                for (int i = 0; i < t.Actions.Count; i++)
                {
                    var a = t.Actions[i];
                    var state = a.Detect(_ctx);
                    if (state != ActionState.NotApplied) continue;
                    var record = new ActionRecord { Index = i, Description = a.Describe(Lang.En), Snapshot = a.Snapshot(_ctx) };
                    entry.Actions.Add(record);
                    // Recorded before Apply: an action that changes something and then throws must be rolled back too.
                    done.Add(i);
                    a.Apply(_ctx);
                }
                entry.Result = EntryResult.Applied.ToString();
            }
            catch (Exception ex)
            {
                // Put back whatever part of this tweak already ran, so a failure never leaves it half applied.
                foreach (var rec in entry.Actions.Where(r => done.Contains(r.Index)).Reverse())
                {
                    try { t.Actions[rec.Index].Revert(_ctx, rec.Snapshot); } catch (Exception) { }
                }
                entry.Result = EntryResult.Failed.ToString();
                entry.Error = ex.Message;
                entry.Reverted = true;
                entry.RevertedUtc = _clock().ToString("o", CultureInfo.InvariantCulture);
            }
            return entry;
        }

        private static bool SameId(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>Puts a tweak back to how it was before Optimizzzer first changed it.</summary>
        public UndoResult Undo(string tweakId, IList<Tweak> catalog)
        {
            var result = new UndoResult();
            var tweak = catalog.FirstOrDefault(t => SameId(t.Id, tweakId));
            var sessions = _store.LoadAll();
            var open = sessions
                .SelectMany(s => s.Entries.Select(e => new { s, e }))
                .Where(x => SameId(x.e.TweakId, tweakId) && !x.e.Reverted && x.e.ResultKind == EntryResult.Applied)
                .ToList();
            if (open.Count == 0 || tweak == null) return result;
            if (!tweak.Reversible) { result.NotReversible.Add(tweak.Id); return result; }

            // Later entries exist when the tweak was applied again after Windows reverted part of it, and they can
            // hold actions the first entry never touched. Revert the union, using the oldest snapshot of each action.
            var records = new Dictionary<int, ActionRecord>();
            foreach (var x in open)
                foreach (var rec in x.e.Actions)
                    if (!records.ContainsKey(rec.Index)) records[rec.Index] = rec;
            bool ok = RevertRecords(tweak, records.Values.OrderBy(r => r.Index));
            // A failed undo stays open so it can be retried from History.
            if (ok)
            {
                var now = _clock().ToString("o", CultureInfo.InvariantCulture);
                foreach (var x in open) { x.e.Reverted = true; x.e.RevertedUtc = now; }
                foreach (var s in open.Select(x => x.s).Distinct()) _store.Save(s);
            }
            (ok ? result.Reverted : result.Failed).Add(tweak.Id);
            return result;
        }

        public UndoResult UndoSession(string sessionId, IList<Tweak> catalog)
        {
            var result = new UndoResult();
            var session = _store.LoadAll().FirstOrDefault(s => SameId(s.Id, sessionId));
            if (session == null) return result;
            foreach (var e in session.Entries.AsEnumerable().Reverse())
            {
                if (e.Reverted || e.ResultKind != EntryResult.Applied) continue;
                var tweak = catalog.FirstOrDefault(t => SameId(t.Id, e.TweakId));
                if (tweak == null) { result.Failed.Add(e.TweakId); continue; }
                if (!tweak.Reversible) { result.NotReversible.Add(e.TweakId); continue; }
                bool ok = RevertRecords(tweak, e.Actions);
                if (ok)
                {
                    e.Reverted = true;
                    e.RevertedUtc = _clock().ToString("o", CultureInfo.InvariantCulture);
                }
                (ok ? result.Reverted : result.Failed).Add(e.TweakId);
            }
            _store.Save(session);
            return result;
        }

        private bool RevertRecords(Tweak tweak, IEnumerable<ActionRecord> records)
        {
            bool ok = true;
            foreach (var rec in records.Reverse())
            {
                if (rec.Index >= tweak.Actions.Count) { ok = false; continue; }
                var a = tweak.Actions[rec.Index];
                if (a.Describe(Lang.En) != rec.Description || !a.Reversible) { ok = false; continue; }
                try { a.Revert(_ctx, rec.Snapshot); }
                catch (Exception) { ok = false; }
            }
            return ok;
        }

        /// <summary>
        /// A manual change replaces any earlier manual change to the same service or task: the earlier entries are
        /// closed, and the new entry inherits the oldest snapshot, so undo returns to the state before the first one.
        /// Without this, Drift would report the earlier choice as "reverted by Windows".
        /// </summary>
        private void SupersedeManual(JournalSession current, JournalEntry entry)
        {
            var target = ManualTweaks.TargetOf(entry.TweakId);
            if (target == null) return;
            var now = _clock().ToString("o", CultureInfo.InvariantCulture);
            var sessions = _store.LoadAll().Where(s => s.Id != current.Id).Concat(new[] { current }).ToList();
            var older = sessions
                .SelectMany(s => s.Entries.Select(e => new { s, e }))
                .Where(x => !x.e.Reverted && x.e.ResultKind == EntryResult.Applied && x.e != entry && ManualTweaks.TargetOf(x.e.TweakId) == target)
                .OrderBy(x => x.e.AppliedUtc, StringComparer.Ordinal)
                .ToList();
            if (older.Count == 0) return;
            var original = older[0].e.Actions.FirstOrDefault();
            if (original != null && entry.Actions.Count > 0) entry.Actions[0].Snapshot = original.Snapshot;
            foreach (var x in older) { x.e.Reverted = true; x.e.RevertedUtc = now; }
            foreach (var s in older.Select(x => x.s).Distinct().Where(s => s != current)) _store.Save(s);
        }

        /// <summary>Tweaks Optimizzzer applied that Windows (usually an update) has since changed back.</summary>
        public IList<Tweak> FindDrift(IList<Tweak> catalog)
        {
            var applied = _store.LoadAll()
                .SelectMany(s => s.Entries)
                .Where(e => !e.Reverted && e.ResultKind == EntryResult.Applied)
                .Select(e => e.TweakId)
                .Distinct();
            var drifted = new List<Tweak>();
            foreach (var id in applied)
            {
                var t = catalog.FirstOrDefault(x => SameId(x.Id, id));
                if (t == null) continue;
                TweakStatus st;
                try { st = Detect(t); } catch (Exception) { continue; }
                if (st == TweakStatus.NotApplied || st == TweakStatus.Partial) drifted.Add(t);
            }
            return drifted;
        }

        /// <summary>Tweak ids currently applied by Optimizzzer and not undone, newest first.</summary>
        public IList<(JournalSession Session, JournalEntry Entry)> History() =>
            _store.LoadAll()
                .SelectMany(s => s.Entries.Select(e => (s, e)))
                .Where(x => x.e.ResultKind != EntryResult.Skipped)
                .OrderByDescending(x => x.e.AppliedUtc, StringComparer.Ordinal)
                .ToList();
    }
}
