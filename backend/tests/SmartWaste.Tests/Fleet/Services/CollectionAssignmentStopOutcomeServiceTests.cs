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
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Collection.Services;

public class CollectionAssignmentStopOutcomeServiceTests
{
    [Fact]
    public async Task CompleteReportAndFailBin_OwnStartedAssignment_PreservesRunAndSourceBoundaries()
    {
        await using var db = Context();
        var (officer, driver, report, bin, vehicle, reportTask, binTask) = await SeedAsync(db);
        var service = new CollectionAssignmentService(db, Users());
        var created = await service.CreateAsync(Request(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);
        var started = await service.StartAsync(created.Id, driver.Id, AppRoles.Driver);
        var reportStop = started.Route!.Stops.Single(x => x.Task.Id == reportTask.Id);
        var binStop = started.Route.Stops.Single(x => x.Task.Id == binTask.Id);

        var completed = await service.CompleteStopAsync(created.Id, reportStop.Id, driver.Id, AppRoles.Driver);
        var failed = await service.FailStopAsync(created.Id, binStop.Id, new FailRouteStopRequest { Reason = "Access was safely blocked at the bin location." }, driver.Id, AppRoles.Driver);

        completed.Status.Should().Be(CollectionAssignmentStatus.InProgress);
        failed.Status.Should().Be(CollectionAssignmentStatus.InProgress);
        (await db.CollectionAssignments.SingleAsync(x => x.Id == created.Id)).Status.Should().Be(CollectionAssignmentStatus.InProgress);
        var persistedReportStop = await db.RouteStops.SingleAsync(x => x.Id == reportStop.Id);
        persistedReportStop.Status.Should().Be(RouteStopStatus.Completed);
        persistedReportStop.CompletedAt.Should().NotBeNull();
        var persistedBinStop = await db.RouteStops.SingleAsync(x => x.Id == binStop.Id);
        persistedBinStop.Status.Should().Be(RouteStopStatus.Failed);
        persistedBinStop.FailureReason.Should().Be("Access was safely blocked at the bin location.");
        persistedBinStop.FailedAt.Should().NotBeNull();
        (await db.CollectionTasks.SingleAsync(x => x.Id == reportTask.Id)).Status.Should().Be(CollectionTaskStatus.Completed);
        (await db.CollectionTasks.SingleAsync(x => x.Id == binTask.Id)).Status.Should().Be(CollectionTaskStatus.Failed);
        (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Resolved);
        (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().BeNull();
        (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);
        (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == created.Id).ToListAsync()).Should().OnlyContain(x => x.IsActive);
        (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == created.Id)).Should().Be(2);
        (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == reportTask.Id || x.CollectionTaskId == binTask.Id)).Should().Be(6);
        (await db.RouteStopStatusHistories.CountAsync(x => x.RouteStopId == reportStop.Id || x.RouteStopId == binStop.Id)).Should().Be(4);
        (await db.WasteReportStatusHistories.CountAsync(x => x.WasteReportId == report.Id)).Should().Be(2);
        (await db.Routes.Include(x => x.Stops).SingleAsync(x => x.CollectionAssignmentId == created.Id)).Stops.Select(x => x.Sequence).Should().Equal(1, 2);
        vehicle.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StopOutcome_RejectsOtherDriverInvalidFailureAndRepeatedOutcomeWithoutChanges()
    {
        await using var db = Context();
        var (officer, driver, report, _, vehicle, reportTask, binTask) = await SeedAsync(db);
        var otherDriver = User("Other Driver");
        db.Users.Add(otherDriver);
        db.DriverProfiles.Add(new DriverProfile { UserId = otherDriver.Id, LicenseNumber = "OUTCOME-OTHER", IsEligible = true });
        await db.SaveChangesAsync();
        var service = new CollectionAssignmentService(db, Users());
        var created = await service.CreateAsync(Request(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);
        var started = await service.StartAsync(created.Id, driver.Id, AppRoles.Driver);
        var reportStop = started.Route!.Stops.Single(x => x.Task.Id == reportTask.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CompleteStopAsync(created.Id, reportStop.Id, otherDriver.Id, AppRoles.Driver));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.FailStopAsync(created.Id, reportStop.Id, new FailRouteStopRequest { Reason = "bad" }, driver.Id, AppRoles.Driver));
        await service.CompleteStopAsync(created.Id, reportStop.Id, driver.Id, AppRoles.Driver);
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.FailStopAsync(created.Id, reportStop.Id, new FailRouteStopRequest { Reason = "The same stop must not receive another outcome." }, driver.Id, AppRoles.Driver));

