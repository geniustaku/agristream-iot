// AgriStream IoT - user and role management, Admin only
// Author: Genius Mhirizhonga (ST10541838)

using AgriStream.Web.Data;
using AgriStream.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class UsersController(AgriStreamDbContext db, IPasswordHasher<AppUser> hasher) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await LoadListsAsync(cancellationToken);
        return View(new CreateUserViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Username == model.Username, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");
        }

        if (!ModelState.IsValid)
        {
            await LoadListsAsync(cancellationToken);
            return View(nameof(Index), model);
        }

        var user = new AppUser { Username = model.Username, FullName = model.FullName, RoleId = model.RoleId };
        user.Password = hasher.HashPassword(user, model.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        TempData["Message"] = $"User {model.Username} created.";
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadListsAsync(CancellationToken cancellationToken)
    {
        ViewBag.Users = await db.Users.AsNoTracking().Include(u => u.Role).OrderBy(u => u.Username).ToListAsync(cancellationToken);
        ViewBag.Roles = new SelectList(await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken), "RoleId", "Name");
    }
}
