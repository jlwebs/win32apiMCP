using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using WinAPIMCP.Models;
using WinAPIMCP.Win32;

namespace WinAPIMCP.Services;

/// <summary>
/// Implementation of window management operations using Win32 APIs
/// </summary>
public class WindowManager : IWindowManager
{
    private readonly ILogger<WindowManager> _logger;
    private readonly ISecurityManager _securityManager;

    public WindowManager(ILogger<WindowManager> logger, ISecurityManager securityManager)
    {
        _logger = logger;
        _securityManager = securityManager;
    }

    public async Task<IEnumerable<WindowInfo>> EnumerateWindowsAsync(bool includeMinimized = false, string? titleFilter = null)
    {
        _logger.LogDebug("Enumerating windows (includeMinimized: {IncludeMinimized}, filter: {Filter})", 
                        includeMinimized, titleFilter);

        var windows = new List<WindowInfo>();
        Regex? titleRegex = null;
        
        if (!string.IsNullOrEmpty(titleFilter))
        {
            try
            {
                titleRegex = new Regex(titleFilter, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid regex pattern for title filter: {Pattern}. Error: {Error}", titleFilter, ex.Message);
                titleRegex = null;
            }
        }

        // Enumerate top-level windows
        User32.EnumWindows((hwnd, lParam) =>
        {
            try
            {
                // Check if window is visible (unless we want minimized windows)
                if (!includeMinimized && !User32.IsWindowVisible(hwnd))
                    return true;

                var windowInfo = CreateWindowInfo(hwnd);
                if (windowInfo != null)
                {
                    // Apply title filter if specified
                    if (titleRegex != null && !titleRegex.IsMatch(windowInfo.Title))
                        return true;

                    windows.Add(windowInfo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error processing window {Handle}: {Error}", hwnd, ex.Message);
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        _logger.LogDebug("Found {Count} windows", windows.Count);
        return await Task.FromResult(windows);
    }

    public async Task<IEnumerable<WindowInfo>> EnumerateChildWindowsAsync(IntPtr parentHandle, bool includeAllDescendants = true)
    {
        _logger.LogDebug("Enumerating child windows for parent {Parent}", parentHandle);

        if (parentHandle == IntPtr.Zero)
        {
            throw new ArgumentException("Parent handle cannot be zero", nameof(parentHandle));
        }

        await _securityManager.ValidateWindowAccessAsync(parentHandle);

        var children = new List<WindowInfo>();

        // First, collect all immediate children synchronously from the Win32 callback
        User32.EnumChildWindows(parentHandle, (hwnd, lParam) =>
        {
            try
            {
                // Only process windows that are direct children of the parentHandle if we're not doing flat recursion
                // Wait, EnumChildWindows is already recursive. But if we want WindowInfo hierarchy, 
                // we should only handle immediate children here.
                if (User32.GetParent(hwnd) == parentHandle)
                {
                    var childInfo = CreateWindowInfo(hwnd);
                    if (childInfo != null)
                    {
                        children.Add(childInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error processing child window {Handle}: {Error}", hwnd, ex.Message);
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        // Now, if descendants are requested, handle recursion asynchronously
        if (includeAllDescendants)
        {
            foreach (var child in children)
            {
                var descendants = await EnumerateChildWindowsAsync(child.Handle, true);
                child.ChildWindows = descendants.ToList();
            }
        }

        _logger.LogDebug("Found {Count} child windows", children.Count);
        return children;
    }

    public async Task<WindowInfo?> GetWindowInfoAsync(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return null;

        await _securityManager.ValidateWindowAccessAsync(handle);
        return await Task.FromResult(CreateWindowInfo(handle));
    }

    public async Task<string> GetWindowTitleAsync(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return string.Empty;

        await _securityManager.ValidateWindowAccessAsync(handle);
        return await Task.FromResult(GetWindowText(handle));
    }

    public async Task<string> GetWindowClassNameAsync(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return string.Empty;

        await _securityManager.ValidateWindowAccessAsync(handle);
        return await Task.FromResult(GetClassName(handle));
    }

    public async Task<bool> SetWindowFocusAsync(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return false;

        await _securityManager.ValidateWindowAccessAsync(handle);

        try
        {
            var result = User32.SetForegroundWindow(handle);
            _logger.LogDebug("Set focus to window {Handle}: {Success}", handle, result);
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set focus to window {Handle}", handle);
            return false;
        }
    }

    public async Task<bool> ShowWindowAsync(IntPtr handle, int showState)
    {
        if (handle == IntPtr.Zero)
            return false;

        await _securityManager.ValidateWindowAccessAsync(handle);

        try
        {
            var result = User32.ShowWindow(handle, showState);
            _logger.LogDebug("Show window {Handle} with state {State}: {Success}", handle, showState, result);
            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show window {Handle} with state {State}", handle, showState);
            return false;
        }
    }

    public async Task<IEnumerable<WindowInfo>> FindWindowsByTitleAsync(string titlePattern, bool exactMatch = false)
    {
        var windows = await EnumerateWindowsAsync(true);

        if (exactMatch)
        {
            return windows.Where(w => string.Equals(w.Title, titlePattern, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            try
            {
                var regex = new Regex(titlePattern, RegexOptions.IgnoreCase);
                return windows.Where(w => regex.IsMatch(w.Title));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid regex pattern: {Pattern}. Error: {Error}", titlePattern, ex.Message);
                return Enumerable.Empty<WindowInfo>();
            }
        }
    }

    public async Task<IEnumerable<WindowInfo>> FindWindowsByClassAsync(string className)
    {
        var windows = await EnumerateWindowsAsync(true);
        return windows.Where(w => string.Equals(w.ClassName, className, StringComparison.OrdinalIgnoreCase));
    }

    private WindowInfo? CreateWindowInfo(IntPtr handle)
    {
        try
        {
            if (!User32.IsWindow(handle))
                return null;

            var windowInfo = new WindowInfo
            {
                Handle = handle,
                Title = GetWindowText(handle),
                ClassName = GetClassName(handle),
                ProcessId = GetWindowProcessId(handle),
                IsVisible = User32.IsWindowVisible(handle),
                IsEnabled = User32.IsWindowEnabled(handle),
                Bounds = GetWindowBounds(handle),
                State = GetWindowState(handle),
                ParentHandle = User32.GetParent(handle),
                Style = (uint)User32.GetWindowLong(handle, User32.GWL_STYLE),
                ExtendedStyle = (uint)User32.GetWindowLong(handle, User32.GWL_EXSTYLE)
            };

            return windowInfo;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to create window info for handle {Handle}: {Error}", handle, ex.Message);
            return null;
        }
    }

    private string GetWindowText(IntPtr handle)
    {
        try
        {
            var length = User32.GetWindowTextLength(handle);
            if (length == 0)
                return string.Empty;

            var buffer = new char[length + 1];
            User32.GetWindowText(handle, buffer, buffer.Length);
            return new string(buffer).TrimEnd('\0');
        }
        catch
        {
            return string.Empty;
        }
    }

    private string GetClassName(IntPtr handle)
    {
        try
        {
            var buffer = new char[256];
            var length = User32.GetClassName(handle, buffer, buffer.Length);
            return length > 0 ? new string(buffer, 0, length) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private int GetWindowProcessId(IntPtr handle)
    {
        try
        {
            User32.GetWindowThreadProcessId(handle, out var processId);
            return (int)processId;
        }
        catch
        {
            return 0;
        }
    }

    private System.Drawing.Rectangle GetWindowBounds(IntPtr handle)
    {
        try
        {
            if (User32.GetWindowRect(handle, out var rect))
            {
                return new System.Drawing.Rectangle(
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top
                );
            }
        }
        catch
        {
            // Return empty rectangle on error
        }

        return System.Drawing.Rectangle.Empty;
    }

    private WindowState GetWindowState(IntPtr handle)
    {
        try
        {
            if (!User32.IsWindowVisible(handle))
                return WindowState.Hidden;

            if (User32.IsIconic(handle))
                return WindowState.Minimized;

            if (User32.IsZoomed(handle))
                return WindowState.Maximized;

            return WindowState.Normal;
        }
        catch
        {
            return WindowState.Normal;
        }
    }

    public async Task<bool> CloseWindowAsync(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return false;

        await _securityManager.ValidateWindowAccessAsync(handle);

        try
        {
            const uint WM_CLOSE = 0x0010;
            return await Task.FromResult(User32.PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WM_CLOSE to window {Handle}", handle);
            return false;
        }
    }
}