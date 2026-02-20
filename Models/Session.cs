namespace FocusPomodoro.Models;

public class FocusRecord
{
    public DateTime Timestamp { get; set; }
    public int MinutesFocused { get; set; }
    public string Note { get; set; } = "";
}

public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalDurationMinutes { get; set; }
    public int FocusedMinutes { get; set; }
    public List<FocusRecord> FocusRecords { get; set; } = new();
    public string? TaskName { get; set; }
}

public class SessionData
{
    public List<Session> Sessions { get; set; } = new();
}
