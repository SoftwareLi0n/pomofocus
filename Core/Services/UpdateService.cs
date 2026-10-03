using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace FocusPomodoro.Core.Services;

public class UpdateInfo
{
    public string? version { get; set; }
    public string? url { get; set; }
}

public class UpdateService
{
    private const string UpdateJsonUrl = "https://softwarelion.pe/updates/soldado/update.json";
    private static readonly HttpClient _httpClient = new HttpClient();

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(UpdateJsonUrl);
            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(response);

            if (updateInfo != null && !string.IsNullOrEmpty(updateInfo.version) && !string.IsNullOrEmpty(updateInfo.url))
            {
                if (Version.TryParse(updateInfo.version, out Version? remoteVersion))
                {
                    Version? localVersion = Assembly.GetExecutingAssembly().GetName().Version;

                    if (localVersion != null && remoteVersion > localVersion)
                    {
                        await DownloadAndInstallUpdateAsync(updateInfo.url);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking for updates: {ex.Message}");
        }
    }

    private async Task DownloadAndInstallUpdateAsync(string downloadUrl)
    {
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"Soldado_Update_{Guid.NewGuid()}.exe");
            
            var fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(tempFile, fileBytes);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = tempFile,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /FORCECLOSEAPPLICATIONS",
                UseShellExecute = true
            };

            Process.Start(processStartInfo);
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading or installing update: {ex.Message}");
        }
    }
}
