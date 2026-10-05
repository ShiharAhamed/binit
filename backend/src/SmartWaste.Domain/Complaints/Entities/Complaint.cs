using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Domain.Entities;

namespace SmartWaste.Domain.Complaints.Entities;

/// <summary>
/// Represents a service complaint submitted by a citizen.
/// Supports optional snapshot location where the incident occurred.
/// </summary>
public class Complaint
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CitizenId { get; set; }
    public AppUser? Citizen { get; set; }

    public ComplaintCategory Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }

    public ComplaintStatus Status { get; set; } = ComplaintStatus.Submitted;

    public Guid? ResolvedByUserId { get; set; }
    public AppUser? ResolvedByUser { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
