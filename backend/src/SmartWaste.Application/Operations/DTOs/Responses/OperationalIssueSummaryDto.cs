using SmartWaste.Domain.Operations.Enums;

namespace SmartWaste.Application.Operations.DTOs.Responses;

/// <summary>
/// Lightweight summary response DTO optimized for paginated operational issue list views.
/// </summary>
public class OperationalIssueSummaryDto
{
    public Guid Id { get; set; }

    public Guid DriverId { get; set; }
    public string? DriverName { get; set; }

    public OperationalIssueType IssueType { get; set; }
    public string Title { get; set; } = string.Empty;
    public OperationalIssueStatus Status { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
