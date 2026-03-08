using WinAPIMCP.Models;

namespace WinAPIMCP.Services;

/// <summary>
/// Interface for tracking API activities and operations
/// </summary>
public interface IActivityTracker
{
    /// <summary>
    /// Event fired when a new activity is added
    /// </summary>
    event EventHandler<ActivityInfo>? ActivityAdded;

    /// <summary>
    /// Event fired when an activity is updated
    /// </summary>
    event EventHandler<ActivityInfo>? ActivityUpdated;

    /// <summary>
    /// Starts tracking a new activity
    /// </summary>
    /// <param name="type">Type of activity</param>
    /// <param name="operation">Operation name</param>
    /// <param name="parameters">Operation parameters</param>
    /// <param name="source">Source of the request</param>
    /// <returns>Activity ID for tracking</returns>
    Guid StartActivity(ActivityType type, string operation, string parameters = "", string source = "Unknown");

    /// <summary>
    /// Updates an existing activity with progress information
    /// </summary>
    /// <param name="activityId">Activity ID</param>
    /// <param name="status">New status</param>
    /// <param name="result">Operation result</param>
    /// <param name="errorMessage">Error message if failed</param>
    void UpdateActivity(Guid activityId, ActivityStatus status, string result = "", string? errorMessage = null);

    /// <summary>
    /// Completes an activity successfully
    /// </summary>
    /// <param name="activityId">Activity ID</param>
    /// <param name="result">Operation result</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    void CompleteActivity(Guid activityId, string result, long durationMs);

    /// <summary>
    /// Fails an activity with an error
    /// </summary>
    /// <param name="activityId">Activity ID</param>
    /// <param name="errorMessage">Error message</param>
    /// <param name="durationMs">Operation duration in milliseconds</param>
    void FailActivity(Guid activityId, string errorMessage, long durationMs);

    /// <summary>
    /// Marks an activity as requiring permission
    /// </summary>
    /// <param name="activityId">Activity ID</param>
    void RequestPermission(Guid activityId);

    /// <summary>
    /// Records the user's permission response
    /// </summary>
    /// <param name="activityId">Activity ID</param>
    /// <param name="granted">Whether permission was granted</param>
    void RecordPermissionResponse(Guid activityId, bool granted);

    /// <summary>
    /// Gets all activities
    /// </summary>
    /// <returns>List of all tracked activities</returns>
    IReadOnlyList<ActivityInfo> GetAllActivities();

    /// <summary>
    /// Gets recent activities
    /// </summary>
    /// <param name="count">Number of recent activities to retrieve</param>
    /// <returns>List of recent activities</returns>
    IReadOnlyList<ActivityInfo> GetRecentActivities(int count = 50);

    /// <summary>
    /// Gets activities of a specific type
    /// </summary>
    /// <param name="type">Activity type to filter by</param>
    /// <returns>List of activities of the specified type</returns>
    IReadOnlyList<ActivityInfo> GetActivitiesByType(ActivityType type);

    /// <summary>
    /// Gets activities within a date range
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <returns>List of activities within the date range</returns>
    IReadOnlyList<ActivityInfo> GetActivitiesByDateRange(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Clears all activities
    /// </summary>
    void ClearActivities();

    /// <summary>
    /// Gets the current activity count
    /// </summary>
    int ActivityCount { get; }
}