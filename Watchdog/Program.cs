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

    static async Task Main(string[] args)
    {
        Console.WriteLine($"SoldadoWatchdog started. Monitoring {TargetProcessName}");

        EnsureDirectoryExists();

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
                    }
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
                    FileName = exePath,
                    UseShellExecute = true
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
}