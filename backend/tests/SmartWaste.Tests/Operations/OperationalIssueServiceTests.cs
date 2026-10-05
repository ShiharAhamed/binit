using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Operations.DTOs.Requests;
using SmartWaste.Application.Operations.Queries;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Operations.Entities;
using SmartWaste.Domain.Operations.Enums;
using SmartWaste.Infrastructure.Operations.Services;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Operations;

public class OperationalIssueServiceTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // TEST INFRASTRUCTURE
    // ──────────────────────────────────────────────────────────────────────────

    private static (AppDbContext Db, UserManager<AppUser> UserManager) CreateContext()
    {
        var dbName = $"SmartWaste_Operations_{Guid.NewGuid():N}";
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
            loggerMock.Object
        );

        return (db, userManager);
    }

    private static OperationalIssueService CreateService(AppDbContext db)
    {
        return new OperationalIssueService(db);
    }

    private static async Task<AppUser> SeedUserAsync(
        AppDbContext db,
        UserManager<AppUser> userManager,
        string fullName = "Test Driver",
        string? email = null)
    {
        email ??= $"driver_{Guid.NewGuid():N}@test.com";
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = email,
            UserName = email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.NormalizedEmail = email.ToUpperInvariant();
        user.NormalizedUserName = email.ToUpperInvariant();
        await userManager.CreateAsync(user, "Password123!");
        return user;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. CREATION TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidIssueWithoutLocation_CreatesReportedIssue()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "John Driver");
        var service = CreateService(db);

        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Hydraulic lift failure",
            Description = "The rear bin lifter is leaking hydraulic fluid and failing to lift heavy bins.",
            Latitude = null,
            Longitude = null,
            LocationDescription = null
        };

        var result = await service.CreateAsync(request, driver.Id, AppRoles.Driver);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.DriverId.Should().Be(driver.Id);
        result.DriverName.Should().Be("John Driver");
        result.IssueType.Should().Be(OperationalIssueType.VehicleProblem);
        result.Title.Should().Be("Hydraulic lift failure");
        result.Description.Should().Be("The rear bin lifter is leaking hydraulic fluid and failing to lift heavy bins.");
        result.Status.Should().Be(OperationalIssueStatus.Reported);
        result.Latitude.Should().BeNull();
        result.Longitude.Should().BeNull();
        result.LocationDescription.Should().BeNull();
        result.ResolvedAt.Should().BeNull();
        result.ResolvedByUserId.Should().BeNull();
        result.ResolutionNote.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var persisted = await db.OperationalIssues.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(OperationalIssueStatus.Reported);
        persisted.DriverId.Should().Be(driver.Id);
    }

    [Fact]
    public async Task CreateAsync_ValidIssueWithLocation_PersistsCoordinatesAndDescription()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Jane Driver");
        var service = CreateService(db);

        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.RoadOrAccessIssue,
            Title = "Road blocked by fallen tree",
            Description = "Main access road to sector 4 is completely blocked by a large fallen tree branch.",
            Latitude = 6.9271,
            Longitude = 79.8612,
            LocationDescription = "Opposite the municipal water tower"
        };

        var result = await service.CreateAsync(request, driver.Id, AppRoles.Driver);

        result.Should().NotBeNull();
        result.Latitude.Should().Be(6.9271);
        result.Longitude.Should().Be(79.8612);
        result.LocationDescription.Should().Be("Opposite the municipal water tower");

        var persisted = await db.OperationalIssues.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.Latitude.Should().Be(6.9271);
        persisted.Longitude.Should().Be(79.8612);
        persisted.LocationDescription.Should().Be("Opposite the municipal water tower");
    }

    [Fact]
    public async Task CreateAsync_ServerAuthoritativeDriverId_UsesAuthenticatedActor()
    {
        var (db, userManager) = CreateContext();
        var driver1 = await SeedUserAsync(db, userManager, "Driver One");
        var driver2 = await SeedUserAsync(db, userManager, "Driver Two");
        var service = CreateService(db);

        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.SafetyConcern,
            Title = "Aggressive stray dogs near depot",
            Description = "Pack of aggressive dogs blocking access to the bin loading area."
        };

        var result = await service.CreateAsync(request, driver1.Id, AppRoles.Driver);

        result.DriverId.Should().Be(driver1.Id);
        result.DriverId.Should().NotBe(driver2.Id);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task CreateAsync_NonDriverRole_ThrowsForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var user = await SeedUserAsync(db, userManager, "Non Driver User");
        var service = CreateService(db);

        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.EquipmentProblem,
            Title = "Compactor malfunction",
            Description = "Compactor plate stuck in down position."
        };

        var act = () => service.CreateAsync(request, user.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Driver*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. DRIVER MINE TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMineAsync_DriverActor_ReturnsOnlyOwnIssues()
    {
        var (db, userManager) = CreateContext();
        var driverA = await SeedUserAsync(db, userManager, "Driver A");
        var driverB = await SeedUserAsync(db, userManager, "Driver B");
        var service = CreateService(db);

        // Seed 3 issues for Driver A, 2 for Driver B
        for (int i = 1; i <= 3; i++)
        {
            db.OperationalIssues.Add(new OperationalIssue
            {
                Id = Guid.NewGuid(),
                DriverId = driverA.Id,
                IssueType = OperationalIssueType.VehicleProblem,
                Title = $"Driver A Issue {i}",
                Description = "Description",
                Status = OperationalIssueStatus.Reported,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        for (int i = 1; i <= 2; i++)
        {
            db.OperationalIssues.Add(new OperationalIssue
            {
                Id = Guid.NewGuid(),
                DriverId = driverB.Id,
                IssueType = OperationalIssueType.RoadOrAccessIssue,
                Title = $"Driver B Issue {i}",
                Description = "Description",
                Status = OperationalIssueStatus.Reported,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery { Page = 1, PageSize = 20 };
        var result = await service.GetMineAsync(query, driverA.Id, AppRoles.Driver);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(x => x.DriverId == driverA.Id);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    public async Task GetMineAsync_NonDriverRole_ThrowsForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var user = await SeedUserAsync(db, userManager, "User");
        var service = CreateService(db);

        var query = new OperationalIssueListQuery();
        var act = () => service.GetMineAsync(query, user.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Driver*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. STAFF LIST TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListAsync_WasteOfficerActor_ReturnsAllIssues()
    {
        var (db, userManager) = CreateContext();
        var driverA = await SeedUserAsync(db, userManager, "Driver A");
        var driverB = await SeedUserAsync(db, userManager, "Driver B");
        var officer = await SeedUserAsync(db, userManager, "Officer Smith");
        var service = CreateService(db);

        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driverA.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Driver A Issue",
            Description = "Description",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driverB.Id,
            IssueType = OperationalIssueType.OperationalDelay,
            Title = "Driver B Issue",
            Description = "Description",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery { Page = 1, PageSize = 20 };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetListAsync_MunicipalManagerActor_ReturnsAllIssues()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var manager = await SeedUserAsync(db, userManager, "Manager Alice");
        var service = CreateService(db);

        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.Other,
            Title = "Issue for Manager",
            Description = "Description",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery();
        var result = await service.GetListAsync(query, manager.Id, AppRoles.MunicipalManager);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
    }

    [Theory]
    [InlineData(AppRoles.Driver)]
    [InlineData(AppRoles.Citizen)]
    public async Task GetListAsync_UnauthorizedRole_ThrowsForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var user = await SeedUserAsync(db, userManager, "User");
        var service = CreateService(db);

        var query = new OperationalIssueListQuery();
        var act = () => service.GetListAsync(query, user.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Waste Officers and Municipal Managers*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. FILTERING & SEARCH TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListAsync_FilterByStatus_ReturnsMatchingOnly()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var service = CreateService(db);

        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Reported 1",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "InReview 1",
            Description = "Desc",
            Status = OperationalIssueStatus.InReview,
            CreatedAt = DateTime.UtcNow
        });
        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Resolved 1",
            Description = "Desc",
            Status = OperationalIssueStatus.Resolved,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery { Status = OperationalIssueStatus.InReview };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Title.Should().Be("InReview 1");
        result.Items.Single().Status.Should().Be(OperationalIssueStatus.InReview);
    }

    [Fact]
    public async Task GetListAsync_FilterByIssueType_ReturnsMatchingOnly()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var service = CreateService(db);

        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Vehicle Issue",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.SafetyConcern,
            Title = "Safety Concern Issue",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery { IssueType = OperationalIssueType.SafetyConcern };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Title.Should().Be("Safety Concern Issue");
        result.Items.Single().IssueType.Should().Be(OperationalIssueType.SafetyConcern);
    }

    [Fact]
    public async Task GetListAsync_SearchByTitle_ReturnsMatchingOnly()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var service = CreateService(db);

        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Engine coolant leakage detected",
            Description = "Generic description",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Routine tire pressure check",
            Description = "Generic description",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery { Search = "coolant" };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Title.Should().Be("Engine coolant leakage detected");
    }

    [Fact]
    public async Task GetListAsync_SearchByDescription_ReturnsMatchingOnly()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var service = CreateService(db);

        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.RoadOrAccessIssue,
            Title = "Road blockage",
            Description = "Construction barrier placed by bridge contractor blocking vehicle passage",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.RoadOrAccessIssue,
            Title = "Another issue",
            Description = "Narrow street parked cars",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery { Search = "contractor" };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Title.Should().Be("Road blockage");
    }

    [Fact]
    public async Task GetListAsync_CombinedFilter_ReturnsExpectedResults()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var service = CreateService(db);

        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.EquipmentProblem,
            Title = "Broken lift mechanism",
            Description = "Winch cable snapped on sector 3 truck",
            Status = OperationalIssueStatus.InReview,
            CreatedAt = DateTime.UtcNow
        });
        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.EquipmentProblem,
            Title = "Broken lift mechanism 2",
            Description = "Winch cable snapped on sector 5 truck",
            Status = OperationalIssueStatus.Reported, // Different status
            CreatedAt = DateTime.UtcNow
        });
        db.OperationalIssues.Add(new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem, // Different type
            Title = "Engine issue with cable",
            Description = "Cable problem",
            Status = OperationalIssueStatus.InReview,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery
        {
            IssueType = OperationalIssueType.EquipmentProblem,
            Status = OperationalIssueStatus.InReview,
            Search = "sector 3"
        };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Title.Should().Be("Broken lift mechanism");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. PAGINATION TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListAsync_Pagination_ReturnsCorrectPagedResult()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var service = CreateService(db);

        for (int i = 1; i <= 25; i++)
        {
            db.OperationalIssues.Add(new OperationalIssue
            {
                Id = Guid.NewGuid(),
                DriverId = driver.Id,
                IssueType = OperationalIssueType.VehicleProblem,
                Title = $"Issue #{i:D2}",
                Description = "Description",
                Status = OperationalIssueStatus.Reported,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var query = new OperationalIssueListQuery { Page = 2, PageSize = 10 };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(10);
        result.Items.First().Title.Should().Be("Issue #11");
        result.Items.Last().Title.Should().Be("Issue #20");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. DETAIL & OWNERSHIP TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_DriverActor_OwnIssue_ReturnsDetail()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Sam Driver");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.SafetyConcern,
            Title = "Exposed electrical wiring",
            Description = "Overhead electrical cable dangling low over collection point.",
            Latitude = 6.9271,
            Longitude = 79.8612,
            LocationDescription = "Outside substation #12",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(issue.Id, driver.Id, AppRoles.Driver);

        result.Should().NotBeNull();
        result.Id.Should().Be(issue.Id);
        result.DriverId.Should().Be(driver.Id);
        result.DriverName.Should().Be("Sam Driver");
        result.Title.Should().Be("Exposed electrical wiring");
        result.Description.Should().Be("Overhead electrical cable dangling low over collection point.");
        result.Latitude.Should().Be(6.9271);
        result.Longitude.Should().Be(79.8612);
        result.LocationDescription.Should().Be("Outside substation #12");
        result.Status.Should().Be(OperationalIssueStatus.Reported);
    }

    [Fact]
    public async Task GetByIdAsync_DriverActor_OtherDriverIssue_ThrowsForbiddenException()
    {
        var (db, userManager) = CreateContext();
        var driver1 = await SeedUserAsync(db, userManager, "Driver 1");
        var driver2 = await SeedUserAsync(db, userManager, "Driver 2");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver1.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Driver 1 Issue",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var act = () => service.GetByIdAsync(issue.Id, driver2.Id, AppRoles.Driver);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*own*");
    }

    [Fact]
    public async Task GetByIdAsync_WasteOfficerActor_AnyIssue_ReturnsDetail()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Driver Issue",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(issue.Id, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Id.Should().Be(issue.Id);
    }

    [Fact]
    public async Task GetByIdAsync_MunicipalManagerActor_AnyIssue_ReturnsDetail()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var manager = await SeedUserAsync(db, userManager, "Manager");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Driver Issue",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(issue.Id, manager.Id, AppRoles.MunicipalManager);

        result.Should().NotBeNull();
        result.Id.Should().Be(issue.Id);
    }

    [Fact]
    public async Task GetByIdAsync_CitizenActor_ThrowsForbiddenException()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Driver Issue",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var act = () => service.GetByIdAsync(issue.Id, citizen.Id, AppRoles.Citizen);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Citizens*");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsNotFoundException()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        var act = () => service.GetByIdAsync(Guid.NewGuid(), officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. START REVIEW TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartReviewAsync_ReportedIssue_TransitionsToInReview()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Vehicle Problem",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var result = await service.StartReviewAsync(issue.Id, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Status.Should().Be(OperationalIssueStatus.InReview);
        result.UpdatedAt.Should().NotBeNull();
        result.ResolvedAt.Should().BeNull();
        result.ResolvedByUserId.Should().BeNull();
        result.ResolutionNote.Should().BeNull();

        var persisted = await db.OperationalIssues.FindAsync(issue.Id);
        persisted!.Status.Should().Be(OperationalIssueStatus.InReview);
        persisted.UpdatedAt.Should().NotBeNull();
        persisted.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task StartReviewAsync_InReviewIssue_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Vehicle Problem",
            Description = "Desc",
            Status = OperationalIssueStatus.InReview,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var act = () => service.StartReviewAsync(issue.Id, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*InReview*Reported*");
    }

    [Fact]
    public async Task StartReviewAsync_ResolvedIssue_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Vehicle Problem",
            Description = "Desc",
            Status = OperationalIssueStatus.Resolved,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ResolvedAt = DateTime.UtcNow.AddHours(-1),
            ResolvedByUserId = officer.Id,
            ResolutionNote = "Already resolved"
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var act = () => service.StartReviewAsync(issue.Id, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Resolved*");
    }

    [Theory]
    [InlineData(AppRoles.Driver)]
    [InlineData(AppRoles.Citizen)]
    public async Task StartReviewAsync_UnauthorizedRole_ThrowsForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var user = await SeedUserAsync(db, userManager, "User");
        var service = CreateService(db);

        var act = () => service.StartReviewAsync(Guid.NewGuid(), user.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 8. RESOLVE TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_InReviewIssue_TransitionsToResolvedWithMetadata()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer Sarah");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.EquipmentProblem,
            Title = "Equipment failure",
            Description = "Desc",
            Status = OperationalIssueStatus.InReview,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var request = new ResolveOperationalIssueRequest
        {
            ResolutionNote = "Replacement vehicle dispatched and route continued."
        };

        var result = await service.ResolveAsync(issue.Id, request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Status.Should().Be(OperationalIssueStatus.Resolved);
        result.ResolvedAt.Should().NotBeNull();
        result.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.ResolvedByUserId.Should().Be(officer.Id);
        result.ResolvedByUserName.Should().Be("Officer Sarah");
        result.ResolutionNote.Should().Be("Replacement vehicle dispatched and route continued.");
        result.UpdatedAt.Should().NotBeNull();

        var persisted = await db.OperationalIssues.FindAsync(issue.Id);
        persisted!.Status.Should().Be(OperationalIssueStatus.Resolved);
        persisted.ResolvedByUserId.Should().Be(officer.Id);
        persisted.ResolutionNote.Should().Be("Replacement vehicle dispatched and route continued.");
        persisted.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ReportedIssue_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Direct resolve attempt",
            Description = "Desc",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var request = new ResolveOperationalIssueRequest
        {
            ResolutionNote = "Direct resolution without review."
        };

        var act = () => service.ResolveAsync(issue.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Reported*InReview*");
    }

    [Fact]
    public async Task ResolveAsync_ResolvedIssue_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Already resolved",
            Description = "Desc",
            Status = OperationalIssueStatus.Resolved,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ResolvedAt = DateTime.UtcNow.AddHours(-1),
            ResolvedByUserId = officer.Id,
            ResolutionNote = "Previous resolution note"
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var request = new ResolveOperationalIssueRequest
        {
            ResolutionNote = "Second resolution attempt."
        };

        var act = () => service.ResolveAsync(issue.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Resolved*InReview*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_EmptyOrWhitespaceResolutionNote_ThrowsArgumentException(string note)
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "InReview issue",
            Description = "Desc",
            Status = OperationalIssueStatus.InReview,
            CreatedAt = DateTime.UtcNow
        };
        db.OperationalIssues.Add(issue);
        await db.SaveChangesAsync();

        var request = new ResolveOperationalIssueRequest
        {
            ResolutionNote = note
        };

        var act = () => service.ResolveAsync(issue.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Resolution note*");
    }

    [Theory]
    [InlineData(AppRoles.Driver)]
    [InlineData(AppRoles.Citizen)]
    public async Task ResolveAsync_UnauthorizedRole_ThrowsForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var user = await SeedUserAsync(db, userManager, "User");
        var service = CreateService(db);

        var request = new ResolveOperationalIssueRequest { ResolutionNote = "Some valid note" };
        var act = () => service.ResolveAsync(Guid.NewGuid(), request, user.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 9. LOCATION IMMUTABILITY TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Location_RemainsUnchanged_AcrossStartReviewAndResolve()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db);

        const double initialLat = 6.9271;
        const double initialLon = 79.8612;
        const string initialLocationDesc = "Beside the fuel depot main entry gate";

        // 1. Create with location
        var createReq = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.RoadOrAccessIssue,
            Title = "Road blockage at depot",
            Description = "Construction vehicle blocking main gate",
            Latitude = initialLat,
            Longitude = initialLon,
            LocationDescription = initialLocationDesc
        };
        var created = await service.CreateAsync(createReq, driver.Id, AppRoles.Driver);

        created.Latitude.Should().Be(initialLat);
        created.Longitude.Should().Be(initialLon);
        created.LocationDescription.Should().Be(initialLocationDesc);

        // 2. Start Review
        var inReview = await service.StartReviewAsync(created.Id, officer.Id, AppRoles.WasteOfficer);
        inReview.Latitude.Should().Be(initialLat);
        inReview.Longitude.Should().Be(initialLon);
        inReview.LocationDescription.Should().Be(initialLocationDesc);

        // 3. Resolve
        var resolveReq = new ResolveOperationalIssueRequest
        {
            ResolutionNote = "Construction vehicle moved by site manager."
        };
        var resolved = await service.ResolveAsync(created.Id, resolveReq, officer.Id, AppRoles.WasteOfficer);
        resolved.Latitude.Should().Be(initialLat);
        resolved.Longitude.Should().Be(initialLon);
        resolved.LocationDescription.Should().Be(initialLocationDesc);

        // 4. Verify directly in database
        var finalEntity = await db.OperationalIssues.FindAsync(created.Id);
        finalEntity!.Latitude.Should().Be(initialLat);
        finalEntity.Longitude.Should().Be(initialLon);
        finalEntity.LocationDescription.Should().Be(initialLocationDesc);
    }
}
