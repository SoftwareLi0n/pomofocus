using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FocusPomodoro.Models;
using FocusPomodoro.Services;

namespace FocusPomodoro;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly SessionService _sessionService;
    private readonly SettingsService _settingsService;
    private int _remainingSeconds;
    private int _focusDurationMinutes;
    private int _breakDurationMinutes;
    private double _currentOpacity = 0.75;
    private bool _isRunning;
    private bool _isPaused;
    private bool _isBreakMode;
    private DateTime _sessionStart;
    private Session? _currentSession;

    public MainWindow()
    {
        InitializeComponent();
        _sessionService = new SessionService();
        _settingsService = new SettingsService();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        ApplySettings(_settingsService.Settings);
        UpdateTimerDisplay();
    }

    private void ApplySettings(AppSettings settings)
    {
        _focusDurationMinutes = settings.FocusMinutes;
        _breakDurationMinutes = settings.BreakMinutes;
        _currentOpacity = settings.Opacity;
        
        if (!_isBreakMode)
        {
            _remainingSeconds = _focusDurationMinutes * 60;
        }
        
        var alpha = (byte)(_currentOpacity * 255);
        var color = Color.FromArgb(alpha, 26, 26, 46);
        MainBorder.Background = new SolidColorBrush(color);
        
        UpdateTimerDisplay();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
            UpdateTimerDisplay();
        }
        else
        {
            if (_isBreakMode)
            {
                CompleteBreak();
            }
            else
            {
                CompleteSession();
            }
        }
    }

    private void UpdateTimerDisplay()
    {
        var minutes = _remainingSeconds / 60;
        var seconds = _remainingSeconds % 60;
        TimerText.Text = $"{minutes:D2}:{seconds:D2}";
    }

    private void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isBreakMode && _isPaused)
        {
            ResumeBreak();
        }
        else
        {
            StartNewSession();
        }
    }

    private void StartNewSession()
    {
        _isBreakMode = false;
        _remainingSeconds = _focusDurationMinutes * 60;
        _sessionStart = DateTime.Now;
        
        _currentSession = new Session
        {
            StartTime = _sessionStart,
            TotalDurationMinutes = _focusDurationMinutes
        };

        _isRunning = true;
        _isPaused = false;
        _timer.Start();

        StartBtn.IsEnabled = false;
        StatusText.Text = "Focusing...";
        TimerText.Foreground = Brushes.LightGreen;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 74, 222, 128));
    }

    private void CompleteSession()
    {
        _timer.Stop();
        _isRunning = false;
        
        if (_currentSession != null)
        {
            _currentSession.EndTime = DateTime.Now;
            _currentSession.FocusedMinutes = _currentSession.FocusSegments.Sum(s => s.TotalSeconds / 60);
            _sessionService.AddSession(_currentSession);
        }

        System.Media.SystemSounds.Exclamation.Play();
        
        var breakWindow = new BreakWindow(_breakDurationMinutes, OnBreakComplete);
        breakWindow.Show();
    }

    private void OnBreakComplete()
    {
        Dispatcher.Invoke(() =>
        {
            ResetToFocusMode();
        });
    }

    private void CompleteBreak()
    {
        _timer.Stop();
        _isRunning = false;

        System.Media.SystemSounds.Asterisk.Play();
        ResetToFocusMode();
    }

    private void ResetToFocusMode()
    {
        _isBreakMode = false;
        _isRunning = false;
        _isPaused = false;
        _remainingSeconds = _focusDurationMinutes * 60;
        _currentSession = null;

        UpdateTimerDisplay();
        StartBtn.IsEnabled = true;
        StatusText.Text = "Ready to focus";
        TimerText.Foreground = Brushes.Cyan;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255));
    }

    private void ResumeBreak()
    {
        _isRunning = true;
        _isPaused = false;
        _timer.Start();

        StartBtn.IsEnabled = false;
        StatusText.Text = "On break...";
        TimerText.Foreground = Brushes.Gold;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 251, 191, 36));
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settingsService, ApplySettings, _currentOpacity)
        {
            Owner = this,
            Topmost = true
        };
        settingsWindow.ShowDialog();
    }

    private void DonateBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://softwarelion.pe/#/donaciones",
                UseShellExecute = true
            });
        }
        catch
        {
            MessageBox.Show(
                "Could not open the donation page. Please visit:\nhttps://softwarelion.pe/#/donaciones",
                "Donate",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch
        {
        }
    }
}
