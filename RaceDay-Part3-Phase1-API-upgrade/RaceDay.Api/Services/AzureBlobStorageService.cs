using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>Stores images in Azure Blob Storage (or the Azurite emulator, depending on the connection string).</summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly Lazy<BlobServiceClient> _service;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IConfiguration config, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        _service = new Lazy<BlobServiceClient>(() =>
        {
            var connectionString = config["AzureStorage:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("AzureStorage:ConnectionString is not configured.");

            return new BlobServiceClient(connectionString);
        });
    }

    public async Task<string> UploadImageAsync(string containerName, string blobName, Stream content,
        string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var container = _service.Value.GetBlobContainerClient(containerName);

            // Public read access for blobs so the browser can display the image from its URL
            await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

            var blob = container.GetBlobClient(blobName);
            await blob.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, cancellationToken);

            return blob.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uploading {Blob} to container {Container} failed.", blobName, containerName);
            throw new StorageUnavailableException("The image storage service is not available right now.", ex);
        }
    }

    public async Task DeleteByUrlAsync(string? blobUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobUrl) || !Uri.TryCreate(blobUrl, UriKind.Absolute, out var uri))
            return;

        try
        {
            var parts = new BlobUriBuilder(uri);
            await _service.Value
                .GetBlobContainerClient(parts.BlobContainerName)
                .GetBlobClient(parts.BlobName)
                .DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // Deleting an old image is best effort and must never break the request
            _logger.LogWarning(ex, "Could not delete blob {Url}.", blobUrl);
        }
    }
}
