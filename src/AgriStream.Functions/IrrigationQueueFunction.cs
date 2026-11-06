// AgriStream IoT - Function 4 of 4: Queue trigger
// Author: Genius Mhirizhonga (ST10541838)

using System.Data;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

public class IrrigationQueueFunction(ILogger<IrrigationQueueFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Takes each command off the queue and updates the master device registry in Azure SQL.
    // An exception puts the message back, and after five tries it moves to the poison queue,
    // so a command is never lost because the database was briefly busy.
    [Function("ProcessIrrigationCommand")]
    public async Task Run(
        [QueueTrigger("irrigation-commands", Connection = "StorageConnection")] string message,
        CancellationToken cancellationToken)
    {
        var command = JsonSerializer.Deserialize<IrrigationCommand>(message, JsonOptions)
            ?? throw new InvalidOperationException("The queue message was empty.");

        var status = command.Action == "Start" ? "Irrigating" : "Idle";
        var summary = command.Action == "Start"
            ? $"Start for {command.DurationMinutes} min by {command.RequestedBy}"
            : $"Stop by {command.RequestedBy}";

        // Storage Queues do not promise strict ordering, so the WHERE clause makes sure an
        // older command that arrives late can never overwrite a newer one.
        const string sql = """
            UPDATE dbo.Devices
            SET OperationalStatus = @status, LastCommand = @summary, LastUpdatedUtc = @requestedUtc
            WHERE DeviceId = @deviceId AND LastUpdatedUtc <= @requestedUtc;
            """;

        await using var connection = new SqlConnection(Environment.GetEnvironmentVariable("SqlConnection"));
        await connection.OpenAsync(cancellationToken);

        await using var update = new SqlCommand(sql, connection);
        update.Parameters.AddWithValue("@status", status);
        update.Parameters.AddWithValue("@summary", summary);
        update.Parameters.AddWithValue("@deviceId", command.DeviceId);
        update.Parameters.Add("@requestedUtc", SqlDbType.DateTime2).Value = command.RequestedUtc;

        var rows = await update.ExecuteNonQueryAsync(cancellationToken);

        if (rows == 0)
        {
            logger.LogWarning("Command {Id} skipped: device {Device} is unknown or already has a newer command", command.CommandId, command.DeviceId);
            return;
        }

        logger.LogInformation("Device {Device} is now {Status}, command {Id}", command.DeviceId, status, command.CommandId);
    }
}
