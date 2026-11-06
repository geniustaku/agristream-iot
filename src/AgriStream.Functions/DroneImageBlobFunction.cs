// AgriStream IoT - Function 1 of 4: Blob trigger
// Author: Genius Mhirizhonga (ST10541838)

using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgriStream.Functions;

public class DroneImageBlobFunction(ILogger<DroneImageBlobFunction> logger)
{
    // Runs whenever a drone image lands in the container. The return value goes through
    // the table output binding, so no table client code is needed to write the audit row.
    [Function("DroneImageBlobTrigger")]
    [TableOutput("DroneImageAudit", Connection = "StorageConnection")]
    public async Task<DroneImageAuditEntity> Run(
        [BlobTrigger("drone-images/{name}", Connection = "StorageConnection")] BlobClient blob,
        string name,
        CancellationToken cancellationToken)
    {
        var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;

        // Metadata is set by the MVC app at upload time. Defaults cover direct uploads.
        properties.Metadata.TryGetValue("fieldId", out var fieldId);
        properties.Metadata.TryGetValue("uploadedBy", out var uploadedBy);
        properties.Metadata.TryGetValue("originalName", out var originalName);

        logger.LogInformation("Drone image {Name} received, {Bytes} bytes, field {Field}",
            name, properties.ContentLength, fieldId);

        return new DroneImageAuditEntity
        {
            PartitionKey = string.IsNullOrWhiteSpace(fieldId) ? "UNASSIGNED" : fieldId,
            RowKey = name,
            BlobName = name,
            OriginalName = originalName ?? name,
            UploadedBy = uploadedBy ?? "unknown",
            ContentType = properties.ContentType,
            SizeBytes = properties.ContentLength,
            ProcessingStatus = "Processed",
            ProcessedUtc = DateTime.UtcNow
        };
    }
}
