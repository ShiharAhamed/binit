using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using SmartWaste.Application.Collection.DTOs.Requests;
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

public class CollectionAssignmentFinalizationServiceTests
{
    [Theory]
    [InlineData(false, CollectionAssignmentStatus.Completed)]
    [InlineData(true, CollectionAssignmentStatus.PartiallyCompleted)]
    public async Task Finalize_OwnTerminalStops_CalculatesStatusAndReleasesClaims(bool failBin, CollectionAssignmentStatus expected)
    {
        await using var db = Context();
        var (officer, driver, report, bin, vehicle, reportTask, binTask) = await SeedAsync(db);
        var service = new CollectionAssignmentService(db, Users());
        var started = await StartAsync(service, officer, driver, vehicle, reportTask, binTask);
        var reportStop = started.Route!.Stops.Single(x => x.Task.Id == reportTask.Id);
        var binStop = started.Route.Stops.Single(x => x.Task.Id == binTask.Id);
        await service.CompleteStopAsync(started.Id, reportStop.Id, driver.Id, AppRoles.Driver);
        if (failBin) await service.FailStopAsync(started.Id, binStop.Id, new FailRouteStopRequest { Reason = "Access was blocked after a safe site assessment." }, driver.Id, AppRoles.Driver);
        else await service.CompleteStopAsync(started.Id, binStop.Id, driver.Id, AppRoles.Driver);
        var collectedAtBeforeFinalization = (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt;

        var finalized = await service.FinalizeAsync(started.Id, driver.Id, AppRoles.Driver);

        finalized.Status.Should().Be(expected);
        var assignment = await db.CollectionAssignments.SingleAsync(x => x.Id == started.Id);
        assignment.Status.Should().Be(expected);
        assignment.FinalizedAt.Should().NotBeNull();
        (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == started.Id).ToListAsync()).Should().OnlyContain(x => !x.IsActive && x.ReleasedByUserId == driver.Id && x.ReleaseReason == "Assignment finalized.");
        (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == started.Id)).Should().Be(3);
        (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Resolved);
        (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().Be(collectedAtBeforeFinalization, "the bin completion timestamp is set before finalization and must not be rewritten");
        (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);
        (await new VehicleService(db).GetByIdAsync(vehicle.Id, officer.Id, AppRoles.WasteOfficer)).IsOccupied.Should().BeFalse();
    }

    [Fact]
    public async Task Finalize_AllFailedStops_LeavesReportUnresolvedAndReleasesResources()
    {
        await using var db = Context();
        var (officer, driver, report, bin, vehicle, reportTask, binTask) = await SeedAsync(db);
        var service = new CollectionAssignmentService(db, Users());
        var started = await StartAsync(service, officer, driver, vehicle, reportTask, binTask);
        foreach (var stop in started.Route!.Stops)
            await service.FailStopAsync(started.Id, stop.Id, new FailRouteStopRequest { Reason = "Collection could not be safely completed at this stop." }, driver.Id, AppRoles.Driver);

        var finalized = await service.FinalizeAsync(started.Id, driver.Id, AppRoles.Driver);

        finalized.Status.Should().Be(CollectionAssignmentStatus.Failed);
        (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.InProgress);
        (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().BeNull();
        (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == started.Id).ToListAsync()).Should().OnlyContain(x => !x.IsActive);
        (await new VehicleService(db).GetByIdAsync(vehicle.Id, officer.Id, AppRoles.WasteOfficer)).IsOccupied.Should().BeFalse();
    }

