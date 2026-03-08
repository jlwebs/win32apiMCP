using System.Windows.Forms;
using WinAPIMCP.Models;

namespace WinAPIMCP.UI.Forms;

/// <summary>
/// Form for displaying detailed information about an activity
/// </summary>
public partial class ActivityDetailsForm : Form
{
    private readonly ActivityInfo _activity;

    public ActivityDetailsForm(ActivityInfo activity)
    {
        _activity = activity;
        InitializeComponent();
        LoadActivityDetails();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form properties
        this.Text = $"Activity Details - {_activity.Operation}";
        this.Size = new Size(600, 500);
        this.FormBorderStyle = FormBorderStyle.Sizable;
        this.StartPosition = FormStartPosition.CenterParent;
        this.ShowInTaskbar = false;
        this.MinimumSize = new Size(500, 400);

        // Create main layout
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

        // Create content area with tab control
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill
        };

        // General tab
        var generalTab = new TabPage("General");
        CreateGeneralTab(generalTab);
        tabControl.TabPages.Add(generalTab);

        // Details tab
        var detailsTab = new TabPage("Details");
        CreateDetailsTab(detailsTab);
        tabControl.TabPages.Add(detailsTab);

        // Metadata tab
        if (_activity.Metadata.Count > 0)
        {
            var metadataTab = new TabPage("Metadata");
            CreateMetadataTab(metadataTab);
            tabControl.TabPages.Add(metadataTab);
        }

        mainPanel.Controls.Add(tabControl, 0, 0);

