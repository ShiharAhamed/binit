using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Operations.Enums;

namespace SmartWaste.Domain.Operations.Entities;

/// <summary>
/// Represents an operational or field issue reported by a driver during collection.
/// Supports optional snapshot location where the incident occurred.
/// </summary>
public class OperationalIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DriverId { get; set; }
    public AppUser? Driver { get; set; }

    public OperationalIssueType IssueType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }

    public OperationalIssueStatus Status { get; set; } = OperationalIssueStatus.Reported;

    public Guid? ResolvedByUserId { get; set; }
    public AppUser? ResolvedByUser { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
