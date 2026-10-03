using System.Diagnostics;
using System.IO;

namespace SoldadoWatchdog;

internal class Program
{
    private const string TargetProcessName = "Soldado";
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FocusPomodoro");
    private static readonly string LockFilePath = Path.Combine(AppDataPath, "watchdog.lock");
    private static int _taskCheckCounter;

    static async Task Main(string[] args)
    {
        Console.WriteLine($"SoldadoWatchdog started. Monitoring {TargetProcessName}");

        EnsureDirectoryExists();
        
        // Wait 15 seconds at startup to let Windows load peacefully
        await Task.Delay(15000);

        while (true)
        {
            try
            {
                var targetProcess = FindProcessByName(TargetProcessName);
                
                if (targetProcess == null || targetProcess.HasExited)
                {
                    if (ShouldStartTarget())
                    {
                        Console.WriteLine($"{TargetProcessName} not running. Starting...");
                        StartTarget();
                        
                        // Cooldown: Wait 30 seconds after launching to give user time to accept UAC prompt
                        await Task.Delay(30000);
                        continue; // Skip the rest of the loop for this iteration
                    }
                }

                // Check scheduled tasks every ~10 iterations (30 seconds)
                _taskCheckCounter++;
                if (_taskCheckCounter >= 10)
                {
                    _taskCheckCounter = 0;
                    EnsureScheduledTasks();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            await Task.Delay(3000);
        }
    }

    private static void EnsureDirectoryExists()
    {
        if (!Directory.Exists(AppDataPath))
        {
            Directory.CreateDirectory(AppDataPath);
        }
    }

    private static Process? FindProcessByName(string name)
    {
        var processes = Process.GetProcessesByName(name);
        return processes.Length > 0 ? processes[0] : null;
    }

    private static bool ShouldStartTarget()
    {
        try
        {
            if (!File.Exists(LockFilePath))
                return true;

            var content = File.ReadAllText(LockFilePath).Trim();
            return !content.Equals("disabled", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static void StartTarget()
    {
        try
        {
            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{TargetProcessName}.exe");
            
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{exePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Console.WriteLine($"{TargetProcessName} started successfully");
            }
            else
            {
                Console.WriteLine($"Target executable not found: {exePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start {TargetProcessName}: {ex.Message}");
        }
    }

    private static void EnsureScheduledTasks()
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            EnsureScheduledTask("SoldadoAutoStart", Path.Combine(appDir, "Soldado.exe"));
            EnsureScheduledTask("SoldadoWatchdogAutoStart", Path.Combine(appDir, "SoldadoWatchdog.exe"));
        }
        catch { }
    }

    private static void EnsureScheduledTask(string taskName, string exePath)
    {
        try
        {
            var query = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/query /tn \"{taskName}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            query?.WaitForExit();

            if (query?.ExitCode != 0 && File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = $"/create /tn \"{taskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                Console.WriteLine($"Scheduled task '{taskName}' re-created.");
            }
        }
        catch { }
    }
}