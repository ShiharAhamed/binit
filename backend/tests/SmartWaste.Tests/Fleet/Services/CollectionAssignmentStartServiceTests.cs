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

public class CollectionAssignmentStartServiceTests
{
    [Fact]
    public async Task Start_OwnMultiTaskAssignment_TransitionsAssignmentTasksAndReportOnly()
    {
        await using var db = Context();
        var (officer, driver, report, bin, vehicle, reportTask, binTask) = await SeedAsync(db);
        var service = new CollectionAssignmentService(db, Users());
        var created = await service.CreateAsync(Request(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);
        (await db.DriverProfiles.SingleAsync(x => x.UserId == driver.Id)).AvailabilityStatus = DriverAvailabilityStatus.OffDuty;
        await db.SaveChangesAsync();

        var started = await service.StartAsync(created.Id, driver.Id, AppRoles.Driver);

        started.Status.Should().Be(CollectionAssignmentStatus.InProgress);
        started.Route!.Stops.Select(x => x.Sequence).Should().Equal(1, 2);
        started.Route.Stops.Should().OnlyContain(x => x.Status == RouteStopStatus.Pending);
        (await db.CollectionAssignments.SingleAsync(x => x.Id == created.Id)).StartedAt.Should().NotBeNull();
        (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == created.Id).ToListAsync()).Should().OnlyContain(x => x.IsActive);
        (await db.CollectionTasks.Where(x => x.Id == reportTask.Id || x.Id == binTask.Id).Select(x => x.Status).ToListAsync()).Should().AllBeEquivalentTo(CollectionTaskStatus.InProgress);
        (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.InProgress);
        (await db.WasteReportStatusHistories.SingleAsync(x => x.WasteReportId == report.Id)).ChangedByUserId.Should().Be(driver.Id);
        (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == created.Id)).Should().Be(2);
        (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == reportTask.Id || x.CollectionTaskId == binTask.Id)).Should().Be(4);
        (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().BeNull();
        (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);
        (await new VehicleService(db).GetByIdAsync(vehicle.Id, officer.Id, AppRoles.WasteOfficer)).IsOccupied.Should().BeTrue();
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.StartAsync(created.Id, driver.Id, AppRoles.Driver));
    }

    [Fact]
    public async Task Start_RejectsOtherDriverAndInvalidReportWithoutPartialTransitions()
    {
        await using var db = Context();
        var (officer, driver, report, _, vehicle, reportTask, binTask) = await SeedAsync(db);
        var otherDriver = User("Other Driver");
        db.Users.Add(otherDriver);
        db.DriverProfiles.Add(new DriverProfile { UserId = otherDriver.Id, LicenseNumber = "START-OTHER", IsEligible = true });
        await db.SaveChangesAsync();
        var service = new CollectionAssignmentService(db, Users());
        var created = await service.CreateAsync(Request(driver.Id, vehicle.Id, reportTask.Id, binTask.Id), officer.Id, AppRoles.WasteOfficer);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.StartAsync(created.Id, otherDriver.Id, AppRoles.Driver));
        report.Status = WasteReportStatus.Verified;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.StartAsync(created.Id, driver.Id, AppRoles.Driver));

        (await db.CollectionAssignments.SingleAsync(x => x.Id == created.Id)).Status.Should().Be(CollectionAssignmentStatus.Assigned);
        (await db.CollectionTasks.Where(x => x.Id == reportTask.Id || x.Id == binTask.Id).Select(x => x.Status).ToListAsync()).Should().AllBeEquivalentTo(CollectionTaskStatus.Assigned);
        (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == created.Id)).Should().Be(1);
        (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == reportTask.Id || x.CollectionTaskId == binTask.Id)).Should().Be(2);
    }

    [Theory]
    [InlineData(AppRoles.WasteOfficer)]
    [InlineData(AppRoles.MunicipalManager)]
    [InlineData(AppRoles.Citizen)]
    public async Task Start_RejectsNonDriverRole(string role)
    {
        await using var db = Context();
        await Assert.ThrowsAsync<ForbiddenException>(() => new CollectionAssignmentService(db, Users()).StartAsync(Guid.NewGuid(), Guid.NewGuid(), role));
    }

    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static UserManager<AppUser> Users(){var store=new Mock<IUserStore<AppUser>>();var users=new Mock<UserManager<AppUser>>(store.Object,null!,null!,null!,null!,null!,null!,null!,null!);users.Setup(x=>x.IsInRoleAsync(It.IsAny<AppUser>(),AppRoles.Driver)).ReturnsAsync(true);return users.Object;}
    private static async Task<(AppUser Officer, AppUser Driver, WasteReport Report, WasteBin Bin, Vehicle Vehicle, CollectionTask ReportTask, CollectionTask BinTask)> SeedAsync(AppDbContext db){var officer=User("Officer");var driver=User("Driver");var citizen=User("Citizen");var report=new WasteReport{CitizenId=citizen.Id,Description="Start report",WasteType=WasteType.General,Latitude=6.9,Longitude=79.9,Status=WasteReportStatus.Scheduled};var bin=new WasteBin{BinCode="C3-START-BIN",CapacityLiters=100,Latitude=6.91,Longitude=79.91,AdministrativeStatus=BinAdministrativeStatus.Active};bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType{WasteType=WasteType.General});var vehicle=new Vehicle{RegistrationNumber="C3-START",CapacityLiters=1000,OperationalStatus=VehicleOperationalStatus.Available};vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType{WasteType=WasteType.General});var reportTask=new CollectionTask{TaskCode="START-REPORT",WasteReportId=report.Id,CollectionReason=CollectionReason.VerifiedReport,Status=CollectionTaskStatus.Scheduled,ScheduledAt=DateTime.UtcNow.AddHours(1),CreatedByUserId=officer.Id};var binTask=new CollectionTask{TaskCode="START-BIN",WasteBinId=bin.Id,CollectionReason=CollectionReason.OfficerDiscretion,SchedulingReason="Start test",Status=CollectionTaskStatus.Scheduled,ScheduledAt=DateTime.UtcNow.AddHours(2),CreatedByUserId=officer.Id};db.Users.AddRange(officer,driver,citizen);db.DriverProfiles.Add(new DriverProfile{UserId=driver.Id,LicenseNumber="START-LICENSE",AvailabilityStatus=DriverAvailabilityStatus.Available,IsEligible=true});db.AddRange(report,bin,vehicle,reportTask,binTask);await db.SaveChangesAsync();return(officer,driver,report,bin,vehicle,reportTask,binTask);}
    private static CreateCollectionAssignmentRequest Request(Guid driverId,Guid vehicleId,Guid reportTaskId,Guid binTaskId)=>new(){DriverId=driverId,VehicleId=vehicleId,CollectionTaskIds=new[]{reportTaskId,binTaskId},Stops=new[]{new CreateRouteStopRequest{CollectionTaskId=reportTaskId,Sequence=1},new CreateRouteStopRequest{CollectionTaskId=binTaskId,Sequence=2}}};
    private static AppUser User(string name)=>new(){Id=Guid.NewGuid(),FullName=name,UserName=$"{name}-{Guid.NewGuid():N}",IsActive=true};
}
