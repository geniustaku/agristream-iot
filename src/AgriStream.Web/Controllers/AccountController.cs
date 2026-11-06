// AgriStream IoT - sign in and sign out against the Azure SQL users table
// Author: Genius Mhirizhonga (ST10541838)

using System.Security.Claims;
using AgriStream.Web.Data;
using AgriStream.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Controllers;

public class AccountController(AgriStreamDbContext db, IPasswordHasher<AppUser> hasher) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == model.Username, cancellationToken);

        if (user is null || hasher.VerifyHashedPassword(user, user.Password, model.Password) == PasswordVerificationResult.Failed)
        {
            // The same message for both cases, so usernames can't be guessed from it.
            ModelState.AddModelError(string.Empty, "That username or password is not correct.");
            return View(model);
        }

        // The role goes in the signed cookie, so no server has to remember the session.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("FullName", user.FullName),
            new(ClaimTypes.Role, user.Role!.Name)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl) : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();
}
