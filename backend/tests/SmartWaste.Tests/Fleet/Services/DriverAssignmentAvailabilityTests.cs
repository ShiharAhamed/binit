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
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Fleet.Services;

public class DriverAssignmentAvailabilityTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SmartWaste_DriverAssignmentAvailability_{Guid.NewGuid():N}")
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    [Fact]
    public async Task Create_RejectsOffDutyDriverForANewAssignment()
    {
        await using var db = CreateContext();
        var graph = await SeedAsync(db, DriverAvailabilityStatus.OffDuty);

        var act = () => new CollectionAssignmentService(db, DriverRoleManager()).CreateAsync(graph.Request, graph.Officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>();
    }

    [Fact]
    public async Task Create_RejectsDriverWithAnExistingUnfinishedAssignment()
    {
        await using var db = CreateContext();
        var graph = await SeedAsync(db, DriverAvailabilityStatus.Available);
        var service = new CollectionAssignmentService(db, DriverRoleManager());
        await service.CreateAsync(graph.Request, graph.Officer.Id, AppRoles.WasteOfficer);

        var secondVehicle = new Vehicle { RegistrationNumber = "C3-SECOND-VEHICLE", CapacityLiters = 100, OperationalStatus = VehicleOperationalStatus.Available };
        var secondTask = new CollectionTask { TaskCode = "C3-SECOND-TASK", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow, WasteReportId = Guid.NewGuid() };
        db.AddRange(secondVehicle, secondTask);
        await db.SaveChangesAsync();
        var secondRequest = new CreateCollectionAssignmentRequest
        {
            DriverId = graph.Driver.Id,
            VehicleId = secondVehicle.Id,
            CollectionTaskIds = new[] { secondTask.Id },
            Stops = new[] { new CreateRouteStopRequest { CollectionTaskId = secondTask.Id, Sequence = 1 } }
        };

        var act = () => service.CreateAsync(secondRequest, graph.Officer.Id, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<BusinessRuleConflictException>();
    }

    private static async Task<(AppUser Officer, AppUser Driver, CreateCollectionAssignmentRequest Request)> SeedAsync(AppDbContext db, DriverAvailabilityStatus availability)
    {
        var officer = new AppUser { Id = Guid.NewGuid(), FullName = "Officer", UserName = "officer", IsActive = true };
        var driver = new AppUser { Id = Guid.NewGuid(), FullName = "Driver", UserName = "driver", IsActive = true };
        var profile = new DriverProfile { UserId = driver.Id, User = driver, AvailabilityStatus = availability, IsEligible = false };
        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "C3-AVAILABILITY", CapacityLiters = 100, OperationalStatus = VehicleOperationalStatus.Available };
        var task = new CollectionTask { Id = Guid.NewGuid(), TaskCode = "C3-AVAILABILITY-TASK", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow, WasteReportId = Guid.NewGuid() };
        db.AddRange(officer, driver, profile, vehicle, task);
        await db.SaveChangesAsync();
        return (officer, driver, new CreateCollectionAssignmentRequest
        {
            DriverId = driver.Id,
            VehicleId = vehicle.Id,
            CollectionTaskIds = new[] { task.Id },
            Stops = new[] { new CreateRouteStopRequest { CollectionTaskId = task.Id, Sequence = 1 } }
        });
    }

    private static UserManager<AppUser> DriverRoleManager()
    {
        var store = new Mock<IUserStore<AppUser>>();
        var users = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        users.Setup(manager => manager.IsInRoleAsync(It.IsAny<AppUser>(), AppRoles.Driver)).ReturnsAsync(true);
        return users.Object;
    }
}
