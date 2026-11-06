// AgriStream IoT - Management web application
// Author: Genius Mhirizhonga (ST10541838)
// Module: CLDV7112 - Cloud Development B

using System.Globalization;
using AgriStream.Web.Data;
using AgriStream.Web.Models;
using AgriStream.Web.Services;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Keeps number and date handling the same on a local machine and on Linux in Azure.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

// Both values come from App Service settings in Azure and are never committed.
var sqlConnection = builder.Configuration.GetConnectionString("SqlDatabase");
var storageConnection = builder.Configuration.GetConnectionString("AzureStorage");

if (string.IsNullOrWhiteSpace(sqlConnection) || string.IsNullOrWhiteSpace(storageConnection))
{
    throw new InvalidOperationException("The SqlDatabase and AzureStorage connection strings must both be set.");
}

builder.Services.AddControllersWithViews();
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddDbContext<AgriStreamDbContext>(options =>
    options.UseSqlServer(sqlConnection, sql => sql.EnableRetryOnFailure(5)));

builder.Services.AddSingleton(new BlobServiceClient(storageConnection));
builder.Services.AddSingleton(new TableServiceClient(storageConnection));
builder.Services.AddSingleton(new QueueServiceClient(storageConnection));
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<DroneImageStore>();
builder.Services.AddHttpClient<FunctionsClient>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// Sign-in cookies must be readable by every instance after a scale-out,
// so the encryption keys are shared in Blob Storage instead of one server's disk.
builder.Services.AddDataProtection()
    .SetApplicationName("AgriStream")
    .PersistKeysToAzureBlobStorage(storageConnection, "data-protection", "keys.xml");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

var app = builder.Build();

// Startup work is guarded so a brief database wake-up cannot stop the site from starting.
try
{
    await DatabaseSeeder.SeedAsync(app.Services, app.Configuration, app.Logger);

    var telemetry = app.Services.GetRequiredService<TelemetryStore>();
    if (await telemetry.IsEmptyAsync(CancellationToken.None))
    {
        await telemetry.WriteReadingsAsync(48, TimeSpan.FromMinutes(30), CancellationToken.None);
    }
}
catch (Exception exception)
{
    app.Logger.LogError(exception, "Startup seeding did not complete");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Shows which instance answered, which is how the load test proves that scale-out happened.
var instance = (Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName);
instance = instance[..Math.Min(8, instance.Length)];
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Served-By"] = instance;
    context.Items["Instance"] = instance;
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
