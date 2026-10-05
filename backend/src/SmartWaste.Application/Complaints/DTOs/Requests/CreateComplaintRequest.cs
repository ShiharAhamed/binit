using SmartWaste.Domain.Complaints.Enums;

namespace SmartWaste.Application.Complaints.DTOs.Requests;

/// <summary>
/// Request payload for citizen complaint creation (POST /api/v1/complaints).
/// CitizenId and lifecycle/resolution fields are server-controlled and forbidden on client input.
/// </summary>
public class CreateComplaintRequest
{
    public ComplaintCategory? Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
}
