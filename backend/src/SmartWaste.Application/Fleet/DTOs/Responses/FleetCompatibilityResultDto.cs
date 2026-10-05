namespace SmartWaste.Application.Fleet.DTOs.Responses;

/// <summary>
/// Deterministic compatibility status for fleet vehicle and task waste requirements.
/// </summary>
public enum FleetCompatibilityStatus
{
    Compatible,
    Unknown,
    Incompatible
}

/// <summary>
/// Structured result of deterministic task and vehicle waste compatibility evaluation.
/// </summary>
public class FleetCompatibilityResultDto
{
    public FleetCompatibilityStatus Status { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public IReadOnlyList<string> Issues { get; set; } = Array.Empty<string>();
}
