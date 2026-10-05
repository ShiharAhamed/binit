using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Domain.Reporting.Entities;

/// <summary>
/// Represents an illegal dumping or overflow report submitted by a citizen.
/// </summary>
public class WasteReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CitizenId { get; set; }
    public AppUser? Citizen { get; set; }

    public string Description { get; set; } = string.Empty;

    public WasteType WasteType { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? AddressText { get; set; }

    public WasteReportStatus Status { get; set; } = WasteReportStatus.Submitted;

    public WasteReportPriority? Priority { get; set; }

    public Guid? VerifiedByUserId { get; set; }
    public AppUser? VerifiedByUser { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<ReportAttachment> Attachments { get; set; } = new List<ReportAttachment>();
    public ICollection<WasteReportStatusHistory> StatusHistory { get; set; } = new List<WasteReportStatusHistory>();
}
