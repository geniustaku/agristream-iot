// AgriStream IoT - calls the HTTP-triggered Azure Functions
// Author: Genius Mhirizhonga (ST10541838)

using System.Net.Http.Json;
using AgriStream.Web.Models;

namespace AgriStream.Web.Services;

public class FunctionsClient
{
    private readonly HttpClient _http;

    public FunctionsClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _http.BaseAddress = new Uri(configuration["Functions:BaseUrl"]!.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Add("x-functions-key", configuration["Functions:Key"]);

        // A consumption plan can take a few seconds to wake up from idle.
        _http.Timeout = TimeSpan.FromSeconds(45);
    }

    public async Task<List<TelemetryReading>> GetTelemetryAsync(string fieldId, int top, CancellationToken cancellationToken)
    {
        var readings = await _http.GetFromJsonAsync<List<TelemetryReading>>(
            $"api/telemetry/{Uri.EscapeDataString(fieldId)}?top={top}", cancellationToken);
        return readings ?? [];
    }

    public async Task SendIrrigationCommandAsync(string deviceId, string action, int minutes, string requestedBy, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("api/irrigation/commands",
            new { deviceId, action, durationMinutes = minutes, requestedBy }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
