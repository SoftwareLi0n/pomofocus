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
    private bool _isDrastic;
    private DateTime _sessionStart;
    private Session? _currentSession;

    // Constantes del anillo de progreso
    private const double ArcCanvasSize = 190;
    private const double ArcStrokeThickness = 7;
    private const double ArcRadius = (ArcCanvasSize - ArcStrokeThickness) / 2.0;
    private const double ArcCenterX = ArcCanvasSize / 2.0;
    private const double ArcCenterY = ArcCanvasSize / 2.0;

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
        _isDrastic = settings.IsDrastic;

        if (!_isBreakMode)
        {
            _remainingSeconds = _focusDurationMinutes * 60;
        }

        var alpha = (byte)(_currentOpacity * 255);
        var color = Color.FromArgb(alpha, 13, 13, 22);
        MainBorder.Background = new SolidColorBrush(color);

        // Actualizar visibilidad de botones segun modo
        ResetBtn.Visibility = _isDrastic ? Visibility.Collapsed : Visibility.Visible;

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
        UpdateProgressArc();
    }

    private void UpdateProgressArc()
    {
        int totalSeconds = (_isBreakMode ? _breakDurationMinutes : _focusDurationMinutes) * 60;
        if (totalSeconds == 0)
        {
            ProgressArc.Data = null;
            return;
        }

        double progress = (double)_remainingSeconds / totalSeconds;
        if (progress <= 0)
        {
            ProgressArc.Data = null;
            return;
        }

        double angle = progress * 360.0;
        if (angle >= 360.0) angle = 359.99;

        double startRad = -Math.PI / 2.0;
        double endRad = startRad + angle * Math.PI / 180.0;

        double x1 = ArcCenterX + ArcRadius * Math.Cos(startRad);
        double y1 = ArcCenterY + ArcRadius * Math.Sin(startRad);
        double x2 = ArcCenterX + ArcRadius * Math.Cos(endRad);
        double y2 = ArcCenterY + ArcRadius * Math.Sin(endRad);

        var figure = new PathFigure
        {
            StartPoint = new Point(x1, y1),
            IsClosed = false
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(x2, y2),
            Size = new Size(ArcRadius, ArcRadius),
            IsLargeArc = angle > 180,
            SweepDirection = SweepDirection.Clockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ProgressArc.Data = geometry;
    }

    private void SetArcColor(string hex)
    {
        ProgressArc.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    }

    private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            // Pausar (solo en modo moderado)
            if (_isDrastic) return;

            _timer.Stop();
            _isPaused = true;
            _isRunning = false;
            PlayPauseIcon.Text = "▶";
            StatusText.Text = "PAUSED";
            TimerText.Foreground = new SolidColorBrush(Color.FromRgb(250, 200, 60));
            SetArcColor("#facc3c");
        }
        else if (_isPaused)
        {
            // Reanudar
            _timer.Start();
            _isPaused = false;
            _isRunning = true;
            PlayPauseIcon.Text = "⏸";
            if (_isBreakMode)
            {
                StatusText.Text = "BREAK";
                TimerText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                SetArcColor("#fbbf24");
            }
            else
            {
                StatusText.Text = "FOCUS";
                TimerText.Foreground = Brushes.White;
                SetArcColor("#e85d04");
            }
        }
        else
        {
            // Iniciar nueva sesion
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

        PlayPauseIcon.Text = _isDrastic ? "▶" : "⏸";
        StatusText.Text = "FOCUS";
        TimerText.Foreground = Brushes.White;
        SetArcColor("#e85d04");
        UpdateTimerDisplay();
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

        var breakWindow = new BreakWindow(_breakDurationMinutes, OnBreakComplete, _isDrastic);
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

        PlayPauseIcon.Text = "▶";
        ResetBtn.Visibility = _isDrastic ? Visibility.Collapsed : Visibility.Visible;
        StatusText.Text = "READY";
        TimerText.Foreground = Brushes.White;
        SetArcColor("#00d9ff");
        UpdateTimerDisplay();
    }

    private void ResumeBreak()
    {
        _isRunning = true;
        _isPaused = false;
        _timer.Start();

        PlayPauseIcon.Text = "⏸";
        StatusText.Text = "BREAK";
        TimerText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
        SetArcColor("#fbbf24");
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        ResetToFocusMode();
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
