using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;
using WinAPIMCP.Models;

namespace WinAPIMCP.Services;

/// <summary>
/// Implementation of process management operations
/// </summary>
public class ProcessManager : IProcessManager
{
    private readonly ILogger<ProcessManager> _logger;
    private readonly ISecurityManager _securityManager;

    public ProcessManager(ILogger<ProcessManager> logger, ISecurityManager securityManager)
    {
        _logger = logger;
        _securityManager = securityManager;
    }

    public async Task<IEnumerable<ProcessInfo>> EnumerateProcessesAsync(bool includeSystemProcesses = false)
    {
        _logger.LogDebug("Enumerating processes (includeSystemProcesses: {IncludeSystemProcesses})", includeSystemProcesses);

        var processes = new List<ProcessInfo>();
        var systemProcesses = Process.GetProcesses();

        foreach (var process in systemProcesses)
        {
            try
            {
                using (process)
                {
                    // Skip system processes if not requested
                    if (!includeSystemProcesses && IsSystemProcess(process))
                    {
                        continue;
                    }

                    var processInfo = await CreateProcessInfoAsync(process);
                    if (processInfo != null)
                    {
                        processes.Add(processInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error processing process {ProcessId}: {Error}", process.Id, ex.Message);
            }
        }

        _logger.LogDebug("Found {Count} processes", processes.Count);
        return processes;
    }

    public async Task<ProcessInfo?> GetProcessInfoAsync(int processId)
    {
        try
        {
            _securityManager.ValidateProcessAccess(processId);
            using var process = Process.GetProcessById(processId);
            return await CreateProcessInfoAsync(process);
        }
        catch (ArgumentException)
        {
            _logger.LogWarning("Process {ProcessId} not found", processId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process info for {ProcessId}", processId);
            return null;
        }
    }

    public async Task<IntPtr> GetProcessMainWindowAsync(int processId)
    {
        try
        {
            _securityManager.ValidateProcessAccess(processId);
            using var process = Process.GetProcessById(processId);
            return await Task.FromResult(process.MainWindowHandle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting main window for process {ProcessId}", processId);
            return IntPtr.Zero;
        }
    }

    public async Task<IEnumerable<ProcessInfo>> FindProcessesByNameAsync(string namePattern, bool exactMatch = false)
    {
        var processes = await EnumerateProcessesAsync(true);

        if (exactMatch)
        {
            return processes.Where(p => string.Equals(p.Name, namePattern, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            try
            {
                var regex = new Regex(namePattern, RegexOptions.IgnoreCase);
                return processes.Where(p => regex.IsMatch(p.Name));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid regex pattern: {Pattern}. Error: {Error}", namePattern, ex.Message);
                return Enumerable.Empty<ProcessInfo>();
            }
        }
    }

    private async Task<ProcessInfo?> CreateProcessInfoAsync(Process process)
    {
        try
        {
            var processInfo = new ProcessInfo
            {
                Id = process.Id,
                Name = process.ProcessName,
                StartTime = GetProcessStartTime(process),
                IsElevated = _securityManager.IsProcessElevated(process.Id),
                IsResponding = process.Responding,
                PriorityClass = MapPriorityClass(process.PriorityClass),
                MemoryUsage = GetProcessMemoryUsage(process),
                ExecutablePath = GetProcessExecutablePath(process),
                Architecture = GetProcessArchitecture(process),
                Type = GetApplicationType(process)
            };

            // Get window information
            processInfo.WindowHandles = GetProcessWindows(process).ToList();
            processInfo.WindowCount = processInfo.WindowHandles.Count;

            return await Task.FromResult(processInfo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to create process info for {ProcessId}: {Error}", process.Id, ex.Message);
            return null;
        }
    }

    private bool IsSystemProcess(Process process)
    {
        try
        {
            // Consider processes with session ID 0 as system processes
            if (process.SessionId == 0)
                return true;

            // Check for common system process names
            var systemProcessNames = new[]
            {
                "System", "Idle", "csrss", "winlogon", "services", "lsass",
                "svchost", "explorer", "dwm", "winlogon"
            };

            return systemProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private DateTime GetProcessStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private Models.ProcessPriorityClass MapPriorityClass(System.Diagnostics.ProcessPriorityClass priorityClass)
    {
        return priorityClass switch
        {
            System.Diagnostics.ProcessPriorityClass.Idle => Models.ProcessPriorityClass.Idle,
            System.Diagnostics.ProcessPriorityClass.BelowNormal => Models.ProcessPriorityClass.BelowNormal,
            System.Diagnostics.ProcessPriorityClass.Normal => Models.ProcessPriorityClass.Normal,
            System.Diagnostics.ProcessPriorityClass.AboveNormal => Models.ProcessPriorityClass.AboveNormal,
            System.Diagnostics.ProcessPriorityClass.High => Models.ProcessPriorityClass.High,
            System.Diagnostics.ProcessPriorityClass.RealTime => Models.ProcessPriorityClass.RealTime,
            _ => Models.ProcessPriorityClass.Normal
        };
    }

    private long GetProcessMemoryUsage(Process process)
    {
        try
        {
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    private string GetProcessExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private ProcessArchitecture GetProcessArchitecture(Process process)
    {
        // This is a simplified implementation
        // In practice, you would need to check the PE headers or use WinAPI calls
        return ProcessArchitecture.Unknown;
    }

    private ApplicationType GetApplicationType(Process process)
    {
        // This is a simplified implementation
        // In practice, you would need to analyze the executable or check for specific characteristics
        try
        {
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return ApplicationType.Win32; // Has a window, likely GUI app
            }
            return ApplicationType.Console; // No window, likely console app
        }
        catch
        {
            return ApplicationType.Unknown;
        }
    }

    private IEnumerable<IntPtr> GetProcessWindows(Process process)
    {
        var windows = new List<IntPtr>();
        
        // This is a placeholder - in practice you would enumerate windows
        // and filter by process ID
        if (process.MainWindowHandle != IntPtr.Zero)
        {
            windows.Add(process.MainWindowHandle);
        }

        return windows;
    }
}