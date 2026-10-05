namespace SmartWaste.Application.Fleet.DTOs.Requests;

/// <summary>
/// Request to check deterministic compatibility between collection tasks and a candidate vehicle.
/// </summary>
public class CheckFleetCompatibilityRequest
{
    public IReadOnlyList<Guid> TaskIds { get; set; } = Array.Empty<Guid>();
    public Guid VehicleId { get; set; }
}
