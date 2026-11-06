// AgriStream IoT - Function 3 of 4: HTTP POST trigger
// Author: Genius Mhirizhonga (ST10541838)

using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

public class IrrigationHttpPostFunction(ILogger<IrrigationHttpPostFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedActions = ["Start", "Stop"];

    // Two outputs from one function: the HTTP reply and the queue message.
    public class CommandOutput
    {
        [QueueOutput("irrigation-commands", Connection = "StorageConnection")]
        public string? QueueMessage { get; set; }

        public required HttpResponseData Response { get; set; }
    }

    // Accepts the command and queues it straight away, so a surge of requests never
    // waits on the database. The queue trigger does the slower SQL update afterwards.
    [Function("SendIrrigationCommand")]
    public async Task<CommandOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "irrigation/commands")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        IrrigationCommand? command = null;
        try
        {
            command = await JsonSerializer.DeserializeAsync<IrrigationCommand>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Irrigation request body was not valid JSON");
        }

        if (command is null || string.IsNullOrWhiteSpace(command.DeviceId) || !AllowedActions.Contains(command.Action))
        {
            var rejected = request.CreateResponse(HttpStatusCode.BadRequest);
            await rejected.WriteStringAsync("A deviceId and an action of Start or Stop are required.", cancellationToken);
            return new CommandOutput { Response = rejected };
        }

        command.CommandId = Guid.NewGuid().ToString("N");
        command.RequestedUtc = DateTime.UtcNow;
        logger.LogInformation("Queued {Action} for {Device}, command {Id}", command.Action, command.DeviceId, command.CommandId);

        var accepted = request.CreateResponse(HttpStatusCode.Accepted);
        await accepted.WriteAsJsonAsync(new { command.CommandId, status = "Queued" }, cancellationToken);
        accepted.StatusCode = HttpStatusCode.Accepted;

        return new CommandOutput
        {
            QueueMessage = JsonSerializer.Serialize(command, JsonOptions),
            Response = accepted
        };
    }
}
