using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Interfaces;
using SmartWaste.Application.Fleet.Queries;
using SmartWaste.Application.Fleet.Services;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Fleet.Services;

/// <summary>
/// Infrastructure implementation of IFleetPlanningAiService for internal AI tools.
/// Enforces data minimization, authoritative eligibility rules, and deterministic compatibility.
/// </summary>
public class FleetPlanningAiService : IFleetPlanningAiService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;

    public FleetPlanningAiService(AppDbContext db, UserManager<AppUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<FleetPlanningContextDto> GetPlanningContextAsync(
        GetFleetPlanningContextForAiQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Scheduled collection tasks available for assignment
        var tasksQuery = _db.CollectionTasks.AsNoTracking()
            .Include(x => x.WasteReport)
            .Include(x => x.WasteBin)
                .ThenInclude(x => x!.AcceptedWasteTypes)
            .Where(x => x.Status == CollectionTaskStatus.Scheduled
                   && !_db.CollectionAssignmentTaskClaims.Any(c => c.CollectionTaskId == x.Id && c.IsActive));

        var totalTaskCount = await tasksQuery.CountAsync(cancellationToken);

        var pagedTasks = await tasksQuery
            .OrderBy(x => x.ScheduledAt)
            .ThenBy(x => x.TaskCode)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var taskDtos = pagedTasks.Select(t => new FleetPlanningTaskDto
        {
            TaskId = t.Id,
            TaskCode = t.TaskCode,
            TargetType = t.WasteReportId.HasValue ? "Report" : (t.WasteBinId.HasValue ? "Bin" : "Unknown"),
            CollectionReason = t.CollectionReason,
            ScheduledAt = t.ScheduledAt,
            AddressText = t.WasteReport?.AddressText ?? t.WasteBin?.AddressText,
            Latitude = t.WasteReport?.Latitude ?? t.WasteBin?.Latitude,
            Longitude = t.WasteReport?.Longitude ?? t.WasteBin?.Longitude,
            WasteTypes = t.WasteReport != null
                ? new[] { t.WasteReport.WasteType.ToString() }
                : (t.WasteBin != null && t.WasteBin.AcceptedWasteTypes.Any()
                    ? t.WasteBin.AcceptedWasteTypes.Select(w => w.WasteType.ToString()).OrderBy(w => w).ToArray()
                    : Array.Empty<string>())
        }).ToList();

        // 2. Occupied resources from unfinished assignments
        var occupiedDriverIds = await _db.CollectionAssignments.AsNoTracking()
            .Where(x => x.Status == CollectionAssignmentStatus.Assigned || x.Status == CollectionAssignmentStatus.InProgress)
            .Select(x => x.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var occupiedVehicleIds = await _db.CollectionAssignments.AsNoTracking()
            .Where(x => x.Status == CollectionAssignmentStatus.Assigned || x.Status == CollectionAssignmentStatus.InProgress)
            .Select(x => x.VehicleId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // 3. Drivers: availabilityStatus == Available AND isOccupied == false
        var driverProfiles = await _db.DriverProfiles.AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.User != null
                     && x.User.IsActive
                     && x.AvailabilityStatus == DriverAvailabilityStatus.Available
                     && !occupiedDriverIds.Contains(x.UserId))
            .OrderBy(x => x.User!.FullName)
            .ToListAsync(cancellationToken);

        var driverDtos = new List<FleetPlanningDriverDto>();
        foreach (var p in driverProfiles)
        {
            if (p.User == null || !await _users.IsInRoleAsync(p.User, AppRoles.Driver))
                continue;

            driverDtos.Add(new FleetPlanningDriverDto
            {
                DriverId = p.UserId,
                DisplayName = p.User.FullName,
                AvailabilityStatus = p.AvailabilityStatus,
                IsOccupied = false
            });
        }

        // 4. Vehicles: operationalStatus == Available AND isOccupied == false
        var vehicles = await _db.Vehicles.AsNoTracking()
            .Include(x => x.SupportedWasteTypes)
            .Where(x => x.OperationalStatus == VehicleOperationalStatus.Available
                     && !occupiedVehicleIds.Contains(x.Id))
            .OrderBy(x => x.RegistrationNumber)
            .ToListAsync(cancellationToken);

        var vehicleDtos = vehicles.Select(v => new FleetPlanningVehicleDto
        {
            VehicleId = v.Id,
            RegistrationNumber = v.RegistrationNumber,
            VehicleType = v.VehicleType,
            CapacityLiters = v.CapacityLiters,
            OperationalStatus = v.OperationalStatus,
            IsOccupied = false,
            SupportedWasteTypes = v.SupportedWasteTypes.Select(w => w.WasteType.ToString()).OrderBy(w => w).ToArray()
        }).ToList();

        return new FleetPlanningContextDto
        {
            Tasks = taskDtos,
            Drivers = driverDtos,
            Vehicles = vehicleDtos,
            TaskPage = query.Page,
            TaskPageSize = query.PageSize,
            TaskTotalCount = totalTaskCount
        };
    }

    public async Task<FleetCompatibilityResultDto> CheckCompatibilityAsync(
        CheckFleetCompatibilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await _db.Vehicles.AsNoTracking()
            .Include(x => x.SupportedWasteTypes)
            .SingleOrDefaultAsync(x => x.Id == request.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle was not found.");

        var tasks = await _db.CollectionTasks.AsNoTracking()
            .Include(x => x.WasteReport)
            .Include(x => x.WasteBin)
                .ThenInclude(x => x!.AcceptedWasteTypes)
            .Where(x => request.TaskIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (tasks.Count != request.TaskIds.Count)
        {
            throw new NotFoundException("One or more collection tasks were not found.");
        }

        return FleetWasteCompatibilityEvaluator.Evaluate(tasks, vehicle);
    }

    public async Task<OperationalValidationContextDto> GetOperationalValidationContextAsync(
        GetOperationalValidationContextRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch fresh task state for requested task IDs
        var tasks = new List<OperationalValidationTaskDto>();
        if (request.TaskIds.Any())
        {
            var taskEntities = await _db.CollectionTasks.AsNoTracking()
                .Include(x => x.WasteReport)
                .Include(x => x.WasteBin)
                    .ThenInclude(x => x!.AcceptedWasteTypes)
                .Where(x => request.TaskIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var activeClaimTaskIds = await _db.CollectionAssignmentTaskClaims.AsNoTracking()
                .Where(c => c.IsActive && request.TaskIds.Contains(c.CollectionTaskId))
                .Select(c => c.CollectionTaskId)
                .Distinct()
                .ToListAsync(cancellationToken);

            tasks = taskEntities.Select(t => new OperationalValidationTaskDto
            {
                TaskId = t.Id,
                TaskCode = t.TaskCode,
                Status = t.Status.ToString(),
                HasActiveAssignment = activeClaimTaskIds.Contains(t.Id)
                    || t.Status == CollectionTaskStatus.Assigned
                    || t.Status == CollectionTaskStatus.InProgress,
                WasteTypes = t.WasteReport != null
                    ? new[] { t.WasteReport.WasteType.ToString() }
                    : (t.WasteBin != null && t.WasteBin.AcceptedWasteTypes.Any()
                        ? t.WasteBin.AcceptedWasteTypes.Select(w => w.WasteType.ToString()).OrderBy(w => w).ToArray()
                        : Array.Empty<string>())
            }).ToList();
        }

        // 2. Fetch fresh driver state for requested driver IDs
        var drivers = new List<OperationalValidationDriverDto>();
        if (request.DriverIds.Any())
        {
            var occupiedDriverIds = await _db.CollectionAssignments.AsNoTracking()
                .Where(x => (x.Status == CollectionAssignmentStatus.Assigned || x.Status == CollectionAssignmentStatus.InProgress)
                         && request.DriverIds.Contains(x.DriverId))
                .Select(x => x.DriverId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var driverProfiles = await _db.DriverProfiles.AsNoTracking()
                .Include(x => x.User)
                .Where(x => request.DriverIds.Contains(x.UserId))
                .ToListAsync(cancellationToken);

            drivers = driverProfiles.Select(p => new OperationalValidationDriverDto
            {
                DriverId = p.UserId,
                DisplayName = p.User?.FullName ?? "Unknown",
                AvailabilityStatus = p.AvailabilityStatus.ToString(),
                IsOccupied = occupiedDriverIds.Contains(p.UserId)
            }).ToList();
        }

        // 3. Fetch fresh vehicle state for requested vehicle IDs
        var vehicles = new List<OperationalValidationVehicleDto>();
        if (request.VehicleIds.Any())
        {
            var occupiedVehicleIds = await _db.CollectionAssignments.AsNoTracking()
                .Where(x => (x.Status == CollectionAssignmentStatus.Assigned || x.Status == CollectionAssignmentStatus.InProgress)
                         && request.VehicleIds.Contains(x.VehicleId))
                .Select(x => x.VehicleId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var vehicleEntities = await _db.Vehicles.AsNoTracking()
                .Include(x => x.SupportedWasteTypes)
                .Where(x => request.VehicleIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            vehicles = vehicleEntities.Select(v => new OperationalValidationVehicleDto
            {
                VehicleId = v.Id,
                RegistrationNumber = v.RegistrationNumber,
                VehicleType = v.VehicleType.ToString(),
                OperationalStatus = v.OperationalStatus.ToString(),
                IsOccupied = occupiedVehicleIds.Contains(v.Id),
                SupportedWasteTypes = v.SupportedWasteTypes.Select(w => w.WasteType.ToString()).OrderBy(w => w).ToArray()
            }).ToList();
        }

        return new OperationalValidationContextDto
        {
            Tasks = tasks,
            Drivers = drivers,
            Vehicles = vehicles
        };
    }
}
