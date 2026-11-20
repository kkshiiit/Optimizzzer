using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Optimizzzer.Core.Tools
{
    /// <summary>
    /// "What takes the most space": walks a drive once and ranks folders up to a few levels deep. It only reads;
    /// nothing is deleted. A folder is replaced by its child when that child holds most of it, so the list points
    /// at specific places (a game, a download folder) rather than at "Users".
    /// </summary>
    public static class FolderSizes
    {
        public static IList<(string Path, long Bytes)> Largest(string root, int depth, int top, CancellationToken ct = default, IProgress<string> progress = null)
        {
            var sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var rootInfo = new DirectoryInfo(root);
            var stack = new Stack<(DirectoryInfo Dir, int Level, List<string> Ancestors)>();
            stack.Push((rootInfo, 0, new List<string>()));
            int visited = 0;
            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var (dir, level, ancestors) = stack.Pop();
                long here = 0;
                try { foreach (var f in dir.EnumerateFiles()) { try { here += f.Length; } catch (Exception) { } } }
                catch (Exception) { }
                foreach (var a in ancestors) sizes[a] += here;
                if (++visited % 500 == 0) progress?.Report(dir.FullName);
                DirectoryInfo[] subs;
                try { subs = dir.GetDirectories(); } catch (Exception) { continue; }
                foreach (var s in subs)
                {
                    if ((s.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    var next = ancestors;
                    if (level < depth)
                    {
                        next = new List<string>(ancestors) { s.FullName };
                        sizes[s.FullName] = 0;
                    }
                    stack.Push((s, level + 1, next));
                }
            }
            // Prefer the specific folder when one child holds 70% or more of its parent.
            var ranked = sizes.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ToList();
            var result = new List<(string, long)>();
            foreach (var kv in ranked)
            {
                var prefix = kv.Key.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                bool dominatedByChild = sizes.Any(o => o.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && o.Value >= kv.Value * 0.7);
                if (dominatedByChild) continue;
                result.Add((kv.Key, kv.Value));
                if (result.Count >= top) break;
            }
            return result;
        }
    }
}
