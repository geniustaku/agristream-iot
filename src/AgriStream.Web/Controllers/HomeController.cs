// AgriStream IoT - public landing page, error page and load test endpoints
// Author: Genius Mhirizhonga (ST10541838)

using Microsoft.AspNetCore.Mvc;

namespace AgriStream.Web.Controllers;

public class HomeController(IConfiguration configuration) : Controller
{
    public IActionResult Index() => User.Identity?.IsAuthenticated == true
        ? RedirectToAction("Index", "Dashboard")
        : RedirectToAction("Login", "Account");

    public IActionResult Error() => View();

    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "healthy", instance = HttpContext.Items["Instance"] });

    // Stands in for image analysis during the load test. It works through a grid of pixels
    // the way an NDVI crop health calculation would, so the test puts real pressure on the CPU.
    // It needs a key from the app settings, so it can't be used by the public.
    [HttpGet("/load/analyse")]
    public IActionResult Analyse(string? key)
    {
        var expected = configuration["LoadTest:Key"];
        if (string.IsNullOrEmpty(expected) || key != expected)
        {
            return NotFound();
        }

        const int size = 700;
        double total = 0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var nearInfrared = 0.5 + (0.5 * Math.Sin(x * 0.013));
                var red = 0.3 + (0.3 * Math.Cos(y * 0.017));
                total += (nearInfrared - red) / (nearInfrared + red + 0.0001);
            }
        }

        return Ok(new { ndvi = Math.Round(total / (size * size), 4), instance = HttpContext.Items["Instance"] });
    }
}
