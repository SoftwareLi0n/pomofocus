using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using FocusPomodoro.Core.Services;

namespace FocusPomodoro;

public partial class VentanaDescanso : Window
{
    private readonly DispatcherTimer _timer;
    private readonly int _breakDurationMinutes;
    private int _remainingSeconds;
    private bool _isRunning;
    private readonly Action? _onBreakComplete;
    private readonly Action<int>? _onEmergencyGranted;
    private DispatcherTimer? _emergencyPollTimer;
    private readonly bool _isDrastic;
    private readonly List<Window> _blockerWindows = new();
    private readonly bool _autoStart;
    private readonly ServicioEstadoDrastico _drasticStateService = new();
    private readonly ServicioBloqueoVentana _bloqueo = new();
    private int _saveTickCounter;
    private bool _allowClose;
    
    // Constantes del anillo de progreso
    private const double ArcCanvasSize = 190;
    private const double ArcStrokeThickness = 7;
    private const double ArcRadius = (ArcCanvasSize - ArcStrokeThickness) / 2.0;
    private const double ArcCenterX = ArcCanvasSize / 2.0;
    private const double ArcCenterY = ArcCanvasSize / 2.0;

    public VentanaDescanso(int breakDurationMinutes, Action? onBreakComplete = null, bool isDrastic = false, int? remainingSeconds = null, bool autoStart = false, Action<int>? onEmergencyGranted = null)
    {
        InitializeComponent();

        _breakDurationMinutes = breakDurationMinutes;
        _remainingSeconds = remainingSeconds ?? breakDurationMinutes * 60;
        _onBreakComplete = onBreakComplete;
        _onEmergencyGranted = onEmergencyGranted;
        _isDrastic = isDrastic;
        _autoStart = autoStart;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;

        UpdateTimerDisplay();

        // Activar protección de bloqueo via servicio compartido
        _bloqueo.Activar(this);

        if (_isDrastic)
        {
            var state = _drasticStateService.Load();
            if (state != null && state.IsInBreak)
            {
                _remainingSeconds = state.RemainingSeconds;
                UpdateTimerDisplay();
            }
        }

        if (_autoStart)
        {
            _timer.Start();
            _isRunning = true;
            SetButtonContent("⏸", "Pausar descanso");
            StatusText.Text = "Descanso en progreso...";
            TimerText.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#fbbf24")!;
        }

        // En modo drástico no se puede saltar ni pausar el descanso
        if (_isDrastic)
        {
            SkipBreakBtn.Visibility = Visibility.Collapsed;
            StartBreakBtn.IsEnabled = false;
            StartBreakBtn.Opacity = 0.5;
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
            UpdateTimerDisplay();

            if (_isDrastic)
            {
                _saveTickCounter++;
                if (_saveTickCounter % 5 == 0)
                {
                    var state = _drasticStateService.Load();
                    if (state != null)
                    {
                        state.RemainingSeconds = _remainingSeconds;
                        state.IsInBreak = true;
                        _drasticStateService.Save(state);
                    }
                }
            }
        }
        else
        {
            CompleteBreak();
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
        int totalSeconds = _breakDurationMinutes * 60;
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
            RotationAngle = 0.0,
            IsLargeArc = angle > 180.0,
            SweepDirection = SweepDirection.Clockwise,
            IsStroked = true
        });

