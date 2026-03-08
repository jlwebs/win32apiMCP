using WinAPIMCP.Models;

namespace WinAPIMCP.Services;

/// <summary>
/// Interface for handling user permission requests
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Requests permission from the user for an API operation
    /// </summary>
    /// <param name="operation">Description of the operation</param>
    /// <param name="details">Additional details about the operation</param>
    /// <param name="activityType">Type of activity requiring permission</param>
    /// <returns>True if permission is granted, false otherwise</returns>
    Task<bool> RequestPermissionAsync(string operation, string details, ActivityType activityType);

    /// <summary>
    /// Shows a notification to the user
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="type">Type of notification</param>
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);

    /// <summary>
    /// Checks if the user should be prompted for permission based on current settings
    /// </summary>
    /// <returns>True if permission should be requested</returns>
    bool ShouldRequestPermission();
}

/// <summary>
/// Types of notifications
/// </summary>
public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success
}