namespace SmartWaste.Application.Reporting.Interfaces;

/// <summary>
/// Provider-independent cloud object storage service abstraction.
/// Enables decoupled file upload, deletion, and time-limited signed read URL generation
/// without leaking provider-specific SDKs or HTTP implementation details into the Application layer.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads binary content to managed cloud object storage using the specified storage key and MIME content type.
    /// </summary>
    /// <param name="storageKey">Provider-independent object key (e.g., waste-reports/{reportId}/{attachmentId}.ext).</param>
    /// <param name="content">Seekable or readable binary stream containing the file payload.</param>
    /// <param name="contentType">Canonical MIME content type (e.g., image/jpeg, image/png, image/webp).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UploadAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file object from managed cloud storage by its storage key.
    /// </summary>
    /// <param name="storageKey">Provider-independent object key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a secure, short-lived signed read URL for accessing a private cloud object.
    /// </summary>
    /// <param name="storageKey">Provider-independent object key.</param>
    /// <param name="expiresIn">Duration until the signed URL expires.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A signed, time-limited URL string allowing authorized read access.</returns>
    Task<string> GetReadUrlAsync(
        string storageKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);
}
