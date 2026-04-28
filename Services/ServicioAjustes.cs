using System.IO;
using System.Text.Json;
using FocusPomodoro.Models;

namespace FocusPomodoro.Services;

public class ServicioAjustes
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FocusPomodoro",
        "settings.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private AjustesApp _settings = new();

    public AjustesApp Settings => _settings;

    public ServicioAjustes()
    {
        LoadSettings();
    }

    public void SaveSettings(AjustesApp settings)
    {
        _settings = settings;
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AjustesApp>(json, JsonOptions);
                if (settings != null)
                {
                    _settings = settings;
                }
            }
        }
        catch
        {
            _settings = new AjustesApp();
        }
    }
}
