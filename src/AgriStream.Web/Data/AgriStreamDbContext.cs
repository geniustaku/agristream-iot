// AgriStream IoT - Entity Framework context for Azure SQL Database
// Author: Genius Mhirizhonga (ST10541838)

using AgriStream.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace AgriStream.Web.Data;

public class AgriStreamDbContext(DbContextOptions<AgriStreamDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(r => r.Name).IsUnique();
    }
}
