using WinAPIMCP.Models;

namespace WinAPIMCP.Services;

/// <summary>
/// Interface for process management operations
/// </summary>
public interface IProcessManager
{
    /// <summary>
    /// Enumerates all running processes
    /// </summary>
    /// <param name="includeSystemProcesses">Whether to include system processes</param>
    /// <returns>Collection of process information</returns>
    Task<IEnumerable<ProcessInfo>> EnumerateProcessesAsync(bool includeSystemProcesses = false);

    /// <summary>
    /// Gets detailed information about a specific process
    /// </summary>
    /// <param name="processId">Process ID</param>
    /// <returns>Process information or null if not found</returns>
    Task<ProcessInfo?> GetProcessInfoAsync(int processId);

    /// <summary>
    /// Gets the main window handle for a process
    /// </summary>
    /// <param name="processId">Process ID</param>
    /// <returns>Main window handle or IntPtr.Zero if no main window</returns>
    Task<IntPtr> GetProcessMainWindowAsync(int processId);

    /// <summary>
    /// Finds processes by name pattern
    /// </summary>
    /// <param name="namePattern">Process name pattern (regex)</param>
    /// <param name="exactMatch">Whether to match exactly or use pattern matching</param>
    /// <returns>Collection of matching processes</returns>
    Task<IEnumerable<ProcessInfo>> FindProcessesByNameAsync(string namePattern, bool exactMatch = false);
}