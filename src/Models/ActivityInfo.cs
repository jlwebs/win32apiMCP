namespace WinAPIMCP.Models;

/// <summary>
/// Information about an API activity/invocation
/// </summary>
public class ActivityInfo
{
    /// <summary>
    /// Unique identifier for this activity
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Timestamp when the activity occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Type of activity (API call, permission request, etc.)
    /// </summary>
    public ActivityType Type { get; set; }

    /// <summary>
    /// API method or operation that was called
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Parameters passed to the operation
    /// </summary>
    public string Parameters { get; set; } = string.Empty;

    /// <summary>
    /// Result of the operation
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// Status of the activity
    /// </summary>
    public ActivityStatus Status { get; set; }

    /// <summary>
    /// Error message if the activity failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Duration of the operation in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Whether user permission was requested for this activity
    /// </summary>
    public bool PermissionRequested { get; set; }

    /// <summary>
    /// Whether user granted permission (if permission was requested)
    /// </summary>
    public bool? PermissionGranted { get; set; }

    /// <summary>
    /// Source of the request (MCP client, internal, etc.)
    /// </summary>
    public string Source { get; set; } = "Unknown";

    /// <summary>
    /// Additional metadata about the activity
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Types of activities that can be tracked
/// </summary>
public enum ActivityType
{
    WindowEnumeration,
    WindowQuery,
    WindowControl,
    ProcessEnumeration,
    ProcessQuery,
    ProcessControl,
    PermissionRequest,
    SystemQuery,
    Configuration,
    ToolCall,
    UIInteraction,
    Error
}

/// <summary>
/// Status of an activity
/// </summary>
public enum ActivityStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    PermissionDenied
}