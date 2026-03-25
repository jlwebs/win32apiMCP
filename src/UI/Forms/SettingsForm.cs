using System.Windows.Forms;
using WinAPIMCP.Configuration;

namespace WinAPIMCP.UI.Forms;

/// <summary>
/// Settings dialog for configuring application settings
/// </summary>
public partial class SettingsForm : Form
{
    private readonly SettingsManager _settingsManager;
    private AppSettings _currentSettings;

    // Controls
    private CheckBox _agenticModeCheckBox = null!;
    private CheckBox _showNotificationsCheckBox = null!;
    private CheckBox _startWithWindowsCheckBox = null!;
    private CheckBox _minimizeToTrayCheckBox = null!;
    private NumericUpDown _portNumericUpDown = null!;
    private NumericUpDown _maxActivitiesNumericUpDown = null!;
    private ComboBox _logLevelComboBox = null!;
    private CheckBox _allowElevatedCheckBox = null!;

    public SettingsForm(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        _currentSettings = _settingsManager.Settings;
        
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form properties
        this.Text = "Settings - Windows API MCP";
        this.Size = new Size(500, 450);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterParent;
        this.ShowInTaskbar = false;

        // Create tab control
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Size = new Size(480, 350)
        };

        // General tab
        var generalTab = new TabPage("General");
        CreateGeneralTab(generalTab);
        tabControl.TabPages.Add(generalTab);

        // Security tab
        var securityTab = new TabPage("Security");
        CreateSecurityTab(securityTab);
        tabControl.TabPages.Add(securityTab);

        // Advanced tab
        var advancedTab = new TabPage("Advanced");
        CreateAdvancedTab(advancedTab);
        tabControl.TabPages.Add(advancedTab);

