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
using FocusPomodoro.Models;
using FocusPomodoro.Services;
using Microsoft.Web.WebView2.Core;

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
            // En modo drastico, no permitir saltar el descanso
            SkipBreakBtn.Visibility = Visibility.Collapsed;
        }

        Loaded += BreakWindow_Loaded;
        Closing += BreakWindow_Closing;
        Closed += BreakWindow_Closed;
    }

    private bool _allowClose;

    private void BreakWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDrastic && !_allowClose)
        {
            e.Cancel = true;
        }
    }

    private async void BreakWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        CreateBlockerWindows();
        await InitializeAdAsync();

        if (_autoStart)
        {
            StartBreak();
        }
    }

    private void BreakWindow_Closed(object? sender, EventArgs e)
    {
        UnhookKeyboard();
        AdWebView?.Dispose();
        foreach (var w in _blockerWindows)
            w.Close();
        _blockerWindows.Clear();
    }

    private void CreateBlockerWindows()
    {
        // Get the screen where the main BreakWindow is displayed
        var mainHandle = new WindowInteropHelper(this).Handle;
        var mainMonitor = MonitorFromWindow(mainHandle, 0x00000002); // MONITOR_DEFAULTTONEAREST

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
        {
            if (hMonitor == mainMonitor)
                return true; // skip the main monitor, BreakWindow already covers it

            var blocker = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                ShowInTaskbar = false,
                Background = new SolidColorBrush(Color.FromRgb(10, 10, 26)),
                Left = lprcMonitor.Left / _dpiScale,
                Top = lprcMonitor.Top / _dpiScale,
                Width = (lprcMonitor.Right - lprcMonitor.Left) / _dpiScale,
                Height = (lprcMonitor.Bottom - lprcMonitor.Top) / _dpiScale,
                AllowsTransparency = false
            };
            blocker.Show();
            _blockerWindows.Add(blocker);
            return true;
        }, IntPtr.Zero);
    }

    private double _dpiScale
    {
        get
        {
            var source = PresentationSource.FromVisual(this);
            return source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private string? _adHtmlPath;

    private async Task InitializeAdAsync()
    {
        try
        {
            // Crear archivo HTML temporal para el anuncio
            var adFolder = Path.Combine(Path.GetTempPath(), "FocusPomodoro_Ads");
            Directory.CreateDirectory(adFolder);
            _adHtmlPath = Path.Combine(adFolder, "ad.html");
            File.WriteAllText(_adHtmlPath, BuildAdHtml());

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(Path.GetTempPath(), "FocusPomodoro_WebView2"));

            await AdWebView.EnsureCoreWebView2Async(env);

            AdWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            AdWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            AdWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            AdWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            // Mapear host virtual para que el HTML tenga un origen valido
            AdWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "focuspomodoro.ads", adFolder, CoreWebView2HostResourceAccessKind.Allow);

            AdWebView.CoreWebView2.NewWindowRequested += (s, args) =>
            {
                args.Handled = true;
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            };

            AdWebView.CoreWebView2.Navigate("https://focuspomodoro.ads/ad.html");
            AdWebView.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ad init failed: {ex.Message}");
            AdWebView.Visibility = Visibility.Collapsed;
        }
    }

    private string BuildAdHtml()
    {
        return """
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8"/>
                <style>
                    * { margin: 0; padding: 0; box-sizing: border-box; }
                    body {
                        background: transparent;
                        display: flex;
                        flex-direction: column;
                        align-items: center;
                        gap: 15px;
                        width: 300px;
                        height: 780px;
                        overflow: hidden;
                    }
                    .ad-slot { width: 300px; height: 250px; }
                </style>
            </head>
            <body>
                <!-- Ad 1 -->
                <div class="ad-slot">
                    <script type="text/javascript">
                        atOptions = {
                            'key' : '1c828a2e78a57e80663a67cf56c0e77f',
                            'format' : 'iframe',
                            'height' : 250,
                            'width' : 300,
                            'params' : {}
                        };
                    </script>
                    <script type="text/javascript"
                            src="https://www.highperformanceformat.com/1c828a2e78a57e80663a67cf56c0e77f/invoke.js">
                    </script>
                </div>
                <!-- Ad 2 -->
                <div class="ad-slot">
                    <script type="text/javascript">
                        atOptions = {
                            'key' : 'fc8ca41d6cda48d37b90d4fed6c68c6c',
                            'format' : 'iframe',
                            'height' : 250,
                            'width' : 300,
                            'params' : {}
                        };
                    </script>
                    <script type="text/javascript"
                            src="https://www.highperformanceformat.com/fc8ca41d6cda48d37b90d4fed6c68c6c/invoke.js">
                    </script>
                </div>
                <!-- Ad 3 -->
                <div class="ad-slot">
                    <script type="text/javascript">
                        atOptions = {
                            'key' : '8635ef9d9765aa735ecc4c78a028d8f3',
                            'format' : 'iframe',
                            'height' : 250,
                            'width' : 300,
                            'params' : {}
                        };
                    </script>
                    <script type="text/javascript"
                            src="https://www.highperformanceformat.com/8635ef9d9765aa735ecc4c78a028d8f3/invoke.js">
                    </script>
                </div>
            </body>
            </html>
            """;
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
    }

    private void SkipBreakBtn_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _isRunning = false;
        _allowClose = true;
        _onBreakComplete?.Invoke();
        Close();
    }

    private void StartBreakBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            // En modo drastico no se puede pausar el descanso
            if (_isDrastic) return;
            PauseBreak();
        }
        else
        {
            StartBreak();
        }
    }

    private void StartBreak()
    {
        _isRunning = true;
        _timer.Start();
        
        SetButtonContent("⏸", "Pausar");
        StatusText.Text = "Descansando... Relajate";
        TimerText.Foreground = Brushes.LightGreen;
        MotivationText.Text = "Cierra los ojos, estira el cuerpo, respira profundo.";
    }

    private void PauseBreak()
    {
        _isRunning = false;
        _timer.Stop();
        
        SetButtonContent("▶", "Reanudar");
        StatusText.Text = "Descanso en pausa";
        TimerText.Foreground = Brushes.Gold;
    }

    private void CompleteBreak()
    {
        _timer.Stop();
        _isRunning = false;

        System.Media.SystemSounds.Asterisk.Play();

        StatusText.Text = "Descanso completado!";
        TimerText.Foreground = Brushes.Cyan;
        MotivationText.Text = "Excelente! Estas listo para continuar.";

        SetButtonContent("✓", "Continuar");
        StartBreakBtn.Click -= StartBreakBtn_Click;
        StartBreakBtn.Click += (s, e) =>
        {
            _allowClose = true;
            _onBreakComplete?.Invoke();
            Close();
        };
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

    #region Keyboard Hook

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private static IntPtr _hookID = IntPtr.Zero;
    private static LowLevelKeyboardProc _proc = HookCallback;

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            int vkCode = Marshal.ReadInt32(lParam);
            
            if (vkCode == 115 && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                return (IntPtr)1;
            
            if (vkCode == 9 && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                return (IntPtr)1;
            
            if (vkCode == 27 && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                return (IntPtr)1;

            // Bloquear Ctrl+Shift+Esc (abrir Task Manager)
            if (vkCode == 27 && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                return (IntPtr)1;

            if (vkCode == 91 || vkCode == 92)
                return (IntPtr)1;
            
            if (vkCode == 115)
                return (IntPtr)1;
        }
        
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private void HookKeyboard()
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule!)
        {
            _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private void UnhookKeyboard()
    {
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
    }

    #endregion
}
