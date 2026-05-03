using FocusPomodoro.Core.Services;
using System.Diagnostics;
using System.Windows;

namespace FocusPomodoro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private void RunWatchdog(int targetPid)
    {
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
                    FileName = Environment.ProcessPath!,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        Dispatcher.Invoke(() => Shutdown());
    }
}

