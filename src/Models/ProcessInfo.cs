namespace WinAPIMCP.Models;

/// <summary>
/// Information about a Windows process
/// </summary>
public class ProcessInfo
{
    /// <summary>
    /// Process ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Process name (executable name)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of application
    /// </summary>
    public ApplicationType Type { get; set; }

    /// <summary>
    /// Full path to the executable
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Command line arguments used to start the process
    /// </summary>
    public string CommandLine { get; set; } = string.Empty;

    /// <summary>
    /// Process start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Whether the process is running with elevated privileges
    /// </summary>
    public bool IsElevated { get; set; }

    /// <summary>
    /// Number of windows owned by this process
    /// </summary>
    public int WindowCount { get; set; }

    /// <summary>
    /// Window handles owned by this process
    /// </summary>
    public IList<IntPtr> WindowHandles { get; set; } = new List<IntPtr>();

    /// <summary>
    /// Process architecture (x86, x64, ARM64)
    /// </summary>
    public ProcessArchitecture Architecture { get; set; }

    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// CPU usage percentage
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Whether the process is responding to system messages
    /// </summary>
    public bool IsResponding { get; set; }

    /// <summary>
    /// Process priority class
    /// </summary>
    public ProcessPriorityClass PriorityClass { get; set; }
}

/// <summary>
/// Types of Windows applications
/// </summary>
public enum ApplicationType
{
    Unknown,
    Console,
    Win32,
    WinForms,
    WPF,
    UWP,
    DotNet,
    Service,
    Driver
}

/// <summary>
/// Process architecture types
/// </summary>
public enum ProcessArchitecture
{
    Unknown,
    X86,
    X64,
    ARM64
}

/// <summary>
/// Process priority classes
/// </summary>
public enum ProcessPriorityClass
{
    Idle,
    BelowNormal,
    Normal,
    AboveNormal,
    High,
    RealTime
}