// AgriStream IoT - view models and data shapes passed between layers
// Author: Genius Mhirizhonga (ST10541838)

using System.ComponentModel.DataAnnotations;

namespace AgriStream.Web.Models;

public static class Roles
{
    public const string Farmer = "Farmer";
    public const string Agronomist = "Agronomist";
    public const string Admin = "Admin";
}

public record Field(string Id, string Name, string Crop);

public static class Farm
{
    public static readonly IReadOnlyList<Field> Fields =
    [
        new("FIELD-A", "North Block", "Maize"),
        new("FIELD-B", "River Block", "Citrus"),
        new("FIELD-C", "East Block", "Wheat")
    ];

    public static Field Find(string? id) => Fields.FirstOrDefault(f => f.Id == id) ?? Fields[0];
}

public record TelemetryReading(
    string FieldId,
    string SensorId,
    double SoilMoisture,
    double SoilTemperature,
    double AirTemperature,
    double Humidity,
    int BatteryLevel,
    DateTime RecordedUtc);

public class LoginViewModel
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class DashboardViewModel
{
    public Field Field { get; set; } = Farm.Fields[0];
    public IReadOnlyList<TelemetryReading> Readings { get; set; } = [];
    public string? Error { get; set; }
    public long FunctionMilliseconds { get; set; }
    public bool HasData => Readings.Count > 0;
    public double AverageMoisture => HasData ? Readings.Average(r => r.SoilMoisture) : 0;
    public double AverageSoilTemperature => HasData ? Readings.Average(r => r.SoilTemperature) : 0;
    public double AverageHumidity => HasData ? Readings.Average(r => r.Humidity) : 0;
    public int LowestBattery => HasData ? Readings.Min(r => r.BatteryLevel) : 0;
    public int SensorCount => Readings.Select(r => r.SensorId).Distinct().Count();
    public DateTime? LatestUtc => HasData ? Readings.Max(r => r.RecordedUtc) : null;
}

public record DroneImage(
    string BlobName,
    string OriginalName,
    string FieldId,
    string UploadedBy,
    long SizeBytes,
    DateTime UploadedUtc,
    string ImageUrl,
    string Status,
    DateTime? ProcessedUtc);

public class CreateUserViewModel
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public int RoleId { get; set; }
}

public record HealthCheck(string Service, string Detail, bool Healthy, long Milliseconds);

public class SystemViewModel
{
    public IReadOnlyList<HealthCheck> Checks { get; set; } = [];
    public string Instance { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int QueueDepth { get; set; }
}
