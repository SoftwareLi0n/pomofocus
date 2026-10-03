using FocusPomodoro.Core.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace FocusPomodoro;

public partial class App : Application
{
    private Mutex? _instanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Application.Current.SessionEnding += App_SessionEnding;

        if (e.Args.Length >= 2 && e.Args[0] == "--watchdog")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (int.TryParse(e.Args[1], out int targetPid))
            {
                Task.Run(() => RunWatchdog(targetPid));
            }
            else
            {
                Shutdown();
            }
            return;
        }

        // Single instance check for the main app
        _instanceMutex = new Mutex(true, "FocusPomodoro_Soldado_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // Another instance is already running
            Shutdown();
            return;
        }

        try
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during startup: {ex.Message}\n\nStacktrace:\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void App_SessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        var stateService = new ServicioEstadoDrastico();
        var state = stateService.Load();
        
        if (state != null && state.LastUpdated > DateTime.Now.AddHours(-12))
        {
            e.Cancel = true;
        }
    }

    private void RunWatchdog(int targetPid)
    {
        bool createdNew;
        using var mutex = new Mutex(true, "SoldadoInternalWatchdog_" + targetPid, out createdNew);
        if (!createdNew)
        {
            Dispatcher.Invoke(() => Shutdown());
            return;
        }

        try
        {
            using var process = Process.GetProcessById(targetPid);
            process.WaitForExit();
        }
        catch (ArgumentException) { }

        var stateService = new ServicioEstadoDrastico();
        var state = stateService.Load();
        if (state != null)
        {
            var elapsed = (int)(DateTime.Now - state.LastUpdated).TotalSeconds;
            state.RemainingSeconds = Math.Max(0, state.RemainingSeconds - elapsed);
            stateService.Save(state);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{Environment.ProcessPath!}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch { }
        }

        Dispatcher.Invoke(() => Shutdown());
    }
}

