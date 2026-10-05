using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Reporting.DTOs.Responses;

/// <summary>
/// Allow-listed, read-only report data for advisory analysis before human verification.
/// Citizen-entered description is untrusted data, not a workflow instruction.
/// </summary>
public class WasteReportForVerificationToolItemDto
{
    public Guid Id { get; set; }

    public string ReportReference => SmartWaste.Application.Reporting.WasteReportReference.FromId(Id);

    public string Description { get; set; } = string.Empty;

    public WasteType WasteType { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? AddressText { get; set; }

    public WasteReportStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public int AttachmentCount { get; set; }
}
