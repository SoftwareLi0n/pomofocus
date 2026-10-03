using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FocusPomodoro.Core.Services;

/// <summary>
/// Servicio reutilizable que protege una ventana contra bypass del usuario.
/// Bloquea teclas del sistema (Win, Alt+Tab, Alt+F4, Ctrl+Esc),
/// previene el cierre de la ventana, y recupera el foco si se pierde.
/// </summary>
public class ServicioBloqueoVentana : IDisposable
{
    private Window? _window;
    private bool _allowClose;
    private DispatcherTimer? _focusTimer;
    private bool _disposed;

    /// <summary>
    /// Pausa temporalmente la recuperación de foco (por ejemplo, al abrir un diálogo modal).
    /// </summary>
    public bool PauseFocusEnforcement { get; set; }

    // Keyboard hook
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    private LowLevelKeyboardProc? _proc;
    private IntPtr _hookID = IntPtr.Zero;

    // Constantes Win32
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_F4 = 0x73;
    private const int VK_LMENU = 0xA4;  // Left Alt
    private const int VK_RMENU = 0xA5;  // Right Alt
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;

    #region Win32 P/Invoke

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
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    #endregion

    /// <summary>
    /// Activa todas las protecciones sobre la ventana indicada.
    /// </summary>
    public void Activar(Window window)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ServicioBloqueoVentana));

        _window = window;
        _allowClose = false;

        // 1. Hook de teclado global
        InstallKeyboardHook();

        // 2. Prevenir cierre
        _window.Closing += Window_Closing;

        // 3. Timer de recuperación de foco (cada 500ms)
        _focusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _focusTimer.Tick += FocusTimer_Tick;
        _focusTimer.Start();

        // 4. Recuperar foco cuando la ventana se desactiva
        _window.Deactivated += Window_Deactivated;
    }

    /// <summary>
    /// Permite que la ventana se cierre y desactiva todas las protecciones.
    /// Llamar antes de cerrar la ventana legítimamente.
    /// </summary>
    public void PermitirCierre()
    {
        _allowClose = true;
    }

    /// <summary>
    /// Desactiva todas las protecciones sin cerrar la ventana.
    /// </summary>
    public void Desactivar()
    {
        UninstallKeyboardHook();

        if (_focusTimer != null)
        {
            _focusTimer.Stop();
            _focusTimer.Tick -= FocusTimer_Tick;
            _focusTimer = null;
        }

        if (_window != null)
        {
            _window.Closing -= Window_Closing;
            _window.Deactivated -= Window_Deactivated;
            _window = null;
        }
    }

    #region Keyboard Hook

    private void InstallKeyboardHook()
    {
        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);

        if (_hookID == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Console.WriteLine($"ServicioBloqueoVentana: SetWindowsHookEx failed. Error: {error}");
        }
    }

    private void UninstallKeyboardHook()
    {
        if (_hookID != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
        _proc = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            // Interceptar tanto WM_KEYDOWN como WM_SYSKEYDOWN
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                if (IsBlockedKey(vkCode))
                {
                    return (IntPtr)1; // Bloquear la tecla
                }
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    private bool IsBlockedKey(int vkCode)
    {
        bool altPressed = (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0
                       || (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0;
        bool ctrlPressed = (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0
                        || (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;

        switch (vkCode)
        {
            // Bloquear tecla Windows siempre
            case VK_LWIN:
            case VK_RWIN:
                return true;

            // Bloquear Alt+Tab
            case VK_TAB when altPressed:
                return true;

            // Bloquear Alt+F4
            case VK_F4 when altPressed:
                return true;

            // Bloquear Ctrl+Esc (equivalente a tecla Windows)
            case VK_ESCAPE when ctrlPressed:
                return true;
        }

        return false;
    }

    #endregion

    #region Protección de cierre y foco

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (PauseFocusEnforcement) return;
        
        // Recuperar el foco inmediatamente cuando se pierde
        RestoreFocus();
    }

    private void FocusTimer_Tick(object? sender, EventArgs e)
    {
        if (PauseFocusEnforcement) return;
        if (_window == null || !_window.IsVisible) return;

        // Verificar si nuestra ventana tiene el foco
        var windowHandle = new WindowInteropHelper(_window).Handle;
        if (windowHandle == IntPtr.Zero) return;

        var foregroundHandle = GetForegroundWindow();
        if (foregroundHandle != windowHandle)
        {
            RestoreFocus();
        }
    }

    private void RestoreFocus()
    {
        if (_window == null || !_window.IsVisible) return;

        _window.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
        {
            try
            {
                _window.Topmost = true;
                _window.Activate();
                _window.Focus();

                var handle = new WindowInteropHelper(_window).Handle;
                if (handle != IntPtr.Zero)
                {
                    SetForegroundWindow(handle);
                }
            }
            catch { }
        });
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Desactivar();
    }
}