        // Create button panel
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 50
        };

        var copyButton = new Button
        {
            Text = "Copy to Clipboard",
            Size = new Size(120, 23),
            Location = new Point(20, 15)
        };
        copyButton.Click += OnCopyClicked;

        var closeButton = new Button
        {
            Text = "Close",
            Size = new Size(75, 23),
            Location = new Point(500, 15),
            DialogResult = DialogResult.OK
        };

        buttonPanel.Controls.AddRange(new Control[] { copyButton, closeButton });
        mainPanel.Controls.Add(buttonPanel, 0, 1);

        this.Controls.Add(mainPanel);
        this.AcceptButton = closeButton;

        this.ResumeLayout(false);
    }

    private void CreateGeneralTab(TabPage tab)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
            Padding = new Padding(10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        AddDetailRow(panel, "Activity ID:", _activity.Id.ToString());
        AddDetailRow(panel, "Timestamp:", _activity.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        AddDetailRow(panel, "Type:", _activity.Type.ToString());
        AddDetailRow(panel, "Operation:", _activity.Operation);
        AddDetailRow(panel, "Status:", _activity.Status.ToString(), GetStatusColor(_activity.Status));
        AddDetailRow(panel, "Source:", _activity.Source);
        AddDetailRow(panel, "Duration:", _activity.DurationMs > 0 ? $"{_activity.DurationMs} ms" : "N/A");
        
        if (_activity.PermissionRequested)
        {
            var permissionText = _activity.PermissionGranted.HasValue 
                ? (_activity.PermissionGranted.Value ? "Granted" : "Denied")
                : "Pending";
            AddDetailRow(panel, "Permission:", permissionText);
        }

        tab.Controls.Add(panel);
    }

    private void CreateDetailsTab(TabPage tab)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(10)
        };

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Parameters label
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));   // Parameters text
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Result label
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));   // Result text
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F)); // Error label
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));   // Error text

        // Parameters
        var parametersLabel = new Label
        {
            Text = "Parameters:",
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        panel.Controls.Add(parametersLabel, 0, 0);

        var parametersTextBox = new TextBox
        {
            Text = _activity.Parameters,
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            BackColor = SystemColors.Window
        };
        panel.Controls.Add(parametersTextBox, 0, 1);

        // Result
        var resultLabel = new Label
        {
            Text = "Result:",
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        panel.Controls.Add(resultLabel, 0, 2);

        var resultTextBox = new TextBox
        {
            Text = _activity.Result,
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            BackColor = SystemColors.Window
        };
        panel.Controls.Add(resultTextBox, 0, 3);

        // Error
        var errorLabel = new Label
        {
            Text = "Error Message:",
            Dock = DockStyle.Fill,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        panel.Controls.Add(errorLabel, 0, 4);

        var errorTextBox = new TextBox
        {
            Text = _activity.ErrorMessage ?? "",
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            BackColor = string.IsNullOrEmpty(_activity.ErrorMessage) ? SystemColors.Control : Color.MistyRose
        };
        panel.Controls.Add(errorTextBox, 0, 5);

        tab.Controls.Add(panel);
    }

    private void CreateMetadataTab(TabPage tab)
    {
        var listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };

        listView.Columns.Add("Key", 200);
        listView.Columns.Add("Value", 300);

        foreach (var kvp in _activity.Metadata)
        {
            var item = new ListViewItem(kvp.Key);
            item.SubItems.Add(kvp.Value);
            listView.Items.Add(item);
        }

        tab.Controls.Add(listView);
    }

    private void AddDetailRow(TableLayoutPanel panel, string label, string value, Color? valueColor = null)
    {
        var rowIndex = panel.RowCount;
        panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25F));

        var labelControl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold)
        };
        panel.Controls.Add(labelControl, 0, rowIndex);

        var valueControl = new Label
        {
            Text = value,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = valueColor ?? SystemColors.ControlText,
            AutoEllipsis = true
        };
        panel.Controls.Add(valueControl, 1, rowIndex);
    }

    private Color GetStatusColor(ActivityStatus status)
    {
        return status switch
        {
            ActivityStatus.Completed => Color.Green,
            ActivityStatus.Failed => Color.Red,
            ActivityStatus.PermissionDenied => Color.Orange,
            ActivityStatus.InProgress => Color.Blue,
            ActivityStatus.Pending => Color.Goldenrod,
            ActivityStatus.Cancelled => Color.Gray,
            _ => SystemColors.ControlText
        };
    }

    private void LoadActivityDetails()
    {
        // Update the form title with more specific information
        var statusText = _activity.Status == ActivityStatus.Failed && !string.IsNullOrEmpty(_activity.ErrorMessage)
            ? $"({_activity.Status} - {_activity.ErrorMessage.Split('\n')[0]})"
            : $"({_activity.Status})";
        
        this.Text = $"Activity Details - {_activity.Operation} {statusText}";
    }

    private void OnCopyClicked(object? sender, EventArgs e)
    {
        var details = $"Activity Details\n" +
                     $"================\n" +
                     $"ID: {_activity.Id}\n" +
                     $"Timestamp: {_activity.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\n" +
                     $"Type: {_activity.Type}\n" +
                     $"Operation: {_activity.Operation}\n" +
                     $"Status: {_activity.Status}\n" +
                     $"Source: {_activity.Source}\n" +
                     $"Duration: {(_activity.DurationMs > 0 ? $"{_activity.DurationMs} ms" : "N/A")}\n";

        if (_activity.PermissionRequested)
        {
            var permissionText = _activity.PermissionGranted.HasValue 
                ? (_activity.PermissionGranted.Value ? "Granted" : "Denied")
                : "Pending";
            details += $"Permission: {permissionText}\n";
        }

        details += $"\nParameters:\n{_activity.Parameters}\n" +
                  $"\nResult:\n{_activity.Result}\n";

        if (!string.IsNullOrEmpty(_activity.ErrorMessage))
        {
            details += $"\nError Message:\n{_activity.ErrorMessage}\n";
        }

        if (_activity.Metadata.Count > 0)
        {
            details += "\nMetadata:\n";
            foreach (var kvp in _activity.Metadata)
            {
                details += $"  {kvp.Key}: {kvp.Value}\n";
            }
        }

        try
        {
            Clipboard.SetText(details);
            MessageBox.Show("Activity details copied to clipboard!", "Copy Successful", 
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Copy Failed", 
                          MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}