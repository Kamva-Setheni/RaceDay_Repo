using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>
/// Stands in for Azure Blob Storage during tests: nothing is stored, but uploads and deletes are
/// recorded so tests can check the API behaves correctly. Keeps the tests independent of Azure.
/// </summary>
public class FakeBlobStorageService : IBlobStorageService
{
    public ConcurrentBag<string> Uploaded { get; } = new();
    public ConcurrentBag<string> Deleted { get; } = new();

    public Task<string> UploadImageAsync(string containerName, string blobName, Stream content,
        string contentType, CancellationToken cancellationToken = default)
    {
        var url = $"https://fake-storage.test/{containerName}/{blobName}";
        Uploaded.Add(url);
        return Task.FromResult(url);
    }

    public Task DeleteByUrlAsync(string? blobUrl, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(blobUrl)) Deleted.Add(blobUrl);
        return Task.CompletedTask;
    }
}
