using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FocusPomodoro.Models;

namespace FocusPomodoro.Services;

public class ServicioSesion
{
    private static readonly string DataFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FocusPomodoro",
        "sessions.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private DatosSesion _data = new();

    public ServicioSesion()
    {
        LoadData();
    }

    public void AddSession(Sesion session)
    {
        _data.Sesiones.Add(session);
        SaveData();
    }

    public void ClearAllSessions()
    {
        _data.Sesiones.Clear();
        SaveData();
    }

    public List<Sesion> GetAllSessions()
    {
        return _data.Sesiones.OrderByDescending(s => s.StartTime).ToList();
    }

    public List<Sesion> GetSessionsByDate(DateTime date)
    {
        return _data.Sesiones
            .Where(s => s.StartTime.Date == date.Date)
            .OrderByDescending(s => s.StartTime)
            .ToList();
    }

    public Dictionary<DateTime, (int totalMinutes, int focusedMinutes)> GetDailySummary(int days = 7)
    {
        var summary = new Dictionary<DateTime, (int totalMinutes, int focusedMinutes)>();
        var startDate = DateTime.Today.AddDays(-days + 1);

        for (var date = startDate; date <= DateTime.Today; date = date.AddDays(1))
        {
            var sessions = GetSessionsByDate(date);
            var totalMinutes = sessions.Sum(s => s.TotalDurationMinutes);
            var focusedMinutes = sessions.Sum(s => s.FocusedMinutes);
            summary[date] = (totalMinutes, focusedMinutes);
        }

        return summary;
    }

    public string GetDataFilePath() => DataFilePath;

    private void LoadData()
    {
        try
        {
            if (File.Exists(DataFilePath))
            {
                var json = File.ReadAllText(DataFilePath);
                var data = JsonSerializer.Deserialize<DatosSesion>(json, JsonOptions);
                if (data != null)
                {
                    _data = data;
                }
            }
        }
        catch
        {
            _data = new DatosSesion();
        }
    }

    private void SaveData()
    {
        try
        {
            var directory = Path.GetDirectoryName(DataFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_data, JsonOptions);
            File.WriteAllText(DataFilePath, json);
        }
        catch
        {
        }
    }
}
