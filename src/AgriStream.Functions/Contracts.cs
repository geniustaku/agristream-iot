// AgriStream IoT - shared data shapes used by the four functions
// Author: Genius Mhirizhonga (ST10541838)

namespace AgriStream.Functions;

/// <summary>Row written to the DroneImageAudit table by the blob trigger.</summary>
public class DroneImageAuditEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public DateTime ProcessedUtc { get; set; }
}

/// <summary>Reading returned to the MVC dashboard by the HTTP GET function.</summary>
public record TelemetryDto(
    string FieldId,
    string SensorId,
    double SoilMoisture,
    double SoilTemperature,
    double AirTemperature,
    double Humidity,
    int BatteryLevel,
    DateTime RecordedUtc);

/// <summary>Irrigation request received by the POST function and placed on the queue.</summary>
public class IrrigationCommand
{
    public string CommandId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedUtc { get; set; }
}