        // Create button panel
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50
        };

        var okButton = new Button
        {
            Text = "OK",
            Size = new Size(75, 23),
            Location = new Point(250, 15),
            DialogResult = DialogResult.OK
        };
        okButton.Click += OnOkClicked;

        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(75, 23),
            Location = new Point(335, 15),
            DialogResult = DialogResult.Cancel
        };

        var resetButton = new Button
        {
            Text = "Reset to Defaults",
            Size = new Size(100, 23),
            Location = new Point(15, 15)
        };
        resetButton.Click += OnResetClicked;

        buttonPanel.Controls.AddRange(new Control[] { resetButton, okButton, cancelButton });

        // Add controls to form
        this.Controls.Add(tabControl);
        this.Controls.Add(buttonPanel);

        this.AcceptButton = okButton;
        this.CancelButton = cancelButton;

        this.ResumeLayout(false);
    }

    private void CreateGeneralTab(TabPage tab)
    {
        var yPos = 20;

        // Agentic Mode
        _agenticModeCheckBox = new CheckBox
        {
            Text = "Enable Agentic Mode (automatic API execution)",
            Location = new Point(20, yPos),
            Size = new Size(400, 20),
            AutoSize = true
        };
        tab.Controls.Add(_agenticModeCheckBox);
        yPos += 30;

        // Show Notifications
        _showNotificationsCheckBox = new CheckBox
        {
            Text = "Show notifications for API operations",
            Location = new Point(20, yPos),
            Size = new Size(300, 20),
            AutoSize = true
        };
        tab.Controls.Add(_showNotificationsCheckBox);
        yPos += 30;

        // Start with Windows
        _startWithWindowsCheckBox = new CheckBox
        {
            Text = "Start with Windows",
            Location = new Point(20, yPos),
            Size = new Size(200, 20),
            AutoSize = true
        };
        tab.Controls.Add(_startWithWindowsCheckBox);
        yPos += 30;

        // Minimize to Tray
        _minimizeToTrayCheckBox = new CheckBox
        {
            Text = "Minimize to system tray on startup",
            Location = new Point(20, yPos),
            Size = new Size(300, 20),
            AutoSize = true
        };
        tab.Controls.Add(_minimizeToTrayCheckBox);
        yPos += 40;

        // Server Port
        var portLabel = new Label
        {
            Text = "MCP Server Port:",
            Location = new Point(20, yPos),
            Size = new Size(120, 20),
            AutoSize = true
        };
        tab.Controls.Add(portLabel);

        _portNumericUpDown = new NumericUpDown
        {
            Location = new Point(150, yPos - 3),
            Size = new Size(80, 20),
            Minimum = 1024,
            Maximum = 65535,
            Value = 3000
        };
        tab.Controls.Add(_portNumericUpDown);
        yPos += 40;

        // Log Level
        var logLevelLabel = new Label
        {
            Text = "Log Level:",
            Location = new Point(20, yPos),
            Size = new Size(80, 20),
            AutoSize = true
        };
        tab.Controls.Add(logLevelLabel);

        _logLevelComboBox = new ComboBox
        {
            Location = new Point(150, yPos - 3),
            Size = new Size(100, 21),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _logLevelComboBox.Items.AddRange(new[] { "Debug", "Info", "Warn", "Error" });
        tab.Controls.Add(_logLevelComboBox);
    }

    private void CreateSecurityTab(TabPage tab)
    {
        var yPos = 20;

        // Description
        var descLabel = new Label
        {
            Text = "Security settings control how the application handles permissions and elevated processes:",
            Location = new Point(20, yPos),
            Size = new Size(420, 40),
            AutoSize = false
        };
        tab.Controls.Add(descLabel);
        yPos += 50;

        // Allow Elevated
        _allowElevatedCheckBox = new CheckBox
        {
            Text = "Allow interaction with elevated processes",
            Location = new Point(20, yPos),
            Size = new Size(350, 20),
            AutoSize = true
        };
        tab.Controls.Add(_allowElevatedCheckBox);
        yPos += 30;

        // Warning label
        var warningLabel = new Label
        {
            Text = "⚠️ Warning: Enabling elevated process access may pose security risks.",
            Location = new Point(40, yPos),
            Size = new Size(400, 20),
            ForeColor = Color.DarkRed,
            AutoSize = true
        };
        tab.Controls.Add(warningLabel);
        yPos += 40;

        // Agentic Mode Info
        var agenticInfoLabel = new Label
        {
            Text = "When Agentic Mode is disabled, you will be prompted before each API operation.",
            Location = new Point(20, yPos),
            Size = new Size(420, 40),
            AutoSize = false
        };
        tab.Controls.Add(agenticInfoLabel);
    }

    private void CreateAdvancedTab(TabPage tab)
    {
        var yPos = 20;

        // Max Activities
        var maxActivitiesLabel = new Label
        {
            Text = "Maximum activity history count:",
            Location = new Point(20, yPos),
            Size = new Size(200, 20),
            AutoSize = true
        };
        tab.Controls.Add(maxActivitiesLabel);

        _maxActivitiesNumericUpDown = new NumericUpDown
        {
            Location = new Point(230, yPos - 3),
            Size = new Size(80, 20),
            Minimum = 100,
            Maximum = 10000,
            Value = 1000,
            Increment = 100
        };
        tab.Controls.Add(_maxActivitiesNumericUpDown);
        yPos += 40;

        // Settings file location
        var settingsLocationLabel = new Label
        {
            Text = "Settings file location:",
            Location = new Point(20, yPos),
            Size = new Size(150, 20),
            AutoSize = true
        };
        tab.Controls.Add(settingsLocationLabel);

        var settingsPathLabel = new Label
        {
            Text = GetSettingsPath(),
            Location = new Point(20, yPos + 25),
            Size = new Size(420, 20),
            ForeColor = Color.Gray,
            AutoSize = true
        };
        tab.Controls.Add(settingsPathLabel);
        yPos += 70;

        // Application info
        var infoLabel = new Label
        {
            Text = $"Windows API MCP Server\\nVersion: 1.0.0\\nBuild: {DateTime.Now:yyyy.MM.dd}",
            Location = new Point(20, yPos),
            Size = new Size(300, 60),
            AutoSize = false
        };
        tab.Controls.Add(infoLabel);
    }

    private void LoadSettings()
    {
        _agenticModeCheckBox.Checked = _currentSettings.AgenticModeEnabled;
        _showNotificationsCheckBox.Checked = _currentSettings.ShowNotifications;
        _startWithWindowsCheckBox.Checked = _currentSettings.StartWithWindows;
        _minimizeToTrayCheckBox.Checked = _currentSettings.MinimizeToTrayOnStartup;
        _portNumericUpDown.Value = _currentSettings.Port;
        _maxActivitiesNumericUpDown.Value = _currentSettings.MaxActivityHistoryCount;
        _logLevelComboBox.SelectedItem = _currentSettings.LogLevel;
        _allowElevatedCheckBox.Checked = _currentSettings.AllowElevated;
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        // Update settings
        _currentSettings.AgenticModeEnabled = _agenticModeCheckBox.Checked;
        _currentSettings.ShowNotifications = _showNotificationsCheckBox.Checked;
        _currentSettings.StartWithWindows = _startWithWindowsCheckBox.Checked;
        _currentSettings.MinimizeToTrayOnStartup = _minimizeToTrayCheckBox.Checked;
        _currentSettings.Port = (int)_portNumericUpDown.Value;
        _currentSettings.MaxActivityHistoryCount = (int)_maxActivitiesNumericUpDown.Value;
        _currentSettings.LogLevel = _logLevelComboBox.SelectedItem?.ToString() ?? "Info";
        _currentSettings.AllowElevated = _allowElevatedCheckBox.Checked;

        // Save settings
        _settingsManager.UpdateSettings(_currentSettings);

        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to their default values?",
            "Reset Settings",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _currentSettings = new AppSettings();
            LoadSettings();
        }
    }

    private string GetSettingsPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "WinAPIMCP", "settings.json");
    }
}