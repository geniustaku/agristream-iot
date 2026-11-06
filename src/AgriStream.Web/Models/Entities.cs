// AgriStream IoT - relational entities stored in Azure SQL Database
// Author: Genius Mhirizhonga (ST10541838)

using System.ComponentModel.DataAnnotations;

namespace AgriStream.Web.Models;

public class Role
{
    public int RoleId { get; set; }

    [MaxLength(30)]
    public string Name { get; set; } = string.Empty;

    public List<AppUser> Users { get; set; } = [];
}

public class AppUser
{
    [Key]
    public int UserId { get; set; }

    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    // Holds a salted PBKDF2 hash. The plain text password is never stored.
    [MaxLength(256)]
    public string Password { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Master device registry. The queue-triggered function updates this table.</summary>
public class Device
{
    [Key, MaxLength(20)]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string FieldId { get; set; } = string.Empty;

    [MaxLength(20)]
    public string OperationalStatus { get; set; } = "Idle";

    [MaxLength(120)]
    public string? LastCommand { get; set; }

    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
