using WinAPIMCP.Models;

namespace WinAPIMCP.Services;

/// <summary>
/// Interface for window management operations
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// Enumerates all visible desktop windows
    /// </summary>
    /// <param name="includeMinimized">Whether to include minimized windows</param>
    /// <param name="titleFilter">Optional regex filter for window titles</param>
    /// <returns>Collection of window information</returns>
    Task<IEnumerable<WindowInfo>> EnumerateWindowsAsync(bool includeMinimized = false, string? titleFilter = null);

    /// <summary>
    /// Enumerates child windows of a parent window
    /// </summary>
    /// <param name="parentHandle">Handle of the parent window</param>
    /// <param name="includeAllDescendants">Whether to include all descendants or just direct children</param>
    /// <returns>Collection of child window information</returns>
    Task<IEnumerable<WindowInfo>> EnumerateChildWindowsAsync(IntPtr parentHandle, bool includeAllDescendants = true);

    /// <summary>
    /// Gets detailed information about a specific window
    /// </summary>
    /// <param name="handle">Window handle</param>
    /// <returns>Window information or null if not found</returns>
    Task<WindowInfo?> GetWindowInfoAsync(IntPtr handle);

    /// <summary>
    /// Gets the title of a window
    /// </summary>
    /// <param name="handle">Window handle</param>
    /// <returns>Window title</returns>
    Task<string> GetWindowTitleAsync(IntPtr handle);

    /// <summary>
    /// Gets the class name of a window
    /// </summary>
    /// <param name="handle">Window handle</param>
    /// <returns>Window class name</returns>
    Task<string> GetWindowClassNameAsync(IntPtr handle);

    /// <summary>
    /// Sets focus to a window
    /// </summary>
    /// <param name="handle">Window handle</param>
    /// <returns>True if successful</returns>
    Task<bool> SetWindowFocusAsync(IntPtr handle);

    /// <summary>
    /// Shows or hides a window
    /// </summary>
    /// <param name="handle">Window handle</param>
    /// <param name="showState">Show state (SW_SHOW, SW_HIDE, etc.)</param>
    /// <returns>True if successful</returns>
    Task<bool> ShowWindowAsync(IntPtr handle, int showState);

    /// <summary>
    /// Finds windows by title pattern
    /// </summary>
    /// <param name="titlePattern">Title pattern (regex)</param>
    /// <param name="exactMatch">Whether to match exactly or use pattern matching</param>
    /// <returns>Collection of matching windows</returns>
    Task<IEnumerable<WindowInfo>> FindWindowsByTitleAsync(string titlePattern, bool exactMatch = false);

    /// <summary>
    /// Finds windows by class name
    /// </summary>
    /// <param name="className">Window class name</param>
    /// <returns>Collection of matching windows</returns>
    Task<IEnumerable<WindowInfo>> FindWindowsByClassAsync(string className);

    /// <summary>
    /// Closes a window
    /// </summary>
    /// <param name="handle">Window handle</param>
    /// <returns>True if successful</returns>
    Task<bool> CloseWindowAsync(IntPtr handle);
}