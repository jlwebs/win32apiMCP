using System.ComponentModel;
using System.Windows.Forms;
using WinAPIMCP.Configuration;
using WinAPIMCP.Models;
using WinAPIMCP.Services;

namespace WinAPIMCP.UI.Forms;

/// <summary>
/// Main application window showing API activity history and current operations
/// </summary>
public partial class MainWindow : Form
{
    private readonly IActivityTracker _activityTracker;
    private readonly SettingsManager _settingsManager;
    private ListView _activityListView;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _statusLabel;
    private ToolStripStatusLabel _activityCountLabel;
    private ToolStripStatusLabel _agenticModeLabel;
    private System.Windows.Forms.Timer _refreshTimer;
    private bool _autoRefresh = true;

    public MainWindow(IActivityTracker activityTracker, SettingsManager settingsManager)
    {
        _activityTracker = activityTracker;
        _settingsManager = settingsManager;
        
        InitializeComponent();
        SetupEventHandlers();
        RefreshActivityList();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form properties
        this.Text = "Windows API MCP Server - Activity Monitor";
        this.Size = new Size(900, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Icon = SystemIcons.Application;
        this.MinimumSize = new Size(800, 400);

        // Create menu bar
        var menuStrip = new MenuStrip();
        CreateMenuBar(menuStrip);
        this.MainMenuStrip = menuStrip;
        this.Controls.Add(menuStrip);

        // Create toolbar
        var toolStrip = new ToolStrip();
        CreateToolBar(toolStrip);
        this.Controls.Add(toolStrip);

        // Create main content area
        CreateContentArea();

        // Create status bar
        CreateStatusBar();

        // Set up refresh timer
        _refreshTimer = new System.Windows.Forms.Timer();
        _refreshTimer.Interval = 2000; // Refresh every 2 seconds
        _refreshTimer.Tick += OnRefreshTimer;
        _refreshTimer.Start();

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void CreateMenuBar(MenuStrip menuStrip)
    {
        // File menu
        var fileMenu = new ToolStripMenuItem("&File");
        
        var clearHistoryItem = new ToolStripMenuItem("Clear &History");
        clearHistoryItem.Click += OnClearHistory;
        fileMenu.DropDownItems.Add(clearHistoryItem);
        
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        
        var exitItem = new ToolStripMenuItem("E&xit");
        exitItem.Click += (s, e) => this.Hide();
        fileMenu.DropDownItems.Add(exitItem);

        // View menu
        var viewMenu = new ToolStripMenuItem("&View");
        
        var autoRefreshItem = new ToolStripMenuItem("&Auto Refresh")
        {
            Checked = _autoRefresh,
            CheckOnClick = true
        };
        autoRefreshItem.CheckedChanged += OnAutoRefreshToggled;
        viewMenu.DropDownItems.Add(autoRefreshItem);
        
        var refreshItem = new ToolStripMenuItem("&Refresh Now");
        refreshItem.ShortcutKeys = Keys.F5;
        refreshItem.Click += (s, e) => RefreshActivityList();
        viewMenu.DropDownItems.Add(refreshItem);

        // Tools menu
        var toolsMenu = new ToolStripMenuItem("&Tools");
        
        var settingsItem = new ToolStripMenuItem("&Settings...");
        settingsItem.Click += OnShowSettings;
        toolsMenu.DropDownItems.Add(settingsItem);

        // Help menu
        var helpMenu = new ToolStripMenuItem("&Help");
        
        var aboutItem = new ToolStripMenuItem("&About");
        aboutItem.Click += OnShowAbout;
        helpMenu.DropDownItems.Add(aboutItem);

        menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, toolsMenu, helpMenu });
    }

    private void CreateToolBar(ToolStrip toolStrip)
    {
        var refreshButton = new ToolStripButton("Refresh")
        {
            Image = SystemIcons.Question.ToBitmap(), // In practice, use proper icons
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
        };
        refreshButton.Click += (s, e) => RefreshActivityList();

        var clearButton = new ToolStripButton("Clear History")
        {
            Image = SystemIcons.Warning.ToBitmap(),
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
        };
        clearButton.Click += OnClearHistory;

        var agenticModeButton = new ToolStripButton("Agentic Mode")
        {
            CheckOnClick = true,
            Checked = _settingsManager.GetSetting(s => s.AgenticModeEnabled),
            DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
        };
        agenticModeButton.CheckedChanged += OnAgenticModeToggled;

        toolStrip.Items.AddRange(new ToolStripItem[] 
        { 
            refreshButton, 
            new ToolStripSeparator(),
            clearButton,
            new ToolStripSeparator(),
            agenticModeButton
        });
    }

