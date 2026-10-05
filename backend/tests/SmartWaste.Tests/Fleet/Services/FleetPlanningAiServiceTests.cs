using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Queries;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Fleet.Services;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Fleet.Services;

public class FleetPlanningAiServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static UserManager<AppUser> CreateUserManager(bool isDriver = true)
    {
        var store = new Mock<IUserStore<AppUser>>();
        var manager = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(m => m.IsInRoleAsync(It.IsAny<AppUser>(), AppRoles.Driver)).ReturnsAsync(isDriver);
        return manager.Object;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. FLEET PLANNING CONTEXT: TASKS FILTERING & PROJECTION
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlanningContext_IncludesOnlyScheduledUnclaimedTasks_AndRespectsPagination()
    {
        await using var db = CreateContext();

        // 1. Scheduled eligible task
        var eligibleTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-ELIGIBLE",
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(1),
            CollectionReason = CollectionReason.VerifiedReport,
            WasteReport = new WasteReport
            {
                Id = Guid.NewGuid(),
                Description = "Verified organic waste",
                WasteType = WasteType.Organic,
                Latitude = 6.9271,
                Longitude = 79.8612,
                AddressText = "123 Main St",
                Status = WasteReportStatus.Scheduled
            }
        };

        // 2. Scheduled but claimed task
        var claimedTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-CLAIMED",
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };
        var activeClaim = new CollectionAssignmentTaskClaim
        {
            Id = Guid.NewGuid(),
            CollectionTaskId = claimedTask.Id,
            CollectionAssignmentId = Guid.NewGuid(),
            IsActive = true
        };

        // 3. InProgress task (ineligible)
        var inProgressTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-INPROGRESS",
            Status = CollectionTaskStatus.InProgress,
            ScheduledAt = DateTime.UtcNow.AddHours(3)
        };

        // 4. Completed task (ineligible)
        var completedTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-COMPLETED",
            Status = CollectionTaskStatus.Completed,
            ScheduledAt = DateTime.UtcNow.AddHours(4)
        };

        // 5. Cancelled task (ineligible)
        var cancelledTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-CANCELLED",
            Status = CollectionTaskStatus.Cancelled,
            ScheduledAt = DateTime.UtcNow.AddHours(5)
        };

        db.CollectionTasks.AddRange(eligibleTask, claimedTask, inProgressTask, completedTask, cancelledTask);
        db.CollectionAssignmentTaskClaims.Add(activeClaim);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var context = await service.GetPlanningContextAsync(new GetFleetPlanningContextForAiQuery { Page = 1, PageSize = 10 });

        context.TaskTotalCount.Should().Be(1);
        context.Tasks.Should().HaveCount(1);
        var taskDto = context.Tasks.Single();
        taskDto.TaskId.Should().Be(eligibleTask.Id);
        taskDto.TaskCode.Should().Be("TASK-ELIGIBLE");
        taskDto.TargetType.Should().Be("Report");
        taskDto.WasteTypes.Should().Equal("Organic");
        taskDto.AddressText.Should().Be("123 Main St");
        taskDto.Latitude.Should().Be(6.9271);
        taskDto.Longitude.Should().Be(79.8612);
    }

    [Fact]
    public async Task GetPlanningContext_ProjectsBinTargetWasteTypesCorrectly()
    {
        await using var db = CreateContext();

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-RECYCLE",
            CapacityLiters = 500,
            Latitude = 6.91,
            Longitude = 79.85,
            AddressText = "Recycle Point"
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.Recyclable });
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.General });

        var binTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-BIN",
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            WasteBin = bin,
            WasteBinId = bin.Id
        };

        db.WasteBins.Add(bin);
        db.CollectionTasks.Add(binTask);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var context = await service.GetPlanningContextAsync(new GetFleetPlanningContextForAiQuery { Page = 1, PageSize = 20 });

        context.Tasks.Should().HaveCount(1);
        var dto = context.Tasks.Single();
        dto.TargetType.Should().Be("Bin");
        dto.WasteTypes.Should().BeEquivalentTo(new[] { "General", "Recyclable" });
    }

    [Fact]
    public async Task GetPlanningContext_BinWithNoAcceptedWasteTypes_ProjectsEmptyWasteTypes()
    {
        await using var db = CreateContext();

        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-EMPTY-TYPES",
            CapacityLiters = 200
        };

        var binTask = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TASK-NO-TYPES",
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow,
            WasteBin = bin,
            WasteBinId = bin.Id
        };

        db.WasteBins.Add(bin);
        db.CollectionTasks.Add(binTask);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var context = await service.GetPlanningContextAsync(new GetFleetPlanningContextForAiQuery { Page = 1, PageSize = 20 });

        context.Tasks.Should().HaveCount(1);
        context.Tasks.Single().WasteTypes.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. FLEET PLANNING CONTEXT: DRIVERS FILTERING
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlanningContext_IncludesOnlyAvailableAndUnoccupiedDrivers()
    {
        await using var db = CreateContext();

        // 1. Available + unoccupied (eligible)
        var user1 = new AppUser { Id = Guid.NewGuid(), FullName = "Driver Available", IsActive = true };
        var profile1 = new DriverProfile { UserId = user1.Id, User = user1, AvailabilityStatus = DriverAvailabilityStatus.Available };

        // 2. Available + occupied in Assigned assignment (ineligible)
        var user2 = new AppUser { Id = Guid.NewGuid(), FullName = "Driver Occupied Assigned", IsActive = true };
        var profile2 = new DriverProfile { UserId = user2.Id, User = user2, AvailabilityStatus = DriverAvailabilityStatus.Available };
        var assignment2 = new CollectionAssignment { Id = Guid.NewGuid(), DriverId = user2.Id, Status = CollectionAssignmentStatus.Assigned };

        // 3. Available + occupied in InProgress assignment (ineligible)
        var user3 = new AppUser { Id = Guid.NewGuid(), FullName = "Driver Occupied InProgress", IsActive = true };
        var profile3 = new DriverProfile { UserId = user3.Id, User = user3, AvailabilityStatus = DriverAvailabilityStatus.Available };
        var assignment3 = new CollectionAssignment { Id = Guid.NewGuid(), DriverId = user3.Id, Status = CollectionAssignmentStatus.InProgress };

        // 4. OffDuty + unoccupied (ineligible)
        var user4 = new AppUser { Id = Guid.NewGuid(), FullName = "Driver OffDuty Unoccupied", IsActive = true };
        var profile4 = new DriverProfile { UserId = user4.Id, User = user4, AvailabilityStatus = DriverAvailabilityStatus.OffDuty };

        // 5. OffDuty + occupied (ineligible)
        var user5 = new AppUser { Id = Guid.NewGuid(), FullName = "Driver OffDuty Occupied", IsActive = true };
        var profile5 = new DriverProfile { UserId = user5.Id, User = user5, AvailabilityStatus = DriverAvailabilityStatus.OffDuty };
        var assignment5 = new CollectionAssignment { Id = Guid.NewGuid(), DriverId = user5.Id, Status = CollectionAssignmentStatus.Assigned };

        // 6. Available but Inactive user account (ineligible)
        var user6 = new AppUser { Id = Guid.NewGuid(), FullName = "Driver Inactive", IsActive = false };
        var profile6 = new DriverProfile { UserId = user6.Id, User = user6, AvailabilityStatus = DriverAvailabilityStatus.Available };

        db.Users.AddRange(user1, user2, user3, user4, user5, user6);
        db.DriverProfiles.AddRange(profile1, profile2, profile3, profile4, profile5, profile6);
        db.CollectionAssignments.AddRange(assignment2, assignment3, assignment5);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var context = await service.GetPlanningContextAsync(new GetFleetPlanningContextForAiQuery { Page = 1, PageSize = 20 });

        context.Drivers.Should().HaveCount(1);
        var driverDto = context.Drivers.Single();
        driverDto.DriverId.Should().Be(user1.Id);
        driverDto.DisplayName.Should().Be("Driver Available");
        driverDto.AvailabilityStatus.Should().Be(DriverAvailabilityStatus.Available);
        driverDto.IsOccupied.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. FLEET PLANNING CONTEXT: VEHICLES FILTERING
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlanningContext_IncludesOnlyAvailableAndUnoccupiedVehicles()
    {
        await using var db = CreateContext();

        // 1. Available + unoccupied (eligible)
        var v1 = new Vehicle
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = "V-AVAIL-UNOCC",
            VehicleType = VehicleType.Compactor,
            CapacityLiters = 2500,
            OperationalStatus = VehicleOperationalStatus.Available
        };
        v1.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.Organic });

        // 2. Available + occupied in active assignment (ineligible)
        var v2 = new Vehicle
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = "V-AVAIL-OCC",
            OperationalStatus = VehicleOperationalStatus.Available
        };
        var assignment2 = new CollectionAssignment { Id = Guid.NewGuid(), VehicleId = v2.Id, Status = CollectionAssignmentStatus.Assigned };

        // 3. Maintenance (ineligible)
        var v3 = new Vehicle
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = "V-MAINT",
            OperationalStatus = VehicleOperationalStatus.Maintenance
        };

        // 4. Inactive (ineligible)
        var v4 = new Vehicle
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = "V-INACTIVE",
            OperationalStatus = VehicleOperationalStatus.Inactive
        };

        db.Vehicles.AddRange(v1, v2, v3, v4);
        db.CollectionAssignments.Add(assignment2);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var context = await service.GetPlanningContextAsync(new GetFleetPlanningContextForAiQuery { Page = 1, PageSize = 20 });

        context.Vehicles.Should().HaveCount(1);
        var vehicleDto = context.Vehicles.Single();
        vehicleDto.VehicleId.Should().Be(v1.Id);
        vehicleDto.RegistrationNumber.Should().Be("V-AVAIL-UNOCC");
        vehicleDto.VehicleType.Should().Be(VehicleType.Compactor);
        vehicleDto.CapacityLiters.Should().Be(2500);
        vehicleDto.OperationalStatus.Should().Be(VehicleOperationalStatus.Available);
        vehicleDto.IsOccupied.Should().BeFalse();
        vehicleDto.SupportedWasteTypes.Should().Equal("Organic");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. FLEET COMPATIBILITY: DETERMINISTIC LOGIC TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CheckCompatibility_CompatibleVehicleAndTasks_ReturnsCompatible()
    {
        await using var db = CreateContext();

        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "V-COMPAT", OperationalStatus = VehicleOperationalStatus.Available };
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.Organic });
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.General });

        var report = new WasteReport { Id = Guid.NewGuid(), Description = "Organic", WasteType = WasteType.Organic };
        var task1 = new CollectionTask { Id = Guid.NewGuid(), TaskCode = "T-1", Status = CollectionTaskStatus.Scheduled, WasteReport = report, WasteReportId = report.Id };

        var bin = new WasteBin { Id = Guid.NewGuid(), BinCode = "B-1" };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.General });
        var task2 = new CollectionTask { Id = Guid.NewGuid(), TaskCode = "T-2", Status = CollectionTaskStatus.Scheduled, WasteBin = bin, WasteBinId = bin.Id };

        db.Vehicles.Add(vehicle);
        db.WasteReports.Add(report);
        db.WasteBins.Add(bin);
        db.CollectionTasks.AddRange(task1, task2);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var result = await service.CheckCompatibilityAsync(new CheckFleetCompatibilityRequest
        {
            TaskIds = new[] { task1.Id, task2.Id },
            VehicleId = vehicle.Id
        });

        result.Status.Should().Be(FleetCompatibilityStatus.Compatible);
        result.RequiresAcknowledgement.Should().BeFalse();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckCompatibility_IncompatibleReportTask_ReturnsIncompatible()
    {
        await using var db = CreateContext();

        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "V-INCOMPAT", OperationalStatus = VehicleOperationalStatus.Available };
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.Organic });

        var report = new WasteReport { Id = Guid.NewGuid(), Description = "Hazardous", WasteType = WasteType.Hazardous };
        var task = new CollectionTask { Id = Guid.NewGuid(), TaskCode = "T-HAZ", Status = CollectionTaskStatus.Scheduled, WasteReport = report, WasteReportId = report.Id };

        db.Vehicles.Add(vehicle);
        db.WasteReports.Add(report);
        db.CollectionTasks.Add(task);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var result = await service.CheckCompatibilityAsync(new CheckFleetCompatibilityRequest
        {
            TaskIds = new[] { task.Id },
            VehicleId = vehicle.Id
        });

        result.Status.Should().Be(FleetCompatibilityStatus.Incompatible);
        result.RequiresAcknowledgement.Should().BeFalse();
        result.Issues.Should().ContainSingle(i => i.Contains("Hazardous") && i.Contains("V-INCOMPAT"));
    }

    [Fact]
    public async Task CheckCompatibility_IncompatibleBinTask_NoSharedWasteTypes_ReturnsIncompatible()
    {
        await using var db = CreateContext();

        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "V-BIN-INCOMPAT" };
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.Organic });

        var bin = new WasteBin { Id = Guid.NewGuid(), BinCode = "B-HAZ" };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.Hazardous });
        var task = new CollectionTask { Id = Guid.NewGuid(), TaskCode = "T-BIN", Status = CollectionTaskStatus.Scheduled, WasteBin = bin, WasteBinId = bin.Id };

        db.Vehicles.Add(vehicle);
        db.WasteBins.Add(bin);
        db.CollectionTasks.Add(task);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var result = await service.CheckCompatibilityAsync(new CheckFleetCompatibilityRequest
        {
            TaskIds = new[] { task.Id },
            VehicleId = vehicle.Id
        });

        result.Status.Should().Be(FleetCompatibilityStatus.Incompatible);
        result.RequiresAcknowledgement.Should().BeFalse();
        result.Issues.Should().ContainSingle();
    }

    [Fact]
    public async Task CheckCompatibility_BinWithNoAcceptedWasteTypes_ReturnsUnknownWithAcknowledgementRequired()
    {
        await using var db = CreateContext();

        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "V-BIN-UNKNOWN" };
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.Organic });

        var bin = new WasteBin { Id = Guid.NewGuid(), BinCode = "B-EMPTY" };
        var task = new CollectionTask { Id = Guid.NewGuid(), TaskCode = "T-EMPTY-BIN", Status = CollectionTaskStatus.Scheduled, WasteBin = bin, WasteBinId = bin.Id };

        db.Vehicles.Add(vehicle);
        db.WasteBins.Add(bin);
        db.CollectionTasks.Add(task);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var result = await service.CheckCompatibilityAsync(new CheckFleetCompatibilityRequest
        {
            TaskIds = new[] { task.Id },
            VehicleId = vehicle.Id
        });

        result.Status.Should().Be(FleetCompatibilityStatus.Unknown);
        result.RequiresAcknowledgement.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Contains("no registered accepted waste types"));
    }

    [Fact]
    public async Task CheckCompatibility_BinWithMixedWasteTypesPartialOverlap_ReturnsUnknownWithAcknowledgementRequired()
    {
        await using var db = CreateContext();

        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "V-MIXED" };
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.Organic });

        // Bin accepts Organic (supported) AND Hazardous (unsupported)
        var bin = new WasteBin { Id = Guid.NewGuid(), BinCode = "B-MIXED" };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.Organic });
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.Hazardous });
        var task = new CollectionTask { Id = Guid.NewGuid(), TaskCode = "T-MIXED", Status = CollectionTaskStatus.Scheduled, WasteBin = bin, WasteBinId = bin.Id };

        db.Vehicles.Add(vehicle);
        db.WasteBins.Add(bin);
        db.CollectionTasks.Add(task);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());
        var result = await service.CheckCompatibilityAsync(new CheckFleetCompatibilityRequest
        {
            TaskIds = new[] { task.Id },
            VehicleId = vehicle.Id
        });

        result.Status.Should().Be(FleetCompatibilityStatus.Unknown);
        result.RequiresAcknowledgement.Should().BeTrue();
        result.Issues.Should().ContainSingle(i => i.Contains("mixed waste types"));
    }

    [Fact]
    public async Task CheckCompatibility_UnknownVehicleId_ThrowsNotFoundException()
    {
        await using var db = CreateContext();
        var service = new FleetPlanningAiService(db, CreateUserManager());

        Func<Task> action = () => service.CheckCompatibilityAsync(new CheckFleetCompatibilityRequest
        {
            TaskIds = new[] { Guid.NewGuid() },
            VehicleId = Guid.NewGuid()
        });

        await action.Should().ThrowAsync<NotFoundException>().WithMessage("*Vehicle was not found*");
    }

    [Fact]
    public async Task CheckCompatibility_UnknownTaskId_ThrowsNotFoundException()
    {
        await using var db = CreateContext();

        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = "V-OK" };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        var service = new FleetPlanningAiService(db, CreateUserManager());

        Func<Task> action = () => service.CheckCompatibilityAsync(new CheckFleetCompatibilityRequest
        {
            TaskIds = new[] { Guid.NewGuid() },
            VehicleId = vehicle.Id
        });

        await action.Should().ThrowAsync<NotFoundException>().WithMessage("*collection tasks were not found*");
    }
}
