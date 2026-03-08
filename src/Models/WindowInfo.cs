using System.Drawing;

namespace WinAPIMCP.Models;

/// <summary>
/// Information about a Windows window
/// </summary>
public class WindowInfo
{
    /// <summary>
    /// Window handle (HWND)
    /// </summary>
    public IntPtr Handle { get; set; }

    /// <summary>
    /// Window title text
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Window class name
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Process ID that owns this window
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Whether the window is visible
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Whether the window is enabled for user interaction
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Window bounds (position and size)
    /// </summary>
    public Rectangle Bounds { get; set; }

    /// <summary>
    /// Current window state
    /// </summary>
    public WindowState State { get; set; }

    /// <summary>
    /// Parent window handle, if this is a child window
    /// </summary>
    public IntPtr ParentHandle { get; set; }

    /// <summary>
    /// Child windows of this window
    /// </summary>
    public IList<WindowInfo> ChildWindows { get; set; } = new List<WindowInfo>();

    /// <summary>
    /// Window style flags
    /// </summary>
    public uint Style { get; set; }

    /// <summary>
    /// Extended window style flags
    /// </summary>
    public uint ExtendedStyle { get; set; }
}

/// <summary>
/// Window state enumeration
/// </summary>
public enum WindowState
{
    Normal,
    Minimized,
    Maximized,
    Hidden
}

/// <summary>
/// Information about a control within a window
/// </summary>
public class ControlInfo
{
    /// <summary>
    /// Control handle (HWND)
    /// </summary>
    public IntPtr Handle { get; set; }

    /// <summary>
    /// Control ID (for dialog controls)
    /// </summary>
    public int ControlId { get; set; }

    /// <summary>
    /// Control text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Type of control (Button, Edit, Static, etc.)
    /// </summary>
    public string ControlType { get; set; } = string.Empty;

    /// <summary>
    /// Control bounds relative to parent window
    /// </summary>
    public Rectangle Bounds { get; set; }

    /// <summary>
    /// Whether the control is enabled
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Whether the control is visible
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Whether the control currently has focus
    /// </summary>
    public bool HasFocus { get; set; }
}