using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FocusPomodoro;

public partial class BreakWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly int _breakDurationMinutes;
    private int _remainingSeconds;
    private bool _isRunning;
    private bool _breakCompleted;
    private readonly Action? _onBreakComplete;

    public BreakWindow(int breakDurationMinutes, Action? onBreakComplete = null, Action? onSkipBreak = null)
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

    private void BreakWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
    }

    private void BreakWindow_Closed(object? sender, EventArgs e)
    {
        UnhookKeyboard();
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
        _breakCompleted = true;
        
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
