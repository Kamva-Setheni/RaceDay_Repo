namespace RaceDay.Api.Services;

/// <summary>Raised when Azure Blob Storage cannot be reached or rejects a request.</summary>
public class StorageUnavailableException : Exception
{
    public StorageUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// All file handling goes through the API. The MVC application never talks to Azure directly.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>Uploads an image and returns the public URL of the stored blob.</summary>
    Task<string> UploadImageAsync(string containerName, string blobName, Stream content,
        string contentType, CancellationToken cancellationToken = default);

    /// <summary>Deletes the blob behind a URL previously returned by UploadImageAsync (best effort).</summary>
    Task DeleteByUrlAsync(string? blobUrl, CancellationToken cancellationToken = default);
}