        (await db.RouteStops.SingleAsync(x => x.Id == reportStop.Id)).Status.Should().Be(RouteStopStatus.Completed);
        (await db.CollectionTasks.SingleAsync(x => x.Id == reportTask.Id)).Status.Should().Be(CollectionTaskStatus.Completed);
        (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Resolved);
        (await db.RouteStopStatusHistories.CountAsync(x => x.RouteStopId == reportStop.Id)).Should().Be(2);
        (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == reportTask.Id)).Should().Be(3);
        (await db.CollectionTasks.SingleAsync(x => x.Id == binTask.Id)).Status.Should().Be(CollectionTaskStatus.InProgress);
    }

    [Fact]
    public async Task CompleteBinStop_UpdatesCollectionTimestampWithoutCreatingObservation()
    {
        await using var db = Context();
        var (officer, driver, _, bin, vehicle, reportTask, binTask) = await SeedAsync(db);
        var service = new CollectionAssignmentService(db, Users());
        var created = await service.CreateAsync(Request(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);
        var started = await service.StartAsync(created.Id, driver.Id, AppRoles.Driver);
        var binStop = started.Route!.Stops.Single(x => x.Task.Id == binTask.Id);

        await service.CompleteStopAsync(created.Id, binStop.Id, driver.Id, AppRoles.Driver);

        (await db.RouteStops.SingleAsync(x => x.Id == binStop.Id)).Status.Should().Be(RouteStopStatus.Completed);
        (await db.CollectionTasks.SingleAsync(x => x.Id == binTask.Id)).Status.Should().Be(CollectionTaskStatus.Completed);
        (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().NotBeNull();
        (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);
        (await db.CollectionAssignments.SingleAsync(x => x.Id == created.Id)).Status.Should().Be(CollectionAssignmentStatus.InProgress);
    }

    [Fact]
    public async Task RecordDriverBinObservation_OwnCompletedBinStop_IsAppendOnlyAndPreservesCollectionState()
    {
        await using var db = Context();
        var (officer, driver, _, bin, vehicle, reportTask, binTask) = await SeedAsync(db);
        var service = new CollectionAssignmentService(db, Users());
        var created = await service.CreateAsync(Request(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);
        var started = await service.StartAsync(created.Id, driver.Id, AppRoles.Driver);
        var binStop = started.Route!.Stops.Single(x => x.Task.Id == binTask.Id);
        await service.CompleteStopAsync(created.Id, binStop.Id, driver.Id, AppRoles.Driver);
        var collectedAt = (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt;

        var observation = await service.RecordDriverBinObservationAsync(created.Id, binStop.Id, new RecordBinObservationRequest { FillLevelPercent = 25, Condition = BinCondition.Good, Notes = "Observed after genuine collection." }, driver.Id, AppRoles.Driver);

        observation.WasteBinId.Should().Be(bin.Id); observation.RecordedByUserId.Should().Be(driver.Id);
        (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(1);
        (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().Be(collectedAt);
        (await db.CollectionAssignments.SingleAsync(x => x.Id == created.Id)).Status.Should().Be(CollectionAssignmentStatus.InProgress);
        (await db.CollectionTasks.SingleAsync(x => x.Id == binTask.Id)).Status.Should().Be(CollectionTaskStatus.Completed);
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.RecordDriverBinObservationAsync(created.Id, started.Route.Stops.Single(x => x.Task.Id == reportTask.Id).Id, new RecordBinObservationRequest { FillLevelPercent = 25, Condition = BinCondition.Good }, driver.Id, AppRoles.Driver));
        await Assert.ThrowsAsync<ForbiddenException>(() => service.RecordDriverBinObservationAsync(created.Id, binStop.Id, new RecordBinObservationRequest { FillLevelPercent = 25, Condition = BinCondition.Good }, Guid.NewGuid(), AppRoles.WasteOfficer));
    }

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    [InlineData(AppRoles.Citizen)]
    public async Task StopOutcome_RejectsNonDriverRole(string role)
    {
        await using var db = Context();
        await Assert.ThrowsAsync<ForbiddenException>(() => new CollectionAssignmentService(db, Users()).CompleteStopAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), role));
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static UserManager<AppUser> Users(){var store=new Mock<IUserStore<AppUser>>();var users=new Mock<UserManager<AppUser>>(store.Object,null!,null!,null!,null!,null!,null!,null!,null!);users.Setup(x=>x.IsInRoleAsync(It.IsAny<AppUser>(),AppRoles.Driver)).ReturnsAsync(true);return users.Object;}
    private static async Task<(AppUser Officer, AppUser Driver, WasteReport Report, WasteBin Bin, Vehicle Vehicle, CollectionTask ReportTask, CollectionTask BinTask)> SeedAsync(AppDbContext db){var officer=User("Officer");var driver=User("Driver");var citizen=User("Citizen");var report=new WasteReport{CitizenId=citizen.Id,Description="Outcome report",WasteType=WasteType.General,Latitude=6.9,Longitude=79.9,Status=WasteReportStatus.Scheduled};var bin=new WasteBin{BinCode="C3-OUTCOME-BIN",CapacityLiters=100,Latitude=6.91,Longitude=79.91,AdministrativeStatus=BinAdministrativeStatus.Active};bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType{WasteType=WasteType.General});var vehicle=new Vehicle{RegistrationNumber="C3-OUTCOME",CapacityLiters=1000,OperationalStatus=VehicleOperationalStatus.Available};vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType{WasteType=WasteType.General});var reportTask=new CollectionTask{TaskCode="OUTCOME-REPORT",WasteReportId=report.Id,CollectionReason=CollectionReason.VerifiedReport,Status=CollectionTaskStatus.Scheduled,ScheduledAt=DateTime.UtcNow.AddHours(1),CreatedByUserId=officer.Id};var binTask=new CollectionTask{TaskCode="OUTCOME-BIN",WasteBinId=bin.Id,CollectionReason=CollectionReason.OfficerDiscretion,SchedulingReason="Outcome test",Status=CollectionTaskStatus.Scheduled,ScheduledAt=DateTime.UtcNow.AddHours(2),CreatedByUserId=officer.Id};db.Users.AddRange(officer,driver,citizen);db.DriverProfiles.Add(new DriverProfile{UserId=driver.Id,LicenseNumber="OUTCOME-LICENSE",AvailabilityStatus=DriverAvailabilityStatus.Available,IsEligible=true});db.AddRange(report,bin,vehicle,reportTask,binTask);await db.SaveChangesAsync();return(officer,driver,report,bin,vehicle,reportTask,binTask);}
    private static CreateCollectionAssignmentRequest Request(Guid driverId,Guid vehicleId,Guid reportTaskId,Guid binTaskId)=>new(){DriverId=driverId,VehicleId=vehicleId,CollectionTaskIds=new[]{reportTaskId,binTaskId},Stops=new[]{new CreateRouteStopRequest{CollectionTaskId=reportTaskId,Sequence=1},new CreateRouteStopRequest{CollectionTaskId=binTaskId,Sequence=2}}};
    private static AppUser User(string name)=>new(){Id=Guid.NewGuid(),FullName=name,UserName=$"{name}-{Guid.NewGuid():N}",IsActive=true};
}
