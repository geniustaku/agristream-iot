// AgriStream IoT - Function 2 of 4: HTTP GET trigger
// Author: Genius Mhirizhonga (ST10541838)

using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AgriStream.Functions;

public class TelemetryHttpGetFunction
{
    private const int DefaultTop = 48;
    private const int MaxTop = 200;

    // Called by the MVC dashboard. RowKeys start with inverted ticks, so the newest readings
    // come back first and only one partition is read, which keeps this fast at any table size.
    [Function("GetLatestTelemetry")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "telemetry/{fieldId}")] HttpRequestData request,
        string fieldId,
        [TableInput("SensorTelemetry", Connection = "StorageConnection")] TableClient table,
        CancellationToken cancellationToken)
    {
        var top = int.TryParse(System.Web.HttpUtility.ParseQueryString(request.Url.Query)["top"], out var value)
            ? Math.Clamp(value, 1, MaxTop)
            : DefaultTop;

        var readings = new List<TelemetryDto>(top);
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {fieldId}");

        await foreach (var row in table.QueryAsync<TableEntity>(filter, maxPerPage: top, cancellationToken: cancellationToken))
        {
            readings.Add(new TelemetryDto(
                row.PartitionKey,
                row.GetString("SensorId") ?? string.Empty,
                row.GetDouble("SoilMoisture") ?? 0,
                row.GetDouble("SoilTemperature") ?? 0,
                row.GetDouble("AirTemperature") ?? 0,
                row.GetDouble("Humidity") ?? 0,
                row.GetInt32("BatteryLevel") ?? 0,
                row.GetDateTime("RecordedUtc") ?? DateTime.MinValue));

            if (readings.Count >= top)
            {
                break;
            }
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(readings, cancellationToken);
        return response;
    }
}
