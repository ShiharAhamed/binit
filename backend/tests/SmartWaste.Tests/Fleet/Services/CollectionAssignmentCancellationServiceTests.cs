using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
using SmartWaste.Infrastructure.Fleet.Services;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Collection.Services;

public class CollectionAssignmentCancellationServiceTests
{
    [Fact]
    public async Task Cancel_RequeuesTasksReleasesClaimsAndPreservesSourceState()
    {
        await using var db = Context();
        var (officer, manager, driver, vehicle, report, bin, reportTask, binTask) = await SeedAsync(db);
        var service = new CollectionAssignmentService(db, Users());
        var created = await service.CreateAsync(CreateRequest(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);

        var cancelled = await service.CancelAsync(created.Id, new CancelCollectionAssignmentRequest { Reason = "Vehicle was reassigned before departure." }, manager.Id, AppRoles.MunicipalManager);

        cancelled.Status.Should().Be(CollectionAssignmentStatus.Cancelled);
        cancelled.Route!.Stops.Should().HaveCount(2);
        cancelled.Route.Stops.Should().OnlyContain(x => x.Status == RouteStopStatus.Pending);
        (await db.CollectionAssignments.SingleAsync(x => x.Id == created.Id)).CancellationReason.Should().Be("Vehicle was reassigned before departure.");
        (await db.CollectionAssignments.SingleAsync(x => x.Id == created.Id)).CancelledAt.Should().NotBeNull();
        (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == created.Id).ToListAsync()).Should().OnlyContain(x => !x.IsActive && x.ReleasedByUserId == manager.Id && x.ReleaseReason == "Vehicle was reassigned before departure.");
        (await db.CollectionTasks.Where(x => x.Id == reportTask.Id || x.Id == binTask.Id).Select(x => x.Status).ToListAsync()).Should().AllBeEquivalentTo(CollectionTaskStatus.Scheduled);
        (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == created.Id)).Should().Be(2);
        (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == reportTask.Id || x.CollectionTaskId == binTask.Id)).Should().Be(4);
        (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Scheduled);
        (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().BeNull();
        (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);
        (await new AssignmentReadService(db).GetAvailableTasksAsync(new AvailableAssignmentTaskQuery(), manager.Id, AppRoles.MunicipalManager)).Items.Select(x => x.Id).Should().Contain(new[] { reportTask.Id, binTask.Id });
        (await new VehicleService(db).GetByIdAsync(vehicle.Id, manager.Id, AppRoles.MunicipalManager)).IsOccupied.Should().BeFalse();

        var reassigned = await service.CreateAsync(CreateRequest(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);
        reassigned.Id.Should().NotBe(created.Id);
        (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionTaskId == reportTask.Id || x.CollectionTaskId == binTask.Id).ToListAsync()).Count(x => x.IsActive).Should().Be(2);
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.CancelAsync(created.Id, new CancelCollectionAssignmentRequest { Reason = "Already cancelled assignment." }, officer.Id, AppRoles.WasteOfficer));
    }

    [Theory]
    [InlineData(AppRoles.Driver)]
    [InlineData(AppRoles.Citizen)]
    public async Task Cancel_RejectsUnauthorizedRole(string role)
    {
        await using var db = Context();
        await Assert.ThrowsAsync<ForbiddenException>(() => new CollectionAssignmentService(db, Users()).CancelAsync(Guid.NewGuid(), new CancelCollectionAssignmentRequest { Reason = "No authority." }, Guid.NewGuid(), role));
    }

    [Fact]
    public async Task Cancel_RejectsInvalidReasonBeforeChangingAssignment()
    {
        await using var db = Context();
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => new CollectionAssignmentService(db, Users()).CancelAsync(Guid.NewGuid(), new CancelCollectionAssignmentRequest { Reason = "bad" }, Guid.NewGuid(), AppRoles.WasteOfficer));
        (await db.CollectionAssignments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cancel_RejectsExecutedStopAndPreservesActiveState()
    {
        await using var db = Context();
        var (officer, _, driver, vehicle, _, _, reportTask, binTask) = await SeedAsync(db);
        var service = new CollectionAssignmentService(db, Users());
        var created = await service.CreateAsync(CreateRequest(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);
        var stop = await db.RouteStops.FirstAsync(x => x.RouteId == created.Route!.Id);
        stop.Status = RouteStopStatus.Completed;
        stop.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.CancelAsync(created.Id, new CancelCollectionAssignmentRequest { Reason = "Stop unexpectedly has an outcome." }, officer.Id, AppRoles.WasteOfficer));

        (await db.CollectionAssignments.SingleAsync(x => x.Id == created.Id)).Status.Should().Be(CollectionAssignmentStatus.Assigned);
        (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == created.Id).ToListAsync()).Should().OnlyContain(x => x.IsActive);
        (await db.CollectionTasks.Where(x => x.Id == reportTask.Id || x.Id == binTask.Id).Select(x => x.Status).ToListAsync()).Should().AllBeEquivalentTo(CollectionTaskStatus.Assigned);
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static UserManager<AppUser> Users()
    {
        var store = new Mock<IUserStore<AppUser>>();
        var users = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        users.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), AppRoles.Driver)).ReturnsAsync(true);
        return users.Object;
    }

    private static async Task<(AppUser Officer, AppUser Manager, AppUser Driver, Vehicle Vehicle, WasteReport Report, WasteBin Bin, CollectionTask ReportTask, CollectionTask BinTask)> SeedAsync(AppDbContext db)
    {
        var officer = User("Officer"); var manager = User("Manager"); var driver = User("Driver"); var citizen = User("Citizen");
        var report = new WasteReport { CitizenId = citizen.Id, Description = "Cancellation report target", WasteType = WasteType.General, Latitude = 6.9, Longitude = 79.9, Status = WasteReportStatus.Scheduled };
        var bin = new WasteBin { BinCode = "C3-CANCEL-BIN", CapacityLiters = 100, Latitude = 6.91, Longitude = 79.91, AdministrativeStatus = BinAdministrativeStatus.Active };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.General });
        var vehicle = new Vehicle { RegistrationNumber = "C3-CANCEL", CapacityLiters = 1000, OperationalStatus = VehicleOperationalStatus.Available };
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.General });
        var reportTask = new CollectionTask { TaskCode = "CANCEL-REPORT", WasteReportId = report.Id, CollectionReason = CollectionReason.VerifiedReport, Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(1), CreatedByUserId = officer.Id };
        var binTask = new CollectionTask { TaskCode = "CANCEL-BIN", WasteBinId = bin.Id, CollectionReason = CollectionReason.OfficerDiscretion, SchedulingReason = "Cancellation test", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(2), CreatedByUserId = officer.Id };
        db.Users.AddRange(officer, manager, driver, citizen);
        db.DriverProfiles.Add(new DriverProfile { UserId = driver.Id, LicenseNumber = "CANCEL-LICENSE", AvailabilityStatus = DriverAvailabilityStatus.Available, IsEligible = true });
        db.AddRange(report, bin, vehicle, reportTask, binTask);
        await db.SaveChangesAsync();
        return (officer, manager, driver, vehicle, report, bin, reportTask, binTask);
    }

    private static CreateCollectionAssignmentRequest CreateRequest(Guid driverId, Guid vehicleId, Guid reportTaskId, Guid binTaskId) => new()
    {
        DriverId = driverId,
        VehicleId = vehicleId,
        CollectionTaskIds = new[] { reportTaskId, binTaskId },
        Stops = new[]
        {
            new CreateRouteStopRequest { CollectionTaskId = reportTaskId, Sequence = 1 },
            new CreateRouteStopRequest { CollectionTaskId = binTaskId, Sequence = 2 }
        }
    };

    private static AppUser User(string name) => new() { Id = Guid.NewGuid(), FullName = name, UserName = $"{name}-{Guid.NewGuid():N}", IsActive = true };
}
