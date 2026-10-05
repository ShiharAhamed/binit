using System.Collections.Concurrent;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Reporting.Interfaces;

namespace SmartWaste.Tests.Reporting.Fakes;

/// <summary>
/// In-memory test double for IFileStorageService.
/// Allows unit and integration tests to verify file upload, deletion, and signed URL generation
/// deterministically without requiring live internet access or real Supabase credentials.
/// </summary>
public class FakeFileStorageService : IFileStorageService
{
    public ConcurrentDictionary<string, (byte[] Data, string ContentType)> StoredFiles { get; } = new();
    public ConcurrentBag<string> UploadedKeys { get; } = new();
    public ConcurrentBag<string> DeletedKeys { get; } = new();

    public bool SimulateUploadFailure { get; set; }
    public bool SimulateDeleteFailure { get; set; }
    public bool SimulateSignFailure { get; set; }

    public async Task UploadAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (SimulateUploadFailure)
        {
            throw new StorageServiceException("Simulated cloud storage upload failure.");
        }

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();

        StoredFiles[storageKey] = (bytes, contentType);
        UploadedKeys.Add(storageKey);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        if (SimulateDeleteFailure)
        {
            throw new StorageServiceException("Simulated cloud storage delete failure.");
        }

        StoredFiles.TryRemove(storageKey, out _);
        DeletedKeys.Add(storageKey);
        return Task.CompletedTask;
    }

    public Task<string> GetReadUrlAsync(
        string storageKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        if (SimulateSignFailure)
        {
            throw new StorageServiceException("Simulated cloud storage signed URL generation failure.");
        }

        // Return deterministic mock signed URL containing storage key and expiry token
        var url = $"https://storage.fake.local/{storageKey.TrimStart('/')}?token=mock-signed-url&expires={(int)expiresIn.TotalSeconds}";
        return Task.FromResult(url);
    }

    public void Reset()
    {
        StoredFiles.Clear();
        SimulateUploadFailure = false;
        SimulateDeleteFailure = false;
        SimulateSignFailure = false;
    }
}
