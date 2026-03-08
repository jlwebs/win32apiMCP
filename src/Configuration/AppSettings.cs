using System.Text.Json;

namespace WinAPIMCP.Configuration;

/// <summary>
/// Application settings model
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Whether agentic mode is enabled (automatic API execution without user prompts)
    /// </summary>
    public bool AgenticModeEnabled { get; set; } = false;

    /// <summary>
    /// Server port for MCP communication
    /// </summary>
    public int Port { get; set; } = 3000;

    /// <summary>
    /// Whether to allow interaction with elevated processes
    /// </summary>
    public bool AllowElevated { get; set; } = false;

    /// <summary>
    /// Logging level
    /// </summary>
    public string LogLevel { get; set; } = "Info";

    /// <summary>
    /// Configuration file path (if using external config)
    /// </summary>
    public string? ConfigFile { get; set; }

    /// <summary>
    /// Whether to show notifications for API requests
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Maximum number of activities to keep in history
    /// </summary>
    public int MaxActivityHistoryCount { get; set; } = 1000;

    /// <summary>
    /// Whether to start with Windows
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Whether to minimize to tray on startup
    /// </summary>
    public bool MinimizeToTrayOnStartup { get; set; } = true;

    /// <summary>
    /// Creates a copy of the current settings
    /// </summary>
    public AppSettings Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}