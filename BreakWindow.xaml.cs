using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;

namespace FocusPomodoro;

public partial class BreakWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly int _breakDurationMinutes;
    private int _remainingSeconds;
    private bool _isRunning;
    private readonly Action? _onBreakComplete;

    public BreakWindow(int breakDurationMinutes, Action? onBreakComplete = null)
    {
        InitializeComponent();
        
        _breakDurationMinutes = breakDurationMinutes;
        _remainingSeconds = breakDurationMinutes * 60;
        _onBreakComplete = onBreakComplete;
        
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
        
        UpdateTimerDisplay();
        
        HookKeyboard();
        
        Loaded += BreakWindow_Loaded;
        Closed += BreakWindow_Closed;
    }

    private async void BreakWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        await InitializeAdAsync();
    }

    private void BreakWindow_Closed(object? sender, EventArgs e)
    {
        UnhookKeyboard();
        AdWebView?.Dispose();
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

    private void StartBreakBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
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
        
        SetButtonContent("⏸", "Pause");
        StatusText.Text = "On break... Relax";
        TimerText.Foreground = Brushes.LightGreen;
        MotivationText.Text = "Close your eyes, stretch your body, take a deep breath.";
    }

    private void PauseBreak()
    {
        _isRunning = false;
        _timer.Stop();
        
        SetButtonContent("▶", "Resume");
        StatusText.Text = "Break paused";
        TimerText.Foreground = Brushes.Gold;
    }

    private void CompleteBreak()
    {
        _timer.Stop();
        _isRunning = false;
        
        System.Media.SystemSounds.Asterisk.Play();
        
        StatusText.Text = "Break completed!";
        TimerText.Foreground = Brushes.Cyan;
        MotivationText.Text = "Great! You're ready to continue.";
        
        SetButtonContent("✓", "Continue");
        StartBreakBtn.Click -= StartBreakBtn_Click;
        StartBreakBtn.Click += (s, e) => 
        {
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
