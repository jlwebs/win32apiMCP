using Microsoft.Extensions.Logging;
using WinAPIMCP.Configuration;
using WinAPIMCP.Models;
using System.Collections.Concurrent;

namespace WinAPIMCP.Services;

/// <summary>
/// Implementation of activity tracker for monitoring API operations
/// </summary>
public class ActivityTracker : IActivityTracker
{
    private readonly ILogger<ActivityTracker> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly ConcurrentDictionary<Guid, ActivityInfo> _activities;
    private readonly object _listLock = new object();

    /// <summary>
    /// Event fired when a new activity is added
    /// </summary>
    public event EventHandler<ActivityInfo>? ActivityAdded;

    /// <summary>
    /// Event fired when an activity is updated
    /// </summary>
    public event EventHandler<ActivityInfo>? ActivityUpdated;

    public ActivityTracker(ILogger<ActivityTracker> logger, SettingsManager settingsManager)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _activities = new ConcurrentDictionary<Guid, ActivityInfo>();
    }

    public Guid StartActivity(ActivityType type, string operation, string parameters = "", string source = "Unknown")
    {
        var activity = new ActivityInfo
        {
            Id = Guid.NewGuid(),
            Type = type,
            Operation = operation,
            Parameters = parameters,
            Source = source,
            Status = ActivityStatus.InProgress,
            Timestamp = DateTime.Now
        };

        _activities[activity.Id] = activity;
        _logger.LogDebug("Started activity {ActivityId}: {Operation}", activity.Id, operation);

        // Cleanup old activities if we exceed the limit
        CleanupOldActivities();

        ActivityAdded?.Invoke(this, activity);
        return activity.Id;
    }

    public void UpdateActivity(Guid activityId, ActivityStatus status, string result = "", string? errorMessage = null)
    {
        if (_activities.TryGetValue(activityId, out var activity))
        {
            activity.Status = status;
            if (!string.IsNullOrEmpty(result))
                activity.Result = result;
            if (!string.IsNullOrEmpty(errorMessage))
                activity.ErrorMessage = errorMessage;

            _logger.LogDebug("Updated activity {ActivityId}: Status = {Status}", activityId, status);
            ActivityUpdated?.Invoke(this, activity);
        }
    }

    public void CompleteActivity(Guid activityId, string result, long durationMs)
    {
        if (_activities.TryGetValue(activityId, out var activity))
        {
            activity.Status = ActivityStatus.Completed;
            activity.Result = result;
            activity.DurationMs = durationMs;

            _logger.LogDebug("Completed activity {ActivityId}: {Operation} in {Duration}ms", 
                           activityId, activity.Operation, durationMs);
            ActivityUpdated?.Invoke(this, activity);
        }
    }

    public void FailActivity(Guid activityId, string errorMessage, long durationMs)
    {
        if (_activities.TryGetValue(activityId, out var activity))
        {
            activity.Status = ActivityStatus.Failed;
            activity.ErrorMessage = errorMessage;
            activity.DurationMs = durationMs;

            _logger.LogWarning("Failed activity {ActivityId}: {Operation} - {Error}", 
                             activityId, activity.Operation, errorMessage);
            ActivityUpdated?.Invoke(this, activity);
        }
    }

    public void RequestPermission(Guid activityId)
    {
        if (_activities.TryGetValue(activityId, out var activity))
        {
            activity.PermissionRequested = true;
            activity.Status = ActivityStatus.Pending;

            _logger.LogDebug("Requested permission for activity {ActivityId}: {Operation}", 
                           activityId, activity.Operation);
            ActivityUpdated?.Invoke(this, activity);
        }
    }

    public void RecordPermissionResponse(Guid activityId, bool granted)
    {
        if (_activities.TryGetValue(activityId, out var activity))
        {
            activity.PermissionGranted = granted;
            if (!granted)
            {
                activity.Status = ActivityStatus.PermissionDenied;
                activity.ErrorMessage = "User denied permission";
            }
            else
            {
                activity.Status = ActivityStatus.InProgress;
            }

            _logger.LogDebug("Permission {Result} for activity {ActivityId}: {Operation}", 
                           granted ? "granted" : "denied", activityId, activity.Operation);
            ActivityUpdated?.Invoke(this, activity);
        }
    }

    public IReadOnlyList<ActivityInfo> GetAllActivities()
    {
        lock (_listLock)
        {
            return _activities.Values.OrderByDescending(a => a.Timestamp).ToList().AsReadOnly();
        }
    }

    public IReadOnlyList<ActivityInfo> GetRecentActivities(int count = 50)
    {
        lock (_listLock)
        {
            return _activities.Values
                .OrderByDescending(a => a.Timestamp)
                .Take(count)
                .ToList()
                .AsReadOnly();
        }
    }

    public IReadOnlyList<ActivityInfo> GetActivitiesByType(ActivityType type)
    {
        lock (_listLock)
        {
            return _activities.Values
                .Where(a => a.Type == type)
                .OrderByDescending(a => a.Timestamp)
                .ToList()
                .AsReadOnly();
        }
    }

    public IReadOnlyList<ActivityInfo> GetActivitiesByDateRange(DateTime startDate, DateTime endDate)
    {
        lock (_listLock)
        {
            return _activities.Values
                .Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate)
                .OrderByDescending(a => a.Timestamp)
                .ToList()
                .AsReadOnly();
        }
    }

    public void ClearActivities()
    {
        lock (_listLock)
        {
            _activities.Clear();
            _logger.LogInformation("Cleared all activity history");
        }
    }

    public int ActivityCount => _activities.Count;

    private void CleanupOldActivities()
    {
        var maxCount = _settingsManager.GetSetting(s => s.MaxActivityHistoryCount);
        if (_activities.Count <= maxCount)
            return;

        lock (_listLock)
        {
            var activitiesToRemove = _activities.Values
                .OrderBy(a => a.Timestamp)
                .Take(_activities.Count - maxCount)
                .Select(a => a.Id)
                .ToList();

            foreach (var id in activitiesToRemove)
            {
                _activities.TryRemove(id, out _);
            }

            if (activitiesToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} old activities", activitiesToRemove.Count);
            }
        }
    }
}