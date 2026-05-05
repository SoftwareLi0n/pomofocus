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
    private readonly bool _isDrastic;
    private readonly List<Window> _blockerWindows = new();
    private readonly bool _autoStart;
    private readonly ServicioEstadoDrastico _drasticStateService = new();
    private int _saveTickCounter;
    private bool _allowClose;
    
    // Constantes del anillo de progreso
    private const double ArcCanvasSize = 190;
    private const double ArcStrokeThickness = 7;
    private const double ArcRadius = (ArcCanvasSize - ArcStrokeThickness) / 2.0;
    private const double ArcCenterX = ArcCanvasSize / 2.0;
    private const double ArcCenterY = ArcCanvasSize / 2.0;

    public VentanaDescanso(int breakDurationMinutes, Action? onBreakComplete = null, bool isDrastic = false, int? remainingSeconds = null, bool autoStart = false)
    {
        InitializeComponent();

        _breakDurationMinutes = breakDurationMinutes;
        _remainingSeconds = remainingSeconds ?? breakDurationMinutes * 60;
        _onBreakComplete = onBreakComplete;
        _isDrastic = isDrastic;
        _autoStart = autoStart;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;

        UpdateTimerDisplay();

        HookKeyboard();

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

        private void HookKeyboard()
        {
        _hookID = IntPtr.Zero;
        _proc = HookCallback;
        _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
        if (_hookID == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Console.WriteLine($"SetWindowsHookEx failed. Error code: {errorCode}");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            if (IsBlockedKey(vkCode))
            {
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

        private bool IsBlockedKey(int vkCode)
        {
            // Block Alt+Tab, Win key, etc.
            switch ((Keys)vkCode)
            {
                case Keys.LWin:
                case Keys.RWin:
                case Keys.Tab:
                    return GetKeyState(VK_LWIN) < 0 || GetKeyState(VK_RWIN) < 0;
                case Keys.Escape:
                    return GetKeyState((int)Keys.Escape) < 0;
            }
            return false;
        }

    private void UnhookKeyboard()
    {
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
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

    #region Windows API

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelKeyboardProc? _proc;
    private IntPtr _hookID = IntPtr.Zero;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private static readonly int VK_LWIN = 0x5B;
    private static readonly int VK_RWIN = 0x5C;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int vKey);

    #endregion

        #region Enums

        private enum Keys
        {
            LWin = 0x5B,
            RWin = 0x5C,
            Tab = 0x09,
            Escape = 0x1B
        }

        #endregion

    protected override void OnClosed(EventArgs e)
    {
        UnhookKeyboard();
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
        _onBreakComplete?.Invoke();
        Close();
    }
}