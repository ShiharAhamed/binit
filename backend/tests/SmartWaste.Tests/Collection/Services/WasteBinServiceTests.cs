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
/// Focused unit tests for WasteBinService using EF Core InMemory provider.
/// Verifies authorization, validation, unique code enforcement, public availability state machine,
/// observation freshness, and proximity discovery.
/// </summary>
public class WasteBinServiceTests
{
    private static (AppDbContext Db, UserManager<AppUser> UserManager) CreateContext()
    {
        var dbName = $"SmartWaste_Collection_{Guid.NewGuid():N}";
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

    private class ThrowingAppDbContext : AppDbContext
    {
        private readonly Exception _exceptionToThrow;

        public ThrowingAppDbContext(DbContextOptions<AppDbContext> options, Exception exceptionToThrow)
            : base(options)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw _exceptionToThrow;
        }
    }

    private static (AppDbContext Db, UserManager<AppUser> UserManager) CreateThrowingContext(Exception exceptionToThrow)
    {
        var dbName = $"SmartWaste_Collection_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new ThrowingAppDbContext(options, exceptionToThrow);

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

    #region Registration (CreateAsync) Tests

    [Fact]
    public async Task CreateAsync_WasteOfficer_ValidData_ShouldSucceed()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0001",
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Main Street, Pettah",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Recyclable },
            CollectionWeekdays = new[] { 5, 1, 3 } // un-ordered
        };

