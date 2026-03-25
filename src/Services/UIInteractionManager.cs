using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using WinAPIMCP.Models;

namespace WinAPIMCP.Services;

/// <summary>
/// Implementation of UI interaction and automation operations using Windows API
/// </summary>
public class UIInteractionManager : IUIInteractionManager
{
    private readonly ILogger<UIInteractionManager> _logger;
    private readonly IWindowManager _windowManager;

    public UIInteractionManager(ILogger<UIInteractionManager> logger, IWindowManager windowManager)
    {
        _logger = logger;
        _windowManager = windowManager;
    }

    public async Task<bool> ClickAtCoordinatesAsync(int x, int y, MouseButton button = MouseButton.Left, int clickCount = 1)
    {
        try
        {
            _logger.LogDebug("Clicking at coordinates ({X}, {Y}) with {Button} button, {ClickCount} times", x, y, button, clickCount);

            // Move cursor to position
            SetCursorPos(x, y);
            await Task.Delay(50); // Small delay for cursor movement

            // Determine mouse events
            uint downEvent = button switch
            {
                MouseButton.Right => MOUSEEVENTF_RIGHTDOWN,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
                _ => MOUSEEVENTF_LEFTDOWN
            };

            uint upEvent = button switch
            {
                MouseButton.Right => MOUSEEVENTF_RIGHTUP,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
                _ => MOUSEEVENTF_LEFTUP
            };

            // Perform clicks
            for (int i = 0; i < clickCount; i++)
            {
                mouse_event(downEvent, (uint)x, (uint)y, 0, 0);
                await Task.Delay(50);
                mouse_event(upEvent, (uint)x, (uint)y, 0, 0);
                
                if (i < clickCount - 1)
                    await Task.Delay(100); // Delay between multiple clicks
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to click at coordinates ({X}, {Y})", x, y);
            return false;
        }
    }

    public async Task<bool> ClickControlAsync(IntPtr windowHandle, IntPtr controlHandle, MouseButton button = MouseButton.Left)
    {
        try
        {
            _logger.LogDebug("Clicking control 0x{Control:X8} in window 0x{Window:X8}", controlHandle.ToInt64(), windowHandle.ToInt64());

            // Get control bounds
            if (!GetWindowRect(controlHandle, out RECT controlRect))
            {
                _logger.LogWarning("Failed to get control bounds for 0x{Control:X8}", controlHandle.ToInt64());
                return false;
            }

            // Calculate center point
            int centerX = controlRect.Left + (controlRect.Right - controlRect.Left) / 2;
            int centerY = controlRect.Top + (controlRect.Bottom - controlRect.Top) / 2;

            // Ensure window has focus first
            SetForegroundWindow(windowHandle);
            await Task.Delay(100);

            // Click at center of control
            return await ClickAtCoordinatesAsync(centerX, centerY, button, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to click control 0x{Control:X8}", controlHandle.ToInt64());
            return false;
        }
    }

    public async Task<bool> SendTextAsync(IntPtr windowHandle, string text, IntPtr? controlHandle = null)
    {
        try
        {
            _logger.LogDebug("Sending text '{Text}' to window 0x{Window:X8}", text, windowHandle.ToInt64());

            var targetHandle = controlHandle ?? windowHandle;
            
            // Ensure window has focus
            SetForegroundWindow(windowHandle);
            await Task.Delay(100);

            // If specific control, set focus to it
            if (controlHandle.HasValue)
            {
                SetFocus(controlHandle.Value);
                await Task.Delay(50);
            }

            // Send text character by character using SendMessage
            foreach (char c in text)
            {
                SendMessage(targetHandle, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                await Task.Delay(10); // Small delay between characters
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send text to window 0x{Window:X8}", windowHandle.ToInt64());
            return false;
        }
    }

    public async Task<bool> SendKeysAsync(IntPtr windowHandle, string keys, IntPtr? controlHandle = null)
    {
        try
        {
            _logger.LogDebug("Sending keys '{Keys}' to window 0x{Window:X8}", keys, windowHandle.ToInt64());

            // Ensure window has focus
            SetForegroundWindow(windowHandle);
            await Task.Delay(100);

            // If specific control, set focus to it
            if (controlHandle.HasValue)
            {
                SetFocus(controlHandle.Value);
                await Task.Delay(50);
            }

            // Parse and send key combinations
            return await SendKeySequence(keys);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send keys '{Keys}' to window 0x{Window:X8}", keys, windowHandle.ToInt64());
            return false;
        }
    }

    public async Task<string> GetControlTextAsync(IntPtr windowHandle, IntPtr controlHandle)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogDebug("Getting text from control 0x{Control:X8}", controlHandle.ToInt64());

            // First try WM_GETTEXT
            int textLength = SendMessage(controlHandle, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero).ToInt32();
            if (textLength > 0)
            {
                var buffer = new StringBuilder(textLength + 1);
                SendMessage(controlHandle, WM_GETTEXT, (IntPtr)buffer.Capacity, buffer);
                return buffer.ToString();
            }

            // If no text, try getting window text
            var windowTextBuffer = new StringBuilder(256);
            GetWindowText(controlHandle, windowTextBuffer, windowTextBuffer.Capacity);
            return windowTextBuffer.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get text from control 0x{Control:X8}", controlHandle.ToInt64());
            return string.Empty;
        }
    }

    public async Task<bool> SetControlTextAsync(IntPtr windowHandle, IntPtr controlHandle, string text)
    {
        try
        {
            _logger.LogDebug("Setting text '{Text}' in control 0x{Control:X8}", text, controlHandle.ToInt64());

            // Ensure window has focus
            SetForegroundWindow(windowHandle);
            await Task.Delay(100);

            // Try WM_SETTEXT first
            var result = SendMessage(controlHandle, WM_SETTEXT, IntPtr.Zero, text);
            if (result != IntPtr.Zero)
                return true;

            // Fallback: Clear existing text and send new text
            SetFocus(controlHandle);
            await Task.Delay(50);

            // Select all existing text
            SendMessage(controlHandle, EM_SETSEL, IntPtr.Zero, new IntPtr(-1));
            await Task.Delay(50);

            // Send new text
            return await SendTextAsync(windowHandle, text, controlHandle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set text in control 0x{Control:X8}", controlHandle.ToInt64());
            return false;
        }
    }

    public async Task<bool> SelectTextAsync(IntPtr windowHandle, IntPtr controlHandle, int startIndex = 0, int length = -1)
    {
        try
        {
            _logger.LogDebug("Selecting text in control 0x{Control:X8} from {Start} length {Length}", controlHandle.ToInt64(), startIndex, length);

            // Ensure window has focus
            SetForegroundWindow(windowHandle);
            await Task.Delay(100);
            SetFocus(controlHandle);
            await Task.Delay(50);

            // Calculate end position
            int endIndex = length == -1 ? -1 : startIndex + length;

            // Send selection message
            SendMessage(controlHandle, EM_SETSEL, (IntPtr)startIndex, (IntPtr)endIndex);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select text in control 0x{Control:X8}", controlHandle.ToInt64());
            return false;
        }
    }

    public async Task<bool> OpenContextMenuAsync(IntPtr windowHandle, int x, int y)
    {
        try
        {
            _logger.LogDebug("Opening context menu at ({X}, {Y}) in window 0x{Window:X8}", x, y, windowHandle.ToInt64());

            // Convert window-relative coordinates to screen coordinates
            var point = new POINT { X = x, Y = y };
            ClientToScreen(windowHandle, ref point);

            // Right-click at the position
            return await ClickAtCoordinatesAsync(point.X, point.Y, MouseButton.Right, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open context menu");
            return false;
        }
    }

    public async Task<bool> DragAsync(int startX, int startY, int endX, int endY, MouseButton button = MouseButton.Left)
    {
        try
        {
            _logger.LogDebug("Dragging from ({StartX}, {StartY}) to ({EndX}, {EndY})", startX, startY, endX, endY);

            // Move to start position
            SetCursorPos(startX, startY);
            await Task.Delay(100);

            // Mouse down
            uint downEvent = button switch
            {
                MouseButton.Right => MOUSEEVENTF_RIGHTDOWN,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
                _ => MOUSEEVENTF_LEFTDOWN
            };

            mouse_event(downEvent, (uint)startX, (uint)startY, 0, 0);
            await Task.Delay(100);

            // Drag to end position
            SetCursorPos(endX, endY);
            await Task.Delay(100);

            // Mouse up
            uint upEvent = button switch
            {
                MouseButton.Right => MOUSEEVENTF_RIGHTUP,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
                _ => MOUSEEVENTF_LEFTUP
            };

            mouse_event(upEvent, (uint)endX, (uint)endY, 0, 0);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to drag from ({StartX}, {StartY}) to ({EndX}, {EndY})", startX, startY, endX, endY);
            return false;
        }
    }

    public async Task<bool> ScrollAsync(IntPtr windowHandle, ScrollDirection direction, int amount = 3, IntPtr? controlHandle = null)
    {
        try
        {
            _logger.LogDebug("Scrolling {Direction} {Amount} steps in window 0x{Window:X8}", direction, amount, windowHandle.ToInt64());

            var targetHandle = controlHandle ?? windowHandle;

            // Ensure window has focus
            SetForegroundWindow(windowHandle);
            await Task.Delay(100);

            // Determine scroll parameters
            uint message = direction switch
            {
                ScrollDirection.Up or ScrollDirection.Down => WM_VSCROLL,
                ScrollDirection.Left or ScrollDirection.Right => WM_HSCROLL,
                _ => WM_VSCROLL
            };

            IntPtr wParam = direction switch
            {
                ScrollDirection.Up => (IntPtr)SB_LINEUP,
                ScrollDirection.Down => (IntPtr)SB_LINEDOWN,
                ScrollDirection.Left => (IntPtr)SB_LINELEFT,
                ScrollDirection.Right => (IntPtr)SB_LINERIGHT,
                _ => (IntPtr)SB_LINEDOWN
            };

            // Send scroll messages
            for (int i = 0; i < amount; i++)
            {
                SendMessage(targetHandle, message, wParam, IntPtr.Zero);
                await Task.Delay(50);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scroll in window 0x{Window:X8}", windowHandle.ToInt64());
            return false;
        }
    }

    public async Task<byte[]?> TakeScreenshotAsync(IntPtr windowHandle = default)
    {
        await Task.CompletedTask;
        try
        {
            _logger.LogDebug("Taking screenshot of {Target}", windowHandle == IntPtr.Zero ? "full screen" : $"window 0x{windowHandle.ToInt64():X8}");

            Rectangle bounds;

            if (windowHandle == IntPtr.Zero)
            {
                // Full screen screenshot
                bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            }
            else
            {
                // Window screenshot
                if (!GetWindowRect(windowHandle, out RECT windowRect))
                {
                    _logger.LogWarning("Failed to get window bounds for screenshot");
                    return null;
                }
                bounds = new Rectangle(windowRect.Left, windowRect.Top, 
                                     windowRect.Right - windowRect.Left, 
                                     windowRect.Bottom - windowRect.Top);
            }

            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to take screenshot");
            return null;
        }
    }

    public async Task<IEnumerable<UIElement>> FindElementsByTextAsync(IntPtr windowHandle, string text, bool exactMatch = false)
    {
        try
        {
            _logger.LogDebug("Finding elements with text '{Text}' in window 0x{Window:X8}", text, windowHandle.ToInt64());

            var elements = new List<UIElement>();
            var childWindows = await _windowManager.EnumerateChildWindowsAsync(windowHandle, true);

            foreach (var childWindow in childWindows)
            {
                if (string.IsNullOrEmpty(childWindow.Title))
                    continue;

                bool matches = exactMatch ? 
                    childWindow.Title.Equals(text, StringComparison.OrdinalIgnoreCase) :
                    childWindow.Title.Contains(text, StringComparison.OrdinalIgnoreCase);

                if (matches)
                {
                    elements.Add(new UIElement
                    {
                        Handle = childWindow.Handle,
                        ParentHandle = windowHandle,
                        Text = childWindow.Title,
                        ElementType = childWindow.ClassName,
                        ClassName = childWindow.ClassName,
                        Bounds = childWindow.Bounds,
                        IsVisible = childWindow.IsVisible,
                        IsEnabled = childWindow.IsEnabled,
                        HasFocus = false // TODO: Implement focus detection
                    });
                }
            }

            return elements;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find elements by text in window 0x{Window:X8}", windowHandle.ToInt64());
            return Enumerable.Empty<UIElement>();
        }
    }

    public async Task<Point> GetCursorPositionAsync()
    {
        await Task.CompletedTask;
        try
        {
            GetCursorPos(out POINT point);
            return new Point(point.X, point.Y);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cursor position");
            return Point.Empty;
        }
    }

    public async Task<bool> MoveCursorAsync(int x, int y)
    {
        await Task.CompletedTask;
        try
        {
            return SetCursorPos(x, y);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move cursor to ({X}, {Y})", x, y);
            return false;
        }
    }

    public Task<IntPtr> SendMessageAsync(IntPtr windowHandle, uint msg, IntPtr wParam, IntPtr lParam)
    {
        _logger.LogDebug("Sending message {Msg} to window 0x{Window:X8}", msg, windowHandle.ToInt64());
        var result = SendMessage(windowHandle, msg, wParam, lParam);
        return Task.FromResult(result);
    }

    public Task<bool> PostMessageAsync(IntPtr windowHandle, uint msg, IntPtr wParam, IntPtr lParam)
    {
        _logger.LogDebug("Posting message {Msg} to window 0x{Window:X8}", msg, windowHandle.ToInt64());
        var result = PostMessage(windowHandle, msg, wParam, lParam);
        return Task.FromResult(result);
    }

    // Helper method to send key sequences
    private async Task<bool> SendKeySequence(string keys)
    {
        try
        {
            var keySequences = ParseKeySequence(keys);
            
            foreach (var sequence in keySequences)
            {
                await SendSingleKey(sequence);
                await Task.Delay(50);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send key sequence '{Keys}'", keys);
            return false;
        }
    }

    private List<KeySequence> ParseKeySequence(string keys)
    {
        var sequences = new List<KeySequence>();
        
        // Handle common key combinations
        var parts = keys.Split('+');
        var modifiers = new List<VirtualKey>();
        VirtualKey? mainKey = null;

        foreach (var part in parts)
        {
            var trimmed = part.Trim().ToLower();
            
            if (trimmed == "ctrl" || trimmed == "control")
                modifiers.Add(VirtualKey.VK_CONTROL);
            else if (trimmed == "alt")
                modifiers.Add(VirtualKey.VK_MENU);
            else if (trimmed == "shift")
                modifiers.Add(VirtualKey.VK_SHIFT);
            else if (trimmed == "win" || trimmed == "windows")
                modifiers.Add(VirtualKey.VK_LWIN);
            else
            {
                mainKey = ParseSingleKey(trimmed);
            }
        }

        // If no modifiers, treat as simple key
        if (modifiers.Count == 0 && mainKey.HasValue)
        {
            sequences.Add(new KeySequence { Key = mainKey.Value, Modifiers = new List<VirtualKey>() });
        }
        else if (mainKey.HasValue)
        {
            sequences.Add(new KeySequence { Key = mainKey.Value, Modifiers = modifiers });
        }

        return sequences;
    }

    private VirtualKey ParseSingleKey(string key)
    {
        return key.ToLower() switch
        {
            "enter" => VirtualKey.VK_RETURN,
            "space" => VirtualKey.VK_SPACE,
            "tab" => VirtualKey.VK_TAB,
            "escape" => VirtualKey.VK_ESCAPE,
            "backspace" => VirtualKey.VK_BACK,
            "delete" => VirtualKey.VK_DELETE,
            "home" => VirtualKey.VK_HOME,
            "end" => VirtualKey.VK_END,
            "pageup" => VirtualKey.VK_PRIOR,
            "pagedown" => VirtualKey.VK_NEXT,
            "up" => VirtualKey.VK_UP,
            "down" => VirtualKey.VK_DOWN,
            "left" => VirtualKey.VK_LEFT,
            "right" => VirtualKey.VK_RIGHT,
            "f1" => VirtualKey.VK_F1,
            "f2" => VirtualKey.VK_F2,
            "f3" => VirtualKey.VK_F3,
            "f4" => VirtualKey.VK_F4,
            "f5" => VirtualKey.VK_F5,
            "f6" => VirtualKey.VK_F6,
            "f7" => VirtualKey.VK_F7,
            "f8" => VirtualKey.VK_F8,
            "f9" => VirtualKey.VK_F9,
            "f10" => VirtualKey.VK_F10,
            "f11" => VirtualKey.VK_F11,
            "f12" => VirtualKey.VK_F12,
            _ when key.Length == 1 => (VirtualKey)key.ToUpper()[0],
            _ => VirtualKey.VK_SPACE
        };
    }

    private async Task SendSingleKey(KeySequence sequence)
    {
        // Press modifiers
        foreach (var modifier in sequence.Modifiers)
        {
            keybd_event((byte)modifier, 0, 0, 0);
        }

        await Task.Delay(10);

        // Press main key
        keybd_event((byte)sequence.Key, 0, 0, 0);
        await Task.Delay(50);
        keybd_event((byte)sequence.Key, 0, KEYEVENTF_KEYUP, 0);

        await Task.Delay(10);

        // Release modifiers (in reverse order)
        for (int i = sequence.Modifiers.Count - 1; i >= 0; i--)
        {
            keybd_event((byte)sequence.Modifiers[i], 0, KEYEVENTF_KEYUP, 0);
        }
    }

    // Helper classes
    private class KeySequence
    {
        public VirtualKey Key { get; set; }
        public List<VirtualKey> Modifiers { get; set; } = new();
    }

    #region Windows API Declarations

    // Mouse events
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

    // Window messages
    private const uint WM_GETTEXT = 0x000D;
    private const uint WM_GETTEXTLENGTH = 0x000E;
    private const uint WM_SETTEXT = 0x000C;
    private const uint WM_CHAR = 0x0102;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_VSCROLL = 0x0115;
    private const uint WM_HSCROLL = 0x0114;

    // Edit control messages
    private const uint EM_SETSEL = 0x00B1;

    // Scroll bar constants
    private const int SB_LINEUP = 0;
    private const int SB_LINEDOWN = 1;
    private const int SB_LINELEFT = 0;
    private const int SB_LINERIGHT = 1;

    // Key event flags
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // Virtual key codes
    private enum VirtualKey : byte
    {
        VK_BACK = 0x08,
        VK_TAB = 0x09,
        VK_RETURN = 0x0D,
        VK_SHIFT = 0x10,
        VK_CONTROL = 0x11,
        VK_MENU = 0x12, // Alt key
        VK_ESCAPE = 0x1B,
        VK_SPACE = 0x20,
        VK_PRIOR = 0x21, // Page Up
        VK_NEXT = 0x22,  // Page Down
        VK_END = 0x23,
        VK_HOME = 0x24,
        VK_LEFT = 0x25,
        VK_UP = 0x26,
        VK_RIGHT = 0x27,
        VK_DOWN = 0x28,
        VK_DELETE = 0x2E,
        VK_LWIN = 0x5B,
        VK_F1 = 0x70,
        VK_F2 = 0x71,
        VK_F3 = 0x72,
        VK_F4 = 0x73,
        VK_F5 = 0x74,
        VK_F6 = 0x75,
        VK_F7 = 0x76,
        VK_F8 = 0x77,
        VK_F9 = 0x78,
        VK_F10 = 0x79,
        VK_F11 = 0x7A,
        VK_F12 = 0x7B
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    #endregion
}