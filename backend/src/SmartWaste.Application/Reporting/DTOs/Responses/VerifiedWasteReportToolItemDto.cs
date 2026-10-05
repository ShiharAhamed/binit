using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Reporting.DTOs.Responses;

/// <summary>
/// Strictly read-only, allow-listed DTO tailored for internal AI tools.
/// Conforms to data minimization rules: excludes all citizen identifiers, officer IDs,
/// storage keys, signed URLs, status history, and notes.
/// </summary>
public class VerifiedWasteReportToolItemDto
{
    public Guid Id { get; set; }

    public string ReportReference => SmartWaste.Application.Reporting.WasteReportReference.FromId(Id);

    public string Description { get; set; } = string.Empty;

    public WasteType WasteType { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? AddressText { get; set; }

    public WasteReportStatus Status { get; set; } = WasteReportStatus.Verified;

    public DateTime CreatedAt { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public int AttachmentCount { get; set; }
}
