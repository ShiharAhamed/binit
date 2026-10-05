using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Domain.Reporting.Entities;

/// <summary>
/// Chronological audit record tracking state transitions of a waste report.
/// </summary>
public class WasteReportStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WasteReportId { get; set; }
    public WasteReport? WasteReport { get; set; }

    public WasteReportStatus? FromStatus { get; set; }

    public WasteReportStatus ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public string? Notes { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
