using SmartWaste.Domain.Operations.Enums;

namespace SmartWaste.Application.Operations.DTOs.Responses;

/// <summary>
/// Full detail response DTO for an operational issue including driver information, resolution, and optional snapshot location.
/// </summary>
public class OperationalIssueDetailDto
{
    public Guid Id { get; set; }

    public Guid DriverId { get; set; }
    public string? DriverName { get; set; }

    public OperationalIssueType IssueType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }

    public OperationalIssueStatus Status { get; set; }

    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByUserName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
