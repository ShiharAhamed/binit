using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Collection.Services;

/// <summary>
/// Focused unit tests for BinObservationService using EF Core InMemory provider.
/// Verifies authorization, validation, append-only persistence, server audit fields,
/// absence of unintended task/report side effects, and chronological history retrieval.
/// </summary>
public class BinObservationServiceTests
{
    private static (AppDbContext Db, UserManager<AppUser> UserManager) CreateContext()
    {
        var dbName = $"SmartWaste_Observation_{Guid.NewGuid():N}";
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

    private static WasteBin CreateSampleBin(AppDbContext db, BinAdministrativeStatus status = BinAdministrativeStatus.Active)
    {
        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = $"BIN-{Guid.NewGuid():N}"[..12].ToUpper(),
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 1100,
            AdministrativeStatus = status,
            CollectionWeekdays = new[] { 1, 3, 5 },
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastCollectedAt = null
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = bin.Id, WasteType = WasteType.General });
        db.WasteBins.Add(bin);
        return bin;
    }

    private static AppUser CreateSampleUser(AppDbContext db, string role, string fullName)
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

    #region Observation Recording Tests

    [Fact]
    public async Task RecordObservationAsync_WasteOfficer_ValidData_ShouldSucceedAndPersistAuthoritativeAudit()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        var officer = CreateSampleUser(db, AppRoles.WasteOfficer, "Officer Silva");
        await db.SaveChangesAsync();

        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 75,
            Condition = BinCondition.Good,
            Notes = "Routine inspection. Bin is in good working order."
        };

        var beforeTime = DateTime.UtcNow.AddSeconds(-1);
        var result = await service.RecordObservationAsync(bin.Id, request, officer.Id, AppRoles.WasteOfficer);
        var afterTime = DateTime.UtcNow.AddSeconds(1);

        result.Should().NotBeNull();
        result.WasteBinId.Should().Be(bin.Id);
        result.FillLevelPercent.Should().Be(75);
        result.Condition.Should().Be(BinCondition.Good);
        result.Notes.Should().Be("Routine inspection. Bin is in good working order.");
        result.RecordedByUserId.Should().Be(officer.Id);
        result.RecordedByUserName.Should().Be("Officer Silva");
        result.RecordedAt.Should().BeOnOrAfter(beforeTime).And.BeOnOrBefore(afterTime);

        // Verify in database
        var inDb = await db.BinObservations.FindAsync(result.Id);
        inDb.Should().NotBeNull();
        inDb!.RecordedByUserId.Should().Be(officer.Id);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task RecordObservationAsync_UnauthorizedRoles_ShouldThrowForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        await db.SaveChangesAsync();

        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 50,
            Condition = BinCondition.Good
        };

        var act = () => service.RecordObservationAsync(bin.Id, request, Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers can record bin observations*");
    }

    [Fact]
    public async Task RecordObservationAsync_MissingBin_ShouldThrowNotFoundException()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var missingBinId = Guid.NewGuid();

        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 50,
            Condition = BinCondition.Good
        };

        var act = () => service.RecordObservationAsync(missingBinId, request, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingBinId}*");
    }

    [Fact]
    public async Task RecordObservationAsync_RetiredBin_ShouldThrowBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db, BinAdministrativeStatus.Retired);
        await db.SaveChangesAsync();

        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 50,
            Condition = BinCondition.Good
        };

        var act = () => service.RecordObservationAsync(bin.Id, request, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*retired waste bin*");
    }

    [Fact]
    public async Task RecordObservationAsync_OutOfServiceBin_ShouldSucceed()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db, BinAdministrativeStatus.OutOfService);
        var officer = CreateSampleUser(db, AppRoles.WasteOfficer, "Officer Perera");
        await db.SaveChangesAsync();

        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 25,
            Condition = BinCondition.Damaged,
            Notes = "Damaged hinge awaiting repair in depot"
        };

        var result = await service.RecordObservationAsync(bin.Id, request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Condition.Should().Be(BinCondition.Damaged);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(33)]
    [InlineData(-25)]
    [InlineData(120)]
    public async Task RecordObservationAsync_InvalidFillLevel_ShouldThrowValidationException(int fillLevel)
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        await db.SaveChangesAsync();

        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = fillLevel,
            Condition = BinCondition.Good
        };

        var act = () => service.RecordObservationAsync(bin.Id, request, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RecordObservationAsync_ExcessivelyLongNotes_ShouldThrowValidationException()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        await db.SaveChangesAsync();

        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 50,
            Condition = BinCondition.Good,
            Notes = new string('N', 501)
        };

        var act = () => service.RecordObservationAsync(bin.Id, request, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task RecordObservationAsync_PreservesPreviousObservations_AppendOnly()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        var officer = CreateSampleUser(db, AppRoles.WasteOfficer, "Officer Silva");
        await db.SaveChangesAsync();

        var request1 = new RecordBinObservationRequest
        {
            FillLevelPercent = 25,
            Condition = BinCondition.Good,
            Notes = "First observation"
        };

        var request2 = new RecordBinObservationRequest
        {
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            Notes = "Second observation - full"
        };

        var obs1 = await service.RecordObservationAsync(bin.Id, request1, officer.Id, AppRoles.WasteOfficer);
        var obs2 = await service.RecordObservationAsync(bin.Id, request2, officer.Id, AppRoles.WasteOfficer);

        obs1.Id.Should().NotBe(obs2.Id);

        var allObservations = await db.BinObservations
            .Where(o => o.WasteBinId == bin.Id)
            .OrderBy(o => o.RecordedAt)
            .ToListAsync();

        allObservations.Should().HaveCount(2);
        allObservations[0].Notes.Should().Be("First observation");
        allObservations[1].Notes.Should().Be("Second observation - full");
    }

    [Fact]
    public async Task RecordObservationAsync_DoesNotCreateCollectionTaskOrModifyBinState()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        var officer = CreateSampleUser(db, AppRoles.WasteOfficer, "Officer Silva");
        await db.SaveChangesAsync();

        // 100% full observation
        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 100,
            Condition = BinCondition.Blocked,
            Notes = "Bin is full and blocked by street cart"
        };

        await service.RecordObservationAsync(bin.Id, request, officer.Id, AppRoles.WasteOfficer);

        // 1. Invariant check: Zero collection tasks created
        var taskCount = await db.CollectionTasks.CountAsync();
        taskCount.Should().Be(0);

        // 2. Invariant check: Bin administrative status and LastCollectedAt unchanged
        var binInDb = await db.WasteBins.FindAsync(bin.Id);
        binInDb.Should().NotBeNull();
        binInDb!.AdministrativeStatus.Should().Be(BinAdministrativeStatus.Active);
        binInDb.LastCollectedAt.Should().BeNull();
    }

    #endregion

    #region Observation History Tests

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task GetObservationHistoryAsync_StaffRoles_ShouldSucceed(string role)
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        var officer = CreateSampleUser(db, AppRoles.WasteOfficer, "Officer Silva");

        db.BinObservations.Add(new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 50,
            Condition = BinCondition.Good,
            RecordedByUserId = officer.Id,
            RecordedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new ObservationListQuery { Page = 1, PageSize = 20 };
        var result = await service.GetObservationHistoryAsync(bin.Id, query, officer.Id, role);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items[0].RecordedByUserName.Should().Be("Officer Silva");
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task GetObservationHistoryAsync_NonStaffRoles_ShouldThrowForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        await db.SaveChangesAsync();

        var query = new ObservationListQuery();
        var act = () => service.GetObservationHistoryAsync(bin.Id, query, Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers and Municipal Managers*");
    }

    [Fact]
    public async Task GetObservationHistoryAsync_MissingBin_ShouldThrowNotFoundException()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var missingBinId = Guid.NewGuid();

        var query = new ObservationListQuery();
        var act = () => service.GetObservationHistoryAsync(missingBinId, query, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingBinId}*");
    }

    [Fact]
    public async Task GetObservationHistoryAsync_ScopedToRequestedBinOnly()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin1 = CreateSampleBin(db);
        var bin2 = CreateSampleBin(db);
        var officer = CreateSampleUser(db, AppRoles.WasteOfficer, "Officer Silva");

        db.BinObservations.AddRange(
            new BinObservation
            {
                Id = Guid.NewGuid(),
                WasteBinId = bin1.Id,
                FillLevelPercent = 50,
                Condition = BinCondition.Good,
                RecordedByUserId = officer.Id,
                RecordedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new BinObservation
            {
                Id = Guid.NewGuid(),
                WasteBinId = bin2.Id,
                FillLevelPercent = 75,
                Condition = BinCondition.Good,
                RecordedByUserId = officer.Id,
                RecordedAt = DateTime.UtcNow.AddMinutes(-2)
            }
        );
        await db.SaveChangesAsync();

        var query = new ObservationListQuery();
        var result = await service.GetObservationHistoryAsync(bin1.Id, query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items[0].WasteBinId.Should().Be(bin1.Id);
        result.Items[0].FillLevelPercent.Should().Be(50);
    }

    [Fact]
    public async Task GetObservationHistoryAsync_DeterministicOrdering_RecordedAtDescThenIdDesc()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        var officer = CreateSampleUser(db, AppRoles.WasteOfficer, "Officer Silva");

        var fixedTimestamp = new DateTime(2026, 9, 21, 10, 0, 0, DateTimeKind.Utc);

        // Two observations with IDENTICAL timestamp
        var idSmall = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var idLarge = Guid.Parse("99999999-9999-9999-9999-999999999999");

        db.BinObservations.AddRange(
            new BinObservation
            {
                Id = idSmall,
                WasteBinId = bin.Id,
                FillLevelPercent = 25,
                Condition = BinCondition.Good,
                RecordedByUserId = officer.Id,
                RecordedAt = fixedTimestamp
            },
            new BinObservation
            {
                Id = idLarge,
                WasteBinId = bin.Id,
                FillLevelPercent = 75,
                Condition = BinCondition.Good,
                RecordedByUserId = officer.Id,
                RecordedAt = fixedTimestamp
            }
        );
        await db.SaveChangesAsync();

        var query = new ObservationListQuery { Page = 1, PageSize = 10 };
        var result = await service.GetObservationHistoryAsync(bin.Id, query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(2);
        // Tie breaker: Id DESC, so idLarge precedes idSmall
        result.Items[0].Id.Should().Be(idLarge);
        result.Items[1].Id.Should().Be(idSmall);
    }

    [Fact]
    public async Task GetObservationHistoryAsync_Pagination_ReturnsCorrectSliceAndTotalCount()
    {
        var (db, userManager) = CreateContext();
        var service = new BinObservationService(db, userManager);
        var bin = CreateSampleBin(db);
        var officer = CreateSampleUser(db, AppRoles.WasteOfficer, "Officer Silva");

        for (var i = 1; i <= 5; i++)
        {
            db.BinObservations.Add(new BinObservation
            {
                Id = Guid.NewGuid(),
                WasteBinId = bin.Id,
                FillLevelPercent = i * 20,
                Condition = BinCondition.Good,
                RecordedByUserId = officer.Id,
                RecordedAt = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var query = new ObservationListQuery { Page = 2, PageSize = 2 };
        var result = await service.GetObservationHistoryAsync(bin.Id, query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    #endregion
}
