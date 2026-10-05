using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Complaints.Entities;
using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Dashboard.Services;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Dashboard;

public class WasteOfficerDashboardServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_CountsOnlyTheFiveAuthoritativeStatusPredicates()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dashboard_{Guid.NewGuid():N}").Options);

        db.WasteReports.AddRange(
            Report(WasteReportStatus.Submitted), Report(WasteReportStatus.Submitted),
            Report(WasteReportStatus.UnderReview), Report(WasteReportStatus.Verified),
            Report(WasteReportStatus.Rejected), Report(WasteReportStatus.Cancelled));
        db.WasteBins.AddRange(
            Bin(BinAdministrativeStatus.Active), Bin(BinAdministrativeStatus.Active),
            Bin(BinAdministrativeStatus.Active), Bin(BinAdministrativeStatus.OutOfService),
            Bin(BinAdministrativeStatus.Retired));
        db.CollectionTasks.AddRange(
            Task(CollectionTaskStatus.Scheduled), Task(CollectionTaskStatus.Scheduled),
            Task(CollectionTaskStatus.Assigned), Task(CollectionTaskStatus.InProgress),
            Task(CollectionTaskStatus.Completed), Task(CollectionTaskStatus.Failed),
            Task(CollectionTaskStatus.Cancelled));
        db.Complaints.AddRange(
            Complaint(ComplaintStatus.Submitted), Complaint(ComplaintStatus.Submitted),
            Complaint(ComplaintStatus.InReview), Complaint(ComplaintStatus.Resolved));
        await db.SaveChangesAsync();

        var result = await new WasteOfficerDashboardService(db).GetOverviewAsync();

        result.ReportsAwaitingReview.Should().Be(2);
        result.ActiveBins.Should().Be(3);
        result.ScheduledCollections.Should().Be(2);
        result.OpenCollectionTasks.Should().Be(2);
        result.OpenComplaints.Should().Be(2);
    }

    [Fact]
    public async Task GetNeedsAttentionAsync_ReturnsNewestFiveAcrossBothSubmittedTypes()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dashboard_queue_{Guid.NewGuid():N}").Options);
        var now = DateTime.UtcNow;
        var reports = Enumerable.Range(0, 4).Select(index => new WasteReport
        {
            Id = Guid.NewGuid(), Status = WasteReportStatus.Submitted,
            CreatedAt = now.AddMinutes(-(index * 2)), WasteType = WasteType.General
        }).ToArray();
        var complaints = Enumerable.Range(0, 4).Select(index => new Complaint
        {
            Id = Guid.NewGuid(), Status = ComplaintStatus.Submitted,
            CreatedAt = now.AddMinutes(-(index * 2 + 1)), Category = ComplaintCategory.MissedCollection
        }).ToArray();
        var citizen = new AppUser { Id = Guid.NewGuid(), FullName = "Kasun Silva" };
        reports[0].Citizen = citizen;
        reports[0].AddressText = " Rajagiriya ";
        complaints[0].Citizen = citizen;
        complaints[0].LocationDescription = " Colombo 05 ";
        db.WasteReports.AddRange(reports);
        db.Complaints.AddRange(complaints);
        await db.SaveChangesAsync();

        var result = await new WasteOfficerDashboardService(db).GetNeedsAttentionAsync();

        result.Should().HaveCount(5);
        result.Select(item => item.Id).Should().Equal(
            reports[0].Id, complaints[0].Id, reports[1].Id, complaints[1].Id, reports[2].Id);
        result[0].Reference.Should().Be($"Report {reports[0].Id.ToString("N")[..8].ToUpperInvariant()}");
        result[1].Reference.Should().Be($"Complaint {complaints[0].Id.ToString("N")[..8].ToUpperInvariant()}");
        result[1].SecondaryLabel.Should().Be("Missed collection");
        result[0].SubmittedByName.Should().Be("Kasun Silva");
        result[0].AddressText.Should().Be("Rajagiriya");
        result[1].SubmittedByName.Should().Be("Kasun Silva");
        result[1].AddressText.Should().Be("Colombo 05");
        result.Single(item => item.Id == complaints[1].Id).AddressText.Should().BeNull();
        result.Single(item => item.Id == reports[1].Id).SubmittedByName.Should().Be("Citizen");

        var managerResult = await new MunicipalManagerDashboardService(db).GetNeedsAttentionAsync();
        managerResult.Should().BeEquivalentTo(result, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetNeedsAttentionAsync_ExcludesItemsWhoseInitialReviewStarted()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dashboard_queue_{Guid.NewGuid():N}").Options);
        var submittedReport = Report(WasteReportStatus.Submitted);
        var submittedComplaint = Complaint(ComplaintStatus.Submitted);
        db.WasteReports.AddRange(submittedReport, Report(WasteReportStatus.UnderReview),
            Report(WasteReportStatus.Verified), Report(WasteReportStatus.Rejected),
            Report(WasteReportStatus.Cancelled));
        db.Complaints.AddRange(submittedComplaint, Complaint(ComplaintStatus.InReview),
            Complaint(ComplaintStatus.Resolved));
        await db.SaveChangesAsync();

        var result = await new WasteOfficerDashboardService(db).GetNeedsAttentionAsync();

        result.Select(item => item.Id).Should().BeEquivalentTo([submittedReport.Id, submittedComplaint.Id]);
    }

    [Fact]
    public async Task GetNeedsAttentionAsync_ReturnsEmptyArrayWhenNoReviewIsPending()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dashboard_queue_{Guid.NewGuid():N}").Options);
        (await new WasteOfficerDashboardService(db).GetNeedsAttentionAsync()).Should().BeEmpty();
    }

    private static WasteReport Report(WasteReportStatus status) => new() { Status = status };
    private static WasteBin Bin(BinAdministrativeStatus status) => new() { AdministrativeStatus = status };
    private static CollectionTask Task(CollectionTaskStatus status) => new() { Status = status };
    private static Complaint Complaint(ComplaintStatus status) => new() { Status = status };
}
