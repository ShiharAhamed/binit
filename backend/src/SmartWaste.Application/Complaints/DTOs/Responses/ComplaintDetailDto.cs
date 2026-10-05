using SmartWaste.Domain.Complaints.Enums;

namespace SmartWaste.Application.Complaints.DTOs.Responses;

/// <summary>
/// Full detail response DTO for a citizen complaint including resolution and optional snapshot location.
/// </summary>
public class ComplaintDetailDto
{
    public Guid Id { get; set; }

    public Guid CitizenId { get; set; }
    public string? CitizenName { get; set; }

    public ComplaintCategory Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }

    public ComplaintStatus Status { get; set; }

    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByUserName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
