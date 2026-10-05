using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Complaints.DTOs.Requests;
using SmartWaste.Application.Complaints.Queries;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Complaints.Entities;
using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Complaints.Services;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Complaints;

public class ComplaintServiceTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // TEST INFRASTRUCTURE
    // ──────────────────────────────────────────────────────────────────────────

    private static (AppDbContext Db, UserManager<AppUser> UserManager) CreateContext()
    {
        var dbName = $"SmartWaste_Complaints_{Guid.NewGuid():N}";
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

    private static ComplaintService CreateService(AppDbContext db, UserManager<AppUser>? userManager = null)
    {
        return new ComplaintService(db);
    }

    private static async Task<AppUser> SeedUserAsync(
        AppDbContext db,
        UserManager<AppUser> userManager,
        string fullName = "Test Citizen",
        string? email = null)
    {
        email ??= $"user_{Guid.NewGuid():N}@test.com";
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
    public async Task CreateAsync_ValidComplaintWithoutLocation_CreatesSubmittedComplaint()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "John Citizen");
        var service = CreateService(db, userManager);

        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection on 5th Avenue",
            Description = "The waste truck did not arrive for scheduled Monday morning pickup.",
            Latitude = null,
            Longitude = null,
            LocationDescription = null
        };

        var result = await service.CreateAsync(request, citizen.Id, AppRoles.Citizen);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.CitizenId.Should().Be(citizen.Id);
        result.CitizenName.Should().Be("John Citizen");
        result.Category.Should().Be(ComplaintCategory.MissedCollection);
        result.Subject.Should().Be("Missed collection on 5th Avenue");
        result.Description.Should().Be("The waste truck did not arrive for scheduled Monday morning pickup.");
        result.Status.Should().Be(ComplaintStatus.Submitted);
        result.Latitude.Should().BeNull();
        result.Longitude.Should().BeNull();
        result.LocationDescription.Should().BeNull();
        result.ResolvedAt.Should().BeNull();
        result.ResolvedByUserId.Should().BeNull();
        result.ResolutionNote.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var persisted = await db.Complaints.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ComplaintStatus.Submitted);
        persisted.CitizenId.Should().Be(citizen.Id);
    }

    [Fact]
    public async Task CreateAsync_ValidComplaintWithLocation_PersistsCoordinatesAndDescription()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Jane Citizen");
        var service = CreateService(db, userManager);

        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.PoorService,
            Subject = "Spill during collection near park entrance",
            Description = "Waste was spilled beside the community park gate during collection.",
            Latitude = 6.9271,
            Longitude = 79.8612,
            LocationDescription = "Opposite the children playground main gate"
        };

        var result = await service.CreateAsync(request, citizen.Id, AppRoles.Citizen);

        result.Should().NotBeNull();
        result.Latitude.Should().Be(6.9271);
        result.Longitude.Should().Be(79.8612);
        result.LocationDescription.Should().Be("Opposite the children playground main gate");

        var persisted = await db.Complaints.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.Latitude.Should().Be(6.9271);
        persisted.Longitude.Should().Be(79.8612);
        persisted.LocationDescription.Should().Be("Opposite the children playground main gate");
    }

    [Fact]
    public async Task CreateAsync_ServerAuthoritativeCitizenId_UsesAuthenticatedActor()
    {
        var (db, userManager) = CreateContext();
        var citizen1 = await SeedUserAsync(db, userManager, "Citizen One");
        var citizen2 = await SeedUserAsync(db, userManager, "Citizen Two");
        var service = CreateService(db, userManager);

        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.UnresolvedIssue,
            Subject = "Damaged public bin unresolved",
            Description = "The metal lid has broken off completely and was not fixed."
        };

        // Even if request originated elsewhere, server assigns authenticated actor citizen1.Id
        var result = await service.CreateAsync(request, citizen1.Id, AppRoles.Citizen);

        result.CitizenId.Should().Be(citizen1.Id);
        result.CitizenId.Should().NotBe(citizen2.Id);
    }

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    [InlineData(AppRoles.Driver)]
    public async Task CreateAsync_NonCitizenRole_ThrowsForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var user = await SeedUserAsync(db, userManager, "Staff User");
        var service = CreateService(db, userManager);

        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.Other,
            Subject = "Late night noise",
            Description = "Truck collection occurred at 3 AM with excessive noise."
        };

        var act = () => service.CreateAsync(request, user.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Citizen*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. LIST & OWNERSHIP TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListAsync_CitizenActor_ReturnsOnlyOwnComplaints()
    {
        var (db, userManager) = CreateContext();
        var citizenA = await SeedUserAsync(db, userManager, "Citizen A");
        var citizenB = await SeedUserAsync(db, userManager, "Citizen B");
        var service = CreateService(db, userManager);

        // Seed 3 complaints for Citizen A, 2 for Citizen B
        for (int i = 1; i <= 3; i++)
        {
            db.Complaints.Add(new Complaint
            {
                Id = Guid.NewGuid(),
                CitizenId = citizenA.Id,
                Category = ComplaintCategory.MissedCollection,
                Subject = $"A Complaint {i}",
                Description = "Description",
                Status = ComplaintStatus.Submitted,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        for (int i = 1; i <= 2; i++)
        {
            db.Complaints.Add(new Complaint
            {
                Id = Guid.NewGuid(),
                CitizenId = citizenB.Id,
                Category = ComplaintCategory.DelayedService,
                Subject = $"B Complaint {i}",
                Description = "Description",
                Status = ComplaintStatus.Submitted,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery { Page = 1, PageSize = 20 };
        var result = await service.GetListAsync(query, citizenA.Id, AppRoles.Citizen);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(x => x.CitizenId == citizenA.Id);
    }

    [Fact]
    public async Task GetListAsync_WasteOfficerActor_ReturnsAllComplaints()
    {
        var (db, userManager) = CreateContext();
        var citizenA = await SeedUserAsync(db, userManager, "Citizen A");
        var citizenB = await SeedUserAsync(db, userManager, "Citizen B");
        var officer = await SeedUserAsync(db, userManager, "Officer Smith");
        var service = CreateService(db, userManager);

        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizenA.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Citizen A Complaint",
            Description = "Description",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizenB.Id,
            Category = ComplaintCategory.PoorService,
            Subject = "Citizen B Complaint",
            Description = "Description",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery { Page = 1, PageSize = 20 };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetListAsync_MunicipalManagerActor_ReturnsAllComplaints()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen A");
        var manager = await SeedUserAsync(db, userManager, "Manager Alice");
        var service = CreateService(db, userManager);

        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.Other,
            Subject = "Complaint for Manager",
            Description = "Description",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery();
        var result = await service.GetListAsync(query, manager.Id, AppRoles.MunicipalManager);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetListAsync_DriverActor_ThrowsForbiddenException()
    {
        var (db, userManager) = CreateContext();
        var driver = await SeedUserAsync(db, userManager, "Driver Dave");
        var service = CreateService(db, userManager);

        var query = new ComplaintListQuery();
        var act = () => service.GetListAsync(query, driver.Id, AppRoles.Driver);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Driver*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. FILTERING & SEARCH TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListAsync_FilterByStatus_ReturnsMatchingOnly()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var service = CreateService(db, userManager);

        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Submitted 1",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "InReview 1",
            Description = "Desc",
            Status = ComplaintStatus.InReview,
            CreatedAt = DateTime.UtcNow
        });
        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Resolved 1",
            Description = "Desc",
            Status = ComplaintStatus.Resolved,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery { Status = ComplaintStatus.InReview };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Subject.Should().Be("InReview 1");
        result.Items.Single().Status.Should().Be(ComplaintStatus.InReview);
    }

    [Fact]
    public async Task GetListAsync_FilterByCategory_ReturnsMatchingOnly()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var service = CreateService(db, userManager);

        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.DelayedService,
            Subject = "Delayed Service Complaint",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.PoorService,
            Subject = "Poor Service Complaint",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery { Category = ComplaintCategory.PoorService };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Subject.Should().Be("Poor Service Complaint");
        result.Items.Single().Category.Should().Be(ComplaintCategory.PoorService);
    }

    [Fact]
    public async Task GetListAsync_SearchBySubject_ReturnsMatchingOnly()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var service = CreateService(db, userManager);

        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Hazardous chemical leakage",
            Description = "Generic description",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Routine missed collection",
            Description = "Generic description",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery { Search = "chemical" };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Subject.Should().Be("Hazardous chemical leakage");
    }

    [Fact]
    public async Task GetListAsync_SearchByDescription_ReturnsMatchingOnly()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var service = CreateService(db, userManager);

        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.PoorService,
            Subject = "Overflowing bin area",
            Description = "Cardboard boxes piling up behind supermarket alleyway",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.PoorService,
            Subject = "Another bin",
            Description = "Household garbage overflowing",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery { Search = "supermarket" };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Subject.Should().Be("Overflowing bin area");
    }

    [Fact]
    public async Task GetListAsync_CombinedFilter_ReturnsExpectedResults()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var service = CreateService(db, userManager);

        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.PoorService,
            Subject = "Major service failure in ravine",
            Description = "Old construction debris dumped in wetland",
            Status = ComplaintStatus.InReview,
            CreatedAt = DateTime.UtcNow
        });
        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.PoorService,
            Subject = "Poor service near highway",
            Description = "Tires on the shoulder",
            Status = ComplaintStatus.Submitted, // Different status
            CreatedAt = DateTime.UtcNow
        });
        db.Complaints.Add(new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.DelayedService, // Different category
            Subject = "Broken lid in ravine",
            Description = "Debris in ravine",
            Status = ComplaintStatus.InReview,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery
        {
            Category = ComplaintCategory.PoorService,
            Status = ComplaintStatus.InReview,
            Search = "wetland"
        };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Subject.Should().Be("Major service failure in ravine");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. PAGINATION TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListAsync_Pagination_ReturnsCorrectPagedResult()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var service = CreateService(db, userManager);

        for (int i = 1; i <= 25; i++)
        {
            db.Complaints.Add(new Complaint
            {
                Id = Guid.NewGuid(),
                CitizenId = citizen.Id,
                Category = ComplaintCategory.MissedCollection,
                Subject = $"Complaint #{i:D2}",
                Description = "Description",
                Status = ComplaintStatus.Submitted,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        var query = new ComplaintListQuery { Page = 2, PageSize = 10 };
        var result = await service.GetListAsync(query, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.Items.Should().HaveCount(10);
        result.Items.First().Subject.Should().Be("Complaint #11");
        result.Items.Last().Subject.Should().Be("Complaint #20");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. DETAIL & OWNERSHIP TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_CitizenActor_OwnComplaint_ReturnsDetail()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Sam Citizen");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.PoorService,
            Subject = "Rude staff interaction",
            Description = "Collection crew was disrespectful when asked about missed bin.",
            Latitude = 6.9271,
            Longitude = 79.8612,
            LocationDescription = "Outside residence #42",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(complaint.Id, citizen.Id, AppRoles.Citizen);

        result.Should().NotBeNull();
        result.Id.Should().Be(complaint.Id);
        result.CitizenId.Should().Be(citizen.Id);
        result.CitizenName.Should().Be("Sam Citizen");
        result.Subject.Should().Be("Rude staff interaction");
        result.Description.Should().Be("Collection crew was disrespectful when asked about missed bin.");
        result.Latitude.Should().Be(6.9271);
        result.Longitude.Should().Be(79.8612);
        result.LocationDescription.Should().Be("Outside residence #42");
        result.Status.Should().Be(ComplaintStatus.Submitted);
    }

    [Fact]
    public async Task GetByIdAsync_CitizenActor_OtherCitizenComplaint_ThrowsForbiddenException()
    {
        var (db, userManager) = CreateContext();
        var citizen1 = await SeedUserAsync(db, userManager, "Citizen 1");
        var citizen2 = await SeedUserAsync(db, userManager, "Citizen 2");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen1.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Citizen 1 Complaint",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var act = () => service.GetByIdAsync(complaint.Id, citizen2.Id, AppRoles.Citizen);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*own*");
    }

    [Fact]
    public async Task GetByIdAsync_WasteOfficerActor_AnyComplaint_ReturnsDetail()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Citizen Complaint",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(complaint.Id, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Id.Should().Be(complaint.Id);
    }

    [Fact]
    public async Task GetByIdAsync_MunicipalManagerActor_AnyComplaint_ReturnsDetail()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var manager = await SeedUserAsync(db, userManager, "Manager");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Citizen Complaint",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var result = await service.GetByIdAsync(complaint.Id, manager.Id, AppRoles.MunicipalManager);

        result.Should().NotBeNull();
        result.Id.Should().Be(complaint.Id);
    }

    [Fact]
    public async Task GetByIdAsync_DriverActor_ThrowsForbiddenException()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var driver = await SeedUserAsync(db, userManager, "Driver");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Citizen Complaint",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var act = () => service.GetByIdAsync(complaint.Id, driver.Id, AppRoles.Driver);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Driver*");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsNotFoundException()
    {
        var (db, userManager) = CreateContext();
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        var act = () => service.GetByIdAsync(Guid.NewGuid(), officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. START REVIEW TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartReviewAsync_SubmittedComplaint_TransitionsToInReview()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var result = await service.StartReviewAsync(complaint.Id, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Status.Should().Be(ComplaintStatus.InReview);
        result.UpdatedAt.Should().NotBeNull();
        result.ResolvedAt.Should().BeNull();
        result.ResolvedByUserId.Should().BeNull();
        result.ResolutionNote.Should().BeNull();

        var persisted = await db.Complaints.FindAsync(complaint.Id);
        persisted!.Status.Should().Be(ComplaintStatus.InReview);
        persisted.UpdatedAt.Should().NotBeNull();
        persisted.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task StartReviewAsync_InReviewComplaint_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection",
            Description = "Desc",
            Status = ComplaintStatus.InReview,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var act = () => service.StartReviewAsync(complaint.Id, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*InReview*Submitted*");
    }

    [Fact]
    public async Task StartReviewAsync_ResolvedComplaint_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection",
            Description = "Desc",
            Status = ComplaintStatus.Resolved,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ResolvedAt = DateTime.UtcNow.AddHours(-1),
            ResolvedByUserId = officer.Id,
            ResolutionNote = "Already resolved"
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var act = () => service.StartReviewAsync(complaint.Id, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Resolved*");
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task StartReviewAsync_UnauthorizedRole_ThrowsForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var user = await SeedUserAsync(db, userManager, "User");
        var service = CreateService(db, userManager);

        var act = () => service.StartReviewAsync(Guid.NewGuid(), user.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. RESOLVE TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_InReviewComplaint_TransitionsToResolvedWithMetadata()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer Sarah");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.PoorService,
            Subject = "Poor collection service",
            Description = "Desc",
            Status = ComplaintStatus.InReview,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var request = new ResolveComplaintRequest
        {
            ResolutionNote = "Special collection crew dispatched and area cleared."
        };

        var result = await service.ResolveAsync(complaint.Id, request, officer.Id, AppRoles.WasteOfficer);

        result.Should().NotBeNull();
        result.Status.Should().Be(ComplaintStatus.Resolved);
        result.ResolvedAt.Should().NotBeNull();
        result.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.ResolvedByUserId.Should().Be(officer.Id);
        result.ResolvedByUserName.Should().Be("Officer Sarah");
        result.ResolutionNote.Should().Be("Special collection crew dispatched and area cleared.");
        result.UpdatedAt.Should().NotBeNull();

        var persisted = await db.Complaints.FindAsync(complaint.Id);
        persisted!.Status.Should().Be(ComplaintStatus.Resolved);
        persisted.ResolvedByUserId.Should().Be(officer.Id);
        persisted.ResolutionNote.Should().Be("Special collection crew dispatched and area cleared.");
        persisted.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_SubmittedComplaint_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Direct resolve attempt",
            Description = "Desc",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var request = new ResolveComplaintRequest
        {
            ResolutionNote = "Direct resolution without review."
        };

        var act = () => service.ResolveAsync(complaint.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Submitted*InReview*");
    }

    [Fact]
    public async Task ResolveAsync_ResolvedComplaint_ThrowsBusinessRuleConflictException()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "Already resolved",
            Description = "Desc",
            Status = ComplaintStatus.Resolved,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ResolvedAt = DateTime.UtcNow.AddHours(-1),
            ResolvedByUserId = officer.Id,
            ResolutionNote = "Previous resolution note"
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var request = new ResolveComplaintRequest
        {
            ResolutionNote = "Second resolution attempt."
        };

        var act = () => service.ResolveAsync(complaint.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Resolved*InReview*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_EmptyOrWhitespaceResolutionNote_ThrowsArgumentException(string note)
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = citizen.Id,
            Category = ComplaintCategory.MissedCollection,
            Subject = "InReview complaint",
            Description = "Desc",
            Status = ComplaintStatus.InReview,
            CreatedAt = DateTime.UtcNow
        };
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var request = new ResolveComplaintRequest
        {
            ResolutionNote = note
        };

        var act = () => service.ResolveAsync(complaint.Id, request, officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Resolution note*");
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task ResolveAsync_UnauthorizedRole_ThrowsForbiddenException(string role)
    {
        var (db, userManager) = CreateContext();
        var user = await SeedUserAsync(db, userManager, "User");
        var service = CreateService(db, userManager);

        var request = new ResolveComplaintRequest { ResolutionNote = "Some valid note" };
        var act = () => service.ResolveAsync(Guid.NewGuid(), request, user.Id, role);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 8. LOCATION IMMUTABILITY TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Location_RemainsUnchanged_AcrossStartReviewAndResolve()
    {
        var (db, userManager) = CreateContext();
        var citizen = await SeedUserAsync(db, userManager, "Citizen");
        var officer = await SeedUserAsync(db, userManager, "Officer");
        var service = CreateService(db, userManager);

        const double initialLat = 6.9271;
        const double initialLon = 79.8612;
        const string initialLocationDesc = "Beside the public library main gate";

        // 1. Create with location
        var createReq = new CreateComplaintRequest
        {
            Category = ComplaintCategory.PoorService,
            Subject = "Damaged bin at library",
            Description = "Lid is shattered",
            Latitude = initialLat,
            Longitude = initialLon,
            LocationDescription = initialLocationDesc
        };
        var created = await service.CreateAsync(createReq, citizen.Id, AppRoles.Citizen);

        created.Latitude.Should().Be(initialLat);
        created.Longitude.Should().Be(initialLon);
        created.LocationDescription.Should().Be(initialLocationDesc);

        // 2. Start Review
        var inReview = await service.StartReviewAsync(created.Id, officer.Id, AppRoles.WasteOfficer);
        inReview.Latitude.Should().Be(initialLat);
        inReview.Longitude.Should().Be(initialLon);
        inReview.LocationDescription.Should().Be(initialLocationDesc);

        // 3. Resolve
        var resolveReq = new ResolveComplaintRequest
        {
            ResolutionNote = "Bin lid replaced by maintenance team."
        };
        var resolved = await service.ResolveAsync(created.Id, resolveReq, officer.Id, AppRoles.WasteOfficer);
        resolved.Latitude.Should().Be(initialLat);
        resolved.Longitude.Should().Be(initialLon);
        resolved.LocationDescription.Should().Be(initialLocationDesc);

        // 4. Verify directly in database
        var finalEntity = await db.Complaints.FindAsync(created.Id);
        finalEntity!.Latitude.Should().Be(initialLat);
        finalEntity.Longitude.Should().Be(initialLon);
        finalEntity.LocationDescription.Should().Be(initialLocationDesc);
    }
}
