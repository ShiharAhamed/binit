using SmartWaste.Application.Reporting.DTOs.Responses;

namespace SmartWaste.Application.Reporting.Interfaces;

/// <summary>
/// Application service interface for managing Waste Report photographic attachments.
/// Enforces citizen ownership, status rules, file validation, storage key generation,
/// atomic PostgreSQL persistence, and cloud storage upload/delete with compensation.
/// </summary>
public interface IWasteReportAttachmentService
{
    /// <summary>
    /// Uploads a photographic attachment to a citizen-submitted waste report.
    /// Validates actor role (Citizen only), report ownership, Submitted status,
    /// maximum count (max 3), file size (<= 5 MB), and image magic bytes.
    /// Persists ReportAttachment in PostgreSQL and returns ReportAttachmentDto with signed read URL.
    /// </summary>
    /// <param name="reportId">The waste report identifier.</param>
    /// <param name="content">The binary stream of the image file.</param>
    /// <param name="contentType">The optional client-provided content type.</param>
    /// <param name="fileName">The optional client-provided filename.</param>
    /// <param name="length">The file length in bytes.</param>
    /// <param name="actorUserId">The authenticated caller's user ID.</param>
    /// <param name="actorRole">The authenticated caller's primary role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created ReportAttachmentDto containing dynamic signed FileUrl.</returns>
    Task<ReportAttachmentDto> UploadAsync(
        Guid reportId,
        Stream content,
        string? contentType,
        string? fileName,
        long length,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an existing photographic attachment from a waste report.
    /// Validates actor role (Citizen only), report ownership, and Submitted status.
    /// Deletes the cloud storage object and removes the PostgreSQL ReportAttachment record.
    /// </summary>
    /// <param name="reportId">The waste report identifier.</param>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="actorUserId">The authenticated caller's user ID.</param>
    /// <param name="actorRole">The authenticated caller's primary role.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        Guid reportId,
        Guid attachmentId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