    [Fact]
    public async Task Finalize_RejectsPendingStopOtherDriverAndRepeatedAttemptWithoutChangingClaims()
    {
        await using var db = Context();
        var (officer, driver, _, _, vehicle, reportTask, binTask) = await SeedAsync(db);
        var otherDriver = User("Other Driver"); db.Users.Add(otherDriver); await db.SaveChangesAsync();
        var service = new CollectionAssignmentService(db, Users());
        var started = await StartAsync(service, officer, driver, vehicle, reportTask, binTask);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.FinalizeAsync(started.Id, otherDriver.Id, AppRoles.Driver));
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.FinalizeAsync(started.Id, driver.Id, AppRoles.Driver));
        (await db.CollectionAssignments.SingleAsync(x => x.Id == started.Id)).Status.Should().Be(CollectionAssignmentStatus.InProgress);
        (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == started.Id).ToListAsync()).Should().OnlyContain(x => x.IsActive);
        foreach (var stop in started.Route!.Stops) await service.CompleteStopAsync(started.Id, stop.Id, driver.Id, AppRoles.Driver);
        await service.FinalizeAsync(started.Id, driver.Id, AppRoles.Driver);
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.FinalizeAsync(started.Id, driver.Id, AppRoles.Driver));
        (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == started.Id)).Should().Be(3);
    }

    private static async Task<SmartWaste.Application.Collection.DTOs.Responses.AssignmentDetailDto> StartAsync(CollectionAssignmentService service, AppUser officer, AppUser driver, Vehicle vehicle, CollectionTask reportTask, CollectionTask binTask)
    {
        var created = await service.CreateAsync(new CreateCollectionAssignmentRequest { DriverId = driver.Id, VehicleId = vehicle.Id, CollectionTaskIds = new[] { reportTask.Id, binTask.Id }, Stops = new[] { new CreateRouteStopRequest { CollectionTaskId = reportTask.Id, Sequence = 1 }, new CreateRouteStopRequest { CollectionTaskId = binTask.Id, Sequence = 2 } } }, officer.Id, AppRoles.WasteOfficer);
        return await service.StartAsync(created.Id, driver.Id, AppRoles.Driver);
    }
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static UserManager<AppUser> Users(){var store=new Mock<IUserStore<AppUser>>();var users=new Mock<UserManager<AppUser>>(store.Object,null!,null!,null!,null!,null!,null!,null!,null!);users.Setup(x=>x.IsInRoleAsync(It.IsAny<AppUser>(),AppRoles.Driver)).ReturnsAsync(true);return users.Object;}
    private static async Task<(AppUser Officer, AppUser Driver, WasteReport Report, WasteBin Bin, Vehicle Vehicle, CollectionTask ReportTask, CollectionTask BinTask)> SeedAsync(AppDbContext db){var officer=User("Officer");var driver=User("Driver");var citizen=User("Citizen");var report=new WasteReport{CitizenId=citizen.Id,Description="Finalization report",WasteType=WasteType.General,Latitude=6.9,Longitude=79.9,Status=WasteReportStatus.Scheduled};var bin=new WasteBin{BinCode="C3-FINAL-BIN",CapacityLiters=100,Latitude=6.91,Longitude=79.91,AdministrativeStatus=BinAdministrativeStatus.Active};bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType{WasteType=WasteType.General});var vehicle=new Vehicle{RegistrationNumber="C3-FINAL",CapacityLiters=1000,OperationalStatus=VehicleOperationalStatus.Available};vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType{WasteType=WasteType.General});var reportTask=new CollectionTask{TaskCode="FINAL-REPORT",WasteReportId=report.Id,CollectionReason=CollectionReason.VerifiedReport,Status=CollectionTaskStatus.Scheduled,ScheduledAt=DateTime.UtcNow.AddHours(1),CreatedByUserId=officer.Id};var binTask=new CollectionTask{TaskCode="FINAL-BIN",WasteBinId=bin.Id,CollectionReason=CollectionReason.OfficerDiscretion,SchedulingReason="Finalization test",Status=CollectionTaskStatus.Scheduled,ScheduledAt=DateTime.UtcNow.AddHours(2),CreatedByUserId=officer.Id};db.Users.AddRange(officer,driver,citizen);db.DriverProfiles.Add(new DriverProfile{UserId=driver.Id,LicenseNumber="FINAL-LICENSE",AvailabilityStatus=DriverAvailabilityStatus.Available,IsEligible=true});db.AddRange(report,bin,vehicle,reportTask,binTask);await db.SaveChangesAsync();return(officer,driver,report,bin,vehicle,reportTask,binTask);}
    private static AppUser User(string name)=>new(){Id=Guid.NewGuid(),FullName=name,UserName=$"{name}-{Guid.NewGuid():N}",IsActive=true};
}
