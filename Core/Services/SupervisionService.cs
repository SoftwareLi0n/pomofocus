using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FocusPomodoro.Core.Services;

public class SupervisionData
{
    public string DeviceId { get; set; } = string.Empty;
    public string LinkCode { get; set; } = string.Empty;
}

public class EmergencyStatusResult
{
    public int Id { get; set; }
    public int? MinutosAprobados { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public class ApiResponse<T>
{
    public bool success { get; set; }
    public string message { get; set; } = string.Empty;
    public T? result { get; set; }
}

public class RegistrarResult
{
    public string codigoVinculacion { get; set; } = string.Empty;
}

public class SupervisionService
{
    private static SupervisionService? _instance;
    public static SupervisionService Instance => _instance ??= new SupervisionService();

    private const string ApiBaseUrl = "http://localhost:3500/api";
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    private readonly string _storagePath;
    public string DeviceId { get; private set; } = string.Empty;
    public string LinkCode { get; private set; } = string.Empty;

    public SupervisionService()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusPomodoro");
        if (!Directory.Exists(appData))
        {
            Directory.CreateDirectory(appData);
        }
        _storagePath = Path.Combine(appData, "supervision.json");
        LoadLocalData();
    }

    private void LoadLocalData()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                var json = File.ReadAllText(_storagePath);
                var data = JsonSerializer.Deserialize<SupervisionData>(json);
                if (data != null && !string.IsNullOrWhiteSpace(data.DeviceId))
                {
                    DeviceId = data.DeviceId;
                    LinkCode = data.LinkCode ?? string.Empty;
                    return;
                }
            }
        }
        catch { }

        DeviceId = Guid.NewGuid().ToString();
        SaveLocalData();
    }

    private void SaveLocalData()
    {
        try
        {
            var data = new SupervisionData
            {
                DeviceId = DeviceId,
                LinkCode = LinkCode
            };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, json);
        }
        catch { }
    }

    public async Task<string> RegisterDeviceAsync()
    {
        try
        {
            var payload = new
            {
                codigoDispositivo = DeviceId,
                nombreDispositivo = Environment.MachineName,
                versionApp = "1.0.0"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/dispositivos/registrar", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<RegistrarResult>>(responseString);
                if (result?.result != null && !string.IsNullOrWhiteSpace(result.result.codigoVinculacion))
                {
                    LinkCode = result.result.codigoVinculacion;
                    SaveLocalData();
                    return LinkCode;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SupervisionService] Error registrando dispositivo: {ex.Message}");
        }

        return LinkCode;
    }

    public async Task SendHeartbeatAsync(string estado)
    {
        try
        {
            var payload = new
            {
                codigoDispositivo = DeviceId,
                estado = estado
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"{ApiBaseUrl}/dispositivos/heartbeat", content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SupervisionService] Error enviando heartbeat: {ex.Message}");
        }
    }

    public async Task<bool> RequestEmergencyAsync(string motivo, int minutosSolicitados)
    {
        try
        {
            var payload = new
            {
                codigoDispositivo = DeviceId,
                motivo = motivo,
                minutosSolicitados = minutosSolicitados
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{ApiBaseUrl}/emergencia/solicitar", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SupervisionService] Error solicitando emergencia: {ex.Message}");
            return false;
        }
    }

    public async Task<EmergencyStatusResult?> CheckEmergencyStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/emergencia/verificar/{DeviceId}");
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse<EmergencyStatusResult>>(responseString);
                return result?.result;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SupervisionService] Error verificando estado emergencia: {ex.Message}");
        }

        return null;
    }
}
