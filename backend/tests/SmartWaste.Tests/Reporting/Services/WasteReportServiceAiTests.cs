using FluentAssertions;
using SmartWaste.Application.Common.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Application.Reporting.Queries;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Reporting.Services;
using SmartWaste.Tests.Reporting.Fakes;
using Xunit;

namespace SmartWaste.Tests.Reporting.Services;

public class WasteReportServiceAiTests
{
    private static (AppDbContext Db, UserManager<AppUser> UserManager) CreateContext()
    {
        var dbName = $"SmartWaste_Reporting_Ai_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new AppDbContext(options);

        var userStore = new UserStore<AppUser, IdentityRole<Guid>, AppDbContext, Guid>(db);
        var passwordHasher = new PasswordHasher<AppUser>();
        var userValidators = new List<IUserValidator<AppUser>> { new UserValidator<AppUser>() };
        var passwordValidators = new List<IPasswordValidator<AppUser>> { new PasswordValidator<AppUser>() };
        var keyNorm = new UpperInvariantLookupNormalizer();
        var errDescriber = new IdentityErrorDescriber();
        var services = new Mock<IServiceProvider>().Object;
        var loggerMock = new Mock<ILogger<UserManager<AppUser>>>();

        var userManager = new UserManager<AppUser>(
            userStore,
            new Mock<IOptions<IdentityOptions>>().Object,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNorm,
            errDescriber,
            services,
            loggerMock.Object);

        return (db, userManager);
    }

    private static WasteReportService CreateService(AppDbContext db, UserManager<AppUser> userManager)
    {
        var fakeStorage = new FakeFileStorageService();
        return new WasteReportService(db, userManager, fakeStorage);
    }

    [Fact]
    public async Task GetVerifiedReportsForAiAsync_ReturnsOnlyVerifiedReports()
    {
        var (db, userManager) = CreateContext();
        var service = CreateService(db, userManager);
        var citizenId = Guid.NewGuid();

        db.WasteReports.AddRange(
            new WasteReport { Id = Guid.NewGuid(), CitizenId = citizenId, Description = "Submitted", Status = WasteReportStatus.Submitted },
            new WasteReport { Id = Guid.NewGuid(), CitizenId = citizenId, Description = "UnderReview", Status = WasteReportStatus.UnderReview },
            new WasteReport { Id = Guid.NewGuid(), CitizenId = citizenId, Description = "Verified 1", Status = WasteReportStatus.Verified },
            new WasteReport { Id = Guid.NewGuid(), CitizenId = citizenId, Description = "Verified 2", Status = WasteReportStatus.Verified },
            new WasteReport { Id = Guid.NewGuid(), CitizenId = citizenId, Description = "Rejected", Status = WasteReportStatus.Rejected },
            new WasteReport { Id = Guid.NewGuid(), CitizenId = citizenId, Description = "Cancelled", Status = WasteReportStatus.Cancelled }
        );
        await db.SaveChangesAsync();

        var query = new GetVerifiedWasteReportsForAiQuery { Page = 1, PageSize = 10 };
        var result = await service.GetVerifiedReportsForAiAsync(query);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(r => r.Status == WasteReportStatus.Verified);
        result.Items.Should().OnlyContain(r => r.ReportReference == r.Id.ToString("N").Substring(0, 8).ToUpperInvariant());
        result.Items.Select(r => r.Description).Should().Contain(new[] { "Verified 1", "Verified 2" });
    }

    [Fact]
    public async Task GetVerifiedReportsForAiAsync_CalculatesAttachmentCountCorrectly()
    {
        var (db, userManager) = CreateContext();
        var service = CreateService(db, userManager);
        var citizenId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        var report = new WasteReport
        {
            Id = reportId,
            CitizenId = citizenId,
            Description = "Report with 2 attachments",
            Status = WasteReportStatus.Verified,
            Attachments = new List<ReportAttachment>
            {
                new() { Id = Guid.NewGuid(), StorageKey = "key1", FileType = "image/jpeg" },
                new() { Id = Guid.NewGuid(), StorageKey = "key2", FileType = "image/jpeg" }
            }
        };
        db.WasteReports.Add(report);
        await db.SaveChangesAsync();

        var query = new GetVerifiedWasteReportsForAiQuery { Page = 1, PageSize = 10 };
        var result = await service.GetVerifiedReportsForAiAsync(query);

        result.Items.Should().ContainSingle();
        result.Items[0].AttachmentCount.Should().Be(2);
    }

    [Fact]
    public async Task GetReportForVerificationAiAsync_ReturnsOnlyRequestedSubmittedReport()
    {
        var (db, userManager) = CreateContext();
        var target = new WasteReport
        {
            CitizenId = Guid.NewGuid(),
            Description = "Target report description is untrusted input",
            Status = WasteReportStatus.Submitted,
            WasteType = WasteType.Organic,
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Market entrance",
            CreatedAt = DateTime.UtcNow,
            Attachments = new List<ReportAttachment>
            {
                new() { StorageKey = "private-key", FileType = "image/jpeg" }
            }
        };
        db.WasteReports.AddRange(target,
            new WasteReport { CitizenId = Guid.NewGuid(), Description = "Other submitted report", Status = WasteReportStatus.Submitted });
        await db.SaveChangesAsync();

        var result = await CreateService(db, userManager).GetReportForVerificationAiAsync(target.Id);

        result.Id.Should().Be(target.Id);
        result.ReportReference.Should().Be(target.Id.ToString("N")[..8].ToUpperInvariant());
        result.Description.Should().Be(target.Description);
        result.Status.Should().Be(WasteReportStatus.Submitted);
        result.AttachmentCount.Should().Be(1);
        target.Status.Should().Be(WasteReportStatus.Submitted);
    }

    [Fact]
    public async Task GetReportForVerificationAiAsync_AllowsUnderReview()
    {
        var (db, userManager) = CreateContext();
        var report = new WasteReport { CitizenId = Guid.NewGuid(), Description = "Under review", Status = WasteReportStatus.UnderReview };
        db.WasteReports.Add(report);
        await db.SaveChangesAsync();

        var result = await CreateService(db, userManager).GetReportForVerificationAiAsync(report.Id);

        result.Id.Should().Be(report.Id);
        result.Status.Should().Be(WasteReportStatus.UnderReview);
    }

    [Theory]
    [InlineData(WasteReportStatus.Verified)]
    [InlineData(WasteReportStatus.Rejected)]
    [InlineData(WasteReportStatus.Cancelled)]
    [InlineData(WasteReportStatus.Scheduled)]
    [InlineData(WasteReportStatus.InProgress)]
    [InlineData(WasteReportStatus.Resolved)]
    public async Task GetReportForVerificationAiAsync_RejectsIneligibleStatus(WasteReportStatus status)
    {
        var (db, userManager) = CreateContext();
        var report = new WasteReport { CitizenId = Guid.NewGuid(), Description = "Ineligible report", Status = status };
        db.WasteReports.Add(report);
        await db.SaveChangesAsync();

        var act = () => CreateService(db, userManager).GetReportForVerificationAiAsync(report.Id);

        await act.Should().ThrowAsync<BusinessRuleConflictException>();
        report.Status.Should().Be(status);
    }

    [Fact]
    public async Task GetReportForVerificationAiAsync_UnknownIdThrowsNotFound()
    {
        var (db, userManager) = CreateContext();

        var act = () => CreateService(db, userManager).GetReportForVerificationAiAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
