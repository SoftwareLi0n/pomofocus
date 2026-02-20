using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FocusPomodoro.Models;

namespace FocusPomodoro.Services;

public class SessionService
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

    private SessionData _data = new();

    public SessionService()
    {
        LoadData();
    }

    public void AddSession(Session session)
    {
        _data.Sessions.Add(session);
        SaveData();
    }

    public void ClearAllSessions()
    {
        _data.Sessions.Clear();
        SaveData();
    }

    public List<Session> GetAllSessions()
    {
        return _data.Sessions.OrderByDescending(s => s.StartTime).ToList();
    }

    public List<Session> GetSessionsByDate(DateTime date)
    {
        return _data.Sessions
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
                var data = JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
                if (data != null)
                {
                    _data = data;
                }
            }
        }
        catch
        {
            _data = new SessionData();
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
