using System.Drawing;

namespace WinAPIMCP.Models;

/// <summary>
/// Represents a UI element found during automation
/// </summary>
public class UIElement
{
    /// <summary>
    /// Window handle of the element
    /// </summary>
    public IntPtr Handle { get; set; }

    /// <summary>
    /// Parent window handle
    /// </summary>
    public IntPtr ParentHandle { get; set; }

    /// <summary>
    /// Text content of the element
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Element type (Button, Edit, Static, etc.)
    /// </summary>
    public string ElementType { get; set; } = string.Empty;

    /// <summary>
    /// Element class name
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Element bounds (position and size)
    /// </summary>
    public Rectangle Bounds { get; set; }

    /// <summary>
    /// Whether the element is visible
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Whether the element is enabled for interaction
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Whether the element has focus
    /// </summary>
    public bool HasFocus { get; set; }

    /// <summary>
    /// Control ID (for dialog controls)
    /// </summary>
    public int ControlId { get; set; }

    /// <summary>
    /// Additional properties specific to the element type
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the center point of the element for clicking
    /// </summary>
    public Point CenterPoint => new Point(
        Bounds.X + Bounds.Width / 2,
        Bounds.Y + Bounds.Height / 2
    );

    /// <summary>
    /// Gets a description of the element for debugging
    /// </summary>
    public string Description => $"{ElementType} '{Text}' at {Bounds} (Handle: 0x{Handle.ToInt64():X8})";
}