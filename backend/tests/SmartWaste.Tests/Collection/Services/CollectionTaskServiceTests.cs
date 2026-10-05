using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Collection.Services;

/// <summary>
/// Focused unit tests for CollectionTaskService read and manual creation operations using EF Core InMemory provider.
/// Verifies authorization, pagination, filtering, target projection, detail retrieval,
/// chronological audit history, manual task creation, C1 atomic lifecycle synchronization,
/// bin eligibility rules, and strict concurrency/mutation guarantees.
/// </summary>
public class CollectionTaskServiceTests
{
    private static AppDbContext CreateContext(string? dbName = null)
    {
        dbName ??= $"SmartWaste_CollectionTask_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static CollectionTaskService CreateService(AppDbContext db, string timeZoneId = "Asia/Colombo")
    {
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Municipality:TimeZoneId"]).Returns(timeZoneId);
        return new CollectionTaskService(db, configMock.Object);
    }

    private static DateTime MunicipalDayStartUtc(DateOnly date)
    {
        var municipalityTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        return TimeZoneInfo.ConvertTimeToUtc(
            date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            municipalityTimeZone);
    }

    private static AppUser CreateSampleUser(AppDbContext db, string fullName = "Officer Silva")
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"{fullName.Replace(" ", "").ToLower()}@example.com",
            Email = $"{fullName.Replace(" ", "").ToLower()}@example.com",
            FullName = fullName,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        return user;
    }

