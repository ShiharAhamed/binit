namespace SmartWaste.Application.Fleet.DTOs.Requests;

/// <summary>
/// Request to retrieve fresh operational context for specific tasks, drivers, and vehicles referenced in an AI dispatch plan.
/// Used exclusively by the internal AI validation and operations service.
/// </summary>
public class GetOperationalValidationContextRequest
{
    public IReadOnlyList<Guid> TaskIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> DriverIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> VehicleIds { get; set; } = Array.Empty<Guid>();
}
