using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Fleet.DTOs.Responses;

/// <summary>
/// Structured, minimal fleet planning context for the internal AI Fleet and Route Agent.
/// Exposes scheduled collection tasks available for assignment, available and unoccupied drivers,
/// and operationally available and unoccupied vehicles.
/// </summary>
public class FleetPlanningContextDto
{
    public IReadOnlyList<FleetPlanningTaskDto> Tasks { get; set; } = Array.Empty<FleetPlanningTaskDto>();
    public IReadOnlyList<FleetPlanningDriverDto> Drivers { get; set; } = Array.Empty<FleetPlanningDriverDto>();
    public IReadOnlyList<FleetPlanningVehicleDto> Vehicles { get; set; } = Array.Empty<FleetPlanningVehicleDto>();
    public int TaskPage { get; set; } = 1;
    public int TaskPageSize { get; set; } = 20;
    public int TaskTotalCount { get; set; }
    public int TaskTotalPages => TaskPageSize > 0 ? (int)Math.Ceiling((double)TaskTotalCount / TaskPageSize) : 0;
}

/// <summary>
/// Minimal projection of a Scheduled collection task available for new assignment planning.
/// Excludes citizen PII, officer notes, attachments, and audit histories.
/// </summary>
public class FleetPlanningTaskDto
{
    public Guid TaskId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public CollectionReason CollectionReason { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? AddressText { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public IReadOnlyList<string> WasteTypes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Minimal projection of an available, unoccupied Driver suitable for new assignment planning.
/// Excludes personal contact details, license numbers, and administrative notes.
/// </summary>
public class FleetPlanningDriverDto
{
    public Guid DriverId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DriverAvailabilityStatus AvailabilityStatus { get; set; }
    public bool IsOccupied { get; set; }
}

/// <summary>
/// Minimal projection of an operationally available, unoccupied Vehicle suitable for new assignment planning.
/// Excludes internal maintenance history and audit data.
/// </summary>
public class FleetPlanningVehicleDto
{
    public Guid VehicleId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public int CapacityLiters { get; set; }
    public VehicleOperationalStatus OperationalStatus { get; set; }
    public bool IsOccupied { get; set; }
    public IReadOnlyList<string> SupportedWasteTypes { get; set; } = Array.Empty<string>();
}