    private static WasteBin CreateSampleBin(
        AppDbContext db,
        string binCode = "BIN-COL-0042",
        BinAdministrativeStatus status = BinAdministrativeStatus.Active,
        int[]? weekdays = null,
        DateTime? lastCollectedAt = null,
        DateTime? createdAt = null)
    {
        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = binCode,
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AdministrativeStatus = status,
            CollectionWeekdays = weekdays ?? new[] { 1, 3, 5 },
            CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-10),
            LastCollectedAt = lastCollectedAt,
            AddressText = "Main Street, Pettah"
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType
        {
            WasteBinId = bin.Id,
            WasteType = WasteType.General
        });
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType
        {
            WasteBinId = bin.Id,
            WasteType = WasteType.Recyclable
        });
        db.WasteBins.Add(bin);
        return bin;
    }

    private static WasteReport CreateSampleReport(
        AppDbContext db,
        string addressText = "Main Street, Pettah",
        WasteReportStatus status = WasteReportStatus.Verified)
    {
        var report = new WasteReport
        {
            Id = Guid.NewGuid(),
            CitizenId = Guid.NewGuid(),
            Description = "Overflowing waste behind market stalls",
            WasteType = WasteType.General,
            Latitude = 6.9351,
            Longitude = 79.8512,
            AddressText = addressText,
            Status = status,
            CreatedAt = DateTime.UtcNow.AddHours(-12)
        };
        db.WasteReports.Add(report);
        return report;
    }

    private static CollectionTask CreateSampleTask(
        AppDbContext db,
        Guid createdByUserId,
        Guid? binId = null,
        Guid? reportId = null,
        CollectionTaskStatus status = CollectionTaskStatus.Scheduled,
        CollectionReason reason = CollectionReason.FullOrBlockedBin,
        DateTime? scheduledAt = null,
        string? taskCode = null)
    {
        var task = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = taskCode ?? $"TSK-{Guid.NewGuid():N}"[..16].ToUpper(),
            WasteBinId = binId,
            WasteReportId = reportId,
            CollectionReason = reason,
            Status = status,
            ScheduledAt = scheduledAt ?? DateTime.UtcNow.AddHours(4),
            HandlingNotes = "Compactor vehicle required.",
            SchedulingReason = "Acute roadside fill",
            CreatedByUserId = createdByUserId,
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.CollectionTasks.Add(task);
        return task;
    }

    #region 1. Authorization Tests

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task GetListAsync_StaffRoles_ShouldSucceed(string role)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        CreateSampleTask(db, officer.Id);
        await db.SaveChangesAsync();

        var query = new CollectionTaskListQuery();
        var result = await service.GetListAsync(query, officer.Id, role);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    [InlineData("UnknownRole")]
    public async Task GetListAsync_NonStaffRoles_ShouldThrowForbiddenException(string role)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);

        var query = new CollectionTaskListQuery();
        var act = () => service.GetListAsync(query, Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers and Municipal Managers*");
    }

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task GetByIdAsync_StaffRoles_ShouldSucceed(string role)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);
        var task = CreateSampleTask(db, officer.Id, binId: bin.Id);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(task.Id, officer.Id, role);

        result.Should().NotBeNull();
        result.Id.Should().Be(task.Id);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task GetByIdAsync_NonStaffRoles_ShouldThrowForbiddenException(string role)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);

        var act = () => service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers and Municipal Managers*");
    }

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task GetTaskAuditTrailAsync_StaffRoles_ShouldSucceed(string role)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var task = CreateSampleTask(db, officer.Id);
        await db.SaveChangesAsync();

        var result = await service.GetTaskAuditTrailAsync(task.Id, officer.Id, role);

        result.Should().NotBeNull();
        result.CollectionTaskId.Should().Be(task.Id);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task GetTaskAuditTrailAsync_NonStaffRoles_ShouldThrowForbiddenException(string role)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);

        var act = () => service.GetTaskAuditTrailAsync(Guid.NewGuid(), Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers and Municipal Managers*");
    }

    #endregion

    #region 2. List Filtering, Pagination & Ordering Tests

    [Fact]
    public async Task GetListAsync_FilterByStatus_ReturnsOnlyMatchingTasks()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);

        CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Scheduled);
        CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.InProgress);
        CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Completed);
        await db.SaveChangesAsync();

        var query = new CollectionTaskListQuery { Status = CollectionTaskStatus.InProgress };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        result.Items[0].Status.Should().Be(CollectionTaskStatus.InProgress);
    }

    [Fact]
    public async Task GetListAsync_FilterByTargetType_ReturnsOnlyReportsOrBins()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);
        var report = CreateSampleReport(db);

        CreateSampleTask(db, officer.Id, binId: bin.Id);
        CreateSampleTask(db, officer.Id, reportId: report.Id);
        await db.SaveChangesAsync();

        // Query Reports only
        var reportResult = await service.GetListAsync(
            new CollectionTaskListQuery { TargetType = "Report" },
            officer.Id,
            AppRoles.WasteOfficer);

        reportResult.TotalCount.Should().Be(1);
        reportResult.Items[0].TargetType.Should().Be("Report");
        reportResult.Items[0].WasteReportId.Should().Be(report.Id);

        // Query Bins only
        var binResult = await service.GetListAsync(
            new CollectionTaskListQuery { TargetType = "Bin" },
            officer.Id,
            AppRoles.WasteOfficer);

        binResult.TotalCount.Should().Be(1);
        binResult.Items[0].TargetType.Should().Be("Bin");
        binResult.Items[0].WasteBinId.Should().Be(bin.Id);
    }

    [Fact]
    public async Task GetListAsync_FilterByCollectionReason_ReturnsOnlyMatchingReason()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);

        CreateSampleTask(db, officer.Id, reason: CollectionReason.FullOrBlockedBin);
        CreateSampleTask(db, officer.Id, reason: CollectionReason.RoutineCollection);
        await db.SaveChangesAsync();

        var query = new CollectionTaskListQuery { CollectionReason = CollectionReason.RoutineCollection };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items[0].CollectionReason.Should().Be(CollectionReason.RoutineCollection);
    }

    [Fact]
    public async Task GetListAsync_DateFromOnly_IncludesSelectedMunicipalDayAndLater()
    {
        var db = CreateContext();
        var service = CreateService(db);
        var officer = CreateSampleUser(db);
        var selectedDate = new DateOnly(2026, 9, 23);
        var selectedDayStartUtc = MunicipalDayStartUtc(selectedDate);

        CreateSampleTask(db, officer.Id, scheduledAt: selectedDayStartUtc.AddTicks(-1));
        var atStart = CreateSampleTask(db, officer.Id, scheduledAt: selectedDayStartUtc);
        var laterThatDay = CreateSampleTask(db, officer.Id, scheduledAt: selectedDayStartUtc.AddHours(12));
        await db.SaveChangesAsync();

        var query = new CollectionTaskListQuery { DateFrom = selectedDate };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(2);
        result.Items.Select(task => task.Id).Should().BeEquivalentTo([atStart.Id, laterThatDay.Id]);
    }

    [Fact]
    public async Task GetListAsync_DateToOnly_IncludesTheWholeSelectedMunicipalDayButExcludesNextDayStart()
    {
        var db = CreateContext();
        var service = CreateService(db);
        var officer = CreateSampleUser(db);
        var selectedDate = new DateOnly(2026, 9, 23);
        var selectedDayStartUtc = MunicipalDayStartUtc(selectedDate);
        var nextDayStartUtc = MunicipalDayStartUtc(selectedDate.AddDays(1));

        var atStart = CreateSampleTask(db, officer.Id, scheduledAt: selectedDayStartUtc);
        var laterThatDay = CreateSampleTask(db, officer.Id, scheduledAt: nextDayStartUtc.AddTicks(-1));
        CreateSampleTask(db, officer.Id, scheduledAt: nextDayStartUtc);
        await db.SaveChangesAsync();

        var result = await service.GetListAsync(
            new CollectionTaskListQuery { DateTo = selectedDate }, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(2);
        result.Items.Select(task => task.Id).Should().BeEquivalentTo([atStart.Id, laterThatDay.Id]);
    }

    [Fact]
    public async Task GetListAsync_DateRange_IncludesBothSelectedMunicipalDaysAndExcludesOutsideTasks()
    {
        var db = CreateContext();
        var service = CreateService(db);
        var officer = CreateSampleUser(db);
        var fromDate = new DateOnly(2026, 9, 23);
        var toDate = new DateOnly(2026, 9, 24);
        var fromUtc = MunicipalDayStartUtc(fromDate);
        var toExclusiveUtc = MunicipalDayStartUtc(toDate.AddDays(1));

        CreateSampleTask(db, officer.Id, scheduledAt: fromUtc.AddTicks(-1));
        var fromStart = CreateSampleTask(db, officer.Id, scheduledAt: fromUtc);
        var toDayLater = CreateSampleTask(db, officer.Id, scheduledAt: toExclusiveUtc.AddHours(-1));
        CreateSampleTask(db, officer.Id, scheduledAt: toExclusiveUtc);
        await db.SaveChangesAsync();

        var result = await service.GetListAsync(
            new CollectionTaskListQuery { DateFrom = fromDate, DateTo = toDate }, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(2);
        result.Items.Select(task => task.Id).Should().BeEquivalentTo([fromStart.Id, toDayLater.Id]);
    }

    [Fact]
    public async Task GetListAsync_NoDateFilters_PreservesUnfilteredResults()
    {
        var db = CreateContext();
        var service = CreateService(db);
        var officer = CreateSampleUser(db);

        CreateSampleTask(db, officer.Id, scheduledAt: MunicipalDayStartUtc(new DateOnly(2026, 9, 22)));
        CreateSampleTask(db, officer.Id, scheduledAt: MunicipalDayStartUtc(new DateOnly(2026, 9, 25)));
        await db.SaveChangesAsync();

        var result = await service.GetListAsync(new CollectionTaskListQuery(), officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public void MunicipalDayStartUtc_AsiaColombo_ProducesUtcBoundaries()
    {
        MunicipalDayStartUtc(new DateOnly(2026, 9, 23))
            .Should().Be(new DateTime(2026, 9, 22, 18, 30, 0, DateTimeKind.Utc));
        MunicipalDayStartUtc(new DateOnly(2026, 9, 24))
            .Should().Be(new DateTime(2026, 9, 23, 18, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetListAsync_Pagination_ReturnsCorrectPageAndPageSize()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);

        for (var i = 1; i <= 5; i++)
        {
            CreateSampleTask(db, officer.Id, scheduledAt: DateTime.UtcNow.AddHours(i));
        }
        await db.SaveChangesAsync();

        var query = new CollectionTaskListQuery { Page = 2, PageSize = 2 };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetListAsync_DeterministicOrdering_ScheduledAtAscThenIdAsc()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);

        var fixedTime = new DateTime(2026, 9, 21, 14, 0, 0, DateTimeKind.Utc);
        var idSmall = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var idLarge = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var taskEarlier = CreateSampleTask(db, officer.Id, scheduledAt: fixedTime.AddHours(-1));
        var taskLater1 = CreateSampleTask(db, officer.Id, scheduledAt: fixedTime);
        taskLater1.Id = idLarge;
        var taskLater2 = CreateSampleTask(db, officer.Id, scheduledAt: fixedTime);
        taskLater2.Id = idSmall;
        await db.SaveChangesAsync();

        var query = new CollectionTaskListQuery();
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.Items[0].Id.Should().Be(taskEarlier.Id);
        result.Items[1].Id.Should().Be(idSmall);
        result.Items[2].Id.Should().Be(idLarge);
    }

    [Fact]
    public async Task GetListAsync_TargetReferenceMapping_MapsBinCodeOrReportCodeCorrectly()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db, "BIN-COL-9999");
        var report = CreateSampleReport(db);

        CreateSampleTask(db, officer.Id, binId: bin.Id);
        CreateSampleTask(db, officer.Id, reportId: report.Id);
        await db.SaveChangesAsync();

        var query = new CollectionTaskListQuery();
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        var binTask = result.Items.Single(t => t.TargetType == "Bin");
        binTask.TargetReference.Should().Be("BIN-COL-9999");

        var reportTask = result.Items.Single(t => t.TargetType == "Report");
        reportTask.TargetReference.Should().Be($"Report {report.Id.ToString("N")[..8].ToUpperInvariant()}");
    }

    #endregion

    #region 3. Task Detail Tests

    [Fact]
    public async Task GetByIdAsync_MissingTask_ThrowsNotFoundException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var missingId = Guid.NewGuid();

        var act = () => service.GetByIdAsync(missingId, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingId}*");
    }

    [Fact]
    public async Task GetByIdAsync_BinTargetedTask_MapsFullDetailAndTargetSummary()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db, "Officer Silva");
        var bin = CreateSampleBin(db, "BIN-COL-0042");

        db.BinObservations.Add(new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            RecordedByUserId = officer.Id,
            RecordedAt = DateTime.UtcNow.AddMinutes(-30)
        });

        var task = CreateSampleTask(
            db,
            officer.Id,
            binId: bin.Id,
            status: CollectionTaskStatus.Scheduled,
            reason: CollectionReason.FullOrBlockedBin,
            taskCode: "TSK-20260921-0012");
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(task.Id, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Id.Should().Be(task.Id);
        result.TaskCode.Should().Be("TSK-20260921-0012");
        result.TargetType.Should().Be("Bin");
        result.WasteBinId.Should().Be(bin.Id);
        result.WasteReportId.Should().BeNull();
        result.CollectionReason.Should().Be(CollectionReason.FullOrBlockedBin);
        result.Status.Should().Be(CollectionTaskStatus.Scheduled);
        result.CreatedByUserName.Should().Be("Officer Silva");
        result.HandlingNotes.Should().Be("Compactor vehicle required.");

        result.TargetSummary.Should().NotBeNull();
        result.TargetSummary.Identifier.Should().Be("BIN-COL-0042");
        result.TargetSummary.CapacityLiters.Should().Be(660);
        result.TargetSummary.WasteTypes.Should().Contain(new[] { "General", "Recyclable" });
        result.TargetSummary.LatestFillLevelPercent.Should().Be(100);
        result.TargetSummary.AddressText.Should().Be("Main Street, Pettah");
    }

    [Fact]
    public async Task GetByIdAsync_ReportTargetedTask_MapsFullDetailAndTargetSummary()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db, "Officer Silva");
        var report = CreateSampleReport(db, "Pettah Bus Station");

        var task = CreateSampleTask(
            db,
            officer.Id,
            reportId: report.Id,
            status: CollectionTaskStatus.Scheduled,
            reason: CollectionReason.VerifiedReport);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(task.Id, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.TargetType.Should().Be("Report");
        result.WasteReportId.Should().Be(report.Id);
        result.WasteBinId.Should().BeNull();
        result.CollectionReason.Should().Be(CollectionReason.VerifiedReport);

        result.TargetSummary.Should().NotBeNull();
        result.TargetSummary.Identifier.Should().Be($"Report {report.Id.ToString("N")[..8].ToUpperInvariant()}");
        result.TargetSummary.CapacityLiters.Should().BeNull();
        result.TargetSummary.WasteTypes.Should().ContainSingle().Which.Should().Be("General");
        result.TargetSummary.AddressText.Should().Be("Pettah Bus Station");
    }

    #endregion

    #region 4. Audit Trail Tests

    [Fact]
    public async Task GetTaskAuditTrailAsync_MissingTask_ThrowsNotFoundException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var missingId = Guid.NewGuid();

        var act = () => service.GetTaskAuditTrailAsync(missingId, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingId}*");
    }

    [Fact]
    public async Task GetTaskAuditTrailAsync_ReturnsChronologicalStatusAndScheduleHistories()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db, "Officer Silva");
        var task = CreateSampleTask(db, officer.Id, taskCode: "TSK-20260921-0099");

        var t0 = DateTime.UtcNow.AddHours(-3);
        var t1 = DateTime.UtcNow.AddHours(-2);
        var t2 = DateTime.UtcNow.AddHours(-1);

        db.CollectionTaskStatusHistories.AddRange(
            new CollectionTaskStatusHistory
            {
                Id = Guid.NewGuid(),
                CollectionTaskId = task.Id,
                FromStatus = null,
                ToStatus = CollectionTaskStatus.Scheduled,
                ChangedByUserId = officer.Id,
                Notes = "Task manually created",
                ChangedAt = t0
            },
            new CollectionTaskStatusHistory
            {
                Id = Guid.NewGuid(),
                CollectionTaskId = task.Id,
                FromStatus = CollectionTaskStatus.Scheduled,
                ToStatus = CollectionTaskStatus.Assigned,
                ChangedByUserId = officer.Id,
                Notes = "Assigned to driver",
                ChangedAt = t2
            }
        );

        db.CollectionTaskScheduleHistories.Add(new CollectionTaskScheduleHistory
        {
            Id = Guid.NewGuid(),
            CollectionTaskId = task.Id,
            PreviousScheduledAt = DateTime.UtcNow.AddHours(4),
            NewScheduledAt = DateTime.UtcNow.AddHours(8),
            Reason = "Truck breakdown delay",
            RescheduledByUserId = officer.Id,
            RescheduledAt = t1
        });

        await db.SaveChangesAsync();

        var result = await service.GetTaskAuditTrailAsync(task.Id, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.CollectionTaskId.Should().Be(task.Id);
        result.TaskCode.Should().Be("TSK-20260921-0099");

        result.StatusHistory.Should().HaveCount(2);
        result.StatusHistory[0].FromStatus.Should().BeNull();
        result.StatusHistory[0].ToStatus.Should().Be("Scheduled");
        result.StatusHistory[0].ChangedByUserName.Should().Be("Officer Silva");
        result.StatusHistory[1].FromStatus.Should().Be("Scheduled");
        result.StatusHistory[1].ToStatus.Should().Be("Assigned");

        result.ScheduleHistory.Should().HaveCount(1);
        result.ScheduleHistory[0].Reason.Should().Be("Truck breakdown delay");
        result.ScheduleHistory[0].RescheduledByUserName.Should().Be("Officer Silva");
        result.ScheduleHistory[0].PreviousScheduledAt.Should().BeBefore(result.ScheduleHistory[0].NewScheduledAt);
    }

    #endregion

    #region 5. Read-Only Guarantee & Reschedule Guard

    [Fact]
    public async Task ReadMethods_DoNotMutateDatabaseEntities()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);
        var report = CreateSampleReport(db);
        var task = CreateSampleTask(db, officer.Id, binId: bin.Id, reportId: report.Id);
        await db.SaveChangesAsync();

        var tasksBefore = await db.CollectionTasks.AsNoTracking().ToListAsync();
        var binsBefore = await db.WasteBins.AsNoTracking().ToListAsync();
        var reportsBefore = await db.WasteReports.AsNoTracking().ToListAsync();

        await service.GetListAsync(new CollectionTaskListQuery(), officer.Id, AppRoles.WasteOfficer);
        await service.GetByIdAsync(task.Id, officer.Id, AppRoles.WasteOfficer);
        await service.GetTaskAuditTrailAsync(task.Id, officer.Id, AppRoles.WasteOfficer);

        var tasksAfter = await db.CollectionTasks.AsNoTracking().ToListAsync();
        var binsAfter = await db.WasteBins.AsNoTracking().ToListAsync();
        var reportsAfter = await db.WasteReports.AsNoTracking().ToListAsync();

        tasksAfter.Should().BeEquivalentTo(tasksBefore);
        binsAfter.Should().BeEquivalentTo(binsBefore);
        reportsAfter.Should().BeEquivalentTo(reportsBefore);
    }


    #endregion

    #region 6. Manual Task Creation Tests (Step 10A.4e.2)

    [Fact]
    public async Task CreateManualTaskAsync_WasteOfficer_ValidReportTask_ShouldSucceedAndTransitionReport()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db, "Officer Silva");
        var report = CreateSampleReport(db, "Main Street, Pettah", WasteReportStatus.Verified);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(3),
            HandlingNotes = "Bulky compaction required."
        };

        var result = await service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        // 1. Task properties
        result.Should().NotBeNull();
        result.TargetType.Should().Be("Report");
        result.WasteReportId.Should().Be(report.Id);
        result.WasteBinId.Should().BeNull();
        result.CollectionReason.Should().Be(CollectionReason.VerifiedReport);
        result.Status.Should().Be(CollectionTaskStatus.Scheduled);
        result.CreatedByUserId.Should().Be(officer.Id);
        result.CreatedByUserName.Should().Be("Officer Silva");
        result.CreationMethod.Should().Be(TaskCreationMethod.Manual);
        result.TaskCode.Should().StartWith("TSK-");
        result.HandlingNotes.Should().Be("Bulky compaction required.");

        // 2. Report state transition (Verified -> Scheduled)
        var updatedReport = await db.WasteReports.FindAsync(report.Id);
        updatedReport.Should().NotBeNull();
        updatedReport!.Status.Should().Be(WasteReportStatus.Scheduled);
        updatedReport.UpdatedAt.Should().NotBeNull();

        // 3. C1 Report Status History entry
        var reportHistory = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == report.Id)
            .ToListAsync();
        reportHistory.Should().ContainSingle();
        reportHistory[0].FromStatus.Should().Be(WasteReportStatus.Verified);
        reportHistory[0].ToStatus.Should().Be(WasteReportStatus.Scheduled);
        reportHistory[0].ChangedByUserId.Should().Be(officer.Id);
        reportHistory[0].Notes.Should().Be($"Collection task {result.TaskCode} scheduled");

        // 4. Initial C2 Task Status History entry
        var taskHistory = await db.CollectionTaskStatusHistories
            .Where(h => h.CollectionTaskId == result.Id)
            .ToListAsync();
        taskHistory.Should().ContainSingle();
        taskHistory[0].FromStatus.Should().BeNull();
        taskHistory[0].ToStatus.Should().Be(CollectionTaskStatus.Scheduled);
        taskHistory[0].ChangedByUserId.Should().Be(officer.Id);
        taskHistory[0].Notes.Should().Be("Task manually created");
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    [InlineData(AppRoles.MunicipalManager)]
    [InlineData("UnknownRole")]
    public async Task CreateManualTaskAsync_NonWasteOfficerRoles_ThrowsForbiddenException(string role)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var report = CreateSampleReport(db);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var act = () => service.CreateManualTaskAsync(request, Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers can manually create collection tasks*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_TargetXorViolation_ThrowsValidationException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var report = CreateSampleReport(db);
        var bin = CreateSampleBin(db);
        await db.SaveChangesAsync();

        // Both targets set
        var requestBoth = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };
        var actBoth = () => service.CreateManualTaskAsync(requestBoth, Guid.NewGuid(), AppRoles.WasteOfficer);
        await actBoth.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Exactly one of WasteReportId or WasteBinId must be supplied*");

        // Neither target set
        var requestNeither = new CreateManualCollectionTaskRequest
        {
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };
        var actNeither = () => service.CreateManualTaskAsync(requestNeither, Guid.NewGuid(), AppRoles.WasteOfficer);
        await actNeither.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Exactly one of WasteReportId or WasteBinId must be supplied*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_InvalidReasonTargetCombination_ThrowsValidationException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var report = CreateSampleReport(db);
        var bin = CreateSampleBin(db);
        await db.SaveChangesAsync();

        // Report with RoutineCollection
        var requestReportInvalid = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.RoutineCollection,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };
        var act1 = () => service.CreateManualTaskAsync(requestReportInvalid, Guid.NewGuid(), AppRoles.WasteOfficer);
        await act1.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Collection reason must be 'VerifiedReport' when targeting a waste report*");

        // Bin with VerifiedReport
        var requestBinInvalid = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };
        var act2 = () => service.CreateManualTaskAsync(requestBinInvalid, Guid.NewGuid(), AppRoles.WasteOfficer);
        await act2.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Collection reason must be 'FullOrBlockedBin', 'RoutineCollection', or 'OfficerDiscretion'*");
    }

    [Theory]
    [InlineData(WasteReportStatus.Submitted)]
    [InlineData(WasteReportStatus.UnderReview)]
    [InlineData(WasteReportStatus.Scheduled)]
    [InlineData(WasteReportStatus.InProgress)]
    [InlineData(WasteReportStatus.Resolved)]
    [InlineData(WasteReportStatus.Rejected)]
    [InlineData(WasteReportStatus.Cancelled)]
    public async Task CreateManualTaskAsync_NonVerifiedReportStatus_ThrowsBusinessRuleConflictException(WasteReportStatus status)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var report = CreateSampleReport(db, status: status);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*'Verified' is required*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_ReportWithActiveTask_ThrowsBusinessRuleConflictException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var report = CreateSampleReport(db, status: WasteReportStatus.Verified);
        CreateSampleTask(db, officer.Id, reportId: report.Id, status: CollectionTaskStatus.Scheduled);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already has an active collection task*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_ConcurrentRequests_CannotBothScheduleSameVerifiedReport()
    {
        var dbName = $"SmartWaste_CollectionTask_{Guid.NewGuid():N}";
        var db = CreateContext(dbName);
        var officer = CreateSampleUser(db, "Officer Silva");
        var report = CreateSampleReport(db, status: WasteReportStatus.Verified);
        await db.SaveChangesAsync();

        var request1 = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var request2 = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(3)
        };

        var db1 = CreateContext(dbName);
        var db2 = CreateContext(dbName);
        var service1 = new CollectionTaskService(db1);
        var service2 = new CollectionTaskService(db2);

        var task1 = Task.Run(async () =>
        {
            try
            {
                await service1.CreateManualTaskAsync(request1, officer.Id, AppRoles.WasteOfficer);
                return true;
            }
            catch (BusinessRuleConflictException)
            {
                return false;
            }
        });

        var task2 = Task.Run(async () =>
        {
            try
            {
                await service2.CreateManualTaskAsync(request2, officer.Id, AppRoles.WasteOfficer);
                return true;
            }
            catch (BusinessRuleConflictException)
            {
                return false;
            }
        });

        var results = await Task.WhenAll(task1, task2);

        results.Count(r => r == true).Should().Be(1);
        results.Count(r => r == false).Should().Be(1);

        var verifyDb = CreateContext(dbName);
        var tasks = await verifyDb.CollectionTasks.Where(t => t.WasteReportId == report.Id).ToListAsync();
        tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateManualTaskAsync_Fresh100PercentGoodObservation_CreatesFullOrBlockedBinTask()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db, "Officer Silva");
        var bin = CreateSampleBin(db);

        var obs = new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            RecordedByUserId = officer.Id,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.BinObservations.Add(obs);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var result = await service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.TargetType.Should().Be("Bin");
        result.WasteBinId.Should().Be(bin.Id);
        result.CollectionReason.Should().Be(CollectionReason.FullOrBlockedBin);
        result.Status.Should().Be(CollectionTaskStatus.Scheduled);

        // Verify TriggerObservationId set in DB
        var taskInDb = await db.CollectionTasks.FindAsync(result.Id);
        taskInDb.Should().NotBeNull();
        taskInDb!.TriggerObservationId.Should().Be(obs.Id);

        // Verify LastCollectedAt NOT modified
        var binInDb = await db.WasteBins.FindAsync(bin.Id);
        binInDb!.LastCollectedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateManualTaskAsync_FreshBlockedObservation_CreatesFullOrBlockedBinTask()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);

        var obs = new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 50,
            Condition = BinCondition.Blocked,
            RecordedByUserId = officer.Id,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.BinObservations.Add(obs);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var result = await service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.CollectionReason.Should().Be(CollectionReason.FullOrBlockedBin);
    }

    [Fact]
    public async Task CreateManualTaskAsync_75PercentGoodObservation_ThrowsBusinessRuleConflictException_WarningOnly()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);

        db.BinObservations.Add(new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 75,
            Condition = BinCondition.Good,
            RecordedByUserId = officer.Id,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*A fill level of 100% or 'Blocked' condition is required*");
    }

    [Theory]
    [InlineData(BinCondition.Damaged)]
    [InlineData(BinCondition.Missing)]
    public async Task CreateManualTaskAsync_DamagedOrMissingCondition_ThrowsBusinessRuleConflictException(BinCondition condition)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);

        db.BinObservations.Add(new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 100,
            Condition = condition,
            RecordedByUserId = officer.Id,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*maintenance concern rather than an ordinary collection need*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_StaleOrSupersededObservation_ThrowsBusinessRuleConflictException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);

        // Case 1: Superseded by later collection
        var obsTime = DateTime.UtcNow.AddHours(-4);
        var collectionTime = DateTime.UtcNow.AddHours(-2);
        var binSuperseded = CreateSampleBin(db, "BIN-SUP", lastCollectedAt: collectionTime);
        db.BinObservations.Add(new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = binSuperseded.Id,
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            RecordedByUserId = officer.Id,
            RecordedAt = obsTime
        });

        // Case 2: Stale observation > 48 hours
        var binStale = CreateSampleBin(db, "BIN-STL");
        db.BinObservations.Add(new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = binStale.Id,
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            RecordedByUserId = officer.Id,
            RecordedAt = DateTime.UtcNow.AddHours(-50)
        });

        await db.SaveChangesAsync();

        var requestSuperseded = new CreateManualCollectionTaskRequest
        {
            WasteBinId = binSuperseded.Id,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };
        var act1 = () => service.CreateManualTaskAsync(requestSuperseded, officer.Id, AppRoles.WasteOfficer);
        await act1.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*superseded by a subsequent collection*");

        var requestStale = new CreateManualCollectionTaskRequest
        {
            WasteBinId = binStale.Id,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };
        var act2 = () => service.CreateManualTaskAsync(requestStale, officer.Id, AppRoles.WasteOfficer);
        await act2.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*stale (older than 48 hours)*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_RoutineCollection_WhenDue_CreatesRoutineTask()
    {
        var db = CreateContext();
        var service = CreateService(db, "Asia/Colombo");
        var officer = CreateSampleUser(db);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayWeekday = (int)localNow.DayOfWeek == 0 ? 7 : (int)localNow.DayOfWeek;

        var bin = CreateSampleBin(db, weekdays: new[] { todayWeekday });
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.RoutineCollection,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var result = await service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.CollectionReason.Should().Be(CollectionReason.RoutineCollection);

        var taskInDb = await db.CollectionTasks.FindAsync(result.Id);
        taskInDb!.RoutineDueDate.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateManualTaskAsync_RoutineCollection_WhenNotDue_ThrowsBusinessRuleConflictException()
    {
        var db = CreateContext();
        var service = CreateService(db, "Asia/Colombo");
        var officer = CreateSampleUser(db);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayWeekday = (int)localNow.DayOfWeek == 0 ? 7 : (int)localNow.DayOfWeek;
        // Non-today weekday
        var otherWeekday = todayWeekday == 7 ? 1 : todayWeekday + 1;

        // Created today, configured for tomorrow -> not due today
        var bin = CreateSampleBin(db, weekdays: new[] { otherWeekday }, createdAt: DateTime.UtcNow);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.RoutineCollection,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*not currently due for routine collection*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_OfficerDiscretion_WithValidReason_CreatesTask()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.OfficerDiscretion,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            SchedulingReason = "Anticipated heavy overflow due to street food festival"
        };

        var result = await service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.CollectionReason.Should().Be(CollectionReason.OfficerDiscretion);
        result.SchedulingReason.Should().Be("Anticipated heavy overflow due to street food festival");
    }

    [Theory]
    [InlineData(BinAdministrativeStatus.OutOfService)]
    [InlineData(BinAdministrativeStatus.Retired)]
    public async Task CreateManualTaskAsync_InactiveBin_ThrowsBusinessRuleConflictException(BinAdministrativeStatus status)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db, status: status);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.OfficerDiscretion,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            SchedulingReason = "Administrative test dispatch"
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Only 'Active' bins can be scheduled*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_BinWithActiveTask_ThrowsBusinessRuleConflictException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);
        CreateSampleTask(db, officer.Id, binId: bin.Id, status: CollectionTaskStatus.Scheduled);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.OfficerDiscretion,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            SchedulingReason = "Duplicate dispatch test"
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already has an active collection task*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_BinWithUnreviewedFailedTask_ThrowsBusinessRuleConflictException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);
        CreateSampleTask(db, officer.Id, binId: bin.Id, status: CollectionTaskStatus.Failed);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.OfficerDiscretion,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            SchedulingReason = "Attempt to replace failed task"
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*previous collection task*failed*review is required*");
    }

    [Fact]
    public async Task CreateManualTaskAsync_PastScheduledAt_ReportTarget_ThrowsValidationExceptionAndPreservesState()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var report = CreateSampleReport(db, status: WasteReportStatus.Verified);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Scheduled time cannot be in the past*");

        var persistedReport = await db.WasteReports.FindAsync(report.Id);
        persistedReport!.Status.Should().Be(WasteReportStatus.Verified);
        persistedReport.UpdatedAt.Should().BeNull();

        var tasks = await db.CollectionTasks.Where(t => t.WasteReportId == report.Id).ToListAsync();
        tasks.Should().BeEmpty();

        var reportHistories = await db.WasteReportStatusHistories.Where(h => h.WasteReportId == report.Id).ToListAsync();
        reportHistories.Should().BeEmpty();

        var taskHistories = await db.CollectionTaskStatusHistories.ToListAsync();
        taskHistories.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateManualTaskAsync_PastScheduledAt_BinTarget_ThrowsValidationExceptionAndPreservesState()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var bin = CreateSampleBin(db);
        db.BinObservations.Add(new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            RecordedByUserId = officer.Id,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var act = () => service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Scheduled time cannot be in the past*");

        var tasks = await db.CollectionTasks.Where(t => t.WasteBinId == bin.Id).ToListAsync();
        tasks.Should().BeEmpty();

        var taskHistories = await db.CollectionTaskStatusHistories.ToListAsync();
        taskHistories.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateManualTaskAsync_FutureScheduledAt_Accepted()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var report = CreateSampleReport(db, status: WasteReportStatus.Verified);
        await db.SaveChangesAsync();

        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = report.Id,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddMinutes(30)
        };

        var result = await service.CreateManualTaskAsync(request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Status.Should().Be(CollectionTaskStatus.Scheduled);
        result.ScheduledAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(5));

        var updatedReport = await db.WasteReports.FindAsync(report.Id);
        updatedReport!.Status.Should().Be(WasteReportStatus.Scheduled);
    }

    #endregion

    #region 7. Task Rescheduling Tests (Step 10A.4e.3)

    [Fact]
    public async Task RescheduleTaskAsync_WasteOfficer_ReportTask_ShouldSucceedAndRecordScheduleHistory()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db, "Officer Silva");
        var report = CreateSampleReport(db, "Main Street, Pettah", WasteReportStatus.Scheduled);
        var initialScheduledAt = DateTime.UtcNow.AddHours(2);
        var task = CreateSampleTask(db, officer.Id, reportId: report.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: initialScheduledAt);
        await db.SaveChangesAsync();

        var initialReportUpdatedAt = report.UpdatedAt;
        var newScheduledAt = DateTime.UtcNow.AddHours(8);
        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = newScheduledAt,
            Reason = "Severe tropical downpour delayed municipal collection vehicles."
        };

        var result = await service.RescheduleTaskAsync(task.Id, request, officer.Id, AppRoles.WasteOfficer);

        // Result validation
        result.Should().NotBeNull();
        result.Id.Should().Be(task.Id);
        result.Status.Should().Be(CollectionTaskStatus.Scheduled);
        result.ScheduledAt.Should().BeCloseTo(newScheduledAt, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().NotBeNull();
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.ScheduleHistory.Should().HaveCount(1);

        var historyDto = result.ScheduleHistory.Single();
        historyDto.PreviousScheduledAt.Should().BeCloseTo(initialScheduledAt, TimeSpan.FromSeconds(5));
        historyDto.NewScheduledAt.Should().BeCloseTo(newScheduledAt, TimeSpan.FromSeconds(5));
        historyDto.Reason.Should().Be(request.Reason);
        historyDto.RescheduledByUserId.Should().Be(officer.Id);
        historyDto.RescheduledByUserName.Should().Be(officer.FullName);
        historyDto.RescheduledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        result.StatusHistory.Should().BeEmpty();

        // Database state verification
        var dbTask = await db.CollectionTasks.FindAsync(task.Id);
        dbTask.Should().NotBeNull();
        dbTask!.ScheduledAt.Should().BeCloseTo(newScheduledAt, TimeSpan.FromSeconds(5));
        dbTask.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        dbTask.Status.Should().Be(CollectionTaskStatus.Scheduled);

        var dbScheduleHistories = await db.CollectionTaskScheduleHistories
            .Where(h => h.CollectionTaskId == task.Id)
            .ToListAsync();
        dbScheduleHistories.Should().HaveCount(1);
        var dbHistory = dbScheduleHistories.Single();
        dbHistory.PreviousScheduledAt.Should().BeCloseTo(initialScheduledAt, TimeSpan.FromSeconds(5));
        dbHistory.NewScheduledAt.Should().BeCloseTo(newScheduledAt, TimeSpan.FromSeconds(5));
        dbHistory.Reason.Should().Be(request.Reason);
        dbHistory.RescheduledByUserId.Should().Be(officer.Id);

        var dbStatusHistories = await db.CollectionTaskStatusHistories
            .Where(h => h.CollectionTaskId == task.Id)
            .ToListAsync();
        dbStatusHistories.Should().BeEmpty();

        // Linked WasteReport must remain untouched
        var dbReport = await db.WasteReports.FindAsync(report.Id);
        dbReport.Should().NotBeNull();
        dbReport!.Status.Should().Be(WasteReportStatus.Scheduled);
        dbReport.UpdatedAt.Should().Be(initialReportUpdatedAt);

        var reportHistories = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == report.Id)
            .ToListAsync();
        reportHistories.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleTaskAsync_WasteOfficer_BinTask_ShouldSucceedAndPreserveBinState()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db, "Officer Perera");
        var lastCollectedAt = DateTime.UtcNow.AddDays(-2);
        var bin = CreateSampleBin(db, "BIN-COL-0099", BinAdministrativeStatus.Active, lastCollectedAt: lastCollectedAt);
        var initialScheduledAt = DateTime.UtcNow.AddHours(3);
        var task = CreateSampleTask(db, officer.Id, binId: bin.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: initialScheduledAt);
        await db.SaveChangesAsync();

        var newScheduledAt = DateTime.UtcNow.AddHours(12);
        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = newScheduledAt,
            Reason = "Route re-sequencing due to scheduled marathon event."
        };

        var result = await service.RescheduleTaskAsync(task.Id, request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.ScheduledAt.Should().BeCloseTo(newScheduledAt, TimeSpan.FromSeconds(5));
        result.ScheduleHistory.Should().HaveCount(1);

        // WasteBin state must remain completely untouched
        var dbBin = await db.WasteBins.FindAsync(bin.Id);
        dbBin.Should().NotBeNull();
        dbBin!.AdministrativeStatus.Should().Be(BinAdministrativeStatus.Active);
        dbBin.LastCollectedAt.Should().Be(lastCollectedAt);

        var binObservations = await db.BinObservations
            .Where(o => o.WasteBinId == bin.Id)
            .ToListAsync();
        binObservations.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleTaskAsync_MultipleReschedules_ShouldAccumulateScheduleHistoryInOrder()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db, "Officer Fernando");
        var t0 = DateTime.UtcNow.AddHours(2);
        var task = CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: t0);
        await db.SaveChangesAsync();

        // Reschedule 1: t0 -> t1
        var t1 = DateTime.UtcNow.AddHours(6);
        var request1 = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = t1,
            Reason = "First reschedule: driver reassigned to emergency clearance."
        };
        await service.RescheduleTaskAsync(task.Id, request1, officer.Id, AppRoles.WasteOfficer);

        // Reschedule 2: t1 -> t2
        var t2 = DateTime.UtcNow.AddHours(10);
        var request2 = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = t2,
            Reason = "Second reschedule: replacement driver vehicle breakdown."
        };
        var result2 = await service.RescheduleTaskAsync(task.Id, request2, officer.Id, AppRoles.WasteOfficer);

        result2.ScheduledAt.Should().BeCloseTo(t2, TimeSpan.FromSeconds(5));
        result2.ScheduleHistory.Should().HaveCount(2);

        result2.ScheduleHistory[0].PreviousScheduledAt.Should().BeCloseTo(t0, TimeSpan.FromSeconds(5));
        result2.ScheduleHistory[0].NewScheduledAt.Should().BeCloseTo(t1, TimeSpan.FromSeconds(5));
        result2.ScheduleHistory[0].Reason.Should().Be(request1.Reason);

        result2.ScheduleHistory[1].PreviousScheduledAt.Should().BeCloseTo(t1, TimeSpan.FromSeconds(5));
        result2.ScheduleHistory[1].NewScheduledAt.Should().BeCloseTo(t2, TimeSpan.FromSeconds(5));
        result2.ScheduleHistory[1].Reason.Should().Be(request2.Reason);

        var auditTrail = await service.GetTaskAuditTrailAsync(task.Id, officer.Id, AppRoles.WasteOfficer);
        auditTrail.ScheduleHistory.Should().HaveCount(2);
        auditTrail.ScheduleHistory[0].Reason.Should().Be(request1.Reason);
        auditTrail.ScheduleHistory[1].Reason.Should().Be(request2.Reason);
    }

    [Theory]
    [InlineData(AppRoles.MunicipalManager)]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    [InlineData("UnknownRole")]
    [InlineData("")]
    public async Task RescheduleTaskAsync_NonWasteOfficerRoles_ShouldThrowForbiddenException(string role)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var initialScheduledAt = DateTime.UtcNow.AddHours(4);
        var task = CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: initialScheduledAt);
        await db.SaveChangesAsync();

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(8),
            Reason = "Unauthorized attempt to reschedule task."
        };

        var act = () => service.RescheduleTaskAsync(task.Id, request, officer.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers can reschedule collection tasks.*");

        var dbTask = await db.CollectionTasks.FindAsync(task.Id);
        dbTask!.ScheduledAt.Should().Be(initialScheduledAt);
        dbTask.UpdatedAt.Should().BeNull();

        var histories = await db.CollectionTaskScheduleHistories.ToListAsync();
        histories.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleTaskAsync_TaskNotFound_ShouldThrowNotFoundException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var missingTaskId = Guid.NewGuid();

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(5),
            Reason = "Attempt to reschedule missing task."
        };

        var act = () => service.RescheduleTaskAsync(missingTaskId, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingTaskId}*");
    }

    [Theory]
    [InlineData(CollectionTaskStatus.Assigned)]
    [InlineData(CollectionTaskStatus.InProgress)]
    [InlineData(CollectionTaskStatus.Completed)]
    [InlineData(CollectionTaskStatus.Failed)]
    [InlineData(CollectionTaskStatus.Cancelled)]
    public async Task RescheduleTaskAsync_NonScheduledStatuses_ShouldThrowBusinessRuleConflictException(CollectionTaskStatus status)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var initialScheduledAt = DateTime.UtcNow.AddHours(3);
        var task = CreateSampleTask(db, officer.Id, status: status, scheduledAt: initialScheduledAt);
        await db.SaveChangesAsync();

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(7),
            Reason = "Attempt to reschedule active or finalized task."
        };

        var act = () => service.RescheduleTaskAsync(task.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage($"*Current status is '{status}', but only 'Scheduled' tasks can be rescheduled.*");

        var dbTask = await db.CollectionTasks.FindAsync(task.Id);
        dbTask!.ScheduledAt.Should().Be(initialScheduledAt);
        dbTask.Status.Should().Be(status);

        var histories = await db.CollectionTaskScheduleHistories.ToListAsync();
        histories.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleTaskAsync_PastNewScheduledAt_ShouldThrowValidationException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var initialScheduledAt = DateTime.UtcNow.AddHours(4);
        var task = CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: initialScheduledAt);
        await db.SaveChangesAsync();

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddMinutes(-10),
            Reason = "Attempting retroactive rescheduling."
        };

        var act = () => service.RescheduleTaskAsync(task.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*cannot be in the past*");

        var dbTask = await db.CollectionTasks.FindAsync(task.Id);
        dbTask!.ScheduledAt.Should().Be(initialScheduledAt);

        var histories = await db.CollectionTaskScheduleHistories.ToListAsync();
        histories.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    public async Task RescheduleTaskAsync_InvalidReason_ShouldThrowValidationException(string reason)
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var initialScheduledAt = DateTime.UtcNow.AddHours(4);
        var task = CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: initialScheduledAt);
        await db.SaveChangesAsync();

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(6),
            Reason = reason
        };

        var act = () => service.RescheduleTaskAsync(task.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>();

        var dbTask = await db.CollectionTasks.FindAsync(task.Id);
        dbTask!.ScheduledAt.Should().Be(initialScheduledAt);

        var histories = await db.CollectionTaskScheduleHistories.ToListAsync();
        histories.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleTaskAsync_ReasonExceedingMaxLength_ShouldThrowValidationException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var initialScheduledAt = DateTime.UtcNow.AddHours(4);
        var task = CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: initialScheduledAt);
        await db.SaveChangesAsync();

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(6),
            Reason = new string('X', 501)
        };

        var act = () => service.RescheduleTaskAsync(task.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Reason must be between 5 and 500 characters*");

        var dbTask = await db.CollectionTasks.FindAsync(task.Id);
        dbTask!.ScheduledAt.Should().Be(initialScheduledAt);

        var histories = await db.CollectionTaskScheduleHistories.ToListAsync();
        histories.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleTaskAsync_NullNewScheduledAt_ShouldThrowValidationException()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var initialScheduledAt = DateTime.UtcNow.AddHours(4);
        var task = CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: initialScheduledAt);
        await db.SaveChangesAsync();

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = null,
            Reason = "Valid reason with null timestamp"
        };

        var act = () => service.RescheduleTaskAsync(task.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*New scheduled time is required*");

        var dbTask = await db.CollectionTasks.FindAsync(task.Id);
        dbTask!.ScheduledAt.Should().Be(initialScheduledAt);

        var histories = await db.CollectionTaskScheduleHistories.ToListAsync();
        histories.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleTaskAsync_IdenticalScheduledAt_NoOp_ReturnsDetailWithoutHistoryOrUpdatedAtChange()
    {
        var db = CreateContext();
        var service = new CollectionTaskService(db);
        var officer = CreateSampleUser(db);
        var scheduledAt = DateTime.UtcNow.AddHours(5);
        var initialUpdatedAt = DateTime.UtcNow.AddMinutes(-30);
        var task = CreateSampleTask(db, officer.Id, status: CollectionTaskStatus.Scheduled, scheduledAt: scheduledAt);
        task.UpdatedAt = initialUpdatedAt;
        await db.SaveChangesAsync();

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = scheduledAt,
            Reason = "Rescheduling with identical timestamp."
        };

        var result = await service.RescheduleTaskAsync(task.Id, request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.ScheduledAt.Should().Be(scheduledAt);
        result.UpdatedAt.Should().Be(initialUpdatedAt);
        result.ScheduleHistory.Should().BeEmpty();

        var dbTask = await db.CollectionTasks.FindAsync(task.Id);
        dbTask!.ScheduledAt.Should().Be(scheduledAt);
        dbTask.UpdatedAt.Should().Be(initialUpdatedAt);

        var histories = await db.CollectionTaskScheduleHistories.ToListAsync();
        histories.Should().BeEmpty();
    }

    [Fact]
    public async Task RescheduleTaskAsync_ConcurrentModification_ShouldThrowBusinessRuleConflictException()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var officerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var initialTime = DateTime.UtcNow.AddHours(3);

        using (var setupDb = new AppDbContext(options))
        {
            var user = new AppUser
            {
                Id = officerId,
                UserName = "officer.concurrent@example.com",
                Email = "officer.concurrent@example.com",
                FullName = "Officer Concurrent",
                CreatedAt = DateTime.UtcNow
            };
            setupDb.Users.Add(user);

            var task = new CollectionTask
            {
                Id = taskId,
                TaskCode = "TSK-CONCUR-0001",
                Status = CollectionTaskStatus.Scheduled,
                ScheduledAt = initialTime,
                CollectionReason = CollectionReason.FullOrBlockedBin,
                CreatedByUserId = officerId,
                CreationMethod = TaskCreationMethod.Manual,
                CreatedAt = DateTime.UtcNow
            };
            setupDb.CollectionTasks.Add(task);
            await setupDb.SaveChangesAsync();
        }

        // Two services simulating two concurrent requests
        using var db1 = new AppDbContext(options);
        using var db2 = new AppDbContext(options);
        var service1 = new CollectionTaskService(db1);
        var service2 = new CollectionTaskService(db2);

        var newTime1 = DateTime.UtcNow.AddHours(5);
        var newTime2 = DateTime.UtcNow.AddHours(7);

        var req1 = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = newTime1,
            Reason = "First reschedule operation."
        };
        var req2 = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = newTime2,
            Reason = "Stale concurrent reschedule operation."
        };

        // First operation succeeds
        var res1 = await service1.RescheduleTaskAsync(taskId, req1, officerId, AppRoles.WasteOfficer);
        res1.Should().NotBeNull();
        res1.ScheduledAt.Should().BeCloseTo(newTime1, TimeSpan.FromSeconds(5));

        // Second operation with stale prior knowledge:
        var taskInDb2 = await db2.CollectionTasks.FindAsync(taskId);
        taskInDb2!.ScheduledAt = initialTime;

        var act2 = () => service2.RescheduleTaskAsync(taskId, req2, officerId, AppRoles.WasteOfficer);
        await act2.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*concurrently modified or rescheduled*");
    }

    #endregion
}
