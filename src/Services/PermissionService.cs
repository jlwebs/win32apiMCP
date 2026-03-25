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

        var tcs = new TaskCompletionSource<bool>();
        
        var thread = new Thread(() =>
        {
            try
            {
                _logger.LogInformation(">>> [STA Thread] Starting PermissionDialog creation...");
                
                // Extremely important: enable visual styles for the thread before creating WinForms controls
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                var dialog = new PermissionDialog(operation, details, activityType);
                
                _logger.LogInformation(">>> [STA Thread] Calling dialog.ShowDialog(). Waiting for user input...");
                dialog.ShowDialog();
                _logger.LogInformation(">>> [STA Thread] dialog.ShowDialog() returned.");
                
                var granted = dialog.PermissionGranted;
                _logger.LogInformation($">>> [STA Thread] User choice: Granted={granted}, AlwaysAllow={dialog.AlwaysAllowSelected}");

                if (dialog.AlwaysAllowSelected)
                {
                    _logger.LogInformation(">>> [STA Thread] User selected 'Always Allow', applying to SettingsManager...");
                    try {
                        _settingsManager.UpdateSetting<bool>(s => s.AgenticModeEnabled = true);
                        _logger.LogInformation(">>> [STA Thread] UpdateSetting completed successfully.");
                    } catch (Exception ex) {
                        _logger.LogError(ex, ">>> [STA Thread] UpdateSetting threw an exception!");
                    }
                    granted = true;
                }
                
                _logger.LogInformation("Permission {Result} for operation: {Operation}", 
                                      granted ? "granted" : "denied", operation);

                if (_settingsManager.GetSetting(s => s.ShowNotifications))
                {
                    _logger.LogInformation(">>> [STA Thread] Showing tray notification...");
                    var notificationTitle = granted ? "Permission Granted" : "Permission Denied";
                    var notificationMessage = $"{operation}: {(granted ? "Allowed" : "Blocked")}";
                    var notificationType = granted ? NotificationType.Success : NotificationType.Warning;
                    
                    ShowNotification(notificationTitle, notificationMessage, notificationType);
                    _logger.LogInformation(">>> [STA Thread] Tray notification shown.");
                }

                _logger.LogInformation(">>> [STA Thread] Resolving TCS result...");
                tcs.SetResult(granted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [STA Thread] Error showing permission dialog for: {Operation}", operation);
                tcs.SetResult(false);
            }
        });
        
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.IsBackground = true;
        _logger.LogInformation(">>> Starting STA thread to show dialog...");
        thread.Start();

        _logger.LogInformation(">>> Waiting for TCS task to complete...");
        return await tcs.Task;
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