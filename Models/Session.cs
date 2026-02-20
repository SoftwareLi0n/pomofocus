namespace FocusPomodoro.Models;

public class FocusSegment
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalSeconds => (int)(EndTime - StartTime).TotalSeconds;
    public string FormattedDuration
    {
        get
        {
            var mins = TotalSeconds / 60;
            var secs = TotalSeconds % 60;
            if (secs > 0)
                return $"{mins} min {secs} seg";
            return $"{mins} min";
        }
    }
}

public class Session
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalDurationMinutes { get; set; }
    public int FocusedMinutes { get; set; }
    public List<FocusSegment> FocusSegments { get; set; } = new();
    public string? TaskName { get; set; }
}

public class SessionData
{
    public List<Session> Sessions { get; set; } = new();
}
