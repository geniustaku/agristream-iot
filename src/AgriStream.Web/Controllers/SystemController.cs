// AgriStream IoT - live connection checks across every Azure service, Admin only
// Author: Genius Mhirizhonga (ST10541838)

using System.Diagnostics;
using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class SystemController(
    AgriStreamDbContext db,
    BlobServiceClient blobs,
    TableServiceClient tables,
    QueueServiceClient queues,
    FunctionsClient functions) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var checks = new List<HealthCheck>();
        var queue = queues.GetQueueClient("irrigation-commands");

        await RunCheck(checks, "Azure SQL Database", "Users and device registry", async () =>
            $"{await db.Users.CountAsync(cancellationToken)} users, {await db.Devices.CountAsync(cancellationToken)} devices");

        await RunCheck(checks, "Azure Blob Storage", "drone-images container", async () =>
        {
            var count = 0;
            await foreach (var _ in blobs.GetBlobContainerClient("drone-images").GetBlobsAsync(cancellationToken: cancellationToken)) count++;
            return $"{count} images stored";
        });

        await RunCheck(checks, "Azure Table Storage", "SensorTelemetry table", async () =>
        {
            var count = 0;
            await foreach (var _ in tables.GetTableClient("SensorTelemetry").QueryAsync<TableEntity>(
                e => e.PartitionKey == Farm.Fields[0].Id, maxPerPage: 100, select: ["RowKey"], cancellationToken: cancellationToken))
            {
                if (++count >= 100) break;
            }
            return count >= 100 ? "100+ readings in FIELD-A" : $"{count} readings in FIELD-A";
        });

        await RunCheck(checks, "Azure Storage Queue", "irrigation-commands", async () =>
            $"{(await queue.GetPropertiesAsync(cancellationToken)).Value.ApproximateMessagesCount} messages waiting");

        await RunCheck(checks, "Azure Functions", "GetLatestTelemetry over HTTP", async () =>
            $"{(await functions.GetTelemetryAsync(Farm.Fields[0].Id, 1, cancellationToken)).Count} reading returned");

        return View(new SystemViewModel
        {
            Checks = checks,
            Instance = HttpContext.Items["Instance"]?.ToString() ?? string.Empty,
            Region = Environment.GetEnvironmentVariable("REGION_NAME") ?? "Local"
        });
    }

    private static async Task RunCheck(List<HealthCheck> checks, string service, string target, Func<Task<string>> action)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            var detail = await action();
            checks.Add(new HealthCheck(service, $"{target}: {detail}", true, timer.ElapsedMilliseconds));
        }
        catch (Exception exception)
        {
            checks.Add(new HealthCheck(service, $"{target}: {exception.GetType().Name}", false, timer.ElapsedMilliseconds));
        }
    }
}
