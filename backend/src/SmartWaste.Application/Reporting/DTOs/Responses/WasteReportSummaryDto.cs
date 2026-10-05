using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Reporting.DTOs.Responses;

/// <summary>
/// Compact summary response DTO optimized for paginated report list views.
/// </summary>
public class WasteReportSummaryDto
{
    public Guid Id { get; set; }
    public string ReportReference => SmartWaste.Application.Reporting.WasteReportReference.FromId(Id);
    public string Description { get; set; } = string.Empty;
    public WasteType WasteType { get; set; }
    public WasteReportStatus Status { get; set; }
    public WasteReportPriority? Priority { get; set; }
    public string? AddressText { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Guid? CitizenId { get; set; }
    public string? CitizenName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