    private void CreateContentArea()
    {
        // Create the activity list view
        _activityListView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = true
        };

        // Add columns
        _activityListView.Columns.Add("Time", 120);
        _activityListView.Columns.Add("Type", 100);
        _activityListView.Columns.Add("Operation", 200);
        _activityListView.Columns.Add("Status", 80);
        _activityListView.Columns.Add("Source", 100);
        _activityListView.Columns.Add("Duration", 80);
        _activityListView.Columns.Add("Parameters", 300);

        // Create context menu for list view
        var contextMenu = new ContextMenuStrip();
        var copyItem = new ToolStripMenuItem("Copy Details");
        copyItem.Click += OnCopyActivityDetails;
        contextMenu.Items.Add(copyItem);
        
        var viewDetailsItem = new ToolStripMenuItem("View Details");
        viewDetailsItem.Click += OnViewActivityDetails;
        contextMenu.Items.Add(viewDetailsItem);

        _activityListView.ContextMenuStrip = contextMenu;
        _activityListView.DoubleClick += OnViewActivityDetails;

        this.Controls.Add(_activityListView);
    }

    private void CreateStatusBar()
    {
        _statusStrip = new StatusStrip();

        _statusLabel = new ToolStripStatusLabel("Ready")
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _activityCountLabel = new ToolStripStatusLabel($"Activities: {_activityTracker.ActivityCount}");
        
        _agenticModeLabel = new ToolStripStatusLabel(
            _settingsManager.GetSetting(s => s.AgenticModeEnabled) ? "Agentic Mode: ON" : "Agentic Mode: OFF")
        {
            BackColor = _settingsManager.GetSetting(s => s.AgenticModeEnabled) ? Color.LightGreen : Color.LightCoral
        };

        _statusStrip.Items.AddRange(new ToolStripItem[] 
        { 
            _statusLabel, 
            _activityCountLabel,
            _agenticModeLabel
        });

        this.Controls.Add(_statusStrip);
    }

    private void SetupEventHandlers()
    {
        _activityTracker.ActivityAdded += OnActivityAdded;
        _activityTracker.ActivityUpdated += OnActivityUpdated;
        _settingsManager.SettingsChanged += OnSettingsChanged;

        this.FormClosing += OnFormClosing;
    }

    private void OnActivityAdded(object? sender, ActivityInfo activity)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(() => OnActivityAdded(sender, activity)));
            return;
        }

        AddActivityToList(activity);
        UpdateStatusBar();
    }

    private void OnActivityUpdated(object? sender, ActivityInfo activity)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(() => OnActivityUpdated(sender, activity)));
            return;
        }

        UpdateActivityInList(activity);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(() => OnSettingsChanged(sender, settings)));
            return;
        }

        UpdateStatusBar();
    }

    private void RefreshActivityList()
    {
        _activityListView.Items.Clear();
        
        var activities = _activityTracker.GetRecentActivities(100);
        foreach (var activity in activities)
        {
            AddActivityToList(activity);
        }

        UpdateStatusBar();
        _statusLabel.Text = $"Refreshed at {DateTime.Now:HH:mm:ss}";
    }

    private void AddActivityToList(ActivityInfo activity)
    {
        var item = new ListViewItem(activity.Timestamp.ToString("HH:mm:ss"))
        {
            Tag = activity,
            UseItemStyleForSubItems = false
        };

        item.SubItems.Add(activity.Type.ToString());
        item.SubItems.Add(activity.Operation);
        
        var statusSubItem = item.SubItems.Add(activity.Status.ToString());
        SetStatusColor(statusSubItem, activity.Status);
        
        item.SubItems.Add(activity.Source);
        item.SubItems.Add(activity.DurationMs > 0 ? $"{activity.DurationMs}ms" : "-");
        item.SubItems.Add(TruncateText(activity.Parameters, 50));

        _activityListView.Items.Insert(0, item); // Add to top

        // Limit the number of items displayed to prevent memory issues
        while (_activityListView.Items.Count > 1000)
        {
            _activityListView.Items.RemoveAt(_activityListView.Items.Count - 1);
        }
    }

    private void UpdateActivityInList(ActivityInfo activity)
    {
        var existingItem = _activityListView.Items.Cast<ListViewItem>()
            .FirstOrDefault(item => item.Tag is ActivityInfo info && info.Id == activity.Id);

        if (existingItem != null)
        {
            existingItem.Tag = activity;
            existingItem.SubItems[3].Text = activity.Status.ToString();
            SetStatusColor(existingItem.SubItems[3], activity.Status);
            existingItem.SubItems[5].Text = activity.DurationMs > 0 ? $"{activity.DurationMs}ms" : "-";
        }
    }

    private void SetStatusColor(ListViewItem.ListViewSubItem subItem, ActivityStatus status)
    {
        subItem.BackColor = status switch
        {
            ActivityStatus.Completed => Color.LightGreen,
            ActivityStatus.Failed => Color.LightCoral,
            ActivityStatus.PermissionDenied => Color.Orange,
            ActivityStatus.InProgress => Color.LightBlue,
            ActivityStatus.Pending => Color.LightYellow,
            _ => Color.Transparent
        };
    }

    private void UpdateStatusBar()
    {
        _activityCountLabel.Text = $"Activities: {_activityTracker.ActivityCount}";
        
        var isAgenticMode = _settingsManager.GetSetting(s => s.AgenticModeEnabled);
        _agenticModeLabel.Text = isAgenticMode ? "Agentic Mode: ON" : "Agentic Mode: OFF";
        _agenticModeLabel.BackColor = isAgenticMode ? Color.LightGreen : Color.LightCoral;
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        
        return text.Substring(0, maxLength - 3) + "...";
    }

    // Event handlers
    private void OnRefreshTimer(object? sender, EventArgs e)
    {
        if (_autoRefresh && this.Visible)
        {
            UpdateStatusBar();
        }
    }

    private void OnAutoRefreshToggled(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem item)
        {
            _autoRefresh = item.Checked;
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
            RefreshActivityList();
        }
    }

    private void OnShowSettings(object? sender, EventArgs e)
    {
        using var settingsForm = new SettingsForm(_settingsManager);
        settingsForm.ShowDialog(this);
    }

    private void OnShowAbout(object? sender, EventArgs e)
    {
        var about = "Windows API MCP Server\\n" +
                   "Version: 1.0.0\\n\\n" +
                   "A Model Context Protocol server for Windows API operations.";

        MessageBox.Show(about, "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnAgenticModeToggled(object? sender, EventArgs e)
    {
        if (sender is ToolStripButton button)
        {
            _settingsManager.UpdateSetting<bool>(s => s.AgenticModeEnabled = button.Checked);
        }
    }

    private void OnCopyActivityDetails(object? sender, EventArgs e)
    {
        if (_activityListView.SelectedItems.Count > 0 &&
            _activityListView.SelectedItems[0].Tag is ActivityInfo activity)
        {
            var details = $"Activity Details:\\n" +
                         $"ID: {activity.Id}\\n" +
                         $"Time: {activity.Timestamp}\\n" +
                         $"Type: {activity.Type}\\n" +
                         $"Operation: {activity.Operation}\\n" +
                         $"Status: {activity.Status}\\n" +
                         $"Source: {activity.Source}\\n" +
                         $"Duration: {activity.DurationMs}ms\\n" +
                         $"Parameters: {activity.Parameters}\\n" +
                         $"Result: {activity.Result}\\n" +
                         $"Error: {activity.ErrorMessage}";

            Clipboard.SetText(details);
            _statusLabel.Text = "Activity details copied to clipboard";
        }
    }

    private void OnViewActivityDetails(object? sender, EventArgs e)
    {
        if (_activityListView.SelectedItems.Count > 0 &&
            _activityListView.SelectedItems[0].Tag is ActivityInfo activity)
        {
            using var detailsForm = new ActivityDetailsForm(activity);
            detailsForm.ShowDialog(this);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Just hide the form instead of closing it completely
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            this.Hide();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Dispose();
            _activityTracker.ActivityAdded -= OnActivityAdded;
            _activityTracker.ActivityUpdated -= OnActivityUpdated;
            _settingsManager.SettingsChanged -= OnSettingsChanged;
        }
        base.Dispose(disposing);
    }
}