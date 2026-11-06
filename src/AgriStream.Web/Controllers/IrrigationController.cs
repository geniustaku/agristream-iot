// AgriStream IoT - device registry and irrigation commands
// Author: Genius Mhirizhonga (ST10541838)

using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize]
public class IrrigationController(AgriStreamDbContext db, FunctionsClient functions, ILogger<IrrigationController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await db.Devices.AsNoTracking().OrderBy(d => d.DeviceId).ToListAsync(cancellationToken));

    // The page polls this, so the status changes on screen once the queue trigger has run.
    public async Task<IActionResult> Status(CancellationToken cancellationToken) =>
        Json(await db.Devices.AsNoTracking()
            .OrderBy(d => d.DeviceId)
            .Select(d => new { d.DeviceId, d.OperationalStatus, d.LastCommand, updated = d.LastUpdatedUtc.ToString("HH:mm:ss") })
            .ToListAsync(cancellationToken));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = $"{Roles.Farmer},{Roles.Admin}")]
    public async Task<IActionResult> Trigger(string deviceId, string action, int minutes, CancellationToken cancellationToken)
    {
        if (action is not ("Start" or "Stop") || !await db.Devices.AnyAsync(d => d.DeviceId == deviceId, cancellationToken))
        {
            TempData["Error"] = "That irrigation command was not valid.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await functions.SendIrrigationCommandAsync(deviceId, action, Math.Clamp(minutes, 5, 240), User.Identity!.Name!, cancellationToken);
            TempData["Message"] = $"{action} command for {deviceId} placed on the queue.";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(exception, "Irrigation function call failed for {Device}", deviceId);
            TempData["Error"] = "The command could not be sent. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }
}
