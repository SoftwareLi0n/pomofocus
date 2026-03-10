using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    private Storyboard? _pulseStoryboard;
    private System.Windows.Shapes.Path[] _glowSegments = null!;

    private void SetArcColor(string hex)
    {
        ProgressArc.Stroke = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
    }

    private void CreateGlowSegments()
    {
        _glowSegments = [GlowSeg1, GlowSeg2, GlowSeg3, GlowSeg4];

        for (int i = 0; i < 4; i++)
        {
            double startDeg = -90 + i * 90 + 3;
            double endDeg = startDeg + 84;

            double startRad = startDeg * Math.PI / 180.0;
            double endRad = endDeg * Math.PI / 180.0;

            double x1 = ArcCenterX + ArcRadius * Math.Cos(startRad);
            double y1 = ArcCenterY + ArcRadius * Math.Sin(startRad);
            double x2 = ArcCenterX + ArcRadius * Math.Cos(endRad);
            double y2 = ArcCenterY + ArcRadius * Math.Sin(endRad);

            var figure = new PathFigure { StartPoint = new Point(x1, y1), IsClosed = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = new Point(x2, y2),
                Size = new Size(ArcRadius, ArcRadius),
                IsLargeArc = false,
                SweepDirection = SweepDirection.Clockwise
            });

            var geo = new PathGeometry();
            geo.Figures.Add(figure);
            _glowSegments[i].Data = geo;
        }
    }

    private void StartPulseAnimation()
    {
        if (_pulseStoryboard != null) return;

        if (_glowSegments == null)
            CreateGlowSegments();

        _pulseStoryboard = new Storyboard();

        var cycle = TimeSpan.FromSeconds(3.0);

        // Par A (seg 1 y 3): ondas en la primera mitad
        AddWaveAnimation(GlowSeg1, cycle, 0.0, 1.4);
        AddWaveAnimation(GlowSeg3, cycle, 0.0, 1.4);

        // Par B (seg 2 y 4): ondas en la segunda mitad
        AddWaveAnimation(GlowSeg2, cycle, 1.5, 2.9);
        AddWaveAnimation(GlowSeg4, cycle, 1.5, 2.9);

        _pulseStoryboard.Begin(this, true);
    }

    private void AddWaveAnimation(System.Windows.Shapes.Path glow, TimeSpan cycle, double onStart, double onEnd)
    {
        // Opacity: aparece y se desvanece mientras sale
        var opacity = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = cycle
        };
        if (onStart > 0)
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(onStart))));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.4, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(onStart + 0.15))));
        opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(onEnd))));
        if (onEnd < cycle.TotalSeconds)
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(cycle)));
        Storyboard.SetTarget(opacity, glow);
        Storyboard.SetTargetProperty(opacity, new PropertyPath("Opacity"));
        _pulseStoryboard!.Children.Add(opacity);

        // ScaleX: expande hacia afuera
        var scaleX = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = cycle
        };
        if (onStart > 0)
            scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(onStart))));
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(onEnd))));
        if (onEnd < cycle.TotalSeconds)
            scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(cycle)));
        Storyboard.SetTarget(scaleX, glow);
        Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.ScaleX"));
        _pulseStoryboard.Children.Add(scaleX);

        // ScaleY: igual que ScaleX
        var scaleY = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            Duration = cycle
        };
        if (onStart > 0)
            scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(onStart))));
        scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(onEnd))));
        if (onEnd < cycle.TotalSeconds)
            scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(cycle)));
        Storyboard.SetTarget(scaleY, glow);
        Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.ScaleY"));
        _pulseStoryboard.Children.Add(scaleY);
    }

    private void StopPulseAnimation()
    {
        if (_pulseStoryboard == null) return;
        _pulseStoryboard.Stop(this);
        _pulseStoryboard = null;
        if (_glowSegments != null)
            foreach (var seg in _glowSegments)
                seg.Opacity = 0;
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
            StopPulseAnimation();
            PlayPauseIcon.Text = "▶";

            TimerText.Foreground = new SolidColorBrush(Color.FromRgb(250, 200, 60));
            SetArcColor("#facc3c");
        }
        else if (_isPaused)
        {
            // Reanudar
            _timer.Start();
            _isPaused = false;
            _isRunning = true;
            StartPulseAnimation();
            PlayPauseIcon.Text = "⏸";
            if (_isBreakMode)
            {

                TimerText.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
                SetArcColor("#fbbf24");
            }
            else
            {

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

        TimerText.Foreground = Brushes.White;
        SetArcColor("#e85d04");
        StartPulseAnimation();
        UpdateTimerDisplay();
    }

    private void CompleteSession()
    {
        _timer.Stop();
        _isRunning = false;
        StopPulseAnimation();

        if (_currentSession != null)
        {
            _currentSession.EndTime = DateTime.Now;
            _currentSession.FocusedMinutes = _currentSession.FocusSegments.Sum(s => s.TotalSeconds / 60);
            _sessionService.AddSession(_currentSession);
        }

        System.Media.SystemSounds.Exclamation.Play();

        // Mostrar Smartlink 5 segundos, luego abrir BreakWindow
        var smartlink = new SmartlinkWindow(() =>
        {
            Dispatcher.Invoke(() =>
            {
                var breakWindow = new BreakWindow(_breakDurationMinutes, OnBreakComplete, _isDrastic);
                breakWindow.Show();
            });
        });
        smartlink.Show();
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
        StopPulseAnimation();

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

        StopPulseAnimation();
        PlayPauseIcon.Text = "▶";
        ResetBtn.Visibility = _isDrastic ? Visibility.Collapsed : Visibility.Visible;

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
                "No se pudo abrir la pagina de donaciones. Visita:\nhttps://softwarelion.pe/#/donaciones",
                "Donar",
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
