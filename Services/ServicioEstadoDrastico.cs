using System.IO;
using System.Text.Json;
using FocusPomodoro.Models;

namespace FocusPomodoro.Services;

public class ServicioEstadoDrastico
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FocusPomodoro",
        "drastic_state.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public void Save(EstadoDrastico state)
    {
        state.LastUpdated = DateTime.Now;
        try
        {
            var directory = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(StatePath, json);
        }
        catch { }
    }

    public EstadoDrastico? Load()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                var json = File.ReadAllText(StatePath);
                return JsonSerializer.Deserialize<EstadoDrastico>(json, JsonOptions);
            }
        }
        catch { }
        return null;
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(StatePath))
                File.Delete(StatePath);
        }
        catch { }
    }
}
