using System.Drawing;
using WinAPIMCP.Models;

namespace WinAPIMCP.Services;

/// <summary>
/// Interface for UI interaction and automation operations
/// </summary>
public interface IUIInteractionManager
{
    /// <summary>
    /// Clicks at the specified screen coordinates
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="button">Mouse button to click (Left, Right, Middle)</param>
    /// <param name="clickCount">Number of clicks (1 for single, 2 for double)</param>
    /// <returns>True if successful</returns>
    Task<bool> ClickAtCoordinatesAsync(int x, int y, MouseButton button = MouseButton.Left, int clickCount = 1);

    /// <summary>
    /// Clicks on a specific window control
    /// </summary>
    /// <param name="windowHandle">Handle of the parent window</param>
    /// <param name="controlHandle">Handle of the control to click</param>
    /// <param name="button">Mouse button to click</param>
    /// <returns>True if successful</returns>
    Task<bool> ClickControlAsync(IntPtr windowHandle, IntPtr controlHandle, MouseButton button = MouseButton.Left);

    /// <summary>
    /// Sends text input to a window or control
    /// </summary>
    /// <param name="windowHandle">Handle of the target window</param>
    /// <param name="text">Text to send</param>
    /// <param name="controlHandle">Optional specific control handle</param>
    /// <returns>True if successful</returns>
    Task<bool> SendTextAsync(IntPtr windowHandle, string text, IntPtr? controlHandle = null);

    /// <summary>
    /// Sends keyboard input (key combinations, special keys)
    /// </summary>
    /// <param name="windowHandle">Handle of the target window</param>
    /// <param name="keys">Key combination string (e.g., "Ctrl+C", "Alt+F4", "Enter")</param>
    /// <param name="controlHandle">Optional specific control handle</param>
    /// <returns>True if successful</returns>
    Task<bool> SendKeysAsync(IntPtr windowHandle, string keys, IntPtr? controlHandle = null);

    /// <summary>
    /// Gets text content from a control
    /// </summary>
    /// <param name="windowHandle">Handle of the parent window</param>
    /// <param name="controlHandle">Handle of the control</param>
    /// <returns>Text content or empty string if not available</returns>
    Task<string> GetControlTextAsync(IntPtr windowHandle, IntPtr controlHandle);

    /// <summary>
    /// Sets text content in a control (clears and replaces)
    /// </summary>
    /// <param name="windowHandle">Handle of the parent window</param>
    /// <param name="controlHandle">Handle of the control</param>
    /// <param name="text">New text content</param>
    /// <returns>True if successful</returns>
    Task<bool> SetControlTextAsync(IntPtr windowHandle, IntPtr controlHandle, string text);

    /// <summary>
    /// Selects text in a control
    /// </summary>
    /// <param name="windowHandle">Handle of the parent window</param>
    /// <param name="controlHandle">Handle of the control</param>
    /// <param name="startIndex">Start position of selection</param>
    /// <param name="length">Length of selection (-1 for all)</param>
    /// <returns>True if successful</returns>
    Task<bool> SelectTextAsync(IntPtr windowHandle, IntPtr controlHandle, int startIndex = 0, int length = -1);

    /// <summary>
    /// Right-clicks to open context menu
    /// </summary>
    /// <param name="windowHandle">Handle of the target window</param>
    /// <param name="x">X coordinate relative to window</param>
    /// <param name="y">Y coordinate relative to window</param>
    /// <returns>True if successful</returns>
    Task<bool> OpenContextMenuAsync(IntPtr windowHandle, int x, int y);

    /// <summary>
    /// Drags from one point to another
    /// </summary>
    /// <param name="startX">Start X coordinate</param>
    /// <param name="startY">Start Y coordinate</param>
    /// <param name="endX">End X coordinate</param>
    /// <param name="endY">End Y coordinate</param>
    /// <param name="button">Mouse button to use for drag</param>
    /// <returns>True if successful</returns>
    Task<bool> DragAsync(int startX, int startY, int endX, int endY, MouseButton button = MouseButton.Left);

    /// <summary>
    /// Scrolls in a window or control
    /// </summary>
    /// <param name="windowHandle">Handle of the target window</param>
    /// <param name="direction">Scroll direction</param>
    /// <param name="amount">Number of scroll steps</param>
    /// <param name="controlHandle">Optional specific control handle</param>
    /// <returns>True if successful</returns>
    Task<bool> ScrollAsync(IntPtr windowHandle, ScrollDirection direction, int amount = 3, IntPtr? controlHandle = null);

    /// <summary>
    /// Takes a screenshot of a window or the entire screen
    /// </summary>
    /// <param name="windowHandle">Window handle (IntPtr.Zero for full screen)</param>
    /// <returns>Screenshot as byte array (PNG format)</returns>
    Task<byte[]?> TakeScreenshotAsync(IntPtr windowHandle = default);

    /// <summary>
    /// Finds UI elements by text content
    /// </summary>
    /// <param name="windowHandle">Handle of the parent window</param>
    /// <param name="text">Text to search for</param>
    /// <param name="exactMatch">Whether to match exactly or partial</param>
    /// <returns>Collection of matching UI elements</returns>
    Task<IEnumerable<UIElement>> FindElementsByTextAsync(IntPtr windowHandle, string text, bool exactMatch = false);

    /// <summary>
    /// Gets the current cursor position
    /// </summary>
    /// <returns>Current cursor coordinates</returns>
    Task<Point> GetCursorPositionAsync();

    /// <summary>
    /// Moves the cursor to specified coordinates
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <returns>True if successful</returns>
    Task<bool> MoveCursorAsync(int x, int y);

    /// <summary>
    /// Sends a window message directly using SendMessage
    /// </summary>
    Task<IntPtr> SendMessageAsync(IntPtr windowHandle, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Posts a window message directly using PostMessage
    /// </summary>
    Task<bool> PostMessageAsync(IntPtr windowHandle, uint msg, IntPtr wParam, IntPtr lParam);
}

/// <summary>
/// Mouse button enumeration
/// </summary>
public enum MouseButton
{
    Left,
    Right,
    Middle,
    X1,
    X2
}

/// <summary>
/// Scroll direction enumeration
/// </summary>
public enum ScrollDirection
{
    Up,
    Down,
    Left,
    Right
}