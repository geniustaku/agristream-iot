// AgriStream IoT - writes sensor readings into Azure Table Storage
// Author: Genius Mhirizhonga (ST10541838)

using Azure.Data.Tables;
using AgriStream.Web.Models;

namespace AgriStream.Web.Services;

public class TelemetryStore(TableServiceClient tables)
{
    private const int SensorsPerField = 4;
    private readonly TableClient _table = tables.GetTableClient("SensorTelemetry");

    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken)
    {
        await foreach (var _ in _table.QueryAsync<TableEntity>(maxPerPage: 1, select: ["PartitionKey"], cancellationToken: cancellationToken))
        {
            return false;
        }

        return true;
    }

    // Writes one round of readings per sensor for each time step. Batches are per
    // partition because Table Storage only allows a transaction inside one partition.
    public async Task<int> WriteReadingsAsync(int rounds, TimeSpan spacing, CancellationToken cancellationToken)
    {
        var written = 0;
        var now = DateTime.UtcNow;

        foreach (var field in Farm.Fields)
        {
            var batch = new List<TableTransactionAction>();

            for (var round = 0; round < rounds; round++)
            {
                var recorded = now - (spacing * round);
                for (var sensor = 1; sensor <= SensorsPerField; sensor++)
                {
                    batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, BuildReading(field, sensor, recorded)));
                    written++;

                    if (batch.Count == 100)
                    {
                        await _table.SubmitTransactionAsync(batch, cancellationToken);
                        batch.Clear();
                    }
                }
            }

            if (batch.Count > 0)
            {
                await _table.SubmitTransactionAsync(batch, cancellationToken);
            }
        }

        return written;
    }

    private static TableEntity BuildReading(Field field, int sensor, DateTime recorded)
    {
        // Values follow a daily curve with some noise so the dashboard looks like a real field.
        var dayCurve = Math.Sin((recorded.Hour + (recorded.Minute / 60.0)) / 24 * 2 * Math.PI);
        var sensorId = $"{field.Id[^1]}-SNS-{sensor:00}";

        // Inverted ticks make the newest reading sort first within the partition.
        var rowKey = $"{DateTime.MaxValue.Ticks - recorded.Ticks:D19}_{sensorId}";

        return new TableEntity(field.Id, rowKey)
        {
            ["SensorId"] = sensorId,
            ["SoilMoisture"] = Math.Round(34 - (dayCurve * 6) + Random.Shared.NextDouble() * 3, 1),
            ["SoilTemperature"] = Math.Round(19 + (dayCurve * 4) + Random.Shared.NextDouble(), 1),
            ["AirTemperature"] = Math.Round(24 + (dayCurve * 7) + Random.Shared.NextDouble(), 1),
            ["Humidity"] = Math.Round(55 - (dayCurve * 14) + Random.Shared.NextDouble() * 4, 1),
            ["BatteryLevel"] = Random.Shared.Next(62, 100),
            ["RecordedUtc"] = DateTime.SpecifyKind(recorded, DateTimeKind.Utc)
        };
    }
}
