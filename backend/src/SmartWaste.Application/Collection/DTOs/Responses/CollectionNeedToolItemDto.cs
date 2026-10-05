using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Sanitized, read-only collection-need projection for the internal AI service.
/// Deliberately excludes PII, descriptions, attachments, notes, task details, and audit data.
/// </summary>
public class CollectionNeedToolItemDto
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public Guid? WasteReportId { get; set; }
    public Guid? WasteBinId { get; set; }
    public string? ReportReference => WasteReportId.HasValue ? SmartWaste.Application.Reporting.WasteReportReference.FromId(WasteReportId.Value) : null;
    public string? BinCode => BinTelemetry?.BinCode;
    public string CollectionReason { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AddressText { get; set; }
    public IReadOnlyList<string> WasteTypes { get; set; } = Array.Empty<string>();
    public string Urgency { get; set; } = string.Empty;
    public DateTime TriggerDate { get; set; }
    public CollectionNeedBinTelemetryDto? BinTelemetry { get; set; }
}

/// <summary>
/// Essential bin telemetry for collection planning. Null for report-targeted needs.
/// </summary>
public class CollectionNeedBinTelemetryDto
{
    public string BinCode { get; set; } = string.Empty;
    public int CapacityLiters { get; set; }
    public int? LatestFillLevelPercent { get; set; }
    public BinCondition? LatestCondition { get; set; }
    public double? ObservationAgeHours { get; set; }
}
