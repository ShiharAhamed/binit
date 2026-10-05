using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Application.Reporting.Interfaces;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Reporting.Storage;
using SmartWaste.Infrastructure.Reporting.Validation;

namespace SmartWaste.Infrastructure.Reporting.Services;

/// <summary>
/// Business service handling photographic attachment uploads and deletions for Component 1.
/// Coordinates file validation, cloud object storage, and atomic PostgreSQL persistence.
/// </summary>
public class WasteReportAttachmentService : IWasteReportAttachmentService
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB in bytes
    private const int MaxAttachmentsPerReport = 3;

    private readonly AppDbContext _db;
    private readonly IFileStorageService _fileStorageService;
    private readonly SupabaseStorageOptions _storageOptions;
    private readonly ILogger<WasteReportAttachmentService> _logger;

    public WasteReportAttachmentService(
        AppDbContext db,
        IFileStorageService fileStorageService,
        IOptions<SupabaseStorageOptions>? storageOptions,
        ILogger<WasteReportAttachmentService> logger)
    {
        _db = db;
        _fileStorageService = fileStorageService;
        _storageOptions = storageOptions?.Value ?? new SupabaseStorageOptions();
        _logger = logger;
    }

    public async Task<ReportAttachmentDto> UploadAsync(
        Guid reportId,
        Stream content,
        string? contentType,
        string? fileName,
        long length,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Role guard — only Citizen actors can upload attachments
        if (actorRole != AppRoles.Citizen)
        {
            throw new ForbiddenException("Only Citizens can upload photographic evidence.");
        }

        // 2. File size validation
        if (length <= 0)
        {
            throw new FileValidationException("Uploaded file is empty.");
        }

        if (length > MaxFileSize)
        {
            throw new FileValidationException($"Uploaded file exceeds the maximum {MaxFileSize / (1024 * 1024)} MB limit.");
        }

        // 3. Ensure seekable stream for magic-byte detection + storage upload
        Stream seekableStream = content;
        MemoryStream? memoryStream = null;
        if (!content.CanSeek)
        {
            memoryStream = new MemoryStream();
            await content.CopyToAsync(memoryStream, cancellationToken);
            if (memoryStream.Length > MaxFileSize)
            {
                throw new FileValidationException($"Uploaded file exceeds the maximum {MaxFileSize / (1024 * 1024)} MB limit.");
            }
            memoryStream.Position = 0;
            seekableStream = memoryStream;
        }

        try
        {
            // 4. Inspect magic bytes / image signature
            var headerBytes = new byte[16];
            var bytesRead = await seekableStream.ReadAsync(headerBytes.AsMemory(0, 16), cancellationToken);
            if (bytesRead < 3)
            {
                throw new FileValidationException("Uploaded file is too small to be a valid image.");
            }

            var validatedImage = ImageSignatureValidator.Validate(
                headerBytes.AsSpan(0, bytesRead),
                contentType,
                fileName);

            // Rewind stream before uploading to cloud
            seekableStream.Position = 0;

            // 5. Query WasteReport
            var report = await _db.WasteReports
                .Include(r => r.Attachments)
                .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

            if (report is null)
            {
                throw new NotFoundException($"Waste report '{reportId}' was not found.");
            }

            // 6. Ownership guard
            if (report.CitizenId != actorUserId)
            {
                throw new ForbiddenException("You can only upload attachments to your own waste reports.");
            }

            // 7. Status guard — evidence can only be added while report is in Submitted status
            if (report.Status != WasteReportStatus.Submitted)
            {
                throw new BusinessRuleConflictException(
                    $"Attachments cannot be uploaded. Current status '{report.Status}' does not allow uploading evidence. Attachments can only be added while report is in Submitted status.");
            }

            // 8. Attachment count guard — maximum 3 attachments per report
            if (report.Attachments.Count >= MaxAttachmentsPerReport)
            {
                throw new BusinessRuleConflictException(
                    $"Waste report already has the maximum of {MaxAttachmentsPerReport} attachments.");
            }

            // 9. Generate attachment ID and provider-independent storage key
            var attachmentId = Guid.NewGuid();
            var storageKey = $"waste-reports/{reportId}/{attachmentId}.{validatedImage.Extension}";

            // 10. Upload to cloud storage
            await _fileStorageService.UploadAsync(storageKey, seekableStream, validatedImage.CanonicalMime, cancellationToken);

            // 11. Persist metadata in PostgreSQL with upload compensation
            var attachment = new ReportAttachment
            {
                Id = attachmentId,
                WasteReportId = reportId,
                StorageKey = storageKey,
                FileType = validatedImage.CanonicalMime,
                CreatedAt = DateTime.UtcNow
            };

            _db.ReportAttachments.Add(attachment);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception dbEx)
            {
                // Compensation: best-effort delete from cloud storage if DB save fails
                _logger.LogError(dbEx,
                    "Database save failed after cloud upload for attachment {AttachmentId}. Executing cloud compensation delete for {StorageKey}.",
                    attachmentId, storageKey);

                try
                {
                    await _fileStorageService.DeleteAsync(storageKey, CancellationToken.None);
                }
                catch (Exception delEx)
                {
                    _logger.LogError(delEx, "Compensation delete failed for cloud object {StorageKey}.", storageKey);
                }

                throw;
            }

            // 12. Generate signed read URL for API presentation
            var expiry = TimeSpan.FromSeconds(_storageOptions.SignedUrlExpirySeconds > 0 ? _storageOptions.SignedUrlExpirySeconds : 900);
            var signedUrl = await _fileStorageService.GetReadUrlAsync(storageKey, expiry, cancellationToken);

            return new ReportAttachmentDto
            {
                Id = attachment.Id,
                WasteReportId = attachment.WasteReportId,
                FileType = attachment.FileType,
                CreatedAt = attachment.CreatedAt,
                FileUrl = signedUrl
            };
        }
        finally
        {
            if (memoryStream is not null)
            {
                await memoryStream.DisposeAsync();
            }
        }
    }

    public async Task DeleteAsync(
        Guid reportId,
        Guid attachmentId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Role guard — only Citizen actors can delete attachments
        if (actorRole != AppRoles.Citizen)
        {
            throw new ForbiddenException("Only Citizens can delete photographic evidence.");
        }

        // 2. Query report
        var report = await _db.WasteReports
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report is null)
        {
            throw new NotFoundException($"Waste report '{reportId}' was not found.");
        }

        // 3. Ownership guard
        if (report.CitizenId != actorUserId)
        {
            throw new ForbiddenException("You can only delete attachments from your own waste reports.");
        }

        // 4. Status guard — attachments can only be deleted while report is in Submitted status
        if (report.Status != WasteReportStatus.Submitted)
        {
            throw new BusinessRuleConflictException(
                $"Attachments cannot be removed. Current status '{report.Status}' does not allow deleting evidence. Attachments can only be modified while report is in Submitted status.");
        }

        // 5. Query attachment
        var attachment = await _db.ReportAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.WasteReportId == reportId, cancellationToken);

        if (attachment is null)
        {
            throw new NotFoundException($"Attachment '{attachmentId}' was not found on waste report '{reportId}'.");
        }

        // 6. Delete from cloud storage FIRST. If cloud deletion fails, DB metadata is NOT removed.
        await _fileStorageService.DeleteAsync(attachment.StorageKey, cancellationToken);

        // 7. Remove DB metadata
        _db.ReportAttachments.Remove(attachment);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