        var result = await service.CreateAsync(request, officerId, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.BinCode.Should().Be("BIN-COL-0001");
        result.CapacityLiters.Should().Be(1100);
        result.AdministrativeStatus.Should().Be(BinAdministrativeStatus.Active);
        result.AcceptedWasteTypes.Should().BeEquivalentTo(new[] { "General", "Recyclable" });
        result.CollectionWeekdays.Should().Equal(new[] { 1, 3, 5 }); // normalized: sorted and distinct
        result.HasActiveTask.Should().BeFalse();
        result.LatestObservation.Should().BeNull();

        var inDb = await db.WasteBins.Include(b => b.AcceptedWasteTypes).FirstOrDefaultAsync(b => b.BinCode == "BIN-COL-0001");
        inDb.Should().NotBeNull();
        inDb!.AcceptedWasteTypes.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task CreateAsync_UnauthorizedRoles_ShouldThrowForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);

        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0002",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var act = () => service.CreateAsync(request, Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers*");
    }

    [Fact]
    public async Task CreateAsync_DuplicateBinCode_ShouldThrowBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var request1 = new CreateWasteBinRequest
        {
            BinCode = "BIN-DUP-0001",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        await service.CreateAsync(request1, officerId, AppRoles.WasteOfficer);

        var request2 = new CreateWasteBinRequest
        {
            BinCode = "bin-dup-0001", // case-insensitive match
            Latitude = 6.9300,
            Longitude = 79.8700,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.Organic },
            CollectionWeekdays = new[] { 2 }
        };

        var act = () => service.CreateAsync(request2, officerId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_ZeroAcceptedWasteTypes_ShouldThrowValidationException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);

        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-EMPTY-TYPES",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = Array.Empty<WasteType>(),
            CollectionWeekdays = new[] { 1 }
        };

        var act = () => service.CreateAsync(request, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAsync_LowerCaseCode_ShouldStoreAndReturnCanonicalUppercase()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var request = new CreateWasteBinRequest
        {
            BinCode = "bin-col-0042",
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Test Street",
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var result = await service.CreateAsync(request, officerId, AppRoles.WasteOfficer);

        result.BinCode.Should().Be("BIN-COL-0042");
        var inDb = await db.WasteBins.FirstOrDefaultAsync(b => b.Id == result.Id);
        inDb.Should().NotBeNull();
        inDb!.BinCode.Should().Be("BIN-COL-0042");
    }

    [Fact]
    public async Task CreateAsync_UpperCaseCode_ShouldStoreAndReturnCanonicalUppercase()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0043",
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Test Street",
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var result = await service.CreateAsync(request, officerId, AppRoles.WasteOfficer);

        result.BinCode.Should().Be("BIN-COL-0043");
        var inDb = await db.WasteBins.FirstOrDefaultAsync(b => b.Id == result.Id);
        inDb.Should().NotBeNull();
        inDb!.BinCode.Should().Be("BIN-COL-0043");
    }

    [Theory]
    [InlineData(" BIN-COL-0044")]
    [InlineData("BIN-COL-0044 ")]
    [InlineData(" BIN-COL-0044 ")]
    public async Task CreateAsync_WhitespaceInBinCode_ValidatorRejectsAccordingToContract(string codeWithWhitespace)
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);

        var request = new CreateWasteBinRequest
        {
            BinCode = codeWithWhitespace,
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var act = () => service.CreateAsync(request, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*alphanumeric characters and hyphens*");
    }

    [Fact]
    public async Task CreateAsync_SequentialCaseVariantDuplicate_ShouldThrowBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var request1 = new CreateWasteBinRequest
        {
            BinCode = "bin-dup-0099",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };
        await service.CreateAsync(request1, officerId, AppRoles.WasteOfficer);

        var request2 = new CreateWasteBinRequest
        {
            BinCode = "BIN-DUP-0099",
            Latitude = 6.9300,
            Longitude = 79.8700,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.Organic },
            CollectionWeekdays = new[] { 2 }
        };

        var act = () => service.CreateAsync(request2, officerId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*BIN-DUP-0099*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_PreservesBinCode_DoesNotModifyCode()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var createReq = new CreateWasteBinRequest
        {
            BinCode = "bin-preserve-01",
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Original Address",
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };
        var created = await service.CreateAsync(createReq, officerId, AppRoles.WasteOfficer);
        created.BinCode.Should().Be("BIN-PRESERVE-01");

        var updateReq = new UpdateWasteBinRequest
        {
            Latitude = 6.9350,
            Longitude = 79.8700,
            AddressText = "Updated Address",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.Recyclable },
            CollectionWeekdays = new[] { 3 }
        };

        var updated = await service.UpdateAsync(created.Id, updateReq, officerId, AppRoles.WasteOfficer);

        updated.BinCode.Should().Be("BIN-PRESERVE-01");
        var inDb = await db.WasteBins.FindAsync(created.Id);
        inDb!.BinCode.Should().Be("BIN-PRESERVE-01");
    }

    [Theory]
    [InlineData("duplicate key value violates unique constraint \"IX_WasteBins_BinCode\"")]
    [InlineData("duplicate key value violates unique constraint \"IX_WasteBins_BinCode_CaseInsensitive\"")]
    public async Task CreateAsync_ConfirmedBinCodeDbUniqueViolation_ShouldThrowBusinessRuleConflictException(string constraintMessage)
    {
        var dbEx = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new Exception(constraintMessage));

        var (db, userManager) = CreateThrowingContext(dbEx);
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-CONFLICT-01",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var act = () => service.CreateAsync(request, officerId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*BIN-CONFLICT-01*already exists*");
    }

    [Fact]
    public async Task CreateAsync_UnrelatedDbFailure_ShouldRethrowDbUpdateException()
    {
        var dbEx = new DbUpdateException(
            "Database connection reset",
            new Exception("Connection refused by host"));

        var (db, userManager) = CreateThrowingContext(dbEx);
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-FAIL-01",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var act = () => service.CreateAsync(request, officerId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<DbUpdateException>()
            .WithMessage("*Database connection reset*");
    }

    #endregion

    #region Update & Deactivate Tests

    [Fact]
    public async Task UpdateAsync_WasteOfficer_ShouldUpdateMetadataAndSynchronizeWasteTypes()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var createReq = new CreateWasteBinRequest
        {
            BinCode = "BIN-UPD-0001",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Organic },
            CollectionWeekdays = new[] { 1 }
        };
        var created = await service.CreateAsync(createReq, officerId, AppRoles.WasteOfficer);

        var updateReq = new UpdateWasteBinRequest
        {
            Latitude = 6.9350,
            Longitude = 79.8700,
            AddressText = "Updated Address",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Recyclable }, // Organic removed, Recyclable added
            CollectionWeekdays = new[] { 4, 2 }
        };

        var updated = await service.UpdateAsync(created.Id, updateReq, officerId, AppRoles.WasteOfficer);

        updated.CapacityLiters.Should().Be(1100);
        updated.AddressText.Should().Be("Updated Address");
        updated.Latitude.Should().Be(6.9350);
        updated.Longitude.Should().Be(79.8700);
        updated.AcceptedWasteTypes.Should().BeEquivalentTo(new[] { "General", "Recyclable" });
        updated.CollectionWeekdays.Should().Equal(new[] { 2, 4 });
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_MissingBin_ShouldThrowNotFoundException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);

        var updateReq = new UpdateWasteBinRequest
        {
            Latitude = 6.9350,
            Longitude = 79.8700,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var act = () => service.UpdateAsync(Guid.NewGuid(), updateReq, Guid.NewGuid(), AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeactivateAsync_ShouldTransitionStatusAndPreserveHistoricalData()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var createReq = new CreateWasteBinRequest
        {
            BinCode = "BIN-DEACT-0001",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };
        var created = await service.CreateAsync(createReq, officerId, AppRoles.WasteOfficer);

        // Seed an observation and task
        var obs = new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = created.Id,
            FillLevelPercent = 50,
            Condition = BinCondition.Good,
            RecordedByUserId = officerId,
            RecordedAt = DateTime.UtcNow.AddHours(-2)
        };
        db.BinObservations.Add(obs);
        await db.SaveChangesAsync();

        var deactReq = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService,
            Reason = "Depot hinge repair"
        };

        var result = await service.DeactivateAsync(created.Id, deactReq, officerId, AppRoles.WasteOfficer);

        result.AdministrativeStatus.Should().Be(BinAdministrativeStatus.OutOfService);
        result.UpdatedAt.Should().NotBeNull();

        // Check DB: bin and observation still exist
        var binInDb = await db.WasteBins.FindAsync(created.Id);
        binInDb.Should().NotBeNull();
        binInDb!.AdministrativeStatus.Should().Be(BinAdministrativeStatus.OutOfService);

        var obsCount = await db.BinObservations.CountAsync(o => o.WasteBinId == created.Id);
        obsCount.Should().Be(1);
    }

    [Fact]
    public async Task DeactivateAsync_AlreadyRetiredBin_ShouldThrowBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var createReq = new CreateWasteBinRequest
        {
            BinCode = "BIN-RET-0001",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };
        var created = await service.CreateAsync(createReq, officerId, AppRoles.WasteOfficer);

        await service.DeactivateAsync(created.Id, new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.Retired,
            Reason = "Decommissioned"
        }, officerId, AppRoles.WasteOfficer);

        // Try deactivating again
        var act = () => service.DeactivateAsync(created.Id, new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService
        }, officerId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already retired*");
    }

    [Fact]
    public async Task UpdateAsync_RetiredBin_ShouldThrowBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var createReq = new CreateWasteBinRequest
        {
            BinCode = "BIN-RET-0002",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };
        var created = await service.CreateAsync(createReq, officerId, AppRoles.WasteOfficer);

        await service.DeactivateAsync(created.Id, new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.Retired
        }, officerId, AppRoles.WasteOfficer);

        var updateReq = new UpdateWasteBinRequest
        {
            Latitude = 6.9300,
            Longitude = 79.8700,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        var act = () => service.UpdateAsync(created.Id, updateReq, officerId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Cannot update a retired waste bin*");
    }

    #endregion

    #region Internal Reads Tests

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task GetInternalByIdAsync_StaffRoles_ShouldSucceed(string role)
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-INT-0001",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1 },
            CreatedAt = DateTime.UtcNow
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = bin.Id, WasteType = WasteType.General });
        db.WasteBins.Add(bin);
        await db.SaveChangesAsync();

        var result = await service.GetInternalByIdAsync(bin.Id, Guid.NewGuid(), role);

        result.Should().NotBeNull();
        result.BinCode.Should().Be("BIN-INT-0001");
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task GetInternalByIdAsync_NonStaffRoles_ShouldThrowForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);

        var act = () => service.GetInternalByIdAsync(Guid.NewGuid(), Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only Waste Officers and Municipal Managers*");
    }

    [Fact]
    public async Task GetInternalListAsync_ShouldFilterByWasteTypeAndCondition()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);

        var bin1 = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-RECY-01",
            CapacityLiters = 1100,
            Latitude = 6.9,
            Longitude = 79.8,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1 },
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        bin1.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = bin1.Id, WasteType = WasteType.Recyclable });

        var bin2 = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-GEN-02",
            CapacityLiters = 660,
            Latitude = 6.9,
            Longitude = 79.8,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 2 },
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        bin2.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = bin2.Id, WasteType = WasteType.General });

        db.WasteBins.AddRange(bin1, bin2);
        await db.SaveChangesAsync();

        var query = new WasteBinListQuery
        {
            WasteType = WasteType.Recyclable
        };

        var result = await service.GetInternalListAsync(query, Guid.NewGuid(), AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items[0].BinCode.Should().Be("BIN-RECY-01");
    }

    #endregion

    #region Public Discovery & Availability Tests

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.Driver)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task GetPublicListAsync_NonCitizenRoles_ShouldThrowForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);

        var act = () => service.GetPublicListAsync(new PublicWasteBinQuery(), Guid.NewGuid(), role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Only authenticated Citizens*");
    }

    [Fact]
    public void PublicAvailability_NoObservation_ShouldReturnUnknown()
    {
        var now = DateTime.UtcNow;
        var availability = WasteBinService.ComputePublicAvailability(
            BinAdministrativeStatus.Active,
            lastCollectedAt: null,
            latestObservation: null,
            now);

        availability.Should().Be("Unknown");
    }

    [Fact]
    public void PublicAvailability_ObservationOlderThan48Hours_ShouldReturnUnknown()
    {
        var now = DateTime.UtcNow;
        var oldObs = new BinObservation
        {
            FillLevelPercent = 25,
            Condition = BinCondition.Good,
            RecordedAt = now.AddHours(-49) // 49 hours ago (> 48h)
        };

        var availability = WasteBinService.ComputePublicAvailability(
            BinAdministrativeStatus.Active,
            lastCollectedAt: null,
            latestObservation: oldObs,
            now);

        availability.Should().Be("Unknown");
    }

    [Fact]
    public void PublicAvailability_FreshObservationAt47Hours_ShouldReturnUsable()
    {
        var now = DateTime.UtcNow;
        var freshObs = new BinObservation
        {
            FillLevelPercent = 25,
            Condition = BinCondition.Good,
            RecordedAt = now.AddHours(-47) // 47 hours ago (<= 48h)
        };

        var availability = WasteBinService.ComputePublicAvailability(
            BinAdministrativeStatus.Active,
            lastCollectedAt: null,
            latestObservation: freshObs,
            now);

        availability.Should().Be("Usable");
    }

    [Fact]
    public void PublicAvailability_ObservationPrecedingLastCollectedAt_ShouldReturnUnknown()
    {
        var now = DateTime.UtcNow;
        var preCollectionObs = new BinObservation
        {
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            RecordedAt = now.AddHours(-5)
        };

        // Collection happened AFTER the observation was taken
        var lastCollected = now.AddHours(-2);

        var availability = WasteBinService.ComputePublicAvailability(
            BinAdministrativeStatus.Active,
            lastCollectedAt: lastCollected,
            latestObservation: preCollectionObs,
            now);

        // Pre-collection 100% fill reading is obsolete
        availability.Should().Be("Unknown");
    }

    [Theory]
    [InlineData(BinAdministrativeStatus.OutOfService)]
    [InlineData(BinAdministrativeStatus.Retired)]
    public void PublicAvailability_AdministrativelyInactive_ShouldReturnUnavailable(BinAdministrativeStatus status)
    {
        var now = DateTime.UtcNow;
        var freshObs = new BinObservation
        {
            FillLevelPercent = 0,
            Condition = BinCondition.Good,
            RecordedAt = now.AddHours(-1)
        };

        var availability = WasteBinService.ComputePublicAvailability(
            status,
            lastCollectedAt: null,
            latestObservation: freshObs,
            now);

        availability.Should().Be("Unavailable");
    }

    [Theory]
    [InlineData(BinCondition.Damaged)]
    [InlineData(BinCondition.Blocked)]
    [InlineData(BinCondition.Missing)]
    public void PublicAvailability_PoorCondition_ShouldReturnUnavailable(BinCondition condition)
    {
        var now = DateTime.UtcNow;
        var freshObs = new BinObservation
        {
            FillLevelPercent = 25,
            Condition = condition,
            RecordedAt = now.AddHours(-1)
        };

        var availability = WasteBinService.ComputePublicAvailability(
            BinAdministrativeStatus.Active,
            lastCollectedAt: null,
            latestObservation: freshObs,
            now);

        availability.Should().Be("Unavailable");
    }

    [Fact]
    public void PublicAvailability_100PercentFill_ShouldReturnFull()
    {
        var now = DateTime.UtcNow;
        var freshObs = new BinObservation
        {
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            RecordedAt = now.AddHours(-1)
        };

        var availability = WasteBinService.ComputePublicAvailability(
            BinAdministrativeStatus.Active,
            lastCollectedAt: null,
            latestObservation: freshObs,
            now);

        availability.Should().Be("Full");
    }

    [Fact]
    public void PublicAvailability_75PercentFill_ShouldReturnWarning()
    {
        var now = DateTime.UtcNow;
        var freshObs = new BinObservation
        {
            FillLevelPercent = 75,
            Condition = BinCondition.Good,
            RecordedAt = now.AddHours(-1)
        };

        var availability = WasteBinService.ComputePublicAvailability(
            BinAdministrativeStatus.Active,
            lastCollectedAt: null,
            latestObservation: freshObs,
            now);

        availability.Should().Be("Warning");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    public void PublicAvailability_Under75PercentFill_ShouldReturnUsable(int fillLevel)
    {
        var now = DateTime.UtcNow;
        var freshObs = new BinObservation
        {
            FillLevelPercent = fillLevel,
            Condition = BinCondition.Good,
            RecordedAt = now.AddHours(-1)
        };

        var availability = WasteBinService.ComputePublicAvailability(
            BinAdministrativeStatus.Active,
            lastCollectedAt: null,
            latestObservation: freshObs,
            now);

        availability.Should().Be("Usable");
    }

    [Fact]
    public async Task GetPublicListAsync_RadiusAndProximityFiltering_ShouldCalculateAccurateDistances()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var citizenId = Guid.NewGuid();

        // Bin A: Galle Face (approx 1 km from Pettah)
        var binA = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-NEAR-01",
            Latitude = 6.9271,
            Longitude = 79.8450,
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1 },
            CreatedAt = DateTime.UtcNow
        };
        binA.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = binA.Id, WasteType = WasteType.General });

        // Bin B: Dehiwala (approx 10 km from Pettah)
        var binB = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-FAR-02",
            Latitude = 6.8390,
            Longitude = 79.8650,
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1 },
            CreatedAt = DateTime.UtcNow
        };
        binB.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = binB.Id, WasteType = WasteType.General });

        db.WasteBins.AddRange(binA, binB);
        await db.SaveChangesAsync();

        // Search within 3 km of Pettah (6.9351, 79.8512)
        var query = new PublicWasteBinQuery
        {
            Latitude = 6.9351,
            Longitude = 79.8512,
            RadiusKm = 3.0
        };

        var result = await service.GetPublicListAsync(query, citizenId, AppRoles.Citizen);

        result.TotalCount.Should().Be(1);
        result.Items[0].BinCode.Should().Be("BIN-NEAR-01");
        result.Items[0].DistanceMeters.Should().NotBeNull();
        result.Items[0].DistanceMeters!.Value.Should().BeLessThan(3000);
    }

    [Fact]
    public async Task GetPublicByIdAsync_ShouldExcludeStaffMetadataAndIncludeScheduledFlag()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var citizenId = Guid.NewGuid();
        var officerId = Guid.NewGuid();

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-PUB-DETAIL",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1 },
            CreatedAt = DateTime.UtcNow
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = bin.Id, WasteType = WasteType.General });

        var obs = new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 50,
            Condition = BinCondition.Good,
            Notes = "Secret internal staff note that must not leak",
            RecordedByUserId = officerId,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        };

        var task = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TSK-20260921-0001",
            WasteBinId = bin.Id,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(4),
            CreatedByUserId = officerId,
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow
        };

        db.WasteBins.Add(bin);
        db.Users.Add(new AppUser { Id = officerId, UserName = "bin-public-detail-officer", FullName = "Bin detail officer" });
        db.BinObservations.Add(obs);
        db.CollectionTasks.Add(task);
        await db.SaveChangesAsync();

        var detail = await service.GetPublicByIdAsync(bin.Id, citizenId, AppRoles.Citizen);

        detail.Should().NotBeNull();
        detail.BinCode.Should().Be("BIN-PUB-DETAIL");
        detail.PublicAvailability.Should().Be("Usable");
        detail.IsCollectionScheduled.Should().BeTrue();
        detail.LastObservedAt.Should().Be(obs.RecordedAt);
    }

    [Fact]
    public async Task DeterministicLatestObservation_OrdersByRecordedAtDescThenIdDesc()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();
        db.Users.Add(new AppUser
        {
            Id = officerId,
            UserName = "officer.silva",
            NormalizedUserName = "OFFICER.SILVA",
            FullName = "Officer Silva"
        });

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-OBS-ORDER",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1 },
            CreatedAt = DateTime.UtcNow
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = bin.Id, WasteType = WasteType.General });

        var olderObs = new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 25,
            Condition = BinCondition.Good,
            RecordedByUserId = officerId,
            RecordedAt = DateTime.UtcNow.AddHours(-3)
        };

        var newerObs = new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = 75,
            Condition = BinCondition.Good,
            RecordedByUserId = officerId,
            RecordedAt = DateTime.UtcNow.AddHours(-1)
        };

        db.WasteBins.Add(bin);
        db.BinObservations.AddRange(olderObs, newerObs);
        await db.SaveChangesAsync();

        var detail = await service.GetInternalByIdAsync(bin.Id, officerId, AppRoles.WasteOfficer);

        detail.LatestObservation.Should().NotBeNull();
        detail.LatestObservation!.Id.Should().Be(newerObs.Id);
        detail.LatestObservation.FillLevelPercent.Should().Be(75);
        detail.LatestObservation.RecordedByUserName.Should().Be("Officer Silva");
    }

    #endregion

    #region Deactivation & Active-Task Protection Tests

    [Theory]
    [InlineData(CollectionTaskStatus.Scheduled)]
    [InlineData(CollectionTaskStatus.Assigned)]
    [InlineData(CollectionTaskStatus.InProgress)]
    public async Task DeactivateAsync_ActiveBinTargetedTask_BlocksDeactivationAndLeavesStateUnchanged(CollectionTaskStatus activeStatus)
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-ACTIVE-TASK",
            CapacityLiters = 1100,
            Latitude = 6.9,
            Longitude = 79.8,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var activeTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TSK-0001",
            WasteBinId = bin.Id,
            Status = activeStatus,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = officerId,
            CreatedAt = DateTime.UtcNow
        };

        db.WasteBins.Add(bin);
        db.CollectionTasks.Add(activeTask);
        await db.SaveChangesAsync();

        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService,
            Reason = "Damaged hinge"
        };

        var act = async () => await service.DeactivateAsync(bin.Id, request, officerId, AppRoles.WasteOfficer);

        var ex = await act.Should().ThrowAsync<BusinessRuleConflictException>();
        ex.WithMessage("*active collection task*");

        // Assert bin remains Active in database
        var binInDb = await db.WasteBins.FindAsync(bin.Id);
        binInDb.Should().NotBeNull();
        binInDb!.AdministrativeStatus.Should().Be(BinAdministrativeStatus.Active);

        // Assert task remains unchanged
        var taskInDb = await db.CollectionTasks.FindAsync(activeTask.Id);
        taskInDb.Should().NotBeNull();
        taskInDb!.Status.Should().Be(activeStatus);
    }

    [Theory]
    [InlineData(CollectionTaskStatus.Completed)]
    [InlineData(CollectionTaskStatus.Failed)]
    [InlineData(CollectionTaskStatus.Cancelled)]
    public async Task DeactivateAsync_InactiveTasks_DoNotBlockDeactivation(CollectionTaskStatus inactiveStatus)
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-INACTIVE-TASK",
            CapacityLiters = 1100,
            Latitude = 6.9,
            Longitude = 79.8,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var task = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TSK-0002",
            WasteBinId = bin.Id,
            Status = inactiveStatus,
            ScheduledAt = DateTime.UtcNow.AddHours(-2),
            CreatedByUserId = officerId,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        db.WasteBins.Add(bin);
        db.CollectionTasks.Add(task);
        await db.SaveChangesAsync();

        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService,
            Reason = "Scheduled maintenance"
        };

        var result = await service.DeactivateAsync(bin.Id, request, officerId, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.AdministrativeStatus.Should().Be(BinAdministrativeStatus.OutOfService);

        var binInDb = await db.WasteBins.FindAsync(bin.Id);
        binInDb!.AdministrativeStatus.Should().Be(BinAdministrativeStatus.OutOfService);
        binInDb.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeactivateAsync_TaskTargetingDifferentBin_DoesNotBlockDeactivation()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var targetBin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-TARGET",
            CapacityLiters = 1100,
            Latitude = 6.9,
            Longitude = 79.8,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var otherBin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-OTHER",
            CapacityLiters = 1100,
            Latitude = 6.9,
            Longitude = 79.8,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var activeTaskOnOtherBin = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TSK-OTHER",
            WasteBinId = otherBin.Id,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = officerId,
            CreatedAt = DateTime.UtcNow
        };

        db.WasteBins.AddRange(targetBin, otherBin);
        db.CollectionTasks.Add(activeTaskOnOtherBin);
        await db.SaveChangesAsync();

        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.Retired,
            Reason = "Decommissioned"
        };

        var result = await service.DeactivateAsync(targetBin.Id, request, officerId, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.AdministrativeStatus.Should().Be(BinAdministrativeStatus.Retired);

        var targetInDb = await db.WasteBins.FindAsync(targetBin.Id);
        targetInDb!.AdministrativeStatus.Should().Be(BinAdministrativeStatus.Retired);

        var otherInDb = await db.WasteBins.FindAsync(otherBin.Id);
        otherInDb!.AdministrativeStatus.Should().Be(BinAdministrativeStatus.Active);
    }

    [Fact]
    public async Task DeactivateAsync_ReportTargetedTask_DoesNotBlockDeactivation()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-REPORT-TASK",
            CapacityLiters = 1100,
            Latitude = 6.9,
            Longitude = 79.8,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var reportTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TSK-REPORT",
            WasteReportId = Guid.NewGuid(),
            WasteBinId = null,
            Status = CollectionTaskStatus.InProgress,
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            CreatedByUserId = officerId,
            CreatedAt = DateTime.UtcNow
        };

        db.WasteBins.Add(bin);
        db.CollectionTasks.Add(reportTask);
        await db.SaveChangesAsync();

        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService,
            Reason = "Depot inspection"
        };

        var result = await service.DeactivateAsync(bin.Id, request, officerId, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.AdministrativeStatus.Should().Be(BinAdministrativeStatus.OutOfService);
    }

    [Fact]
    public async Task DeactivateAsync_AlreadyRetiredBin_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-RETIRED",
            CapacityLiters = 1100,
            Latitude = 6.9,
            Longitude = 79.8,
            AdministrativeStatus = BinAdministrativeStatus.Retired,
            CreatedAt = DateTime.UtcNow
        };

        db.WasteBins.Add(bin);
        await db.SaveChangesAsync();

        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService
        };

        var act = async () => await service.DeactivateAsync(bin.Id, request, officerId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already retired*");
    }

    [Fact]
    public async Task DeactivateAsync_MissingBin_ThrowsNotFoundException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var officerId = Guid.NewGuid();
        var missingId = Guid.NewGuid();

        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService
        };

        var act = async () => await service.DeactivateAsync(missingId, request, officerId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{missingId}*");
    }

    [Fact]
    public async Task DeactivateAsync_NonWasteOfficerRole_ThrowsForbiddenException()
    {
        var (db, userManager) = CreateContext();
        var service = new WasteBinService(db, userManager);
        var binId = Guid.NewGuid();

        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService
        };

        var actCitizen = async () => await service.DeactivateAsync(binId, request, Guid.NewGuid(), AppRoles.Citizen);
        await actCitizen.Should().ThrowAsync<ForbiddenException>();

        var actManager = async () => await service.DeactivateAsync(binId, request, Guid.NewGuid(), AppRoles.MunicipalManager);
        await actManager.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion
}
