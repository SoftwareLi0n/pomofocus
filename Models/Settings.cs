namespace FocusPomodoro.Models;

public class AppSettings
{
    public int FocusMinutes { get; set; } = 50;
    public int BreakMinutes { get; set; } = 10;
    public double Opacity { get; set; } = 0.75;
    public bool IsDrastic { get; set; } = false;
}
