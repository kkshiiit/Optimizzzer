using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Optimizzzer.Core;

namespace Optimizzzer.Windows
{
    public sealed class ScheduledTaskInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string Folder { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public bool Enabled { get; set; }
        public DateTime? LastRun { get; set; }
        public bool IsMicrosoft => Folder.StartsWith(@"\Microsoft\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Uses the Task Scheduler COM API (Schedule.Service), which ships with every Windows version.</summary>
    public sealed class WinTasks : ITaskScheduler
    {
        private static dynamic Connect()
        {
            var type = Type.GetTypeFromProgID("Schedule.Service");
            dynamic svc = Activator.CreateInstance(type);
            svc.Connect();
            return svc;
        }

        private static void Split(string path, out string folder, out string name)
        {
            int i = path.LastIndexOf('\\');
            folder = i <= 0 ? "\\" : path.Substring(0, i);
            name = path.Substring(i + 1);
        }

        private static dynamic Find(string path)
        {
            Split(path, out var folder, out var name);
            try
            {
                dynamic svc = Connect();
                dynamic f = svc.GetFolder(folder);
                return f.GetTask(name);
            }
            catch (COMException) { return null; }
            catch (System.IO.FileNotFoundException) { return null; }
        }

        public bool? IsEnabled(string path)
        {
            var task = Find(path);
            if (task == null) return null;
            return (bool)task.Enabled;
        }

        public void SetEnabled(string path, bool enabled)
        {
            var task = Find(path);
            if (task == null) return;
            task.Enabled = enabled;
        }

        public IList<ScheduledTaskInfo> List()
        {
            var list = new List<ScheduledTaskInfo>();
            dynamic svc = Connect();
            Walk(svc.GetFolder("\\"), list);
            list.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static void Walk(dynamic folder, List<ScheduledTaskInfo> list)
        {
            try
            {
                // 1 = TASK_ENUM_HIDDEN, so hidden Microsoft tasks are listed too.
                foreach (dynamic t in folder.GetTasks(1))
                {
                    try
                    {
                        string desc = null, author = null;
                        try { desc = t.Definition.RegistrationInfo.Description; author = t.Definition.RegistrationInfo.Author; } catch (Exception) { }
                        DateTime last = t.LastRunTime;
                        list.Add(new ScheduledTaskInfo
                        {
                            Path = t.Path,
                            Name = t.Name,
                            Folder = folder.Path,
                            Description = Native.ResolveIndirect(desc),
                            Author = Native.ResolveIndirect(author),
                            Enabled = t.Enabled,
                            LastRun = last.Year < 2000 ? (DateTime?)null : last,
                        });
                    }
                    catch (Exception) { }
                }
                foreach (dynamic sub in folder.GetFolders(0)) Walk(sub, list);
            }
            catch (Exception) { }
        }
    }
}
