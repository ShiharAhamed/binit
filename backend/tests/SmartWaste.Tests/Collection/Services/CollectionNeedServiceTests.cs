using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Collection.Services;

/// <summary>
/// Focused unit tests for CollectionNeedService using EF Core InMemory provider.
/// Verifies authorization, validation, Source A (Verified Reports), Source B (Observations),
/// Source C (Routine Due), active task suppression, overlapping bin handling, and read-only guarantees.
/// </summary>
public class CollectionNeedServiceTests
{
    private static (AppDbContext Db, IConfiguration Configuration) CreateContext(string? timeZoneId = "Asia/Colombo")
    {
        var dbName = $"SmartWaste_CollectionNeed_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Municipality:TimeZoneId"]).Returns(timeZoneId);

        return (db, configMock.Object);
    }

    private static WasteReport CreateSampleReport(
        AppDbContext db,
        WasteReportStatus status = WasteReportStatus.Verified,
        WasteType wasteType = WasteType.General,
        string addressText = "123 Galle Road, Colombo",
        WasteReportPriority? priority = WasteReportPriority.High)
    {
        var report = new WasteReport
        {
            Id = Guid.NewGuid(),
            CitizenId = Guid.NewGuid(),
            Description = "Illegal waste accumulation near pavement",
            WasteType = wasteType,
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = addressText,
            Status = status,
            Priority = priority,
            VerifiedAt = status == WasteReportStatus.Verified ? DateTime.UtcNow.AddHours(-3) : null,
            CreatedAt = DateTime.UtcNow.AddHours(-5)
        };
        db.WasteReports.Add(report);
        return report;
    }

    private static WasteBin CreateSampleBin(
        AppDbContext db,
        string binCode = "BIN-001",
        BinAdministrativeStatus status = BinAdministrativeStatus.Active,
        int[]? weekdays = null,
        DateTime? lastCollectedAt = null,
        DateTime? createdAt = null,
        WasteType wasteType = WasteType.General,
        string addressText = "Market Place, Pettah")
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
            AddressText = addressText
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType
        {
            WasteBinId = bin.Id,
            WasteType = wasteType
        });
        db.WasteBins.Add(bin);
        return bin;
    }

    private static BinObservation CreateSampleObservation(
        AppDbContext db,
        Guid binId,
        int fillLevelPercent = 100,
        BinCondition condition = BinCondition.Good,
        DateTime? recordedAt = null)
    {
        var obs = new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = binId,
            FillLevelPercent = fillLevelPercent,
            Condition = condition,
            Notes = "Field observation",
            RecordedByUserId = Guid.NewGuid(),
            RecordedAt = recordedAt ?? DateTime.UtcNow.AddHours(-1)
        };
        db.BinObservations.Add(obs);
        return obs;
    }

    private static CollectionTask CreateSampleTask(
        AppDbContext db,
        Guid? reportId = null,
        Guid? binId = null,
        CollectionTaskStatus status = CollectionTaskStatus.Scheduled)
    {
        var task = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = $"TSK-{Guid.NewGuid():N}"[..12].ToUpper(),
            WasteReportId = reportId,
            WasteBinId = binId,
            CollectionReason = reportId != null ? CollectionReason.VerifiedReport : CollectionReason.RoutineCollection,
            Status = status,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        db.CollectionTasks.Add(task);
        return task;
    }

    #region 1. Authorization Tests

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task GetCollectionNeedsAsync_StaffRoles_ShouldSucceed(string role)
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), role);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    [InlineData("InvalidRole")]
    public async Task GetCollectionNeedsAsync_NonStaffRoles_ShouldThrowForbiddenException(string role)
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var query = new CollectionNeedListQuery();
        var act = () => service.GetCollectionNeedsAsync(query, Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers and Municipal Managers*");
    }

    #endregion

    #region 2. Source A: Verified Report Needs

    [Fact]
    public async Task GetCollectionNeedsAsync_VerifiedReport_IncludedInQueue()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var report = CreateSampleReport(db, WasteReportStatus.Verified, WasteType.General, "Pettah Market");
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
        var item = result.Items[0];
        item.Id.Should().Be(report.Id);
        item.TargetType.Should().Be("Report");
        item.CollectionReason.Should().Be("VerifiedReport");
        item.Title.Should().StartWith($"Report {report.Id.ToString("N")[..8].ToUpperInvariant()}: ");
        item.Title.Should().Contain("Pettah Market");
        item.WasteTypes.Should().Contain("General");
        item.Urgency.Should().Be("High");
        item.BinDetails.Should().BeNull();
    }

    [Theory]
    [InlineData(WasteReportStatus.Submitted)]
    [InlineData(WasteReportStatus.UnderReview)]
    [InlineData(WasteReportStatus.Rejected)]
    [InlineData(WasteReportStatus.Cancelled)]
    [InlineData(WasteReportStatus.Scheduled)]
    [InlineData(WasteReportStatus.InProgress)]
    [InlineData(WasteReportStatus.Resolved)]
    public async Task GetCollectionNeedsAsync_NonVerifiedReports_ExcludedFromQueue(WasteReportStatus status)
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        CreateSampleReport(db, status);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_VerifiedReport_DoesNotRequireRegisteredBin()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        // Standalone citizen report without any bins in database
        CreateSampleReport(db, WasteReportStatus.Verified);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items[0].TargetType.Should().Be("Report");
        result.Items[0].BinDetails.Should().BeNull();
    }

    [Theory]
    [InlineData(CollectionTaskStatus.Scheduled)]
    [InlineData(CollectionTaskStatus.Assigned)]
    [InlineData(CollectionTaskStatus.InProgress)]
    public async Task GetCollectionNeedsAsync_VerifiedReportWithActiveTask_SuppressedFromQueue(CollectionTaskStatus activeStatus)
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var report = CreateSampleReport(db, WasteReportStatus.Verified);
        CreateSampleTask(db, reportId: report.Id, status: activeStatus);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(CollectionTaskStatus.Completed)]
    [InlineData(CollectionTaskStatus.Failed)]
    [InlineData(CollectionTaskStatus.Cancelled)]
    public async Task GetCollectionNeedsAsync_VerifiedReportWithInactiveTask_NotSuppressed(CollectionTaskStatus inactiveStatus)
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var report = CreateSampleReport(db, WasteReportStatus.Verified);
        CreateSampleTask(db, reportId: report.Id, status: inactiveStatus);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
    }

    #endregion

    #region 3. Source B: Bin Observation Needs

    [Fact]
    public async Task GetCollectionNeedsAsync_Fresh100PercentGoodObservation_ProducesCollectionNeed()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var bin = CreateSampleBin(db, "BIN-100", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        var item = result.Items[0];
        item.Id.Should().Be(bin.Id);
        item.TargetType.Should().Be("Bin");
        item.CollectionReason.Should().Be("FullOrBlockedBin");
        item.Title.Should().Be("BIN-100 (100% Full)");
        item.BinDetails.Should().NotBeNull();
        item.BinDetails!.LatestFillLevelPercent.Should().Be(100);
        item.BinDetails.LatestCondition.Should().Be(BinCondition.Good);
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_FreshBlockedObservation_ProducesCollectionNeed()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var bin = CreateSampleBin(db, "BIN-BLK", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 50, condition: BinCondition.Blocked);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        var item = result.Items[0];
        item.CollectionReason.Should().Be("FullOrBlockedBin");
        item.Title.Should().Be("BIN-BLK (Blocked)");
        item.BinDetails!.LatestCondition.Should().Be(BinCondition.Blocked);
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_75PercentGoodObservation_DoesNotProduceCollectionNeed_WarningOnly()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var bin = CreateSampleBin(db, "BIN-075", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 75, condition: BinCondition.Good);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        // 75% is warning only, NOT an ordinary collection trigger
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(BinCondition.Damaged)]
    [InlineData(BinCondition.Missing)]
    public async Task GetCollectionNeedsAsync_DamagedOrMissingCondition_DoesNotProduceOrdinaryCollectionNeed(BinCondition condition)
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        // Even with 100% fill level, damaged/missing is maintenance concern rather than ordinary collection
        var bin = CreateSampleBin(db, "BIN-DMG", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: condition);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(BinAdministrativeStatus.OutOfService)]
    [InlineData(BinAdministrativeStatus.Retired)]
    public async Task GetCollectionNeedsAsync_OutOfServiceOrRetiredBins_ExcludedFromOrdinaryCollection(BinAdministrativeStatus status)
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var bin = CreateSampleBin(db, "BIN-INA", status: status, weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_BinWithoutObservation_DoesNotDefaultToFullOrProduceNeed()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        CreateSampleBin(db, "BIN-NO-OBS", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>());
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_PreCollectionObservation_NotTreatedAsCurrentPostCollectionEvidence()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var obsTime = DateTime.UtcNow.AddHours(-4);
        var collectionTime = DateTime.UtcNow.AddHours(-2); // Collected AFTER observation

        var bin = CreateSampleBin(db, "BIN-COLL", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>(), lastCollectedAt: collectionTime);
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good, recordedAt: obsTime);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        // Pre-collection observation was cleared by the subsequent collection
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_StaleObservationOlderThan48Hours_DoesNotProduceNeed()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var staleTime = DateTime.UtcNow.AddHours(-49);
        var bin = CreateSampleBin(db, "BIN-STALE", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good, recordedAt: staleTime);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_LatestObservationOrdering_UsesRecordedAtDescThenIdDesc()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var bin = CreateSampleBin(db, "BIN-ORD", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>());
        
        // Older observation was 100% full
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good, recordedAt: DateTime.UtcNow.AddHours(-10));
        // Newer observation was 25% full
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 25, condition: BinCondition.Good, recordedAt: DateTime.UtcNow.AddHours(-1));
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        // Latest observation is 25%, which is not full
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_ActiveTaskOnBin_SuppressesBinObservationNeed()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var bin = CreateSampleBin(db, "BIN-ACT", BinAdministrativeStatus.Active, weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good);
        CreateSampleTask(db, binId: bin.Id, status: CollectionTaskStatus.Scheduled);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    #endregion

    #region 4. Source C: Routine Collection Due Needs

    [Fact]
    public async Task GetCollectionNeedsAsync_EmptyWeekdaysArray_DoesNotProduceRoutineNeed()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        CreateSampleBin(db, "BIN-ON-DEMAND", weekdays: Array.Empty<int>());
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_NeverCollectedBin_BecomesDueOnConfiguredWeekday()
    {
        var (db, config) = CreateContext("Asia/Colombo");
        var service = new CollectionNeedService(db, config);

        // Calculate today's ISO weekday in Colombo
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayWeekday = (int)localNow.DayOfWeek == 0 ? 7 : (int)localNow.DayOfWeek;

        // Configure bin for today's weekday
        var bin = CreateSampleBin(db, "BIN-ROUTINE", weekdays: new[] { todayWeekday }, lastCollectedAt: null);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        var item = result.Items[0];
        item.Id.Should().Be(bin.Id);
        item.TargetType.Should().Be("Bin");
        item.CollectionReason.Should().Be("RoutineCollection");
        item.Title.Should().Be("BIN-ROUTINE (Routine Collection Due)");
        item.Urgency.Should().Be("Medium");
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_MissedWeekday_RemainsOverdueOnSubsequentDays()
    {
        var (db, config) = CreateContext("Asia/Colombo");
        var service = new CollectionNeedService(db, config);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var localToday = DateOnly.FromDateTime(localNow);

        // Configure bin created 5 days ago with weekdays matching yesterday's weekday
        var yesterday = localToday.AddDays(-1);
        var yesterdayWeekday = (int)yesterday.DayOfWeek == 0 ? 7 : (int)yesterday.DayOfWeek;

        var bin = CreateSampleBin(
            db,
            "BIN-MISSED",
            weekdays: new[] { yesterdayWeekday },
            createdAt: DateTime.UtcNow.AddDays(-5),
            lastCollectedAt: null);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items[0].CollectionReason.Should().Be("RoutineCollection");
        // Trigger date reflects the missed day start in UTC
        var expectedTriggerUtc = TimeZoneInfo.ConvertTimeToUtc(yesterday.ToDateTime(TimeOnly.MinValue), tz);
        result.Items[0].TriggerDate.Should().Be(expectedTriggerUtc);
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_CollectionCompletedToday_ClearsDueStatusForToday()
    {
        var (db, config) = CreateContext("Asia/Colombo");
        var service = new CollectionNeedService(db, config);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayWeekday = (int)localNow.DayOfWeek == 0 ? 7 : (int)localNow.DayOfWeek;

        // Collected 1 hour ago (which is today in local calendar)
        var bin = CreateSampleBin(
            db,
            "BIN-DONE-TODAY",
            weekdays: new[] { todayWeekday },
            lastCollectedAt: DateTime.UtcNow.AddHours(-1));
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        // Bin was collected today, so it is NOT due again today
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_ActiveTaskOnRoutineBin_SuppressesNeed()
    {
        var (db, config) = CreateContext("Asia/Colombo");
        var service = new CollectionNeedService(db, config);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayWeekday = (int)localNow.DayOfWeek == 0 ? 7 : (int)localNow.DayOfWeek;

        var bin = CreateSampleBin(db, "BIN-ROUTINE-ACT", weekdays: new[] { todayWeekday });
        CreateSampleTask(db, binId: bin.Id, status: CollectionTaskStatus.Assigned);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery();
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    #endregion

    #region 5. Overlapping Needs & Unified Query Tests

    [Fact]
    public async Task GetCollectionNeedsAsync_OverlappingBinNeeds_UnifiedView_PrioritizesFullOrBlockedWithoutDuplicates()
    {
        var (db, config) = CreateContext("Asia/Colombo");
        var service = new CollectionNeedService(db, config);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayWeekday = (int)localNow.DayOfWeek == 0 ? 7 : (int)localNow.DayOfWeek;

        // Bin is BOTH routine due today AND 100% full
        var bin = CreateSampleBin(db, "BIN-DUAL", weekdays: new[] { todayWeekday });
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery(); // Unified queue without reason filter
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        // Single item in queue (no duplicate entry for same physical bin)
        result.TotalCount.Should().Be(1);
        var item = result.Items[0];
        item.Id.Should().Be(bin.Id);
        item.CollectionReason.Should().Be("FullOrBlockedBin"); // Higher acute priority
        item.Urgency.Should().Be("High");
        item.BinDetails!.LatestFillLevelPercent.Should().Be(100);
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_OverlappingBinNeeds_FilteredByRoutine_ReturnsRoutineNeed()
    {
        var (db, config) = CreateContext("Asia/Colombo");
        var service = new CollectionNeedService(db, config);

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var todayWeekday = (int)localNow.DayOfWeek == 0 ? 7 : (int)localNow.DayOfWeek;

        var bin = CreateSampleBin(db, "BIN-DUAL", weekdays: new[] { todayWeekday });
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good);
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery { CollectionReason = "RoutineCollection" };
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items[0].CollectionReason.Should().Be("RoutineCollection");
        result.Items[0].BinDetails!.LatestFillLevelPercent.Should().Be(100);
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_TargetTypeFilter_WorksCorrectly()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var report = CreateSampleReport(db, WasteReportStatus.Verified);
        var bin = CreateSampleBin(db, "BIN-OBS", weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good);
        await db.SaveChangesAsync();

        // Query only Reports
        var reportResult = await service.GetCollectionNeedsAsync(
            new CollectionNeedListQuery { TargetType = "Report" },
            Guid.NewGuid(),
            AppRoles.WasteOfficer);

        reportResult.TotalCount.Should().Be(1);
        reportResult.Items[0].TargetType.Should().Be("Report");

        // Query only Bins
        var binResult = await service.GetCollectionNeedsAsync(
            new CollectionNeedListQuery { TargetType = "Bin" },
            Guid.NewGuid(),
            AppRoles.WasteOfficer);

        binResult.TotalCount.Should().Be(1);
        binResult.Items[0].TargetType.Should().Be("Bin");
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_WasteTypeFilter_FiltersCorrectly()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        CreateSampleReport(db, WasteReportStatus.Verified, wasteType: WasteType.General);
        CreateSampleReport(db, WasteReportStatus.Verified, wasteType: WasteType.Hazardous);
        await db.SaveChangesAsync();

        var result = await service.GetCollectionNeedsAsync(
            new CollectionNeedListQuery { WasteType = WasteType.Hazardous },
            Guid.NewGuid(),
            AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items[0].WasteTypes.Should().Contain("Hazardous");
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_SearchFilter_MatchesAddressOrDescription()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        CreateSampleReport(db, WasteReportStatus.Verified, addressText: "Kollupitiya Junction");
        CreateSampleReport(db, WasteReportStatus.Verified, addressText: "Nugegoda High Level Rd");
        await db.SaveChangesAsync();

        var result = await service.GetCollectionNeedsAsync(
            new CollectionNeedListQuery { Search = "Kollupitiya" },
            Guid.NewGuid(),
            AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items[0].AddressText.Should().Contain("Kollupitiya");
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_Pagination_ReturnsCorrectPageAndTotalCount()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        for (var i = 1; i <= 5; i++)
        {
            CreateSampleReport(db, WasteReportStatus.Verified, addressText: $"Location {i}");
        }
        await db.SaveChangesAsync();

        var query = new CollectionNeedListQuery { Page = 2, PageSize = 2 };
        var result = await service.GetCollectionNeedsAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_Ordering_OrdersByUrgencyThenTriggerDateAsc()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        // Report 1: Medium urgency, created 5 hours ago
        var r1 = CreateSampleReport(db, WasteReportStatus.Verified, priority: WasteReportPriority.Medium);
        r1.CreatedAt = DateTime.UtcNow.AddHours(-5);
        r1.VerifiedAt = DateTime.UtcNow.AddHours(-5);

        // Report 2: Urgent, created 2 hours ago
        var r2 = CreateSampleReport(db, WasteReportStatus.Verified, priority: WasteReportPriority.Urgent);
        r2.CreatedAt = DateTime.UtcNow.AddHours(-2);
        r2.VerifiedAt = DateTime.UtcNow.AddHours(-2);

        // Report 3: Urgent, created 4 hours ago (older urgent)
        var r3 = CreateSampleReport(db, WasteReportStatus.Verified, priority: WasteReportPriority.Urgent);
        r3.CreatedAt = DateTime.UtcNow.AddHours(-4);
        r3.VerifiedAt = DateTime.UtcNow.AddHours(-4);

        await db.SaveChangesAsync();

        var result = await service.GetCollectionNeedsAsync(
            new CollectionNeedListQuery(),
            Guid.NewGuid(),
            AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(3);
        // Urgent precedes Medium. Among Urgent, older (r3) precedes newer (r2)
        result.Items[0].Id.Should().Be(r3.Id);
        result.Items[1].Id.Should().Be(r2.Id);
        result.Items[2].Id.Should().Be(r1.Id);
    }

    [Fact]
    public async Task GetCollectionNeedsAsync_ReadOnlyGuarantee_ZeroMutationsToDatabase()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var report = CreateSampleReport(db, WasteReportStatus.Verified);
        var bin = CreateSampleBin(db, "BIN-READONLY", weekdays: Array.Empty<int>());
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good);
        await db.SaveChangesAsync();

        var reportsBefore = await db.WasteReports.AsNoTracking().ToListAsync();
        var binsBefore = await db.WasteBins.AsNoTracking().ToListAsync();
        var tasksBefore = await db.CollectionTasks.AsNoTracking().ToListAsync();

        await service.GetCollectionNeedsAsync(new CollectionNeedListQuery(), Guid.NewGuid(), AppRoles.WasteOfficer);

        var reportsAfter = await db.WasteReports.AsNoTracking().ToListAsync();
        var binsAfter = await db.WasteBins.AsNoTracking().ToListAsync();
        var tasksAfter = await db.CollectionTasks.AsNoTracking().ToListAsync();

        reportsAfter.Should().BeEquivalentTo(reportsBefore);
        binsAfter.Should().BeEquivalentTo(binsBefore);
        tasksAfter.Should().BeEquivalentTo(tasksBefore);
    }

    [Fact]
    public async Task GetCollectionNeedsForAiAsync_ReturnsOnlyTheAllowListedProjection()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);

        var report = CreateSampleReport(
            db,
            WasteReportStatus.Verified,
            WasteType.Hazardous,
            "AI-safe report location",
            WasteReportPriority.Urgent);
        var bin = CreateSampleBin(db, "BIN-AI-100", weekdays: Array.Empty<int>(), wasteType: WasteType.Organic);
        CreateSampleObservation(db, bin.Id, fillLevelPercent: 100, condition: BinCondition.Good);
        await db.SaveChangesAsync();

        var result = await service.GetCollectionNeedsForAiAsync(
            new GetCollectionNeedsForAiQuery { Page = 1, PageSize = 20 });

        result.TotalCount.Should().Be(2);
        var reportNeed = result.Items.Should().ContainSingle(item => item.WasteReportId == report.Id).Subject;
        reportNeed.TargetType.Should().Be("Report");
        reportNeed.Id.Should().Be(report.Id);
        reportNeed.ReportReference.Should().Be(report.Id.ToString("N")[..8].ToUpperInvariant());
        using (var json = JsonDocument.Parse(JsonSerializer.Serialize(reportNeed, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
        {
            json.RootElement.GetProperty("wasteReportId").GetGuid().Should().Be(report.Id);
            json.RootElement.GetProperty("reportReference").GetString().Should().Be(reportNeed.ReportReference);
        }
        reportNeed.CollectionReason.Should().Be("VerifiedReport");
        reportNeed.Urgency.Should().Be("Urgent");
        reportNeed.WasteTypes.Should().ContainSingle().Which.Should().Be("Hazardous");
        reportNeed.BinTelemetry.Should().BeNull();

        var binNeed = result.Items.Should().ContainSingle(item => item.WasteBinId == bin.Id).Subject;
        binNeed.TargetType.Should().Be("Bin");
        binNeed.Id.Should().Be(bin.Id);
        binNeed.BinCode.Should().Be("BIN-AI-100");
        using (var json = JsonDocument.Parse(JsonSerializer.Serialize(binNeed, new JsonSerializerOptions(JsonSerializerDefaults.Web))))
        {
            json.RootElement.GetProperty("wasteBinId").GetGuid().Should().Be(bin.Id);
            json.RootElement.GetProperty("binCode").GetString().Should().Be(bin.BinCode);
        }
        binNeed.CollectionReason.Should().Be("FullOrBlockedBin");
        binNeed.BinTelemetry.Should().NotBeNull();
        binNeed.BinTelemetry!.BinCode.Should().Be("BIN-AI-100");
        binNeed.BinTelemetry.CapacityLiters.Should().Be(660);
        binNeed.BinTelemetry.LatestFillLevelPercent.Should().Be(100);

        typeof(SmartWaste.Application.Collection.DTOs.Responses.CollectionNeedToolItemDto)
            .GetProperty("Title").Should().BeNull("the internal tool must not expose staff-only titles or report descriptions");
        typeof(SmartWaste.Application.Collection.DTOs.Responses.CollectionNeedToolItemDto)
            .GetProperty("AttachmentCount").Should().BeNull("the internal tool must not expose attachments");
    }

    [Fact]
    public async Task GetCollectionNeedsForAiAsync_TargetDateUsesMunicipalityLocalDateForRoutineNeeds()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);
        var targetDate = new DateOnly(2026, 1, 5); // Monday in Asia/Colombo

        var bin = CreateSampleBin(
            db,
            "BIN-AI-ROUTINE",
            weekdays: new[] { 1 },
            createdAt: new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        await db.SaveChangesAsync();

        var result = await service.GetCollectionNeedsForAiAsync(new GetCollectionNeedsForAiQuery
        {
            TargetType = "Bin",
            CollectionReason = "RoutineCollection",
            TargetDate = targetDate
        });

        result.Items.Should().ContainSingle();
        var need = result.Items[0];
        need.WasteBinId.Should().Be(bin.Id);
        need.CollectionReason.Should().Be("RoutineCollection");
        need.TriggerDate.Should().Be(new DateTime(2026, 1, 4, 18, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetCollectionNeedsForAiAsync_ActiveTasksRemainSuppressedAndTheReadDoesNotMutateData()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);
        var report = CreateSampleReport(db, WasteReportStatus.Verified);
        CreateSampleTask(db, reportId: report.Id, status: CollectionTaskStatus.Scheduled);
        await db.SaveChangesAsync();

        var reportsBefore = await db.WasteReports.AsNoTracking().ToListAsync();
        var tasksBefore = await db.CollectionTasks.AsNoTracking().ToListAsync();

        var result = await service.GetCollectionNeedsForAiAsync(new GetCollectionNeedsForAiQuery());

        result.Items.Should().BeEmpty();
        (await db.WasteReports.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(reportsBefore);
        (await db.CollectionTasks.AsNoTracking().ToListAsync()).Should().BeEquivalentTo(tasksBefore);
    }

    [Fact]
    public async Task GetCollectionNeedsForAiAsync_PreservesUrgencyOrderingAndPagination()
    {
        var (db, config) = CreateContext();
        var service = new CollectionNeedService(db, config);
        var low = CreateSampleReport(db, priority: WasteReportPriority.Low);
        var urgent = CreateSampleReport(db, priority: WasteReportPriority.Urgent);
        await db.SaveChangesAsync();

        var result = await service.GetCollectionNeedsForAiAsync(new GetCollectionNeedsForAiQuery
        {
            Page = 1,
            PageSize = 1
        });

        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(2);
        result.Items.Should().ContainSingle();
        result.Items[0].WasteReportId.Should().Be(urgent.Id);
        result.Items[0].WasteReportId.Should().NotBe(low.Id);
    }

    #endregion
}
