using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Reporting.DTOs.Responses;

/// <summary>
/// Status transition audit record response DTO for report history timeline.
/// </summary>
public class WasteReportStatusHistoryDto
{
    public Guid Id { get; set; }
    public Guid WasteReportId { get; set; }
    public WasteReportStatus? FromStatus { get; set; }
    public WasteReportStatus ToStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }
}
