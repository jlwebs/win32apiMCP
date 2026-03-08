using Microsoft.Extensions.Logging;
using WinAPIMCP.Configuration;
using WinAPIMCP.Services;
using System.Windows.Forms;
using System.Drawing;
using WinAPIMCP.UI.Forms;
using Microsoft.Extensions.Hosting;

namespace WinAPIMCP.UI.Services;

/// <summary>
/// Service for managing the system tray icon and context menu
/// </summary>
public class TrayIconService : IDisposable
{
    private readonly ILogger<TrayIconService> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly IActivityTracker _activityTracker;
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _contextMenu;
    private MainWindow? _mainWindow;
    private SettingsForm? _settingsForm;
    private IHost? _webServerHost;
    private CancellationTokenSource? _cancellationTokenSource;
    private Thread? _webServerThread;
    private bool _disposed = false;

    public TrayIconService(
        ILogger<TrayIconService> logger,
        SettingsManager settingsManager,
        IActivityTracker activityTracker)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _activityTracker = activityTracker;
        
        InitializeTrayIcon();
    }

    /// <summary>
    /// Public method to ensure tray icon is initialized and start web server after UI is ready
    /// </summary>
    public void Initialize(IHost webServerHost)
    {
        _webServerHost = webServerHost;
        
        if (_trayIcon == null)
        {
            InitializeTrayIcon();
        }
        
        if (_trayIcon != null)
        {
            _trayIcon.Visible = true;
            _logger.LogInformation("Tray icon is now visible");
            
            // Start web server after UI is ready
            StartWebServer();
        }
    }

    private void InitializeTrayIcon()
    {
        try
        {
            // Create the tray icon
            _trayIcon = new NotifyIcon
            {
                Text = "Windows API MCP Server",
                Visible = true,
                Icon = CreateTrayIcon()
            };

            // Create context menu
            CreateContextMenu();

            // Set up event handlers
            _trayIcon.DoubleClick += OnTrayIconDoubleClick;
            _trayIcon.BalloonTipClicked += OnBalloonTipClicked;

            _logger.LogInformation("Tray icon initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize tray icon");
        }
    }

    private Icon CreateTrayIcon()
    {
        // Create a simple icon programmatically
        var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        
        // Draw a simple "W" for Windows API MCP
        graphics.FillRectangle(Brushes.DarkBlue, 0, 0, 16, 16);
        using var font = new Font("Arial", 8, FontStyle.Bold);
        graphics.DrawString("W", font, Brushes.White, 2, 1);
        
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        // Main window menu item
        var showMainWindowItem = new ToolStripMenuItem("Show Main Window")
        {
            Font = new Font(_contextMenu.Font, FontStyle.Bold)
        };
        showMainWindowItem.Click += OnShowMainWindow;
        _contextMenu.Items.Add(showMainWindowItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Settings menu item
        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += OnShowSettings;
        _contextMenu.Items.Add(settingsItem);

        // Agentic mode toggle
        var agenticModeItem = new ToolStripMenuItem("Agentic Mode")
        {
            CheckOnClick = true,
            Checked = _settingsManager.GetSetting(s => s.AgenticModeEnabled)
        };
        agenticModeItem.CheckedChanged += OnAgenticModeToggled;
        _contextMenu.Items.Add(agenticModeItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // Clear activity history
        var clearHistoryItem = new ToolStripMenuItem("Clear Activity History");
        clearHistoryItem.Click += OnClearHistory;
        _contextMenu.Items.Add(clearHistoryItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // About item
        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += OnShowAbout;
        _contextMenu.Items.Add(aboutItem);

        // Exit item
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += OnExit;
        _contextMenu.Items.Add(exitItem);

        _trayIcon!.ContextMenuStrip = _contextMenu;

        // Subscribe to settings changes to update the menu
        _settingsManager.SettingsChanged += OnSettingsChanged;
    }

    private void OnTrayIconDoubleClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnShowMainWindow(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnShowSettings(object? sender, EventArgs e)
    {
        ShowSettingsWindow();
    }

    private void OnAgenticModeToggled(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item)
        {
            _settingsManager.UpdateSetting<bool>(s => s.AgenticModeEnabled = item.Checked);
            
            var message = item.Checked ? "Agentic mode enabled" : "Agentic mode disabled";
            ShowBalloonTip("Settings Updated", message, ToolTipIcon.Info);
            
            _logger.LogInformation("Agentic mode {Status}", item.Checked ? "enabled" : "disabled");
        }
    }

    private void OnClearHistory(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all activity history?",
            "Clear History",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _activityTracker.ClearActivities();
            ShowBalloonTip("History Cleared", "All activity history has been cleared", ToolTipIcon.Info);
        }
    }

    private void OnShowAbout(object? sender, EventArgs e)
    {
        var about = $"Windows API MCP Server\\n" +
                   $"Version: 1.0.0\\n" +
                   $"A Model Context Protocol server for Windows API operations\\n\\n" +
                   $"Agentic Mode: {(_settingsManager.GetSetting(s => s.AgenticModeEnabled) ? "Enabled" : "Disabled")}\\n" +
                   $"Activities Tracked: {_activityTracker.ActivityCount}";

        MessageBox.Show(about, "About Windows API MCP", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        StopWebServer();
        Application.Exit();
    }
    
    private void StartWebServer()
    {
        if (_webServerHost == null)
        {
            _logger.LogWarning("No web server host provided");
            return;
        }
        
        try
        {
            _cancellationTokenSource = new CancellationTokenSource();
            
            _webServerThread = new Thread(() =>
            {
                try
                {
                    _logger.LogInformation("Starting web server on background thread");
                    _webServerHost.RunAsync(_cancellationTokenSource.Token).Wait();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Web server stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Web server error");
                }
            })
            {
                IsBackground = true,
                Name = "WebServerThread"
            };
            
            // Set up graceful shutdown when application exits
            Application.ApplicationExit += (sender, e) =>
            {
                _logger.LogInformation("Application exiting, stopping web server");
                StopWebServer();
            };
            
            _webServerThread.Start();
            _logger.LogInformation("Web server thread started successfully");
            
            // Show a notification that the server is ready
            ShowBalloonTip("Server Started", "Windows API MCP Server is running", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start web server");
            ShowBalloonTip("Server Error", "Failed to start web server", ToolTipIcon.Error);
        }
    }
    
    private void StopWebServer()
    {
        try
        {
            if (_cancellationTokenSource != null)
            {
                _logger.LogInformation("Stopping web server");
                _cancellationTokenSource.Cancel();
            }
            
            if (_webServerThread != null && _webServerThread.IsAlive)
            {
                _webServerThread.Join(TimeSpan.FromSeconds(5));
            }
            
            if (_webServerHost != null)
            {
                _webServerHost.StopAsync(TimeSpan.FromSeconds(5)).Wait(TimeSpan.FromSeconds(10));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during web server shutdown");
        }
    }

    private void OnSettingsChanged(object? sender, Configuration.AppSettings settings)
    {
        // Update context menu to reflect settings changes
        if (_contextMenu?.Items.OfType<ToolStripMenuItem>()
                .FirstOrDefault(item => item.Text == "Agentic Mode") is ToolStripMenuItem agenticItem)
        {
            agenticItem.Checked = settings.AgenticModeEnabled;
        }

        // Update tray icon tooltip
        if (_trayIcon != null)
        {
            _trayIcon.Text = $"Windows API MCP Server - {(settings.AgenticModeEnabled ? "Agentic" : "Manual")} Mode";
        }
    }

    public void ShowMainWindow()
    {
        try
        {
            if (_mainWindow == null || _mainWindow.IsDisposed)
            {
                _mainWindow = new MainWindow(_activityTracker, _settingsManager);
            }

            _mainWindow.Show();
            _mainWindow.WindowState = FormWindowState.Normal;
            _mainWindow.BringToFront();
            _mainWindow.Activate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show main window");
        }
    }

    public void ShowSettingsWindow()
    {
        try
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(_settingsManager);
            }

            _settingsForm.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show settings window");
        }
    }

    public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
    {
        if (_trayIcon != null && _settingsManager.GetSetting(s => s.ShowNotifications))
        {
            _trayIcon.ShowBalloonTip(timeout, title, text, icon);
        }
    }

    public void UpdateIcon(bool hasActivity)
    {
        if (_trayIcon != null)
        {
            // You could change the icon to indicate activity
            // For now, just update the tooltip
            var baseText = $"Windows API MCP Server - {(_settingsManager.GetSetting(s => s.AgenticModeEnabled) ? "Agentic" : "Manual")} Mode";
            _trayIcon.Text = hasActivity ? $"{baseText} (Active)" : baseText;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            StopWebServer();
            
            _settingsManager.SettingsChanged -= OnSettingsChanged;
            
            _trayIcon?.Dispose();
            _contextMenu?.Dispose();
            _mainWindow?.Dispose();
            _settingsForm?.Dispose();
            _cancellationTokenSource?.Dispose();
            
            _disposed = true;
        }
    }

    ~TrayIconService()
    {
        Dispose(false);
    }
}