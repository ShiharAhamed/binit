using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Collection.Services;

public class CollectionTaskReplacementServiceTests
{
    [Fact]
    public async Task ReplaceFailedReport_CreatesDistinctScheduledTaskAndReturnsReportToScheduled()
    {
        await using var db = Context();
        var (officer, original, report, _, _) = await SeedAsync(db, reportTarget: true);
        var service = new CollectionTaskService(db);

        var replacement = await service.CreateReplacementTaskAsync(original.Id, Request(), officer.Id, AppRoles.WasteOfficer);

        replacement.Id.Should().NotBe(original.Id);
        replacement.TaskCode.Should().NotBe(original.TaskCode);
        replacement.Status.Should().Be(CollectionTaskStatus.Scheduled);
        replacement.WasteReportId.Should().Be(report!.Id);
        (await db.CollectionTasks.SingleAsync(x => x.Id == original.Id)).Status.Should().Be(CollectionTaskStatus.Failed);
        (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Scheduled);
        (await db.WasteReportStatusHistories.CountAsync(x => x.WasteReportId == report.Id && x.FromStatus == WasteReportStatus.InProgress && x.ToStatus == WasteReportStatus.Scheduled)).Should().Be(1);
        (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == replacement.Id)).Should().Be(1);
        (await db.CollectionAssignmentTaskClaims.AnyAsync(x => x.CollectionTaskId == replacement.Id && x.IsActive)).Should().BeFalse();
    }

    [Fact]
    public async Task ReplaceFailedBin_PreservesBinCollectionStateAndOriginalTask()
    {
        await using var db = Context();
        var (officer, original, _, bin, lastCollectedAt) = await SeedAsync(db, reportTarget: false);
        var service = new CollectionTaskService(db);

        var replacement = await service.CreateReplacementTaskAsync(original.Id, Request(), officer.Id, AppRoles.WasteOfficer);

        replacement.Id.Should().NotBe(original.Id);
        replacement.WasteBinId.Should().Be(bin!.Id);
        replacement.CollectionReason.Should().Be(CollectionReason.OfficerDiscretion);
        (await db.CollectionTasks.SingleAsync(x => x.Id == original.Id)).Status.Should().Be(CollectionTaskStatus.Failed);
        (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().Be(lastCollectedAt);
        (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Replace_RejectsActiveTargetInvalidOriginalAndUnauthorizedCallers()
    {
        await using var db = Context();
        var (officer, original, report, _, _) = await SeedAsync(db, reportTarget: true);
        var service = new CollectionTaskService(db);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateReplacementTaskAsync(original.Id, Request(), officer.Id, AppRoles.MunicipalManager));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.CreateReplacementTaskAsync(original.Id, new CreateReplacementCollectionTaskRequest { ScheduledAt = DateTime.UtcNow.AddHours(1), ReplacementReason = "bad" }, officer.Id, AppRoles.WasteOfficer));
        db.CollectionTasks.Add(new CollectionTask { TaskCode = "ACTIVE-REPORT", WasteReportId = report!.Id, CollectionReason = CollectionReason.VerifiedReport, Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(3), CreatedByUserId = officer.Id });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.CreateReplacementTaskAsync(original.Id, Request(), officer.Id, AppRoles.WasteOfficer));
        (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.InProgress);
        (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == original.Id)).Should().Be(1);
    }

    private static CreateReplacementCollectionTaskRequest Request() => new() { ScheduledAt = DateTime.UtcNow.AddHours(2), ReplacementReason = "Officer reviewed the failed collection and approved a replacement.", HandlingNotes = "Use the recorded handling controls." };
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<(AppUser Officer, CollectionTask Original, WasteReport? Report, WasteBin? Bin, DateTime? LastCollectedAt)> SeedAsync(AppDbContext db, bool reportTarget)
    {
        var officer = new AppUser { Id = Guid.NewGuid(), FullName = "Officer", UserName = $"officer-{Guid.NewGuid():N}", IsActive = true };
        var citizen = new AppUser { Id = Guid.NewGuid(), FullName = "Citizen", UserName = $"citizen-{Guid.NewGuid():N}", IsActive = true };
        db.Users.AddRange(officer, citizen);
        if (reportTarget)
        {
            var report = new WasteReport { CitizenId = citizen.Id, Description = "Replacement report", WasteType = WasteType.General, Latitude = 6.9, Longitude = 79.9, Status = WasteReportStatus.InProgress };
            var task = new CollectionTask { TaskCode = "FAILED-REPORT", WasteReportId = report.Id, CollectionReason = CollectionReason.VerifiedReport, Status = CollectionTaskStatus.Failed, ScheduledAt = DateTime.UtcNow.AddHours(-2), CreatedByUserId = officer.Id };
            db.AddRange(report, task); db.CollectionTaskStatusHistories.Add(new CollectionTaskStatusHistory { CollectionTaskId = task.Id, ToStatus = CollectionTaskStatus.Failed, ChangedByUserId = officer.Id, ChangedAt = DateTime.UtcNow, Notes = "Collection failed." });
            await db.SaveChangesAsync(); return (officer, task, report, null, null);
        }
        var collected = DateTime.UtcNow.AddDays(-2); var bin = new WasteBin { BinCode = "REPLACE-BIN", CapacityLiters = 100, Latitude = 6.91, Longitude = 79.91, AdministrativeStatus = BinAdministrativeStatus.Active, LastCollectedAt = collected };
        var binTask = new CollectionTask { TaskCode = "FAILED-BIN", WasteBinId = bin.Id, CollectionReason = CollectionReason.OfficerDiscretion, SchedulingReason = "Original officer review reason.", Status = CollectionTaskStatus.Failed, ScheduledAt = DateTime.UtcNow.AddHours(-2), CreatedByUserId = officer.Id };
        db.AddRange(bin, binTask); db.CollectionTaskStatusHistories.Add(new CollectionTaskStatusHistory { CollectionTaskId = binTask.Id, ToStatus = CollectionTaskStatus.Failed, ChangedByUserId = officer.Id, ChangedAt = DateTime.UtcNow, Notes = "Collection failed." });
        await db.SaveChangesAsync(); return (officer, binTask, null, bin, collected);
    }
}
