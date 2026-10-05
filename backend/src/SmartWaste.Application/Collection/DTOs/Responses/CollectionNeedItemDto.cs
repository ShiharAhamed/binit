namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Derived, non-persisted operational collection need item (GET /api/v1/collection-needs).
/// Synthesized dynamically from Verified reports (Source A), Full/Blocked bins (Source B), and Routine due bins (Source C).
/// </summary>
public class CollectionNeedItemDto
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string? ReportReference => TargetType == "Report" ? SmartWaste.Application.Reporting.WasteReportReference.FromId(Id) : null;
    public string CollectionReason { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AddressText { get; set; }
    public IReadOnlyList<string> WasteTypes { get; set; } = Array.Empty<string>();
    public string Urgency { get; set; } = string.Empty;
    public DateTime TriggerDate { get; set; }
    public int AttachmentCount { get; set; }
    public CollectionNeedBinDetailsDto? BinDetails { get; set; }
}
