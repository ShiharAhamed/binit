using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Reporting.DTOs.Responses;

/// <summary>
/// Full detail response DTO for a specific waste report including verification and attachments.
/// </summary>
public class WasteReportDetailDto
{
    public Guid Id { get; set; }
    public string ReportReference => SmartWaste.Application.Reporting.WasteReportReference.FromId(Id);
    public Guid CitizenId { get; set; }
    public string CitizenName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WasteType WasteType { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AddressText { get; set; }
    public WasteReportStatus Status { get; set; }
    public WasteReportPriority? Priority { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    public string? VerifiedByUserName { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public IReadOnlyList<ReportAttachmentDto> Attachments { get; set; } = Array.Empty<ReportAttachmentDto>();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
