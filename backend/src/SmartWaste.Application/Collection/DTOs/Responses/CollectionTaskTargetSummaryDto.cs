namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Target summary details embedded within a CollectionTaskDetailDto.
/// Projects key operational location and attribute metrics for the collection target.
/// </summary>
public class CollectionTaskTargetSummaryDto
{
    public string Identifier { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AddressText { get; set; }
    public int? CapacityLiters { get; set; }
    public IReadOnlyList<string> WasteTypes { get; set; } = Array.Empty<string>();
    public int? LatestFillLevelPercent { get; set; }
}
