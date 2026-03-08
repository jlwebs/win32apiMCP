using Microsoft.Extensions.Logging;
using WinAPIMCP.Configuration;
using WinAPIMCP.Models;
using System.Windows.Forms;

namespace WinAPIMCP.Services;

/// <summary>
/// Implementation of permission service using Windows Forms dialogs and notifications
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly ILogger<PermissionService> _logger;
    private readonly SettingsManager _settingsManager;

    public PermissionService(ILogger<PermissionService> logger, SettingsManager settingsManager)
    {
        _logger = logger;
        _settingsManager = settingsManager;
    }

    public async Task<bool> RequestPermissionAsync(string operation, string details, ActivityType activityType)
    {
        if (!ShouldRequestPermission())
        {
            _logger.LogDebug("Agentic mode enabled, auto-granting permission for: {Operation}", operation);
            return true;
        }

        _logger.LogInformation("Requesting user permission for: {Operation}", operation);

        return await Task.Run(() =>
        {
            try
            {
                var dialog = new PermissionDialog(operation, details, activityType);
                dialog.ShowDialog();
                
                var granted = dialog.PermissionGranted;

                if (dialog.AlwaysAllowSelected)
                {
                    _logger.LogInformation("User selected 'Always Allow', enabling Agentic Mode");
                    _settingsManager.UpdateSetting<bool>(s => s.AgenticModeEnabled = true);
                    granted = true;
                }
                
                _logger.LogInformation("Permission {Result} for operation: {Operation}", 
                                      granted ? "granted" : "denied", operation);

                // Show notification about the result if enabled
                if (_settingsManager.GetSetting(s => s.ShowNotifications))
                {
                    var notificationTitle = granted ? "Permission Granted" : "Permission Denied";
                    var notificationMessage = $"{operation}: {(granted ? "Allowed" : "Blocked")}";
                    var notificationType = granted ? NotificationType.Success : NotificationType.Warning;
                    
                    ShowNotification(notificationTitle, notificationMessage, notificationType);
                }

                return granted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing permission dialog for: {Operation}", operation);
                return false; // Deny permission on error for security
            }
        });
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        if (!_settingsManager.GetSetting(s => s.ShowNotifications))
        {
            return;
        }

        try
        {
            // Use Windows 10+ toast notifications if available, otherwise fall back to tray balloon
            ShowTrayBalloon(title, message, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing notification: {Title}", title);
        }
    }

    public bool ShouldRequestPermission()
    {
        return !_settingsManager.GetSetting(s => s.AgenticModeEnabled);
    }

    private MessageBoxIcon GetIconForActivityType(ActivityType activityType)
    {
        return activityType switch
        {
            ActivityType.ProcessControl => MessageBoxIcon.Warning,
            ActivityType.WindowControl => MessageBoxIcon.Question,
            ActivityType.SystemQuery => MessageBoxIcon.Information,
            ActivityType.Error => MessageBoxIcon.Error,
            _ => MessageBoxIcon.Question
        };
    }

    private void ShowTrayBalloon(string title, string message, NotificationType type)
    {
        // This would typically be handled by the tray icon service
        // For now, we'll use a simple approach
        var toolTipIcon = type switch
        {
            NotificationType.Error => ToolTipIcon.Error,
            NotificationType.Warning => ToolTipIcon.Warning,
            NotificationType.Success => ToolTipIcon.Info,
            _ => ToolTipIcon.Info
        };

        // Note: This requires access to the NotifyIcon from the tray service
        // In practice, this would be coordinated through events or dependency injection
        _logger.LogInformation("Notification: {Title} - {Message}", title, message);
    }
}

/// <summary>
/// Permission dialog for more detailed permission requests
/// </summary>
public partial class PermissionDialog : Form
{
    private bool _permissionGranted = false;
    private bool _alwaysAllowSelected = false;

    public bool PermissionGranted => _permissionGranted;
    public bool AlwaysAllowSelected => _alwaysAllowSelected;

    public PermissionDialog(string operation, string details, ActivityType activityType)
    {
        InitializeComponent();
        SetupDialog(operation, details, activityType);
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        
        // Form properties
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(450, 200);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.TopMost = true;
        this.Text = "Windows API MCP - Permission Request";
        
        // Create controls
        var labelOperation = new Label
        {
            Location = new System.Drawing.Point(12, 15),
            Size = new System.Drawing.Size(426, 20),
            Text = "Operation:",
            Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
        };

        var labelDetails = new Label
        {
            Location = new System.Drawing.Point(12, 80),
            Size = new System.Drawing.Size(426, 60),
            Text = "Details will be shown here...",
            AutoSize = false
        };

        var buttonAlwaysAllow = new Button
        {
            Location = new System.Drawing.Point(12, 150),
            Size = new System.Drawing.Size(100, 23),
            Text = "Always Allow",
            UseVisualStyleBackColor = true
        };
        buttonAlwaysAllow.Click += (s, e) => { _alwaysAllowSelected = true; _permissionGranted = true; this.Close(); };

        var buttonAllow = new Button
        {
            Location = new System.Drawing.Point(275, 150),
            Size = new System.Drawing.Size(75, 23),
            Text = "Allow",
            UseVisualStyleBackColor = true
        };
        buttonAllow.Click += (s, e) => { _permissionGranted = true; this.Close(); };

        var buttonDeny = new Button
        {
            Location = new System.Drawing.Point(356, 150),
            Size = new System.Drawing.Size(75, 23),
            Text = "Deny",
            UseVisualStyleBackColor = true
        };
        buttonDeny.Click += (s, e) => { _permissionGranted = false; this.Close(); };

        // Add controls to form
        this.Controls.AddRange(new Control[] { 
            labelOperation, labelDetails, buttonAlwaysAllow, buttonAllow, buttonDeny 
        });

        this.ResumeLayout(false);
    }

    private void SetupDialog(string operation, string details, ActivityType activityType)
    {
        var operationLabel = this.Controls.OfType<Label>().First();
        var detailsLabel = this.Controls.OfType<Label>().Skip(1).First();

        operationLabel.Text = $"Operation: {operation}";
        detailsLabel.Text = $"Details:\n{details}";

        // Set icon based on activity type
        this.Icon = SystemIcons.Question;
    }
}