        var pathGeometry = new PathGeometry();
        pathGeometry.Figures.Add(figure);
        ProgressArc.Data = pathGeometry;
    }

    private void CompleteBreak()
    {
        _timer.Stop();
        _isRunning = false;

        System.Media.SystemSounds.Asterisk.Play();

        StatusText.Text = "Descanso completado!";
        TimerText.Foreground = Brushes.Cyan;
        MotivationText.Text = "Excelente! Estas listo para continuar.";

        if (_isDrastic)
        {
            // Auto-complete in Drastic mode
            _allowClose = true;
            _bloqueo.PermitirCierre();
            _onBreakComplete?.Invoke();
            Close();
        }
        else
        {
            // Manual mode - wait for user
            SetButtonContent("✓", "Continuar");
            StartBreakBtn.Click -= StartBreakBtn_Click;
            StartBreakBtn.Click += (s, e) =>
            {
                _allowClose = true;
                _bloqueo.PermitirCierre();
                _onBreakComplete?.Invoke();
                Close();
            };
        }
    }

    private void SetButtonContent(string icon, string text)
    {
        StartBreakBtn.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock { Text = icon, Margin = new Thickness(0, 0, 10, 0), FontSize = 16 },
                new TextBlock { Text = text }
            }
        };
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift))
        {
            // Backdoor para desarrolladores
            _allowClose = true;
            _bloqueo.PermitirCierre();
            _drasticStateService.Clear();
            MessageBox.Show("Modo Desarrollo: Bloqueo Drástico Desactivado", "Soldado", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _emergencyPollTimer?.Stop();
        _bloqueo.Dispose();
        base.OnClosed(e);
    }

    private void StartBreakBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRunning)
        {
            _timer.Start();
            _isRunning = true;
            SetButtonContent("⏸", "Pausar descanso");
            StatusText.Text = "Descanso en progreso...";
            TimerText.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#fbbf24")!;
        }
        else
        {
            _timer.Stop();
            _isRunning = false;
            SetButtonContent("▶", "Reanudar descanso");
            StatusText.Text = "Descanso pausado";
            TimerText.Foreground = Brushes.Gray;
        }
    }

    private void SkipBreakBtn_Click(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        _bloqueo.PermitirCierre();
        _onBreakComplete?.Invoke();
        Close();
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var settingsService = new ServicioAjustes();
        var currentOpacity = settingsService.Settings.Opacity;
        
        var settingsWindow = new VentanaAjustes(settingsService, (s) => {}, currentOpacity)
        {
            Owner = this,
            Topmost = true
        };
        
        _bloqueo.PauseFocusEnforcement = true;
        settingsWindow.ShowDialog();
        _bloqueo.PauseFocusEnforcement = false;
    }

    private void EmergencyBtn_Click(object sender, RoutedEventArgs e)
    {
        _bloqueo.PauseFocusEnforcement = true;
        EmergencyOverlay.Visibility = Visibility.Visible;
        EmergencyStatusTxt.Visibility = Visibility.Collapsed;
        SendEmergencyBtn.IsEnabled = true;
        EmergencyMotivoTxt.Focus();
    }

    private void CancelEmergencyBtn_Click(object sender, RoutedEventArgs e)
    {
        _emergencyPollTimer?.Stop();
        EmergencyOverlay.Visibility = Visibility.Collapsed;
        _bloqueo.PauseFocusEnforcement = false;
    }

    private async void SendEmergencyBtn_Click(object sender, RoutedEventArgs e)
    {
        var motivo = EmergencyMotivoTxt.Text?.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
        {
            EmergencyStatusTxt.Text = "Por favor ingresa un motivo para la emergencia.";
            EmergencyStatusTxt.Foreground = Brushes.OrangeRed;
            EmergencyStatusTxt.Visibility = Visibility.Visible;
            return;
        }

        int requestedMinutes = 15;
        if (EmergencyMinutesCombo.SelectedItem is ComboBoxItem selectedItem && int.TryParse(selectedItem.Tag?.ToString(), out int mins))
        {
            requestedMinutes = mins;
        }

        SendEmergencyBtn.IsEnabled = false;
        EmergencyStatusTxt.Text = "Enviando solicitud y esperando al supervisor...";
        EmergencyStatusTxt.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#fbbf24")!;
        EmergencyStatusTxt.Visibility = Visibility.Visible;

        bool sent = await SupervisionService.Instance.RequestEmergencyAsync(motivo, requestedMinutes);
        if (!sent)
        {
            EmergencyStatusTxt.Text = "No se pudo contactar al servidor. Verifica la conexión.";
            EmergencyStatusTxt.Foreground = Brushes.OrangeRed;
            SendEmergencyBtn.IsEnabled = true;
            return;
        }

        _emergencyPollTimer?.Stop();
        _emergencyPollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _emergencyPollTimer.Tick += async (s, args) =>
        {
            var status = await SupervisionService.Instance.CheckEmergencyStatusAsync();
            if (status != null)
            {
                if (status.Estado.Equals("aprobado", StringComparison.OrdinalIgnoreCase))
                {
                    _emergencyPollTimer.Stop();
                    int grantedMinutes = status.MinutosAprobados ?? requestedMinutes;
                    EmergencyStatusTxt.Text = $"¡Solicitud Aprobada! Se concedieron {grantedMinutes} minutos.";
                    EmergencyStatusTxt.Foreground = Brushes.LightGreen;

                    await Task.Delay(1500);

                    _allowClose = true;
                    _bloqueo.PermitirCierre();
                    _onEmergencyGranted?.Invoke(grantedMinutes);
                    Close();
                }
                else if (status.Estado.Equals("rechazado", StringComparison.OrdinalIgnoreCase))
                {
                    _emergencyPollTimer.Stop();
                    EmergencyStatusTxt.Text = "Solicitud rechazada por el supervisor.";
                    EmergencyStatusTxt.Foreground = Brushes.OrangeRed;
                    SendEmergencyBtn.IsEnabled = true;
                }
            }
        };
        _emergencyPollTimer.Start();
    }
}