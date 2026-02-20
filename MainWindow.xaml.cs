using System.Collections.Generic;
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
    private bool _isDistracted;
    private bool _isBreakMode;
    private bool _sessionSaved;
    private DateTime _sessionStart;
    private DateTime _focusSegmentStart;
    private int _currentFocusMinutes;
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
        else if (_isPaused && !_isDistracted)
        {
            ResumeSession();
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
        _focusSegmentStart = DateTime.Now;
        _currentFocusMinutes = 0;
        _isDistracted = false;
        
        _currentSession = new Session
        {
            StartTime = _sessionStart,
            TotalDurationMinutes = _focusDurationMinutes
        };

        _isRunning = true;
        _isPaused = false;
        _timer.Start();

        StartBtn.IsEnabled = false;
        PauseBtn.IsEnabled = true;
        DistractedBtn.IsEnabled = true;
        DistractedBtn.Visibility = Visibility.Visible;
        RefocusBtn.Visibility = Visibility.Collapsed;
        StatusText.Text = "Concentrado...";
        TimerText.Foreground = Brushes.LightGreen;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 74, 222, 128));
    }

    private void RecordFocusSegment()
    {
        if (_currentSession == null) return;
        
        var segment = new FocusSegment
        {
            StartTime = _focusSegmentStart,
            EndTime = DateTime.Now
        };
        
        if (segment.TotalSeconds >= 10)
        {
            _currentSession.FocusSegments.Add(segment);
        }
    }

    private void ResumeSession()
    {
        _focusSegmentStart = DateTime.Now;
        _isRunning = true;
        _isPaused = false;
        _timer.Start();

        StartBtn.IsEnabled = false;
        PauseBtn.IsEnabled = true;
        DistractedBtn.IsEnabled = true;
        DistractedBtn.Visibility = Visibility.Visible;
        RefocusBtn.Visibility = Visibility.Collapsed;
        StatusText.Text = "Concentrado...";
        TimerText.Foreground = Brushes.LightGreen;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 74, 222, 128));
    }

    private void PauseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning) return;

        _isRunning = false;
        _isPaused = true;
        _timer.Stop();

        StartBtn.IsEnabled = true;
        PauseBtn.IsEnabled = false;

        if (_isBreakMode)
        {
            StartBtn.Content = "Continuar Descanso";
            StatusText.Text = "Descanso pausado";
            TimerText.Foreground = Brushes.Orange;
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 251, 191, 36));
        }
        else if (!_isDistracted)
        {
            RecordFocusSegment();
            _currentFocusMinutes = _currentSession?.FocusSegments.Sum(s => s.TotalSeconds / 60) ?? 0;
            StartBtn.Content = "Continuar";
            DistractedBtn.IsEnabled = false;
            StatusText.Text = "Pausado";
            TimerText.Foreground = Brushes.Orange;
            MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 251, 191, 36));
        }
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _isRunning = false;
        _isPaused = false;
        _isDistracted = false;
        _isBreakMode = false;
        _sessionSaved = false;
        _remainingSeconds = _focusDurationMinutes * 60;
        _currentSession = null;
        _currentFocusMinutes = 0;

        UpdateTimerDisplay();
        StartBtn.IsEnabled = true;
        StartBtn.Content = "Iniciar";
        PauseBtn.IsEnabled = false;
        DistractedBtn.IsEnabled = false;
        DistractedBtn.Visibility = Visibility.Visible;
        RefocusBtn.Visibility = Visibility.Collapsed;
        StatusText.Text = "Listo para comenzar";
        TimerText.Foreground = Brushes.Cyan;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255));
    }

    private void DistractedBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning) return;

        RecordFocusSegment();
        _currentFocusMinutes = _currentSession?.FocusSegments.Sum(s => s.TotalSeconds / 60) ?? 0;

        _timer.Stop();
        _isRunning = false;
        _isDistracted = true;
        
        DistractedBtn.Visibility = Visibility.Collapsed;
        RefocusBtn.Visibility = Visibility.Visible;
        PauseBtn.IsEnabled = false;
        StatusText.Text = $"Distraido - Concentrado: {_currentFocusMinutes} min";
        TimerText.Foreground = Brushes.Red;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 71, 87));
    }

    private void RefocusBtn_Click(object sender, RoutedEventArgs e)
    {
        _focusSegmentStart = DateTime.Now;
        _isRunning = true;
        _isDistracted = false;
        _timer.Start();

        DistractedBtn.Visibility = Visibility.Visible;
        RefocusBtn.Visibility = Visibility.Collapsed;
        PauseBtn.IsEnabled = true;
        StatusText.Text = "Concentrado...";
        TimerText.Foreground = Brushes.LightGreen;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 74, 222, 128));
    }

    private void CompleteSession()
    {
        _timer.Stop();
        _isRunning = false;
        
        if (_currentSession != null)
        {
            if (!_isDistracted)
            {
                RecordFocusSegment();
            }
            
            _currentSession.EndTime = DateTime.Now;
            _currentSession.FocusedMinutes = _currentSession.FocusSegments.Sum(s => s.TotalSeconds / 60);
            
            _sessionService.AddSession(_currentSession);
            _sessionSaved = true;
        }

        var adWindow = new AdWindow(ShowSessionCompleteMessage)
        {
            Owner = this,
            Topmost = true
        };
        adWindow.ShowDialog();
    }

    private void ShowSessionCompleteMessage()
    {
        var focusedMinutes = _currentSession?.FocusSegments.Sum(s => s.TotalSeconds / 60) ?? 0;
        var efficiency = _focusDurationMinutes > 0 ? (focusedMinutes * 100 / _focusDurationMinutes) : 0;

        var result = MessageBox.Show(
            $"Sesion completada!\n\nTiempo total: {_focusDurationMinutes} min\nConcentrado: {focusedMinutes} min\nEficiencia: {efficiency}%\n\n¿Iniciar descanso de {_breakDurationMinutes} min?",
            "Sesion Finalizada",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information
        );

        if (result == MessageBoxResult.Yes)
        {
            StartBreak();
        }
        else
        {
            ResetToFocusMode();
        }
    }

    private void StartBreak()
    {
        _isBreakMode = true;
        _isRunning = false;
        _isPaused = true;
        _isDistracted = false;
        _remainingSeconds = _breakDurationMinutes * 60;
        
        UpdateTimerDisplay();
        
        StartBtn.IsEnabled = true;
        StartBtn.Content = "Iniciar Descanso";
        PauseBtn.IsEnabled = false;
        DistractedBtn.IsEnabled = false;
        DistractedBtn.Visibility = Visibility.Collapsed;
        RefocusBtn.Visibility = Visibility.Collapsed;
        StatusText.Text = $"Tiempo de descanso: {_breakDurationMinutes} min";
        TimerText.Foreground = Brushes.Gold;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 251, 191, 36));
    }

    private void ResumeBreak()
    {
        _isRunning = true;
        _isPaused = false;
        _timer.Start();

        StartBtn.IsEnabled = false;
        PauseBtn.IsEnabled = true;
        StatusText.Text = "Descansando...";
        TimerText.Foreground = Brushes.Gold;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 251, 191, 36));
    }

    private void CompleteBreak()
    {
        _timer.Stop();
        _isRunning = false;

        MessageBox.Show(
            "Descanso completado!\n\nTiempo de volver a concentrarse.",
            "Descanso Finalizado",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

        ResetToFocusMode();
    }

    private void ResetToFocusMode()
    {
        _isBreakMode = false;
        _isRunning = false;
        _isPaused = false;
        _isDistracted = false;
        _sessionSaved = false;
        _remainingSeconds = _focusDurationMinutes * 60;
        _currentSession = null;
        _currentFocusMinutes = 0;

        UpdateTimerDisplay();
        StartBtn.IsEnabled = true;
        StartBtn.Content = "Iniciar";
        PauseBtn.IsEnabled = false;
        DistractedBtn.IsEnabled = false;
        DistractedBtn.Visibility = Visibility.Visible;
        RefocusBtn.Visibility = Visibility.Collapsed;
        StatusText.Text = "Listo para comenzar";
        TimerText.Foreground = Brushes.Cyan;
        MainBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255));
    }

    private void HistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        Session? sessionForReport = null;
        
        if (_currentSession != null && !_sessionSaved && (_isRunning || _isPaused || _isDistracted))
        {
            var segments = new List<FocusSegment>(_currentSession.FocusSegments);
            
            if (!_isDistracted && _isRunning)
            {
                var currentSegment = new FocusSegment
                {
                    StartTime = _focusSegmentStart,
                    EndTime = DateTime.Now
                };
                if (currentSegment.TotalSeconds >= 10)
                {
                    segments.Add(currentSegment);
                }
            }
            
            sessionForReport = new Session
            {
                StartTime = _currentSession.StartTime,
                TotalDurationMinutes = _focusDurationMinutes,
                FocusedMinutes = segments.Sum(s => s.TotalSeconds / 60),
                FocusSegments = segments
            };
        }
        
        var historyWindow = new HistoryWindow(_sessionService, _currentOpacity, sessionForReport)
        {
            Owner = this,
            Topmost = true
        };
        historyWindow.ShowDialog();
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

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning || _isDistracted)
        {
            var result = MessageBox.Show(
                "Hay una sesion en curso. ¿Deseas guardar el progreso antes de cerrar?",
                "Sesion en curso",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                if (_currentSession != null)
                {
                    if (!_isDistracted)
                    {
                        var finalFocusMinutes = (int)(DateTime.Now - _focusSegmentStart).TotalMinutes;
                        _currentFocusMinutes += finalFocusMinutes;
                    }
                    
                    _currentSession.EndTime = DateTime.Now;
                    _currentSession.FocusedMinutes = _currentFocusMinutes;
                    _sessionService.AddSession(_currentSession);
                }
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }

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
