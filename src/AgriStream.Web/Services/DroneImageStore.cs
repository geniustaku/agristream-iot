// AgriStream IoT - uploads drone media and reads back the pipeline audit trail
// Author: Genius Mhirizhonga (ST10541838)

using System.Globalization;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using AgriStream.Web.Models;

namespace AgriStream.Web.Services;

public class DroneImageStore(BlobServiceClient blobs, TableServiceClient tables)
{
    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxBytes = 25 * 1024 * 1024;

    private readonly BlobContainerClient _container = blobs.GetBlobContainerClient("drone-images");
    private readonly TableClient _audit = tables.GetTableClient("DroneImageAudit");

    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0) return "Please choose an image to upload.";
        if (file.Length > MaxBytes) return "Images must be 25 MB or smaller.";
        return AllowedTypes.Contains(file.ContentType) ? null : "Only JPG, PNG and WEBP images are accepted.";
    }

    public async Task UploadAsync(IFormFile file, string fieldId, string uploadedBy, CancellationToken cancellationToken)
    {
        // A generated name stops two drones overwriting each other's files.
        var blob = _container.GetBlobClient($"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}");

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType },
            Metadata = new Dictionary<string, string>
            {
                ["fieldId"] = fieldId,
                ["uploadedBy"] = uploadedBy,
                ["originalName"] = Path.GetFileName(file.FileName)
            }
        }, cancellationToken);
    }

    public async Task<List<DroneImage>> ListAsync(CancellationToken cancellationToken)
    {
        var audit = new Dictionary<string, TableEntity>();
        await foreach (var row in _audit.QueryAsync<TableEntity>(cancellationToken: cancellationToken))
        {
            audit[row.RowKey] = row;
        }

        var images = new List<DroneImage>();
        await foreach (var item in _container.GetBlobsAsync(new GetBlobsOptions { Traits = BlobTraits.Metadata }, cancellationToken))
        {
            item.Metadata.TryGetValue("fieldId", out var fieldId);
            item.Metadata.TryGetValue("uploadedBy", out var uploadedBy);
            item.Metadata.TryGetValue("originalName", out var originalName);
            audit.TryGetValue(item.Name, out var entry);

            // The container is private, so the browser gets a short-lived read-only link.
            var url = _container.GetBlobClient(item.Name).GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));

            images.Add(new DroneImage(
                item.Name,
                originalName ?? item.Name,
                fieldId ?? "UNASSIGNED",
                uploadedBy ?? "unknown",
                item.Properties.ContentLength ?? 0,
                item.Properties.CreatedOn?.UtcDateTime ?? DateTime.MinValue,
                url.ToString(),
                entry?.GetString("ProcessingStatus") ?? "Awaiting pipeline",
                ReadProcessedTime(entry)));
        }

        return images.OrderByDescending(i => i.UploadedUtc).ToList();
    }

    // The function's table output binding stores this date as text, not as a table date.
    private static DateTime? ReadProcessedTime(TableEntity? entry)
    {
        var value = entry?["ProcessedUtc"]?.ToString();
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var processed)
            ? processed
            : null;
    }
}
