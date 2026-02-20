using System.Windows;
using System.Windows.Threading;

namespace FocusPomodoro;

public partial class AdWindow : Window
{
    private readonly DispatcherTimer _timer;
    private int _remainingSeconds = 5;
    private readonly Action? _onClose;

    public AdWindow(Action? onClose = null)
    {
        InitializeComponent();
        _onClose = onClose;
        
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        
        Loaded += AdWindow_Loaded;
    }

    private void AdWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        UpdateProgress();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        UpdateProgress();

        if (_remainingSeconds <= 0)
        {
            _timer.Stop();
            _onClose?.Invoke();
            Close();
        }
    }

    private void UpdateProgress()
    {
        CountdownText.Text = $"Cerrando en {_remainingSeconds} segundos...";
        AdProgressBar.Value = (5 - _remainingSeconds) * 20;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
    }
}
