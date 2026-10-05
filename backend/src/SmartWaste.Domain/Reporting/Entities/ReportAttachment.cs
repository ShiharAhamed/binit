namespace SmartWaste.Domain.Reporting.Entities;

/// <summary>
/// Photographic evidence attached to a waste report.
/// Persists a provider-independent StorageKey referencing cloud object storage.
/// </summary>
public class ReportAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WasteReportId { get; set; }
    public WasteReport? WasteReport { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
