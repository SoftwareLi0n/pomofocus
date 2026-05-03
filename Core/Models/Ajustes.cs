namespace FocusPomodoro.Core.Models;

public class AjustesApp
{
    public int FocusMinutes { get; set; } = 50;
    public int BreakMinutes { get; set; } = 10;
    public double Opacity { get; set; } = 0.75;
    public bool IsDrastic { get; set; } = false;
    public TimeSpan WorkStartTime { get; set; } = new TimeSpan(7, 0, 0);
    public TimeSpan WorkEndTime { get; set; } = new TimeSpan(18, 30, 0);
}
