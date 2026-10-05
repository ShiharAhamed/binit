using SmartWaste.Domain.Complaints.Enums;

namespace SmartWaste.Application.Complaints.DTOs.Responses;

/// <summary>
/// Lightweight summary response DTO optimized for paginated complaint list views.
/// </summary>
public class ComplaintSummaryDto
{
    public Guid Id { get; set; }
    public Guid CitizenId { get; set; }
    public string? CitizenName { get; set; }

    public ComplaintCategory Category { get; set; }
    public string Subject { get; set; } = string.Empty;
    public ComplaintStatus Status { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
