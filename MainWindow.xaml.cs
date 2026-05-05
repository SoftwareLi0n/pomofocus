using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FocusPomodoro.Core.Models;
using FocusPomodoro.Core.Services;

namespace FocusPomodoro;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly ServicioSesion _sessionService;
    private readonly ServicioAjustes _settingsService;
    private int _remainingSeconds;
    private int _focusDurationMinutes;
    private int _breakDurationMinutes;
    private double _currentOpacity = 0.75;
    private bool _isRunning;
    private bool _isPaused;
    private bool _isBreakMode;
    private bool _isDrastic;
    private DateTime _sessionStart;
    private Sesion? _currentSession;
    private readonly ServicioEstadoDrastico _drasticStateService;
    private Process? _watchdogProcess;
    private int _saveTickCounter;
    private bool _inDrasticSession;
    private Window? _blockerWindow;

    // Constantes del anillo de progreso
    private const double ArcCanvasSize = 190;
    private const double ArcStrokeThickness = 7;
    private const double ArcRadius = (ArcCanvasSize - ArcStrokeThickness) / 2.0;
    private const double ArcCenterX = ArcCanvasSize / 2.0;
    private const double ArcCenterY = ArcCanvasSize / 2.0;

    public MainWindow()
    {
        InitializeComponent();
        _sessionService = new ServicioSesion();
        _settingsService = new ServicioAjustes();
        _drasticStateService = new ServicioEstadoDrastico();
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        
        var workHoursTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        workHoursTimer.Tick += WorkHoursTimer_Tick;
        workHoursTimer.Start();
        
        var externalWatchdogTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        externalWatchdogTimer.Tick += ExternalWatchdogTimer_Tick;
        externalWatchdogTimer.Start();
        
        Closing += MainWindow_Closing;
        Loaded += MainWindow_Loaded;
        ApplySettings(_settingsService.Settings);
        UpdateTimerDisplay();
    }

    private void ExternalWatchdogTimer_Tick(object? sender, EventArgs e)
    {
        EnsureExternalWatchdogRunning();
    }

    private void EnsureExternalWatchdogRunning()
    {
        try
        {
            var watchdogProcesses = Process.GetProcessesByName("SoldadoWatchdog");
            bool isRunning = watchdogProcesses.Length > 0 && !watchdogProcesses[0].HasExited;

            if (!isRunning)
            {
                var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SoldadoWatchdog.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                    Console.WriteLine("External watchdog not running. Started.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking external watchdog: {ex.Message}");
        }
    }

    private void WorkHoursTimer_Tick(object? sender, EventArgs e)
    {
        var settings = _settingsService.Settings;
        var now = DateTime.Now.TimeOfDay;
        bool isInWorkHours = now >= settings.WorkStartTime && now <= settings.WorkEndTime;

        if (!isInWorkHours && this.IsVisible)
        {
            CheckWorkHoursAndBlockIfNeeded();
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDrastic && _inDrasticSession)
        {
            e.Cancel = true;
        }
        else
        {
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusPomodoro");
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);
                
                var lockFilePath = Path.Combine(appDataPath, "watchdog.lock");
                File.WriteAllText(lockFilePath, "disabled");
            }
            catch { }
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusPomodoro");
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);
            
            var lockFilePath = Path.Combine(appDataPath, "watchdog.lock");
            if (File.Exists(lockFilePath))
                File.Delete(lockFilePath);
        }
        catch { }

        EnsureExternalWatchdogRunning();
        
        CheckWorkHoursAndBlockIfNeeded();
        
        // Check for drastic state on startup
        var state = _drasticStateService.Load();
        if (state != null)
        {
            // Always resume drastic state if it exists and is less than 12 hours old
            // The saved state itself indicates we were in a drastic session
            if (state.LastUpdated > DateTime.Now.AddHours(-12))
            {
                _isDrastic = true;  // Ensure drastic mode is enabled
                ResumeFromDrasticState(state);
            }
            else
            {
                _drasticStateService.Clear();
            }
        }
    }

    private void CheckWorkHoursAndBlockIfNeeded()
    {
        var settings = _settingsService.Settings;
        var now = DateTime.Now.TimeOfDay;

        bool isInWorkHours = now >= settings.WorkStartTime && now <= settings.WorkEndTime;

        if (!isInWorkHours)
        {
            if (_blockerWindow != null && _blockerWindow.IsVisible)
                return;

            _blockerWindow = new Window
            {
                WindowState = WindowState.Maximized,
                WindowStyle = WindowStyle.None,
                Topmost = true,
                Background = new SolidColorBrush(Color.FromRgb(13, 13, 22))
            };

            var grid = new Grid();
            var textBlock = new TextBlock
            {
                Text = "Fuera de horario de trabajo\nVuelve a las " + settings.WorkStartTime.ToString(@"hh\:mm"),
                FontSize = 32,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 217, 255)),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            grid.Children.Add(textBlock);
            _blockerWindow.Content = grid;
            _blockerWindow.Show();
            this.Hide();
            return;
        }
        else
        {
            if (_blockerWindow != null)
            {
                _blockerWindow.Close();
                _blockerWindow = null;
            }
            if (!this.IsVisible)
                this.Show();
        }
    }

    private void ResumeFromDrasticState(EstadoDrastico state)
    {
        _focusDurationMinutes = state.FocusDurationMinutes;
        _breakDurationMinutes = state.BreakDurationMinutes;

        var elapsed = (int)(DateTime.Now - state.LastUpdated).TotalSeconds;
        var adjustedSeconds = Math.Max(0, state.RemainingSeconds - elapsed);

        // Si el tiempo ya pasó, iniciar descanso
        if (adjustedSeconds <= 0)
        {
            adjustedSeconds = 0;
        }

        if (state.IsInBreak)
        {
            if (adjustedSeconds > 0)
            {
                LaunchWatchdog();
                var breakWindow = new VentanaDescanso(_breakDurationMinutes, OnBreakComplete, true, adjustedSeconds, true);
                breakWindow.Show();
            }
            else
            {
                _drasticStateService.Clear();
                StartNewSession();
            }
        }
        else
        {
            // FOCOUS MODE: Reanudar directamente
            _isBreakMode = false;
            _remainingSeconds = adjustedSeconds;
            _isRunning = true;
            _isPaused = false;
            _timer.Start();

            // Configurar UI
            PlayPauseIcon.Text = "⏸";
            PlayPauseBtn.IsEnabled = !_isDrastic;
            TimerText.Foreground = Brushes.White;
            SetArcColor("#e85d04");
            StartPulseAnimation();
            UpdateTimerDisplay();

            // Configurar sesión drástica
            if (_isDrastic)
            {
                _inDrasticSession = true;
                CloseBtn.Visibility = Visibility.Collapsed;
                SettingsBtn.IsEnabled = false;
                SaveDrasticState(false, _remainingSeconds);
                LaunchWatchdog();
            }

            // Crear nueva sesión si no existe
            if (_currentSession == null)
            {
                _sessionStart = DateTime.Now;
                _currentSession = new Sesion
                {
                    StartTime = _sessionStart,
                    TotalDurationMinutes = _focusDurationMinutes
                };
            }
        }
    }

    private void SaveDrasticState(bool isInBreak, int remainingSeconds)
    {
        _drasticStateService.Save(new EstadoDrastico
        {
            RemainingSeconds = remainingSeconds,
            FocusDurationMinutes = _focusDurationMinutes,
            BreakDurationMinutes = _breakDurationMinutes,
            IsInBreak = isInBreak,
            LastUpdated = DateTime.Now
        });
    }

    private void LaunchWatchdog()
    {
        if (_watchdogProcess != null && !_watchdogProcess.HasExited)
            return;

        try
        {
            _watchdogProcess = Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                Arguments = $"--watchdog {Environment.ProcessId}",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch { }
    }

    private void StopWatchdog()
    {
        try
        {
            if (_watchdogProcess != null && !_watchdogProcess.HasExited)
                _watchdogProcess.Kill();
        }
        catch { }
        _watchdogProcess = null;
        _drasticStateService.Clear();
    }

    private void ApplySettings(AjustesApp settings)
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

            if (_isDrastic && _isRunning)
            {
                SaveDrasticState(false, _remainingSeconds);
            }
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

    private void StartNewSession(int? resumeRemainingSeconds = null)
    {
        _isBreakMode = false;
        _remainingSeconds = resumeRemainingSeconds ?? _focusDurationMinutes * 60;
        _sessionStart = DateTime.Now;

        _currentSession = new Sesion
        {
            StartTime = _sessionStart,
            TotalDurationMinutes = _focusDurationMinutes
        };

        _isRunning = true;
        _isPaused = false;
        _timer.Start();

        PlayPauseIcon.Text = "⏸";
        PlayPauseBtn.IsEnabled = !_isDrastic;

        // Ocultar botones en modo drastico
        if (_isDrastic)
        {
            _inDrasticSession = true;
            CloseBtn.Visibility = Visibility.Collapsed;
            SettingsBtn.IsEnabled = false;
            SaveDrasticState(false, _remainingSeconds);
            LaunchWatchdog();
        }

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
            _currentSession.FocusedMinutes = _currentSession.SegmentosEnfoque.Sum(s => s.TotalSeconds / 60);
            _sessionService.AddSession(_currentSession);
        }

        System.Media.SystemSounds.Exclamation.Play();

        if (_isDrastic)
        {
            SaveDrasticState(true, _breakDurationMinutes * 60);
        }

        // Ir directamente a la ventana de descanso
        Dispatcher.Invoke(() =>
        {
            var breakWindow = new VentanaDescanso(_breakDurationMinutes, OnBreakComplete, _isDrastic, null, _isDrastic);
            breakWindow.Show();
        });
    }

    private void OnBreakComplete()
    {
        Dispatcher.Invoke(() =>
        {
            ResetToFocusMode();
            if (_isDrastic)
            {
                StartNewSession();
            }
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
        _inDrasticSession = false;
        _remainingSeconds = _focusDurationMinutes * 60;
        _currentSession = null;

        StopWatchdog();
        StopPulseAnimation();
        PlayPauseIcon.Text = "▶";
        ResetBtn.Visibility = _isDrastic ? Visibility.Collapsed : Visibility.Visible;
        CloseBtn.Visibility = Visibility.Visible;
        SettingsBtn.IsEnabled = true;

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
        var settingsWindow = new VentanaAjustes(_settingsService, ApplySettings, _currentOpacity)
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
