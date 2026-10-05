using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Reporting.Services;
using SmartWaste.Infrastructure.Reporting.Storage;
using SmartWaste.Tests.Reporting.Fakes;
using Xunit;

namespace SmartWaste.Tests.Reporting.Services;

/// <summary>
/// Unit tests for WasteReportAttachmentService covering business invariants:
/// role enforcement, citizen ownership, status rules, file validation,
/// compensation on DB failure, and atomic metadata cleanup.
/// </summary>
public class WasteReportAttachmentServiceTests
{
    private static readonly byte[] ValidJpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
    private static readonly byte[] ValidPngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
    private static readonly byte[] ValidWebpBytes = { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38 };
    private static readonly byte[] InvalidPdfBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35, 0x0A }; // %PDF-1.5

    private static (AppDbContext Db, FakeFileStorageService Storage, WasteReportAttachmentService Service) CreateFixture()
    {
        var dbName = $"SmartWaste_AttachmentTests_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new AppDbContext(options);
        var storage = new FakeFileStorageService();
        var storageOptions = Microsoft.Extensions.Options.Options.Create(new SupabaseStorageOptions
        {
            Bucket = "waste-report-attachments",
            SignedUrlExpirySeconds = 900
        });
        var logger = new Mock<ILogger<WasteReportAttachmentService>>().Object;
        var service = new WasteReportAttachmentService(db, storage, storageOptions, logger);

        return (db, storage, service);
    }

    private static async Task<WasteReport> SeedReportAsync(
        AppDbContext db,
        Guid citizenId,
        WasteReportStatus status = WasteReportStatus.Submitted)
    {
        var report = new WasteReport
        {
            Id = Guid.NewGuid(),
            CitizenId = citizenId,
            Description = "Report for attachment testing",
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

        db.WasteReports.Add(report);
        await db.SaveChangesAsync();
        return report;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. UPLOAD SUCCESS: JPEG, PNG, WEBP & STORAGE KEY FORMAT
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("test.jpg", "image/jpeg", 0)] // JPEG
    [InlineData("evidence.png", "image/png", 1)] // PNG
    [InlineData("dump.webp", "image/webp", 2)] // WebP
    public async Task UploadAsync_ValidImageFormats_SucceedsAndGeneratesCorrectStorageKey(
        string fileName, string mimeType, int formatIndex)
    {
        var (db, storage, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        var bytes = formatIndex switch
        {
            0 => ValidJpegBytes,
            1 => ValidPngBytes,
            _ => ValidWebpBytes
        };

        using var stream = new MemoryStream(bytes);
        var result = await service.UploadAsync(
            report.Id,
            stream,
            mimeType,
            fileName,
            bytes.Length,
            citizenId,
            AppRoles.Citizen);

        // Result verification
        result.Should().NotBeNull();
        result.WasteReportId.Should().Be(report.Id);
        result.FileType.Should().Be(mimeType);
        result.FileUrl.Should().StartWith("https://storage.fake.local/waste-reports/");
        result.FileUrl.Should().Contain(report.Id.ToString());

        // Verify storage key format: waste-reports/{reportId}/{attachmentId}.{ext}
        storage.UploadedKeys.Should().HaveCount(1);
        var uploadedKey = storage.UploadedKeys.First();
        uploadedKey.Should().StartWith($"waste-reports/{report.Id}/");
        uploadedKey.Should().EndWith(formatIndex switch { 0 => ".jpg", 1 => ".png", _ => ".webp" });
        uploadedKey.Should().NotContain(fileName, "Original client filename must NOT be used in storage key");

        // Verify PostgreSQL persistence
        var dbAttachment = await db.ReportAttachments.FirstOrDefaultAsync(a => a.Id == result.Id);
        dbAttachment.Should().NotBeNull();
        dbAttachment!.WasteReportId.Should().Be(report.Id);
        dbAttachment.StorageKey.Should().Be(uploadedKey);
        dbAttachment.FileType.Should().Be(mimeType);

        // Verify invariants: report status unchanged, no status history created, UpdatedAt untouched
        var updatedReport = await db.WasteReports.FindAsync(report.Id);
        updatedReport!.Status.Should().Be(WasteReportStatus.Submitted);
        updatedReport.UpdatedAt.Should().BeNull();
        var histories = await db.WasteReportStatusHistories.Where(h => h.WasteReportId == report.Id).ToListAsync();
        histories.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. AUTHORIZATION & OWNERSHIP GUARDS
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    [InlineData(AppRoles.Driver)]
    public async Task UploadAsync_NonCitizenRole_ThrowsForbiddenException(string nonCitizenRole)
    {
        var (db, _, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        using var stream = new MemoryStream(ValidJpegBytes);
        var act = () => service.UploadAsync(
            report.Id,
            stream,
            "image/jpeg",
            "test.jpg",
            ValidJpegBytes.Length,
            citizenId,
            nonCitizenRole);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Citizens can upload photographic evidence*");
    }

    [Fact]
    public async Task UploadAsync_OtherCitizen_ThrowsForbiddenException()
    {
        var (db, _, service) = CreateFixture();
        var ownerCitizenId = Guid.NewGuid();
        var callerCitizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, ownerCitizenId);

        using var stream = new MemoryStream(ValidJpegBytes);
        var act = () => service.UploadAsync(
            report.Id,
            stream,
            "image/jpeg",
            "test.jpg",
            ValidJpegBytes.Length,
            callerCitizenId,
            AppRoles.Citizen);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*You can only upload attachments to your own waste reports*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. STATUS GUARDS (SUBMITTED ONLY)
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(WasteReportStatus.UnderReview)]
    [InlineData(WasteReportStatus.Verified)]
    [InlineData(WasteReportStatus.Rejected)]
    [InlineData(WasteReportStatus.Cancelled)]
    public async Task UploadAsync_NonSubmittedStatus_ThrowsBusinessRuleConflictException(WasteReportStatus nonSubmittedStatus)
    {
        var (db, _, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId, nonSubmittedStatus);

        using var stream = new MemoryStream(ValidJpegBytes);
        var act = () => service.UploadAsync(
            report.Id,
            stream,
            "image/jpeg",
            "test.jpg",
            ValidJpegBytes.Length,
            citizenId,
            AppRoles.Citizen);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Attachments can only be added while report is in Submitted status*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. MAXIMUM 3 ATTACHMENTS ENFORCEMENT
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_WhenReportAlreadyHas3Attachments_ThrowsConflictException()
    {
        var (db, _, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        // Add 3 attachments to report
        for (int i = 0; i < 3; i++)
        {
            db.ReportAttachments.Add(new ReportAttachment
            {
                Id = Guid.NewGuid(),
                WasteReportId = report.Id,
                StorageKey = $"waste-reports/{report.Id}/test-{i}.jpg",
                FileType = "image/jpeg",
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();

        using var stream = new MemoryStream(ValidJpegBytes);
        var act = () => service.UploadAsync(
            report.Id,
            stream,
            "image/jpeg",
            "fourth.jpg",
            ValidJpegBytes.Length,
            citizenId,
            AppRoles.Citizen);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already has the maximum of 3 attachments*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. FILE VALIDATION: SIZE, EMPTY & MAGIC BYTES
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_EmptyFile_ThrowsFileValidationException()
    {
        var (db, _, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        using var stream = new MemoryStream(Array.Empty<byte>());
        var act = () => service.UploadAsync(
            report.Id,
            stream,
            "image/jpeg",
            "empty.jpg",
            0,
            citizenId,
            AppRoles.Citizen);

        await act.Should().ThrowAsync<FileValidationException>()
            .WithMessage("*Uploaded file is empty*");
    }

    [Fact]
    public async Task UploadAsync_FileExceeds5Mb_ThrowsFileValidationException()
    {
        var (db, _, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        var largeLength = 5 * 1024 * 1024 + 1; // 5 MB + 1 byte
        using var stream = new MemoryStream(ValidJpegBytes);
        var act = () => service.UploadAsync(
            report.Id,
            stream,
            "image/jpeg",
            "large.jpg",
            largeLength,
            citizenId,
            AppRoles.Citizen);

        await act.Should().ThrowAsync<FileValidationException>()
            .WithMessage("*exceeds the maximum 5 MB limit*");
    }

    [Fact]
    public async Task UploadAsync_InvalidMagicBytes_PdfDisguisedAsJpg_ThrowsFileValidationException()
    {
        var (db, _, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        using var stream = new MemoryStream(InvalidPdfBytes);
        var act = () => service.UploadAsync(
            report.Id,
            stream,
            "image/jpeg",
            "malicious.jpg",
            InvalidPdfBytes.Length,
            citizenId,
            AppRoles.Citizen);

        await act.Should().ThrowAsync<FileValidationException>()
            .WithMessage("*Only JPEG, PNG, and WebP images are allowed*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. UPLOAD COMPENSATION (DB FAILURE DELETES CLOUD OBJECT)
    // ──────────────────────────────────────────────────────────────────────────

    private class TestSaveChangesInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public bool ShouldThrow { get; set; }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new DbUpdateException("Simulated database failure during save.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task UploadAsync_WhenDatabaseSaveFails_ExecutesCloudCompensationDelete()
    {
        var interceptor = new TestSaveChangesInterceptor();
        var dbName = $"SmartWaste_CompTest_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptor)
            .Options;

        var db = new AppDbContext(options);
        var storage = new FakeFileStorageService();
        var storageOptions = Microsoft.Extensions.Options.Options.Create(new SupabaseStorageOptions());
        var logger = new Mock<ILogger<WasteReportAttachmentService>>().Object;
        var service = new WasteReportAttachmentService(db, storage, storageOptions, logger);

        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        // Enable interceptor so that the next SaveChangesAsync (in UploadAsync) throws
        interceptor.ShouldThrow = true;

        using var stream = new MemoryStream(ValidJpegBytes);
        var act = () => service.UploadAsync(
            report.Id,
            stream,
            "image/jpeg",
            "test.jpg",
            ValidJpegBytes.Length,
            citizenId,
            AppRoles.Citizen);

        await act.Should().ThrowAsync<DbUpdateException>();

        // Assert that storage delete was invoked as compensation for the uploaded key
        storage.DeletedKeys.Should().NotBeEmpty("Cloud object must be deleted when DB save fails");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. DELETE SUCCESS & OWNERSHIP / STATUS CHECKS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_OwningCitizenOnSubmittedReport_RemovesCloudAndDbRecord()
    {
        var (db, storage, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        var storageKey = $"waste-reports/{report.Id}/att-1.jpg";
        var attachment = new ReportAttachment
        {
            Id = Guid.NewGuid(),
            WasteReportId = report.Id,
            StorageKey = storageKey,
            FileType = "image/jpeg",
            CreatedAt = DateTime.UtcNow
        };
        db.ReportAttachments.Add(attachment);
        await db.SaveChangesAsync();

        // Seed file in fake storage
        await storage.UploadAsync(storageKey, new MemoryStream(ValidJpegBytes), "image/jpeg");
        storage.StoredFiles.Should().ContainKey(storageKey);

        // Delete through service
        await service.DeleteAsync(report.Id, attachment.Id, citizenId, AppRoles.Citizen);

        // Verify removed from DB
        var dbRow = await db.ReportAttachments.FindAsync(attachment.Id);
        dbRow.Should().BeNull();

        // Verify removed from storage
        storage.StoredFiles.Should().NotContainKey(storageKey);
        storage.DeletedKeys.Should().Contain(storageKey);

        // Invariant: no status history created, UpdatedAt untouched
        var histories = await db.WasteReportStatusHistories.Where(h => h.WasteReportId == report.Id).ToListAsync();
        histories.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_OtherCitizen_ThrowsForbiddenException()
    {
        var (db, _, service) = CreateFixture();
        var ownerCitizenId = Guid.NewGuid();
        var otherCitizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, ownerCitizenId);

        var attachment = new ReportAttachment
        {
            Id = Guid.NewGuid(),
            WasteReportId = report.Id,
            StorageKey = $"waste-reports/{report.Id}/att-1.jpg",
            FileType = "image/jpeg"
        };
        db.ReportAttachments.Add(attachment);
        await db.SaveChangesAsync();

        var act = () => service.DeleteAsync(report.Id, attachment.Id, otherCitizenId, AppRoles.Citizen);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*You can only delete attachments from your own waste reports*");
    }

    [Fact]
    public async Task DeleteAsync_NonSubmittedStatus_ThrowsConflictException()
    {
        var (db, _, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId, WasteReportStatus.UnderReview);

        var attachment = new ReportAttachment
        {
            Id = Guid.NewGuid(),
            WasteReportId = report.Id,
            StorageKey = $"waste-reports/{report.Id}/att-1.jpg",
            FileType = "image/jpeg"
        };
        db.ReportAttachments.Add(attachment);
        await db.SaveChangesAsync();

        var act = () => service.DeleteAsync(report.Id, attachment.Id, citizenId, AppRoles.Citizen);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Attachments can only be modified while report is in Submitted status*");
    }

    [Fact]
    public async Task DeleteAsync_WrongAttachmentId_ThrowsNotFoundException()
    {
        var (db, _, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        var act = () => service.DeleteAsync(report.Id, Guid.NewGuid(), citizenId, AppRoles.Citizen);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_CloudDeleteFailure_LeavesDatabaseMetadataIntact()
    {
        var (db, storage, service) = CreateFixture();
        var citizenId = Guid.NewGuid();
        var report = await SeedReportAsync(db, citizenId);

        var attachment = new ReportAttachment
        {
            Id = Guid.NewGuid(),
            WasteReportId = report.Id,
            StorageKey = $"waste-reports/{report.Id}/att-1.jpg",
            FileType = "image/jpeg"
        };
        db.ReportAttachments.Add(attachment);
        await db.SaveChangesAsync();

        // Simulate cloud storage delete failure
        storage.SimulateDeleteFailure = true;

        var act = () => service.DeleteAsync(report.Id, attachment.Id, citizenId, AppRoles.Citizen);

        await act.Should().ThrowAsync<StorageServiceException>();

        // Verify DB row was NOT deleted
        var dbRow = await db.ReportAttachments.FindAsync(attachment.Id);
        dbRow.Should().NotBeNull("DB metadata must remain intact when cloud deletion fails");
    }
}
