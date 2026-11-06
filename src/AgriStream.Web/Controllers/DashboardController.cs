// AgriStream IoT - field dashboard, reads telemetry through the HTTP GET function
// Author: Genius Mhirizhonga (ST10541838)

using System.Diagnostics;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

[Authorize]
public class DashboardController(FunctionsClient functions, TelemetryStore telemetry, ILogger<DashboardController> logger) : Controller
{
    public async Task<IActionResult> Index(string? fieldId, CancellationToken cancellationToken)
    {
        var model = new DashboardViewModel { Field = Farm.Find(fieldId) };
        var timer = Stopwatch.StartNew();

        try
        {
            model.Readings = await functions.GetTelemetryAsync(model.Field.Id, 96, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(exception, "Telemetry function call failed for {Field}", model.Field.Id);
            model.Error = "Live telemetry is not available right now. Please try again shortly.";
        }

        model.FunctionMilliseconds = timer.ElapsedMilliseconds;
        return View(model);
    }

    // Simulates a surge of sensor readings arriving at the same moment.
    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{Roles.Agronomist},{Roles.Admin}")]
    public async Task<IActionResult> SimulateBurst(string? fieldId, CancellationToken cancellationToken)
    {
        var written = await telemetry.WriteReadingsAsync(10, TimeSpan.FromSeconds(10), cancellationToken);
        TempData["Message"] = $"{written} sensor readings written to Azure Table Storage.";
        return RedirectToAction(nameof(Index), new { fieldId });
    }
}
