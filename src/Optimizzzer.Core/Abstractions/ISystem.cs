using System;
using System.Collections.Generic;
using System.Threading;

namespace Optimizzzer.Core
{
    public enum Hive { LocalMachine, CurrentUser, ClassesRoot, Users }

    public enum RegKind { DWord, QWord, String, ExpandString, MultiString, Binary }

    public interface IRegistry
    {
        /// <summary>Returns null when the value (or its key) does not exist.</summary>
        RegValue GetValue(Hive hive, string path, string name);
        void SetValue(Hive hive, string path, string name, RegValue value);
        void DeleteValue(Hive hive, string path, string name);
        bool KeyExists(Hive hive, string path);
        void CreateKey(Hive hive, string path);
        void DeleteKeyTree(Hive hive, string path);
        IList<string> GetValueNames(Hive hive, string path);
        IList<string> GetSubKeyNames(Hive hive, string path);
        /// <summary>
        /// False when the key (or, if it does not exist yet, its nearest existing parent) refuses writes even to an
        /// administrator, as Windows does for some protected policy keys.
        /// </summary>
        bool CanWrite(Hive hive, string path);
    }

    public enum StartMode { Boot, System, Automatic, AutomaticDelayed, Manual, Disabled }

    public interface IServiceManager
    {
        bool Exists(string name);
        StartMode? GetStartMode(string name);
        void SetStartMode(string name, StartMode mode);
        /// <summary>Best effort, never throws for a service that is already stopped.</summary>
        void Stop(string name);
        bool IsRunning(string name);
        /// <summary>Best effort, never throws for a service that is already running.</summary>
        void Start(string name);
    }

    public interface ITaskScheduler
    {
        /// <summary>Null when the task does not exist.</summary>
        bool? IsEnabled(string path);
        void SetEnabled(string path, bool enabled);
    }

    public sealed class ProcessResult
    {
        public ProcessResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode; Output = output ?? ""; Error = error ?? "";
        }
        public int ExitCode { get; }
        public string Output { get; }
        public string Error { get; }
        public bool Ok => ExitCode == 0;
    }

    public interface IProcessRunner
    {
        ProcessResult Run(string file, string arguments, int timeoutMs = 120000);
        /// <summary>Streams each output line. Returns the exit code.</summary>
        int RunStreaming(string file, string arguments, Action<string> onLine, CancellationToken ct);
    }

    public interface IAppxManager
    {
        bool IsInstalled(string packageName);
        /// <summary>Removes for all users and removes the provisioned copy so new users do not get it.</summary>
        void Remove(string packageName);
    }

    public interface IRestorePoint
    {
        /// <summary>Returns null on success or an error message.</summary>
        string Create(string description);
    }

    public interface ISystemInfo
    {
        int Build { get; }
        int Ubr { get; }
        bool IsWin11 { get; }
        string ProductName { get; }
        string DisplayVersion { get; }
        bool IsLaptop { get; }
        /// <summary>Null when it could not be detected.</summary>
        bool? SystemDriveIsSsd { get; }
        string CpuName { get; }
        int LogicalProcessors { get; }
        long TotalMemoryMb { get; }
        IList<string> Gpus { get; }
        bool HasNvidia { get; }
        bool HasAmdGpu { get; }
        bool IsVirtualMachine { get; }
    }

    /// <summary>Everything an action can touch. One instance per run.</summary>
    public sealed class SystemContext
    {
        public IRegistry Registry { get; set; }
        public IServiceManager Services { get; set; }
        public ITaskScheduler Tasks { get; set; }
        public IProcessRunner Process { get; set; }
        public IAppxManager Appx { get; set; }
        public IRestorePoint RestorePoint { get; set; }
        public ISystemInfo Info { get; set; }
    }
}
