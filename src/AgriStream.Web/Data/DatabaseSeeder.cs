// AgriStream IoT - creates the schema and the sample accounts on first start
// Author: Genius Mhirizhonga (ST10541838)

using AgriStream.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgriStreamDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();

        await db.Database.EnsureCreatedAsync();

        if (!await db.Roles.AnyAsync())
        {
            db.Roles.AddRange(new Role { Name = Roles.Farmer }, new Role { Name = Roles.Agronomist }, new Role { Name = Roles.Admin });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync())
        {
            // Sample passwords come from app settings, so they are not in the public repository.
            var roles = await db.Roles.ToDictionaryAsync(r => r.Name, r => r.RoleId);
            AddUser("farmer", "Thabo Mokoena", configuration["Seed:FarmerPassword"], Roles.Farmer);
            AddUser("agronomist", "Lerato Dlamini", configuration["Seed:AgronomistPassword"], Roles.Agronomist);
            AddUser("admin", "Genius Mhirizhonga", configuration["Seed:AdminPassword"], Roles.Admin);
            await db.SaveChangesAsync();

            void AddUser(string username, string name, string? password, string role)
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    logger.LogWarning("No seed password set for {User}, account skipped", username);
                    return;
                }

                var user = new AppUser { Username = username, FullName = name, RoleId = roles[role] };
                user.Password = hasher.HashPassword(user, password);
                db.Users.Add(user);
            }
        }

        if (!await db.Devices.AnyAsync())
        {
            db.Devices.AddRange(
                new Device { DeviceId = "IRR-A01", Name = "North pivot 1", FieldId = "FIELD-A" },
                new Device { DeviceId = "IRR-A02", Name = "North pivot 2", FieldId = "FIELD-A" },
                new Device { DeviceId = "IRR-B01", Name = "River drip line", FieldId = "FIELD-B" },
                new Device { DeviceId = "IRR-B02", Name = "River booster valve", FieldId = "FIELD-B" },
                new Device { DeviceId = "IRR-C01", Name = "East sprinkler bank", FieldId = "FIELD-C" });
            await db.SaveChangesAsync();
        }
    }
}
