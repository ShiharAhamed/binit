namespace SmartWaste.Application.Complaints.DTOs.Requests;

/// <summary>
/// Request payload for resolving a citizen complaint.
/// Resolution note provides mandatory staff context explaining the resolution outcome.
/// </summary>
public class ResolveComplaintRequest
{
    public string ResolutionNote { get; set; } = string.Empty;
}
