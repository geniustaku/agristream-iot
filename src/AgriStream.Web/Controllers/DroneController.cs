// AgriStream IoT - drone media uploads to Azure Blob Storage
// Author: Genius Mhirizhonga (ST10541838)

using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = $"{Roles.Agronomist},{Roles.Admin}")]
public class DroneController(DroneImageStore images) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await images.ListAsync(cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, string? fieldId, CancellationToken cancellationToken)
    {
        var problem = DroneImageStore.Validate(file);
        if (problem is not null)
        {
            TempData["Error"] = problem;
            return RedirectToAction(nameof(Index));
        }

        await images.UploadAsync(file!, Farm.Find(fieldId).Id, User.Identity!.Name!, cancellationToken);
        TempData["Message"] = "Image uploaded. The blob trigger will add its audit entry shortly.";
        return RedirectToAction(nameof(Index));
    }
}
