using SmartWaste.Domain.Operations.Enums;

namespace SmartWaste.Application.Operations.DTOs.Requests;

/// <summary>
/// Request payload for reporting an operational issue from the field (POST /api/v1/operations/issues).
/// DriverId and lifecycle/resolution fields are server-controlled and forbidden on client input.
/// </summary>
public class CreateOperationalIssueRequest
{
    public OperationalIssueType? IssueType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
}
