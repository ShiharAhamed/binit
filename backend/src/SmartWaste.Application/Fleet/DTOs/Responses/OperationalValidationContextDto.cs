namespace SmartWaste.Application.Fleet.DTOs.Responses;

/// <summary>
/// Minimal structured response containing fresh authoritative state of specific tasks, drivers, and vehicles for C4 AI validation.
/// Excludes citizen PII, administrative notes, and internal secrets.
/// </summary>
public class OperationalValidationContextDto
{
    public IReadOnlyList<OperationalValidationTaskDto> Tasks { get; set; } = Array.Empty<OperationalValidationTaskDto>();
    public IReadOnlyList<OperationalValidationDriverDto> Drivers { get; set; } = Array.Empty<OperationalValidationDriverDto>();
    public IReadOnlyList<OperationalValidationVehicleDto> Vehicles { get; set; } = Array.Empty<OperationalValidationVehicleDto>();
}

public class OperationalValidationTaskDto
{
    public Guid TaskId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool HasActiveAssignment { get; set; }
    public IReadOnlyList<string> WasteTypes { get; set; } = Array.Empty<string>();
}

public class OperationalValidationDriverDto
{
    public Guid DriverId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string AvailabilityStatus { get; set; } = string.Empty;
    public bool IsOccupied { get; set; }
}

public class OperationalValidationVehicleDto
{
    public Guid VehicleId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = string.Empty;
    public bool IsOccupied { get; set; }
    public IReadOnlyList<string> SupportedWasteTypes { get; set; } = Array.Empty<string>();
}
