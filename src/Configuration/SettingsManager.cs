using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace WinAPIMCP.Configuration;

/// <summary>
/// Manages application settings persistence and access
/// </summary>
public class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    private readonly string _settingsFilePath;
    private AppSettings _settings;
    private readonly object _lock = new object();

    /// <summary>
    /// Event fired when settings are changed
    /// </summary>
    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsManager(ILogger<SettingsManager> logger)
    {
        _logger = logger;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "WinAPIMCP");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        
        _settings = LoadSettings();
    }

    /// <summary>
    /// Gets the current settings
    /// </summary>
    public AppSettings Settings
    {
        get
        {
            lock (_lock)
            {
                return _settings.Clone();
            }
        }
    }

    /// <summary>
    /// Updates the settings and saves them to disk
    /// </summary>
    /// <param name="settings">New settings to save</param>
    public void UpdateSettings(AppSettings settings)
    {
        lock (_lock)
        {
            _settings = settings.Clone();
            SaveSettings();
            SettingsChanged?.Invoke(this, _settings.Clone());
        }
    }

    /// <summary>
    /// Gets a specific setting value
    /// </summary>
    /// <typeparam name="T">Type of the setting value</typeparam>
    /// <param name="selector">Function to select the setting from AppSettings</param>
    /// <returns>The setting value</returns>
    public T GetSetting<T>(Func<AppSettings, T> selector)
    {
        lock (_lock)
        {
            return selector(_settings);
        }
    }

    /// <summary>
    /// Updates a specific setting
    /// </summary>
    /// <typeparam name="T">Type of the setting value</typeparam>
    /// <param name="setter">Action to update the setting</param>
    public void UpdateSetting<T>(Action<AppSettings> setter)
    {
        lock (_lock)
        {
            setter(_settings);
            SaveSettings();
            SettingsChanged?.Invoke(this, _settings.Clone());
        }
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _logger.LogInformation("Settings loaded from {Path}", _settingsFilePath);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}", _settingsFilePath);
        }

        _logger.LogInformation("Using default settings");
        return new AppSettings();
    }

    private void SaveSettings()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsFilePath, json);
            _logger.LogDebug("Settings saved to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsFilePath);
        }
    }

    /// <summary>
    /// Resets settings to default values
    /// </summary>
    public void ResetToDefaults()
    {
        UpdateSettings(new AppSettings());
    }
}