namespace FocusPomodoro.Models;

public class DrasticState
{
    public int RemainingSeconds { get; set; }
    public DateTime LastUpdated { get; set; }
    public int FocusDurationMinutes { get; set; }
    public int BreakDurationMinutes { get; set; }
    public bool IsInBreak { get; set; }
}
