using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WinAPIMCP.Win32;
using WinAPIMCP.Configuration;
using WinAPIMCP.Models;
using System.Diagnostics;
using System.Security.Principal;

namespace WinAPIMCP.Services;

/// <summary>
/// Implementation of security manager for validating access to windows and processes
/// </summary>
public class SecurityManager : ISecurityManager
{
    private readonly ILogger<SecurityManager> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly IPermissionService? _permissionService;
    private readonly IActivityTracker? _activityTracker;

    public SecurityManager(
        ILogger<SecurityManager> logger, 
        SettingsManager settingsManager,
        IPermissionService? permissionService = null,
        IActivityTracker? activityTracker = null)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _permissionService = permissionService;
        _activityTracker = activityTracker;
    }

    /// <summary>
    /// Validates that the current process has permission to access the specified window
    /// </summary>
    /// <param name="windowHandle">Handle of the window to validate access for</param>
    /// <returns>A task representing the asynchronous validation operation</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if access is not allowed</exception>
    public async Task ValidateWindowAccessAsync(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("Invalid window handle", nameof(windowHandle));
        }

        // Check if window exists
        if (!User32.IsWindow(windowHandle))
        {
            throw new UnauthorizedAccessException("Window does not exist or is no longer valid");
        }

        // Get the process ID that owns the window
        User32.GetWindowThreadProcessId(windowHandle, out var processId);
        
        // Check if permission is required
        if (_permissionService?.ShouldRequestPermission() == true)
        {
            var activityId = _activityTracker?.StartActivity(
                ActivityType.WindowControl, 
                "Window Access", 
                $"Handle: {windowHandle}, ProcessId: {processId}", 
                "SecurityManager");

            if (activityId.HasValue)
            {
                _activityTracker?.RequestPermission(activityId.Value);
            }

            try
            {
                var granted = await _permissionService.RequestPermissionAsync(
                    "Window Access Request",
                    $"Access window with handle {windowHandle} from process {processId}",
                    ActivityType.WindowControl
                );

                if (activityId.HasValue)
                {
                    _activityTracker?.RecordPermissionResponse(activityId.Value, granted);
                }

                if (!granted)
                {
                    throw new UnauthorizedAccessException("User denied permission to access window");
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting permission for window access");
                throw new UnauthorizedAccessException("Permission request failed");
            }
        }
        
        // Validate process access
        ValidateProcessAccess((int)processId);
    }

    public void ValidateProcessAccess(int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentException("Invalid process ID", nameof(processId));
        }

        try
        {
            // Check if the process exists
            using var process = Process.GetProcessById(processId);
            
            // If elevated access is not allowed, check if the target process is elevated
            if (!_settingsManager.GetSetting(s => s.AllowElevated) && IsProcessElevated(processId))
            {
                _logger.LogWarning("Access denied to elevated process {ProcessId} ({ProcessName})", 
                                 processId, process.ProcessName);
                throw new UnauthorizedAccessException($"Access to elevated process '{process.ProcessName}' is not allowed");
            }

            _logger.LogDebug("Access granted to process {ProcessId} ({ProcessName})", 
                           processId, process.ProcessName);
        }
        catch (ArgumentException)
        {
            throw new UnauthorizedAccessException($"Process with ID {processId} does not exist");
        }
        catch (InvalidOperationException)
        {
            throw new UnauthorizedAccessException($"Process with ID {processId} has exited");
        }
    }

    public bool IsElevatedAccessAllowed()
    {
        return _settingsManager.GetSetting(s => s.AllowElevated);
    }

    public bool IsProcessElevated(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            
            // Try to access the process token to determine if it's elevated
            // This is a simplified check - in practice you might want more sophisticated detection
            try
            {
                // Attempt to open the process with PROCESS_QUERY_INFORMATION
                // If this fails, it might be elevated (but could also be other reasons)
                var handle = process.Handle;
                
                // Check if current process is elevated
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                var isCurrentProcessElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
                
                // If current process is not elevated but we can access the target process,
                // then the target is likely not elevated
                // If current process is elevated, we need more sophisticated checks
                
                if (!isCurrentProcessElevated)
                {
                    // We're running non-elevated, so if we can access it, it's probably not elevated
                    return false;
                }
                
                // For elevated current process, we'd need to check the target process token
                // This is a simplified implementation
                return false;
            }
            catch (Exception)
            {
                // If we can't access the process, assume it might be elevated
                // This is a conservative approach
                return true;
            }
        }
        catch (ArgumentException)
        {
            // Process doesn't exist
            return false;
        }
        catch (InvalidOperationException)
        {
            // Process has exited
            return false;
        }
    }

    public bool IsWindowFromElevatedProcess(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !User32.IsWindow(windowHandle))
        {
            return false;
        }

        try
        {
            User32.GetWindowThreadProcessId(windowHandle, out var processId);
            return IsProcessElevated((int)processId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine elevation status for window {Handle}", windowHandle);
            return true; // Conservative approach - assume elevated if we can't determine
        }
    }
}