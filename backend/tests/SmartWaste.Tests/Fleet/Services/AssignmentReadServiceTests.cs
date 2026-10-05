using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Collection.Services;

public class AssignmentReadServiceTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task AvailableTasks_ReturnsOnlyScheduledTasksWithoutActiveClaims()
    {
        await using var db = Db();
        var available = new CollectionTask { TaskCode = "AVAILABLE", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow };
        var claimed = new CollectionTask { TaskCode = "CLAIMED", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow };
        var assigned = new CollectionTask { TaskCode = "ASSIGNED", Status = CollectionTaskStatus.Assigned, ScheduledAt = DateTime.UtcNow };
        db.AddRange(available, claimed, assigned);
        db.CollectionAssignmentTaskClaims.Add(new CollectionAssignmentTaskClaim { CollectionTaskId = claimed.Id, CollectionAssignmentId = Guid.NewGuid(), IsActive = true });
        await db.SaveChangesAsync();
        var result = await new AssignmentReadService(db).GetAvailableTasksAsync(new AvailableAssignmentTaskQuery(), Guid.NewGuid(), AppRoles.WasteOfficer);
        result.Items.Select(x => x.TaskCode).Should().Equal("AVAILABLE");
    }

    [Fact]
    public async Task Driver_CanReadOnlyOwnAssignment()
    {
        await using var db = Db();
        var driver = new AppUser { Id = Guid.NewGuid(), FullName = "Driver", UserName = "driver" };
        var other = new AppUser { Id = Guid.NewGuid(), FullName = "Other", UserName = "other" };
        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "C3-READ", CapacityLiters = 1 };
        var assignment = new CollectionAssignment { AssignmentNumber = 42, DriverId = driver.Id, Driver = new DriverProfile { UserId = driver.Id, User = driver, LicenseNumber = "L1" }, VehicleId = vehicle.Id, Vehicle = vehicle, AssignedByUserId = Guid.NewGuid() };
        db.AddRange(driver, other, vehicle, assignment); await db.SaveChangesAsync();
        var service = new AssignmentReadService(db);
        var detail = await service.GetDetailAsync(assignment.Id, driver.Id, AppRoles.Driver);
        detail.Id.Should().Be(assignment.Id);
        detail.AssignmentNumber.Should().Be(42);
        detail.AssignmentReference.Should().Be("Assignment 042");
        var list = await service.GetMineAsync(new AssignmentListQuery(), driver.Id, AppRoles.Driver);
        list.Items.Single().Id.Should().Be(assignment.Id);
        list.Items.Single().AssignmentReference.Should().Be("Assignment 042");
        await FluentActions.Invoking(() => service.GetDetailAsync(assignment.Id, other.Id, AppRoles.Driver)).Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DriverWithoutProfile_HasNoAssignmentsToRead()
    {
        await using var db = Db();
        var driver = new AppUser { Id = Guid.NewGuid(), FullName = "Unassigned driver", UserName = "unassigned-driver" };
        db.Users.Add(driver);
        await db.SaveChangesAsync();

        var result = await new AssignmentReadService(db).GetMineAsync(new AssignmentListQuery(), driver.Id, AppRoles.Driver);
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}
